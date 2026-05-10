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
		public float BowAimTime = 1.5f;
		public float BowCooldown = 0.01f;
		public float CrossbowAimTime = 1.5f;
		public float CrossbowCooldown = 0.01f;
		public float ThrowableAimTime = 0.6f;
		public float ThrowableCooldown = 0.8f;
		
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

		// Internal aim/cooldown state
		float m_aimTimer;
		bool m_isAiming;
		float m_cooldownTimer;

		// Clothing equip delay
		int m_pendingClothingValue;
		int m_pendingClothingSlotIndex;
		float m_clothingEquipTimer;

		// Throwable state
		bool m_isThrowing;
		List<int> m_throwableIndices = new List<int>();

		// Tipos de flecha permitidos (solo flechas, sin virotes)
		static readonly ArrowBlock.ArrowType[] s_bowArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.FireArrow,
			ArrowBlock.ArrowType.CopperArrow
		};

		// Tipos de virote para ballesta
		static readonly ArrowBlock.ArrowType[] s_crossbowBoltTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
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

			// Suscribirse a proyectiles para forzar que las flechas y virotes desaparezcan al caer
			m_subsystemProjectiles.ProjectileAdded += OnProjectileAdded;

			// Inicializar lista de objetos lanzables
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
			// Solo si el dueño es esta criatura y es una flecha o virote
			if (projectile.Owner == m_componentCreature)
			{
				int contents = Terrain.ExtractContents(projectile.Value);
				if (contents == ArrowBlock.Index)
				{
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				}
			}
		}

		bool IsThrowable(int contents)
		{
			return m_throwableIndices.Contains(contents);
		}

		public void Update(float dt)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null)
				return;

			// --- Clothing equip logic (independent) ---
			if (CanEquipClothing)
			{
				IInventory clothingInventory = FindClothingInventory();
				if (clothingInventory != null)
				{
					if (m_clothingEquipTimer > 0f)
					{
						m_clothingEquipTimer -= dt;
						if (m_clothingEquipTimer <= 0f)
						{
							// Equip the stored clothing
							clothingInventory.ProcessSlotItems(
								m_pendingClothingSlotIndex,
								m_pendingClothingValue,
								1, 1,
								out int _, out int _);
							m_pendingClothingValue = 0;
						}
					}
					else
					{
						// Search for a wearable clothing item
						for (int i = 0; i < inventory.SlotsCount; i++)
						{
							int slotValue = inventory.GetSlotValue(i);
							if (slotValue != 0)
							{
								Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
								ClothingData clothingData = block.GetClothingData(slotValue);
								if (clothingData != null)
								{
									// Found clothing
									int removed = inventory.RemoveSlotItems(i, 1);
									if (removed > 0)
									{
										// Map clothing slot to inventory slot index
										ClothingSlot clothingSlot = clothingData.Slot;
										int targetSlot = ComponentCreatureClothing.GetClothingSlotIndex(clothingSlot);
										m_pendingClothingValue = slotValue;
										m_pendingClothingSlotIndex = targetSlot;
										m_clothingEquipTimer = 0.55f;
										break; // Only one at a time
									}
								}
							}
						}
					}
				}
			}

			// --- Weapon/musket/bow/crossbow/throwable logic (CanUseInventory) ---
			if (!CanUseInventory)
				return;

			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = m_chaseBehavior.Target;
			if (target == null || target.ComponentHealth.Health <= 0f)
			{
				CancelAiming(inventory);
				m_isThrowing = false;
				return;
			}

			// If stuck, stop all aiming/shooting and don't change weapons
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

			int activeSlotValue = inventory.GetSlotValue(inventory.ActiveSlotIndex);
			int activeContents = Terrain.ExtractContents(activeSlotValue);
			bool isThrowable = IsThrowable(activeContents);

			// Prioridad absoluta para lanzables si estamos en rango y visibles
			if (isThrowable && distance >= ThrowableRange.X && distance <= ThrowableRange.Y && targetVisible)
			{
				// Detener movimiento mientras apuntamos
				if (!m_isThrowing)
				{
					m_isThrowing = true;
					m_componentPathfinding.Stop();
				}

				if (m_cooldownTimer > 0f)
				{
					m_cooldownTimer -= dt;
					if (m_cooldownTimer < 0f)
						m_cooldownTimer = 0f;
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

						// Después de lanzar, permitir movimiento de nuevo
						m_isThrowing = false;
						// Verificar si aún quedan lanzables; si no, se usará otra arma en el próximo ciclo
					}
					else
					{
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.InProgress);
					}
				}
				// Si estamos lanzando, no procesamos otras armas
				return;
			}
			else
			{
				// Si estábamos en modo lanzamiento pero ya no es válido, cancelar
				if (m_isThrowing)
				{
					CancelAiming(inventory);
					m_isThrowing = false;
				}
			}

			// Si se está lanzando, no continuar con otras armas
			if (m_isThrowing)
				return;

			int musketSlot = FindMusketSlot(inventory);
			int bowSlot = FindBowSlot(inventory);
			int crossbowSlot = FindCrossbowSlot(inventory);
			int meleeSlot = FindMeleeSlot(inventory);

			// Choose weapon based on distance and visibility
			if (distance > EngagementRange.Y)
			{
				CancelAiming(inventory);
				if (musketSlot >= 0)
					EquipSlot(inventory, musketSlot);
				else if (bowSlot >= 0)
					EquipSlot(inventory, bowSlot);
				else if (crossbowSlot >= 0)
					EquipSlot(inventory, crossbowSlot);
				else if (meleeSlot >= 0)
					EquipSlot(inventory, meleeSlot);
			}
			else if (distance < EngagementRange.X)
			{
				if (targetVisible && meleeSlot >= 0)
				{
					CancelAiming(inventory);
					EquipSlot(inventory, meleeSlot);
				}
				else if (musketSlot >= 0)
				{
					EquipSlot(inventory, musketSlot);
				}
				else if (bowSlot >= 0)
				{
					EquipSlot(inventory, bowSlot);
				}
				else if (crossbowSlot >= 0)
				{
					EquipSlot(inventory, crossbowSlot);
				}
			}
			else
			{
				if (musketSlot >= 0)
					EquipSlot(inventory, musketSlot);
				else if (bowSlot >= 0)
					EquipSlot(inventory, bowSlot);
				else if (crossbowSlot >= 0)
					EquipSlot(inventory, crossbowSlot);
				else if (meleeSlot >= 0)
					EquipSlot(inventory, meleeSlot);
			}

			// Process current weapon usage (only when target is visible and in range)
			activeSlotValue = inventory.GetSlotValue(inventory.ActiveSlotIndex);
			activeContents = Terrain.ExtractContents(activeSlotValue);
			if (activeContents == MusketBlock.Index && targetVisible)
			{
				EnsureMusketLoaded(inventory);

				if (m_cooldownTimer > 0f)
				{
					m_cooldownTimer -= dt;
					if (m_cooldownTimer < 0f)
						m_cooldownTimer = 0f;
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

					if (m_aimTimer >= MusketAimTime)
					{
						BulletBlock.BulletType? currentBulletType = MusketBlock.GetBulletType(
							Terrain.ExtractData(inventory.GetSlotValue(inventory.ActiveSlotIndex)));

						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.Completed);
						m_isAiming = false;
						m_cooldownTimer = MusketCooldown;
						EnsureMusketLoaded(inventory);

						if (m_random.Float(0f, 1f) < 0.05f)
						{
							Vector3 musketPos = eyePos + m_componentCreature.ComponentBody.Matrix.Right * 0.3f
								- m_componentCreature.ComponentBody.Matrix.Up * 0.2f;
							Vector3 musketDir = Vector3.Normalize(musketPos + aimDir * 10f - musketPos);

							if (currentBulletType != BulletBlock.BulletType.MusketBall)
								FireSingleProjectile(BulletBlock.BulletType.MusketBall, musketPos, musketDir, 120f, Vector3.Zero, 1);
							if (currentBulletType != BulletBlock.BulletType.Buckshot)
								FireBuckshot(musketPos, musketDir);
							if (currentBulletType != BulletBlock.BulletType.BuckshotBall)
								FireSingleProjectile(BulletBlock.BulletType.BuckshotBall, musketPos, musketDir, 60f, new Vector3(0.06f, 0.06f, 0f), 1);
						}
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

				if (m_cooldownTimer > 0f)
				{
					m_cooldownTimer -= dt;
					if (m_cooldownTimer < 0f)
						m_cooldownTimer = 0f;
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

					if (m_aimTimer >= BowAimTime)
					{
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.Completed);
						m_isAiming = false;
						m_cooldownTimer = BowCooldown;
					}
					else
					{
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.InProgress);
					}
				}
			}
			else if (activeContents == CrossbowBlock.Index && targetVisible)
			{
				float distToTarget = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					target.ComponentBody.BoundingBox.Center());
				EnsureCrossbowLoaded(inventory, distToTarget);

				if (m_cooldownTimer > 0f)
				{
					m_cooldownTimer -= dt;
					if (m_cooldownTimer < 0f)
						m_cooldownTimer = 0f;
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

					if (m_aimTimer >= CrossbowAimTime)
					{
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.Completed);
						m_isAiming = false;
						m_cooldownTimer = CrossbowCooldown;
					}
					else
					{
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.InProgress);
					}
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
				Vector3 variant = aimDirection +
					(m_random.Float(-spread.X, spread.X) * perp1) +
					(m_random.Float(-spread.Y, spread.Y) * perp2) +
					(m_random.Float(-spread.Z, spread.Z) * aimDirection);
				Vector3 velocity = m_componentCreature.ComponentBody.Velocity + speed * variant;
				Projectile projectile = m_subsystemProjectiles.FireProjectile(
					bulletValue, origin, velocity, Vector3.Zero, m_componentCreature);
				if (projectile != null)
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			}
		}

		void FireBuckshot(Vector3 origin, Vector3 aimDirection)
		{
			FireSingleProjectile(BulletBlock.BulletType.BuckshotBall, origin, aimDirection, 80f,
				new Vector3(0.04f, 0.04f, 0.25f), 8);
		}

		void CancelAiming(IInventory inventory)
		{
			if (m_isAiming)
			{
				int slotValue = inventory.GetSlotValue(inventory.ActiveSlotIndex);
				int contents = Terrain.ExtractContents(slotValue);
				if (contents == MusketBlock.Index || contents == BowBlock.Index || contents == CrossbowBlock.Index || IsThrowable(contents))
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
			if (slotValue == 0 || Terrain.ExtractContents(slotValue) != MusketBlock.Index)
				return;

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

		void EnsureBowLoaded(IInventory inventory)
		{
			int slotIndex = inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(slotIndex);
			if (slotValue == 0 || Terrain.ExtractContents(slotValue) != BowBlock.Index)
				return;

			int data = Terrain.ExtractData(slotValue);
			ArrowBlock.ArrowType? currentArrow = BowBlock.GetArrowType(data);
			int currentDraw = BowBlock.GetDraw(data);

			if (currentArrow != null && currentDraw == 15)
				return;

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
			if (slotValue == 0 || Terrain.ExtractContents(slotValue) != CrossbowBlock.Index)
				return;

			int data = Terrain.ExtractData(slotValue);
			ArrowBlock.ArrowType? currentBolt = CrossbowBlock.GetArrowType(data);
			int currentDraw = CrossbowBlock.GetDraw(data);

			if (currentBolt != null && currentDraw == 15)
				return;

			if (currentDraw != 15)
				data = CrossbowBlock.SetDraw(data, 15);

			if (currentBolt == null)
			{
				ArrowBlock.ArrowType randomBoltType;
				if (distanceToTarget >= RangeOfExplosives.X && distanceToTarget <= RangeOfExplosives.Y)
				{
					randomBoltType = s_crossbowBoltTypes[m_random.Int(0, s_crossbowBoltTypes.Length - 1)];
				}
				else
				{
					randomBoltType = s_crossbowBoltTypes[m_random.Int(0, 1)];
				}
				data = CrossbowBlock.SetArrowType(data, randomBoltType);
			}

			int newValue = Terrain.MakeBlockValue(CrossbowBlock.Index, 0, data);
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

			BodyRaycastResult? bodyResult = m_componentMiner.Raycast<BodyRaycastResult>(
				ray, RaycastMode.Interaction, true, true, true, distance + 1f);

			return bodyResult != null && bodyResult.Value.ComponentBody == target.ComponentBody;
		}

		int FindMusketSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == MusketBlock.Index && inventory.GetSlotCount(i) > 0)
					return i;
			}
			return -1;
		}

		int FindBowSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == BowBlock.Index && inventory.GetSlotCount(i) > 0)
					return i;
			}
			return -1;
		}

		int FindCrossbowSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == CrossbowBlock.Index && inventory.GetSlotCount(i) > 0)
					return i;
			}
			return -1;
		}

		int FindMeleeSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(slotValue);
				if (contents != 0 && contents != MusketBlock.Index && contents != BowBlock.Index && contents != CrossbowBlock.Index && contents != BulletBlock.Index && !IsThrowable(contents) && inventory.GetSlotCount(i) > 0)
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
