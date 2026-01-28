using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentPirateTrader : Component
	{
		public List<int> TradeItems = new List<int>();
		public List<int> TradePrices = new List<int>();
		public List<bool> TradeItemsSold = new List<bool>();
		public event System.Action TradeCompleted;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			TradeItems.Add(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<FlameThrowerBlock>()));
			TradePrices.Add(50);
			TradeItemsSold.Add(false);

			TradeItems.Add(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<MusketBlock>()));
			TradePrices.Add(30);
			TradeItemsSold.Add(false);
		}

		public bool TryBuyItem(ComponentInventory playerInventory, int itemIndex, IInventory coinInventory, out int purchasedItemValue)
		{
			purchasedItemValue = 0;

			if (itemIndex < 0 || itemIndex >= TradeItems.Count)
				return false;

			if (TradeItemsSold[itemIndex])
				return false;

			int price = TradePrices[itemIndex];
			int coinIndex = BlocksManager.GetBlockIndex<NuclearCoinBlock>();

			if (coinInventory == null || coinInventory.SlotsCount == 0)
				return false;

			int coinValue = coinInventory.GetSlotValue(0);
			if (Terrain.ExtractContents(coinValue) != coinIndex)
				return false;

			int coinCount = coinInventory.GetSlotCount(0);
			if (coinCount < price)
				return false;

			coinInventory.RemoveSlotItems(0, price);

			purchasedItemValue = TradeItems[itemIndex];

			if (playerInventory != null)
			{
				int acquireSlotIndex = ComponentInventoryBase.FindAcquireSlotForItem(playerInventory, purchasedItemValue);

				if (acquireSlotIndex >= 0)
				{
					playerInventory.AddSlotItems(acquireSlotIndex, purchasedItemValue, 1);
				}
				else
				{
					coinInventory.AddSlotItems(0, coinValue, price);
					return false;
				}
			}

			TradeItemsSold[itemIndex] = true;

			TradeCompleted?.Invoke();
			return true;
		}

		public bool TryBuyItem(ComponentInventory playerInventory, int itemIndex, IInventory coinInventory)
		{
			int purchasedItemValue;
			return TryBuyItem(playerInventory, itemIndex, coinInventory, out purchasedItemValue);
		}

		public bool IsItemAvailable(int itemIndex)
		{
			if (itemIndex < 0 || itemIndex >= TradeItemsSold.Count)
				return false;
			return !TradeItemsSold[itemIndex];
		}
	}
}
