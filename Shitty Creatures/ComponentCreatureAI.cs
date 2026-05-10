using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureAI : Component, IUpdateable
	{
		// Configurable fields (not from dictionary)
		public Vector2 EngagementRange = new Vector2(5f, 100f);
		public float MusketAimTime = 1.5f;
		public float MusketCooldown = 0.5f;
		public float BowAimTime = 1.5f;
		public float BowCooldown = 0.01f;

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

			// Suscribirse a proyectiles para forzar que las flechas desaparezcan al caer
			m_subsystemProjectiles.ProjectileAdded += OnProjectileAdded;
		}

		void OnProjectileAdded(Projectile projectile)
		{
			// Solo si el dueño es esta criatura y es una flecha (no virote)
			if (projectile.Owner == m_componentCreature)
			{
				int contents = Terrain.ExtractContents(projectile.Value);
				if (contents == ArrowBlock.Index)
				{
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				}
			}
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

			// --- Weapon/musket/bow logic (CanUseInventory) ---
			if (!CanUseInventory)
				return;

			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = m_chaseBehavior.Target;
			if (target == null || target.ComponentHealth.Health <= 0f)
			{
				CancelAiming(inventory);
				return;
			}

			// If stuck, stop all aiming/shooting and don't change weapons
			if (m_componentPathfinding.IsStuck)
			{
				CancelAiming(inventory);
				return;
			}

			Vector3 creaturePos = m_componentCreature.ComponentBody.Position;
			Vector3 targetPos = target.ComponentBody.BoundingBox.Center();
			float distance = Vector3.Distance(creaturePos, targetPos);

			bool targetInFront = IsTargetInFront(target);
			bool targetVisible = targetInFront && IsTargetVisible(target);

			int musketSlot = FindMusketSlot(inventory);
			int bowSlot = FindBowSlot(inventory);
			int meleeSlot = FindMeleeSlot(inventory);

			// Choose weapon based on distance and visibility
			if (distance > EngagementRange.Y)
			{
				CancelAiming(inventory);
				if (musketSlot >= 0)
					EquipSlot(inventory, musketSlot);
				else if (bowSlot >= 0)
					EquipSlot(inventory, bowSlot);
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
			}
			else
			{
				if (musketSlot >= 0)
					EquipSlot(inventory, musketSlot);
				else if (bowSlot >= 0)
					EquipSlot(inventory, bowSlot);
				else if (meleeSlot >= 0)
					EquipSlot(inventory, meleeSlot);
			}

			// Process current weapon usage (only when target is visible and in range)
			int activeSlotValue = inventory.GetSlotValue(inventory.ActiveSlotIndex);
			int activeContents = Terrain.ExtractContents(activeSlotValue);
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
				if (contents == MusketBlock.Index || contents == BowBlock.Index)
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
				// Solo tipos de flecha reales (sin virotes)
				ArrowBlock.ArrowType randomArrowType = s_bowArrowTypes[m_random.Int(0, s_bowArrowTypes.Length - 1)];
				data = BowBlock.SetArrowType(data, randomArrowType);
			}

			data = BowBlock.SetDraw(data, 15);
			int newValue = Terrain.MakeBlockValue(BowBlock.Index, 0, data);
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

		int FindMeleeSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(slotValue);
				if (contents != 0 && contents != MusketBlock.Index && contents != BowBlock.Index && contents != BulletBlock.Index && inventory.GetSlotCount(i) > 0)
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
