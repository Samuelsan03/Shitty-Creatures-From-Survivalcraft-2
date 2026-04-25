using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentVehicleInventory : ComponentInventoryBase
	{
		public const int SlotCount = 16;

		public override int VisibleSlotsCount => SlotCount;

		public override int ActiveSlotIndex { get; set; } = -1;

		public void OpenInventory(ComponentPlayer player)
		{
			if (player == null) return;
			player.ComponentGui.ModalPanelWidget = new VehicleInventoryWidget(
				player.ComponentMiner.Inventory,
				this
			);
			AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			valuesDictionary.SetValue<int>("SlotsCount", SlotCount);
			base.Load(valuesDictionary, idToEntityMap);
		}
	}
}
