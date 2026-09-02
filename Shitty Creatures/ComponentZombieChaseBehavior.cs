using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using static Game.SubsystemGreenNightSky;

namespace Game
{
	public class ComponentZombieChaseBehavior : ComponentBehavior, IUpdateable, INoiseAttractListener
	{
		// Propiedades
		public bool ForceAttackDuringGreenNight => this.m_forceAttackDuringGreenNight;
		public bool Suppressed
		{
			get => this.m_suppressed;
			set => this.m_suppressed = value;
		}
		public ComponentCreature Target => this.m_target;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => this.m_importanceLevel;
		public string CurrentState => this.m_stateMachine?.CurrentState;

		// Campos copiados de ComponentChaseBehavior
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemSky m_subsystemSky;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTime m_subsystemTime;
		private SubsystemNoise m_subsystemNoise;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentMiner m_componentMiner;
		private ComponentRandomFeedBehavior m_componentFeedBehavior;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentFactors m_componentFactors;
		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		private Random m_random = new Random();
		private StateMachine m_stateMachine = new StateMachine();

		private float m_dayChaseRange;
		private float m_nightChaseRange;
		private float m_dayChaseTime;
		private float m_nightChaseTime;
		private float m_chaseNonPlayerProbability;
		private float m_chaseWhenAttackedProbability;
		private float m_chaseOnTouchProbability;
		private CreatureCategory m_autoChaseMask;

		private float m_importanceLevel;
		private float m_targetUnsuitableTime;
		private float m_targetInRangeTime;
		private double m_nextUpdateTime;
		private ComponentCreature m_target;
		private float m_dt;
		private float m_range;
		private float m_chaseTime;
		private bool m_isPersistent;
		private float m_autoChaseSuppressionTime;

		// Campos de configuración
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
		private bool m_suppressed;
		public bool PlayIdleSoundWhenStartToChase = true;
		public bool PlayAngrySoundWhenChasing = true;
		public float TargetInRangeTimeToChase = 3f;

		// Campos específicos del zombie
		private static readonly HashSet<string> s_excludedMountNames = new HashSet<string>
		{
			"Horse_Black_Saddled",
			"Horse_Palomino_Saddled",
			"Camel_Saddled",
			"Horse_Chestnut_Saddled",
			"Horse_White_Saddled",
			"Donkey_Saddled",
			"Horse_Bay_Saddled"
		};

		private Vector3 m_attractPosition;
		private float m_investigationTimeRemaining = 0f;
		private ComponentPathfinding m_zombiePathfinding;

		private string m_stateBeforeNoise;
		private ComponentCreature m_targetBeforeNoise;
		private float m_chaseTimeBeforeNoise;
		private bool m_wasPersistentBeforeNoise;
		private float m_rangeBeforeNoise;

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
		private DifficultyMode m_currentDifficulty;
		private float m_baseRange;

		// ==========================================
		// MÉTODO LOAD (completo sin base)
		// ==========================================
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Inicialización de subsistemas (copiado de ComponentChaseBehavior)
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			this.m_componentFeedBehavior = base.Entity.FindComponent<ComponentRandomFeedBehavior>();
			this.m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			this.m_componentFactors = base.Entity.FindComponent<ComponentFactors>(true);

			// Cargar valores de configuración
			this.m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			this.m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			this.m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			this.m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");
			this.m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask");
			this.m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
			this.m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
			this.m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");

			// Cargar valores específicos del zombie
			this.m_componentZombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
			this.m_subsystemGreenNightSky = base.Project.FindSubsystem<SubsystemGreenNightSky>(true);
			this.m_attacksSameHerd = valuesDictionary.GetValue<bool>("AttacksSameHerd", false);
			this.m_attacksAllCategories = valuesDictionary.GetValue<bool>("AttacksAllCategories", true);
			this.m_fleeFromSameHerd = valuesDictionary.GetValue<bool>("FleeFromSameHerd", true);
			this.m_fleeDistance = valuesDictionary.GetValue<float>("FleeDistance", 10f);
			this.m_forceAttackDuringGreenNight = valuesDictionary.GetValue<bool>("ForceAttackDuringGreenNight", false);

