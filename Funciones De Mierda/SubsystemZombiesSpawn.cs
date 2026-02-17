using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000046 RID: 70
	public class SubsystemZombiesSpawn : Subsystem, IUpdateable
	{
		// Token: 0x17000060 RID: 96
		// (get) Token: 0x0600024E RID: 590 RVA: 0x0001CF50 File Offset: 0x0001B150
		public Dictionary<ComponentCreature, bool>.KeyCollection Creatures
		{
			get
			{
				return this.m_creatures.Keys;
			}
		}

		// Token: 0x17000061 RID: 97
		// (get) Token: 0x0600024F RID: 591 RVA: 0x0001CF5D File Offset: 0x0001B15D
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x06000250 RID: 592 RVA: 0x0001CF60 File Offset: 0x0001B160
		public void Update(float dt)
		{
			// Actualizar estado de noche verde
			this.m_isGreenNightActive = (this.m_subsystemGreenNightSky != null && this.m_subsystemGreenNightSky.IsGreenNightActive);

			bool flag = this.m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode > EnvironmentBehaviorMode.Living;
			if (!flag)
			{
				bool flag2 = this.m_newSpawnChunks.Count > 0;
				if (flag2)
				{
					this.m_newSpawnChunks.RandomShuffle((int max) => this.m_random.Int(0, max - 1));
					int maxAttemptsNew = this.m_isGreenNightActive ? 20 : 10;  // ← MODIFICADO
					foreach (SpawnChunk chunk in this.m_newSpawnChunks)
					{
						this.SpawnChunkCreatures(chunk, maxAttemptsNew, false);
					}
					this.m_newSpawnChunks.Clear();
				}
				bool flag3 = this.m_spawnChunks.Count > 0;
				if (flag3)
				{
					this.m_spawnChunks.RandomShuffle((int max) => this.m_random.Int(0, max - 1));
					int maxAttemptsConst = this.m_isGreenNightActive ? 8 : 2;  // ← MODIFICADO
					foreach (SpawnChunk chunk2 in this.m_spawnChunks)
					{
						this.SpawnChunkCreatures(chunk2, maxAttemptsConst, true);
					}
					this.m_spawnChunks.Clear();
				}
				double period = this.m_isGreenNightActive ? 15.0 : 60.0;  // ← MODIFICADO
				bool flag4 = this.m_subsystemTime.PeriodicGameTimeEvent(period, 2.0);
				if (flag4)
				{
					this.SpawnRandomCreature();
				}
			}
		}

		// Token: 0x06000251 RID: 593 RVA: 0x0001D0C4 File Offset: 0x0001B2C4
		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemGreenNightSky = base.Project.FindSubsystem<SubsystemGreenNightSky>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemSpawn = base.Project.FindSubsystem<SubsystemSpawn>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemViews = base.Project.FindSubsystem<SubsystemGameWidgets>(true);
			this.InitializeCreatureTypes();
			SubsystemSpawn subsystemSpawn = this.m_subsystemSpawn;
			subsystemSpawn.SpawningChunk = (Action<SpawnChunk>)Delegate.Combine(subsystemSpawn.SpawningChunk, new Action<SpawnChunk>(delegate (SpawnChunk chunk)
			{
				this.m_spawnChunks.Add(chunk);
				bool flag = !chunk.IsSpawned;
				if (flag)
				{
					this.m_newSpawnChunks.Add(chunk);
				}
			}));
		}

		// Token: 0x06000252 RID: 594 RVA: 0x0001D184 File Offset: 0x0001B384
		public override void OnEntityAdded(Entity entity)
		{
			foreach (ComponentCreature key in entity.FindComponents<ComponentCreature>())
			{
				this.m_creatures.Add(key, true);
			}
		}

		// Token: 0x06000253 RID: 595 RVA: 0x0001D1E8 File Offset: 0x0001B3E8
		public override void OnEntityRemoved(Entity entity)
		{
			foreach (ComponentCreature key in entity.FindComponents<ComponentCreature>())
			{
				this.m_creatures.Remove(key);
			}
		}

		// Token: 0x06000254 RID: 596 RVA: 0x0001D24C File Offset: 0x0001B44C
		private void InitializeCreatureTypes()
		{
			// Boomer1 - Aparece desde el día 28, CUALQUIER HORA (día o noche)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Boomer1", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 28
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay28OrLater = currentDay >= 28;
					if (!isDay28OrLater)
						return 0f;

					// NO hay restricción de hora - aparece día y noche

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 70% de probabilidad desde día 28, cualquier hora
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Boomer1", point, 1).Count)
			});

			// Versión constante (spawn continuo) de Boomer1 - también desde día 28
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Boomer1 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 28
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay28OrLater = currentDay >= 28;
					if (!isDay28OrLater)
						return 0f;

					// NO hay restricción de hora

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.5f; // 30% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Boomer1", point, 1).Count)
			});

			// Boomer2 - Aparece desde el día 34, CUALQUIER HORA (día o noche)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Boomer2", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 34
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay34OrLater = currentDay >= 34;
					if (!isDay34OrLater)
						return 0f;

					// NO hay restricción de hora - aparece día y noche

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 75% de probabilidad desde día 34, cualquier hora
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Boomer2", point, 1).Count)
			});

			// Versión constante (spawn continuo) de Boomer2 - también desde día 34
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Boomer2 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 34
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay34OrLater = currentDay >= 34;
					if (!isDay34OrLater)
						return 0f;

					// NO hay restricción de hora

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.5f; // 35% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Boomer2", point, 1).Count)
			});

			// Boomer3 - Aparece desde el día 40, CUALQUIER HORA (día o noche)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Boomer3", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 40
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay40OrLater = currentDay >= 40;
					if (!isDay40OrLater)
						return 0f;

					// NO hay restricción de hora - aparece día y noche

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.8f; // 80% de probabilidad desde día 40, cualquier hora
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Boomer3", point, 1).Count)
			});

			// Versión constante (spawn continuo) de Boomer3 - también desde día 40
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Boomer3 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 40
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay40OrLater = currentDay >= 40;
					if (!isDay40OrLater)
						return 0f;

					// NO hay restricción de hora

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 40% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Boomer3", point, 1).Count)
			});

			// Charger1 - Aparece desde el día 29, solo de noche
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Charger1", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 29
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay29OrLater = currentDay >= 29;
					if (!isDay29OrLater)
						return 0f;

					// Condición de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.65f; // 65% de probabilidad cada noche desde día 29
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Charger1", point, 1).Count)
			});

			// Versión constante (spawn continuo) de Charger1 - también desde día 29
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Charger1 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 29
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay29OrLater = currentDay >= 29;
					if (!isDay29OrLater)
						return 0f;

					// Condición de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.5f; // 25% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Charger1", point, 1).Count)
			});

			// Charger2 - Aparece desde el día 30, solo de noche
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Charger2", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 30
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay30OrLater = currentDay >= 30;
					if (!isDay30OrLater)
						return 0f;

					// Condición de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 70% de probabilidad cada noche desde día 30
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Charger2", point, 1).Count)
			});

			// Versión constante (spawn continuo) de Charger2 - también desde día 30
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Charger2 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 30
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay30OrLater = currentDay >= 30;
					if (!isDay30OrLater)
						return 0f;

					// Condición de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.5f; // 30% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Charger2", point, 1).Count)
			});

			// InfectedFly1 - Aparece desde el día 9, CUALQUIER HORA (día o noche)
