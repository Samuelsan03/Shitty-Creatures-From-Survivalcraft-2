using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureAI : Component, IUpdateable
	{
		// Configurable fields (not from dictionary)
		public Vector2 RangeOfExplosives = new Vector2(20f, 100f);
		public Vector2 EngagementRange = new Vector2(5f, 100f);
		public Vector2 ThrowableRange = new Vector2(5f, 15f);
		public float MusketAimTime = 1.5f;
		public float MusketCooldown = 0.02f;
		public float DoubleMusketAimTime = 1.5f;
		public float DoubleMusketCooldown = 0.02f;
		public float FlameThrowerAimTime = 1.5f;
		public float FlameThrowerCooldown = 0.01f;
		public float ItemsLauncherAimTime = 1.5f;
		public float ItemsLauncherCooldown = 0.02f;
		public float BowAimTime = 1.5f;
		public float BowCooldown = 0.01f;
		public float CrossbowAimTime = 1.5f;
		public float CrossbowCooldown = 0.01f;
		public float RepeatCrossbowAimTime = 1.5f;
		public float RepeatCrossbowCooldown = 0.01f;
		public float ThrowableAimTime = 1.55f;
		public float ThrowableCooldown = 0.01f;

		// Dictionary parameters
		public bool CanUseInventory = false;
		public bool CanEquipClothing = false;

		SubsystemTime m_subsystemTime;
		SubsystemProjectiles m_subsystemProjectiles;
		SubsystemAudio m_subsystemAudio;
		SubsystemParticles m_subsystemParticles;
		SubsystemTerrain m_subsystemTerrain;

		ComponentCreature m_componentCreature;
		ComponentChaseBehavior m_chaseBehavior;
		ComponentMiner m_componentMiner;
		ComponentPathfinding m_componentPathfinding;

		Random m_random = new Random();

		float m_aimTimer;
		bool m_isAiming;
		float m_cooldownTimer;

		int m_pendingClothingValue;
		int m_pendingClothingSlotIndex;
		float m_clothingEquipTimer;

		bool m_isThrowing;
		List<int> m_throwableIndices = new List<int>();

		// Firearm fields
		private bool m_isFirearmReloading = false;
		private float m_firearmReloadTimer = 0f;
		private int m_firearmShotsSinceReload = 0;
		private const float FirearmReloadTime = 1.0f;
		private double m_lastFirearmShotTime;
		private bool m_hasCompletedInitialAim = false;

		// ========== CONFIGURACIÓN INTERNA DE ARMAS DE FUEGO ==========
		private class FirearmDefConfig
		{
			public Type BulletBlockType;
			public string ShootSound;
			public float FireRate;
			public float BulletSpeed;
			public int ProjectilesPerShot;
			public Vector3 SpreadVector;
			public bool IsSniper;
			public bool IsAutomatic;
			public int MaxShotsBeforeReload;
		}

		private static Dictionary<int, FirearmDefConfig> m_firearmConfigs;

		static ComponentCreatureAI()
		{
			m_firearmConfigs = new Dictionary<int, FirearmDefConfig>();
			void Add(Type weaponType, Type bulletType, string sound, double fireRate, float bulletSpeed, int projPerShot, Vector3 spread, int maxShots = 30, bool sniper = false, bool automatic = false)
			{
				int idx = BlocksManager.GetBlockIndex(weaponType, true, false);
				m_firearmConfigs[idx] = new FirearmDefConfig
				{
					BulletBlockType = bulletType,
					ShootSound = sound,
					FireRate = (float)fireRate,
					BulletSpeed = bulletSpeed,
					ProjectilesPerShot = projPerShot,
					SpreadVector = spread,
					IsSniper = sniper,
					IsAutomatic = automatic,
					MaxShotsBeforeReload = maxShots
				};
			}

			Add(typeof(AK48Block), typeof(NuevaBala6), "Audio/Armas/AK48 fire", 0.17, 280f, 2, new Vector3(0.01f, 0.01f, 0.05f), 60, automatic: true);
			Add(typeof(Master308Block), typeof(NuevaBala4), "Audio/Armas/308 Master fire", 0.48, 300f, 1, new Vector3(0.001f, 0.001f, 0.001f), 8);
			Add(typeof(BK43Block), typeof(NuevaBala3), "Audio/Armas/bk 43", 1.5, 280f, 8, new Vector3(0.1f, 0.1f, 0.03f), 2);
			Add(typeof(AKBlock), typeof(NuevaBala2), "Audio/Armas/ak 47 fuego", 0.17, 280f, 2, new Vector3(0.01f, 0.01f, 0.05f), 30, automatic: true);
			Add(typeof(M4Block), typeof(NuevaBala2), "Audio/Armas/M4 fuego", 0.15, 300f, 3, new Vector3(0.008f, 0.008f, 0.04f), 22, automatic: true);
			Add(typeof(KABlock), typeof(NuevaBala5), "Audio/Armas/KA fuego", 0.1, 320f, 3, new Vector3(0.007f, 0.007f, 0.03f), 40, automatic: true);
			Add(typeof(Mac10Block), typeof(NuevaBala3), "Audio/Armas/mac 10 fuego", 0.1, 300f, 1, new Vector3(0.012f, 0.012f, 0.035f), 30, automatic: true);
			Add(typeof(SWM500Block), typeof(NuevaBala4), "Audio/Armas/desert eagle fuego", 0.5, 320f, 1, new Vector3(0.02f, 0.02f, 0.05f), 5);
			Add(typeof(G3Block), typeof(NuevaBala), "Audio/Armas/FX05", 0.12, 290f, 2, new Vector3(0.009f, 0.009f, 0.04f), 30, automatic: true);
			Add(typeof(Izh43Block), typeof(NuevaBala), "Audio/Armas/shotgun fuego", 1.0, 280f, 8, new Vector3(0.09f, 0.09f, 0.09f), 2);
			Add(typeof(MinigunBlock), typeof(NuevaBala6), "Audio/Armas/Chaingun fuego", 0.08, 260f, 1, new Vector3(0.02f, 0.02f, 0.08f), 100, automatic: true);
			Add(typeof(SPAS12Block), typeof(NuevaBala), "Audio/Armas/SPAS 12 fuego", 0.8, 280f, 8, new Vector3(0.09f, 0.09f, 0.09f), 8);
			Add(typeof(UziBlock), typeof(NuevaBala2), "Audio/Armas/Uzi fuego", 0.08, 320f, 2, new Vector3(0.015f, 0.015f, 0.06f), 30, automatic: true);
			Add(typeof(SniperBlock), typeof(NuevaBala6), "Audio/Armas/Sniper fuego", 2.0, 450f, 1, new Vector3(0.001f, 0.001f, 0.001f), 1, sniper: true);
			Add(typeof(AUGBlock), typeof(NuevaBala), "Audio/Armas/AUG fuego", 0.17, 280f, 2, new Vector3(0.01f, 0.01f, 0.05f), 30, automatic: true);
			Add(typeof(P90Block), typeof(NuevaBala4), "Audio/Armas/FN P90 fuego", 0.067, 320f, 1, new Vector3(0.012f, 0.012f, 0.04f), 50, automatic: true);
			Add(typeof(SCARBlock), typeof(NuevaBala3), "Audio/Armas/FN Scar fuego", 0.1, 310f, 1, new Vector3(0.01f, 0.01f, 0.03f), 30, automatic: true);
			Add(typeof(RevolverBlock), typeof(NuevaBala4), "Audio/Armas/Revolver fuego", 0.6, 320f, 1, new Vector3(0.02f, 0.02f, 0.05f), 6);
			Add(typeof(FamasBlock), typeof(NuevaBala4), "Audio/Armas/FAMAS fuego", 0.09, 450f, 1, new Vector3(0.012f, 0.012f, 0.04f), 30, automatic: true);
			Add(typeof(AA12Block), typeof(NuevaBala6), "Audio/Armas/AA12 fuego", 0.2, 350f, 8, new Vector3(0.03f, 0.03f, 0.06f), 20, automatic: true);
			Add(typeof(M249Block), typeof(NuevaBala5), "Audio/Armas/M249 fuego", 0.08, 400f, 1, new Vector3(0.01f, 0.01f, 0.01f), 100, automatic: true);
			Add(typeof(NewG3Block), typeof(NuevaBala3), "Audio/Armas/G3 fuego", 0.12, 290f, 2, new Vector3(0.009f, 0.009f, 0.04f), 30, automatic: true);
			Add(typeof(MP5SSDBlock), typeof(NuevaBala3), "Audio/Armas/MP5SSD fuego", 0.12, 290f, 2, new Vector3(0.009f, 0.009f, 0.04f), 30, automatic: true);
			Add(typeof(MendozaBlock), typeof(NuevaBala3), "Audio/Armas/Mendoza fuego", 0.12, 290f, 2, new Vector3(0.009f, 0.009f, 0.04f), 30, automatic: true);
			Add(typeof(GrozaBlock), typeof(NuevaBala3), "Audio/Armas/Groza fuego", 0.12, 290f, 2, new Vector3(0.009f, 0.009f, 0.04f), 30, automatic: true);
		}
		// ========== FIN CONFIGURACIÓN ARMAS DE FUEGO ==========

		static readonly ArrowBlock.ArrowType[] s_bowArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.FireArrow,
			ArrowBlock.ArrowType.CopperArrow
		};

		static readonly ArrowBlock.ArrowType[] s_crossbowBoltTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
		};

		static readonly RepeatArrowBlock.ArrowType[] s_repeatArrowTypes = new RepeatArrowBlock.ArrowType[]
		{
			RepeatArrowBlock.ArrowType.CopperArrow,
			RepeatArrowBlock.ArrowType.IronArrow,
			RepeatArrowBlock.ArrowType.DiamondArrow,
			RepeatArrowBlock.ArrowType.ExplosiveArrow,
			RepeatArrowBlock.ArrowType.PoisonArrow,
			RepeatArrowBlock.ArrowType.SeriousPoisonArrow
		};

		static readonly FlameBulletBlock.FlameBulletType[] s_flameBulletTypes = new FlameBulletBlock.FlameBulletType[]
		{
			FlameBulletBlock.FlameBulletType.Flame,
			FlameBulletBlock.FlameBulletType.Poison
		};

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue("CanUseInventory", CanUseInventory);
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_chaseBehavior = Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);

			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory");
			CanEquipClothing = valuesDictionary.GetValue<bool>("CanEquipClothing");

			m_subsystemProjectiles.ProjectileAdded += OnProjectileAdded;

			m_throwableIndices.Add(BlocksManager.GetBlockIndex<StoneChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<SulphurChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<CoalChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<DiamondChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<GermaniumChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<GermaniumOreChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<IronOreChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<MalachiteChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<SaltpeterChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<GunpowderBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<BombBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<IncendiaryBombBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<PoisonBombBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<BrickBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<SnowballBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<EggBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<CopperSpearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<DiamondSpearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<IronSpearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<WoodenSpearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<WoodenLongspearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<StoneSpearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<StoneLongspearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<IronLongspearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<LavaLongspearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<LavaSpearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<DiamondLongspearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<FreezingSnowballBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<FreezeBombBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<FireworksBlock>(false, false));
		}

		void OnProjectileAdded(Projectile projectile)
		{
			if (projectile.Owner == m_componentCreature)
			{
				int contents = Terrain.ExtractContents(projectile.Value);
				if (contents == ArrowBlock.Index || contents == RepeatArrowBlock.Index)
				{
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				}
			}
		}

		bool IsThrowable(int contents)
		{
			return m_throwableIndices.Contains(contents);
		}

		int FindThrowableSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int value = inventory.GetSlotValue(i);
				if (IsThrowable(Terrain.ExtractContents(value)) && inventory.GetSlotCount(i) > 0)
					return i;
			}
			return -1;
		}

		int FindFirearmSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(slotValue);
				if (m_firearmConfigs.ContainsKey(contents) && inventory.GetSlotCount(i) > 0)
					return i;
			}
			return -1;
		}

		public void Update(float dt)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return;

			if (CanEquipClothing)
			{
				IInventory clothingInv = FindClothingInventory();
				if (clothingInv != null)
				{
					if (m_clothingEquipTimer > 0f)
					{
						m_clothingEquipTimer -= dt;
						if (m_clothingEquipTimer <= 0f)
						{
							clothingInv.ProcessSlotItems(m_pendingClothingSlotIndex, m_pendingClothingValue, 1, 1, out _, out _);
							m_pendingClothingValue = 0;
						}
					}
					else
					{
						for (int i = 0; i < inventory.SlotsCount; i++)
						{
							int slotValue = inventory.GetSlotValue(i);
							if (slotValue != 0)
							{
								Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
								ClothingData clothingData = block.GetClothingData(slotValue);
								if (clothingData != null)
								{
									int removed = inventory.RemoveSlotItems(i, 1);
									if (removed > 0)
									{
										ClothingSlot slot = clothingData.Slot;
										int targetSlot = ComponentCreatureClothing.GetClothingSlotIndex(slot);
										m_pendingClothingValue = slotValue;
										m_pendingClothingSlotIndex = targetSlot;
										m_clothingEquipTimer = 0.55f;
										break;
									}
								}
							}
						}
					}
				}
			}

			if (!CanUseInventory) return;
			if (m_componentCreature.ComponentHealth.Health <= 0f) return;

			ComponentCreature target = m_chaseBehavior.Target;
			if (target == null || target.ComponentHealth.Health <= 0f)
			{
				CancelAiming(inventory);
				m_isThrowing = false;
				return;
			}

			if (m_componentPathfinding.IsStuck)
			{
				CancelAiming(inventory);
				m_isThrowing = false;
				return;
			}

			Vector3 creaturePos = m_componentCreature.ComponentBody.Position;
			Vector3 targetPos = target.ComponentBody.BoundingBox.Center();
			float distance = Vector3.Distance(creaturePos, targetPos);

			bool targetInFront = IsTargetInFront(target);
			bool targetVisible = targetInFront && IsTargetVisible(target);

			int throwableSlot = FindThrowableSlot(inventory);
			if (throwableSlot >= 0 && distance >= ThrowableRange.X && distance <= ThrowableRange.Y && targetVisible)
			{
				if (inventory.ActiveSlotIndex != throwableSlot)
				{
					CancelAiming(inventory);
					inventory.ActiveSlotIndex = throwableSlot;
					m_cooldownTimer = 0f;
				}

				if (!m_isThrowing)
				{
					m_isThrowing = true;
					m_componentPathfinding.Stop();
				}
				m_componentPathfinding.Stop();

				if (m_cooldownTimer > 0f)
				{
					m_cooldownTimer -= dt;
					if (m_cooldownTimer < 0f) m_cooldownTimer = 0f;
				}

				if (!m_isAiming && m_cooldownTimer <= 0f)
				{
					m_isAiming = true;
					m_aimTimer = 0f;
				}

				if (m_isAiming)
				{
					m_aimTimer += dt;
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 aimDir = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();

					if (m_aimTimer >= ThrowableAimTime)
					{
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.Completed);
						m_isAiming = false;
						m_cooldownTimer = ThrowableCooldown;
						m_isThrowing = false;
					}
					else
					{
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.InProgress);
					}
				}
				return;
			}
			else
			{
				if (m_isThrowing)
				{
					CancelAiming(inventory);
					m_isThrowing = false;
				}
			}

			if (m_isThrowing) return;

			int firearmSlot = FindFirearmSlot(inventory);
			int musketSlot = FindMusketSlot(inventory);
			int doubleMusketSlot = FindDoubleMusketSlot(inventory);
			int flameThrowerSlot = FindFlameThrowerSlot(inventory);
			int itemsLauncherSlot = FindItemsLauncherSlot(inventory);
			int bowSlot = FindBowSlot(inventory);
			int crossbowSlot = FindCrossbowSlot(inventory);
			int repeatCrossbowSlot = FindRepeatCrossbowSlot(inventory);
			int meleeSlot = FindMeleeSlot(inventory);

			if (distance > EngagementRange.Y)
			{
				CancelAiming(inventory);
				if (firearmSlot >= 0) EquipSlot(inventory, firearmSlot);
				else if (musketSlot >= 0) EquipSlot(inventory, musketSlot);
				else if (doubleMusketSlot >= 0) EquipSlot(inventory, doubleMusketSlot);
				else if (flameThrowerSlot >= 0) EquipSlot(inventory, flameThrowerSlot);
				else if (itemsLauncherSlot >= 0) EquipSlot(inventory, itemsLauncherSlot);
				else if (bowSlot >= 0) EquipSlot(inventory, bowSlot);
				else if (crossbowSlot >= 0) EquipSlot(inventory, crossbowSlot);
				else if (repeatCrossbowSlot >= 0) EquipSlot(inventory, repeatCrossbowSlot);
				else if (meleeSlot >= 0) EquipSlot(inventory, meleeSlot);
			}
			else if (distance < EngagementRange.X)
			{
				if (targetVisible && meleeSlot >= 0)
				{
					CancelAiming(inventory);
					EquipSlot(inventory, meleeSlot);
				}
				else if (firearmSlot >= 0) EquipSlot(inventory, firearmSlot);
				else if (musketSlot >= 0) EquipSlot(inventory, musketSlot);
				else if (doubleMusketSlot >= 0) EquipSlot(inventory, doubleMusketSlot);
				else if (flameThrowerSlot >= 0) EquipSlot(inventory, flameThrowerSlot);
				else if (itemsLauncherSlot >= 0) EquipSlot(inventory, itemsLauncherSlot);
				else if (bowSlot >= 0) EquipSlot(inventory, bowSlot);
				else if (crossbowSlot >= 0) EquipSlot(inventory, crossbowSlot);
				else if (repeatCrossbowSlot >= 0) EquipSlot(inventory, repeatCrossbowSlot);
			}
			else
			{
				if (firearmSlot >= 0) EquipSlot(inventory, firearmSlot);
				else if (musketSlot >= 0) EquipSlot(inventory, musketSlot);
				else if (doubleMusketSlot >= 0) EquipSlot(inventory, doubleMusketSlot);
				else if (flameThrowerSlot >= 0) EquipSlot(inventory, flameThrowerSlot);
				else if (itemsLauncherSlot >= 0) EquipSlot(inventory, itemsLauncherSlot);
				else if (bowSlot >= 0) EquipSlot(inventory, bowSlot);
				else if (crossbowSlot >= 0) EquipSlot(inventory, crossbowSlot);
				else if (repeatCrossbowSlot >= 0) EquipSlot(inventory, repeatCrossbowSlot);
				else if (meleeSlot >= 0) EquipSlot(inventory, meleeSlot);
			}

			int activeSlotValue = inventory.GetSlotValue(inventory.ActiveSlotIndex);
			int activeContents = Terrain.ExtractContents(activeSlotValue);

			if (activeContents == MusketBlock.Index && targetVisible)
			{
				EnsureMusketLoaded(inventory);
				if (m_cooldownTimer > 0f) { m_cooldownTimer -= dt; if (m_cooldownTimer < 0f) m_cooldownTimer = 0f; }
				if (!m_isAiming && m_cooldownTimer <= 0f) { m_isAiming = true; m_aimTimer = 0f; }
				if (m_isAiming)
				{
					m_aimTimer += dt;
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 aimDir = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
					if (m_aimTimer >= MusketAimTime)
					{
						BulletBlock.BulletType? currentBulletType = MusketBlock.GetBulletType(Terrain.ExtractData(inventory.GetSlotValue(inventory.ActiveSlotIndex)));
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.Completed);
						m_isAiming = false;
						m_cooldownTimer = MusketCooldown;
						EnsureMusketLoaded(inventory);
						if (m_random.Float(0f, 1f) < 0.05f)
						{
							Vector3 musketPos = eyePos + m_componentCreature.ComponentBody.Matrix.Right * 0.3f - m_componentCreature.ComponentBody.Matrix.Up * 0.2f;
							Vector3 musketDir = Vector3.Normalize(musketPos + aimDir * 10f - musketPos);
							if (currentBulletType != BulletBlock.BulletType.MusketBall)
								FireSingleProjectile(BulletBlock.BulletType.MusketBall, musketPos, musketDir, 120f, Vector3.Zero, 1);
							if (currentBulletType != BulletBlock.BulletType.Buckshot)
								FireBuckshot(musketPos, musketDir);
							if (currentBulletType != BulletBlock.BulletType.BuckshotBall)
								FireSingleProjectile(BulletBlock.BulletType.BuckshotBall, musketPos, musketDir, 60f, new Vector3(0.06f, 0.06f, 0f), 1);
						}
					}
					else m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.InProgress);
				}
			}
			else if (activeContents == DoubleMusketBlock.Index && targetVisible)
			{
				EnsureDoubleMusketLoaded(inventory);
				if (m_cooldownTimer > 0f) { m_cooldownTimer -= dt; if (m_cooldownTimer < 0f) m_cooldownTimer = 0f; }
				if (!m_isAiming && m_cooldownTimer <= 0f) { m_isAiming = true; m_aimTimer = 0f; }
				if (m_isAiming)
				{
					m_aimTimer += dt;
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 aimDir = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
					if (m_aimTimer >= DoubleMusketAimTime)
					{
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.Completed);
						m_isAiming = false;
						m_cooldownTimer = DoubleMusketCooldown;
					}
					else m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.InProgress);
				}
			}
			else if (activeContents == FlameThrowerBlock.Index && targetVisible)
			{
				EnsureFlameThrowerLoaded(inventory);
				if (m_cooldownTimer > 0f) { m_cooldownTimer -= dt; if (m_cooldownTimer < 0f) m_cooldownTimer = 0f; }
				if (!m_isAiming && m_cooldownTimer <= 0f) { m_isAiming = true; m_aimTimer = 0f; }
				if (m_isAiming)
				{
					m_aimTimer += dt;
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 aimDir = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
					if (m_aimTimer >= FlameThrowerAimTime)
					{
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.Completed);
						m_isAiming = false;
						m_cooldownTimer = FlameThrowerCooldown;
					}
					else
					{
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.InProgress);
					}
				}
			}
			else if (activeContents == ItemsLauncherBlock.Index && targetVisible)
			{
				// El lanzador de ítems no necesita Ensure porque no consume munición
				if (m_cooldownTimer > 0f) { m_cooldownTimer -= dt; if (m_cooldownTimer < 0f) m_cooldownTimer = 0f; }
				if (!m_isAiming && m_cooldownTimer <= 0f) { m_isAiming = true; m_aimTimer = 0f; }
				if (m_isAiming)
				{
					m_aimTimer += dt;
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 aimDir = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();

					// Animación manual del modelo (sin usar miner.Aim)
					m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
					m_componentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					m_componentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

					if (m_aimTimer >= ItemsLauncherAimTime)
					{
						// Disparar manualmente
						Vector3 muzzlePos = eyePos + m_componentCreature.ComponentBody.Matrix.Right * 0.3f - m_componentCreature.ComponentBody.Matrix.Up * 0.2f;
						Vector3 dirNorm = Vector3.Normalize(muzzlePos + aimDir * 10f - muzzlePos);
						int musketBallValue = Terrain.MakeBlockValue(BulletBlock.Index, 0, BulletBlock.SetBulletType(0, BulletBlock.BulletType.MusketBall));
						float speed = 60f; // Velocidad fija razonable
						Vector3 velocity = m_componentCreature.ComponentBody.Velocity + speed * dirNorm;
						Projectile projectile = m_subsystemProjectiles.FireProjectile(musketBallValue, muzzlePos, velocity, Vector3.Zero, m_componentCreature);
						if (projectile != null)
						{
							// Opcional: hacer que desaparezca al impactar (si se desea)
							// projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
						}
						m_subsystemAudio.PlaySound("Audio/Items/ItemLauncher/Item Cannon Fire", 0.5f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, true);
						m_subsystemParticles.AddParticleSystem(new GunSmokeParticleSystem(m_subsystemTerrain, muzzlePos + 0.3f * dirNorm, dirNorm), false);
						m_componentCreature.ComponentBody.ApplyImpulse(-4f * dirNorm);

						m_isAiming = false;
						m_cooldownTimer = ItemsLauncherCooldown;
					}
				}
			}
			else if (activeContents == BowBlock.Index && targetVisible)
			{
				EnsureBowLoaded(inventory);
				if (m_cooldownTimer > 0f) { m_cooldownTimer -= dt; if (m_cooldownTimer < 0f) m_cooldownTimer = 0f; }
				if (!m_isAiming && m_cooldownTimer <= 0f) { m_isAiming = true; m_aimTimer = 0f; }
				if (m_isAiming)
				{
					m_aimTimer += dt;
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 aimDir = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
					if (m_aimTimer >= BowAimTime)
					{
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.Completed);
						m_isAiming = false;
						m_cooldownTimer = BowCooldown;
					}
					else m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.InProgress);
				}
			}
			else if (activeContents == CrossbowBlock.Index && targetVisible)
			{
				float distToTarget = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.BoundingBox.Center());
				EnsureCrossbowLoaded(inventory, distToTarget);
				if (m_cooldownTimer > 0f) { m_cooldownTimer -= dt; if (m_cooldownTimer < 0f) m_cooldownTimer = 0f; }
				if (!m_isAiming && m_cooldownTimer <= 0f) { m_isAiming = true; m_aimTimer = 0f; }
				if (m_isAiming)
				{
					m_aimTimer += dt;
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 aimDir = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
					if (m_aimTimer >= CrossbowAimTime)
					{
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.Completed);
						m_isAiming = false;
						m_cooldownTimer = CrossbowCooldown;
					}
					else m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.InProgress);
				}
			}
			else if (activeContents == RepeatCrossbowBlock.Index && targetVisible)
			{
				float distToTarget = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.BoundingBox.Center());
				EnsureRepeatCrossbowLoaded(inventory, distToTarget);
				if (m_cooldownTimer > 0f) { m_cooldownTimer -= dt; if (m_cooldownTimer < 0f) m_cooldownTimer = 0f; }
				if (!m_isAiming && m_cooldownTimer <= 0f) { m_isAiming = true; m_aimTimer = 0f; }
				if (m_isAiming)
				{
					m_aimTimer += dt;
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 aimDir = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
					if (m_aimTimer >= RepeatCrossbowAimTime)
					{
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.Completed);
						m_isAiming = false;
						m_cooldownTimer = RepeatCrossbowCooldown;
					}
					else m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.InProgress);
				}
			}
			else if (m_firearmConfigs.ContainsKey(activeContents) && targetVisible)
			{
				// *** ARMAS DE FUEGO MODERNAS ***
				if (m_isFirearmReloading)
				{
					m_firearmReloadTimer -= dt;
					if (m_firearmReloadTimer <= 0f)
					{
						m_isFirearmReloading = false;
						m_firearmShotsSinceReload = 0;
						// Partículas de fin de recarga
						if (m_subsystemParticles != null && m_subsystemTerrain != null)
						{
							try
							{
								Vector3 basePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
								Vector3 readyPosition = basePosition + new Vector3(0f, 0.2f, 0f);
								KillParticleSystem readyParticles = new KillParticleSystem(m_subsystemTerrain, readyPosition, 0.5f);
								m_subsystemParticles.AddParticleSystem(readyParticles, false);
								for (int i = 0; i < 3; i++)
								{
									Vector3 offset = new Vector3(m_random.Float(-0.2f, 0.2f), m_random.Float(0.1f, 0.4f), m_random.Float(-0.2f, 0.2f));
									KillParticleSystem additionalParticles = new KillParticleSystem(m_subsystemTerrain, basePosition + offset, 0.5f);
									m_subsystemParticles.AddParticleSystem(additionalParticles, false);
								}
							}
							catch (Exception)
							{
							}
						}
						m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, 0f, m_componentCreature.ComponentCreatureModel.EyePosition, 10f, true);
						// Restaurar rotación del modelo tras recarga
						ResetModelRotation();
						m_isAiming = false;
						m_aimTimer = 0f;
						m_hasCompletedInitialAim = false;
						m_cooldownTimer = 0f;
					}
					else
					{
						// Animación de recarga
						ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
						if (model != null)
						{
							model.AimHandAngleOrder = 0f;
							model.InHandItemOffsetOrder = Vector3.Zero;
							model.InHandItemRotationOrder = Vector3.Zero;
							model.LookAtOrder = null;
						}
					}
					if (m_isAiming)
					{
						CancelAiming(inventory);
					}
					return;
				}

				FirearmDefConfig config = m_firearmConfigs[activeContents];
				if (m_cooldownTimer > 0f)
				{
					m_cooldownTimer -= dt;
					if (m_cooldownTimer < 0f) m_cooldownTimer = 0f;
				}

				if (!m_isAiming && m_cooldownTimer <= 0f)
				{
					m_isAiming = true;
					m_aimTimer = 0f;
					m_hasCompletedInitialAim = false;
				}

				if (m_isAiming)
				{
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 aimDir = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
					Ray3 aimRay = new Ray3(eyePos, aimDir);
					float aimTime = config.IsSniper ? 1.0f : 0.5f;

					if (!m_hasCompletedInitialAim)
					{
						m_aimTimer += dt;
						if (config.IsSniper)
						{
							// Animación de apunte del sniper (sin usar Miner.Aim)
							ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
							if (model != null)
							{
								model.AimHandAngleOrder = 1.2f;
								model.InHandItemOffsetOrder = new Vector3(-0.1f, -0.06f, 0.08f);
								model.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
								if (m_chaseBehavior != null && m_chaseBehavior.Target != null)
								{
									model.LookAtOrder = m_chaseBehavior.Target.ComponentCreatureModel.EyePosition;
								}
							}
						}
						else
						{
							m_componentMiner.Aim(aimRay, AimState.InProgress);
						}

						if (m_aimTimer >= aimTime)
						{
							FireFirearm(aimRay, config);
							m_lastFirearmShotTime = m_subsystemTime.GameTime;
							m_hasCompletedInitialAim = true;
							m_aimTimer = aimTime; // mantener el temporizador en el valor de apunte
							if (m_firearmShotsSinceReload >= config.MaxShotsBeforeReload)
							{
								StartFirearmReload();
							}
						}
					}
					else
					{
						// Mantener apunte
						if (config.IsSniper)
						{
							ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
							if (model != null)
							{
								model.AimHandAngleOrder = 1.2f;
								model.InHandItemOffsetOrder = new Vector3(-0.1f, -0.06f, 0.08f);
								model.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
								if (m_chaseBehavior != null && m_chaseBehavior.Target != null)
								{
									model.LookAtOrder = m_chaseBehavior.Target.ComponentCreatureModel.EyePosition;
								}
							}
						}
						else
						{
							m_componentMiner.Aim(aimRay, AimState.InProgress);
						}

						if ((m_subsystemTime.GameTime - m_lastFirearmShotTime) >= config.FireRate)
						{
							FireFirearm(aimRay, config);
							m_lastFirearmShotTime = m_subsystemTime.GameTime;
							if (m_firearmShotsSinceReload >= config.MaxShotsBeforeReload)
							{
								StartFirearmReload();
							}
						}
					}
				}
			}
			else
			{
				CancelAiming(inventory);
			}
		}

		void FireFirearm(Ray3 aimRay, FirearmDefConfig config)
		{
			Vector3 eyePos = aimRay.Position;
			Vector3 direction = aimRay.Direction;
			Vector3 muzzlePos = eyePos + m_componentCreature.ComponentBody.Matrix.Right * 0.3f - m_componentCreature.ComponentBody.Matrix.Up * 0.2f;
			Vector3 dirNorm = Vector3.Normalize(muzzlePos + direction * 10f - muzzlePos);
			Vector3 right = Vector3.Normalize(Vector3.Cross(dirNorm, Vector3.UnitY));
			Vector3 up = Vector3.Normalize(Vector3.Cross(dirNorm, right));

			for (int i = 0; i < config.ProjectilesPerShot; i++)
			{
				Vector3 spread = m_random.Float(-config.SpreadVector.X, config.SpreadVector.X) * right
							   + m_random.Float(-config.SpreadVector.Y, config.SpreadVector.Y) * up
							   + m_random.Float(-config.SpreadVector.Z, config.SpreadVector.Z) * dirNorm;
				int bulletBlockIndex = BlocksManager.GetBlockIndex(config.BulletBlockType, true, false);
				int bulletValue;
				if (config.IsSniper)
				{
					bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, 180);
				}
				else
				{
					bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, 0);
				}
				Vector3 velocity = m_componentCreature.ComponentBody.Velocity + config.BulletSpeed * (dirNorm + spread);
				m_subsystemProjectiles.FireProjectile(bulletValue, muzzlePos, velocity, Vector3.Zero, m_componentCreature);
			}

			m_subsystemAudio.PlaySound(config.ShootSound, 1f, m_random.Float(-0.1f, 0.1f), eyePos, 15f, false);
			if (m_subsystemParticles != null)
			{
				m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, muzzlePos + dirNorm * 0.5f, dirNorm), false);
			}
			m_componentCreature.ComponentBody.ApplyImpulse(-1f * dirNorm);
			m_firearmShotsSinceReload++;
		}

		private void ResetModelRotation()
		{
			ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
			if (model != null)
			{
				model.InHandItemRotationOrder = Vector3.Zero;
				model.InHandItemOffsetOrder = Vector3.Zero;
				model.AimHandAngleOrder = 0f;
			}
		}

		void StartFirearmReload()
		{
			m_isFirearmReloading = true;
			m_firearmReloadTimer = FirearmReloadTime;
			m_isAiming = false;
			m_hasCompletedInitialAim = false;
			// Partículas de inicio de recarga
			if (m_subsystemParticles != null && m_subsystemTerrain != null)
			{
				try
				{
					Vector3 basePosition = m_componentCreature.ComponentBody.Position + new Vector3(0f, 1f, 0f);
					KillParticleSystem reloadParticles = new KillParticleSystem(m_subsystemTerrain, basePosition, 0.5f);
					m_subsystemParticles.AddParticleSystem(reloadParticles, false);
					for (int i = 0; i < 3; i++)
					{
						Vector3 offset = new Vector3(m_random.Float(-0.2f, 0.2f), m_random.Float(0.1f, 0.4f), m_random.Float(-0.2f, 0.2f));
						KillParticleSystem additionalParticles = new KillParticleSystem(m_subsystemTerrain, basePosition + offset, 0.5f);
						m_subsystemParticles.AddParticleSystem(additionalParticles, false);
					}
				}
				catch (Exception)
				{
				}
			}
			m_subsystemAudio.PlaySound("Audio/Armas/reload", 0.8f, 0f, m_componentCreature.ComponentCreatureModel.EyePosition, 10f, true);
			ResetModelRotation();
		}

		void FireSingleProjectile(BulletBlock.BulletType type, Vector3 origin, Vector3 aimDirection, float speed, Vector3 spread, int count)
		{
			int bulletValue = Terrain.MakeBlockValue(BulletBlock.Index, 0, BulletBlock.SetBulletType(0, type));
			Vector3 perp1 = Vector3.Normalize(Vector3.Cross(aimDirection, Vector3.UnitY));
			Vector3 perp2 = Vector3.Normalize(Vector3.Cross(aimDirection, perp1));
			for (int i = 0; i < count; i++)
			{
				Vector3 variant = aimDirection + (m_random.Float(-spread.X, spread.X) * perp1) + (m_random.Float(-spread.Y, spread.Y) * perp2) + (m_random.Float(-spread.Z, spread.Z) * aimDirection);
				Vector3 velocity = m_componentCreature.ComponentBody.Velocity + speed * variant;
				Projectile projectile = m_subsystemProjectiles.FireProjectile(bulletValue, origin, velocity, Vector3.Zero, m_componentCreature);
				if (projectile != null) projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			}
		}

		void FireBuckshot(Vector3 origin, Vector3 aimDirection)
		{
			FireSingleProjectile(BulletBlock.BulletType.BuckshotBall, origin, aimDirection, 80f, new Vector3(0.04f, 0.04f, 0.25f), 8);
		}

		void CancelAiming(IInventory inventory)
		{
			if (m_isAiming)
			{
				int slotValue = inventory.GetSlotValue(inventory.ActiveSlotIndex);
				int contents = Terrain.ExtractContents(slotValue);
				if (contents == MusketBlock.Index || contents == DoubleMusketBlock.Index || contents == FlameThrowerBlock.Index || contents == ItemsLauncherBlock.Index || contents == BowBlock.Index || contents == CrossbowBlock.Index || contents == RepeatCrossbowBlock.Index || IsThrowable(contents) || m_firearmConfigs.ContainsKey(contents))
				{
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 aimDir = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
					// Solo cancelar con miner.Aim si la usamos (no para ItemsLauncher)
					if (contents != ItemsLauncherBlock.Index)
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.Cancelled);
				}
				m_isAiming = false;
				m_aimTimer = 0f;
				if (m_firearmConfigs.ContainsKey(contents))
				{
					m_hasCompletedInitialAim = false;
				}
			}
		}

		void EnsureMusketLoaded(IInventory inventory)
		{
			int slotIndex = inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(slotIndex);
			if (slotValue == 0 || Terrain.ExtractContents(slotValue) != MusketBlock.Index) return;
			int data = Terrain.ExtractData(slotValue);
			MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
			if (loadState != MusketBlock.LoadState.Loaded)
			{
				BulletBlock.BulletType bulletType = GetRandomBulletType();
				data = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
				data = MusketBlock.SetBulletType(data, bulletType);
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(MusketBlock.Index, 0, data), 1);
			}
		}

		void EnsureDoubleMusketLoaded(IInventory inventory)
		{
			int slotIndex = inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(slotIndex);
			int contents = Terrain.ExtractContents(slotValue);
			if (contents != DoubleMusketBlock.Index) return;
			int data = Terrain.ExtractData(slotValue);
			bool loaded = DoubleMusketBlock.IsLoaded(data);
			int shots = DoubleMusketBlock.GetShotsRemaining(data);
			bool isAntiTanks = DoubleMusketBlock.IsAntiTanksBullet(data);
			if (loaded && shots == 2 && isAntiTanks) return;
			data = DoubleMusketBlock.SetLoaded(data, true);
			data = DoubleMusketBlock.SetShotsRemaining(data, 2);
			data = DoubleMusketBlock.SetBulletType(data, null);
			data = DoubleMusketBlock.SetAntiTanksBullet(data, true);
			int newValue = Terrain.MakeBlockValue(DoubleMusketBlock.Index, 0, data);
			inventory.RemoveSlotItems(slotIndex, 1);
			inventory.AddSlotItems(slotIndex, newValue, 1);
		}

		void EnsureFlameThrowerLoaded(IInventory inventory)
		{
			int slotIndex = inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(slotIndex);
			if (Terrain.ExtractContents(slotValue) != FlameThrowerBlock.Index) return;

			int data = Terrain.ExtractData(slotValue);
			FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
			int loadCount = FlameThrowerBlock.GetLoadCount(slotValue);
			FlameBulletBlock.FlameBulletType? bulletType = FlameThrowerBlock.GetBulletType(data);

			if (loadState != FlameThrowerBlock.LoadState.Loaded || bulletType == null || loadCount <= 0)
			{
				FlameBulletBlock.FlameBulletType randomType = s_flameBulletTypes[m_random.Int(0, s_flameBulletTypes.Length - 1)];
				data = FlameThrowerBlock.SetLoadState(data, FlameThrowerBlock.LoadState.Loaded);
				data = FlameThrowerBlock.SetBulletType(data, randomType);
				int newValue = Terrain.MakeBlockValue(FlameThrowerBlock.Index, 15, data);
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, newValue, 1);
			}
		}

		void EnsureBowLoaded(IInventory inventory)
		{
			int slotIndex = inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(slotIndex);
			if (slotValue == 0 || Terrain.ExtractContents(slotValue) != BowBlock.Index) return;
			int data = Terrain.ExtractData(slotValue);
			ArrowBlock.ArrowType? currentArrow = BowBlock.GetArrowType(data);
			int currentDraw = BowBlock.GetDraw(data);
			if (currentArrow != null && currentDraw == 15) return;
			if (currentArrow == null)
			{
				ArrowBlock.ArrowType randomArrowType = s_bowArrowTypes[m_random.Int(0, s_bowArrowTypes.Length - 1)];
				data = BowBlock.SetArrowType(data, randomArrowType);
			}
			data = BowBlock.SetDraw(data, 15);
			int newValue = Terrain.MakeBlockValue(BowBlock.Index, 0, data);
			inventory.RemoveSlotItems(slotIndex, 1);
			inventory.AddSlotItems(slotIndex, newValue, 1);
		}

		void EnsureCrossbowLoaded(IInventory inventory, float distanceToTarget)
		{
			int slotIndex = inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(slotIndex);
			if (slotValue == 0 || Terrain.ExtractContents(slotValue) != CrossbowBlock.Index) return;
			int data = Terrain.ExtractData(slotValue);
			ArrowBlock.ArrowType? currentBolt = CrossbowBlock.GetArrowType(data);
			int currentDraw = CrossbowBlock.GetDraw(data);
			if (currentBolt != null && currentDraw == 15) return;
			if (currentDraw != 15) data = CrossbowBlock.SetDraw(data, 15);
			if (currentBolt == null)
			{
				ArrowBlock.ArrowType randomBoltType;
				if (distanceToTarget >= RangeOfExplosives.X && distanceToTarget <= RangeOfExplosives.Y)
					randomBoltType = s_crossbowBoltTypes[m_random.Int(0, s_crossbowBoltTypes.Length - 1)];
				else
					randomBoltType = s_crossbowBoltTypes[m_random.Int(0, 1)];
				data = CrossbowBlock.SetArrowType(data, randomBoltType);
			}
			int newValue = Terrain.MakeBlockValue(CrossbowBlock.Index, 0, data);
			inventory.RemoveSlotItems(slotIndex, 1);
			inventory.AddSlotItems(slotIndex, newValue, 1);
		}

		void EnsureRepeatCrossbowLoaded(IInventory inventory, float distanceToTarget)
		{
			int slotIndex = inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(slotIndex);
			if (slotValue == 0 || Terrain.ExtractContents(slotValue) != RepeatCrossbowBlock.Index) return;
			int data = Terrain.ExtractData(slotValue);
			int draw = RepeatCrossbowBlock.GetDraw(data);
			RepeatArrowBlock.ArrowType? currentArrow = RepeatCrossbowBlock.GetArrowType(data);
			if (draw == 15 && currentArrow != null) return;
			if (draw != 15) data = RepeatCrossbowBlock.SetDraw(data, 15);
			if (currentArrow == null)
			{
				RepeatArrowBlock.ArrowType randomArrow;
				if (distanceToTarget >= RangeOfExplosives.X && distanceToTarget <= RangeOfExplosives.Y)
				{
					randomArrow = s_repeatArrowTypes[m_random.Int(0, s_repeatArrowTypes.Length - 1)];
				}
				else
				{
					do
					{
						randomArrow = s_repeatArrowTypes[m_random.Int(0, s_repeatArrowTypes.Length - 1)];
					}
					while (randomArrow == RepeatArrowBlock.ArrowType.ExplosiveArrow);
				}
				data = RepeatCrossbowBlock.SetArrowType(data, randomArrow);
			}
			int loadCount = RepeatCrossbowBlock.GetLoadCount(slotValue);
			if (loadCount < 1) loadCount = 1;
			int newValue = Terrain.MakeBlockValue(RepeatCrossbowBlock.Index, loadCount, data);
			inventory.RemoveSlotItems(slotIndex, 1);
			inventory.AddSlotItems(slotIndex, newValue, 1);
		}

		BulletBlock.BulletType GetRandomBulletType()
		{
			int index = m_random.Int(0, 2);
			return (BulletBlock.BulletType)index;
		}

		bool IsTargetInFront(ComponentCreature target)
		{
			Vector3 toTarget = target.ComponentBody.BoundingBox.Center() - m_componentCreature.ComponentBody.Position;
			Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
			return Vector3.Dot(forward, Vector3.Normalize(toTarget)) > 0.5f;
		}

		bool IsTargetVisible(ComponentCreature target)
		{
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = target.ComponentBody.BoundingBox.Center();
			float distance = Vector3.Distance(eyePos, targetCenter);
			Ray3 ray = new Ray3(eyePos, Vector3.Normalize(targetCenter - eyePos));
			BodyRaycastResult? bodyResult = m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, distance + 1f);
			return bodyResult != null && bodyResult.Value.ComponentBody == target.ComponentBody;
		}

		int FindMusketSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == MusketBlock.Index && inventory.GetSlotCount(i) > 0) return i;
			}
			return -1;
		}

		int FindDoubleMusketSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == DoubleMusketBlock.Index && inventory.GetSlotCount(i) > 0) return i;
			}
			return -1;
		}

		int FindFlameThrowerSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == FlameThrowerBlock.Index && inventory.GetSlotCount(i) > 0) return i;
			}
			return -1;
		}

		int FindItemsLauncherSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == ItemsLauncherBlock.Index && inventory.GetSlotCount(i) > 0) return i;
			}
			return -1;
		}

		int FindBowSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == BowBlock.Index && inventory.GetSlotCount(i) > 0) return i;
			}
			return -1;
		}

		int FindCrossbowSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == CrossbowBlock.Index && inventory.GetSlotCount(i) > 0) return i;
			}
			return -1;
		}

		int FindRepeatCrossbowSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == RepeatCrossbowBlock.Index && inventory.GetSlotCount(i) > 0) return i;
			}
			return -1;
		}

		int FindMeleeSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(slotValue);
				if (contents != 0 && contents != MusketBlock.Index && contents != DoubleMusketBlock.Index && contents != FlameThrowerBlock.Index && contents != ItemsLauncherBlock.Index && contents != BowBlock.Index && contents != CrossbowBlock.Index && contents != RepeatCrossbowBlock.Index && contents != BulletBlock.Index && !IsThrowable(contents) && !m_firearmConfigs.ContainsKey(contents) && inventory.GetSlotCount(i) > 0)
					return i;
			}
			return -1;
		}

		void EquipSlot(IInventory inventory, int slotIndex)
		{
			if (inventory.ActiveSlotIndex != slotIndex)
			{
				inventory.ActiveSlotIndex = slotIndex;
				CancelAiming(inventory);
				m_cooldownTimer = 0f;
			}
		}

		IInventory FindClothingInventory()
		{
			ComponentCreatureClothing creatureClothing = Entity.FindComponent<ComponentCreatureClothing>();
			if (creatureClothing != null) return creatureClothing;
			ComponentClothing playerClothing = Entity.FindComponent<ComponentClothing>();
			if (playerClothing != null) return playerClothing;
			return null;
		}
	}
}