			this.m_zombiePathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);

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

			// Registrar eventos (copiados de ComponentChaseBehavior)
			ComponentBody componentBody = this.m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(componentBody.CollidedWithBody, new Action<ComponentBody>(delegate (ComponentBody body)
			{
				if (this.m_target == null && this.m_autoChaseSuppressionTime <= 0f && this.m_random.Float(0f, 1f) < this.m_chaseOnTouchProbability)
				{
					ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						bool flag = this.m_subsystemPlayers.IsPlayer(body.Entity);
						bool flag2 = (componentCreature.Category & this.m_autoChaseMask) > (CreatureCategory)0;
						if ((this.AttacksPlayer && flag && this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) || (this.AttacksNonPlayerCreature && !flag && flag2))
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

			// Configurar manejo de heridas (usando el método propio del zombie)
			this.SetupZombieInjuryHandler();

			// Configurar estados
			this.AddFleeState();
			this.AddNoiseAttractionStates();

			// Estados base (copiados de ComponentChaseBehavior)
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
				if (!this.m_suppressed && this.m_autoChaseSuppressionTime <= 0f && (this.m_target == null || this.ScoreTarget(this.m_target) <= 0f) && this.m_componentCreature.ComponentHealth.Health > this.MinHealthToAttackActively)
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

			// Configuración específica de GreenNight
			this.m_previousGreenNightActive = false;
			m_defaultTargetInRangeTime = this.TargetInRangeTimeToChase;

			if (this.m_forceAttackDuringGreenNight && this.m_subsystemGreenNightSky != null && this.m_subsystemGreenNightSky.IsGreenNightActive)
			{
				this.TargetInRangeTimeToChase = 0f;
				this.m_targetInRangeTime = this.TargetInRangeTimeToChase + 1f;
			}
			m_baseRange = this.m_range;
			if (m_subsystemGreenNightSky != null)
			{
				m_currentDifficulty = m_subsystemGreenNightSky.DifficultyMode;
				ApplyDifficultyToChase();
			}
		}

		// ==========================================
		// MÉTODO UPDATE (completo sin base)
		// ==========================================
		public void Update(float dt)
		{
			if (this.m_suppressed)
			{
				this.StopAttack();
			}

			// Si está en estados de ruido, solo actualizar la máquina de estados
			string currentState = this.m_stateMachine?.CurrentState;
			if (currentState == "AttractedToNoise" || currentState == "InvestigatingNoise")
			{
				this.m_dt = dt;
				this.m_stateMachine.Update();
				return;
			}

			// Lógica de Update copiada de ComponentChaseBehavior
			if (this.m_suppressed)
			{
				this.StopAttack();
			}
			this.m_autoChaseSuppressionTime -= dt;
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
						this.m_componentMiner.Hit(hitBody, hitPoint, this.m_componentCreature.ComponentBody.Matrix.Forward);
						this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
					}
				}
			}

			if (this.m_subsystemTime.GameTime >= this.m_nextUpdateTime)
			{
				this.m_dt = this.m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(this.m_subsystemTime.GameTime - this.m_nextUpdateTime), 0.1f);
				this.m_nextUpdateTime = this.m_subsystemTime.GameTime + (double)this.m_dt;
				this.m_stateMachine.Update();
			}

			// Lógica específica del zombie
			if (m_subsystemGreenNightSky != null)
			{
				ApplyDifficultyToChase();
			}

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
					if (kvp.Key == this.m_lastAttacker) this.m_lastAttacker = null;
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

			bool greenNightActive = this.m_forceAttackDuringGreenNight &&
									this.m_subsystemGreenNightSky != null &&
									this.m_subsystemGreenNightSky.IsGreenNightActive;

