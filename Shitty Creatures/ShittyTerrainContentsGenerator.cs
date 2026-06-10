using System;
using Engine;
using Game;

namespace Game
{
	/// <summary>
	/// Generador de terreno personalizado que añade árboles frutales (ShittyTreeType)
	/// sin reemplazar la generación de árboles original.
	/// </summary>
	public class ShittyTerrainContentsGenerator24 : TerrainContentsGenerator24
	{
		private Random m_fruitTreeRandom;

		public ShittyTerrainContentsGenerator24(SubsystemTerrain subsystemTerrain)
			: base(subsystemTerrain)
		{
			// Usamos la misma semilla del mundo para los árboles frutales
			m_fruitTreeRandom = new Random(m_seed);
		}

		/// <summary>
		/// Genera primero los árboles originales y luego añade árboles frutales.
		/// </summary>
		public override void GenerateTrees(TerrainChunk chunk)
		{
			// 1. Generar árboles originales (robles, abedules, etc.)
			base.GenerateTrees(chunk);

			// 2. Generar árboles frutales adicionales
			GenerateFruitTrees(chunk);
		}

		private void GenerateFruitTrees(TerrainChunk chunk)
		{
			Terrain terrain = m_subsystemTerrain.Terrain;
			Point2 origin = chunk.Origin;
			int chunkX = chunk.Coords.X;
			int chunkZ = chunk.Coords.Y;

			// Solo generamos árboles en este mismo chunk (no en vecinos)
			for (int i = chunkX; i <= chunkX; i++)
			{
				for (int j = chunkZ; j <= chunkZ; j++)
				{
					// Semilla local para que sea reproducible
					Random localRandom = new Random(m_seed + i + 3943 * j);

					// Temperatura y humedad en el centro del chunk (para tener una idea del bioma)
					int humidity = CalculateHumidity(i * 16 + 8, j * 16 + 8);
					int temperature = CalculateTemperature(i * 16 + 8, j * 16 + 8);

					// Densidad forestal (similar a la usada por árboles vanilla)
					float forestDensity = CalculateForestDensity(i * 16 + 8, j * 16 + 8);

					// Número de intentos por chunk (entre 0 y ~6 dependiendo de la densidad)
					int attempts = (int)(6f * forestDensity) + localRandom.Int(0, 2);
					int planted = 0;
					int maxAttempts = attempts * 2; // Evitar bucles infinitos

					for (int attempt = 0; attempt < maxAttempts && planted < attempts; attempt++)
					{
						// Posición aleatoria dentro del chunk (evitando bordes)
						int x = i * 16 + localRandom.Int(2, 13);
						int z = j * 16 + localRandom.Int(2, 13);
						int y = terrain.CalculateTopmostCellHeight(x, z);

						if (y < 66) continue; // Muy bajo, probablemente agua

						int groundContents = terrain.GetCellContentsFast(x, y, z);
						if (groundContents != 2 && groundContents != 8) continue; // No es césped ni tierra

						// Verificar espacio libre alrededor
						if (!IsSpaceForTree(terrain, x, y + 1, z)) continue;

						// Temperatura y humedad reales en esa posición
						int realTemp = terrain.GetTemperature(x, z) + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
						int realHum = terrain.GetHumidity(x, z);

						// Decidir si plantar un árbol frutal y de qué tipo
						ShittyTreeType? treeType = ShittyPlantsManager.GenerateRandomTreeType(
							localRandom, realTemp, realHum, y, 1.2f); // Un poco más de densidad que vanilla

						if (treeType != null)
						{
							// Obtener un pincel aleatorio del tipo de árbol
							var brushes = ShittyPlantsManager.GetTreeBrushes(treeType.Value);
							if (brushes.Count > 0)
							{
								TerrainBrush brush = brushes[localRandom.Int(0, brushes.Count - 1)];
								// Pintar el árbol
								brush.PaintFast(chunk, x, y + 1, z);
								// Registrar para posibles actualizaciones futuras (opcional)
								chunk.AddBrushPaint(x, y + 1, z, brush);

								// Las frutas se colocarán automáticamente gracias a AttachFruitsToTree
								// que ya se llama dentro de SubsystemFruitSaplingBlockBehavior.GrowTree
								// (cuando el árbol crece a partir de un retoño). 
								// Pero para generación directa en el mundo, también podemos llamarlo aquí:
								ShittyPlantsManager.AttachFruitsToTree(
									m_subsystemTerrain, x, y + 1, z, brush, treeType.Value, localRandom);

								planted++;
							}
						}
					}
				}
			}
		}

		private bool IsSpaceForTree(Terrain terrain, int x, int y, int z)
		{
			// Comprobación rápida de espacio aéreo y laterales
			for (int dy = 0; dy < 8; dy++)
			{
				if (terrain.GetCellContentsFast(x, y + dy, z) != 0)
					return false;
				if (terrain.GetCellContentsFast(x + 1, y + dy, z) != 0)
					return false;
				if (terrain.GetCellContentsFast(x - 1, y + dy, z) != 0)
					return false;
				if (terrain.GetCellContentsFast(x, y + dy, z + 1) != 0)
					return false;
				if (terrain.GetCellContentsFast(x, y + dy, z - 1) != 0)
					return false;
			}
			return true;
		}
	}
}
