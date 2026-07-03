using System;
using Engine;
using Engine.Graphics;
using TemplatesDatabase;
using Game;

namespace Game
{
	public class BoomerSpawnEggBlock : Block
	{
		public const int Index = 645; // ID único

		private BlockMesh m_blockMesh;
		private Texture2D m_alertTexture;

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Egg");
			Matrix boneTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Egg", true).ParentBone);
			m_alertTexture = ContentManager.Get<Texture2D>("Textures/alerta", throwOnNotFound: true);

			m_blockMesh = new BlockMesh();
			m_blockMesh.AppendModelMeshPart(
				model.FindMesh("Egg", true).MeshParts[0],
				boneTransform,
				false, false, false, false,
				Color.Orange // Color naranja para boomers
			);
			base.Initialize();
		}

		public override string GetCategory(int value)
		{
			return "Spawner Eggs";
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			if (m_alertTexture != null)
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_blockMesh, m_alertTexture, color, size, ref matrix, environmentData);
			else
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_blockMesh, color, size, ref matrix, environmentData);
		}
	}
}