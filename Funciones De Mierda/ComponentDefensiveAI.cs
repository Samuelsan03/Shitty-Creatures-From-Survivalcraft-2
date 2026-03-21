using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentDefensiveAI : ComponentBehavior, IUpdateable
	{
		public override float ImportanceLevel => 0f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public bool CanUseInventory = true;

		private float m_meleeRange = 5f;
		private float m_throwableRange = 15f;
		private float m_rangedRange = 100f;

		private const double BOW_COOLDOWN = 1.5;
		private const double CROSSBOW_COOLDOWN = 1.5;
		private const double MUSKET_COOLDOWN = 0.8;
		private const double REPEAT_CROSSBOW_COOLDOWN = 1.2;
		private const double FLAMETHROWER_COOLDOWN = 0;
		private const double THROWABLE_COOLDOWN = 0.5;

		private const double CROSSBOW_MIN_AIM_TIME = 0.3;
		private const double MUSKET_MIN_AIM_TIME = 1.5;
		private const double REPEAT_CROSSBOW_MIN_AIM_TIME = 0.5;
		private const double THROWABLE_MIN_AIM_TIME = 1.5;

		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentInventory m_componentInventory;
		private ComponentNewChaseBehavior m_componentChase;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentPathfinding m_componentPathfinding;

		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemTerrain m_subsystemTerrain;

		private bool m_isAiming = false;
		private double m_aimStartTime;
		private Ray3 m_aimRay;
		private int m_currentWeaponType;
		private Vector3? m_originalDestination;
		private float m_originalSpeed;
		private float m_originalRange;
		private int m_originalMaxPathfindingPositions;
		private bool m_originalUseRandomMovements;
		private bool m_originalIgnoreHeightDifference;
		private bool m_originalRaycastDestination;
		private ComponentBody m_originalDoNotAvoidBody;

		private double m_lastBowShotTime = -1000;
		private double m_lastCrossbowShotTime = -1000;
		private double m_lastMusketShotTime = -1000;
		private double m_lastRepeatCrossbowShotTime = -1000;
		private double m_lastFlameThrowerShotTime = -1000;
		private double m_lastThrowableShotTime = -1000;

		private int m_bowBlockIndex;
		private int m_crossbowBlockIndex;
		private int m_musketBlockIndex;
		private int m_repeatCrossbowBlockIndex;
		private int m_flameThrowerBlockIndex;
		private int m_arrowBlockIndex;
		private int m_bulletBlockIndex;
		private int m_repeatArrowBlockIndex;
		private int m_flameBulletBlockIndex;

		private HashSet<int> m_throwableBlockIndices;

		private Random m_random = new Random();

		private ArrowBlock.ArrowType[] m_allArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.CopperArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.FireArrow
		};

		private ArrowBlock.ArrowType[] m_crossbowBolts = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
		};

		private BulletBlock.BulletType[] m_bulletTypes = new BulletBlock.BulletType[]
		{
			BulletBlock.BulletType.MusketBall,
			BulletBlock.BulletType.Buckshot,
			BulletBlock.BulletType.BuckshotBall
		};

		private RepeatArrowBlock.ArrowType[] m_repeatArrowTypes = new RepeatArrowBlock.ArrowType[]
		{
			RepeatArrowBlock.ArrowType.CopperArrow,
			RepeatArrowBlock.ArrowType.IronArrow,
			RepeatArrowBlock.ArrowType.DiamondArrow,
			RepeatArrowBlock.ArrowType.ExplosiveArrow,
			RepeatArrowBlock.ArrowType.PoisonArrow,
			RepeatArrowBlock.ArrowType.SeriousPoisonArrow
		};

		private FlameBulletBlock.FlameBulletType[] m_flameBulletTypes = new FlameBulletBlock.FlameBulletType[]
		{
			FlameBulletBlock.FlameBulletType.Flame,
			FlameBulletBlock.FlameBulletType.Poison
		};

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentInventory = Entity.FindComponent<ComponentInventory>();
			m_componentChase = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>();

			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);

			m_bowBlockIndex = BlocksManager.GetBlockIndex<BowBlock>(false);
			m_crossbowBlockIndex = BlocksManager.GetBlockIndex<CrossbowBlock>(false);
			m_musketBlockIndex = BlocksManager.GetBlockIndex<MusketBlock>(false);
			m_repeatCrossbowBlockIndex = BlocksManager.GetBlockIndex<RepeatCrossbowBlock>(false);
			m_flameThrowerBlockIndex = BlocksManager.GetBlockIndex<FlameThrowerBlock>(false);
			m_arrowBlockIndex = BlocksManager.GetBlockIndex<ArrowBlock>(false);
			m_bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false);
			m_repeatArrowBlockIndex = BlocksManager.GetBlockIndex<RepeatArrowBlock>(false);
			m_flameBulletBlockIndex = BlocksManager.GetBlockIndex<FlameBulletBlock>(false);

			m_throwableBlockIndices = new HashSet<int>
			{
				BlocksManager.GetBlockIndex<StoneChunkBlock>(),
				BlocksManager.GetBlockIndex<SulphurChunkBlock>(),
				BlocksManager.GetBlockIndex<CoalChunkBlock>(),
				BlocksManager.GetBlockIndex<DiamondChunkBlock>(),
				BlocksManager.GetBlockIndex<GermaniumChunkBlock>(),
				BlocksManager.GetBlockIndex<GermaniumOreChunkBlock>(),
				BlocksManager.GetBlockIndex<IronOreChunkBlock>(),
				BlocksManager.GetBlockIndex<MalachiteChunkBlock>(),
				BlocksManager.GetBlockIndex<SaltpeterChunkBlock>(),
				BlocksManager.GetBlockIndex<GunpowderBlock>(),
				BlocksManager.GetBlockIndex<BombBlock>(),
				BlocksManager.GetBlockIndex<IncendiaryBombBlock>(),
				BlocksManager.GetBlockIndex<PoisonBombBlock>(),
				BlocksManager.GetBlockIndex<BrickBlock>(),
				BlocksManager.GetBlockIndex<SnowballBlock>(),
				BlocksManager.GetBlockIndex<EggBlock>(),
				BlocksManager.GetBlockIndex<CopperSpearBlock>(),
				BlocksManager.GetBlockIndex<DiamondSpearBlock>(),
				BlocksManager.GetBlockIndex<IronSpearBlock>(),
				BlocksManager.GetBlockIndex<WoodenSpearBlock>(),
				BlocksManager.GetBlockIndex<WoodenLongspearBlock>(),
				BlocksManager.GetBlockIndex<StoneSpearBlock>(),
				BlocksManager.GetBlockIndex<StoneLongspearBlock>(),
				BlocksManager.GetBlockIndex<IronLongspearBlock>(),
				BlocksManager.GetBlockIndex<LavaLongspearBlock>(),
				BlocksManager.GetBlockIndex<LavaSpearBlock>(),
				BlocksManager.GetBlockIndex<DiamondLongspearBlock>(),
				BlocksManager.GetBlockIndex<FreezingSnowballBlock>(),
				BlocksManager.GetBlockIndex<FreezeBombBlock>(),
				BlocksManager.GetBlockIndex<FireworksBlock>()
			};

			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", CanUseInventory);

			if (m_subsystemProjectiles != null)
				m_subsystemProjectiles.ProjectileAdded += OnProjectileAdded;
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue("CanUseInventory", CanUseInventory);
		}

		public override void Dispose()
		{
			if (m_subsystemProjectiles != null)
				m_subsystemProjectiles.ProjectileAdded -= OnProjectileAdded;
			base.Dispose();
		}

		public void Update(float dt)
		{
			if (!CanUseInventory || m_componentMiner == null || m_componentInventory == null)
				return;

			if (m_componentChase == null || m_componentChase.Target == null)
			{
				CancelAiming();
				return;
			}

			ComponentCreature target = m_componentChase.Target;
			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);

			bool hasThrowable = HasThrowableWeapon();
			bool useMelee = distance <= m_meleeRange;
			bool useThrowable = !useMelee && distance <= m_throwableRange && hasThrowable;
			bool useRanged = !useThrowable && distance > m_meleeRange && distance <= m_rangedRange;

			if (useMelee)
			{
				CancelAiming();
				EquipMeleeOrThrowableWeapon();  // prioriza el arma lanzable para melee
			}
			else if (useThrowable)
			{
				if (!EquipThrowableWeapon())
				{
					CancelAiming();
					return;
				}
				if (HasLineOfSight(target) && IsTargetInFront(target))
				{
					StopMovement();
					UpdateAiming(target);
				}
				else
				{
					CancelAiming();
				}
			}
			else if (useRanged)
			{
				if (!EquipAndLoadRangedWeapon())
				{
					CancelAiming();
					return;
				}
				UpdateAiming(target);
			}
			else
			{
				CancelAiming();
			}
		}

		private void EquipMeleeOrThrowableWeapon()
		{
			// Prioridad: arma lanzable como melee
			if (HasThrowableWeapon())
			{
				EquipThrowableWeapon();
				return;
			}

			// Si no hay lanzable, equipar un arma melee normal
			int activeSlot = m_componentInventory.ActiveSlotIndex;
			int activeValue = m_componentInventory.GetSlotValue(activeSlot);
			if (IsMeleeWeapon(activeValue)) return;

			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int value = m_componentInventory.GetSlotValue(i);
				if (IsMeleeWeapon(value))
				{
					m_componentInventory.ActiveSlotIndex = i;
					break;
				}
			}
		}

		/// <summary>
		/// Verifica si el objetivo está frente al NPC (dentro de un ángulo de 60 grados)
		/// </summary>
		private bool IsTargetInFront(ComponentCreature target)
		{
			if (target == null || m_componentCreature?.ComponentBody == null)
				return false;

			Vector3 toTarget = Vector3.Normalize(target.ComponentBody.Position - m_componentCreature.ComponentBody.Position);
			Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
			float dot = Vector3.Dot(forward, toTarget);
			// Umbral de 0.5 corresponde a ~60°, ajustable según necesidad
			return dot > 0.5f;
		}

		private bool HasThrowableWeapon()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int value = m_componentInventory.GetSlotValue(i);
				if (IsThrowableWeapon(value))
					return true;
			}
			return false;
		}

		private bool EquipThrowableWeapon()
		{
			int activeSlot = m_componentInventory.ActiveSlotIndex;
			int activeValue = m_componentInventory.GetSlotValue(activeSlot);
			if (IsThrowableWeapon(activeValue))
			{
				m_currentWeaponType = Terrain.ExtractContents(activeValue);
				return true;
			}

			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int value = m_componentInventory.GetSlotValue(i);
				if (IsThrowableWeapon(value))
				{
					m_componentInventory.ActiveSlotIndex = i;
					m_currentWeaponType = Terrain.ExtractContents(value);
					return true;
				}
			}
			return false;
		}

		private void EquipMeleeWeapon()
		{
			int activeSlot = m_componentInventory.ActiveSlotIndex;
			int activeValue = m_componentInventory.GetSlotValue(activeSlot);
			if (IsMeleeWeapon(activeValue)) return;

			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int value = m_componentInventory.GetSlotValue(i);
				if (IsMeleeWeapon(value))
				{
					m_componentInventory.ActiveSlotIndex = i;
					break;
				}
			}
		}

		private bool EquipAndLoadRangedWeapon()
		{
			int activeSlot = m_componentInventory.ActiveSlotIndex;
			int activeValue = m_componentInventory.GetSlotValue(activeSlot);

			if (IsRangedWeapon(activeValue))
			{
				if (IsWeaponLoaded(activeValue))
				{
					m_currentWeaponType = Terrain.ExtractContents(activeValue);
					TryFullyLoadWeapon(activeSlot);
					return true;
				}
				if (LoadWeapon(activeSlot))
				{
					TryFullyLoadWeapon(activeSlot);
					m_currentWeaponType = Terrain.ExtractContents(m_componentInventory.GetSlotValue(activeSlot));
					return true;
				}
			}

			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int value = m_componentInventory.GetSlotValue(i);
				if (IsRangedWeapon(value) && LoadWeapon(i))
				{
					TryFullyLoadWeapon(i);
					m_componentInventory.ActiveSlotIndex = i;
					m_currentWeaponType = Terrain.ExtractContents(m_componentInventory.GetSlotValue(i));
					return true;
				}
			}
			return false;
		}

		private void TryFullyLoadWeapon(int slot)
		{
			int maxAttempts = 20;
			for (int i = 0; i < maxAttempts; i++)
			{
				int weaponValue = m_componentInventory.GetSlotValue(slot);
				if (!IsRangedWeapon(weaponValue)) break;
				if (IsWeaponFullyLoaded(weaponValue)) break;
				if (!LoadWeapon(slot)) break;
			}
		}

		private bool IsWeaponFullyLoaded(int weaponValue)
		{
			int contents = Terrain.ExtractContents(weaponValue);
			int data = Terrain.ExtractData(weaponValue);

			if (contents == m_repeatCrossbowBlockIndex)
			{
				int loadCount = RepeatCrossbowBlock.GetLoadCount(weaponValue);
				return loadCount >= 1;
			}
			if (contents == m_flameThrowerBlockIndex)
			{
				int loadCount = FlameThrowerBlock.GetLoadCount(weaponValue);
				return loadCount >= 15;
			}
			return IsWeaponLoaded(weaponValue);
		}

		private bool LoadWeapon(int slot)
		{
			int weaponValue = m_componentInventory.GetSlotValue(slot);
			int contents = Terrain.ExtractContents(weaponValue);
			int data = Terrain.ExtractData(weaponValue);

			if (contents == m_bowBlockIndex)
			{
				if (BowBlock.GetArrowType(data) != null)
					return true;

				ArrowBlock.ArrowType arrowType = m_allArrowTypes[m_random.Int(m_allArrowTypes.Length)];
				int newData = BowBlock.SetArrowType(data, arrowType);
				int newValue = Terrain.MakeBlockValue(m_bowBlockIndex, 0, newData);
				m_componentInventory.RemoveSlotItems(slot, 1);
				m_componentInventory.AddSlotItems(slot, newValue, 1);
				return true;
			}
			else if (contents == m_crossbowBlockIndex)
			{
				int draw = CrossbowBlock.GetDraw(data);
				ArrowBlock.ArrowType? arrowType = CrossbowBlock.GetArrowType(data);
				if (draw == 15 && arrowType != null)
					return true;

				ArrowBlock.ArrowType boltType = m_crossbowBolts[m_random.Int(m_crossbowBolts.Length)];
				int newData = CrossbowBlock.SetArrowType(data, boltType);
				newData = CrossbowBlock.SetDraw(newData, 15);
				int newValue = Terrain.MakeBlockValue(m_crossbowBlockIndex, 0, newData);
				m_componentInventory.RemoveSlotItems(slot, 1);
				m_componentInventory.AddSlotItems(slot, newValue, 1);
				return true;
			}
			else if (contents == m_musketBlockIndex)
			{
				MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
				if (loadState == MusketBlock.LoadState.Loaded)
					return true;

				BulletBlock.BulletType bulletType = m_bulletTypes[m_random.Int(m_bulletTypes.Length)];
				int newData = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
				newData = MusketBlock.SetBulletType(newData, bulletType);
				int newValue = Terrain.MakeBlockValue(m_musketBlockIndex, 0, newData);
				m_componentInventory.RemoveSlotItems(slot, 1);
				m_componentInventory.AddSlotItems(slot, newValue, 1);
				return true;
			}
			else if (contents == m_repeatCrossbowBlockIndex)
			{
				int draw = RepeatCrossbowBlock.GetDraw(data);
				RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
				int loadCount = RepeatCrossbowBlock.GetLoadCount(weaponValue);

				if (draw < 15)
				{
					int newData = RepeatCrossbowBlock.SetDraw(data, 15);
					int newValue = Terrain.MakeBlockValue(m_repeatCrossbowBlockIndex, loadCount, newData);
					m_componentInventory.RemoveSlotItems(slot, 1);
					m_componentInventory.AddSlotItems(slot, newValue, 1);
					return true;
				}

				if (arrowType == null)
				{
					RepeatArrowBlock.ArrowType newArrowType = m_repeatArrowTypes[m_random.Int(m_repeatArrowTypes.Length)];
					int newData = RepeatCrossbowBlock.SetArrowType(data, newArrowType);
					int newValue = Terrain.MakeBlockValue(m_repeatCrossbowBlockIndex, 1, newData);
					m_componentInventory.RemoveSlotItems(slot, 1);
					m_componentInventory.AddSlotItems(slot, newValue, 1);
					return true;
				}

				if (loadCount < 8)
				{
					int newData = data;
					int newValue = Terrain.MakeBlockValue(m_repeatCrossbowBlockIndex, loadCount + 1, newData);
					m_componentInventory.RemoveSlotItems(slot, 1);
					m_componentInventory.AddSlotItems(slot, newValue, 1);
					return true;
				}

				return true;
			}
			else if (contents == m_flameThrowerBlockIndex)
			{
				FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
				int loadCount = FlameThrowerBlock.GetLoadCount(weaponValue);
				FlameBulletBlock.FlameBulletType? bulletType = FlameThrowerBlock.GetBulletType(data);

				if (loadState == FlameThrowerBlock.LoadState.Empty)
				{
					FlameBulletBlock.FlameBulletType newBulletType = m_flameBulletTypes[m_random.Int(m_flameBulletTypes.Length)];
					int newData = FlameThrowerBlock.SetBulletType(data, newBulletType);
					newData = FlameThrowerBlock.SetLoadState(newData, FlameThrowerBlock.LoadState.Loaded);
					newData = FlameThrowerBlock.SetSwitchState(newData, true);
					int newValue = Terrain.MakeBlockValue(m_flameThrowerBlockIndex, 1, newData);
					m_componentInventory.RemoveSlotItems(slot, 1);
					m_componentInventory.AddSlotItems(slot, newValue, 1);
					return true;
				}

				if (loadCount < 15)
				{
					int newData = data;
					int newValue = Terrain.MakeBlockValue(m_flameThrowerBlockIndex, loadCount + 1, newData);
					m_componentInventory.RemoveSlotItems(slot, 1);
					m_componentInventory.AddSlotItems(slot, newValue, 1);
					return true;
				}

				return true;
			}
			return false;
		}

		private bool IsWeaponLoaded(int weaponValue)
		{
			int contents = Terrain.ExtractContents(weaponValue);
			int data = Terrain.ExtractData(weaponValue);

			if (contents == m_bowBlockIndex)
				return BowBlock.GetArrowType(data) != null;
			if (contents == m_crossbowBlockIndex)
			{
				int draw = CrossbowBlock.GetDraw(data);
				return draw == 15 && CrossbowBlock.GetArrowType(data) != null;
			}
			if (contents == m_musketBlockIndex)
			{
				MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
				return loadState == MusketBlock.LoadState.Loaded;
			}
			if (contents == m_repeatCrossbowBlockIndex)
			{
				int draw = RepeatCrossbowBlock.GetDraw(data);
				RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
				int loadCount = RepeatCrossbowBlock.GetLoadCount(weaponValue);
				return draw == 15 && arrowType != null && loadCount > 0;
			}
			if (contents == m_flameThrowerBlockIndex)
			{
				FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
				int loadCount = FlameThrowerBlock.GetLoadCount(weaponValue);
				return loadState == FlameThrowerBlock.LoadState.Loaded && loadCount > 0;
			}
			return false;
		}

		private bool IsMeleeWeapon(int value)
		{
			int contents = Terrain.ExtractContents(value);
			Block block = BlocksManager.Blocks[contents];
			float meleePower = block.GetMeleePower(value);
			if (meleePower <= 0) return false;
			return !IsRangedWeapon(value) && !IsThrowableWeapon(value);
		}

		private bool IsRangedWeapon(int value)
		{
			int contents = Terrain.ExtractContents(value);
			return contents == m_bowBlockIndex ||
				   contents == m_crossbowBlockIndex ||
				   contents == m_musketBlockIndex ||
				   contents == m_repeatCrossbowBlockIndex ||
				   contents == m_flameThrowerBlockIndex;
		}

		private bool IsThrowableWeapon(int value)
		{
			int contents = Terrain.ExtractContents(value);
			return m_throwableBlockIndices.Contains(contents);
		}

		private void StopMovement()
		{
			if (m_componentPathfinding != null && m_componentPathfinding.Destination != null)
			{
				m_originalDestination = m_componentPathfinding.Destination;
				m_originalSpeed = m_componentPathfinding.Speed;
				m_originalRange = m_componentPathfinding.Range;
				m_originalMaxPathfindingPositions = m_componentPathfinding.MaxPathfindingPositions;
				m_originalUseRandomMovements = m_componentPathfinding.UseRandomMovements;
				m_originalIgnoreHeightDifference = m_componentPathfinding.IgnoreHeightDifference;
				m_originalRaycastDestination = m_componentPathfinding.RaycastDestination;
				m_originalDoNotAvoidBody = m_componentPathfinding.DoNotAvoidBody;
				m_componentPathfinding.Stop();
			}
		}

		private void ResumeMovement()
		{
			if (m_componentChase != null && m_componentChase.Target != null)
			{
				if (m_componentPathfinding != null)
				{
					Vector3 targetPos = m_componentChase.Target.ComponentBody.Position;
					m_componentPathfinding.SetDestination(targetPos, m_originalSpeed > 0 ? m_originalSpeed : 1f,
						m_originalRange > 0 ? m_originalRange : 1f, m_originalMaxPathfindingPositions,
						m_originalUseRandomMovements, m_originalIgnoreHeightDifference,
						m_originalRaycastDestination, m_originalDoNotAvoidBody);
				}
			}
			else if (m_originalDestination != null)
			{
				if (m_componentPathfinding != null)
				{
					m_componentPathfinding.SetDestination(m_originalDestination, m_originalSpeed, m_originalRange,
						m_originalMaxPathfindingPositions, m_originalUseRandomMovements,
						m_originalIgnoreHeightDifference, m_originalRaycastDestination, m_originalDoNotAvoidBody);
					m_originalDestination = null;
				}
			}
		}

		private bool HasLineOfSight(ComponentCreature target)
		{
			if (m_subsystemTerrain == null || m_componentCreatureModel == null || target.ComponentCreatureModel == null)
				return false;

			Vector3 start = m_componentCreatureModel.EyePosition;
			Vector3 end = target.ComponentCreatureModel.EyePosition;

			TerrainRaycastResult? result = m_subsystemTerrain.Raycast(start, end, false, true, (int value, float distance) =>
			{
				Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
				return block.IsCollidable_(value);
			});

			if (result == null)
				return true;

			float distanceToTarget = Vector3.Distance(start, end);
			if (result.Value.Distance >= distanceToTarget - 0.1f)
				return true;

			return false;
		}

		private void UpdateAiming(ComponentCreature target)
		{
			// Cancelar si el objetivo ya no está enfrente (para armas lanzables)
			// Esto evita que se siga apuntando si el objetivo se mueve detrás
			if (!IsTargetInFront(target))
			{
				CancelAiming();
				return;
			}

			double currentTime = m_subsystemTime.GameTime;
			bool canFireCooldown = true;

			if (m_currentWeaponType == m_bowBlockIndex)
				canFireCooldown = currentTime - m_lastBowShotTime >= BOW_COOLDOWN;
			else if (m_currentWeaponType == m_crossbowBlockIndex)
				canFireCooldown = currentTime - m_lastCrossbowShotTime >= CROSSBOW_COOLDOWN;
			else if (m_currentWeaponType == m_musketBlockIndex)
				canFireCooldown = currentTime - m_lastMusketShotTime >= MUSKET_COOLDOWN;
			else if (m_currentWeaponType == m_repeatCrossbowBlockIndex)
				canFireCooldown = currentTime - m_lastRepeatCrossbowShotTime >= REPEAT_CROSSBOW_COOLDOWN;
			else if (m_currentWeaponType == m_flameThrowerBlockIndex)
				canFireCooldown = currentTime - m_lastFlameThrowerShotTime >= FLAMETHROWER_COOLDOWN;
			else if (IsThrowableWeapon(m_componentInventory.GetSlotValue(m_componentInventory.ActiveSlotIndex)))
				canFireCooldown = currentTime - m_lastThrowableShotTime >= THROWABLE_COOLDOWN;

			if (!canFireCooldown)
			{
				CancelAiming();
				return;
			}

			if (!HasLineOfSight(target))
			{
				CancelAiming();
				return;
			}

			ComponentCreatureModel targetModel = target.ComponentCreatureModel;
			Vector3 targetAimPoint;
			if (targetModel != null)
			{
				targetAimPoint = targetModel.EyePosition;
			}
			else
			{
				targetAimPoint = target.ComponentBody.BoundingBox.Center() + Vector3.UnitY * 0.5f;
			}

			Vector3 eyePos = m_componentCreatureModel.EyePosition;
			Vector3 aimDir = Vector3.Normalize(targetAimPoint - eyePos);
			m_aimRay = new Ray3(eyePos, aimDir);

			if (!m_isAiming)
			{
				m_isAiming = true;
				m_aimStartTime = currentTime;
			}

			m_componentMiner.Aim(m_aimRay, AimState.InProgress);

			int activeSlot = m_componentInventory.ActiveSlotIndex;
			int weaponValue = m_componentInventory.GetSlotValue(activeSlot);
			int data = Terrain.ExtractData(weaponValue);
			bool readyToFire = false;
			double aimTime = currentTime - m_aimStartTime;

			if (m_currentWeaponType == m_bowBlockIndex)
			{
				int draw = BowBlock.GetDraw(data);
				readyToFire = draw == 15;
			}
			else if (m_currentWeaponType == m_crossbowBlockIndex)
			{
				readyToFire = aimTime >= CROSSBOW_MIN_AIM_TIME;
			}
			else if (m_currentWeaponType == m_musketBlockIndex)
			{
				bool hammerCocked = MusketBlock.GetHammerState(data);
				MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
				readyToFire = hammerCocked && loadState == MusketBlock.LoadState.Loaded && aimTime >= MUSKET_MIN_AIM_TIME;
			}
			else if (m_currentWeaponType == m_repeatCrossbowBlockIndex)
			{
				int draw = RepeatCrossbowBlock.GetDraw(data);
				RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
				readyToFire = draw == 15 && arrowType != null && aimTime >= REPEAT_CROSSBOW_MIN_AIM_TIME;
			}
			else if (m_currentWeaponType == m_flameThrowerBlockIndex)
			{
				FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
				int loadCount = FlameThrowerBlock.GetLoadCount(weaponValue);
				bool switchState = FlameThrowerBlock.GetSwitchState(data);
				readyToFire = loadState == FlameThrowerBlock.LoadState.Loaded && loadCount > 0 && switchState;
			}
			else if (IsThrowableWeapon(weaponValue))
			{
				readyToFire = aimTime >= THROWABLE_MIN_AIM_TIME;
			}

			if (readyToFire)
			{
				bool isAutomatic = (m_currentWeaponType == m_flameThrowerBlockIndex);
				bool isThrowable = IsThrowableWeapon(weaponValue);

				if (!isAutomatic)
				{
					m_componentMiner.Aim(m_aimRay, AimState.Completed);
					m_isAiming = false;

					if (isThrowable)
					{
						ResumeMovement();
					}

					if (m_currentWeaponType == m_bowBlockIndex)
						m_lastBowShotTime = currentTime;
					else if (m_currentWeaponType == m_crossbowBlockIndex)
						m_lastCrossbowShotTime = currentTime;
					else if (m_currentWeaponType == m_musketBlockIndex)
						m_lastMusketShotTime = currentTime;
					else if (m_currentWeaponType == m_repeatCrossbowBlockIndex)
						m_lastRepeatCrossbowShotTime = currentTime;
					else if (isThrowable)
						m_lastThrowableShotTime = currentTime;
				}
				else
				{
					m_lastFlameThrowerShotTime = currentTime;
				}
			}

			if (m_currentWeaponType == m_flameThrowerBlockIndex)
			{
				int loadCount = FlameThrowerBlock.GetLoadCount(weaponValue);
				FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
				if (loadCount <= 0 || loadState != FlameThrowerBlock.LoadState.Loaded)
				{
					CancelAiming();
				}
			}

			if (m_componentCreatureModel != null)
				m_componentCreatureModel.AttackOrder = false;
		}

		private void CancelAiming()
		{
			if (m_isAiming)
			{
				m_componentMiner.Aim(m_aimRay, AimState.Cancelled);
				m_isAiming = false;
				ResumeMovement();
			}
		}

		private void OnProjectileAdded(Projectile projectile)
		{
			if (projectile.Owner == m_componentCreature)
			{
				if (!IsThrowableWeapon(projectile.Value))
				{
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				}
			}
		}
	}
}
