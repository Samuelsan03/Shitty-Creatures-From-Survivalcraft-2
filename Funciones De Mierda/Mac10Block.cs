using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x02000024 RID: 36
	public class Mac10Block : Block
	{
		// Token: 0x060000C2 RID: 194 RVA: 0x0000833C File Offset: 0x0000653C
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/MAC10Gun", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Gun", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Gun", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x060000C3 RID: 195 RVA: 0x000083C0 File Offset: 0x000065C0
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x060000C4 RID: 196 RVA: 0x000083F7 File Offset: 0x000065F7
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.4f * size, ref matrix, environmentData);
		}

		// Token: 0x060000C5 RID: 197 RVA: 0x0000841C File Offset: 0x0000661C
		public static int GetBulletNum(int data)
		{
			return data >> 4 & 63;
		}

		// Token: 0x060000C6 RID: 198 RVA: 0x00008434 File Offset: 0x00006634
		public static int SetBulletNum(int data)
		{
			return (data & -64) | (data & 63) << 4;
		}

		// Token: 0x04000090 RID: 144
		public const int Index = 344;

		// Token: 0x04000091 RID: 145
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x04000092 RID: 146
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/MAC10", null);
	}
}
