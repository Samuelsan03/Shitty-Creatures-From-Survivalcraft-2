using System;
using Engine;
using Game;

namespace Game
{
	/// <summary>
	/// Generador de terreno personalizado que añade árboles frutales, arbustos de arándanos y sandías.
	/// </summary>
	public class ShittyTerrainContentsGenerator24 : TerrainContentsGenerator24
	{
		private Random m_fruitTreeRandom;
		private Random m_cropsRandom;

		public ShittyTerrainContentsGenerator24(SubsystemTerrain subsystemTerrain)
			: base(subsystemTerrain)
		{
			m_fruitTreeRandom = new Random(m_seed);
			m_cropsRandom = new Random(m_seed + 7777);
		}

		// ==================== ÁRBOLES FRUTALES ====================
		public override void GenerateTrees(TerrainChunk chunk)
		{
			base.GenerateTrees(chunk);
			GenerateFruitTrees(chunk);
		}

		private void GenerateFruitTrees(TerrainChunk chunk)
		{
			Terrain terrain = m_subsystemTerrain.Terrain;
			int chunkX = chunk.Coords.X;
			int chunkZ = chunk.Coords.Y;

			for (int i = chunkX; i <= chunkX; i++)
			{
				for (int j = chunkZ; j <= chunkZ; j++)
				{
					Random localRandom = new Random(m_seed + i + 3943 * j);
					int humidity = CalculateHumidity(i * 16 + 8, j * 16 + 8);
					int temperature = CalculateTemperature(i * 16 + 8, j * 16 + 8);
					float forestDensity = CalculateForestDensity(i * 16 + 8, j * 16 + 8);

					// Número reducido de intentos para evitar sobregeneración (1.5f en lugar de 3f)
					int attempts = (int)(1.5f * forestDensity) + localRandom.Int(0, 1);
					int planted = 0;
					int maxAttempts = attempts * 3;

					for (int attempt = 0; attempt < maxAttempts && planted < attempts; attempt++)
					{
						int x = i * 16 + localRandom.Int(2, 13);
						int z = j * 16 + localRandom.Int(2, 13);
						int y = terrain.CalculateTopmostCellHeight(x, z);
						if (y < 66) continue;

						int groundContents = terrain.GetCellContentsFast(x, y, z);
						if (groundContents != 2 && groundContents != 8) continue;
						if (!IsSpaceForTree(terrain, x, y + 1, z)) continue;

						int realTemp = terrain.GetTemperature(x, z) + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
						int realHum = terrain.GetHumidity(x, z);

						ShittyTreeType? treeType = ShittyPlantsManager.GenerateRandomTreeType(localRandom, realTemp, realHum, y, 0.6f);
						if (treeType != null)
						{
							float density = ShittyPlantsManager.CalculateTreeDensity(treeType.Value, realTemp, realHum, y);
							// Factor reducido a 0.3f para menor probabilidad de plantación
							if (localRandom.Bool(density * 0.3f))
							{
								var brushes = ShittyPlantsManager.GetTreeBrushes(treeType.Value);
								if (brushes.Count > 0)
								{
									TerrainBrush brush = brushes[localRandom.Int(0, brushes.Count - 1)];
									brush.PaintFast(chunk, x, y + 1, z);
									chunk.AddBrushPaint(x, y + 1, z, brush);

									float fruitDensity = ShittyPlantsManager.CalculateFruitDensity(treeType.Value, realTemp, realHum, y);
									ShittyPlantsManager.AttachFruitsToTreeFast(chunk, x, y + 1, z, brush, treeType.Value, localRandom, fruitDensity);
									planted++;
								}
							}
						}
					}
				}
			}
		}

		private bool IsSpaceForTree(Terrain terrain, int x, int y, int z)
		{
			for (int dy = 0; dy < 8; dy++)
				if (terrain.GetCellContentsFast(x, y + dy, z) != 0 ||
					terrain.GetCellContentsFast(x + 1, y + dy, z) != 0 ||
					terrain.GetCellContentsFast(x - 1, y + dy, z) != 0 ||
					terrain.GetCellContentsFast(x, y + dy, z + 1) != 0 ||
					terrain.GetCellContentsFast(x, y + dy, z - 1) != 0)
					return false;
			return true;
		}

		// ==================== PLANTAS ADICIONALES (ARÁNDANOS Y SANDÍAS) ====================
		public override void GenerateGrassAndPlants(TerrainChunk chunk)
		{
			// Primero generamos la vegetación original (hierbas, flores, etc.)
			base.GenerateGrassAndPlants(chunk);

			// Luego generamos nuestros cultivos adicionales
			GenerateBlueberries(chunk);
			GenerateWatermelons(chunk);
		}

