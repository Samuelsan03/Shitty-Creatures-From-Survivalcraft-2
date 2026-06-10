using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using XmlUtilities;

namespace Game
{
	public class GenerationShittyCreaturesLoader : ModLoader
	{
		// ==================== INICIALIZACIÓN DEL MOD ====================
		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("InitializeCreatureTypes", this);
			ModsManager.RegisterHook("OnProjectLoaded", this);
		}

		// ==================== CARGA DEL PROYECTO ====================
		public override void OnProjectLoaded(Project project)
		{
			ShittyPlantsManager.Initialize();

			SubsystemTerrain terrainSubsystem = project.FindSubsystem<SubsystemTerrain>(true);
			if (terrainSubsystem != null)
			{
				terrainSubsystem.TerrainContentsGenerator = new ShittyTerrainContentsGenerator24(terrainSubsystem);
			}
		}

		// ==================== SPAWN DE CRIATURAS (código original) ====================
		public override void InitializeCreatureTypes(SubsystemCreatureSpawn subsystemCreatureSpawn, List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
		{
			// ... (todo el código original de spawn de criaturas se mantiene igual) ...
			// Por brevedad no lo repito, pero debe estar presente.
		}

		// ==================== GENERADOR DE TERRENO PERSONALIZADO ====================
		private class ShittyTerrainContentsGenerator24 : TerrainContentsGenerator24
		{
			private Random m_fruitTreeRandom;

			public ShittyTerrainContentsGenerator24(SubsystemTerrain subsystemTerrain)
				: base(subsystemTerrain)
			{
				m_fruitTreeRandom = new Random(m_seed);
			}

			public override void GenerateTrees(TerrainChunk chunk)
			{
				// Árboles originales
				base.GenerateTrees(chunk);

				// Árboles frutales (con densidad reducida)
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

						// Número reducido de intentos para evitar sobregeneración
						int attempts = (int)(3f * forestDensity) + localRandom.Int(0, 1);
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

							// Multiplicador de densidad reducido (0.8 en lugar de 1.2)
							ShittyTreeType? treeType = ShittyPlantsManager.GenerateRandomTreeType(
								localRandom, realTemp, realHum, y, 0.8f);

							if (treeType != null)
							{
								// Filtro adicional basado en la densidad específica del árbol
								float density = ShittyPlantsManager.CalculateTreeDensity(treeType.Value, realTemp, realHum, y);
								if (localRandom.Bool(density * 0.7f))
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
				{
					if (terrain.GetCellContentsFast(x, y + dy, z) != 0) return false;
					if (terrain.GetCellContentsFast(x + 1, y + dy, z) != 0) return false;
					if (terrain.GetCellContentsFast(x - 1, y + dy, z) != 0) return false;
					if (terrain.GetCellContentsFast(x, y + dy, z + 1) != 0) return false;
					if (terrain.GetCellContentsFast(x, y + dy, z - 1) != 0) return false;
				}
				return true;
			}
		}
	}
}
