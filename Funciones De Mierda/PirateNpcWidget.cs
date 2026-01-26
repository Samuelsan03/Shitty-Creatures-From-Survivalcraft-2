using System;
using System.Globalization;
using System.Xml.Linq;
using Engine;
using Game;

namespace WonderfulEra
{
	// Token: 0x0200011C RID: 284
	public class PirateNpcWidget : CanvasWidget
	{
		// Token: 0x0600087B RID: 2171 RVA: 0x0003F724 File Offset: 0x0003D924
		public PirateNpcWidget(IInventory inventory, ComponentTradeFlushInventory tradeFlushInventory)
		{
			this.m_tradeFlushInventory = tradeFlushInventory;
			this.m_inventory = inventory;
			XElement node = ContentManager.Get<XElement>("Widgets/PirateNpcWidget");
			this.LoadContents(this, node);
			this.m_inventoryGrid = this.Children.Find<GridPanelWidget>("InventoryGrid", true);
			this.m_chestGrid = this.Children.Find<GridPanelWidget>("PirateNpcGrid", true);
			this.m_tradeButton = this.Children.Find<ButtonWidget>("TradeButton", true);
			this.m_tradeButton.Color = Color.Green;
			InventorySlotWidget inventorySlotWidget = this.Children.Find<InventorySlotWidget>("TradeSellSlot", true);
			InventorySlotWidget inventorySlotWidget2 = this.Children.Find<InventorySlotWidget>("TradeResultSlot", true);
			inventorySlotWidget.CenterColor = Color.LightYellow;
			inventorySlotWidget.BevelColor = Color.Yellow;
			inventorySlotWidget2.CenterColor = Color.LightGreen;
			inventorySlotWidget2.BevelColor = Color.Green;
			int num = 0;
			for (int i = 0; i < this.m_chestGrid.RowsCount; i++)
			{
				for (int j = 0; j < this.m_chestGrid.ColumnsCount; j++)
				{
					InventorySlotWidget inventorySlotWidget3 = new InventorySlotWidget();
					inventorySlotWidget3.AssignInventorySlot(tradeFlushInventory, num++);
					this.m_chestGrid.Children.Add(inventorySlotWidget3);
					this.m_chestGrid.SetWidgetCell(inventorySlotWidget3, new Point2(j, i));
				}
			}
			num = 10;
			for (int k = 0; k < this.m_inventoryGrid.RowsCount; k++)
			{
				for (int l = 0; l < this.m_inventoryGrid.ColumnsCount; l++)
				{
					InventorySlotWidget inventorySlotWidget4 = new InventorySlotWidget();
					inventorySlotWidget4.AssignInventorySlot(inventory, num++);
					this.m_inventoryGrid.Children.Add(inventorySlotWidget4);
					this.m_inventoryGrid.SetWidgetCell(inventorySlotWidget4, new Point2(l, k));
				}
			}
			inventorySlotWidget.AssignInventorySlot(tradeFlushInventory, tradeFlushInventory.TradeSellSlotIndex);
			inventorySlotWidget2.AssignInventorySlot(tradeFlushInventory, tradeFlushInventory.TradeResultSlotIndex);
		}

		// Token: 0x0600087C RID: 2172 RVA: 0x0003F8FC File Offset: 0x0003DAFC
		public override void Update()
		{
			if (!this.m_tradeFlushInventory.IsAddedToProject)
			{
				base.ParentWidget.Children.Remove(this);
				return;
			}
			this.Children.Find<LabelWidget>("Ratio", true).Text = this.m_tradeFlushInventory.Ratio.ToString(CultureInfo.CurrentCulture);
			Color color = Color.White;
			if ((double)this.m_tradeFlushInventory.Ratio > 1.0)
			{
				color = Color.Lerp(color, Color.DarkRed, this.m_tradeFlushInventory.Ratio - 1f);
			}
			else if ((double)this.m_tradeFlushInventory.Ratio < 1.0)
			{
				color = Color.Lerp(color, Color.DarkGreen, 1f - this.m_tradeFlushInventory.Ratio);
			}
			color.A = 192;
			this.Children.Find<LabelWidget>("Ratio", true).Color = color;
			int slotCount = this.m_tradeFlushInventory.GetSlotCount(this.m_tradeFlushInventory.ActiveSlotIndex);
			int slotValue = this.m_tradeFlushInventory.GetSlotValue(this.m_tradeFlushInventory.ActiveSlotIndex);
			if (slotValue != 0 && slotCount > 0)
			{
				ComponentTradeFlushInventory.Price price;
				this.m_tradeButton.IsEnabled = this.m_tradeFlushInventory.CanTrade(this.m_inventory, out price);
				if (this.m_tradeButton.IsClicked && this.m_tradeFlushInventory.TradeProcess(slotValue, slotCount, price))
				{
					AudioManager.PlaySound("Audio/CashRegister", 1f, 0f, 0f);
				}
				this.Children.Find<BlockIconWidget>("Currency", true).Value = price.CurrencyValue;
				this.Children.Find<LabelWidget>("SellPrice", true).Text = MathUtils.Round((float)price.SellPrice * this.m_tradeFlushInventory.Ratio * (float)slotCount).ToString(CultureInfo.CurrentCulture);
				return;
			}
			this.m_tradeButton.IsEnabled = false;
		}

		// Token: 0x04000575 RID: 1397
		public IInventory m_inventory;

		// Token: 0x04000576 RID: 1398
		public ComponentTradeFlushInventory m_tradeFlushInventory;

		// Token: 0x04000577 RID: 1399
		public GridPanelWidget m_inventoryGrid;

		// Token: 0x04000578 RID: 1400
		public GridPanelWidget m_chestGrid;

		// Token: 0x04000579 RID: 1401
		private readonly ButtonWidget m_tradeButton;
	}
}
