using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
using Game;

namespace Armas
{
	// Token: 0x0200001B RID: 27
	public class AKBullet : Block
	{
		// Token: 0x06000096 RID: 150 RVA: 0x00007900 File Offset: 0x00005B00
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/Plate", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Plate", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Plate", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f) * Matrix.CreateRotationX(1.5707f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x06000097 RID: 151 RVA: 0x00007990 File Offset: 0x00005B90
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x06000098 RID: 152 RVA: 0x000079C7 File Offset: 0x00005BC7
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 1.9f * size, ref matrix, environmentData);
		}

		// Token: 0x04000077 RID: 119
		public const int Index = 339;

		// Token: 0x04000078 RID: 120
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x04000079 RID: 121
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/AK47Bullet", null);
	}
}
