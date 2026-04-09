using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    public class Gravity : ComponentBehavior, IUpdateable
    {
        public float Probability = 1f;
        public float Force = 10f;

        private Random m_random;
        private ComponentCreatureModel m_creatureModel;
        private SubsystemTime m_subsystemTime;

        // Referencias a los tres comportamientos de persecución del atacante
        private ComponentChaseBehavior m_chaseBehavior;
        private ComponentNewChaseBehavior m_newChaseBehavior;
        private ComponentZombieChaseBehavior m_zombieChaseBehavior;

        private StateMachine m_stateMachine;
        private double m_lastHitTime;
        private bool m_hasHit;
        private float m_originalMaxSpeed;
        private ComponentBody m_currentVictimBody;

        public override float ImportanceLevel => 0f;
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);

            Probability = valuesDictionary.GetValue<float>("Probability", 1f);
            Force = valuesDictionary.GetValue<float>("Force", 10f);

            m_random = new Random();
            m_creatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
            m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);

            m_chaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
            m_newChaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();
            m_zombieChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>();

            m_stateMachine = new StateMachine();
            m_stateMachine.AddState("Idle", null, null, null);
            m_stateMachine.AddState("Hit", null, null, null);
            m_stateMachine.TransitionTo("Idle");
        }

        public void Update(float dt)
        {
            m_stateMachine.Update();

            // Restaurar la velocidad máxima original de la víctima después del impulso
            if (m_currentVictimBody != null && m_subsystemTime.GameTime - m_lastHitTime > 0.2)
            {
                m_currentVictimBody.MaxSpeed = m_originalMaxSpeed;
                m_currentVictimBody = null;
            }

            // Detectar el momento exacto del golpe
            if (m_creatureModel != null && m_creatureModel.IsAttackHitMoment)
            {
                ComponentCreature victim = GetCurrentTarget();
                if (victim != null && victim.ComponentBody != null)
                {
                    if (m_random.Float(0f, 1f) <= Probability && m_subsystemTime.GameTime - m_lastHitTime > 0.2)
                    {
                        // Detener persecuciones del atacante (para que no siga atacando mientras la víctima vuela)
                        StopAttackBehaviors();

                        // Detener persecuciones de la víctima (por si los tuviera)
                        StopVictimChaseBehaviors(victim);

                        // Guardar y aumentar temporalmente la MaxSpeed de la víctima
                        m_currentVictimBody = victim.ComponentBody;
                        m_originalMaxSpeed = m_currentVictimBody.MaxSpeed;
                        m_currentVictimBody.MaxSpeed = 1000f; // Valor alto para permitir cualquier fuerza

                        // Dirección desde el atacante hacia la víctima
                        Vector3 direction = victim.ComponentBody.Position - Entity.FindComponent<ComponentBody>(true).Position;
                        direction.Y = Math.Max(direction.Y, 0.5f);
                        if (direction.LengthSquared() > 0.001f)
                            direction = Vector3.Normalize(direction);

                        // Aplicar impulso (la fuerza puede ser cualquier número, sin límite práctico)
                        victim.ComponentBody.ApplyImpulse(direction * Force);

                        m_lastHitTime = m_subsystemTime.GameTime;
                        m_stateMachine.TransitionTo("Hit");
                        m_hasHit = true;
                    }
                }
            }

            if (m_stateMachine.CurrentState == "Hit" && m_hasHit)
            {
                if (m_subsystemTime.GameTime - m_lastHitTime > 0.1)
                {
                    m_stateMachine.TransitionTo("Idle");
                    m_hasHit = false;
                }
            }
        }

        private ComponentCreature GetCurrentTarget()
        {
            if (m_zombieChaseBehavior != null && m_zombieChaseBehavior.Target != null)
                return m_zombieChaseBehavior.Target;
            if (m_newChaseBehavior != null && m_newChaseBehavior.Target != null)
                return m_newChaseBehavior.Target;
            if (m_chaseBehavior != null && m_chaseBehavior.Target != null)
                return m_chaseBehavior.Target;
            return null;
        }

        private void StopAttackBehaviors()
        {
            if (m_chaseBehavior != null && m_chaseBehavior.IsActive)
                m_chaseBehavior.StopAttack();
            if (m_newChaseBehavior != null && m_newChaseBehavior.IsActive)
                m_newChaseBehavior.StopAttack();
            if (m_zombieChaseBehavior != null && m_zombieChaseBehavior.IsActive)
                m_zombieChaseBehavior.StopAttack();
        }

        private void StopVictimChaseBehaviors(ComponentCreature victim)
        {
            var chase = victim.Entity.FindComponent<ComponentChaseBehavior>();
            if (chase != null && chase.IsActive)
                chase.StopAttack();
            var newChase = victim.Entity.FindComponent<ComponentNewChaseBehavior>();
            if (newChase != null && newChase.IsActive)
                newChase.StopAttack();
            var zombieChase = victim.Entity.FindComponent<ComponentZombieChaseBehavior>();
            if (zombieChase != null && zombieChase.IsActive)
                zombieChase.StopAttack();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
