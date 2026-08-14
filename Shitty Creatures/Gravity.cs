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

		private const float m_hitDistance = 1.75f;

		private Random m_random;
		private ComponentMiner m_miner;
		private ComponentBody m_componentBody;
		private SubsystemTime m_subsystemTime;

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
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_miner = Entity.FindComponent<ComponentMiner>();
			m_componentBody = Entity.FindComponent<ComponentBody>(true);

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

			if (m_currentVictimBody != null && m_subsystemTime.GameTime - m_lastHitTime > 0.2)
			{
				m_currentVictimBody.MaxSpeed = m_originalMaxSpeed;
				m_currentVictimBody = null;
			}

			if (m_miner == null)
				return;

			ComponentCreature victim = GetCurrentTarget();
			if (victim != null && victim.ComponentBody != null)
			{
				if (victim.ComponentHealth != null && victim.ComponentHealth.Health <= 0f)
					return;

				Vector3 attackerCenter = m_componentBody.BoundingBox.Center();
				Vector3 victimCenter = victim.ComponentBody.BoundingBox.Center();
				Vector3 dirToVictim = Vector3.Normalize(victimCenter - attackerCenter);
				float distance = Vector3.Distance(attackerCenter, victimCenter);

				// Validar distancia privada de 1.75f
				if (distance <= m_hitDistance)
				{
					// Validar que esté en la mirada (mismo cálculo que ComponentChaseBehavior.IsTargetInAttackRange)
					float dot = Vector3.Dot(dirToVictim, m_componentBody.Matrix.Forward);

					if (dot > 0.25f)
					{
						if (m_random.Float(0f, 1f) <= Probability && m_subsystemTime.GameTime - m_lastHitTime > m_miner.HitInterval)
						{
							Vector3 direction = dirToVictim;
							direction.Y = Math.Max(direction.Y, 0.5f);
							if (direction.LengthSquared() > 0.001f)
								direction = Vector3.Normalize(direction);

							Vector3 hitPoint = victim.ComponentBody.Position;

							StopAttackBehaviors();
							StopVictimChaseBehaviors(victim);

							m_currentVictimBody = victim.ComponentBody;
							m_originalMaxSpeed = m_currentVictimBody.MaxSpeed;
							m_currentVictimBody.MaxSpeed = 1e9f;

							// Usar SOLAMENTE Miner.Hit para saber de verdad que es un golpe real
							m_miner.Hit(victim.ComponentBody, hitPoint, direction);

							victim.ComponentBody.ApplyImpulse(direction * Force);

							m_lastHitTime = m_subsystemTime.GameTime;
							m_stateMachine.TransitionTo("Hit");
							m_hasHit = true;
						}
					}
				}
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
