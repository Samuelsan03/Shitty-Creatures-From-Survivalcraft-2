using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemZombiesSpawn : Subsystem, IUpdateable
	{
		// Subsistemas necesarios
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemSpawn m_subsystemSpawn;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemSky m_subsystemSky;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemGameWidgets m_subsystemViews;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private SubsystemTimeOfDay m_subsystemTimeOfDay;
		private SubsystemPlayers m_subsystemPlayers;

		// Datos de las olas
		private Dictionary<int, List<ZombieSpawnEntry>> m_waves = new Dictionary<int, List<ZombieSpawnEntry>>();

		// Estado actual
		private int m_currentWaveIndex = 1;
		private int m_maxWaveIndex = 19;

		// Control de spawn
		private double m_nextSpawnTime;
		private float m_spawnInterval = 2f;
		private Random m_random = new Random();

		// Control de jefes
		private HashSet<string> m_bossZombieTypes = new HashSet<string>
		{
			"Tank1", "Tank2", "Tank3",
			"TankGhost1", "TankGhost2", "TankGhost3",
			"MachineGunInfected", "FlyingInfectedBoss"
		};

		private List<string> m_pendingBosses = new List<string>();
		private List<Entity> m_activeBosses = new List<Entity>();
		private bool m_bossPhaseActive;
		private double m_lastMidnightCheck;

		// Control de fases lunares
		private int m_nextWavePhase = 0;
		private bool m_advancedThisNight;

		// Bloques prohibidos
		private HashSet<string> m_forbiddenBlockNames = new HashSet<string>
		{
			nameof(BedrockBlock),
			nameof(IronBlock),
			nameof(CopperBlock),
			nameof(DiamondBlock),
			nameof(BrickBlock),
			nameof(MalachiteBlock),
			nameof(WaterBlock),
			nameof(MagmaBlock),
			nameof(GraniteBlock),
			nameof(BasaltBlock),
			nameof(BasaltFenceBlock),
			nameof(BasaltSlabBlock),
			nameof(BasaltStairsBlock),
			nameof(LimestoneBlock)
		};

		// Lista de tipos de criaturas para spawn normal (como en SubsystemCreatureSpawn)
		private List<CreatureType> m_creatureTypes = new List<CreatureType>();

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			// Obtener subsistemas
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemSpawn = Project.FindSubsystem<SubsystemSpawn>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true);
			m_subsystemGreenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>(true);
			m_subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);

			// Cargar las olas
			LoadWaves();

			// Inicializar tipos de criaturas para spawn normal
			InitializeCreatureTypes();

			// Cargar estado guardado
			m_currentWaveIndex = valuesDictionary.GetValue<int>("CurrentWaveIndex", 1);
			m_nextWavePhase = valuesDictionary.GetValue<int>("NextWavePhase", 0);

			if (m_currentWaveIndex < 1) m_currentWaveIndex = 1;
			if (m_currentWaveIndex > m_maxWaveIndex) m_currentWaveIndex = m_maxWaveIndex;
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			valuesDictionary.SetValue("CurrentWaveIndex", m_currentWaveIndex);
			valuesDictionary.SetValue("NextWavePhase", m_nextWavePhase);
		}

		public void Update(float dt)
		{
			// Primero, spawn normal de criaturas (solo HumanoidSkeleton por ahora)
			SpawnNormalCreatures();

			// Solo activar durante noche verde
			if (!m_subsystemGreenNightSky.GreenNightEnabled || !m_subsystemGreenNightSky.IsGreenNightActive)
				return;

			// Verificar si es de noche
			float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
			bool isNight = timeOfDay > 0.5f || timeOfDay < 0.01f;
			if (!isNight) return;

			double now = m_subsystemTime.GameTime;
			int currentPhase = m_subsystemSky.MoonPhase;

			// Avanzar de ola cuando cambia la fase lunar
			if (!m_advancedThisNight)
			{
				if (currentPhase == m_nextWavePhase)
				{
					if (m_currentWaveIndex < m_maxWaveIndex)
					{
						m_currentWaveIndex++;
						m_nextWavePhase = (m_nextWavePhase == 0) ? 4 : 0;
					}
					m_advancedThisNight = true;
				}
			}

			if (IsDawn())
				m_advancedThisNight = false;

			// Fase de jefes
			if (!m_bossPhaseActive && IsMidnight() && now - m_lastMidnightCheck > 10.0)
			{
				m_lastMidnightCheck = now;
				StartBossPhaseForCurrentWave();
			}

			if (m_bossPhaseActive)
			{
				for (int i = m_activeBosses.Count - 1; i >= 0; i--)
				{
					Entity e = m_activeBosses[i];
					if (!e.IsAddedToProject)
					{
						m_activeBosses.RemoveAt(i);
						continue;
					}

					ComponentHealth health = e.FindComponent<ComponentHealth>();
					if (health == null || health.Health <= 0f)
						m_activeBosses.RemoveAt(i);
				}

				if (m_activeBosses.Count == 0 && m_pendingBosses.Count > 0)
					SpawnNextBoss();
				else if (m_activeBosses.Count == 0 && m_pendingBosses.Count == 0)
					m_bossPhaseActive = false;

				return;
			}

			// Spawn normal de oleada (solo noche verde)
			if (now >= m_nextSpawnTime)
			{
				m_nextSpawnTime = now + m_spawnInterval;
				SpawnZombie();
			}
		}

		private void InitializeCreatureTypes()
		{
			// Agregar HumanoidSkeleton como criatura constante que aparece de noche
			m_creatureTypes.Add(new CreatureType("Constant HumanoidSkeleton", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					// Solo aparece de noche (sky light bajo)
					if (m_subsystemSky.SkyLightIntensity < 0.1f)
					{
						// Verificar que el bloque de abajo sea sólido
						int blockBelow = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
						int cellLight = m_subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);

						// Condiciones similares a las criaturas constantes
						if (point.Y < 100 && cellLight <= 7 && (blockBelow == 2 || blockBelow == 3 || blockBelow == 7 || blockBelow == 8))
						{
							return 1f; // Probabilidad de spawn
						}
					}
					return 0f;
				},
				SpawnFunction = (CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "HumanoidSkeleton", point, 1).Count
			});
		}

		private void SpawnNormalCreatures()
		{
			// Límites similares a SubsystemCreatureSpawn
			int totalLimit = 6; // m_totalLimitConstant
			int areaLimit = 4;  // m_areaLimitConstant
			float areaRadius = 42f; // m_areaRadiusConstant

			int totalCount = CountCreatures(false);
			if (totalCount >= totalLimit)
				return;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				Vector3 playerPos = player.ComponentBody.Position;
				Vector2 c1 = new Vector2(playerPos.X - areaRadius, playerPos.Z - areaRadius);
				Vector2 c2 = new Vector2(playerPos.X + areaRadius, playerPos.Z + areaRadius);
				int areaCount = CountCreaturesInArea(c1, c2, false);
				if (areaCount >= areaLimit)
					continue;

				// Obtener punto de spawn aleatorio
				Point3? spawnPoint = GetRandomSpawnPoint(player, SpawnLocationType.Surface);
				if (spawnPoint != null)
				{
					// Buscar criaturas adecuadas para este punto
					var suitableCreatures = m_creatureTypes.Where(c => c.SpawnLocationType == SpawnLocationType.Surface && c.ConstantSpawn == false).ToList();

					foreach (var creatureType in suitableCreatures)
					{
						float suitability = CalculateSpawnSuitability(creatureType, spawnPoint.Value);
						if (suitability > 0f && m_random.Float(0f, 1f) < suitability * 0.1f)
						{
							creatureType.SpawnFunction(creatureType, spawnPoint.Value);
							break;
						}
					}
				}
			}
		}

		private Point3? GetRandomSpawnPoint(ComponentPlayer player, SpawnLocationType spawnLocationType)
		{
			for (int i = 0; i < 10; i++)
			{
				int x = Terrain.ToCell(player.ComponentBody.Position.X) + (m_random.Sign() * m_random.Int(24, 48));
				int y = Math.Clamp(Terrain.ToCell(player.ComponentBody.Position.Y) + m_random.Int(-30, 30), 2, 254);
				int z = Terrain.ToCell(player.ComponentBody.Position.Z) + (m_random.Sign() * m_random.Int(24, 48));

				Point3? result = ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
				if (result != null)
					return result;
			}
			return null;
		}

		private Point3? ProcessSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
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
						return point;

					Point3 point2 = new Point3(x, num - i, z);
					if (TestSpawnPoint(point2, spawnLocationType))
						return point2;
				}
			}
			return null;
		}

		private bool TestSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
		{
			int x = spawnPoint.X;
			int y = spawnPoint.Y;
			int z = spawnPoint.Z;

			if (y <= 3 || y >= 253)
				return false;

			if (spawnLocationType == SpawnLocationType.Surface)
			{
				int cellLight = m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
				if (m_subsystemSky.SkyLightValue - cellLight > 3)
					return false;

				int belowValue = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
				int currentValue = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
				int aboveValue = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

				Block belowBlock = BlocksManager.Blocks[Terrain.ExtractContents(belowValue)];
				Block currentBlock = BlocksManager.Blocks[Terrain.ExtractContents(currentValue)];
				Block aboveBlock = BlocksManager.Blocks[Terrain.ExtractContents(aboveValue)];

				return (belowBlock.IsCollidable_(belowValue) || belowBlock is WaterBlock) &&
					   !currentBlock.IsCollidable_(currentValue) && !(currentBlock is WaterBlock) &&
					   !aboveBlock.IsCollidable_(aboveValue) && !(aboveBlock is WaterBlock);
			}

			return false;
		}

		private float CalculateSpawnSuitability(CreatureType creatureType, Point3 spawnPoint)
		{
			float suitability = creatureType.SpawnSuitabilityFunction(creatureType, spawnPoint);

			// Reducir si hay demasiados de este tipo
			if (CountCreaturesByType(creatureType.Name) > 8)
				suitability *= 0.25f;

			return suitability;
		}

		private int CountCreaturesByType(string creatureName)
		{
			int count = 0;
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body.Entity.ValuesDictionary.DatabaseObject.Name == creatureName)
					count++;
			}
			return count;
		}

		private int CountCreatures(bool constantSpawn)
		{
			int count = 0;
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature.ConstantSpawn == constantSpawn)
					count++;
			}
			return count;
		}

		private int CountCreaturesInArea(Vector2 c1, Vector2 c2, bool constantSpawn)
		{
			int count = 0;
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature.ConstantSpawn == constantSpawn)
				{
					Vector3 pos = body.Position;
					if (pos.X >= c1.X && pos.X <= c2.X && pos.Z >= c1.Y && pos.Z <= c2.Y)
						count++;
				}
			}
			return count;
		}

		private List<Entity> SpawnCreatures(CreatureType creatureType, string templateName, Point3 point, int count)
		{
			List<Entity> spawned = new List<Entity>();
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

				Point3? validPoint = ProcessSpawnPoint(spawnPoint, creatureType.SpawnLocationType);
				if (validPoint != null && CalculateSpawnSuitability(creatureType, validPoint.Value) > 0f)
				{
					Vector3 position = new Vector3(
						validPoint.Value.X + m_random.Float(0.4f, 0.6f),
						validPoint.Value.Y + 1.1f,
						validPoint.Value.Z + m_random.Float(0.4f, 0.6f)
					);

					Entity entity = SpawnEntity(templateName, position);
					if (entity != null)
					{
						ComponentCreature creature = entity.FindComponent<ComponentCreature>();
						if (creature != null)
							creature.ConstantSpawn = creatureType.ConstantSpawn;

						spawned.Add(entity);
						count--;
					}
				}
				attempts++;
			}

			return spawned;
		}

		private void LoadWaves()
		{
			try
			{
				bool anyWaveLoaded = false;

				for (int i = 1; i <= m_maxWaveIndex; i++)
				{
					try
					{
						string content = ContentManager.Get<string>($"Waves/{i}");

						if (!string.IsNullOrEmpty(content))
						{
							string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
							List<ZombieSpawnEntry> entries = new List<ZombieSpawnEntry>();

							foreach (string line in lines)
							{
								if (string.IsNullOrWhiteSpace(line)) continue;

								string[] parts = line.Trim().Split(';');
								if (parts.Length == 2)
								{
									string type = parts[0].Trim();
									if (int.TryParse(parts[1].Trim(), out int weight))
									{
										entries.Add(new ZombieSpawnEntry { Type = type, Weight = weight });
									}
								}
							}

							if (entries.Count > 0)
							{
								m_waves[i] = entries;
								anyWaveLoaded = true;
							}
						}
					}
					catch
					{
					}
				}

				if (!anyWaveLoaded)
				{
					Log.Warning("No se encontraron archivos de olas. Usando olas por defecto.");
					LoadDefaultWaves();
				}
			}
			catch (Exception ex)
			{
				Log.Error($"Error cargando olas: {ex.Message}");
				LoadDefaultWaves();
			}
		}

		private void LoadDefaultWaves()
		{
			Log.Warning("Cargando olas por defecto - los archivos TXT no fueron encontrados");

			m_waves[1] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;40",
				"InfectedBird;35",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"InfectedFly1;4"
			});

			m_waves[2] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;40",
				"InfectedBird;35",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"InfectedFly1;6"
			});

			m_waves[3] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;35",
				"InfectedBird;30",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"InfectedFly1;6"
			});

			m_waves[4] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;35",
				"InfectedBird;30",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"PoisonousInfected1;20",
				"PoisonousInfected2;20",
				"InfectedFly1;6"
			});

			m_waves[5] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;30",
				"InfectedBird;25",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"PoisonousInfected1;20",
				"PoisonousInfected2;20",
				"InfectedFly1;6",
				"InfectedFly2;8",
				"Boomer1;10",
				"InfectedHyena;20"
			});

			m_waves[6] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;30",
				"InfectedBird;25",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"PoisonousInfected1;20",
				"PoisonousInfected2;20",
				"InfectedFly1;6",
				"InfectedFly2;8",
				"Boomer1;10",
				"Boomer2;10",
				"InfectedHyena;22",
				"PredatoryChameleon;5"
			});

			m_waves[7] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;30",
				"InfectedBird;25",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"PoisonousInfected1;20",
				"PoisonousInfected2;20",
				"InfectedFly1;6",
				"InfectedFly2;8",
				"InfectedFly3;5",
				"Boomer1;10",
				"Boomer2;10",
				"Boomer3;10",
				"InfectedHyena;25",
				"PredatoryChameleon;7"
			});

			m_waves[8] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;30",
				"InfectedBird;25",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"PoisonousInfected1;20",
				"PoisonousInfected2;20",
				"PoisonousGhost;12",
				"InfectedFly1;6",
				"InfectedFly2;8",
				"InfectedFly3;5",
				"Boomer1;10",
				"Boomer2;10",
				"Boomer3;10",
				"Charger1;6",
				"InfectedHyena;28",
				"PredatoryChameleon;8"
			});

			m_waves[9] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;30",
				"InfectedBird;25",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"GhostFast;12",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"PoisonousInfected1;20",
				"PoisonousInfected2;20",
				"PoisonousGhost;12",
				"InfectedFly1;6",
				"InfectedFly2;8",
				"InfectedFly3;5",
				"Boomer1;10",
				"Boomer2;10",
				"Boomer3;10",
				"Charger2;6",
				"InfectedHyena;30",
				"PredatoryChameleon;10"
			});

			m_waves[10] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;25",
				"InfectedBird;20",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"GhostNormal;18",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"GhostFast;12",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"PoisonousInfected1;20",
				"PoisonousInfected2;20",
				"PoisonousGhost;12",
				"InfectedFly1;6",
				"InfectedFly2;8",
				"InfectedFly3;5",
				"Boomer1;10",
				"Boomer2;10",
				"Boomer3;10",
				"GhostBoomer1;6",
				"Charger1;6",
				"Charger2;6",
				"InfectedHyena;30",
				"InfectedWildboar;12",
				"PredatoryChameleon;12"
			});

			m_waves[11] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;25",
				"InfectedBird;20",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"GhostNormal;18",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"GhostFast;12",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"PoisonousInfected1;20",
				"PoisonousInfected2;20",
				"PoisonousGhost;12",
				"InfectedFly1;6",
				"InfectedFly2;8",
				"InfectedFly3;5",
				"Boomer1;10",
				"Boomer2;10",
				"Boomer3;10",
				"GhostBoomer1;6",
				"GhostBoomer2;6",
				"Charger1;6",
				"Charger2;6",
				"Tank1;1",
				"InfectedHyena;30",
				"InfectedWildboar;15",
				"InfectedBear;8",
				"PredatoryChameleon;15"
			});

			m_waves[12] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;25",
				"InfectedBird;20",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"GhostNormal;18",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"GhostFast;12",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"PoisonousInfected1;20",
				"PoisonousInfected2;20",
				"PoisonousGhost;12",
				"InfectedFly1;6",
				"InfectedFly2;8",
				"InfectedFly3;5",
				"Boomer1;10",
				"Boomer2;10",
				"Boomer3;10",
				"GhostBoomer1;6",
				"GhostBoomer2;6",
				"GhostBoomer3;6",
				"Charger1;6",
				"Charger2;6",
				"TankGhost1;1",
				"InfectedHyena;30",
				"InfectedWildboar;15",
				"InfectedBear;10",
				"PredatoryChameleon;18"
			});

			m_waves[13] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;25",
				"InfectedBird;20",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"GhostNormal;18",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"GhostFast;12",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"PoisonousInfected1;20",
				"PoisonousInfected2;20",
				"PoisonousGhost;12",
				"InfectedFly1;6",
				"InfectedFly2;8",
				"InfectedFly3;5",
				"Boomer1;10",
				"Boomer2;10",
				"Boomer3;10",
				"GhostBoomer1;6",
				"GhostBoomer2;6",
				"GhostBoomer3;6",
				"GhostCharger;4",
				"Charger1;6",
				"Charger2;6",
				"Tank2;1",
				"InfectedHyena;30",
				"InfectedWildboar;15",
				"InfectedBear;12",
				"PredatoryChameleon;20"
			});

			m_waves[14] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;25",
				"InfectedBird;20",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"GhostNormal;18",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"GhostFast;12",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"PoisonousInfected1;20",
				"PoisonousInfected2;20",
				"PoisonousGhost;12",
				"InfectedFly1;6",
				"InfectedFly2;8",
				"InfectedFly3;5",
				"Boomer1;10",
				"Boomer2;10",
				"Boomer3;10",
				"GhostBoomer1;6",
				"GhostBoomer2;6",
				"GhostBoomer3;6",
				"GhostCharger;4",
				"Charger1;6",
				"Charger2;6",
				"Tank3;1",
				"InfectedHyena;30",
				"InfectedWildboar;15",
				"InfectedBear;15",
				"PredatoryChameleon;22"
			});

			m_waves[15] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;20",
				"InfectedBird;15",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"GhostNormal;18",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"GhostFast;12",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"PoisonousInfected1;20",
				"PoisonousInfected2;20",
				"PoisonousGhost;12",
				"InfectedFly1;6",
				"InfectedFly2;8",
				"InfectedFly3;5",
				"Boomer1;10",
				"Boomer2;10",
				"Boomer3;10",
				"GhostBoomer1;6",
				"GhostBoomer2;6",
				"GhostBoomer3;6",
				"GhostCharger;4",
				"Charger1;6",
				"Charger2;6",
				"TankGhost3;1",
				"InfectedHyena;30",
				"InfectedWildboar;15",
				"InfectedBear;18",
				"PredatoryChameleon;25"
			});

			m_waves[16] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;20",
				"InfectedBird;15",
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"GhostNormal;18",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"GhostFast;12",
				"InfectedMuscle1;35",
				"InfectedMuscle2;35",
				"PoisonousInfected1;20",
				"PoisonousInfected2;20",
				"PoisonousGhost;12",
				"InfectedFly1;6",
				"InfectedFly2;8",
				"InfectedFly3;5",
				"Boomer1;10",
				"Boomer2;10",
				"Boomer3;10",
				"GhostBoomer1;6",
				"GhostBoomer2;6",
				"GhostBoomer3;6",
				"GhostCharger;4",
				"Charger1;6",
				"Charger2;6",
				"FlyingInfectedBoss;1",
				"InfectedHyena;30",
				"InfectedWildboar;15",
				"InfectedBear;20",
				"PredatoryChameleon;28"
			});

			m_waves[17] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;20",
				"InfectedBird;15",
				"InfectedNormal1;35",
				"InfectedNormal2;35",
				"GhostNormal;20",
				"InfectedFast1;30",
				"InfectedFast2;30",
				"GhostFast;15",
				"InfectedMuscle1;40",
				"InfectedMuscle2;40",
				"PoisonousInfected1;25",
				"PoisonousInfected2;25",
				"PoisonousGhost;15",
				"InfectedFly1;8",
				"InfectedFly2;10",
				"InfectedFly3;7",
				"Boomer1;12",
				"Boomer2;12",
				"Boomer3;12",
				"GhostBoomer1;8",
				"GhostBoomer2;8",
				"GhostBoomer3;8",
				"GhostCharger;6",
				"Charger1;8",
				"Charger2;8",
				"InfectedHyena;35",
				"InfectedWildboar;18",
				"InfectedBear;22",
				"PredatoryChameleon;30"
			});

			m_waves[18] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;20",
				"InfectedBird;15",
				"InfectedNormal1;35",
				"InfectedNormal2;35",
				"GhostNormal;20",
				"InfectedFast1;30",
				"InfectedFast2;30",
				"GhostFast;15",
				"InfectedMuscle1;40",
				"InfectedMuscle2;40",
				"PoisonousInfected1;25",
				"PoisonousInfected2;25",
				"PoisonousGhost;15",
				"InfectedFly1;8",
				"InfectedFly2;10",
				"InfectedFly3;6",
				"Boomer1;12",
				"Boomer2;12",
				"Boomer3;12",
				"GhostBoomer1;8",
				"GhostBoomer2;8",
				"GhostBoomer3;8",
				"GhostCharger;6",
				"Charger1;8",
				"Charger2;8",
				"MachineGunInfected;1",
				"InfectedHyena;35",
				"InfectedWildboar;18",
				"InfectedBear;25",
				"PredatoryChameleon;32"
			});

			m_waves[19] = ParseWaveData(new string[]
			{
				"HumanoidSkeleton;15",
				"InfectedBird;10",
				"InfectedNormal1;40",
				"InfectedNormal2;40",
				"GhostNormal;25",
				"InfectedFast1;35",
				"InfectedFast2;35",
				"GhostFast;20",
				"InfectedMuscle1;45",
				"InfectedMuscle2;45",
				"PoisonousInfected1;30",
				"PoisonousInfected2;30",
				"PoisonousGhost;20",
				"InfectedFly1;10",
				"InfectedFly2;12",
				"InfectedFly3;8",
				"Boomer1;15",
				"Boomer2;15",
				"Boomer3;15",
				"GhostBoomer1;10",
				"GhostBoomer2;10",
				"GhostBoomer3;10",
				"Charger1;12",
				"Charger2;12",
				"GhostCharger;8",
				"Tank1;1",
				"Tank2;1",
				"Tank3;1",
				"TankGhost1;1",
				"TankGhost2;1",
				"TankGhost3;1",
				"MachineGunInfected;1",
				"FlyingInfectedBoss;1",
				"InfectedHyena;40",
				"InfectedWildboar;20",
				"InfectedBear;28",
				"PredatoryChameleon;35"
			});
		}

		private List<ZombieSpawnEntry> ParseWaveData(string[] lines)
		{
			var entries = new List<ZombieSpawnEntry>();

			foreach (string line in lines)
			{
				string trimmed = line.Trim();
				if (string.IsNullOrEmpty(trimmed)) continue;

				string[] parts = trimmed.Split(';');
				if (parts.Length == 2)
				{
					string type = parts[0].Trim();
					if (int.TryParse(parts[1].Trim(), out int weight))
					{
						entries.Add(new ZombieSpawnEntry { Type = type, Weight = weight });
					}
				}
			}

			return entries;
		}

		private bool IsDusk()
		{
			float time = m_subsystemTimeOfDay.TimeOfDay;
			return Math.Abs(time - 0.5f) < 0.01f;
		}

		private bool IsDawn()
		{
			float time = m_subsystemTimeOfDay.TimeOfDay;
			return Math.Abs(time) < 0.01f || Math.Abs(time - 1f) < 0.01f;
		}

		private bool IsMidnight()
		{
			float time = m_subsystemTimeOfDay.TimeOfDay;
			return Math.Abs(time - 0.75f) < 0.01f;
		}

		private void StartBossPhaseForCurrentWave()
		{
			if (!m_waves.TryGetValue(m_currentWaveIndex, out List<ZombieSpawnEntry> entries))
				return;

			var bossTypes = entries.Where(e => m_bossZombieTypes.Contains(e.Type)).Select(e => e.Type).ToList();
			if (bossTypes.Count == 0)
				return;

			m_pendingBosses = new List<string>(bossTypes);
			m_bossPhaseActive = true;
			m_activeBosses.Clear();

			if (m_currentWaveIndex == 19)
			{
				ShowMessageToAllPlayers(LanguageControl.Get("ZombiesSpawn", "FinalWave"), new Color(255, 0, 0));
			}
			else if (bossTypes.Count == 1)
			{
				string bossType = bossTypes[0];
				string messageKey = GetBossMessageKey(bossType);
				ShowMessageToAllPlayers(LanguageControl.Get("ZombiesSpawn", messageKey), new Color(255, 0, 0));
			}
			else
			{
				ShowMessageToAllPlayers(LanguageControl.Get("ZombiesSpawn", "BossesAppear"), new Color(255, 0, 0));
			}

			SpawnNextBoss();
		}

		private string GetBossMessageKey(string bossType)
		{
			if (bossType.StartsWith("Tank") && !bossType.Contains("Ghost"))
				return "BossTank";
			if (bossType.StartsWith("TankGhost"))
				return "BossGhostTank";
			if (bossType == "MachineGunInfected")
				return "BossMachineGun";
			if (bossType == "FlyingInfectedBoss")
				return "BossFlying";
			return "BossGeneric";
		}

		private void SpawnNextBoss()
		{
			if (m_pendingBosses.Count == 0) return;

			string bossType = m_pendingBosses[0];
			m_pendingBosses.RemoveAt(0);

			Vector3? spawnPos = FindSpawnPositionNearPlayers(bossType);
			if (spawnPos == null)
				return;

			Entity entity = SpawnEntity(bossType, spawnPos.Value);
			if (entity != null)
				m_activeBosses.Add(entity);
		}

		private void SpawnZombie()
		{
			if (!m_waves.TryGetValue(m_currentWaveIndex, out List<ZombieSpawnEntry> entries))
				return;

			// Excluir jefes (se manejan aparte)
			var regularEntries = entries.Where(e => !m_bossZombieTypes.Contains(e.Type)).ToList();
			if (regularEntries.Count == 0)
				return;

			int totalWeight = regularEntries.Sum(e => e.Weight);
			int r = m_random.Int(0, totalWeight - 1);
			string selectedType = null;

			foreach (var e in regularEntries)
			{
				if (r < e.Weight)
				{
					selectedType = e.Type;
					break;
				}
				r -= e.Weight;
			}

			if (selectedType == null) return;

			Vector3? spawnPos = FindSpawnPositionNearPlayers(selectedType);
			if (spawnPos == null) return;

			SpawnEntity(selectedType, spawnPos.Value);
		}

		private Vector3? FindSpawnPositionNearPlayers(string zombieType)
		{
			bool isFlying = zombieType.Contains("Fly") || zombieType.Contains("Flying") || zombieType.Contains("Bird");

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				Vector3 playerPos = player.ComponentBody.Position;

				for (int attempts = 0; attempts < 10; attempts++)
				{
					float angle = m_random.Float(0, 2 * MathF.PI);
					float distance = m_random.Float(20f, 30f);

					float dx = MathF.Cos(angle) * distance;
					float dz = MathF.Sin(angle) * distance;

					int x = Terrain.ToCell(playerPos.X + dx);
					int z = Terrain.ToCell(playerPos.Z + dz);
					int y;

					if (isFlying)
					{
						int groundY = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
						y = groundY + m_random.Int(5, 15);
					}
					else
					{
						y = m_subsystemTerrain.Terrain.GetTopHeight(x, z) + 1;

						int belowValue = m_subsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
						int belowContents = Terrain.ExtractContents(belowValue);
						Block belowBlock = BlocksManager.Blocks[belowContents];

						if (belowBlock == null || belowBlock.IsTransparent_(belowValue) ||
							m_forbiddenBlockNames.Contains(belowBlock.GetType().Name))
							continue;
					}

					int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
					int cellContents = Terrain.ExtractContents(cellValue);
					Block cellBlock = BlocksManager.Blocks[cellContents];

					if (cellBlock != null && !cellBlock.IsTransparent_(cellValue) && cellBlock.IsCollidable_(cellValue))
						continue;

					return new Vector3(x + 0.5f, y, z + 0.5f);
				}
			}

			return null;
		}

		private Entity SpawnEntity(string templateName, Vector3 position)
		{
			try
			{
				ValuesDictionary values = DatabaseManager.FindEntityValuesDictionary(templateName, true);
				Entity entity = Project.CreateEntity(values);

				ComponentBody body = entity.FindComponent<ComponentBody>(true);
				body.Position = position;
				body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, m_random.Float(0f, 2 * MathF.PI));

				Project.AddEntity(entity);
				return entity;
			}
			catch (Exception ex)
			{
				Log.Error($"Error al spawnear {templateName}: {ex.Message}");
				return null;
			}
		}

		private void ShowMessageToAllPlayers(string message, Color color)
		{
			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				player.ComponentGui.DisplaySmallMessage(message, color, false, true);
			}
		}

		private class ZombieSpawnEntry
		{
			public string Type;
			public int Weight;
		}

		// Clase CreatureType copiada de SubsystemCreatureSpawn
		private class CreatureType
		{
			public string Name;
			public SpawnLocationType SpawnLocationType;
			public bool RandomSpawn;
			public bool ConstantSpawn;
			public Func<CreatureType, Point3, float> SpawnSuitabilityFunction;
			public Func<CreatureType, Point3, int> SpawnFunction;

			public CreatureType(string name, SpawnLocationType spawnLocationType, bool randomSpawn, bool constantSpawn)
			{
				Name = name;
				SpawnLocationType = spawnLocationType;
				RandomSpawn = randomSpawn;
				ConstantSpawn = constantSpawn;
			}

			public override string ToString()
			{
				return Name;
			}
		}
	}
}
