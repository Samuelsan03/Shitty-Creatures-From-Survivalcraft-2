using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x020000B4 RID: 180
	public class NuevaBala : Block
	{
		// Token: 0x06000721 RID: 1825 RVA: 0x0005210A File Offset: 0x0005030A
		public override void Initialize()
		{
			base.Initialize();
			this.m_texture = ContentManager.Get<Texture2D>("Textures/Experience", null);
		}

		// Token: 0x06000722 RID: 1826 RVA: 0x00052125 File Offset: 0x00050325
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x06000723 RID: 1827 RVA: 0x00052128 File Offset: 0x00050328
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawFlatBlock(primitivesRenderer, value, size * 0.05f, ref matrix, this.m_texture, Color.White, true, environmentData);
		}

		// Token: 0x04000689 RID: 1673
		public const int Index = 10000;

		// Token: 0x0400068A RID: 1674
		public Texture2D m_texture;
	}
}
