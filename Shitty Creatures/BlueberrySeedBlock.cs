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
			// Usar el slot de textura 75 que es la forma genérica de semillas
			return 75;
		}

		public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain, ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
		{
			BlockPlacementData result = default(BlockPlacementData);
			result.CellFace = raycastResult.CellFace;

			if (raycastResult.CellFace.Face == 4)
			{
				// Usar BlocksManager con el NOMBRE del bloque para obtener el índice
				Block blueberryBushBlock = BlocksManager.GetBlock("BlueberryBushBlock");

				if (blueberryBushBlock != null)
				{
					// Colocar el arbusto en estado pequeño
					result.Value = Terrain.MakeBlockValue(
						blueberryBushBlock.BlockIndex,
						0,
						BlueberryBushBlock.SetIsSmall(0, true)
					);
				}
			}

			return result;
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Color para las semillas de arándano - tono azulado/morado
			color *= new Color(100, 80, 130);
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, size, ref matrix, null, color, false, environmentData);
		}
	}
}
