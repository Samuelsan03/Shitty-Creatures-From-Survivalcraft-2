using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ChristmasGiftEggBlock : Block
	{
		public static int Index = 681;
		public static string fName = "ChristmasGiftEggBlock";

		public BlockMesh m_blockMesh;
		public Texture2D m_giftTexture;

		public float m_eggScale = 2.5f;

		public override void Initialize()
		{
			// Cargar el modelo Egg original
			Model model = ContentManager.Get<Model>("Models/Egg");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Egg", true).ParentBone);

			m_blockMesh = new BlockMesh();
			m_blockMesh.AppendModelMeshPart(
				model.FindMesh("Egg", true).MeshParts[0],
				boneAbsoluteTransform,
				true,        // <-- makeEmissive = true  (NO multiplicar por lighting)
				false,
				false,
				false,
				Color.White
			);

			// === UV: usar la textura COMPLETA (no atlas) ===
			// Si tu textura "regalo" es un atlas 16x16 (256x256), restaura el scale a 0.0625.
			// Si es una textura completa (un solo huevo dibujado), usa Identity.
			Matrix uvMatrix = Matrix.Identity;
			// Si tu textura es un atlas 16x16, descomenta esto en su lugar:
			// Matrix uvMatrix = Matrix.CreateScale(0.0625f, 0.0625f, 1f) *
			//                    Matrix.CreateTranslation(0f, 0f, 0f);
			m_blockMesh.TransformTextureCoordinates(uvMatrix, -1);

			// Cargar la textura del regalo de navidad
			m_giftTexture = ContentManager.Get<Texture2D>("Textures/regalo");

			base.Initialize();
		}

		public override string GetCategory(int value)
		{
			return "Items";
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(Index, 0, 0);
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			// No genera vertices en el terreno
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Forzamos Color.White para no teñir la textura del regalo
			Texture2D texture = m_giftTexture ?? GetDefaultTexture(value);
			if (texture == null)
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_blockMesh, Color.White, m_eggScale * size, ref matrix, environmentData);
				return;
			}
			BlocksManager.DrawMeshBlock(primitivesRenderer, m_blockMesh, texture, Color.White, m_eggScale * size, ref matrix, environmentData);
		}
	}
}
