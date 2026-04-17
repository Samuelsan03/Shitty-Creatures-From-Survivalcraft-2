using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class BigStoneChunkBlock : ChunkBlock
	{
		// Textura personalizada
		public Texture2D m_texture;

		public BigStoneChunkBlock() : base(
			Matrix.CreateScale(4.5f) * Matrix.CreateRotationX(0f) * Matrix.CreateRotationZ(1f),
			Matrix.CreateScale(2.5f) * Matrix.CreateTranslation(0.1875f, 0.0625f, 0f),
			new Color(64, 0, 55),
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
			if (LanguageControl.TryGetBlock("BigStoneChunk:0", "DisplayName", out displayName))
			{
				return displayName;
			}
			return "Rare Giant Rock";
		}

		// Método para obtener la descripción
		public override string GetDescription(int value)
		{
			string description;
			if (LanguageControl.TryGetBlock("BigStoneChunk:0", "Description", out description))
			{
				return description;
			}
			return "A strange rock, seemingly unearthed from the Paleolithic era its true origin remains a mystery. Oh no! What makes it most alarming is its deadly power: a single throw can kill any person instantly. Even worse, the thought of Tanks wielding it as a throwable weapon is a dire omen. Beware if one of these massive stones is hurled your way, you are already as good as dead. As a note, this rare giant rock cannot be crafted; only Tanks will possess it. Unless you resort to cheats such as using Creative mode or similar methods to obtain one but it's not recommended.";
		}

		public static int Index = 323;
        private BlockMesh m_blockMesh;
    }
}
