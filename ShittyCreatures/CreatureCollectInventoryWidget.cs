using System;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Engine;

namespace Game
{
	// Token: 0x0200000A RID: 10
	[NullableContext(1)]
	[Nullable(0)]
	public class CreatureCollectInventoryWidget : CanvasWidget
	{
		// Token: 0x0600006A RID: 106 RVA: 0x00006C08 File Offset: 0x00004E08
		public CreatureCollectInventoryWidget(IInventory inventory)
		{
			this.m_inventory = inventory;
			XElement xelement = ContentManager.Get<XElement>("Widgets/CreatureCollectInventoryWidget");
			this.LoadContents(this, xelement);
			this.m_inventoryGrid = this.Children.Find<GridPanelWidget>("InventoryGrid", true);
			int slotsCount = this.m_inventory.SlotsCount;
			int num = Math.Min(slotsCount, 4);
			int num2 = (slotsCount + 4 - 1) / 4;
			this.m_inventoryGrid.ColumnsCount = num;
			this.m_inventoryGrid.RowsCount = num2;
			int num3 = 0;
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					bool flag = num3 >= slotsCount;
					bool flag2 = flag;
					if (flag2)
					{
						break;
					}
					InventorySlotWidget inventorySlotWidget = new InventorySlotWidget();
					inventorySlotWidget.AssignInventorySlot(this.m_inventory, num3++);
					this.m_inventoryGrid.Children.Add(inventorySlotWidget);
					this.m_inventoryGrid.SetWidgetCell(inventorySlotWidget, new Point2(j, i));
				}
			}
		}

		// Token: 0x0600006B RID: 107 RVA: 0x00006D18 File Offset: 0x00004F18
		public override void Update()
		{
			ComponentInventory componentInventory = this.m_inventory as ComponentInventory;
			bool flag = componentInventory != null && !componentInventory.IsAddedToProject;
			bool flag2 = flag;
			if (flag2)
			{
				base.ParentWidget.Children.Remove(this);
			}
		}

		// Token: 0x0400007E RID: 126
		private readonly IInventory m_inventory;

		// Token: 0x0400007F RID: 127
		private readonly GridPanelWidget m_inventoryGrid;
	}
}
