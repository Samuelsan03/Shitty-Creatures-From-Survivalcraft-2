using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
using Game;

namespace Armas
{
	// Token: 0x0200003C RID: 60
	public class SWM500Bullet : Block
	{
		// Token: 0x06000146 RID: 326 RVA: 0x000101C4 File Offset: 0x0000E3C4
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/Plate", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Plate", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Plate", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f) * Matrix.CreateRotationX(1.5707f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x06000147 RID: 327 RVA: 0x00010254 File Offset: 0x0000E454
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x06000148 RID: 328 RVA: 0x0001028B File Offset: 0x0000E48B
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.3f * size, ref matrix, environmentData);
		}

		// Token: 0x04000128 RID: 296
		public const int Index = 354;

		// Token: 0x04000129 RID: 297
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x0400012A RID: 298
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/desertt", null);
	}
}
