using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public abstract class ShittyCreaturesBlock : Block
	{
		public virtual int DefaultTextureSlot => 0;

		public virtual int TextureSlotCount => 16;

		public virtual string TextureName => null;

		public override int GetFaceTextureSlot(int face, int value)
		{
			return DefaultTextureSlot;
		}

		public override int GetTextureSlotCount(int value)
		{
			return TextureSlotCount;
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			environmentData = environmentData ?? BlocksManager.m_defaultEnvironmentData;

			Texture2D texture;
			if (!string.IsNullOrEmpty(TextureName))
			{
				texture = ShittyCreaturesBlockManager.LoadTexture(TextureName);
			}
			else
			{
				texture = environmentData.SubsystemTerrain != null
					? environmentData.SubsystemTerrain.SubsystemAnimatedTextures.AnimatedBlocksTexture
					: ShittyCreaturesBlockManager.DefaultShittyCreaturesTexture;
			}

			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, size, ref matrix, texture, color, false, environmentData);
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateCubeVertices(this, value, x, y, z, Color.White, geometry.OpaqueSubsetsByFace);
		}
	}
}
