using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using System.Collections.Generic;

namespace Game
{
	public enum AttackMode
	{
		Default,
		Remote,
		OnlyHand
	}
	public class ComponentNewChaseBehavior : ComponentBehavior, IUpdateable
	{
		// ===== PROPIEDADES PÚBLICAS =====
		public ComponentCreature Target => m_target;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		// ===== PARÁMETROS CONFIGURABLES (valores por defecto) =====
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

		// Parámetros de ataque a distancia
		public Vector2 RangedAttackRange = new Vector2(5f, 20f);
		public AttackMode RangedAttackMode = AttackMode.Default;

		// Nuevos parámetros para objetos lanzables
		public float ThrowableAimTime = 1.55f;
		public float ThrowableCooldown = 0.5f;
		public Vector2 ThrowableAttackRange = new Vector2(5f, 15f);

		// Tiempos de apuntado para armas a distancia
		public float MusketAimTime = 1.5f;
		public float MusketCooldown = 0.35f;
		public float BowAimTime = 1.5f;
		public float BowCooldown = 0.1f;
		public float CrossbowAimTime = 1.5f;
		public float CrossbowCooldown = 0.1f;
		public float RepeatCrossbowAimTime = 1.5f;
		public float RepeatCrossbowCooldown = 0.1f;
		public float FlameThrowerAimTime = 1.5f;
		public float FlameThrowerCooldown = 0.1f;
		public float DoubleMusketAimTime = 1.5f;
		public float DoubleMusketCooldown = 0.35f;

		public bool DestroyBlocksWhenStuck = false;
		public bool InvokeLightningOnHit = false;
		public bool PushWhileAttacking = false;
		public bool ExplodeOnHit = false;
		public bool PlaceBlocksWhenTargetHigh = false;

		private Vector3 m_lastStuckCheckPosition;
		private double m_stuckDetectionStartTime;
		private double m_lastLateralMoveTime;

		private List<Point3> m_placedDirtBlocks = new List<Point3>();
		private double m_lastBlockPlaceTime;
		private const float BlockPlaceCooldown = 0.5f;

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
		private SubsystemProjectiles m_subsystemProjectiles;

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

		// Campos para ataque a distancia
		private double m_nextRangedAttackTime;
		private double m_rangedAimStartTime;
		private bool m_isAimingRanged;
		private bool m_triedToLoad;
		private bool m_aimingStarted;

		// Campos para objetos lanzables
		private bool m_isAimingThrowable;
		private double m_nextThrowableAttackTime;
		private double m_throwableAimStartTime;
		private int m_throwableSlotIndex = -1;
		private int m_throwableValue;

		// Suscripción a eventos de salud de jugadores
		private List<ComponentHealth> m_subscribedPlayerHealths = new List<ComponentHealth>();

		// Manejador para eliminar pickables de flechas/virotes
		private Action<Projectile> m_projectileAddedHandler;

		// Conjunto estático de bloques lanzables (índices)
		private static HashSet<int> s_throwableBlockIndices;

		private double m_stuckStartTime;

		// ===== PROPIEDADES AUXILIARES =====
		private bool IsZombie => HerdName != null && HerdName.ToLower().Contains("zombie");
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

		private bool IsCurrentWeaponMelee
		{
			get
			{
				int activeValue = m_componentMiner?.ActiveBlockValue ?? 0;
				if (activeValue == 0) return true;

				int contents = Terrain.ExtractContents(activeValue);
				if (contents == MusketBlock.Index ||
					contents == BowBlock.Index ||
					contents == CrossbowBlock.Index ||
					contents == RepeatCrossbowBlock.Index ||
					contents == FlameThrowerBlock.Index ||
					contents == DoubleMusketBlock.Index)
				{
					return false;
				}
				if (IsThrowableBlock(activeValue))
					return false;

				return true;
			}
		}

		private bool IsCurrentWeaponRanged
		{
			get
			{
				int activeValue = m_componentMiner?.ActiveBlockValue ?? 0;
				if (activeValue == 0) return false;

				int contents = Terrain.ExtractContents(activeValue);
				return contents == MusketBlock.Index ||
					   contents == BowBlock.Index ||
					   contents == CrossbowBlock.Index ||
					   contents == RepeatCrossbowBlock.Index ||
					   contents == FlameThrowerBlock.Index ||
					   contents == DoubleMusketBlock.Index ||
					   IsThrowableBlock(activeValue);
			}
		}

		// ===== CONSTRUCTOR ESTÁTICO PARA INICIALIZAR BLOQUES LANZABLES =====
		static ComponentNewChaseBehavior()
		{
			s_throwableBlockIndices = new HashSet<int>();
			RegisterThrowableBlock(typeof(StoneChunkBlock));
			RegisterThrowableBlock(typeof(SulphurChunkBlock));
			RegisterThrowableBlock(typeof(CoalChunkBlock));
			RegisterThrowableBlock(typeof(DiamondChunkBlock));
			RegisterThrowableBlock(typeof(GermaniumChunkBlock));
			RegisterThrowableBlock(typeof(GermaniumOreChunkBlock));
			RegisterThrowableBlock(typeof(IronOreChunkBlock));
			RegisterThrowableBlock(typeof(MalachiteChunkBlock));
			RegisterThrowableBlock(typeof(SaltpeterChunkBlock));
			RegisterThrowableBlock(typeof(GunpowderBlock));
			RegisterThrowableBlock(typeof(BombBlock));
			RegisterThrowableBlock(typeof(IncendiaryBombBlock));
			RegisterThrowableBlock(typeof(PoisonBombBlock));
			RegisterThrowableBlock(typeof(BrickBlock));
			RegisterThrowableBlock(typeof(SnowballBlock));
			RegisterThrowableBlock(typeof(EggBlock));
			RegisterThrowableBlock(typeof(CopperSpearBlock));
			RegisterThrowableBlock(typeof(DiamondSpearBlock));
			RegisterThrowableBlock(typeof(IronSpearBlock));
			RegisterThrowableBlock(typeof(WoodenSpearBlock));
			RegisterThrowableBlock(typeof(WoodenLongspearBlock));
			RegisterThrowableBlock(typeof(StoneSpearBlock));
			RegisterThrowableBlock(typeof(StoneLongspearBlock));
			RegisterThrowableBlock(typeof(IronLongspearBlock));
			RegisterThrowableBlock(typeof(LavaLongspearBlock));
			RegisterThrowableBlock(typeof(LavaSpearBlock));
			RegisterThrowableBlock(typeof(DiamondLongspearBlock));
			RegisterThrowableBlock(typeof(FreezingSnowballBlock));
			RegisterThrowableBlock(typeof(FreezeBombBlock));
			RegisterThrowableBlock(typeof(FireworksBlock));
		}

