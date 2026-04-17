using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x020000B5 RID: 181
	public class NuevaBala2 : Block
	{
		// Token: 0x06000725 RID: 1829 RVA: 0x00052154 File Offset: 0x00050354
		public override void Initialize()
		{
			base.Initialize();
			this.m_texture = ContentManager.Get<Texture2D>("Textures/Experience", null);
		}

		// Token: 0x06000726 RID: 1830 RVA: 0x0005216F File Offset: 0x0005036F
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x06000727 RID: 1831 RVA: 0x00052172 File Offset: 0x00050372
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawFlatBlock(primitivesRenderer, value, size * 0.05f, ref matrix, this.m_texture, Color.Black, true, environmentData);
		}

		// Token: 0x0400068B RID: 1675
		public const int Index = 10001;

		// Token: 0x0400068C RID: 1676
		public Texture2D m_texture;
	}
}
