using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
using Game;

namespace Armas
{
	// Token: 0x0200003D RID: 61
	public class UziBlock : Block
	{
		// Token: 0x0600014A RID: 330 RVA: 0x000102D4 File Offset: 0x0000E4D4
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/UZI", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Gun", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Gun", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x0600014B RID: 331 RVA: 0x00010358 File Offset: 0x0000E558
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x0600014C RID: 332 RVA: 0x0001038F File Offset: 0x0000E58F
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.2f * size, ref matrix, environmentData);
		}

		// Token: 0x0600014D RID: 333 RVA: 0x000103B4 File Offset: 0x0000E5B4
		public static int GetBulletNum(int data)
		{
			return data >> 4 & 63;
		}

		// Token: 0x0600014E RID: 334 RVA: 0x000103CC File Offset: 0x0000E5CC
		public static int SetBulletNum(int data)
		{
			return (data & -64) | (data & 63) << 4;
		}

		// Token: 0x0400012B RID: 299
		public const int Index = 346;

		// Token: 0x0400012C RID: 300
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x0400012D RID: 301
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/Uzi", null);
	}
}
