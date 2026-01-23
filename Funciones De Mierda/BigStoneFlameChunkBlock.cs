using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class BigStoneFlameChunkBlock : ChunkBlock
	{
		// Textura personalizada
		public Texture2D m_texture;

		public BigStoneFlameChunkBlock() : base(
			Matrix.CreateScale(4.5f) * Matrix.CreateRotationX(0f) * Matrix.CreateRotationZ(1f),
			Matrix.CreateScale(2.5f) * Matrix.CreateTranslation(0.1875f, 0.0625f, 0f),
			new Color(255, 165, 0), // Color rojizo para indicar fuego
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

		// Método para obtener el nombre mostrado - usa LanguageControl
		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName;
			if (LanguageControl.TryGetBlock("BigStoneFlameChunk:0", "DisplayName", out displayName))
			{
				return displayName;
			}
			return "Flaming Giant Rock";
		}

		// Método para obtener la descripción
		public override string GetDescription(int value)
		{
			string description;
			if (LanguageControl.TryGetBlock("BigStoneFlameChunk:0", "Description", out description))
			{
				return description;
			}
			return "A flaming rock that creates fire on impact. The fire lasts for 30 seconds. Can kill instantly and sets area ablaze.";
		}

		public static int Index = 325;
		private BlockMesh m_blockMesh;
	}
}
