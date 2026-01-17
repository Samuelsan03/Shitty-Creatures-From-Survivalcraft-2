using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBanditHerdBehavior : ComponentHerdBehavior, IUpdateable
	{
		public new UpdateOrder UpdateOrder => UpdateOrder.Default;

		private ComponentChaseBehavior m_componentChase;
		private ModLoader m_registeredLoader;

		public new void Update(float dt)
		{
			base.Update(dt);

			// Si hay un objetivo y es de la misma manada, detener ataque
			if (m_componentChase != null && m_componentChase.Target != null)
			{
				ComponentHerdBehavior targetHerd = m_componentChase.Target.Entity.FindComponent<ComponentHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
					string.Equals(targetHerd.HerdName, HerdName, StringComparison.OrdinalIgnoreCase))
				{
					m_componentChase.StopAttack();
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Buscar componentes necesarios
			m_componentChase = Entity.FindComponent<ComponentChaseBehavior>(true);

			// Registrar el loader para hooks de mods
			m_registeredLoader = null;
			ModsManager.HookAction("ChaseBehaviorScoreTarget", delegate (ModLoader loader)
			{
				m_registeredLoader = loader;
				return false;
			});

			// Configurar para atacar todas las categorías
			if (m_componentChase != null)
			{
				m_componentChase.m_autoChaseMask = CreatureCategory.LandPredator | CreatureCategory.LandOther |
												   CreatureCategory.WaterPredator | CreatureCategory.WaterOther |
												   CreatureCategory.Bird;

				// Aumentar rangos de ataque para bandidos
				m_componentChase.m_dayChaseRange = Math.Max(m_componentChase.m_dayChaseRange, 20f);
				m_componentChase.m_nightChaseRange = Math.Max(m_componentChase.m_nightChaseRange, 25f);

				// Aumentar tiempo de persecución
				m_componentChase.m_dayChaseTime = Math.Max(m_componentChase.m_dayChaseTime, 60f);
				m_componentChase.m_nightChaseTime = Math.Max(m_componentChase.m_nightChaseTime, 90f);

				// Bandidos atacan a todos (incluidos jugadores)
				m_componentChase.AttacksPlayer = true;
				m_componentChase.AttacksNonPlayerCreature = true;

				// Aumentar probabilidad de atacar cuando es atacado
				m_componentChase.m_chaseWhenAttackedProbability = 1f;

				// Hacer ataques persistentes
				m_componentChase.ImportanceLevelPersistent = 300f;
			}

			// Sobrescribir evento Injured para considerar la manada
			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.RemoveAll(componentHealth.Injured, componentHealth.Injured);
			componentHealth.Injured = new Action<Injury>(delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;

				if (attacker != null)
				{
					ComponentHerdBehavior attackerHerd = attacker.Entity.FindComponent<ComponentHerdBehavior>();
					if (attackerHerd != null && !string.IsNullOrEmpty(attackerHerd.HerdName) &&
						string.Equals(attackerHerd.HerdName, HerdName, StringComparison.OrdinalIgnoreCase))
					{
						return; // No hacer nada si el atacante es de la misma manada
					}
				}

				// Llamar a ayuda si el atacante es de otra manada
				CallNearbyCreaturesHelp(attacker, 20f, 30f, false);
			});

			// Sobrescribir evento CollidedWithBody para considerar la manada
			ComponentBody componentBody = m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.RemoveAll(componentBody.CollidedWithBody, componentBody.CollidedWithBody);
			componentBody.CollidedWithBody = new Action<ComponentBody>(delegate (ComponentBody body)
			{
				if (m_componentChase != null && m_componentChase.m_target == null &&
					m_componentChase.m_autoChaseSuppressionTime <= 0f &&
					m_componentChase.m_random.Float(0f, 1f) < m_componentChase.m_chaseOnTouchProbability)
				{
					ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						// Verificar si es de la misma manada
						ComponentHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
						if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
							string.Equals(targetHerd.HerdName, HerdName, StringComparison.OrdinalIgnoreCase))
						{
							return; // No atacar a miembros de la misma manada
						}

						bool flag = m_componentChase.m_subsystemPlayers.IsPlayer(body.Entity);
						bool flag2 = (componentCreature.Category & m_componentChase.m_autoChaseMask) > (CreatureCategory)0;
						if ((m_componentChase.AttacksPlayer && flag &&
							 m_componentChase.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
							(m_componentChase.AttacksNonPlayerCreature && !flag && flag2))
						{
							m_componentChase.Attack(componentCreature,
								m_componentChase.ChaseRangeOnTouch,
								m_componentChase.ChaseTimeOnTouch, false);
						}
					}
				}
			});
		}

		// Método para modificar el score desde los hooks
		public void HandleScoreTarget(ComponentChaseBehavior chaseBehavior, ComponentCreature target, ref float score)
		{
			if (chaseBehavior == m_componentChase && target != null)
			{
				ComponentHerdBehavior targetHerd = target.Entity.FindComponent<ComponentHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
					string.Equals(targetHerd.HerdName, HerdName, StringComparison.OrdinalIgnoreCase))
				{
					// No atacar miembros de la misma manada
					score = 0f;
				}
			}
		}

		public new void CallNearbyCreaturesHelp(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (target == null) return;

			Vector3 position = target.ComponentBody.Position;
			foreach (ComponentCreature componentCreature in m_subsystemCreatureSpawn.Creatures)
			{
				if (Vector3.DistanceSquared(position, componentCreature.ComponentBody.Position) < maxRange * maxRange)
				{
					ComponentHerdBehavior componentHerdBehavior = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
					if (componentHerdBehavior != null && !string.IsNullOrEmpty(componentHerdBehavior.HerdName) &&
						string.Equals(componentHerdBehavior.HerdName, HerdName, StringComparison.OrdinalIgnoreCase) && m_autoNearbyCreaturesHelp)
					{
						ComponentChaseBehavior componentChaseBehavior = componentCreature.Entity.FindComponent<ComponentChaseBehavior>();
						if (componentChaseBehavior != null && componentChaseBehavior.Target == null)
						{
							ComponentHerdBehavior targetHerd = target.Entity.FindComponent<ComponentHerdBehavior>();
							if (targetHerd == null ||
								!string.Equals(targetHerd.HerdName, HerdName, StringComparison.OrdinalIgnoreCase))
							{
								componentChaseBehavior.Attack(target, maxRange, maxChaseTime, isPersistent);
							}
						}
					}
				}
			}
		}
	}
}
