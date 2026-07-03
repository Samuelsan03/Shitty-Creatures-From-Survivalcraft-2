using System;
using Engine;
using Engine.Graphics;
using TemplatesDatabase;
using Game;

namespace Game
{
	public class CommonInfectedSpawnEggBlock : Block
	{
		public const int Index = 644;

		private BlockMesh m_blockMesh;
		private Texture2D m_alertTexture;

		public override void Initialize()
		{
			// Cargar el modelo del huevo
			Model model = ContentManager.Get<Model>("Models/Egg");
			Matrix boneTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Egg", true).ParentBone);

			// Cargar la textura de alerta (asegúrate de que la ruta sea correcta)
			// Si la textura está en "Textures/alerta.png", usa "Textures/alerta" sin extensión
			m_alertTexture = ContentManager.Get<Texture2D>("Textures/alerta", throwOnNotFound: true);

			// Crear el mesh base (usamos color blanco para que la textura se vea sin alterar)
			m_blockMesh = new BlockMesh();
			m_blockMesh.AppendModelMeshPart(
				model.FindMesh("Egg", true).MeshParts[0],
				boneTransform,
				false, false, false, false,
				Color.Green
			);

			base.Initialize();
		}

		public override string GetCategory(int value)
		{
			return "Spawner Eggs";
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			// No genera vértices en el terreno
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Usamos la sobrecarga de DrawMeshBlock que acepta una textura personalizada
			// Si m_alertTexture es null (por error de carga), se usará la textura por defecto
			if (m_alertTexture != null)
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_blockMesh, m_alertTexture, color, size, ref matrix, environmentData);
			}
			else
			{
				// Fallback: textura por defecto
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_blockMesh, color, size, ref matrix, environmentData);
			}
		}
	}
}