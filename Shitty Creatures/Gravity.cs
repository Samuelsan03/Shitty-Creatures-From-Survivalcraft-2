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
		public float MaximumDistance = 1.75f;

		private Random m_random;
		private ComponentBody m_componentBody;
		private ComponentMiner m_componentMiner;
		private SubsystemTime m_subsystemTime;
		private SubsystemBodies m_subsystemBodies;

		private ComponentChaseBehavior m_chaseBehavior;
		private ComponentNewChaseBehavior m_newChaseBehavior;
		private ComponentZombieChaseBehavior m_zombieChaseBehavior;

		private StateMachine m_stateMachine;
		private double m_lastHitTime;
		private bool m_hasHit;

		public override float ImportanceLevel => 0f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			Probability = valuesDictionary.GetValue<float>("Probability", 1f);
			Force = valuesDictionary.GetValue<float>("Force", 10f);

			m_random = new Random();
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);

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

			// Intentamos obtener el objetivo actual de las persecuciones
			ComponentCreature targetCreature = GetCurrentTarget();
			if (targetCreature == null || targetCreature.ComponentBody == null)
				return;

			// Verificamos que no esté muerto
			if (targetCreature.ComponentHealth != null && targetCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentBody targetBody = targetCreature.ComponentBody;

			// Verificamos la distancia real usando el radio de impacto del miner
			Vector3 direction = targetBody.Position - m_componentBody.Position;
			float distanceSquared = direction.LengthSquared();

			if (distanceSquared > MaximumDistance * MaximumDistance)
				return;

			// Verificamos cooldown de tiempo
			if (m_subsystemTime.GameTime - m_lastHitTime <= m_componentMiner.HitInterval)
				return;

			// Verificamos probabilidad
			if (m_random.Float(0f, 1f) > Probability)
				return;

			// Aseguramos que la dirección no sea nula
			if (distanceSquared > 0.001f)
			{
				direction = Vector3.Normalize(direction);
				direction.Y = Math.Max(direction.Y, 0.5f);

				// Aplicamos el golpe REAL usando el sistema nativo del juego
				Vector3 hitPoint = m_componentBody.Position + direction * (float)Math.Sqrt(distanceSquared);
				m_componentMiner.Hit(targetBody, hitPoint, direction);

				// Aplicamos el empuje
				targetBody.ApplyImpulse(direction * Force);

				// Detenemos persecuciones
				StopAttackBehaviors();
				StopVictimChaseBehaviors(targetCreature);

				// Actualizamos estado y tiempos
				m_lastHitTime = m_subsystemTime.GameTime;
				m_stateMachine.TransitionTo("Hit");
				m_hasHit = true;
			}

			if (m_stateMachine.CurrentState == "Hit" && m_hasHit)
			{
				if (m_subsystemTime.GameTime - m_lastHitTime > 0)
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
