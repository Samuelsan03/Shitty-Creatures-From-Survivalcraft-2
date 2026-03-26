using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentRageZombieBehavior : ComponentBehavior, IUpdateable
	{
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentLocomotion m_componentLocomotion;
		private ComponentZombieChaseBehavior m_componentZombieChase;
		private bool m_rageActivated;

		public override float ImportanceLevel => 0f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>();
			m_componentLocomotion = Entity.FindComponent<ComponentLocomotion>();
			m_componentZombieChase = Entity.FindComponent<ComponentZombieChaseBehavior>();
		}

		public void Update(float dt)
		{
			if (m_rageActivated)
				return;

			if (m_componentCreature?.ComponentHealth == null)
				return;

			if (m_componentCreature.ComponentHealth.Health <= 0.3f)
			{
				if (m_componentMiner != null)
					m_componentMiner.AttackPower *= 2.5f;

				if (m_componentLocomotion != null)
				{
					m_componentLocomotion.WalkSpeed *= 2f;
					m_componentLocomotion.LadderSpeed *= 2f;
					m_componentLocomotion.SwimSpeed *= 2f;
				}

				m_rageActivated = true;
			}
		}
	}
}
