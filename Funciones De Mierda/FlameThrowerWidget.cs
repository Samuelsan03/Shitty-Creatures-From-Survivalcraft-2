using System;
using System.Xml.Linq;
using Engine;
using Game;

namespace Game
{
	public class FlameThrowerWidget : CanvasWidget
	{
		public FlameThrowerWidget(IInventory inventory, int slotIndex)
		{
			this.m_inventory = inventory;
			this.m_slotIndex = slotIndex;
			XElement node = ContentManager.Get<XElement>("Widgets/FlameThrowerWidget");
			this.LoadContents(this, node);
			this.m_inventoryGrid = this.Children.Find<GridPanelWidget>("InventoryGrid", true);
			this.m_inventorySlotWidget = this.Children.Find<InventorySlotWidget>("InventorySlot", true);
			this.m_instructionsLabel = this.Children.Find<LabelWidget>("InstructionsLabel", true);
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
			this.m_inventorySlotWidget.CustomViewMatrix = new Matrix?(Matrix.CreateScale(0.8f) * Matrix.CreateLookAt(new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, -0.2f), -Vector3.UnitZ));
		}

		public override void Update()
		{
			int slotValue = this.m_inventory.GetSlotValue(this.m_slotIndex);
			int slotCount = this.m_inventory.GetSlotCount(this.m_slotIndex);
			if (Terrain.ExtractContents(slotValue) != FlameThrowerBlock.Index || slotCount <= 0)
			{
				base.ParentWidget.Children.Remove(this);
				return;
			}
			FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(Terrain.ExtractData(slotValue));
			if (loadState == FlameThrowerBlock.LoadState.Empty)
			{
				this.m_instructionsLabel.Text = "Load ammunition";
				this.m_instructionsLabel.Color = Color.White;
				return;
			}
			if (loadState != FlameThrowerBlock.LoadState.Loaded)
			{
				this.m_instructionsLabel.Text = string.Empty;
				return;
			}

			// Solo hay balas de fuego ahora
			this.m_instructionsLabel.Text = "Prepare to spray fire " + FlameThrowerBlock.GetLoadCount(slotValue).ToString() + "/15";
			this.m_instructionsLabel.Color = Color.White;
		}

		public IInventory m_inventory;
		public int m_slotIndex;
		public GridPanelWidget m_inventoryGrid;
		public InventorySlotWidget m_inventorySlotWidget;
		public LabelWidget m_instructionsLabel;
	}
}
