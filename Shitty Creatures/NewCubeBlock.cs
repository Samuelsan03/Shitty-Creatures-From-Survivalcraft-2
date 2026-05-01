using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x02000042 RID: 66
	public abstract class NewCubeBlock : CubeBlock
	{
		// Token: 0x06000144 RID: 324 RVA: 0x0000ABD0 File Offset: 0x00008DD0
		public NewCubeBlock(string texturePath)
		{
			this.m_texture = ContentManager.Get<Texture2D>(texturePath);
		}

		// Token: 0x06000145 RID: 325 RVA: 0x0000ABE8 File Offset: 0x00008DE8
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateCubeVertices(this, value, x, y, z, Color.White, geometry.GetGeometry(this.m_texture).OpaqueSubsetsByFace);
		}

		// Token: 0x06000146 RID: 326 RVA: 0x0000AC1B File Offset: 0x00008E1B
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), ref matrix, color, color, environmentData, this.m_texture);
		}

		// Token: 0x06000147 RID: 327 RVA: 0x0000AC3C File Offset: 0x00008E3C
		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, this.DestructionDebrisScale, Color.White, this.GetFaceTextureSlot(4, value), this.m_texture);
		}

		// Token: 0x0400008F RID: 143
		public Texture2D m_texture;
	}
}
