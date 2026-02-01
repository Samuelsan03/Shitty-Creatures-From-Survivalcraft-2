using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieChaseBehavior : ComponentChaseBehavior
	{
		private ComponentZombieHerdBehavior m_componentZombieHerdBehavior;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private Dictionary<ComponentCreature, float> m_lastAttackTimes = new Dictionary<ComponentCreature, float>();
		private float m_retaliationMemoryDuration = 30f;

		private bool m_attacksSameHerd;
		private bool m_attacksAllCategories;
		private bool m_fleeFromSameHerd;
		private float m_fleeDistance = 10f;
		private bool m_forceAttackDuringGreenNight;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_componentZombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
			m_subsystemGreenNightSky = base.Project.FindSubsystem<SubsystemGreenNightSky>(true);

			m_attacksSameHerd = valuesDictionary.GetValue<bool>("AttacksSameHerd", false);
			m_attacksAllCategories = valuesDictionary.GetValue<bool>("AttacksAllCategories", true);
			m_fleeFromSameHerd = valuesDictionary.GetValue<bool>("FleeFromSameHerd", true);
			m_fleeDistance = valuesDictionary.GetValue<float>("FleeDistance", 10f);
			m_forceAttackDuringGreenNight = valuesDictionary.GetValue<bool>("ForceAttackDuringGreenNight", false);

			if (m_attacksAllCategories)
			{
				m_autoChaseMask = CreatureCategory.LandPredator |
								  CreatureCategory.LandOther |
								  CreatureCategory.WaterPredator |
								  CreatureCategory.WaterOther |
								  CreatureCategory.Bird;
				this.AttacksNonPlayerCreature = true;
				this.AttacksPlayer = true;
			}

			this.SetupZombieInjuryHandler();
			this.AddFleeState();
		}

		private void SetupZombieInjuryHandler()
		{
			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
			Action<Injury> originalHandler = componentHealth.Injured;

			Action<Injury> zombieInjuryHandler = delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;

				if (attacker != null)
				{
					m_lastAttackTimes[attacker] = m_retaliationMemoryDuration;
				}

				if (attacker != null && !m_attacksSameHerd && IsSameHerd(attacker))
				{
					if (m_componentZombieHerdBehavior != null && m_componentZombieHerdBehavior.CallForHelpWhenAttacked)
					{
						ComponentCreature externalAttacker = FindExternalAttacker(injury);
						if (externalAttacker != null)
						{
							m_componentZombieHerdBehavior.CallZombiesForHelp(externalAttacker);
						}
					}

					if (m_fleeFromSameHerd)
					{
						FleeFromTarget(attacker);
					}

					return;
				}

				if (originalHandler != null)
				{
					originalHandler(injury);
				}
			};

			componentHealth.Injured = zombieInjuryHandler;
		}

		private ComponentCreature FindExternalAttacker(Injury injury)
		{
			if (injury.Attacker == null) return null;

			if (!IsSameHerd(injury.Attacker))
			{
				return injury.Attacker;
			}

			return null;
		}

		private bool IsSameHerd(ComponentCreature otherCreature)
		{
			if (otherCreature == null || m_componentZombieHerdBehavior == null)
				return false;

			return m_componentZombieHerdBehavior.IsSameZombieHerd(otherCreature);
		}

		public virtual void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent, bool isRetaliation)
		{
			if (!isRetaliation && !m_attacksSameHerd && IsSameHerd(target))
			{
				if (m_componentZombieHerdBehavior != null)
				{
					ComponentCreature externalEnemy = FindExternalEnemyNearby(maxRange);
					if (externalEnemy != null)
					{
						m_componentZombieHerdBehavior.CoordinateGroupAttack(externalEnemy);
					}
				}
				return;
			}

			if (isRetaliation)
			{
				this.Suppressed = false;

				if (m_forceAttackDuringGreenNight && m_subsystemGreenNightSky != null &&
					m_subsystemGreenNightSky.IsGreenNightActive &&
					target.Entity.FindComponent<ComponentPlayer>() == null)
				{
					Vector3 position = this.m_componentCreature.ComponentBody.Position;
					ComponentPlayer nearbyPlayer = null;
					float nearestDistance = float.MaxValue;

					var players = base.Project.FindSubsystem<SubsystemPlayers>(true);
					if (players != null)
					{
						foreach (ComponentPlayer player in players.ComponentPlayers)
						{
							if (player != null && player.ComponentHealth.Health > 0f)
							{
								float distance = Vector3.Distance(position, player.ComponentBody.Position);
								if (distance <= maxRange && distance < nearestDistance)
								{
									nearestDistance = distance;
									nearbyPlayer = player;
								}
							}
						}
					}

					if (nearbyPlayer != null && nearestDistance < maxRange * 0.5f)
					{
						target = nearbyPlayer;
					}
				}
			}

			base.Attack(target, maxRange, maxChaseTime, isPersistent);

			if (!isRetaliation && m_componentZombieHerdBehavior != null)
			{
				m_componentZombieHerdBehavior.CoordinateGroupAttack(target);
			}
		}

		public override void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			this.Attack(target, maxRange, maxChaseTime, isPersistent, false);
		}

		private ComponentCreature FindExternalEnemyNearby(float range)
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentCreature bestTarget = null;
			float bestScore = 0f;

			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), range, this.m_componentBodies);

			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentCreature creature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature != this.m_componentCreature)
				{
					if (!IsSameHerd(creature))
					{
						float distance = Vector3.Distance(position, creature.ComponentBody.Position);
						float score = range - distance;

						if (score > bestScore)
						{
							bestScore = score;
							bestTarget = creature;
						}
					}
				}
			}

			return bestTarget;
		}

		public override ComponentCreature FindTarget()
		{
			if (m_forceAttackDuringGreenNight && m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive)
			{
				Vector3 position = this.m_componentCreature.ComponentBody.Position;

				ComponentCreature bestTarget = null;
				float bestScore = 0f;

				ComponentPlayer nearestPlayer = null;
				float nearestPlayerDistance = float.MaxValue;

				var players = base.Project.FindSubsystem<SubsystemPlayers>(true);
				if (players != null)
				{
					foreach (ComponentPlayer player in players.ComponentPlayers)
					{
						if (player != null && player.ComponentHealth.Health > 0f)
						{
							float distance = Vector3.Distance(position, player.ComponentBody.Position);
							if (distance <= this.m_range && distance < nearestPlayerDistance)
							{
								nearestPlayerDistance = distance;
								nearestPlayer = player;
							}
						}
					}
				}

				this.m_componentBodies.Clear();
				this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), this.m_range, this.m_componentBodies);

				for (int i = 0; i < this.m_componentBodies.Count; i++)
				{
					ComponentCreature creature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
					if (creature != null && creature != this.m_componentCreature)
					{
						if (!m_attacksSameHerd && IsSameHerd(creature))
						{
							continue;
						}

						float distance = Vector3.Distance(position, creature.ComponentBody.Position);
						float score = this.ScoreTarget(creature);

						if (creature.Entity.FindComponent<ComponentPlayer>() != null)
						{
							score *= 1.5f;
						}

						if (score > bestScore)
						{
							bestScore = score;
							bestTarget = creature;
						}
					}
				}

				if (nearestPlayer != null && nearestPlayerDistance <= this.m_range * 0.7f)
				{
					return nearestPlayer;
				}

				return bestTarget;
			}

			if (!m_attacksSameHerd)
			{
				Vector3 position = this.m_componentCreature.ComponentBody.Position;
				ComponentCreature result = null;
				float bestScore = 0f;

				this.m_componentBodies.Clear();
				this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), this.m_range, this.m_componentBodies);

				for (int i = 0; i < this.m_componentBodies.Count; i++)
				{
					ComponentCreature creature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
					if (creature != null)
					{
						if (IsSameHerd(creature))
						{
							continue;
						}

						float score = this.ScoreTarget(creature);
						if (score > bestScore)
						{
							bestScore = score;
							result = creature;
						}
					}
				}

				return result;
			}

			return base.FindTarget();
		}

		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			if (!m_attacksSameHerd && IsSameHerd(componentCreature))
			{
				return 0f;
			}

			return base.ScoreTarget(componentCreature);
		}

		private void AddFleeState()
		{
			this.m_stateMachine.AddState("Fleeing", delegate
			{
				this.m_importanceLevel = 150f;
				this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
			}, delegate
			{
				if (this.m_componentCreature.ComponentHealth.Health < 0.2f)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
					return;
				}

				if (this.m_target == null || this.m_componentCreature.ComponentHealth.Health <= 0f)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
					return;
				}

				Vector3 fleeDirection = this.m_componentCreature.ComponentBody.Position - this.m_target.ComponentBody.Position;
				if (fleeDirection.LengthSquared() > 0.01f)
				{
					fleeDirection = Vector3.Normalize(fleeDirection);
					Vector3 destination = this.m_componentCreature.ComponentBody.Position + fleeDirection * m_fleeDistance;

					this.m_componentPathfinding.SetDestination(
						new Vector3?(destination),
						1f,
						1.5f,
						0,
						false,
						true,
						false,
						null
					);
				}

				float distanceToTarget = Vector3.Distance(
					this.m_componentCreature.ComponentBody.Position,
					this.m_target.ComponentBody.Position
				);

				if (distanceToTarget > m_fleeDistance * 1.5f)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}

				if (this.m_random.Float(0f, 1f) < 0.05f * this.m_dt)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
				}
			}, delegate
			{
				this.m_componentPathfinding.Stop();
				this.m_importanceLevel = 0f;
			});
		}

		private void FleeFromTarget(ComponentCreature target)
		{
			if (this.m_componentCreature.ComponentHealth.Health < 0.2f)
			{
				return;
			}

			if (target == null || this.m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			this.m_target = target;
			this.m_stateMachine.TransitionTo("Fleeing");
		}

		public override void Update(float dt)
		{
			base.Update(dt);

			List<ComponentCreature> toRemove = new List<ComponentCreature>();
			foreach (var kvp in m_lastAttackTimes)
			{
				m_lastAttackTimes[kvp.Key] = kvp.Value - dt;
				if (m_lastAttackTimes[kvp.Key] <= 0f)
				{
					toRemove.Add(kvp.Key);
				}
			}

			foreach (var creature in toRemove)
			{
				m_lastAttackTimes.Remove(creature);
			}

			if (m_forceAttackDuringGreenNight && m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive)
			{
				this.AttacksPlayer = true;
				this.Suppressed = false;

				if (this.m_target != null)
				{
					ComponentPlayer playerComponent = this.m_target.Entity.FindComponent<ComponentPlayer>();
					if (playerComponent == null)
					{
					}
				}

				if (this.m_stateMachine.CurrentState == "Fleeing")
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
			}
			else if (m_subsystemGreenNightSky != null && !m_subsystemGreenNightSky.IsGreenNightActive)
			{
				this.AttacksPlayer = m_attacksAllCategories;
			}
		}

		public override void StopAttack()
		{
			base.StopAttack();
		}
	}
}
