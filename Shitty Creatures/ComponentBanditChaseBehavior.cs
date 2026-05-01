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
		private ComponentBanditRunAwayBehavior m_componentBanditRunAway;
		private ModLoader m_registeredLoader;
		private bool m_isNearDeath;
		private float m_attackPersistanceFactor = 1f;
		private ComponentCreature m_lastAttacker;
		private double m_lastAttackTime;
		private float m_switchTargetCooldown = 0f;
		private ComponentMiner m_componentMiner;
		private float m_lastShotSoundTime;
		private System.Reflection.FieldInfo m_minerHasDigOrderField;

		// Duración de la memoria del último atacante (30 segundos)
		private const float AttackerMemoryDuration = 30f;

		// Umbral de salud para considerar "cerca de la muerte" (20%)
		private const float NearDeathThreshold = 0.2f;

		private bool IsNearDeath()
		{
			if (m_componentCreature?.ComponentHealth == null) return false;
			return m_componentCreature.ComponentHealth.Health <= NearDeathThreshold;
		}

		// Método para desactivar el comportamiento de huida cuando está cerca de la muerte
		private void DisableRunAwayBehavior()
		{
			if (m_componentBanditRunAway != null)
			{
				// Acceder al campo m_importanceLevel mediante reflexión ya que es privado
				var field = typeof(ComponentRunAwayBehavior).GetField("m_importanceLevel",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (field != null)
				{
					field.SetValue(m_componentBanditRunAway, 0f);
				}

				// También podemos anular cualquier delegado de Injured que cause huida
				if (m_componentCreature?.ComponentHealth != null)
				{
					// No eliminamos completamente, pero aseguramos que nuestra lógica prevalezca
					// El ComponentBanditRunAwayBehavior ya debería tener su propio Injured anulado
				}
			}
		}

		// Método para verificar y mantener desactivado el comportamiento de huida
		private void EnsureRunAwayIsDisabled()
		{
			if (m_isNearDeath && m_componentBanditRunAway != null)
			{
				var field = typeof(ComponentRunAwayBehavior).GetField("m_importanceLevel",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (field != null)
				{
					float currentImportance = (float)field.GetValue(m_componentBanditRunAway);
					if (currentImportance > 0f)
					{
						field.SetValue(m_componentBanditRunAway, 0f);
					}
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Obtener referencias a los componentes necesarios
			m_componentBanditHerd = Entity.FindComponent<ComponentBanditHerdBehavior>();
			m_componentBanditRunAway = Entity.FindComponent<ComponentBanditRunAwayBehavior>();
			m_componentMiner = Entity.FindComponent<ComponentMiner>();

			// Configurar máscara de persecución
			m_autoChaseMask = CreatureCategory.LandPredator | CreatureCategory.LandOther |
							  CreatureCategory.WaterPredator | CreatureCategory.WaterOther |
							  CreatureCategory.Bird;

			// Configurar comportamiento de ataque
			AttacksPlayer = true;
			AttacksNonPlayerCreature = true;

			// Ajustar rangos y tiempos de persecución
			m_dayChaseRange = Math.Max(m_dayChaseRange, 20f);
			m_nightChaseRange = Math.Max(m_nightChaseRange, 25f);
			m_dayChaseTime = Math.Max(m_dayChaseTime, 60f);
			m_nightChaseTime = Math.Max(m_nightChaseTime, 90f);
			m_chaseWhenAttackedProbability = 1f;
			ImportanceLevelPersistent = 300f;

			// Inicializar variables
			m_lastAttacker = null;
			m_lastAttackTime = 0f;
			m_switchTargetCooldown = 0f;
			m_lastShotSoundTime = 0f;

			// Guardar manejadores originales
			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			Action<Injury> originalInjured = componentHealth.Injured;

			ComponentBody componentBody = m_componentCreature.ComponentBody;
			Action<ComponentBody> originalCollided = componentBody.CollidedWithBody;

			// NUEVO MANEJADOR DE INJURED - CON INTEGRACIÓN DE BANDITRUNAWAY
			componentHealth.Injured = delegate (Injury injury)
			{
				// Ejecutar manejador original
				originalInjured?.Invoke(injury);

				// Actualizar estado de salud
				bool wasNearDeath = m_isNearDeath;
				m_isNearDeath = IsNearDeath();

				// Si está cerca de la muerte, aumentar persistencia de ataque Y DESACTIVAR HUIDA
				if (m_isNearDeath && !wasNearDeath)
				{
					m_attackPersistanceFactor = 2f;
					DisableRunAwayBehavior(); // ¡DESACTIVAR COMPORTAMIENTO DE HUIDA!
				}
				else if (!m_isNearDeath && wasNearDeath)
				{
					m_attackPersistanceFactor = 1f;
					// Cuando ya no está cerca de la muerte, el comportamiento de huida
					// se maneja normalmente por ComponentBanditRunAwayBehavior (que ya está configurado para NO huir)
				}

				ComponentCreature attacker = injury.Attacker;

				// Ignorar ataques de miembros de la misma manada
				if (attacker != null && m_componentBanditHerd != null)
				{
					ComponentBanditHerdBehavior attackerHerd = attacker.Entity.FindComponent<ComponentBanditHerdBehavior>();
					if (attackerHerd != null && !string.IsNullOrEmpty(attackerHerd.HerdName) &&
						string.Equals(attackerHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
					{
						return; // Ignorar completamente
					}
				}

				// Recordar al atacante
				if (attacker != null)
				{
					m_lastAttacker = attacker;
					m_lastAttackTime = m_subsystemTime.GameTime;
				}

				// Si no hay objetivo actual y hay atacante, contraatacar
				if (m_target == null && attacker != null)
				{
					bool isPersistent = false;
					float range, chaseTime;

					if (m_chaseWhenAttackedProbability >= 1f)
					{
						range = 30f;
						chaseTime = 60f * m_attackPersistanceFactor;
						isPersistent = true;
					}
					else
					{
						range = 7f;
						chaseTime = 7f * m_attackPersistanceFactor;
					}

					range = ChaseRangeOnAttacked.GetValueOrDefault(range);
					chaseTime = ChaseTimeOnAttacked.GetValueOrDefault(chaseTime);
					isPersistent = ChasePersistentOnAttacked.GetValueOrDefault(isPersistent);

					Attack(attacker, range, chaseTime, isPersistent);
				}

				// Verificar que la huida sigue desactivada si está near death
				EnsureRunAwayIsDisabled();
			};

			// NUEVO MANEJADOR DE COLLISION - CON INTEGRACIÓN DE BANDITRUNAWAY
			componentBody.CollidedWithBody = delegate (ComponentBody body)
			{
				// Ejecutar manejador original
				originalCollided?.Invoke(body);

				// Actualizar estado de salud
				bool wasNearDeath = m_isNearDeath;
				m_isNearDeath = IsNearDeath();

				// Si está cerca de la muerte, aumentar persistencia de ataque Y DESACTIVAR HUIDA
				if (m_isNearDeath && !wasNearDeath)
				{
					m_attackPersistanceFactor = 2f;
					DisableRunAwayBehavior(); // ¡DESACTIVAR COMPORTAMIENTO DE HUIDA!
				}
				else if (!m_isNearDeath && wasNearDeath)
				{
					m_attackPersistanceFactor = 1f;
				}

				// Solo procesar si no hay objetivo y no está suprimido
				if (m_target == null && m_autoChaseSuppressionTime <= 0f &&
					m_random.Float(0f, 1f) < m_chaseOnTouchProbability)
				{
					ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
					if (creature != null)
					{
						// Excluir miembros de la misma manada
						if (m_componentBanditHerd != null)
						{
							ComponentBanditHerdBehavior targetHerd = creature.Entity.FindComponent<ComponentBanditHerdBehavior>();
							if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
								string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
							{
								return;
							}
						}

						bool isPlayer = m_subsystemPlayers.IsPlayer(body.Entity);
						bool isValidTarget = (creature.Category & m_autoChaseMask) > (CreatureCategory)0;

						if ((AttacksPlayer && isPlayer && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
							(AttacksNonPlayerCreature && !isPlayer && isValidTarget))
						{
							// Recordar al objetivo
							m_lastAttacker = creature;
							m_lastAttackTime = m_subsystemTime.GameTime;

							// Iniciar ataque
							float chaseTime = ChaseTimeOnTouch * m_attackPersistanceFactor;
							Attack(creature, ChaseRangeOnTouch, chaseTime, false);
						}
					}
				}

				// Comportamiento de salto si el objetivo está encima
				if (m_target != null && JumpWhenTargetStanding && body == m_target.ComponentBody &&
					body.StandingOnBody == m_componentCreature.ComponentBody)
				{
					m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
				}

				// Verificar que la huida sigue desactivada si está near death
				EnsureRunAwayIsDisabled();
			};

			// Registrar hook para mods
			m_registeredLoader = null;
			ModsManager.HookAction("ChaseBehaviorScoreTarget", delegate (ModLoader loader)
			{
				m_registeredLoader = loader;
				return false;
			});

			// Obtener campo privado de ComponentMiner para detección de disparo
			m_minerHasDigOrderField = typeof(ComponentMiner).GetField("m_hasDigOrder",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		}

		public new void Update(float dt)
		{
			// Actualizar estado de salud
			bool wasNearDeath = m_isNearDeath;
			m_isNearDeath = IsNearDeath();

			// Ajustar factor de persistencia según salud Y MANEJAR COMPORTAMIENTO DE HUIDA
			if (m_isNearDeath && !wasNearDeath)
			{
				m_attackPersistanceFactor = 2f;
				DisableRunAwayBehavior(); // ¡DESACTIVAR COMPORTAMIENTO DE HUIDA!
			}
			else if (!m_isNearDeath && wasNearDeath)
			{
				m_attackPersistanceFactor = 1f;
				// Cuando sale de near death, el comportamiento de huida se restablece
				// pero como ComponentBanditRunAwayBehavior ya está configurado para NO huir,
				// no necesitamos hacer nada especial
			}

			// Actualizar cooldown de cambio de objetivo
			if (m_switchTargetCooldown > 0f)
			{
				m_switchTargetCooldown -= dt;
			}

			// Llamar al Update base
			base.Update(dt);

			// CAMBIAR AL ÚLTIMO ATACANTE SI ES MEJOR OBJETIVO
			if (m_lastAttacker != null && m_switchTargetCooldown <= 0f)
			{
				float timeSinceLastAttack = (float)(m_subsystemTime.GameTime - m_lastAttackTime);
				if (timeSinceLastAttack <= AttackerMemoryDuration)
				{
					bool isAttackerAlive = m_lastAttacker.ComponentHealth.Health > 0f;
					float distanceToAttacker = Vector3.Distance(
						m_componentCreature.ComponentBody.Position,
						m_lastAttacker.ComponentBody.Position);
					float currentRange = m_subsystemSky.SkyLightIntensity < 0.2f ?
						m_nightChaseRange : m_dayChaseRange;

					if (isAttackerAlive && distanceToAttacker <= currentRange * 1.2f)
					{
						if (m_target != m_lastAttacker)
						{
							float chaseTime = m_subsystemSky.SkyLightIntensity < 0.2f ?
								m_nightChaseTime : m_dayChaseTime;
							Attack(m_lastAttacker, currentRange,
								chaseTime * m_attackPersistanceFactor, true);
							m_switchTargetCooldown = 2f;
						}
					}
				}
				else
				{
					m_lastAttacker = null;
				}
			}

			// Reproducir sonido de ataque
			if (m_componentCreature.ComponentCreatureModel.IsAttackHitMoment)
			{
				m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
			}

			// Manejar sonido de disparo para bandidos con arco
			if (m_componentMiner != null)
			{
				if (m_lastShotSoundTime <= 0f)
				{
					bool isShooting = false;

					if (m_minerHasDigOrderField != null)
					{
						isShooting = (bool)m_minerHasDigOrderField.GetValue(m_componentMiner);
					}

					if (isShooting && m_target != null)
					{
						m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						m_lastShotSoundTime = 0.3f;
					}
				}
				else
				{
					m_lastShotSoundTime -= dt;
				}
			}

			// Aumentar importancia si está cerca de la muerte y tiene objetivo
			if (m_isNearDeath && m_target != null)
			{
				if (m_importanceLevel < ImportanceLevelPersistent * m_attackPersistanceFactor)
				{
					m_importanceLevel = ImportanceLevelPersistent * m_attackPersistanceFactor;
				}
			}

			// BUSCAR MEJOR OBJETIVO
			if (m_target != null && m_switchTargetCooldown <= 0f)
			{
				ComponentCreature betterTarget = FindBetterTarget();
				if (betterTarget != null && betterTarget != m_target)
				{
					float chaseRange = m_subsystemSky.SkyLightIntensity < 0.2f ?
						m_nightChaseRange : m_dayChaseRange;
					float chaseTime = m_subsystemSky.SkyLightIntensity < 0.2f ?
						m_nightChaseTime : m_dayChaseTime;
					Attack(betterTarget, chaseRange,
						chaseTime * m_attackPersistanceFactor, true);
					m_switchTargetCooldown = 2f;
				}
			}

			// VERIFICACIÓN CONTINUA: Asegurar que la huida está desactivada si está near death
			EnsureRunAwayIsDisabled();
		}

		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			// Excluir miembros de la misma manada
			if (m_componentBanditHerd != null && componentCreature != null)
			{
				ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
					string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
				{
					return 0f;
				}
			}

			// Obtener puntuación base
			float score = base.ScoreTarget(componentCreature);
			if (score <= 0f) return 0f;

			// Hook para mods
			if (m_registeredLoader != null)
			{
				m_registeredLoader.ChaseBehaviorScoreTarget(this, componentCreature, ref score);
			}

			// BONUS POR SER EL ÚLTIMO ATACANTE
			if (componentCreature == m_lastAttacker)
			{
				float timeSinceLastAttack = (float)(m_subsystemTime.GameTime - m_lastAttackTime);
				if (timeSinceLastAttack <= AttackerMemoryDuration)
				{
					score *= 5f; // Prioridad máxima
				}
				else if (timeSinceLastAttack <= AttackerMemoryDuration * 2)
				{
					score *= 2f; // Prioridad media
				}
			}

			// BONUS POR CERCANÍA (si está mucho más cerca que el objetivo actual)
			if (m_target != null && componentCreature != m_target)
			{
				float currentDistance = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					m_target.ComponentBody.Position);
				float newDistance = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					componentCreature.ComponentBody.Position);

				if (newDistance < currentDistance * 0.5f)
				{
					score *= 1.5f;
				}
			}

			// BONUS POR ESTAR CERCA DE LA MUERTE (más agresivo)
			if (m_isNearDeath && score > 0f)
			{
				score *= 1.5f;
			}

			return score;
		}

		public override void Attack(ComponentCreature componentCreature, float maxRange,
			float maxChaseTime, bool isPersistent)
		{
			if (Suppressed || componentCreature == null)
			{
				return;
			}

			// No atacar a miembros de la misma manada
			if (m_componentBanditHerd != null)
			{
				ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
					string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
			}

			// AJUSTAR PARÁMETROS SEGÚN SALUD
			float adjustedChaseTime = maxChaseTime;
			bool adjustedPersistent = isPersistent;

			if (m_isNearDeath)
			{
				adjustedChaseTime *= m_attackPersistanceFactor;
				adjustedPersistent = true; // Persistencia forzada
				maxRange *= 1.2f; // Mayor rango

				// Al atacar estando near death, aseguramos que la huida está desactivada
				EnsureRunAwayIsDisabled();
			}

			// Llamar al método base
			base.Attack(componentCreature, maxRange, adjustedChaseTime, adjustedPersistent);

			// Aumentar importancia si está cerca de la muerte
			if (m_isNearDeath)
			{
				m_importanceLevel = Math.Max(m_importanceLevel,
					ImportanceLevelPersistent * m_attackPersistanceFactor);
			}
		}

		public override ComponentCreature FindTarget()
		{
			Vector3 position = m_componentCreature.ComponentBody.Position;
			ComponentCreature result = null;
			float bestScore = 0f;

			// Búsqueda en rango normal
			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(
				new Vector2(position.X, position.Z), m_range, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					// Excluir miembros de la misma manada
					if (m_componentBanditHerd != null)
					{
						ComponentBanditHerdBehavior targetHerd = creature.Entity.FindComponent<ComponentBanditHerdBehavior>();
						if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
							string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}
					}

					float score = ScoreTarget(creature);
					if (score > bestScore)
					{
						bestScore = score;
						result = creature;
					}
				}
			}

			// Si está cerca de la muerte y no hay objetivo, buscar en rango extendido
			if (m_isNearDeath && result == null && m_range < 40f)
			{
				float extendedRange = 40f;
				m_componentBodies.Clear();
				m_subsystemBodies.FindBodiesAroundPoint(
					new Vector2(position.X, position.Z), extendedRange, m_componentBodies);

				for (int i = 0; i < m_componentBodies.Count; i++)
				{
					ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
					if (creature != null)
					{
						if (m_componentBanditHerd != null)
						{
							ComponentBanditHerdBehavior targetHerd = creature.Entity.FindComponent<ComponentBanditHerdBehavior>();
							if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
								string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
							{
								continue;
							}
						}

						float score = ScoreTarget(creature);
						if (score > bestScore)
						{
							bestScore = score;
							result = creature;
						}
					}
				}
			}

			// Verificar que la huida está desactivada si está near death
			EnsureRunAwayIsDisabled();

			return result;
		}

		public new void StopAttack()
		{
			// SOLO DETENER EL ATAQUE SI NO ESTÁ CERCA DE LA MUERTE
			if (!m_isNearDeath)
			{
				base.StopAttack();
			}
			// Si está cerca de la muerte, NUNCA detiene el ataque

			// Verificar que la huida está desactivada si está near death
			EnsureRunAwayIsDisabled();
		}

		private ComponentCreature FindBetterTarget()
		{
			if (m_target == null) return null;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			ComponentCreature bestTarget = m_target;
			float bestScore = ScoreTarget(m_target);

			float searchRange = m_range * 1.5f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(
				new Vector2(position.X, position.Z), searchRange, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature != m_target)
				{
					// Excluir miembros de la misma manada
					if (m_componentBanditHerd != null)
					{
						ComponentBanditHerdBehavior targetHerd = creature.Entity.FindComponent<ComponentBanditHerdBehavior>();
						if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
							string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}
					}

					float score = ScoreTarget(creature);
					if (score > bestScore * 1.2f) // 20% mejor para cambiar
					{
						bestScore = score;
						bestTarget = creature;
					}
				}
			}

			return bestTarget != m_target ? bestTarget : null;
		}
	}
}
