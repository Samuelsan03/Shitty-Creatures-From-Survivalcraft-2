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

		// ==================== CARGA DEL PROYECTO (reemplazar generador de terreno) ====================
		public override void OnProjectLoaded(Project project)
		{
			// Inicializar los pinceles de árboles frutales
			ShittyPlantsManager.Initialize();

			SubsystemTerrain terrainSubsystem = project.FindSubsystem<SubsystemTerrain>(true);
			if (terrainSubsystem != null)
			{
				// Obtener la configuración del mundo
				SubsystemGameInfo gameInfo = project.FindSubsystem<SubsystemGameInfo>(true);
				TerrainGenerationMode mode = gameInfo.WorldSettings.TerrainGenerationMode;

				// Solo reemplazar el generador en modos que NO sean planos
				if (mode != TerrainGenerationMode.FlatContinent && mode != TerrainGenerationMode.FlatIsland)
				{
					terrainSubsystem.TerrainContentsGenerator = new ShittyTerrainContentsGenerator24(terrainSubsystem);
				}
				// Para modos planos, el juego usará su generador original (Flat/FlatIsland)
				// y no se añadirán árboles frutales, arbustos ni sandías.
			}
		}

		// ==================== SPAWN DE CRIATURAS (código original) ====================
		public override void InitializeCreatureTypes(SubsystemCreatureSpawn subsystemCreatureSpawn, List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
		{
			// Subsistemas necesarios
			SubsystemTime time = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTime>(true);
			SubsystemTimeOfDay timeOfDay = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
			SubsystemGameInfo gameInfo = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemGameInfo>(true);
			SubsystemSky sky = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemSky>(true);
			SubsystemSeasons seasons = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemSeasons>(true);
			Season currentSeason = seasons.Season;

			// Función auxiliar: detectar si un punto está cerca del agua
			Func<Point3, bool> isNearWater = delegate (Point3 point)
			{
				float shoreDistance = subsystemCreatureSpawn.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
				int blockUnder = Terrain.ExtractContents(subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
				return (shoreDistance >= -5f && shoreDistance <= 15f) || (BlocksManager.Blocks[blockUnder] is WaterBlock);
			};

			// Función auxiliar: aparecer grupo de 3 o 5 criaturas
			Func<SubsystemCreatureSpawn, SubsystemCreatureSpawn.CreatureType, Point3, string, int> spawnGroup = delegate (SubsystemCreatureSpawn spawnSys, SubsystemCreatureSpawn.CreatureType ct, Point3 point, string templateName)
			{
				int count = spawnSys.m_random.Int(0, 1) == 0 ? 3 : 5;
				return spawnSys.SpawnCreatures(ct, templateName, point, count).Count;
			};

			// ==========================================
			// 1. PIRATA NORMAL (día ≥ 5)
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("PirataNormal", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime) < 5.0) return 0f;
					return isNearWater(point) ? 2.5f : 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnGroup(subsystemCreatureSpawn, ct, point, "PirataNormal");
				}
			});

			// ==========================================
			// 2. PIRATA ELITE (día ≥ 15)
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("PirataElite", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime) < 15.0) return 0f;
					return isNearWater(point) ? 2.5f : 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnGroup(subsystemCreatureSpawn, ct, point, "PirataElite");
				}
			});

			// ==========================================
			// 3. PIRATA HOSTIL COMERCIANTE (día ≥ 35) - solo
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("PirataHostilComerciante", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime) < 35.0) return 0f;
					return isNearWater(point) ? 2.5f : 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return subsystemCreatureSpawn.SpawnCreatures(ct, "PirataHostilComerciante", point, 1).Count;
				}
			});

			// ==========================================
			// 4. CAPITÁN PIRATA (día ≥ 55) - solo
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("CapitanPirata", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime) < 55.0) return 0f;
					return isNearWater(point) ? 2.5f : 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return subsystemCreatureSpawn.SpawnCreatures(ct, "CapitanPirata", point, 1).Count;
				}
			});

			// ==========================================
			// 5. RAYMAN (montañas altas)
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Rayman", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					float mountainFactor = subsystemCreatureSpawn.m_subsystemTerrain.TerrainContentsGenerator.CalculateMountainRangeFactor((float)point.X, (float)point.Z);
					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					bool nearTop = (point.Y >= topHeight - 5);
					if (mountainFactor >= 0.95f && topHeight >= 120 && nearTop)
						return 5000f;
					else
						return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					Point3 correctedPoint = new Point3(point.X, topHeight, point.Z);
					return subsystemCreatureSpawn.SpawnCreatures(ct, "Rayman", correctedPoint, 1).Count;
				}
			});

			// ==========================================
			// 6. SONIC THE HEDGEHOG
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("SonicTheHedgehog", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;

					Season currentSeason = subsystemCreatureSpawn.m_subsystemSeasons.Season;
					if (currentSeason != Season.Spring && currentSeason != Season.Summer)
						return 0f;

					int blockUnder = Terrain.ExtractContents(subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (blockUnder != GrassBlock.Index &&
						blockUnder != DirtBlock.Index &&
						blockUnder != SandBlock.Index &&
						blockUnder != GravelBlock.Index)
						return 0f;

					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					if (point.Y < topHeight - 2) return 0f;

					return 2.5f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					Point3 correctedPoint = new Point3(point.X, topHeight, point.Z);
					return subsystemCreatureSpawn.SpawnCreatures(ct, "SonicTheHedgehog", correctedPoint, 1).Count;
				}
			});

			// ==========================================
			// 7. MILES "TAILS" PROWER
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("MilesTailsPrower", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;

					double totalDays = timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime);
					if (totalDays < 2.0) return 0f;

					// ===== NUEVO: Verificar bloque bajo (como Sonic) =====
					int blockUnder = Terrain.ExtractContents(subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (blockUnder != GrassBlock.Index &&
						blockUnder != DirtBlock.Index &&
						blockUnder != SandBlock.Index &&
						blockUnder != GravelBlock.Index)
						return 0f;

					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					if (point.Y < topHeight - 2) return 0f;

					bool sonicNearby = false;
					Vector3 center = new Vector3(point.X, point.Y, point.Z);
					var bodiesSubsystem = subsystemCreatureSpawn.m_subsystemBodies;
					DynamicArray<ComponentBody> bodies = new DynamicArray<ComponentBody>();
					bodiesSubsystem.FindBodiesAroundPoint(new Vector2(center.X, center.Z), 8f, bodies);

					for (int i = 0; i < bodies.Count; i++)
					{
						ComponentBody body = bodies.Array[i];
						if (body?.Entity != null && Vector3.DistanceSquared(center, body.Position) <= 64f)
						{
							ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
							if (creature != null && creature.Entity.ValuesDictionary?.DatabaseObject?.Name == "SonicTheHedgehog")
							{
								sonicNearby = true;
								break;
							}
						}
					}

					return sonicNearby ? 15f : 1.5f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					Point3 correctedPoint = new Point3(point.X, topHeight, point.Z);
					return subsystemCreatureSpawn.SpawnCreatures(ct, "MilesTailsPrower", correctedPoint, 1).Count;
				}
			});

			// ==========================================
			// 8. KNUCKLES THE ECHIDNA (montañas altas, invierno)
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("KnucklesTheEchidna", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;

					Season currentSeason = subsystemCreatureSpawn.m_subsystemSeasons.Season;
					if (currentSeason != Season.Winter) return 0f;

					float mountainFactor = subsystemCreatureSpawn.m_subsystemTerrain.TerrainContentsGenerator.CalculateMountainRangeFactor((float)point.X, (float)point.Z);
					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					bool nearTop = (point.Y >= topHeight - 5);

					if (mountainFactor >= 0.95f && topHeight >= 120 && nearTop)
						return 5000f;
					else
						return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					Point3 correctedPoint = new Point3(point.X, topHeight, point.Z);
					return subsystemCreatureSpawn.SpawnCreatures(ct, "KnucklesTheEchidna", correctedPoint, 1).Count;
				}
			});

			// ==========================================
			// 9. FANG THE SNIPER (desierto, día)
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("FangTheSniper", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;

					int humidity = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
					int temperature = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
					int blockUnder = Terrain.ExtractContents(subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					if (humidity >= 8 || temperature <= 8 || blockUnder != SandBlock.Index)
						return 0f;

					float shoreDistance = subsystemCreatureSpawn.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
					if (shoreDistance <= 20f) return 0f;

					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					if (point.Y < topHeight - 2) return 0f;

					return 2.5f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					Point3 correctedPoint = new Point3(point.X, topHeight, point.Z);
					return subsystemCreatureSpawn.SpawnCreatures(ct, "FangTheSniper", correctedPoint, 1).Count;
				}
			});

			// ==========================================
			// 10. INFINITE THE JACKAL (desierto, noche)
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("InfiniteTheJackal", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity >= 0.1f) return 0f;

					int humidity = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
					int temperature = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
					int blockUnder = Terrain.ExtractContents(subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					if (humidity >= 8 || temperature <= 8 || blockUnder != SandBlock.Index)
						return 0f;

					float shoreDistance = subsystemCreatureSpawn.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
					if (shoreDistance <= 20f) return 0f;

					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					if (point.Y < topHeight - 2) return 0f;

					return 2.5f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					Point3 correctedPoint = new Point3(point.X, topHeight, point.Z);
					return subsystemCreatureSpawn.SpawnCreatures(ct, "InfiniteTheJackal", correctedPoint, 1).Count;
				}
			});
		}

		// ==================== GENERADOR DE TERRENO PERSONALIZADO (con árboles frutales) ====================
		/// <summary>
		/// Generador de terreno que añade árboles frutales (ShittyTreeType) sin eliminar los árboles originales.
		/// </summary>
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
				// 1. Generar árboles originales (robles, abedules, etc.)
				base.GenerateTrees(chunk);

				// 2. Generar árboles frutales adicionales
				GenerateFruitTrees(chunk);
			}

			private void GenerateFruitTrees(TerrainChunk chunk)
			{
				Terrain terrain = m_subsystemTerrain.Terrain;
				int chunkX = chunk.Coords.X;
				int chunkZ = chunk.Coords.Y;
				Random globalRand = new Random(m_seed + chunkX * 1000 + chunkZ);

				// Densidad global del bosque (factor multiplicador, igual que usabas antes)
				float forestDensity = CalculateForestDensity(chunkX * 16 + 8, chunkZ * 16 + 8);

				// Ejemplo: probamos cada bloque de superficie en el chunk (o puedes usar un stride)
				// Para rendimiento, usamos un paso de 2 o 3, pero igual puedes probar todos.
				for (int dx = 2; dx < 14; dx += 2)
				{
					for (int dz = 2; dz < 14; dz += 2)
					{
						int x = chunkX * 16 + dx;
						int z = chunkZ * 16 + dz;
						int y = terrain.CalculateTopmostCellHeight(x, z);

						if (y < 66) continue;

						// Bloque suelo debe ser césped o tierra
						int ground = terrain.GetCellContentsFast(x, y, z);
						if (ground != 2 && ground != 8) continue;

						// Espacio suficiente para el árbol?
						if (!IsSpaceForTree(terrain, x, y + 1, z)) continue;

						// Temperatura y humedad reales (con ajuste estacional)
						int realTemp = terrain.GetTemperature(x, z) + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
						int realHum = terrain.GetHumidity(x, z);

						// Probabilidad base de que aparezca ALGÚN árbol frutal en esta celda
						// Usamos la densidad máxima entre todos los tipos (o puedes usar un promedio)
						float maxDensity = 0f;
						foreach (ShittyTreeType type in Enum.GetValues(typeof(ShittyTreeType)))
						{
							float d = ShittyPlantsManager.CalculateTreeDensity(type, realTemp, realHum, y);
							if (d > maxDensity) maxDensity = d;
						}

						// Aplicamos un factor global (forestDensity) y un pequeño random para variedad
						float spawnProb = maxDensity * forestDensity * 0.5f; // Ajusta el 0.5f a gusto
						if (globalRand.Float(0, 1) > spawnProb) continue;

						// Ahora elegimos el tipo de árbol aleatoriamente (ya incluye su propia probabilidad)
						Random localRand = new Random(m_seed + x + 3943 * z);
						ShittyTreeType? treeType = ShittyPlantsManager.GenerateRandomTreeType(
							localRand, realTemp, realHum, y, densityMultiplier: 1.2f);

						if (treeType == null) continue;

						var brushes = ShittyPlantsManager.GetTreeBrushes(treeType.Value);
						if (brushes.Count == 0) continue;

						TerrainBrush brush = brushes[localRand.Int(0, brushes.Count - 1)];
						brush.PaintFast(chunk, x, y + 1, z);
						chunk.AddBrushPaint(x, y + 1, z, brush);

						// Colocar frutos usando la densidad calculada (como ya hacías)
						float fruitDensity = ShittyPlantsManager.CalculateFruitDensity(treeType.Value, realTemp, realHum, y);
						ShittyPlantsManager.AttachFruitsToTreeFast(chunk, x, y + 1, z, brush, treeType.Value, localRand, fruitDensity);
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
