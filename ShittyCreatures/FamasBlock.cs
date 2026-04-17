using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FamasBlock : Block
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/FAMAS", null);
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
			return (data >> 4) & 31; // 5 bits para 0-31 (0-30 balas)
		}

		public static int SetBulletNum(int data, int bulletNum)
		{
			bulletNum = MathUtils.Clamp(bulletNum, 0, 30);
			// Limpiar bits 4-8 (máscara 111110000 = 496) y establecer nuevo valor
			return (data & ~496) | ((bulletNum & 31) << 4);
		}

		public const int Index = 372;

		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/Famas", null);
	}
}
