using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using static Game.SubsystemCreatureSpawn;

namespace Game
{
	/// <summary>
	/// Subsystem that handles spawning of Digimon creatures only.
	/// Optimized to prevent lag with dynamic limits, species-based population control,
	/// and incremental species counting for O(1) lookups.
	/// </summary>
	public class SubsystemDigimonSpawn : Subsystem, IUpdateable
	{
		public Dictionary<ComponentCreature, bool>.KeyCollection Creatures => m_creatures.Keys;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

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

				// Seasonal spawn interval: 180 seconds in winter, 90 seconds otherwise
				float interval = (this.m_subsystemSeasons.Season == Season.Winter) ? 180f : 90f;
				bool flag4 = this.m_subsystemTime.PeriodicGameTimeEvent(interval, 2.0);
				if (flag4)
				{
					this.SpawnRandomCreature();
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemSpawn = base.Project.FindSubsystem<SubsystemSpawn>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemViews = base.Project.FindSubsystem<SubsystemGameWidgets>(true);
			this.m_subsystemSeasons = base.Project.FindSubsystem<SubsystemSeasons>(true);
			this.InitializeCreatureTypes();

			SubsystemSpawn subsystemSpawn = this.m_subsystemSpawn;
			subsystemSpawn.SpawningChunk = (Action<SpawnChunk>)Delegate.Combine(
				subsystemSpawn.SpawningChunk,
				new Action<SpawnChunk>(delegate (SpawnChunk chunk)
				{
					this.m_spawnChunks.Add(chunk);
					bool flag = !chunk.IsSpawned;
					if (flag)
					{
						this.m_newSpawnChunks.Add(chunk);
					}
				}));
		}

		public override void OnEntityAdded(Entity entity)
		{
			foreach (ComponentCreature component in entity.FindComponents<ComponentCreature>())
			{
				this.m_creatures.Add(component, true);
				// Update species count
				string speciesName = component.Entity.ValuesDictionary.DatabaseObject.Name;
				if (this.m_speciesCount.ContainsKey(speciesName))
					this.m_speciesCount[speciesName]++;
				else
					this.m_speciesCount[speciesName] = 1;
			}
		}

		public override void OnEntityRemoved(Entity entity)
		{
			foreach (ComponentCreature component in entity.FindComponents<ComponentCreature>())
			{
				this.m_creatures.Remove(component);
				// Update species count
				string speciesName = component.Entity.ValuesDictionary.DatabaseObject.Name;
				if (this.m_speciesCount.ContainsKey(speciesName))
				{
					this.m_speciesCount[speciesName]--;
					if (this.m_speciesCount[speciesName] <= 0)
						this.m_speciesCount.Remove(speciesName);
				}
			}
		}

		/// <summary>
		/// Calculates spawn suitability with population control.
		/// Reduces suitability by 75% if more than 8 of the same species exist.
		/// </summary>
		private float CalculateSpawnSuitability(CreatureType creatureType, Point3 point)
		{
			float suitability = creatureType.SpawnSuitabilityFunction(creatureType, point);
			if (suitability <= 0f)
				return 0f;

			int currentCount = CountCreaturesOfType(creatureType.Name);
			if (currentCount > 8)
				suitability *= 0.25f;

			return suitability;
		}

		/// <summary>
		/// Counts how many creatures of a specific type exist in the world.
		/// Uses cached dictionary for O(1) lookup.
		/// </summary>
		private int CountCreaturesOfType(string typeName)
		{
			return this.m_speciesCount.TryGetValue(typeName, out int count) ? count : 0;
		}

		private void InitializeCreatureTypes()
		{
			// ----- Digimon: Veemon -----
			this.m_creatureTypes.Add(new CreatureType("Veemon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 3) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 0.5f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Veemon", p, 2).Count)
			});

