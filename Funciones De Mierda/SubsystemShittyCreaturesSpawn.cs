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
			// Spawn para Naomi con 100% de probabilidad - DESDE DÍA 0, cualquier estación y hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Naomi", SpawnLocationType.Surface, true, true)
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

			// Spawn para Brayan con 100% de probabilidad - DESDE DÍA 0, cualquier estación y hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Brayan", SpawnLocationType.Surface, true, true)
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

			// Spawn para Tulio con 100% de probabilidad - DESDE DÍA 0, cualquier estación y hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Tulio", SpawnLocationType.Surface, true, true)
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

			// Spawn para Ricardo con 100% de probabilidad - DESDE DÍA 0, cualquier estación y hora
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("Ricardo", SpawnLocationType.Surface, true, true)
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

			// BetelGammamon - Solo en otoño e invierno, solo de día
			this.m_creatureTypes.Add(new SubsystemShittyCreaturesSpawn.CreatureType("BetelGammamon", SpawnLocationType.Surface, true, true) // ← Cambiado a true, true
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

					return (oceanDistance > 20f && temperature >= 8 && point.Y < 100 && lightLevel >= 7 &&
						(blockBelow == 8 || blockBelow == 2 || blockBelow == 3 || blockBelow == 7)) ? 1f : 0f;
				},
				SpawnFunction = ((SubsystemShittyCreaturesSpawn.CreatureType creatureType, Point3 point) =>
					this.SpawnCreatures(creatureType, "BetelGammamon", point, this.m_random.Int(1, 2)).Count)
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
