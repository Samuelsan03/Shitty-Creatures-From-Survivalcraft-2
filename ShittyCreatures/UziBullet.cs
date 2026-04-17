using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x0200003E RID: 62
	public class UziBullet : Block
	{
		// Token: 0x06000150 RID: 336 RVA: 0x00010410 File Offset: 0x0000E610
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/Plate", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Plate", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Plate", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f) * Matrix.CreateRotationX(1.5707f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x06000151 RID: 337 RVA: 0x000104A0 File Offset: 0x0000E6A0
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x06000152 RID: 338 RVA: 0x000104D7 File Offset: 0x0000E6D7
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.6f * size, ref matrix, environmentData);
		}

		// Token: 0x0400012E RID: 302
		public const int Index = 347;

		// Token: 0x0400012F RID: 303
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x04000130 RID: 304
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/MendozaBullet", null);
	}
}
