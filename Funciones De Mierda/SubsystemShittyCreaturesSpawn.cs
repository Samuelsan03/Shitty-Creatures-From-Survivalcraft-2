using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using Game;

namespace Game
{
	public class SubsystemShittyCreaturesSpawn : Subsystem, IUpdateable
	{
		// ====== CONSTANTS ======
		public static int m_totalLimit = 26;
		public static int m_areaLimit = 3;
		public static int m_areaRadius = 16;
		public static int m_totalLimitConstant = 6;
		public static int m_totalLimitConstantChallenging = 12;
		public static int m_areaLimitConstant = 4;
		public static int m_areaRadiusConstant = 42;
		public const float m_populationReductionConstant = 0.25f;

		// ====== FIELDS ======
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemSpawn m_subsystemSpawn;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemTime m_subsystemTime;
		public SubsystemSky m_subsystemSky;
		public SubsystemSeasons m_subsystemSeasons;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemGameWidgets m_subsystemViews;
		public SubsystemTimeOfDay m_subsystemTimeOfDay;

		public Random m_random = new Random();
		public List<CreatureType> m_creatureTypes = new List<CreatureType>();
		public Dictionary<ComponentCreature, bool> m_creatures = new Dictionary<ComponentCreature, bool>();
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		public List<SpawnChunk> m_newSpawnChunks = new List<SpawnChunk>();
		public List<SpawnChunk> m_spawnChunks = new List<SpawnChunk>();

		// ====== PROPERTIES ======
		public Dictionary<ComponentCreature, bool>.KeyCollection Creatures => m_creatures.Keys;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// ====== MÉTODO AUXILIAR ======
		private int GetCurrentDay()
		{
			return (int)Math.Floor(m_subsystemTimeOfDay.Day) + 1;
		}

		// ====== UPDATE ======
		public virtual void Update(float dt)
		{
			if (m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
			{
				if (m_newSpawnChunks.Count > 0)
				{
					m_newSpawnChunks.RandomShuffle((int max) => m_random.Int(0, max - 1));
					foreach (SpawnChunk chunk in m_newSpawnChunks)
					{
						SpawnChunkCreatures(chunk, 10, false);
					}
					m_newSpawnChunks.Clear();
				}

				if (m_spawnChunks.Count > 0)
				{
					m_spawnChunks.RandomShuffle((int max) => m_random.Int(0, max - 1));
					foreach (SpawnChunk chunk2 in m_spawnChunks)
					{
						SpawnChunkCreatures(chunk2, 2, true);
					}
					m_spawnChunks.Clear();
				}

				float num = (m_subsystemSeasons.Season == Season.Winter) ? 120f : 60f;
				if (m_subsystemTime.PeriodicGameTimeEvent((double)num, 2.0))
				{
					SpawnRandomCreature();
				}
			}
		}

		// ====== LOAD ======
		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemSpawn = Project.FindSubsystem<SubsystemSpawn>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemSeasons = Project.FindSubsystem<SubsystemSeasons>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true);
			m_subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true);

			InitializeCreatureTypes();

