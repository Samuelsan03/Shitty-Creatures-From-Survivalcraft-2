using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentRageZombieBehavior : Component, IUpdateable
	{
		public float RageHealthThreshold { get; set; } = 0.3f;
		public float AttackPowerMultiplier { get; set; } = 2f;
		public float SpeedMultiplier { get; set; } = 1.5f;
		public bool IsEnraged { get; private set; }

		private ComponentHealth m_health;
		private ComponentLocomotion m_locomotion;
		private ComponentMiner m_miner;
		private ComponentCreatureModel m_creatureModel;

		private float m_originalWalkSpeed;
		private float m_originalAttackPower;
		private float m_originalInjuryColor;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_health = Entity.FindComponent<ComponentHealth>(true);
			m_locomotion = Entity.FindComponent<ComponentLocomotion>(true);
			m_miner = Entity.FindComponent<ComponentMiner>(true);
			m_creatureModel = Entity.FindComponent<ComponentCreatureModel>(true);

			if (m_locomotion != null)
				m_originalWalkSpeed = m_locomotion.WalkSpeed;

			if (m_miner != null)
				m_originalAttackPower = m_miner.AttackPower;

			if (m_creatureModel != null)
				m_originalInjuryColor = m_creatureModel.m_injuryColorFactor;
		}

		public void Update(float dt)
		{
			if (m_health == null) return;

			float healthFraction = m_health.Health;

			if (healthFraction <= RageHealthThreshold && !IsEnraged)
			{
				EnterRage();
			}
			else if (healthFraction > RageHealthThreshold && IsEnraged)
			{
				ExitRage();
			}
		}

		private void EnterRage()
		{
			IsEnraged = true;

			if (m_locomotion != null)
				m_locomotion.WalkSpeed = m_originalWalkSpeed * SpeedMultiplier;

			if (m_miner != null)
				m_miner.AttackPower = m_originalAttackPower * AttackPowerMultiplier;

			if (m_creatureModel != null)
				m_creatureModel.m_injuryColorFactor = 1f;
		}

		private void ExitRage()
		{
			IsEnraged = false;

			if (m_locomotion != null)
				m_locomotion.WalkSpeed = m_originalWalkSpeed;

			if (m_miner != null)
				m_miner.AttackPower = m_originalAttackPower;

			if (m_creatureModel != null)
				m_creatureModel.m_injuryColorFactor = m_originalInjuryColor;
		}
	}
}
