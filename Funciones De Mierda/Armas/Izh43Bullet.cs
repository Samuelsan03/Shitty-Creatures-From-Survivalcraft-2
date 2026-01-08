using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
using Game;

namespace Armas
{
	// Token: 0x02000021 RID: 33
	public class Izh43Bullet : Block
	{
		// Token: 0x060000B4 RID: 180 RVA: 0x00007FE0 File Offset: 0x000061E0
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/Plate", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Plate", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Plate", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f) * Matrix.CreateRotationX(1.5707f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x060000B5 RID: 181 RVA: 0x00008070 File Offset: 0x00006270
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x060000B6 RID: 182 RVA: 0x000080A7 File Offset: 0x000062A7
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 1.9f * size, ref matrix, environmentData);
		}

		// Token: 0x04000087 RID: 135
		public const int Index = 352;

		// Token: 0x04000088 RID: 136
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x04000089 RID: 137
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/calibre12", null);
	}
}
