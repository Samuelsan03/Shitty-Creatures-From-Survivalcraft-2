using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class CannonWidget : CanvasWidget
	{
		public CannonWidget(IInventory inventory, int slotIndex)
		{
			this.m_inventory = inventory;
			this.m_slotIndex = slotIndex;
			XElement node = ContentManager.Get<XElement>("Widgets/CannonWidget");
			this.LoadContents(this, node);
			this.m_inventoryGrid = this.Children.Find<GridPanelWidget>("InventoryGrid", true);
			this.m_inventorySlotWidget = this.Children.Find<InventorySlotWidget>("InventorySlot", true);
			this.m_instructionsLabel = this.Children.Find<LabelWidget>("InstructionsLabel", true);

			// Asignación explícita de los textos en C# usando LanguageControl para las claves 1 y 2
			int labelIndex = 0;
			foreach (Widget child in this.Children)
			{
				if (child is LabelWidget label)
				{
					if (labelIndex == 0)
					{
						label.Text = LanguageControl.Get(CannonWidget.fName, 1);
					}
					else if (labelIndex == 1)
					{
						label.Text = LanguageControl.Get(CannonWidget.fName, 2);
					}
					labelIndex++;
				}
			}

			for (int i = 0; i < this.m_inventoryGrid.RowsCount; i++)
			{
				for (int j = 0; j < this.m_inventoryGrid.ColumnsCount; j++)
				{
					InventorySlotWidget widget = new InventorySlotWidget();
					this.m_inventoryGrid.Children.Add(widget);
					this.m_inventoryGrid.SetWidgetCell(widget, new Point2(j, i));
				}
			}
			int num = 10;
			foreach (Widget widget2 in this.m_inventoryGrid.Children)
			{
				InventorySlotWidget inventorySlotWidget = widget2 as InventorySlotWidget;
				if (inventorySlotWidget != null)
				{
					inventorySlotWidget.AssignInventorySlot(inventory, num++);
				}
			}
			this.m_inventorySlotWidget.AssignInventorySlot(inventory, slotIndex);
			this.m_inventorySlotWidget.CustomViewMatrix = new Matrix?(Matrix.CreateLookAt(new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 0f), -Vector3.UnitZ));
		}

		public override void Update()
		{
			int slotValue = this.m_inventory.GetSlotValue(this.m_slotIndex);
			int slotCount = this.m_inventory.GetSlotCount(this.m_slotIndex);
			if (Terrain.ExtractContents(slotValue) != CannonBlock.Index || slotCount <= 0)
			{
				base.ParentWidget.Children.Remove(this);
				return;
			}
			switch (CannonBlock.GetLoadState(Terrain.ExtractData(slotValue)))
			{
				case CannonBlock.LoadState.Empty:
					this.m_instructionsLabel.Text = LanguageControl.Get(CannonWidget.fName, 0);
					return;
				case CannonBlock.LoadState.Loaded:
					this.m_instructionsLabel.Text = LanguageControl.Get(CannonWidget.fName, 3);
					return;
				default:
					this.m_instructionsLabel.Text = string.Empty;
					return;
			}
		}

		public IInventory m_inventory;
		public int m_slotIndex;
		public GridPanelWidget m_inventoryGrid;
		public InventorySlotWidget m_inventorySlotWidget;
		public LabelWidget m_instructionsLabel;
		public static string fName = "CannonWidget";
	}
}
