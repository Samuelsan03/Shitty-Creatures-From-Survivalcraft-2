using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBanditChaseBehavior : ComponentChaseBehavior, IUpdateable
	{
		public new UpdateOrder UpdateOrder => UpdateOrder.Default;

		private ComponentBanditHerdBehavior m_componentBanditHerd;
		private ModLoader m_registeredLoader;
		private bool m_isNearDeath;
		private float m_attackPersistanceFactor = 1f; // Factor para hacer los ataques más persistentes

		// Método para verificar si esta manada es de bandidos
		private bool IsBanditHerd()
		{
			return m_componentBanditHerd != null &&
				   !string.IsNullOrEmpty(m_componentBanditHerd.HerdName) &&
				   string.Equals(m_componentBanditHerd.HerdName, "bandit", StringComparison.OrdinalIgnoreCase);
		}

		// Método para verificar si el NPC está cerca de la muerte
		private bool IsNearDeath()
		{
			if (m_componentCreature?.ComponentHealth == null) return false;
			return m_componentCreature.ComponentHealth.Health <= 0.2f; // 20% o menos de vida
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Llamar al Load base primero
			base.Load(valuesDictionary, idToEntityMap);

			// Buscar el componente de manada de bandidos
			m_componentBanditHerd = Entity.FindComponent<ComponentBanditHerdBehavior>();

			// Configurar para atacar todas las categorías
			m_autoChaseMask = CreatureCategory.LandPredator | CreatureCategory.LandOther |
							  CreatureCategory.WaterPredator | CreatureCategory.WaterOther |
							  CreatureCategory.Bird;

			// Bandidos siempre atacan jugadores
			AttacksPlayer = true;
			AttacksNonPlayerCreature = true;

			// Aumentar rango de ataque para bandidos con armas
			m_dayChaseRange = Math.Max(m_dayChaseRange, 20f);
			m_nightChaseRange = Math.Max(m_nightChaseRange, 25f);

			// Aumentar tiempo de persecución
			m_dayChaseTime = Math.Max(m_dayChaseTime, 60f);
			m_nightChaseTime = Math.Max(m_nightChaseTime, 90f);

			// Aumentar probabilidad de atacar cuando es atacado
			m_chaseWhenAttackedProbability = 1f;

			// Hacer que los ataques sean persistentes por defecto
			ImportanceLevelPersistent = 300f;

			// Sobrescribir el evento Injured para considerar la manada y estado de salud
			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.RemoveAll(componentHealth.Injured, componentHealth.Injured);
			componentHealth.Injured = new Action<Injury>(delegate (Injury injury)
			{
				// Actualizar estado de salud
				m_isNearDeath = IsNearDeath();

				// Cuando están cerca de la muerte, aumentar la persistencia del ataque
				if (m_isNearDeath)
				{
					m_attackPersistanceFactor = 2f; // Doble de persistencia
				}

				ComponentCreature attacker = injury.Attacker;

				// Verificar si el atacante es de la misma manada
				if (attacker != null && m_componentBanditHerd != null)
				{
					ComponentBanditHerdBehavior attackerHerd = attacker.Entity.FindComponent<ComponentBanditHerdBehavior>();
					if (attackerHerd != null && !string.IsNullOrEmpty(attackerHerd.HerdName) &&
						string.Equals(attackerHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
					{
						// No hacer nada si el atacante es de la misma manada
						return;
					}
				}

				// Comportamiento normal para atacantes de otras manadas
				// AUN CUANDO ESTÉ CERCA DE LA MUERTE, LOS BANDITOS SIGUEN ATACANDO
				if (m_random.Float(0f, 1f) < m_chaseWhenAttackedProbability)
				{
					bool flag = false;
					float num;
					float num2;
					if (m_chaseWhenAttackedProbability >= 1f)
					{
						num = 30f;
						num2 = 60f * m_attackPersistanceFactor; // Aumentar tiempo de persecución
						flag = true;
					}
					else
					{
						num = 7f;
						num2 = 7f * m_attackPersistanceFactor;
					}
					num = ChaseRangeOnAttacked.GetValueOrDefault(num);
					num2 = ChaseTimeOnAttacked.GetValueOrDefault(num2);
					flag = ChasePersistentOnAttacked.GetValueOrDefault(flag);
					Attack(attacker, num, num2, flag);
				}
			});

			// Sobrescribir el evento CollidedWithBody para considerar la manada y salud
			ComponentBody componentBody = m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.RemoveAll(componentBody.CollidedWithBody, componentBody.CollidedWithBody);
			componentBody.CollidedWithBody = new Action<ComponentBody>(delegate (ComponentBody body)
			{
				// Actualizar estado de salud
				m_isNearDeath = IsNearDeath();

				// Cuando están cerca de la muerte, aumentar la persistencia del ataque
				if (m_isNearDeath)
				{
					m_attackPersistanceFactor = 2f;
				}

				if (m_target == null && m_autoChaseSuppressionTime <= 0f && m_random.Float(0f, 1f) < m_chaseOnTouchProbability)
				{
					ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						// Verificar si es de la misma manada
						if (m_componentBanditHerd != null)
						{
							ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
							if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
								string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
							{
								return; // No atacar a miembros de la misma manada
							}
						}

						bool flag = m_subsystemPlayers.IsPlayer(body.Entity);
						bool flag2 = (componentCreature.Category & m_autoChaseMask) > (CreatureCategory)0;
						if ((AttacksPlayer && flag && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
							(AttacksNonPlayerCreature && !flag && flag2))
						{
							// LOS BANDITOS SIGUEN ATACANDO AUNQUE ESTÉN CERCA DE LA MUERTE
							float chaseTime = ChaseTimeOnTouch * m_attackPersistanceFactor;
							Attack(componentCreature, ChaseRangeOnTouch, chaseTime, false);
						}
					}
				}
				if (m_target != null && JumpWhenTargetStanding && body == m_target.ComponentBody &&
					body.StandingOnBody == m_componentCreature.ComponentBody)
				{
					m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
				}
			});

			// Añadir hook personalizado para el cálculo de score
			m_registeredLoader = null;
			ModsManager.HookAction("ChaseBehaviorScoreTarget", delegate (ModLoader loader)
			{
				m_registeredLoader = loader;
				return false;
			});
		}

		public new void Update(float dt)
		{
			// Actualizar estado de salud en cada frame
			bool wasNearDeath = m_isNearDeath;
			m_isNearDeath = IsNearDeath();

			// Si acabamos de entrar en estado de near-death, aumentar la persistencia
			if (m_isNearDeath && !wasNearDeath)
			{
				m_attackPersistanceFactor = 2f;
			}

			// Si recuperamos salud, restaurar factor normal
			if (!m_isNearDeath && wasNearDeath)
			{
				m_attackPersistanceFactor = 1f;
			}

			// Llamar al Update base para mantener el comportamiento de persecución
			base.Update(dt);

			// LOS BANDITOS CONTINÚAN SU COMPORTAMIENTO DE ATAQUE AUNQUE ESTÉN CERCA DE LA MUERTE
			// No hay lógica de huida o detención cuando están a punto de morir

			// Si estamos cerca de la muerte y tenemos objetivo, asegurarnos de mantener el ataque
			if (m_isNearDeath && m_target != null)
			{
				// Forzar que la importancia se mantenga alta
				if (m_importanceLevel < ImportanceLevelPersistent * m_attackPersistanceFactor)
				{
					m_importanceLevel = ImportanceLevelPersistent * m_attackPersistanceFactor;
				}
			}
		}

		// Sobrescribir el método ScoreTarget para manejar la lógica de manada
		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			float score = base.ScoreTarget(componentCreature);

			// Verificar si es de la misma manada (comparación insensible a mayúsculas/minúsculas)
			if (m_componentBanditHerd != null && componentCreature != null && score > 0f)
			{
				ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
					string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
				{
					return 0f; // No atacar miembros de la misma manada
				}
			}

			// Llamar al hook del loader si está registrado
			if (m_registeredLoader != null)
			{
				m_registeredLoader.ChaseBehaviorScoreTarget(this, componentCreature, ref score);
			}

			// Si estamos cerca de la muerte, aumentar ligeramente el score para mantener la agresividad
			if (m_isNearDeath && score > 0f)
			{
				score *= 1.5f; // 50% más de score cuando están cerca de la muerte
			}

			return score;
		}

		public override void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (Suppressed || componentCreature == null)
			{
				return;
			}

			// Verificar si el objetivo es de la misma manada (comparación insensible)
			if (m_componentBanditHerd != null)
			{
				ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
					string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
				{
					return; // No atacar a miembros de la misma manada
				}
			}

			// Ajustar tiempos de persecución si estamos cerca de la muerte
			float adjustedChaseTime = maxChaseTime;
			bool adjustedPersistent = isPersistent;

			if (m_isNearDeath)
			{
				adjustedChaseTime *= m_attackPersistanceFactor; // Aumentar tiempo de persecución
				adjustedPersistent = true; // Forzar persistencia

				// Aumentar también el rango de ataque cuando están cerca de la muerte (último esfuerzo)
				maxRange *= 1.2f;
			}

			// LOS BANDITOS SIGUEN ATACANDO AUNQUE ESTÉN CERCA DE LA MUERTE
			// Comportamiento normal con ajustes
			base.Attack(componentCreature, maxRange, adjustedChaseTime, adjustedPersistent);

			// Asegurarse de que el ataque sea persistente incluso cuando estén cerca de la muerte
			if (m_isNearDeath)
			{
				// Aumentar la importancia del ataque
				m_importanceLevel = Math.Max(m_importanceLevel, ImportanceLevelPersistent * m_attackPersistanceFactor);
			}
		}

		public override ComponentCreature FindTarget()
		{
			Vector3 position = m_componentCreature.ComponentBody.Position;
			ComponentCreature result = null;
			float num = 0f;
			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), m_range, m_componentBodies);
			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature componentCreature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (componentCreature != null)
				{
					// Verificar si es de la misma manada (comparación insensible)
					if (m_componentBanditHerd != null)
					{
						ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
						if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
							string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
						{
							continue; // Saltar miembros de la misma manada
						}
					}

					float num2 = ScoreTarget(componentCreature);
					if (num2 > num)
					{
						num = num2;
						result = componentCreature;
					}
				}
			}

			// LOS BANDITOS SIGUEN BUSCANDO OBJETIVOS AUNQUE ESTÉN CERCA DE LA MUERTE
			// Si estamos cerca de la muerte y no encontramos objetivo, buscar en un rango más amplio
			if (m_isNearDeath && result == null && m_range < 40f)
			{
				float extendedRange = 40f;
				m_componentBodies.Clear();
				m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), extendedRange, m_componentBodies);
				for (int i = 0; i < m_componentBodies.Count; i++)
				{
					ComponentCreature componentCreature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						// Verificar si es de la misma manada (comparación insensible)
						if (m_componentBanditHerd != null)
						{
							ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
							if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
								string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
							{
								continue; // Saltar miembros de la misma manada
							}
						}

						float num2 = ScoreTarget(componentCreature);
						if (num2 > num)
						{
							num = num2;
							result = componentCreature;
						}
					}
				}
			}

			return result;
		}

		// Método para detener el ataque (sobrescribir para evitar que se detenga cuando están cerca de la muerte)
		public new void StopAttack()
		{
			// Solo detener el ataque si no estamos cerca de la muerte
			if (!m_isNearDeath)
			{
				base.StopAttack();
			}
			// Si estamos cerca de la muerte, ignorar la llamada para detener el ataque
			// Esto hace que los bandidos sigan atacando hasta el final
		}
	}
}
