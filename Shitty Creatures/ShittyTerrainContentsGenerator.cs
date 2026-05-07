using System;
using Engine;

namespace Game
{
	/// <summary>
	/// Generador de terreno modificado para incluir árboles frutales adicionales.
	/// No reemplaza los árboles vanilla, solo añade árboles frutales.
	/// </summary>
	public class ShittyTerrainContentsGenerator : TerrainContentsGenerator24
	{
		public ShittyTerrainContentsGenerator(SubsystemTerrain subsystemTerrain) : base(subsystemTerrain)
		{
		}

		/// <summary>
		/// Primero genera árboles vanilla, luego añade árboles frutales.
		/// </summary>
		public override void GenerateTrees(TerrainChunk chunk)
		{
			// 1. Árboles normales del juego (robles, abedules, pinos, etc.)
			base.GenerateTrees(chunk);

			// 2. Añadir árboles frutales (manzano, peral, naranjo, cerezo)
			if (!TGExtras) return;

			Terrain terrain = m_subsystemTerrain.Terrain;
			int chunkX = chunk.Coords.X;
			int chunkZ = chunk.Coords.Y;

			Random random = new Random(m_seed + chunkX + 3943 * chunkZ + 12345);
			int humidity = CalculateHumidity(chunkX * 16, chunkZ * 16);
			int temperature = CalculateTemperature(chunkX * 16, chunkZ * 16);
			float forestDensity = CalculateForestDensity(chunkX * 16, chunkZ * 16);

			// Cantidad de árboles frutales según la densidad del bosque
			int targetFruitTrees = (int)(2.5f * forestDensity);
			int attempts = 0;
			int planted = 0;
			int maxAttempts = 48; // Aumentamos los intentos para compensar los rechazos

			// Mapa para rastrear posiciones ocupadas
			bool[,] occupied = new bool[16, 16];

			while (attempts < maxAttempts && planted < targetFruitTrees)
			{
				attempts++;

				int localX = random.Int(2, 13);
				int localZ = random.Int(2, 13);
				int worldX = chunkX * 16 + localX;
				int worldZ = chunkZ * 16 + localZ;
				int y = terrain.CalculateTopmostCellHeight(worldX, worldZ);

				if (y < 66) continue;

				int ground = terrain.GetCellContentsFast(worldX, y, worldZ);
				if (ground != 2 && ground != 8) continue; // Solo césped o tierra

				y++; // posición de plantación

				// Verificar espacio libre inmediato
				if (BlocksManager.Blocks[terrain.GetCellContentsFast(worldX + 1, y, worldZ)].IsCollidable ||
					BlocksManager.Blocks[terrain.GetCellContentsFast(worldX - 1, y, worldZ)].IsCollidable ||
					BlocksManager.Blocks[terrain.GetCellContentsFast(worldX, y, worldZ + 1)].IsCollidable ||
					BlocksManager.Blocks[terrain.GetCellContentsFast(worldX, y, worldZ - 1)].IsCollidable)
					continue;

				// Verificar que la posición no esté ya ocupada por otro árbol frutal
				if (occupied[localX, localZ]) continue;

				// Verificar separación mínima entre árboles frutales (radio de 4 bloques)
				bool tooClose = false;
				for (int dx = -4; dx <= 4 && !tooClose; dx++)
				{
					for (int dz = -4; dz <= 4 && !tooClose; dz++)
					{
						int checkLocalX = localX + dx;
						int checkLocalZ = localZ + dz;
						if (checkLocalX >= 0 && checkLocalX < 16 && checkLocalZ >= 0 && checkLocalZ < 16)
						{
							if (dx == 0 && dz == 0) continue;
							if (occupied[checkLocalX, checkLocalZ])
							{
								tooClose = true;
							}
						}
					}
				}
				if (tooClose) continue;

				// Elegir tipo de árbol frutal según clima
				ShittyTreeType? fruitTreeType = ShittyPlantsManager.GenerateRandomFruitTreeType(random, temperature, humidity, y);
				if (fruitTreeType == null) continue;

				var brushes = ShittyPlantsManager.GetTreeBrushes(fruitTreeType.Value);
				if (brushes.Count == 0) continue;

				// Seleccionar pincel aleatorio
				TerrainBrush brush = brushes[random.Int(brushes.Count)];

				// Aplicar el pincel
				brush.PaintFast(chunk, worldX, y, worldZ);
				chunk.AddBrushPaint(worldX, y, worldZ, brush);

				// Marcar posición como ocupada y sus alrededores inmediatos
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dz = -1; dz <= 1; dz++)
					{
						int markX = localX + dx;
						int markZ = localZ + dz;
						if (markX >= 0 && markX < 16 && markZ >= 0 && markZ < 16)
						{
							occupied[markX, markZ] = true;
						}
					}
				}

				planted++;
			}
		}
	}
}
