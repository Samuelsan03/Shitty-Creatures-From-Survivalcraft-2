using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x0200001F RID: 31
	public class G3Bullet : Block
	{
		// Token: 0x060000AA RID: 170 RVA: 0x00007D94 File Offset: 0x00005F94
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/Plate", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Plate", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Plate", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f) * Matrix.CreateRotationX(1.5707f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x060000AB RID: 171 RVA: 0x00007E24 File Offset: 0x00006024
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x060000AC RID: 172 RVA: 0x00007E5B File Offset: 0x0000605B
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 1.9f * size, ref matrix, environmentData);
		}

		// Token: 0x04000081 RID: 129
		public const int Index = 349;

		// Token: 0x04000082 RID: 130
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x04000083 RID: 131
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/FX05Bullet", null);
	}
}
