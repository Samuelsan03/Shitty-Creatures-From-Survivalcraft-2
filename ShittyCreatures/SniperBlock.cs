using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x0200002B RID: 43
	public class SniperBlock : Block
	{
		// Token: 0x060000E4 RID: 228 RVA: 0x00008A6C File Offset: 0x00006C6C
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/Barret", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Gun", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Gun", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x060000E5 RID: 229 RVA: 0x00008AF0 File Offset: 0x00006CF0
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x060000E6 RID: 230 RVA: 0x00008B27 File Offset: 0x00006D27
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.6f * size, ref matrix, environmentData);
		}

		// Token: 0x060000E7 RID: 231 RVA: 0x00008B4C File Offset: 0x00006D4C
		public static int GetBulletNum(int data)
		{
			return data >> 4 & 63;
		}

		// Token: 0x060000E8 RID: 232 RVA: 0x00008B64 File Offset: 0x00006D64
		public static int SetBulletNum(int data)
		{
			return (data & -64) | (data & 63) << 4;
		}
		public const int Index = 355;

		// Token: 0x040000A3 RID: 163
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x040000A4 RID: 164
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/Barret", null);
	}
}
