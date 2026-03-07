using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentPirateTrader : ComponentInventoryBase
	{
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemPickables m_subsystemPickables;
		private SubsystemGameInfo m_subsystemGameInfo;

		private int m_modificationLock = 0;
		private int m_selectedSlotIndex = -1;

		// Bandera para detectar si hay un arrastre en curso (actualizada desde el widget)
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

			bool anyItem = false;
			for (int i = 0; i < SlotsCount; i++)
				if (GetSlotValue(i) != 0) { anyItem = true; break; }

			if (!anyItem)
			{
				int musketIndex = BlocksManager.GetBlockIndex(typeof(MusketBlock).Name, false);
				if (musketIndex >= 0)
					AddSlotItems(0, Terrain.MakeBlockValue(musketIndex), 1);
			}
		}

		public override void AddSlotItems(int slotIndex, int value, int count)
		{
			bool isCreative = (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative);

			// En modo creativo, si se intenta añadir 1 a un slot vacío y hay un arrastre en curso, se pone la capacidad máxima
			if (isCreative && count == 1 && GetSlotCount(slotIndex) == 0 && IsDragInProgress)
			{
				int capacity = GetSlotCapacity(slotIndex, value);
				if (capacity > 1)
					count = capacity; // Poner 100.000 en lugar de 1
			}

			if (slotIndex == 8 || m_modificationLock > 0 || isCreative)
				base.AddSlotItems(slotIndex, value, count);
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
			return (BlocksManager.Blocks[Terrain.ExtractContents(value)] is MusketBlock) ? 50 : 0;
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

			int totalPrice = pricePerItem * count;
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
	}
}