		private static void RegisterThrowableBlock(Type blockType)
		{
			var field = blockType.GetField("Index");
			if (field != null && field.FieldType == typeof(int))
			{
				int index = (int)field.GetValue(null);
				if (index != 0)
				{
					s_throwableBlockIndices.Add(index);
				}
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
			m_isAimingRanged = false;
			m_isAimingThrowable = false;
			m_nextRangedAttackTime = 0;
			m_nextThrowableAttackTime = 0;
			m_triedToLoad = false;
			m_aimingStarted = false;
			m_stuckStartTime = 0;
			m_lastStuckCheckPosition = m_componentCreature.ComponentBody.Position;

			SubscribeToProjectileEvents();

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
			m_isAimingRanged = false;
			m_isAimingThrowable = false;
			m_nextRangedAttackTime = 0;
			m_nextThrowableAttackTime = 0;
			m_triedToLoad = false;
			m_aimingStarted = false;
			m_stuckStartTime = 0;

			UnsubscribeFromProjectileEvents();
		}

		private void SubscribeToProjectileEvents()
		{
			if (m_subsystemProjectiles == null)
				return;

			if (m_projectileAddedHandler == null)
			{
				m_projectileAddedHandler = OnProjectileAdded;
				m_subsystemProjectiles.ProjectileAdded += m_projectileAddedHandler;
			}
		}

		private void UnsubscribeFromProjectileEvents()
		{
			if (m_subsystemProjectiles != null && m_projectileAddedHandler != null)
			{
				m_subsystemProjectiles.ProjectileAdded -= m_projectileAddedHandler;
				m_projectileAddedHandler = null;
			}
		}

		private void OnProjectileAdded(Projectile projectile)
		{
			if (projectile.Owner != m_componentCreature)
				return;

			int contents = Terrain.ExtractContents(projectile.Value);
			if (contents == ArrowBlock.Index)
			{
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			}
		}

		// ===== MÉTODO UNIFICADO PARA DETECCIÓN DE ARMAS =====
		private bool HasWeapon(int weaponType, out int slotIndex, out int value)
		{
			slotIndex = -1;
			value = 0;
			if (m_componentMiner?.Inventory == null) return false;

			int targetIndex = -1;
			switch (weaponType)
			{
				case 0: targetIndex = MusketBlock.Index; break;
				case 1: targetIndex = BowBlock.Index; break;
				case 2: targetIndex = CrossbowBlock.Index; break;
				case 3: targetIndex = RepeatCrossbowBlock.Index; break;
				case 4: targetIndex = FlameThrowerBlock.Index; break;
				case 5: targetIndex = DoubleMusketBlock.Index; break;
				default: return false;
			}

			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				int slotCount = m_componentMiner.Inventory.GetSlotCount(i);
				if (slotCount > 0 && Terrain.ExtractContents(slotValue) == targetIndex)
				{
					slotIndex = i;
					value = slotValue;
					return true;
				}
			}
			return false;
		}

		private int FindBestMeleeWeapon(out int slotIndex, out int value)
		{
			slotIndex = -1;
			value = 0;
			if (m_componentMiner?.Inventory == null) return -1;

			float bestPower = 0f;
			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				int slotCount = m_componentMiner.Inventory.GetSlotCount(i);
				if (slotCount > 0)
				{
					int contents = Terrain.ExtractContents(slotValue);
					if (contents == MusketBlock.Index ||
						contents == BowBlock.Index ||
						contents == CrossbowBlock.Index ||
						contents == RepeatCrossbowBlock.Index ||
						contents == FlameThrowerBlock.Index ||
						IsThrowableBlock(slotValue))
					{
						continue;
					}
					Block block = BlocksManager.Blocks[contents];
					float power = block.GetMeleePower(slotValue);
					if (power > bestPower)
					{
						bestPower = power;
						slotIndex = i;
						value = slotValue;
					}
				}
			}
			if (slotIndex == -1)
			{
				value = 0;
				return -1;
			}
			return slotIndex;
		}

