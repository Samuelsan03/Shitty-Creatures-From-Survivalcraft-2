using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x0200002D RID: 45
	public class SPAS12Block : Block
	{
		// Token: 0x060000EE RID: 238 RVA: 0x00008CB8 File Offset: 0x00006EB8
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/SPAS12", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Gun", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Gun", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x060000EF RID: 239 RVA: 0x00008D3C File Offset: 0x00006F3C
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x060000F0 RID: 240 RVA: 0x00008D73 File Offset: 0x00006F73
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.6f * size, ref matrix, environmentData);
		}

		// Token: 0x060000F1 RID: 241 RVA: 0x00008D98 File Offset: 0x00006F98
		public static int GetBulletNum(int data)
		{
			return data >> 4 & 63;
		}

		// Token: 0x060000F2 RID: 242 RVA: 0x00008DB0 File Offset: 0x00006FB0
		public static int SetBulletNum(int data)
		{
			return (data & -64) | (data & 63) << 4;
		}

		// Token: 0x040000A7 RID: 167
		public const int Index = 342;

		// Token: 0x040000A8 RID: 168
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x040000A9 RID: 169
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/SPAS 12", null);
	}
}
