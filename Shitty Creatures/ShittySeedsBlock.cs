using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class ShittySeedsBlock : FlatBlock
	{
		public static int Index = 431;

		public override IEnumerable<int> GetCreativeValues()
		{
			List<int> list = new List<int>();
			foreach (int data in EnumUtils.GetEnumValues<ShittySeedsBlock.SeedType>())
			{
				list.Add(Terrain.MakeBlockValue(Index, 0, data));
			}
			return list;
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
				if (Terrain.ExtractData(value) == 0)
				{
					result.Value = Terrain.MakeBlockValue(BlueberryBushBlock.Index, 0, 0);
				}
			}
			return result;
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			color *= new Color(50, 60, 120);
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, size, ref matrix, null, color, false, environmentData);
		}

		public enum SeedType
		{
			Blueberry
		}
	}
}
