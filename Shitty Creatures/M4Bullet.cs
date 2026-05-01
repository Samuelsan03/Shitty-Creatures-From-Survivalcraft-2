using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x02000023 RID: 35
	public class M4Bullet : Block
	{
		// Token: 0x060000BE RID: 190 RVA: 0x0000822C File Offset: 0x0000642C
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/Plate", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Plate", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Plate", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f) * Matrix.CreateRotationX(1.5707f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x060000BF RID: 191 RVA: 0x000082BC File Offset: 0x000064BC
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x060000C0 RID: 192 RVA: 0x000082F3 File Offset: 0x000064F3
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.4f * size, ref matrix, environmentData);
		}

		// Token: 0x0400008D RID: 141
		public const int Index = 341;

		// Token: 0x0400008E RID: 142
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x0400008F RID: 143
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/M16Bullet", null);
	}
}
