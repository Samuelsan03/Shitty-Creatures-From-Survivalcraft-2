using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using XmlUtilities;

namespace Game
{
	public class GenerationShittyCreaturesLoader : ModLoader
	{
		private SubsystemBanditInvasion m_subsystemBanditInvasion;

		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("InitializeCreatureTypes", this);
			ModsManager.RegisterHook("OnProjectLoaded", this);
		}

		public override void OnProjectLoaded(Project project)
		{
			ShittyPlantsManager.Initialize();

			m_subsystemBanditInvasion = project.FindSubsystem<SubsystemBanditInvasion>(true);

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

		public override void InitializeCreatureTypes(SubsystemCreatureSpawn subsystemCreatureSpawn, List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
		{
			SubsystemTime time = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTime>(true);
			SubsystemTimeOfDay timeOfDay = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
			SubsystemGameInfo gameInfo = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemGameInfo>(true);
			SubsystemSky sky = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemSky>(true);
			SubsystemSeasons seasons = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemSeasons>(true);
			SubsystemTerrain terrain = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTerrain>(true);
			Season currentSeason = seasons.Season;

			Func<Point3, bool> isNearWater = delegate (Point3 point)
			{
				float shoreDistance = subsystemCreatureSpawn.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
				int blockUnder = Terrain.ExtractContents(subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
				return (shoreDistance >= -5f && shoreDistance <= 15f) || (BlocksManager.Blocks[blockUnder] is WaterBlock);
			};

			Func<SubsystemCreatureSpawn, SubsystemCreatureSpawn.CreatureType, Point3, string, int> spawnGroup = delegate (SubsystemCreatureSpawn spawnSys, SubsystemCreatureSpawn.CreatureType ct, Point3 point, string templateName)
			{
				int count = spawnSys.m_random.Int(0, 1) == 0 ? 3 : 5;
				return spawnSys.SpawnCreatures(ct, templateName, point, count).Count;
			};

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

			Func<SubsystemCreatureSpawn.CreatureType, Point3, string, int> spawnBanditWithMode = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point, string templateName)
			{
				int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
				Point3 correctedPoint = new Point3(point.X, topHeight, point.Z);
				List<Entity> entities = subsystemCreatureSpawn.SpawnCreatures(ct, templateName, correctedPoint, 1);
				if (entities.Count > 0 && m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
				{
					Entity entity = entities[0];
					var banditChase = entity.FindComponent<ComponentBanditChaseBehavior>();
					if (banditChase != null)
					{
						banditChase.IsDrugTraffickerMode = true;
					}
				}
				return entities.Count;
			};

			Func<SubsystemCreatureSpawn.CreatureType, Point3, string, int> spawnBandit = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point, string templateName)
			{
				int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
				Point3 correctedPoint = new Point3(point.X, topHeight, point.Z);
				return subsystemCreatureSpawn.SpawnCreatures(ct, templateName, correctedPoint, 1).Count;
			};

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

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("SonicTheHedgehog", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					Season currentSeason = subsystemCreatureSpawn.m_subsystemSeasons.Season;
					if (currentSeason != Season.Spring && currentSeason != Season.Summer)
						return 0f;
					int blockUnder = Terrain.ExtractContents(subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (blockUnder != GrassBlock.Index && blockUnder != DirtBlock.Index && blockUnder != SandBlock.Index && blockUnder != GravelBlock.Index)
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

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("MilesTailsPrower", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					double totalDays = timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime);
					if (totalDays < 2.0) return 0f;
					int blockUnder = Terrain.ExtractContents(subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (blockUnder != GrassBlock.Index && blockUnder != DirtBlock.Index && blockUnder != SandBlock.Index && blockUnder != GravelBlock.Index)
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
			// BANDIDOS CON APARICIÓN NORMAL (DÍA Y NOCHE)
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit7", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (isValidGround(point))
						return 0.25f; // Probabilidad normal
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit7");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit11", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (isValidGround(point))
						return 0.25f; // Probabilidad normal
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit11");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit12", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (isValidGround(point))
						return 0.25f; // Probabilidad normal
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit12");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit17", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (isValidGround(point))
						return 0.25f; // Probabilidad normal
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "Bandit17");
				}
			});

			// VENDEDOR DE ARMAS (Aparición normal diurna)
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("FirearmsDealer", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (isValidGround(point))
						return 0.05f; // Muy raro, aparición normal
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBandit(ct, point, "FirearmsDealer");
				}
			});

