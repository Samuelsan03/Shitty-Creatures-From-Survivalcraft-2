using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieChaseBehavior : ComponentBehavior, IUpdateable
	{
		// Propiedades públicas
		public ComponentCreature Target
		{
			get { return m_target; }
		}

		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

		public override float ImportanceLevel
		{
			get { return m_importanceLevel; }
		}

		public bool AttacksSameHerd { get; set; } = false;
		public bool AttacksAllCategories { get; set; } = false; // CAMBIADO: false por defecto
		public bool FleeFromSameHerd { get; set; } = true;
		public float FleeDistance { get; set; } = 10f;
		public bool ForceAttackDuringGreenNight { get; set; } = true;
		public bool Suppressed { get; set; } = false;

		// Campos privados
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
		private ComponentZombieHerdBehavior m_componentZombieHerdBehavior;
		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		private Game.Random m_random = new Game.Random();
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

		// Configuración
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
		public bool PlayIdleSoundWhenStartToChase = true;
		public bool PlayAngrySoundWhenChasing = true;
		public float TargetInRangeTimeToChase = 3f;

		// Para Noche Verde
		private bool m_lastGreenNightState = false;
		private ComponentCreature m_greenNightTargetPlayer = null;

		public virtual void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (this.Suppressed)
			{
				return;
			}

			// Verificar si el objetivo es de la misma manada (si aplica)
			if (!this.AttacksSameHerd && componentCreature != null && this.m_componentZombieHerdBehavior != null)
			{
				if (this.m_componentZombieHerdBehavior.IsSameZombieHerd(componentCreature))
				{
					// Si no ataca a la misma manada y el objetivo es de la misma manada, huir
					if (this.FleeFromSameHerd)
					{
						this.FleeFromTarget(componentCreature);
					}
					return;
				}
			}

			this.m_target = componentCreature;
			this.m_nextUpdateTime = 0.0;
			this.m_range = maxRange;
			this.m_chaseTime = maxChaseTime;
			this.m_isPersistent = isPersistent;
			this.m_importanceLevel = (isPersistent ? this.ImportanceLevelPersistent : this.ImportanceLevelNonPersistent);

			// Durante Noche Verde, asegurar que el ataque sea persistente y con mayor prioridad
			if (SubsystemGreenNightSky.IsGreenNight() && this.ForceAttackDuringGreenNight)
			{
				this.m_isPersistent = true;
				this.m_importanceLevel = 500f; // Prioridad muy alta
				this.m_chaseTime = MathUtils.Max(this.m_chaseTime, 60f); // Mínimo 60 segundos

				// Si es un jugador, guardarlo como objetivo de Noche Verde
				if (componentCreature != null && componentCreature.Entity.FindComponent<ComponentPlayer>() != null)
				{
					m_greenNightTargetPlayer = componentCreature;
				}
			}

			// NO usar hooks de ModsManager ya que ComponentZombieChaseBehavior no es ComponentChaseBehavior
			// Los mods no tienen hooks específicos para esta clase
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

			// Limpiar objetivo de Noche Verde si estamos saliendo de ese estado
			if (!SubsystemGreenNightSky.IsGreenNight())
			{
				m_greenNightTargetPlayer = null;
			}
		}

		public virtual void Update(float dt)
		{
			// Verificar si estamos en Noche Verde
			bool isGreenNight = SubsystemGreenNightSky.IsGreenNight();

			// Si acaba de empezar la Noche Verde, resetear el objetivo
			if (isGreenNight && !m_lastGreenNightState)
			{
				m_greenNightTargetPlayer = null;
				this.m_autoChaseSuppressionTime = 0f; // Resetear supresión
				Log.Information("Noche Verde detectada - Zombi buscará jugadores agresivamente");
			}

			// Si acaba de terminar la Noche Verde, limpiar el objetivo
			if (!isGreenNight && m_lastGreenNightState)
			{
				m_greenNightTargetPlayer = null;
				Log.Information("Noche Verde terminada - Zombi regresa a comportamiento normal");

				// Detener cualquier ataque forzado
				if (this.m_target != null && this.m_subsystemPlayers.IsPlayer(this.m_target.Entity))
				{
					this.StopAttack();
				}
			}

			m_lastGreenNightState = isGreenNight;

			if (this.Suppressed)
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

						bool bodyToHit = true;
						bool playAttackSound = true;

						// NO usar hooks ya que no existen para ComponentZombieChaseBehavior

						if (bodyToHit)
						{
							this.m_componentMiner.Hit(hitBody, hitPoint, this.m_componentCreature.ComponentBody.Matrix.Forward);
						}

						if (playAttackSound)
						{
							this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						}
					}
					else
					{
						// NO usar hooks ya que no existen para ComponentZombieChaseBehavior
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

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

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
			this.m_componentZombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();

			// Cargar propiedades específicas de zombis
			this.AttacksSameHerd = valuesDictionary.GetValue<bool>("AttacksSameHerd", false);
			this.AttacksAllCategories = valuesDictionary.GetValue<bool>("AttacksAllCategories", false); // CAMBIADO: false por defecto
			this.FleeFromSameHerd = valuesDictionary.GetValue<bool>("FleeFromSameHerd", true);
			this.FleeDistance = valuesDictionary.GetValue<float>("FleeDistance", 10f);
			this.ForceAttackDuringGreenNight = valuesDictionary.GetValue<bool>("ForceAttackDuringGreenNight", true);

			this.m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange", 8f);
			this.m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange", 12f);
			this.m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime", 30f);
			this.m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime", 30f);
			this.m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask", (CreatureCategory)0); // VACÍO por defecto
			this.m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability", 1f);
			this.m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability", 1f);
			this.m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability", 1f);

			// Configurar para atacar todas las categorías si está habilitado
			if (this.AttacksAllCategories)
			{
				this.m_autoChaseMask = CreatureCategory.LandPredator | CreatureCategory.LandOther |
									   CreatureCategory.WaterPredator | CreatureCategory.WaterOther |
									   CreatureCategory.Bird;
			}
			else
			{
				// Si no ataca todas las categorías, dejar el mask vacío
				this.m_autoChaseMask = (CreatureCategory)0;
			}

			// Configurar handlers de eventos
			this.SetupEventHandlers();

			// Configurar la máquina de estados
			this.SetupStateMachine();

			this.m_stateMachine.TransitionTo("LookingForTarget");

			Log.Information($"ComponentZombieChaseBehavior cargado. Atacar misma manada: {this.AttacksSameHerd}, Atacar todas categorías: {this.AttacksAllCategories}, AutoChaseMask: {this.m_autoChaseMask}");
		}

		private void SetupEventHandlers()
		{
			ComponentBody componentBody = this.m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(componentBody.CollidedWithBody, new Action<ComponentBody>(delegate (ComponentBody body)
			{
				if (this.m_target == null && this.m_autoChaseSuppressionTime <= 0f && this.m_random.Float(0f, 1f) < this.m_chaseOnTouchProbability)
				{
					ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						bool isPlayer = this.m_subsystemPlayers.IsPlayer(body.Entity);
						bool isInAutoChaseMask = (componentCreature.Category & this.m_autoChaseMask) > (CreatureCategory)0;

						// Verificar si es de la misma manada (si aplica)
						if (!this.AttacksSameHerd && this.m_componentZombieHerdBehavior != null)
						{
							if (this.m_componentZombieHerdBehavior.IsSameZombieHerd(componentCreature))
							{
								// Si no ataca a la misma manada, huir
								if (this.FleeFromSameHerd)
								{
									this.FleeFromTarget(componentCreature);
								}
								return;
							}
						}

						// SÓLO atacar jugadores si:
						// 1. Es Noche Verde y ForceAttackDuringGreenNight está activo
						// 2. El jugador atacó al zombi (se maneja en otro evento)
						// 3. Configuración específica lo permite
						bool greenNightCondition = SubsystemGreenNightSky.IsGreenNight() && this.ForceAttackDuringGreenNight;

						if (this.AttacksPlayer && isPlayer && greenNightCondition)
						{
							// SOLO durante Noche Verde atacar jugadores automáticamente
							this.Attack(componentCreature, this.ChaseRangeOnTouch, this.ChaseTimeOnTouch, false);
						}
						else if (this.AttacksNonPlayerCreature && !isPlayer && isInAutoChaseMask)
						{
							// Para criaturas no-jugador, seguir la lógica normal
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
				if (attacker != null)
				{
					// Verificar si el atacante es de la misma manada (si aplica)
					if (!this.AttacksSameHerd && this.m_componentZombieHerdBehavior != null)
					{
						if (this.m_componentZombieHerdBehavior.IsSameZombieHerd(attacker))
						{
							// Si es de la misma manada y no atacamos a la misma manada, huir
							if (this.FleeFromSameHerd)
							{
								this.FleeFromTarget(attacker);
							}
							return;
						}
					}

					// Perseguir al atacante SIEMPRE (si no es de la misma manada)
					if (this.m_random.Float(0f, 1f) < this.m_chaseWhenAttackedProbability)
					{
						bool flag = false;
						float chaseRange, chaseTime;

						if (this.m_chaseWhenAttackedProbability >= 1f)
						{
							chaseRange = 30f;
							chaseTime = 60f;
							flag = true;
						}
						else
						{
							chaseRange = 7f;
							chaseTime = 7f;
						}

						chaseRange = this.ChaseRangeOnAttacked.GetValueOrDefault(chaseRange);
						chaseTime = this.ChaseTimeOnAttacked.GetValueOrDefault(chaseTime);
						flag = this.ChasePersistentOnAttacked.GetValueOrDefault(flag);

						// Si es jugador y es Noche Verde, hacerlo persistente
						bool isPlayer = this.m_subsystemPlayers.IsPlayer(attacker.Entity);
						if (isPlayer && SubsystemGreenNightSky.IsGreenNight() && this.ForceAttackDuringGreenNight)
						{
							flag = true;
							chaseTime = MathUtils.Max(chaseTime, 60f);
						}

						this.Attack(attacker, chaseRange, chaseTime, flag);
					}
				}
			}));
		}

		private void SetupStateMachine()
		{
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

				if (!this.Suppressed && this.m_autoChaseSuppressionTime <= 0f &&
					(this.m_target == null || this.ScoreTarget(this.m_target) <= 0f) &&
					this.m_componentCreature.ComponentHealth.Health > this.MinHealthToAttackActively)
				{
					// Determinar rango basado en hora del día o Noche Verde
					if (SubsystemGreenNightSky.IsGreenNight() && this.ForceAttackDuringGreenNight)
					{
						// Durante Noche Verde, rangos aumentados SOLO para jugadores
						this.m_range = this.m_nightChaseRange * 1.5f;
						this.m_range *= 1.5f; // Bonus adicional
					}
					else
					{
						this.m_range = ((this.m_subsystemSky.SkyLightIntensity < 0.2f) ? this.m_nightChaseRange : this.m_dayChaseRange);
					}

					this.m_range *= this.m_componentFactors.GetOtherFactorResult("ChaseRange", false, false);

					// Buscar objetivo
					ComponentCreature componentCreature = this.FindTarget();

					if (componentCreature != null)
					{
						this.m_targetInRangeTime += this.m_dt;
					}
					else
					{
						this.m_targetInRangeTime = 0f;
					}

					// SOLO atacar si:
					// 1. Es Noche Verde y ForceAttackDuringGreenNight está activo
					// 2. Es una criatura no-jugador que está en el AutoChaseMask
					bool greenNightCondition = SubsystemGreenNightSky.IsGreenNight() && this.ForceAttackDuringGreenNight;
					bool isPlayer = componentCreature != null && this.m_subsystemPlayers.IsPlayer(componentCreature.Entity);
					bool shouldChase = false;

					if (componentCreature != null)
					{
						if (isPlayer && greenNightCondition)
						{
							// Jugador durante Noche Verde
							shouldChase = true;
						}
						else if (!isPlayer && (componentCreature.Category & this.m_autoChaseMask) > (CreatureCategory)0)
						{
							// Criatura no-jugador que está en el mask
							shouldChase = true;
						}
					}

					if (shouldChase && this.m_targetInRangeTime > this.TargetInRangeTimeToChase)
					{
						bool isDayTime = this.m_subsystemSky.SkyLightIntensity >= 0.1f;
						float maxRange = isDayTime ? (this.m_dayChaseRange + 6f) : (this.m_nightChaseRange + 6f);
						float maxChaseTime = isDayTime ? (this.m_dayChaseTime * this.m_random.Float(0.75f, 1f)) : (this.m_nightChaseTime * this.m_random.Float(0.75f, 1f));

						// Durante Noche Verde, ataque persistente contra jugadores
						if (greenNightCondition && isPlayer)
						{
							maxRange *= 1.5f;
							maxChaseTime *= 2f;
							this.Attack(componentCreature, maxRange, maxChaseTime, true);
						}
						else
						{
							this.Attack(componentCreature, maxRange, maxChaseTime, !isDayTime);
						}
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

					// Durante Noche Verde, buscar nuevo objetivo inmediatamente
					if (SubsystemGreenNightSky.IsGreenNight() && this.ForceAttackDuringGreenNight)
					{
						this.m_stateMachine.TransitionTo("LookingForTarget");
					}
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

						this.m_componentPathfinding.SetDestination(new Vector3?(vector + num2 * num * this.m_target.ComponentBody.Velocity),
							1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);

						if (this.PlayAngrySoundWhenChasing && this.m_random.Float(0f, 1f) < 0.33f * this.m_dt)
						{
							this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						}
					}
				}
			}, null);
		}

		private void FleeFromTarget(ComponentCreature target)
		{
			if (target == null || this.m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			Vector3 fleeDirection = this.m_componentCreature.ComponentBody.Position - target.ComponentBody.Position;

			if (fleeDirection.LengthSquared() > 0.01f)
			{
				fleeDirection = Vector3.Normalize(fleeDirection);
				Vector3 destination = this.m_componentCreature.ComponentBody.Position + fleeDirection * this.FleeDistance;

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

				// Reproducir sonido de dolor/miedo
				this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
			}
		}

		public virtual ComponentCreature FindTarget()
		{
			// Durante Noche Verde, priorizar jugadores
			if (SubsystemGreenNightSky.IsGreenNight() && this.ForceAttackDuringGreenNight)
			{
				ComponentCreature playerTarget = this.FindPlayerTarget();
				if (playerTarget != null)
				{
					m_greenNightTargetPlayer = playerTarget;
					return playerTarget;
				}
			}

			// Si no es Noche Verde o no hay jugadores, buscar criaturas no-jugador
			if (this.m_autoChaseMask != (CreatureCategory)0)
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
						// Verificar si es de la misma manada (si aplica)
						if (!this.AttacksSameHerd && creature != this.m_componentCreature &&
							this.m_componentZombieHerdBehavior != null)
						{
							if (this.m_componentZombieHerdBehavior.IsSameZombieHerd(creature))
							{
								continue; // Saltar miembros de la misma manada
							}
						}

						// Solo considerar criaturas no-jugador que estén en el AutoChaseMask
						bool isPlayer = this.m_subsystemPlayers.IsPlayer(creature.Entity);
						if (!isPlayer && (creature.Category & this.m_autoChaseMask) > (CreatureCategory)0)
						{
							float score = this.ScoreTarget(creature);
							if (score > bestScore)
							{
								bestScore = score;
								result = creature;
							}
						}
					}
				}

				return result;
			}

			return null;
		}

		private ComponentCreature FindPlayerTarget()
		{
			// Buscar jugadores cercanos SOLO durante Noche Verde
			if (!SubsystemGreenNightSky.IsGreenNight() || !this.ForceAttackDuringGreenNight)
				return null;

			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentCreature bestPlayerTarget = null;
			float bestScore = 0f;

			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), this.m_range * 2f, this.m_componentBodies);

			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentPlayer player = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentPlayer>();
				if (player != null && player.ComponentHealth.Health > 0f)
				{
					float distance = Vector3.Distance(position, player.ComponentBody.Position);
					float score = this.m_range * 2f - distance;

					// Bonus si es el mismo jugador que antes
					if (player == m_greenNightTargetPlayer)
					{
						score *= 1.5f;
					}

					// Bonus extra durante Noche Verde
					if (SubsystemGreenNightSky.IsGreenNight() && this.ForceAttackDuringGreenNight)
					{
						score *= 2f;
					}

					if (score > bestScore)
					{
						bestScore = score;
						bestPlayerTarget = player;
					}
				}
			}

			return bestPlayerTarget;
		}

		public virtual float ScoreTarget(ComponentCreature componentCreature)
		{
			float score = 0f;

			if (componentCreature == null || componentCreature == this.m_componentCreature)
				return 0f;

			// Verificar si el objetivo es de la misma manada (si aplica)
			if (!this.AttacksSameHerd && this.m_componentZombieHerdBehavior != null)
			{
				if (this.m_componentZombieHerdBehavior.IsSameZombieHerd(componentCreature))
				{
					return 0f; // No puntuar miembros de la misma manada
				}
			}

			bool isPlayer = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
			bool isNotWaterCreature = this.m_componentCreature.Category != CreatureCategory.WaterPredator &&
									  this.m_componentCreature.Category != CreatureCategory.WaterOther;
			bool isCurrentTarget = componentCreature == this.Target;
			bool isInAutoChaseMask = (componentCreature.Category & this.m_autoChaseMask) > (CreatureCategory)0;

			// Verificar si es Noche Verde
			bool greenNightCondition = SubsystemGreenNightSky.IsGreenNight() && this.ForceAttackDuringGreenNight;

			// Si es Noche Verde y es un jugador, dar máxima prioridad
			if (greenNightCondition && isPlayer)
			{
				float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
					componentCreature.ComponentBody.Position);

				if (distance < this.m_range * 2f)
				{
					score = (this.m_range * 2f - distance) * 3f; // Prioridad muy alta
					return score; // Retornar inmediatamente
				}
			}

			// Si NO es Noche Verde y es jugador, NO dar puntuación (a menos que sea el actual target)
			if (!greenNightCondition && isPlayer && componentCreature != this.Target)
			{
				return 0f; // Jugadores normales no reciben puntuación fuera de Noche Verde
			}

			// Comportamiento para criaturas no-jugador
			if (!isPlayer && isInAutoChaseMask)
			{
				bool shouldChaseNonPlayer = MathUtils.Remainder(0.004999999888241291 * this.m_subsystemTime.GameTime +
					(double)((float)(this.GetHashCode() % 1000) / 1000f) +
					(double)((float)(componentCreature.GetHashCode() % 1000) / 1000f), 1.0) <
					(double)this.m_chaseNonPlayerProbability;

				if (componentCreature.Entity.IsAddedToProject &&
					componentCreature.ComponentHealth.Health > 0f &&
					(isNotWaterCreature || this.IsTargetInWater(componentCreature.ComponentBody)))
				{
					float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
						componentCreature.ComponentBody.Position);

					if (distance < this.m_range)
					{
						score = this.m_range - distance;

						// Bonus si el objetivo es el que nos atacó
						if (componentCreature == this.Target)
						{
							score *= 1.2f;
						}
					}
				}
			}

			return score;
		}

		public virtual bool IsTargetInWater(ComponentBody target)
		{
			return target.ImmersionDepth > 0f ||
				   (target.ParentBody != null && this.IsTargetInWater(target.ParentBody)) ||
				   (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y &&
					this.IsTargetInWater(target.StandingOnBody));
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

			return (target.ParentBody != null && this.IsTargetInAttackRange(target.ParentBody)) ||
				   (this.AllowAttackingStandingOnBody && target.StandingOnBody != null &&
					target.StandingOnBody.Position.Y < target.Position.Y && this.IsTargetInAttackRange(target.StandingOnBody));
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

			if (bodyRaycastResult != null && bodyRaycastResult.Value.Distance < this.MaxAttackRange &&
				(bodyRaycastResult.Value.ComponentBody == target ||
				 bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) ||
				 target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody) ||
				 (target.StandingOnBody == bodyRaycastResult.Value.ComponentBody && this.AllowAttackingStandingOnBody)))
			{
				hitPoint = bodyRaycastResult.Value.HitPoint();
				return bodyRaycastResult.Value.ComponentBody;
			}

			hitPoint = default(Vector3);
			return null;
		}
	}
}
