using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class FreezingSnowballBlock : Block
	{
		public const int Index = 401;

		private BlockMesh m_standaloneBlockMesh = new BlockMesh();
		private Texture2D m_texture;

		public override void Initialize()
		{
			// Cargar textura personalizada
			m_texture = ContentManager.Get<Texture2D>("Textures/Items/nieve congelante");

			// Construir el mesh igual que la bola de nieve original
			Model model = ContentManager.Get<Model>("Models/Snowball");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Snowball", true).ParentBone);
			m_standaloneBlockMesh.AppendModelMeshPart(
				model.FindMesh("Snowball", true).MeshParts[0],
				boneAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f),
				false, false, false, false,
				Color.White
			);

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			// No genera vértices en el terreno (es un item)
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Usar el mismo método que BigStone: BlocksManager.DrawMeshBlock con textura
			// Nota: Asumimos que existe una sobrecarga que acepta Texture2D como segundo parámetro.
			// Si no existe, usa la que tiene 6 parámetros y asigna la textura a environmentData de otra forma.
			BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMesh, m_texture, color, 2.5f * size, ref matrix, environmentData);
		}

		public override float GetProjectilePower(int value) => 0.1f;
	}
}