		private int FindBestRangedWeapon(out int slotIndex, out int value)
		{
			slotIndex = -1;
			value = 0;
			if (m_componentMiner?.Inventory == null) return -1;

			float bestPower = 0f;
			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				int slotCount = m_componentMiner.Inventory.GetSlotCount(i);
				if (slotCount > 0)
				{
					int contents = Terrain.ExtractContents(slotValue);
					bool isRanged = contents == MusketBlock.Index ||
									contents == BowBlock.Index ||
									contents == CrossbowBlock.Index ||
									contents == RepeatCrossbowBlock.Index ||
									contents == FlameThrowerBlock.Index ||
									contents == DoubleMusketBlock.Index;
					if (!isRanged) continue;

					float power = 0f;
					if (contents == MusketBlock.Index) power = 100f;
					else if (contents == BowBlock.Index) power = 80f;
					else if (contents == CrossbowBlock.Index) power = 90f;
					else if (contents == RepeatCrossbowBlock.Index) power = 95f;
					else if (contents == FlameThrowerBlock.Index) power = 70f;
					else if (contents == DoubleMusketBlock.Index) power = 110f;  // más potente que el mosquete simple
					if (power > bestPower)
					{
						bestPower = power;
						slotIndex = i;
						value = slotValue;
					}
				}
			}
			return slotIndex;
		}

		private void EquipWeapon(int slotIndex)
		{
			if (m_componentMiner?.Inventory == null) return;
			if (slotIndex >= 0 && slotIndex < m_componentMiner.Inventory.SlotsCount)
			{
				m_componentMiner.Inventory.ActiveSlotIndex = slotIndex;
			}
		}

		private bool IsTargetInViewCone()
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
			float halfAngleRad = 45f * 3.14159274f / 180f;
			float cosHalfAngle = MathF.Cos(halfAngleRad);

			return cosAngle >= cosHalfAngle;
		}

		private bool IsDoubleMusketLoaded()
		{
			if (!HasWeapon(5, out int slotIndex, out int musketValue))
				return false;

			int data = Terrain.ExtractData(musketValue);
			return DoubleMusketBlock.IsLoaded(data) && DoubleMusketBlock.GetShotsRemaining(data) > 0;
		}

		private void EnsureDoubleMusketLoaded(int slotIndex, int currentValue)
		{
			int data = Terrain.ExtractData(currentValue);
			if (!DoubleMusketBlock.IsLoaded(data) || DoubleMusketBlock.GetShotsRemaining(data) == 0)
			{
				// Cargar con balas antitanque (munición principal del DoubleMusket)
				data = DoubleMusketBlock.SetLoaded(data, true);
				data = DoubleMusketBlock.SetShotsRemaining(data, 2);
				data = DoubleMusketBlock.SetAntiTanksBullet(data, true);
				// No se asigna BulletType (o se puede dejar en null)
				int newValue = Terrain.MakeBlockValue(DoubleMusketBlock.Index, 0, data);
				m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				m_componentMiner.Inventory.AddSlotItems(slotIndex, newValue, 1);
			}
		}

		private void EnsureDoubleMusketActive()
		{
			if (HasWeapon(5, out int slotIndex, out _))
			{
				if (m_componentMiner.Inventory.ActiveSlotIndex != slotIndex)
					m_componentMiner.Inventory.ActiveSlotIndex = slotIndex;
			}
		}

		private void StartDoubleMusketAim()
		{
			EnsureDoubleMusketActive();
			if (!HasWeapon(5, out int slotIndex, out int musketValue))
				return;
			EnsureDoubleMusketLoaded(slotIndex, musketValue);
			m_aimingStarted = true;
			m_isAimingRanged = true;
			m_rangedAimStartTime = m_subsystemTime.GameTime;
			m_triedToLoad = false;
		}

		private void CompleteDoubleMusketAim()
		{
			EnsureDoubleMusketActive();
			if (!HasWeapon(5, out int slotIndex, out int musketValue))
			{
				m_isAimingRanged = false;
				m_aimingStarted = false;
				return;
			}
			EnsureDoubleMusketLoaded(slotIndex, musketValue);
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
			Ray3 ray = new Ray3(eyePos, direction);
			m_componentMiner.Aim(ray, AimState.Completed);
			m_nextRangedAttackTime = m_subsystemTime.GameTime + DoubleMusketCooldown;
			m_isAimingRanged = false;
			m_aimingStarted = false;
			m_triedToLoad = false;
		}
		private void ManageWeaponSwitching(bool shouldUseMelee)
		{
			if (m_componentMiner?.Inventory == null) return;

			if (m_isAimingThrowable)
				return;

			if (shouldUseMelee)
			{
				if (!IsCurrentWeaponMelee)
				{
					int meleeSlot = FindBestMeleeWeapon(out int meleeSlotIdx, out int meleeValue);
					if (meleeSlot != -1)
					{
						CancelRangedAim();
						CancelThrowableAim();
						EquipWeapon(meleeSlot);
					}
				}
			}
			else
			{
				if (!IsCurrentWeaponRanged && !m_isAimingThrowable)
				{
					if (HasThrowableItem(out int throwSlot, out int throwValue))
					{
						EquipWeapon(throwSlot);
					}
					else
					{
						int rangedSlot = FindBestRangedWeapon(out int rangedSlotIdx, out int rangedValue);
						if (rangedSlot != -1)
						{
							CancelRangedAim();
							EquipWeapon(rangedSlot);
						}
					}
				}
			}
		}

		private int FindBestRangedWeapon(out int value)
		{
			int slot;
			return FindBestRangedWeapon(out slot, out value);
		}

		private int FindBestMeleeWeapon(out int value)
		{
			int slot;
			return FindBestMeleeWeapon(out slot, out value);
		}

		private bool ShouldForceAttackPlayer()
		{
			return (IsZombie && IsGreenNightActive) || IsBandit;
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

			if (IsZombie && !IsGreenNightActive && m_target != null && m_target.Entity.FindComponent<ComponentPlayer>() != null)
			{
				StopAttack();
				return;
			}

			if (ShouldForceAttackPlayer())
			{
				ComponentPlayer player = FindNearestPlayer(SpecialChaseRange);
				if (player != null && (m_target != player || m_target == null))
				{
					StopAttack();
					Attack(player, SpecialChaseRange, 120f, true);
					m_chaseTime = 120f;
					m_isPersistent = true;
					return;
				}
			}

			if (IsActive && m_target != null)
			{
				m_chaseTime -= dt;
				m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(m_target.ComponentCreatureModel.EyePosition);

				float distance = GetDistanceToTarget();
				bool inMeleeRange = IsTargetInAttackRange(m_target.ComponentBody);
				bool shouldUseMelee = distance <= RangedAttackRange.X;

				if (!m_isAimingThrowable)
				{
					ManageWeaponSwitching(shouldUseMelee);
				}

				if (inMeleeRange)
				{
					CancelRangedAim();
					CancelThrowableAim();

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
								m_chaseTime = MathUtils.Max(m_chaseTime, extraChaseTime);
								m_componentMiner.Hit(hitBody, hitPoint, m_componentCreature.ComponentBody.Matrix.Forward);
								m_componentCreature.ComponentCreatureSounds.PlayAttackSound();

								// Nueva funcionalidad: explosión al golpear con 10% de probabilidad
								if (ExplodeOnHit && m_random.Float(0f, 1f) < 0.1f)
								{
									// Obtener SubsystemExplosions (si no está ya en caché)
									SubsystemExplosions subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true);
									if (subsystemExplosions != null)
									{
										subsystemExplosions.AddExplosion(
											Terrain.ToCell(hitPoint.X),
											Terrain.ToCell(hitPoint.Y),
											Terrain.ToCell(hitPoint.Z),
											255f,        // presión típica de barril mediano
											false,      // no incendiario
											false       // con sonido de explosión
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
									m_target.ComponentBody.ApplyImpulse(direction * 55f);
									m_target.ComponentBody.MaxSpeed = originalMaxSpeed;
								}

								if (InvokeLightningOnHit && m_subsystemSky != null && m_random.Float(0f, 1f) < 0.05f)
								{
									m_subsystemSky.MakeLightningStrike(m_target.ComponentBody.Position, true);
								}
							}
						}
					}
					m_isAimingRanged = false;
					m_isAimingThrowable = false;
					m_aimingStarted = false;
				}
				else
				{
					if (HasThrowableItem(out _, out _))
					{
						UpdateThrowableAttack(dt);
					}
					else if (IsCurrentWeaponRanged)
					{
						UpdateRangedAttack(dt);
					}
				}
			}

			if (IsActive && m_target != null && PlaceBlocksWhenTargetHigh)
			{
				TryPlaceDirtBlocksToReachTarget();
			}

			if (m_subsystemTime.GameTime >= m_nextUpdateTime)

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

		private bool HasThrowableItem(out int slotIndex, out int value)
		{
			slotIndex = -1;
			value = 0;
			if (m_componentMiner?.Inventory == null) return false;

			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				int slotCount = m_componentMiner.Inventory.GetSlotCount(i);
				if (slotCount > 0 && IsThrowableBlock(slotValue))
				{
					slotIndex = i;
					value = slotValue;
					return true;
				}
			}
			return false;
		}

		private bool IsThrowableBlock(int value)
		{
			int contents = Terrain.ExtractContents(value);
			return s_throwableBlockIndices.Contains(contents);
		}

		private void EnsureThrowableActive()
		{
			if (m_componentMiner?.Inventory == null) return;

			if (HasThrowableItem(out int slotIndex, out _))
			{
				if (m_componentMiner.Inventory.ActiveSlotIndex != slotIndex)
				{
					m_componentMiner.Inventory.ActiveSlotIndex = slotIndex;
				}
			}
		}

		private bool HasLineOfSightToTarget()
		{
			if (m_target == null) return false;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			ComponentBody targetBody = m_target.ComponentBody;

			Vector3[] targetPoints = {
				targetBody.BoundingBox.Center(),
				m_target.ComponentCreatureModel.EyePosition,
				targetBody.BoundingBox.Min + new Vector3(0, 0.1f, 0),
				targetBody.BoundingBox.Max - new Vector3(0, 0.1f, 0),
				targetBody.BoundingBox.Min + new Vector3(0.3f, 0.5f, 0.3f),
				targetBody.BoundingBox.Max - new Vector3(0.3f, 0.5f, 0.3f)
			};

			foreach (Vector3 targetPoint in targetPoints)
			{
				Vector3 direction = targetPoint - eyePos;
				float distance = direction.Length();
				if (distance < 0.1f) continue;
				direction /= distance;

				Ray3 ray = new Ray3(eyePos, direction);
				object result = m_componentMiner.Raycast(ray, RaycastMode.Interaction, true, true, true, null);

				if (result is BodyRaycastResult bodyResult)
				{
					if (bodyResult.ComponentBody == targetBody ||
						targetBody.IsChildOfBody(bodyResult.ComponentBody) ||
						bodyResult.ComponentBody.IsChildOfBody(targetBody))
					{
						return true;
					}

					if (bodyResult.ComponentBody == m_componentCreature.ComponentBody)
					{
						continue;
					}

					if (bodyResult.Distance < distance)
					{
						continue;
					}
				}
				else if (result is TerrainRaycastResult terrainResult)
				{
					if (terrainResult.Distance < distance)
					{
						continue;
					}
				}
				else
				{
					return true;
				}
			}

			return false;
		}

		private void StartThrowableAim()
		{
			if (!HasThrowableItem(out int slotIndex, out int value))
				return;

			m_throwableSlotIndex = slotIndex;
			m_throwableValue = value;
			EnsureThrowableActive();

			m_isAimingThrowable = true;
			m_throwableAimStartTime = m_subsystemTime.GameTime;
		}

		private void CompleteThrowableAim()
		{
			if (!HasThrowableItem(out int slotIndex, out int value))
			{
				CancelThrowableAim();
				return;
			}

			EnsureThrowableActive();

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
			Ray3 ray = new Ray3(eyePos, direction);

			m_componentMiner.Aim(ray, AimState.Completed);

			m_nextThrowableAttackTime = m_subsystemTime.GameTime + ThrowableCooldown;

			m_isAimingThrowable = false;
			m_throwableSlotIndex = -1;
			m_throwableValue = 0;

			if (!HasThrowableItem(out _, out _))
			{
				if (IsTargetInAttackRange(m_target?.ComponentBody))
				{
					int meleeSlot = FindBestMeleeWeapon(out int meleeSlotIdx, out int meleeValue);
					if (meleeSlot != -1)
						EquipWeapon(meleeSlot);
				}
				else
				{
					int rangedSlot = FindBestRangedWeapon(out int rangedSlotIdx, out int rangedValue);
					if (rangedSlot != -1)
						EquipWeapon(rangedSlot);
				}
			}
		}

		private void CancelThrowableAim()
		{
			if (m_isAimingThrowable)
			{
				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
				Ray3 ray = new Ray3(eyePos, direction);
				m_componentMiner.Aim(ray, AimState.Cancelled);
			}
			m_isAimingThrowable = false;
			m_throwableSlotIndex = -1;
			m_throwableValue = 0;

			if (!HasThrowableItem(out _, out _))
			{
				if (IsTargetInAttackRange(m_target?.ComponentBody))
				{
					int meleeSlot = FindBestMeleeWeapon(out int meleeSlotIdx, out int meleeValue);
					if (meleeSlot != -1)
						EquipWeapon(meleeSlot);
				}
				else
				{
					int rangedSlot = FindBestRangedWeapon(out int rangedSlotIdx, out int rangedValue);
					if (rangedSlot != -1)
						EquipWeapon(rangedSlot);
				}
			}
		}

		private void UpdateThrowableAttack(float dt)
		{
			if (m_target == null)
			{
				CancelThrowableAim();
				return;
			}

			if (!IsTargetInViewCone())
			{
				CancelThrowableAim();
				return;
			}

			if (m_componentPathfinding.IsStuck)
			{
				CancelThrowableAim();
				return;
			}

			float distance = GetDistanceToTarget();
			bool inThrowableRange = distance >= ThrowableAttackRange.X && distance <= ThrowableAttackRange.Y;

			if (!inThrowableRange)
			{
				CancelThrowableAim();
				return;
			}

			if (!HasThrowableItem(out int slotIndex, out int value))
			{
				CancelThrowableAim();
				if (!HasThrowableItem(out _, out _))
				{
					if (IsTargetInAttackRange(m_target?.ComponentBody))
					{
						int meleeSlot = FindBestMeleeWeapon(out int meleeSlotIdx, out int meleeValue);
						if (meleeSlot != -1)
							EquipWeapon(meleeSlot);
					}
					else
					{
						int rangedSlot = FindBestRangedWeapon(out int rangedSlotIdx, out int rangedValue);
						if (rangedSlot != -1)
							EquipWeapon(rangedSlot);
					}
				}
				return;
			}

			if (!HasLineOfSightToTarget())
			{
				if (!m_componentPathfinding.IsStuck)
				{
					if (m_subsystemTime.GameTime - m_lastLateralMoveTime > 1.0)
					{
						Vector3 toTarget = m_target.ComponentBody.Position - m_componentCreature.ComponentBody.Position;
						toTarget.Y = 0;
						if (toTarget.LengthSquared() > 0.1f)
						{
							toTarget = Vector3.Normalize(toTarget);
							Vector3 perpendicular = Vector3.Cross(toTarget, Vector3.UnitY);
							float direction = m_random.Float(0f, 1f) > 0.5f ? 1f : -1f;
							Vector3 lateralMove = m_componentCreature.ComponentBody.Position + direction * perpendicular * 5f;
							m_componentPathfinding.SetDestination(lateralMove, 1f, 1.5f, 100, true, false, true, null);
						}
						else
						{
							Vector3 lateralMove = m_componentCreature.ComponentBody.Position +
								new Vector3(3f * m_random.Float(-1f, 1f), 0, 3f * m_random.Float(-1f, 1f));
							m_componentPathfinding.SetDestination(lateralMove, 1f, 1.5f, 100, true, false, true, null);
						}
						m_lastLateralMoveTime = m_subsystemTime.GameTime;
					}
				}
				return;
			}

			if (m_subsystemTime.GameTime < m_nextThrowableAttackTime)
			{
				return;
			}

			if (!m_isAimingThrowable)
			{
				if (m_componentPathfinding != null)
				{
					m_componentPathfinding.Stop();
				}

				StartThrowableAim();
				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
				Vector3 direction = targetCenter - eyePos;
				float len = MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
				direction = new Vector3(direction.X / len, direction.Y / len, direction.Z / len);
				Ray3 ray = new Ray3(eyePos, direction);
				m_componentMiner.Aim(ray, AimState.InProgress);
			}
			else
			{
				if (!IsTargetInViewCone())
				{
					CancelThrowableAim();
					return;
				}

				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
				Vector3 direction = targetCenter - eyePos;
				float len = MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
				direction = new Vector3(direction.X / len, direction.Y / len, direction.Z / len);
				Ray3 ray = new Ray3(eyePos, direction);
				m_componentMiner.Aim(ray, AimState.InProgress);

				double aimTimeElapsed = m_subsystemTime.GameTime - m_throwableAimStartTime;
				if (aimTimeElapsed >= ThrowableAimTime)
				{
					CompleteThrowableAim();
				}
				else if (!HasThrowableItem(out _, out _))
				{
					if (HasThrowableItem(out int newSlot, out int newValue))
					{
						m_throwableSlotIndex = newSlot;
						m_throwableValue = newValue;
						EnsureThrowableActive();
					}
					else
					{
						CancelThrowableAim();
					}
				}
			}
		}

		// ===== MÉTODOS DE CARGA DE ARMAS A DISTANCIA =====
		private bool IsMusketLoaded()
		{
			if (!HasWeapon(0, out int slotIndex, out int musketValue))
				return false;

			int data = Terrain.ExtractData(musketValue);
			return MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded;
		}

		private bool IsBowLoaded()
		{
			if (!HasWeapon(1, out int slotIndex, out int bowValue))
				return false;

			int data = Terrain.ExtractData(bowValue);
			return BowBlock.GetArrowType(data) != null;
		}

		private bool IsCrossbowLoaded()
		{
			if (!HasWeapon(2, out int slotIndex, out int crossbowValue))
				return false;

			int data = Terrain.ExtractData(crossbowValue);
			return CrossbowBlock.GetDraw(data) == 15 && CrossbowBlock.GetArrowType(data) != null;
		}

		private bool IsRepeatCrossbowLoaded()
		{
			if (!HasWeapon(3, out int slotIndex, out int crossbowValue))
				return false;

			int data = Terrain.ExtractData(crossbowValue);
			int draw = RepeatCrossbowBlock.GetDraw(data);
			RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
			return draw == 15 && arrowType != null;
		}

		private bool IsFlameThrowerLoaded()
		{
			if (!HasWeapon(4, out int slotIndex, out int flameThrowerValue))
				return false;

			int data = Terrain.ExtractData(flameThrowerValue);
			FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
			FlameBulletBlock.FlameBulletType? bulletType = FlameThrowerBlock.GetBulletType(data);
			int loadCount = FlameThrowerBlock.GetLoadCount(flameThrowerValue);

			return loadState == FlameThrowerBlock.LoadState.Loaded && bulletType != null && loadCount > 0;
		}

		private void EnsureMusketLoaded(int slotIndex, int currentValue)
		{
			int data = Terrain.ExtractData(currentValue);
			if (MusketBlock.GetLoadState(data) != MusketBlock.LoadState.Loaded)
			{
				data = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
				BulletBlock.BulletType bulletType;
				int bulletIndex = m_random.Int(0, 2);
				if (bulletIndex == 0)
					bulletType = BulletBlock.BulletType.MusketBall;
				else if (bulletIndex == 1)
					bulletType = BulletBlock.BulletType.Buckshot;
				else
					bulletType = BulletBlock.BulletType.BuckshotBall;
				data = MusketBlock.SetBulletType(data, bulletType);
				int newValue = Terrain.MakeBlockValue(MusketBlock.Index, 0, data);
				m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				m_componentMiner.Inventory.AddSlotItems(slotIndex, newValue, 1);
			}
		}

		private void EnsureBowLoaded(int slotIndex, int currentValue)
		{
			int data = Terrain.ExtractData(currentValue);
			if (BowBlock.GetArrowType(data) == null)
			{
				ArrowBlock.ArrowType[] arrowTypes = new ArrowBlock.ArrowType[]
				{
					ArrowBlock.ArrowType.WoodenArrow,
					ArrowBlock.ArrowType.StoneArrow,
					ArrowBlock.ArrowType.IronArrow,
					ArrowBlock.ArrowType.DiamondArrow,
					ArrowBlock.ArrowType.FireArrow,
					ArrowBlock.ArrowType.CopperArrow
				};
				ArrowBlock.ArrowType selected = arrowTypes[m_random.Int(0, arrowTypes.Length - 1)];
				data = BowBlock.SetArrowType(data, selected);
				int newValue = Terrain.MakeBlockValue(BowBlock.Index, 0, data);
				m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				m_componentMiner.Inventory.AddSlotItems(slotIndex, newValue, 1);
			}
		}

		private void EnsureCrossbowLoaded(int slotIndex, int currentValue)
		{
			int data = Terrain.ExtractData(currentValue);
			bool loaded = CrossbowBlock.GetDraw(data) == 15 && CrossbowBlock.GetArrowType(data) != null;
			if (!loaded)
			{
				float distance = GetDistanceToTarget();
				ArrowBlock.ArrowType selected;
				if (distance <= 20f)
				{
					ArrowBlock.ArrowType[] allBolts = new ArrowBlock.ArrowType[]
					{
						ArrowBlock.ArrowType.IronBolt,
						ArrowBlock.ArrowType.DiamondBolt,
						ArrowBlock.ArrowType.ExplosiveBolt
					};
					selected = allBolts[m_random.Int(0, allBolts.Length - 1)];
				}
				else
				{
					ArrowBlock.ArrowType[] nonExplosiveBolts = new ArrowBlock.ArrowType[]
					{
						ArrowBlock.ArrowType.IronBolt,
						ArrowBlock.ArrowType.DiamondBolt
					};
					selected = nonExplosiveBolts[m_random.Int(0, nonExplosiveBolts.Length - 1)];
				}
				data = CrossbowBlock.SetArrowType(data, selected);
				data = CrossbowBlock.SetDraw(data, 15);
				int newValue = Terrain.MakeBlockValue(CrossbowBlock.Index, 0, data);
				m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				m_componentMiner.Inventory.AddSlotItems(slotIndex, newValue, 1);
			}
		}

		private void EnsureRepeatCrossbowLoaded(int slotIndex, int currentValue)
		{
			int data = Terrain.ExtractData(currentValue);
			int draw = RepeatCrossbowBlock.GetDraw(data);
			RepeatArrowBlock.ArrowType? currentArrowType = RepeatCrossbowBlock.GetArrowType(data);

			if (draw == 15 && currentArrowType != null)
				return;

			float distance = GetDistanceToTarget();
			RepeatArrowBlock.ArrowType selectedType;
			if (distance <= 20f)
			{
				Array arrowTypes = Enum.GetValues(typeof(RepeatArrowBlock.ArrowType));
				selectedType = (RepeatArrowBlock.ArrowType)arrowTypes.GetValue(m_random.Int(0, arrowTypes.Length - 1));
			}
			else
			{
				RepeatArrowBlock.ArrowType[] nonExplosiveTypes = new RepeatArrowBlock.ArrowType[]
				{
					RepeatArrowBlock.ArrowType.CopperArrow,
					RepeatArrowBlock.ArrowType.IronArrow,
					RepeatArrowBlock.ArrowType.DiamondArrow,
					RepeatArrowBlock.ArrowType.PoisonArrow,
					RepeatArrowBlock.ArrowType.SeriousPoisonArrow
				};
				selectedType = nonExplosiveTypes[m_random.Int(0, nonExplosiveTypes.Length - 1)];
			}
			int newData = RepeatCrossbowBlock.SetDraw(data, 15);
			newData = RepeatCrossbowBlock.SetArrowType(newData, selectedType);
			int newValue = Terrain.MakeBlockValue(RepeatCrossbowBlock.Index, 0, newData);
			m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
			m_componentMiner.Inventory.AddSlotItems(slotIndex, newValue, 1);
		}

		private void EnsureFlameThrowerLoaded(int slotIndex, int currentValue)
		{
			int data = Terrain.ExtractData(currentValue);
			FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
			FlameBulletBlock.FlameBulletType? currentBulletType = FlameThrowerBlock.GetBulletType(data);
			int currentLoadCount = FlameThrowerBlock.GetLoadCount(currentValue);

			if (loadState == FlameThrowerBlock.LoadState.Loaded && currentBulletType != null && currentLoadCount > 0)
				return;

			FlameBulletBlock.FlameBulletType newBulletType;
			if (currentBulletType.HasValue && currentLoadCount == 0)
			{
				newBulletType = (currentBulletType.Value == FlameBulletBlock.FlameBulletType.Flame)
					? FlameBulletBlock.FlameBulletType.Poison
					: FlameBulletBlock.FlameBulletType.Flame;
			}
			else if (currentBulletType.HasValue)
			{
				newBulletType = currentBulletType.Value;
			}
			else
			{
				newBulletType = m_random.Bool(0.5f)
					? FlameBulletBlock.FlameBulletType.Flame
					: FlameBulletBlock.FlameBulletType.Poison;
			}

			int newData = data;
			newData = FlameThrowerBlock.SetLoadState(newData, FlameThrowerBlock.LoadState.Loaded);
			newData = FlameThrowerBlock.SetBulletType(newData, newBulletType);
			newData = FlameThrowerBlock.SetSwitchState(newData, true);
			int newValue = Terrain.MakeBlockValue(FlameThrowerBlock.Index, 1, newData);
			newValue = FlameThrowerBlock.SetLoadCount(newValue, 15);

			m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
			m_componentMiner.Inventory.AddSlotItems(slotIndex, newValue, 1);
		}

		private void EnsureMusketActive()
		{
			if (HasWeapon(0, out int slotIndex, out _))
			{
				if (m_componentMiner.Inventory.ActiveSlotIndex != slotIndex)
					m_componentMiner.Inventory.ActiveSlotIndex = slotIndex;
			}
		}

		private void EnsureBowActive()
		{
			if (HasWeapon(1, out int slotIndex, out _))
			{
				if (m_componentMiner.Inventory.ActiveSlotIndex != slotIndex)
					m_componentMiner.Inventory.ActiveSlotIndex = slotIndex;
			}
		}

		private void EnsureCrossbowActive()
		{
			if (HasWeapon(2, out int slotIndex, out _))
			{
				if (m_componentMiner.Inventory.ActiveSlotIndex != slotIndex)
					m_componentMiner.Inventory.ActiveSlotIndex = slotIndex;
			}
		}

		private void EnsureRepeatCrossbowActive()
		{
			if (HasWeapon(3, out int slotIndex, out _))
			{
				if (m_componentMiner.Inventory.ActiveSlotIndex != slotIndex)
					m_componentMiner.Inventory.ActiveSlotIndex = slotIndex;
			}
		}

		private void EnsureFlameThrowerActive()
		{
			if (HasWeapon(4, out int slotIndex, out _))
			{
				if (m_componentMiner.Inventory.ActiveSlotIndex != slotIndex)
					m_componentMiner.Inventory.ActiveSlotIndex = slotIndex;
			}
		}

		private void StartMusketAim()
		{
			EnsureMusketActive();
			if (!HasWeapon(0, out int slotIndex, out int musketValue))
				return;
			EnsureMusketLoaded(slotIndex, musketValue);
			m_aimingStarted = true;
			m_isAimingRanged = true;
			m_rangedAimStartTime = m_subsystemTime.GameTime;
			m_triedToLoad = false;
		}

		private void StartBowAim()
		{
			EnsureBowActive();
			if (!HasWeapon(1, out int slotIndex, out int bowValue))
				return;
			EnsureBowLoaded(slotIndex, bowValue);
			m_aimingStarted = true;
			m_isAimingRanged = true;
			m_rangedAimStartTime = m_subsystemTime.GameTime;
			m_triedToLoad = false;
		}

		private void StartCrossbowAim()
		{
			EnsureCrossbowActive();
			if (!HasWeapon(2, out int slotIndex, out int crossbowValue))
				return;
			EnsureCrossbowLoaded(slotIndex, crossbowValue);
			m_aimingStarted = true;
			m_isAimingRanged = true;
			m_rangedAimStartTime = m_subsystemTime.GameTime;
			m_triedToLoad = false;
		}

		private void StartRepeatCrossbowAim()
		{
			EnsureRepeatCrossbowActive();
			if (!HasWeapon(3, out int slotIndex, out int crossbowValue))
				return;
			EnsureRepeatCrossbowLoaded(slotIndex, crossbowValue);
			m_aimingStarted = true;
			m_isAimingRanged = true;
			m_rangedAimStartTime = m_subsystemTime.GameTime;
			m_triedToLoad = false;
		}

		private void StartFlameThrowerAim()
		{
			EnsureFlameThrowerActive();
			if (!HasWeapon(4, out int slotIndex, out int flameThrowerValue))
				return;
			EnsureFlameThrowerLoaded(slotIndex, flameThrowerValue);
			m_aimingStarted = true;
			m_isAimingRanged = true;
			m_rangedAimStartTime = m_subsystemTime.GameTime;
			m_triedToLoad = false;
		}

		private void CompleteMusketAim()
		{
			EnsureMusketActive();
			if (!HasWeapon(0, out int slotIndex, out int musketValue))
			{
				m_isAimingRanged = false;
				m_aimingStarted = false;
				return;
			}
			EnsureMusketLoaded(slotIndex, musketValue);
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
			Ray3 ray = new Ray3(eyePos, direction);
			m_componentMiner.Aim(ray, AimState.Completed);
			m_nextRangedAttackTime = m_subsystemTime.GameTime + MusketCooldown;
			m_isAimingRanged = false;
			m_aimingStarted = false;
			m_triedToLoad = false;
		}

		private void CompleteBowAim()
		{
			EnsureBowActive();
			if (!HasWeapon(1, out int slotIndex, out int bowValue))
			{
				m_isAimingRanged = false;
				m_aimingStarted = false;
				return;
			}
			EnsureBowLoaded(slotIndex, bowValue);
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
			Ray3 ray = new Ray3(eyePos, direction);
			m_componentMiner.Aim(ray, AimState.Completed);
			m_nextRangedAttackTime = m_subsystemTime.GameTime + BowCooldown;
			m_isAimingRanged = false;
			m_aimingStarted = false;
			m_triedToLoad = false;
		}

		private void CompleteCrossbowAim()
		{
			EnsureCrossbowActive();
			if (!HasWeapon(2, out int slotIndex, out int crossbowValue))
			{
				m_isAimingRanged = false;
				m_aimingStarted = false;
				return;
			}
			EnsureCrossbowLoaded(slotIndex, crossbowValue);
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
			Ray3 ray = new Ray3(eyePos, direction);
			m_componentMiner.Aim(ray, AimState.Completed);
			m_nextRangedAttackTime = m_subsystemTime.GameTime + CrossbowCooldown;
			m_isAimingRanged = false;
			m_aimingStarted = false;
			m_triedToLoad = false;
		}

		private void CompleteRepeatCrossbowAim()
		{
			EnsureRepeatCrossbowActive();
			if (!HasWeapon(3, out int slotIndex, out int crossbowValue))
			{
				m_isAimingRanged = false;
				m_aimingStarted = false;
				return;
			}
			int data = Terrain.ExtractData(crossbowValue);
			int draw = RepeatCrossbowBlock.GetDraw(data);
			RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
			if (draw != 15 || arrowType == null)
			{
				CancelRangedAim();
				return;
			}
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
			Ray3 ray = new Ray3(eyePos, direction);
			m_componentMiner.Aim(ray, AimState.Completed);
			m_nextRangedAttackTime = m_subsystemTime.GameTime + RepeatCrossbowCooldown;
			int newData = RepeatCrossbowBlock.SetDraw(RepeatCrossbowBlock.SetArrowType(data, null), 0);
			int newValue = Terrain.MakeBlockValue(RepeatCrossbowBlock.Index, 0, newData);
			m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
			m_componentMiner.Inventory.AddSlotItems(slotIndex, newValue, 1);
			m_isAimingRanged = false;
			m_aimingStarted = false;
			m_triedToLoad = false;
		}

		private void CompleteFlameThrowerAim()
		{
			EnsureFlameThrowerActive();
			if (!HasWeapon(4, out int slotIndex, out int flameThrowerValue))
			{
				m_isAimingRanged = false;
				m_aimingStarted = false;
				return;
			}
			m_nextRangedAttackTime = m_subsystemTime.GameTime + FlameThrowerCooldown;
			m_isAimingRanged = false;
			m_aimingStarted = false;
			m_triedToLoad = false;
		}

		private void CancelRangedAim()
		{
			if (m_isAimingRanged)
			{
				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
				Ray3 ray = new Ray3(eyePos, direction);
				m_componentMiner.Aim(ray, AimState.Cancelled);
			}
			m_isAimingRanged = false;
			m_aimingStarted = false;
			m_triedToLoad = false;
		}

		private void HandleRangedWeapon(int weaponType, int slot, int currentValue)
		{
			if (!m_isAimingRanged && m_subsystemTime.GameTime < m_nextRangedAttackTime)
				return;

			float aimTime;
			float cooldown;
			System.Func<bool> isLoaded;
			System.Action<int, int> ensureLoaded;
			System.Action ensureActive;
			System.Action startAim;
			System.Action completeAim;
			bool isRepeatCrossbow = false;

			switch (weaponType)
			{
				case 0:
					aimTime = MusketAimTime;
					cooldown = MusketCooldown;
					isLoaded = IsMusketLoaded;
					ensureLoaded = EnsureMusketLoaded;
					ensureActive = EnsureMusketActive;
					startAim = StartMusketAim;
					completeAim = CompleteMusketAim;
					break;
				case 1:
					aimTime = BowAimTime;
					cooldown = BowCooldown;
					isLoaded = IsBowLoaded;
					ensureLoaded = EnsureBowLoaded;
					ensureActive = EnsureBowActive;
					startAim = StartBowAim;
					completeAim = CompleteBowAim;
					break;
				case 2:
					aimTime = CrossbowAimTime;
					cooldown = CrossbowCooldown;
					isLoaded = IsCrossbowLoaded;
					ensureLoaded = EnsureCrossbowLoaded;
					ensureActive = EnsureCrossbowActive;
					startAim = StartCrossbowAim;
					completeAim = CompleteCrossbowAim;
					break;
				case 3:
					aimTime = RepeatCrossbowAimTime;
					cooldown = RepeatCrossbowCooldown;
					isLoaded = () =>
					{
						if (!HasWeapon(3, out int s, out int v)) return false;
						int data = Terrain.ExtractData(v);
						return RepeatCrossbowBlock.GetDraw(data) == 15 && RepeatCrossbowBlock.GetArrowType(data) != null;
					};
					ensureLoaded = EnsureRepeatCrossbowLoaded;
					ensureActive = EnsureRepeatCrossbowActive;
					startAim = StartRepeatCrossbowAim;
					completeAim = () =>
					{
						EnsureRepeatCrossbowActive();
						if (!HasWeapon(3, out int s, out int v))
						{
							m_isAimingRanged = false;
							m_aimingStarted = false;
							return;
						}
						int data = Terrain.ExtractData(v);
						int draw = RepeatCrossbowBlock.GetDraw(data);
						RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
						if (draw != 15 || arrowType == null)
						{
							CancelRangedAim();
							return;
						}
						Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
						Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
						Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
						Ray3 ray = new Ray3(eyePos, direction);
						m_componentMiner.Aim(ray, AimState.Completed);
						m_nextRangedAttackTime = m_subsystemTime.GameTime + RepeatCrossbowCooldown;
						int newData = RepeatCrossbowBlock.SetDraw(RepeatCrossbowBlock.SetArrowType(data, null), 0);
						int newValue = Terrain.MakeBlockValue(RepeatCrossbowBlock.Index, 0, newData);
						m_componentMiner.Inventory.RemoveSlotItems(s, 1);
						m_componentMiner.Inventory.AddSlotItems(s, newValue, 1);
						m_isAimingRanged = false;
						m_aimingStarted = false;
						m_triedToLoad = false;
					};
					isRepeatCrossbow = true;
					break;
				case 4:
					aimTime = FlameThrowerAimTime;
					cooldown = FlameThrowerCooldown;
					isLoaded = IsFlameThrowerLoaded;
					ensureLoaded = EnsureFlameThrowerLoaded;
					ensureActive = EnsureFlameThrowerActive;
					startAim = StartFlameThrowerAim;
					completeAim = () =>
					{
						EnsureFlameThrowerActive();
						if (!HasWeapon(4, out int s, out int v))
						{
							m_isAimingRanged = false;
							m_aimingStarted = false;
							return;
						}
						m_nextRangedAttackTime = m_subsystemTime.GameTime + FlameThrowerCooldown;
						m_isAimingRanged = false;
						m_aimingStarted = false;
						m_triedToLoad = false;
					};
					break;
				case 5:
					aimTime = DoubleMusketAimTime;
					cooldown = DoubleMusketCooldown;
					isLoaded = IsDoubleMusketLoaded;
					ensureLoaded = EnsureDoubleMusketLoaded;
					ensureActive = EnsureDoubleMusketActive;
					startAim = StartDoubleMusketAim;
					completeAim = CompleteDoubleMusketAim;
					break;
				default:
					return;
			}

			if (!m_isAimingRanged)
			{
				if (!isLoaded())
				{
					if (!m_triedToLoad)
					{
						ensureLoaded(slot, currentValue);
						m_triedToLoad = true;
					}
					return;
				}

				startAim();
				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
				Ray3 ray = new Ray3(eyePos, direction);
				m_componentMiner.Aim(ray, AimState.InProgress);
			}
			else
			{
				if (!IsTargetInViewCone())
				{
					CancelRangedAim();
					return;
				}

				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
				Ray3 ray = new Ray3(eyePos, direction);
				m_componentMiner.Aim(ray, AimState.InProgress);

				double aimTimeElapsed = m_subsystemTime.GameTime - m_rangedAimStartTime;
				if (aimTimeElapsed >= aimTime)
				{
					if (weaponType == 0 && m_random.Float(0f, 1f) < 0.05f)
					{
						int musketSlot = slot;
						int data = Terrain.ExtractData(currentValue);
						int newData = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
						newData = MusketBlock.SetBulletType(newData, BulletBlock.BulletType.MusketBall);
						int musketBallValue = Terrain.MakeBlockValue(MusketBlock.Index, 0, newData);
						newData = MusketBlock.SetBulletType(data, BulletBlock.BulletType.Buckshot);
						int buckshotValue = Terrain.MakeBlockValue(MusketBlock.Index, 0, newData);
						newData = MusketBlock.SetBulletType(data, BulletBlock.BulletType.BuckshotBall);
						int buckshotBallValue = Terrain.MakeBlockValue(MusketBlock.Index, 0, newData);

						m_componentMiner.Inventory.RemoveSlotItems(musketSlot, 1);
						m_componentMiner.Inventory.AddSlotItems(musketSlot, musketBallValue, 1);
						m_componentMiner.Aim(ray, AimState.Completed);
						m_componentMiner.Inventory.RemoveSlotItems(musketSlot, 1);
						m_componentMiner.Inventory.AddSlotItems(musketSlot, buckshotValue, 1);
						m_componentMiner.Aim(ray, AimState.Completed);
						m_componentMiner.Inventory.RemoveSlotItems(musketSlot, 1);
						m_componentMiner.Inventory.AddSlotItems(musketSlot, buckshotBallValue, 1);
						m_componentMiner.Aim(ray, AimState.Completed);
						int emptyValue = Terrain.MakeBlockValue(MusketBlock.Index, 0, MusketBlock.SetLoadState(data, MusketBlock.LoadState.Empty));
						m_componentMiner.Inventory.AddSlotItems(musketSlot, emptyValue, 1);
					}
					else
					{
						completeAim();
					}

					m_nextRangedAttackTime = m_subsystemTime.GameTime + cooldown;
					if (!isRepeatCrossbow)
					{
						m_isAimingRanged = false;
						m_aimingStarted = false;
						m_triedToLoad = false;
					}
				}
				else if (!isLoaded())
				{
					CancelRangedAim();
				}
			}
		}

		private void UpdateRangedAttack(float dt)
		{
			if (m_target == null)
			{
				CancelRangedAim();
				return;
			}

			if (!IsTargetInViewCone())
			{
				CancelRangedAim();
				return;
			}

			if (m_componentPathfinding.IsStuck)
			{
				CancelRangedAim();
				return;
			}

			if (!HasLineOfSightToTarget())
			{
				CancelRangedAim();

				if (m_componentPathfinding.Destination != null && !m_componentPathfinding.IsStuck)
				{
					if (m_componentPathfinding.m_componentPilot.Destination == null)
					{
						Vector3 toTarget = m_target.ComponentBody.Position - m_componentCreature.ComponentBody.Position;
						toTarget.Y = 0;
						if (toTarget.LengthSquared() > 0.1f)
						{
							toTarget = Vector3.Normalize(toTarget);
							Vector3 perpendicular = Vector3.Cross(toTarget, Vector3.UnitY);
							float direction = m_random.Float(0f, 1f) > 0.5f ? 1f : -1f;
							Vector3 lateralMove = m_componentCreature.ComponentBody.Position + direction * perpendicular * 5f;
							m_componentPathfinding.SetDestination(lateralMove, 1f, 1.5f, 100, true, false, true, null);
						}
						else
						{
							Vector3 lateralMove = m_componentCreature.ComponentBody.Position +
								new Vector3(3f * m_random.Float(-1f, 1f), 0, 3f * m_random.Float(-1f, 1f));
							m_componentPathfinding.SetDestination(lateralMove, 1f, 1.5f, 100, true, false, true, null);
						}
					}
				}
				return;
			}

			float distance = GetDistanceToTarget();
			bool inRangedRange = false;
			if (RangedAttackMode == AttackMode.Remote)
				inRangedRange = distance <= RangedAttackRange.Y;
			else if (RangedAttackMode == AttackMode.Default)
				inRangedRange = distance >= RangedAttackRange.X && distance <= RangedAttackRange.Y && distance > MaxAttackRange;
			else
			{
				CancelRangedAim();
				return;
			}

			if (!inRangedRange)
			{
				CancelRangedAim();
				return;
			}

			int weaponType = -1;
			int slot = -1;
			int value = 0;

			if (HasWeapon(0, out int musketSlot, out int musketValue))
			{
				weaponType = 0;
				slot = musketSlot;
				value = musketValue;
			}
			else if (HasWeapon(1, out int bowSlot, out int bowValue))
			{
				weaponType = 1;
				slot = bowSlot;
				value = bowValue;
			}
			else if (HasWeapon(2, out int crossbowSlot, out int crossbowValue))
			{
				weaponType = 2;
				slot = crossbowSlot;
				value = crossbowValue;
			}
			else if (HasWeapon(3, out int repeatCrossbowSlot, out int repeatCrossbowValue))
			{
				weaponType = 3;
				slot = repeatCrossbowSlot;
				value = repeatCrossbowValue;
			}
			else if (HasWeapon(4, out int flameThrowerSlot, out int flameThrowerValue))
			{
				weaponType = 4;
				slot = flameThrowerSlot;
				value = flameThrowerValue;
			}
			else if (HasWeapon(5, out int doubleMusketSlot, out int doubleMusketValue))
			{
				weaponType = 5;
				slot = doubleMusketSlot;
				value = doubleMusketValue;
			}

			if (weaponType == -1)
			{
				CancelRangedAim();
				return;
			}

			HandleRangedWeapon(weaponType, slot, value);
		}

		private float GetDistanceToTarget()
		{
			if (m_target == null) return float.MaxValue;
			Vector3 selfPos = m_componentCreature.ComponentBody.Position;
			Vector3 targetPos = m_target.ComponentBody.Position;
			return Vector3.Distance(selfPos, targetPos);
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
						DestroyBlockAtPosition(eyePos + Vector3.UnitY * 0.5f);
						DestroyBlockAtPosition(eyePos + Vector3.UnitY * 1.6f);
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
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);

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
			RangedAttackRange = valuesDictionary.GetValue<Vector2>("RangedAttackRange", new Vector2(5f, 20f));
			RangedAttackMode = valuesDictionary.GetValue<AttackMode>("AttackMode", AttackMode.Default);
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
				Attack(attacker, 20f, 30f, false);

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

			if (m_componentHerd != null && !m_componentHerd.CanAttackCreature(creature))
				return false;

			return true;
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap) { }

		public override void Dispose()
		{
			UnsubscribeFromProjectileEvents();

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

					Attack(attacker, range, time, persistent);

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

			// Limpiar lista de bloques que ya no son de tierra
			m_placedDirtBlocks.RemoveAll(p =>
			{
				int contents = m_subsystemTerrain.Terrain.GetCellContents(p.X, p.Y, p.Z);
				return contents != DirtBlock.Index;
			});

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

			if (!m_subsystemTerrain.Terrain.IsCellValid(feetX, targetY1, feetZ) ||
				!m_subsystemTerrain.Terrain.IsCellValid(feetX, targetY2, feetZ))
				return;

			int contents1 = m_subsystemTerrain.Terrain.GetCellContents(feetX, targetY1, feetZ);
			int contents2 = m_subsystemTerrain.Terrain.GetCellContents(feetX, targetY2, feetZ);
			if (contents1 != 0 || contents2 != 0)
				return;

			// --- GESTIÓN DE INVENTARIO (INVOCACIÓN MÁGICA) ---
			IInventory inventory = m_componentMiner.Inventory;
			int originalActiveSlot = inventory?.ActiveSlotIndex ?? 0;
			int originalSlotValue = 0;
			int originalSlotCount = 0;
			bool hadItem = false;
			if (inventory != null)
			{
				originalSlotValue = inventory.GetSlotValue(originalActiveSlot);
				originalSlotCount = inventory.GetSlotCount(originalActiveSlot);
				hadItem = originalSlotCount > 0;
			}

			int dirtValue = Terrain.MakeBlockValue(DirtBlock.Index);
			// Reemplazar el slot activo con exactamente 2 bloques de tierra (invocación)
			if (inventory != null)
			{
				inventory.RemoveSlotItems(originalActiveSlot, int.MaxValue); // vaciar completamente
				inventory.AddSlotItems(originalActiveSlot, dirtValue, 2);
				// Asegurar que el cambio se refleje en la mano inmediatamente
				m_componentMiner.Inventory.ActiveSlotIndex = originalActiveSlot;
				// Forzar actualización del modelo de mano
				if (m_componentCreature.ComponentCreatureModel != null)
				{
					m_componentCreature.ComponentCreatureModel.Opacity = m_componentCreature.ComponentCreatureModel.Opacity;
				}
			}

			try
			{
				// Colocar ambos bloques directamente (simultáneos)
				m_subsystemTerrain.ChangeCell(feetX, targetY1, feetZ, dirtValue, true);
				m_subsystemTerrain.ChangeCell(feetX, targetY2, feetZ, dirtValue, true);

				// Forzar actualización visual del chunk
				TerrainChunk chunk = m_subsystemTerrain.Terrain.GetChunkAtCell(feetX, feetZ);
				if (chunk != null)
				{
					chunk.ModificationCounter++;
					m_subsystemTerrain.TerrainUpdater.DowngradeChunkNeighborhoodState(chunk.Coords, 1, TerrainChunkState.InvalidLight, true);
				}

				// Sonido de colocación (propio del bloque)
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
			finally
			{
				// --- CONSUMIR LOS BLOQUES Y RESTAURAR OBJETO ORIGINAL ---
				if (inventory != null)
				{
					// Eliminar exactamente los 2 bloques de tierra que se usaron
					inventory.RemoveSlotItems(originalActiveSlot, 2);
					// Devolver el objeto que tenía originalmente (si tenía)
					if (hadItem)
					{
						inventory.AddSlotItems(originalActiveSlot, originalSlotValue, originalSlotCount);
					}
					// Forzar nuevamente actualización de mano
					m_componentMiner.Inventory.ActiveSlotIndex = originalActiveSlot;
					if (m_componentCreature.ComponentCreatureModel != null)
					{
						m_componentCreature.ComponentCreatureModel.Opacity = m_componentCreature.ComponentCreatureModel.Opacity;
					}
				}
			}
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
				m_isAimingRanged = false;
				m_isAimingThrowable = false;
				m_triedToLoad = false;
				m_aimingStarted = false;

				m_placedDirtBlocks.Clear();
			}, () =>
			{
				if (!IsActive)
				{
					m_stateMachine.TransitionTo("LookingForTarget");
					CancelRangedAim();
					CancelThrowableAim();
				}
				else if (m_chaseTime <= 0f)
				{
					m_autoChaseSuppressionTime = m_random.Float(10f, 60f);
					m_importanceLevel = 0f;
					CancelRangedAim();
					CancelThrowableAim();
				}
				else if (m_target == null)
				{
					m_importanceLevel = 0f;
					CancelRangedAim();
					CancelThrowableAim();
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
					CancelRangedAim();
					CancelThrowableAim();
				}
				else if (!m_isPersistent && m_componentPathfinding.IsStuck)
				{
					m_importanceLevel = 0f;
					CancelRangedAim();
					CancelThrowableAim();
				}
				else if (m_isPersistent && m_componentPathfinding.IsStuck)
				{
					m_stateMachine.TransitionTo("RandomMoving");
					CancelRangedAim();
					CancelThrowableAim();
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
						CancelRangedAim();
						CancelThrowableAim();
					}
					else
					{
						if (!m_isAimingThrowable)
						{
							int maxPathfindingPositions = 0;
							if (m_isPersistent)
							{
								maxPathfindingPositions = ((m_subsystemTime.FixedTimeStep != null) ? 2000 : 500);
							}
							BoundingBox bbSelf = m_componentCreature.ComponentBody.BoundingBox;
							BoundingBox bbTarget = m_target.ComponentBody.BoundingBox;
							Vector3 selfCenter = 0.5f * (bbSelf.Min + bbSelf.Max);
							Vector3 targetCenter = 0.5f * (bbTarget.Min + bbTarget.Max);
							float dist = Vector3.Distance(selfCenter, targetCenter);
							float followFactor = (dist < 4f) ? 0.2f : 0f;
							m_componentPathfinding.SetDestination(targetCenter + followFactor * dist * m_target.ComponentBody.Velocity,
																1f, 1.5f, maxPathfindingPositions, true, false, true, m_target.ComponentBody);
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
