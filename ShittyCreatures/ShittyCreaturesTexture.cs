using Engine;
using Engine.Graphics;

namespace Game
{
	public class ShittyCreaturesTexture : Block
	{
		private static Texture2D m_cachedTexture;

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			if (m_cachedTexture == null)
				m_cachedTexture = BlocksTexturesManager.LoadTexture("ShittyTextures");

			BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), ref matrix, color, color, environmentData, m_cachedTexture);
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateCubeVertices(this, value, x, y, z, Color.White, geometry.OpaqueSubsetsByFace);
		}
	}
}
