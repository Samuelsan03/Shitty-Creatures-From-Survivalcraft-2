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
			// 1. Árboles normales del juego
			base.GenerateTrees(chunk);

			// 2. Árboles frutales adicionales
			if (!TGExtras) return;

			Terrain terrain = m_subsystemTerrain.Terrain;
			int chunkX = chunk.Coords.X;
			int chunkZ = chunk.Coords.Y;

			Random random = new Random(m_seed + chunkX + 3943 * chunkZ + 12345);
			int humidity = CalculateHumidity(chunkX * 16, chunkZ * 16);
			int baseTemperature = CalculateTemperature(chunkX * 16, chunkZ * 16);
			float forestDensity = CalculateForestDensity(chunkX * 16, chunkZ * 16);

			// Menos árboles frutales que en vanilla
			int targetFruitTrees = (int)(2f * forestDensity);
			int attempts = 0;
			int planted = 0;
			int maxAttempts = 36;

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
				if (ground != 2 && ground != 8) continue;

				y++; // posición de plantación

				// Espacio inmediato libre
				if (BlocksManager.Blocks[terrain.GetCellContentsFast(worldX + 1, y, worldZ)].IsCollidable ||
					BlocksManager.Blocks[terrain.GetCellContentsFast(worldX - 1, y, worldZ)].IsCollidable ||
					BlocksManager.Blocks[terrain.GetCellContentsFast(worldX, y, worldZ + 1)].IsCollidable ||
					BlocksManager.Blocks[terrain.GetCellContentsFast(worldX, y, worldZ - 1)].IsCollidable)
					continue;

				// Evitar superposición con otros árboles frutales
				if (occupied[localX, localZ]) continue;
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
								tooClose = true;
						}
					}
				}
				if (tooClose) continue;

				// Temperatura ajustada por altura (como hace el generador vanilla)
				int adjustedTemp = baseTemperature + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);

				// Elegir tipo (puede fallar por clima o densidad)
				ShittyTreeType? fruitTreeType = ShittyPlantsManager.GenerateRandomFruitTreeType(random, adjustedTemp, humidity, y);
				if (fruitTreeType == null) continue;

				var brushes = ShittyPlantsManager.GetTreeBrushes(fruitTreeType.Value);
				if (brushes.Count == 0) continue;

				TerrainBrush brush = brushes[random.Int(brushes.Count)];
				brush.PaintFast(chunk, worldX, y, worldZ);
				chunk.AddBrushPaint(worldX, y, worldZ, brush);

				// Marcar zona ocupada
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dz = -1; dz <= 1; dz++)
					{
						int markX = localX + dx;
						int markZ = localZ + dz;
						if (markX >= 0 && markX < 16 && markZ >= 0 && markZ < 16)
							occupied[markX, markZ] = true;
					}
				}
				planted++;
			}

			// 3. Arbustos de arándanos
			int blueberryBushIndex = ShittyPlantsManager.GetBlueberryBushIndex();
			if (blueberryBushIndex != 0)
			{
				int targetBushes = (int)(4f * forestDensity); // Mayor densidad que árboles frutales
				int bushPlanted = 0;
				int bushAttempts = 0;
				int maxBushAttempts = targetBushes * 3;

				while (bushAttempts < maxBushAttempts && bushPlanted < targetBushes)
				{
					bushAttempts++;
					int localX = random.Int(2, 13);
					int localZ = random.Int(2, 13);
					int worldX = chunkX * 16 + localX;
					int worldZ = chunkZ * 16 + localZ;
					int y = terrain.CalculateTopmostCellHeight(worldX, worldZ);

					if (y < 66) continue;
					int ground = terrain.GetCellContentsFast(worldX, y, worldZ);
					// Solo sobre tierra o hierba (2 = dirt, 8 = grass)
					if (ground != 2 && ground != 8) continue;

					y++; // Posición del arbusto

					// No colocar sobre otro arbusto o bloque ocupado
					if (occupied[localX, localZ]) continue;
					int existing = terrain.GetCellContentsFast(worldX, y, worldZ);
					if (existing != 0) continue;

					int adjustedTemp = baseTemperature + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
					if (!ShittyPlantsManager.CanPlaceBlueberryBush(adjustedTemp, humidity, y))
						continue;

					// Colocar el arbusto
					terrain.SetCellValueFast(worldX, y, worldZ, Terrain.MakeBlockValue(blueberryBushIndex));
					chunk.ModificationCounter++;
					// Marcar posición ocupada para evitar superposición cercana
					occupied[localX, localZ] = true;
					bushPlanted++;
				}
			}
		}
	}
}
