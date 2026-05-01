using System;
using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentRefrigeratorXiaomi : ComponentInventoryBase, IUpdateable
	{
		public bool PowerOn
		{
			get { return this.m_powerOn || this.GetIsConstantFrozen(); }
			set { this.m_powerOn = value; }
		}

		public ComponentRefrigeratorXiaomi() { }

		// ÚNICO método abstracto requerido
		public override int GetSlotCapacity(int slotIndex, int value)
		{
			return 64;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_componentBlockEntity = base.Entity.FindComponent<ComponentBlockEntity>();

			if (this.m_componentBlockEntity != null)
			{
				this.m_componentBlockEntity.m_inventoryToGatherPickable = this;
			}

			int slotsCount = valuesDictionary.GetValue<int>("SlotsCount", 12);
			base.m_slots = new List<ComponentInventoryBase.Slot>();
			for (int i = 0; i < slotsCount; i++)
			{
				base.m_slots.Add(new ComponentInventoryBase.Slot());
			}
		}

		public void Freeze(bool open) { this.m_powerOn = open; }
		public bool GetIsConstantFrozen() { return false; }

		public UpdateOrder UpdateOrder { get { return UpdateOrder.Default; } }

		public void Update(float dt)
		{
			if (!this.m_powerOn) return;

			double gameTime = this.m_subsystemTime.GameTime;
			if (gameTime < this.m_nextUpdateTime) return;

			if (this.m_componentBlockEntity != null)
			{
				Vector3 position = this.m_componentBlockEntity.Position;
				this.m_subsystemAudio.PlaySound("Audio/Refrigerator/Running Refrigerator", 3f, 0f, position, 0.9f, true);
			}

			this.m_nextUpdateTime = gameTime + 2.1;
		}

		// Campos
		public ComponentBlockEntity m_componentBlockEntity;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemTime m_subsystemTime;
		public double m_nextUpdateTime;
		public bool m_powerOn;
	}
}
