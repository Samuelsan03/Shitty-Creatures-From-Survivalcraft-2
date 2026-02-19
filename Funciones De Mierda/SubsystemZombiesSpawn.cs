using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemZombiesSpawn : Subsystem, IUpdateable
	{
		public Dictionary<ComponentCreature, bool>.KeyCollection Creatures
		{
			get
			{
				return this.m_creatures.Keys;
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemSpawn m_subsystemSpawn;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemSky m_subsystemSky;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemGameWidgets m_subsystemViews;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private SubsystemTimeOfDay m_subsystemTimeOfDay;

		private Random m_random = new Random();
		private List<ZombieType> m_zombieTypes = new List<ZombieType>();
		private Dictionary<ComponentCreature, bool> m_creatures = new Dictionary<ComponentCreature, bool>();
		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		private List<SpawnChunk> m_newSpawnChunks = new List<SpawnChunk>();
		private List<SpawnChunk> m_spawnChunks = new List<SpawnChunk>();

		private static int m_totalLimit = 40;
		private static int m_areaLimit = 8;
		private static int m_areaRadius = 16;

		private double m_lastMoonDay;
		private bool m_hasSpawnedFastsThisMoon;

		public virtual void Update(float dt)
		{
			if (m_subsystemGreenNightSky == null || !m_subsystemGreenNightSky.IsGreenNightActive)
				return;

			if (this.m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
			{
				if (this.m_newSpawnChunks.Count > 0)
				{
					this.m_newSpawnChunks.RandomShuffle((int max) => this.m_random.Int(0, max - 1));
					foreach (SpawnChunk chunk in this.m_newSpawnChunks)
					{
						this.SpawnChunkZombies(chunk, 10, true);
					}
					this.m_newSpawnChunks.Clear();
				}
				if (this.m_spawnChunks.Count > 0)
				{
					this.m_spawnChunks.RandomShuffle((int max) => this.m_random.Int(0, max - 1));
					foreach (SpawnChunk chunk2 in this.m_spawnChunks)
					{
						this.SpawnChunkZombies(chunk2, 2, true);
					}
					this.m_spawnChunks.Clear();
				}

				float num = 60f;
				if (this.m_subsystemTime.PeriodicGameTimeEvent((double)num, 0.0))
				{
					this.SpawnRandomZombie();
				}

				// Spawn de normales siempre durante noche verde
				if (this.m_subsystemTime.PeriodicGameTimeEvent(6.0, 0.0))
				{
					this.SpawnNormalWave();
				}

				// Spawn de fasts solo en luna llena (0) o luna nueva (4)
				if (this.m_subsystemSky.MoonPhase == 0 || this.m_subsystemSky.MoonPhase == 4)
				{
					double currentDay = Math.Floor(this.m_subsystemTimeOfDay.Day);

					if (currentDay > this.m_lastMoonDay)
					{
						this.m_lastMoonDay = currentDay;
						this.m_hasSpawnedFastsThisMoon = false;
					}

					if (!this.m_hasSpawnedFastsThisMoon && this.m_subsystemTime.PeriodicGameTimeEvent(8.0, 2.0))
					{
						this.SpawnFastWave();
						this.m_hasSpawnedFastsThisMoon = true;
					}
				}
			}
		}

		private void SpawnNormalWave()
		{
			if (this.CountZombies() >= SubsystemZombiesSpawn.m_totalLimit) return;

			foreach (GameWidget gameWidget in this.m_subsystemViews.GameWidgets)
			{
				for (int i = 0; i < 2; i++)
				{
					Point3? spawnPoint = this.GetRandomSpawnPoint(gameWidget.ActiveCamera, SpawnLocationType.Surface);
					if (spawnPoint.HasValue)
					{
						string template = this.m_random.Bool(0.5f) ? "InfectedNormal1" : "InfectedNormal2";
						this.SpawnZombie(template, new Vector3(spawnPoint.Value.X + 0.5f, spawnPoint.Value.Y + 1.1f, spawnPoint.Value.Z + 0.5f));
					}
				}
			}
		}

		private void SpawnFastWave()
		{
			if (this.CountZombies() >= SubsystemZombiesSpawn.m_totalLimit) return;

			foreach (GameWidget gameWidget in this.m_subsystemViews.GameWidgets)
			{
				for (int i = 0; i < 3; i++)
				{
					Point3? spawnPoint = this.GetRandomSpawnPoint(gameWidget.ActiveCamera, SpawnLocationType.Surface);
					if (spawnPoint.HasValue)
					{
						string template = this.m_random.Bool(0.5f) ? "InfectedFast1" : "InfectedFast2";
						this.SpawnZombie(template, new Vector3(spawnPoint.Value.X + 0.5f, spawnPoint.Value.Y + 1.1f, spawnPoint.Value.Z + 0.5f));
					}
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
			this.m_subsystemGreenNightSky = base.Project.FindSubsystem<SubsystemGreenNightSky>(true);
			this.m_subsystemTimeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);

			this.m_lastMoonDay = Math.Floor(this.m_subsystemTimeOfDay.Day);
			this.m_hasSpawnedFastsThisMoon = false;

			this.InitializeZombieTypes();

			SubsystemSpawn subsystemSpawn = this.m_subsystemSpawn;
			subsystemSpawn.SpawningChunk = (Action<SpawnChunk>)Delegate.Combine(subsystemSpawn.SpawningChunk, new Action<SpawnChunk>(delegate (SpawnChunk chunk)
			{
				this.m_spawnChunks.Add(chunk);
				if (!chunk.IsSpawned)
				{
					this.m_newSpawnChunks.Add(chunk);
				}
			}));
		}

		public override void OnEntityAdded(Entity entity)
		{
			foreach (ComponentCreature key in entity.FindComponents<ComponentCreature>())
			{
				string name = entity.ValuesDictionary.DatabaseObject.Name;
				if (name == "InfectedNormal1" || name == "InfectedNormal2" ||
					name == "InfectedFast1" || name == "InfectedFast2")
				{
					this.m_creatures.Add(key, true);
				}
			}
		}

		public override void OnEntityRemoved(Entity entity)
		{
			foreach (ComponentCreature key in entity.FindComponents<ComponentCreature>())
			{
				string name = entity.ValuesDictionary.DatabaseObject.Name;
				if (name == "InfectedNormal1" || name == "InfectedNormal2" ||
					name == "InfectedFast1" || name == "InfectedFast2")
				{
					this.m_creatures.Remove(key);
				}
			}
		}

		public virtual void InitializeZombieTypes()
		{
			this.m_zombieTypes.Add(new ZombieType("InfectedNormal1", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (ZombieType _, Point3 point)
				{
					float num = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
					int num2 = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					int topHeight = this.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);

					if (num <= 10f || point.Y < topHeight)
					{
						return 0f;
					}

					if (num2 != 8 && num2 != 2 && num2 != 3 && num2 != 7)
					{
						return 0f;
					}

					return 2f;
				},
				SpawnFunction = ((ZombieType zombieType, Point3 point) => this.SpawnZombies(zombieType, "InfectedNormal1", point, 1).Count)
			});

			this.m_zombieTypes.Add(new ZombieType("InfectedNormal2", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (ZombieType _, Point3 point)
				{
					float num = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
					int num2 = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					int topHeight = this.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);

					if (num <= 10f || point.Y < topHeight)
					{
						return 0f;
					}

					if (num2 != 8 && num2 != 2 && num2 != 3 && num2 != 7)
					{
						return 0f;
					}

					return 2f;
				},
				SpawnFunction = ((ZombieType zombieType, Point3 point) => this.SpawnZombies(zombieType, "InfectedNormal2", point, 1).Count)
			});

			this.m_zombieTypes.Add(new ZombieType("InfectedFast1", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (ZombieType _, Point3 point)
				{
					float num = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
					int num2 = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					int topHeight = this.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);

					if (num <= 10f || point.Y < topHeight)
					{
						return 0f;
					}

					if (num2 != 8 && num2 != 2 && num2 != 3 && num2 != 7)
					{
						return 0f;
					}

					return 2f;
				},
				SpawnFunction = ((ZombieType zombieType, Point3 point) => this.SpawnZombies(zombieType, "InfectedFast1", point, 1).Count)
			});

			this.m_zombieTypes.Add(new ZombieType("InfectedFast2", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate (ZombieType _, Point3 point)
				{
					float num = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
					int num2 = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					int topHeight = this.m_subsystemTerrain.Terrain.GetTopHeight(point.X, point.Z);

					if (num <= 10f || point.Y < topHeight)
					{
						return 0f;
					}

					if (num2 != 8 && num2 != 2 && num2 != 3 && num2 != 7)
					{
						return 0f;
					}

					return 2f;
				},
				SpawnFunction = ((ZombieType zombieType, Point3 point) => this.SpawnZombies(zombieType, "InfectedFast2", point, 1).Count)
			});
		}

		public virtual void SpawnRandomZombie()
		{
			if (this.CountZombies() < SubsystemZombiesSpawn.m_totalLimit)
			{
				foreach (GameWidget gameWidget in this.m_subsystemViews.GameWidgets)
				{
					int num = 8;
					Vector2 v = new Vector2(gameWidget.ActiveCamera.ViewPosition.X, gameWidget.ActiveCamera.ViewPosition.Z);
					if (this.CountZombiesInArea(v - new Vector2(68f), v + new Vector2(68f)) >= num)
					{
						break;
					}

					Point3? spawnPoint = this.GetRandomSpawnPoint(gameWidget.ActiveCamera, SpawnLocationType.Surface);
					if (spawnPoint != null)
					{
						Vector2 c3 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) - new Vector2(16f);
						Vector2 c2 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) + new Vector2(16f);
						if (this.CountZombiesInArea(c3, c2) >= 3)
						{
							break;
						}

						IEnumerable<ZombieType> enumerable = from c in this.m_zombieTypes
															 where c.SpawnLocationType == SpawnLocationType.Surface && c.RandomSpawn
															 select c;

						IEnumerable<ZombieType> source = (enumerable as ZombieType[]) ?? enumerable.ToArray<ZombieType>();
						IEnumerable<float> items = from c in source
												   select this.CalculateSpawnSuitability(c, spawnPoint.Value);

						int randomWeightedItem = this.GetRandomWeightedItem(items);
						if (randomWeightedItem >= 0)
						{
							ZombieType zombieType = source.ElementAt(randomWeightedItem);
							zombieType.SpawnFunction(zombieType, spawnPoint.Value);
						}
					}
				}
			}
		}

		public virtual void SpawnChunkZombies(SpawnChunk chunk, int maxAttempts, bool constantSpawn)
		{
			int num = SubsystemZombiesSpawn.m_totalLimit;
			int num2 = SubsystemZombiesSpawn.m_areaLimit;
			float v = (float)SubsystemZombiesSpawn.m_areaRadius;

			int num3 = this.CountZombies();
			Vector2 c3 = new Vector2((float)(chunk.Point.X * 16), (float)(chunk.Point.Y * 16)) - new Vector2(v);
			Vector2 c2 = new Vector2((float)((chunk.Point.X + 1) * 16), (float)((chunk.Point.Y + 1) * 16)) + new Vector2(v);
			int num4 = this.CountZombiesInArea(c3, c2);

			for (int i = 0; i < maxAttempts; i++)
			{
				if (num3 >= num || num4 >= num2)
				{
					break;
				}

				Point3? spawnPoint = this.GetRandomChunkSpawnPoint(chunk, SpawnLocationType.Surface);
				if (spawnPoint != null)
				{
					IEnumerable<ZombieType> enumerable = from c in this.m_zombieTypes
														 where c.SpawnLocationType == SpawnLocationType.Surface && c.ConstantSpawn == constantSpawn
														 select c;

					IEnumerable<ZombieType> source = (enumerable as ZombieType[]) ?? enumerable.ToArray<ZombieType>();
					IEnumerable<float> items = from c in source
											   select this.CalculateSpawnSuitability(c, spawnPoint.Value);

					int randomWeightedItem = this.GetRandomWeightedItem(items);
					if (randomWeightedItem >= 0)
					{
						ZombieType zombieType = source.ElementAt(randomWeightedItem);
						int num5 = zombieType.SpawnFunction(zombieType, spawnPoint.Value);
						num3 += num5;
						num4 += num5;
					}
				}
			}
		}

		public virtual List<Entity> SpawnZombies(ZombieType zombieType, string templateName, Point3 point, int count)
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

				Point3? point2 = this.ProcessSpawnPoint(spawnPoint, SpawnLocationType.Surface);
				if (point2 != null && this.CalculateSpawnSuitability(zombieType, point2.Value) > 0f)
				{
					Vector3 position = new Vector3((float)point2.Value.X + this.m_random.Float(0.4f, 0.6f), (float)point2.Value.Y + 1.1f, (float)point2.Value.Z + this.m_random.Float(0.4f, 0.6f));
					Entity entity = this.SpawnZombie(templateName, position);
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

		public virtual Entity SpawnZombie(string templateName, Vector3 position)
		{
			Entity result;
			try
			{
				Entity entity = DatabaseManager.CreateEntity(base.Project, templateName, true);
				entity.FindComponent<ComponentBody>(true).Position = position;
				entity.FindComponent<ComponentBody>(true).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, this.m_random.Float(0f, 6.2831855f));
				base.Project.AddEntity(entity);
				result = entity;
			}
			catch (Exception value)
			{
				Log.Error("Unable to spawn zombie with template \"" + templateName + "\". Reason: " + value.ToString());
				result = null;
			}
			return result;
		}

		public virtual Point3? GetRandomChunkSpawnPoint(SpawnChunk chunk, SpawnLocationType spawnLocationType)
		{
			for (int i = 0; i < 5; i++)
			{
				int x = 16 * chunk.Point.X + this.m_random.Int(0, 15);
				int y = this.m_random.Int(10, 246);
				int z = 16 * chunk.Point.Y + this.m_random.Int(0, 15);
				Point3? result = this.ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
				if (result != null)
				{
					return result;
				}
			}
			return null;
		}

		public virtual Point3? GetRandomSpawnPoint(Camera camera, SpawnLocationType spawnLocationType)
		{
			for (int i = 0; i < 10; i++)
			{
				int x = Terrain.ToCell(camera.ViewPosition.X) + this.m_random.Sign() * this.m_random.Int(24, 48);
				int y = Math.Clamp(Terrain.ToCell(camera.ViewPosition.Y) + this.m_random.Int(-30, 30), 2, 254);
				int z = Terrain.ToCell(camera.ViewPosition.Z) + this.m_random.Sign() * this.m_random.Int(24, 48);
				Point3? result = this.ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
				if (result != null)
				{
					return result;
				}
			}
			return null;
		}

		public virtual Point3? ProcessSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
		{
			int x = spawnPoint.X;
			int num = Math.Clamp(spawnPoint.Y, 1, 254);
			int z = spawnPoint.Z;

			TerrainChunk chunkAtCell = this.m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunkAtCell != null && chunkAtCell.State > TerrainChunkState.InvalidPropagatedLight)
			{
				for (int i = 0; i < 30; i++)
				{
					Point3 point = new Point3(x, num + i, z);
					if (this.TestSpawnPoint(point, spawnLocationType))
					{
						return new Point3?(point);
					}
					Point3 point2 = new Point3(x, num - i, z);
					if (this.TestSpawnPoint(point2, spawnLocationType))
					{
						return new Point3?(point2);
					}
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
			{
				return false;
			}

			switch (spawnLocationType)
			{
				case SpawnLocationType.Surface:
					{
						int cellLightFast = this.m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
						if (this.m_subsystemSky.SkyLightValue - cellLightFast > 3)
						{
							return false;
						}

						int cellValueFast = this.m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
						int cellValueFast2 = this.m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
						int cellValueFast3 = this.m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

						Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
						Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
						Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];

						return (block.IsCollidable_(cellValueFast) || block is WaterBlock) &&
							   !block2.IsCollidable_(cellValueFast2) && !(block2 is WaterBlock) &&
							   !block3.IsCollidable_(cellValueFast3) && !(block3 is WaterBlock);
					}
				default:
					throw new InvalidOperationException("Unknown spawn location type.");
			}
		}

		public virtual float CalculateSpawnSuitability(ZombieType zombieType, Point3 spawnPoint)
		{
			float num = zombieType.SpawnSuitabilityFunction(zombieType, spawnPoint);
			if (this.CountZombies(zombieType) > 15)
			{
				num *= 0.25f;
			}
			return num;
		}

		public virtual int CountZombies(ZombieType zombieType)
		{
			int num = 0;
			foreach (ComponentBody body in this.m_subsystemBodies.Bodies)
			{
				if (body.Entity.ValuesDictionary.DatabaseObject.Name == zombieType.Name)
				{
					num++;
				}
			}
			return num;
		}

		public virtual int CountZombies()
		{
			int num = 0;
			foreach (ComponentBody body in this.m_subsystemBodies.Bodies)
			{
				string name = body.Entity.ValuesDictionary.DatabaseObject.Name;
				if (name == "InfectedNormal1" || name == "InfectedNormal2" ||
					name == "InfectedFast1" || name == "InfectedFast2")
				{
					num++;
				}
			}
			return num;
		}

		public virtual int CountZombiesInArea(Vector2 c1, Vector2 c2)
		{
			int num = 0;
			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesInArea(c1, c2, this.m_componentBodies);

			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentBody componentBody = this.m_componentBodies.Array[i];
				string name = componentBody.Entity.ValuesDictionary.DatabaseObject.Name;
				if (name == "InfectedNormal1" || name == "InfectedNormal2" ||
					name == "InfectedFast1" || name == "InfectedFast2")
				{
					Vector3 position = componentBody.Position;
					if (position.X >= c1.X && position.X <= c2.X && position.Z >= c1.Y && position.Z <= c2.Y)
					{
						num++;
					}
				}
			}
			return num;
		}

		public virtual int GetRandomWeightedItem(IEnumerable<float> items)
		{
			float[] array = (items as float[]) ?? items.ToArray<float>();
			float max = MathUtils.Max(array.Sum(), 1f);
			float num = this.m_random.Float(0f, max);
			int num2 = 0;
			foreach (float num3 in array)
			{
				if (num < num3)
				{
					return num2;
				}
				num -= num3;
				num2++;
			}
			return -1;
		}

		public class ZombieType
		{
			public ZombieType(string name, SpawnLocationType spawnLocationType, bool randomSpawn, bool constantSpawn)
			{
				this.Name = name;
				this.SpawnLocationType = spawnLocationType;
				this.RandomSpawn = randomSpawn;
				this.ConstantSpawn = constantSpawn;
			}

			public override string ToString()
			{
				return this.Name;
			}

			public string Name;
			public SpawnLocationType SpawnLocationType;
			public bool RandomSpawn;
			public bool ConstantSpawn;
			public Func<ZombieType, Point3, float> SpawnSuitabilityFunction;
			public Func<ZombieType, Point3, int> SpawnFunction;
		}
	}
}
