using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class DoubleMusketWidget : CanvasWidget
	{
		private IInventory m_inventory;
		private int m_slotIndex;
		private GridPanelWidget m_inventoryGrid;
		private InventorySlotWidget m_inventorySlotWidget;
		private LabelWidget m_instructionsLabel;

		public static string fName = "DoubleMusketWidget";

		public DoubleMusketWidget(IInventory inventory, int slotIndex)
		{
			m_inventory = inventory;
			m_slotIndex = slotIndex;
			XElement node = ContentManager.Get<XElement>("Widgets/DoubleMusketWidget");
			LoadContents(this, node);
			m_inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid", true);
			m_inventorySlotWidget = Children.Find<InventorySlotWidget>("InventorySlot", true);
			m_instructionsLabel = Children.Find<LabelWidget>("InstructionsLabel", true);

			for (int i = 0; i < m_inventoryGrid.RowsCount; i++)
			{
				for (int j = 0; j < m_inventoryGrid.ColumnsCount; j++)
				{
					InventorySlotWidget widget = new InventorySlotWidget();
					m_inventoryGrid.Children.Add(widget);
					m_inventoryGrid.SetWidgetCell(widget, new Point2(j, i));
				}
			}
			int num = 10;
			foreach (Widget widget in m_inventoryGrid.Children)
			{
				if (widget is InventorySlotWidget slotWidget)
					slotWidget.AssignInventorySlot(inventory, num++);
			}
			m_inventorySlotWidget.AssignInventorySlot(inventory, slotIndex);
			m_inventorySlotWidget.CustomViewMatrix = Matrix.CreateLookAt(new Vector3(1f, 0f, 0f), Vector3.Zero, -Vector3.UnitZ);
		}

		public override void Update()
		{
			int slotValue = m_inventory.GetSlotValue(m_slotIndex);
			int slotCount = m_inventory.GetSlotCount(m_slotIndex);
			if (Terrain.ExtractContents(slotValue) != DoubleMusketBlock.Index || slotCount <= 0)
			{
				ParentWidget.Children.Remove(this);
				return;
			}

			int data = Terrain.ExtractData(slotValue);
			bool isLoaded = DoubleMusketBlock.IsLoaded(data);
			int shotsRemaining = DoubleMusketBlock.GetShotsRemaining(data);

			if (isLoaded && shotsRemaining > 0)
				m_instructionsLabel.Text = string.Format(LanguageControl.GetContentWidgets(fName, 3), shotsRemaining);
			else
				m_instructionsLabel.Text = LanguageControl.GetContentWidgets(fName, 0);
		}
	}
}
