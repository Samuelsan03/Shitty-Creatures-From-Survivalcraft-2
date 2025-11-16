using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemNewCreatureSpawn : Subsystem, IUpdateable
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

		// Variables necesarias
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemSpawn m_subsystemSpawn;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemTime m_subsystemTime;
		public SubsystemSky m_subsystemSky;
		public SubsystemSeasons m_subsystemSeasons;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemGameWidgets m_subsystemViews;
		public Random m_random = new Random();
		public List<SubsystemCreatureSpawn.CreatureType> m_creatureTypes = new List<SubsystemCreatureSpawn.CreatureType>();
		public Dictionary<ComponentCreature, bool> m_creatures = new Dictionary<ComponentCreature, bool>();
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		public List<SpawnChunk> m_newSpawnChunks = new List<SpawnChunk>();
		public List<SpawnChunk> m_spawnChunks = new List<SpawnChunk>();

		// Límites para spawn constante
		public static int m_totalLimitConstant = 6;
		public static int m_areaLimitConstant = 4;
		public static int m_areaRadiusConstant = 42;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemSpawn = base.Project.FindSubsystem<SubsystemSpawn>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemSeasons = base.Project.FindSubsystem<SubsystemSeasons>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemViews = base.Project.FindSubsystem<SubsystemGameWidgets>(true);

			this.InitializeCreatureTypes();

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

		public virtual void Update(float dt)
		{
			if (this.m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
			{
				if (this.m_newSpawnChunks.Count > 0)
				{
					this.m_newSpawnChunks.RandomShuffle((int max) => this.m_random.Int(0, max - 1));
					foreach (SpawnChunk chunk in this.m_newSpawnChunks)
					{
						this.SpawnChunkCreatures(chunk, 10, false);
					}
					this.m_newSpawnChunks.Clear();
				}
				if (this.m_spawnChunks.Count > 0)
				{
					this.m_spawnChunks.RandomShuffle((int max) => this.m_random.Int(0, max - 1));
					foreach (SpawnChunk chunk2 in this.m_spawnChunks)
					{
						this.SpawnChunkCreatures(chunk2, 2, true); // ESTO ES IMPORTANTE - spawn constante
					}
					this.m_spawnChunks.Clear();
				}
				float num = (this.m_subsystemSeasons.Season == Season.Winter) ? 120f : 60f;
				if (this.m_subsystemTime.PeriodicGameTimeEvent((double)num, 2.0))
				{
					this.SpawnRandomCreature();
				}
			}
		}

		public override void OnEntityAdded(Entity entity)
		{
			foreach (ComponentCreature key in entity.FindComponents<ComponentCreature>())
			{
				this.m_creatures.Add(key, true);
			}
		}

		public override void OnEntityRemoved(Entity entity)
		{
			foreach (ComponentCreature key in entity.FindComponents<ComponentCreature>())
			{
				this.m_creatures.Remove(key);
			}
		}

		public virtual void InitializeCreatureTypes()
		{
			// SPAWN CONSTANTE de Naomi - Aparece cuando el jugador se acerca
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Naomi", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					// Spawnear de día en cualquier bioma
					int blockBelow = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					int cellLightFast = this.m_subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);

					// Aparece en cualquier bioma con buena luz
					if (cellLightFast >= 8 && cellLightFast <= 15 &&  // Luz de día normal
						(blockBelow == 18 || blockBelow == 7 || blockBelow == 6 || blockBelow == 62 ||
						 blockBelow == 8 || blockBelow == 2 || blockBelow == 3 || blockBelow == 72))
					{
						return 2.5f; // Mayor probabilidad
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Naomi", point, 1).Count)
			});

			// También puedes agregar spawn aleatorio normal si quieres
			this.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Naomi Spawn", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType _, Point3 point)
				{
					int blockBelow = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					int cellLightFast = this.m_subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);

					// Aparece en cualquier bioma con buena luz
					if (cellLightFast >= 8 && cellLightFast <= 15 &&  // Luz de día normal
						(blockBelow == 18 || blockBelow == 7 || blockBelow == 6 || blockBelow == 62 ||
						 blockBelow == 8 || blockBelow == 2 || blockBelow == 3 || blockBelow == 72))
					{
						return 1.8f;
					}
					return 0f;
				},
				SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "Naomi", point, 1).Count)
			});
		}

		// MÉTODOS FALTANTES - AGREGAR ESTOS:

		public virtual void SpawnRandomCreature()
		{
			if (this.CountCreatures(false) < 26) // m_totalLimit
			{
				foreach (GameWidget gameWidget in this.m_subsystemViews.GameWidgets)
				{
					int num = 52;
					Vector2 v = new Vector2(gameWidget.ActiveCamera.ViewPosition.X, gameWidget.ActiveCamera.ViewPosition.Z);
					if (this.CountCreaturesInArea(v - new Vector2(68f), v + new Vector2(68f), false) >= num)
					{
						break;
					}
					SpawnLocationType spawnLocationType = SpawnLocationType.Surface;
					Point3? spawnPoint = this.GetRandomSpawnPoint(gameWidget.ActiveCamera, spawnLocationType);
					if (spawnPoint != null)
					{
						Vector2 c3 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) - new Vector2(16f);
						Vector2 c2 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) + new Vector2(16f);
						if (this.CountCreaturesInArea(c3, c2, false) >= 3)
						{
							break;
						}
						IEnumerable<SubsystemCreatureSpawn.CreatureType> enumerable = from c in this.m_creatureTypes
																					  where c.SpawnLocationType == spawnLocationType && c.RandomSpawn
																					  select c;
						IEnumerable<SubsystemCreatureSpawn.CreatureType> source = enumerable.ToArray();
						IEnumerable<float> items = from c in source
												   select this.CalculateSpawnSuitability(c, spawnPoint.Value);
						int randomWeightedItem = this.GetRandomWeightedItem(items);
						if (randomWeightedItem >= 0)
						{
							SubsystemCreatureSpawn.CreatureType creatureType = source.ElementAt(randomWeightedItem);
							creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						}
					}
				}
			}
		}

		public virtual List<Entity> SpawnCreatures(SubsystemCreatureSpawn.CreatureType creatureType, string templateName, Point3 point, int count)
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
				if (point2 != null && this.CalculateSpawnSuitability(creatureType, point2.Value) > 0f)
				{
					Vector3 position = new Vector3((float)point2.Value.X + this.m_random.Float(0.4f, 0.6f), (float)point2.Value.Y + 1.1f, (float)point2.Value.Z + this.m_random.Float(0.4f, 0.6f));
					Entity entity = this.SpawnCreature(templateName, position, creatureType.ConstantSpawn);
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
			try
			{
				Entity entity = DatabaseManager.CreateEntity(base.Project, templateName, true);
				entity.FindComponent<ComponentBody>(true).Position = position;
				entity.FindComponent<ComponentBody>(true).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, this.m_random.Float(0f, 6.2831855f));
				entity.FindComponent<ComponentCreature>(true).ConstantSpawn = constantSpawn;
				base.Project.AddEntity(entity);
				return entity;
			}
			catch (Exception value)
			{
				Log.Error($"Unable to spawn creature with template \"{templateName}\". Reason: {value}");
				return null;
			}
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
						return (block.IsCollidable_(cellValueFast) || block is WaterBlock) && !block2.IsCollidable_(cellValueFast2) && !(block2 is WaterBlock) && !block3.IsCollidable_(cellValueFast3) && !(block3 is WaterBlock);
					}
				default:
					return false;
			}
		}

		public virtual float CalculateSpawnSuitability(SubsystemCreatureSpawn.CreatureType creatureType, Point3 spawnPoint)
		{
			float num = creatureType.SpawnSuitabilityFunction(creatureType, spawnPoint);
			return num;
		}

		// Método para spawn constante en chunks
		public virtual void SpawnChunkCreatures(SpawnChunk chunk, int maxAttempts, bool constantSpawn)
		{
			int num = constantSpawn ? m_totalLimitConstant : 26;
			int num2 = constantSpawn ? m_areaLimitConstant : 3;
			float v = (float)(constantSpawn ? m_areaRadiusConstant : 16);

			int num3 = this.CountCreatures(constantSpawn);
			Vector2 c3 = new Vector2((float)(chunk.Point.X * 16), (float)(chunk.Point.Y * 16)) - new Vector2(v);
			Vector2 c2 = new Vector2((float)((chunk.Point.X + 1) * 16), (float)((chunk.Point.Y + 1) * 16)) + new Vector2(v);
			int num4 = this.CountCreaturesInArea(c3, c2, constantSpawn);

			for (int i = 0; i < maxAttempts; i++)
			{
				if (num3 >= num || num4 >= num2)
				{
					break;
				}
				SpawnLocationType spawnLocationType = SpawnLocationType.Surface;
				Point3? spawnPoint = this.GetRandomChunkSpawnPoint(chunk, spawnLocationType);
				if (spawnPoint != null)
				{
					IEnumerable<SubsystemCreatureSpawn.CreatureType> enumerable = from c in this.m_creatureTypes
																				  where c.SpawnLocationType == spawnLocationType && c.ConstantSpawn == constantSpawn
																				  select c;

					var source = enumerable.ToArray();
					var items = source.Select(c => this.CalculateSpawnSuitability(c, spawnPoint.Value));
					int randomWeightedItem = this.GetRandomWeightedItem(items);

					if (randomWeightedItem >= 0)
					{
						SubsystemCreatureSpawn.CreatureType creatureType = source.ElementAt(randomWeightedItem);
						int num5 = creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						num3 += num5;
						num4 += num5;
					}
				}
			}
		}

		// Métodos auxiliares necesarios
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

		public virtual int CountCreatures(bool constantSpawn)
		{
			int num = 0;
			foreach (ComponentBody componentBody in this.m_subsystemBodies.Bodies)
			{
				ComponentCreature componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
				if (componentCreature != null && componentCreature.ConstantSpawn == constantSpawn)
				{
					num++;
				}
			}
			return num;
		}

		public virtual int CountCreaturesInArea(Vector2 c1, Vector2 c2, bool constantSpawn)
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
			float[] array = items.ToArray();
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
	}
}