this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFly1", SpawnLocationType.Surface, false, false)
{
    SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
    {
        // Condición de día: solo a partir del día 9
        SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
        int currentDay = 0;
        if (timeOfDay != null)
        {
            currentDay = (int)Math.Floor(timeOfDay.Day);
        }

        bool isDay9OrLater = currentDay >= 9;
        if (!isDay9OrLater)
            return 0f;

        // NO hay restricción de hora - aparece día y noche

        // Verificar que no esté en agua o lava
        int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
        if (blockAbove == 18) // Agua o lava
        {
            return 0f;
        }

        // Verificar bloque debajo
        int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

        // Condiciones de terreno
        if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
        {
            return 1.0f; // 100% de probabilidad desde día 9, cualquier hora
        }
        return 0f;
    },
    SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
        this.SpawnCreatures(creatureType, "InfectedFly1", point, 1).Count) // Individual
});

// Versión constante (spawn continuo) de InfectedFly1 - también desde día 9
this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFly1 Constant", SpawnLocationType.Surface, false, true)
{
    SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
    {
        // Condición de día: solo a partir del día 9
        SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
        int currentDay = 0;
        if (timeOfDay != null)
        {
            currentDay = (int)Math.Floor(timeOfDay.Day);
        }

        bool isDay9OrLater = currentDay >= 9;
        if (!isDay9OrLater)
            return 0f;

        // NO hay restricción de hora

        // Verificar que no esté en agua o lava
        int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
        if (blockAbove == 18) // Agua o lava
        {
            return 0f;
        }

        // Verificar bloque debajo
        int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

        // Condiciones de terreno
        if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
        {
            return 0.5f; // 50% de probabilidad para spawn constante
        }
        return 0f;
    },
    SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
        this.SpawnCreatures(creatureType, "InfectedFly1", point, 1).Count) // Individual
});

// InfectedFly2 - Aparece desde el día 9, CUALQUIER HORA (día o noche)
this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFly2", SpawnLocationType.Surface, false, false)
{
    SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
    {
        // Condición de día: solo a partir del día 9
        SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
        int currentDay = 0;
        if (timeOfDay != null)
        {
            currentDay = (int)Math.Floor(timeOfDay.Day);
        }

        bool isDay9OrLater = currentDay >= 9;
        if (!isDay9OrLater)
            return 0f;

        // NO hay restricción de hora

        // Verificar que no esté en agua o lava
        int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
        if (blockAbove == 18) // Agua o lava
        {
            return 0f;
        }

        // Verificar bloque debajo
        int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

        // Condiciones de terreno
        if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
        {
            return 1.0f; // 100% de probabilidad desde día 9, cualquier hora
        }
        return 0f;
    },
    SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
        this.SpawnCreatures(creatureType, "InfectedFly2", point, 1).Count) // Individual
});

// Versión constante (spawn continuo) de InfectedFly2 - también desde día 9
this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFly2 Constant", SpawnLocationType.Surface, false, true)
{
    SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
    {
        // Condición de día: solo a partir del día 9
        SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
        int currentDay = 0;
        if (timeOfDay != null)
        {
            currentDay = (int)Math.Floor(timeOfDay.Day);
        }

        bool isDay9OrLater = currentDay >= 9;
        if (!isDay9OrLater)
            return 0f;

        // NO hay restricción de hora

        // Verificar que no esté en agua o lava
        int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
        if (blockAbove == 18) // Agua o lava
        {
            return 0f;
        }

        // Verificar bloque debajo
        int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

        // Condiciones de terreno
        if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
        {
            return 0.5f; // 50% de probabilidad para spawn constante
        }
        return 0f;
    },
    SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
        this.SpawnCreatures(creatureType, "InfectedFly2", point, 1).Count) // Individual
});

// InfectedFly3 - Aparece desde el día 9, CUALQUIER HORA (día o noche)
this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFly3", SpawnLocationType.Surface, false, false)
{
    SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
    {
        // Condición de día: solo a partir del día 9
        SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
        int currentDay = 0;
        if (timeOfDay != null)
        {
            currentDay = (int)Math.Floor(timeOfDay.Day);
        }

        bool isDay9OrLater = currentDay >= 9;
        if (!isDay9OrLater)
            return 0f;

        // NO hay restricción de hora

        // Verificar que no esté en agua o lava
        int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
        if (blockAbove == 18) // Agua o lava
        {
            return 0f;
        }

        // Verificar bloque debajo
        int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

        // Condiciones de terreno
        if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
        {
            return 1.0f; // 100% de probabilidad desde día 9, cualquier hora
        }
        return 0f;
    },
    SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
        this.SpawnCreatures(creatureType, "InfectedFly3", point, 1).Count) // Individual
});

// Versión constante (spawn continuo) de InfectedFly3 - también desde día 9
this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFly3 Constant", SpawnLocationType.Surface, false, true)
{
    SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
    {
        // Condición de día: solo a partir del día 9
        SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
        int currentDay = 0;
        if (timeOfDay != null)
        {
            currentDay = (int)Math.Floor(timeOfDay.Day);
        }

        bool isDay9OrLater = currentDay >= 9;
        if (!isDay9OrLater)
            return 0f;

        // NO hay restricción de hora

        // Verificar que no esté en agua o lava
        int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
        if (blockAbove == 18) // Agua o lava
        {
            return 0f;
        }

        // Verificar bloque debajo
        int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

        // Condiciones de terreno
        if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
        {
            return 0.5f; // 50% de probabilidad para spawn constante
        }
        return 0f;
    },
    SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
        this.SpawnCreatures(creatureType, "InfectedFly3", point, 1).Count) // Individual
});

// FlyingInfectedBoss - Aparece desde el día 31, CUALQUIER HORA (día o noche)
this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("FlyingInfectedBoss", SpawnLocationType.Surface, false, false)
{
    SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
    {
        // Condición de día: solo a partir del día 31
        SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
        int currentDay = 0;
        if (timeOfDay != null)
        {
            currentDay = (int)Math.Floor(timeOfDay.Day);
        }

        bool isDay31OrLater = currentDay >= 31;
        if (!isDay31OrLater)
            return 0f;

        // NO hay restricción de hora - boss aparece día y noche

        // Verificar que no esté en agua o lava
        int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
        if (blockAbove == 18) // Agua o lava
        {
            return 0f;
        }

        // Verificar bloque debajo
        int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

        // Condiciones de terreno
        if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
        {
            return 1.0f; // 100% de probabilidad desde día 31, cualquier hora
        }
        return 0f;
    },
    SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
        this.SpawnCreatures(creatureType, "FlyingInfectedBoss", point, 1).Count) // Individual
});

