using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;
namespace Game
{
	public class PirateTraderWidget : CanvasWidget
	{
		private ComponentPirateTrader m_trader;
		private ComponentInventory m_playerInventory;
		private List<InventorySlotWidget> m_tradeSlots = new List<InventorySlotWidget>();
		private List<InventorySlotWidget> m_playerSlots = new List<InventorySlotWidget>();
		private int m_selectedIndex = -1;
		private TraderInventory m_traderInventory;
		private BevelledButtonWidget m_buyButton;
		private LabelWidget m_priceLabel;
		private InventorySlotWidget m_tradeResultSlot;
		private InventorySlotWidget m_tradeSellSlot;
		private BlockIconWidget m_currencyIcon;
		private GridPanelWidget m_npcGrid;
		private GridPanelWidget m_inventoryGrid;
		private Entity m_npcEntity;
		private ComponentCreature m_npcCreature;
		private LabelWidget m_traderLabel;
		private TradeResultInventory m_tradeResultInventory;
		private PlayerCoinInventory m_playerCoinInventory;
		private static Dictionary<Entity, PlayerCoinInventory> s_persistentCoinInventories = new Dictionary<Entity, PlayerCoinInventory>();
		private ComponentPlayer m_player;
		private bool m_hasAvailableItems = false;
		public PirateTraderWidget(ComponentPirateTrader trader, ComponentInventory playerInventory, Entity npcEntity, ComponentPlayer player)
		{
			m_trader = trader;
			m_playerInventory = playerInventory;
			m_npcEntity = npcEntity;
			m_npcCreature = npcEntity.FindComponent<ComponentCreature>();
			m_traderInventory = new TraderInventory(trader);
			m_player = player;
			XElement node = ContentManager.Get<XElement>("Widgets/PirateNpcWidget");
			this.LoadContents(this, node);
			m_npcGrid = this.Children.Find<GridPanelWidget>("PirateNpcGrid");
			m_inventoryGrid = this.Children.Find<GridPanelWidget>("InventoryGrid");
			m_priceLabel = this.Children.Find<LabelWidget>("SellPrice");
			m_buyButton = this.Children.Find<BevelledButtonWidget>("TradeButton");
			m_tradeResultSlot = this.Children.Find<InventorySlotWidget>("TradeResultSlot");
			m_tradeSellSlot = this.Children.Find<InventorySlotWidget>("TradeSellSlot");
			m_currencyIcon = this.Children.Find<BlockIconWidget>("Currency");
			m_traderLabel = this.Children.Find<LabelWidget>("SnowManLabel");
			if (m_trader != null && m_trader.Entity != null)
			{
				m_tradeResultInventory = new TradeResultInventory(m_trader.Entity.Project);
				m_tradeResultSlot.AssignInventorySlot(m_tradeResultInventory, 0);
				if (!s_persistentCoinInventories.TryGetValue(npcEntity, out m_playerCoinInventory))
				{
					m_playerCoinInventory = new PlayerCoinInventory(m_trader.Entity.Project);
					s_persistentCoinInventories[npcEntity] = m_playerCoinInventory;
				}
				m_tradeSellSlot.AssignInventorySlot(m_playerCoinInventory, 0);
			}
			if (m_currencyIcon != null)
			{
				try
				{
					int nuclearCoinIndex = BlocksManager.GetBlockIndex<NuclearCoinBlock>();
					if (nuclearCoinIndex > 0) m_currencyIcon.Value = Terrain.MakeBlockValue(nuclearCoinIndex);
				}
				catch { }
			}
			if (m_traderLabel != null)
			{
				string creatureName = GetCreatureName();
				m_traderLabel.Text = $"Shop";
			}
			if (m_traderInventory == null || m_traderInventory.SlotsCount == 0)
			{
				Log.Warning("Trader inventory is empty or null");
				return;
			}
			m_hasAvailableItems = false;
			for (int i = 0; i < m_trader.TradeItems.Count; i++)
			{
				if (m_trader.IsItemAvailable(i))
				{
					m_hasAvailableItems = true;
					break;
				}
			}
			if (m_npcGrid != null && m_traderInventory != null)
			{
				int maxSlots = Math.Min(m_traderInventory.SlotsCount, 8);
				for (int i = 0; i < maxSlots; i++)
				{
					InventorySlotWidget slot = new InventorySlotWidget();
					slot.AssignInventorySlot(m_traderInventory, i);
					m_tradeSlots.Add(slot);
					m_npcGrid.Children.Add(slot);
					int col = i % 4;
					int row = i / 4;
					m_npcGrid.SetWidgetCell(slot, new Point2(col, row));
				}
			}
			if (m_inventoryGrid != null && m_playerInventory != null)
			{
				m_inventoryGrid.Children.Clear();
				m_playerSlots.Clear();
				for (int i = 10; i < m_playerInventory.SlotsCount && i < 26; i++)
				{
					InventorySlotWidget slot = new InventorySlotWidget();
					slot.AssignInventorySlot(m_playerInventory, i);
					m_playerSlots.Add(slot);
					m_inventoryGrid.Children.Add(slot);
					int col = (i - 10) % 4;
					int row = (i - 10) / 4;
					m_inventoryGrid.SetWidgetCell(slot, new Point2(col, row));
				}
			}
			for (int i = 0; i < m_trader.TradeItems.Count; i++)
			{
				if (m_trader.IsItemAvailable(i))
				{
					m_selectedIndex = i;
					break;
				}
			}
			UpdateSelection();
			if (m_trader != null)
			{
				m_trader.TradeCompleted += OnTradeCompleted;
				m_trader.TradeMessage += OnTradeMessage;
			}
		}
		private string GetCreatureName()
		{
			if (m_npcCreature != null && !string.IsNullOrEmpty(m_npcCreature.DisplayName))
				return m_npcCreature.DisplayName;
			var componentName = m_npcEntity.FindComponent<ComponentName>();
			if (componentName != null && !string.IsNullOrEmpty(componentName.Name))
				return componentName.Name;
			return "Pirate Trader";
		}
		private void UpdateSelection()
		{
			if (m_selectedIndex >= 0 && m_selectedIndex < m_trader.TradeItems.Count)
			{
				if (m_priceLabel != null) m_priceLabel.Text = m_trader.TradePrices[m_selectedIndex].ToString();
				if (m_buyButton != null)
				{
					m_buyButton.IsEnabled = m_trader.IsItemAvailable(m_selectedIndex);
				}
			}
			else
			{
				if (m_buyButton != null)
				{
					m_buyButton.IsEnabled = false;
				}
			}
		}
		private void OnTradeCompleted()
		{
			UpdatePlayerSlots();
			m_hasAvailableItems = false;
			for (int i = 0; i < m_trader.TradeItems.Count; i++)
			{
				if (m_trader.IsItemAvailable(i))
				{
					m_hasAvailableItems = true;
					break;
				}
			}
			if (!m_hasAvailableItems && m_buyButton != null)
			{
				m_buyButton.IsEnabled = false;
			}
		}
		private void OnTradeMessage(string message, Color color)
		{
			if (m_player != null && m_player.ComponentGui != null)
			{
				try { AudioManager.PlaySound("Audio/UI/warning", 1f, 0f, 0f); }
				catch { }
				m_player.ComponentGui.DisplaySmallMessage(message, color, true, false);
			}
		}
		private void UpdatePlayerSlots()
		{
			foreach (var slot in m_playerSlots)
			{
				if (slot != null)
				{
					slot.Update();
				}
			}
		}
		public override void Update()
		{
			base.Update();
			if (m_trader == null || m_traderInventory == null || m_playerInventory == null) return;
			WidgetInput input = base.Input;
			if (input.Click.HasValue)
			{
				Vector2 clickPosition = input.Click.Value.Start;
				for (int i = 0; i < m_tradeSlots.Count; i++)
				{
					var slot = m_tradeSlots[i];
					if (slot.HitTestGlobal(clickPosition, null) == slot)
					{
						if (slot.HitTestGlobal(input.Click.Value.End, null) == slot)
						{
							if (i < m_trader.TradeItems.Count && m_trader.IsItemAvailable(i))
							{
								m_selectedIndex = i;
								UpdateSelection();
								try { AudioManager.PlaySound("Audio/UI/ButtonClick", 0.5f, 0f, 0f); }
								catch { }
								break;
							}
						}
					}
				}
			}
			foreach (var slot in m_tradeSlots)
			{
				if (slot != null)
				{
					slot.Update();
				}
			}
			UpdatePlayerSlots();
			if (m_buyButton != null && m_buyButton.IsClicked && m_buyButton.IsEnabled)
			{
				if (m_selectedIndex >= 0 && m_selectedIndex < m_trader.TradeItems.Count)
				{
					if (m_playerCoinInventory != null && m_trader.IsItemAvailable(m_selectedIndex))
					{
						int purchasedItemValue;
						bool success = m_trader.TryBuyItem(m_playerInventory, m_selectedIndex, m_playerCoinInventory, out purchasedItemValue, m_player);
						if (success)
						{
							m_tradeResultInventory.SetItemValue(purchasedItemValue, 1);
							m_tradeResultSlot.Update();
							m_tradeSellSlot.Update();
							try { AudioManager.PlaySound("Audio/UI/money", 0.5f, 0f, 0f); }
							catch { }
							foreach (var slot in m_tradeSlots) slot.Update();
							UpdatePlayerSlots();
							m_selectedIndex = -1;
							for (int i = 0; i < m_trader.TradeItems.Count; i++)
							{
								if (m_trader.IsItemAvailable(i))
								{
									m_selectedIndex = i;
									break;
								}
							}
							UpdateSelection();
						}
					}
				}
			}
			if (m_tradeResultSlot != null) m_tradeResultSlot.Update();
		}
		public override void Dispose()
		{
			if (m_trader != null)
			{
				m_trader.TradeCompleted -= OnTradeCompleted;
				m_trader.TradeMessage -= OnTradeMessage;
			}
			m_playerSlots.Clear();
			base.Dispose();
		}
		public static void CleanupPersistentInventory(Entity npcEntity)
		{
			if (npcEntity != null) s_persistentCoinInventories.Remove(npcEntity);
		}
		private class TraderInventory : IInventory
		{
			private ComponentPirateTrader m_trader;
			private Project m_project;
			public TraderInventory(ComponentPirateTrader trader)
			{
				m_trader = trader;
				if (trader != null && trader.Entity != null) m_project = trader.Entity.Project;
			}
			public int SlotsCount => 8;
			public int GetSlotValue(int slotIndex)
			{
				if (m_trader == null || m_trader.TradeItems == null) return 0;
				if (slotIndex >= 0 && slotIndex < m_trader.TradeItems.Count && m_trader.IsItemAvailable(slotIndex))
					return m_trader.TradeItems[slotIndex];
				return 0;
			}
			public int GetSlotCount(int slotIndex) => GetSlotValue(slotIndex) != 0 ? 1 : 0;
			public int GetSlotCapacity(int slotIndex, int value) => 0;
			public int GetSlotProcessCapacity(int slotIndex, int value) => 0;
			public void AddSlotItems(int slotIndex, int value, int count) { }
			public int RemoveSlotItems(int slotIndex, int count) => 0;
			public void DropAllItems(Vector3 position) { }
			public void ProcessSlotItems(int slotIndex, int value, int count, int processCount,
				out int processedValue, out int processedCount)
			{
				processedValue = 0;
				processedCount = 0;
			}
			public Project Project => m_project;
			public int VisibleSlotsCount { get => SlotsCount; set { } }
			public int ActiveSlotIndex { get; set; }
		}
		private class TradeResultInventory : IInventory
		{
			private int m_itemValue;
			private int m_itemCount;
			private Project m_project;
			public TradeResultInventory(Project project) { m_project = project; }
			public int SlotsCount => 1;
			public int GetSlotValue(int slotIndex) => (slotIndex == 0) ? m_itemValue : 0;
			public int GetSlotCount(int slotIndex) => m_itemCount;
			public int GetSlotCapacity(int slotIndex, int value)
			{
				if (slotIndex == 0 && m_itemValue == 0) return 64;
				if (slotIndex == 0 && value != 0 && Terrain.ExtractContents(value) == Terrain.ExtractContents(m_itemValue))
					return 64;
				return 0;
			}
			public int GetSlotProcessCapacity(int slotIndex, int value) => 0;
			public void AddSlotItems(int slotIndex, int value, int count)
			{
				if (slotIndex == 0)
				{
					if (m_itemValue == 0)
					{
						m_itemValue = value;
						m_itemCount = count;
					}
					else if (Terrain.ExtractContents(value) == Terrain.ExtractContents(m_itemValue))
					{
						m_itemCount += count;
					}
				}
			}
			public int RemoveSlotItems(int slotIndex, int count)
			{
				if (slotIndex == 0 && m_itemCount > 0)
				{
					int toRemove = Math.Min(count, m_itemCount);
					m_itemCount -= toRemove;
					if (m_itemCount == 0) m_itemValue = 0;
					return toRemove;
				}
				return 0;
			}
			public void DropAllItems(Vector3 position) { }
			public void ProcessSlotItems(int slotIndex, int value, int count, int processCount,
				out int processedValue, out int processedCount)
			{
				processedValue = 0;
				processedCount = 0;
			}
			public Project Project => m_project;
			public int VisibleSlotsCount { get => 1; set { } }
			public int ActiveSlotIndex { get; set; }
			public void SetItemValue(int itemValue, int count = 1)
			{
				m_itemValue = itemValue;
				m_itemCount = count;
			}
			public void Clear() { m_itemValue = 0; m_itemCount = 0; }
		}
		private class PlayerCoinInventory : IInventory
		{
			private int m_coinValue;
			private int m_coinCount;
			private Project m_project;
			public PlayerCoinInventory(Project project) { m_project = project; }
			public int SlotsCount => 1;
			public int GetSlotValue(int slotIndex) => (slotIndex == 0) ? m_coinValue : 0;
			public int GetSlotCount(int slotIndex) => m_coinCount;
			public int GetSlotCapacity(int slotIndex, int value)
			{
				int coinIndex = BlocksManager.GetBlockIndex<NuclearCoinBlock>();
				return (Terrain.ExtractContents(value) == coinIndex) ? 100000 : 0;
			}
			public int GetSlotProcessCapacity(int slotIndex, int value) => 0;
			public void AddSlotItems(int slotIndex, int value, int count)
			{
				if (slotIndex == 0)
				{
					int coinIndex = BlocksManager.GetBlockIndex<NuclearCoinBlock>();
					if (Terrain.ExtractContents(value) == coinIndex)
					{
						if (m_coinValue == 0) m_coinValue = value;
						m_coinCount += count;
					}
				}
			}
			public int RemoveSlotItems(int slotIndex, int count)
			{
				if (slotIndex == 0 && m_coinCount > 0)
				{
					int toRemove = Math.Min(count, m_coinCount);
					m_coinCount -= toRemove;
					if (m_coinCount == 0) m_coinValue = 0;
					return toRemove;
				}
				return 0;
			}
			public void DropAllItems(Vector3 position) { }
			public void ProcessSlotItems(int slotIndex, int value, int count, int processCount,
				out int processedValue, out int processedCount)
			{
				processedValue = 0;
				processedCount = 0;
			}
			public Project Project => m_project;
			public int VisibleSlotsCount { get => 1; set { } }
			public int ActiveSlotIndex { get; set; }
			public bool HasEnoughCoins(int requiredCoins) => m_coinCount >= requiredCoins;
			public void ClearCoins() { m_coinValue = 0; m_coinCount = 0; }
		}
	}
}
