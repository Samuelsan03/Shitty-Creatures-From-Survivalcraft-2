using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x0200001E RID: 30
	public class G3Block : Block
	{
		// Token: 0x060000A4 RID: 164 RVA: 0x00007C58 File Offset: 0x00005E58
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/FX05", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Gun", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Gun", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x060000A5 RID: 165 RVA: 0x00007CDC File Offset: 0x00005EDC
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x060000A6 RID: 166 RVA: 0x00007D13 File Offset: 0x00005F13
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.4f * size, ref matrix, environmentData);
		}

		// Token: 0x060000A7 RID: 167 RVA: 0x00007D38 File Offset: 0x00005F38
		public static int GetBulletNum(int data)
		{
			return data >> 4 & 63;
		}

		// Token: 0x060000A8 RID: 168 RVA: 0x00007D50 File Offset: 0x00005F50
		public static int SetBulletNum(int data)
		{
			return (data & -64) | (data & 63) << 4;
		}

		// Token: 0x0400007E RID: 126
		public const int Index = 348;

		// Token: 0x0400007F RID: 127
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x04000080 RID: 128
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/FX05Gun", null);
	}
}
