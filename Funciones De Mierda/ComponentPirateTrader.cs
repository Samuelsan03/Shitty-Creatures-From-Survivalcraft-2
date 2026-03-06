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

		// Control de modificaciones permitidas (solo para slots de venta)
		private int m_modificationLock = 0;

		// Almacena el slot seleccionado para el resaltado visual
		private int m_selectedSlotIndex = -1;

		public override int SlotsCount => 9; // Slot 8 es para monedas

		// Anular ActiveSlotIndex para que realmente guarde el índice seleccionado,
		// pero solo permitir valores entre 0 y 7 (slots de venta)
		public override int ActiveSlotIndex
		{
			get { return m_selectedSlotIndex; }
			set
			{
				if (value >= 0 && value < 8)
					m_selectedSlotIndex = value;
				// Si se intenta establecer a 8 o cualquier otro, se ignora
			}
		}

		// Métodos para permitir modificaciones internas
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

		// Solo se permite modificar slot 8, si hay bloqueo interno, o si el mundo es creativo
		public override void AddSlotItems(int slotIndex, int value, int count)
		{
			bool isCreative = (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative);
			if (slotIndex == 8 || m_modificationLock > 0 || isCreative)
				base.AddSlotItems(slotIndex, value, count);
			// Si no, no hace nada (el item no se añade)
		}

		public override int RemoveSlotItems(int slotIndex, int count)
		{
			bool isCreative = (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative);
			if (slotIndex == 8 || m_modificationLock > 0 || isCreative)
				return base.RemoveSlotItems(slotIndex, count);
			return 0; // No se quita nada
		}

		public override int GetSlotCapacity(int slotIndex, int value)
		{
			bool isCreative = (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative);

			if (slotIndex == 8)
			{
				// Solo nuclearcoin
				int coinIndex = BlocksManager.GetBlockIndex(typeof(NuclearCoinBlock).Name, true);
				if (Terrain.ExtractContents(value) != coinIndex)
					return 0;

				// Capacidad fija de 100.000 para monedas, ignorando el stacking original
				return 100000;
			}
			else // slots 0-7
			{
				// En modo creativo, se permite modificar siempre
				if (isCreative)
				{
					int baseCapacity = base.GetSlotCapacity(slotIndex, value);
					if (baseCapacity <= 1)
						return baseCapacity; // items no apilables (ej. herramientas) mantienen su límite
					return 100000; // items apilables pueden acumular hasta 100.000
				}

				// En modo no creativo, solo si hay bloqueo interno (durante compra)
				if (m_modificationLock == 0)
					return 0; // No se permite meter items a menos que estemos en compra

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
				buyer.ComponentGui.DisplaySmallMessage("This item is no longer available.", Color.Red, true, true);
				m_subsystemAudio.PlaySound("Audio/UI/warning", 1f, 0f, 0f, 0f);
				return false;
			}

			int pricePerItem = GetPrice(slotIndex);
			if (pricePerItem <= 0)
			{
				buyer.ComponentGui.DisplaySmallMessage("This item cannot be bought.", Color.Red, true, true);
				m_subsystemAudio.PlaySound("Audio/UI/warning", 1f, 0f, 0f, 0f);
				return false;
			}

			int totalPrice = pricePerItem * count;
			int coinIndex = BlocksManager.GetBlockIndex(typeof(NuclearCoinBlock).Name, true);

			int coinSlotValue = GetSlotValue(8);
			int coinSlotCount = GetSlotCount(8);
			if (Terrain.ExtractContents(coinSlotValue) != coinIndex || coinSlotCount < totalPrice)
			{
				buyer.ComponentGui.DisplaySmallMessage($"Need {totalPrice} nuclear coins in payment slot. Have: {coinSlotCount}", Color.Red, true, true);
				m_subsystemAudio.PlaySound("Audio/UI/warning", 1f, 0f, 0f, 0f);
				return false;
			}

			// Verificar espacio en inventario del jugador
			int itemsToAdd = count;
			int tmpValue = value;
			while (itemsToAdd > 0)
			{
				int slot = ComponentInventoryBase.FindAcquireSlotForItem(buyer.ComponentMiner.Inventory, tmpValue);
				if (slot < 0)
				{
					buyer.ComponentGui.DisplaySmallMessage("Not enough space in inventory.", Color.Red, true, true);
					m_subsystemAudio.PlaySound("Audio/UI/warning", 1f, 0f, 0f, 0f);
					return false;
				}
				itemsToAdd--;
			}

			// --- Bloqueamos modificaciones para permitir cambios internos ---
			BeginModification();

			// Remover monedas
			RemoveSlotItems(8, totalPrice);

			// Entregar items al jugador (esto no necesita bloqueo porque es otro inventory)
			itemsToAdd = count;
			while (itemsToAdd-- > 0)
			{
				int slot = ComponentInventoryBase.FindAcquireSlotForItem(buyer.ComponentMiner.Inventory, value);
				buyer.ComponentMiner.Inventory.AddSlotItems(slot, value, 1);
			}

			// Remover item del trader
			RemoveSlotItems(slotIndex, count);

			EndModification();
			// --- Fin del bloqueo ---

			buyer.ComponentGui.DisplaySmallMessage("Purchase successful!", Color.Green, false, false);
			m_subsystemAudio.PlaySound("Audio/UI/money", 1f, 0f, 0f, 0f);
			return true;
		}
	}
}
