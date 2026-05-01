using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x02000020 RID: 32
	public class Izh43Block : Block
	{
		// Token: 0x060000AE RID: 174 RVA: 0x00007EA4 File Offset: 0x000060A4
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/izh43", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("izh43", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("izh43", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x060000AF RID: 175 RVA: 0x00007F28 File Offset: 0x00006128
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x060000B0 RID: 176 RVA: 0x00007F5F File Offset: 0x0000615F
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.6f * size, ref matrix, environmentData);
		}

		// Token: 0x060000B1 RID: 177 RVA: 0x00007F84 File Offset: 0x00006184
		public static int GetBulletNum(int data)
		{
			return data >> 4 & 63;
		}

		// Token: 0x060000B2 RID: 178 RVA: 0x00007F9C File Offset: 0x0000619C
		public static int SetBulletNum(int data)
		{
			return (data & -64) | (data & 63) << 4;
		}

		// Token: 0x04000084 RID: 132
		public const int Index = 351;

		// Token: 0x04000085 RID: 133
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x04000086 RID: 134
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/izh43 gun", null);
	}
}
