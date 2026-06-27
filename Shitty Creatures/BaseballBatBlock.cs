using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class BaseballBatBlock : Block
	{
		public override void Initialize()
		{
			int num = 47;
			Model model = ContentManager.Get<Model>("Models/WoodenClub");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Handle", true).ParentBone);
			BlockMesh blockMesh = new BlockMesh();
			blockMesh.AppendModelMeshPart(model.FindMesh("Handle", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
			blockMesh.TransformTextureCoordinates(Matrix.CreateTranslation((float)(num % 16) / 16f, (float)(num / 16) / 16f, 0f), -1);
			this.m_standaloneBlockMesh.AppendBlockMesh(blockMesh);
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, new Color(255,171,0), 2f * size, ref matrix, environmentData);
		}

		public static int Index = 518;

		public BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