// Versión constante (spawn continuo) de FlyingInfectedBoss - también desde día 31
this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("FlyingInfectedBoss Constant", SpawnLocationType.Surface, false, true)
{
    SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
    {
        // Condición de día: solo a partir del día 31
        SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
        int currentDay = 0;
        if (timeOfDay != null)
        {
            currentDay = (int)Math.Floor(timeOfDay.Day);
        }

        bool isDay31OrLater = currentDay >= 31;
        if (!isDay31OrLater)
            return 0f;

        // NO hay restricción de hora

        // Verificar que no esté en agua o lava
        int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
        if (blockAbove == 18) // Agua o lava
        {
            return 0f;
        }

        // Verificar bloque debajo
        int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

        // Condiciones de terreno
        if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
        {
            return 0.5f; // 50% de probabilidad para spawn constante
        }
        return 0f;
    },
    SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
        this.SpawnCreatures(creatureType, "FlyingInfectedBoss", point, 1).Count) // Individual
});

// PoisonousInfected1 - Aparece desde el día 6, solo de noche
this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("PoisonousInfected1", SpawnLocationType.Surface, false, false)
{
    SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
    {
        // Condición de día: solo a partir del día 6
        SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
        int currentDay = 0;
        if (timeOfDay != null)
        {
            currentDay = (int)Math.Floor(timeOfDay.Day);
        }

        bool isDay6OrLater = currentDay >= 6;
        if (!isDay6OrLater)
            return 0f;

        // Condición de noche
        bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
        if (!isNight)
            return 0f;

        // Verificar que no esté en agua o lava
        int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
        if (blockAbove == 18) // Agua o lava
        {
            return 0f;
        }

        // Verificar bloque debajo
        int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

        // Condiciones de terreno
        if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
        {
            return 1.0f; // 100% de probabilidad cada noche desde día 6
        }
        return 0f;
    },
    SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
        this.SpawnCreatures(creatureType, "PoisonousInfected1", point, 1).Count) // Individual
});

// Versión constante (spawn continuo) de PoisonousInfected1 - también desde día 6
this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("PoisonousInfected1 Constant", SpawnLocationType.Surface, false, true)
{
    SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
    {
        // Condición de día: solo a partir del día 6
        SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
        int currentDay = 0;
        if (timeOfDay != null)
        {
            currentDay = (int)Math.Floor(timeOfDay.Day);
        }

        bool isDay6OrLater = currentDay >= 6;
        if (!isDay6OrLater)
            return 0f;

        // Condición de noche
        bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
        if (!isNight)
            return 0f;

        // Verificar que no esté en agua o lava
        int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
        if (blockAbove == 18) // Agua o lava
        {
            return 0f;
        }

        // Verificar bloque debajo
        int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

        // Condiciones de terreno
        if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
        {
            return 0.5f; // 50% de probabilidad para spawn constante
        }
        return 0f;
    },
    SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
        this.SpawnCreatures(creatureType, "PoisonousInfected1", point, 1).Count) // Individual
});

// PoisonousInfected2 - Aparece desde el día 6, solo de noche (mismo día que PoisonousInfected1)
this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("PoisonousInfected2", SpawnLocationType.Surface, false, false)
{
    SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
    {
        // Condición de día: solo a partir del día 6
        SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
        int currentDay = 0;
        if (timeOfDay != null)
        {
            currentDay = (int)Math.Floor(timeOfDay.Day);
        }

        bool isDay6OrLater = currentDay >= 6;
        if (!isDay6OrLater)
            return 0f;

        // Condición de noche
        bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
        if (!isNight)
            return 0f;

        // Verificar que no esté en agua o lava
        int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
        if (blockAbove == 18) // Agua o lava
        {
            return 0f;
        }

        // Verificar bloque debajo
        int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

        // Condiciones de terreno
        if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
        {
            return 1.0f; // 100% de probabilidad cada noche desde día 6
        }
        return 0f;
    },
    SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
        this.SpawnCreatures(creatureType, "PoisonousInfected2", point, 1).Count) // Individual
});

