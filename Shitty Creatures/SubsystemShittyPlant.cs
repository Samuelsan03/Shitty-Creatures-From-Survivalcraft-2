using System;
using System.Linq;
using Engine;

namespace Game
{
	public class SubsystemShittyPlant : SubsystemPollableBlockBehavior
	{
		// Se llenará en el constructor estático con los índices reales
		private static readonly int[] FruitIndices;

		static SubsystemShittyPlant()
		{
			// Obtiene los índices de los bloques de fruta consultando al BlocksManager.
			// Esto es inmune a cambios de índices por carga de CSV o conflictos de mods.
			FruitIndices = new int[]
			{
				BlocksManager.GetBlockIndex("AppleBlock", true),
				BlocksManager.GetBlockIndex("PearBlock", true),
				BlocksManager.GetBlockIndex("OrangeBlock", true),
				BlocksManager.GetBlockIndex("CherryBlock", true)
			};
		}

		public override int[] HandledBlocks
		{
			get
			{
				var list = new System.Collections.Generic.List<int> { BlueberryBushBlock.Index };
				list.AddRange(FruitIndices);
				return list.ToArray();
			}
		}

		public sealed override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
		{
			int cellValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y, z);
			int contents = Terrain.ExtractContents(cellValue);

			if (FruitIndices.Contains(contents))
			{
				// FRUTA: Depende del bloque de ARRIBA (la hoja)
				if (neighborY == y + 1)
				{
					int aboveValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
					if (Terrain.ExtractContents(aboveValue) == 0)
					{
						base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
					}
				}
			}
			else
			{
				// ARBUSTO: Depende del bloque de ABAJO (tierra/hierba)
				if (neighborY == y - 1)
				{
					int belowValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
					Block belowBlock = BlocksManager.Blocks[Terrain.ExtractContents(belowValue)];

					if (!belowBlock.IsSuitableForPlants(belowValue, cellValue))
					{
						base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
					}
				}
			}
		}

		public override void OnPoll(int value, int x, int y, int z, int pollPass)
		{
		}

		public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
		{
			if (isLoaded) return;
			int contents = Terrain.ExtractContents(value);
			if (FruitIndices.Contains(contents))
			{
				int aboveValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
				if (Terrain.ExtractContents(aboveValue) == 0)
				{
					base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
				}
			}
		}
	}
}
