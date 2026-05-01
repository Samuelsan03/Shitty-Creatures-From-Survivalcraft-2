using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class AntiTanksBulletBlock : FlatBlock
	{
		public static int Index = 329;

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Tamaño: 2.5, Color: RGB(159, 88, 140)
			float adjustedSize = size * 2.5f;
			Color bulletColor = new Color(159, 88, 140);
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, adjustedSize, ref matrix, null, bulletColor, false, environmentData);
		}

		public override float GetProjectilePower(int value)
		{
			return 1000000f;
		}

		public override float GetExplosionPressure(int value)
		{
			return 0f;
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(Index, 0, 0);
		}


		public override int GetFaceTextureSlot(int face, int value)
		{
			return 229; // Misma textura que MusketBall
		}
	}
}
