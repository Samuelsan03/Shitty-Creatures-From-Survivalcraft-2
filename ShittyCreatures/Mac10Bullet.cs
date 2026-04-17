using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x02000025 RID: 37
	public class Mac10Bullet : Block
	{
		// Token: 0x060000C8 RID: 200 RVA: 0x00008478 File Offset: 0x00006678
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/Plate", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Plate", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Plate", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f) * Matrix.CreateRotationX(1.5707f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x060000C9 RID: 201 RVA: 0x00008508 File Offset: 0x00006708
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x060000CA RID: 202 RVA: 0x0000853F File Offset: 0x0000673F
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 1.9f * size, ref matrix, environmentData);
		}

		// Token: 0x04000093 RID: 147
		public const int Index = 345;

		// Token: 0x04000094 RID: 148
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x04000095 RID: 149
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/MAC10Bullet", null);
	}
}
