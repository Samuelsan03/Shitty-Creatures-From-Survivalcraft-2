using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class M249Block : Block
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/M249", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Gun", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Gun", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, true, false, Color.White);
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.7f * size, ref matrix, environmentData);
		}

		public static int GetBulletNum(int data)
		{
			return (data >> 4) & 127; // 7 bits para 0-127 (capacidad del cargador real de la M249 es 100-200)
		}

		public static int SetBulletNum(int data, int bulletNum)
		{
			bulletNum = MathUtils.Clamp(bulletNum, 0, 100); // Capacidad real de la M249
			return (data & ~2032) | ((bulletNum & 127) << 4); // 2032 = 127 << 4
		}

		public const int Index = 379; // Nuevo índice

		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/M249", null);
	}
}