			if (greenNightActive != this.m_previousGreenNightActive)
			{
				if (greenNightActive && !this.m_previousGreenNightActive)
				{
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
							this.m_stateMachine.TransitionTo("LookingForTarget");
					}
					this.AttacksPlayer = this.m_attacksAllCategories;
				}
				this.m_previousGreenNightActive = greenNightActive;
			}

			if (greenNightActive)
			{
				this.AttacksPlayer = true;
				this.m_suppressed = false;
				this.TargetInRangeTimeToChase = 0f;
				this.m_targetInRangeTime = 1f;
				if (this.m_stateMachine.CurrentState == "Fleeing") this.m_stateMachine.TransitionTo("LookingForTarget");
				if (!this.m_isRetaliating && this.m_target == null)
				{
					ComponentPlayer nearestPlayer = this.FindNearestPlayer(this.m_range);
					if (nearestPlayer != null) this.Attack(nearestPlayer, this.m_range, 120f, true);
				}
			}
			else
			{
				if (this.m_subsystemGreenNightSky != null && !this.m_subsystemGreenNightSky.IsGreenNightActive)
					this.TargetInRangeTimeToChase = m_defaultTargetInRangeTime;

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
									   Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_retaliationTarget.ComponentBody.Position) <= this.m_range * 2f &&
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

		// ==========================================
		// MÉTODOS DE ATAQUE (copiados y modificados)
		// ==========================================
		public virtual void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (this.m_suppressed)
				return;

			// Salir de estados de ruido
			ExitNoiseAttractionStates();

			bool isRetaliating = this.m_isRetaliating && componentCreature == this.m_retaliationTarget;
			bool isSameHerdTarget = !isRetaliating && !this.m_attacksSameHerd && this.IsSameHerd(componentCreature);

			if (isSameHerdTarget)
			{
				if (this.m_componentZombieHerdBehavior != null)
				{
					ComponentCreature externalEnemy = this.FindExternalEnemyNearby(maxRange);
					if (externalEnemy != null) this.m_componentZombieHerdBehavior.CoordinateGroupAttack(externalEnemy);
				}
			}
			else
			{
				if (isRetaliating)
				{
					this.m_suppressed = false;
					this.ImportanceLevelNonPersistent = 500f;
					this.ImportanceLevelPersistent = 500f;
					this.m_autoChaseSuppressionTime = 0f;
				}

				bool isGreenNightActive = this.m_forceAttackDuringGreenNight && this.m_subsystemGreenNightSky != null && this.m_subsystemGreenNightSky.IsGreenNightActive;
				if (isGreenNightActive && !isRetaliating)
				{
					if (componentCreature.Entity != null && componentCreature.Entity.FindComponent<ComponentPlayer>() == null)
					{
						ComponentPlayer nearestPlayer = this.FindNearestPlayer(maxRange);
						if (nearestPlayer != null) componentCreature = nearestPlayer;
					}
				}

				// Lógica copiada de ComponentChaseBehavior.Attack
				this.m_target = componentCreature;
				this.m_nextUpdateTime = 0.0;
				this.m_range = maxRange;
				this.m_chaseTime = maxChaseTime;
				this.m_isPersistent = isPersistent;
				this.m_importanceLevel = (isPersistent ? this.ImportanceLevelPersistent : this.ImportanceLevelNonPersistent);

				if (!isRetaliating && this.m_componentZombieHerdBehavior != null)
					this.m_componentZombieHerdBehavior.CoordinateGroupAttack(componentCreature);
			}
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

		// ==========================================
		// MÉTODOS DE BÚSQUEDA (copiados y modificados)
		// ==========================================
		public virtual ComponentCreature FindTarget()
		{
			ComponentCreature retaliationTarget = this.GetNextRetaliationTarget();
			if (retaliationTarget != null)
			{
				bool shouldExcludeMounts = (m_currentDifficulty == DifficultyMode.Hard || m_currentDifficulty == DifficultyMode.Extreme);
				if (shouldExcludeMounts)
				{
					string name = retaliationTarget.Entity.ValuesDictionary.DatabaseObject.Name;
					if (!s_excludedMountNames.Contains(name)) return retaliationTarget;
				}
				else return retaliationTarget;
			}

			bool isGreenNightActive = this.m_forceAttackDuringGreenNight && this.m_subsystemGreenNightSky != null && this.m_subsystemGreenNightSky.IsGreenNightActive;
			if (isGreenNightActive)
			{
				ComponentPlayer nearestPlayer = this.FindNearestPlayer(this.m_range);
				if (nearestPlayer != null) return nearestPlayer;
			}

			if (!this.m_attacksSameHerd)
			{
				Vector3 position = this.m_componentCreature.ComponentBody.Position;
				ComponentCreature bestTarget = null;
				float bestScore = 0f;

				this.m_componentBodies.Clear();
				this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), this.m_range, this.m_componentBodies);
				bool shouldExcludeMounts = (m_currentDifficulty == DifficultyMode.Hard || m_currentDifficulty == DifficultyMode.Extreme);

				for (int i = 0; i < this.m_componentBodies.Count; i++)
				{
					ComponentCreature candidate = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
					if (candidate != null && !this.IsSameHerd(candidate))
					{
						if (shouldExcludeMounts)
						{
							string templateName = candidate.Entity.ValuesDictionary.DatabaseObject.Name;
							if (!string.IsNullOrEmpty(templateName) && s_excludedMountNames.Contains(templateName)) continue;
						}
						float score = this.ScoreTarget(candidate);
						if (score > bestScore) { bestScore = score; bestTarget = candidate; }
					}
				}
				return bestTarget;
			}

			// Versión copiada de ComponentChaseBehavior.FindTarget
			Vector3 position2 = this.m_componentCreature.ComponentBody.Position;
			ComponentCreature result = null;
			float num = 0f;
			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position2.X, position2.Z), this.m_range, this.m_componentBodies);
			for (int j = 0; j < this.m_componentBodies.Count; j++)
			{
				ComponentCreature componentCreature = this.m_componentBodies.Array[j].Entity.FindComponent<ComponentCreature>();
				if (componentCreature != null)
				{
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
			if (componentCreature != null)
			{
				bool shouldExcludeMounts = (m_currentDifficulty == DifficultyMode.Hard || m_currentDifficulty == DifficultyMode.Extreme);
				if (shouldExcludeMounts)
				{
					string templateName = componentCreature.Entity.ValuesDictionary.DatabaseObject.Name;
					if (!string.IsNullOrEmpty(templateName) && s_excludedMountNames.Contains(templateName)) return 0f;
				}
			}
			if (!this.m_attacksSameHerd && this.IsSameHerd(componentCreature)) return 0f;

			// Versión copiada de ComponentChaseBehavior.ScoreTarget
			float score = 0f;
			bool flag = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
			bool flag2 = this.m_componentCreature.Category != CreatureCategory.WaterPredator && this.m_componentCreature.Category != CreatureCategory.WaterOther;
			bool flag3 = componentCreature == this.m_target || this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
			bool flag4 = (componentCreature.Category & this.m_autoChaseMask) > (CreatureCategory)0;
			bool flag5 = componentCreature == this.m_target || (flag4 && MathUtils.Remainder(0.004999999888241291 * this.m_subsystemTime.GameTime + (double)((float)(this.GetHashCode() % 1000) / 1000f) + (double)((float)(componentCreature.GetHashCode() % 1000) / 1000f), 1.0) < (double)this.m_chaseNonPlayerProbability);

			if (componentCreature != this.m_componentCreature && ((!flag && flag5) || (flag && flag3)) && componentCreature.Entity.IsAddedToProject && componentCreature.ComponentHealth.Health > 0f && (flag2 || this.IsTargetInWater(componentCreature.ComponentBody)))
			{
				float num = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, componentCreature.ComponentBody.Position);
				if (num < this.m_range)
				{
					score = this.m_range - num;
				}
			}

			// Modificaciones específicas del zombie (retaliación)
			if (this.m_retaliationQueue.Contains(componentCreature) && this.m_lastAttackTimes.ContainsKey(componentCreature) && this.m_lastAttackTimes[componentCreature] > 0f)
				return score * 10f;
			if (componentCreature == this.m_lastAttacker && this.m_lastAttackTimes.ContainsKey(componentCreature) && this.m_lastAttackTimes[componentCreature] > 0f)
				return score * 8f;

			return score;
		}

		// ==========================================
		// MÉTODOS AUXILIARES (copiados de ComponentChaseBehavior)
		// ==========================================
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

		// ==========================================
		// MÉTODO DE ATRACCIÓN POR RUIDO (INoiseAttractListener)
		// ==========================================
		public void AttractedToNoise(ComponentBody sourceBody, Vector3 sourcePosition, float lureStrength)
		{
			m_attractPosition = sourcePosition;
			string currentState = this.m_stateMachine?.CurrentState;

			if (currentState == "AttractedToNoise")
			{
				if (m_zombiePathfinding != null)
				{
					m_zombiePathfinding.SetDestination(m_attractPosition, 1f, 1f, 10, true, false, false, null);
				}
				return;
			}

			if (currentState == "InvestigatingNoise")
			{
				return;
			}

			m_stateBeforeNoise = !string.IsNullOrEmpty(currentState) ? currentState : "LookingForTarget";
			m_targetBeforeNoise = m_target;
			m_chaseTimeBeforeNoise = m_chaseTime;
			m_wasPersistentBeforeNoise = m_isPersistent;
			m_rangeBeforeNoise = m_range;

			this.m_stateMachine.TransitionTo("AttractedToNoise");
		}

		// ==========================================
		// ESTADOS DE ATRACCIÓN POR RUIDO
		// ==========================================
		private void AddNoiseAttractionStates()
		{
			this.m_stateMachine.AddState("AttractedToNoise",
				delegate
				{
					m_target = null;
					this.IsActive = false;
					this.m_range = 0f;
					this.m_chaseTime = 0f;
					this.m_isPersistent = false;
					this.m_importanceLevel = 0f;
					this.m_nextUpdateTime = 0.0;
					this.m_componentCreatureModel.AttackOrder = false;
					this.m_componentCreature.ComponentCreatureModel.LookAtOrder = null;

					if (m_zombiePathfinding != null && m_componentCreature.ComponentBody != null)
					{
						m_zombiePathfinding.Stop();
						m_zombiePathfinding.SetDestination(m_attractPosition, 1f, 1f, 10, true, false, false, null);
					}
				},
				delegate
				{
					if (m_zombiePathfinding != null && m_componentCreature.ComponentBody != null)
					{
						float distToAttract = Vector3.Distance(m_componentCreature.ComponentBody.Position, m_attractPosition);

						if (distToAttract <= 2f)
						{
							m_zombiePathfinding.Stop();
							m_stateMachine.TransitionTo("InvestigatingNoise");
						}
						else if (m_zombiePathfinding.Destination == null || m_zombiePathfinding.IsStuck)
						{
							m_zombiePathfinding.SetDestination(m_attractPosition, 1f, 1f, 10, true, false, false, null);
						}
					}
				},
				delegate
				{
					if (m_zombiePathfinding != null)
					{
						m_zombiePathfinding.Stop();
					}
				}
			);

			this.m_stateMachine.AddState("InvestigatingNoise",
				delegate
				{
					m_investigationTimeRemaining = 2.5f;
				},
				delegate
				{
					m_investigationTimeRemaining -= m_dt;

					if (m_investigationTimeRemaining <= 0f)
					{
						bool resumedChase = TryResumePreviousChase();

						if (!resumedChase)
						{
							m_stateMachine.TransitionTo("LookingForTarget");
						}

						m_targetBeforeNoise = null;
						m_stateBeforeNoise = null;
					}
				},
				delegate
				{
					m_investigationTimeRemaining = 0f;
				}
			);
		}

		private bool TryResumePreviousChase()
		{
			if (m_stateBeforeNoise != "Chasing" || m_targetBeforeNoise == null)
			{
				return false;
			}

			if (m_targetBeforeNoise.ComponentHealth == null || m_targetBeforeNoise.ComponentHealth.Health <= 0f)
			{
				return false;
			}

			float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position, m_targetBeforeNoise.ComponentBody.Position);
			float maxResumeRange = m_rangeBeforeNoise * 1.3f;

			if (dist > maxResumeRange)
			{
				return false;
			}

			m_target = m_targetBeforeNoise;
			m_chaseTime = MathUtils.Max(m_chaseTimeBeforeNoise - 3f, 2f);
			m_isPersistent = m_wasPersistentBeforeNoise;
			m_range = m_rangeBeforeNoise;
			m_importanceLevel = m_isPersistent ? this.ImportanceLevelPersistent : this.ImportanceLevelNonPersistent;
			this.IsActive = true;

			m_stateMachine.TransitionTo("Chasing");
			return true;
		}

		private void ExitNoiseAttractionStates()
		{
			string currentState = this.m_stateMachine?.CurrentState;
			if (currentState == "AttractedToNoise" || currentState == "InvestigatingNoise")
			{
				m_targetBeforeNoise = null;
				m_stateBeforeNoise = null;
				m_investigationTimeRemaining = 0f;

				m_stateMachine.TransitionTo("LookingForTarget");
			}
		}

		// ==========================================
		// MANEJO DE HERIDAS Y RETALIACIÓN
		// ==========================================
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

					while (this.m_retaliationQueue.Count > 5) this.m_retaliationQueue.RemoveAt(0);

					bool shouldAttackAttacker = !this.IsSameHerd(attacker) || this.m_attacksSameHerd;

					if (shouldAttackAttacker)
					{
						if (this.m_target != attacker)
						{
							ExitNoiseAttractionStates();

							bool isGreenNightActive = this.m_forceAttackDuringGreenNight && this.m_subsystemGreenNightSky != null && this.m_subsystemGreenNightSky.IsGreenNightActive;
							float chaseTime = isGreenNightActive ? 120f : 60f;

							this.Attack(attacker, 40f, chaseTime, true);
							this.m_retaliationCooldown = 1f;
							this.m_isRetaliating = true;
							this.m_retaliationTarget = attacker;
						}
					}

					if (!this.IsSameHerd(attacker) && this.m_componentZombieHerdBehavior != null && this.m_componentZombieHerdBehavior.CallForHelpWhenAttacked)
						this.m_componentZombieHerdBehavior.CallZombiesForHelp(attacker);

					if (attacker != null && !this.m_attacksSameHerd && this.IsSameHerd(attacker))
					{
						if (this.m_componentZombieHerdBehavior != null && this.m_componentZombieHerdBehavior.CallForHelpWhenAttacked)
						{
							ComponentCreature externalAttacker = this.FindExternalAttacker(injury);
							if (externalAttacker != null) this.m_componentZombieHerdBehavior.CallZombiesForHelp(externalAttacker);
						}
						if (this.m_fleeFromSameHerd) this.FleeFromTarget(attacker);
					}
				}
			}));
		}

		private ComponentCreature FindExternalAttacker(Injury injury)
		{
			return (!this.IsSameHerd(injury.Attacker)) ? injury.Attacker : null;
		}

		private bool IsSameHerd(ComponentCreature otherCreature)
		{
			return otherCreature != null && this.m_componentZombieHerdBehavior != null && this.m_componentZombieHerdBehavior.IsSameZombieHerd(otherCreature);
		}

		// ==========================================
		// ESTADO DE HUÍDA
		// ==========================================
		private void AddFleeState()
		{
			this.m_stateMachine.AddState("Fleeing", delegate
			{
				this.m_importanceLevel = 150f;
				this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
			}, delegate
			{
				if (this.m_target == null || this.m_componentCreature.ComponentHealth.Health <= 0f)
					this.m_stateMachine.TransitionTo("LookingForTarget");
				else
				{
					Vector3 v = this.m_componentCreature.ComponentBody.Position - this.m_target.ComponentBody.Position;
					if (v.LengthSquared() > 0.01f)
					{
						v = Vector3.Normalize(v);
						this.m_componentPathfinding.SetDestination(new Vector3?(this.m_componentCreature.ComponentBody.Position + v * this.m_fleeDistance), 1f, 1.5f, 0, false, true, false, null);
					}
					float dist = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_target.ComponentBody.Position);
					if (dist > this.m_fleeDistance * 1.5f)
						this.m_stateMachine.TransitionTo("LookingForTarget");
					if (this.m_random.Float(0f, 1f) < 0.05f * this.m_dt)
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
			if (m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.DifficultyMode >= DifficultyMode.Hard)
			{
				if (target != null && !m_isRetaliating)
				{
					this.Attack(target, this.m_range, 60f, true);
					m_isRetaliating = true;
					m_retaliationTarget = target;
				}
				return;
			}
			if (target != null && this.m_componentCreature.ComponentHealth.Health > 0f)
			{
				this.m_target = target;
				this.m_stateMachine.TransitionTo("Fleeing");
			}
		}

		// ==========================================
		// MÉTODOS DE DIFICULTAD Y UTILIDADES
		// ==========================================
		private void ApplyDifficultyToChase()
		{
			if (m_subsystemGreenNightSky == null) return;
			DifficultyMode mode = m_subsystemGreenNightSky.DifficultyMode;
			if (mode == m_currentDifficulty) return;
			m_currentDifficulty = mode;
			float rangeMult = SubsystemGreenNightSky.DifficultyModifiers.GetAggressionRangeMultiplier(mode);
			this.m_range = m_baseRange * rangeMult;
		}

		private ComponentCreature FindExternalEnemyNearby(float range)
		{
			if (this.m_componentCreature == null || this.m_componentCreature.ComponentBody == null) return null;
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
					if (score > bestScore) { bestScore = score; bestTarget = creature; }
				}
			}
			return bestTarget;
		}

		private ComponentPlayer FindNearestPlayer(float range)
		{
			if (this.m_componentCreature == null || this.m_componentCreature.ComponentBody == null) return null;
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
						if (dist <= range && dist < minDist) { minDist = dist; nearestPlayer = player; }
					}
				}
			}
			return nearestPlayer;
		}

		private ComponentCreature GetNextRetaliationTarget()
		{
			for (int i = this.m_retaliationQueue.Count - 1; i >= 0; i--)
			{
				ComponentCreature attacker = this.m_retaliationQueue[i];
				if (attacker == null || attacker.ComponentHealth.Health <= 0f || !this.m_lastAttackTimes.ContainsKey(attacker) || this.m_lastAttackTimes[attacker] <= 0f)
					this.m_retaliationQueue.RemoveAt(i);
			}
			if (this.m_retaliationQueue.Count > 0)
			{
				ComponentCreature latestAttacker = this.m_retaliationQueue[this.m_retaliationQueue.Count - 1];
				bool shouldExcludeMounts = (m_currentDifficulty == DifficultyMode.Hard || m_currentDifficulty == DifficultyMode.Extreme);
				if (shouldExcludeMounts)
				{
					string name = latestAttacker.Entity.ValuesDictionary.DatabaseObject.Name;
					if (!string.IsNullOrEmpty(name) && s_excludedMountNames.Contains(name)) return null;
				}
				bool isValid = (!this.IsSameHerd(latestAttacker) || this.m_attacksSameHerd) && Vector3.Distance(this.m_componentCreature.ComponentBody.Position, latestAttacker.ComponentBody.Position) <= this.m_range * 2f;
				if (isValid) return latestAttacker;
			}
			return null;
		}
	}
}
