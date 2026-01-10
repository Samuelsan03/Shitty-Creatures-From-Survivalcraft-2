using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	// Token: 0x02000022 RID: 34
	public class M4Block : Block
	{
		// Token: 0x060000B8 RID: 184 RVA: 0x000080F0 File Offset: 0x000062F0
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/M4", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Gun", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Gun", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x060000B9 RID: 185 RVA: 0x00008174 File Offset: 0x00006374
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x060000BA RID: 186 RVA: 0x000081AB File Offset: 0x000063AB
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.6f * size, ref matrix, environmentData);
		}

		// Token: 0x060000BB RID: 187 RVA: 0x000081D0 File Offset: 0x000063D0
		public static int GetBulletNum(int data)
		{
			return data >> 4 & 63;
		}

		// Token: 0x060000BC RID: 188 RVA: 0x000081E8 File Offset: 0x000063E8
		public static int SetBulletNum(int data)
		{
			return (data & -64) | (data & 63) << 4;
		}

		// Token: 0x0400008A RID: 138
		public const int Index = 340;

		// Token: 0x0400008B RID: 139
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x0400008C RID: 140
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/M4", null);
	}
}
