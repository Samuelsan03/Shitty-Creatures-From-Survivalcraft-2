using System;
using Engine;
using Engine.Graphics;
using Engine.Media;
using Game;

namespace Game
{
	/// <summary>
	/// Gestor estático para la textura personalizada "Textures/ShittyCreaturesTextures".
	/// Proporciona acceso a la textura y métodos de dibujo auxiliares.
	/// </summary>
	public static class ShittyCreaturesBlockManager
	{
		// Coordenadas de textura para ranuras 0..255 (16x16 grid)
		private static Vector4[] m_slotTexCoords = new Vector4[256];

		// Textura predeterminada (pública para acceso externo)
		public static Texture2D DefaultBlocksTexture { get; private set; }

		/// <summary>
		/// Inicializa el gestor cargando la textura y calculando las tablas de coordenadas.
		/// </summary>
		public static void Initialize()
		{
			CalculateSlotTexCoordTables();
			DefaultBlocksTexture = ContentManager.Get<Texture2D>("Textures/ShittyCreaturesTextures");
		}

		/// <summary>
		/// Calcula las coordenadas de textura para cada ranura (0-255).
		/// </summary>
		private static void CalculateSlotTexCoordTables()
		{
			for (int i = 0; i < 256; i++)
			{
				m_slotTexCoords[i] = TextureSlotToTextureCoords(i);
			}
		}

		/// <summary>
		/// Convierte un índice de ranura (0-255) en coordenadas UV.
		/// </summary>
		public static Vector4 TextureSlotToTextureCoords(int slot)
		{
			int x = slot % 16;
			int y = slot / 16;
			float u1 = (x + 0.001f) / 16f;
			float v1 = (y + 0.001f) / 16f;
			float u2 = (x + 1 - 0.001f) / 16f;
			float v2 = (y + 1 - 0.001f) / 16f;
			return new Vector4(u1, v1, u2, v2);
		}

		/// <summary>
		/// Dibuja un bloque como extrusión de imagen (similar a los bloques planos en el inventario).
		/// </summary>
		public static void DrawImageExtrusionBlock(PrimitivesRenderer3D primitivesRenderer, int value, float size, ref Matrix matrix, Color color, DrawBlockEnvironmentData environmentData)
		{
			int contents = Terrain.ExtractContents(value);
			Block block = BlocksManager.Blocks[contents];
			try
			{
				// Se asume que DefaultBlocksTexture.Tag contiene la imagen original (Image)
				Image image = DefaultBlocksTexture.Tag as Image;
				if (image != null)
				{
					int slot = block.GetFaceTextureSlot(-1, value);
					BlockMesh mesh = GetImageExtrusionBlockMesh(image, slot);
					BlocksManager.DrawMeshBlock(primitivesRenderer, mesh, DefaultBlocksTexture, color, 1.7f * size, ref matrix, environmentData);
				}
			}
			catch
			{
				// Silenciar errores de dibujo
			}
		}

		/// <summary>
		/// Genera un BlockMesh a partir de una imagen y una ranura de textura.
		/// </summary>
		private static BlockMesh GetImageExtrusionBlockMesh(Image image, int slot)
		{
			BlockMesh mesh = new BlockMesh();
			Vector4 uv = m_slotTexCoords[slot];
			int x0 = (int)MathF.Round(uv.X * image.Width);
			int y0 = (int)MathF.Round(uv.Y * image.Height);
			int x1 = (int)MathF.Round(uv.Z * image.Width);
			int y1 = (int)MathF.Round(uv.W * image.Height);
			int maxSide = MathUtils.Max(x1 - x0, y1 - y0);
			Vector3 scale = new Vector3(1f / maxSide, 1f / maxSide, 0.083333336f);
			mesh.AppendImageExtrusion(image, new Rectangle(x0, y0, x1 - x0, y1 - y0), scale, Color.White, 0);
			return mesh;
		}
	}
}