using System;
using System.Xml.Linq;
using Engine;
using Game;

namespace Game
{
	public class RefrigeratorXiaomiWidget : CanvasWidget
	{
		public RefrigeratorXiaomiWidget(IInventory inventory, ComponentRefrigeratorXiaomi componentRefrigerator)
		{
			this.m_componentRefrigerator = componentRefrigerator;
			XElement node = ContentManager.Get<XElement>("Widgets/RefrigeratorWidget");
			this.LoadContents(this, node);

			// Obtener referencias a los widgets
			this.m_refrigeratorLabel = this.Children.Find<LabelWidget>("RefrigeratorLabel", true);
			this.m_inventoryLabel = this.Children.Find<LabelWidget>("InventoryLabel", true);
			this.m_statusLabel = this.Children.Find<LabelWidget>("StatusLabel", true);
			this.m_inventoryGrid = this.Children.Find<GridPanelWidget>("InventoryGrid", true);
			this.m_chestGrid = this.Children.Find<GridPanelWidget>("ChestGrid", true);

			// Establecer textos localizados
			if (this.m_refrigeratorLabel != null)
				this.m_refrigeratorLabel.Text = LanguageControl.Get("RefrigeratorXiaomi", "RefrigeratorLabel");
			if (this.m_inventoryLabel != null)
				this.m_inventoryLabel.Text = LanguageControl.Get("RefrigeratorXiaomi", "InventoryLabel");

			int num = 0;
			for (int i = 0; i < this.m_chestGrid.RowsCount; i++)
			{
				for (int j = 0; j < this.m_chestGrid.ColumnsCount; j++)
				{
					InventorySlotWidget inventorySlotWidget = new InventorySlotWidget();
					inventorySlotWidget.AssignInventorySlot(componentRefrigerator, num++);
					this.m_chestGrid.Children.Add(inventorySlotWidget);
					this.m_chestGrid.SetWidgetCell(inventorySlotWidget, new Point2(j, i));
				}
			}

			num = 10;
			for (int k = 0; k < this.m_inventoryGrid.RowsCount; k++)
			{
				for (int l = 0; l < this.m_inventoryGrid.ColumnsCount; l++)
				{
					InventorySlotWidget inventorySlotWidget2 = new InventorySlotWidget();
					inventorySlotWidget2.AssignInventorySlot(inventory, num++);
					this.m_inventoryGrid.Children.Add(inventorySlotWidget2);
					this.m_inventoryGrid.SetWidgetCell(inventorySlotWidget2, new Point2(l, k));
				}
			}
		}

		public override void Update()
		{
			if (this.m_componentRefrigerator.PowerOn)
			{
				this.m_statusLabel.Text = LanguageControl.Get("RefrigeratorXiaomi", "CoolingStatus");
				this.m_statusLabel.Color = Color.DarkGreen;
			}
			else
			{
				this.m_statusLabel.Text = LanguageControl.Get("RefrigeratorXiaomi", "NoPowerStatus");
				this.m_statusLabel.Color = Color.DarkRed;
			}

			if (!this.m_componentRefrigerator.IsAddedToProject)
			{
				base.ParentWidget.Children.Remove(this);
			}
		}

		// Campos
		public ComponentRefrigeratorXiaomi m_componentRefrigerator;
		public LabelWidget m_refrigeratorLabel;
		public LabelWidget m_inventoryLabel;
		public LabelWidget m_statusLabel;
		public GridPanelWidget m_inventoryGrid;
		public GridPanelWidget m_chestGrid;
	}
}
