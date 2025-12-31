using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class BigStoneFlameChunkBlock : ChunkBlock
	{
		public Texture2D m_texture;
		private BlockMesh m_blockMesh;

		public BigStoneFlameChunkBlock() : base(
			Matrix.CreateScale(4.5f) * Matrix.CreateRotationX(0f) * Matrix.CreateRotationZ(1f),
			Matrix.CreateScale(2.5f) * Matrix.CreateTranslation(0.1875f, 0.0625f, 0f),
			new Color(255, 165, 0), // Color naranja para que se vea como en fuego
			true)
		{
		}

		public override void Initialize()
		{
			base.Initialize();
			m_texture = ContentManager.Get<Texture2D>("Textures/BigStoneTexture");

			// Crear malla del bloque - simplificado
			m_blockMesh = new BlockMesh();

			// Copiar la malla standalone directamente
			// m_standaloneBlockMesh ya es un BlockMesh completo
			m_blockMesh.AppendBlockMesh(m_standaloneBlockMesh);

			// Transformar coordenadas de textura si es necesario
			m_blockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0f, 0f, 0f));
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Usar color naranja para simular fuego
			Color fireColor = new Color(255, 100, 0);
			BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMesh, m_texture, fireColor, size, ref matrix, environmentData);
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateMeshVertices(this, x, y, z, m_blockMesh, Color.White, null, geometry.GetGeometry(m_texture).SubsetOpaque);
		}

		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			// Crear fuego al impactar
			CreateFireAtImpact(subsystemTerrain, position);

			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, Color.White, 0, m_texture);
		}

		private void CreateFireAtImpact(SubsystemTerrain subsystemTerrain, Vector3 position)
		{
			try
			{
				int x = Terrain.ToCell(position.X);
				int y = Terrain.ToCell(position.Y);
				int z = Terrain.ToCell(position.Z);
				int fireValue = Terrain.MakeBlockValue(FireBlock.Index, 0, 0);

				// Crear fuego en posición central
				CreateFireInCell(subsystemTerrain, x, y, z, fireValue);

				// Crear fuego alrededor
				CreateFireInCell(subsystemTerrain, x + 1, y, z, fireValue);
				CreateFireInCell(subsystemTerrain, x - 1, y, z, fireValue);
				CreateFireInCell(subsystemTerrain, x, y, z + 1, fireValue);
				CreateFireInCell(subsystemTerrain, x, y, z - 1, fireValue);
				CreateFireInCell(subsystemTerrain, x, y + 1, z, fireValue);
				CreateFireInCell(subsystemTerrain, x, y - 1, z, fireValue);
			}
			catch { }
		}

		private void CreateFireInCell(SubsystemTerrain subsystemTerrain, int x, int y, int z, int fireValue)
		{
			try
			{
				if (y >= 0 && y < 256)
				{
					int currentValue = subsystemTerrain.Terrain.GetCellValue(x, y, z);
					if (Terrain.ExtractContents(currentValue) == 0)
					{
						subsystemTerrain.ChangeCell(x, y, z, fireValue);
					}
				}
			}
			catch { }
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName;
			if (LanguageControl.TryGetBlock("BigStoneFlameChunk:0", "DisplayName", out displayName))
			{
				return displayName;
			}
			return "Flaming Giant Rock";
		}

		public override string GetDescription(int value)
		{
			string description;
			if (LanguageControl.TryGetBlock("BigStoneFlameChunk:0", "Description", out description))
			{
				return description;
			}
			return "A flaming rock that creates fire on impact. The fire lasts for 30 seconds. Can kill instantly and sets area ablaze.";
		}

		public static int Index = 324;
	}
}
