using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentTrader : ComponentInventoryBase, IUpdateable
	{
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemPickables m_subsystemPickables;
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemTime m_subsystemTime;

		private int m_modificationLock = 0;
		private int m_selectedSlotIndex = -1;

		// Lista de objetos que puede vender (con probabilidad, precio, cantidad y variante)
		private List<TradeItem> m_tradeItems;
		private Dictionary<int, TradeItem> m_itemDataMap; // BlockIndex -> TradeItem (para obtener precio y cantidad máxima)
		private double m_nextRestockTime;
		private double m_restockInterval = 300.0;
		private Random m_random = new Random();

		public bool IsDragInProgress { get; set; }

		public override int SlotsCount => 9;

		public override int ActiveSlotIndex
		{
			get { return m_selectedSlotIndex; }
			set
			{
				if (value >= 0 && value < 8)
					m_selectedSlotIndex = value;
			}
		}

		public void BeginModification() { m_modificationLock++; }
		public void EndModification() { m_modificationLock--; }

		public struct TradeItem
		{
			public int BlockIndex;
			public int Variant; // -1 si se usa template name
			public string CreatureTemplateName; // para huevos
			public float Probability;
			public int Price;
			public int MaxCount;

			public int GetBlockValue()
			{
				// Si es un huevo con template name, lo resolvemos
				if (BlockIndex == EggBlock.Index && !string.IsNullOrEmpty(CreatureTemplateName))
				{
					EggBlock eggBlock = (EggBlock)BlocksManager.Blocks[BlockIndex];
					var eggType = eggBlock.GetEggTypeByCreatureTemplateName(CreatureTemplateName);
					if (eggType != null)
					{
						int data = EggBlock.SetEggType(0, eggType.EggTypeIndex);
						data = EggBlock.SetIsLaid(data, false); // Huevo sin poner
						return Terrain.MakeBlockValue(BlockIndex, 0, data);
					}
					// fallback
					return Terrain.MakeBlockValue(BlockIndex);
				}
				else if (Variant >= 0)
				{
					Block block = BlocksManager.Blocks[BlockIndex];
					return Terrain.MakeBlockValue(BlockIndex, 0, Variant);
				}
				else
				{
					return Terrain.MakeBlockValue(BlockIndex);
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_slots.Clear();
			for (int i = 0; i < SlotsCount; i++) m_slots.Add(new Slot());

			if (valuesDictionary.ContainsKey("Slots"))
				base.Load(valuesDictionary, idToEntityMap);

			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);

			m_restockInterval = valuesDictionary.GetValue<double>("RestockInterval", 300.0);

			string tradeItemsStr = valuesDictionary.GetValue<string>("TradeItems", null);
			if (!string.IsNullOrEmpty(tradeItemsStr))
			{
				ParseTradeItems(tradeItemsStr);
				BuildItemDataMap();

				m_nextRestockTime = valuesDictionary.GetValue<double>("NextRestockTime", m_subsystemTime.GameTime + m_restockInterval);

				bool anyItem = false;
				for (int i = 0; i < 8; i++)
					if (GetSlotValue(i) != 0) { anyItem = true; break; }

				if (!anyItem && m_tradeItems.Count > 0)
				{
					Restock();
				}
			}
			else
			{
				m_tradeItems = new List<TradeItem>();
				m_itemDataMap = new Dictionary<int, TradeItem>();
			}
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			valuesDictionary.SetValue("NextRestockTime", m_nextRestockTime);
		}

		private void ParseTradeItems(string str)
		{
			m_tradeItems = new List<TradeItem>();
			string[] items = str.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string item in items)
			{
				string[] parts = item.Split(':');
				if (parts.Length >= 4)
				{
					string blockName = parts[0].Trim();
					float prob;
					int price;
					int maxCount;
					int variant = -1;
					string templateName = null;

					if (float.TryParse(parts[1], out prob) &&
						int.TryParse(parts[2], out price) &&
						int.TryParse(parts[3], out maxCount))
					{
						int blockIndex = BlocksManager.GetBlockIndex(blockName, false);
						if (blockIndex >= 0)
						{
							// Si es un huevo y hay 5 partes, la quinta es el template name
							if (blockIndex == EggBlock.Index && parts.Length >= 5)
							{
								templateName = parts[4].Trim();
							}
							else if (parts.Length >= 5)
							{
								// Para otros bloques, la quinta parte es la variante entera
								int.TryParse(parts[4], out variant);
							}

							m_tradeItems.Add(new TradeItem
							{
								BlockIndex = blockIndex,
								Variant = variant,
								CreatureTemplateName = templateName,
								Probability = prob,
								Price = price,
								MaxCount = Math.Max(1, maxCount)
							});
						}
					}
				}
			}
		}

		private void BuildItemDataMap()
		{
			m_itemDataMap = new Dictionary<int, TradeItem>();
			foreach (var item in m_tradeItems)
			{
				int value = item.GetBlockValue(); // valor único para este item
				if (!m_itemDataMap.ContainsKey(value))
					m_itemDataMap[value] = item;
			}
		}

		private int GetRandomOccupiedSlotsCount()
		{
			float r = m_random.Float();

			if (r < 0.05f) // 5% - Casi vacío (1 slot)
				return 1;
			else if (r < 0.35f) // 30% - Poco lleno (2-3 slots)
				return m_random.Int(2, 3);
			else if (r < 0.75f) // 40% - Medianamente lleno (4-5 slots)
				return m_random.Int(4, 5);
			else if (r < 0.95f) // 20% - Bastante lleno (6-7 slots)
				return m_random.Int(6, 7);
			else // 5% - Completamente lleno (8 slots)
				return 8;
		}

		public override void DropAllItems(Vector3 position)
		{
			// Soltar solo las monedas del slot 8
			int coinValue = GetSlotValue(8);
			int coinCount = GetSlotCount(8);
			if (coinValue != 0 && coinCount > 0)
			{
				SubsystemPickables subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
				subsystemPickables.AddPickable(coinValue, coinCount, position, null, null, Entity);
				RemoveSlotItems(8, coinCount);
			}
			// Los items de venta no se sueltan
		}

		private void Restock()
		{
			if (m_tradeItems == null || m_tradeItems.Count == 0) return;

			// Limpiar todas las ranuras de venta (0-7)
			for (int i = 0; i < 8; i++)
			{
				m_slots[i].Value = 0;
				m_slots[i].Count = 0;
			}

			// Crear una lista de ítems disponibles
			List<TradeItem> availableItems = new List<TradeItem>(m_tradeItems);

			// Determinar cuántos slots queremos llenar
			int slotsToFill = GetRandomOccupiedSlotsCount();

			// Crear y barajar índices de slots
			List<int> slotIndices = new List<int>();
			for (int i = 0; i < 8; i++)
				slotIndices.Add(i);

			for (int i = 0; i < slotIndices.Count; i++)
			{
				int j = m_random.Int(i, slotIndices.Count - 1);
				int temp = slotIndices[i];
				slotIndices[i] = slotIndices[j];
				slotIndices[j] = temp;
			}

			// Llenar los slots
			for (int i = 0; i < slotsToFill; i++)
			{
				int selectedSlot = slotIndices[i];

				// Si se agotan los ítems, recargar la lista (esto permite duplicados)
				if (availableItems.Count == 0)
					availableItems = new List<TradeItem>(m_tradeItems);

				int itemIndex = m_random.Int(0, availableItems.Count - 1);
				TradeItem selectedItem = availableItems[itemIndex];
				availableItems.RemoveAt(itemIndex);

				m_slots[selectedSlot].Value = selectedItem.GetBlockValue();
				m_slots[selectedSlot].Count = selectedItem.MaxCount;
			}
		}
		public override void AddSlotItems(int slotIndex, int value, int count)
		{
			bool isCreative = (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative);

			if (slotIndex == 8)
			{
				// Comportamiento especial para el slot de monedas (arrastrar en creativo)
				if (isCreative && count == 1 && GetSlotCount(slotIndex) == 0 && IsDragInProgress)
				{
					int tempCapacity = GetSlotCapacity(slotIndex, value);
					if (tempCapacity > 1)
						count = tempCapacity;
				}

				int currentCount = GetSlotCount(slotIndex);
				int maxCapacity = GetSlotCapacity(slotIndex, value);

				if (currentCount + count > maxCapacity)
				{
					count = maxCapacity - currentCount;
					if (count <= 0) return;
				}

				base.AddSlotItems(slotIndex, value, count);
			}
			else if (m_modificationLock > 0)
			{
				// Solo durante reabastecimiento o compra se modifican los slots de venta
				int currentCount = GetSlotCount(slotIndex);
				int maxCapacity = GetSlotCapacity(slotIndex, value);

				if (currentCount + count > maxCapacity)
				{
					count = maxCapacity - currentCount;
					if (count <= 0) return;
				}

				base.AddSlotItems(slotIndex, value, count);
			}
			// En cualquier otro caso, no se hace nada
		}
		public override int RemoveSlotItems(int slotIndex, int count)
		{
			if (slotIndex == 8 || m_modificationLock > 0)
				return base.RemoveSlotItems(slotIndex, count);
			return 0;
		}

		public override int GetSlotCapacity(int slotIndex, int value)
		{
			if (slotIndex == 8)
			{
				int coinIndex = BlocksManager.GetBlockIndex(typeof(NuclearCoinBlock).Name, true);
				if (Terrain.ExtractContents(value) != coinIndex)
					return 0;
				return 100000;
			}
			else
			{
				if (m_modificationLock == 0)
					return 0;

				if (m_itemDataMap != null && m_itemDataMap.TryGetValue(value, out TradeItem tradeItem))
				{
					return tradeItem.MaxCount;
				}

				int baseCapacity = base.GetSlotCapacity(slotIndex, value);
				return baseCapacity <= 1 ? baseCapacity : 100000;
			}
		}

		public override int GetSlotProcessCapacity(int slotIndex, int value)
		{
			if (slotIndex == 8)
			{
				int coinIndex = BlocksManager.GetBlockIndex(typeof(NuclearCoinBlock).Name, true);
				if (Terrain.ExtractContents(value) != coinIndex)
					return 0;
				return base.GetSlotProcessCapacity(slotIndex, value);
			}
			else
			{
				if (m_modificationLock > 0)
					return base.GetSlotProcessCapacity(slotIndex, value);
				return 0;
			}
		}

		public virtual int GetPrice(int slotIndex)
		{
			if (slotIndex == 8) return 0;
			int value = GetSlotValue(slotIndex);
			if (value == 0) return 0;
			if (m_itemDataMap != null && m_itemDataMap.TryGetValue(value, out TradeItem tradeItem))
				return tradeItem.Price;
			return 0;
		}
		public virtual bool TryBuy(int slotIndex, ComponentPlayer buyer)
		{
			if (slotIndex == 8) return false;

			int value = GetSlotValue(slotIndex);
			int count = GetSlotCount(slotIndex);
			if (value == 0 || count <= 0)
			{
				buyer.ComponentGui.DisplaySmallMessage(
					LanguageControl.GetContentWidgets("Trader", "ItemNoLongerAvailable"),
					Color.Red, true, false);
				m_subsystemAudio.PlaySound("Audio/UI/warning", 1f, 0f, 0f, 0f);
				return false;
			}

			int pricePerItem = GetPrice(slotIndex);
			if (pricePerItem <= 0)
			{
				buyer.ComponentGui.DisplaySmallMessage(
					LanguageControl.GetContentWidgets("Trader", "ItemCannotBeBought"),
					Color.Red, true, false);
				m_subsystemAudio.PlaySound("Audio/UI/warning", 1f, 0f, 0f, 0f);
				return false;
			}

			int totalPrice = pricePerItem; // Precio fijo por todo el stack
			int coinIndex = BlocksManager.GetBlockIndex(typeof(NuclearCoinBlock).Name, true);

			int coinSlotValue = GetSlotValue(8);
			int coinSlotCount = GetSlotCount(8);
			if (Terrain.ExtractContents(coinSlotValue) != coinIndex || coinSlotCount < totalPrice)
			{
				Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
				string itemName = block.GetDisplayName(m_subsystemTerrain, value);
				string msg = string.Format(
					LanguageControl.GetContentWidgets("Trader", "InsufficientCoins"),
					totalPrice, itemName, count, pricePerItem);
				buyer.ComponentGui.DisplaySmallMessage(msg, Color.Red, true, false);
				m_subsystemAudio.PlaySound("Audio/UI/warning", 1f, 0f, 0f, 0f);
				return false;
			}

			int itemsToAdd = count;
			int tmpValue = value;
			while (itemsToAdd > 0)
			{
				int slot = ComponentInventoryBase.FindAcquireSlotForItem(buyer.ComponentMiner.Inventory, tmpValue);
				if (slot < 0)
				{
					buyer.ComponentGui.DisplaySmallMessage(
						LanguageControl.GetContentWidgets("Trader", "NotEnoughSpace"),
						Color.Red, true, false);
					m_subsystemAudio.PlaySound("Audio/UI/warning", 1f, 0f, 0f, 0f);
					return false;
				}
				itemsToAdd--;
			}

			BeginModification();

			RemoveSlotItems(8, totalPrice);

			itemsToAdd = count;
			while (itemsToAdd-- > 0)
			{
				int slot = ComponentInventoryBase.FindAcquireSlotForItem(buyer.ComponentMiner.Inventory, value);
				buyer.ComponentMiner.Inventory.AddSlotItems(slot, value, 1);
			}

			RemoveSlotItems(slotIndex, count);

			EndModification();

			buyer.ComponentGui.DisplaySmallMessage(
				LanguageControl.GetContentWidgets("Trader", "PurchaseSuccessful"),
				Color.Green, false, false);
			m_subsystemAudio.PlaySound("Audio/UI/money", 1f, 0f, 0f, 0f);
			return true;
		}
		public void Update(float dt)
		{
			if (m_subsystemTime != null && m_tradeItems != null && m_tradeItems.Count > 0)
			{
				if (m_subsystemTime.GameTime >= m_nextRestockTime)
				{
					Restock();
					m_nextRestockTime = m_subsystemTime.GameTime + m_restockInterval;
				}
			}
		}
	}
}
