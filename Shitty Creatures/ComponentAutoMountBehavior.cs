using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    public class ComponentAutoMountBehavior : ComponentBehavior, IUpdateable
    {
        public UpdateOrder UpdateOrder => UpdateOrder.Default;
        public override float ImportanceLevel => m_importanceLevel;

        public float SearchRange = 20f;

        private static readonly string[] s_mountableTemplates = new string[]
        {
            "InfectedBearTamed", "FlyingInfectedBossTamed", "InfectedFlyTamed1",
            "Horse_Bay_Saddled", "Donkey_Saddled", "Horse_Chestnut_Saddled",
            "Camel_Saddled", "Horse_Black_Saddled", "Horse_Palomino_Saddled",
            "Horse_White_Saddled"
        };

        private SubsystemTime m_subsystemTime;
        private SubsystemBodies m_subsystemBodies;
        private SubsystemTerrain m_subsystemTerrain;
        private SubsystemCampfireBlockBehavior m_subsystemCampfireBlockBehavior;
        private ComponentCreature m_componentCreature;
        private ComponentPathfinding m_componentPathfinding; // pathfinding del jinete
        private ComponentRider m_componentRider;
        private StateMachine m_stateMachine = new StateMachine();
        private Random m_random = new Random();
        private float m_importanceLevel;
        private ComponentMount m_targetMount;

        private Vector3? m_wanderDestination;
        private double m_nextWanderUpdateTime;

        private ComponentNewChaseBehavior m_chaseBehavior;
        private ComponentSummonBehavior m_summonBehavior;

        private double m_summonStoppedTime = -1.0; // para imitar el comportamiento de FollowTarget

        private DynamicArray<ComponentBody> m_tempBodies = new DynamicArray<ComponentBody>();

        public virtual void Update(float dt)
        {
            m_stateMachine.Update();
        }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
            m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
            m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_subsystemCampfireBlockBehavior = Project.FindSubsystem<SubsystemCampfireBlockBehavior>(true);
            m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
            m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
            m_componentRider = Entity.FindComponent<ComponentNewRider>() as ComponentRider
                               ?? Entity.FindComponent<ComponentRider>();

            m_chaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();
            m_summonBehavior = Entity.FindComponent<ComponentSummonBehavior>();

            SearchRange = valuesDictionary.GetValue<float>("SearchRange", 20f);

            // ----- Estados -----
            // Idle
            m_stateMachine.AddState("Idle", delegate {
                m_importanceLevel = 0f;
                m_targetMount = null;
            }, delegate {
                if (m_componentRider.Mount != null) {
                    m_stateMachine.TransitionTo("Wander");
                    return;
                }
                ComponentMount mount = FindMountableCreature();
                if (mount != null) {
                    m_targetMount = mount;
                    m_importanceLevel = 300f;
                    m_stateMachine.TransitionTo("Approach");
                } else m_importanceLevel = 0f;
            }, null);

            // Approach
            m_stateMachine.AddState("Approach", delegate {
                m_componentPathfinding.SetDestination(
                    m_targetMount.ComponentBody.Position, 1f, 1.5f, 100, false, false, true, null);
            }, delegate {
                if (m_targetMount == null || m_targetMount.ComponentBody == null ||
                    m_targetMount.Entity.FindComponent<ComponentHealth>()?.Health <= 0f ||
                    m_targetMount.Rider != null) {
                    m_stateMachine.TransitionTo("Idle");
                    return;
                }
                float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position,
                    m_targetMount.ComponentBody.Position);
                if (dist < 2.5f) m_stateMachine.TransitionTo("Mounting");
                else if (m_componentPathfinding.IsStuck && dist > 8f) {
                    m_targetMount = null;
                    m_stateMachine.TransitionTo("Idle");
                }
            }, () => m_componentPathfinding.Stop());

            // Mounting
            m_stateMachine.AddState("Mounting", delegate {
                m_componentRider.StartMounting(m_targetMount);
            }, delegate {
                if (m_componentRider.Mount == m_targetMount) {
                    m_stateMachine.TransitionTo("Wander");
                    return;
                }
                if (m_targetMount == null || m_targetMount.ComponentBody == null ||
                    m_targetMount.Entity.FindComponent<ComponentHealth>()?.Health <= 0f ||
                    m_targetMount.Rider != null) {
                    m_targetMount = null;
                    m_stateMachine.TransitionTo("Idle");
                }
            }, null);

            // Wander (maneja persecución, llamado y evitación de fuego)
            m_stateMachine.AddState("Wander", delegate {
                m_importanceLevel = 10f;
                m_wanderDestination = null;
                m_nextWanderUpdateTime = 0.0;
                m_summonStoppedTime = -1.0;
            }, delegate {
                if (m_componentRider.Mount == null) {
                    m_stateMachine.TransitionTo("Idle");
                    return;
                }

                ComponentMount mount = m_componentRider.Mount;
                Entity mountEntity = mount.Entity;
                ComponentPathfinding mountPathfinding = mountEntity.FindComponent<ComponentPathfinding>();
                if (mountPathfinding == null) return;

                // ----- Detección de tareas urgentes -----
                Vector3? urgentTarget = null;
                float urgentImportance = 10f;
                float speed = 1f;
                float range = 1.5f;

                // 1. Persecución: mover montura hacia el target y detener pathfinding del jinete
                if (m_chaseBehavior != null && m_chaseBehavior.IsActive && m_chaseBehavior.Target != null) {
                    urgentTarget = m_chaseBehavior.Target.ComponentBody.Position;
                    urgentImportance = 250f;
                    // Detener pathfinding del jinete para que no interfiera
                    m_componentPathfinding.Stop();
                }
                // 2. Llamada (silbato): imitar FollowTarget original
                else if (m_summonBehavior != null && m_summonBehavior.SummonTarget != null) {
                    Vector3 targetPos = m_summonBehavior.SummonTarget.Position;
                    float distToTarget = Vector3.Distance(mount.ComponentBody.Position, targetPos);
                    if (distToTarget > 4f) {
                        // Calcular punto lateral (igual que ComponentSummonBehavior)
                        Vector3 cross = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, targetPos - mount.ComponentBody.Position));
                        float side = 0.75f * (float)((GetHashCode() % 2 != 0) ? 1 : -1) * (float)(1 + GetHashCode() % 3);
                        Vector3 lateralDest = targetPos + cross * side;
                        speed = MathUtils.Lerp(0.4f, 1f, MathUtils.Saturate(0.25f * (distToTarget - 5f)));
                        range = 3.75f;
                        urgentTarget = lateralDest;
                        urgentImportance = 250f;
                        m_summonStoppedTime = -1.0; // reiniciar contador
                        // Detener pathfinding del jinete
                        m_componentPathfinding.Stop();
                    } else {
                        // Cerca del jugador: detenerse
                        mountPathfinding.Stop();
                        m_summonStoppedTime = m_subsystemTime.GameTime;
                        m_importanceLevel = 0f; // para que pueda desactivarse
                        return;
                    }
                }
                // 3. Evitar fuego
                else {
                    Vector3 mountPos = mount.ComponentBody.Position;
                    float closestFireDist = 12f;
                    Vector3? closestFire = null;
                    if (m_subsystemCampfireBlockBehavior != null) {
                        foreach (Point3 point in m_subsystemCampfireBlockBehavior.Campfires) {
                            Vector3 firePos = new Vector3(point.X + 0.5f, point.Y + 0.5f, point.Z + 0.5f);
                            float dist = Vector3.Distance(mountPos, firePos);
                            if (dist < closestFireDist) {
                                closestFireDist = dist;
                                closestFire = firePos;
                            }
                        }
                    }
                    if (closestFire.HasValue) {
                        Vector3 awayFromFire = mountPos - closestFire.Value;
                        awayFromFire.Y = 0f;
                        if (awayFromFire.LengthSquared() > 0.01f) {
                            awayFromFire = Vector3.Normalize(awayFromFire);
                            urgentTarget = mountPos + awayFromFire * 10f;
                            urgentImportance = 200f;
                        }
                    }
                }

                if (urgentTarget.HasValue) {
                    m_importanceLevel = urgentImportance;
                    mountPathfinding.SetDestination(urgentTarget.Value, speed, range, 100, false, true, false, null);
                    m_wanderDestination = null;
                    return;
                }

                // Si no hay tareas urgentes, restaurar importancia baja
                m_importanceLevel = 10f;

                // ----- Wander normal -----
                if (m_subsystemTime.GameTime >= m_nextWanderUpdateTime) {
                    m_wanderDestination = FindWanderDestination();
                    m_nextWanderUpdateTime = m_subsystemTime.GameTime + m_random.Float(8f, 20f);
                }
                if (m_wanderDestination.HasValue && mountPathfinding.Destination == null) {
                    mountPathfinding.SetDestination(m_wanderDestination.Value,
                        m_random.Float(0.25f, 0.4f), 3f, 0, false, true, false, null);
                }
                if (mountPathfinding.IsStuck) {
                    m_wanderDestination = null;
                    m_nextWanderUpdateTime = m_subsystemTime.GameTime + 2f;
                }
            }, () => {
                if (m_componentRider.Mount != null) {
                    ComponentPathfinding mp = m_componentRider.Mount.Entity.FindComponent<ComponentPathfinding>();
                    if (mp != null) mp.Stop();
                }
            });

            m_stateMachine.TransitionTo("Idle");
        }

        private Vector3 FindWanderDestination()
        {
            Vector3 currentPos = m_componentRider.Mount != null
                ? m_componentRider.Mount.ComponentBody.Position
                : m_componentCreature.ComponentBody.Position;
            float bestScore = float.MinValue;
            Vector3 bestDest = currentPos;
            for (int i = 0; i < 8; i++) {
                Vector2 offset = m_random.Vector2(6f, 18f);
                Vector3 candidate = new Vector3(currentPos.X + offset.X, 0f, currentPos.Z + offset.Y);
                int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(Terrain.ToCell(candidate.X), Terrain.ToCell(candidate.Z));
                candidate.Y = topHeight + 1;
                float score = ScoreWanderDestination(candidate, currentPos);
                if (score > bestScore) { bestScore = score; bestDest = candidate; }
            }
            return bestDest;
        }

        private float ScoreWanderDestination(Vector3 dest, Vector3 currentPos)
        {
            float score = MathF.Max(0f, 10f - MathF.Abs(dest.Y - currentPos.Y));
            int cx = Terrain.ToCell(dest.X), cz = Terrain.ToCell(dest.Z);
            int topY = m_subsystemTerrain.Terrain.GetTopHeight(cx, cz);
            if (m_subsystemTerrain.Terrain.GetCellContents(cx, topY, cz) == 18) score -= 20f;
            return score;
        }

        private ComponentMount FindMountableCreature()
        {
            Vector3 pos = m_componentCreature.ComponentBody.Position;
            m_tempBodies.Clear();
            m_subsystemBodies.FindBodiesAroundPoint(new Vector2(pos.X, pos.Z), SearchRange, m_tempBodies);
            foreach (ComponentBody body in m_tempBodies) {
                Entity entity = body.Entity;
                ComponentMount mount = entity.FindComponent<ComponentNewMount>() as ComponentMount
                                       ?? entity.FindComponent<ComponentMount>();
                if (mount == null) continue;
                if (mount.Rider != null) continue;
                ComponentHealth health = entity.FindComponent<ComponentHealth>();
                if (health != null && health.Health <= 0f) continue;
                string name = entity.ValuesDictionary.DatabaseObject.Name;
                foreach (string t in s_mountableTemplates) if (name == t) return mount;
            }
            return null;
        }
    }
}
