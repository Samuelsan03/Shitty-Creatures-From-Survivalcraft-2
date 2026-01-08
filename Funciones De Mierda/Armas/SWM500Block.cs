using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
using Game;

namespace Armas
{
	// Token: 0x0200003B RID: 59
	public class SWM500Block : Block
	{
		// Token: 0x06000140 RID: 320 RVA: 0x00010088 File Offset: 0x0000E288
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/deserteagle", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("deserteagle", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("deserteagle", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x06000141 RID: 321 RVA: 0x0001010C File Offset: 0x0000E30C
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x06000142 RID: 322 RVA: 0x00010143 File Offset: 0x0000E343
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 1.8f * size, ref matrix, environmentData);
		}

		// Token: 0x06000143 RID: 323 RVA: 0x00010168 File Offset: 0x0000E368
		public static int GetBulletNum(int data)
		{
			return data >> 4 & 63;
		}

		// Token: 0x06000144 RID: 324 RVA: 0x00010180 File Offset: 0x0000E380
		public static int SetBulletNum(int data)
		{
			return (data & -64) | (data & 63) << 4;
		}

		// Token: 0x04000125 RID: 293
		public const int Index = 353;

		// Token: 0x04000126 RID: 294
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x04000127 RID: 295
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/deserteagle", null);
	}
}
