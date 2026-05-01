using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public abstract class ThinHammerBlock : Block
	{
		public int m_handleTextureSlot;
		public int m_headTextureSlot;
		public BlockMesh m_standaloneBlockMesh = new BlockMesh();

		public ThinHammerBlock(int handleTextureSlot, int headTextureSlot)
		{
			m_handleTextureSlot = handleTextureSlot;
			m_headTextureSlot = headTextureSlot;
		}

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/HammerX");
			Matrix handleTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Handle", true).ParentBone);
			Matrix headTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Head", true).ParentBone);

			BlockMesh handleMesh = new BlockMesh();
			handleMesh.AppendModelMeshPart(model.FindMesh("Handle", true).MeshParts[0],
				handleTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
			handleMesh.TransformTextureCoordinates(Matrix.CreateTranslation(
				(float)(m_handleTextureSlot % 16) / 16f,
				(float)(m_handleTextureSlot / 16) / 16f, 0f), -1);

			BlockMesh headMesh = new BlockMesh();
			headMesh.AppendModelMeshPart(model.FindMesh("Head", true).MeshParts[0],
				headTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
			headMesh.TransformTextureCoordinates(Matrix.CreateTranslation(
				(float)(m_headTextureSlot % 16) / 16f,
				(float)(m_headTextureSlot / 16) / 16f, 0f), -1);

			m_standaloneBlockMesh.AppendBlockMesh(handleMesh);
			m_standaloneBlockMesh.AppendBlockMesh(headMesh);
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}
	}
}
