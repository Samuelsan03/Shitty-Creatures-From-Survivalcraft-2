using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x0200001A RID: 26
	public abstract class Armas : Block
	{
		// Token: 0x1700001B RID: 27
		// (get) Token: 0x060000A3 RID: 163 RVA: 0x00008AEE File Offset: 0x00006CEE
		// (set) Token: 0x060000A4 RID: 164 RVA: 0x00008AF6 File Offset: 0x00006CF6
		public string ModelName { get; private set; }

		// Token: 0x1700001C RID: 28
		// (get) Token: 0x060000A5 RID: 165 RVA: 0x00008AFF File Offset: 0x00006CFF
		// (set) Token: 0x060000A6 RID: 166 RVA: 0x00008B07 File Offset: 0x00006D07
		public Texture2D BlockTexture { get; private set; }

		// Token: 0x1700001D RID: 29
		// (get) Token: 0x060000A7 RID: 167 RVA: 0x00008B10 File Offset: 0x00006D10
		// (set) Token: 0x060000A8 RID: 168 RVA: 0x00008B18 File Offset: 0x00006D18
		public BlockMesh Mesh { get; private set; }

		// Token: 0x060000A9 RID: 169 RVA: 0x00008B21 File Offset: 0x00006D21
		protected Armas(string modelName, string texturePath)
		{
			this.ModelName = modelName;
			this.BlockTexture = ContentManager.Get<Texture2D>(texturePath, null);
			this.Mesh = new BlockMesh();
		}

		// Token: 0x060000AA RID: 170 RVA: 0x00008B48 File Offset: 0x00006D48
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>(this.ModelName, null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("arma", true).ParentBone);
			this.Mesh.AppendModelMeshPart(model.FindMesh("arma", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
			base.Initialize();
		}

		// Token: 0x060000AB RID: 171 RVA: 0x00003C2D File Offset: 0x00001E2D
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x060000AC RID: 172 RVA: 0x00008BC7 File Offset: 0x00006DC7
		public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(renderer, this.Mesh, this.BlockTexture, color, 2f * size, ref matrix, environmentData);
		}
	}
}
