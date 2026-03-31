using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class LargeCraftingTableWidget : CanvasWidget
	{
		private GridPanelWidget m_inventoryGrid;
		private GridPanelWidget m_craftingGrid;
		private InventorySlotWidget m_craftingResultSlot;
		private InventorySlotWidget m_craftingRemainsSlot;
		private ComponentLargeCraftingTable m_componentCraftingTable;

		public LargeCraftingTableWidget(IInventory inventory, ComponentLargeCraftingTable componentCraftingTable)
		{
			m_componentCraftingTable = componentCraftingTable;

			XElement node = ContentManager.Get<XElement>("Widgets/LargeCraftingTableWidget");
			LoadContents(this, node);

			m_inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid", true);
			m_craftingGrid = Children.Find<GridPanelWidget>("CraftingGrid", true);
			m_craftingResultSlot = Children.Find<InventorySlotWidget>("CraftingResultSlot", true);
			m_craftingRemainsSlot = Children.Find<InventorySlotWidget>("CraftingRemainsSlot", true);

			// Inventario del jugador: 4x4 = 16 slots, empezando desde el 10
			int slotIndex = 10;
			for (int i = 0; i < m_inventoryGrid.RowsCount; i++)
			{
				for (int j = 0; j < m_inventoryGrid.ColumnsCount; j++)
				{
					InventorySlotWidget slot = new InventorySlotWidget();
					slot.AssignInventorySlot(inventory, slotIndex++);
					m_inventoryGrid.Children.Add(slot);
					m_inventoryGrid.SetWidgetCell(slot, new Point2(j, i));
				}
			}

			// Cuadrícula de crafteo: 5x5 = 25 slots
			int craftingSlotIndex = 0;
			for (int i = 0; i < m_craftingGrid.RowsCount; i++)
			{
				for (int j = 0; j < m_craftingGrid.ColumnsCount; j++)
				{
					InventorySlotWidget slot = new InventorySlotWidget();
					slot.AssignInventorySlot(m_componentCraftingTable, craftingSlotIndex++);
					m_craftingGrid.Children.Add(slot);
					m_craftingGrid.SetWidgetCell(slot, new Point2(j, i));
				}
			}

			// Asignar slots de resultado y resto
			m_craftingResultSlot.AssignInventorySlot(m_componentCraftingTable, ComponentLargeCraftingTable.ResultSlotIndex);
			m_craftingRemainsSlot.AssignInventorySlot(m_componentCraftingTable, ComponentLargeCraftingTable.RemainsSlotIndex);

			// Ya no se usan eventos; el componente actualiza automáticamente el resultado
			// cuando se modifica la cuadrícula mediante las sobrecargas de Add/RemoveSlotItems.
		}

		public override void Update()
		{
			if (!m_componentCraftingTable.IsAddedToProject)
			{
				ParentWidget.Children.Remove(this);
			}
		}
	}
}
