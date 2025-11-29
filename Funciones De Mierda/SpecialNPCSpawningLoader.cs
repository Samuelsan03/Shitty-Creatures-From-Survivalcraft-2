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
					var creatures = spawn.SpawnCreatures(creatureType, "LaMuerteX", point, 5);
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
					var creatures = spawn.SpawnCreatures(creatureType, "ElBallestador", point, 5);
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
					var creatures = spawn.SpawnCreatures(creatureType, "ElArquero", point, 4);
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
					var creatures = spawn.SpawnCreatures(creatureType, "Agumon", point, 2);
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
					var creatures = spawn.SpawnCreatures(creatureType, "Veemon", point, 2);
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
					var creatures = spawn.SpawnCreatures(creatureType, "Gaomon", point, 2);
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
					var creatures = spawn.SpawnCreatures(creatureType, "Shoutmon", point, 2);
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
					var creatures = spawn.SpawnCreatures(creatureType, "Impmon", point, 2);
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
					var creatures = spawn.SpawnCreatures(creatureType, "Guilmon", point, 2);
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
					var creatures = spawn.SpawnCreatures(creatureType, "Gumdramon", point, 2);
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
					var creatures = spawn.SpawnCreatures(creatureType, "Betelgammamon", point, 2);
					return creatures.Count;
				})
			});

			// Para Paco - Día 2 en adelante
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Paco", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemGameInfo gameInfo = spawn.Project.FindSubsystem<SubsystemGameInfo>(true);
					bool isDay2OrLater = gameInfo != null && (gameInfo.TotalElapsedGameTime / 1200.0) >= 1.0;

					if (isDay2OrLater && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Paco", point, 3);
					return creatures.Count;
				})
			});

			// Para ElSenorDeLasTumbasMoradas - Día 2+ y noche
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElSenorDeLasTumbasMoradas", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					SubsystemGameInfo gameInfo = spawn.Project.FindSubsystem<SubsystemGameInfo>(true);
					bool isNight = timeOfDay != null && (timeOfDay.TimeOfDay >= timeOfDay.NightStart || timeOfDay.TimeOfDay < timeOfDay.DawnStart);
					bool isDay2OrLater = gameInfo != null && (gameInfo.TotalElapsedGameTime / 1200.0) >= 1.0;

					if (isDay2OrLater && isNight && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ElSenorDeLasTumbasMoradas", point, 3);
					return creatures.Count;
				})
			});

			// LiderCalavericoSupremo - Aparece desde la noche del día 7 en adelante
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("LiderCalavericoSupremo", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isNight = timeOfDay != null && (timeOfDay.TimeOfDay >= timeOfDay.NightStart || timeOfDay.TimeOfDay < timeOfDay.DawnStart);
					bool isDay7OrLater = timeOfDay != null && timeOfDay.Day >= 6.0; // Día 7 en adelante

					if (isDay7OrLater && isNight && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad desde noche del día 7
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "LiderCalavericoSupremo", point, 2);
					return creatures.Count;
				})
			});

			// Para Barack - Día 4 en adelante (igual condiciones que Naomi)
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Barack", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay4OrLater = timeOfDay != null && timeOfDay.Day >= 3.0;

					// Condiciones flexibles igual que Naomi - cualquier hora, cualquier lugar
					if (isDay4OrLater && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Barack", point, 3);
					return creatures.Count;
				})
			});

			// Para FumadorQuimico - Día 6 en adelante, solo atardecer y amanecer
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("FumadorQuimico", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay6OrLater = timeOfDay != null && timeOfDay.Day >= 5.0;

					// Solo atardecer (Dusk) y amanecer (Dawn)
					bool isDusk = timeOfDay != null && timeOfDay.TimeOfDay >= timeOfDay.DuskStart && timeOfDay.TimeOfDay < timeOfDay.NightStart;
					bool isDawn = timeOfDay != null && timeOfDay.TimeOfDay >= timeOfDay.DawnStart && timeOfDay.TimeOfDay < timeOfDay.DayStart;
					bool isTwilight = isDusk || isDawn;

					if (isDay6OrLater && isTwilight && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "FumadorQuimico", point, 2);
					return creatures.Count;
				})
			});

			// Para ElMarihuanero - Día 8 en adelante, solo de día
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElMarihuanero", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay8OrLater = timeOfDay != null && timeOfDay.Day >= 7.0;

					// Solo durante el día (excluyendo amanecer y atardecer)
					bool isDay = timeOfDay != null && timeOfDay.TimeOfDay >= timeOfDay.DayStart && timeOfDay.TimeOfDay < timeOfDay.DuskStart;

					if (isDay8OrLater && isDay && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ElMarihuanero", point, 2);
					return creatures.Count;
				})
			});

			// Para ElMarihuaneroMamon - Día 9 en adelante, solo de noche y en grupo
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElMarihuaneroMamon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay9OrLater = timeOfDay != null && timeOfDay.Day >= 8.0;

					// Solo durante la noche
					bool isNight = timeOfDay != null && (timeOfDay.TimeOfDay >= timeOfDay.NightStart || timeOfDay.TimeOfDay < timeOfDay.DawnStart);

					if (isDay9OrLater && isNight && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ElMarihuaneroMamon", point, 3);
					return creatures.Count;
				})
			});

			// Para ClaudeSpeed - Día 13 en adelante, cualquier hora
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ClaudeSpeed", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay13OrLater = timeOfDay != null && timeOfDay.Day >= 12.0;

					// Cualquier hora del día
					if (isDay13OrLater && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ClaudeSpeed", point, 2);
					return creatures.Count;
				})
			});

			// Para TommyVercetti - Día 13 en adelante, cualquier hora
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("TommyVercetti", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay13OrLater = timeOfDay != null && timeOfDay.Day >= 12.0;

					// Cualquier hora del día
					if (isDay13OrLater && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "TommyVercetti", point, 2);
					return creatures.Count;
				})
			});

			// Para Conker - Día 18 en adelante, solo de día
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Conker", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay18OrLater = timeOfDay != null && timeOfDay.Day >= 17.0;

					// Solo durante el día (excluyendo amanecer y atardecer)
					bool isDay = timeOfDay != null && timeOfDay.TimeOfDay >= timeOfDay.DayStart && timeOfDay.TimeOfDay < timeOfDay.DuskStart;

					if (isDay18OrLater && isDay && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Conker", point, 2);
					return creatures.Count;
				})
			});

			// Para Butt-Head - Día 14 en adelante, solo de día
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Butt-Head", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay14OrLater = timeOfDay != null && timeOfDay.Day >= 13.0;

					// Solo durante el día (excluyendo amanecer y atardecer)
					bool isDay = timeOfDay != null && timeOfDay.TimeOfDay >= timeOfDay.DayStart && timeOfDay.TimeOfDay < timeOfDay.DuskStart;

					if (isDay14OrLater && isDay && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Butt-Head", point, 2);
					return creatures.Count;
				})
			});

			// Para Beavis - Día 14 en adelante, solo de día
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Beavis", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay14OrLater = timeOfDay != null && timeOfDay.Day >= 13.0;

					// Solo durante el día (excluyendo amanecer y atardecer)
					bool isDay = timeOfDay != null && timeOfDay.TimeOfDay >= timeOfDay.DayStart && timeOfDay.TimeOfDay < timeOfDay.DuskStart;

					if (isDay14OrLater && isDay && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Beavis", point, 2);
					return creatures.Count;
				})
			});

			// Para HombreAgua - Día 21 en adelante, solo de noche y en agua con 85% de probabilidad
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("HombreAgua", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;

					// Verificar que el bloque en la posición sea agua (bloque 18) o que esté cerca de agua
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAtPoint = Terrain.ExtractContents(cellValue);

					// Verificar bloques alrededor para determinar tipo de zona acuática
					bool isNearWater = false;
					bool isCoast = false;
					bool isDeepOcean = false;

					int waterBlocksCount = 0;
					int landBlocksCount = 0;

					for (int x = -5; x <= 5; x++)
					{
						for (int z = -5; z <= 5; z++)
						{
							int nearbyCellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X + x, point.Y, point.Z + z);
							int nearbyBlock = Terrain.ExtractContents(nearbyCellValue);

							if (nearbyBlock == 18) // Bloque de agua
							{
								waterBlocksCount++;
								isNearWater = true;
							}
							else if (nearbyBlock == 2 || nearbyBlock == 3 || nearbyBlock == 7 || nearbyBlock == 8) // Bloques de tierra
							{
								landBlocksCount++;
							}
						}
					}

					// Determinar si es costa (mezcla de agua y tierra) - no océano profundo
					isCoast = isNearWater && landBlocksCount > 5 && waterBlocksCount > 5;
					isDeepOcean = isNearWater && landBlocksCount <= 2; // Muy poca tierra = océano profundo

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay21OrLater = timeOfDay != null && timeOfDay.Day >= 20.0;

					// Solo durante la noche
					bool isNight = timeOfDay != null && (timeOfDay.TimeOfDay >= timeOfDay.NightStart || timeOfDay.TimeOfDay < timeOfDay.DawnStart);

					// HombreAgua aparece en zonas acuáticas, preferencia por costas
					if (isDay21OrLater && isNight && point.Y < 90 && point.Y > 50 && isNearWater && !isDeepOcean)
					{
						// 85% de probabilidad base, mayor en costas
						if (isCoast)
						{
							return 0.95f; // 95% en costas
						}
						else
						{
							return 0.85f; // 85% en otras zonas acuáticas
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "HombreAgua", point, 2);
					return creatures.Count;
				})
			});

			// Para BallestadoraMusculosa - Día 25 en adelante, solo de noche y en grupo de 4
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("BallestadoraMusculosa", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay25OrLater = timeOfDay != null && timeOfDay.Day >= 24.0;

					// Solo durante la noche
					bool isNight = timeOfDay != null && (timeOfDay.TimeOfDay >= timeOfDay.NightStart || timeOfDay.TimeOfDay < timeOfDay.DawnStart);

					if (isDay25OrLater && isNight && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "BallestadoraMusculosa", point, 4);
					return creatures.Count;
				})
			});

			// Para ArqueroPrisionero - Día 26 en adelante, cualquier hora
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ArqueroPrisionero", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay26OrLater = timeOfDay != null && timeOfDay.Day >= 25.0;

					// Cualquier hora del día
					if (isDay26OrLater && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ArqueroPrisionero", point, 3);
					return creatures.Count;
				})
			});

			// Para AladinaCorrupta - Día 29 en adelante, cualquier hora
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("AladinaCorrupta", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay29OrLater = timeOfDay != null && timeOfDay.Day >= 28.0;

					// Cualquier hora del día
					if (isDay29OrLater && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "AladinaCorrupta", point, 3);
					return creatures.Count;
				})
			});

			// Para AladinaCorrupta - Día 29 en adelante, cualquier hora
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("AladinaCorrupta", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay29OrLater = timeOfDay != null && timeOfDay.Day >= 28.0;

					// Cualquier hora del día
					if (isDay29OrLater && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "AladinaCorrupta", point, 2);
					return creatures.Count;
				})
			});

			// Para WalterZombie - Día 17 en adelante, solo de noche
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("WalterZombie", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay17OrLater = timeOfDay != null && timeOfDay.Day >= 16.0;

					// Solo durante la noche
					bool isNight = timeOfDay != null && (timeOfDay.TimeOfDay >= timeOfDay.NightStart || timeOfDay.TimeOfDay < timeOfDay.DawnStart);

					if (isDay17OrLater && isNight && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "WalterZombie", point, 2);
					return creatures.Count;
				})
			});

			// Para CarlJohnson - Día 22 en adelante, solo de noche, cualquier altura
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("CarlJohnson", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isDay22OrLater = timeOfDay != null && timeOfDay.Day >= 21.0;

					// Solo durante la noche
					bool isNight = timeOfDay != null && (timeOfDay.TimeOfDay >= timeOfDay.NightStart || timeOfDay.TimeOfDay < timeOfDay.DawnStart);

					// Sin restricción de altura - aparece en cualquier Y
					if (isDay22OrLater && isNight &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "CarlJohnson", point, 2);
					return creatures.Count;
				})
			});
		}
	}
}
