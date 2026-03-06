using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureInventory : ComponentInventory
	{
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Asegurar que ActiveSlotIndex existe
			if (!valuesDictionary.ContainsKey("ActiveSlotIndex"))
				valuesDictionary.SetValue<int>("ActiveSlotIndex", 0);

			// Forzar SlotsCount a 16 si no se especifica en XDB
			if (!valuesDictionary.ContainsKey("SlotsCount"))
				valuesDictionary.SetValue<int>("SlotsCount", 16);

			base.Load(valuesDictionary, idToEntityMap);

			// Todas las ranuras son visibles
			VisibleSlotsCount = SlotsCount;
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
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
