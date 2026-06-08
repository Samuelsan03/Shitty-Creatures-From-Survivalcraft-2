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
		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("InitializeCreatureTypes", this);
		}

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
			// 6. FANGTHE SNIPER (desiertos con cactus cerca)
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("FangTheSniper", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;

					int temperature = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
					int humidity = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetHumidity(point.X, point.Z);
					if (temperature < 10 || humidity > 4) return 0f;

					int blockUnder = Terrain.ExtractContents(subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (blockUnder != SandBlock.Index) return 0f;

					float shoreDistance = subsystemCreatureSpawn.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
					if (shoreDistance < 20f) return 0f;

					// Buscar cactus en un radio de 3 bloques
					bool hasCactusNearby = false;
					for (int dx = -3; dx <= 3; dx++)
					{
						for (int dz = -3; dz <= 3; dz++)
						{
							int cactusCell = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellContents(point.X + dx, point.Y, point.Z + dz);
							if (cactusCell == CactusBlock.Index)
							{
								hasCactusNearby = true;
								break;
							}
							int cactusBelow = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellContents(point.X + dx, point.Y - 1, point.Z + dz);
							if (cactusBelow == CactusBlock.Index)
							{
								hasCactusNearby = true;
								break;
							}
						}
						if (hasCactusNearby) break;
					}

					if (!hasCactusNearby) return 0f;

					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					if (point.Y < topHeight - 2) return 0f;

					return 5f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					int topHeight = subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);
					Point3 correctedPoint = new Point3(point.X, topHeight, point.Z);
					return subsystemCreatureSpawn.SpawnCreatures(ct, "FangTheSniper", correctedPoint, 1).Count;
				}
			});

			// ==========================================
			// 7. SONIC THE HEDGEHOG (solo en Grass, Dirt, Sand, Gravel - solo primavera y verano)
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("SonicTheHedgehog", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					// Solo de día
					if (sky.SkyLightIntensity < 0.4f) return 0f;

					// Obtener la estación actual
					Season currentSeason = subsystemCreatureSpawn.m_subsystemSeasons.Season;

					// Solo aparece en Primavera (Spring) o Verano (Summer)
					if (currentSeason != Season.Spring && currentSeason != Season.Summer)
						return 0f;

					// Verificar el bloque bajo la criatura usando BlocksManager
					int blockUnder = Terrain.ExtractContents(subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Solo aparece en Grass(8), Dirt(2), Sand(7) o Gravel(6)
					if (blockUnder != GrassBlock.Index &&
						blockUnder != DirtBlock.Index &&
						blockUnder != SandBlock.Index &&
						blockUnder != GravelBlock.Index)
						return 0f;

					// Asegurar que está en la superficie (top height)
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
		}
	}
}