		private void GenerateBlueberries(TerrainChunk chunk)
		{
			// Obtener índice del arbusto de arándanos
			int blueberryIndex = BlocksManager.GetBlockIndex("BlueberryBushBlock", false);
			if (blueberryIndex == -1) return;

			Terrain terrain = m_subsystemTerrain.Terrain;
			Random random = new Random(m_seed + chunk.Coords.X + 888 * chunk.Coords.Y);

			// Probabilidad de que el chunk tenga arándanos (~30%)
			if (!random.Bool(0.3f)) return;

			// Número máximo de arbustos en este chunk
			int maxPlants = random.Int(0, 3);

			for (int attempt = 0; attempt < 12 && maxPlants > 0; attempt++)
			{
				int x = chunk.Origin.X + random.Int(1, 14);
				int z = chunk.Origin.Y + random.Int(1, 14);
				int y = terrain.CalculateTopmostCellHeight(x, z);

				// Condiciones de altura (evitar agua, montañas muy altas)
				if (y < 65 || y > 100) continue;

				int groundValue = terrain.GetCellValueFast(x, y, z);
				int groundContents = Terrain.ExtractContents(groundValue);
				// Debe crecer sobre césped o tierra
				if (groundContents != 2 && groundContents != 8) continue;

				int temperature = terrain.GetTemperature(x, z);
				int humidity = terrain.GetHumidity(x, z);

				// Condiciones climáticas: humedad media/alta, temperatura templada
				if (humidity >= 7 && temperature >= 6 && temperature <= 14)
				{
					// Verificar espacio aéreo
					if (terrain.GetCellContentsFast(x, y + 1, z) == 0)
					{
						int value = Terrain.MakeBlockValue(blueberryIndex);
						chunk.SetCellValueFast(x - chunk.Origin.X, y + 1, z - chunk.Origin.Y, value);
						maxPlants--;
					}
				}
			}
		}

		private void GenerateWatermelons(TerrainChunk chunk)
		{
			// Obtener índices de sandía normal y podrida
			int watermelonIndex = BlocksManager.GetBlockIndex("WatermelonBlock", false);
			int rottenWatermelonIndex = BlocksManager.GetBlockIndex("RottenWatermelonBlock", false);
			if (watermelonIndex == -1) return;

			Terrain terrain = m_subsystemTerrain.Terrain;
			Random random = new Random(m_seed + chunk.Coords.X + 999 * chunk.Coords.Y);

			// Probabilidad baja (15% de que el chunk tenga sandías)
			if (!random.Bool(0.15f)) return;

			int maxWatermelons = random.Int(0, 2);

			for (int attempt = 0; attempt < 8 && maxWatermelons > 0; attempt++)
			{
				int x = chunk.Origin.X + random.Int(1, 14);
				int z = chunk.Origin.Y + random.Int(1, 14);
				int y = terrain.CalculateTopmostCellHeight(x, z);

				if (y < 65 || y > 85) continue;

				int groundValue = terrain.GetCellValueFast(x, y, z);
				int groundContents = Terrain.ExtractContents(groundValue);
				// Las sandías crecen sobre césped o tierra (a veces en tierras volcánicas)
				if (groundContents != 2 && groundContents != 8) continue;

				int temperature = terrain.GetTemperature(x, z);
				int humidity = terrain.GetHumidity(x, z);

				// Condiciones: humedad alta, temperatura cálida
				if (humidity >= 9 && temperature >= 7 && temperature <= 14)
				{
					// Espacio aéreo suficiente (al menos 2 bloques de alto)
					if (terrain.GetCellContentsFast(x, y + 1, z) == 0 &&
						terrain.GetCellContentsFast(x, y + 2, z) == 0)
					{
						int fruitIndex = watermelonIndex;
						// Posibilidad de sandía podrida si temperatura es extremadamente alta o humedad baja
						if (temperature > 13 || (humidity < 10 && random.Bool(0.2f)))
							fruitIndex = rottenWatermelonIndex != -1 ? rottenWatermelonIndex : watermelonIndex;

						// Crear sandía madura (tamaño 7)
						int data = BaseWatermelonBlock.SetSize(BaseWatermelonBlock.SetIsDead(0, false), 7);
						int value = Terrain.MakeBlockValue(fruitIndex, 0, data);
						chunk.SetCellValueFast(x - chunk.Origin.X, y + 1, z - chunk.Origin.Y, value);
						maxWatermelons--;
					}
				}
			}
		}
	}
}
