using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public abstract class BowlBlock : Block
	{
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			// Los bowls son objetos 3D, no generan geometría en el terreno
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Se implementa en las clases hijas
		}
	}
}
