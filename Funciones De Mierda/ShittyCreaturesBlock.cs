using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class ShittyCreaturesBlock : Block
	{
		public int TextureSlot { get; set; } = 0;

		public ShittyCreaturesBlock()
		{
			IsCollidable = true;
			IsPlaceable = true;
			IsTransparent = false;
			GenerateFacesForSameNeighbors = false;
			DefaultTextureSlot = TextureSlot;
			Density = 1f;
			DigResilience = 1f;
			DefaultDropContent = 0;
			DefaultDropCount = 0;
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			return TextureSlot;
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			// Obtener la textura del manager
			Texture2D texture = ShittyCreaturesBlockManager.AnimatedBlocksTexture ?? ShittyCreaturesBlockManager.BlocksTexture;

			// Obtener el geometry específico para esta textura
			TerrainGeometry targetGeometry = geometry.GetGeometry(texture);

			// Color base para las caras (la iluminación se aplicará internamente)
			Color sideColor = Color.White;

			// Elegir los subsets según el tipo de transparencia
			if (IsTransparent_(value))
			{
				generator.GenerateCubeVertices(this, value, x, y, z, sideColor, targetGeometry.TransparentSubsetsByFace);
			}
			else if (IsDiggingTransparent)
			{
				generator.GenerateCubeVertices(this, value, x, y, z, sideColor, targetGeometry.AlphaTestSubsetsByFace);
			}
			else
			{
				generator.GenerateCubeVertices(this, value, x, y, z, sideColor, targetGeometry.OpaqueSubsetsByFace);
			}
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			Texture2D texture = ShittyCreaturesBlockManager.AnimatedBlocksTexture ?? ShittyCreaturesBlockManager.BlocksTexture;
			// Usar la sobrecarga que acepta Texture2D al final
			BlocksManager.DrawCubeBlock(primitivesRenderer, value, Vector3.One, ref matrix, color, color, environmentData, texture);
		}
	}
}
