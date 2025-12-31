using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class BigStonePoisonChunkBlock : ChunkBlock
	{
		// Textura personalizada para la piedra venenosa
		public Texture2D m_texture;

		public BigStonePoisonChunkBlock() : base(
			Matrix.CreateScale(4.5f) * Matrix.CreateRotationX(0f) * Matrix.CreateRotationZ(1f),
			Matrix.CreateScale(2.5f) * Matrix.CreateTranslation(0.1875f, 0.0625f, 0f),
			new Color(50, 200, 50), // Color verdoso para indicar veneno
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

		// Sobrescribir CreateDebrisParticleSystem para usar partículas venenosas
		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			// Partículas con color verde para indicar veneno
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, this.DestructionDebrisScale, new Color(50, 200, 50), 0, m_texture);
		}

		// Método para obtener el nombre mostrado - usa LanguageControl
		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName;
			if (LanguageControl.TryGetBlock("BigStonePoisonChunk:0", "DisplayName", out displayName))
			{
				return displayName;
			}
			return "Poisonous Giant Rock";
		}

		// Método para obtener la descripción
		public override string GetDescription(int value)
		{
			string description;
			if (LanguageControl.TryGetBlock("BigStonePoisonChunk:0", "Description", out description))
			{
				return description;
			}
			return "A giant rock imbued with toxic properties. Upon impact, it releases a poisonous cloud that slowly weakens its victims. While the initial damage is moderate, the lingering poison effect can be deadly over time.";
		}

		// Índice del bloque (debe coincidir con el del CSV)
		public static int Index = 326;
		private BlockMesh m_blockMesh;
	}
}
