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
		private float m_attackPersistanceFactor = 1f;
		private ComponentCreature m_lastAttacker;
		private double m_lastAttackTime;
		private float m_switchTargetCooldown = 0f;
		private ComponentMiner m_componentMiner;
		private float m_lastShotSoundTime;
		private System.Reflection.FieldInfo m_minerHasDigOrderField;

		private bool IsBanditHerd()
		{
			return m_componentBanditHerd != null &&
				   !string.IsNullOrEmpty(m_componentBanditHerd.HerdName) &&
				   string.Equals(m_componentBanditHerd.HerdName, "bandit", StringComparison.OrdinalIgnoreCase);
		}

		private bool IsNearDeath()
		{
			if (m_componentCreature?.ComponentHealth == null) return false;
			return m_componentCreature.ComponentHealth.Health <= 0.2f;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_componentBanditHerd = Entity.FindComponent<ComponentBanditHerdBehavior>();
			m_componentMiner = Entity.FindComponent<ComponentMiner>();

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
			componentHealth.Injured = (Action<Injury>)Delegate.RemoveAll(componentHealth.Injured, componentHealth.Injured);
			componentHealth.Injured = new Action<Injury>(delegate (Injury injury)
			{
				m_isNearDeath = IsNearDeath();

				if (m_isNearDeath)
				{
					m_attackPersistanceFactor = 2f;
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
				}

				if (m_random.Float(0f, 1f) < m_chaseWhenAttackedProbability)
				{
					bool flag = false;
					float num;
					float num2;
					if (m_chaseWhenAttackedProbability >= 1f)
					{
						num = 30f;
						num2 = 60f * m_attackPersistanceFactor;
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

			ComponentBody componentBody = m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.RemoveAll(componentBody.CollidedWithBody, componentBody.CollidedWithBody);
			componentBody.CollidedWithBody = new Action<ComponentBody>(delegate (ComponentBody body)
			{
				m_isNearDeath = IsNearDeath();

				if (m_isNearDeath)
				{
					m_attackPersistanceFactor = 2f;
				}

				if (m_target == null && m_autoChaseSuppressionTime <= 0f && m_random.Float(0f, 1f) < m_chaseOnTouchProbability)
				{
					ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						if (m_componentBanditHerd != null)
						{
							ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
							if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
								string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
							{
								return;
							}
						}

						bool flag = m_subsystemPlayers.IsPlayer(body.Entity);
						bool flag2 = (componentCreature.Category & m_autoChaseMask) > (CreatureCategory)0;
						if ((AttacksPlayer && flag && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
							(AttacksNonPlayerCreature && !flag && flag2))
						{
							if (flag || flag2)
							{
								m_lastAttacker = componentCreature;
								m_lastAttackTime = m_subsystemTime.GameTime;
							}

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
			}

			if (!m_isNearDeath && wasNearDeath)
			{
				m_attackPersistanceFactor = 1f;
			}

			if (m_switchTargetCooldown > 0f)
			{
				m_switchTargetCooldown -= dt;
			}

			base.Update(dt);

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

			if (m_target != null && m_switchTargetCooldown <= 0f)
			{
				ComponentCreature betterTarget = FindBetterTarget();
				if (betterTarget != null && betterTarget != m_target)
				{
					float chaseRange = m_subsystemSky.SkyLightIntensity < 0.2f ? m_nightChaseRange : m_dayChaseRange;
					float chaseTime = m_subsystemSky.SkyLightIntensity < 0.2f ? m_nightChaseTime : m_dayChaseTime;
					Attack(betterTarget, chaseRange, chaseTime, true);
					m_switchTargetCooldown = 2.0f;
				}
			}
		}

		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			float score = base.ScoreTarget(componentCreature);

			if (score <= 0f) return 0f;

			if (m_componentBanditHerd != null && componentCreature != null)
			{
				ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
					string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
				{
					return 0f;
				}
			}

			if (m_registeredLoader != null)
			{
				m_registeredLoader.ChaseBehaviorScoreTarget(this, componentCreature, ref score);
			}

			if (componentCreature == m_lastAttacker &&
				(m_subsystemTime.GameTime - m_lastAttackTime) < 10.0)
			{
				score *= 3.0f;
			}
			else if (componentCreature == m_lastAttacker)
			{
				score *= 2.0f;
			}

			if (m_target != null && componentCreature != m_target)
			{
				float currentDistance = Vector3.Distance(m_componentCreature.ComponentBody.Position,
														 m_target.ComponentBody.Position);
				float newDistance = Vector3.Distance(m_componentCreature.ComponentBody.Position,
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

			return score;
		}

		public override void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
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
			}

			base.Attack(componentCreature, maxRange, adjustedChaseTime, adjustedPersistent);

			if (m_isNearDeath)
			{
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
					if (m_componentBanditHerd != null)
					{
						ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
						if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
							string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
						{
							continue;
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
						if (m_componentBanditHerd != null)
						{
							ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
							if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
								string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
							{
								continue;
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

		public new void StopAttack()
		{
			if (!m_isNearDeath)
			{
				base.StopAttack();
			}
		}

		private ComponentCreature FindBetterTarget()
		{
			if (m_target == null) return null;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			ComponentCreature bestTarget = m_target;
			float bestScore = ScoreTarget(m_target);

			float searchRange = m_range * 1.5f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), searchRange, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature componentCreature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (componentCreature != null && componentCreature != m_target)
				{
					if (m_componentBanditHerd != null)
					{
						ComponentBanditHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
						if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
							string.Equals(targetHerd.HerdName, m_componentBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}
					}

					float score = ScoreTarget(componentCreature);
					if (score > bestScore * 1.2f)
					{
						bestScore = score;
						bestTarget = componentCreature;
					}
				}
			}

			return bestTarget != m_target ? bestTarget : null;
		}
	}
}
