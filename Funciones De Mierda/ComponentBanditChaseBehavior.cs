using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBanditChaseBehavior : ComponentChaseBehavior, IUpdateable
	{
		public new UpdateOrder UpdateOrder => UpdateOrder.Default;

		private ComponentHerdBehavior m_componentHerd;
		private ModLoader m_registeredLoader;

		// Método para verificar si esta manada es de bandidos
		private bool IsBanditHerd()
		{
			return m_componentHerd != null &&
				   !string.IsNullOrEmpty(m_componentHerd.HerdName) &&
				   string.Equals(m_componentHerd.HerdName, "bandit", StringComparison.OrdinalIgnoreCase);
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Llamar al Load base primero
			base.Load(valuesDictionary, idToEntityMap);

			// Buscar el componente de manada
			m_componentHerd = Entity.FindComponent<ComponentHerdBehavior>();

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

			// Sobrescribir el evento Injured para considerar la manada
			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.RemoveAll(componentHealth.Injured, componentHealth.Injured);
			componentHealth.Injured = new Action<Injury>(delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;

				// Verificar si el atacante es de la misma manada
				if (attacker != null && m_componentHerd != null)
				{
					ComponentHerdBehavior attackerHerd = attacker.Entity.FindComponent<ComponentHerdBehavior>();
					if (attackerHerd != null && !string.IsNullOrEmpty(attackerHerd.HerdName) &&
						string.Equals(attackerHerd.HerdName, m_componentHerd.HerdName, StringComparison.OrdinalIgnoreCase))
					{
						// No hacer nada si el atacante es de la misma manada
						return;
					}
				}

				// Comportamiento normal para atacantes de otras manadas
				if (m_random.Float(0f, 1f) < m_chaseWhenAttackedProbability)
				{
					bool flag = false;
					float num;
					float num2;
					if (m_chaseWhenAttackedProbability >= 1f)
					{
						num = 30f;
						num2 = 60f;
						flag = true;
					}
					else
					{
						num = 7f;
						num2 = 7f;
					}
					num = ChaseRangeOnAttacked.GetValueOrDefault(num);
					num2 = ChaseTimeOnAttacked.GetValueOrDefault(num2);
					flag = ChasePersistentOnAttacked.GetValueOrDefault(flag);
					Attack(attacker, num, num2, flag);
				}
			});

			// Sobrescribir el evento CollidedWithBody para considerar la manada
			ComponentBody componentBody = m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.RemoveAll(componentBody.CollidedWithBody, componentBody.CollidedWithBody);
			componentBody.CollidedWithBody = new Action<ComponentBody>(delegate (ComponentBody body)
			{
				if (m_target == null && m_autoChaseSuppressionTime <= 0f && m_random.Float(0f, 1f) < m_chaseOnTouchProbability)
				{
					ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						// Verificar si es de la misma manada
						if (m_componentHerd != null)
						{
							ComponentHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
							if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
								string.Equals(targetHerd.HerdName, m_componentHerd.HerdName, StringComparison.OrdinalIgnoreCase))
							{
								return; // No atacar a miembros de la misma manada
							}
						}

						bool flag = m_subsystemPlayers.IsPlayer(body.Entity);
						bool flag2 = (componentCreature.Category & m_autoChaseMask) > (CreatureCategory)0;
						if ((AttacksPlayer && flag && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
							(AttacksNonPlayerCreature && !flag && flag2))
						{
							Attack(componentCreature, ChaseRangeOnTouch, ChaseTimeOnTouch, false);
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

		// Sobrescribir el método ScoreTarget para manejar la lógica de manada
		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			float score = base.ScoreTarget(componentCreature);

			// Verificar si es de la misma manada (comparación insensible a mayúsculas/minúsculas)
			if (m_componentHerd != null && componentCreature != null && score > 0f)
			{
				ComponentHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
					string.Equals(targetHerd.HerdName, m_componentHerd.HerdName, StringComparison.OrdinalIgnoreCase))
				{
					return 0f; // No atacar miembros de la misma manada
				}
			}

			// Llamar al hook del loader si está registrado
			if (m_registeredLoader != null)
			{
				m_registeredLoader.ChaseBehaviorScoreTarget(this, componentCreature, ref score);
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
			if (m_componentHerd != null)
			{
				ComponentHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
					string.Equals(targetHerd.HerdName, m_componentHerd.HerdName, StringComparison.OrdinalIgnoreCase))
				{
					return; // No atacar a miembros de la misma manada
				}
			}

			// Comportamiento normal
			base.Attack(componentCreature, maxRange, maxChaseTime, isPersistent);
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
					if (m_componentHerd != null)
					{
						ComponentHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
						if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
							string.Equals(targetHerd.HerdName, m_componentHerd.HerdName, StringComparison.OrdinalIgnoreCase))
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
			return result;
		}
	}
}
