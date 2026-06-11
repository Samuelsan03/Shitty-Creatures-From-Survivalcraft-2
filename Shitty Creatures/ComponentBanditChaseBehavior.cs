using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBanditChaseBehavior : ComponentBehavior, IUpdateable
	{
		// Properties
		public ComponentCreature Target => m_target;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		// Modo narcotraficante: cuando está activo, persigue al jugador sin descanso
		public bool IsDrugTraffickerMode { get; set; }

		// Nombre de la manada (se carga desde la plantilla)
		public string HerdName { get; set; }

		// Fields
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemPlayers m_subsystemPlayers;
		public SubsystemSky m_subsystemSky;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemTime m_subsystemTime;
		public SubsystemNoise m_subsystemNoise;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public ComponentMiner m_componentMiner;
		public ComponentRandomFeedBehavior m_componentFeedBehavior;
		public ComponentCreatureModel m_componentCreatureModel;
		public ComponentFactors m_componentFactors;
		public ComponentBanditHerdBehavior m_componentBanditHerd;
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		public Random m_random = new Random();
		public StateMachine m_stateMachine = new StateMachine();

		// Configuration fields
		public float m_dayChaseRange;
		public float m_nightChaseRange;
		public float m_dayChaseTime;
		public float m_nightChaseTime;
		public float m_chaseNonPlayerProbability;
		public float m_chaseWhenAttackedProbability;
		public float m_chaseOnTouchProbability;
		public CreatureCategory m_autoChaseMask;

		// Runtime state
		public float m_importanceLevel;
		public float m_targetUnsuitableTime;
		public float m_targetInRangeTime;
		public double m_nextUpdateTime;
		public ComponentCreature m_target;
		public float m_dt;
		public float m_range;
		public float m_chaseTime;
		public bool m_isPersistent;
		public float m_autoChaseSuppressionTime;

		// Tweakable values
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

		// Public methods
		public virtual void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (Suppressed) return;

			// No atacar a miembros de la misma manada (excepto si es modo narcotraficante y el objetivo es jugador)
			if (!(IsDrugTraffickerMode && componentCreature.Entity.FindComponent<ComponentPlayer>() != null) &&
				IsSameBanditHerd(componentCreature))
				return;

			m_target = componentCreature;
			IsActive = true;   // Activar el comportamiento
			m_nextUpdateTime = 0.0;
			m_range = maxRange;
			m_chaseTime = maxChaseTime;
			m_isPersistent = isPersistent;
			m_importanceLevel = isPersistent ? ImportanceLevelPersistent : ImportanceLevelNonPersistent;

			// Forzar la transición inmediata al estado "Chasing" para que empiece a moverse
			m_stateMachine.TransitionTo("Chasing");
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

		public virtual void Update(float dt)
		{
			if (Suppressed)
			{
				StopAttack();
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
						float x = m_isPersistent ? m_random.Float(8f, 10f) : 2f;
						m_chaseTime = MathUtils.Max(m_chaseTime, x);

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

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Subsystems
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);

			// Components
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentFeedBehavior = Entity.FindComponent<ComponentRandomFeedBehavior>();
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentFactors = Entity.FindComponent<ComponentFactors>(true);
			m_componentBanditHerd = Entity.FindComponent<ComponentBanditHerdBehavior>();

			// Cargar el nombre de la manada DESDE LA PLANTILLA
			HerdName = valuesDictionary.GetValue<string>("HerdName", null);

			// Si no se especificó HerdName en la plantilla del chase, intentar obtenerlo del herd behavior
			if (string.IsNullOrEmpty(HerdName) && m_componentBanditHerd != null)
			{
				HerdName = m_componentBanditHerd.HerdName;
			}

			// Load configuration
			m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");
			m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask");
			m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
			m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
			m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");

			// Cargar el modo narcotraficante (por defecto false)
			IsDrugTraffickerMode = valuesDictionary.GetValue<bool>("IsDrugTraffickerMode", false);

			// Bandit-specific defaults
			m_autoChaseMask = CreatureCategory.LandPredator | CreatureCategory.LandOther |
							  CreatureCategory.WaterPredator | CreatureCategory.WaterOther |
							  CreatureCategory.Bird;
			m_dayChaseRange = Math.Max(m_dayChaseRange, 20f);
			m_nightChaseRange = Math.Max(m_nightChaseRange, 25f);
			m_dayChaseTime = Math.Max(m_dayChaseTime, 60f);
			m_nightChaseTime = Math.Max(m_nightChaseTime, 90f);
			AttacksPlayer = true;
			AttacksNonPlayerCreature = true;
			m_chaseWhenAttackedProbability = 1f;
			ImportanceLevelPersistent = 300f;

			// Events
			ComponentBody componentBody = m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(
				componentBody.CollidedWithBody,
				new Action<ComponentBody>(OnCollidedWithBody));

			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(
				componentHealth.Injured,
				new Action<Injury>(OnInjured));

			InitializeStateMachine();
			m_stateMachine.TransitionTo("LookingForTarget");
		}

		protected void InitializeStateMachine()
		{
			m_stateMachine.AddState("LookingForTarget",
				delegate { m_importanceLevel = 0f; m_target = null; },
				LookingForTargetUpdate,
				null);

			m_stateMachine.AddState("RandomMoving",
				RandomMovingEnter,
				RandomMovingUpdate,
				RandomMovingLeave);

			m_stateMachine.AddState("Chasing",
				ChasingEnter,
				ChasingUpdate,
				null);
		}

		protected void LookingForTargetUpdate()
		{
			// Modo narcotraficante: perseguir jugadores inmediatamente
			if (IsDrugTraffickerMode)
			{
				// Si ya estamos persiguiendo a un jugador y sigue activo, no hacer nada
				if (IsActive && m_target != null && m_target.Entity.FindComponent<ComponentPlayer>() != null)
					return;

				// Buscar cualquier jugador
				ComponentCreature playerTarget = FindNearestPlayer();
				if (playerTarget != null)
				{
					// Atacar con rango grande y persistente (nunca se rinde hasta que termine la guerra)
					Attack(playerTarget, 50f, float.MaxValue, true);
				}
				return;
			}

			// Comportamiento normal (sin modo narcotraficante)
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

				ComponentCreature potentialTarget = FindTarget();
				if (potentialTarget != null)
					m_targetInRangeTime += m_dt;
				else
					m_targetInRangeTime = 0f;

				if (m_targetInRangeTime > TargetInRangeTimeToChase)
				{
					bool isDay = m_subsystemSky.SkyLightIntensity >= 0.1f;
					float maxRange = isDay ? (m_dayChaseRange + 6f) : (m_nightChaseRange + 6f);
					float maxChaseTime = isDay ? (m_dayChaseTime * m_random.Float(0.75f, 1f)) : (m_nightChaseTime * m_random.Float(0.75f, 1f));
					Attack(potentialTarget, maxRange, maxChaseTime, !isDay);
				}
			}
		}

		protected ComponentCreature FindNearestPlayer()
		{
			ComponentCreature nearest = null;
			float bestDistSq = float.MaxValue;
			Vector3 myPos = m_componentCreature.ComponentBody.Position;

			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player.ComponentHealth.Health > 0f)
				{
					float distSq = Vector3.DistanceSquared(myPos, player.ComponentBody.Position);
					if (distSq < bestDistSq)
					{
						bestDistSq = distSq;
						nearest = player;
					}
				}
			}
			return nearest;
		}

		protected void RandomMovingEnter()
		{
			Vector3 pos = m_componentCreature.ComponentBody.Position;
			Vector3 dest = pos + new Vector3(6f * m_random.Float(-1f, 1f), 0f, 6f * m_random.Float(-1f, 1f));
			m_componentPathfinding.SetDestination(dest, 1f, 1f, 0, false, true, false, null);
		}

		protected void RandomMovingUpdate()
		{
			if (m_componentPathfinding.IsStuck || m_componentPathfinding.Destination == null)
				m_stateMachine.TransitionTo("Chasing");
			if (!IsActive)
				m_stateMachine.TransitionTo("LookingForTarget");
		}

		protected void RandomMovingLeave()
		{
			m_componentPathfinding.Stop();
		}

		protected void ChasingEnter()
		{
			m_subsystemNoise.MakeNoise(m_componentCreature.ComponentBody, 0.25f, 6f);
			if (PlayIdleSoundWhenStartToChase)
				m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
			m_nextUpdateTime = 0.0;
		}

		protected void ChasingUpdate()
		{
			if (!IsActive)
			{
				m_stateMachine.TransitionTo("LookingForTarget");
			}
			else if (m_chaseTime <= 0f && !IsDrugTraffickerMode)
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
					m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + m_random.Float(1f, 3f), delegate
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

				if (m_targetUnsuitableTime > 3f && !IsDrugTraffickerMode)
				{
					m_importanceLevel = 0f;
				}
				else
				{
					int maxPath = m_isPersistent ? (m_subsystemTime.FixedTimeStep != null ? 2000 : 500) : 0;
					BoundingBox myBox = m_componentCreature.ComponentBody.BoundingBox;
					BoundingBox targetBox = m_target.ComponentBody.BoundingBox;
					Vector3 myCenter = 0.5f * (myBox.Min + myBox.Max);
					Vector3 targetCenter = 0.5f * (targetBox.Min + targetBox.Max);
					float dist = Vector3.Distance(myCenter, targetCenter);
					float factor = (dist < 4f) ? 0.2f : 0f;
					Vector3 dest = targetCenter + factor * dist * m_target.ComponentBody.Velocity;
					m_componentPathfinding.SetDestination(dest, 1f, 1.5f, maxPath, true, false, true, m_target.ComponentBody);

					if (PlayAngrySoundWhenChasing && m_random.Float(0f, 1f) < 0.33f * m_dt)
						m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
				}
			}
		}

		public virtual ComponentCreature FindTarget()
		{
			Vector3 pos = m_componentCreature.ComponentBody.Position;
			ComponentCreature best = null;
			float bestScore = 0f;
			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(pos.X, pos.Z), m_range, m_componentBodies);

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

		public virtual float ScoreTarget(ComponentCreature componentCreature)
		{
			// En modo narcotraficante, dar máxima prioridad a los jugadores
			if (IsDrugTraffickerMode)
			{
				if (componentCreature.Entity.FindComponent<ComponentPlayer>() != null)
					return float.MaxValue;
				else
					return 0f;
			}

			// Comportamiento normal: NO atacar a miembros de la misma manada
			if (IsSameBanditHerd(componentCreature))
				return 0f;

			float score = 0f;
			bool isPlayer = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
			bool canHuntOnLand = m_componentCreature.Category != CreatureCategory.WaterPredator &&
								 m_componentCreature.Category != CreatureCategory.WaterOther;
			bool isValidGameMode = isPlayer ? (m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) : true;
			bool isValidCategory = (componentCreature.Category & m_autoChaseMask) > (CreatureCategory)0;
			bool shouldChaseNonPlayer = !isPlayer && MathUtils.Remainder(0.005 * m_subsystemTime.GameTime +
				(double)(GetHashCode() % 1000 / 1000f) + (double)(componentCreature.GetHashCode() % 1000 / 1000f), 1.0) < m_chaseNonPlayerProbability;

			if (componentCreature != m_componentCreature &&
				((!isPlayer && shouldChaseNonPlayer) || (isPlayer && isValidGameMode)) &&
				componentCreature.Entity.IsAddedToProject &&
				componentCreature.ComponentHealth.Health > 0f &&
				(canHuntOnLand || IsTargetInWater(componentCreature.ComponentBody)))
			{
				float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position, componentCreature.ComponentBody.Position);
				if (dist < m_range)
					score = m_range - dist;
			}
			return score;
		}

		public virtual bool IsTargetInWater(ComponentBody target)
		{
			if (target.ImmersionDepth > 0f) return true;
			if (target.ParentBody != null && IsTargetInWater(target.ParentBody)) return true;
			if (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && IsTargetInWater(target.StandingOnBody)) return true;
			return false;
		}

		public virtual bool IsTargetInAttackRange(ComponentBody target)
		{
			if (IsBodyInAttackRange(target)) return true;

			BoundingBox myBox = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox targetBox = target.BoundingBox;
			Vector3 myCenter = 0.5f * (myBox.Min + myBox.Max);
			Vector3 offset = 0.5f * (targetBox.Min + targetBox.Max) - myCenter;
			float length = offset.Length();
			Vector3 dir = offset / length;
			float widthSum = 0.5f * (myBox.Max.X - myBox.Min.X + targetBox.Max.X - targetBox.Min.X);
			float heightSum = 0.5f * (myBox.Max.Y - myBox.Min.Y + targetBox.Max.Y - targetBox.Min.Y);

			if (MathF.Abs(offset.Y) < heightSum * 0.99f)
			{
				if (length < widthSum + 0.99f && Vector3.Dot(dir, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (length < heightSum + 0.3f && MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
			{
				return true;
			}

			if (target.ParentBody != null && IsTargetInAttackRange(target.ParentBody)) return true;
			if (AllowAttackingStandingOnBody && target.StandingOnBody != null &&
				target.StandingOnBody.Position.Y < target.Position.Y && IsTargetInAttackRange(target.StandingOnBody)) return true;
			return false;
		}

		public virtual bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox myBox = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox targetBox = target.BoundingBox;
			Vector3 myCenter = 0.5f * (myBox.Min + myBox.Max);
			Vector3 offset = 0.5f * (targetBox.Min + targetBox.Max) - myCenter;
			float length = offset.Length();
			Vector3 dir = offset / length;
			float widthSum = 0.5f * (myBox.Max.X - myBox.Min.X + targetBox.Max.X - targetBox.Min.X);
			float heightSum = 0.5f * (myBox.Max.Y - myBox.Min.Y + targetBox.Max.Y - targetBox.Min.Y);

			if (MathF.Abs(offset.Y) < heightSum * 0.99f)
			{
				if (length < widthSum + 0.99f && Vector3.Dot(dir, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (length < heightSum + 0.3f && MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
			{
				return true;
			}
			return false;
		}

		public virtual ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 start = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 end = target.BoundingBox.Center();
			Ray3 ray = new Ray3(start, Vector3.Normalize(end - start));
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

		// Comparar nombres de manada usando la propiedad HerdName
		protected bool IsSameBanditHerd(ComponentCreature other)
		{
			if (string.IsNullOrEmpty(HerdName) || other == null)
				return false;

			ComponentBanditChaseBehavior otherChase = other.Entity.FindComponent<ComponentBanditChaseBehavior>();
			if (otherChase == null)
				return false;

			return string.Equals(HerdName, otherChase.HerdName, StringComparison.OrdinalIgnoreCase);
		}

		protected void OnCollidedWithBody(ComponentBody body)
		{
			if (m_target == null && m_autoChaseSuppressionTime <= 0f && m_random.Float(0f, 1f) < m_chaseOnTouchProbability)
			{
				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null && !IsSameBanditHerd(creature))
				{
					bool isPlayer = m_subsystemPlayers.IsPlayer(body.Entity);
					bool validCategory = (creature.Category & m_autoChaseMask) > (CreatureCategory)0;
					if ((AttacksPlayer && isPlayer && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
						(AttacksNonPlayerCreature && !isPlayer && validCategory))
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
		}

		protected void OnInjured(Injury injury)
		{
			ComponentCreature attacker = injury.Attacker;
			if (attacker != null && !IsSameBanditHerd(attacker) && m_random.Float(0f, 1f) < m_chaseWhenAttackedProbability)
			{
				float range = ChaseRangeOnAttacked ?? (m_chaseWhenAttackedProbability >= 1f ? 30f : 7f);
				float time = ChaseTimeOnAttacked ?? (m_chaseWhenAttackedProbability >= 1f ? 60f : 7f);
				bool persistent = ChasePersistentOnAttacked ?? (m_chaseWhenAttackedProbability >= 1f);
				Attack(attacker, range, time, persistent);
			}
		}
	}
}
