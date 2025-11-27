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
					var creatures = spawn.SpawnCreatures(creatureType, "Naomi", point, 1);
					return creatures.Count;
				})
			});

			// Aquí puedes agregar más NPCs en el futuro
		}
	}
}
