using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class WatermelonSeedBlock : FlatBlock
	{
		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(WatermelonSeedBlock.Index, 0, 0);
		}

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
				result.Value = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("WatermelonBlock"), 0, BaseWatermelonBlock.SetSize(BaseWatermelonBlock.SetIsDead(0, false), 0));
			}
			return result;
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			color *= new Color(40, 100, 40);
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, size, ref matrix, null, color, false, environmentData);
		}

		public static int Index = 554;
	}
}