			SubsystemSpawn subsystemSpawn = m_subsystemSpawn;
			subsystemSpawn.SpawningChunk = (Action<SpawnChunk>)Delegate.Combine(subsystemSpawn.SpawningChunk, new Action<SpawnChunk>(delegate (SpawnChunk chunk)
			{
				m_spawnChunks.Add(chunk);
				if (!chunk.IsSpawned)
				{
					m_newSpawnChunks.Add(chunk);
				}
			}));
		}

		public override void OnEntityAdded(Entity entity)
		{
			foreach (ComponentCreature creature in entity.FindComponents<ComponentCreature>())
				m_creatures.Add(creature, true);
		}

		public override void OnEntityRemoved(Entity entity)
		{
			foreach (ComponentCreature creature in entity.FindComponents<ComponentCreature>())
				m_creatures.Remove(creature);
		}

		// ====== INITIALIZE CREATURE TYPES ======
		private void InitializeCreatureTypes()
		{
			// ----- Naomi (day, normal spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Naomi", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Naomi", point, 1).Count
			});

			// ----- Naomi Constant (day, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant Naomi", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Naomi", point, 1).Count
			});

			// ----- Ricardo (day, normal spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Ricardo", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Ricardo", point, 1).Count
			});

			// ----- Ricardo Constant (day, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant Ricardo", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Ricardo", point, 1).Count
			});

			// ----- Tulio (day, normal spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Tulio", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Tulio", point, 1).Count
			});

			// ----- Tulio Constant (day, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant Tulio", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Tulio", point, 1).Count
			});

			// ----- Brayan (day, normal spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Brayan", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Brayan", point, 1).Count
			});

			// ----- Brayan Constant (day, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant Brayan", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Brayan", point, 1).Count
			});

			// ----- LaMuerteX (night, normal spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("LaMuerteX", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "LaMuerteX", point, 1).Count
			});

			// ----- LaMuerteX Constant (night, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant LaMuerteX", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "LaMuerteX", point, 1).Count
			});

			// ----- ElSenorDeLasTumbasMoradas (night of day >= 2, normal spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("ElSenorDeLasTumbasMoradas", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 2) return 0f;
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "ElSenorDeLasTumbasMoradas", point, 1).Count
			});

			// ----- ElSenorDeLasTumbasMoradas Constant (night of day >= 2, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant ElSenorDeLasTumbasMoradas", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 2) return 0f;
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "ElSenorDeLasTumbasMoradas", point, 1).Count
			});

			// ----- Paco (day, day >= 4, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Paco", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 4) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Paco", point, 1).Count
			});

			// ----- Paco Constant (day, day >= 4, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant Paco", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 4) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Paco", point, 1).Count
			});

			// ----- Barack (day, day >= 3, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Barack", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 3) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Barack", point, 1).Count
			});

			// ----- Barack Constant (day, day >= 3, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant Barack", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 3) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Barack", point, 1).Count
			});

			// ----- Sparkster (any time, day >= 2, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Sparkster", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 2) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Sparkster", point, 1).Count
			});

			// ----- Sparkster Constant (any time, day >= 2, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant Sparkster", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 2) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Sparkster", point, 1).Count
			});

			// ----- ElArquero (night, day >= 5, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("ElArquero", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 5) return 0f;
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "ElArquero", point, 1).Count
			});

			// ----- ElArquero Constant (night, day >= 5, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant ElArquero", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 5) return 0f;
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "ElArquero", point, 1).Count
			});

			// ----- ArqueroPrisionero (night, day >= 6, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("ArqueroPrisionero", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 6) return 0f;
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "ArqueroPrisionero", point, 1).Count
			});

			// ----- ArqueroPrisionero Constant (night, day >= 6, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant ArqueroPrisionero", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 6) return 0f;
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "ArqueroPrisionero", point, 1).Count
			});

			// ----- ElBallestador (night, day >= 10, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("ElBallestador", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 10) return 0f;
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "ElBallestador", point, 1).Count
			});

			// ----- ElBallestador Constant (night, day >= 10, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant ElBallestador", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 10) return 0f;
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "ElBallestador", point, 1).Count
			});

			// ----- BallestadoraMusculosa (night, day >= 11, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("BallestadoraMusculosa", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 11) return 0f;
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "BallestadoraMusculosa", point, 1).Count
			});

			// ----- BallestadoraMusculosa Constant (night, day >= 11, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant BallestadoraMusculosa", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 11) return 0f;
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "BallestadoraMusculosa", point, 1).Count
			});

			// ----- ElMarihuanero (any time, day >= 4, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("ElMarihuanero", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 4) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "ElMarihuanero", point, 1).Count
			});

			// ----- ElMarihuanero Constant (any time, day >= 4, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant ElMarihuanero", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 4) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "ElMarihuanero", point, 1).Count
			});

			// ----- ElMarihuaneroMamon (night, day >= 6, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("ElMarihuaneroMamon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 6) return 0f;
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "ElMarihuaneroMamon", point, 1).Count
			});

			// ----- ElMarihuaneroMamon Constant (night, day >= 6, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant ElMarihuaneroMamon", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 6) return 0f;
					if (m_subsystemSky.SkyLightIntensity >= 0.2f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "ElMarihuaneroMamon", point, 1).Count
			});

			// ----- Aimep3 (any time, day >= 3, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Aimep3", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 3) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Aimep3", point, 1).Count
			});

			// ----- Aimep3 Constant (any time, day >= 3, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant Aimep3", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 3) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<GrassBlock>() &&
						contents != BlocksManager.GetBlockIndex<SandBlock>() &&
						contents != BlocksManager.GetBlockIndex<SnowBlock>() &&
						contents != BlocksManager.GetBlockIndex<DirtBlock>())
						return 0f;
					if (contents == BlocksManager.GetBlockIndex<WaterBlock>() ||
						contents == BlocksManager.GetBlockIndex<MagmaBlock>())
						return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "Aimep3", point, 1).Count
			});

			// ----- PirataNormal (day, day >= 4, beach proximity, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("PirataNormal", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 4) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<SandBlock>())
						return 0f;
					bool nearWater = false;
					for (int dx = -5; dx <= 5 && !nearWater; dx++)
					{
						for (int dz = -5; dz <= 5 && !nearWater; dz++)
						{
							int waterCheck = m_subsystemTerrain.Terrain.GetCellContentsFast(point.X + dx, point.Y - 1, point.Z + dz);
							if (waterCheck == BlocksManager.GetBlockIndex<WaterBlock>())
							{
								nearWater = true;
							}
						}
					}
					if (!nearWater) return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "PirataNormal", point, 1).Count
			});

			// ----- PirataNormal Constant (day, day >= 4, beach proximity, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant PirataNormal", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 4) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<SandBlock>())
						return 0f;
					bool nearWater = false;
					for (int dx = -5; dx <= 5 && !nearWater; dx++)
					{
						for (int dz = -5; dz <= 5 && !nearWater; dz++)
						{
							int waterCheck = m_subsystemTerrain.Terrain.GetCellContentsFast(point.X + dx, point.Y - 1, point.Z + dz);
							if (waterCheck == BlocksManager.GetBlockIndex<WaterBlock>())
							{
								nearWater = true;
							}
						}
					}
					if (!nearWater) return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "PirataNormal", point, 1).Count
			});

			// ----- PirataElite (day, day >= 11, beach proximity, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("PirataElite", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 11) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<SandBlock>())
						return 0f;
					bool nearWater = false;
					for (int dx = -5; dx <= 5 && !nearWater; dx++)
					{
						for (int dz = -5; dz <= 5 && !nearWater; dz++)
						{
							int waterCheck = m_subsystemTerrain.Terrain.GetCellContentsFast(point.X + dx, point.Y - 1, point.Z + dz);
							if (waterCheck == BlocksManager.GetBlockIndex<WaterBlock>())
							{
								nearWater = true;
							}
						}
					}
					if (!nearWater) return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "PirataElite", point, 1).Count
			});

			// ----- PirataElite Constant (day, day >= 11, beach proximity, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant PirataElite", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 11) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<SandBlock>())
						return 0f;
					bool nearWater = false;
					for (int dx = -5; dx <= 5 && !nearWater; dx++)
					{
						for (int dz = -5; dz <= 5 && !nearWater; dz++)
						{
							int waterCheck = m_subsystemTerrain.Terrain.GetCellContentsFast(point.X + dx, point.Y - 1, point.Z + dz);
							if (waterCheck == BlocksManager.GetBlockIndex<WaterBlock>())
							{
								nearWater = true;
							}
						}
					}
					if (!nearWater) return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "PirataElite", point, 1).Count
			});

			// ----- PirataHostilComerciante (day, day >= 15, beach proximity, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("PirataHostilComerciante", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 15) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<SandBlock>())
						return 0f;
					bool nearWater = false;
					for (int dx = -5; dx <= 5 && !nearWater; dx++)
					{
						for (int dz = -5; dz <= 5 && !nearWater; dz++)
						{
							int waterCheck = m_subsystemTerrain.Terrain.GetCellContentsFast(point.X + dx, point.Y - 1, point.Z + dz);
							if (waterCheck == BlocksManager.GetBlockIndex<WaterBlock>())
							{
								nearWater = true;
							}
						}
					}
					if (!nearWater) return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "PirataHostilComerciante", point, 1).Count
			});

			// ----- PirataHostilComerciante Constant (day, day >= 15, beach proximity, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant PirataHostilComerciante", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 15) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<SandBlock>())
						return 0f;
					bool nearWater = false;
					for (int dx = -5; dx <= 5 && !nearWater; dx++)
					{
						for (int dz = -5; dz <= 5 && !nearWater; dz++)
						{
							int waterCheck = m_subsystemTerrain.Terrain.GetCellContentsFast(point.X + dx, point.Y - 1, point.Z + dz);
							if (waterCheck == BlocksManager.GetBlockIndex<WaterBlock>())
							{
								nearWater = true;
							}
						}
					}
					if (!nearWater) return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "PirataHostilComerciante", point, 1).Count
			});

			// ----- CapitanPirata (day, day >= 25, beach proximity, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("CapitanPirata", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 25) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<SandBlock>())
						return 0f;
					bool nearWater = false;
					for (int dx = -5; dx <= 5 && !nearWater; dx++)
					{
						for (int dz = -5; dz <= 5 && !nearWater; dz++)
						{
							int waterCheck = m_subsystemTerrain.Terrain.GetCellContentsFast(point.X + dx, point.Y - 1, point.Z + dz);
							if (waterCheck == BlocksManager.GetBlockIndex<WaterBlock>())
							{
								nearWater = true;
							}
						}
					}
					if (!nearWater) return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "CapitanPirata", point, 1).Count
			});

			// ----- CapitanPirata Constant (day, day >= 25, beach proximity, constant spawn, 50% probability) -----
			m_creatureTypes.Add(new CreatureType("Constant CapitanPirata", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = (ct, point) =>
				{
					if (GetCurrentDay() < 25) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.5f) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int contents = Terrain.ExtractContents(below);
					if (contents != BlocksManager.GetBlockIndex<SandBlock>())
						return 0f;
					bool nearWater = false;
					for (int dx = -5; dx <= 5 && !nearWater; dx++)
					{
						for (int dz = -5; dz <= 5 && !nearWater; dz++)
						{
							int waterCheck = m_subsystemTerrain.Terrain.GetCellContentsFast(point.X + dx, point.Y - 1, point.Z + dz);
							if (waterCheck == BlocksManager.GetBlockIndex<WaterBlock>())
							{
								nearWater = true;
							}
						}
					}
					if (!nearWater) return 0f;
					if (m_random.Float(0f, 1f) > 0.5f) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, point) => SpawnCreatures(ct, "CapitanPirata", point, 1).Count
			});
		}

		// ====== SPAWN METHODS ======
		public virtual void SpawnRandomCreature()
		{
			if (CountCreatures(false) < m_totalLimit)
			{
				foreach (GameWidget gameWidget in m_subsystemViews.GameWidgets)
				{
					Vector2 v = new Vector2(gameWidget.ActiveCamera.ViewPosition.X, gameWidget.ActiveCamera.ViewPosition.Z);
					if (CountCreaturesInArea(v - new Vector2(68f), v + new Vector2(68f), false) >= 52)
						break;

					SpawnLocationType spawnLocationType = GetRandomSpawnLocationType();
					Point3? spawnPoint = GetRandomSpawnPoint(gameWidget.ActiveCamera, spawnLocationType);

					if (spawnPoint != null)
					{
						Vector2 c3 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) - new Vector2(16f);
						Vector2 c2 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) + new Vector2(16f);

						if (CountCreaturesInArea(c3, c2, false) >= 3)
							break;

						IEnumerable<CreatureType> enumerable = from c in m_creatureTypes
															   where c.SpawnLocationType == spawnLocationType && c.RandomSpawn
															   select c;
						IEnumerable<CreatureType> source = (enumerable as CreatureType[]) ?? enumerable.ToArray();

						IEnumerable<float> items = from c in source select CalculateSpawnSuitability(c, spawnPoint.Value);
						int randomWeightedItem = GetRandomWeightedItem(items);

						if (randomWeightedItem >= 0)
						{
							CreatureType creatureType = source.ElementAt(randomWeightedItem);
							creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						}
					}
				}
			}
		}

		public virtual void SpawnChunkCreatures(SpawnChunk chunk, int maxAttempts, bool constantSpawn)
		{
			int num = constantSpawn ? ((m_subsystemGameInfo.WorldSettings.GameMode >= GameMode.Challenging) ? m_totalLimitConstantChallenging : m_totalLimitConstant) : m_totalLimit;
			int num2 = constantSpawn ? m_areaLimitConstant : m_areaLimit;
			float v = (float)(constantSpawn ? m_areaRadiusConstant : m_areaRadius);
			int num3 = CountCreatures(constantSpawn);
			Vector2 c3 = new Vector2((float)(chunk.Point.X * 16), (float)(chunk.Point.Y * 16)) - new Vector2(v);
			Vector2 c2 = new Vector2((float)((chunk.Point.X + 1) * 16), (float)((chunk.Point.Y + 1) * 16)) + new Vector2(v);
			int num4 = CountCreaturesInArea(c3, c2, constantSpawn);

			for (int i = 0; i < maxAttempts; i++)
			{
				if (num3 >= num || num4 >= num2)
					break;

				SpawnLocationType spawnLocationType = GetRandomSpawnLocationType();
				Point3? spawnPoint = GetRandomChunkSpawnPoint(chunk, spawnLocationType);

				if (spawnPoint != null)
				{
					IEnumerable<CreatureType> enumerable = from c in m_creatureTypes
														   where c.SpawnLocationType == spawnLocationType && c.ConstantSpawn == constantSpawn
														   select c;
					IEnumerable<CreatureType> source = (enumerable as CreatureType[]) ?? enumerable.ToArray();

					IEnumerable<float> items = from c in source select CalculateSpawnSuitability(c, spawnPoint.Value);
					int randomWeightedItem = GetRandomWeightedItem(items);

					if (randomWeightedItem >= 0)
					{
						CreatureType creatureType = source.ElementAt(randomWeightedItem);
						int num5 = creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						num3 += num5;
						num4 += num5;
					}
				}
			}
		}

		public virtual List<Entity> SpawnCreatures(CreatureType creatureType, string templateName, Point3 point, int count)
		{
			List<Entity> list = new List<Entity>();
			int num = 0;
			while (count > 0 && num < 50)
			{
				Point3 spawnPoint = point;
				if (num > 0)
				{
					spawnPoint.X += m_random.Int(-8, 8);
					spawnPoint.Y += m_random.Int(-4, 8);
					spawnPoint.Z += m_random.Int(-8, 8);
				}

				Point3? point2 = ProcessSpawnPoint(spawnPoint, creatureType.SpawnLocationType);
				if (point2 != null && CalculateSpawnSuitability(creatureType, point2.Value) > 0f)
				{
					Vector3 position = new Vector3((float)point2.Value.X + m_random.Float(0.4f, 0.6f), (float)point2.Value.Y + 1.1f, (float)point2.Value.Z + m_random.Float(0.4f, 0.6f));
					Entity entity = SpawnCreature(templateName, position, creatureType.ConstantSpawn);
					if (entity != null)
					{
						list.Add(entity);
						count--;
					}
				}
				num++;
			}
			return list;
		}

		public virtual Entity SpawnCreature(string templateName, Vector3 position, bool constantSpawn)
		{
			Entity result;
			try
			{
				Entity entity = DatabaseManager.CreateEntity(Project, templateName, true);
				entity.FindComponent<ComponentBody>(true).Position = position;
				entity.FindComponent<ComponentBody>(true).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, m_random.Float(0f, 6.2831855f));
				entity.FindComponent<ComponentCreature>(true).ConstantSpawn = constantSpawn;
				Project.AddEntity(entity);
				result = entity;
			}
			catch (Exception value)
			{
				Log.Error($"Unable to spawn creature with template \"{templateName}\". Reason: {value}");
				result = null;
			}
			return result;
		}

		public virtual Point3? GetRandomChunkSpawnPoint(SpawnChunk chunk, SpawnLocationType spawnLocationType)
		{
			for (int i = 0; i < 5; i++)
			{
				int x = 16 * chunk.Point.X + m_random.Int(0, 15);
				int y = m_random.Int(10, 246);
				int z = 16 * chunk.Point.Y + m_random.Int(0, 15);
				Point3? result = ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
				if (result != null)
					return result;
			}
			return null;
		}

		public virtual Point3? GetRandomSpawnPoint(Camera camera, SpawnLocationType spawnLocationType)
		{
			for (int i = 0; i < 10; i++)
			{
				int x = Terrain.ToCell(camera.ViewPosition.X) + m_random.Sign() * m_random.Int(24, 48);
				int y = Math.Clamp(Terrain.ToCell(camera.ViewPosition.Y) + m_random.Int(-30, 30), 2, 254);
				int z = Terrain.ToCell(camera.ViewPosition.Z) + m_random.Sign() * m_random.Int(24, 48);
				Point3? result = ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
				if (result != null)
					return result;
			}
			return null;
		}

		public virtual Point3? ProcessSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
		{
			int x = spawnPoint.X;
			int num = Math.Clamp(spawnPoint.Y, 1, 254);
			int z = spawnPoint.Z;
			TerrainChunk chunkAtCell = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunkAtCell != null && chunkAtCell.State > TerrainChunkState.InvalidPropagatedLight)
			{
				for (int i = 0; i < 30; i++)
				{
					Point3 point = new Point3(x, num + i, z);
					if (TestSpawnPoint(point, spawnLocationType))
						return new Point3?(point);
					Point3 point2 = new Point3(x, num - i, z);
					if (TestSpawnPoint(point2, spawnLocationType))
						return new Point3?(point2);
				}
			}
			return null;
		}

		public virtual bool TestSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
		{
			int x = spawnPoint.X;
			int y = spawnPoint.Y;
			int z = spawnPoint.Z;
			if (y <= 3 || y >= 253)
				return false;

			switch (spawnLocationType)
			{
				case SpawnLocationType.Surface:
					{
						int cellLightFast = m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
						if (m_subsystemSky.SkyLightValue - cellLightFast > 3)
							return false;
						int cellValueFast = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
						int cellValueFast2 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
						int cellValueFast3 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);
						Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
						Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
						Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];
						return (block.IsCollidable_(cellValueFast) || block is WaterBlock) && !block2.IsCollidable_(cellValueFast2) && !(block2 is WaterBlock) && !block3.IsCollidable_(cellValueFast3) && !(block3 is WaterBlock);
					}
				case SpawnLocationType.Cave:
					{
						int cellLightFast2 = m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
						if (m_subsystemSky.SkyLightValue - cellLightFast2 < 5)
							return false;
						int cellValueFast4 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
						int cellValueFast5 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
						int cellValueFast6 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);
						Block block4 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast4)];
						Block block5 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast5)];
						Block block6 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast6)];
						return (block4.IsCollidable_(cellValueFast4) || block4 is WaterBlock) && !block5.IsCollidable_(cellValueFast5) && !(block5 is WaterBlock) && !block6.IsCollidable_(cellValueFast6) && !(block6 is WaterBlock);
					}
				case SpawnLocationType.Water:
					{
						int cellContentsFast = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
						int cellValueFast7 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);
						int cellValueFast8 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 2, z);
						Block block7 = BlocksManager.Blocks[Terrain.ExtractContents(cellContentsFast)];
						Block block8 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast7)];
						Block block9 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast8)];
						return block7 is WaterBlock && !block8.IsCollidable_(cellValueFast7) && !block9.IsCollidable_(cellValueFast8);
					}
				default:
					throw new InvalidOperationException("Unknown spawn location type.");
			}
		}

		public virtual float CalculateSpawnSuitability(CreatureType creatureType, Point3 spawnPoint)
		{
			float num = creatureType.SpawnSuitabilityFunction(creatureType, spawnPoint);
			if (CountCreatures(creatureType) > 8)
				num *= 0.25f;
			return num;
		}

		public virtual int CountCreatures(CreatureType creatureType)
		{
			int num = 0;
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body.Entity.ValuesDictionary.DatabaseObject.Name == creatureType.Name)
					num++;
			}
			return num;
		}

		public virtual int CountCreatures(bool constantSpawn)
		{
			int num = 0;
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature.ConstantSpawn == constantSpawn)
					num++;
			}
			return num;
		}

		public virtual int CountCreaturesInArea(Vector2 c1, Vector2 c2, bool constantSpawn)
		{
			int num = 0;
			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesInArea(c1, c2, m_componentBodies);
			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentBody body = m_componentBodies.Array[i];
				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature.ConstantSpawn == constantSpawn)
				{
					Vector3 position = body.Position;
					if (position.X >= c1.X && position.X <= c2.X && position.Z >= c1.Y && position.Z <= c2.Y)
						num++;
				}
			}

			Point2 point = Terrain.ToChunk(c1);
			Point2 point2 = Terrain.ToChunk(c2);
			for (int j = point.X; j <= point2.X; j++)
			{
				for (int k = point.Y; k <= point2.Y; k++)
				{
					SpawnChunk spawnChunk = m_subsystemSpawn.GetSpawnChunk(new Point2(j, k));
					if (spawnChunk != null)
					{
						foreach (SpawnEntityData spawnData in spawnChunk.SpawnsData)
						{
							if (spawnData.ConstantSpawn == constantSpawn)
							{
								Vector3 position2 = spawnData.Position;
								if (position2.X >= c1.X && position2.X <= c2.X && position2.Z >= c1.Y && position2.Z <= c2.Y)
									num++;
							}
						}
					}
				}
			}
			return num;
		}

		public virtual int GetRandomWeightedItem(IEnumerable<float> items)
		{
			float[] array = (items as float[]) ?? items.ToArray();
			float max = MathUtils.Max(array.Sum(), 1f);
			float num = m_random.Float(0f, max);
			int num2 = 0;
			foreach (float num3 in array)
			{
				if (num < num3)
					return num2;
				num -= num3;
				num2++;
			}
			return -1;
		}

		public virtual SpawnLocationType GetRandomSpawnLocationType()
		{
			float num = m_random.Float();
			if (num <= 0.3f)
				return SpawnLocationType.Surface;
			if (num <= 0.6f)
				return SpawnLocationType.Cave;
			return SpawnLocationType.Water;
		}

		// ====== CREATURE TYPE CLASS ======
		public class CreatureType
		{
			public string Name;
			public SpawnLocationType SpawnLocationType;
			public bool RandomSpawn;
			public bool ConstantSpawn;
			public Func<CreatureType, Point3, float> SpawnSuitabilityFunction;
			public Func<CreatureType, Point3, int> SpawnFunction;

			public CreatureType() { }

			public CreatureType(string name, SpawnLocationType spawnLocationType, bool randomSpawn, bool constantSpawn)
			{
				Name = name;
				SpawnLocationType = spawnLocationType;
				RandomSpawn = randomSpawn;
				ConstantSpawn = constantSpawn;
			}

			public override string ToString() => Name;
		}
	}
}
