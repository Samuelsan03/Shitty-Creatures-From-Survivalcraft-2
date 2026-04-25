using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class VehicleInventoryWidget : CanvasWidget
	{
		private readonly IInventory m_playerInventory;
		private readonly IInventory m_vehicleInventory;
		private GridPanelWidget m_vehicleGrid;
		private GridPanelWidget m_inventoryGrid;
		private LabelWidget m_vehicleLabel;
		private LabelWidget m_inventoryLabel;

		public VehicleInventoryWidget(IInventory playerInventory, IInventory vehicleInventory)
		{
			m_playerInventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
			m_vehicleInventory = vehicleInventory ?? throw new ArgumentNullException(nameof(vehicleInventory));

			// Cargar la interfaz desde el XML (debe existir "Widgets/VehicleInventoryWidget")
			XElement node = ContentManager.Get<XElement>("Widgets/VehicleInventoryWidget");
			LoadContents(this, node);

			// Referencias a los elementos del widget
			m_vehicleLabel = Children.Find<LabelWidget>("VehicleLabel", true);
			m_inventoryLabel = Children.Find<LabelWidget>("InventoryLabel", true);
			m_vehicleGrid = Children.Find<GridPanelWidget>("VehicleGrid", true);
			m_inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid", true);

			// Asignar textos usando LanguageControl (las claves deben estar en tu JSON)
			if (m_vehicleLabel != null)
				m_vehicleLabel.Text = LanguageControl.GetContentWidgets("VehicleInventoryWidget", "1");   // "Vehículo"
			if (m_inventoryLabel != null)
				m_inventoryLabel.Text = LanguageControl.GetContentWidgets("VehicleInventoryWidget", "2"); // "Inventario"

			// Llenar slots del vehículo (4x4 = 16 slots)
			int slotIndex = 0;
			for (int y = 0; y < m_vehicleGrid.RowsCount; y++)
			{
				for (int x = 0; x < m_vehicleGrid.ColumnsCount; x++)
				{
					var slotWidget = new InventorySlotWidget();
					slotWidget.AssignInventorySlot(vehicleInventory, slotIndex++);
					m_vehicleGrid.Children.Add(slotWidget);
					m_vehicleGrid.SetWidgetCell(slotWidget, new Point2(x, y));
				}
			}

			// Llenar slots del jugador (del 10 al 25, igual que en ChestWidget)
			slotIndex = 10;
			for (int y = 0; y < m_inventoryGrid.RowsCount; y++)
			{
				for (int x = 0; x < m_inventoryGrid.ColumnsCount; x++)
				{
					var slotWidget = new InventorySlotWidget();
					slotWidget.AssignInventorySlot(playerInventory, slotIndex++);
					m_inventoryGrid.Children.Add(slotWidget);
					m_inventoryGrid.SetWidgetCell(slotWidget, new Point2(x, y));
				}
			}
		}

		// Opcional: cerrar el widget si el vehículo ya no existe (necesitarías una referencia a la entidad)
		// Si tienes acceso a la entidad del vehículo, guárdala en el constructor y agrega este Update.
		// Por ahora lo dejamos sin cierre automático.
	}
}
