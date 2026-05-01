using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public abstract class SharpHammerBlock : Block
	{
		// Token: 0x06000400 RID: 1024
		public SharpHammerBlock(int handleTextureSlot, int headTextureSlot)
		{
			this.m_handleTextureSlot = handleTextureSlot;
			this.m_headTextureSlot = headTextureSlot;
		}

		// Token: 0x06000401 RID: 1025
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/SharpHammer");

			// En tu COLLADA, las mallas se llaman "Handle.002" y "Head.002"
			// Pero en el código del juego probablemente usan nombres sin el .002
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
				model.FindMesh("Handle", true).ParentBone);
			Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
				model.FindMesh("Head", true).ParentBone);

			BlockMesh blockMesh = new BlockMesh();
			blockMesh.AppendModelMeshPart(
				model.FindMesh("Handle", true).MeshParts[0],
				boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
				false, false, false, false, Color.White);
			blockMesh.TransformTextureCoordinates(
				Matrix.CreateTranslation(
					(float)(this.m_handleTextureSlot % 16) / 16f,
					(float)(this.m_handleTextureSlot / 16) / 16f, 0f), -1);

			BlockMesh blockMesh2 = new BlockMesh();
			blockMesh2.AppendModelMeshPart(
				model.FindMesh("Head", true).MeshParts[0],
				boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f),
				false, false, false, false, Color.White);
			blockMesh2.TransformTextureCoordinates(
				Matrix.CreateTranslation(
					(float)(this.m_headTextureSlot % 16) / 16f,
					(float)(this.m_headTextureSlot / 16) / 16f, 0f), -1);

			this.m_standaloneBlockMesh.AppendBlockMesh(blockMesh);
			this.m_standaloneBlockMesh.AppendBlockMesh(blockMesh2);

			base.Initialize();
		}

		// Token: 0x06000402 RID: 1026
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator,
			TerrainGeometry geometry, int value, int x, int y, int z)
		{
			// No genera vértices de terreno (es un objeto 3D independiente)
		}

		// Token: 0x06000403 RID: 1027
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value,
			Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh,
				color, 2f * size, ref matrix, environmentData);
		}

		// Token: 0x040001A8 RID: 424
		public int m_handleTextureSlot;

		// Token: 0x040001A9 RID: 425
		public int m_headTextureSlot;

		// Token: 0x040001AA RID: 426
		public BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}