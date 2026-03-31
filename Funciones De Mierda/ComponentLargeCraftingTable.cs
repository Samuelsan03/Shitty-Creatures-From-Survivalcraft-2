using System;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentLargeCraftingTable : ComponentInventoryBase
	{
		public const int CraftingGridWidth = 5;
		public const int CraftingGridHeight = 5;
		public const int CraftingGridSlotsCount = CraftingGridWidth * CraftingGridHeight;
		public const int ResultSlotIndex = CraftingGridSlotsCount;
		public const int RemainsSlotIndex = ResultSlotIndex + 1;

		private LargeCraftingRecipe m_currentRecipe;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			int slotsCount = valuesDictionary.GetValue<int>("SlotsCount");
			if (slotsCount != CraftingGridSlotsCount + 2)
				throw new InvalidOperationException($"LargeCraftingTable debe tener {CraftingGridSlotsCount + 2} slots, pero tiene {slotsCount}.");
			base.Load(valuesDictionary, idToEntityMap);
		}

		// Evita que se puedan añadir objetos al slot de resultado (capacidad 0)
		public override int GetSlotCapacity(int slotIndex, int value)
		{
			if (slotIndex == ResultSlotIndex)
				return 0;
			return base.GetSlotCapacity(slotIndex, value);
		}

		// Actualiza el slot de resultado en función de la cuadrícula actual
		public void UpdateResultSlot()
		{
			// Recolectar ingredientes de la cuadrícula 5x5
			string[] ingredients = new string[CraftingGridSlotsCount];
			for (int i = 0; i < CraftingGridSlotsCount; i++)
			{
				int value = GetSlotValue(i);
				if (value != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
					ingredients[i] = block.GetCraftingId(value);
				}
				else
				{
					ingredients[i] = null;
				}
			}

			// Buscar receta
			LargeCraftingRecipe recipe = LargeCraftingRecipesManager.FindMatchingRecipe(
				Project.FindSubsystem<SubsystemTerrain>(true),
				ingredients,
				0f,  // heatLevel (no usado)
				1f   // playerLevel (no usado)
			);

			if (recipe != null)
			{
				// Establecer resultado en el slot correspondiente
				m_slots[ResultSlotIndex].Value = recipe.ResultValue;
				m_slots[ResultSlotIndex].Count = recipe.ResultCount;
				m_currentRecipe = recipe;
			}
			else
			{
				m_slots[ResultSlotIndex].Value = 0;
				m_slots[ResultSlotIndex].Count = 0;
				m_currentRecipe = null;
			}
		}

		// Maneja la extracción del resultado (consumir ingredientes, generar resto)
		private bool TakeResult(int requestedCount)
		{
			if (m_currentRecipe == null)
				return false;

			// Consumir un stack de cada ingrediente (1 unidad de cada)
			for (int i = 0; i < CraftingGridSlotsCount; i++)
			{
				if (!string.IsNullOrEmpty(m_currentRecipe.Ingredients[i]))
				{
					int currentCount = GetSlotCount(i);
					if (currentCount > 0)
						RemoveSlotItems(i, 1);
				}
			}

			// Generar resto si existe
			if (m_currentRecipe.RemainsValue != 0 && m_currentRecipe.RemainsCount > 0)
			{
				int remainsValue = m_currentRecipe.RemainsValue;
				int remainsCount = m_currentRecipe.RemainsCount;

				int currentRemainsValue = GetSlotValue(RemainsSlotIndex);
				int currentRemainsCount = GetSlotCount(RemainsSlotIndex);

				if (currentRemainsValue == 0)
				{
					m_slots[RemainsSlotIndex].Value = remainsValue;
					m_slots[RemainsSlotIndex].Count = remainsCount;
				}
				else if (currentRemainsValue == remainsValue)
				{
					int newCount = currentRemainsCount + remainsCount;
					int maxStack = BlocksManager.Blocks[Terrain.ExtractContents(remainsValue)].GetMaxStacking(remainsValue);
					if (newCount <= maxStack)
						m_slots[RemainsSlotIndex].Count = newCount;
					else
						m_slots[RemainsSlotIndex].Count = maxStack;
				}
			}

			// Limpiar slot de resultado y receta actual
			m_slots[ResultSlotIndex].Value = 0;
			m_slots[ResultSlotIndex].Count = 0;
			m_currentRecipe = null;

			// Recalcular resultado por si queda algún ingrediente
			UpdateResultSlot();
			return true;
		}

		// --- Sobrecargas para detectar cambios en la cuadrícula de crafteo ---

		public override void AddSlotItems(int slotIndex, int value, int count)
		{
			base.AddSlotItems(slotIndex, value, count);
			if (slotIndex < CraftingGridSlotsCount)
				UpdateResultSlot();
		}

		public override int RemoveSlotItems(int slotIndex, int count)
		{
			// Si se intenta sacar del slot de resultado, manejar la receta
			if (slotIndex == ResultSlotIndex)
			{
				if (TakeResult(count))
					return count;   // Se ha "consumido" el resultado
				else
					return 0;       // No se pudo tomar (no había receta)
			}
			int removed = base.RemoveSlotItems(slotIndex, count);
			if (slotIndex < CraftingGridSlotsCount && removed > 0)
				UpdateResultSlot();
			return removed;
		}

		public override void ProcessSlotItems(int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			base.ProcessSlotItems(slotIndex, value, count, processCount, out processedValue, out processedCount);
			if (slotIndex < CraftingGridSlotsCount)
				UpdateResultSlot();
		}
	}
}
