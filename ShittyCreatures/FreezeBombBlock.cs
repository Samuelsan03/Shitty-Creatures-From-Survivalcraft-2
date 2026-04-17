// FreezeBombBlock.cs
using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FreezeBombBlock : Block
	{
		public static int Index = 402;
		public BlockMesh m_standaloneBlockMesh = new BlockMesh();

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Bomb");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Bomb", true).ParentBone);

			m_standaloneBlockMesh.AppendModelMeshPart(
				model.FindMesh("Bomb", true).MeshParts[0],
				boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.25f, 0f),
				false, false, false, false,
				new Color(0f, 188f, 255f) // Azul hielo
			);

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}
	}
}
