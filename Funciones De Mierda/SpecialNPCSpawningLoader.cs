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
			//Tank y sus variantes
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Tank3", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 55 en adelante, CUALQUIER HORA
					bool isDay55OrLater = currentDay >= 55;

					if (isDay55OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 55
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Tank3", point, 1);
					return creatures.Count;
				})
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Tank2", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 50 en adelante, CUALQUIER HORA
					bool isDay50OrLater = currentDay >= 50;

					if (isDay50OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 50
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Tank2", point, 1);
					return creatures.Count;
				})
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Tank1", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 41 en adelante, CUALQUIER HORA
					bool isDay41OrLater = currentDay >= 41;

					if (isDay41OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 31
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Tank1", point, 1);
					return creatures.Count;
				})
			});

			// Charger y sus variantes
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Boomer2", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 31 en adelante, CUALQUIER HORA
					bool isDay20OrLater = currentDay >= 40;

					if (isDay20OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 20
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Charger", point, 1);
					return creatures.Count;
				})
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Charger1", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 19 en adelante, CUALQUIER HORA
					bool isDay19OrLater = currentDay >= 19;

					if (isDay19OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 19
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Charger1", point, 1);
					return creatures.Count;
				})
			});

			// Boomer y sus variantes
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Boomer3", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 43 en adelante, CUALQUIER HORA
					bool isDay43OrLater = currentDay >= 43;

					if (isDay43OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 31
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Boomer3", point, 1);
					return creatures.Count;
				})
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Boomer2", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 41 en adelante, CUALQUIER HORA
					bool isDay41OrLater = currentDay >= 41;

					if (isDay41OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 31
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Boomer2", point, 1);
					return creatures.Count;
				})
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Boomer1", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 40 en adelante, CUALQUIER HORA
					bool isDay40OrLater = currentDay >= 40;

					if (isDay40OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 31
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Boomer1", point, 1);
					return creatures.Count;
				})
			});

			// Infectado Volador y sus variantes
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("FlyingInfectedBoss", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 31 en adelante, CUALQUIER HORA
					bool isDay31OrLater = currentDay >= 31;

					if (isDay31OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 31
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "FlyingInfectedBoss", point, 1);
					return creatures.Count;
				})
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("InfectedFly3", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 26 en adelante, CUALQUIER HORA
					bool isDay26OrLater = currentDay >= 26;

					if (isDay26OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 26
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "InfectedFly3", point, 2);
					return creatures.Count;
				})
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("InfectedFly2", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 25 en adelante, CUALQUIER HORA
					bool isDay25OrLater = currentDay >= 25;

					if (isDay25OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 25
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "InfectedFly2", point, 2);
					return creatures.Count;
				})
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("InfectedFly1", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 24 en adelante, CUALQUIER HORA
					bool isDay24OrLater = currentDay >= 24;

					if (isDay24OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 24
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "InfectedFly1", point, 2);
					return creatures.Count;
				})
			});

			// Infectado Musculoso 2 - Día 5+ (solo de noche, 100%), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("InfectedMuscle1", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es de noche (entre NightStart y DawnStart)
					bool isNight = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Es noche si está entre NightStart y DawnStart (considerando wrap-around)
						isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA NOCHE desde el día 5 en adelante
					bool isDay5OrLater = currentDay >= 5;

					if (isDay5OrLater && isNight && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada noche desde día 5
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "InfectedMuscle2", point, 2);
					return creatures.Count;
				})
			});

			// Infectado Musculoso 1 - Día 5+ (solo de noche, 100%), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("InfectedMuscle1", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es de noche (entre NightStart y DawnStart)
					bool isNight = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Es noche si está entre NightStart y DawnStart (considerando wrap-around)
						isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA NOCHE desde el día 5 en adelante
					bool isDay5OrLater = currentDay >= 5;

					if (isDay5OrLater && isNight && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada noche desde día 5
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "InfectedMuscle1", point, 2);
					return creatures.Count;
				})
			});

			// Infectado Veloz 2 - Día 3+ (solo de noche, 100%), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("InfectedFast2", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es de noche (entre NightStart y DawnStart)
					bool isNight = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Es noche si está entre NightStart y DawnStart (considerando wrap-around)
						isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA NOCHE desde el día 3 en adelante
					bool isDay3OrLater = currentDay >= 3;

					if (isDay3OrLater && isNight && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada noche desde día 2
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "InfectedFast1", point, 2);
					return creatures.Count;
				})
			});

			// Infectado Veloz 1 - Día 3+ (solo de noche, 100%), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("InfectedFast1", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es de noche (entre NightStart y DawnStart)
					bool isNight = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Es noche si está entre NightStart y DawnStart (considerando wrap-around)
						isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA NOCHE desde el día 3 en adelante
					bool isDay3OrLater = currentDay >= 3;

					if (isDay3OrLater && isNight && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada noche desde día 2
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "InfectedFast1", point, 2);
					return creatures.Count;
				})
			});

			// Spawn para Infectado Normal 1 con 100% de probabilidad - DESDE DÍA 0, cualquier estación y hora
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("InfectedNormal1", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 0.96f;
					}

					// Condiciones flexibles para Naomi - SIN RESTRICCIÓN DE ALTURA
					// SIN RESTRICCIÓN DE DÍAS, HORAS O ESTACIONES - aparece desde el inicio
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.99f; // 99% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "InfectedNormal1", point, 2);
					return creatures.Count;
				})
			});

			// Para Infectado Normal 2 - Desde el inicio, solo de noche, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("InfectedNormal2", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener hora del día
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es de noche (entre NightStart y DawnStart)
					bool isNight = false;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Es noche si está entre NightStart y DawnStart (considerando wrap-around)
						isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);
					}

					// SIN RESTRICCIÓN DE DÍAS - aparece desde el inicio cada noche
					// SIN RESTRICCIÓN DE ESTACIÓN - aparece en cualquier estación
					if (isNight && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "InfectedNormal2", point, 2);
					return creatures.Count;
				})
			});

			// Spawn para Naomi con 100% de probabilidad - DESDE DÍA 0, cualquier estación y hora
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Naomi", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Condiciones flexibles para Naomi - SIN RESTRICCIÓN DE ALTURA
					// SIN RESTRICCIÓN DE DÍAS, HORAS O ESTACIONES - aparece desde el inicio
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Naomi", point, 3);
					return creatures.Count;
				})
			});

			// Spawn para Brayan con 100% de probabilidad - DESDE DÍA 0, cualquier estación y hora
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Brayan", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Condiciones flexibles para Naomi - SIN RESTRICCIÓN DE ALTURA
					// SIN RESTRICCIÓN DE DÍAS, HORAS O ESTACIONES - aparece desde el inicio
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Brayan", point, 2);
					return creatures.Count;
				})
			});

			// Spawn para Tulio con 100% de probabilidad - DESDE DÍA 0, cualquier estación y hora
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Tulio", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Condiciones flexibles para Naomi - SIN RESTRICCIÓN DE ALTURA
					// SIN RESTRICCIÓN DE DÍAS, HORAS O ESTACIONES - aparece desde el inicio
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Tulio", point, 2);
					return creatures.Count;
				})
			});

			// Spawn para Ricardo con 100% de probabilidad - DESDE DÍA 0, cualquier estación y hora
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Ricardo", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Condiciones flexibles para Ricardo - SIN RESTRICCIÓN DE ALTURA
					// Aparece desde el día 0 (cuando se crea el mundo), cualquier hora y estación
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Ricardo", point, 3);
					return creatures.Count;
				})
			});

			// Spawn para Sparkster - DESDE DÍA 2, aparece cada día
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Sparkster", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Obtener el día actual
					SubsystemTimeOfDay subsystemTimeOfDay = spawn.m_subsystemGameInfo.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = (int)Math.Floor(subsystemTimeOfDay.Day);

					// Sparkster aparece SOLO desde el día 2 en adelante
					if (currentDay < 2)
					{
						return 0f; // No spawn antes del día 2
					}

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 1 || groundBlock == 7 ||
						groundBlock == 8 || groundBlock == 4 || groundBlock == 5 || groundBlock == 6)
					{
						// Verificar que no esté en agua
						int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
						int blockAbove = Terrain.ExtractContents(cellValueAbove);

						if (blockAbove == 18 || blockAbove == 92) // Agua o lava
						{
							return 0f;
						}

						// Sparkster aparecerá cada día desde el día 2
						// Probabilidad constante cada día
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Sparkster", point, 1);
					return creatures.Count;
				})
			});

			// Para LaMuerteX - Desde el inicio, solo de noche, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("LaMuerteX", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 999f;
					}

					// Obtener hora del día
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es de noche (entre NightStart y DawnStart)
					bool isNight = false;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Es noche si está entre NightStart y DawnStart (considerando wrap-around)
						isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);
					}

					// SIN RESTRICCIÓN DE DÍAS - aparece desde el inicio cada noche
					// SIN RESTRICCIÓN DE ESTACIÓN - aparece en cualquier estación
					if (isNight && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 999999f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "LaMuerteX", point, 5);
					return creatures.Count;
				})
			});

			// EL ARQUERO - aparece CADA NOCHE desde el día 5 en adelante, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElArquero", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es de noche
					bool isNight = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA NOCHE desde el día 5 en adelante
					// Días: 5, 6, 7, 8... (no solo día 5, sino todos los días siguientes)
					bool isDay5OrLater = currentDay >= 5;

					if (isDay5OrLater && isNight && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada noche desde día 5
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ElArquero", point, 5);
					return creatures.Count;
				})
			});

			// EL BALLESTADOR - aparece CADA NOCHE desde el día 10 en adelante, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElBallestador", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es de noche
					bool isNight = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA NOCHE desde el día 10 en adelante
					// Días: 10, 11, 12, 13... (no solo día 10, sino todos los días siguientes)
					bool isDay10OrLater = currentDay >= 10;

					if (isDay10OrLater && isNight && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada noche desde día 10
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ElBallestador", point, 5);
					return creatures.Count;
				})
			});

			// AGUMON - Día 3+ (solo horario central del día), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Agumon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es horario central del día (excluye amanecer/atardecer)
					bool isProperDaytime = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Horario central del día (excluyendo primeros y últimos 10% del día)
						isProperDaytime = (time >= timeOfDay.DayStart + 0.1f && time < timeOfDay.DuskStart - 0.1f);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA DÍA desde el día 3 en adelante, solo en horario central
					bool isDay3OrLater = currentDay >= 3;

					if (isDay3OrLater && isProperDaytime && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 3
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Agumon", point, 2);
					return creatures.Count;
				})
			});

			// VEEMON - Día 3+ (solo horario central del día), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Veemon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es horario central del día (excluye amanecer/atardecer)
					bool isProperDaytime = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Horario central del día (excluyendo primeros y últimos 10% del día)
						isProperDaytime = (time >= timeOfDay.DayStart + 0.1f && time < timeOfDay.DuskStart - 0.1f);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA DÍA desde el día 3 en adelante, solo en horario central
					bool isDay3OrLater = currentDay >= 3;

					if (isDay3OrLater && isProperDaytime && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 3
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Veemon", point, 2);
					return creatures.Count;
				})
			});

			// GAOMON - Día 3+ (solo horario central del día), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Gaomon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 100f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es horario central del día (excluye amanecer/atardecer)
					bool isProperDaytime = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Horario central del día (excluyendo primeros y últimos 10% del día)
						isProperDaytime = (time >= timeOfDay.DayStart + 0.1f && time < timeOfDay.DuskStart - 0.1f);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA DÍA desde el día 3 en adelante, solo en horario central
					bool isDay3OrLater = currentDay >= 3;

					if (isDay3OrLater && isProperDaytime && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 3
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Gaomon", point, 2);
					return creatures.Count;
				})
			});

			// SHOUTMON - Día 5+ (de día, 100%), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Shoutmon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es de día (entre DawnStart y DuskStart)
					bool isDay = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						isDay = (time >= timeOfDay.DawnStart && time < timeOfDay.DuskStart);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA DÍA desde el día 5 en adelante, solo de día
					bool isDay5OrLater = currentDay >= 5;

					if (isDay5OrLater && isDay && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 5
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Shoutmon", point, 2);
					return creatures.Count;
				})
			});

			// IMPMON - Día 5+ (solo de noche, 100%), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Impmon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es de noche (entre NightStart y DawnStart)
					bool isNight = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Es noche si está entre NightStart y DawnStart (considerando wrap-around)
						isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA NOCHE desde el día 5 en adelante
					bool isDay5OrLater = currentDay >= 5;

					if (isDay5OrLater && isNight && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada noche desde día 5
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Impmon", point, 2);
					return creatures.Count;
				})
			});

			// GUILMON - Día 12+ (solo horario central del día), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Guilmon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es horario central del día (excluye amanecer/atardecer)
					bool isProperDaytime = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Horario central del día (excluyendo primeros y últimos 10% del día)
						isProperDaytime = (time >= timeOfDay.DayStart + 0.1f && time < timeOfDay.DuskStart - 0.1f);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA DÍA desde el día 12 en adelante, solo en horario central
					bool isDay12OrLater = currentDay >= 12;

					if (isDay12OrLater && isProperDaytime && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 12
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Guilmon", point, 2);
					return creatures.Count;
				})
			});

			// GUMDRAMON - Día 12+ (solo horario central del día), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Gumdramon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es horario central del día (excluye amanecer/atardecer)
					bool isProperDaytime = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Horario central del día (excluyendo primeros y últimos 10% del día)
						isProperDaytime = (time >= timeOfDay.DayStart + 0.1f && time < timeOfDay.DuskStart - 0.1f);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA DÍA desde el día 12 en adelante, solo en horario central
					bool isDay12OrLater = currentDay >= 12;

					if (isDay12OrLater && isProperDaytime && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 12
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Gumdramon", point, 2);
					return creatures.Count;
				})
			});

			// BETELGAMMAMON - Aparece en Otoño o Invierno, CUALQUIER HORA del día
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("BetelGammamon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Obtener la estación actual del SubsystemSeasons
					SubsystemSeasons seasons = spawn.Project.FindSubsystem<SubsystemSeasons>(true);
					bool isAutumnOrWinter = false;

					if (seasons != null)
					{
						// Usar el método original del juego para determinar la estación
						Season currentSeason = seasons.Season;

						// Aparece en Otoño (Autumn = 1) o Invierno (Winter = 2)
						// según el enum del código original
						isAutumnOrWinter = (currentSeason == Season.Autumn || currentSeason == Season.Winter);
					}

					// Aparece en cualquier hora del día, solo durante Otoño o Invierno
					if (isAutumnOrWinter &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8 || groundBlock == 62))
					{
						return 100f; // 100% de probabilidad durante Otoño/Invierno
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "BetelGammamon", point, 2);
					return creatures.Count;
				})
			});

			// Para Paco - Día 2 en adelante, cualquier hora y estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Paco", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 2 en adelante, cualquier hora
					bool isDay2OrLater = currentDay >= 2;

					if (isDay2OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 2
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Paco", point, 3);
					return creatures.Count;
				})
			});

			// EL SEÑOR DE LAS TUMBAS MORADAS - aparece CADA NOCHE desde el día 2 en adelante, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElSenorDeLasTumbasMoradas", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay2OrLater = currentDay >= 2;

						// Solo aparece de noche
						float time = timeOfDay.TimeOfDay;
						bool isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);

						// CONDICIÓN: Día 2+ + Noche + Terreno válido
						if (isDay2OrLater && isNight &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada noche desde día 2
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ElSenorDeLasTumbasMoradas", point, 5);
					return creatures.Count;
				})
			});

			// LiderCalavericoSupremo - Aparece CADA NOCHE desde el día 7 en adelante, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("LiderCalavericoSupremo", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay7OrLater = currentDay >= 7;

						// Solo aparece de noche
						float time = timeOfDay.TimeOfDay;
						bool isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);

						if (isDay7OrLater && isNight &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada noche desde día 7
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "LiderCalavericoSupremo", point, 2);
					return creatures.Count;
				})
			});

			// Para Barack - Día 4 en adelante, cualquier hora y estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Barack", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 4 en adelante, cualquier hora
					bool isDay4OrLater = currentDay >= 4;

					// Condiciones flexibles igual que Naomi - cualquier hora, cualquier estación
					if (isDay4OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 4
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Barack", point, 3);
					return creatures.Count;
				})
			});

			// Para FumadorQuimico - Día 6 en adelante, solo de noche, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("FumadorQuimico", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay6OrLater = currentDay >= 6;

						// Solo durante la noche
						float time = timeOfDay.TimeOfDay;
						bool isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);

						if (isDay6OrLater && isNight &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada noche desde día 6
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "FumadorQuimico", point, 2);
					return creatures.Count;
				})
			});

			// Para ElMarihuanero - Día 8 en adelante, solo de día, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElMarihuanero", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay8OrLater = currentDay >= 8;

						// Solo durante el día
						float time = timeOfDay.TimeOfDay;
						bool isDay = (time >= timeOfDay.DawnStart && time < timeOfDay.DuskStart);

						if (isDay8OrLater && isDay &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada día desde día 8
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ElMarihuanero", point, 2);
					return creatures.Count;
				})
			});

			// Para ElMarihuaneroMamon - Día 9 en adelante, solo de noche, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElMarihuaneroMamon", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay9OrLater = currentDay >= 9;

						// Solo durante la noche
						float time = timeOfDay.TimeOfDay;
						bool isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);

						if (isDay9OrLater && isNight &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada noche desde día 9
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ElMarihuaneroMamon", point, 3);
					return creatures.Count;
				})
			});

			// Para ClaudeSpeed - Día 13 en adelante, cualquier hora, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ClaudeSpeed", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 13 en adelante, cualquier hora
					bool isDay13OrLater = currentDay >= 13;

					// Cualquier hora del día, cualquier estación
					if (isDay13OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 13
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ClaudeSpeed", point, 2);
					return creatures.Count;
				})
			});

			// Para TommyVercetti - Día 13 en adelante, cualquier hora, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("TommyVercetti", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 13 en adelante, cualquier hora
					bool isDay13OrLater = currentDay >= 13;

					// Cualquier hora del día, cualquier estación
					if (isDay13OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 13
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "TommyVercetti", point, 2);
					return creatures.Count;
				})
			});

			// Para Conker - Día 18 en adelante, solo de día, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Conker", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay18OrLater = currentDay >= 18;

						// Solo durante el día
						float time = timeOfDay.TimeOfDay;
						bool isDay = (time >= timeOfDay.DawnStart && time < timeOfDay.DuskStart);

						if (isDay18OrLater && isDay &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada día desde día 18
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Conker", point, 2);
					return creatures.Count;
				})
			});

			// Para Butt-Head - Día 14 en adelante, solo de día, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Butt-Head", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay14OrLater = currentDay >= 14;

						// Solo durante el día
						float time = timeOfDay.TimeOfDay;
						bool isDay = (time >= timeOfDay.DawnStart && time < timeOfDay.DuskStart);

						if (isDay14OrLater && isDay &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada día desde día 14
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Butt-Head", point, 2);
					return creatures.Count;
				})
			});

			// Para Beavis - Día 14 en adelante, solo de día, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Beavis", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay14OrLater = currentDay >= 14;

						// Solo durante el día
						float time = timeOfDay.TimeOfDay;
						bool isDay = (time >= timeOfDay.DawnStart && time < timeOfDay.DuskStart);

						if (isDay14OrLater && isDay &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada día desde día 14
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Beavis", point, 2);
					return creatures.Count;
				})
			});

			// Para BallestadoraMusculosa - Día 25 en adelante, solo de noche, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("BallestadoraMusculosa", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay25OrLater = currentDay >= 25;

						// Solo durante la noche
						float time = timeOfDay.TimeOfDay;
						bool isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);

						if (isDay25OrLater && isNight &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada noche desde día 25
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "BallestadoraMusculosa", point, 4);
					return creatures.Count;
				})
			});

			// Para ArqueroPrisionero - Día 26 en adelante, cualquier hora, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ArqueroPrisionero", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 26 en adelante, cualquier hora
					bool isDay26OrLater = currentDay >= 26;

					// Cualquier hora del día, cualquier estación
					if (isDay26OrLater && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 100f; // 100% de probabilidad cada día desde día 26
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ArqueroPrisionero", point, 3);
					return creatures.Count;
				})
			});

			// Para AladinaCorrupta - Día 29 en adelante, SOLO DE NOCHE
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("AladinaCorrupta", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay29OrLater = currentDay >= 29;

						// SOLO DE NOCHE
						float time = timeOfDay.TimeOfDay;
						bool isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);

						// Aparece CADA NOCHE desde el día 29 en adelante
						if (isDay29OrLater && isNight &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada noche desde día 29
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "AladinaCorrupta", point, 2);
					return creatures.Count;
				})
			});

			// Para HombreLava - Día 29 en adelante, cualquier hora, cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("HombreLava", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua (aunque es hombre lava, evita agua)
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 92) // Lava
					{
						return 100f;
					}

					// Obtener día actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					// Aparece CADA DÍA desde el día 29 en adelante, cualquier hora
					bool isDay29OrLater = currentDay >= 29;

					// Aparece SOLO en bloques rocosos: grava (6), cobblestone (5), granite (3), sandstone (4), limestone (66)
					// Cualquier hora del día - SIN RESTRICCIÓN DE ALTURA
					if (isDay29OrLater &&
						(groundBlock == 6 || groundBlock == 5 || groundBlock == 3 || groundBlock == 4 || groundBlock == 66))
					{
						return 100f; // 100% de probabilidad cada día desde día 29
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "HombreLava", point, 2);
					return creatures.Count;
				})
			});

			// Para HombreAgua - Día 21 en adelante, solo de noche y en TIERRA/ARENA cerca de agua
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("HombreAgua", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;

					// Verificar el bloque DEBAJO (donde estaría parado)
					int cellValueBelow = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueBelow);

					// Solo puede aparecer en bloques de tierra y arena
					bool isValidGround = groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8;

					// Verificar que no esté en lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua
					{
						return 100f;
					}

					// Verificar bloques alrededor para detectar agua cercana
					bool isNearWater = false;
					int waterBlocksCount = 0;

					for (int x = -3; x <= 3; x++)
					{
						for (int z = -3; z <= 3; z++)
						{
							int nearbyCellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X + x, point.Y, point.Z + z);
							int nearbyBlock = Terrain.ExtractContents(nearbyCellValue);

							if (nearbyBlock == 18) // Bloque de agua
							{
								waterBlocksCount++;
								isNearWater = true;
							}
						}
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay21OrLater = currentDay >= 21;

						// Solo durante la noche
						float time = timeOfDay.TimeOfDay;
						bool isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);

						// HombreAgua aparece en tierra/arena cerca de agua
						// Requiere al menos 2 bloques de agua cerca
						if (isDay21OrLater && isNight && isValidGround && isNearWater && waterBlocksCount >= 2)
						{
							return 100f; // 100% de probabilidad cada noche desde día 21
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

			// Para WalterZombie - Día 17 en adelante, solo de noche
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("WalterZombie", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay17OrLater = currentDay >= 17;

						// Solo durante la noche
						float time = timeOfDay.TimeOfDay;
						bool isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);

						// Altura restringida: entre 50 y 90
						bool isValidHeight = point.Y < 90 && point.Y > 50;

						if (isDay17OrLater && isNight && isValidHeight &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada noche desde día 17
						}
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

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay22OrLater = currentDay >= 22;

						// Solo durante la noche
						float time = timeOfDay.TimeOfDay;
						bool isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);

						// Sin restricción de altura - aparece en cualquier Y
						if (isDay22OrLater && isNight &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada noche desde día 22
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "CarlJohnson", point, 2);
					return creatures.Count;
				})
			});

			// EL GUERRILLERO - aparece CADA NOCHE desde el día 16 en adelante
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElGuerrillero", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay16OrLater = currentDay >= 16;

						// Solo durante la noche
						float time = timeOfDay.TimeOfDay;
						bool isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);

						if (isDay16OrLater && isNight &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada noche desde día 16
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					// Genera un número aleatorio entre 0 y 1
					float randomValue = spawn.m_random.Float(0f, 1f);
					int groupSize = (randomValue > 0.5f) ? 3 : 2; // 50% probabilidad de 3

					var creatures = spawn.SpawnCreatures(creatureType, "ElGuerrillero", point, groupSize);
					return creatures.Count;
				})
			});

			// EL GUERRILLERO TENEBROSO - aparece SOLO UNA NOCHE en el día 17
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElGuerrilleroTenebroso", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						// SOLO DÍA 17 (aparece solo una vez)
						bool isDay17 = currentDay == 17;

						// Solo durante la noche
						float time = timeOfDay.TimeOfDay;
						bool isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);

						if (isDay17 && isNight &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad solo en la noche del día 17
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ElGuerrilleroTenebroso", point, 1);
					return creatures.Count;
				})
			});

			// CamisasMorenas - Día 20 en adelante, solo de noche
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("CamisasMorenas", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay20OrLater = currentDay >= 20;

						// Solo durante la noche
						float time = timeOfDay.TimeOfDay;
						bool isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);

						// Aparece en cualquier noche DESPUÉS del día 20
						if (isDay20OrLater && isNight &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada noche desde día 20
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "CamisasMorenas", point, 2);
					return creatures.Count;
				})
			});

			// ZOMBIE REPETIDOR - aparece CADA NOCHE desde el día 13 en adelante
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ZombieRepetidor", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					if (timeOfDay != null)
					{
						// Día actual
						int currentDay = (int)Math.Floor(timeOfDay.Day);
						bool isDay13OrLater = currentDay >= 13;

						// Solo durante la noche
						float time = timeOfDay.TimeOfDay;
						bool isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);

						if (isDay13OrLater && isNight &&
							(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
						{
							return 100f; // 100% de probabilidad cada noche desde día 13
						}
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "ZombieRepetidor", point, 5);
					return creatures.Count;
				})
			});

			// Richard - Día 2 (solo de noche, 100%), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Richard", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18 || blockAbove == 92) // Agua o lava
					{
						return 0f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es de noche (entre NightStart y DawnStart)
					bool isNight = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Es noche si está entre NightStart y DawnStart (considerando wrap-around)
						isNight = (time >= timeOfDay.NightStart || time < timeOfDay.DawnStart);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA NOCHE desde el día 5 en adelante
					bool isDay2OrLater = currentDay >= 2;

					if (isDay2OrLater && isNight && (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 999f; // 100% de probabilidad cada noche desde día 5
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "Richard", point, 3);
					return creatures.Count;
				})
			});

			// Pirata Normal Aliado - Día 3+ (solo horario central del día), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("PirataNormalAliado", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua
					{
						return 100f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es horario central del día (excluye amanecer/atardecer)
					bool isProperDaytime = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Horario central del día (excluyendo primeros y últimos 10% del día)
						isProperDaytime = (time >= timeOfDay.DayStart + 0.1f && time < timeOfDay.DuskStart - 0.1f);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA DÍA desde el día 3 en adelante, solo en horario central
					bool isDay3OrLater = currentDay >= 3;

					if (isDay3OrLater && isProperDaytime && (groundBlock == 21))
					{
						return 100f; // 100% de probabilidad cada día desde día 3
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "PirataNormalAliado", point, 2);
					return creatures.Count;
				})
			});

			// Pirata Elite Aliado - Día 11+ (solo horario central del día), cualquier estación
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("PirataEliteAliado", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					// Verificar que no esté en agua o lava
					int cellValueAbove = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValueAbove);

					if (blockAbove == 18) // Agua
					{
						return 100f;
					}

					// Obtener día y hora actual
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);

					// Verificar si es horario central del día (excluye amanecer/atardecer)
					bool isProperDaytime = false;
					int currentDay = 0;
					if (timeOfDay != null)
					{
						float time = timeOfDay.TimeOfDay;
						// Horario central del día (excluyendo primeros y últimos 10% del día)
						isProperDaytime = (time >= timeOfDay.DayStart + 0.1f && time < timeOfDay.DuskStart - 0.1f);
						currentDay = (int)Math.Floor(timeOfDay.Day); // Día actual como entero
					}

					// Aparece CADA DÍA desde el día 3 en adelante, solo en horario central
					bool isDay11OrLater = currentDay >= 3;

					if (isDay11OrLater && isProperDaytime && (groundBlock == 21))
					{
						return 100f; // 100% de probabilidad cada día desde día 11
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "PirataEliteAliado", point, 2);
					return creatures.Count;
				})
			});
		}
	}
}
