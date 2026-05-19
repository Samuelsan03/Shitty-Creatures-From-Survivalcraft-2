using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class BlueberrySeedBlock : FlatBlock
	{
		public static int Index = 433;

		public override IEnumerable<int> GetCreativeValues()
		{
			return new int[] { Terrain.MakeBlockValue(Index, 0, 0) };
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			return 75; // Ranura de textura de la semilla
		}

		public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain, ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
		{
			BlockPlacementData result = default(BlockPlacementData);
			result.CellFace = raycastResult.CellFace;
			if (raycastResult.CellFace.Face == 4) // Solo en la cara superior (tierra)
			{
				// Colocar arbusto en estado pequeño: data = 1 (bit 0 activado)
				int smallData = FlowerBlock.SetIsSmall(0, true); // Devuelve 1
				result.Value = Terrain.MakeBlockValue(BlueberryBushBlock.Index, 0, smallData);
			}
			return result;
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			color *= new Color(70, 90, 150); // Tinte azulado para la semilla
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, size, ref matrix, null, color, false, environmentData);
		}
	}
}