			this.m_creatureTypes.Add(new CreatureType("Veemon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 3) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 0.5f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Veemon", p, 1).Count)
			});

			// ----- Digimon: Gaomon -----
			this.m_creatureTypes.Add(new CreatureType("Gaomon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 3) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 0.5f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Gaomon", p, 2).Count)
			});

			this.m_creatureTypes.Add(new CreatureType("Gaomon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 3) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 0.5f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Gaomon", p, 2).Count)
			});

			// ----- Digimon: Agumon -----
			this.m_creatureTypes.Add(new CreatureType("Agumon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 3) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 0.5f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Agumon", p, 2).Count)
			});

			this.m_creatureTypes.Add(new CreatureType("Agumon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 3) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 0.5f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Agumon", p, 2).Count)
			});

			// ----- Digimon: Shoutmon -----
			this.m_creatureTypes.Add(new CreatureType("Shoutmon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 5) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 1.0f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Shoutmon", p, 2).Count)
			});

			this.m_creatureTypes.Add(new CreatureType("Shoutmon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 5) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 0.5f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Shoutmon", p, 2).Count)
			});

			// ----- Digimon: BetelGammamon -----
			this.m_creatureTypes.Add(new CreatureType("BetelGammamon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 0) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					SubsystemSeasons seasons = base.Project.FindSubsystem<SubsystemSeasons>(true);
					if (seasons == null) return 0f;
					bool isAutumnOrWinter = (seasons.Season == Season.Autumn || seasons.Season == Season.Winter);
					if (!isAutumnOrWinter) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 1.0f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "BetelGammamon", p, 1).Count)
			});

			this.m_creatureTypes.Add(new CreatureType("BetelGammamon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 0) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					SubsystemSeasons seasons = base.Project.FindSubsystem<SubsystemSeasons>(true);
					if (seasons == null) return 0f;
					bool isAutumnOrWinter = (seasons.Season == Season.Autumn || seasons.Season == Season.Winter);
					if (!isAutumnOrWinter) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 0.5f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "BetelGammamon", p, 1).Count)
			});

			// ----- Digimon: Impmon -----
			this.m_creatureTypes.Add(new CreatureType("Impmon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 5) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity >= 0.3f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 1.0f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Impmon", p, 1).Count)
			});

			this.m_creatureTypes.Add(new CreatureType("Impmon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 5) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity >= 0.3f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 0.5f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Impmon", p, 1).Count)
			});

			// ----- Digimon: Hawkmon -----
			this.m_creatureTypes.Add(new CreatureType("Hawkmon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 1) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					SubsystemSeasons seasons = base.Project.FindSubsystem<SubsystemSeasons>(true);
					if (seasons == null) return 0f;
					bool isSpring = (seasons.Season == Season.Spring);
					if (!isSpring) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if ((groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8) && point.Y > 70)
						return 1.0f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Hawkmon", p, 1).Count)
			});

			this.m_creatureTypes.Add(new CreatureType("Hawkmon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 1) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					SubsystemSeasons seasons = base.Project.FindSubsystem<SubsystemSeasons>(true);
					if (seasons == null) return 0f;
					bool isSpring = (seasons.Season == Season.Spring);
					if (!isSpring) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if ((groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8) && point.Y > 70)
						return 0.5f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Hawkmon", p, 1).Count)
			});

			// ----- Digimon: Gabumon -----
			this.m_creatureTypes.Add(new CreatureType("Gabumon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 2) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity >= 0.3f) return 0f;

					SubsystemSeasons seasons = base.Project.FindSubsystem<SubsystemSeasons>(true);
					if (seasons == null) return 0f;
					bool isWinter = (seasons.Season == Season.Winter);
					if (!isWinter) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 62 || groundBlock == 2 || groundBlock == 3)
						return 1.0f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Gabumon", p, 1).Count)
			});

			this.m_creatureTypes.Add(new CreatureType("Gabumon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 2) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity >= 0.3f) return 0f;

					SubsystemSeasons seasons = base.Project.FindSubsystem<SubsystemSeasons>(true);
					if (seasons == null) return 0f;
					bool isWinter = (seasons.Season == Season.Winter);
					if (!isWinter) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 62 || groundBlock == 2 || groundBlock == 3)
						return 0.5f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Gabumon", p, 1).Count)
			});

			// ----- Digimon: Guilmon -----
			this.m_creatureTypes.Add(new CreatureType("Guilmon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 10) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 1.0f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Guilmon", p, 1).Count)
			});

			this.m_creatureTypes.Add(new CreatureType("Guilmon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 10) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 0.5f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Guilmon", p, 1).Count)
			});

			// ----- Digimon: Gumdramon -----
			this.m_creatureTypes.Add(new CreatureType("Gumdramon", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 10) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 1.0f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Gumdramon", p, 1).Count)
			});

			this.m_creatureTypes.Add(new CreatureType("Gumdramon Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType creatureType, Point3 point)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z);
					int blockAbove = Terrain.ExtractContents(cellValue);

					int cellValueHead = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z);
					int blockHead = Terrain.ExtractContents(cellValueHead);

					if (blockAbove == 18 || blockAbove == 92 || blockHead == 18 || blockHead == 92)
						return 0f;

					SubsystemTimeOfDay timeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
					int currentDay = 0;
					if (timeOfDay != null) currentDay = (int)Math.Floor(timeOfDay.Day);
					if (currentDay < 10) return 0f;

					if (this.m_subsystemSky.SkyLightIntensity <= 0.5f) return 0f;

					int cellValueGround = this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z);
					int groundBlock = Terrain.ExtractContents(cellValueGround);

					if (groundBlock == 2 || groundBlock == 3 || groundBlock == 7 || groundBlock == 8)
						return 0.5f;

					return 0f;
				},
				SpawnFunction = ((CreatureType ct, Point3 p) => this.SpawnCreatures(ct, "Gumdramon", p, 1).Count)
			});
		}

		// ----------------------------------------------------------------------
		// Spawning methods with optimized limits and population control
		// ----------------------------------------------------------------------

		private void SpawnRandomCreature()
		{
			int totalLimit = 18; // Reduced from 24
			if (this.CountCreatures(false) >= totalLimit)
				return;

			// Choose a random game widget instead of iterating all
			var widgets = this.m_subsystemViews.GameWidgets;
			if (widgets.Count == 0) return;
			int index = this.m_random.Int(0, widgets.Count - 1);
			GameWidget gameWidget = widgets[index];

			int areaLimit = 3;
			Vector2 v = new Vector2(gameWidget.ActiveCamera.ViewPosition.X, gameWidget.ActiveCamera.ViewPosition.Z);
			if (this.CountCreaturesInArea(v - new Vector2(60f), v + new Vector2(60f), false) >= areaLimit)
				return;

			SpawnLocationType spawnLocationType = this.GetRandomSpawnLocationType();
			Point3? spawnPoint = this.GetRandomSpawnPoint(gameWidget.ActiveCamera, spawnLocationType);
			if (spawnPoint != null)
			{
				Vector2 c3 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) - new Vector2(16f);
				Vector2 c2 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) + new Vector2(16f);
				if (this.CountCreaturesInArea(c3, c2, false) >= 3)
					return;

				IEnumerable<CreatureType> source = from c in this.m_creatureTypes
												   where c.SpawnLocationType == spawnLocationType && c.RandomSpawn
												   select c;
				IEnumerable<float> items = from c in source
										   select this.CalculateSpawnSuitability(c, spawnPoint.Value);
				int randomWeightedItem = this.GetRandomWeightedItem(items);
				if (randomWeightedItem >= 0)
				{
					CreatureType creatureType = source.ElementAt(randomWeightedItem);
					creatureType.SpawnFunction(creatureType, spawnPoint.Value);
				}
			}
		}

		private void SpawnChunkCreatures(SpawnChunk chunk, int maxAttempts, bool constantSpawn)
		{
			// Dynamic limits based on game mode for constant spawn
			int totalLimit;
			if (constantSpawn)
			{
				totalLimit = (this.m_subsystemGameInfo.WorldSettings.GameMode >= GameMode.Challenging) ? 10 : 5; // Reduced from 12/6
			}
			else
			{
				totalLimit = 18; // Reduced from 24
			}

			int areaLimit = constantSpawn ? 3 : 2; // Reduced from 4/3
			float radius = constantSpawn ? 42f : 16f;

			int currentTotal = this.CountCreatures(constantSpawn);
			Vector2 c3 = new Vector2((float)(chunk.Point.X * 16), (float)(chunk.Point.Y * 16)) - new Vector2(radius);
			Vector2 c2 = new Vector2((float)((chunk.Point.X + 1) * 16), (float)((chunk.Point.Y + 1) * 16)) + new Vector2(radius);
			int areaCount = this.CountCreaturesInArea(c3, c2, constantSpawn);

			for (int i = 0; i < maxAttempts; i++)
			{
				if (currentTotal >= totalLimit || areaCount >= areaLimit)
					break;

				SpawnLocationType spawnLocationType = this.GetRandomSpawnLocationType();
				Point3? spawnPoint = this.GetRandomChunkSpawnPoint(chunk, spawnLocationType);
				if (spawnPoint != null)
				{
					IEnumerable<CreatureType> source = from c in this.m_creatureTypes
													   where c.SpawnLocationType == spawnLocationType && c.ConstantSpawn == constantSpawn
													   select c;
					IEnumerable<float> items = from c in source
											   select this.CalculateSpawnSuitability(c, spawnPoint.Value);
					int randomWeightedItem = this.GetRandomWeightedItem(items);
					if (randomWeightedItem >= 0)
					{
						CreatureType creatureType = source.ElementAt(randomWeightedItem);
						int spawned = creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						currentTotal += spawned;
						areaCount += spawned;
					}
				}
			}
		}

		private List<Entity> SpawnCreatures(CreatureType creatureType, string templateName, Point3 point, int count)
		{
			List<Entity> list = new List<Entity>();
			int num = 0;
			while (count > 0 && num < 50)
			{
				Point3 spawnPoint = point;
				if (num > 0)
				{
					spawnPoint.X += this.m_random.Int(-8, 8);
					spawnPoint.Y += this.m_random.Int(-4, 8);
					spawnPoint.Z += this.m_random.Int(-8, 8);
				}
				Point3? point2 = this.ProcessSpawnPoint(spawnPoint, creatureType.SpawnLocationType);
				bool flag = point2 != null && this.CalculateSpawnSuitability(creatureType, point2.Value) > 0f;
				if (flag)
				{
					Vector3 position = new Vector3((float)point2.Value.X + this.m_random.Float(0.4f, 0.6f), (float)point2.Value.Y + 1.1f, (float)point2.Value.Z + this.m_random.Float(0.4f, 0.6f));
					Entity entity = this.SpawnCreature(templateName, position, creatureType.ConstantSpawn);
					bool flag2 = entity != null;
					if (flag2)
					{
						list.Add(entity);
						count--;
					}
				}
				num++;
			}
			return list;
		}

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

		private Point3? GetRandomChunkSpawnPoint(SpawnChunk chunk, SpawnLocationType spawnLocationType)
		{
			for (int i = 0; i < 5; i++)
			{
				int x = 16 * chunk.Point.X + this.m_random.Int(0, 15);
				int y = this.m_random.Int(10, 246);
				int z = 16 * chunk.Point.Y + this.m_random.Int(0, 15);
				Point3? result = this.ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
				if (result != null) return result;
			}
			return null;
		}

		private Point3? GetRandomSpawnPoint(Camera camera, SpawnLocationType spawnLocationType)
		{
			for (int i = 0; i < 10; i++)
			{
				int x = Terrain.ToCell(camera.ViewPosition.X) + this.m_random.Sign() * this.m_random.Int(20, 40);
				int y = MathUtils.Clamp(Terrain.ToCell(camera.ViewPosition.Y) + this.m_random.Int(-30, 30), 2, 254);
				int z = Terrain.ToCell(camera.ViewPosition.Z) + this.m_random.Sign() * this.m_random.Int(20, 40);
				Point3? result = this.ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
				if (result != null) return result;
			}
			return null;
		}

		private Point3? ProcessSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
		{
			int x = spawnPoint.X;
			int num = MathUtils.Clamp(spawnPoint.Y, 1, 254);
			int z = spawnPoint.Z;
			TerrainChunk chunkAtCell = this.m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunkAtCell != null && chunkAtCell.State > TerrainChunkState.InvalidPropagatedLight)
			{
				for (int i = 0; i < 30; i++)
				{
					Point3 point = new Point3(x, num + i, z);
					if (this.TestSpawnPoint(point, spawnLocationType)) return new Point3?(point);

					Point3 point2 = new Point3(x, num - i, z);
					if (this.TestSpawnPoint(point2, spawnLocationType)) return new Point3?(point2);
				}
			}
			return null;
		}

		private bool TestSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
		{
			int x = spawnPoint.X;
			int y = spawnPoint.Y;
			int z = spawnPoint.Z;
			if (y <= 3 || y >= 253) return false;

			switch (spawnLocationType)
			{
				case SpawnLocationType.Surface:
					{
						int cellLightFast = this.m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
						if (this.m_subsystemSky.SkyLightValue - cellLightFast > 3) return false;

						int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
						int cellValue2 = this.m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
						int cellValue3 = this.m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

						Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValue)];
						Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValue2)];
						Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValue3)];

						return (block.IsCollidable || block is WaterBlock) &&
							   !block2.IsCollidable && !(block2 is WaterBlock) &&
							   !block3.IsCollidable && !(block3 is WaterBlock);
					}
				case SpawnLocationType.Cave:
					{
						int cellLightFast = this.m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
						if (this.m_subsystemSky.SkyLightValue - cellLightFast < 5) return false;

						int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
						int cellValue2 = this.m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
						int cellValue3 = this.m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

						Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValue)];
						Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValue2)];
						Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValue3)];

						return (block.IsCollidable || block is WaterBlock) &&
							   !block2.IsCollidable && !(block2 is WaterBlock) &&
							   !block3.IsCollidable && !(block3 is WaterBlock);
					}
				case SpawnLocationType.Water:
					{
						int cellContents = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
						int cellValue = this.m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);
						int cellValue2 = this.m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 2, z);

						Block block = BlocksManager.Blocks[cellContents];
						Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValue)];
						Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValue2)];

						return block is WaterBlock && !block2.IsCollidable && !block3.IsCollidable;
					}
				default:
					throw new InvalidOperationException("Unknown spawn location type.");
			}
		}

		private int CountCreatures(bool constantSpawn)
		{
			int num = 0;
			foreach (ComponentBody componentBody in this.m_subsystemBodies.Bodies)
			{
				ComponentCreature componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
				if (componentCreature != null && componentCreature.ConstantSpawn == constantSpawn) num++;
			}
			return num;
		}

		private int CountCreaturesInArea(Vector2 c1, Vector2 c2, bool constantSpawn)
		{
			int num = 0;
			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesInArea(c1, c2, this.m_componentBodies);
			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentBody componentBody = this.m_componentBodies.Array[i];
				ComponentCreature componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
				if (componentCreature != null && componentCreature.ConstantSpawn == constantSpawn)
				{
					Vector3 position = componentBody.Position;
					if (position.X >= c1.X && position.X <= c2.X && position.Z >= c1.Y && position.Z <= c2.Y) num++;
				}
			}

			Point2 point = Terrain.ToChunk(c1);
			Point2 point2 = Terrain.ToChunk(c2);
			for (int j = point.X; j <= point2.X; j++)
			{
				for (int k = point.Y; k <= point2.Y; k++)
				{
					SpawnChunk spawnChunk = this.m_subsystemSpawn.GetSpawnChunk(new Point2(j, k));
					if (spawnChunk != null)
					{
						foreach (SpawnEntityData spawnEntityData in spawnChunk.SpawnsData)
						{
							if (spawnEntityData.ConstantSpawn == constantSpawn)
							{
								Vector3 position2 = spawnEntityData.Position;
								if (position2.X >= c1.X && position2.X <= c2.X && position2.Z >= c1.Y && position2.Z <= c2.Y) num++;
							}
						}
					}
				}
			}
			return num;
		}

		private int GetRandomWeightedItem(IEnumerable<float> items)
		{
			float max = MathUtils.Max(items.Sum(), 1f);
			float num = this.m_random.Float(0f, max);
			int num2 = 0;
			foreach (float num3 in items)
			{
				if (num < num3) return num2;
				num -= num3;
				num2++;
			}
			return -1;
		}

		public SpawnLocationType GetRandomSpawnLocationType()
		{
			return SubsystemDigimonSpawn.m_spawnLocations[this.m_random.Int(0, SubsystemDigimonSpawn.m_spawnLocations.Length - 1)];
		}

		// Fields
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemSpawn m_subsystemSpawn;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemSky m_subsystemSky;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemGameWidgets m_subsystemViews;
		private SubsystemSeasons m_subsystemSeasons;
		private Random m_random = new Random();
		private List<CreatureType> m_creatureTypes = new List<CreatureType>();
		private Dictionary<ComponentCreature, bool> m_creatures = new Dictionary<ComponentCreature, bool>();
		private Dictionary<string, int> m_speciesCount = new Dictionary<string, int>(); // Cached species counts
		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		private List<SpawnChunk> m_newSpawnChunks = new List<SpawnChunk>();
		private List<SpawnChunk> m_spawnChunks = new List<SpawnChunk>();
		private static SpawnLocationType[] m_spawnLocations = EnumUtils.GetEnumValues(typeof(SpawnLocationType)).Cast<SpawnLocationType>().ToArray<SpawnLocationType>();

		// Nested class
		private class CreatureType
		{
			public CreatureType(string name, SpawnLocationType spawnLocationType, bool randomSpawn, bool constantSpawn)
			{
				this.Name = name;
				this.SpawnLocationType = spawnLocationType;
				this.RandomSpawn = randomSpawn;
				this.ConstantSpawn = constantSpawn;
			}

			public override string ToString() => this.Name;

			public string Name;
			public SpawnLocationType SpawnLocationType;
			public bool RandomSpawn;
			public bool ConstantSpawn;
			public Func<CreatureType, Point3, float> SpawnSuitabilityFunction;
			public Func<CreatureType, Point3, int> SpawnFunction;
		}
	}
}
