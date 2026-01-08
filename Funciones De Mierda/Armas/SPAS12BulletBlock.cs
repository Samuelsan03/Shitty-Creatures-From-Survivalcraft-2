using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
using Game;

namespace Armas
{
	// Token: 0x0200002E RID: 46
	public class SPAS12BulletBlock : Block
	{
		// Token: 0x060000F4 RID: 244 RVA: 0x00008DF4 File Offset: 0x00006FF4
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/Plate", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Plate", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Plate", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f) * Matrix.CreateRotationX(1.5707f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x060000F5 RID: 245 RVA: 0x00008E84 File Offset: 0x00007084
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x060000F6 RID: 246 RVA: 0x00008EBB File Offset: 0x000070BB
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 1.9f * size, ref matrix, environmentData);
		}

		// Token: 0x040000AA RID: 170
		public const int Index = 343;

		// Token: 0x040000AB RID: 171
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x040000AC RID: 172
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/AA12Bullet", null);
	}
}
