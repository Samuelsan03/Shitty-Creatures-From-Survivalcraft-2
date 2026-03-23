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

		// Parámetros de ataque a distancia (armas)
		public Vector2 RangedAttackRange = new Vector2(5f, 20f);
		public AttackMode RangedAttackMode = AttackMode.Default;

		// Tiempos de apuntado (armas)
		public float MusketAimTime = 1.65f;
		public float MusketCooldown = 0.8f;
		public float BowAimTime = 1.2f;
		public float BowCooldown = 0.5f;
		public float CrossbowAimTime = 1.5f;
		public float CrossbowCooldown = 1.0f;
		public float RepeatCrossbowAimTime = 1.5f;
		public float RepeatCrossbowCooldown = 1.0f;
		public float FlameThrowerAimTime = 1.55f;
		public float FlameThrowerCooldown = 0.0f;

		// Nuevos parámetros para objetos lanzables
		public Vector2 ThrowableAttackRange = new Vector2(5f, 15f);
		public float ThrowableAimTime = 1.0f;
		public float ThrowableCooldown = 0.5f;

		// ===== CAMPOS PRIVADOS =====
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

		// Campos para ataque a distancia (armas)
		private double m_nextRangedAttackTime;
		private double m_rangedAimStartTime;
		private bool m_isAimingRanged;
		private bool m_triedToLoad;
		private bool m_aimingStarted;

		// Campos para disparo triple
		public float TripleShotProbability = 0.05f;

		// Campos para objetos lanzables
		private double m_nextThrowableAttackTime;
		private double m_throwableAimStartTime;
		private bool m_isThrowing;
		private int m_throwableSlotIndex = -1;
		private int m_throwableValue;

		// Suscripción a eventos de salud de jugadores
		private List<ComponentHealth> m_subscribedPlayerHealths = new List<ComponentHealth>();

		// Manejador para eliminar pickables de flechas/virotes
		private Action<Projectile> m_projectileAddedHandler;

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
			m_nextRangedAttackTime = 0;
			m_triedToLoad = false;
			m_aimingStarted = false;
			m_isThrowing = false;
			m_nextThrowableAttackTime = 0;

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
			m_nextRangedAttackTime = 0;
			m_triedToLoad = false;
			m_aimingStarted = false;
			m_isThrowing = false;
			m_nextThrowableAttackTime = 0;

			UnsubscribeFromProjectileEvents();
		}

		// ===== EVENTO PARA ELIMINAR PICKABLES DE FLECHAS / VIROTES =====
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

				bool inMeleeRange = IsTargetInAttackRange(m_target.ComponentBody);

				if (inMeleeRange)
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
						}
					}
					m_isAimingRanged = false;
					m_aimingStarted = false;
					m_isThrowing = false;
				}
				else
				{
					UpdateRangedAttack(dt);
				}
			}

			if (m_subsystemTime.GameTime >= m_nextUpdateTime)
			{
				m_dt = m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(m_subsystemTime.GameTime - m_nextUpdateTime), 0.1f);
				m_nextUpdateTime = m_subsystemTime.GameTime + (double)m_dt;
				m_stateMachine.Update();
			}
		}

		// ===== MÉTODOS DE DETECCIÓN DE ARMAS =====
		private bool HasMusket(out int slotIndex, out int value)
		{
			slotIndex = -1;
			value = 0;
			if (m_componentMiner?.Inventory == null) return false;

			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == MusketBlock.Index)
				{
					slotIndex = i;
					value = slotValue;
					return true;
				}
			}
			return false;
		}

		private bool HasBow(out int slotIndex, out int value)
		{
			slotIndex = -1;
			value = 0;
			if (m_componentMiner?.Inventory == null) return false;

			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == BowBlock.Index)
				{
					slotIndex = i;
					value = slotValue;
					return true;
				}
			}
			return false;
		}

		private bool HasCrossbow(out int slotIndex, out int value)
		{
			slotIndex = -1;
			value = 0;
			if (m_componentMiner?.Inventory == null) return false;

			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == CrossbowBlock.Index)
				{
					slotIndex = i;
					value = slotValue;
					return true;
				}
			}
			return false;
		}

		private bool HasRepeatCrossbow(out int slotIndex, out int value)
		{
			slotIndex = -1;
			value = 0;
			if (m_componentMiner?.Inventory == null) return false;

			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == RepeatCrossbowBlock.Index)
				{
					slotIndex = i;
					value = slotValue;
					return true;
				}
			}
			return false;
		}

		private bool HasFlameThrower(out int slotIndex, out int value)
		{
			slotIndex = -1;
			value = 0;
			if (m_componentMiner?.Inventory == null) return false;

			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == FlameThrowerBlock.Index)
				{
					slotIndex = i;
					value = slotValue;
					return true;
				}
			}
			return false;
		}

		private bool IsRepeatCrossbowLoaded()
		{
			if (!HasRepeatCrossbow(out int slotIndex, out int crossbowValue))
				return false;

			int data = Terrain.ExtractData(crossbowValue);
			int draw = RepeatCrossbowBlock.GetDraw(data);
			RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
			return draw == 15 && arrowType != null;
		}

		private bool IsFlameThrowerLoaded()
		{
			if (!HasFlameThrower(out int slotIndex, out int flameThrowerValue))
				return false;

			int data = Terrain.ExtractData(flameThrowerValue);
			FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
			FlameBulletBlock.FlameBulletType? bulletType = FlameThrowerBlock.GetBulletType(data);
			int loadCount = FlameThrowerBlock.GetLoadCount(flameThrowerValue);
			return loadState == FlameThrowerBlock.LoadState.Loaded && bulletType != null && loadCount > 0;
		}

		private void EnsureRepeatCrossbowActive()
		{
			if (m_componentMiner?.Inventory == null) return;

			if (HasRepeatCrossbow(out int slotIndex, out _))
			{
				if (m_componentMiner.Inventory.ActiveSlotIndex != slotIndex)
					m_componentMiner.Inventory.ActiveSlotIndex = slotIndex;
			}
		}

		private void EnsureFlameThrowerActive()
		{
			if (m_componentMiner?.Inventory == null) return;

			if (HasFlameThrower(out int slotIndex, out _))
			{
				if (m_componentMiner.Inventory.ActiveSlotIndex != slotIndex)
					m_componentMiner.Inventory.ActiveSlotIndex = slotIndex;
			}
		}

		private void StartRepeatCrossbowAim()
		{
			EnsureRepeatCrossbowActive();
			if (!HasRepeatCrossbow(out int slotIndex, out int crossbowValue))
				return;

			m_aimingStarted = true;
			m_isAimingRanged = true;
			m_rangedAimStartTime = m_subsystemTime.GameTime;
			m_triedToLoad = false;
		}

		private void StartFlameThrowerAim()
		{
			EnsureFlameThrowerActive();
			if (!HasFlameThrower(out int slotIndex, out int flameThrowerValue))
				return;

			EnsureFlameThrowerLoaded();
			if (!IsFlameThrowerLoaded())
				return;

			m_aimingStarted = true;
			m_isAimingRanged = true;
			m_rangedAimStartTime = m_subsystemTime.GameTime;
			m_triedToLoad = false;
		}

		private void CompleteRepeatCrossbowAim()
		{
			EnsureRepeatCrossbowActive();
			if (!HasRepeatCrossbow(out int slotIndex, out int crossbowValue))
			{
				m_isAimingRanged = false;
				m_aimingStarted = false;
				return;
			}

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
			Ray3 ray = new Ray3(eyePos, direction);

			m_componentMiner.Aim(ray, AimState.Completed);
			m_nextRangedAttackTime = m_subsystemTime.GameTime + RepeatCrossbowCooldown;
			m_isAimingRanged = false;
			m_aimingStarted = false;
			m_triedToLoad = false;
		}

		private bool HasAnyRangedWeapon(out int slotIndex, out int value, out bool isMusket, out bool isBow, out bool isCrossbow)
		{
			slotIndex = -1;
			value = 0;
			isMusket = false;
			isBow = false;
			isCrossbow = false;

			if (HasMusket(out slotIndex, out value))
			{
				isMusket = true;
				return true;
			}
			if (HasBow(out slotIndex, out value))
			{
				isBow = true;
				return true;
			}
			if (HasCrossbow(out slotIndex, out value))
			{
				isCrossbow = true;
				return true;
			}
			return false;
		}

		// ===== MÉTODOS DE CARGA Y DISPARO (ARMAS) =====
		private void EnsureMusketActive()
		{
			if (m_componentMiner?.Inventory == null) return;

			if (HasMusket(out int slotIndex, out _))
			{
				if (m_componentMiner.Inventory.ActiveSlotIndex != slotIndex)
				{
					m_componentMiner.Inventory.ActiveSlotIndex = slotIndex;
				}
			}
		}

		private void EnsureBowActive()
		{
			if (m_componentMiner?.Inventory == null) return;

			if (HasBow(out int slotIndex, out _))
			{
				if (m_componentMiner.Inventory.ActiveSlotIndex != slotIndex)
				{
					m_componentMiner.Inventory.ActiveSlotIndex = slotIndex;
				}
			}
		}

		private void EnsureCrossbowActive()
		{
			if (m_componentMiner?.Inventory == null) return;

			if (HasCrossbow(out int slotIndex, out _))
			{
				if (m_componentMiner.Inventory.ActiveSlotIndex != slotIndex)
				{
					m_componentMiner.Inventory.ActiveSlotIndex = slotIndex;
				}
			}
		}

		private void EnsureMusketLoaded(int slotIndex, int currentValue)
		{
			int data = Terrain.ExtractData(currentValue);
			MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
			if (loadState != MusketBlock.LoadState.Loaded)
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
				ArrowBlock.ArrowType[] boltTypes = new ArrowBlock.ArrowType[]
				{
					ArrowBlock.ArrowType.IronBolt,
					ArrowBlock.ArrowType.DiamondBolt,
					ArrowBlock.ArrowType.ExplosiveBolt
				};
				ArrowBlock.ArrowType selected = boltTypes[m_random.Int(0, boltTypes.Length - 1)];
				data = CrossbowBlock.SetArrowType(data, selected);
				data = CrossbowBlock.SetDraw(data, 15);
				int newValue = Terrain.MakeBlockValue(CrossbowBlock.Index, 0, data);
				m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				m_componentMiner.Inventory.AddSlotItems(slotIndex, newValue, 1);
			}
		}

		private void EnsureFlameThrowerLoaded()
		{
			if (m_componentMiner?.Inventory == null) return;

			if (!HasFlameThrower(out int slotIndex, out int flameThrowerValue))
				return;

			int data = Terrain.ExtractData(flameThrowerValue);
			FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
			FlameBulletBlock.FlameBulletType? bulletType = FlameThrowerBlock.GetBulletType(data);
			int loadCount = FlameThrowerBlock.GetLoadCount(flameThrowerValue);

			if (loadState == FlameThrowerBlock.LoadState.Loaded && bulletType != null && loadCount > 0)
				return;

			FlameBulletBlock.FlameBulletType selectedBullet = m_random.Bool(0.5f)
				? FlameBulletBlock.FlameBulletType.Flame
				: FlameBulletBlock.FlameBulletType.Poison;

			int newData = data;
			newData = FlameThrowerBlock.SetLoadState(newData, FlameThrowerBlock.LoadState.Loaded);
			newData = FlameThrowerBlock.SetBulletType(newData, new FlameBulletBlock.FlameBulletType?(selectedBullet));
			newData = FlameThrowerBlock.SetSwitchState(newData, true);
			int newValue = Terrain.MakeBlockValue(FlameThrowerBlock.Index, 1, newData);
			newValue = FlameThrowerBlock.SetLoadCount(newValue, 15);

			m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
			m_componentMiner.Inventory.AddSlotItems(slotIndex, newValue, 1);
		}

		private bool IsMusketLoaded()
		{
			if (!HasMusket(out int slotIndex, out int musketValue))
				return false;

			int data = Terrain.ExtractData(musketValue);
			return MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded;
		}

		private bool CanFireMusket()
		{
			if (!HasMusket(out int slotIndex, out int musketValue))
				return false;

			int data = Terrain.ExtractData(musketValue);
			MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
			bool isHammerCocked = MusketBlock.GetHammerState(data);

			return loadState == MusketBlock.LoadState.Loaded && isHammerCocked;
		}

		private bool IsBowLoaded()
		{
			if (!HasBow(out int slotIndex, out int bowValue))
				return false;

			int data = Terrain.ExtractData(bowValue);
			return BowBlock.GetArrowType(data) != null;
		}

		private bool IsCrossbowLoaded()
		{
			if (!HasCrossbow(out int slotIndex, out int crossbowValue))
				return false;

			int data = Terrain.ExtractData(crossbowValue);
			return CrossbowBlock.GetDraw(data) == 15 && CrossbowBlock.GetArrowType(data) != null;
		}

		private void StartMusketAim()
		{
			EnsureMusketActive();
			if (!HasMusket(out int slotIndex, out int musketValue))
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
			if (!HasBow(out int slotIndex, out int bowValue))
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
			if (!HasCrossbow(out int slotIndex, out int crossbowValue))
				return;

			EnsureCrossbowLoaded(slotIndex, crossbowValue);
			m_aimingStarted = true;
			m_isAimingRanged = true;
			m_rangedAimStartTime = m_subsystemTime.GameTime;
			m_triedToLoad = false;
		}

		private void CompleteMusketAim()
		{
			EnsureMusketActive();
			if (!HasMusket(out int slotIndex, out int musketValue))
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
			if (!HasBow(out int slotIndex, out int bowValue))
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
			if (!HasCrossbow(out int slotIndex, out int crossbowValue))
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

		private void CancelRangedAim()
		{
			if (m_isAimingRanged)
			{
				if (HasMusket(out int musketSlot, out int musketValue))
				{
					EnsureMusketActive();
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
					Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
					Ray3 ray = new Ray3(eyePos, direction);
					m_componentMiner.Aim(ray, AimState.Cancelled);
				}
				else if (HasBow(out int bowSlot, out int bowValue))
				{
					EnsureBowActive();
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
					Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
					Ray3 ray = new Ray3(eyePos, direction);
					m_componentMiner.Aim(ray, AimState.Cancelled);
				}
				else if (HasCrossbow(out int crossbowSlot, out int crossbowValue))
				{
					EnsureCrossbowActive();
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
					Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
					Ray3 ray = new Ray3(eyePos, direction);
					m_componentMiner.Aim(ray, AimState.Cancelled);
				}
				else if (HasRepeatCrossbow(out int repeatCrossbowSlot, out int repeatCrossbowValue))
				{
					EnsureRepeatCrossbowActive();
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
					Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
					Ray3 ray = new Ray3(eyePos, direction);
					m_componentMiner.Aim(ray, AimState.Cancelled);
				}
				else if (HasFlameThrower(out int flameThrowerSlot, out int flameThrowerValue))
				{
					EnsureFlameThrowerActive();
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
					Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
					Ray3 ray = new Ray3(eyePos, direction);
					m_componentMiner.Aim(ray, AimState.Cancelled);
				}
			}
			m_isAimingRanged = false;
			m_aimingStarted = false;
			m_triedToLoad = false;
			m_isThrowing = false;
		}

		// ===== NUEVOS MÉTODOS PARA OBJETOS LANZABLES =====
		private bool IsThrowableBlock(int value)
		{
			if (value == 0) return false;
			int contents = Terrain.ExtractContents(value);
			Type blockType = BlocksManager.Blocks[contents].GetType();

			// Lista de tipos de bloques lanzables
			if (blockType == typeof(StoneChunkBlock)) return true;
			if (blockType == typeof(SulphurChunkBlock)) return true;
			if (blockType == typeof(CoalChunkBlock)) return true;
			if (blockType == typeof(DiamondChunkBlock)) return true;
			if (blockType == typeof(GermaniumChunkBlock)) return true;
			if (blockType == typeof(GermaniumOreChunkBlock)) return true;
			if (blockType == typeof(IronOreChunkBlock)) return true;
			if (blockType == typeof(MalachiteChunkBlock)) return true;
			if (blockType == typeof(SaltpeterChunkBlock)) return true;
			if (blockType == typeof(GunpowderBlock)) return true;
			if (blockType == typeof(BombBlock)) return true;
			if (blockType == typeof(IncendiaryBombBlock)) return true;
			if (blockType == typeof(PoisonBombBlock)) return true;
			if (blockType == typeof(BrickBlock)) return true;
			if (blockType == typeof(SnowballBlock)) return true;
			if (blockType == typeof(EggBlock)) return true;
			if (blockType == typeof(CopperSpearBlock)) return true;
			if (blockType == typeof(DiamondSpearBlock)) return true;
			if (blockType == typeof(IronSpearBlock)) return true;
			if (blockType == typeof(WoodenSpearBlock)) return true;
			if (blockType == typeof(WoodenLongspearBlock)) return true;
			if (blockType == typeof(StoneSpearBlock)) return true;
			if (blockType == typeof(StoneLongspearBlock)) return true;
			if (blockType == typeof(IronLongspearBlock)) return true;
			if (blockType == typeof(LavaLongspearBlock)) return true;
			if (blockType == typeof(LavaSpearBlock)) return true;
			if (blockType == typeof(DiamondLongspearBlock)) return true;
			if (blockType == typeof(FreezingSnowballBlock)) return true;
			if (blockType == typeof(FreezeBombBlock)) return true;
			if (blockType == typeof(FireworksBlock)) return true;

			return false;
		}

		private bool HasThrowableItem(out int slotIndex, out int value)
		{
			slotIndex = -1;
			value = 0;
			if (m_componentMiner?.Inventory == null) return false;

			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				if (slotValue != 0 && m_componentMiner.Inventory.GetSlotCount(i) > 0 && IsThrowableBlock(slotValue))
				{
					slotIndex = i;
					value = slotValue;
					return true;
				}
			}
			return false;
		}

		private void EnsureThrowableActive()
		{
			if (m_componentMiner?.Inventory == null) return;

			if (m_throwableSlotIndex != -1 && m_componentMiner.Inventory.ActiveSlotIndex != m_throwableSlotIndex)
			{
				m_componentMiner.Inventory.ActiveSlotIndex = m_throwableSlotIndex;
			}
		}

		private void StartThrowableAim()
		{
			if (!HasThrowableItem(out int slotIndex, out int value))
			{
				m_isThrowing = false;
				return;
			}

			m_throwableSlotIndex = slotIndex;
			m_throwableValue = value;
			EnsureThrowableActive();

			m_isThrowing = true;
			m_throwableAimStartTime = m_subsystemTime.GameTime;

			// Detener movimiento mientras apunta
			m_componentPathfinding.Stop();

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
			Ray3 ray = new Ray3(eyePos, direction);
			m_componentMiner.Aim(ray, AimState.InProgress);
		}

		private void CompleteThrowableAim()
		{
			if (!m_isThrowing) return;

			EnsureThrowableActive();
			if (!HasThrowableItem(out int slotIndex, out int value))
			{
				m_isThrowing = false;
				return;
			}

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
			Ray3 ray = new Ray3(eyePos, direction);

			m_componentMiner.Aim(ray, AimState.Completed);
			m_nextThrowableAttackTime = m_subsystemTime.GameTime + ThrowableCooldown;
			m_isThrowing = false;

			// Reanudar movimiento después de lanzar
			// El estado Chasing reanudará la navegación en la próxima actualización
		}

		private void CancelThrowableAim()
		{
			if (m_isThrowing)
			{
				EnsureThrowableActive();
				if (HasThrowableItem(out int slotIndex, out int value))
				{
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
					Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
					Ray3 ray = new Ray3(eyePos, direction);
					m_componentMiner.Aim(ray, AimState.Cancelled);
				}
			}
			m_isThrowing = false;
		}

		// ===== ACTUALIZACIÓN DEL ATAQUE A DISTANCIA (MODIFICADA PARA INCLUIR LANZABLES) =====
		private void UpdateRangedAttack(float dt)
		{
			if (m_target == null)
			{
				CancelRangedAim();
				CancelThrowableAim();
				return;
			}

			float distance = GetDistanceToTarget();
			bool inThrowableRange = false;
			bool inRangedWeaponRange = false;

			// Rango para lanzables
			if (distance >= ThrowableAttackRange.X && distance <= ThrowableAttackRange.Y)
				inThrowableRange = true;

			// Rango para armas a distancia (bows, muskets, etc.)
			if (RangedAttackMode == AttackMode.Remote)
			{
				inRangedWeaponRange = distance <= RangedAttackRange.Y;
			}
			else if (RangedAttackMode == AttackMode.Default)
			{
				inRangedWeaponRange = distance >= RangedAttackRange.X && distance <= RangedAttackRange.Y && distance > MaxAttackRange;
			}

			// PRIORIDAD: 1. Lanzables, 2. Armas a distancia, 3. Cuerpo a cuerpo (manejado fuera de este método)
			bool hasThrowable = HasThrowableItem(out int throwableSlot, out int throwableValue);

			if (hasThrowable && inThrowableRange)
			{
				// Manejar lanzables
				if (!m_isThrowing)
				{
					if (m_subsystemTime.GameTime < m_nextThrowableAttackTime)
						return;

					StartThrowableAim();
				}
				else
				{
					// Continuar apuntando
					EnsureThrowableActive();
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
					Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
					Ray3 ray = new Ray3(eyePos, direction);
					m_componentMiner.Aim(ray, AimState.InProgress);

					double aimTimeElapsed = m_subsystemTime.GameTime - m_throwableAimStartTime;
					if (aimTimeElapsed >= ThrowableAimTime)
					{
						CompleteThrowableAim();
					}
					else if (!HasThrowableItem(out _, out _))
					{
						CancelThrowableAim();
					}
				}
				return; // Prioridad lanzables, no se procesan otras armas
			}
			else if (inRangedWeaponRange)
			{
				// Cancelar lanzable si está activo pero ya no cumple condiciones
				CancelThrowableAim();

				// Resto del código original para armas a distancia (sin cambios)
				bool hasMusket = HasMusket(out int musketSlot, out int musketValue);
				bool hasBow = HasBow(out int bowSlot, out int bowValue);
				bool hasCrossbow = HasCrossbow(out int crossbowSlot, out int crossbowValue);
				bool hasRepeatCrossbow = HasRepeatCrossbow(out int repeatCrossbowSlot, out int repeatCrossbowValue);
				bool hasFlameThrower = HasFlameThrower(out int flameThrowerSlot, out int flameThrowerValue);

				bool useMusket = hasMusket;
				bool useBow = !useMusket && hasBow;
				bool useCrossbow = !useMusket && !useBow && hasCrossbow;
				bool useRepeatCrossbow = !useMusket && !useBow && !useCrossbow && hasRepeatCrossbow;
				bool useFlameThrower = !useMusket && !useBow && !useCrossbow && !useRepeatCrossbow && hasFlameThrower;

				if (!useMusket && !useBow && !useCrossbow && !useRepeatCrossbow && !useFlameThrower)
				{
					CancelRangedAim();
					return;
				}

				if (useMusket)
				{
					// Código original para mosquete...
					if (!m_isAimingRanged)
					{
						if (m_subsystemTime.GameTime < m_nextRangedAttackTime)
							return;

						if (!IsMusketLoaded())
						{
							if (!m_triedToLoad)
							{
								EnsureMusketLoaded(musketSlot, musketValue);
								m_triedToLoad = true;
							}
							return;
						}

						StartMusketAim();
						Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
						Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
						Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
						Ray3 ray = new Ray3(eyePos, direction);
						m_componentMiner.Aim(ray, AimState.InProgress);
					}
					else
					{
						Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
						Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
						Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
						Ray3 ray = new Ray3(eyePos, direction);
						m_componentMiner.Aim(ray, AimState.InProgress);

						double aimTimeElapsed = m_subsystemTime.GameTime - m_rangedAimStartTime;
						if (aimTimeElapsed >= MusketAimTime)
						{
							CompleteMusketAim();
						}
						else if (!IsMusketLoaded())
						{
							CancelRangedAim();
						}
					}
				}
				else if (useFlameThrower)
				{
					// Código original para lanzallamas...
					if (!m_isAimingRanged)
					{
						if (!IsFlameThrowerLoaded())
						{
							EnsureFlameThrowerLoaded();
							return;
						}

						StartFlameThrowerAim();
						Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
						Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
						Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
						Ray3 ray = new Ray3(eyePos, direction);
						m_componentMiner.Aim(ray, AimState.InProgress);
					}
					else
					{
						Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
						Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
						Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
						Ray3 ray = new Ray3(eyePos, direction);
						m_componentMiner.Aim(ray, AimState.InProgress);

						if (!IsFlameThrowerLoaded())
						{
							EnsureFlameThrowerLoaded();
							if (!IsFlameThrowerLoaded())
							{
								CancelRangedAim();
							}
						}
					}
				}
				else if (useBow)
				{
					// Código original para arco...
					if (!m_isAimingRanged)
					{
						if (m_subsystemTime.GameTime < m_nextRangedAttackTime)
							return;

						if (!IsBowLoaded())
						{
							if (!m_triedToLoad)
							{
								EnsureBowLoaded(bowSlot, bowValue);
								m_triedToLoad = true;
							}
							return;
						}

						StartBowAim();
						Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
						Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
						Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
						Ray3 ray = new Ray3(eyePos, direction);
						m_componentMiner.Aim(ray, AimState.InProgress);
					}
					else
					{
						Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
						Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
						Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
						Ray3 ray = new Ray3(eyePos, direction);
						m_componentMiner.Aim(ray, AimState.InProgress);

						double aimTimeElapsed = m_subsystemTime.GameTime - m_rangedAimStartTime;
						if (aimTimeElapsed >= BowAimTime)
						{
							CompleteBowAim();
						}
						else if (!IsBowLoaded())
						{
							CancelRangedAim();
						}
					}
				}
				else if (useCrossbow)
				{
					// Código original para ballesta...
					if (!m_isAimingRanged)
					{
						if (m_subsystemTime.GameTime < m_nextRangedAttackTime)
							return;

						if (!IsCrossbowLoaded())
						{
							if (!m_triedToLoad)
							{
								EnsureCrossbowLoaded(crossbowSlot, crossbowValue);
								m_triedToLoad = true;
							}
							return;
						}

						StartCrossbowAim();
						Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
						Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
						Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
						Ray3 ray = new Ray3(eyePos, direction);
						m_componentMiner.Aim(ray, AimState.InProgress);
					}
					else
					{
						Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
						Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
						Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
						Ray3 ray = new Ray3(eyePos, direction);
						m_componentMiner.Aim(ray, AimState.InProgress);

						double aimTimeElapsed = m_subsystemTime.GameTime - m_rangedAimStartTime;
						if (aimTimeElapsed >= CrossbowAimTime)
						{
							CompleteCrossbowAim();
						}
						else if (!IsCrossbowLoaded())
						{
							CancelRangedAim();
						}
					}
				}
				else if (useRepeatCrossbow)
				{
					// Código original para ballesta repetidora...
					if (!m_isAimingRanged)
					{
						if (m_subsystemTime.GameTime < m_nextRangedAttackTime)
							return;

						StartRepeatCrossbowAim();
						Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
						Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
						Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
						Ray3 ray = new Ray3(eyePos, direction);
						m_componentMiner.Aim(ray, AimState.InProgress);
					}
					else
					{
						Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
						Vector3 targetCenter = m_target.ComponentCreatureModel.EyePosition;
						Vector3 direction = Vector3.Normalize(targetCenter - eyePos);
						Ray3 ray = new Ray3(eyePos, direction);
						m_componentMiner.Aim(ray, AimState.InProgress);

						double aimTimeElapsed = m_subsystemTime.GameTime - m_rangedAimStartTime;
						if (aimTimeElapsed >= RepeatCrossbowAimTime)
						{
							CompleteRepeatCrossbowAim();
						}
					}
				}
			}
			else
			{
				CancelRangedAim();
				CancelThrowableAim();
			}
		}

		private float GetDistanceToTarget()
		{
			if (m_target == null) return float.MaxValue;
			Vector3 selfPos = m_componentCreature.ComponentBody.Position;
			Vector3 targetPos = m_target.ComponentBody.Position;
			return Vector3.Distance(selfPos, targetPos);
		}

		// ===== MÉTODOS DE APOYO ORIGINALES =====
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
				m_isAimingRanged = false;
				m_triedToLoad = false;
				m_aimingStarted = false;
				m_isThrowing = false;
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
						// Si no está apuntando un lanzable, actualizar destino
						if (!m_isThrowing)
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

	public enum AttackMode
	{
		Default,
		Remote,
		OnlyHand
	}
}
