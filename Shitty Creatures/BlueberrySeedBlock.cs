using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class BlueberrySeedBlock : FlatBlock
	{
		public static int Index = 433;

		public override int GetFaceTextureSlot(int face, int value)
		{
			return 75;
		}

		public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain, ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
		{
			BlockPlacementData result = default(BlockPlacementData);
			result.CellFace = raycastResult.CellFace;

			if (raycastResult.CellFace.Face == 4)
			{
				// Verificar el bloque donde se plantará (debajo)
				int belowValue = subsystemTerrain.Terrain.GetCellValue(
					raycastResult.CellFace.Point.X,
					raycastResult.CellFace.Point.Y,
					raycastResult.CellFace.Point.Z
				);
				int belowContents = Terrain.ExtractContents(belowValue);
				Block belowBlock = BlocksManager.Blocks[belowContents];

				// Permitir: bloques suitability para plantas O tierra rastrillada (168)
				bool canPlant = belowBlock.IsSuitableForPlants(belowValue, value) || belowContents == 168;

				if (canPlant)
				{
					Block blueberryBushBlock = BlocksManager.GetBlock("BlueberryBushBlock");

					if (blueberryBushBlock != null)
					{
						result.Value = Terrain.MakeBlockValue(
							blueberryBushBlock.BlockIndex,
							0,
							BlueberryBushBlock.SetIsSmall(0, true)
						);
					}
				}
			}

			return result;
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			color *= new Color(100, 80, 130);
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, size, ref matrix, null, color, false, environmentData);
		}
	}
}
