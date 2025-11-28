using System;
using System.Collections.Generic;
using Engine;
using Game;

namespace Game
{
	public class SpecialNPCSpawningLoader : ModLoader
	{
		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("InitializeCreatureTypes", this);
		}

		public override void InitializeCreatureTypes(SubsystemCreatureSpawn spawn, List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
		{
			// Spawn para Naomi con 100% de probabilidad
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Naomi", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Condiciones flexibles para Naomi
					if (point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Naomi", point, 3);
					return creatures.Count;
				})
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Ricardo", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					if (point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Ricardo", point, 2);
					return creatures.Count;
				})
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("LaMuerteX", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isNight = timeOfDay != null && (timeOfDay.TimeOfDay >= timeOfDay.NightStart || timeOfDay.TimeOfDay < timeOfDay.DawnStart);

					if (isNight && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "LaMuerteX", point, 3);
					return creatures.Count;
				})
			});

			// EL BALLESTADOR - aparece CADA NOCHE desde el día 5 en adelante
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElBallestador", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isNight = timeOfDay != null && (timeOfDay.TimeOfDay >= timeOfDay.NightStart || timeOfDay.TimeOfDay < timeOfDay.DawnStart);
					bool isDay5OrLater = timeOfDay != null && timeOfDay.Day >= 4.0;

					if (isDay5OrLater && isNight && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ElBallestador", point, 2);
					return creatures.Count;
				})
			});

			// EL ARQUERO - aparece CADA NOCHE desde el día 10 en adelante  
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElArquero", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isNight = timeOfDay != null && (timeOfDay.TimeOfDay >= timeOfDay.NightStart || timeOfDay.TimeOfDay < timeOfDay.DawnStart);
					bool isDay10OrLater = timeOfDay != null && timeOfDay.Day >= 9.0;

					if (isDay10OrLater && isNight && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ElArquero", point, 3);
					return creatures.Count;
				})
			});

			// AGUMON - Día 3+ (solo horario central del día)
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Agumon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isProperDaytime = timeOfDay != null && timeOfDay.TimeOfDay >= timeOfDay.DayStart + 0.1f && timeOfDay.TimeOfDay < timeOfDay.DuskStart - 0.1f;
					bool isDay3OrLater = timeOfDay != null && timeOfDay.Day >= 2.0;

					if (isDay3OrLater && isProperDaytime && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Agumon", point, 1);
					return creatures.Count;
				})
			});

			// VEEMON - Día 3+ (solo horario central del día)
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Veemon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isProperDaytime = timeOfDay != null && timeOfDay.TimeOfDay >= timeOfDay.DayStart + 0.1f && timeOfDay.TimeOfDay < timeOfDay.DuskStart - 0.1f;
					bool isDay3OrLater = timeOfDay != null && timeOfDay.Day >= 2.0;

					if (isDay3OrLater && isProperDaytime && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Veemon", point, 1);
					return creatures.Count;
				})
			});

			// GAOMON - Día 3+ (solo horario central del día)
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Gaomon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isProperDaytime = timeOfDay != null && timeOfDay.TimeOfDay >= timeOfDay.DayStart + 0.1f && timeOfDay.TimeOfDay < timeOfDay.DuskStart - 0.1f;
					bool isDay3OrLater = timeOfDay != null && timeOfDay.Day >= 2.0;

					if (isDay3OrLater && isProperDaytime && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Gaomon", point, 1);
					return creatures.Count;
				})
			});

			// SHOUTMON - Día 5+ (de día, 100%)
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Shoutmon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay = timeOfDay != null && timeOfDay.TimeOfDay >= timeOfDay.DawnStart && timeOfDay.TimeOfDay < timeOfDay.DuskStart;
					bool isDay5OrLater = timeOfDay != null && timeOfDay.Day >= 4.0;

					if (isDay5OrLater && isDay && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Shoutmon", point, 1);
					return creatures.Count;
				})
			});

			// IMPMON - Día 5+ (solo de noche, 100%)
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Impmon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isNight = timeOfDay != null && (timeOfDay.TimeOfDay >= timeOfDay.NightStart || timeOfDay.TimeOfDay < timeOfDay.DawnStart);
					bool isDay5OrLater = timeOfDay != null && timeOfDay.Day >= 4.0;

					if (isDay5OrLater && isNight && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Impmon", point, 1);
					return creatures.Count;
				})
			});

			// GUILMON - Día 12+ (solo horario central del día)
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Guilmon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isProperDaytime = timeOfDay != null && timeOfDay.TimeOfDay >= timeOfDay.DayStart + 0.1f && timeOfDay.TimeOfDay < timeOfDay.DuskStart - 0.1f;
					bool isDay12OrLater = timeOfDay != null && timeOfDay.Day >= 11.0;

					if (isDay12OrLater && isProperDaytime && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Guilmon", point, 1);
					return creatures.Count;
				})
			});

			// GUMDRAMON - Día 12+ (solo horario central del día)
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Gumdramon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isProperDaytime = timeOfDay != null && timeOfDay.TimeOfDay >= timeOfDay.DayStart + 0.1f && timeOfDay.TimeOfDay < timeOfDay.DuskStart - 0.1f;
					bool isDay12OrLater = timeOfDay != null && timeOfDay.Day >= 11.0;

					if (isDay12OrLater && isProperDaytime && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Gumdramon", point, 1);
					return creatures.Count;
				})
			});

			// BETELGAMMAMON - Día 15+ + INVIERNO + BIOMAS FRÍOS (ultra legendario)
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Betelgammamon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay = timeOfDay != null && timeOfDay.TimeOfDay >= timeOfDay.DawnStart && timeOfDay.TimeOfDay < timeOfDay.DuskStart;
					bool isDay15OrLater = timeOfDay != null && timeOfDay.Day >= 14.0;

					SubsystemSeasons seasons = spawn.Project.FindSubsystem<SubsystemSeasons>(true);
					bool isWinter = seasons != null && seasons.Season == Season.Winter;
					bool isColdBiome = groundBlock == 62 || groundBlock == 63 || groundBlock == 2;

					if (isDay15OrLater && isDay && isWinter && isColdBiome && point.Y < 90 && point.Y > 50)
					{
						return 0.1f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Betelgammamon", point, 1);
					return creatures.Count;
				})
			});
		}
	}
}
