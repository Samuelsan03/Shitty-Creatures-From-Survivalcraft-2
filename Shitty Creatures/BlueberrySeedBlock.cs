using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class BlueberrySeedBlock : FlatBlock
	{
		public static int Index = 431;

		public override IEnumerable<int> GetCreativeValues()
		{
			return new int[] { Terrain.MakeBlockValue(Index, 0, 0) };
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			// Usar una ranura de textura específica para la semilla de arándano
			return 75; // Puedes cambiar si existe otra textura
		}

		public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain, ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
		{
			BlockPlacementData result = default(BlockPlacementData);
			result.CellFace = raycastResult.CellFace;
			if (raycastResult.CellFace.Face == 4) // Solo colocar en la cara superior
			{
				// Colocar un arbusto de arándano (BlueberryBushBlock) con tamaño 0 (pequeño)
				result.Value = Terrain.MakeBlockValue(422, 0, 0);
			}
			return result;
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Tinte opcional para la semilla
			color *= new Color(70, 90, 150); // Color azulado
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, size, ref matrix, null, color, false, environmentData);
		}
	}
}
