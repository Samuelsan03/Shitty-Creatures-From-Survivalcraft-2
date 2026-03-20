using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewChaseBehavior : ComponentBehavior, IUpdateable
	{
		// ===== PROPIEDADES PÚBLICAS =====
		public ComponentCreature Target => m_target;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		// ===== PARÁMETROS CONFIGURABLES =====
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

		// ===== CAMPOS PRIVADOS =====
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemSky m_subsystemSky;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTime m_subsystemTime;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;

		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentMiner m_componentMiner;
		private ComponentRandomFeedBehavior m_componentFeedBehavior;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentFactors m_componentFactors;
		private ComponentNewHerdBehavior m_componentHerd;
		private ComponentHireableNPC m_componentHireable;

		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		private Random m_random = new Random();
		private StateMachine m_stateMachine = new StateMachine();

		private ComponentCreature m_target;
		private float m_range;
		private float m_chaseTime;
		private bool m_isPersistent;
		private float m_importanceLevel;
		private float m_targetUnsuitableTime;
		private float m_targetInRangeTime;
		private double m_nextUpdateTime;
		private float m_dt;
		private float m_autoChaseSuppressionTime;

		// Suscripción a eventos de salud de jugadores
		private List<ComponentHealth> m_subscribedPlayerHealths = new List<ComponentHealth>();

		// ===== PROPIEDADES AUXILIARES =====
		private bool IsZombie => HerdName != null && HerdName.ToLower().Contains("Zombie");
		private bool IsBandit => HerdName != null && HerdName.ToLower().Contains("bandits");
		private bool IsGreenNightActive => m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive;
		private float SpecialChaseRange => (IsZombie && IsGreenNightActive) || IsBandit ? 50f : m_range;

		private bool ShouldProtectPlayer =>
			!string.IsNullOrEmpty(HerdName) &&
			(HerdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
			 HerdName.ToLower().Contains("guardian"));

		public string HerdName
		{
			get
			{
				if (m_componentHerd == null)
					m_componentHerd = Entity.FindComponent<ComponentNewHerdBehavior>();
				return m_componentHerd != null ? m_componentHerd.HerdName : null;
			}
		}

		// ===== MÉTODOS PÚBLICOS =====
		public virtual void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (m_componentHireable != null && !m_componentHireable.IsHired)
				return;

			if (Suppressed || target == null) return;

			if (m_componentHerd != null && !m_componentHerd.CanAttackCreature(target))
				return;

			if (IsExtremePriorityTarget(target))
			{
				isPersistent = true;
				maxChaseTime = Math.Max(maxChaseTime, 120f);
				maxRange = Math.Max(maxRange, SpecialChaseRange);
			}

			m_target = target;
			m_nextUpdateTime = 0.0;
			m_range = maxRange;
			m_chaseTime = maxChaseTime;
			m_isPersistent = isPersistent;
			m_importanceLevel = isPersistent ? ImportanceLevelPersistent : ImportanceLevelNonPersistent;
			IsActive = true;
			m_stateMachine.TransitionTo("Chasing");
		}

		public void RespondToCommandImmediately(ComponentCreature target)
		{
			Attack(target, 30f, 45f, false);
		}

		public virtual void StopAttack()
		{
			m_stateMachine.TransitionTo("LookingForTarget");
			IsActive = false;
			m_target = null;
			m_nextUpdateTime = 0.0;
			m_range = 0f;
			m_chaseTime = 0f;
			m_isPersistent = false;
			m_importanceLevel = 0f;
		}

		// ===== UPDATE =====
		public virtual void Update(float dt)
		{
			if (Suppressed)
			{
				StopAttack();
				return;
			}

			if (m_target != null && IsExtremePriorityTarget(m_target))
			{
				m_autoChaseSuppressionTime = 0f;
			}

			m_autoChaseSuppressionTime -= dt;

			if (IsActive && m_target != null)
			{
				m_chaseTime -= dt;
				m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(m_target.ComponentCreatureModel.EyePosition);

				if (IsTargetInAttackRange(m_target.ComponentBody))
				{
					m_componentCreatureModel.AttackOrder = true;
				}

				if (m_componentCreatureModel.IsAttackHitMoment)
				{
					Vector3 hitPoint;
					ComponentBody hitBody = GetHitBody(m_target.ComponentBody, out hitPoint);
					if (hitBody != null)
					{
						float extraChaseTime = m_isPersistent ? m_random.Float(8f, 10f) : 2f;
						m_chaseTime = MathUtils.Max(m_chaseTime, extraChaseTime);
						m_componentMiner.Hit(hitBody, hitPoint, m_componentCreature.ComponentBody.Matrix.Forward);
						m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
					}
				}
			}

			if (m_subsystemTime.GameTime >= m_nextUpdateTime)
			{
				m_dt = m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(m_subsystemTime.GameTime - m_nextUpdateTime), 0.1f);
				m_nextUpdateTime = m_subsystemTime.GameTime + (double)m_dt;
				m_stateMachine.Update();
			}
		}

		// ===== LOAD =====
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_subsystemGreenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentFeedBehavior = Entity.FindComponent<ComponentRandomFeedBehavior>();
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentFactors = Entity.FindComponent<ComponentFactors>(true);
			m_componentHerd = Entity.FindComponent<ComponentNewHerdBehavior>(true);
			m_componentHireable = Entity.FindComponent<ComponentHireableNPC>();

			m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");
			m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask");
			m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
			m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
			m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");

			RegisterEvents();

			if (ShouldProtectPlayer)
			{
				SubscribeToPlayersForProtection();
			}

			SetupStateMachine();
			m_stateMachine.TransitionTo("LookingForTarget");
		}

		// ===== SUSCRIPCIÓN A JUGADORES PARA PROTEGERLOS =====
		private void SubscribeToPlayersForProtection()
		{
			if (m_subsystemPlayers == null) return;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player?.ComponentHealth != null && !m_subscribedPlayerHealths.Contains(player.ComponentHealth))
				{
					player.ComponentHealth.Injured += OnPlayerInjured;
					m_subscribedPlayerHealths.Add(player.ComponentHealth);
				}
			}
		}

		// ===== MANEJAR DAÑO A JUGADORES =====
		private void OnPlayerInjured(Injury injury)
		{
			if (!ShouldProtectPlayer) return;

			if (m_componentHireable != null && !m_componentHireable.IsHired) return;

			ComponentDefensiveRunAwayBehavior defensiveRunAway = Entity.FindComponent<ComponentDefensiveRunAwayBehavior>();
			if (defensiveRunAway != null && defensiveRunAway.IsActive) return;

			ComponentCreature attacker = injury.Attacker;
			if (attacker != null && CanAttackCreature(attacker))
			{
				Attack(attacker, 20f, 30f, false);

				if (m_componentHerd != null)
				{
					m_componentHerd.CallNearbyCreaturesHelp(attacker, 20f, 30f, false, true);
				}
			}
		}

		// ===== REACCIÓN AL GOLPE A PUÑO LIMPIO DEL JUGADOR =====
		public void OnPlayerHitWithFist(ComponentCreature hitCreature, ComponentPlayer player)
		{
			if (!ShouldProtectPlayer) return;

			if (m_componentHireable != null && !m_componentHireable.IsHired) return;

			ComponentDefensiveRunAwayBehavior defensiveRunAway = Entity.FindComponent<ComponentDefensiveRunAwayBehavior>();
			if (defensiveRunAway != null && defensiveRunAway.IsActive) return;

			if (hitCreature == null || hitCreature.ComponentHealth.Health <= 0f) return;
			if (!CanAttackCreature(hitCreature)) return;
			if (hitCreature.Entity.FindComponent<ComponentPlayer>() != null) return;

			Attack(hitCreature, 30f, 45f, false);

			if (m_componentHerd != null)
			{
				m_componentHerd.CallNearbyCreaturesHelp(hitCreature, 30f, 45f, false, true);
			}
		}

		// ===== VERIFICAR SI PUEDE ATACAR A UNA CRIATURA =====
		private bool CanAttackCreature(ComponentCreature creature)
		{
			if (creature == null) return false;
			if (m_componentHireable != null && !m_componentHireable.IsHired) return false;

			if (m_componentHerd != null && !m_componentHerd.CanAttackCreature(creature))
				return false;

			return true;
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap) { }

		public override void Dispose()
		{
			if (m_subscribedPlayerHealths != null)
			{
				foreach (ComponentHealth health in m_subscribedPlayerHealths)
				{
					if (health != null)
						health.Injured -= OnPlayerInjured;
				}
				m_subscribedPlayerHealths.Clear();
			}
			base.Dispose();
		}

		// ===== REGISTRO DE EVENTOS =====
		private void RegisterEvents()
		{
			m_componentCreature.ComponentBody.CollidedWithBody += delegate (ComponentBody body)
			{
				if (m_componentHireable != null && !m_componentHireable.IsHired)
					return;

				if (m_target == null && m_autoChaseSuppressionTime <= 0f && m_random.Float(0f, 1f) < m_chaseOnTouchProbability)
				{
					ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
					if (creature != null && CanAttackCreature(creature))
					{
						bool isPlayer = m_subsystemPlayers.IsPlayer(body.Entity);
						if ((AttacksPlayer && isPlayer && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
							(AttacksNonPlayerCreature && !isPlayer && (creature.Category & m_autoChaseMask) > (CreatureCategory)0))
						{
							Attack(creature, ChaseRangeOnTouch, ChaseTimeOnTouch, false);
						}
					}
				}

				if (m_target != null && JumpWhenTargetStanding && body == m_target.ComponentBody &&
					body.StandingOnBody == m_componentCreature.ComponentBody)
				{
					m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
				}
			};

			m_componentCreature.ComponentHealth.Injured += delegate (Injury injury)
			{
				if (m_componentHireable != null && !m_componentHireable.IsHired)
					return;

				ComponentCreature attacker = injury.Attacker;
				if (attacker != null && attacker != m_componentCreature && CanAttackCreature(attacker))
				{
					bool persistent = false;
					float range, time;

					range = 7f;
					time = 7f;
					persistent = false;

					range = ChaseRangeOnAttacked ?? range;
					time = ChaseTimeOnAttacked ?? time;
					persistent = ChasePersistentOnAttacked ?? persistent;

					Attack(attacker, range, time, persistent);

					if (m_componentHerd != null)
					{
						m_componentHerd.CallNearbyCreaturesHelp(attacker, 20f, 30f, false, true);
					}
				}
			};
		}

		// ===== CONFIGURACIÓN DE LA MÁQUINA DE ESTADOS =====
		private void SetupStateMachine()
		{
			m_stateMachine.AddState("LookingForTarget", () =>
			{
				m_importanceLevel = 0f;
				m_target = null;
			}, () =>
			{
				if (IsActive)
				{
					m_stateMachine.TransitionTo("Chasing");
					return;
				}

				if (!Suppressed && m_autoChaseSuppressionTime <= 0f &&
					(m_target == null || ScoreTarget(m_target) <= 0f) &&
					m_componentCreature.ComponentHealth.Health > MinHealthToAttackActively)
				{
					m_range = (m_subsystemSky.SkyLightIntensity < 0.2f) ? m_nightChaseRange : m_dayChaseRange;
					m_range *= m_componentFactors.GetOtherFactorResult("ChaseRange", false, false);

					ComponentCreature target = FindTarget();
					if (target != null)
					{
						float score = ScoreTarget(target);
						if (score > 1e9f)
						{
							bool isDay = m_subsystemSky.SkyLightIntensity >= 0.1f;
							float maxRange = isDay ? (m_dayChaseRange + 6f) : (m_nightChaseRange + 6f);
							float maxChaseTime = isDay ? (m_dayChaseTime * m_random.Float(0.75f, 1f)) : (m_nightChaseTime * m_random.Float(0.75f, 1f));
							maxRange = Math.Max(maxRange, SpecialChaseRange);
							Attack(target, maxRange, maxChaseTime, true);
							return;
						}

						m_targetInRangeTime += m_dt;
					}
					else
					{
						m_targetInRangeTime = 0f;
					}

					if (m_targetInRangeTime > TargetInRangeTimeToChase && target != null)
					{
						bool isDay = m_subsystemSky.SkyLightIntensity >= 0.1f;
						float maxRange = isDay ? (m_dayChaseRange + 6f) : (m_nightChaseRange + 6f);
						float maxChaseTime = isDay ? (m_dayChaseTime * m_random.Float(0.75f, 1f)) : (m_nightChaseTime * m_random.Float(0.75f, 1f));
						Attack(target, maxRange, maxChaseTime, !isDay);
					}
				}
			}, null);

			m_stateMachine.AddState("RandomMoving", () =>
			{
				Vector3 offset = new Vector3(6f * m_random.Float(-1f, 1f), 0f, 6f * m_random.Float(-1f, 1f));
				m_componentPathfinding.SetDestination(m_componentCreature.ComponentBody.Position + offset, 1f, 1f, 0, false, true, false, null);
			}, () =>
			{
				if (m_componentPathfinding.IsStuck || m_componentPathfinding.Destination == null)
				{
					m_stateMachine.TransitionTo("Chasing");
				}
				if (!IsActive)
				{
					m_stateMachine.TransitionTo("LookingForTarget");
				}
			}, () => m_componentPathfinding.Stop());

			m_stateMachine.AddState("Chasing", () =>
			{
				m_subsystemNoise.MakeNoise(m_componentCreature.ComponentBody, 0.25f, 6f);
				if (PlayIdleSoundWhenStartToChase)
				{
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}
				m_nextUpdateTime = 0.0;
			}, () =>
			{
				if (!IsActive)
				{
					m_stateMachine.TransitionTo("LookingForTarget");
				}
				else if (m_chaseTime <= 0f)
				{
					m_autoChaseSuppressionTime = m_random.Float(10f, 60f);
					m_importanceLevel = 0f;
				}
				else if (m_target == null)
				{
					m_importanceLevel = 0f;
				}
				else if (m_target.ComponentHealth.Health <= 0f)
				{
					if (m_componentFeedBehavior != null)
					{
						m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + m_random.Float(1f, 3f), () =>
						{
							if (m_target != null)
								m_componentFeedBehavior.Feed(m_target.ComponentBody.Position);
						});
					}
					m_importanceLevel = 0f;
				}
				else if (!m_isPersistent && m_componentPathfinding.IsStuck)
				{
					m_importanceLevel = 0f;
				}
				else if (m_isPersistent && m_componentPathfinding.IsStuck)
				{
					m_stateMachine.TransitionTo("RandomMoving");
				}
				else
				{
					if (ScoreTarget(m_target) <= 0f)
						m_targetUnsuitableTime += m_dt;
					else
						m_targetUnsuitableTime = 0f;

					if (m_targetUnsuitableTime > 3f)
					{
						m_importanceLevel = 0f;
					}
					else
					{
						int maxPathfinding = m_isPersistent ? (m_subsystemTime.FixedTimeStep != null ? 2000 : 500) : 0;

						BoundingBox bbSelf = m_componentCreature.ComponentBody.BoundingBox;
						BoundingBox bbTarget = m_target.ComponentBody.BoundingBox;
						Vector3 selfCenter = 0.5f * (bbSelf.Min + bbSelf.Max);
						Vector3 targetCenter = 0.5f * (bbTarget.Min + bbTarget.Max);

						float dist = Vector3.Distance(selfCenter, targetCenter);
						float followFactor = (dist < 4f) ? 0.2f : 0f;

						m_componentPathfinding.SetDestination(targetCenter + followFactor * dist * m_target.ComponentBody.Velocity,
							1f, 1.5f, maxPathfinding, true, false, true, m_target.ComponentBody);

						if (PlayAngrySoundWhenChasing && m_random.Float(0f, 1f) < 0.33f * m_dt)
						{
							m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						}
					}
				}
			}, null);
		}

		// ===== MÉTODOS AUXILIARES =====
		private ComponentPlayer FindNearestPlayer(float range)
		{
			if (m_subsystemPlayers == null || m_componentCreature?.ComponentBody == null)
				return null;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			ComponentPlayer nearest = null;
			float minDistSq = range * range;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player != null && player.ComponentHealth.Health > 0f)
				{
					float distSq = Vector3.DistanceSquared(position, player.ComponentBody.Position);
					if (distSq <= minDistSq)
					{
						minDistSq = distSq;
						nearest = player;
					}
				}
			}
			return nearest;
		}

		private bool IsExtremePriorityTarget(ComponentCreature creature)
		{
			if (creature == null) return false;
			bool isPlayer = creature.Entity.FindComponent<ComponentPlayer>() != null;
			return (IsZombie && IsGreenNightActive && isPlayer) || (IsBandit && isPlayer);
		}

		private ComponentCreature FindTarget()
		{
			if (m_componentHireable != null && !m_componentHireable.IsHired)
				return null;

			if ((IsZombie && IsGreenNightActive) || IsBandit)
			{
				ComponentPlayer player = FindNearestPlayer(SpecialChaseRange);
				if (player != null)
				{
					return player;
				}
			}

			Vector3 position = m_componentCreature.ComponentBody.Position;
			ComponentCreature best = null;
			float bestScore = 0f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), m_range, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					float score = ScoreTarget(creature);
					if (score > bestScore)
					{
						bestScore = score;
						best = creature;
					}
				}
			}
			return best;
		}

		private float ScoreTarget(ComponentCreature creature)
		{
			if (m_componentHireable != null && !m_componentHireable.IsHired)
				return 0f;

			if (!CanAttackCreature(creature))
				return 0f;

			bool isPlayer = creature.Entity.FindComponent<ComponentPlayer>() != null;
			bool isWaterPrey = m_componentCreature.Category != CreatureCategory.WaterPredator &&
							  m_componentCreature.Category != CreatureCategory.WaterOther;
			bool isTargetOrCreative = creature == Target || m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
			bool categoryMatch = (creature.Category & m_autoChaseMask) > (CreatureCategory)0;

			double randomSeed = 0.005 * m_subsystemTime.GameTime +
							   (GetHashCode() % 1000) / 1000.0 +
							   (creature.GetHashCode() % 1000) / 1000.0;
			bool probabilityMatch = creature == Target || (categoryMatch &&
				MathUtils.Remainder(randomSeed, 1.0) < m_chaseNonPlayerProbability);

			if (isPlayer && ((IsZombie && IsGreenNightActive) || IsBandit))
			{
				return float.MaxValue / 2;
			}

			if (creature != m_componentCreature &&
				((!isPlayer && probabilityMatch) || (isPlayer && isTargetOrCreative)) &&
				creature.Entity.IsAddedToProject &&
				creature.ComponentHealth.Health > 0f &&
				(isWaterPrey || IsTargetInWater(creature.ComponentBody)))
			{
				float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position, creature.ComponentBody.Position);
				if (dist < m_range)
					return m_range - dist;
			}
			return 0f;
		}

		private bool IsTargetInWater(ComponentBody target)
		{
			return target.ImmersionDepth > 0f ||
				   (target.ParentBody != null && IsTargetInWater(target.ParentBody)) ||
				   (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y &&
					IsTargetInWater(target.StandingOnBody));
		}

		private bool IsTargetInAttackRange(ComponentBody target)
		{
			if (IsBodyInAttackRange(target)) return true;

			BoundingBox bbSelf = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox bbTarget = target.BoundingBox;
			Vector3 selfCenter = 0.5f * (bbSelf.Min + bbSelf.Max);
			Vector3 toTarget = 0.5f * (bbTarget.Min + bbTarget.Max) - selfCenter;
			float dist = toTarget.Length();
			Vector3 dir = toTarget / dist;
			float width = 0.5f * (bbSelf.Max.X - bbSelf.Min.X + bbTarget.Max.X - bbTarget.Min.X);
			float height = 0.5f * (bbSelf.Max.Y - bbSelf.Min.Y + bbTarget.Max.Y - bbTarget.Min.Y);

			if (Math.Abs(toTarget.Y) < height * 0.99f)
			{
				if (dist < width + 0.99f && Vector3.Dot(dir, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (dist < height + 0.3f && Math.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
				return true;

			return (target.ParentBody != null && IsTargetInAttackRange(target.ParentBody)) ||
				   (AllowAttackingStandingOnBody && target.StandingOnBody != null &&
					target.StandingOnBody.Position.Y < target.Position.Y &&
					IsTargetInAttackRange(target.StandingOnBody));
		}

		private bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox bbSelf = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox bbTarget = target.BoundingBox;
			Vector3 selfCenter = 0.5f * (bbSelf.Min + bbSelf.Max);
			Vector3 toTarget = 0.5f * (bbTarget.Min + bbTarget.Max) - selfCenter;
			float dist = toTarget.Length();
			Vector3 dir = toTarget / dist;
			float width = 0.5f * (bbSelf.Max.X - bbSelf.Min.X + bbTarget.Max.X - bbTarget.Min.X);
			float height = 0.5f * (bbSelf.Max.Y - bbSelf.Min.Y + bbTarget.Max.Y - bbTarget.Min.Y);

			if (Math.Abs(toTarget.Y) < height * 0.99f)
			{
				if (dist < width + 0.99f && Vector3.Dot(dir, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (dist < height + 0.3f && Math.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
				return true;

			return false;
		}

		private ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 eye = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 targetCenter = target.BoundingBox.Center();
			Ray3 ray = new Ray3(eye, Vector3.Normalize(targetCenter - eye));

			BodyRaycastResult? result = m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);

			if (result != null && result.Value.Distance < MaxAttackRange &&
				(result.Value.ComponentBody == target ||
				 result.Value.ComponentBody.IsChildOfBody(target) ||
				 target.IsChildOfBody(result.Value.ComponentBody) ||
				 (target.StandingOnBody == result.Value.ComponentBody && AllowAttackingStandingOnBody)))
			{
				hitPoint = result.Value.HitPoint();
				return result.Value.ComponentBody;
			}

			hitPoint = Vector3.Zero;
			return null;
		}

		// ===== CAMPOS DE LA BASE DE DATOS =====
		private float m_dayChaseRange;
		private float m_nightChaseRange;
		private float m_dayChaseTime;
		private float m_nightChaseTime;
		private float m_chaseNonPlayerProbability;
		private float m_chaseWhenAttackedProbability;
		private float m_chaseOnTouchProbability;
		private CreatureCategory m_autoChaseMask;
	}
}
