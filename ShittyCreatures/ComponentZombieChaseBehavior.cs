using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieChaseBehavior : ComponentChaseBehavior, IUpdateable
	{
		public bool ForceAttackDuringGreenNight => this.m_forceAttackDuringGreenNight;
		public bool Suppressed
		{
			get => base.Suppressed;
			set => base.Suppressed = value;
		}
		public string CurrentState => this.m_stateMachine?.CurrentState;
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_componentZombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
			this.m_subsystemGreenNightSky = base.Project.FindSubsystem<SubsystemGreenNightSky>(true);
			this.m_attacksSameHerd = valuesDictionary.GetValue<bool>("AttacksSameHerd", false);
			this.m_attacksAllCategories = valuesDictionary.GetValue<bool>("AttacksAllCategories", true);
			this.m_fleeFromSameHerd = valuesDictionary.GetValue<bool>("FleeFromSameHerd", true);
			this.m_fleeDistance = valuesDictionary.GetValue<float>("FleeDistance", 10f);
			this.m_forceAttackDuringGreenNight = valuesDictionary.GetValue<bool>("ForceAttackDuringGreenNight", false); // Por defecto false

			// Referencia al ComponentZombieRunAwayBehavior
			this.m_zombieRunAwayBehavior = base.Entity.FindComponent<ComponentZombieRunAwayBehavior>();
			if (this.m_zombieRunAwayBehavior != null)
			{
				this.m_lowHealthToEscape = this.m_zombieRunAwayBehavior.LowHealthToEscape;
			}
			else
			{
				this.m_lowHealthToEscape = 0.2f;
			}

			bool attacksAllCategories = this.m_attacksAllCategories;
			if (attacksAllCategories)
			{
				this.m_autoChaseMask = (CreatureCategory.LandPredator | CreatureCategory.LandOther | CreatureCategory.WaterPredator | CreatureCategory.WaterOther | CreatureCategory.Bird);
				this.AttacksNonPlayerCreature = true;
				this.AttacksPlayer = true;
			}
			this.SetupZombieInjuryHandler();
			this.AddFleeState();

			this.m_previousGreenNightActive = false;
			m_defaultTargetInRangeTime = this.TargetInRangeTimeToChase;

			// Solo si ForceAttackDuringGreenNight es true y la noche verde está activa, se ajustan los tiempos
			if (this.m_forceAttackDuringGreenNight && this.m_subsystemGreenNightSky != null && this.m_subsystemGreenNightSky.IsGreenNightActive)
			{
				this.TargetInRangeTimeToChase = 0f;
				this.m_targetInRangeTime = this.TargetInRangeTimeToChase + 1f;
			}
		}

		private void SetupZombieInjuryHandler()
		{
			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
			Action<Injury> originalHandler = componentHealth.Injured;

			componentHealth.Injured = (Action<Injury>)Delegate.Combine(originalHandler, new Action<Injury>(delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				if (attacker != null)
				{
					this.m_lastAttackTimes[attacker] = this.m_retaliationMemoryDuration;
					this.m_lastAttacker = attacker;
					this.m_retaliationQueue.Add(attacker);

					while (this.m_retaliationQueue.Count > 5)
					{
						this.m_retaliationQueue.RemoveAt(0);
					}

					bool shouldAttackAttacker = !this.IsSameHerd(attacker) || this.m_attacksSameHerd;

					if (shouldAttackAttacker)
					{
						if (this.m_target != attacker)
						{
							this.StopAttack();

							bool isGreenNightActive = this.m_forceAttackDuringGreenNight &&
													  this.m_subsystemGreenNightSky != null &&
													  this.m_subsystemGreenNightSky.IsGreenNightActive;

							float chaseTime = isGreenNightActive ? 120f : 60f;

							this.Attack(attacker, 40f, chaseTime, true);
							this.m_retaliationCooldown = 1f;
							this.m_isRetaliating = true;
							this.m_retaliationTarget = attacker;
						}
					}

					if (!this.IsSameHerd(attacker) && this.m_componentZombieHerdBehavior != null &&
						this.m_componentZombieHerdBehavior.CallForHelpWhenAttacked)
					{
						this.m_componentZombieHerdBehavior.CallZombiesForHelp(attacker);
					}

					if (attacker != null && !this.m_attacksSameHerd && this.IsSameHerd(attacker))
					{
						if (this.m_componentZombieHerdBehavior != null &&
							this.m_componentZombieHerdBehavior.CallForHelpWhenAttacked)
						{
							ComponentCreature externalAttacker = this.FindExternalAttacker(injury);
							if (externalAttacker != null)
							{
								this.m_componentZombieHerdBehavior.CallZombiesForHelp(externalAttacker);
							}
						}

						if (this.m_fleeFromSameHerd)
						{
							this.FleeFromTarget(attacker);
						}
					}
				}
			}));
		}

		private ComponentCreature FindExternalAttacker(Injury injury)
		{
			if (injury.Attacker == null)
				return null;

			return !this.IsSameHerd(injury.Attacker) ? injury.Attacker : null;
		}

		private bool IsSameHerd(ComponentCreature otherCreature)
		{
			return otherCreature != null && this.m_componentZombieHerdBehavior != null &&
				   this.m_componentZombieHerdBehavior.IsSameZombieHerd(otherCreature);
		}

		public override void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (target == null)
				return;

			bool isRetaliating = this.m_isRetaliating && target == this.m_retaliationTarget;
			bool isSameHerdTarget = !isRetaliating && !this.m_attacksSameHerd && this.IsSameHerd(target);

			if (isSameHerdTarget)
			{
				if (this.m_componentZombieHerdBehavior != null)
				{
					ComponentCreature externalEnemy = this.FindExternalEnemyNearby(maxRange);
					if (externalEnemy != null)
					{
						this.m_componentZombieHerdBehavior.CoordinateGroupAttack(externalEnemy);
					}
				}
			}
			else
			{
				if (isRetaliating)
				{
					this.Suppressed = false;
					this.ImportanceLevelNonPersistent = 500f;
					this.ImportanceLevelPersistent = 500f;
					this.m_autoChaseSuppressionTime = 0f;
				}

				// Solo si ForceAttackDuringGreenNight es true, se prioriza a los jugadores
				bool isGreenNightActive = this.m_forceAttackDuringGreenNight &&
										  this.m_subsystemGreenNightSky != null &&
										  this.m_subsystemGreenNightSky.IsGreenNightActive;

				if (isGreenNightActive && !isRetaliating)
				{
					if (target.Entity != null && target.Entity.FindComponent<ComponentPlayer>() == null)
					{
						ComponentPlayer nearestPlayer = this.FindNearestPlayer(maxRange);
						if (nearestPlayer != null)
						{
							target = nearestPlayer;
						}
					}
				}

				base.Attack(target, maxRange, maxChaseTime, isPersistent);

				if (!isRetaliating && this.m_componentZombieHerdBehavior != null)
				{
					this.m_componentZombieHerdBehavior.CoordinateGroupAttack(target);
				}
			}
		}

		private ComponentCreature FindExternalEnemyNearby(float range)
		{
			if (this.m_componentCreature == null || this.m_componentCreature.ComponentBody == null)
				return null;

			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentCreature bestTarget = null;
			float bestScore = 0f;

			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), range, this.m_componentBodies);

			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentCreature creature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature != this.m_componentCreature && !this.IsSameHerd(creature))
				{
					float dist = Vector3.Distance(position, creature.ComponentBody.Position);
					float score = range - dist;
					if (score > bestScore)
					{
						bestScore = score;
						bestTarget = creature;
					}
				}
			}
			return bestTarget;
		}

		private ComponentPlayer FindNearestPlayer(float range)
		{
			if (this.m_componentCreature == null || this.m_componentCreature.ComponentBody == null)
				return null;

			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentPlayer nearestPlayer = null;
			float minDist = float.MaxValue;

			SubsystemPlayers subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			if (subsystemPlayers != null)
			{
				foreach (ComponentPlayer player in subsystemPlayers.ComponentPlayers)
				{
					if (player != null && player.ComponentHealth.Health > 0f)
					{
						float dist = Vector3.Distance(position, player.ComponentBody.Position);
						if (dist <= range && dist < minDist)
						{
							minDist = dist;
							nearestPlayer = player;
						}
					}
				}
			}
			return nearestPlayer;
		}

		public override ComponentCreature FindTarget()
		{
			ComponentCreature retaliationTarget = this.GetNextRetaliationTarget();
			if (retaliationTarget != null)
			{
				return retaliationTarget;
			}

			// Solo si ForceAttackDuringGreenNight es true, se busca jugadores durante noche verde
			bool isGreenNightActive = this.m_forceAttackDuringGreenNight &&
									  this.m_subsystemGreenNightSky != null &&
									  this.m_subsystemGreenNightSky.IsGreenNightActive;
			if (isGreenNightActive)
			{
				ComponentPlayer nearestPlayer = this.FindNearestPlayer(this.m_range);
				if (nearestPlayer != null)
				{
					return nearestPlayer;
				}
			}

			if (!this.m_attacksSameHerd)
			{
				Vector3 position = this.m_componentCreature.ComponentBody.Position;
				ComponentCreature bestTarget = null;
				float bestScore = 0f;

				this.m_componentBodies.Clear();
				this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), this.m_range, this.m_componentBodies);

				for (int i = 0; i < this.m_componentBodies.Count; i++)
				{
					ComponentCreature candidate = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
					if (candidate != null && !this.IsSameHerd(candidate))
					{
						float score = this.ScoreTarget(candidate);
						if (score > bestScore)
						{
							bestScore = score;
							bestTarget = candidate;
						}
					}
				}
				return bestTarget;
			}

			return base.FindTarget();
		}

		private ComponentCreature GetNextRetaliationTarget()
		{
			for (int i = this.m_retaliationQueue.Count - 1; i >= 0; i--)
			{
				ComponentCreature attacker = this.m_retaliationQueue[i];
				if (attacker == null || attacker.ComponentHealth.Health <= 0f ||
					!this.m_lastAttackTimes.ContainsKey(attacker) ||
					this.m_lastAttackTimes[attacker] <= 0f)
				{
					this.m_retaliationQueue.RemoveAt(i);
				}
			}

			if (this.m_retaliationQueue.Count > 0)
			{
				ComponentCreature latestAttacker = this.m_retaliationQueue[this.m_retaliationQueue.Count - 1];

				bool isValid = (!this.IsSameHerd(latestAttacker) || this.m_attacksSameHerd) &&
							   Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
											   latestAttacker.ComponentBody.Position) <= this.m_range * 2f;

				if (isValid)
				{
					return latestAttacker;
				}
			}

			return null;
		}

		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			if (!this.m_attacksSameHerd && this.IsSameHerd(componentCreature))
			{
				return 0f;
			}

			float baseScore = base.ScoreTarget(componentCreature);

			if (this.m_retaliationQueue.Contains(componentCreature) &&
				this.m_lastAttackTimes.ContainsKey(componentCreature) &&
				this.m_lastAttackTimes[componentCreature] > 0f)
			{
				return baseScore * 10f;
			}

			if (componentCreature == this.m_lastAttacker &&
				this.m_lastAttackTimes.ContainsKey(componentCreature) &&
				this.m_lastAttackTimes[componentCreature] > 0f)
			{
				return baseScore * 8f;
			}

			return baseScore;
		}

		private void AddFleeState()
		{
			this.m_stateMachine.AddState("Fleeing", delegate
			{
				this.m_importanceLevel = 150f;
				this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
			}, delegate
			{
				if (this.m_target == null || this.m_componentCreature.ComponentHealth.Health <= 0f)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
				else
				{
					Vector3 v = this.m_componentCreature.ComponentBody.Position - this.m_target.ComponentBody.Position;
					if (v.LengthSquared() > 0.01f)
					{
						v = Vector3.Normalize(v);
						Vector3 destination = this.m_componentCreature.ComponentBody.Position + v * this.m_fleeDistance;
						this.m_componentPathfinding.SetDestination(new Vector3?(destination), 1f, 1.5f, 0, false, true, false, null);
					}

					float dist = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_target.ComponentBody.Position);
					if (dist > this.m_fleeDistance * 1.5f)
					{
						this.m_stateMachine.TransitionTo("LookingForTarget");
					}

					if (this.m_random.Float(0f, 1f) < 0.05f * this.m_dt)
					{
						this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
					}
				}
			}, delegate
			{
				this.m_componentPathfinding.Stop();
				this.m_importanceLevel = 0f;
			});
		}

		private void FleeFromTarget(ComponentCreature target)
		{
			if (target != null && this.m_componentCreature.ComponentHealth.Health > 0f)
			{
				this.m_target = target;
				this.m_stateMachine.TransitionTo("Fleeing");
			}
		}

		public override void Update(float dt)
		{
			base.Update(dt);

			if (this.m_retaliationCooldown > 0f)
			{
				this.m_retaliationCooldown -= dt;
			}

			List<ComponentCreature> expiredAttackers = new List<ComponentCreature>();
			foreach (var kvp in this.m_lastAttackTimes)
			{
				this.m_lastAttackTimes[kvp.Key] = kvp.Value - dt;
				if (this.m_lastAttackTimes[kvp.Key] <= 0f)
				{
					expiredAttackers.Add(kvp.Key);
					this.m_retaliationQueue.Remove(kvp.Key);
					if (kvp.Key == this.m_lastAttacker)
					{
						this.m_lastAttacker = null;
					}
					if (kvp.Key == this.m_retaliationTarget)
					{
						this.m_retaliationTarget = null;
						this.m_isRetaliating = false;
					}
				}
			}

			foreach (ComponentCreature attacker in expiredAttackers)
			{
				this.m_lastAttackTimes.Remove(attacker);
			}

			// Detectar noche verde solo si ForceAttackDuringGreenNight es true
			bool greenNightActive = this.m_forceAttackDuringGreenNight &&
									this.m_subsystemGreenNightSky != null &&
									this.m_subsystemGreenNightSky.IsGreenNightActive;

			if (greenNightActive != this.m_previousGreenNightActive)
			{
				if (greenNightActive && !this.m_previousGreenNightActive)
				{
					// Solo se activa el comportamiento agresivo si ForceAttackDuringGreenNight es true
					this.TargetInRangeTimeToChase = 0f;

					if (!this.m_isRetaliating)
					{
						ComponentPlayer nearestPlayer = this.FindNearestPlayer(this.m_range);
						if (nearestPlayer != null)
						{
							this.StopAttack();
							this.Attack(nearestPlayer, this.m_range, 120f, true);
						}
					}
				}
				else if (!greenNightActive && this.m_previousGreenNightActive)
				{
					this.TargetInRangeTimeToChase = m_defaultTargetInRangeTime;

					if (!this.m_isRetaliating)
					{
						this.StopAttack();
						this.m_target = null;
						if (this.m_stateMachine.CurrentState != "LookingForTarget")
						{
							this.m_stateMachine.TransitionTo("LookingForTarget");
						}
					}

					this.AttacksPlayer = this.m_attacksAllCategories;
				}

				this.m_previousGreenNightActive = greenNightActive;
			}

			// Comportamiento durante noche verde SOLO si ForceAttackDuringGreenNight es true
			if (greenNightActive)
			{
				this.AttacksPlayer = true;
				this.Suppressed = false;
				this.TargetInRangeTimeToChase = 0f;
				this.m_targetInRangeTime = 1f;

				if (this.m_stateMachine.CurrentState == "Fleeing")
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}

				if (!this.m_isRetaliating && this.m_target == null)
				{
					ComponentPlayer nearestPlayer = this.FindNearestPlayer(this.m_range);
					if (nearestPlayer != null)
					{
						this.Attack(nearestPlayer, this.m_range, 120f, true);
					}
				}
			}
			else
			{
				// Comportamiento normal fuera de noche verde
				if (this.m_subsystemGreenNightSky != null && !this.m_subsystemGreenNightSky.IsGreenNightActive)
				{
					this.TargetInRangeTimeToChase = m_defaultTargetInRangeTime;
				}

				if (!this.m_isRetaliating)
				{
					ComponentCreature nextRetaliation = this.GetNextRetaliationTarget();
					if (nextRetaliation != null && nextRetaliation != this.m_target)
					{
						this.StopAttack();
						this.Attack(nextRetaliation, 30f, 60f, true);
						this.m_isRetaliating = true;
						this.m_retaliationTarget = nextRetaliation;
						this.m_retaliationCooldown = 1f;
					}
				}
			}

			if (this.m_isRetaliating && this.m_retaliationTarget != null)
			{
				bool targetStillValid = this.m_retaliationTarget.ComponentHealth.Health > 0f &&
									   Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
													   this.m_retaliationTarget.ComponentBody.Position) <= this.m_range * 2f &&
									   (!this.IsSameHerd(this.m_retaliationTarget) || this.m_attacksSameHerd);

				if (!targetStillValid)
				{
					this.m_isRetaliating = false;
					this.m_retaliationTarget = null;

					ComponentCreature nextTarget = this.GetNextRetaliationTarget();
					if (nextTarget != null)
					{
						this.Attack(nextTarget, 30f, 60f, true);
						this.m_isRetaliating = true;
						this.m_retaliationTarget = nextTarget;
					}
				}
			}
		}

		public override void StopAttack()
		{
			base.StopAttack();
		}

		private ComponentZombieHerdBehavior m_componentZombieHerdBehavior;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private Dictionary<ComponentCreature, float> m_lastAttackTimes = new Dictionary<ComponentCreature, float>();
		private float m_retaliationMemoryDuration = 30f;
		private ComponentCreature m_lastAttacker;
		private float m_retaliationCooldown;
		private bool m_attacksSameHerd;
		private bool m_attacksAllCategories;
		private bool m_fleeFromSameHerd;
		private float m_fleeDistance = 10f;
		private bool m_forceAttackDuringGreenNight;
		private ComponentZombieRunAwayBehavior m_zombieRunAwayBehavior;
		private float m_lowHealthToEscape;
		private bool m_previousGreenNightActive;
		private float m_defaultTargetInRangeTime = 3f;
		private List<ComponentCreature> m_retaliationQueue = new List<ComponentCreature>();
		private bool m_isRetaliating;
		private ComponentCreature m_retaliationTarget;
	}
}
