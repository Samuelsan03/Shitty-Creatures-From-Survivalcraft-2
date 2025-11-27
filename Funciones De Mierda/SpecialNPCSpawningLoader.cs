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
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)) // Usar IDs numéricos por seguridad
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

					// Condiciones flexibles para Naomi
					if (point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)) // Usar IDs numéricos por seguridad
					{
						return 1.0f; // 100% de probabilidad
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

					// Verificar si es de noche usando TimeOfDay
					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isNight = timeOfDay != null && (timeOfDay.TimeOfDay >= timeOfDay.NightStart || timeOfDay.TimeOfDay < timeOfDay.DawnStart);

					// Condiciones para LaMuerteX - solo de noche
					if (isNight && point.Y < 90 && point.Y > 50 &&
						(groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8))
					{
						return 1.0f; // 100% de probabilidad solo de noche
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
				{
					var creatures = spawn.SpawnCreatures(creatureType, "LaMuerteX", point, 3);
					return creatures.Count;
				})
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("ElBallestador", 0, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					SubsystemTerrain subsystemTerrain = spawn.m_subsystemTerrain;
					int cellValue = subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValue);

					SubsystemTimeOfDay timeOfDay = spawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					bool isNight = timeOfDay != null && (timeOfDay.TimeOfDay >= timeOfDay.NightStart || timeOfDay.TimeOfDay < timeOfDay.DawnStart);

					// CONDICIÓN MÁS SEGURA - probar diferentes rangos
					bool isDay5Range = timeOfDay != null &&
									  (timeOfDay.Day >= 4.5 && timeOfDay.Day < 5.5) || // Rango amplio
									  (timeOfDay.Day >= 4.0 && timeOfDay.Day < 6.0);   // Rango más amplio aún

					if (isDay5Range && isNight && point.Y < 90 && point.Y > 50 &&
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
			// Aquí puedes agregar más NPCs en el futuro
		}
	}
}
