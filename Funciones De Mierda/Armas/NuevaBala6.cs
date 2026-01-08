using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
using Game;

namespace Armas
{
	// Token: 0x02000026 RID: 38
	public class NuevaBala6 : Block
	{
		// Token: 0x060000CC RID: 204 RVA: 0x00008587 File Offset: 0x00006787
		public override void Initialize()
		{
			base.Initialize();
			this.m_texture = ContentManager.Get<Texture2D>("Textures/Experience", null);
		}

		// Token: 0x060000CD RID: 205 RVA: 0x000085A2 File Offset: 0x000067A2
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x060000CE RID: 206 RVA: 0x000085A5 File Offset: 0x000067A5
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawFlatBlock(primitivesRenderer, value, size * 0.05f, ref matrix, this.m_texture, Color.Black, true, environmentData);
		}

		// Token: 0x04000096 RID: 150
		public Texture2D m_texture;
	}
}
