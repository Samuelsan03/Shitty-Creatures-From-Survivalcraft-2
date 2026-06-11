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

		// Modo narcotraficante
		public bool IsDrugTraffickerMode { get; set; } = false;

		private const float AttackerMemoryDuration = 30f;
		private const float NearDeathThreshold = 0.2f;

		private bool IsNearDeath()
		{
			if (m_componentCreature?.ComponentHealth == null) return false;
			return m_componentCreature.ComponentHealth.Health <= NearDeathThreshold;
		}

		private void DisableRunAwayBehavior()
		{
			if (m_componentBanditRunAway != null)
			{
				var field = typeof(ComponentRunAwayBehavior).GetField("m_importanceLevel",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (field != null)
				{
					field.SetValue(m_componentBanditRunAway, 0f);
				}
			}
		}

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

			m_componentBanditHerd = Entity.FindComponent<ComponentBanditHerdBehavior>();
			m_componentBanditRunAway = Entity.FindComponent<ComponentBanditRunAwayBehavior>();
			m_componentMiner = Entity.FindComponent<ComponentMiner>();
			IsDrugTraffickerMode = valuesDictionary.GetValue<bool>("IsDrugTraffickerMode", false);

			m_autoChaseMask = CreatureCategory.LandPredator | CreatureCategory.LandOther |
							  CreatureCategory.WaterPredator | CreatureCategory.WaterOther |
							  CreatureCategory.Bird;

			AttacksPlayer = true;
			AttacksNonPlayerCreature = true;

			m_dayChaseRange = Math.Max(m_dayChaseRange, 20f);
			m_nightChaseRange = Math.Max(m_nightChaseRange, 25f);
			m_dayChaseTime = Math.Max(m_dayChaseTime, 60f);
			m_nightChaseTime = Math.Max(m_nightChaseTime, 90f);
			m_chaseWhenAttackedProbability = 1f;
			ImportanceLevelPersistent = 300f;

			m_lastAttacker = null;
			m_lastAttackTime = 0f;
			m_switchTargetCooldown = 0f;
			m_lastShotSoundTime = 0f;

			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			Action<Injury> originalInjured = componentHealth.Injured;

			ComponentBody componentBody = m_componentCreature.ComponentBody;
			Action<ComponentBody> originalCollided = componentBody.CollidedWithBody;

			componentHealth.Injured = delegate (Injury injury)
			{
				originalInjured?.Invoke(injury);

				bool wasNearDeath = m_isNearDeath;
				m_isNearDeath = IsNearDeath();

				if (m_isNearDeath && !wasNearDeath)
				{
					m_attackPersistanceFactor = 2f;
					DisableRunAwayBehavior();
				}
				else if (!m_isNearDeath && wasNearDeath)
				{
					m_attackPersistanceFactor = 1f;
				}

				ComponentCreature attacker = injury.Attacker;

				if (attacker != null && m_componentBanditHerd != null)
				{
					ComponentBanditHerdBehavior attackerHerd = attacker.Entity.FindComponent<ComponentBanditHerdBehavior>();
					if (attackerHerd != null && !string.IsNullOrEmpty(attackerHerd.HerdName) &&
						string.Equals(attackerHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
					{
						return;
					}
				}

				if (attacker != null)
				{
					m_lastAttacker = attacker;
					m_lastAttackTime = m_subsystemTime.GameTime;
					// Forzar cooldown a 0 para que no retrase el cambio de objetivo
					m_switchTargetCooldown = 0f;
				}

				if (attacker != null)
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

				EnsureRunAwayIsDisabled();
			};

			componentBody.CollidedWithBody = delegate (ComponentBody body)
			{
				originalCollided?.Invoke(body);

				bool wasNearDeath = m_isNearDeath;
				m_isNearDeath = IsNearDeath();

				if (m_isNearDeath && !wasNearDeath)
				{
					m_attackPersistanceFactor = 2f;
					DisableRunAwayBehavior();
				}
				else if (!m_isNearDeath && wasNearDeath)
				{
					m_attackPersistanceFactor = 1f;
				}

				if (m_target == null && m_autoChaseSuppressionTime <= 0f &&
					m_random.Float(0f, 1f) < m_chaseOnTouchProbability)
				{
					ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
					if (creature != null)
					{
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
							m_lastAttacker = creature;
							m_lastAttackTime = m_subsystemTime.GameTime;
							m_switchTargetCooldown = 0f;

							float chaseTime = ChaseTimeOnTouch * m_attackPersistanceFactor;
							Attack(creature, ChaseRangeOnTouch, chaseTime, false);
						}
					}
				}

				if (m_target != null && JumpWhenTargetStanding && body == m_target.ComponentBody &&
					body.StandingOnBody == m_componentCreature.ComponentBody)
				{
					m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
				}

				EnsureRunAwayIsDisabled();
			};

			m_registeredLoader = null;
			ModsManager.HookAction("ChaseBehaviorScoreTarget", delegate (ModLoader loader)
			{
				m_registeredLoader = loader;
				return false;
			});

			m_minerHasDigOrderField = typeof(ComponentMiner).GetField("m_hasDigOrder",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		}

		public new void Update(float dt)
		{
			bool wasNearDeath = m_isNearDeath;
			m_isNearDeath = IsNearDeath();

			if (m_isNearDeath && !wasNearDeath)
			{
				m_attackPersistanceFactor = 2f;
				DisableRunAwayBehavior();
			}
			else if (!m_isNearDeath && wasNearDeath)
			{
				m_attackPersistanceFactor = 1f;
			}

			if (m_switchTargetCooldown > 0f)
			{
				m_switchTargetCooldown -= dt;
			}

			base.Update(dt);

			// Prioridad absoluta al último atacante mientras esté vivo
			if (m_lastAttacker != null)
			{
				float timeSinceLastAttack = (float)(m_subsystemTime.GameTime - m_lastAttackTime);
				if (timeSinceLastAttack <= AttackerMemoryDuration)
				{
					bool isAttackerAlive = m_lastAttacker.ComponentHealth.Health > 0f;
					if (isAttackerAlive)
					{
						float distanceToAttacker = Vector3.Distance(
							m_componentCreature.ComponentBody.Position,
							m_lastAttacker.ComponentBody.Position);
						float currentRange = m_subsystemSky.SkyLightIntensity < 0.2f ?
							m_nightChaseRange : m_dayChaseRange;

						// Si el objetivo actual no es el último atacante, atacarlo inmediatamente
						if (m_target != m_lastAttacker)
						{
							float chaseTime = m_subsystemSky.SkyLightIntensity < 0.2f ?
								m_nightChaseTime : m_dayChaseTime;
							Attack(m_lastAttacker, currentRange,
								chaseTime * m_attackPersistanceFactor, true);
							// No ponemos cooldown para que no haya retraso
						}
					}
					else
					{
						// El atacante ha muerto, olvidarlo
						m_lastAttacker = null;
					}
				}
				else
				{
					m_lastAttacker = null;
				}
			}

			if (m_componentCreature.ComponentCreatureModel.IsAttackHitMoment)
			{
				m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
			}

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

			if (m_isNearDeath && m_target != null)
			{
				if (m_importanceLevel < ImportanceLevelPersistent * m_attackPersistanceFactor)
				{
					m_importanceLevel = ImportanceLevelPersistent * m_attackPersistanceFactor;
				}
			}

			// Solo buscar mejor objetivo si no hay un último atacante vivo
			if (m_target != null && m_switchTargetCooldown <= 0f && m_lastAttacker == null)
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

			EnsureRunAwayIsDisabled();
		}

		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			if (m_componentBanditHerd != null && componentCreature != null)
			{
				ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
					string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
				{
					return 0f;
				}
			}

			if (IsDrugTraffickerMode && componentCreature != null)
			{
				bool isPlayer = m_subsystemPlayers.IsPlayer(componentCreature.Entity);
				if (isPlayer)
				{
					return 1000000f;
				}
			}

			float score = base.ScoreTarget(componentCreature);

			// Prioridad extrema al último atacante si está vivo
			if (componentCreature == m_lastAttacker && componentCreature.ComponentHealth.Health > 0f)
			{
				float timeSinceLastAttack = (float)(m_subsystemTime.GameTime - m_lastAttackTime);
				if (timeSinceLastAttack <= AttackerMemoryDuration)
				{
					// Forzar un valor altísimo para que sea imposible que otro objetivo le gane
					score = 100000f;
				}
				else if (timeSinceLastAttack <= AttackerMemoryDuration * 2)
				{
					score = 50000f;
				}
			}

			if (m_registeredLoader != null)
			{
				m_registeredLoader.ChaseBehaviorScoreTarget(this, componentCreature, ref score);
			}

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

			if (m_isNearDeath && score > 0f)
			{
				score *= 1.5f;
			}

			if (IsDrugTraffickerMode && componentCreature != null && !m_subsystemPlayers.IsPlayer(componentCreature.Entity))
			{
				score *= 0.1f;
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

			if (m_componentBanditHerd != null)
			{
				ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
					string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
			}

			float adjustedChaseTime = maxChaseTime;
			bool adjustedPersistent = isPersistent;

			if (m_isNearDeath)
			{
				adjustedChaseTime *= m_attackPersistanceFactor;
				adjustedPersistent = true;
				maxRange *= 1.2f;
				EnsureRunAwayIsDisabled();
			}

			base.Attack(componentCreature, maxRange, adjustedChaseTime, adjustedPersistent);

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

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(
				new Vector2(position.X, position.Z), m_range, m_componentBodies);

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

			EnsureRunAwayIsDisabled();

			return result;
		}

		public new void StopAttack()
		{
			if (!m_isNearDeath)
			{
				base.StopAttack();
			}

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
					if (score > bestScore * 1.2f)
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
