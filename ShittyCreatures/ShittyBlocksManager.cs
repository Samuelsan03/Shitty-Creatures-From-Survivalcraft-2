using System;
using Engine;
using Engine.Graphics;
using Engine.Media;
using Game;

namespace Game
{
	// Token: 0x020000BC RID: 188
	public static class ShittyBlocksManager
	{
		// Token: 0x060005A0 RID: 1440 RVA: 0x00023D14 File Offset: 0x00021F14
		public static void Initialize()
		{
			ShittyBlocksManager.CalculateSlotTexCoordTables();
			ShittyBlocksManager.DefaultBlocksTexture = ContentManager.Get<Texture2D>("Textures/ShittyTextures");
		}

		// Token: 0x060005A1 RID: 1441 RVA: 0x00023D2C File Offset: 0x00021F2C
		public static void CalculateSlotTexCoordTables()
		{
			for (int i = 0; i < 256; i++)
			{
				ShittyBlocksManager.m_slotTexCoords[i] = ShittyBlocksManager.TextureSlotToTextureCoords(i);
			}
		}

		// Token: 0x060005A2 RID: 1442 RVA: 0x00023D5C File Offset: 0x00021F5C
		public static Vector4 TextureSlotToTextureCoords(int slot)
		{
			int num = slot % 16;
			int num2 = slot / 16;
			float x = ((float)num + 0.001f) / 16f;
			float y = ((float)num2 + 0.001f) / 16f;
			float z = ((float)(num + 1) - 0.001f) / 16f;
			float w = ((float)(num2 + 1) - 0.001f) / 16f;
			return new Vector4(x, y, z, w);
		}

		// Token: 0x060005A3 RID: 1443 RVA: 0x00023DBC File Offset: 0x00021FBC
		public static void DrawImageExtrusionBlock(PrimitivesRenderer3D primitivesRenderer, int value, float size, ref Matrix matrix, Texture2D texture2D, Color color, DrawBlockEnvironmentData environmentData)
		{
			int num = Terrain.ExtractContents(value);
			Block block = BlocksManager.Blocks[num];
			try
			{
				BlockMesh imageExtrusionBlockMesh = ShittyBlocksManager.GetImageExtrusionBlockMesh((Image)ShittyBlocksManager.DefaultBlocksTexture.Tag, block.GetFaceTextureSlot(-1, value));
				BlocksManager.DrawMeshBlock(primitivesRenderer, imageExtrusionBlockMesh, ShittyBlocksManager.DefaultBlocksTexture, color, 1.7f * size, ref matrix, environmentData);
			}
			catch (Exception)
			{
			}
		}

		// Token: 0x060005A4 RID: 1444 RVA: 0x00023E24 File Offset: 0x00022024
		public static BlockMesh GetImageExtrusionBlockMesh(Image image, int slot)
		{
			BlockMesh blockMesh = new BlockMesh();
			int num = (int)MathF.Round(ShittyBlocksManager.m_slotTexCoords[slot].X * (float)image.Width);
			int num2 = (int)MathF.Round(ShittyBlocksManager.m_slotTexCoords[slot].Y * (float)image.Height);
			int num3 = (int)MathF.Round(ShittyBlocksManager.m_slotTexCoords[slot].Z * (float)image.Width);
			int num4 = (int)MathF.Round(ShittyBlocksManager.m_slotTexCoords[slot].W * (float)image.Height);
			int num5 = MathUtils.Max(num3 - num, num4 - num2);
			blockMesh.AppendImageExtrusion(image, new Rectangle(num, num2, num3 - num, num4 - num2), new Vector3(1f / (float)num5, 1f / (float)num5, 0.083333336f), Color.White, 0);
			return blockMesh;
		}

        internal static void DrawFlatBlock(PrimitivesRenderer3D primitivesRenderer, int value, float size, ref Matrix matrix, Texture2D texture2D, Color color, bool v1, DrawBlockEnvironmentData environmentData, bool v2)
        {
            throw new NotImplementedException();
        }

        internal static void DrawImageExtrusionBlock(PrimitivesRenderer3D primitivesRenderer, int value, float v1, ref Matrix matrix, Texture2D texture2D, Color color, bool v2, DrawBlockEnvironmentData environmentData, bool v3)
        {
            throw new NotImplementedException();
        }

        // Token: 0x0400034C RID: 844
        public static Vector4[] m_slotTexCoords = new Vector4[256];

		// Token: 0x0400034D RID: 845
		public static Texture2D DefaultBlocksTexture;
	}
}
