using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentRageZombieBehavior : ComponentBehavior, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override float ImportanceLevel => 0f;

		private ComponentHealth m_componentHealth;
		private ComponentLocomotion m_componentLocomotion;
		private ComponentMiner m_componentMiner;
		private ComponentNewGlowingEyes m_glowingEyes;
		private ComponentZombieChaseBehavior m_zombieChaseBehavior;

		private float m_originalWalkSpeed;
		private float m_originalAccelerationFactor;
		private float m_originalAttackPower;
		private Color m_originalGlowColor;

		private bool m_isRaging = false;
		private StateMachine m_stateMachine = new StateMachine();

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentLocomotion = Entity.FindComponent<ComponentLocomotion>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_glowingEyes = Entity.FindComponent<ComponentNewGlowingEyes>();
			m_zombieChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>();

			if (m_componentLocomotion != null)
			{
				m_originalWalkSpeed = m_componentLocomotion.WalkSpeed;
				m_originalAccelerationFactor = m_componentLocomotion.AccelerationFactor;
			}

			if (m_componentMiner != null)
			{
				m_originalAttackPower = m_componentMiner.AttackPower;
			}

			if (m_glowingEyes != null)
			{
				m_originalGlowColor = m_glowingEyes.GlowingEyesColor;
			}
		}

		public void Update(float dt)
		{
			if (m_componentHealth == null || m_componentLocomotion == null)
				return;

			float health = m_componentHealth.Health;
			bool shouldRage = health < 0.3f && health > 0f;

			if (shouldRage && !m_isRaging)
			{
				m_isRaging = true;

				if (m_componentLocomotion != null)
				{
					m_componentLocomotion.WalkSpeed = m_originalWalkSpeed * 1.55f;
					m_componentLocomotion.AccelerationFactor = m_originalAccelerationFactor * 1.55f;
				}

				if (m_componentMiner != null)
				{
					m_componentMiner.AttackPower = m_originalAttackPower * 2.0f;
				}

				if (m_glowingEyes != null)
				{
					m_glowingEyes.GlowingEyesColor = new Color(0, 255, 0);
				}
			}
			else if (!shouldRage && m_isRaging)
			{
				m_isRaging = false;

				if (m_componentLocomotion != null)
				{
					m_componentLocomotion.WalkSpeed = m_originalWalkSpeed;
					m_componentLocomotion.AccelerationFactor = m_originalAccelerationFactor;
				}

				if (m_componentMiner != null)
				{
					m_componentMiner.AttackPower = m_originalAttackPower;
				}

				if (m_glowingEyes != null)
				{
					m_glowingEyes.GlowingEyesColor = m_originalGlowColor;
				}
			}
		}
	}
}
