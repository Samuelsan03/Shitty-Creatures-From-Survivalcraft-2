using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureInventory : ComponentInventory
	{
		private int m_activeSlotIndex;

		public override int ActiveSlotIndex
		{
			get { return m_activeSlotIndex; }
			set { m_activeSlotIndex = Math.Clamp(value, 0, Math.Max(0, SlotsCount - 1)); }
		}

		public override int VisibleSlotsCount
		{
			get { return SlotsCount; }
			set { /* Ignorar */ }
		}

		public override int GetSlotCapacity(int slotIndex, int value)
		{
			if (slotIndex < 0 || slotIndex >= SlotsCount)
				return 0;
			return BlocksManager.Blocks[Terrain.ExtractContents(value)].GetMaxStacking(value);
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Asegurar que SlotsCount sea 16
			int slotsCount = 16;
			if (valuesDictionary.ContainsKey("SlotsCount"))
				slotsCount = valuesDictionary.GetValue<int>("SlotsCount");
			else
				valuesDictionary.SetValue<int>("SlotsCount", slotsCount);

			// Asegurar ActiveSlotIndex
			if (!valuesDictionary.ContainsKey("ActiveSlotIndex"))
				valuesDictionary.SetValue<int>("ActiveSlotIndex", 0);

			// Asegurar que Slots existe
			if (!valuesDictionary.ContainsKey("Slots"))
			{
				var slotsDict = new ValuesDictionary();
				valuesDictionary.SetValue<ValuesDictionary>("Slots", slotsDict);
			}

			base.Load(valuesDictionary, idToEntityMap);

			m_activeSlotIndex = valuesDictionary.GetValue<int>("ActiveSlotIndex");
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<int>("ActiveSlotIndex", m_activeSlotIndex);
			base.Save(valuesDictionary, entityToIdMap);
		}

		public void OpenInventory(ComponentPlayer player)
		{
			if (player?.ComponentGui != null && player.ComponentMiner?.Inventory != null)
			{
				player.ComponentGui.ModalPanelWidget = new CreatureInventoryWidget(player.ComponentMiner.Inventory, this);
				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
			}
		}
	}
}
