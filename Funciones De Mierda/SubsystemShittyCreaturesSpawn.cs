using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemShittyCreaturesSpawn : Subsystem, IUpdateable
	{
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemSpawn m_subsystemSpawn;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemTime m_subsystemTime;
		public SubsystemSky m_subsystemSky;
		public SubsystemSeasons m_subsystemSeasons;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemGameWidgets m_subsystemViews;
		public Random m_random = new Random();

		public List<CreatureType> m_creatureTypes = new List<CreatureType>();
		public Dictionary<ComponentCreature, bool> m_creatures = new Dictionary<ComponentCreature, bool>();
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		public List<SpawnChunk> m_newSpawnChunks = new List<SpawnChunk>();
		public List<SpawnChunk> m_spawnChunks = new List<SpawnChunk>();

		public static int m_totalLimit = 26;
		public static int m_areaLimit = 3;
		public static int m_areaRadius = 16;
		public static int m_totalLimitConstant = 6;
		public static int m_totalLimitConstantChallenging = 12;
		public static int m_areaLimitConstant = 4;
		public static int m_areaRadiusConstant = 42;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

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

			m_subsystemSpawn.SpawningChunk += (SpawnChunk chunk) =>
			{
				m_spawnChunks.Add(chunk);
				if (!chunk.IsSpawned) m_newSpawnChunks.Add(chunk);
			};

			InitializeCreatureTypes();
		}

		public override void OnEntityAdded(Entity entity)
		{
			foreach (var creature in entity.FindComponents<ComponentCreature>())
				m_creatures[creature] = true;
		}

		public override void OnEntityRemoved(Entity entity)
		{
			foreach (var creature in entity.FindComponents<ComponentCreature>())
				m_creatures.Remove(creature);
		}

		public void Update(float dt)
		{
			if (m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode != EnvironmentBehaviorMode.Living)
				return;

			if (m_newSpawnChunks.Count > 0)
			{
				m_newSpawnChunks.RandomShuffle(max => m_random.Int(0, max - 1));
				foreach (var chunk in m_newSpawnChunks)
					SpawnChunkCreatures(chunk, 10, false);
				m_newSpawnChunks.Clear();
			}

			if (m_spawnChunks.Count > 0)
			{
				m_spawnChunks.RandomShuffle(max => m_random.Int(0, max - 1));
				foreach (var chunk in m_spawnChunks)
					SpawnChunkCreatures(chunk, 2, true);
				m_spawnChunks.Clear();
			}

			float interval = (m_subsystemSeasons.Season == Season.Winter) ? 120f : 60f;
			if (m_subsystemTime.PeriodicGameTimeEvent(interval, 2.0))
				SpawnRandomCreature();
		}

		public void SpawnRandomCreature()
		{
			int total = CountCreatures(false);
			if (total >= m_totalLimit) return;

			foreach (var gameWidget in m_subsystemViews.GameWidgets)
			{
				Vector2 camPos = new Vector2(gameWidget.ActiveCamera.ViewPosition.X, gameWidget.ActiveCamera.ViewPosition.Z);
				if (CountCreaturesInArea(camPos - new Vector2(68f), camPos + new Vector2(68f), false) >= 52)
					break;

				SpawnLocationType locType = GetRandomSpawnLocationType();
				Point3? spawnPoint = GetRandomSpawnPoint(gameWidget.ActiveCamera, locType);
				if (spawnPoint == null) continue;

				Vector2 areaMin = new Vector2(spawnPoint.Value.X - 16f, spawnPoint.Value.Z - 16f);
				Vector2 areaMax = new Vector2(spawnPoint.Value.X + 16f, spawnPoint.Value.Z + 16f);
				if (CountCreaturesInArea(areaMin, areaMax, false) >= 3)
					break;

				var candidates = m_creatureTypes.Where(c => c.SpawnLocationType == locType && c.RandomSpawn).ToList();
				if (candidates.Count == 0) continue;

				var suits = candidates.Select(c => CalculateSpawnSuitability(c, spawnPoint.Value)).ToList();
				int idx = GetRandomWeightedItem(suits);
				if (idx >= 0)
					candidates[idx].SpawnFunction(candidates[idx], spawnPoint.Value);
			}
		}

		public void SpawnChunkCreatures(SpawnChunk chunk, int maxAttempts, bool constantSpawn)
		{
			int totalLimit = constantSpawn ? (m_subsystemGameInfo.WorldSettings.GameMode >= GameMode.Challenging ? m_totalLimitConstantChallenging : m_totalLimitConstant) : m_totalLimit;
			int areaLimit = constantSpawn ? m_areaLimitConstant : m_areaLimit;
			float radius = constantSpawn ? m_areaRadiusConstant : m_areaRadius;

			int totalCount = CountCreatures(constantSpawn);
			Vector2 areaMin = new Vector2(chunk.Point.X * 16, chunk.Point.Y * 16) - new Vector2(radius);
			Vector2 areaMax = new Vector2((chunk.Point.X + 1) * 16, (chunk.Point.Y + 1) * 16) + new Vector2(radius);
			int areaCount = CountCreaturesInArea(areaMin, areaMax, constantSpawn);

			for (int i = 0; i < maxAttempts; i++)
			{
				if (totalCount >= totalLimit || areaCount >= areaLimit) break;

				SpawnLocationType locType = GetRandomSpawnLocationType();
				Point3? spawnPoint = GetRandomChunkSpawnPoint(chunk, locType);
				if (spawnPoint == null) continue;

				var candidates = m_creatureTypes.Where(c => c.SpawnLocationType == locType && c.ConstantSpawn == constantSpawn).ToList();
				if (candidates.Count == 0) continue;

				var suits = candidates.Select(c => CalculateSpawnSuitability(c, spawnPoint.Value)).ToList();
				int idx = GetRandomWeightedItem(suits);
				if (idx >= 0)
				{
					int spawned = candidates[idx].SpawnFunction(candidates[idx], spawnPoint.Value);
					totalCount += spawned;
					areaCount += spawned;
				}
			}
		}

		public List<Entity> SpawnCreatures(CreatureType creatureType, string templateName, Point3 point, int count)
		{
			var list = new List<Entity>();
			int attempts = 0;
			while (count > 0 && attempts < 50)
			{
				Point3 spawnPoint = point;
				if (attempts > 0)
				{
					spawnPoint.X += m_random.Int(-8, 8);
					spawnPoint.Y += m_random.Int(-4, 8);
					spawnPoint.Z += m_random.Int(-8, 8);
				}

				Point3? processed = ProcessSpawnPoint(spawnPoint, creatureType.SpawnLocationType);
				if (processed != null && CalculateSpawnSuitability(creatureType, processed.Value) > 0f)
				{
					Vector3 pos = new Vector3(
						processed.Value.X + m_random.Float(0.4f, 0.6f),
						processed.Value.Y + 1.1f,
						processed.Value.Z + m_random.Float(0.4f, 0.6f)
					);
					var entity = SpawnCreature(templateName, pos, creatureType.ConstantSpawn);
					if (entity != null)
					{
						list.Add(entity);
						count--;
					}
				}
				attempts++;
			}
			return list;
		}

		public Entity SpawnCreature(string templateName, Vector3 position, bool constantSpawn)
		{
			try
			{
				var entity = DatabaseManager.CreateEntity(Project, templateName, true);
				var body = entity.FindComponent<ComponentBody>(true);
				body.Position = position;
				body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, m_random.Float(0f, 6.2831855f));
				var creature = entity.FindComponent<ComponentCreature>(true);
				creature.ConstantSpawn = constantSpawn;
				Project.AddEntity(entity);
				return entity;
			}
			catch (Exception ex)
			{
				Log.Error($"Unable to spawn creature \"{templateName}\": {ex.Message}");
				return null;
			}
		}

		public Point3? GetRandomChunkSpawnPoint(SpawnChunk chunk, SpawnLocationType locType)
		{
			for (int i = 0; i < 5; i++)
			{
				int x = chunk.Point.X * 16 + m_random.Int(0, 15);
				int y = m_random.Int(10, 246);
				int z = chunk.Point.Y * 16 + m_random.Int(0, 15);
				var point = ProcessSpawnPoint(new Point3(x, y, z), locType);
				if (point != null) return point;
			}
			return null;
		}

		public Point3? GetRandomSpawnPoint(Camera camera, SpawnLocationType locType)
		{
			for (int i = 0; i < 10; i++)
			{
				int x = Terrain.ToCell(camera.ViewPosition.X) + m_random.Sign() * m_random.Int(24, 48);
				int y = Math.Clamp(Terrain.ToCell(camera.ViewPosition.Y) + m_random.Int(-30, 30), 2, 254);
				int z = Terrain.ToCell(camera.ViewPosition.Z) + m_random.Sign() * m_random.Int(24, 48);
				var point = ProcessSpawnPoint(new Point3(x, y, z), locType);
				if (point != null) return point;
			}
			return null;
		}

		public Point3? ProcessSpawnPoint(Point3 spawnPoint, SpawnLocationType locType)
		{
			int x = spawnPoint.X;
			int y = Math.Clamp(spawnPoint.Y, 1, 254);
			int z = spawnPoint.Z;
			var chunk = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunk == null || chunk.State < TerrainChunkState.InvalidPropagatedLight)
				return null;

			for (int i = 0; i < 30; i++)
			{
				var up = new Point3(x, y + i, z);
				if (TestSpawnPoint(up, locType)) return up;
				var down = new Point3(x, y - i, z);
				if (TestSpawnPoint(down, locType)) return down;
			}
			return null;
		}

		public bool TestSpawnPoint(Point3 point, SpawnLocationType locType)
		{
			int x = point.X, y = point.Y, z = point.Z;
			if (y <= 3 || y >= 253) return false;

			switch (locType)
			{
				case SpawnLocationType.Surface:
				{
					int light = m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
					if (m_subsystemSky.SkyLightValue - light > 3) return false;

					int below = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);
					Block bBelow = BlocksManager.Blocks[Terrain.ExtractContents(below)];
					Block bAt = BlocksManager.Blocks[Terrain.ExtractContents(at)];
					Block bAbove = BlocksManager.Blocks[Terrain.ExtractContents(above)];
					return (bBelow.IsCollidable_(below) || bBelow is WaterBlock)
						&& !bAt.IsCollidable_(at) && !(bAt is WaterBlock)
						&& !bAbove.IsCollidable_(above) && !(bAbove is WaterBlock);
				}
				case SpawnLocationType.Cave:
				{
					int light = m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
					if (m_subsystemSky.SkyLightValue - light < 5) return false;

					int below = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);
					Block bBelow = BlocksManager.Blocks[Terrain.ExtractContents(below)];
					Block bAt = BlocksManager.Blocks[Terrain.ExtractContents(at)];
					Block bAbove = BlocksManager.Blocks[Terrain.ExtractContents(above)];
					return (bBelow.IsCollidable_(below) || bBelow is WaterBlock)
						&& !bAt.IsCollidable_(at) && !(bAt is WaterBlock)
						&& !bAbove.IsCollidable_(above) && !(bAbove is WaterBlock);
				}
				case SpawnLocationType.Water:
				{
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);
					int above2 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 2, z);
					Block bAt = BlocksManager.Blocks[Terrain.ExtractContents(at)];
					Block bAbove = BlocksManager.Blocks[Terrain.ExtractContents(above)];
					Block bAbove2 = BlocksManager.Blocks[Terrain.ExtractContents(above2)];
					return bAt is WaterBlock && !bAbove.IsCollidable_(above) && !bAbove2.IsCollidable_(above2);
				}
				default:
					return false;
			}
		}

		public float CalculateSpawnSuitability(CreatureType creature, Point3 point)
		{
			float suitability = creature.SpawnSuitabilityFunction(creature, point);
			if (CountCreatures(creature) > 8)
				suitability *= 0.25f;
			return suitability;
		}

		public int CountCreatures(CreatureType creatureType)
		{
			int count = 0;
			foreach (var body in m_subsystemBodies.Bodies)
				if (body.Entity.ValuesDictionary.DatabaseObject.Name == creatureType.Name) count++;
			return count;
		}

		public int CountCreatures(bool constantSpawn)
		{
			int count = 0;
			foreach (var body in m_subsystemBodies.Bodies)
			{
				var creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature.ConstantSpawn == constantSpawn) count++;
			}
			return count;
		}

		public int CountCreaturesInArea(Vector2 c1, Vector2 c2, bool constantSpawn)
		{
			int count = 0;
			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesInArea(c1, c2, m_componentBodies);
			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				var body = m_componentBodies.Array[i];
				var creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature.ConstantSpawn == constantSpawn)
				{
					if (body.Position.X >= c1.X && body.Position.X <= c2.X &&
						body.Position.Z >= c1.Y && body.Position.Z <= c2.Y)
						count++;
				}
			}

			Point2 minChunk = Terrain.ToChunk(c1);
			Point2 maxChunk = Terrain.ToChunk(c2);
			for (int cx = minChunk.X; cx <= maxChunk.X; cx++)
				for (int cz = minChunk.Y; cz <= maxChunk.Y; cz++)
				{
					var spawnChunk = m_subsystemSpawn.GetSpawnChunk(new Point2(cx, cz));
					if (spawnChunk != null)
						foreach (var data in spawnChunk.SpawnsData)
							if (data.ConstantSpawn == constantSpawn &&
								data.Position.X >= c1.X && data.Position.X <= c2.X &&
								data.Position.Z >= c1.Y && data.Position.Z <= c2.Y)
								count++;
				}
			return count;
		}

		public int GetRandomWeightedItem(IEnumerable<float> weights)
		{
			var list = weights.ToList();
			float total = MathUtils.Max(list.Sum(), 1f);
			float r = m_random.Float(0f, total);
			float acc = 0f;
			for (int i = 0; i < list.Count; i++)
			{
				acc += list[i];
				if (r < acc) return i;
			}
			return -1;
		}

		public SpawnLocationType GetRandomSpawnLocationType()
		{
			float r = m_random.Float();
			if (r <= 0.3f) return SpawnLocationType.Surface;
			if (r <= 0.6f) return SpawnLocationType.Cave;
			return SpawnLocationType.Water;
		}

		private void InitializeCreatureTypes()
		{
			m_creatureTypes.Add(new CreatureType("Naomi", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					if (m_subsystemSky.SkyLightIntensity < 0.2f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "Naomi", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("Ricardo", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					if (m_subsystemSky.SkyLightIntensity < 0.2f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "Ricardo", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("Brayan", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					if (m_subsystemSky.SkyLightIntensity < 0.2f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "Brayan", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("Tulio", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					if (m_subsystemSky.SkyLightIntensity < 0.2f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "Tulio", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("LaMuerteX", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "LaMuerteX", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("Paco", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 3) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.2f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "Paco", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("Sparkster", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 2) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.2f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "Sparkster", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("Barack", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 4) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "Barack", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("ElMarihuanero", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 4) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.2f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "ElMarihuanero", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("ElMarihuaneroMamon", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 5) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "ElMarihuaneroMamon", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("ElSenorDeLasTumbasMoradas", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 2) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "ElSenorDeLasTumbasMoradas", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("LiderCalavericoSupremo", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 7) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "LiderCalavericoSupremo", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("FumadorQuimico", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 8) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "FumadorQuimico", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("ClaudeSpeed", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 14) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.2f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "ClaudeSpeed", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("TommyVercetti", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 15) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "TommyVercetti", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("Conker", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 10) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "Conker", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("Beavis", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 9) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.2f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "Beavis", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("Butt-Head", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 9) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.2f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "Butt-Head", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("HombreLava", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 16) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					bool isLava = (at == 90 || at == 91);
					bool validGround = (below == 2 || below == 3 || below == 7 || below == 8);
					if (!isLava && !validGround) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (!isLava && (atBlock.IsCollidable_(at) || atBlock is WaterBlock)) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					float baseSuit = 2.5f;
					return isLava ? baseSuit * 0.5f : baseSuit * 0.05f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "HombreLava", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("HombreAgua", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 16) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					bool isWater = (at == 8 || at == 9);
					bool nearWater = false;
					for (int dx = -2; dx <= 2 && !nearWater; dx++)
						for (int dz = -2; dz <= 2; dz++)
							if (m_subsystemTerrain.Terrain.GetCellContentsFast(x + dx, y, z + dz) == 8 ||
								m_subsystemTerrain.Terrain.GetCellContentsFast(x + dx, y, z + dz) == 9)
							{ nearWater = true; break; }
					if (!isWater && !nearWater) return 0f;
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "HombreAgua", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("ElArquero", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 5) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "ElArquero", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("ArqueroPrisionero", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 6) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "ArqueroPrisionero", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("BallestadoraMusculosa", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 11) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0.2f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "BallestadoraMusculosa", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("ElBallestador", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 10) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "ElBallestador", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("HeadcrabBloodyHuman", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 20) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "HeadcrabBloodyHuman", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("AladinaCorrupta", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 25) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "AladinaCorrupta", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("WalterZombie", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 17) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 0.1f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "WalterZombie", p, 1).Count
			});

			m_creatureTypes.Add(new CreatureType("Aimep3", SpawnLocationType.Surface, true, true)
			{
				SpawnSuitabilityFunction = (_, p) =>
				{
					var tod = Project.FindSubsystem<SubsystemTimeOfDay>(true);
					if (tod == null || Math.Floor(tod.Day) + 1 != 3) return 0f;
					if (m_subsystemSky.SkyLightIntensity < 0f || m_subsystemSky.SkyLightIntensity > 1.0f) return 0f;
					int x = p.X, y = p.Y, z = p.Z;
					if (y <= 3 || y >= 253) return 0f;
					int below = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y - 1, z);
					int at = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y, z);
					int above = m_subsystemTerrain.Terrain.GetCellContentsFast(x, y + 1, z);
					if (below != 2 && below != 3 && below != 7 && below != 8) return 0f;
					Block atBlock = BlocksManager.Blocks[at];
					Block aboveBlock = BlocksManager.Blocks[above];
					if (atBlock.IsCollidable_(at) || atBlock is WaterBlock) return 0f;
					if (aboveBlock.IsCollidable_(above) || aboveBlock is WaterBlock) return 0f;
					return 2.5f;
				},
				SpawnFunction = (ct, p) => SpawnCreatures(ct, "Aimep3", p, 1).Count
			});
		}

		public class CreatureType
		{
			public string Name;
			public SpawnLocationType SpawnLocationType;
			public bool RandomSpawn;
			public bool ConstantSpawn;
			public Func<CreatureType, Point3, float> SpawnSuitabilityFunction;
			public Func<CreatureType, Point3, int> SpawnFunction;

			public CreatureType(string name, SpawnLocationType loc, bool randomSpawn, bool constantSpawn)
			{
				Name = name;
				SpawnLocationType = loc;
				RandomSpawn = randomSpawn;
				ConstantSpawn = constantSpawn;
			}
		}
	}
}
