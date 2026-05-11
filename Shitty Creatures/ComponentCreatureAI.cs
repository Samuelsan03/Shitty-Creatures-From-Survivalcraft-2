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
		public float MusketCooldown = 0.5f;
		public float DoubleMusketAimTime = 1.5f;
		public float DoubleMusketCooldown = 0.5f;
		public float FlameThrowerAimTime = 1.5f;
		public float FlameThrowerCooldown = 0.01f;
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

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);

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

			int musketSlot = FindMusketSlot(inventory);
			int doubleMusketSlot = FindDoubleMusketSlot(inventory);
			int flameThrowerSlot = FindFlameThrowerSlot(inventory);
			int bowSlot = FindBowSlot(inventory);
			int crossbowSlot = FindCrossbowSlot(inventory);
			int repeatCrossbowSlot = FindRepeatCrossbowSlot(inventory);
			int meleeSlot = FindMeleeSlot(inventory);

			if (distance > EngagementRange.Y)
			{
				CancelAiming(inventory);
				if (musketSlot >= 0) EquipSlot(inventory, musketSlot);
				else if (doubleMusketSlot >= 0) EquipSlot(inventory, doubleMusketSlot);
				else if (flameThrowerSlot >= 0) EquipSlot(inventory, flameThrowerSlot);
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
				else if (musketSlot >= 0) EquipSlot(inventory, musketSlot);
				else if (doubleMusketSlot >= 0) EquipSlot(inventory, doubleMusketSlot);
				else if (flameThrowerSlot >= 0) EquipSlot(inventory, flameThrowerSlot);
				else if (bowSlot >= 0) EquipSlot(inventory, bowSlot);
				else if (crossbowSlot >= 0) EquipSlot(inventory, crossbowSlot);
				else if (repeatCrossbowSlot >= 0) EquipSlot(inventory, repeatCrossbowSlot);
			}
			else
			{
				if (musketSlot >= 0) EquipSlot(inventory, musketSlot);
				else if (doubleMusketSlot >= 0) EquipSlot(inventory, doubleMusketSlot);
				else if (flameThrowerSlot >= 0) EquipSlot(inventory, flameThrowerSlot);
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
			else
			{
				CancelAiming(inventory);
			}
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
				if (contents == MusketBlock.Index || contents == DoubleMusketBlock.Index || contents == FlameThrowerBlock.Index || contents == BowBlock.Index || contents == CrossbowBlock.Index || contents == RepeatCrossbowBlock.Index || IsThrowable(contents))
				{
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 aimDir = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
					m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.Cancelled);
				}
				m_isAiming = false;
				m_aimTimer = 0f;
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

			// Si está vacío o sin tipo de bala, recargar completamente variando entre fuego y veneno
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
				if (contents != 0 && contents != MusketBlock.Index && contents != DoubleMusketBlock.Index && contents != FlameThrowerBlock.Index && contents != BowBlock.Index && contents != CrossbowBlock.Index && contents != RepeatCrossbowBlock.Index && contents != BulletBlock.Index && !IsThrowable(contents) && inventory.GetSlotCount(i) > 0)
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
