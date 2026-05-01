using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class SWM500Block : Block
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/deserteagle", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("deserteagle", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("deserteagle", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, true, false, Color.White);
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 1.8f * size, ref matrix, environmentData);
		}

		// Método NUEVO: Obtener el valor inicial del bloque con 8 balas
		public static int GetInitialValue()
		{
			return Terrain.MakeBlockValue(353, 0, SetBulletNum(8));
		}

		public static int GetBulletNum(int data)
		{
			return data >> 4 & 63;
		}

		public static int SetBulletNum(int bulletCount)
		{
			bulletCount = MathUtils.Clamp(bulletCount, 0, 63);
			return (bulletCount & 63) << 4;
		}

		public const int Index = 353;
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/deserteagle", null);
	}
}
