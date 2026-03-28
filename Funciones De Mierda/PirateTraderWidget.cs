using System;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;

namespace Game
{
	public class PirateTradeWidget : CanvasWidget
	{
		private ComponentTrader m_trader;
		private ComponentPlayer m_player;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemGameInfo m_subsystemGameInfo;

		private GridPanelWidget m_traderGrid;
		private GridPanelWidget m_inventoryGrid;
		private BevelledButtonWidget m_buyButton;
		private LabelWidget m_infoLabel;
		private InventorySlotWidget m_coinSlot;
		private int m_selectedSlot = -1;

		public PirateTradeWidget(IInventory playerInventory, ComponentTrader trader, ComponentPlayer player)
		{
			m_trader = trader;
			m_player = player;
			m_subsystemTerrain = player.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAudio = player.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemGameInfo = player.Project.FindSubsystem<SubsystemGameInfo>(true);

			XElement node = ContentManager.Get<XElement>("Widgets/PirateTradeWidget");
			LoadContents(this, node);

			m_traderGrid = Children.Find<GridPanelWidget>("TraderGrid", true);
			m_inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid", true);
			m_buyButton = Children.Find<BevelledButtonWidget>("BuyButton", true);
			m_infoLabel = Children.Find<LabelWidget>("InfoLabel", true);
			m_coinSlot = Children.Find<InventorySlotWidget>("CoinSlot", true);

			m_infoLabel.Size = new Vector2(200, 40);
			m_infoLabel.FontScale = 0.7f;
			m_infoLabel.HorizontalAlignment = WidgetAlignment.Center;
			m_infoLabel.VerticalAlignment = WidgetAlignment.Far;

			m_buyButton.HorizontalAlignment = WidgetAlignment.Far;
			m_buyButton.Margin = new Vector2(0, 0);

			m_coinSlot.AssignInventorySlot(trader, 8);
			m_coinSlot.HideHighlightRectangle = true;

			int slotIndex = 0;
			for (int row = 0; row < m_traderGrid.RowsCount; row++)
			{
				for (int col = 0; col < m_traderGrid.ColumnsCount; col++)
				{
					if (slotIndex >= 8) break;
					var slotWidget = new InventorySlotWidget();
					slotWidget.AssignInventorySlot(trader, slotIndex++);
					slotWidget.Size = new Vector2(68, 68);
					slotWidget.ProcessingOnly = true;
					m_traderGrid.Children.Add(slotWidget);
					m_traderGrid.SetWidgetCell(slotWidget, new Point2(col, row));
				}
			}

			int invSlot = 10;
			for (int row = 0; row < m_inventoryGrid.RowsCount; row++)
			{
				for (int col = 0; col < m_inventoryGrid.ColumnsCount; col++)
				{
					var slotWidget = new InventorySlotWidget();
					slotWidget.AssignInventorySlot(playerInventory, invSlot++);
					m_inventoryGrid.Children.Add(slotWidget);
					m_inventoryGrid.SetWidgetCell(slotWidget, new Point2(col, row));
				}
			}
		}

		public override void Update()
		{
			if (!m_trader.IsAddedToProject || m_player.ComponentHealth.Health == 0f)
			{
				ParentWidget.Children.Remove(this);
				return;
			}

			var dragHost = m_player.GameWidget?.Children.Find<DragHostWidget>(false);
			m_trader.IsDragInProgress = (dragHost != null && dragHost.IsDragInProgress);

			if (Input.Click != null)
			{
				var hit = HitTestGlobal(Input.Click.Value.Start);
				if (hit is InventorySlotWidget slotWidget && slotWidget.m_inventory == m_trader && slotWidget.m_slotIndex < 8)
				{
					m_selectedSlot = slotWidget.m_slotIndex;
					m_trader.ActiveSlotIndex = m_selectedSlot;
					UpdateInfoLabel();
				}
			}

			if (m_selectedSlot >= 0 && m_trader.GetSlotValue(m_selectedSlot) == 0)
				m_selectedSlot = -1;

			if (m_buyButton.IsClicked)
			{
				if (m_selectedSlot >= 0)
				{
					if (m_trader.TryBuy(m_selectedSlot, m_player))
						m_selectedSlot = -1;
				}
				else
				{
					m_player.ComponentGui.DisplaySmallMessage(
						LanguageControl.GetContentWidgets("Trader", "SelectItemFirst"),
						Color.Red, true, false);
					m_subsystemAudio.PlaySound("Audio/UI/warning", 1f, 0f, 0f, 0f);
				}
			}

			base.Update();
		}

		private void UpdateInfoLabel()
		{
			if (m_selectedSlot < 0)
			{
				m_infoLabel.Text = LanguageControl.GetContentWidgets("Trader", "SelectItemFirst");
				return;
			}
			int value = m_trader.GetSlotValue(m_selectedSlot);
			if (value == 0)
			{
				m_infoLabel.Text = LanguageControl.GetContentWidgets("Trader", "SlotEmpty");
				return;
			}
			int price = m_trader.GetPrice(m_selectedSlot);
			m_infoLabel.Text = string.Format(
				LanguageControl.GetContentWidgets("Trader", "InfoLabelFormat"),
				price);
		}
	}
}
