using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x0200002C RID: 44
	public class SniperBullet : Block
	{
		// Token: 0x060000EA RID: 234 RVA: 0x00008BA8 File Offset: 0x00006DA8
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/Plate", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Plate", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Plate", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f) * Matrix.CreateRotationX(1.5707f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x060000EB RID: 235 RVA: 0x00008C38 File Offset: 0x00006E38
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x060000EC RID: 236 RVA: 0x00008C6F File Offset: 0x00006E6F
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 1.9f * size, ref matrix, environmentData);
		}

		public const int Index = 356;

		// Token: 0x040000A5 RID: 165
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x040000A6 RID: 166
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/sniper bala", null);
	}
}
