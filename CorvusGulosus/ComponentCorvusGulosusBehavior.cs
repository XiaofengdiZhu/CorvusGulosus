using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game {
    public class ComponentCorvusGulosusBehavior : ComponentBehavior, IUpdateable {
        public SubsystemTime m_subsystemTime;
        public SubsystemPickables m_subsystemPickables;
        public SubsystemTerrain m_subsystemTerrain;

        public ComponentCreature m_componentCreature;
        public ComponentPathfinding m_componentPathfinding;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;
        public override float ImportanceLevel => m_importanceLevel;

        public StateMachine m_stateMachine = new StateMachine();
        public Random m_random = new Random();
        public float m_importanceLevel;
        public double m_nextFindPickableTime;
        public Point3? m_eatable;
        public double m_eatTime;
        public float m_blockedTime;
        public int m_blockedCount;
        public int m_ateCount;

        public static readonly HashSet<int> m_eatableHashSet = new HashSet<int> {
            GrassBlock.Index,
            CottonBlock.Index,
            PurpleFlowerBlock.Index,
            RedFlowerBlock.Index,
            WhiteFlowerBlock.Index,
            RyeBlock.Index,
            SaplingBlock.Index,
            TallGrassBlock.Index,
            BirchLeavesBlock.Index,
            MimosaLeavesBlock.Index,
            OakLeavesBlock.Index,
            SpruceLeavesBlock.Index,
            TallSpruceLeavesBlock.Index,
            CactusBlock.Index,
            PumpkinBlock.Index,
            RottenPumpkinBlock.Index,
            JackOLanternBlock.Index,
            GrassTrapBlock.Index,
            ChristmasTreeBlock.Index
        };

        public void Update(float dt) {
            m_stateMachine.Update();
        }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap) {
            m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
            m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
            m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
            m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
            m_stateMachine.AddState(
                "Inactive",
                delegate {
                    m_importanceLevel = 0f;
                    m_eatable = null;
                },
                delegate {
                    if (m_eatable == null) {
                        if (m_subsystemTime.GameTime > m_nextFindPickableTime) {
                            m_nextFindPickableTime = m_subsystemTime.GameTime + m_random.Float(8f, 12f);
                            m_eatable = FindEatable(m_componentCreature.ComponentBody.Position);
                            if (m_eatable == null) {
                                m_blockedCount = 0;
                            }
                        }
                    }
                    else {
                        m_importanceLevel = float.MaxValue;
                    }
                    if (IsActive) {
                        m_stateMachine.TransitionTo("Move");
                        m_blockedCount = 0;
                    }
                },
                null
            );
            m_stateMachine.AddState(
                "Move",
                delegate {
                    if (m_eatable != null) {
                        float speed = m_random.Float(0.5f, 0.7f);
                        int maxPathfindingPositions = 1000;
                        float num2 = Vector3.Distance(m_componentCreature.ComponentCreatureModel.EyePosition, m_componentCreature.ComponentBody.Position);
                        m_componentPathfinding.SetDestination(
                            new Vector3(m_eatable.Value) + new Vector3(0.5f),
                            speed,
                            1f + num2,
                            maxPathfindingPositions,
                            true,
                            false,
                            true,
                            null
                        );
                        if (m_random.Float(0f, 1f) < 0.66f) {
                            m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                        }
                    }
                },
                delegate {
                    if (!IsActive) {
                        m_stateMachine.TransitionTo("Inactive");
                    }
                    else if (m_eatable == null) {
                        m_importanceLevel = 0f;
                    }
                    else if (m_componentPathfinding.IsStuck) {
                        m_importanceLevel = 0f;
                    }
                    else if (!m_componentPathfinding.Destination.HasValue) {
                        m_stateMachine.TransitionTo("Eat");
                    }
                    else if (!m_eatableHashSet.Contains(Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(m_eatable.Value.X, m_eatable.Value.Y, m_eatable.Value.Z)))) {
                        m_stateMachine.TransitionTo("PickableMoved");
                    }
                    if (m_random.Float(0f, 1f) < 0.1f * m_subsystemTime.GameTimeDelta) {
                        m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                    }
                    if (m_eatable != null) {
                        m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3(m_eatable.Value) + new Vector3(0.5f);
                    }
                    else {
                        m_componentCreature.ComponentCreatureModel.LookRandomOrder = true;
                    }
                },
                null
            );
            m_stateMachine.AddState(
                "PickableMoved",
                null,
                delegate {
                    if (m_eatable != null) {
                        m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3(m_eatable.Value) + new Vector3(0.5f);
                    }
                    if (m_subsystemTime.PeriodicGameTimeEvent(0.25, GetHashCode() % 100 * 0.01)) {
                        m_stateMachine.TransitionTo("Move");
                    }
                },
                null
            );
            m_stateMachine.AddState(
                "Eat",
                delegate {
                    m_eatTime = m_random.Float(0.7f, 1.5f);
                    m_blockedTime = 0f;
                },
                delegate {
                    if (!IsActive) {
                        m_stateMachine.TransitionTo("Inactive");
                    }
                    if (m_eatable == null) {
                        m_importanceLevel = 0f;
                    }
                    if (m_eatable != null) {
                        if (Vector3.DistanceSquared(new Vector3(m_componentCreature.ComponentCreatureModel.EyePosition.X, m_componentCreature.ComponentBody.Position.Y, m_componentCreature.ComponentCreatureModel.EyePosition.Z), new Vector3(m_eatable.Value) + new Vector3(0.5f)) < 1f) {
                            m_eatTime -= m_subsystemTime.GameTimeDelta;
                            m_blockedTime = 0f;
                            int nowBlock = m_subsystemTerrain.Terrain.GetCellContentsFast(m_eatable.Value.X, m_eatable.Value.Y, m_eatable.Value.Z);
                            if (m_eatableHashSet.Contains(nowBlock)) {
                                if (m_eatTime <= 0.0) {
                                    for (int j = -1; j <= 1; j++) {
                                        for (int k = -1; k <= 1; k++) {
                                            for (int l = -1; l <= 1; l++) {
                                                int x = m_eatable.Value.X + j;
                                                int y = m_eatable.Value.Y + k;
                                                int z = m_eatable.Value.Z + l;
                                                int nowBlock2 = m_subsystemTerrain.Terrain.GetCellContents(x, y, z);
                                                if (m_eatableHashSet.Contains(nowBlock2)) {
                                                    m_subsystemTerrain.DestroyCell(
                                                        int.MaxValue,
                                                        x,
                                                        y,
                                                        z,
                                                        nowBlock2 == GrassBlock.Index ? DirtBlock.Index : AirBlock.Index,
                                                        true,
                                                        true
                                                    );
                                                    m_ateCount++;
                                                }
                                            }
                                        }
                                    }
                                    if (m_ateCount > 16) {
                                        m_ateCount = 0;
                                        foreach (Point3 face in CellFace.m_faceToPoint3) {
                                            int x = m_eatable.Value.X + face.X;
                                            int y = m_eatable.Value.Y + face.Y;
                                            int z = m_eatable.Value.Z + face.Z;
                                            if (m_subsystemTerrain.Terrain.GetCellContents(x, y, z) == 0) {
                                                Entity entity = DatabaseManager.CreateEntity(Project, "Raven", true);
                                                entity.FindComponent<ComponentBody>(true).Position = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                                                entity.FindComponent<ComponentBody>(true).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, m_random.Float(0f, 6.2831855f));
                                                entity.FindComponent<ComponentSpawn>(true).SpawnDuration = 0.25f;
                                                Project.AddEntity(entity);
                                                break;
                                            }
                                        }
                                    }
                                    m_importanceLevel = 0f;
                                }
                            }
                            else {
                                m_importanceLevel = 0f;
                            }
                        }
                        else {
                            float num = Vector3.Distance(m_componentCreature.ComponentCreatureModel.EyePosition, m_componentCreature.ComponentBody.Position);
                            m_componentPathfinding.SetDestination(
                                new Vector3(m_eatable.Value) + new Vector3(0.5f),
                                0.3f,
                                0.5f + num,
                                0,
                                false,
                                true,
                                false,
                                null
                            );
                            m_blockedTime += m_subsystemTime.GameTimeDelta;
                        }
                        if (m_blockedTime > 8f) {
                            m_blockedCount++;
                            if (m_blockedCount >= 3) {
                                m_importanceLevel = 0f;
                            }
                            else {
                                m_stateMachine.TransitionTo("Move");
                            }
                        }
                    }
                    m_componentCreature.ComponentCreatureModel.FeedOrder = true;
                    if (m_random.Float(0f, 1f) < 0.1f * m_subsystemTime.GameTimeDelta) {
                        m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                    }
                    if (m_random.Float(0f, 1f) < 1.5f * m_subsystemTime.GameTimeDelta) {
                        m_componentCreature.ComponentCreatureSounds.PlayFootstepSound(2f);
                    }
                },
                null
            );
            m_stateMachine.TransitionTo("Inactive");
        }

        public virtual Point3? FindEatable(Vector3 position) {
            Point3 positionInt = new Point3((int)Math.Floor(position.X), (int)Math.Floor(position.Y), (int)Math.Floor(position.Z));
            Point3[] randomFaces = CellFace.m_faceToPoint3.OrderBy(_ => m_random.Int()).ToArray();
            int i1 = 0;
            for (; i1 < 3; i1++) {
                int x = positionInt.X + randomFaces[i1].X;
                int y = positionInt.Y + randomFaces[i1].Y;
                int z = positionInt.Z + randomFaces[i1].Z;
                if (m_eatableHashSet.Contains(m_subsystemTerrain.Terrain.GetCellContents(x, y, z))) {
                    return new Point3(x, y, z);
                }
            }
            for (int i2 = 0; i2 < 6; i2++) {
                int x = positionInt.X + m_random.Int(-5, 5);
                int z = positionInt.Z + m_random.Int(-5, 5);
                int y = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
                if (m_eatableHashSet.Contains(m_subsystemTerrain.Terrain.GetCellContents(x, y, z))) {
                    return new Point3(x, y, z);
                }
            }
            for (; i1 < 6; i1++) {
                int x = positionInt.X + randomFaces[i1].X;
                int y = positionInt.Y + randomFaces[i1].Y;
                int z = positionInt.Z + randomFaces[i1].Z;
                if (m_eatableHashSet.Contains(m_subsystemTerrain.Terrain.GetCellContents(x, y, z))) {
                    return new Point3(x, y, z);
                }
            }
            for (int i3 = 0; i3 < 6; i3++) {
                int x = positionInt.X + m_random.Int(6, 32) * m_random.Sign();
                int z = positionInt.Z + m_random.Int(6, 32) * m_random.Sign();
                int y = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
                if (m_eatableHashSet.Contains(m_subsystemTerrain.Terrain.GetCellContents(x, y, z))) {
                    return new Point3(x, y, z);
                }
            }
            return null;
        }
    }
}