			// ==========================================
			// BANDIDOS DE LA INVASIÓN (SOLO DE NOCHE Y SI HAY GUERRA)
			// Respetan las probabilidades del XML
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit1", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
					{
						if (isValidGround(point))
							return m_subsystemBanditInvasion.GetBanditProbability("Bandit1");
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBanditWithMode(ct, point, "Bandit1");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit2", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
					{
						if (isValidGround(point))
							return m_subsystemBanditInvasion.GetBanditProbability("Bandit2");
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBanditWithMode(ct, point, "Bandit2");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit3", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
					{
						if (isValidGround(point))
							return m_subsystemBanditInvasion.GetBanditProbability("Bandit3");
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBanditWithMode(ct, point, "Bandit3");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit4", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
					{
						if (isValidGround(point))
							return m_subsystemBanditInvasion.GetBanditProbability("Bandit4");
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBanditWithMode(ct, point, "Bandit4");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit5", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
					{
						if (isValidGround(point))
							return m_subsystemBanditInvasion.GetBanditProbability("Bandit5");
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBanditWithMode(ct, point, "Bandit5");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit6", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
					{
						if (isValidGround(point))
							return m_subsystemBanditInvasion.GetBanditProbability("Bandit6");
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBanditWithMode(ct, point, "Bandit6");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit8", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
					{
						if (isValidGround(point))
							return m_subsystemBanditInvasion.GetBanditProbability("Bandit8");
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBanditWithMode(ct, point, "Bandit8");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit9", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
					{
						if (isValidGround(point))
							return m_subsystemBanditInvasion.GetBanditProbability("Bandit9");
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBanditWithMode(ct, point, "Bandit9");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit10", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
					{
						if (isValidGround(point))
							return m_subsystemBanditInvasion.GetBanditProbability("Bandit10");
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBanditWithMode(ct, point, "Bandit10");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit13", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
					{
						if (isValidGround(point))
							return m_subsystemBanditInvasion.GetBanditProbability("Bandit13");
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBanditWithMode(ct, point, "Bandit13");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit14", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
					{
						if (isValidGround(point))
							return m_subsystemBanditInvasion.GetBanditProbability("Bandit14");
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBanditWithMode(ct, point, "Bandit14");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit15", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
					{
						if (isValidGround(point))
							return m_subsystemBanditInvasion.GetBanditProbability("Bandit15");
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBanditWithMode(ct, point, "Bandit15");
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Bandit16", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (m_subsystemBanditInvasion != null && m_subsystemBanditInvasion.IsInvasionActive)
					{
						if (isValidGround(point))
							return m_subsystemBanditInvasion.GetBanditProbability("Bandit16");
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnBanditWithMode(ct, point, "Bandit16");
				}
			});

			// ==========================================
			// CRIATURAS DE CUEVA Y AGUA
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("CaveSpider", SpawnLocationType.Cave, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (!ShittyCreaturesSettingsManager.SpiderSpawnEnabled)
						return 0f;
					int cellValue = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(cellValue);
					if (contents == 2 || contents == 3 || contents == 4 || contents == 66 || contents == 67 || contents == 7)
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return subsystemCreatureSpawn.SpawnCreatures(ct, "InfectedSpider", point, 1).Count;
				}
			});

			// ==========================================
			// CRIATURAS DE LA NOCHE VERDE
			// ==========================================
			string[] greenNightCreatures = new string[]
			{
				"InfectedBird", "InfectedNormal1", "InfectedNormal2",
				"InfectedFly1", "InfectedFreezer", "FrozenGhost",
				"InfectedFast1", "InfectedFast2", "InfectedMuscle1", "InfectedMuscle2",
				"PoisonousInfected1", "PoisonousInfected2", "InfectedFly2",
				"Boomer1", "InfectedHyena", "InfectedWolf",
				"Boomer2", "PredatoryChameleon", "InfectedWerewolf", "InfectedFly3",
				"Boomer3", "PoisonousGhost", "Charger1",
				"GhostFast", "Charger2",
				"GhostNormal", "GhostBoomer1", "InfectedWildboar",
				"GhostBoomer2", "InfectedBear",
				"GhostBoomer3", "GhostCharger"
			};

			foreach (string creatureName in greenNightCreatures)
			{
				bool isFlying = creatureName == "InfectedFly1" || creatureName == "InfectedFly2" || creatureName == "InfectedFly3" || creatureName == "InfectedBird";

				creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType(creatureName, SpawnLocationType.Surface, false, true)
				{
					SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
					{
						SubsystemGreenNightSky greenNight = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemGreenNightSky>(true);
						if (greenNight == null || !greenNight.IsGreenNightActive)
							return 0f;
						if (sky.SkyLightIntensity >= 0.1f)
							return 0f;

						if (isFlying)
						{
							int surfaceHeight = terrain.Terrain.GetTopHeight(point.X, point.Z);
							int airY = surfaceHeight + 20 + subsystemCreatureSpawn.m_random.Int(0, 30);
							if (airY >= 10 && airY <= 255)
								return 1.0f;
							return 0f;
						}

						if (point.Y <= 3 || point.Y >= 253)
							return 0f;

						int cellBelow = terrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
						int cellCurrent = terrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
						int cellAbove = terrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);

						Block blockBelow = BlocksManager.Blocks[Terrain.ExtractContents(cellBelow)];
						Block blockCurrent = BlocksManager.Blocks[Terrain.ExtractContents(cellCurrent)];
						Block blockAbove = BlocksManager.Blocks[Terrain.ExtractContents(cellAbove)];

						bool belowSolid = (blockBelow.IsCollidable_(cellBelow) || blockBelow is WaterBlock);
						bool currentEmpty = (!blockCurrent.IsCollidable_(cellCurrent) && !(blockCurrent is WaterBlock));
						bool aboveEmpty = (!blockAbove.IsCollidable_(cellAbove) && !(blockAbove is WaterBlock));

						if (!belowSolid || !currentEmpty || !aboveEmpty)
							return 0f;

						int belowContents = Terrain.ExtractContents(cellBelow);
						if (belowContents != 2 && belowContents != 3 && belowContents != 7 && belowContents != 8)
							return 0f;

						int groundHeight = terrain.Terrain.GetTopHeight(point.X, point.Z);
						if (point.Y > groundHeight + 2)
							return 0f;

						return 1.0f;
					},
					SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
					{
						return subsystemCreatureSpawn.SpawnCreatures(ct, creatureName, point, 1).Count;
					}
				});
			}
		}
	}
}
