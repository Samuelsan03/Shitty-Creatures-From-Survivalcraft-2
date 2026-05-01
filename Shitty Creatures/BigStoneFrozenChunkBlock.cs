using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class BigStoneFrozenChunkBlock : ChunkBlock
	{
		// Textura personalizada
		public Texture2D m_texture;

		public BigStoneFrozenChunkBlock() : base(
			Matrix.CreateScale(4.5f) * Matrix.CreateRotationX(0f) * Matrix.CreateRotationZ(1f),
			Matrix.CreateScale(2.5f) * Matrix.CreateTranslation(0.1875f, 0.0625f, 0f),
			new Color(0, 196, 255), // Color azul
			true)
		{
		}

		// Sobrescribir Initialize para cargar la textura
		public override void Initialize()
		{
			base.Initialize();
			m_texture = ContentManager.Get<Texture2D>("Textures/BigStoneTexture");
		}

		// Sobrescribir DrawBlock para usar la textura personalizada
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Usar la textura personalizada en lugar de la predeterminada
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, m_texture, color, size, ref matrix, environmentData);
		}

		// Sobrescribir GenerateTerrainVertices para usar la textura personalizada
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			// Usar la textura personalizada para la geometría del terreno
			generator.GenerateMeshVertices(this, x, y, z, this.m_blockMesh, Color.White, null, geometry.GetGeometry(m_texture).SubsetOpaque);
		}

		// Sobrescribir CreateDebrisParticleSystem para usar la textura personalizada
		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, this.DestructionDebrisScale, Color.White, 0, m_texture);
		}
		public static int Index = 424; // ID único para este bloque
		private BlockMesh m_blockMesh;
	}
}
