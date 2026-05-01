using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x020000B6 RID: 182
	
	public class NuevaBala3 : Block
	{
		// Token: 0x06000729 RID: 1833 RVA: 0x0005219E File Offset: 0x0005039E
		public override void Initialize()
		{
			base.Initialize();
			this.m_texture = ContentManager.Get<Texture2D>("Textures/Experience", null);
		}

		// Token: 0x0600072A RID: 1834 RVA: 0x000521B9 File Offset: 0x000503B9
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x0600072B RID: 1835 RVA: 0x000521BC File Offset: 0x000503BC
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawFlatBlock(primitivesRenderer, value, size * 0.05f, ref matrix, this.m_texture, Color.Black, true, environmentData);
		}

		// Token: 0x0400068D RID: 1677
		public Texture2D m_texture;
	}
}
