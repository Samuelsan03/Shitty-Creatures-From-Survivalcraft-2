using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBanditChaseBehavior : ComponentChaseBehavior, IUpdateable
	{
		public bool IsDrugTraffickerMode { get; set; }
		public bool AttackAllCreatures { get; set; }

		private ComponentBanditHerdBehavior m_banditHerd;
		private ComponentCreature m_componentCreature;
		private SubsystemSky m_subsystemSky;
		private SubsystemBanditInvasion m_subsystemBanditInvasion;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			IsDrugTraffickerMode = valuesDictionary.GetValue<bool>("IsDrugTraffickerMode", false);
			AttackAllCreatures = valuesDictionary.GetValue<bool>("AttackAllCreatures", false);

			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_banditHerd = Entity.FindComponent<ComponentBanditHerdBehavior>();
			m_subsystemBanditInvasion = Project.FindSubsystem<SubsystemBanditInvasion>(true);

			// Sincronizar modo narcotraficante con el estado global de invasión
			if (m_subsystemBanditInvasion != null)
			{
				bool globalInvasionActive = m_subsystemBanditInvasion.IsInvasionActive;
				if (IsDrugTraffickerMode != globalInvasionActive)
				{
					IsDrugTraffickerMode = globalInvasionActive;
					if (!globalInvasionActive)
						StopAttack();
				}
			}

			if (m_banditHerd != null)
				m_banditHerd.HerdName = "bandits";
		}

		public override float ScoreTarget(ComponentCreature target)
		{
			if (target == null || target == m_componentCreature)
				return 0f;

			if (target.ComponentHealth.Health <= 0f)
				return 0f;

			bool isPlayer = target.Entity.FindComponent<ComponentPlayer>() != null;
			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);
			float currentRange = (m_subsystemSky.SkyLightIntensity < 0.2f) ? m_nightChaseRange : m_dayChaseRange;

			if (distance >= currentRange)
				return 0f;

			// Verificar estado global de invasión como respaldo
			bool invasionActive = (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive);
			bool drugMode = IsDrugTraffickerMode || invasionActive;

			// Modo narcotraficante: perseguir jugador obsesivamente
			if (drugMode && isPlayer)
				return (currentRange - distance) * 1000f;

			// Modo atacar a TODAS las criaturas (LandPredator, LandOther, WaterPredator, WaterOther, Bird)
			if (AttackAllCreatures && !isPlayer)
			{
				CreatureCategory targetCategory = target.Category;
				if (targetCategory == CreatureCategory.LandPredator ||
					targetCategory == CreatureCategory.LandOther ||
					targetCategory == CreatureCategory.WaterPredator ||
					targetCategory == CreatureCategory.WaterOther ||
					targetCategory == CreatureCategory.Bird)
				{
					return currentRange - distance;
				}
			}

			// Jugador normalmente no es atacado (a menos que esté en modo drogas o sea provocado por la clase base)
			if (isPlayer)
				return 0f;

			return base.ScoreTarget(target);
		}
	}
}
