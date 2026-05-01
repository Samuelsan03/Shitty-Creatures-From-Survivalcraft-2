using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x020000B8 RID: 184
	public class NuevaBala5 : Block
	{
		// Token: 0x06000731 RID: 1841 RVA: 0x00052232 File Offset: 0x00050432
		public override void Initialize()
		{
			base.Initialize();
			this.m_texture = ContentManager.Get<Texture2D>("Textures/Experience", null);
		}

		// Token: 0x06000732 RID: 1842 RVA: 0x0005224D File Offset: 0x0005044D
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x06000733 RID: 1843 RVA: 0x00052250 File Offset: 0x00050450
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawFlatBlock(primitivesRenderer, value, size * 0.05f, ref matrix, this.m_texture, Color.Black, true, environmentData);
		}

		// Token: 0x0400068F RID: 1679
		public Texture2D m_texture;
	}
}