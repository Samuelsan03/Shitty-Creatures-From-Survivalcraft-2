using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x020000B7 RID: 183
	public class NuevaBala4 : Block
	{
		// Token: 0x0600072D RID: 1837 RVA: 0x000521E8 File Offset: 0x000503E8
		public override void Initialize()
		{
			base.Initialize();
			this.m_texture = ContentManager.Get<Texture2D>("Textures/Experience", null);
		}

		// Token: 0x0600072E RID: 1838 RVA: 0x00052203 File Offset: 0x00050403
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x0600072F RID: 1839 RVA: 0x00052206 File Offset: 0x00050406
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawFlatBlock(primitivesRenderer, value, size * 0.05f, ref matrix, this.m_texture, Color.Black, true, environmentData);
		}

		// Token: 0x0400068E RID: 1678
		public Texture2D m_texture;
	}
}
