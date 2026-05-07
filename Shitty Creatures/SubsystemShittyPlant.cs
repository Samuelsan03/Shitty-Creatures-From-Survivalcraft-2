using System;
using Engine;

namespace Game
{
	public class SubsystemShittyPlant : SubsystemPollableBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlueberryBushBlock.Index // 422
                    // Añade aquí más índices de otras plantas planas si las creas
                };
			}
		}

		public sealed override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
		{
			// Solo reaccionar si cambió el bloque de abajo
			if (neighborY == y - 1)
			{
				int cellValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y, z);
				int belowValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
				Block belowBlock = BlocksManager.Blocks[Terrain.ExtractContents(belowValue)];

				// Si el bloque inferior ya no es adecuado para plantas, destruir este
				if (!belowBlock.IsSuitableForPlants(belowValue, cellValue))
				{
					base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
				}
			}
		}

		public override void OnPoll(int value, int x, int y, int z, int pollPass)
		{
			// Sin lógica de crecimiento por ahora
		}

		public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
		{
			// Sin lógica especial al generar
		}
	}
}
