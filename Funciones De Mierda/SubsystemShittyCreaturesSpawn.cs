using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using static Game.SubsystemCreatureSpawn;

namespace Game
{
	// Token: 0x02000046 RID: 70
	public class SubsystemShittyCreaturesSpawn : Subsystem, IUpdateable
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
			bool flag = this.m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode > EnvironmentBehaviorMode.Living;
			if (!flag)
			{
				bool flag2 = this.m_newSpawnChunks.Count > 0;
				if (flag2)
				{
					this.m_newSpawnChunks.RandomShuffle((int max) => this.m_random.Int(0, max - 1));
					foreach (SpawnChunk chunk in this.m_newSpawnChunks)
					{
						this.SpawnChunkCreatures(chunk, 10, false);
					}
					this.m_newSpawnChunks.Clear();
				}
				bool flag3 = this.m_spawnChunks.Count > 0;
				if (flag3)
				{
					this.m_spawnChunks.RandomShuffle((int max) => this.m_random.Int(0, max - 1));
					foreach (SpawnChunk chunk2 in this.m_spawnChunks)
					{
						this.SpawnChunkCreatures(chunk2, 2, true);
					}
					this.m_spawnChunks.Clear();
				}
				bool flag4 = this.m_subsystemTime.PeriodicGameTimeEvent(60.0, 2.0);
				if (flag4)
				{
					this.SpawnRandomCreature();
				}
			}
		}

		// Token: 0x06000251 RID: 593 RVA: 0x0001D0C4 File Offset: 0x0001B2C4
		public override void Load(ValuesDictionary valuesDictionary)
		{
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
			// Spawn para Naomi con 100% de probabilidad - DESDE DÍA 0, cualquier estación y hora (versión normal)
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Naomi", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					// Agua (18) o lava (92)
					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					// Verificar el bloque del suelo
					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					// Condiciones flexibles para Naomi - SIN RESTRICCIÓN DE ALTURA, DÍAS, HORAS O ESTACIONES
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Naomi", point, this.m_random.Int(1, 3)).Count)
			});

			// Spawn para Naomi Constant - DESDE DÍA 0, cualquier estación y hora (versión constante)
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Naomi Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					// Agua (18) o lava (92)
					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					// Verificar el bloque del suelo
					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					// Condiciones flexibles para Naomi - SIN RESTRICCIÓN DE ALTURA, DÍAS, HORAS O ESTACIONES
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Naomi", point, this.m_random.Int(1, 2)).Count)
			});

			// Spawn para Brayan con 100% de probabilidad - DESDE DÍA 0, cualquier estación y hora (versión normal)
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Brayan", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					// Agua (18) o lava (92)
					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					// Condiciones flexibles para Brayan
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Brayan", point, this.m_random.Int(1, 2)).Count)
			});

			// Spawn para Brayan Constant - DESDE DÍA 0, cualquier estación y hora (versión constante)
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Brayan Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					// Agua (18) o lava (92)
					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					// Condiciones flexibles para Brayan
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Brayan", point, 1).Count) // Solo 1 para constante
			});

			// Spawn para Tulio con 100% de probabilidad - DESDE DÍA 0, cualquier estación y hora (versión normal)
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Tulio", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					// Agua (18) o lava (92)
					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					// Condiciones flexibles para Tulio
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Tulio", point, this.m_random.Int(1, 2)).Count)
			});

			// Spawn para Tulio Constant - DESDE DÍA 0, cualquier estación y hora (versión constante)
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Tulio Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					// Agua (18) o lava (92)
					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					// Condiciones flexibles para Tulio
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Tulio", point, 1).Count) // Solo 1 para constante
			});

			// Spawn para Ricardo con 100% de probabilidad - DESDE DÍA 0, cualquier estación y hora (versión normal)
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Ricardo", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					// Agua (18) o lava (92)
					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					// Condiciones flexibles para Ricardo
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Ricardo", point, this.m_random.Int(1, 3)).Count)
			});

			// Spawn para Ricardo Constant - DESDE DÍA 0, cualquier estación y hora (versión constante)
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Ricardo Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					// Agua (18) o lava (92)
					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					// Condiciones flexibles para Ricardo
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Ricardo", point, this.m_random.Int(1, 2)).Count)
			});

			// BetelGammamon - Solo en otoño e invierno, solo de día (versión normal)
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("BetelGammamon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Obtener la estación actual del sistema de estaciones
					SubsystemSeasons subsystemSeasons = base.Project.FindSubsystem<SubsystemSeasons>(true);
					Season currentSeason = subsystemSeasons.Season;

					// Verificar que sea otoño o invierno
					bool isAutumnOrWinter = (currentSeason == Season.Autumn || currentSeason == Season.Winter);
					if (!isAutumnOrWinter)
						return 0f;

					// Verificar que sea de día (SkyLightIntensity alta = día)
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Otras condiciones de spawn...
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
					int temperature = this.m_subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
					int blockBelow = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					int lightLevel = this.m_subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAtPoint = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					// Agua (18) o lava (92)
					if (blockAtPoint == 18 || blockAtPoint == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					return (oceanDistance > 20f && temperature >= 8 && point.Y < 100 && lightLevel >= 7 &&
						(blockBelow == 8 || blockBelow == 2 || blockBelow == 3 || blockBelow == 7)) ? 1f : 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "BetelGammamon", point, this.m_random.Int(1, 2)).Count)
			});

			// BetelGammamon Constant - Solo en otoño e invierno, solo de día (versión constante)
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("BetelGammamon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Obtener la estación actual del sistema de estaciones
					SubsystemSeasons subsystemSeasons = base.Project.FindSubsystem<SubsystemSeasons>(true);
					Season currentSeason = subsystemSeasons.Season;

					// Verificar que sea otoño o invierno
					bool isAutumnOrWinter = (currentSeason == Season.Autumn || currentSeason == Season.Winter);
					if (!isAutumnOrWinter)
						return 0f;

					// Verificar que sea de día (SkyLightIntensity alta = día)
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Otras condiciones de spawn...
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
					int temperature = this.m_subsystemTerrain.Terrain.GetTemperature(point.X, point.Z);
					int blockBelow = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					int lightLevel = this.m_subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAtPoint = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					// Agua (18) o lava (92)
					if (blockAtPoint == 18 || blockAtPoint == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					return (oceanDistance > 20f && temperature >= 8 && point.Y < 100 && lightLevel >= 7 &&
						(blockBelow == 8 || blockBelow == 2 || blockBelow == 3 || blockBelow == 7)) ? 1f : 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "BetelGammamon", point, this.m_random.Int(1, 2)).Count)
			});

			// Spawn para LaMuerteX con 100% de probabilidad - DESDE DÍA 0, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("LaMuerteX", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Verificar que sea de noche (SkyLightIntensity baja = noche)
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					// Agua (18) o lava (92)
					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					// Verificar el bloque del suelo
					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					// Condiciones flexibles para LaMuerteX - SIN RESTRICCIÓN DE ALTURA O ESTACIONES, PERO SOLO DE NOCHE
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "LaMuerteX", point, this.m_random.Int(1, 2)).Count)
			});

			// Spawn para LaMuerteX Constant - DESDE DÍA 0, solo de noche (versión constante)
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("LaMuerteX Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Verificar que sea de noche (SkyLightIntensity baja = noche)
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					// Agua (18) o lava (92)
					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					// Condiciones flexibles para LaMuerteX - SIN RESTRICCIÓN DE ALTURA O ESTACIONES, PERO SOLO DE NOCHE
					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f; // 100% de probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "LaMuerteX", point, 5).Count) // Solo 1 para constante
			});

			// Spawn para ElSenorDeLasTumbasMoradas con 100% de probabilidad - DESDE DÍA 2, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElSenorDeLasTumbasMoradas", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElSenorDeLasTumbasMoradas", point, 2).Count)
			});

			// Spawn para ElSenorDeLasTumbasMoradas Constant - DESDE DÍA 2, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElSenorDeLasTumbasMoradas Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElSenorDeLasTumbasMoradas", point, 2).Count)
			});

			// Spawn para Paco con 100% de probabilidad - DESDE DÍA 2, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Paco", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Paco", point, 2).Count)
			});

			// Spawn para Paco Constant - DESDE DÍA 2, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Paco Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Paco", point, 2).Count)
			});

			// Spawn para Veemon con 100% de probabilidad - DESDE DÍA 3, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Veemon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 3
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay3OrLater = currentDay >= 3;
					if (!isDay3OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Veemon", point, 2).Count)
			});

			// Spawn para Veemon Constant - DESDE DÍA 3, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Veemon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 3
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay3OrLater = currentDay >= 3;
					if (!isDay3OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Veemon", point, 2).Count)
			});

			// Spawn para Gaomon con 100% de probabilidad - DESDE DÍA 3, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Gaomon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 3
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay3OrLater = currentDay >= 3;
					if (!isDay3OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Gaomon", point, 2).Count)
			});

			// Spawn para Gaomon Constant - DESDE DÍA 3, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Gaomon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 3
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay3OrLater = currentDay >= 3;
					if (!isDay3OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Gaomon", point, 2).Count)
			});

			// Spawn para Agumon con 100% de probabilidad - DESDE DÍA 3, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Agumon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 3
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay3OrLater = currentDay >= 3;
					if (!isDay3OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Agumon", point, 2).Count)
			});

			// Spawn para Agumon Constant - DESDE DÍA 3, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Agumon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 3
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay3OrLater = currentDay >= 3;
					if (!isDay3OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Agumon", point, 2).Count)
			});

			// Spawn para Barack con 100% de probabilidad - DESDE DÍA 4, cualquier hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Barack", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Barack", point, 2).Count)
			});

			// Spawn para Barack Constant - DESDE DÍA 4, cualquier hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Barack Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Barack", point, 2).Count)
			});

			// Spawn para ElMarihuanero con 100% de probabilidad - DESDE DÍA 6, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElMarihuanero", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElMarihuanero", point, 2).Count)
			});

			// Spawn para ElMarihuanero Constant - DESDE DÍA 6, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElMarihuanero Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElMarihuanero", point, 2).Count)
			});

			// Spawn para ElMarihuaneroMamon con 100% de probabilidad - DESDE DÍA 7, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElMarihuaneroMamon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 7
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay7OrLater = currentDay >= 7;
					if (!isDay7OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElMarihuaneroMamon", point, 2).Count)
			});

			// Spawn para ElMarihuaneroMamon Constant - DESDE DÍA 7, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElMarihuaneroMamon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 7
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay7OrLater = currentDay >= 7;
					if (!isDay7OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElMarihuaneroMamon", point, 2).Count)
			});

			// Spawn para Shoutmon con 100% de probabilidad - DESDE DÍA 5, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Shoutmon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Shoutmon", point, 2).Count)
			});

			// Spawn para Shoutmon Constant - DESDE DÍA 5, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Shoutmon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Shoutmon", point, 2).Count)
			});

			// Spawn para Impmon con 100% de probabilidad - DESDE DÍA 5, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Impmon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Impmon", point, 2).Count)
			});

			// Spawn para Impmon Constant - DESDE DÍA 5, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Impmon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Impmon", point, 2).Count)
			});

			// Spawn para FumadorQuimico con 100% de probabilidad - DESDE DÍA 7, cualquier hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("FumadorQuimico", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 7
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay7OrLater = currentDay >= 7;
					if (!isDay7OrLater)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "FumadorQuimico", point, 2).Count)
			});

			// Spawn para FumadorQuimico Constant - DESDE DÍA 7, cualquier hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("FumadorQuimico Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 7
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay7OrLater = currentDay >= 7;
					if (!isDay7OrLater)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "FumadorQuimico", point, 2).Count)
			});

			// Spawn para LiderCalavericoSupremo con 100% de probabilidad - DESDE DÍA 8, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("LiderCalavericoSupremo", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 8
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay8OrLater = currentDay >= 8;
					if (!isDay8OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "LiderCalavericoSupremo", point, 2).Count)
			});

			// Spawn para LiderCalavericoSupremo Constant - DESDE DÍA 8, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("LiderCalavericoSupremo Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 8
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay8OrLater = currentDay >= 8;
					if (!isDay8OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "LiderCalavericoSupremo", point, 2).Count)
			});

			// Spawn para Guilmon con 100% de probabilidad - DESDE DÍA 10, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Guilmon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 10
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay10OrLater = currentDay >= 10;
					if (!isDay10OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Guilmon", point, 2).Count)
			});

			// Spawn para Guilmon Constant - DESDE DÍA 10, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Guilmon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 10
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay10OrLater = currentDay >= 10;
					if (!isDay10OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Guilmon", point, 2).Count)
			});

			// Spawn para Gumdramon con 100% de probabilidad - DESDE DÍA 10, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Gumdramon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 10
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay10OrLater = currentDay >= 10;
					if (!isDay10OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Gumdramon", point, 2).Count)
			});

			// Spawn para Gumdramon Constant - DESDE DÍA 10, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Gumdramon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 10
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay10OrLater = currentDay >= 10;
					if (!isDay10OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Gumdramon", point, 2).Count)
			});

			// Spawn para Sparkster con 100% de probabilidad - DESDE DÍA 2, cualquier hora (versión normal)
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Sparkster", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Sparkster", point, 2).Count)
			});

			// Spawn para Sparkster Constant - DESDE DÍA 2, cualquier hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Sparkster Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Sparkster", point, 2).Count)
			});

			// Spawn para ElArquero con 100% de probabilidad - DESDE DÍA 5, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElArquero", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElArquero", point, 2).Count)
			});

			// Spawn para ElArquero Constant - DESDE DÍA 5, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElArquero Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElArquero", point, 2).Count)
			});

			// Spawn para Arqueroprisionero con 100% de probabilidad - DESDE DÍA 6, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Arqueroprisionero", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Arqueroprisionero", point, 2).Count)
			});

			// Spawn para Arqueroprisionero Constant - DESDE DÍA 6, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Arqueroprisionero Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Arqueroprisionero", point, 2).Count)
			});

			// Spawn para Conker con 100% de probabilidad - DESDE DÍA 7, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Conker", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 7
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay7OrLater = currentDay >= 7;
					if (!isDay7OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Conker", point, 2).Count)
			});

			// Spawn para Conker Constant - DESDE DÍA 7, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Conker Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 7
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay7OrLater = currentDay >= 7;
					if (!isDay7OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Conker", point, 2).Count)
			});

			// Spawn para ElBallestador con 100% de probabilidad - DESDE DÍA 10, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElBallestador", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 10
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay10OrLater = currentDay >= 10;
					if (!isDay10OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElBallestador", point, 2).Count)
			});

			// Spawn para ElBallestador Constant - DESDE DÍA 10, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElBallestador Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 10
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay10OrLater = currentDay >= 10;
					if (!isDay10OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElBallestador", point, 2).Count)
			});

			// Spawn para BallestadoraMusculosa con 100% de probabilidad - DESDE DÍA 11, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("BallestadoraMusculosa", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 11
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay11OrLater = currentDay >= 11;
					if (!isDay11OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "BallestadoraMusculosa", point, 2).Count)
			});

			// Spawn para BallestadoraMusculosa Constant - DESDE DÍA 11, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("BallestadoraMusculosa Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 11
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay11OrLater = currentDay >= 11;
					if (!isDay11OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "BallestadoraMusculosa", point, 2).Count)
			});

			// Spawn para Claudespeed con 100% de probabilidad - DESDE DÍA 12, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ClaudeSpeed", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 12
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay12OrLater = currentDay >= 12;
					if (!isDay12OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ClaudeSpeed", point, 1).Count) // Individualmente
			});

			// Spawn para Claudespeed Constant - DESDE DÍA 12, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ClaudeSpeed Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 12
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay12OrLater = currentDay >= 12;
					if (!isDay12OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ClaudeSpeed", point, 1).Count) // Individualmente
			});

			// Spawn para Tommyvercetti con 100% de probabilidad - DESDE DÍA 13, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("TommyVercetti", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 13
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay13OrLater = currentDay >= 13;
					if (!isDay13OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "TommyVercetti", point, 1).Count) // Individualmente
			});

			// Spawn para Tommyvercetti Constant - DESDE DÍA 13, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("TommyVercetti Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 13
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay13OrLater = currentDay >= 13;
					if (!isDay13OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "TommyVercetti", point, 1).Count) // Individualmente
			});

			// Spawn para Carljohnson con 100% de probabilidad - DESDE DÍA 14, cualquier hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("CarlJohnson", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 14
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay14OrLater = currentDay >= 14;
					if (!isDay14OrLater)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "CarlJohnson", point, 1).Count) // Individualmente
			});

			// Spawn para Carljohnson Constant - DESDE DÍA 14, cualquier hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("CarlJohnson Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 14
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay14OrLater = currentDay >= 14;
					if (!isDay14OrLater)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "CarlJohnson", point, 1).Count) // Individualmente
			});

			// Spawn para Beavis con 100% de probabilidad - DESDE DÍA 14, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Beavis", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 14
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay14OrLater = currentDay >= 14;
					if (!isDay14OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Beavis", point, 1).Count) // Individualmente
			});

			// Spawn para Beavis Constant - DESDE DÍA 14, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Beavis Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 14
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay14OrLater = currentDay >= 14;
					if (!isDay14OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Beavis", point, 1).Count) // Individualmente
			});

			// Spawn para Butthead con 100% de probabilidad - DESDE DÍA 14, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Butt-Head", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 14
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay14OrLater = currentDay >= 14;
					if (!isDay14OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Butt-Head", point, 1).Count) // Individualmente
			});

			// Spawn para Butthead Constant - DESDE DÍA 14, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Butt-Head Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 14
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay14OrLater = currentDay >= 14;
					if (!isDay14OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Butt-Head", point, 1).Count) // Individualmente
			});

			// Spawn para HombreLava con 100% de probabilidad - DESDE DÍA 17, solo de noche, en lava
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("HombreLava", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 17
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay17OrLater = currentDay >= 17;
					if (!isDay17OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que esté en lava (bloque 92) o sobre lava
					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					// También verificar el bloque actual y el de arriba
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					// Permitir spawn en lava (92) o sobre lava
					if (groundBlock == 92 || currentBlock == 92)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "HombreLava", point, 1).Count) // Individualmente
			});

			// Spawn para HombreLava Constant - DESDE DÍA 17, solo de noche, en lava
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("HombreLava Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 17
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay17OrLater = currentDay >= 17;
					if (!isDay17OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que esté en lava (bloque 92) o sobre lava
					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					// Permitir spawn en lava (92) o sobre lava
					if (groundBlock == 92 || currentBlock == 92)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "HombreLava", point, 1).Count) // Individualmente
			});

			// Spawn para HombreAgua con 100% de probabilidad - DESDE DÍA 18, solo de noche, en agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("HombreAgua", SpawnLocationType.Water, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 18
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay18OrLater = currentDay >= 18;
					if (!isDay18OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que esté en agua (bloque 18)
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					// Verificar que haya agua en el bloque actual y espacio arriba
					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					int cellValueHead2 = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 2, point.Z);
					int headBlock2 = Terrain.ExtractContents(cellValueHead2);

					if (currentBlock == 18 && headBlock != 18 && headBlock2 != 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "HombreAgua", point, 1).Count) // Individualmente
			});

			// Spawn para HombreAgua Constant - DESDE DÍA 18, solo de noche, en agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("HombreAgua Constant", SpawnLocationType.Water, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 18
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay18OrLater = currentDay >= 18;
					if (!isDay18OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que esté en agua (bloque 18)
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					int cellValueHead2 = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 2, point.Z);
					int headBlock2 = Terrain.ExtractContents(cellValueHead2);

					if (currentBlock == 18 && headBlock != 18 && headBlock2 != 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "HombreAgua", point, 1).Count) // Individualmente
			});

			// Spawn para Zombierepetidor con 100% de probabilidad - DESDE DÍA 21, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ZombieRepetidor", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 21
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay21OrLater = currentDay >= 21;
					if (!isDay21OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ZombieRepetidor", point, 2).Count)
			});

			// Spawn para Zombierepetidor Constant - DESDE DÍA 21, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ZombieRepetidor Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 21
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay21OrLater = currentDay >= 21;
					if (!isDay21OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ZombieRepetidor", point, 2).Count)
			});

			// Spawn para Walterzombie con 100% de probabilidad - DESDE DÍA 22, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("WalterZombie", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 22
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay22OrLater = currentDay >= 22;
					if (!isDay22OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "WalterZombie", point, 1).Count) // Individualmente
			});

			// Spawn para Walterzombie Constant - DESDE DÍA 22, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("WalterZombie Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 22
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay22OrLater = currentDay >= 22;
					if (!isDay22OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "WalterZombie", point, 1).Count) // Individualmente
			});

			// Spawn para ElGuerrilleroTenebroso con 100% de probabilidad - DESDE DÍA 24, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElGuerrilleroTenebroso", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 24
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay24OrLater = currentDay >= 24;
					if (!isDay24OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElGuerrilleroTenebroso", point, 1).Count) // Individualmente
			});

			// Spawn para ElGuerrilleroTenebroso Constant - DESDE DÍA 24, solo de noche
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElGuerrilleroTenebroso Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 24
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay24OrLater = currentDay >= 24;
					if (!isDay24OrLater)
						return 0f;

					// Verificar que sea de noche
					bool isNight = this.m_subsystemSky.SkyLightIntensity < 0.3f;
					if (!isNight)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElGuerrilleroTenebroso", point, 1).Count) // Individualmente
			});

			// Spawn para ElGuerrillero con 100% de probabilidad - DESDE DÍA 25, cualquier hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElGuerrillero", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 25
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay25OrLater = currentDay >= 25;
					if (!isDay25OrLater)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElGuerrillero", point, 1).Count) // Individualmente
			});

			// Spawn para ElGuerrillero Constant - DESDE DÍA 25, cualquier hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("ElGuerrillero Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 25
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay25OrLater = currentDay >= 25;
					if (!isDay25OrLater)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "ElGuerrillero", point, 1).Count) // Individualmente
			});

			// Spawn para AladinaCorrupta con 100% de probabilidad - DESDE DÍA 28, cualquier hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("AladinaCorrupta", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "AladinaCorrupta", point, 2).Count)
			});

			// Spawn para AladinaCorrupta Constant - DESDE DÍA 28, cualquier hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("AladinaCorrupta Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "AladinaCorrupta", point, 2).Count)
			});

			// Spawn para Richard con 100% de probabilidad - DESDE DÍA 30, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Richard", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Richard", point, 2).Count)
			});

			// Spawn para Richard Constant - DESDE DÍA 30, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Richard Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Richard", point, 2).Count)
			});

			// Spawn para PirataNormal con 100% de probabilidad - DESDE DÍA 4, solo de día, cerca del agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("PirataNormal", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que esté cerca del agua o en la costa
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);

					// Verificar que esté cerca del agua (menos de 30 bloques de la costa o en agua)
					bool isNearWater = oceanDistance < 30f;
					if (!isNearWater)
						return 0f;

					// Verificar que no esté en lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					// Solo bloquear lava (92), agua está permitida
					if (currentBlock == 92 || headBlock == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					// Permitir spawn en arena (8) o tierra (2, 3, 7) cerca del agua
					if (groundBlock == 8 || groundBlock == 2 || groundBlock == 3 || groundBlock == 7)
					{
						return 100f;
					}

					// También permitir spawn directamente en agua (18)
					if (currentBlock == 18 || groundBlock == 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PirataNormal", point, 2).Count)
			});

			// Spawn para PirataNormal Constant - DESDE DÍA 4, solo de día, cerca del agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("PirataNormal Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que esté cerca del agua o en la costa
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);

					bool isNearWater = oceanDistance < 30f;
					if (!isNearWater)
						return 0f;

					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					if (currentBlock == 92 || headBlock == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 8 || groundBlock == 2 || groundBlock == 3 || groundBlock == 7)
					{
						return 100f;
					}

					if (currentBlock == 18 || groundBlock == 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PirataNormal", point, 2).Count)
			});

			// Spawn para PirataNormalAliado con 100% de probabilidad - DESDE DÍA 4, solo de día, cerca del agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("PirataNormalAliado", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que esté cerca del agua o en la costa
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);

					bool isNearWater = oceanDistance < 30f;
					if (!isNearWater)
						return 0f;

					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					if (currentBlock == 92 || headBlock == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 8 || groundBlock == 2 || groundBlock == 3 || groundBlock == 7)
					{
						return 100f;
					}

					if (currentBlock == 18 || groundBlock == 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PirataNormalAliado", point, 2).Count)
			});

			// Spawn para PirataNormalAliado Constant - DESDE DÍA 4, solo de día, cerca del agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("PirataNormalAliado Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que esté cerca del agua o en la costa
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);

					bool isNearWater = oceanDistance < 30f;
					if (!isNearWater)
						return 0f;

					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					if (currentBlock == 92 || headBlock == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 8 || groundBlock == 2 || groundBlock == 3 || groundBlock == 7)
					{
						return 100f;
					}

					if (currentBlock == 18 || groundBlock == 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PirataNormalAliado", point, 2).Count)
			});

			// Spawn para PirataElite con 100% de probabilidad - DESDE DÍA 5, solo de día, cerca del agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("PirataElite", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que esté cerca del agua o en la costa
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);

					bool isNearWater = oceanDistance < 30f;
					if (!isNearWater)
						return 0f;

					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					if (currentBlock == 92 || headBlock == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 8 || groundBlock == 2 || groundBlock == 3 || groundBlock == 7)
					{
						return 100f;
					}

					if (currentBlock == 18 || groundBlock == 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PirataElite", point, 2).Count)
			});

			// Spawn para PirataElite Constant - DESDE DÍA 5, solo de día, cerca del agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("PirataElite Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que esté cerca del agua o en la costa
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);

					bool isNearWater = oceanDistance < 30f;
					if (!isNearWater)
						return 0f;

					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					if (currentBlock == 92 || headBlock == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 8 || groundBlock == 2 || groundBlock == 3 || groundBlock == 7)
					{
						return 100f;
					}

					if (currentBlock == 18 || groundBlock == 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PirataElite", point, 2).Count)
			});

			// Spawn para PirataEliteAliado con 100% de probabilidad - DESDE DÍA 5, solo de día, cerca del agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("PirataEliteAliado", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que esté cerca del agua o en la costa
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);

					bool isNearWater = oceanDistance < 30f;
					if (!isNearWater)
						return 0f;

					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					if (currentBlock == 92 || headBlock == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 8 || groundBlock == 2 || groundBlock == 3 || groundBlock == 7)
					{
						return 100f;
					}

					if (currentBlock == 18 || groundBlock == 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PirataEliteAliado", point, 2).Count)
			});

			// Spawn para PirataEliteAliado Constant - DESDE DÍA 5, solo de día, cerca del agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("PirataEliteAliado Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que esté cerca del agua o en la costa
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);

					bool isNearWater = oceanDistance < 30f;
					if (!isNearWater)
						return 0f;

					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					if (currentBlock == 92 || headBlock == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 8 || groundBlock == 2 || groundBlock == 3 || groundBlock == 7)
					{
						return 100f;
					}

					if (currentBlock == 18 || groundBlock == 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PirataEliteAliado", point, 2).Count)
			});

			// Spawn para PirataHostilComerciante con 100% de probabilidad - DESDE DÍA 27, solo de día, cerca del agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("PirataHostilComerciante", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 27
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay27OrLater = currentDay >= 27;
					if (!isDay27OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que esté cerca del agua o en la costa
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);

					bool isNearWater = oceanDistance < 30f;
					if (!isNearWater)
						return 0f;

					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					if (currentBlock == 92 || headBlock == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 8 || groundBlock == 2 || groundBlock == 3 || groundBlock == 7)
					{
						return 100f;
					}

					if (currentBlock == 18 || groundBlock == 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PirataHostilComerciante", point, 1).Count) // Individualmente
			});

			// Spawn para PirataHostilComerciante Constant - DESDE DÍA 27, solo de día, cerca del agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("PirataHostilComerciante Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 27
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay27OrLater = currentDay >= 27;
					if (!isDay27OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que esté cerca del agua o en la costa
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);

					bool isNearWater = oceanDistance < 30f;
					if (!isNearWater)
						return 0f;

					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					if (currentBlock == 92 || headBlock == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 8 || groundBlock == 2 || groundBlock == 3 || groundBlock == 7)
					{
						return 100f;
					}

					if (currentBlock == 18 || groundBlock == 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "PirataHostilComerciante", point, 1).Count) // Individualmente
			});

			// Spawn para CapitanPirata con 100% de probabilidad - DESDE DÍA 39, solo de día, cerca del agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("CapitanPirata", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 39
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay39OrLater = currentDay >= 39;
					if (!isDay39OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que esté cerca del agua o en la costa
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);

					bool isNearWater = oceanDistance < 30f;
					if (!isNearWater)
						return 0f;

					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					if (currentBlock == 92 || headBlock == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 8 || groundBlock == 2 || groundBlock == 3 || groundBlock == 7)
					{
						return 100f;
					}

					if (currentBlock == 18 || groundBlock == 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "CapitanPirata", point, 1).Count) // Individualmente
			});

			// Spawn para CapitanPirata Constant - DESDE DÍA 39, solo de día, cerca del agua
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("CapitanPirata Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
				{
					// Condición de día: solo a partir del día 39
					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null)
					{
						currentDay = (int)Math.Floor(timeOfDay.Day);
					}

					bool isDay39OrLater = currentDay >= 39;
					if (!isDay39OrLater)
						return 0f;

					// Verificar que sea de día
					bool isDay = this.m_subsystemSky.SkyLightIntensity > 0.5f;
					if (!isDay)
						return 0f;

					// Verificar que esté cerca del agua o en la costa
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);

					bool isNearWater = oceanDistance < 30f;
					if (!isNearWater)
						return 0f;

					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int currentBlock = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int headBlock = Terrain.ExtractContents(cellValueHead);

					if (currentBlock == 92 || headBlock == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 8 || groundBlock == 2 || groundBlock == 3 || groundBlock == 7)
					{
						return 100f;
					}

					if (currentBlock == 18 || groundBlock == 18)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "CapitanPirata", point, 1).Count) // Individualmente
			});

			// Spawn para CamisasMorenas con 100% de probabilidad - DESDE DÍA 29, cualquier hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("CamisasMorenas", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "CamisasMorenas", point, 1).Count) // Solo 1 individualmente
			});

			// Spawn para CamisasMorenas Constant - DESDE DÍA 29, cualquier hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("CamisasMorenas Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point)
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

					// Verificar que no esté en agua o lava
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
					{
						return 0f;
					}

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
					{
						return 100f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "CamisasMorenas", point, 1).Count) // Solo 1 individualmente
			});
		}

		// Token: 0x06000255 RID: 597 RVA: 0x0001E11C File Offset: 0x0001C31C
		private void SpawnRandomCreature()
		{
			bool flag = this.CountCreatures(false) >= 24;
			if (!flag)
			{
				foreach (GameWidget gameWidget in this.m_subsystemViews.GameWidgets)
				{
					int num = 48;
					Vector2 v = new Vector2(gameWidget.ActiveCamera.ViewPosition.X, gameWidget.ActiveCamera.ViewPosition.Z);
					bool flag2 = this.CountCreaturesInArea(v - new Vector2(60f), v + new Vector2(60f), false) >= num;
					if (flag2)
					{
						break;
					}
					SpawnLocationType spawnLocationType = this.GetRandomSpawnLocationType();
					Point3? spawnPoint = this.GetRandomSpawnPoint(gameWidget.ActiveCamera, spawnLocationType);
					bool flag3 = spawnPoint != null;
					if (flag3)
					{
						Vector2 c3 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) - new Vector2(16f);
						Vector2 c2 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) + new Vector2(16f);
						bool flag4 = this.CountCreaturesInArea(c3, c2, false) >= 3;
						if (flag4)
						{
							break;
						}
						IEnumerable<SubsystemShittyCreaturesSpawn.CreatureType> source = from c in this.m_creatureTypes
																				 where c.SpawnLocationType == spawnLocationType && c.RandomSpawn
																				 select c;
						IEnumerable<float> items = from c in source
												   select c.SpawnSuitabilityFunction(c, spawnPoint.Value);
						int randomWeightedItem = this.GetRandomWeightedItem(items);
						bool flag5 = randomWeightedItem >= 0;
						if (flag5)
						{
							SubsystemShittyCreaturesSpawn.CreatureType creatureType = source.ElementAt(randomWeightedItem);
							creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						}
					}
				}
			}
		}

		// Token: 0x06000256 RID: 598 RVA: 0x0001E34C File Offset: 0x0001C54C
		private void SpawnChunkCreatures(SpawnChunk chunk, int maxAttempts, bool constantSpawn)
		{
			int num = constantSpawn ? 18 : 24;
			int num2 = constantSpawn ? 4 : 3;
			float v = (float)(constantSpawn ? 42 : 16);
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
					IEnumerable<SubsystemShittyCreaturesSpawn.CreatureType> source = from c in this.m_creatureTypes
																			 where c.SpawnLocationType == spawnLocationType && c.ConstantSpawn == constantSpawn
																			 select c;
					IEnumerable<float> items = from c in source
											   select c.SpawnSuitabilityFunction(c, spawnPoint.Value);
					int randomWeightedItem = this.GetRandomWeightedItem(items);
					bool flag3 = randomWeightedItem >= 0;
					if (flag3)
					{
						SubsystemShittyCreaturesSpawn.CreatureType creatureType = source.ElementAt(randomWeightedItem);
						int num5 = creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						num3 += num5;
						num4 += num5;
					}
				}
			}
		}

		// Token: 0x06000257 RID: 599 RVA: 0x0001E518 File Offset: 0x0001C718
		private List<Entity> SpawnCreatures(SubsystemShittyCreaturesSpawn.CreatureType creatureType, string templateName, Point3 point, int count)
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
			return SubsystemShittyCreaturesSpawn.m_spawnLocations[this.m_random.Int(0, SubsystemShittyCreaturesSpawn.m_spawnLocations.Length - 1)];
		}

		// Token: 0x040002F5 RID: 757
		private SubsystemGameInfo m_subsystemGameInfo;

		// Token: 0x040002F6 RID: 758
		private SubsystemSpawn m_subsystemSpawn;

		// Token: 0x040002F7 RID: 759
		private SubsystemTerrain m_subsystemTerrain;

		// Token: 0x040002F8 RID: 760
		private SubsystemTime m_subsystemTime;

		// Token: 0x040002F9 RID: 761
		private SubsystemSky m_subsystemSky;

		// Token: 0x040002FA RID: 762
		private SubsystemBodies m_subsystemBodies;

		// Token: 0x040002FB RID: 763
		private SubsystemGameWidgets m_subsystemViews;

		// Token: 0x040002FC RID: 764
		private Random m_random = new Random();

		// Token: 0x040002FD RID: 765
		private List<SubsystemShittyCreaturesSpawn.CreatureType> m_creatureTypes = new List<SubsystemShittyCreaturesSpawn.CreatureType>();

		// Token: 0x040002FE RID: 766
		private Dictionary<ComponentCreature, bool> m_creatures = new Dictionary<ComponentCreature, bool>();

		// Token: 0x040002FF RID: 767
		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();

		// Token: 0x04000300 RID: 768
		private List<SpawnChunk> m_newSpawnChunks = new List<SpawnChunk>();

		// Token: 0x04000301 RID: 769
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
			public Func<SubsystemShittyCreaturesSpawn.CreatureType, Point3, float> SpawnSuitabilityFunction;

			// Token: 0x0400038B RID: 907
			public Func<SubsystemShittyCreaturesSpawn.CreatureType, Point3, int> SpawnFunction;
		}
	}
}
