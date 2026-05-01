using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x0200001B RID: 27
	public class BK43Block : Block
	{
		// Token: 0x0600008C RID: 140 RVA: 0x00004AF8 File Offset: 0x00002CF8
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/BK 93", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Musket", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Musket", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x0600008D RID: 141 RVA: 0x00004B7C File Offset: 0x00002D7C
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x0600008E RID: 142 RVA: 0x00004BB3 File Offset: 0x00002DB3
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.4f * size, ref matrix, environmentData);
		}

		// Token: 0x0600008F RID: 143 RVA: 0x00004BD8 File Offset: 0x00002DD8
		public static int GetBulletNum(int data)
		{
			return data >> 4 & 63;
		}

		// Token: 0x06000090 RID: 144 RVA: 0x00004BF0 File Offset: 0x00002DF0
		public static int SetBulletNum(int bulletNum)
		{
			return (bulletNum & 63) << 4;
		}

		// Token: 0x0400004B RID: 75
		public const int Index = 397;

		// Token: 0x0400004C RID: 76
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x0400004D RID: 77
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/BK 93", null);
	}
}
