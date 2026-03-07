using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class CreatureInventoryWidget : CanvasWidget
	{
		private ComponentCreatureInventory m_creatureInventory;
		private GridPanelWidget m_creatureGrid;
		private GridPanelWidget m_inventoryGrid;

		public CreatureInventoryWidget(IInventory playerInventory, ComponentCreatureInventory creatureInventory)
		{
			m_creatureInventory = creatureInventory;
			XElement node = ContentManager.Get<XElement>("Widgets/CreatureInventoryWidget");
			LoadContents(this, node);

			m_creatureGrid = Children.Find<GridPanelWidget>("CreatureGrid", true);
			m_inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid", true);

			// Configurar etiquetas con LanguageControl desde ContentWidgets
			LabelWidget creatureLabel = Children.Find<LabelWidget>("CreatureInventoryLabel", true);
			LabelWidget inventoryLabel = Children.Find<LabelWidget>("InventoryLabel", true);

			creatureLabel.Text = LanguageControl.GetContentWidgets("CreatureInventoryWidget", "CreatureInventoryLabel");
			inventoryLabel.Text = LanguageControl.GetContentWidgets("CreatureInventoryWidget", "InventoryLabel");

			// Fallback por si no encuentra la traducción
			if (string.IsNullOrEmpty(creatureLabel.Text) || creatureLabel.Text.Contains("CreatureInventoryWidget"))
				creatureLabel.Text = "Creature Inventory";
			if (string.IsNullOrEmpty(inventoryLabel.Text) || inventoryLabel.Text.Contains("CreatureInventoryWidget"))
				inventoryLabel.Text = "Inventory";

			int creatureSlots = creatureInventory.SlotsCount; // Debe ser 16

			// Ajustar cuadrícula de la criatura (4 columnas fijas)
			int columns = 4;
			int rows = (creatureSlots + columns - 1) / columns;
			m_creatureGrid.RowsCount = rows;
			m_creatureGrid.ColumnsCount = columns;

			int slotIndex = 0;
			for (int row = 0; row < rows; row++)
			{
				for (int col = 0; col < columns; col++)
				{
					if (slotIndex >= creatureSlots) break;
					InventorySlotWidget slot = new InventorySlotWidget();
					slot.AssignInventorySlot(creatureInventory, slotIndex);
					m_creatureGrid.Children.Add(slot);
					m_creatureGrid.SetWidgetCell(slot, new Point2(col, row));
					slotIndex++;
				}
			}

			// Inventario del jugador (slots desde el índice 10, 4x4 = 16 slots)
			int playerStartSlot = 10;
			for (int row = 0; row < 4; row++)
			{
				for (int col = 0; col < 4; col++)
				{
					int idx = playerStartSlot + row * 4 + col;
					if (idx >= playerInventory.SlotsCount) break;
					InventorySlotWidget slot = new InventorySlotWidget();
					slot.AssignInventorySlot(playerInventory, idx);
					m_inventoryGrid.Children.Add(slot);
					m_inventoryGrid.SetWidgetCell(slot, new Point2(col, row));
				}
			}
		}

		public override void Update()
		{
			if (!m_creatureInventory.IsAddedToProject)
				ParentWidget.Children.Remove(this);
		}
	}
}
