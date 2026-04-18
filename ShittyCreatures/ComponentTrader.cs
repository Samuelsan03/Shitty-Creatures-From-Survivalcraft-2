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
		private Dictionary<int, TradeItem> m_itemDataMap;
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
			public int Variant;
			public string CreatureTemplateName;
			public float Probability;
			public int Price;
			public int MaxCount;

			public int GetBlockValue()
			{
				if (BlockIndex == EggBlock.Index && !string.IsNullOrEmpty(CreatureTemplateName))
				{
					EggBlock eggBlock = (EggBlock)BlocksManager.Blocks[BlockIndex];
					var eggType = eggBlock.GetEggTypeByCreatureTemplateName(CreatureTemplateName);
					if (eggType != null)
					{
						int data = EggBlock.SetEggType(0, eggType.EggTypeIndex);
						data = EggBlock.SetIsLaid(data, false);
						return Terrain.MakeBlockValue(BlockIndex, 0, data);
					}
					return Terrain.MakeBlockValue(BlockIndex);
				}
				else if (Variant >= 0)
				{
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

				// Usar TotalElapsedGameTime (persistente) en lugar de GameTime (se reinicia)
				double worldTime = m_subsystemGameInfo.TotalElapsedGameTime;
				m_nextRestockTime = valuesDictionary.GetValue<double>("NextRestockTime", worldTime + m_restockInterval);

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
				Log.Warning("ComponentTrader: No se ha definido 'TradeItems' en la plantilla. El comerciante no tendrá objetos para vender.");
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
			int lineNumber = 0;
			foreach (string item in items)
			{
				lineNumber++;
				string trimmedItem = item.Trim();
				if (string.IsNullOrEmpty(trimmedItem)) continue;

				string[] parts = trimmedItem.Split(':');
				if (parts.Length < 4)
				{
					Log.Warning(string.Format("ComponentTrader: Formato incorrecto en elemento #{0}: '{1}'. Se esperaban al menos 4 partes separadas por ':'. Se omite este elemento.", lineNumber, trimmedItem));
					continue;
				}

				string blockName = parts[0].Trim();
				float prob;
				int price;
				int maxCount;
				int variant = -1;
				string templateName = null;

				if (!float.TryParse(parts[1], out prob))
				{
					Log.Warning(string.Format("ComponentTrader: Probabilidad inválida en elemento #{0}: '{1}'. Se omite.", lineNumber, parts[1]));
					continue;
				}
				if (!int.TryParse(parts[2], out price))
				{
					Log.Warning(string.Format("ComponentTrader: Precio inválido en elemento #{0}: '{1}'. Se omite.", lineNumber, parts[2]));
					continue;
				}
				if (!int.TryParse(parts[3], out maxCount))
				{
					Log.Warning(string.Format("ComponentTrader: Cantidad máxima inválida en elemento #{0}: '{1}'. Se omite.", lineNumber, parts[3]));
					continue;
				}

				int blockIndex = BlocksManager.GetBlockIndex(blockName, false);
				if (blockIndex < 0)
				{
					Log.Warning(string.Format("ComponentTrader: Nombre de bloque no encontrado '{0}' en elemento #{1}. Se omite.", blockName, lineNumber));
					continue;
				}

				// Procesar parámetros adicionales (variante o template de criatura)
				if (blockIndex == EggBlock.Index && parts.Length >= 5)
				{
					templateName = parts[4].Trim();
					if (string.IsNullOrEmpty(templateName))
					{
						Log.Warning(string.Format("ComponentTrader: Se especificó EggBlock pero el nombre de la criatura está vacío en elemento #{0}. Se usará huevo sin tipo específico.", lineNumber));
					}
				}
				else if (parts.Length >= 5)
				{
					if (!int.TryParse(parts[4], out variant))
					{
						Log.Warning(string.Format("ComponentTrader: Variante inválida '{0}' en elemento #{1}. Se usará -1 (sin variante).", parts[4], lineNumber));
						variant = -1;
					}
				}

				// Validaciones adicionales
				if (prob <= 0f)
				{
					Log.Warning(string.Format("ComponentTrader: Probabilidad <= 0 para '{0}' en elemento #{1}. El ítem nunca aparecerá.", blockName, lineNumber));
				}
				if (price < 0)
				{
					Log.Warning(string.Format("ComponentTrader: Precio negativo para '{0}' en elemento #{1}. Se ajustará a 0.", blockName, lineNumber));
					price = 0;
				}
				if (maxCount <= 0)
				{
					Log.Warning(string.Format("ComponentTrader: Cantidad máxima <= 0 para '{0}' en elemento #{1}. Se ajustará a 1.", blockName, lineNumber));
					maxCount = 1;
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

			if (m_tradeItems.Count == 0)
			{
				Log.Error("ComponentTrader: No se pudo parsear ningún TradeItem válido. El comerciante estará vacío.");
			}
		}

		private void BuildItemDataMap()
		{
			m_itemDataMap = new Dictionary<int, TradeItem>();
			foreach (var item in m_tradeItems)
			{
				int value = item.GetBlockValue();
				if (!m_itemDataMap.ContainsKey(value))
					m_itemDataMap[value] = item;
			}
		}

		private int GetRandomOccupiedSlotsCount()
		{
			float r = m_random.Float();

			if (r < 0.05f) return 1;
			else if (r < 0.35f) return m_random.Int(2, 3);
			else if (r < 0.75f) return m_random.Int(4, 5);
			else if (r < 0.95f) return m_random.Int(6, 7);
			else return 8;
		}

		public override void DropAllItems(Vector3 position)
		{
			int coinValue = GetSlotValue(8);
			int coinCount = GetSlotCount(8);
			if (coinValue != 0 && coinCount > 0)
			{
				SubsystemPickables subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
				subsystemPickables.AddPickable(coinValue, coinCount, position, null, null, Entity);
				RemoveSlotItems(8, coinCount);
			}
		}

		private void Restock()
		{
			if (m_tradeItems == null || m_tradeItems.Count == 0) return;

			for (int i = 0; i < 8; i++)
			{
				m_slots[i].Value = 0;
				m_slots[i].Count = 0;
			}

			List<TradeItem> availableItems = new List<TradeItem>(m_tradeItems);
			int slotsToFill = GetRandomOccupiedSlotsCount();

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

			for (int i = 0; i < slotsToFill; i++)
			{
				int selectedSlot = slotIndices[i];

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
				int currentCount = GetSlotCount(slotIndex);
				int maxCapacity = GetSlotCapacity(slotIndex, value);

				if (currentCount + count > maxCapacity)
				{
					count = maxCapacity - currentCount;
					if (count <= 0) return;
				}

				base.AddSlotItems(slotIndex, value, count);
			}
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

		public double TimeUntilRestock
		{
			get
			{
				if (m_subsystemGameInfo == null)
					return 0.0;
				double remaining = m_nextRestockTime - m_subsystemGameInfo.TotalElapsedGameTime;
				return remaining > 0.0 ? remaining : 0.0;
			}
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

			int totalPrice = pricePerItem;
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
			while (itemsToAdd > 0)
			{
				int slot = ComponentInventoryBase.FindAcquireSlotForItem(buyer.ComponentMiner.Inventory, value);
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
			if (m_subsystemGameInfo != null && m_tradeItems != null && m_tradeItems.Count > 0)
			{
				if (m_subsystemGameInfo.TotalElapsedGameTime >= m_nextRestockTime)
				{
					Restock();
					m_nextRestockTime = m_subsystemGameInfo.TotalElapsedGameTime + m_restockInterval;
				}
			}
		}
	}
}
