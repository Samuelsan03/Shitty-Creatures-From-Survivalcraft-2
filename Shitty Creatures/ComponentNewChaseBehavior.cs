using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using System.Collections.Generic;

namespace Game
{
	public class ComponentNewChaseBehavior : ComponentBehavior, IUpdateable
	{
		// ===== PROPIEDADES PÚBLICAS =====
		public ComponentCreature Target => m_target;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;
		public bool IsProtectingPlayer => ShouldProtectPlayer;

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
		public bool DestroyBlocksWhenStuck = false;
		public bool InvokeLightningOnHit = false;
		public bool PushWhileAttacking = false;
		public bool ExplodeOnHit = false;
		public bool PlaceBlocksWhenTargetHigh = false;
		private float m_greenNightProtectionTimer;
		private bool m_wasGreenNightActiveForProtection;

		// ===== CAMPOS PRIVADOS =====
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemSky m_subsystemSky;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTime m_subsystemTime;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private SubsystemBanditInvasion m_subsystemBanditInvasion;

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
		private bool m_isForcedTarget;

		private Vector3 m_lastStuckCheckPosition;
		private double m_stuckDetectionStartTime;
		private double m_lastLateralMoveTime;

		private List<Point3> m_placedDirtBlocks = new List<Point3>();
		private double m_lastBlockPlaceTime;
		private const float BlockPlaceCooldown = 0.5f;

		private bool m_waitingForDestructionBeforeBuild;

		private double m_stuckStartTime;

		private float m_dayChaseRange;
		private float m_nightChaseRange;
		private float m_dayChaseTime;
		private float m_nightChaseTime;
		private float m_chaseNonPlayerProbability;
		private float m_chaseWhenAttackedProbability;
		private float m_chaseOnTouchProbability;
		private CreatureCategory m_autoChaseMask;

		// Suscripción a eventos de salud de jugadores
		private List<ComponentHealth> m_subscribedPlayerHealths = new List<ComponentHealth>();

		// ===== PROPIEDADES AUXILIARES =====
		private bool IsZombie => HerdName != null && HerdName.ToLower().Contains("zombie");
		private bool IsBanditType => HerdName != null && HerdName.ToLower().Contains("bandits");
		private bool IsGreenNightActive => m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive;
		private float SpecialChaseRange => (IsZombie && IsGreenNightActive) || IsBanditType ? 50f : m_range;

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

		// ===== NUEVO: Protección extrema anti-bandidos =====
		private bool IsExtremeProtectionActive()
		{
			if (!ShouldProtectPlayer) return false;
			if (m_subsystemBanditInvasion == null) return false;
			if (!m_subsystemBanditInvasion.IsInvasionActive) return false;
			// Noche normal (mismo umbral que en SubsystemBanditInvasion)
			return m_subsystemSky.SkyLightIntensity < 0.1f;
		}

		private bool IsBanditCreature(ComponentCreature creature)
		{
			if (creature == null) return false;
			return creature.Entity.FindComponent<ComponentBanditChaseBehavior>() != null;
		}

		private ComponentCreature FindNearestBandit()
		{
			if (m_subsystemCreatureSpawn == null) return null;
			if (m_componentCreature?.ComponentBody == null) return null;

			Vector3 pos = m_componentCreature.ComponentBody.Position;
			ComponentCreature nearest = null;
			float bestDistSq = m_range * m_range;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature == m_componentCreature) continue;
				if (!IsBanditCreature(creature)) continue;
				if (creature.ComponentHealth.Health <= 0f) continue;

