using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewChaseBehavior : ComponentBehavior, IUpdateable
	{
		public ComponentCreature Target
		{
			get
			{
				return this.m_target;
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		public virtual void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (this.Suppressed)
			{
				return;
			}

			// VERIFICACIÓN NUEVA: Usar ComponentNewHerdBehavior si existe
			ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (herdBehavior != null)
			{
				if (!herdBehavior.CanAttackCreature(componentCreature))
				{
					return; // No atacar si es de la misma manada
				}
			}

			this.m_target = componentCreature;
			this.m_nextUpdateTime = 0.0;
			this.m_range = maxRange;
			this.m_chaseTime = maxChaseTime;
			this.m_isPersistent = isPersistent;
			this.m_importanceLevel = (isPersistent ? this.ImportanceLevelPersistent : this.ImportanceLevelNonPersistent);
		}

		public virtual void StopAttack()
		{
			this.m_stateMachine.TransitionTo("LookingForTarget");
			this.IsActive = false;
			this.m_target = null;
			this.m_nextUpdateTime = 0.0;
			this.m_range = 0f;
			this.m_chaseTime = 0f;
			this.m_isPersistent = false;
			this.m_importanceLevel = 0f;
		}

		public virtual void Update(float dt)
		{
			if (this.Suppressed)
			{
				this.StopAttack();
			}
			this.m_autoChaseSuppressionTime -= dt;

			// VERIFICACIÓN NUEVA: Defender al jugador si somos de manada "player"
			this.CheckDefendPlayer(dt);

			if (this.IsActive && this.m_target != null)
			{
				this.m_chaseTime -= dt;
				this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
				if (this.IsTargetInAttackRange(this.m_target.ComponentBody))
				{
					this.m_componentCreatureModel.AttackOrder = true;
				}
				if (this.m_componentCreatureModel.IsAttackHitMoment)
				{
					Vector3 hitPoint;
					ComponentBody hitBody = this.GetHitBody(this.m_target.ComponentBody, out hitPoint);
					if (hitBody != null)
					{
						float chaseTimeBefore = this.m_chaseTime;
						float x = this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f;
						this.m_chaseTime = MathUtils.Max(this.m_chaseTime, x);
						bool bodyToHit = true;
						bool playAttackSound = true;

						if (bodyToHit)
						{
							this.m_componentMiner.Hit(hitBody, hitPoint, this.m_componentCreature.ComponentBody.Matrix.Forward);
						}
						if (playAttackSound)
						{
							this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						}
					}
				}
			}
			if (this.m_subsystemTime.GameTime >= this.m_nextUpdateTime)
			{
				this.m_dt = this.m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(this.m_subsystemTime.GameTime - this.m_nextUpdateTime), 0.1f);
				this.m_nextUpdateTime = this.m_subsystemTime.GameTime + (double)this.m_dt;
				this.m_stateMachine.Update();
			}
		}

		// MÉTODO NUEVO: Defender al jugador
		private void CheckDefendPlayer(float dt)
		{
			ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (herdBehavior == null || string.IsNullOrEmpty(herdBehavior.HerdName) ||
				!herdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			if (this.m_subsystemTime.GameTime < this.m_nextPlayerCheckTime)
			{
				return;
			}

			this.m_nextPlayerCheckTime = this.m_subsystemTime.GameTime + 1.0;

			foreach (ComponentPlayer player in this.m_subsystemPlayers.ComponentPlayers)
			{
				if (player.ComponentHealth.Health > 0f)
				{
					ComponentCreature attacker = FindPlayerAttacker(player);
					if (attacker != null && (this.m_target == null || this.m_target != attacker))
					{
						this.Attack(attacker, 20f, 30f, false);
					}
				}
			}
		}

		// MÉTODO NUEVO: Encontrar atacante del jugador
		private ComponentCreature FindPlayerAttacker(ComponentPlayer player)
		{
			Vector3 playerPosition = player.ComponentBody.Position;
			float searchRange = 20f;
			float searchRangeSquared = searchRange * searchRange;

			foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.Creatures)
			{
				if (creature.ComponentHealth.Health > 0f && creature != this.m_componentCreature)
				{
					if (Vector3.DistanceSquared(playerPosition, creature.ComponentBody.Position) < searchRangeSquared)
					{
						ComponentChaseBehavior chaseBehavior = creature.Entity.FindComponent<ComponentChaseBehavior>();
						if (chaseBehavior != null && chaseBehavior.Target == player)
						{
							ComponentNewHerdBehavior creatureHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
							if (creatureHerd == null ||
								!creatureHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
							{
								return creature;
							}
						}
					}
				}
			}

			return null;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			this.m_subsystemCreatureSpawn = base.Project.FindSubsystem<SubsystemCreatureSpawn>(true);

			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			this.m_componentFeedBehavior = base.Entity.FindComponent<ComponentRandomFeedBehavior>();
			this.m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			this.m_componentFactors = base.Entity.FindComponent<ComponentFactors>(true);

			this.m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			this.m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			this.m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			this.m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");

			// MODIFICACIÓN: Si no se puede obtener CreatureCategory, usar valores por defecto
			try
			{
				this.m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask");
			}
			catch
			{
				this.m_autoChaseMask = (CreatureCategory)0; // Valor por defecto
			}

			this.m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
			this.m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
			this.m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");

			ComponentBody componentBody = this.m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(componentBody.CollidedWithBody, new Action<ComponentBody>(delegate (ComponentBody body)
			{
				if (this.m_target == null && this.m_autoChaseSuppressionTime <= 0f && this.m_random.Float(0f, 1f) < this.m_chaseOnTouchProbability)
				{
					ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						bool flag = this.m_subsystemPlayers.IsPlayer(body.Entity);
						bool flag2 = (this.m_autoChaseMask != (CreatureCategory)0); // Verificación simplificada

						// VERIFICACIÓN NUEVA: Usar ComponentNewHerdBehavior
						ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
						bool canAttack = true;
						if (herdBehavior != null)
						{
							canAttack = herdBehavior.CanAttackCreature(componentCreature);
						}

						if (canAttack && ((this.AttacksPlayer && flag && this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) || (this.AttacksNonPlayerCreature && !flag && flag2)))
						{
							this.Attack(componentCreature, this.ChaseRangeOnTouch, this.ChaseTimeOnTouch, false);
						}
					}
				}
				if (this.m_target != null && this.JumpWhenTargetStanding && body == this.m_target.ComponentBody && body.StandingOnBody == this.m_componentCreature.ComponentBody)
				{
					this.m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
				}
			}));

			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				if (this.m_random.Float(0f, 1f) < this.m_chaseWhenAttackedProbability)
				{
					bool flag = false;
					float num;
					float num2;
					if (this.m_chaseWhenAttackedProbability >= 1f)
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
					num = this.ChaseRangeOnAttacked.GetValueOrDefault(num);
					num2 = this.ChaseTimeOnAttacked.GetValueOrDefault(num2);
					flag = this.ChasePersistentOnAttacked.GetValueOrDefault(flag);

					// VERIFICACIÓN NUEVA: Usar ComponentNewHerdBehavior
					ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
					if (herdBehavior == null || herdBehavior.CanAttackCreature(attacker))
					{
						this.Attack(attacker, num, num2, flag);
					}
				}
			}));

			this.m_stateMachine.AddState("LookingForTarget", delegate
			{
				this.m_importanceLevel = 0f;
				this.m_target = null;
			}, delegate
			{
				if (this.IsActive)
				{
					this.m_stateMachine.TransitionTo("Chasing");
					return;
				}
				if (!this.Suppressed && this.m_autoChaseSuppressionTime <= 0f && (this.m_target == null || this.ScoreTarget(this.m_target) <= 0f) && this.m_componentCreature.ComponentHealth.Health > this.MinHealthToAttackActively)
				{
					this.m_range = ((this.m_subsystemSky.SkyLightIntensity < 0.2f) ? this.m_nightChaseRange : this.m_dayChaseRange);
					this.m_range *= this.m_componentFactors.GetOtherFactorResult("ChaseRange", false, false);
					ComponentCreature componentCreature = this.FindTarget();
					if (componentCreature != null)
					{
						this.m_targetInRangeTime += this.m_dt;
					}
					else
					{
						this.m_targetInRangeTime = 0f;
					}
					if (this.m_targetInRangeTime > this.TargetInRangeTimeToChase)
					{
						bool flag = this.m_subsystemSky.SkyLightIntensity >= 0.1f;
						float maxRange = flag ? (this.m_dayChaseRange + 6f) : (this.m_nightChaseRange + 6f);
						float maxChaseTime = flag ? (this.m_dayChaseTime * this.m_random.Float(0.75f, 1f)) : (this.m_nightChaseTime * this.m_random.Float(0.75f, 1f));
						this.Attack(componentCreature, maxRange, maxChaseTime, !flag);
					}
				}
			}, null);

			this.m_stateMachine.AddState("RandomMoving", delegate
			{
				this.m_componentPathfinding.SetDestination(new Vector3?(this.m_componentCreature.ComponentBody.Position + new Vector3(6f * this.m_random.Float(-1f, 1f), 0f, 6f * this.m_random.Float(-1f, 1f))), 1f, 1f, 0, false, true, false, null);
			}, delegate
			{
				if (this.m_componentPathfinding.IsStuck || this.m_componentPathfinding.Destination == null)
				{
					this.m_stateMachine.TransitionTo("Chasing");
				}
				if (!this.IsActive)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
			}, delegate
			{
				this.m_componentPathfinding.Stop();
			});

			this.m_stateMachine.AddState("Chasing", delegate
			{
				this.m_subsystemNoise.MakeNoise(this.m_componentCreature.ComponentBody, 0.25f, 6f);
				if (this.PlayIdleSoundWhenStartToChase)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}
				this.m_nextUpdateTime = 0.0;
			}, delegate
			{
				if (!this.IsActive)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
				else if (this.m_chaseTime <= 0f)
				{
					this.m_autoChaseSuppressionTime = this.m_random.Float(10f, 60f);
					this.m_importanceLevel = 0f;
				}
				else if (this.m_target == null)
				{
					this.m_importanceLevel = 0f;
				}
				else if (this.m_target.ComponentHealth.Health <= 0f)
				{
					if (this.m_componentFeedBehavior != null)
					{
						this.m_subsystemTime.QueueGameTimeDelayedExecution(this.m_subsystemTime.GameTime + (double)this.m_random.Float(1f, 3f), delegate
						{
							if (this.m_target != null)
							{
								this.m_componentFeedBehavior.Feed(this.m_target.ComponentBody.Position);
							}
						});
					}
					this.m_importanceLevel = 0f;
				}
				else if (!this.m_isPersistent && this.m_componentPathfinding.IsStuck)
				{
					this.m_importanceLevel = 0f;
				}
				else if (this.m_isPersistent && this.m_componentPathfinding.IsStuck)
				{
					this.m_stateMachine.TransitionTo("RandomMoving");
				}
				else
				{
					if (this.ScoreTarget(this.m_target) <= 0f)
					{
						this.m_targetUnsuitableTime += this.m_dt;
					}
					else
					{
						this.m_targetUnsuitableTime = 0f;
					}
					if (this.m_targetUnsuitableTime > 3f)
					{
						this.m_importanceLevel = 0f;
					}
					else
					{
						int maxPathfindingPositions = 0;
						if (this.m_isPersistent)
						{
							maxPathfindingPositions = ((this.m_subsystemTime.FixedTimeStep != null) ? 2000 : 500);
						}
						BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
						BoundingBox boundingBox2 = this.m_target.ComponentBody.BoundingBox;
						Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
						Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max);
						float num = Vector3.Distance(v, vector);
						float num2 = (num < 4f) ? 0.2f : 0f;
						this.m_componentPathfinding.SetDestination(new Vector3?(vector + num2 * num * this.m_target.ComponentBody.Velocity), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
						if (this.PlayAngrySoundWhenChasing && this.m_random.Float(0f, 1f) < 0.33f * this.m_dt)
						{
							this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						}
					}
				}
			}, null);

			this.m_stateMachine.TransitionTo("LookingForTarget");
		}

		public virtual ComponentCreature FindTarget()
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentCreature result = null;
			float num = 0f;
			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), this.m_range, this.m_componentBodies);
			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentCreature componentCreature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (componentCreature != null)
				{
					// VERIFICACIÓN NUEVA: Usar ComponentNewHerdBehavior
					ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
					if (herdBehavior != null)
					{
						if (!herdBehavior.CanAttackCreature(componentCreature))
						{
							continue; // Saltar si no podemos atacar
						}
					}

					float num2 = this.ScoreTarget(componentCreature);
					if (num2 > num)
					{
						num = num2;
						result = componentCreature;
					}
				}
			}
			return result;
		}

		public virtual float ScoreTarget(ComponentCreature componentCreature)
		{
			float score = 0f;
			bool flag = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
			bool flag2 = true; // Simplificado: asumir que puede perseguir en tierra
			bool flag3 = componentCreature == this.Target || this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
			bool flag4 = (this.m_autoChaseMask != (CreatureCategory)0); // Verificación simplificada
			bool flag5 = componentCreature == this.Target || (flag4 && MathUtils.Remainder(0.004999999888241291 * this.m_subsystemTime.GameTime + (double)((float)(this.GetHashCode() % 1000) / 1000f) + (double)((float)(componentCreature.GetHashCode() % 1000) / 1000f), 1.0) < (double)this.m_chaseNonPlayerProbability);

			if (componentCreature != this.m_componentCreature && ((!flag && flag5) || (flag && flag3)) && componentCreature.Entity.IsAddedToProject && componentCreature.ComponentHealth.Health > 0f)
			{
				float num = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, componentCreature.ComponentBody.Position);
				if (num < this.m_range)
				{
					score = this.m_range - num;
				}
			}

			return score;
		}

		public virtual bool IsTargetInWater(ComponentBody target)
		{
			return target.ImmersionDepth > 0f || (target.ParentBody != null && this.IsTargetInWater(target.ParentBody)) || (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && this.IsTargetInWater(target.StandingOnBody));
		}

		public virtual bool IsTargetInAttackRange(ComponentBody target)
		{
			if (this.IsBodyInAttackRange(target))
			{
				return true;
			}
			BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox boundingBox2 = target.BoundingBox;
			Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
			Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max) - v;
			float num = vector.Length();
			Vector3 v2 = vector / num;
			float num2 = 0.5f * (boundingBox.Max.X - boundingBox.Min.X + boundingBox2.Max.X - boundingBox2.Min.X);
			float num3 = 0.5f * (boundingBox.Max.Y - boundingBox.Min.Y + boundingBox2.Max.Y - boundingBox2.Min.Y);
			if (MathF.Abs(vector.Y) < num3 * 0.99f)
			{
				if (num < num2 + 0.99f && Vector3.Dot(v2, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
				{
					return true;
				}
			}
			else if (num < num3 + 0.3f && MathF.Abs(Vector3.Dot(v2, Vector3.UnitY)) > 0.8f)
			{
				return true;
			}
			return (target.ParentBody != null && this.IsTargetInAttackRange(target.ParentBody)) || (this.AllowAttackingStandingOnBody && target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && this.IsTargetInAttackRange(target.StandingOnBody));
		}

		public virtual bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox boundingBox2 = target.BoundingBox;
			Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
			Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max) - v;
			float num = vector.Length();
			Vector3 v2 = vector / num;
			float num2 = 0.5f * (boundingBox.Max.X - boundingBox.Min.X + boundingBox2.Max.X - boundingBox2.Min.X);
			float num3 = 0.5f * (boundingBox.Max.Y - boundingBox.Min.Y + boundingBox2.Max.Y - boundingBox2.Min.Y);
			if (MathF.Abs(vector.Y) < num3 * 0.99f)
			{
				if (num < num2 + 0.99f && Vector3.Dot(v2, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
				{
					return true;
				}
			}
			else if (num < num3 + 0.3f && MathF.Abs(Vector3.Dot(v2, Vector3.UnitY)) > 0.8f)
			{
				return true;
			}
			return false;
		}

		public virtual ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 vector = this.m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 v = target.BoundingBox.Center();
			Ray3 ray = new Ray3(vector, Vector3.Normalize(v - vector));
			BodyRaycastResult? bodyRaycastResult = this.m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			if (bodyRaycastResult != null && bodyRaycastResult.Value.Distance < this.MaxAttackRange && (bodyRaycastResult.Value.ComponentBody == target || bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) || target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody) || (target.StandingOnBody == bodyRaycastResult.Value.ComponentBody && this.AllowAttackingStandingOnBody)))
			{
				hitPoint = bodyRaycastResult.Value.HitPoint();
				return bodyRaycastResult.Value.ComponentBody;
			}
			hitPoint = default(Vector3);
			return null;
		}

		// Campos (igual que el original)
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemPlayers m_subsystemPlayers;
		public SubsystemSky m_subsystemSky;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemTime m_subsystemTime;
		public SubsystemNoise m_subsystemNoise;
		public SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public ComponentMiner m_componentMiner;
		public ComponentRandomFeedBehavior m_componentFeedBehavior;
		public ComponentCreatureModel m_componentCreatureModel;
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		public Random m_random = new Random();
		public StateMachine m_stateMachine = new StateMachine();
		public ComponentFactors m_componentFactors;

		public float m_dayChaseRange;
		public float m_nightChaseRange;
		public float m_dayChaseTime;
		public float m_nightChaseTime;
		public float m_chaseNonPlayerProbability;
		public float m_chaseWhenAttackedProbability;
		public float m_chaseOnTouchProbability;
		public CreatureCategory m_autoChaseMask;

		public float m_importanceLevel;
		public float m_targetUnsuitableTime;
		public float m_targetInRangeTime;
		public double m_nextUpdateTime;
		public double m_nextPlayerCheckTime; // NUEVO: Para controlar chequeos de defensa
		public ComponentCreature m_target;
		public float m_dt;
		public float m_range;
		public float m_chaseTime;
		public bool m_isPersistent;
		public float m_autoChaseSuppressionTime;

		// Propiedades públicas (igual que el original)
		public float ImportanceLevelNonPersistent = 200f;
		public float ImportanceLevelPersistent = 200f;
		public float MaxAttackRange = 1.75f;
		public bool AllowAttackingStandingOnBody = true;
		public bool JumpWhenTargetStanding = true;
		public bool AttacksPlayer = true;
		public bool AttacksNonPlayerCreature = true;
		public float ChaseRangeOnTouch = 7f;
		public float ChaseTimeOnTouch = 7f;
		public float? ChaseRangeOnAttacked;
		public float? ChaseTimeOnAttacked;
		public bool? ChasePersistentOnAttacked;
		public float MinHealthToAttackActively = 0.4f;
		public bool Suppressed;
		public bool PlayIdleSoundWhenStartToChase = true;
		public bool PlayAngrySoundWhenChasing = true;
		public float TargetInRangeTimeToChase = 3f;
	}
}
