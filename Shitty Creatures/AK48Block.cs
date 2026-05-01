using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class AK48Block : Block
	{
		public const int Index = 403; // Elige un índice único, por ejemplo 393. Verifica que no esté en uso.

		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Textura para el modelo 3D del arma
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/AKMODEL", null);

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/AK48", null);
			// El modelo DAE que enviaste tiene un mesh llamado "Musket". Es importante que el nombre del mesh en tu modelo AK48 sea "Musket" o deberás cambiarlo aquí.
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Musket", true).ParentBone);
			m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Musket", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, true, false, Color.White);
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMesh, texture, color, 2.4f * size, ref matrix, environmentData);
		}

		// --- Métodos para la gestión de la munición (data del bloque) ---
		// Usaremos los 6 bits siguientes para la cantidad de balas.
		public static int GetBulletNum(int data)
		{
			return data >> 4 & 63; // Máscara 63 (6 bits) para el cargador
		}

		public static int SetBulletNum(int bulletNum)
		{
			return (bulletNum & 63) << 4;
		}
	}
}
