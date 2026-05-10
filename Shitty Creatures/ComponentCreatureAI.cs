using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureAI : Component, IUpdateable
	{
		// Configurable fields (not from dictionary)
		public Vector2 EngagementRange = new Vector2(3f, 15f);
		public float MusketAimTime = 2f;
		public float MusketCooldown = 2f;

		// Dictionary parameter
		public bool CanUseInventory;

		SubsystemTime m_subsystemTime;

		ComponentCreature m_componentCreature;
		ComponentChaseBehavior m_chaseBehavior;
		ComponentMiner m_componentMiner;

		Random m_random = new Random();

		// Internal aim/cooldown state
		float m_aimTimer;
		bool m_isAiming;
		float m_cooldownTimer;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_chaseBehavior = Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);

			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory");
		}

		public void Update(float dt)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (!CanUseInventory || inventory == null)
				return;

			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = m_chaseBehavior.Target;
			if (target == null || target.ComponentHealth.Health <= 0f)
			{
				CancelAiming(inventory);
				return;
			}

			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);

			int musketSlot = FindMusketSlot(inventory);
			int meleeSlot = FindMeleeSlot(inventory);

			// Choose weapon based on distance
			if (distance > EngagementRange.Y)
			{
				if (musketSlot >= 0)
					EquipSlot(inventory, musketSlot);
				else if (meleeSlot >= 0)
					EquipSlot(inventory, meleeSlot);
			}
			else if (distance < EngagementRange.X)
			{
				if (meleeSlot >= 0)
					EquipSlot(inventory, meleeSlot);
			}
			else
			{
				if (musketSlot >= 0)
					EquipSlot(inventory, musketSlot);
				else if (meleeSlot >= 0)
					EquipSlot(inventory, meleeSlot);
			}

			// Process musket usage
			int activeSlotValue = inventory.GetSlotValue(inventory.ActiveSlotIndex);
			int activeContents = Terrain.ExtractContents(activeSlotValue);
			if (activeContents == MusketBlock.Index)
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
						m_componentMiner.Aim(new Ray3(eyePos, aimDir), AimState.Completed);
						m_isAiming = false;
						m_cooldownTimer = MusketCooldown;
						EnsureMusketLoaded(inventory);
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

		void CancelAiming(IInventory inventory)
		{
			if (m_isAiming)
			{
				int slotValue = inventory.GetSlotValue(inventory.ActiveSlotIndex);
				if (Terrain.ExtractContents(slotValue) == MusketBlock.Index)
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

		BulletBlock.BulletType GetRandomBulletType()
		{
			int index = m_random.Int(0, 2);
			return (BulletBlock.BulletType)index;
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

		int FindMeleeSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(slotValue);
				if (contents != 0 && contents != MusketBlock.Index && contents != BulletBlock.Index && inventory.GetSlotCount(i) > 0)
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
	}
}