				float distSq = Vector3.DistanceSquared(pos, creature.ComponentBody.Position);
				if (distSq < bestDistSq)
				{
					bestDistSq = distSq;
					nearest = creature;
				}
			}
			return nearest;
		}

		// ===== MÉTODOS PÚBLICOS =====
		public virtual void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent, bool isForced = false)
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

			// Protección extrema: override para bandidos
			if (IsExtremeProtectionActive() && IsBanditCreature(target))
			{
				isPersistent = true;
				maxChaseTime = Math.Max(maxChaseTime, 120f);
				maxRange = Math.Max(maxRange, 40f);
			}

			m_target = target;
			m_nextUpdateTime = 0.0;
			m_range = maxRange;
			m_chaseTime = maxChaseTime;
			m_isPersistent = isPersistent;
			m_isForcedTarget = isForced;
			m_importanceLevel = isPersistent ? ImportanceLevelPersistent : ImportanceLevelNonPersistent;
			m_stuckStartTime = 0;
			m_lastStuckCheckPosition = m_componentCreature.ComponentBody.Position;

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
			m_stuckStartTime = 0;
		}

		// ===== UPDATE =====
		public virtual void Update(float dt)
		{
			if (Suppressed)
			{
				if (IsGreenNightExtremeProtectionActive())
				{
					Suppressed = false;
				}
				else
				{
					StopAttack();
					return;
				}
			}

			if (m_target != null && IsExtremePriorityTarget(m_target))
			{
				m_autoChaseSuppressionTime = 0f;
			}

			UpdateGreenNightProtection(dt);

			m_autoChaseSuppressionTime -= dt;

			if (IsZombie && !IsGreenNightActive && m_target != null && m_target.Entity.FindComponent<ComponentPlayer>() != null)
			{
				StopAttack();
				return;
			}

			if (ShouldForceAttackPlayer())
			{
				ComponentPlayer player = FindNearestPlayer(SpecialChaseRange);
				if (player != null)
				{
					StopAttack();
					Attack(player, SpecialChaseRange, 120f, true);
					m_chaseTime = 120f;
					m_isPersistent = true;
					return;
				}
			}

			if (IsExtremeProtectionActive())
			{
				if (m_isForcedTarget && m_target != null && m_target.ComponentHealth.Health > 0f)
				{
				}
				else
				{
					ComponentCreature bandit = FindNearestBandit();

					if (bandit != null)
					{
						if (m_target != bandit)
						{
							Attack(bandit, 40f, 120f, true, false);
						}
						else if (m_target != null && (!m_isPersistent || m_range < 40f || m_chaseTime < 60f))
						{
							Attack(m_target, 40f, 120f, true, false);
						}
					}
					else
					{
						if (m_target != null && IsBanditCreature(m_target))
						{
							StopAttack();
						}
					}
				}
			}

			if (IsGreenNightExtremeProtectionActive() && m_target != null && !m_isForcedTarget)
			{
				ComponentCreature closerEnemy = FindNearestGreenNightEnemy(GetProtectionRange());
				if (closerEnemy != null && closerEnemy != m_target)
				{
					float distToCurrentTarget = Vector3.Distance(m_componentCreature.ComponentBody.Position, m_target.ComponentBody.Position);
					float distToNewTarget = Vector3.Distance(m_componentCreature.ComponentBody.Position, closerEnemy.ComponentBody.Position);

					if (distToNewTarget < distToCurrentTarget || IsDirectlyAttackingPlayer(closerEnemy))
					{
						StopAttack();
						Attack(closerEnemy, GetProtectionRange(), GetProtectionChaseTime(), true, true);
					}
				}
			}

			if (IsActive && m_target != null)
			{
				m_chaseTime -= dt;

				if (IsGreenNightExtremeProtectionActive() && IsGreenNightEnemy(m_target))
				{
					m_chaseTime += dt * 0.5f;
					m_chaseTime = MathUtils.Min(m_chaseTime, GetProtectionChaseTime());
				}

				m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(m_target.ComponentCreatureModel.EyePosition);

				float distance = GetDistanceToTarget();
				bool inMeleeRange = IsTargetInAttackRange(m_target.ComponentBody);

				if (inMeleeRange)
				{
					if (IsTargetInFront())
					{
						m_componentCreatureModel.AttackOrder = true;
						if (m_componentCreatureModel.IsAttackHitMoment)
						{
							Vector3 hitPoint;
							ComponentBody hitBody = GetHitBody(m_target.ComponentBody, out hitPoint);
							if (hitBody != null)
							{
								float extraChaseTime = m_isPersistent ? m_random.Float(8f, 10f) : 2f;

								if (IsGreenNightExtremeProtectionActive()) extraChaseTime = 15f;

								m_chaseTime = MathUtils.Max(m_chaseTime, extraChaseTime);
								m_componentMiner.Hit(hitBody, hitPoint, m_componentCreature.ComponentBody.Matrix.Forward);
								m_componentCreature.ComponentCreatureSounds.PlayAttackSound();

								if (ExplodeOnHit && m_random.Float(0f, 1f) < 0.1f)
								{
									SubsystemExplosions subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true);
									if (subsystemExplosions != null)
									{
										subsystemExplosions.AddExplosion(
											Terrain.ToCell(hitPoint.X),
											Terrain.ToCell(hitPoint.Y),
											Terrain.ToCell(hitPoint.Z),
											255f,
											false,
											false
										);
									}
								}

								if (PushWhileAttacking && m_random.Float(0f, 1f) < 0.5f)
								{
									Vector3 direction = m_target.ComponentBody.Position - m_componentCreature.ComponentBody.Position;
									direction.Y = Math.Max(direction.Y, 0.5f);
									if (direction.LengthSquared() > 0.001f)
										direction = Vector3.Normalize(direction);
									else
										direction = Vector3.UnitY;

									float originalMaxSpeed = m_target.ComponentBody.MaxSpeed;
									m_target.ComponentBody.MaxSpeed = 1e9f;
									float pushForce = IsGreenNightExtremeProtectionActive() ? 75f : 55f;
									m_target.ComponentBody.ApplyImpulse(direction * pushForce);
									m_target.ComponentBody.MaxSpeed = originalMaxSpeed;
								}

								if (InvokeLightningOnHit && m_subsystemSky != null && m_random.Float(0f, 1f) < 0.05f)
								{
									m_subsystemSky.MakeLightningStrike(m_target.ComponentBody.Position, true);
								}
							}
						}
					}
				}

				if (PlaceBlocksWhenTargetHigh)
				{
					bool isStuck = m_componentPathfinding.IsStuck;
					bool targetIsHigh = (m_target.ComponentBody.Position.Y - m_componentCreature.ComponentBody.Position.Y) > 2.0f;
					bool canDestroy = DestroyBlocksWhenStuck;

					if (isStuck && targetIsHigh && canDestroy)
					{
						m_waitingForDestructionBeforeBuild = true;
					}
					else
					{
						m_waitingForDestructionBeforeBuild = false;
					}

					TryPlaceDirtBlocksToReachTarget();
				}
			}

			if (m_subsystemTime.GameTime >= m_nextUpdateTime)
			{
				m_dt = m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(m_subsystemTime.GameTime - m_nextUpdateTime), 0.1f);
				m_nextUpdateTime = m_subsystemTime.GameTime + (double)m_dt;
				m_stateMachine.Update();
			}
		}

		private bool IsTargetInFront()
		{
			if (m_target == null) return false;

			Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
			Vector3 toTarget = m_target.ComponentBody.Position - m_componentCreature.ComponentBody.Position;

			forward.Y = 0f;
			toTarget.Y = 0f;

			float dot = forward.X * toTarget.X + forward.Z * toTarget.Z;
			float lenForward = MathF.Sqrt(forward.X * forward.X + forward.Z * forward.Z);
			float lenToTarget = MathF.Sqrt(toTarget.X * toTarget.X + toTarget.Z * toTarget.Z);

			if (lenToTarget < 0.001f) return true;

			float cosAngle = dot / (lenForward * lenToTarget);
			float halfAngleRad = 45f * MathUtils.DegToRad(1f);
			float cosHalfAngle = MathF.Cos(halfAngleRad);

			return cosAngle >= cosHalfAngle;
		}

		private float GetDistanceToTarget()
		{
			if (m_target == null) return float.MaxValue;
			Vector3 selfPos = m_componentCreature.ComponentBody.Position;
			Vector3 targetPos = m_target.ComponentBody.Position;
			return Vector3.Distance(selfPos, targetPos);
		}

		private bool ShouldForceAttackPlayer()
		{
			return (IsZombie && IsGreenNightActive) || IsBanditType;
		}

		private bool IsTargetInAttackRange(ComponentBody target)
		{
			Vector3 selfCenter = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 targetCenter = target.BoundingBox.Center();
			float centerDistance = Vector3.Distance(selfCenter, targetCenter);
			if (centerDistance <= MaxAttackRange + 0.5f)
				return true;

			if (IsBodyInAttackRange(target)) return true;

			BoundingBox bbSelf = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox bbTarget = target.BoundingBox;
			selfCenter = 0.5f * (bbSelf.Min + bbSelf.Max);
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

			if (target.ParentBody != null && IsTargetInAttackRange(target.ParentBody))
				return true;

			if (AllowAttackingStandingOnBody && target.StandingOnBody != null &&
				target.StandingOnBody.Position.Y < target.Position.Y &&
				IsTargetInAttackRange(target.StandingOnBody))
				return true;

			ComponentBody myStandingOn = m_componentCreature.ComponentBody.StandingOnBody;
			if (AllowAttackingStandingOnBody && myStandingOn != null &&
				myStandingOn == target)
			{
				return true;
			}

			return false;
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
				 (target.StandingOnBody == result.Value.ComponentBody && AllowAttackingStandingOnBody) ||
				 (m_componentCreature.ComponentBody.StandingOnBody == target && AllowAttackingStandingOnBody)))
			{
				hitPoint = result.Value.HitPoint();
				return result.Value.ComponentBody;
			}

			hitPoint = Vector3.Zero;
			return null;
		}

		private bool IsBlockBreakable(int value)
		{
			int contents = Terrain.ExtractContents(value);
			if (contents == 0) return false;

			Block block = BlocksManager.Blocks[contents];

			if (block.GetExplosionResilience(value) >= float.MaxValue)
				return false;

			return block.IsCollidable_(value) && block.DigResilience >= 0f;
		}

		private void TryDestroyBlocksToFree()
		{
			if (!DestroyBlocksWhenStuck || m_target == null) return;

			Vector3 currentPos = m_componentCreature.ComponentBody.Position;

			if (m_stateMachine.CurrentState == "Chasing")
			{
				if (Vector3.Distance(currentPos, m_lastStuckCheckPosition) > 0.1f)
				{
					m_stuckDetectionStartTime = 0;
					m_lastStuckCheckPosition = currentPos;
					return;
				}

				if (m_stuckDetectionStartTime == 0)
				{
					m_stuckDetectionStartTime = m_subsystemTime.GameTime;
					m_lastStuckCheckPosition = currentPos;
					return;
				}

				if (m_subsystemTime.GameTime - m_stuckDetectionStartTime >= 2.0)
				{
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 targetEyePos = m_target.ComponentCreatureModel.EyePosition;
					Vector3 toTarget = targetEyePos - eyePos;
					float distance = toTarget.Length();
					if (distance < 0.1f) return;
					toTarget /= distance;

					float verticalAngle = (float)Math.Asin(Math.Clamp(toTarget.Y, -1f, 1f));
					const float thresholdDeg = 25f;
					float thresholdRad = MathUtils.DegToRad(thresholdDeg);

					bool isUp = verticalAngle > thresholdRad;
					bool isDown = verticalAngle < -thresholdRad;

					if (isUp)
					{
						float headHeight = currentPos.Y + m_componentCreature.ComponentBody.BoxSize.Y;
						Vector3 headPos = new Vector3(currentPos.X, headHeight, currentPos.Z);
						DestroyBlockAtPosition(headPos + Vector3.UnitY * 0.2f);
						DestroyBlockAtPosition(headPos + Vector3.UnitY * 1.2f);
						m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
					}
					else if (isDown)
					{
						float feetY = currentPos.Y + 0.2f;
						DestroyBlockAtPosition(new Vector3(currentPos.X, feetY, currentPos.Z) - Vector3.UnitY * 0.5f);
						DestroyBlockAtPosition(new Vector3(currentPos.X, feetY, currentPos.Z) - Vector3.UnitY * 1.6f);
					}
					else
					{
						Vector3 horizDir = new Vector3(toTarget.X, 0, toTarget.Z);
						if (horizDir.LengthSquared() > 0.001f)
						{
							horizDir = Vector3.Normalize(horizDir);

							float feetY = currentPos.Y + 0.2f;
							float headY = currentPos.Y + m_componentCreature.ComponentBody.StanceBoxSize.Y - 0.2f;

							Vector3 feetPos = new Vector3(currentPos.X, feetY, currentPos.Z) + horizDir * 0.6f;
							Vector3 headPos = new Vector3(currentPos.X, headY, currentPos.Z) + horizDir * 0.6f;

							DestroyBlockAtPosition(feetPos);
							DestroyBlockAtPosition(headPos);
						}
					}

					m_stuckDetectionStartTime = m_subsystemTime.GameTime;
				}
			}
			else
			{
				m_stuckDetectionStartTime = 0;
			}
		}

		private void DestroyBlockAtPosition(Vector3 worldPos)
		{
			int x = Terrain.ToCell(worldPos.X);
			int y = Terrain.ToCell(worldPos.Y);
			int z = Terrain.ToCell(worldPos.Z);

			if (!m_subsystemTerrain.Terrain.IsCellValid(x, y, z))
				return;

			int value = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
			if (value != 0 && IsBlockBreakable(value))
			{
				m_subsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
				var soundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);
				if (soundMaterials != null)
					soundMaterials.PlayImpactSound(value, new Vector3(x, y, z), 1f);
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>();
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_subsystemGreenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>(true);
			m_subsystemBanditInvasion = Project.FindSubsystem<SubsystemBanditInvasion>(true);

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
			DestroyBlocksWhenStuck = valuesDictionary.GetValue<bool>("DestroyBlocksWhenStuck", false);
			PlaceBlocksWhenTargetHigh = valuesDictionary.GetValue<bool>("PlaceBlocksWhenTargetHigh", false);
			InvokeLightningOnHit = valuesDictionary.GetValue<bool>("InvokeLightningOnHit", false);
			PushWhileAttacking = valuesDictionary.GetValue<bool>("PushWhileAttacking", false);
			ExplodeOnHit = valuesDictionary.GetValue<bool>("ExplodeOnHit", false);

			RegisterEvents();

			if (ShouldProtectPlayer)
			{
				SubscribeToPlayersForProtection();
			}

			SetupStateMachine();
			m_stateMachine.TransitionTo("LookingForTarget");
		}

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

		private void OnPlayerInjured(Injury injury)
		{
			if (!ShouldProtectPlayer) return;

			if (m_componentHireable != null && !m_componentHireable.IsHired) return;

			ComponentDefensiveRunAwayBehavior defensiveRunAway = Entity.FindComponent<ComponentDefensiveRunAwayBehavior>();
			if (defensiveRunAway != null && defensiveRunAway.IsActive) return;

			ComponentCreature attacker = injury.Attacker;
			if (attacker != null && CanAttackCreature(attacker))
			{
				Attack(attacker, 20f, 30f, false, true);

				if (m_componentHerd != null)
				{
					m_componentHerd.CallNearbyCreaturesHelp(attacker, 20f, 30f, false, true);
				}
			}
		}

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

		private bool CanAttackCreature(ComponentCreature creature)
		{
			if (creature == null) return false;
			if (m_componentHireable != null && !m_componentHireable.IsHired) return false;

			// NUEVO: No atacar a Infinite durante su duelo activo
			var infiniteChallenge = creature.Entity.FindComponent<ComponentInfiniteChallenge>();
			if (infiniteChallenge != null && infiniteChallenge.IsDuelActive)
				return false;

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
					float range = ChaseRangeOnAttacked ?? 7f;
					float time = ChaseTimeOnAttacked ?? 7f;
					bool persistent = ChasePersistentOnAttacked ?? false;

					Attack(attacker, range, time, persistent, true);

					if (m_componentHerd != null)
					{
						m_componentHerd.CallNearbyCreaturesHelp(attacker, 20f, 30f, false, true);
					}
				}
			};
		}

		private void TryPlaceDirtBlocksToReachTarget()
		{
			if (!PlaceBlocksWhenTargetHigh || m_target == null || m_componentCreature == null || m_subsystemTerrain == null)
				return;

			if (m_subsystemTime.GameTime - m_lastBlockPlaceTime < BlockPlaceCooldown)
				return;

			Vector3 myPos = m_componentCreature.ComponentBody.Position;
			Vector3 targetPos = m_target.ComponentBody.Position;

			float verticalDiff = targetPos.Y - myPos.Y;
			if (verticalDiff < 2.0f)
				return;

			for (int i = m_placedDirtBlocks.Count - 1; i >= 0; i--)
			{
				Point3 p = m_placedDirtBlocks[i];
				int contents = m_subsystemTerrain.Terrain.GetCellContents(p.X, p.Y, p.Z);
				if (contents != DirtBlock.Index)
					m_placedDirtBlocks.RemoveAt(i);
			}

			if (m_placedDirtBlocks.Count >= 2)
				return;

			int feetX = Terrain.ToCell(myPos.X);
			int feetY = Terrain.ToCell(myPos.Y - 0.1f);
			int feetZ = Terrain.ToCell(myPos.Z);

			int belowY = feetY - 1;
			if (!m_subsystemTerrain.Terrain.IsCellValid(feetX, belowY, feetZ))
				return;
			int belowContents = m_subsystemTerrain.Terrain.GetCellContents(feetX, belowY, feetZ);
			Block belowBlock = BlocksManager.Blocks[belowContents];
			if (!belowBlock.IsCollidable_(m_subsystemTerrain.Terrain.GetCellValue(feetX, belowY, feetZ)))
				return;

			int targetY1 = feetY;
			int targetY2 = feetY + 1;

			int headY1 = Terrain.ToCell(myPos.Y + m_componentCreature.ComponentBody.BoxSize.Y - 0.1f);
			int headY2 = headY1 + 1;
			if (!m_subsystemTerrain.Terrain.IsCellValid(feetX, headY1, feetZ) ||
				!m_subsystemTerrain.Terrain.IsCellValid(feetX, headY2, feetZ))
				return;
			int headContents1 = m_subsystemTerrain.Terrain.GetCellContents(feetX, headY1, feetZ);
			int headContents2 = m_subsystemTerrain.Terrain.GetCellContents(feetX, headY2, feetZ);
			Block headBlock1 = BlocksManager.Blocks[headContents1];
			Block headBlock2 = BlocksManager.Blocks[headContents2];
			if (headBlock1.IsCollidable_(m_subsystemTerrain.Terrain.GetCellValue(feetX, headY1, feetZ)) ||
				headBlock2.IsCollidable_(m_subsystemTerrain.Terrain.GetCellValue(feetX, headY2, feetZ)))
			{
				return;
			}

			if (!m_subsystemTerrain.Terrain.IsCellValid(feetX, targetY1, feetZ) ||
				!m_subsystemTerrain.Terrain.IsCellValid(feetX, targetY2, feetZ))
				return;
			int contents1 = m_subsystemTerrain.Terrain.GetCellContents(feetX, targetY1, feetZ);
			int contents2 = m_subsystemTerrain.Terrain.GetCellContents(feetX, targetY2, feetZ);
			if (contents1 != 0 || contents2 != 0)
				return;

			int dirtValue = Terrain.MakeBlockValue(DirtBlock.Index);
			m_subsystemTerrain.ChangeCell(feetX, targetY1, feetZ, dirtValue, true);
			m_subsystemTerrain.ChangeCell(feetX, targetY2, feetZ, dirtValue, true);

			TerrainChunk chunk = m_subsystemTerrain.Terrain.GetChunkAtCell(feetX, feetZ);
			if (chunk != null)
			{
				chunk.State = TerrainChunkState.InvalidLight;
				m_subsystemTerrain.TerrainUpdater.DowngradeChunkNeighborhoodState(
					chunk.Coords, 1, TerrainChunkState.InvalidLight, true);
			}

			SubsystemSoundMaterials soundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);
			if (soundMaterials != null)
			{
				soundMaterials.PlayImpactSound(dirtValue, new Vector3(feetX + 0.5f, targetY1 + 0.5f, feetZ + 0.5f), 1f);
				soundMaterials.PlayImpactSound(dirtValue, new Vector3(feetX + 0.5f, targetY2 + 0.5f, feetZ + 0.5f), 1f);
			}

			m_placedDirtBlocks.Add(new Point3(feetX, targetY1, feetZ));
			m_placedDirtBlocks.Add(new Point3(feetX, targetY2, feetZ));
			m_lastBlockPlaceTime = m_subsystemTime.GameTime;
		}

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

				m_placedDirtBlocks.Clear();
			}, () =>
			{
				if (!IsActive)
				{
					if (IsGreenNightExtremeProtectionActive())
					{
						ComponentCreature enemy = FindNearestGreenNightEnemy(GetProtectionRange());
						if (enemy != null)
						{
							m_target = enemy;
							m_range = GetProtectionRange();
							m_chaseTime = GetProtectionChaseTime();
							m_isPersistent = true;
							m_isForcedTarget = true;
							return;
						}
					}
					m_stateMachine.TransitionTo("LookingForTarget");
				}
				else if (m_chaseTime <= 0f)
				{
					if (IsGreenNightExtremeProtectionActive() && IsGreenNightEnemy(m_target))
					{
						m_chaseTime = GetProtectionChaseTime();
						m_isPersistent = true;
						m_range = GetProtectionRange();
						return;
					}
					m_isForcedTarget = false;
					m_autoChaseSuppressionTime = m_random.Float(10f, 60f);
					m_importanceLevel = 0f;
				}
				else if (m_target == null)
				{
					if (IsGreenNightExtremeProtectionActive())
					{
						ComponentCreature enemy = FindNearestGreenNightEnemy(GetProtectionRange());
						if (enemy != null)
						{
							m_target = enemy;
							m_range = GetProtectionRange();
							m_chaseTime = GetProtectionChaseTime();
							m_isPersistent = true;
							m_isForcedTarget = true;
							return;
						}
					}
					m_isForcedTarget = false;
					m_importanceLevel = 0f;
				}
				else if (m_target.ComponentHealth.Health <= 0f)
				{
					if (IsGreenNightExtremeProtectionActive())
					{
						ComponentCreature nextEnemy = FindNearestGreenNightEnemy(GetProtectionRange());
						if (nextEnemy != null)
						{
							m_target = nextEnemy;
							m_range = GetProtectionRange();
							m_chaseTime = GetProtectionChaseTime();
							m_isPersistent = true;
							m_isForcedTarget = true;
							return;
						}
					}
					m_isForcedTarget = false;
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
					if (IsGreenNightExtremeProtectionActive())
					{
						m_isPersistent = true;
						m_stateMachine.TransitionTo("RandomMoving");
						return;
					}
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
						if (IsGreenNightExtremeProtectionActive() && IsGreenNightEnemy(m_target))
						{
							m_targetUnsuitableTime += m_dt * 0.3f;
						}
						else
						{
							m_targetUnsuitableTime += m_dt;
						}
					}
					else
					{
						m_targetUnsuitableTime = 0f;
					}

					if (m_targetUnsuitableTime > 3f)
					{
						if (IsGreenNightExtremeProtectionActive())
						{
							ComponentCreature otherEnemy = FindNearestGreenNightEnemy(GetProtectionRange());
							if (otherEnemy != null && otherEnemy != m_target)
							{
								m_target = otherEnemy;
								m_range = GetProtectionRange();
								m_chaseTime = GetProtectionChaseTime();
								m_isPersistent = true;
								m_isForcedTarget = true;
								m_targetUnsuitableTime = 0f;
								return;
							}
						}
						m_importanceLevel = 0f;
					}
					else
					{
						int maxPathfindingPositions = 0;
						if (m_isPersistent)
						{
							maxPathfindingPositions = ((m_subsystemTime.FixedTimeStep != null) ? 2000 : 500);
							if (IsGreenNightExtremeProtectionActive()) maxPathfindingPositions = 3000;
						}
						BoundingBox bbSelf = m_componentCreature.ComponentBody.BoundingBox;
						BoundingBox bbTarget = m_target.ComponentBody.BoundingBox;
						Vector3 selfCenter = 0.5f * (bbSelf.Min + bbSelf.Max);
						Vector3 targetCenter = 0.5f * (bbTarget.Min + bbTarget.Max);
						float dist = Vector3.Distance(selfCenter, targetCenter);
						float followFactor = (dist < 4f) ? 0.2f : 0f;
						m_componentPathfinding.SetDestination(targetCenter + followFactor * dist * m_target.ComponentBody.Velocity,
																	1f, 1.5f, maxPathfindingPositions, true, false, true, m_target.ComponentBody);

						float immersion = m_componentCreature.ComponentBody.ImmersionFactor;
						if (immersion > 0.15f)
						{
							Vector3 toTarget = m_target.ComponentBody.Position - m_componentCreature.ComponentBody.Position;
							float verticalDiff = toTarget.Y;
							float horizontalDist = new Vector2(toTarget.X, toTarget.Z).Length();

							float climbSpeed = 2.8f;
							if (IsGreenNightExtremeProtectionActive()) climbSpeed = 4.0f;

							if (verticalDiff > 0.2f)
							{
								float upForce = MathUtils.Saturate((verticalDiff - 0.2f) / 1.5f) * climbSpeed * m_dt;
								upForce *= 1f + immersion * 2f;
								m_componentCreature.ComponentBody.ApplyImpulse(new Vector3(0f, upForce, 0f));
							}

							if (immersion > 0.6f && verticalDiff > -0.3f)
							{
								float surfaceForce = MathUtils.Saturate((immersion - 0.6f) / 0.4f) * climbSpeed * 0.7f * m_dt;
								surfaceForce *= 1f + immersion * 2f;
								m_componentCreature.ComponentBody.ApplyImpulse(new Vector3(0f, surfaceForce, 0f));
							}

							if (horizontalDist > 0.2f)
							{
								Vector2 horizDir = new Vector2(toTarget.X, toTarget.Z) / horizontalDist;
								float horizForce = immersion * 12f * m_dt;
								if (IsGreenNightExtremeProtectionActive()) horizForce *= 1.5f;
								m_componentCreature.ComponentBody.ApplyImpulse(new Vector3(horizDir.X * horizForce, 0f, horizDir.Y * horizForce));
							}
						}

						if (PlayAngrySoundWhenChasing && m_random.Float(0f, 1f) < 0.33f * m_dt)
						{
							m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						}
					}
				}

				TryDestroyBlocksToFree();
			}, null);
		}

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
			return (IsZombie && IsGreenNightActive && isPlayer) || (IsBanditType && isPlayer);
		}

		private ComponentCreature FindTarget()
		{
			if (m_componentHireable != null && !m_componentHireable.IsHired)
				return null;

			if ((IsZombie && IsGreenNightActive) || IsBanditType)
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
			// NUEVO: No considerar a Infinite durante el duelo
			var infiniteChallenge = creature.Entity.FindComponent<ComponentInfiniteChallenge>();
			if (infiniteChallenge != null && infiniteChallenge.IsDuelActive)

				return 0f;
			if (m_componentHireable != null && !m_componentHireable.IsHired)
				return 0f;

			if (!CanAttackCreature(creature))
				return 0f;

			// Protección extrema: bandidos obtienen prioridad muy alta (comparable a noche verde)
			if (IsExtremeProtectionActive() && IsBanditCreature(creature))
			{
				return float.MaxValue / 2;
			}

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

			// Prioridad máxima para jugadores durante noche verde (zombies) o banda (bandidos)
			if (isPlayer && ((IsZombie && IsGreenNightActive) || IsBanditType))
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

		public void ForceProtectiveAttack()
		{
			if (!ShouldProtectPlayer) return;

			if (m_componentHireable != null && !m_componentHireable.IsHired) return;

			if (IsGreenNightExtremeProtectionActive())
			{
				Suppressed = false;
				TargetInRangeTimeToChase = 0f;
				m_autoChaseSuppressionTime = 0f;
			}
			else
			{
				if (Suppressed) return;
			}

			float protectionRange = GetProtectionRange();
			float protectionChaseTime = GetProtectionChaseTime();

			if (m_target != null && m_target.ComponentHealth.Health > 0f && IsGreenNightEnemy(m_target))
			{
				m_chaseTime = protectionChaseTime;
				m_isPersistent = true;
				m_range = protectionRange;
				return;
			}

			ComponentCreature enemy;
			if (IsGreenNightExtremeProtectionActive())
			{
				enemy = FindNearestGreenNightEnemy(protectionRange);
			}
			else
			{
				enemy = FindNearestEnemy(protectionRange);
			}

			if (enemy != null)
			{
				StopAttack();
				Attack(enemy, protectionRange, protectionChaseTime, true, true);

				if (m_componentHerd != null)
				{
					m_componentHerd.CallNearbyCreaturesHelp(enemy, protectionRange * 0.8f, protectionChaseTime, true, true);
				}
			}
		}

		private bool IsEnemy(ComponentCreature creature)
		{
			if (creature == null || creature.ComponentHealth.Health <= 0f)
				return false;

			ComponentZombieChaseBehavior zChase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
			if (zChase != null && zChase.ForceAttackDuringGreenNight) return true;

			ComponentBanditChaseBehavior bChase = creature.Entity.FindComponent<ComponentBanditChaseBehavior>();
			if (bChase != null) return true;

			if (IsDirectlyAttackingPlayer(creature)) return true;

			if (IsGreenNightActive)
			{
				if (zChase != null && zChase.Target != null && zChase.Target.Entity.FindComponent<ComponentPlayer>() != null) return true;
			}

			return false;
		}

		private ComponentCreature FindNearestEnemy(float range)
		{
			if (m_componentCreature?.ComponentBody == null) return null;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			ComponentCreature nearest = null;
			float minDist = float.MaxValue;

			bool isGreenNight = IsGreenNightExtremeProtectionActive();
			float effectiveRange = isGreenNight ? Math.Max(range, 60f) : range;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature == m_componentCreature || creature.ComponentHealth.Health <= 0f) continue;
				if (!IsEnemy(creature)) continue;

				float dist = Vector3.Distance(position, creature.ComponentBody.Position);
				if (dist <= effectiveRange)
				{
					float effectiveDist = dist;
					if (isGreenNight && IsDirectlyAttackingPlayer(creature)) effectiveDist *= 0.5f;

					if (effectiveDist < minDist)
					{
						minDist = effectiveDist;
						nearest = creature;
					}
				}
			}

			return nearest;
		}

		private bool IsGreenNightExtremeProtectionActive()
		{
			if (!ShouldProtectPlayer) return false;
			if (m_subsystemGreenNightSky == null) return false;
			return m_subsystemGreenNightSky.IsGreenNightActive;
		}

		private float GetProtectionRange()
		{
			if (IsGreenNightExtremeProtectionActive()) return 60f;
			if (IsExtremeProtectionActive()) return 40f;
			return 20f;
		}

		private float GetProtectionChaseTime()
		{
			if (IsGreenNightExtremeProtectionActive()) return 180f;
			if (IsExtremeProtectionActive()) return 120f;
			return 30f;
		}

		private bool IsGreenNightEnemy(ComponentCreature creature)
		{
			if (creature == null || creature.ComponentHealth.Health <= 0f) return false;

			ComponentZombieChaseBehavior zChase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
			if (zChase != null && zChase.ForceAttackDuringGreenNight && IsGreenNightActive) return true;

			if (IsGreenNightActive)
			{
				if (zChase != null && zChase.Target != null && zChase.Target.Entity.FindComponent<ComponentPlayer>() != null) return true;
				ComponentNewChaseBehavior nChase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
				if (nChase != null && nChase.Target != null && nChase.Target.Entity.FindComponent<ComponentPlayer>() != null) return true;
			}

			return false;
		}

		private ComponentCreature FindNearestGreenNightEnemy(float range)
		{
			if (m_subsystemCreatureSpawn == null || m_componentCreature?.ComponentBody == null) return null;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			ComponentCreature nearest = null;
			float minDistSq = range * range;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature == m_componentCreature || creature.ComponentHealth.Health <= 0f) continue;
				if (!IsGreenNightEnemy(creature)) continue;

				bool isAttackingPlayer = IsDirectlyAttackingPlayer(creature);
				float distSq = Vector3.DistanceSquared(position, creature.ComponentBody.Position);
				float effectiveDistSq = isAttackingPlayer ? distSq * 0.5f : distSq;

				if (effectiveDistSq < minDistSq)
				{
					minDistSq = effectiveDistSq;
					nearest = creature;
				}
			}
			return nearest;
		}

		private bool IsDirectlyAttackingPlayer(ComponentCreature creature)
		{
			if (creature == null) return false;

			ComponentZombieChaseBehavior zChase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
			if (zChase != null && zChase.Target != null && zChase.Target.Entity.FindComponent<ComponentPlayer>() != null) return true;

			ComponentNewChaseBehavior nChase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (nChase != null && nChase.Target != null && nChase.Target.Entity.FindComponent<ComponentPlayer>() != null) return true;

			return false;
		}

		private ComponentCreature FindCreatureAttackingPlayer(ComponentPlayer player, float range)
		{
			if (m_subsystemCreatureSpawn == null || player == null) return null;

			Vector3 playerPos = player.ComponentBody.Position;
			ComponentCreature nearest = null;
			float minDistSq = range * range;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature == m_componentCreature || creature.ComponentHealth.Health <= 0f) continue;

				bool isAttackingThisPlayer = false;

				ComponentZombieChaseBehavior zChase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
				if (zChase != null && zChase.Target == player) isAttackingThisPlayer = true;

				ComponentNewChaseBehavior nChase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
				if (nChase != null && nChase.Target == player) isAttackingThisPlayer = true;

				ComponentBanditChaseBehavior bChase = creature.Entity.FindComponent<ComponentBanditChaseBehavior>();
				if (bChase != null) isAttackingThisPlayer = true;

				if (isAttackingThisPlayer)
				{
					float distSq = Vector3.DistanceSquared(playerPos, creature.ComponentBody.Position);
					if (distSq < minDistSq)
					{
						minDistSq = distSq;
						nearest = creature;
					}
				}
			}
			return nearest;
		}

		private void UpdateGreenNightProtection(float dt)
		{
			if (!ShouldProtectPlayer) return;
			if (m_componentHireable != null && !m_componentHireable.IsHired) return;

			bool isGreenNightActive = IsGreenNightExtremeProtectionActive();

			if (isGreenNightActive && !m_wasGreenNightActiveForProtection)
			{
				Suppressed = false;
				TargetInRangeTimeToChase = 0f;
				m_autoChaseSuppressionTime = 0f;

				ComponentCreature enemy = FindNearestGreenNightEnemy(GetProtectionRange());
				if (enemy != null)
				{
					StopAttack();
					Attack(enemy, GetProtectionRange(), GetProtectionChaseTime(), true, true);
					if (m_componentHerd != null)
					{
						m_componentHerd.CallNearbyCreaturesHelp(enemy, GetProtectionRange() * 0.8f, GetProtectionChaseTime(), true, true);
					}
				}
			}
			else if (!isGreenNightActive && m_wasGreenNightActiveForProtection)
			{
				if (m_target != null && !IsEnemy(m_target))
				{
					StopAttack();
				}
			}

			m_wasGreenNightActiveForProtection = isGreenNightActive;

			if (!isGreenNightActive) return;

			m_greenNightProtectionTimer -= dt;
			if (m_greenNightProtectionTimer > 0f) return;
			m_greenNightProtectionTimer = 0.3f;

			if (m_target != null)
			{
				if (!IsGreenNightEnemy(m_target) && !m_isForcedTarget)
				{
					ComponentCreature betterTarget = FindNearestGreenNightEnemy(GetProtectionRange());
					if (betterTarget != null)
					{
						StopAttack();
						Attack(betterTarget, GetProtectionRange(), GetProtectionChaseTime(), true, true);
						return;
					}
				}

				if (IsGreenNightEnemy(m_target) && m_chaseTime < 30f)
				{
					m_chaseTime = GetProtectionChaseTime();
					m_isPersistent = true;
					m_range = GetProtectionRange();
				}
			}
			else
			{
				ComponentCreature enemy = FindNearestGreenNightEnemy(GetProtectionRange());
				if (enemy != null)
				{
					StopAttack();
					Attack(enemy, GetProtectionRange(), GetProtectionChaseTime(), true, true);

					if (m_componentHerd != null)
					{
						m_componentHerd.CallNearbyCreaturesHelp(enemy, GetProtectionRange() * 0.8f, GetProtectionChaseTime(), true, true);
					}
				}
				else if (m_subsystemPlayers != null)
				{
					foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
					{
						if (player == null || player.ComponentHealth.Health <= 0f) continue;

						float distToPlayer = Vector3.Distance(m_componentCreature.ComponentBody.Position, player.ComponentBody.Position);
						if (distToPlayer > GetProtectionRange()) continue;

						ComponentCreature playerAttacker = FindCreatureAttackingPlayer(player, GetProtectionRange());
						if (playerAttacker != null)
						{
							StopAttack();
							Attack(playerAttacker, GetProtectionRange(), GetProtectionChaseTime(), true, true);
							break;
						}
					}
				}
			}
		}
	}
}