// Versión constante (spawn continuo) de PoisonousInfected2 - también desde día 6
this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("PoisonousInfected2 Constant", SpawnLocationType.Surface, false, true)
{
    SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
    {
        // Condición de día: solo a partir del día 6
        SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
        int currentDay = 0;
        if (timeOfDay != null)
        {
            currentDay = (int)Math.Floor(timeOfDay.Day);
        }

        bool isDay6OrLater = currentDay >= 6;
        if (!isDay6OrLater)
            return 0f;

        // Condición de noche
        bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
        if (!isNight)
            return 0f;

        // Verificar que no esté en agua o lava
        int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
        if (blockAbove == 18) // Agua o lava
        {
            return 0f;
        }

        // Verificar bloque debajo
        int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

        // Condiciones de terreno
        if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
        {
            return 0.5f; // 50% de probabilidad para spawn constante
        }
        return 0f;
    },
    SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
        this.SpawnCreatures(creatureType, "PoisonousInfected2", point, 1).Count) // Individual
});
			
			// InfectedFly1 - Aparece desde el día 9, CUALQUIER HORA (día o noche)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFly1", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 9
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay9OrLater = currentDay >= 9;
					if (!isDay9OrLater)
						return 0f;

					// NO hay restricción de hora - aparece día y noche

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 100% de probabilidad desde día 9, cualquier hora
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "InfectedFly1", point, 1).Count) // Individual
			});

			// Versión constante (spawn continuo) de InfectedFly1 - también desde día 9
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFly1 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 9
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay9OrLater = currentDay >= 9;
					if (!isDay9OrLater)
						return 0f;

					// NO hay restricción de hora

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.5f; // 50% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "InfectedFly1", point, 1).Count) // Individual
			});

			// InfectedFly2 - Aparece desde el día 9, CUALQUIER HORA (día o noche)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFly2", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 9
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay9OrLater = currentDay >= 9;
					if (!isDay9OrLater)
						return 0f;

					// NO hay restricción de hora

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 100% de probabilidad desde día 9, cualquier hora
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "InfectedFly2", point, 1).Count) // Individual
			});

			// Versión constante (spawn continuo) de InfectedFly2 - también desde día 9
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFly2 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 9
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay9OrLater = currentDay >= 9;
					if (!isDay9OrLater)
						return 0f;

					// NO hay restricción de hora

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.5f; // 50% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "InfectedFly2", point, 1).Count) // Individual
			});

			// InfectedFly3 - Aparece desde el día 9, CUALQUIER HORA (día o noche)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFly3", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 9
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay9OrLater = currentDay >= 9;
					if (!isDay9OrLater)
						return 0f;

					// NO hay restricción de hora

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 100% de probabilidad desde día 9, cualquier hora
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "InfectedFly3", point, 1).Count) // Individual
			});

			// Versión constante (spawn continuo) de InfectedFly3 - también desde día 9
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFly3 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 9
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay9OrLater = currentDay >= 9;
					if (!isDay9OrLater)
						return 0f;

					// NO hay restricción de hora

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.5f; // 50% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "InfectedFly3", point, 1).Count) // Individual
			});

			// FlyingInfectedBoss - Aparece desde el día 31, CUALQUIER HORA (día o noche)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("FlyingInfectedBoss", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 31
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay31OrLater = currentDay >= 31;
					if (!isDay31OrLater)
						return 0f;

					// NO hay restricción de hora - boss aparece día y noche

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 100% de probabilidad desde día 31, cualquier hora
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "FlyingInfectedBoss", point, 1).Count) // Individual
			});

			// Versión constante (spawn continuo) de FlyingInfectedBoss - también desde día 31
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("FlyingInfectedBoss Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 31
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay31OrLater = currentDay >= 31;
					if (!isDay31OrLater)
						return 0f;

					// NO hay restricción de hora

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.5f; // 50% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "FlyingInfectedBoss", point, 1).Count) // Individual
			});

			// PoisonousInfected1 - Aparece desde el día 6, solo de noche
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("PoisonousInfected1", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 6
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay6OrLater = currentDay >= 6;
					if (!isDay6OrLater)
						return 0f;

					// Condición de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 100% de probabilidad cada noche desde día 6
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PoisonousInfected1", point, 1).Count) // Individual
			});

			// Versión constante (spawn continuo) de PoisonousInfected1 - también desde día 6
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("PoisonousInfected1 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 6
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay6OrLater = currentDay >= 6;
					if (!isDay6OrLater)
						return 0f;

					// Condición de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.5f; // 50% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PoisonousInfected1", point, 1).Count) // Individual
			});

			// PoisonousInfected2 - Aparece desde el día 6, solo de noche (mismo día que PoisonousInfected1)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("PoisonousInfected2", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 6
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay6OrLater = currentDay >= 6;
					if (!isDay6OrLater)
						return 0f;

					// Condición de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 100% de probabilidad cada noche desde día 6
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PoisonousInfected2", point, 1).Count) // Individual
			});

			// Versión constante (spawn continuo) de PoisonousInfected2 - también desde día 6
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("PoisonousInfected2 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 6
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay6OrLater = currentDay >= 6;
					if (!isDay6OrLater)
						return 0f;

					// Condición de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.5f; // 50% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PoisonousInfected2", point, 1).Count) // Individual
			});

					// InfectedFast1 - Aparece desde el día 2, solo de noche
					this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFast1", SpawnLocationType.Surface, false, false)
					{
						SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
						{
							// Condición de día: solo a partir del día 2
							SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
							int currentDay = 0;
							if (timeOfDay != null)
							{
								currentDay = (int)Math.Floor(timeOfDay.Day);
							}

							bool isDay2OrLater = currentDay >= 2;
							if (!isDay2OrLater)
								return 0f;

							// Condición de noche
							bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
							if (!isNight)
								return 0f;

							// Verificar que no esté en agua o lava
							int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
							if (blockAbove == 18) // Agua o lava
							{
								return 0f;
							}

							// Verificar bloque debajo
							int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

							// Condiciones de terreno
							if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
							{
								return 1.0f; // 50% de probabilidad cada noche desde día 2
							}
							return 0f;
						},
						SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
							this.SpawnCreatures(creatureType, "InfectedFast1", point, this.m_random.Int(1, 3)).Count)
					});

					// Versión constante (spawn continuo) de InfectedFast1 - también desde día 2
					this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFast1 Constant", SpawnLocationType.Surface, false, true)
					{
						SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
						{
							// Condición de día: solo a partir del día 2
							SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
							int currentDay = 0;
							if (timeOfDay != null)
							{
								currentDay = (int)Math.Floor(timeOfDay.Day);
							}

							bool isDay2OrLater = currentDay >= 2;
							if (!isDay2OrLater)
								return 0f;

							// Condición de noche
							bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
							if (!isNight)
								return 0f;

							// Verificar que no esté en agua o lava
							int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
							if (blockAbove == 18) // Agua o lava
							{
								return 0f;
							}

							// Verificar bloque debajo
							int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

							// Condiciones de terreno
							if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
							{
								return 0.25f; // 25% de probabilidad para spawn constante
							}
							return 0f;
						},
						SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
							this.SpawnCreatures(creatureType, "InfectedFast1", point, this.m_random.Int(1, 2)).Count)
					});

					// InfectedFast2 - Aparece desde el día 2, solo de noche (mismo día que Fast1)
					this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFast2", SpawnLocationType.Surface, false, false)
					{
						SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
						{
							// Condición de día: solo a partir del día 2
							SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
							int currentDay = 0;
							if (timeOfDay != null)
							{
								currentDay = (int)Math.Floor(timeOfDay.Day);
							}

							bool isDay2OrLater = currentDay >= 2;
							if (!isDay2OrLater)
								return 0f;

							// Condición de noche
							bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
							if (!isNight)
								return 0f;

							// Verificar que no esté en agua o lava
							int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
							if (blockAbove == 18) // Agua o lava
							{
								return 0f;
							}

							// Verificar bloque debajo
							int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

							// Condiciones de terreno
							if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
							{
								return 1.0f; // 40% de probabilidad cada noche desde día 2
							}
							return 0f;
						},
						SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
							this.SpawnCreatures(creatureType, "InfectedFast2", point, this.m_random.Int(1, 3)).Count)
					});

					// Versión constante (spawn continuo) de InfectedFast2 - también desde día 2
					this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedFast2 Constant", SpawnLocationType.Surface, false, true)
					{
						SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
						{
							// Condición de día: solo a partir del día 2
							SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
							int currentDay = 0;
							if (timeOfDay != null)
							{
								currentDay = (int)Math.Floor(timeOfDay.Day);
							}

							bool isDay2OrLater = currentDay >= 2;
							if (!isDay2OrLater)
								return 0f;

							// Condición de noche
							bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
							if (!isNight)
								return 0f;

							// Verificar que no esté en agua o lava
							int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
							if (blockAbove == 18) // Agua o lava
							{
								return 0f;
							}

							// Verificar bloque debajo
							int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

							// Condiciones de terreno
							if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
							{
								return 0.5f; // 20% de probabilidad para spawn constante
							}
							return 0f;
						},
						SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
							this.SpawnCreatures(creatureType, "InfectedFast2", point, this.m_random.Int(1, 2)).Count)
					});

					// InfectedMuscle1 - Aparece desde el día 5, solo de noche
					this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedMuscle1", SpawnLocationType.Surface, false, false)
					{
						SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
						{
							// Condición de día: solo a partir del día 5
							SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
							int currentDay = 0;
							if (timeOfDay != null)
							{
								currentDay = (int)Math.Floor(timeOfDay.Day);
							}

							bool isDay5OrLater = currentDay >= 5;
							if (!isDay5OrLater)
								return 0f;

							// Condición de noche
							bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
							if (!isNight)
								return 0f;

							// Verificar que no esté en agua o lava
							int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
							if (blockAbove == 18) // Agua o lava
							{
								return 0f;
							}

							// Verificar bloque debajo
							int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

							// Condiciones de terreno
							if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
							{
								return 1.0f; // 50% de probabilidad cada noche desde día 5
							}
							return 0f;
						},
						SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
							this.SpawnCreatures(creatureType, "InfectedMuscle1", point, this.m_random.Int(1, 2)).Count)
					});

					// Versión constante (spawn continuo) de InfectedMuscle1 - también desde día 5
					this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedMuscle1 Constant", SpawnLocationType.Surface, false, true)
					{
						SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
						{
							// Condición de día: solo a partir del día 5
							SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
							int currentDay = 0;
							if (timeOfDay != null)
							{
								currentDay = (int)Math.Floor(timeOfDay.Day);
							}

							bool isDay5OrLater = currentDay >= 5;
							if (!isDay5OrLater)
								return 0f;

							// Condición de noche
							bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
							if (!isNight)
								return 0f;

							// Verificar que no esté en agua o lava
							int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
							if (blockAbove == 18) // Agua o lava
							{
								return 0f;
							}

							// Verificar bloque debajo
							int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

							// Condiciones de terreno
							if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
							{
								return 0.5f; // 25% de probabilidad para spawn constante
							}
							return 0f;
						},
						SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
							this.SpawnCreatures(creatureType, "InfectedMuscle1", point, 1).Count)
					});

					// InfectedMuscle2 - Aparece desde el día 5, solo de noche
					this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedMuscle2", SpawnLocationType.Surface, false, false)
					{
						SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
						{
							// Condición de día: solo a partir del día 5
							SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
							int currentDay = 0;
							if (timeOfDay != null)
							{
								currentDay = (int)Math.Floor(timeOfDay.Day);
							}

							bool isDay5OrLater = currentDay >= 5;
							if (!isDay5OrLater)
								return 0f;

							// Condición de noche
							bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
							if (!isNight)
								return 0f;

							// Verificar que no esté en agua o lava
							int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
							if (blockAbove == 18) // Agua o lava
							{
								return 0f;
							}

							// Verificar bloque debajo
							int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

							// Condiciones de terreno
							if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
							{
								return 1.0f; // 40% de probabilidad cada noche desde día 5
							}
							return 0f;
						},
						SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
							this.SpawnCreatures(creatureType, "InfectedMuscle2", point, this.m_random.Int(1, 2)).Count)
					});

					// Versión constante (spawn continuo) de InfectedMuscle2 - también desde día 5
					this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedMuscle2 Constant", SpawnLocationType.Surface, false, true)
					{
						SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
						{
							// Condición de día: solo a partir del día 5
							SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
							int currentDay = 0;
							if (timeOfDay != null)
							{
								currentDay = (int)Math.Floor(timeOfDay.Day);
							}

							bool isDay5OrLater = currentDay >= 5;
							if (!isDay5OrLater)
								return 0f;

							// Condición de noche
							bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
							if (!isNight)
								return 0f;

							// Verificar que no esté en agua o lava
							int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
							if (blockAbove == 18) // Agua o lava
							{
								return 0f;
							}

							// Verificar bloque debajo
							int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

							// Condiciones de terreno
							if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
							{
								return 0.5f; // 20% de probabilidad para spawn constante
							}
							return 0f;
						},
						SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
							this.SpawnCreatures(creatureType, "InfectedMuscle2", point, 1).Count)
					});

			// Tank1 - Aparece desde el día 41, CUALQUIER HORA (día o noche)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Tank1", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 41
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay41OrLater = currentDay >= 41;
					if (!isDay41OrLater)
						return 0f;

					// NO hay restricción de hora - aparece día y noche

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 100% de probabilidad desde día 41, cualquier hora
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Tank1", point, 1).Count)
			});

			// Versión constante (spawn continuo) de Tank1 - también desde día 41
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Tank1 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 41
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay41OrLater = currentDay >= 41;
					if (!isDay41OrLater)
						return 0f;

					// NO hay restricción de hora

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.5f; // 50% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Tank1", point, 1).Count)
			});

			// Tank2 - Aparece desde el día 50, CUALQUIER HORA (día o noche)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Tank2", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 50
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay50OrLater = currentDay >= 50;
					if (!isDay50OrLater)
						return 0f;

					// NO hay restricción de hora - aparece día y noche

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 100% de probabilidad desde día 50, cualquier hora
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Tank2", point, 1).Count)
			});

			// Versión constante (spawn continuo) de Tank2 - también desde día 50
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Tank2 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 50
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay50OrLater = currentDay >= 50;
					if (!isDay50OrLater)
						return 0f;

					// NO hay restricción de hora

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.55f; // 55% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Tank2", point, 1).Count)
			});

			// Tank3 - Aparece desde el día 55, CUALQUIER HORA (día o noche)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Tank3", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 55
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay55OrLater = currentDay >= 55;
					if (!isDay55OrLater)
						return 0f;

					// NO hay restricción de hora - aparece día y noche

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 100% de probabilidad desde día 55, cualquier hora
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Tank3", point, 1).Count)
			});

			// Versión constante (spawn continuo) de Tank3 - también desde día 55
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("Tank3 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 55
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay55OrLater = currentDay >= 55;
					if (!isDay55OrLater)
						return 0f;

					// NO hay restricción de hora

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.6f; // 60% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Tank3", point, 1).Count)
			});

			// InfectedNormal1 - Aparece desde el DÍA 1, SOLO DE NOCHE (y durante noche verde)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedNormal1", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Acceder al estado de noche verde (necesitas tener acceso al subsistema)
					bool isGreenNight = false;
					SubsystemGreenNightSky greenNightSys = base.Project.FindSubsystem<SubsystemGreenNightSky>(false);
					if (greenNightSys != null)
						isGreenNight = greenNightSys.IsGreenNightActive;

					// Durante noche verde, pueden spawnear a cualquier hora (día o noche)
					if (!isGreenNight)
					{
						// Si NO es noche verde, solo spawnear de noche
						bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
						if (!isNight)
							return 0f;
					}

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno (TIERRA, PIEDRA, HIERBA)
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						// Durante noche verde: probabilidad MUY ALTA (100%)
						// Durante noche normal: 50%
						if (isGreenNight)
							return 1.0f; // 100% durante noche verde (oleadas)
						else
							return 0.005f; // 50% durante noches normales
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "InfectedNormal1", point, 1).Count) // Individual
			});

			// Versión constante (spawn continuo) - también desde día 1, solo de noche (y durante noche verde)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedNormal1 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Acceder al estado de noche verde
					bool isGreenNight = false;
					SubsystemGreenNightSky greenNightSys = base.Project.FindSubsystem<SubsystemGreenNightSky>(false);
					if (greenNightSys != null)
						isGreenNight = greenNightSys.IsGreenNightActive;

					// Durante noche verde, pueden spawnear a cualquier hora
					if (!isGreenNight)
					{
						// Si NO es noche verde, solo spawnear de noche
						bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
						if (!isNight)
							return 0f;
					}

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						// Durante noche verde: probabilidad ALTA (80%)
						// Durante noche normal: 20%
						if (isGreenNight)
							return 0.8f; // 80% durante noche verde (oleadas constantes)
						else
							return 0.002f; // 20% durante noches normales
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "InfectedNormal1", point, 1).Count) // Individual
			});

			// InfectedNormal2 - Aparece desde el DÍA 1, SOLO DE NOCHE (y durante noche verde)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedNormal2", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Acceder al estado de noche verde
					bool isGreenNight = false;
					SubsystemGreenNightSky greenNightSys = base.Project.FindSubsystem<SubsystemGreenNightSky>(false);
					if (greenNightSys != null)
						isGreenNight = greenNightSys.IsGreenNightActive;

					// Durante noche verde, pueden spawnear a cualquier hora
					if (!isGreenNight)
					{
						// Si NO es noche verde, solo spawnear de noche
						bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
						if (!isNight)
							return 0f;
					}

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno (TIERRA, PIEDRA, HIERBA)
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						// Durante noche verde: probabilidad MUY ALTA (100%)
						// Durante noche normal: 100% (original)
						if (isGreenNight)
							return 1.0f; // 100% durante noche verde (oleadas)
						else
							return 0.001f; // 100% durante noches normales (mantenido del original)
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "InfectedNormal2", point, 1).Count) // Individual
			});

			// Versión constante (spawn continuo) - también desde día 1, solo de noche (y durante noche verde)
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("InfectedNormal2 Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Acceder al estado de noche verde
					bool isGreenNight = false;
					SubsystemGreenNightSky greenNightSys = base.Project.FindSubsystem<SubsystemGreenNightSky>(false);
					if (greenNightSys != null)
						isGreenNight = greenNightSys.IsGreenNightActive;

					// Durante noche verde, pueden spawnear a cualquier hora
					if (!isGreenNight)
					{
						// Si NO es noche verde, solo spawnear de noche
						bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
						if (!isNight)
							return 0f;
					}

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						// Durante noche verde: probabilidad ALTA (90%)
						// Durante noche normal: 50%
						if (isGreenNight)
							return 0.9f; // 90% durante noche verde (oleadas constantes)
						else
							return 0.005f; // 50% durante noches normales
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "InfectedNormal2", point, 1).Count) // Individual
			});

			// GhostNormal - Aparece desde el día 4, solo de noche
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("GhostNormal", SpawnLocationType.Surface, false, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 4
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay4OrLater = currentDay >= 4;
					if (!isDay4OrLater)
						return 0f;

					// Condición de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 1.0f; // 100% de probabilidad cada noche desde día 4
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "GhostNormal", point, 2).Count) // Siempre spawnear 2
			});

			// Versión constante (spawn continuo) de GhostNormal - también desde día 4
			this.m_creatureTypes.Add(new SubsystemZombiesSpawn.CreatureType("GhostNormal Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemZombiesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 4
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay4OrLater = currentDay >= 4;
					if (!isDay4OrLater)
						return 0f;

					// Condición de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.1f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int blockAbove = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
					if (blockAbove == 18) // Agua o lava
					{
						return 0f;
					}

					// Verificar bloque debajo
					int groundBlock = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

					// Condiciones de terreno
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 0.5f; // 50% de probabilidad para spawn constante
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemZombiesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "GhostNormal", point, 1).Count) // Siempre spawnear 2 en constante también
			});

		}

		// Token: 0x06000255 RID: 597 RVA: 0x0001E11C File Offset: 0x0001C31C
		private void SpawnRandomCreature()
		{
			// Límites dinámicos según noche verde
			int totalLimit = this.m_isGreenNightActive ? 48 : 24;          // ← MODIFICADO
			int areaLimit = this.m_isGreenNightActive ? 6 : 3;            // ← MODIFICADO
			float areaRadius = this.m_isGreenNightActive ? 30f : 16f;     // ← MODIFICADO
			float viewRadius = this.m_isGreenNightActive ? 80f : 60f;     // ← MODIFICADO

			bool flag = this.CountCreatures(false) >= totalLimit;
			if (!flag)
			{
				foreach (GameWidget gameWidget in this.m_subsystemViews.GameWidgets)
				{
					int num = 48;
					Vector2 v = new Vector2(gameWidget.ActiveCamera.ViewPosition.X, gameWidget.ActiveCamera.ViewPosition.Z);
					bool flag2 = this.CountCreaturesInArea(v - new Vector2(viewRadius), v + new Vector2(viewRadius), false) >= num;
					if (flag2)
					{
						break;
					}
					SpawnLocationType spawnLocationType = this.GetRandomSpawnLocationType();
					Point3? spawnPoint = this.GetRandomSpawnPoint(gameWidget.ActiveCamera, spawnLocationType);
					bool flag3 = spawnPoint != null;
					if (flag3)
					{
						Vector2 c3 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) - new Vector2(areaRadius);
						Vector2 c2 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) + new Vector2(areaRadius);
						bool flag4 = this.CountCreaturesInArea(c3, c2, false) >= areaLimit;
						if (flag4)
						{
							break;
						}
						IEnumerable<SubsystemZombiesSpawn.CreatureType> source = from c in this.m_creatureTypes
																				 where c.SpawnLocationType == spawnLocationType && c.RandomSpawn
																				 select c;
						IEnumerable<float> items = from c in source
												   select c.SpawnSuitabilityFunction(c, spawnPoint.Value);
						int randomWeightedItem = this.GetRandomWeightedItem(items);
						bool flag5 = randomWeightedItem >= 0;
						if (flag5)
						{
							SubsystemZombiesSpawn.CreatureType creatureType = source.ElementAt(randomWeightedItem);
							creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						}
					}
				}
			}
		}

		// Token: 0x06000256 RID: 598 RVA: 0x0001E34C File Offset: 0x0001C54C
		private void SpawnChunkCreatures(SpawnChunk chunk, int maxAttempts, bool constantSpawn)
		{
			// Límites dinámicos según noche verde
			int num = constantSpawn
				? (m_isGreenNightActive ? 36 : 18)    // ← MODIFICADO: Límite total para constantSpawn
				: (m_isGreenNightActive ? 48 : 24);   // ← MODIFICADO: Límite total para randomSpawn

			int num2 = constantSpawn
				? (m_isGreenNightActive ? 8 : 4)      // ← MODIFICADO: Límite por área para constantSpawn
				: (m_isGreenNightActive ? 6 : 3);     // ← MODIFICADO: Límite por área para randomSpawn

			float v = constantSpawn
				? (m_isGreenNightActive ? 60f : 42f)  // ← MODIFICADO: Radio para constantSpawn
				: (m_isGreenNightActive ? 30f : 16f); // ← MODIFICADO: Radio para randomSpawn

			int num3 = this.CountCreatures(constantSpawn);
			Vector2 c3 = new Vector2((float)(chunk.Point.X * 16), (float)(chunk.Point.Y * 16)) - new Vector2(v);
			Vector2 c2 = new Vector2((float)((chunk.Point.X + 1) * 16), (float)((chunk.Point.Y + 1) * 16)) + new Vector2(v);
			int num4 = this.CountCreaturesInArea(c3, c2, constantSpawn);

			for (int i = 0; i < maxAttempts; i++)
			{
				bool flag = num3 >= num || num4 >= num2;
				if (flag)
				{
					break;
				}

				SpawnLocationType spawnLocationType = this.GetRandomSpawnLocationType();
				Point3? spawnPoint = this.GetRandomChunkSpawnPoint(chunk, spawnLocationType);
				bool flag2 = spawnPoint != null;
				if (flag2)
				{
					IEnumerable<SubsystemZombiesSpawn.CreatureType> source = from c in this.m_creatureTypes
																			 where c.SpawnLocationType == spawnLocationType && c.ConstantSpawn == constantSpawn
																			 select c;
					IEnumerable<float> items = from c in source
											   select c.SpawnSuitabilityFunction(c, spawnPoint.Value);
					int randomWeightedItem = this.GetRandomWeightedItem(items);
					bool flag3 = randomWeightedItem >= 0;
					if (flag3)
					{
						SubsystemZombiesSpawn.CreatureType creatureType = source.ElementAt(randomWeightedItem);
						int num5 = creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						num3 += num5;
						num4 += num5;
					}
				}
			}
		}

		// Token: 0x06000257 RID: 599 RVA: 0x0001E518 File Offset: 0x0001C718
		private List<Entity> SpawnCreatures(SubsystemZombiesSpawn.CreatureType creatureType, string templateName, Point3 point, int count)
		{
			List<Entity> list = new List<Entity>();
			int num = 0;
			while (count > 0 && num < 50)
			{
				Point3 spawnPoint = point;
				bool flag = num > 0;
				if (flag)
				{
					spawnPoint.X += this.m_random.Int(-8, 8);
					spawnPoint.Y += this.m_random.Int(-4, 8);
					spawnPoint.Z += this.m_random.Int(-8, 8);
				}
				Point3? point2 = this.ProcessSpawnPoint(spawnPoint, creatureType.SpawnLocationType);
				bool flag2 = point2 != null && creatureType.SpawnSuitabilityFunction(creatureType, point2.Value) > 0f;
				if (flag2)
				{
					Vector3 position = new Vector3((float)point2.Value.X + this.m_random.Float(0.4f, 0.6f), (float)point2.Value.Y + 1.1f, (float)point2.Value.Z + this.m_random.Float(0.4f, 0.6f));
					Entity entity = this.SpawnCreature(templateName, position, creatureType.ConstantSpawn);
					bool flag3 = entity != null;
					if (flag3)
					{
						list.Add(entity);
						count--;
					}
				}
				num++;
			}
			return list;
		}

		// Token: 0x06000258 RID: 600 RVA: 0x0001E67C File Offset: 0x0001C87C
		private Entity SpawnCreature(string templateName, Vector3 position, bool constantSpawn)
		{
			Entity result;
			try
			{
				Entity entity = DatabaseManager.CreateEntity(base.Project, templateName, true);
				entity.FindComponent<ComponentBody>(true).Position = position;
				entity.FindComponent<ComponentBody>(true).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, this.m_random.Float(0f, 6.2831855f));
				entity.FindComponent<ComponentCreature>(true).ConstantSpawn = constantSpawn;
				base.Project.AddEntity(entity);
				result = entity;
			}
			catch (Exception ex)
			{
				Log.Error("Unable to spawn creature with template \"" + templateName + "\". Reason: " + ex.Message);
				result = null;
			}
			return result;
		}

		// Token: 0x06000259 RID: 601 RVA: 0x0001E728 File Offset: 0x0001C928
		private Point3? GetRandomChunkSpawnPoint(SpawnChunk chunk, SpawnLocationType spawnLocationType)
		{
			for (int i = 0; i < 5; i++)
			{
				int x = 16 * chunk.Point.X + this.m_random.Int(0, 15);
				int y = this.m_random.Int(10, 246);
				int z = 16 * chunk.Point.Y + this.m_random.Int(0, 15);
				Point3? result = this.ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
				bool flag = result != null;
				if (flag)
				{
					return result;
				}
			}
			return null;
		}

		// Token: 0x0600025A RID: 602 RVA: 0x0001E7D4 File Offset: 0x0001C9D4
		private Point3? GetRandomSpawnPoint(Camera camera, SpawnLocationType spawnLocationType)
		{
			for (int i = 0; i < 10; i++)
			{
				int x = Terrain.ToCell(camera.ViewPosition.X) + this.m_random.Sign() * this.m_random.Int(20, 40);
				int y = MathUtils.Clamp(Terrain.ToCell(camera.ViewPosition.Y) + this.m_random.Int(-30, 30), 2, 254);
				int z = Terrain.ToCell(camera.ViewPosition.Z) + this.m_random.Sign() * this.m_random.Int(20, 40);
				Point3? result = this.ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
				bool flag = result != null;
				if (flag)
				{
					return result;
				}
			}
			return null;
		}

		// Token: 0x0600025B RID: 603 RVA: 0x0001E8B8 File Offset: 0x0001CAB8
		private Point3? ProcessSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
		{
			int x = spawnPoint.X;
			int num = MathUtils.Clamp(spawnPoint.Y, 1, 254);
			int z = spawnPoint.Z;
			TerrainChunk chunkAtCell = this.m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			bool flag = chunkAtCell != null && chunkAtCell.State > TerrainChunkState.InvalidPropagatedLight;
			if (flag)
			{
				for (int i = 0; i < 30; i++)
				{
					Point3 point = new Point3(x, num + i, z);
					bool flag2 = this.TestSpawnPoint(point, spawnLocationType);
					if (flag2)
					{
						return new Point3?(point);
					}
					Point3 point2 = new Point3(x, num - i, z);
					bool flag3 = this.TestSpawnPoint(point2, spawnLocationType);
					if (flag3)
					{
						return new Point3?(point2);
					}
				}
			}
			return null;
		}

		// Token: 0x0600025C RID: 604 RVA: 0x0001E98C File Offset: 0x0001CB8C
		private bool TestSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
		{
			int x = spawnPoint.X;
			int y = spawnPoint.Y;
			int z = spawnPoint.Z;
			bool flag = y <= 3 || y >= 253;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				switch (spawnLocationType)
				{
					case SpawnLocationType.Surface:
						{
							int cellLightFast = this.m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
							bool flag2 = this.m_subsystemSky.SkyLightValue - cellLightFast > 3;
							if (flag2)
							{
								result = false;
							}
							else
							{
								int cellContentsFast = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
								int cellContentsFast2 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
								int cellContentsFast3 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
								Block block = BlocksManager.Blocks[cellContentsFast];
								Block block2 = BlocksManager.Blocks[cellContentsFast2];
								Block block3 = BlocksManager.Blocks[cellContentsFast3];
								result = ((block.IsCollidable || block is WaterBlock) && !block2.IsCollidable && !(block2 is WaterBlock) && !block3.IsCollidable && !(block3 is WaterBlock));
							}
							break;
						}
					case SpawnLocationType.Cave:
						{
							int cellLightFast2 = this.m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
							bool flag3 = this.m_subsystemSky.SkyLightValue - cellLightFast2 < 5;
							if (flag3)
							{
								result = false;
							}
							else
							{
								int cellContentsFast4 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
								int cellContentsFast5 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
								int cellContentsFast6 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
								Block block4 = BlocksManager.Blocks[cellContentsFast4];
								Block block5 = BlocksManager.Blocks[cellContentsFast5];
								Block block6 = BlocksManager.Blocks[cellContentsFast6];
								result = ((block4.IsCollidable || block4 is WaterBlock) && !block5.IsCollidable && !(block5 is WaterBlock) && !block6.IsCollidable && !(block6 is WaterBlock));
							}
							break;
						}
					case SpawnLocationType.Water:
						{
							int cellContentsFast7 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
							int cellContentsFast8 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
							int cellContentsFast9 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 2, z);
							Block block7 = BlocksManager.Blocks[cellContentsFast7];
							Block block8 = BlocksManager.Blocks[cellContentsFast8];
							Block block9 = BlocksManager.Blocks[cellContentsFast9];
							result = (block7 is WaterBlock && !block8.IsCollidable && !block9.IsCollidable);
							break;
						}
					default:
						throw new InvalidOperationException("Unknown spawn location type.");
				}
			}
			return result;
		}

		// Token: 0x0600025D RID: 605 RVA: 0x0001EC38 File Offset: 0x0001CE38
		private int CountCreatures(bool constantSpawn)
		{
			int num = 0;
			foreach (ComponentBody componentBody in this.m_subsystemBodies.Bodies)
			{
				ComponentCreature componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
				bool flag = componentCreature != null && componentCreature.ConstantSpawn == constantSpawn;
				if (flag)
				{
					num++;
				}
			}
			return num;
		}

		// Token: 0x0600025E RID: 606 RVA: 0x0001ECC0 File Offset: 0x0001CEC0
		private int CountCreaturesInArea(Vector2 c1, Vector2 c2, bool constantSpawn)
		{
			int num = 0;
			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesInArea(c1, c2, this.m_componentBodies);
			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentBody componentBody = this.m_componentBodies.Array[i];
				ComponentCreature componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
				bool flag = componentCreature != null && componentCreature.ConstantSpawn == constantSpawn;
				if (flag)
				{
					Vector3 position = componentBody.Position;
					bool flag2 = position.X >= c1.X && position.X <= c2.X && position.Z >= c1.Y && position.Z <= c2.Y;
					if (flag2)
					{
						num++;
					}
				}
			}
			Point2 point = Terrain.ToChunk(c1);
			Point2 point2 = Terrain.ToChunk(c2);
			for (int j = point.X; j <= point2.X; j++)
			{
				for (int k = point.Y; k <= point2.Y; k++)
				{
					SpawnChunk spawnChunk = this.m_subsystemSpawn.GetSpawnChunk(new Point2(j, k));
					bool flag3 = spawnChunk == null;
					if (!flag3)
					{
						foreach (SpawnEntityData spawnEntityData in spawnChunk.SpawnsData)
						{
							bool flag4 = spawnEntityData.ConstantSpawn == constantSpawn;
							if (flag4)
							{
								Vector3 position2 = spawnEntityData.Position;
								bool flag5 = position2.X >= c1.X && position2.X <= c2.X && position2.Z >= c1.Y && position2.Z <= c2.Y;
								if (flag5)
								{
									num++;
								}
							}
						}
					}
				}
			}
			return num;
		}

		// Token: 0x0600025F RID: 607 RVA: 0x0001EEE4 File Offset: 0x0001D0E4
		private int GetRandomWeightedItem(IEnumerable<float> items)
		{
			float max = MathUtils.Max(items.Sum(), 1f);
			float num = this.m_random.Float(0f, max);
			int num2 = 0;
			foreach (float num3 in items)
			{
				bool flag = num < num3;
				if (flag)
				{
					return num2;
				}
				num -= num3;
				num2++;
			}
			return -1;
		}

		// Token: 0x06000260 RID: 608 RVA: 0x0001EF74 File Offset: 0x0001D174
		public SpawnLocationType GetRandomSpawnLocationType()
		{
			return SubsystemZombiesSpawn.m_spawnLocations[this.m_random.Int(0, SubsystemZombiesSpawn.m_spawnLocations.Length - 1)];
		}

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemSpawn m_subsystemSpawn;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemSky m_subsystemSky;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemGameWidgets m_subsystemViews;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;  // ← AÑADIDO
		private bool m_isGreenNightActive;  // ← AÑADIDO (SIN this. delante)
		private Random m_random = new Random();
		private List<SubsystemZombiesSpawn.CreatureType> m_creatureTypes = new List<SubsystemZombiesSpawn.CreatureType>();
		private Dictionary<ComponentCreature, bool> m_creatures = new Dictionary<ComponentCreature, bool>();
		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		private List<SpawnChunk> m_newSpawnChunks = new List<SpawnChunk>();
		private List<SpawnChunk> m_spawnChunks = new List<SpawnChunk>();

		// Token: 0x04000302 RID: 770
		private static SpawnLocationType[] m_spawnLocations = EnumUtils.GetEnumValues(typeof(SpawnLocationType)).Cast<SpawnLocationType>().ToArray<SpawnLocationType>();

		// Token: 0x04000303 RID: 771
		private const int m_totalLimit = 24;

		// Token: 0x04000304 RID: 772
		private const int m_areaLimit = 3;

		// Token: 0x04000305 RID: 773
		private const int m_areaRadius = 16;

		// Token: 0x04000306 RID: 774
		private const int m_totalLimitConstant = 18;

		// Token: 0x04000307 RID: 775
		private const int m_areaLimitConstant = 4;

		// Token: 0x04000308 RID: 776
		private const int m_areaRadiusConstant = 42;

		// Token: 0x02000066 RID: 102
		private class CreatureType
		{
			// Token: 0x06000327 RID: 807 RVA: 0x0002420E File Offset: 0x0002240E
			public CreatureType(string name, SpawnLocationType spawnLocationType, bool randomSpawn, bool constantSpawn)
			{
				this.Name = name;
				this.SpawnLocationType = spawnLocationType;
				this.RandomSpawn = randomSpawn;
				this.ConstantSpawn = constantSpawn;
			}

			// Token: 0x06000328 RID: 808 RVA: 0x00024238 File Offset: 0x00022438
			public override string ToString()
			{
				return this.Name;
			}

			// Token: 0x04000386 RID: 902
			public string Name;

			// Token: 0x04000387 RID: 903
			public SpawnLocationType SpawnLocationType;

			// Token: 0x04000388 RID: 904
			public bool RandomSpawn;

			// Token: 0x04000389 RID: 905
			public bool ConstantSpawn;

			// Token: 0x0400038A RID: 906
			public Func<SubsystemZombiesSpawn.CreatureType, Point3, float> SpawnSuitabilityFunction;

			// Token: 0x0400038B RID: 907
			public Func<SubsystemZombiesSpawn.CreatureType, Point3, int> SpawnFunction;
		}
	}
}
