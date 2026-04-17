using Engine;
using Engine.Graphics;

namespace Game
{
	public class Master308Block : Block
	{
		public const int Index = 406;

		private BlockMesh m_standaloneBlockMesh = new BlockMesh();
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/GUN");

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/Master 308");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Musket", true).ParentBone);
			m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Musket", true).MeshParts[0],
				boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f),
				false, false, true, false, Color.White);
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

		// Gestión de munición (similar a AKBlock)
		public static int GetBulletNum(int data)
		{
			return data >> 4 & 63; // 6 bits para la cantidad de cartuchos (máximo 63, pero usaremos 2)
		}

		public static int SetBulletNum(int bulletNum)
		{
			return (bulletNum & 63) << 4;
		}
	}
}
