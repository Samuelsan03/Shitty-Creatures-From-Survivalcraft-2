using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentPirateTrader : ComponentInventoryBase, IUpdateable
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
			public int Variant; // -1 significa sin variante específica
			public float Probability;
			public int Price;
			public int MaxCount;

			public int GetBlockValue()
			{
				if (Variant >= 0)
				{
					// Aplicar la variante al bloque
					Block block = BlocksManager.Blocks[BlockIndex];
					return Terrain.MakeBlockValue(BlockIndex, 0, Variant);
				}
				else
				{
					// Sin variante específica, usar el bloque por defecto
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
				Log.Warning("PirateTrader: No TradeItems defined in XDB. Trader will have no items.");
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

				// Formatos soportados:
				// - BlockName:prob:price:maxCount (sin variante)
				// - BlockName:prob:price:maxCount:variant (con variante)

				if (parts.Length >= 4)
				{
					string blockName = parts[0].Trim();
					float prob;
					int price;
					int maxCount;
					int variant = -1; // -1 significa sin variante específica

					if (float.TryParse(parts[1], out prob) &&
						int.TryParse(parts[2], out price) &&
						int.TryParse(parts[3], out maxCount))
					{
						// Si hay una quinta parte, es la variante
						if (parts.Length >= 5)
						{
							int.TryParse(parts[4], out variant);
						}

						int blockIndex = BlocksManager.GetBlockIndex(blockName, false);
						if (blockIndex >= 0)
						{
							m_tradeItems.Add(new TradeItem
							{
								BlockIndex = blockIndex,
								Variant = variant,
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
				// Para el mapa de datos, usamos BlockIndex + Variant como clave compuesta?
				// Pero para GetSlotCapacity necesitamos identificar el item por su BlockIndex+data
				// Mejor guardar por BlockIndex y luego comparar también la variante
				if (!m_itemDataMap.ContainsKey(item.BlockIndex))
					m_itemDataMap[item.BlockIndex] = item;
				// Nota: Esto asume que un mismo bloque no tiene múltiples entradas con diferentes precios/cantidades
				// Si quieres diferentes precios por variante, necesitarías una clave compuesta
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

			// Calcular probabilidad total de items
			float totalProb = 0f;
			foreach (var item in m_tradeItems)
				totalProb += item.Probability;
			if (totalProb <= 0) return;

			// Limpiar todas las ranuras
			for (int i = 0; i < 8; i++)
			{
				m_slots[i].Value = 0;
				m_slots[i].Count = 0;
			}

			// Determinar cuántos slots van a estar ocupados (1-8)
			int slotsToFill = GetRandomOccupiedSlotsCount();

			// Crear lista de índices disponibles (0-7)
			List<int> availableSlots = new List<int>();
			for (int i = 0; i < 8; i++)
				availableSlots.Add(i);

			// Llenar la cantidad determinada de slots
			for (int i = 0; i < slotsToFill; i++)
			{
				if (availableSlots.Count == 0) break;

				// Seleccionar un slot aleatorio de los disponibles
				int slotIndex = m_random.Int(0, availableSlots.Count - 1);
				int selectedSlot = availableSlots[slotIndex];
				availableSlots.RemoveAt(slotIndex);

				// Seleccionar un item aleatorio según probabilidades
				float r = m_random.Float() * totalProb;
				float accum = 0f;
				foreach (var item in m_tradeItems)
				{
					accum += item.Probability;
					if (r < accum)
					{
						m_slots[selectedSlot].Value = item.GetBlockValue();
						m_slots[selectedSlot].Count = item.MaxCount;
						break;
					}
				}
			}
		}

		public override void AddSlotItems(int slotIndex, int value, int count)
		{
			bool isCreative = (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative);

			if (isCreative && count == 1 && GetSlotCount(slotIndex) == 0 && IsDragInProgress)
			{
				int capacity = GetSlotCapacity(slotIndex, value);
				if (capacity > 1)
					count = capacity;
			}

			if (slotIndex == 8 || m_modificationLock > 0 || isCreative)
			{
				int currentCount = GetSlotCount(slotIndex);
				int capacity = GetSlotCapacity(slotIndex, value);
				if (currentCount + count > capacity)
				{
					count = capacity - currentCount;
					if (count <= 0) return;
				}
				base.AddSlotItems(slotIndex, value, count);
			}
		}

		public override int RemoveSlotItems(int slotIndex, int count)
		{
			bool isCreative = (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative);
			if (slotIndex == 8 || m_modificationLock > 0 || isCreative)
				return base.RemoveSlotItems(slotIndex, count);
			return 0;
		}

		public override int GetSlotCapacity(int slotIndex, int value)
		{
			bool isCreative = (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative);

			if (slotIndex == 8)
			{
				int coinIndex = BlocksManager.GetBlockIndex(typeof(NuclearCoinBlock).Name, true);
				if (Terrain.ExtractContents(value) != coinIndex)
					return 0;
				return 100000;
			}
			else
			{
				if (isCreative)
				{
					int baseCapacity = base.GetSlotCapacity(slotIndex, value);
					if (baseCapacity <= 1)
						return baseCapacity;
					return 100000;
				}

				if (m_modificationLock == 0)
					return 0;

				int blockIndex = Terrain.ExtractContents(value);
				if (m_itemDataMap != null && m_itemDataMap.ContainsKey(blockIndex))
				{
					// Para simplificar, usamos el MaxCount del primer TradeItem que coincida con el BlockIndex
					// Si necesitas diferentes capacidades por variante, habría que mejorar esto
					return m_itemDataMap[blockIndex].MaxCount;
				}

				int baseCapacity2 = base.GetSlotCapacity(slotIndex, value);
				if (baseCapacity2 <= 1)
					return baseCapacity2;
				return 100000;
			}
		}

		public override int GetSlotProcessCapacity(int slotIndex, int value)
		{
			bool isCreative = (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative);
			if (slotIndex == 8)
			{
				int coinIndex = BlocksManager.GetBlockIndex(typeof(NuclearCoinBlock).Name, true);
				if (Terrain.ExtractContents(value) != coinIndex)
					return 0;
				return base.GetSlotProcessCapacity(slotIndex, value);
			}
			else
			{
				if (isCreative || m_modificationLock > 0)
					return base.GetSlotProcessCapacity(slotIndex, value);
				return 0;
			}
		}

		public virtual int GetPrice(int slotIndex)
		{
			if (slotIndex == 8) return 0;
			int value = GetSlotValue(slotIndex);
			if (value == 0) return 0;
			int blockIndex = Terrain.ExtractContents(value);
			if (m_itemDataMap != null && m_itemDataMap.ContainsKey(blockIndex))
				return m_itemDataMap[blockIndex].Price;
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
					LanguageControl.GetContentWidgets("PirateTraderWidget", "ItemNoLongerAvailable"),
					Color.Red, true, true);
				m_subsystemAudio.PlaySound("Audio/UI/warning", 1f, 0f, 0f, 0f);
				return false;
			}

			int pricePerItem = GetPrice(slotIndex);
			if (pricePerItem <= 0)
			{
				buyer.ComponentGui.DisplaySmallMessage(
					LanguageControl.GetContentWidgets("PirateTraderWidget", "ItemCannotBeBought"),
					Color.Red, true, true);
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
					LanguageControl.GetContentWidgets("PirateTraderWidget", "InsufficientCoins"),
					totalPrice, itemName, count, pricePerItem);
				buyer.ComponentGui.DisplaySmallMessage(msg, Color.Red, true, true);
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
						LanguageControl.GetContentWidgets("PirateTraderWidget", "NotEnoughSpace"),
						Color.Red, true, true);
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
				LanguageControl.GetContentWidgets("PirateTraderWidget", "PurchaseSuccessful"),
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
