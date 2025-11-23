using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x0200000B RID: 11
	[NullableContext(1)]
	[Nullable(0)]
	public class SubsystemModCreatureSpawn : Subsystem, IUpdateable
	{
		// Token: 0x17000016 RID: 22
		// (get) Token: 0x0600006C RID: 108 RVA: 0x00006D5B File Offset: 0x00004F5B
		public Dictionary<ComponentCreature, bool>.KeyCollection Creatures
		{
			get
			{
				return this.m_creatures.Keys;
			}
		}

		// Token: 0x17000017 RID: 23
		// (get) Token: 0x0600006D RID: 109 RVA: 0x00006D68 File Offset: 0x00004F68
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x0600006E RID: 110 RVA: 0x00006D6C File Offset: 0x00004F6C
		public void Update(float dt)
		{
			bool flag = this.m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode > EnvironmentBehaviorMode.Living;
			if (!flag)
			{
				bool flag2 = this.m_newSpawnChunks.Count > 0;
				if (flag2)
				{
					this.m_newSpawnChunks.RandomShuffle((int max) => this.m_random.Int(0, max - 1));
					foreach (SpawnChunk newSpawnChunk in this.m_newSpawnChunks)
					{
						this.SpawnChunkCreatures(newSpawnChunk, 10, false);
					}
					this.m_newSpawnChunks.Clear();
				}
				bool flag3 = this.m_spawnChunks.Count > 0;
				if (flag3)
				{
					this.m_spawnChunks.RandomShuffle((int max) => this.m_random.Int(0, max - 1));
					foreach (SpawnChunk spawnChunk in this.m_spawnChunks)
					{
						this.SpawnChunkCreatures(spawnChunk, 2, true);
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

		// Token: 0x0600006F RID: 111 RVA: 0x00006ED0 File Offset: 0x000050D0
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
			subsystemSpawn.SpawningChunk = (Action<SpawnChunk>)Delegate.Combine(subsystemSpawn.SpawningChunk, new Action<SpawnChunk>(delegate(SpawnChunk chunk)
			{
				this.m_spawnChunks.Add(chunk);
				bool flag = !chunk.IsSpawned;
				if (flag)
				{
					this.m_newSpawnChunks.Add(chunk);
				}
			}));
		}

		// Token: 0x06000070 RID: 112 RVA: 0x00006F90 File Offset: 0x00005190
		public override void OnEntityAdded(Entity entity)
		{
			foreach (ComponentCreature item in entity.FindComponents<ComponentCreature>())
			{
				this.m_creatures.Add(item, true);
			}
		}

		// Token: 0x06000071 RID: 113 RVA: 0x00006FF4 File Offset: 0x000051F4
		public override void OnEntityRemoved(Entity entity)
		{
			foreach (ComponentCreature item in entity.FindComponents<ComponentCreature>())
			{
				this.m_creatures.Remove(item);
			}
		}

		// Token: 0x06000072 RID: 114 RVA: 0x00007058 File Offset: 0x00005258
		private void InitializeCreatureTypes()
		{
			this.m_creatureTypes.Add(new SubsystemModCreatureSpawn.CreatureType("Naomi", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = delegate(SubsystemModCreatureSpawn.CreatureType creatureType, Point3 point)
				{
					float oceanDistance = this.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
					int blockBelow = Terrain.ExtractContents(this.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					bool flag = oceanDistance > 0f && blockBelow != 18 && BlocksManager.Blocks[blockBelow].IsCollidable;
					float result;
					if (flag)
					{
						result = 0.8f;
					}
					else
					{
						result = 0f;
					}
					return result;
				},
				SpawnFunction = ((SubsystemModCreatureSpawn.CreatureType creatureType, Point3 point) => this.SpawnCreatures(creatureType, "Naomi", point, 1).Count)
			});
		}

		// Token: 0x06000073 RID: 115 RVA: 0x00007098 File Offset: 0x00005298
		private void SpawnRandomCreature()
		{
			bool flag = this.CountCreatures(false) >= 24;
			if (!flag)
			{
				foreach (GameWidget gameWidget in this.m_subsystemViews.GameWidgets)
				{
					int num = 48;
					Vector2 vector = new Vector2(gameWidget.ActiveCamera.ViewPosition.X, gameWidget.ActiveCamera.ViewPosition.Z);
					bool flag2 = this.CountCreaturesInArea(vector - new Vector2(60f), vector + new Vector2(60f), false) >= num;
					if (flag2)
					{
						break;
					}
					SpawnLocationType spawnLocationType = this.GetRandomSpawnLocationType();
					Point3? spawnPoint = this.GetRandomSpawnPoint(gameWidget.ActiveCamera, spawnLocationType);
					bool flag3 = spawnPoint != null;
					if (flag3)
					{
						Vector2 c2 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) - new Vector2(16f);
						Vector2 c3 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) + new Vector2(16f);
						bool flag4 = this.CountCreaturesInArea(c2, c3, false) >= 3;
						if (flag4)
						{
							break;
						}
						IEnumerable<SubsystemModCreatureSpawn.CreatureType> source = from c in this.m_creatureTypes
						where c.SpawnLocationType == spawnLocationType && c.RandomSpawn
						select c;
						IEnumerable<float> items = from c in source
						select c.SpawnSuitabilityFunction(c, spawnPoint.Value);
						int randomWeightedItem = this.GetRandomWeightedItem(items);
						bool flag5 = randomWeightedItem >= 0;
						if (flag5)
						{
							SubsystemModCreatureSpawn.CreatureType creatureType = source.ElementAt(randomWeightedItem);
							creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						}
					}
				}
			}
		}

		// Token: 0x06000074 RID: 116 RVA: 0x000072C8 File Offset: 0x000054C8
		private void SpawnChunkCreatures(SpawnChunk chunk, int maxAttempts, bool constantSpawn)
		{
			int num = constantSpawn ? 18 : 24;
			int num2 = constantSpawn ? 4 : 3;
			float v = (float)(constantSpawn ? 42 : 16);
			int num3 = this.CountCreatures(constantSpawn);
			Vector2 c2 = new Vector2((float)(chunk.Point.X * 16), (float)(chunk.Point.Y * 16)) - new Vector2(v);
			Vector2 c3 = new Vector2((float)((chunk.Point.X + 1) * 16), (float)((chunk.Point.Y + 1) * 16)) + new Vector2(v);
			int num4 = this.CountCreaturesInArea(c2, c3, constantSpawn);
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
					IEnumerable<SubsystemModCreatureSpawn.CreatureType> source = from c in this.m_creatureTypes
					where c.SpawnLocationType == spawnLocationType && c.ConstantSpawn == constantSpawn
					select c;
					IEnumerable<float> items = from c in source
					select c.SpawnSuitabilityFunction(c, spawnPoint.Value);
					int randomWeightedItem = this.GetRandomWeightedItem(items);
					bool flag3 = randomWeightedItem >= 0;
					if (flag3)
					{
						SubsystemModCreatureSpawn.CreatureType creatureType = source.ElementAt(randomWeightedItem);
						int num5 = creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						num3 += num5;
						num4 += num5;
					}
				}
			}
		}

		// Token: 0x06000075 RID: 117 RVA: 0x00007494 File Offset: 0x00005694
		private List<Entity> SpawnCreatures(SubsystemModCreatureSpawn.CreatureType creatureType, string templateName, Point3 point, int count)
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

		// Token: 0x06000076 RID: 118 RVA: 0x000075F8 File Offset: 0x000057F8
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

		// Token: 0x06000077 RID: 119 RVA: 0x000076A4 File Offset: 0x000058A4
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

		// Token: 0x06000078 RID: 120 RVA: 0x00007750 File Offset: 0x00005950
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

		// Token: 0x06000079 RID: 121 RVA: 0x00007834 File Offset: 0x00005A34
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

		// Token: 0x0600007A RID: 122 RVA: 0x00007908 File Offset: 0x00005B08
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
					int cellLightFast2 = this.m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
					bool flag2 = this.m_subsystemSky.SkyLightValue - cellLightFast2 > 3;
					if (flag2)
					{
						result = false;
					}
					else
					{
						int cellContentsFast7 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
						int cellContentsFast8 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
						int cellContentsFast9 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
						Block block7 = BlocksManager.Blocks[cellContentsFast7];
						Block block8 = BlocksManager.Blocks[cellContentsFast8];
						Block block9 = BlocksManager.Blocks[cellContentsFast9];
						result = ((block7.IsCollidable || block7 is WaterBlock) && !block8.IsCollidable && !(block8 is WaterBlock) && !block9.IsCollidable && !(block9 is WaterBlock));
					}
					break;
				}
				case SpawnLocationType.Cave:
				{
					int cellLightFast3 = this.m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
					bool flag3 = this.m_subsystemSky.SkyLightValue - cellLightFast3 < 5;
					if (flag3)
					{
						result = false;
					}
					else
					{
						int cellContentsFast10 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
						int cellContentsFast11 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
						int cellContentsFast12 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
						Block block10 = BlocksManager.Blocks[cellContentsFast10];
						Block block11 = BlocksManager.Blocks[cellContentsFast11];
						Block block12 = BlocksManager.Blocks[cellContentsFast12];
						result = ((block10.IsCollidable || block10 is WaterBlock) && !block11.IsCollidable && !(block11 is WaterBlock) && !block12.IsCollidable && !(block12 is WaterBlock));
					}
					break;
				}
				case SpawnLocationType.Water:
				{
					int cellContentsFast13 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int cellContentsFast14 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					int cellContentsFast15 = this.m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 2, z);
					Block block13 = BlocksManager.Blocks[cellContentsFast13];
					Block block14 = BlocksManager.Blocks[cellContentsFast14];
					Block block15 = BlocksManager.Blocks[cellContentsFast15];
					result = (block13 is WaterBlock && !block14.IsCollidable && !block15.IsCollidable);
					break;
				}
				default:
					throw new InvalidOperationException("Unknown spawn location type.");
				}
			}
			return result;
		}

		// Token: 0x0600007B RID: 123 RVA: 0x00007BB4 File Offset: 0x00005DB4
		private int CountCreatures(bool constantSpawn)
		{
			int num = 0;
			foreach (ComponentBody body in this.m_subsystemBodies.Bodies)
			{
				ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
				bool flag = componentCreature != null && componentCreature.ConstantSpawn == constantSpawn;
				if (flag)
				{
					num++;
				}
			}
			return num;
		}

		// Token: 0x0600007C RID: 124 RVA: 0x00007C3C File Offset: 0x00005E3C
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
						foreach (SpawnEntityData spawnsDatum in spawnChunk.SpawnsData)
						{
							bool flag4 = spawnsDatum.ConstantSpawn == constantSpawn;
							if (flag4)
							{
								Vector3 position2 = spawnsDatum.Position;
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

		// Token: 0x0600007D RID: 125 RVA: 0x00007E60 File Offset: 0x00006060
		private int GetRandomWeightedItem(IEnumerable<float> items)
		{
			float max = MathUtils.Max(items.Sum(), 1f);
			float num = this.m_random.Float(0f, max);
			int num2 = 0;
			foreach (float item in items)
			{
				bool flag = num < item;
				if (flag)
				{
					return num2;
				}
				num -= item;
				num2++;
			}
			return -1;
		}

		// Token: 0x0600007E RID: 126 RVA: 0x00007EF0 File Offset: 0x000060F0
		public SpawnLocationType GetRandomSpawnLocationType()
		{
			return SubsystemModCreatureSpawn.m_spawnLocations[this.m_random.Int(0, SubsystemModCreatureSpawn.m_spawnLocations.Length - 1)];
		}

		// Token: 0x04000080 RID: 128
		private SubsystemGameInfo m_subsystemGameInfo;

		// Token: 0x04000081 RID: 129
		private SubsystemSpawn m_subsystemSpawn;

		// Token: 0x04000082 RID: 130
		private SubsystemTerrain m_subsystemTerrain;

		// Token: 0x04000083 RID: 131
		private SubsystemTime m_subsystemTime;

		// Token: 0x04000084 RID: 132
		private SubsystemSky m_subsystemSky;

		// Token: 0x04000085 RID: 133
		private SubsystemBodies m_subsystemBodies;

		// Token: 0x04000086 RID: 134
		private SubsystemGameWidgets m_subsystemViews;

		// Token: 0x04000087 RID: 135
		private Random m_random = new Random();

		// Token: 0x04000088 RID: 136
		private List<SubsystemModCreatureSpawn.CreatureType> m_creatureTypes = new List<SubsystemModCreatureSpawn.CreatureType>();

		// Token: 0x04000089 RID: 137
		private Dictionary<ComponentCreature, bool> m_creatures = new Dictionary<ComponentCreature, bool>();

		// Token: 0x0400008A RID: 138
		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();

		// Token: 0x0400008B RID: 139
		private List<SpawnChunk> m_newSpawnChunks = new List<SpawnChunk>();

		// Token: 0x0400008C RID: 140
		private List<SpawnChunk> m_spawnChunks = new List<SpawnChunk>();

		// Token: 0x0400008D RID: 141
		private static SpawnLocationType[] m_spawnLocations = EnumUtils.GetEnumValues(typeof(SpawnLocationType)).Cast<SpawnLocationType>().ToArray<SpawnLocationType>();

		// Token: 0x0400008E RID: 142
		private const int m_totalLimit = 24;

		// Token: 0x0400008F RID: 143
		private const int m_areaLimit = 3;

		// Token: 0x04000090 RID: 144
		private const int m_areaRadius = 16;

		// Token: 0x04000091 RID: 145
		private const int m_totalLimitConstant = 18;

		// Token: 0x04000092 RID: 146
		private const int m_areaLimitConstant = 4;

		// Token: 0x04000093 RID: 147
		private const int m_areaRadiusConstant = 42;

		// Token: 0x02000012 RID: 18
		[Nullable(0)]
		private class CreatureType
		{
			// Token: 0x06000095 RID: 149 RVA: 0x000081F2 File Offset: 0x000063F2
			public CreatureType(string name, SpawnLocationType spawnLocationType, bool randomSpawn, bool constantSpawn)
			{
				this.Name = name;
				this.SpawnLocationType = spawnLocationType;
				this.RandomSpawn = randomSpawn;
				this.ConstantSpawn = constantSpawn;
			}

			// Token: 0x06000096 RID: 150 RVA: 0x0000821C File Offset: 0x0000641C
			public override string ToString()
			{
				return this.Name;
			}

			// Token: 0x040000AE RID: 174
			public string Name;

			// Token: 0x040000AF RID: 175
			public SpawnLocationType SpawnLocationType;

			// Token: 0x040000B0 RID: 176
			public bool RandomSpawn;

			// Token: 0x040000B1 RID: 177
			public bool ConstantSpawn;

			// Token: 0x040000B2 RID: 178
			public Func<SubsystemModCreatureSpawn.CreatureType, Point3, float> SpawnSuitabilityFunction;

			// Token: 0x040000B3 RID: 179
			public Func<SubsystemModCreatureSpawn.CreatureType, Point3, int> SpawnFunction;
		}
	}
}
