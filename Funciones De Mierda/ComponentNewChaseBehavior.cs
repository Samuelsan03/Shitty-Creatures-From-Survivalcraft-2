using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewChaseBehavior : ComponentBehavior, IUpdateable
	{
		// Propiedad pública para acceder al target
		public ComponentCreature Target => m_target;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		// Propiedades públicas configurables
		public float ImportanceLevelNonPersistent = 200f;
		public float ImportanceLevelPersistent = 200f;
		public float MaxAttackRange = 1.75f;
		public bool AllowAttackingStandingOnBody = true;
		public bool JumpWhenTargetStanding = true;
		public float MinHealthToAttackActively = 0.4f;
		public bool Suppressed;
		public bool PlayIdleSoundWhenStartToChase = true;
		public bool PlayAngrySoundWhenChasing = true;
		public float TargetInRangeTimeToChase = 3f;

		// NUEVAS PROPIEDADES PARA ATAQUE AL SER ATACADO
		public float? ChaseRangeOnAttacked { get; set; }
		public float? ChaseTimeOnAttacked { get; set; }
		public bool? ChasePersistentOnAttacked { get; set; }
		public float ChaseRangeOnTouch { get; set; } = 7f;
		public float ChaseTimeOnTouch { get; set; } = 7f;
		public bool AttacksPlayer { get; set; } = true;
		public bool AttacksNonPlayerCreature { get; set; } = true;

		// Nuevas propiedades para el sistema de manadas
		public bool IsCommandedAttack { get; private set; } // True cuando es ataque por comando (target stick)

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemSky m_subsystemSky;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTime m_subsystemTime;
		private SubsystemNoise m_subsystemNoise;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentMiner m_componentMiner;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentNewHerdBehavior m_componentHerd;
		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		private Random m_random = new Random();
		private StateMachine m_stateMachine = new StateMachine();

		protected ComponentCreature m_target;

		private float m_importanceLevel;
		private float m_targetUnsuitableTime;
		private float m_targetInRangeTime;
		private double m_nextUpdateTime;
		private float m_dt;
		private float m_range;
		private float m_chaseTime;
		private bool m_isPersistent;
		private float m_autoChaseSuppressionTime;

		// Variables de configuración
		private float m_dayChaseRange;
		private float m_nightChaseRange;
		private float m_dayChaseTime;
		private float m_nightChaseTime;
		private float m_chaseNonPlayerProbability;
		private float m_chaseWhenAttackedProbability;
		private float m_chaseOnTouchProbability;
		private CreatureCategory m_autoChaseMask;

		// Almacena si el ataque fue por comando
		private bool m_commandAttack;

		public virtual void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (Suppressed)
				return;

			if (m_componentHerd != null && !m_componentHerd.CanAttackCreature(target))
				return;

			m_target = target;
			m_nextUpdateTime = 0.0;
			m_range = maxRange;
			m_chaseTime = maxChaseTime;
			m_isPersistent = isPersistent;
			m_commandAttack = false;
			IsCommandedAttack = false;
			m_importanceLevel = isPersistent ? ImportanceLevelPersistent : ImportanceLevelNonPersistent;

			ModsManager.HookAction("OnChaseBehaviorStartChasing", delegate (ModLoader loader)
			{
				return false;
			});
		}

		public virtual void RespondToCommandImmediately(ComponentCreature target)
		{
			if (Suppressed || target == null)
				return;

			if (m_componentHerd != null && !m_componentHerd.CanAttackCreature(target))
				return;

			m_target = target;
			m_nextUpdateTime = 0.0;
			m_range = 30f;
			m_chaseTime = 45f;
			m_isPersistent = false;
			m_commandAttack = true;
			IsCommandedAttack = true;
			m_importanceLevel = ImportanceLevelPersistent;

			m_stateMachine.TransitionTo("Chasing");

			ModsManager.HookAction("OnChaseBehaviorStartChasing", delegate (ModLoader loader)
			{
				return false;
			});
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
			m_commandAttack = false;
			IsCommandedAttack = false;
			m_importanceLevel = 0f;

			ModsManager.HookAction("OnChaseBehaviorStopChasing", delegate (ModLoader loader)
			{
				return false;
			});
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
						float extraTime = m_commandAttack ? m_random.Float(8f, 10f) : 2f;
						m_chaseTime = MathUtils.Max(m_chaseTime, extraTime);

						bool bodyToHit = true;
						bool playAttackSound = true;

						ModsManager.HookAction("OnChaseBehaviorAttacked", delegate (ModLoader loader)
						{
							return false;
						});

						if (bodyToHit)
						{
							m_componentMiner.Hit(hitBody, hitPoint, m_componentCreature.ComponentBody.Matrix.Forward);
						}
						if (playAttackSound)
						{
							m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						}
					}
					else
					{
						ModsManager.HookAction("OnChaseBehaviorAttackFailed", delegate (ModLoader loader)
						{
							return false;
						});
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

		// Versión modificada de ScoreTarget que respeta el sistema de manadas
		public virtual float ScoreTarget(ComponentCreature creature)
		{
			// --- NUEVA VERIFICACIÓN: Si la criatura no es atacable según la manada, puntuación 0 ---
			if (m_componentHerd != null && !m_componentHerd.CanAttackCreature(creature))
				return 0f;
			// -------------------------------------------------------------------------------------

			float score = 0f;

			// Si es un ataque por comando, ignorar categorías y solo verificar manada (ya verificado arriba)
			if (m_commandAttack)
			{
				if (creature != m_componentCreature && creature != m_target &&
					creature.Entity.IsAddedToProject && creature.ComponentHealth.Health > 0f)
				{
					float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, creature.ComponentBody.Position);
					if (distance < m_range)
					{
						score = m_range - distance;
					}
				}

				ModsManager.HookAction("ChaseBehaviorScoreTarget", delegate (ModLoader loader)
				{
					return false;
				});

				return score;
			}

			// Comportamiento normal (respetando categorías)
			bool isPlayer = creature.Entity.FindComponent<ComponentPlayer>() != null;
			bool canAttackOnLand = m_componentCreature.Category != CreatureCategory.WaterPredator &&
								  m_componentCreature.Category != CreatureCategory.WaterOther;
			bool isTargetInMask = (creature.Category & m_autoChaseMask) > 0;
			bool shouldChaseNonPlayer = MathUtils.Remainder(0.004999999888241291 * m_subsystemTime.GameTime +
										(double)((float)(this.GetHashCode() % 1000) / 1000f) +
										(double)((float)(creature.GetHashCode() % 1000) / 1000f), 1.0) < (double)m_chaseNonPlayerProbability;

			if (creature != m_componentCreature &&
				((!isPlayer && isTargetInMask && shouldChaseNonPlayer) || (isPlayer && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless)) &&
				creature.Entity.IsAddedToProject && creature.ComponentHealth.Health > 0f &&
				(canAttackOnLand || IsTargetInWater(creature.ComponentBody)))
			{
				float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, creature.ComponentBody.Position);
				if (distance < m_range)
				{
					score = m_range - distance;
				}
			}

			ModsManager.HookAction("ChaseBehaviorScoreTarget", delegate (ModLoader loader)
			{
				return false;
			});

			return score;
		}

		public virtual ComponentCreature FindTarget()
		{
			Vector3 position = m_componentCreature.ComponentBody.Position;
			ComponentCreature bestTarget = null;
			float bestScore = 0f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), m_range, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					// Verificar si podemos atacar a esta criatura según el sistema de manadas
					if (m_componentHerd != null && !m_componentHerd.CanAttackCreature(creature))
						continue;

					float score = ScoreTarget(creature);
					if (score > bestScore)
					{
						bestScore = score;
						bestTarget = creature;
					}
				}
			}

			return bestTarget;
		}

		// El resto de métodos (IsTargetInWater, IsTargetInAttackRange, etc.) se mantienen igual...
		public virtual bool IsTargetInWater(ComponentBody target)
		{
			return target.ImmersionDepth > 0f ||
				   (target.ParentBody != null && IsTargetInWater(target.ParentBody)) ||
				   (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y &&
					IsTargetInWater(target.StandingOnBody));
		}

		public virtual bool IsTargetInAttackRange(ComponentBody target)
		{
			if (IsBodyInAttackRange(target))
				return true;

			BoundingBox box = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox targetBox = target.BoundingBox;

			Vector3 center = 0.5f * (box.Min + box.Max);
			Vector3 targetCenter = 0.5f * (targetBox.Min + targetBox.Max) - center;

			float distance = targetCenter.Length();
			Vector3 direction = targetCenter / distance;

			float width = 0.5f * (box.Max.X - box.Min.X + targetBox.Max.X - targetBox.Min.X);
			float height = 0.5f * (box.Max.Y - box.Min.Y + targetBox.Max.Y - targetBox.Min.Y);

			if (MathF.Abs(targetCenter.Y) < height * 0.99f)
			{
				if (distance < width + 0.99f && Vector3.Dot(direction, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (distance < height + 0.3f && MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.8f)
			{
				return true;
			}

			return (target.ParentBody != null && IsTargetInAttackRange(target.ParentBody)) ||
				   (AllowAttackingStandingOnBody && target.StandingOnBody != null &&
					target.StandingOnBody.Position.Y < target.Position.Y &&
					IsTargetInAttackRange(target.StandingOnBody));
		}

		public virtual bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox box = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox targetBox = target.BoundingBox;

			Vector3 center = 0.5f * (box.Min + box.Max);
			Vector3 targetCenter = 0.5f * (targetBox.Min + targetBox.Max) - center;

			float distance = targetCenter.Length();
			Vector3 direction = targetCenter / distance;

			float width = 0.5f * (box.Max.X - box.Min.X + targetBox.Max.X - targetBox.Min.X);
			float height = 0.5f * (box.Max.Y - box.Min.Y + targetBox.Max.Y - targetBox.Min.Y);

			if (MathF.Abs(targetCenter.Y) < height * 0.99f)
			{
				if (distance < width + 0.99f && Vector3.Dot(direction, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (distance < height + 0.3f && MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.8f)
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
				(result.Value.ComponentBody == target || result.Value.ComponentBody.IsChildOfBody(target) ||
				 target.IsChildOfBody(result.Value.ComponentBody) ||
				 (target.StandingOnBody == result.Value.ComponentBody && AllowAttackingStandingOnBody)))
			{
				hitPoint = result.Value.HitPoint();
				return result.Value.ComponentBody;
			}

			hitPoint = default(Vector3);
			return null;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentHerd = base.Entity.FindComponent<ComponentNewHerdBehavior>();

			m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");
			m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask");
			m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
			m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
			m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");

			if (valuesDictionary.ContainsKey("ChaseRangeOnAttacked"))
				ChaseRangeOnAttacked = valuesDictionary.GetValue<float>("ChaseRangeOnAttacked");
			if (valuesDictionary.ContainsKey("ChaseTimeOnAttacked"))
				ChaseTimeOnAttacked = valuesDictionary.GetValue<float>("ChaseTimeOnAttacked");
			if (valuesDictionary.ContainsKey("ChasePersistentOnAttacked"))
				ChasePersistentOnAttacked = valuesDictionary.GetValue<bool>("ChasePersistentOnAttacked");

			ComponentBody componentBody = m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody += delegate (ComponentBody body)
			{
				if (m_target == null && m_autoChaseSuppressionTime <= 0f &&
					m_random.Float(0f, 1f) < m_chaseOnTouchProbability)
				{
					ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
					if (creature != null)
					{
						bool isPlayer = m_subsystemPlayers.IsPlayer(body.Entity);
						bool inMask = (creature.Category & m_autoChaseMask) > 0;

						if (m_componentHerd != null && !m_componentHerd.CanAttackCreature(creature))
							return;

						if ((AttacksPlayer && isPlayer && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
							(AttacksNonPlayerCreature && !isPlayer && inMask))
						{
							this.Attack(creature, ChaseRangeOnTouch, ChaseTimeOnTouch, false);
						}
					}
				}
			};

			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			componentHealth.Injured += delegate (Injury injury)
			{
				ComponentCreature attacker = injury?.Attacker;
				if (attacker == null)
					return;

				// Cambio: Usar ShouldAttackCreature para determinar si debemos ignorar el ataque
				if (m_componentHerd != null && !m_componentHerd.ShouldAttackCreature(attacker))
					return;

				// Cambio: Usar IsSameHerdOrGuardian para verificar si es del mismo grupo amigable
				if (m_componentHerd != null && m_componentHerd.IsSameHerdOrGuardian(attacker))
					return;

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

					this.Attack(attacker, range, time, persistent);
				}
			};

			// Estados de la máquina de estados (sin cambios)
			m_stateMachine.AddState("LookingForTarget", delegate
			{
				m_importanceLevel = 0f;
				m_target = null;
			}, delegate
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
					m_range = ((m_subsystemSky.SkyLightIntensity < 0.2f) ? m_nightChaseRange : m_dayChaseRange);

					ComponentCreature target = FindTarget();
					if (target != null)
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
						float maxTime = isDay ? (m_dayChaseTime * m_random.Float(0.75f, 1f)) :
											   (m_nightChaseTime * m_random.Float(0.75f, 1f));

						Attack(target, maxRange, maxTime, !isDay);
					}
				}
			}, null);

			m_stateMachine.AddState("RandomMoving", delegate
			{
				Vector3 offset = new Vector3(6f * m_random.Float(-1f, 1f), 0f, 6f * m_random.Float(-1f, 1f));
				m_componentPathfinding.SetDestination(new Vector3?(m_componentCreature.ComponentBody.Position + offset),
					1f, 1f, 0, false, true, false, null);
			}, delegate
			{
				if (m_componentPathfinding.IsStuck || m_componentPathfinding.Destination == null)
				{
					m_stateMachine.TransitionTo("Chasing");
				}
				if (!IsActive)
				{
					m_stateMachine.TransitionTo("LookingForTarget");
				}
			}, delegate
			{
				m_componentPathfinding.Stop();
			});

			m_stateMachine.AddState("Chasing", delegate
			{
				m_subsystemNoise.MakeNoise(m_componentCreature.ComponentBody, 0.25f, 6f);
				if (PlayIdleSoundWhenStartToChase)
				{
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}
				m_nextUpdateTime = 0.0;
			}, delegate
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
						int maxPathfinding = m_isPersistent ? ((m_subsystemTime.FixedTimeStep != null) ? 2000 : 500) : 0;

						BoundingBox box = m_componentCreature.ComponentBody.BoundingBox;
						BoundingBox targetBox = m_target.ComponentBody.BoundingBox;

						Vector3 center = 0.5f * (box.Min + box.Max);
						Vector3 targetCenter = 0.5f * (targetBox.Min + targetBox.Max);

						float distance = Vector3.Distance(center, targetCenter);
						float factor = (distance < 4f) ? 0.2f : 0f;

						Vector3 destination = targetCenter + factor * distance * m_target.ComponentBody.Velocity;

						m_componentPathfinding.SetDestination(new Vector3?(destination), 1f, 1.5f,
							maxPathfinding, true, false, true, m_target.ComponentBody);

						if (PlayAngrySoundWhenChasing && m_random.Float(0f, 1f) < 0.33f * m_dt)
						{
							m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						}
					}
				}
			}, null);

			m_stateMachine.TransitionTo("LookingForTarget");
		}
	}
}
