using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class BaseballBatBlock : Block
	{
		public override void Initialize()
		{
			// Cargar el modelo del bate de béisbol
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/BaseballBat", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Gun", true).ParentBone);

			// Crear la malla del bloque con la transformación adecuada
			this.m_standaloneBlockMesh.AppendModelMeshPart(
				model.FindMesh("Gun", true).MeshParts[0],
				boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f),
				false, false, true, false, Color.White
			);
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Dibujar el bate con un tamaño adecuado (ajustar la escala según necesidad)
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.5f * size, ref matrix, environmentData);
		}

		// Índice único para el bloque del bate (asegúrate de que no entre en conflicto con otros bloques)
		public const int Index = 386;

		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Cargar la textura del bate
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/BaseballBat", null);
	}
}
