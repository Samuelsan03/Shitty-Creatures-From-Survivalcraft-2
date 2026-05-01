using System;
using Engine;
using Engine.Graphics;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemBowlBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					m_emptyBowlBlockIndex,
					m_waterBowlBlockIndex,
				};
			}
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			IInventory inventory = componentMiner.Inventory;
			int activeBlockValue = componentMiner.ActiveBlockValue;
			int num = Terrain.ExtractContents(activeBlockValue);

			// CASO 1: Cuenco vacío -> intentar llenar con agua (fuente)
			if (num == m_emptyBowlBlockIndex)
			{
				// Raycast para recoger agua (modo Gathering)
				object hit = componentMiner.Raycast(ray, RaycastMode.Gathering, true, true, true, null);
				if (hit is TerrainRaycastResult terrainHit)
				{
					CellFace cellFace = terrainHit.CellFace;
					int cellValue = SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
					int contents = Terrain.ExtractContents(cellValue);
					int data = Terrain.ExtractData(cellValue);

					// Solo fuentes de agua (nivel 0)
					if (contents == m_waterBlockIndex && FluidBlock.GetLevel(data) == 0)
					{
						int slotCount = inventory.GetSlotCount(inventory.ActiveSlotIndex);
						if (slotCount <= 1)
						{
							// Reemplazar el slot actual
							inventory.RemoveSlotItems(inventory.ActiveSlotIndex, slotCount);
							if (inventory.GetSlotCount(inventory.ActiveSlotIndex) == 0)
								inventory.AddSlotItems(inventory.ActiveSlotIndex, m_waterBowlBlockIndex, 1);
						}
						else
						{
							// Quitar uno del slot activo y buscar otro slot para el bowl lleno
							inventory.RemoveSlotItems(inventory.ActiveSlotIndex, 1);
							int slot = ComponentInventoryBase.FindAcquireSlotForItem(inventory, m_waterBowlBlockIndex);
							if (slot >= 0)
							{
								inventory.AddSlotItems(slot, m_waterBowlBlockIndex, 1);
							}
							else
							{
								// No hay espacio, devolver el bowl vacío
								inventory.AddSlotItems(inventory.ActiveSlotIndex, activeBlockValue, 1);
								return false;
							}
						}
						// Destruir la fuente de agua
						SubsystemTerrain.DestroyCell(0, cellFace.X, cellFace.Y, cellFace.Z, 0, false, false, null);
						return true;
					}
				}
				return false;
			}

			// CASO 2: Cuenco con agua -> vaciarlo (colocar agua)
			if (num == m_waterBowlBlockIndex)
			{
				TerrainRaycastResult? placeResult = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
				if (placeResult != null)
				{
					int slotCount = inventory.GetSlotCount(inventory.ActiveSlotIndex);
					if (slotCount > 1)
					{
						// Intentar colocar el agua con el primer intento
						inventory.RemoveSlotItems(inventory.ActiveSlotIndex, 1);
						int emptySlot = ComponentInventoryBase.FindAcquireSlotForItem(inventory, m_emptyBowlBlockIndex);
						if (emptySlot >= 0 && componentMiner.Place(placeResult.Value, Terrain.MakeBlockValue(m_waterBlockIndex)))
						{
							// Colocación exitosa: añadir bowl vacío en el slot encontrado
							inventory.AddSlotItems(emptySlot, m_emptyBowlBlockIndex, 1);
							return true;
						}
						else
						{
							// Falló: devolver el bowl con agua al slot activo
							inventory.AddSlotItems(inventory.ActiveSlotIndex, activeBlockValue, 1);
							return false;
						}
					}
					else
					{
						// Es el último bowl: intentar colocar el agua
						if (componentMiner.Place(placeResult.Value, Terrain.MakeBlockValue(m_waterBlockIndex)))
						{
							// Éxito: reemplazar el slot activo por bowl vacío
							inventory.RemoveSlotItems(inventory.ActiveSlotIndex, slotCount);
							if (inventory.GetSlotCount(inventory.ActiveSlotIndex) == 0)
								inventory.AddSlotItems(inventory.ActiveSlotIndex, m_emptyBowlBlockIndex, 1);
							return true;
						}
						else
						{
							return false;
						}
					}
				}
				return false;
			}

			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_waterBlockIndex = BlocksManager.GetBlockIndex<WaterBlock>(false, false);
			m_emptyBowlBlockIndex = BlocksManager.GetBlockIndex<EmptyBowlBlock>(false, false);
			m_waterBowlBlockIndex = BlocksManager.GetBlockIndex<WaterBowlBlock>(false, false);
		}

		public int m_emptyBowlBlockIndex;
		public int m_waterBowlBlockIndex;
		public int m_waterBlockIndex;
	}
}
