using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBanditChaseBehavior : ComponentBehavior, IUpdateable
	{
		// Propiedades específicas del bandido
		public bool IsDrugTraffickerMode { get; set; }
		public bool AttackAllCreatures { get; set; }

		private ComponentBanditHerdBehavior m_banditHerd;
		private SubsystemBanditInvasion m_subsystemBanditInvasion;

		// Campos copiados de ComponentChaseBehavior (sin hooks)
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

		// Parámetros configurables (iguales a los de la clase base)
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

		// Propiedades IUpdateable
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// Propiedad para acceder al objetivo
		public ComponentCreature Target => m_target;

		public override float ImportanceLevel => m_importanceLevel;

		// Métodos principales
		public virtual void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (Suppressed) return;

			m_target = componentCreature;
			m_nextUpdateTime = 0.0;
			m_range = maxRange;
			m_chaseTime = maxChaseTime;
			m_isPersistent = isPersistent;
			m_importanceLevel = isPersistent ? ImportanceLevelPersistent : ImportanceLevelNonPersistent;
			// Sin hooks
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
			// Sin hooks
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
						float chaseTimeBefore = m_chaseTime;
						float x = m_isPersistent ? m_random.Float(8f, 10f) : 2f;
						m_chaseTime = MathUtils.Max(m_chaseTime, x);
						// Sin hooks: siempre golpear y reproducir sonido
						m_componentMiner.Hit(hitBody, hitPoint, m_componentCreature.ComponentBody.Matrix.Forward);
						m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
					}
					else
					{
						// Sin hooks
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
			// Cargar propiedades del bandido
			IsDrugTraffickerMode = valuesDictionary.GetValue<bool>("IsDrugTraffickerMode", false);
			AttackAllCreatures = valuesDictionary.GetValue<bool>("AttackAllCreatures", false);

			// Inicializar subsistemas (igual que base)
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentFeedBehavior = Entity.FindComponent<ComponentRandomFeedBehavior>();
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentFactors = Entity.FindComponent<ComponentFactors>(true);

			// Cargar parámetros de persecución
			m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");
			m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask");
			m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
			m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
			m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");

			// Evento de colisión (copia exacta sin hooks)
			ComponentBody componentBody = m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(componentBody.CollidedWithBody, new Action<ComponentBody>(delegate (ComponentBody body)
			{
				if (m_target == null && m_autoChaseSuppressionTime <= 0f && m_random.Float(0f, 1f) < m_chaseOnTouchProbability)
				{
					ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						bool isPlayer = m_subsystemPlayers.IsPlayer(body.Entity);
						bool isAutoChaseTarget = (componentCreature.Category & m_autoChaseMask) > (CreatureCategory)0;
						if ((AttacksPlayer && isPlayer && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) || (AttacksNonPlayerCreature && !isPlayer && isAutoChaseTarget))
						{
							Attack(componentCreature, ChaseRangeOnTouch, ChaseTimeOnTouch, false);
						}
					}
				}
				if (m_target != null && JumpWhenTargetStanding && body == m_target.ComponentBody && body.StandingOnBody == m_componentCreature.ComponentBody)
				{
					m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
				}
			}));

			// Evento de daño (copia exacta sin hooks)
			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				if (m_random.Float(0f, 1f) < m_chaseWhenAttackedProbability)
				{
					bool persistent = false;
					float range, time;
					if (m_chaseWhenAttackedProbability >= 1f)
					{
						range = 30f;
						time = 60f;
						persistent = true;
					}
					else
					{
						range = 7f;
						time = 7f;
					}
					range = ChaseRangeOnAttacked.GetValueOrDefault(range);
					time = ChaseTimeOnAttacked.GetValueOrDefault(time);
					persistent = ChasePersistentOnAttacked.GetValueOrDefault(persistent);
					Attack(attacker, range, time, persistent);
				}
			}));

			// Configurar máquina de estados (sin hooks)
			m_stateMachine.AddState("LookingForTarget",
				delegate
				{
					m_importanceLevel = 0f;
					m_target = null;
				},
				delegate
				{
					if (IsActive)
					{
						m_stateMachine.TransitionTo("Chasing");
						return;
					}
					if (!Suppressed && m_autoChaseSuppressionTime <= 0f && (m_target == null || ScoreTarget(m_target) <= 0f) && m_componentCreature.ComponentHealth.Health > MinHealthToAttackActively)
					{
						m_range = ((m_subsystemSky.SkyLightIntensity < 0.2f) ? m_nightChaseRange : m_dayChaseRange);
						m_range *= m_componentFactors.GetOtherFactorResult("ChaseRange", false, false);
						ComponentCreature found = FindTarget();
						if (found != null)
						{
							m_targetInRangeTime += m_dt;
						}
						else
						{
							m_targetInRangeTime = 0f;
						}
						if (m_targetInRangeTime > TargetInRangeTimeToChase)
						{
							bool isDay = m_subsystemSky.SkyLightIntensity >= 0.1f;
							float maxRange = isDay ? (m_dayChaseRange + 6f) : (m_nightChaseRange + 6f);
							float maxChaseTime = isDay ? (m_dayChaseTime * m_random.Float(0.75f, 1f)) : (m_nightChaseTime * m_random.Float(0.75f, 1f));
							Attack(found, maxRange, maxChaseTime, !isDay);
						}
					}
					// Sin hooks
				},
				null
			);

			m_stateMachine.AddState("RandomMoving",
				delegate
				{
					m_componentPathfinding.SetDestination(new Vector3?(m_componentCreature.ComponentBody.Position + new Vector3(6f * m_random.Float(-1f, 1f), 0f, 6f * m_random.Float(-1f, 1f))), 1f, 1f, 0, false, true, false, null);
				},
				delegate
				{
					if (m_componentPathfinding.IsStuck || m_componentPathfinding.Destination == null)
					{
						m_stateMachine.TransitionTo("Chasing");
					}
					if (!IsActive)
					{
						m_stateMachine.TransitionTo("LookingForTarget");
					}
				},
				delegate
				{
					m_componentPathfinding.Stop();
				}
			);

			m_stateMachine.AddState("Chasing",
				delegate
				{
					m_subsystemNoise.MakeNoise(m_componentCreature.ComponentBody, 0.25f, 6f);
					if (PlayIdleSoundWhenStartToChase)
					{
						m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
					}
					m_nextUpdateTime = 0.0;
				},
				delegate
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
							m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + (double)m_random.Float(1f, 3f), delegate
							{
								if (m_target != null)
								{
									m_componentFeedBehavior.Feed(m_target.ComponentBody.Position);
								}
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
						{
							m_targetUnsuitableTime += m_dt;
						}
						else
						{
							m_targetUnsuitableTime = 0f;
						}
						if (m_targetUnsuitableTime > 3f)
						{
							m_importanceLevel = 0f;
						}
						else
						{
							int maxPathfindingPositions = m_isPersistent ? ((m_subsystemTime.FixedTimeStep != null) ? 2000 : 500) : 0;
							BoundingBox bbSelf = m_componentCreature.ComponentBody.BoundingBox;
							BoundingBox bbTarget = m_target.ComponentBody.BoundingBox;
							Vector3 centerSelf = 0.5f * (bbSelf.Min + bbSelf.Max);
							Vector3 centerTarget = 0.5f * (bbTarget.Min + bbTarget.Max);
							float dist = Vector3.Distance(centerSelf, centerTarget);
							float offset = (dist < 4f) ? 0.2f : 0f;
							m_componentPathfinding.SetDestination(
								new Vector3?(centerTarget + offset * dist * m_target.ComponentBody.Velocity),
								1f, 1.5f, maxPathfindingPositions, true, false, true, m_target.ComponentBody);
							if (PlayAngrySoundWhenChasing && m_random.Float(0f, 1f) < 0.33f * m_dt)
							{
								m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
							}
						}
					}
					// Sin hooks
				},
				null
			);

			m_stateMachine.TransitionTo("LookingForTarget");

			// Inicialización específica del bandido
			m_banditHerd = Entity.FindComponent<ComponentBanditHerdBehavior>();
			m_subsystemBanditInvasion = Project.FindSubsystem<SubsystemBanditInvasion>(true);

			if (m_subsystemBanditInvasion != null)
			{
				bool globalInvasionActive = m_subsystemBanditInvasion.IsInvasionActive;
				if (IsDrugTraffickerMode != globalInvasionActive)
				{
					IsDrugTraffickerMode = globalInvasionActive;
					if (!globalInvasionActive)
						StopAttack();
				}
			}

			if (m_banditHerd != null)
				m_banditHerd.HerdName = "bandits";
		}

		public virtual ComponentCreature FindTarget()
		{
			Vector3 position = m_componentCreature.ComponentBody.Position;
			ComponentCreature result = null;
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
						result = creature;
					}
				}
			}
			return result;
		}

		public virtual float ScoreTarget(ComponentCreature target)
		{
			if (target == null || target == m_componentCreature)
				return 0f;

			if (target.ComponentHealth.Health <= 0f)
				return 0f;

			bool isPlayer = target.Entity.FindComponent<ComponentPlayer>() != null;
			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);
			float currentRange = (m_subsystemSky.SkyLightIntensity < 0.2f) ? m_nightChaseRange : m_dayChaseRange;
			currentRange *= m_componentFactors.GetOtherFactorResult("ChaseRange", false, false);

			if (distance >= currentRange)
				return 0f;

			// Verificar estado global de invasión
			bool invasionActive = (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive);
			bool drugMode = IsDrugTraffickerMode || invasionActive;

			// Modo narcotraficante: perseguir jugador obsesivamente
			if (drugMode && isPlayer)
				return (currentRange - distance) * 1000f;

			// Modo atacar a TODAS las criaturas (excepto jugador)
			if (AttackAllCreatures && !isPlayer)
			{
				CreatureCategory targetCategory = target.Category;
				if (targetCategory == CreatureCategory.LandPredator ||
					targetCategory == CreatureCategory.LandOther ||
					targetCategory == CreatureCategory.WaterPredator ||
					targetCategory == CreatureCategory.WaterOther ||
					targetCategory == CreatureCategory.Bird)
				{
					return currentRange - distance;
				}
			}

			// Lógica de persecución original (copiada de la clase base)
			bool isPlayerTarget = isPlayer;
			bool isWaterPredatorOrOther = (m_componentCreature.Category == CreatureCategory.WaterPredator || m_componentCreature.Category == CreatureCategory.WaterOther);
			bool canAttackPlayer = (isPlayerTarget && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless);
			bool canAttackNonPlayer = (!isPlayerTarget && (target.Category & m_autoChaseMask) > (CreatureCategory)0);
			bool isTargetValid = (canAttackPlayer || canAttackNonPlayer) && target.Entity.IsAddedToProject && target.ComponentHealth.Health > 0f;

			if (!isTargetValid)
				return 0f;

			// Si es jugador y no estamos en modo droga, no atacar (a menos que sea por autoChaseMask)
			if (isPlayerTarget)
				return 0f; // El jugador solo es atacado en modo droga o si está en la máscara, pero lo hemos excluido arriba

			// Para criaturas no jugador, aplicar probabilidad de ataque
			if (!isPlayerTarget && !drugMode && !AttackAllCreatures)
			{
				// Probabilidad de ataque (similar a base)
				double chance = 0.004999999888241291 * m_subsystemTime.GameTime + (double)((float)(GetHashCode() % 1000) / 1000f) + (double)((float)(target.GetHashCode() % 1000) / 1000f);
				if (MathUtils.Remainder(chance, 1.0) >= m_chaseNonPlayerProbability)
					return 0f;
			}

			// Si es criatura acuática pero el bandido no es acuático, no atacar (a menos que esté en agua)
			if (!isWaterPredatorOrOther && !IsTargetInWater(target.ComponentBody))
				return 0f;

			return currentRange - distance;
		}

		public virtual bool IsTargetInWater(ComponentBody target)
		{
			return target.ImmersionDepth > 0f || (target.ParentBody != null && IsTargetInWater(target.ParentBody)) || (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && IsTargetInWater(target.StandingOnBody));
		}

		public virtual bool IsTargetInAttackRange(ComponentBody target)
		{
			if (IsBodyInAttackRange(target))
				return true;

			BoundingBox bbSelf = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox bbTarget = target.BoundingBox;
			Vector3 centerSelf = 0.5f * (bbSelf.Min + bbSelf.Max);
			Vector3 centerTarget = 0.5f * (bbTarget.Min + bbTarget.Max) - centerSelf;
			float dist = centerTarget.Length();
			Vector3 dir = centerTarget / dist;
			float halfWidth = 0.5f * (bbSelf.Max.X - bbSelf.Min.X + bbTarget.Max.X - bbTarget.Min.X);
			float halfHeight = 0.5f * (bbSelf.Max.Y - bbSelf.Min.Y + bbTarget.Max.Y - bbTarget.Min.Y);

			if (MathF.Abs(centerTarget.Y) < halfHeight * 0.99f)
			{
				if (dist < halfWidth + 0.99f && Vector3.Dot(dir, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (dist < halfHeight + 0.3f && MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
			{
				return true;
			}

			return (target.ParentBody != null && IsTargetInAttackRange(target.ParentBody)) ||
				   (AllowAttackingStandingOnBody && target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && IsTargetInAttackRange(target.StandingOnBody));
		}

		public virtual bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox bbSelf = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox bbTarget = target.BoundingBox;
			Vector3 centerSelf = 0.5f * (bbSelf.Min + bbSelf.Max);
			Vector3 centerTarget = 0.5f * (bbTarget.Min + bbTarget.Max) - centerSelf;
			float dist = centerTarget.Length();
			Vector3 dir = centerTarget / dist;
			float halfWidth = 0.5f * (bbSelf.Max.X - bbSelf.Min.X + bbTarget.Max.X - bbTarget.Min.X);
			float halfHeight = 0.5f * (bbSelf.Max.Y - bbSelf.Min.Y + bbTarget.Max.Y - bbTarget.Min.Y);

			if (MathF.Abs(centerTarget.Y) < halfHeight * 0.99f)
			{
				if (dist < halfWidth + 0.99f && Vector3.Dot(dir, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (dist < halfHeight + 0.3f && MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
			{
				return true;
			}
			return false;
		}

		public virtual ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 origin = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 direction = Vector3.Normalize(target.BoundingBox.Center() - origin);
			Ray3 ray = new Ray3(origin, direction);
			BodyRaycastResult? result = m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			if (result != null && result.Value.Distance < MaxAttackRange &&
				(result.Value.ComponentBody == target || result.Value.ComponentBody.IsChildOfBody(target) || target.IsChildOfBody(result.Value.ComponentBody) ||
				(target.StandingOnBody == result.Value.ComponentBody && AllowAttackingStandingOnBody)))
			{
				hitPoint = result.Value.HitPoint();
				return result.Value.ComponentBody;
			}
			hitPoint = default(Vector3);
			return null;
		}
	}
}
