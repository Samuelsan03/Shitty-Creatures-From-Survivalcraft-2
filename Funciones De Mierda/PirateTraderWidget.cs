using System;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;

namespace Game
{
	public class PirateTradeWidget : CanvasWidget
	{
		private ComponentPirateTrader m_trader;
		private ComponentPlayer m_player;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemGameInfo m_subsystemGameInfo;

		private GridPanelWidget m_traderGrid;
		private GridPanelWidget m_inventoryGrid;
		private BevelledButtonWidget m_buyButton;
		private LabelWidget m_infoLabel;
		private LabelWidget m_pirateTraderTitle;
		private LabelWidget m_inventoryTitle;
		private InventorySlotWidget m_coinSlot;
		private LabelWidget m_coinSlotHint;
		private int m_selectedSlot = -1;

		public PirateTradeWidget(IInventory playerInventory, ComponentPirateTrader trader, ComponentPlayer player)
		{
			m_trader = trader;
			m_player = player;
			m_subsystemTerrain = player.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAudio = player.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemGameInfo = player.Project.FindSubsystem<SubsystemGameInfo>(true);

			XElement node = ContentManager.Get<XElement>("Widgets/PirateTradeWidget");
			LoadContents(this, node);

			// Obtener referencias a los widgets
			m_pirateTraderTitle = Children.Find<LabelWidget>("PirateTraderTitle", true);
			m_inventoryTitle = Children.Find<LabelWidget>("InventoryTitle", true);
			m_traderGrid = Children.Find<GridPanelWidget>("TraderGrid", true);
			m_inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid", true);
			m_buyButton = Children.Find<BevelledButtonWidget>("BuyButton", true);
			m_infoLabel = Children.Find<LabelWidget>("InfoLabel", true);
			m_coinSlot = Children.Find<InventorySlotWidget>("CoinSlot", true);

			// Aplicar traducciones
			m_pirateTraderTitle.Text = LanguageControl.GetContentWidgets("PirateTraderWidget", "Title");
			m_inventoryTitle.Text = LanguageControl.GetContentWidgets("PirateTraderWidget", "Inventory");
			m_buyButton.Text = LanguageControl.GetContentWidgets("PirateTraderWidget", "BuyButton");
			m_infoLabel.Text = LanguageControl.GetContentWidgets("PirateTraderWidget", "SelectItemFirst"); // <-- LÍNEA AÑADIDA

			// Crear hint para el slot de monedas
			m_coinSlotHint = new LabelWidget
			{
				Color = new Color(128, 128, 128),
				FontScale = 0.5f,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Far
			};
			CanvasWidget.SetPosition(m_coinSlotHint, new Vector2(12, 192));
			Children.Add(m_coinSlotHint);

			m_coinSlot.AssignInventorySlot(trader, 8);
			m_coinSlot.HideHighlightRectangle = true;

			// Crear slots del trader (índices 0-7) – siempre solo lectura
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

			// Slots del inventario del jugador
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

			// Actualizar hint del slot de monedas
			int coinValue = m_trader.GetSlotValue(8);
			int coinCount = m_trader.GetSlotCount(8);
			m_coinSlotHint.IsVisible = (coinValue == 0 || coinCount == 0);
			m_coinSlotHint.Text = LanguageControl.GetContentWidgets("PirateTraderWidget", "CoinSlotHint");

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
						LanguageControl.GetContentWidgets("PirateTraderWidget", "SelectItemFirst"),
						Color.Red, true, true);
					m_subsystemAudio.PlaySound("Audio/UI/warning", 1f, 0f, 0f, 0f);
				}
			}

			base.Update();
		}

		private void UpdateInfoLabel()
		{
			if (m_selectedSlot < 0)
			{
				m_infoLabel.Text = LanguageControl.GetContentWidgets("PirateTraderWidget", "SelectItemFirst");
				return;
			}
			int value = m_trader.GetSlotValue(m_selectedSlot);
			if (value == 0)
			{
				m_infoLabel.Text = LanguageControl.GetContentWidgets("PirateTraderWidget", "SlotEmpty");
				return;
			}
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			string name = block.GetDisplayName(m_subsystemTerrain, value);
			int price = m_trader.GetPrice(m_selectedSlot);
			int count = m_trader.GetSlotCount(m_selectedSlot);
			int total = price * count;
			m_infoLabel.Text = string.Format(
				LanguageControl.GetContentWidgets("PirateTraderWidget", "InfoLabelFormat"),
				name, count, price, total);
		}
	}
}
