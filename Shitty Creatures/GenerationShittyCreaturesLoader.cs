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
			// Inicializar los pinceles de árboles frutales (reinicializar siempre)
			ShittyPlantsManager.Initialize(); // FORZAR reinicialización

			SubsystemTerrain terrainSubsystem = project.FindSubsystem<SubsystemTerrain>(true);
			if (terrainSubsystem != null)
			{
				SubsystemGameInfo gameInfo = project.FindSubsystem<SubsystemGameInfo>(true);
				TerrainGenerationMode mode = gameInfo.WorldSettings.TerrainGenerationMode;

				if (mode != TerrainGenerationMode.FlatContinent && mode != TerrainGenerationMode.FlatIsland)
				{
					terrainSubsystem.TerrainContentsGenerator = new ShittyTerrainContentsGenerator24(terrainSubsystem);
				}
			}
		}

		// ==================== SPAWN DE CRIATURAS ====================
		public override void InitializeCreatureTypes(SubsystemCreatureSpawn subsystemCreatureSpawn, List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
		{
			// Subsistemas necesarios
			SubsystemTime time = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTime>(true);
			SubsystemTimeOfDay timeOfDay = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
			SubsystemGameInfo gameInfo = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemGameInfo>(true);
			SubsystemSky sky = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemSky>(true);
			SubsystemSeasons seasons = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemSeasons>(true);
			SubsystemTerrain terrain = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTerrain>(true);
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

			// Función auxiliar para verificar bloque de suelo válido (para bandits) - IGUAL QUE EL ORIGINAL
			Func<Point3, bool> isValidGround = delegate (Point3 point)
			{
				int cellValue = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
				int blockAbove = Terrain.ExtractContents(cellValue);
				int cellValueHead = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
				int blockHead = Terrain.ExtractContents(cellValueHead);
				if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					return false;
				int cellValueGround = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
				int groundBlock = Terrain.ExtractContents(cellValueGround);
				return (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8);
			};

			// Función auxiliar para spawn de bandits (1 criatura)
			Func<SubsystemCreatureSpawn.CreatureType, Point3, string, int> spawnBandit = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point, string templateName)
			{
				int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
				Point3 correctedPoint = new Point3(point.X, topHeight, point.Z);
				return subsystemCreatureSpawn.SpawnCreatures(ct, templateName, correctedPoint, 1).Count;
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

			// ==========================================
			// 11. BANDIT1 - 20% probabilidad - DESDE DÍA 0, cualquier hora, estación, ubicación
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit1", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (isValidGround(point))
						return 0.1f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit1");
				}
			});

			// ==========================================
			// 12. BANDIT2 - 10% probabilidad - DESDE DÍA 0, cualquier hora, estación, ubicación
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit2", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (isValidGround(point))
						return 0.1f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit2");
				}
			});

			// ==========================================
			// 13. BANDIT3 - 10% probabilidad - SOLO DE NOCHE
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit3", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity >= 0.3f) return 0f;
					if (isValidGround(point))
						return 0.1f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit3");
				}
			});

			// ==========================================
			// 14. BANDIT4 - 10% probabilidad - SOLO DE NOCHE
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit4", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity >= 0.3f) return 0f;
					if (isValidGround(point))
						return 0.5f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit4");
				}
			});

			// ==========================================
			// 15. BANDIT5 - 10% probabilidad - SOLO DE DÍA
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit5", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity <= 0.5f) return 0f;
					if (isValidGround(point))
						return 0.05f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit5");
				}
			});

			// ==========================================
			// 16. BANDIT6 - 10% probabilidad - SOLO DE DÍA
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit6", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity <= 0.5f) return 0f;
					if (isValidGround(point))
						return 0.05f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit6");
				}
			});

			// ==========================================
			// 17. BANDIT7 - 45% probabilidad - CUALQUIER HORA
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit7", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (isValidGround(point))
						return 0.5f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit7");
				}
			});

			// ==========================================
			// 18. BANDIT8 - 5% probabilidad - SOLO DE DÍA, SOLO OTOÑO
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit8", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity <= 0.5f) return 0f;
					Season currentSeason = subsystemCreatureSpawn.m_subsystemSeasons.Season;
					if (currentSeason != Season.Autumn) return 0f;
					if (isValidGround(point))
						return 0.025f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit8");
				}
			});

			// ==========================================
			// 19. BANDIT9 - 5% probabilidad - SOLO DE DÍA, SOLO OTOÑO
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit9", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity <= 0.5f) return 0f;
					Season currentSeason = subsystemCreatureSpawn.m_subsystemSeasons.Season;
					if (currentSeason != Season.Autumn) return 0f;
					if (isValidGround(point))
						return 0.05f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit9");
				}
			});

			// ==========================================
			// 20. BANDIT10 - 10% probabilidad - SOLO DE NOCHE, SOLO INVIERNO
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit10", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity >= 0.3f) return 0f;
					Season currentSeason = subsystemCreatureSpawn.m_subsystemSeasons.Season;
					if (currentSeason != Season.Winter) return 0f;
					if (isValidGround(point))
						return 0.1f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit10");
				}
			});

			// ==========================================
			// 21. BANDIT11 - 45% probabilidad - CUALQUIER HORA
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit11", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (isValidGround(point))
						return 0.15f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit11");
				}
			});

			// ==========================================
			// 22. BANDIT12 - 5% probabilidad - SOLO DE DÍA, SOLO PRIMAVERA
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit12", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity <= 0.5f) return 0f;
					Season currentSeason = subsystemCreatureSpawn.m_subsystemSeasons.Season;
					if (currentSeason != Season.Spring) return 0f;
					if (isValidGround(point))
						return 0.25f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit12");
				}
			});

			// ==========================================
			// 23. BANDIT13 - 5% probabilidad - SOLO VERANO
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit13", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					Season currentSeason = subsystemCreatureSpawn.m_subsystemSeasons.Season;
					if (currentSeason != Season.Summer) return 0f;
					if (isValidGround(point))
						return 0.05f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit13");
				}
			});

			// ==========================================
			// 24. BANDIT14 - 5% probabilidad - SOLO DE NOCHE, OTOÑO/INVIERNO
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit14", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity >= 0.3f) return 0f;
					Season currentSeason = subsystemCreatureSpawn.m_subsystemSeasons.Season;
					if (currentSeason != Season.Autumn && currentSeason != Season.Winter) return 0f;
					if (isValidGround(point))
						return 0.25f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit14");
				}
			});

			// ==========================================
			// 25. BANDIT15 - 5% probabilidad - PRIMAVERA/VERANO
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit15", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					Season currentSeason = subsystemCreatureSpawn.m_subsystemSeasons.Season;
					if (currentSeason != Season.Spring && currentSeason != Season.Summer) return 0f;
					if (isValidGround(point))
						return 0.25f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit15");
				}
			});

			// ==========================================
			// 26. BANDIT16 - SOLO DE NOCHE
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit16", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity >= 0.3f) return 0f;
					if (isValidGround(point))
						return 0.15f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit16");
				}
			});

			// ==========================================
			// 27. BANDIT17 - SOLO DE NOCHE
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit17", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity >= 0.3f) return 0f;
					if (isValidGround(point))
						return 0.5f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit17");
				}
			});

			// ==========================================
			// 28. FIREARMS DEALER - 10% probabilidad - CUALQUIER HORA, ESTACIÓN, UBICACIÓN            // ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("FirearmsDealer", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (isValidGround(point))
						return 0.5f;
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "FirearmsDealer");
				}
			});

			// ==========================================
			// 29. CAVE SPIDER - SPAWN EN CUEVAS (SIEMPRE ACTIVO, CONTROLADO POR SpiderSpawnEnabled)
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("CaveSpider", SpawnLocationType.Cave, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					// Solo spawnear si la configuración de arañas está activada
					if (!ShittyCreaturesSettingsManager.SpiderSpawnEnabled)
						return 0f;

					// Verificar que sea un bloque de cueva válido
					int cellValue = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(cellValue);

					// Bloques de cueva: Stone=2, Dirt=3, Gravel=4, GravelBlock=66, Sandstone=67, Clay=7
					if (contents == 2 || contents == 3 || contents == 4 ||
						contents == 66 || contents == 67 || contents == 7)
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					// Spawnear una sola araña por punto
					return subsystemCreatureSpawn.SpawnCreatures(ct, "InfectedSpider", point, 1).Count;
				}
			});
		}
	}
}
