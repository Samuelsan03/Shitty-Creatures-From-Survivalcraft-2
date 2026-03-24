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
		// Dependencias
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private SubsystemTimeOfDay m_subsystemTimeOfDay;
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemGameWidgets m_subsystemGameWidgets;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemSeasons m_subsystemSeasons;
		private Random m_random = new Random();

		// Datos de oleadas
		private Dictionary<int, List<WaveEntry>> m_waves = new Dictionary<int, List<WaveEntry>>();
		private int m_currentWave = 1;
		private List<WaveEntry> m_currentWaveEntries;

		// Control de spawn
		private float m_spawnTimer;
		private float m_spawnInterval = 2f;
		private const int MaxCreaturesPerArea = 150;
		private const int MaxGlobalCreatures = 200;
		private const int MaxSpawnsPerFrame = 2;

		// Estado de jefes
		private bool m_bossBattleActive;
		private Queue<string> m_bossQueue = new Queue<string>();
		private Entity m_currentBossEntity;
		private bool m_hasSpawnedBossThisNight;
		private bool m_bossSpawnDelayed = false;
		private float m_bossSpawnDelayTimer = 0f;
		private const float BossSpawnDelay = 0.5f;

		// Control de avance de oleada
		private bool m_wasGreenNightActive;
		private bool m_isAdvancingWave = false;

		// Listas estáticas de templates
		private static readonly HashSet<string> BossTemplates = new HashSet<string>
		{
			"Tank1", "Tank2", "Tank3",
			"TankGhost1", "TankGhost2", "TankGhost3",
			"MachineGunInfected", "FlyingInfectedBoss"
		};

		private static readonly HashSet<string> MiniBossTemplates = new HashSet<string>
		{
			"InfectedBear", "InfectedWildboar"
		};

		// Voladores (spawnean en aire)
		private static readonly HashSet<string> FlyingTemplates = new HashSet<string>
		{
			"InfectedFly1", "InfectedFly2", "InfectedFly3",
			"FlyingInfectedBoss", "InfectedBird"
		};

		// Bloques prohibidos para spawnear
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

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemGreenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>(true);
			if (m_subsystemGreenNightSky != null)
			{
				m_subsystemGreenNightSky.NaturalNightEnded += OnNaturalNightEnded;
			}

			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemGameWidgets = Project.FindSubsystem<SubsystemGameWidgets>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemSeasons = Project.FindSubsystem<SubsystemSeasons>(true);

			LoadWavesFromResources();
			m_currentWave = valuesDictionary.GetValue<int>("CurrentWave", 1);
			SetCurrentWave(m_currentWave);
			m_wasGreenNightActive = m_subsystemGreenNightSky.IsGreenNightActive;
		}

		private void OnNaturalNightEnded()
		{
			AdvanceToNextWave();
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			valuesDictionary.SetValue("CurrentWave", m_currentWave);
		}

		public void Update(float dt)
		{
			bool isGreenNightActive = m_subsystemGreenNightSky.IsGreenNightActive;
			int maxWave = m_waves.Keys.Max();

			if (!m_wasGreenNightActive && isGreenNightActive)
			{
				SendWaveMessage();

				// Si es la ola final, iniciar batalla de jefes inmediatamente al comenzar la noche
				if (m_currentWave == maxWave && !m_hasSpawnedBossThisNight && !m_bossBattleActive)
				{
					StartBossBattle();
					m_bossSpawnDelayed = true;
					m_bossSpawnDelayTimer = 0.5f;
				}
			}
			m_wasGreenNightActive = isGreenNightActive;

			if (!isGreenNightActive)
				return;

			float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
			float midnight = m_subsystemTimeOfDay.Midnight;
			bool isMidnight = Math.Abs(timeOfDay - midnight) < 0.01f;

			// Solo para olas NO finales, los jefes aparecen en medianoche
			if (m_currentWave != maxWave)
			{
				if (!m_hasSpawnedBossThisNight && isMidnight && !m_bossBattleActive && !m_bossSpawnDelayed)
				{
					StartBossBattle();
					m_bossSpawnDelayed = true;
					m_bossSpawnDelayTimer = 0.5f;
				}
			}

			// Procesar delay de spawn de jefes
			if (m_bossSpawnDelayed)
			{
				m_bossSpawnDelayTimer -= dt;
				if (m_bossSpawnDelayTimer <= 0f)
				{
					m_bossSpawnDelayed = false;
					if (m_bossBattleActive && m_currentBossEntity == null)
					{
						SpawnNextBoss();
					}
				}
			}

			if (m_bossBattleActive)
			{
				if (m_currentBossEntity != null && !IsEntityAlive(m_currentBossEntity))
				{
					m_currentBossEntity = null;
					AdvanceBossBattle();
				}
			}

			// Control de spawn de criaturas normales con límite por frame
			float effectiveInterval = m_bossBattleActive ? m_spawnInterval * 2f : m_spawnInterval;
			m_spawnTimer += dt;
			int spawnsThisFrame = 0;

			while (m_spawnTimer >= effectiveInterval && spawnsThisFrame < MaxSpawnsPerFrame)
			{
				m_spawnTimer -= effectiveInterval;
				TrySpawnCreature();
				spawnsThisFrame++;
			}
		}

		private void SendWaveMessage()
		{
			int maxWave = m_waves.Keys.Max();

			if (m_currentWave == maxWave)
			{
				string message = LanguageControl.Get("ZombiesSpawn", "FinalWave");
				foreach (var player in m_subsystemPlayers.ComponentPlayers)
				{
					player.ComponentGui.DisplaySmallMessage(message, new Color(255, 0, 0), true, true);
				}
			}
			else
			{
				string message = string.Format(LanguageControl.Get("ZombiesSpawn", "WaveMessage"), m_currentWave);
				foreach (var player in m_subsystemPlayers.ComponentPlayers)
				{
					player.ComponentGui.DisplayLargeMessage(message, "", 3f, 0f);
				}
			}
		}

		private void AdvanceToNextWave()
		{
			if (m_isAdvancingWave) return;
			m_isAdvancingWave = true;

			m_hasSpawnedBossThisNight = false;
			m_bossBattleActive = false;
			m_bossSpawnDelayed = false;
			m_bossQueue.Clear();
			m_currentBossEntity = null;

			int nextWave = m_currentWave + 1;
			int maxWave = m_waves.Keys.Max();
			if (nextWave <= maxWave && m_waves.ContainsKey(nextWave))
			{
				m_currentWave = nextWave;
				SetCurrentWave(m_currentWave);
			}

			m_isAdvancingWave = false;
		}

		private Vector3 GetBossSpawnPoint()
		{
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				Vector3 playerPos = player.ComponentBody.Position;
				for (int i = 0; i < 15; i++)
				{
					float angle = m_random.Float(0, 2 * MathUtils.PI);
					float distance = m_random.Float(40, 70);
					int x = (int)(playerPos.X + MathF.Cos(angle) * distance);
					int z = (int)(playerPos.Z + MathF.Sin(angle) * distance);
					int y = m_subsystemTerrain.Terrain.GetTopHeight(x, z);

					if (y > 0 && y < 255)
					{
						int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
						int contents = Terrain.ExtractContents(cellValue);
						Block block = BlocksManager.Blocks[contents];

						string blockName = block.GetType().Name;
						if (!m_forbiddenBlockNames.Contains(blockName) && block.IsCollidable)
						{
							return new Vector3(x + 0.5f, y, z + 0.5f);
						}
					}
				}
			}
			return Vector3.Zero;
		}

		private void LoadWavesFromResources()
		{
			m_waves = WavesData.LoadFromXml();

			if (m_waves.Count == 0)
			{
				Log.Error("No se pudieron cargar las oleadas desde Waves.xml. El sistema de aparición de zombis no funcionará correctamente.");
				var defaultWave = new List<WaveEntry>
				{
					new WaveEntry("HumanoidSkeleton", 40),
					new WaveEntry("InfectedBird", 35),
					new WaveEntry("InfectedNormal1", 30),
					new WaveEntry("InfectedNormal2", 30),
					new WaveEntry("InfectedFly1", 4)
				};
				m_waves[1] = defaultWave;
				Log.Warning("Usando oleada por defecto de emergencia.");
			}
		}

		private void SetCurrentWave(int wave)
		{
			if (m_waves.TryGetValue(wave, out var entries))
			{
				m_currentWaveEntries = entries;
				m_currentWave = wave;
				m_spawnInterval = Math.Max(0.6f, 2.0f - (wave * 0.05f));
			}
			else
			{
				m_currentWaveEntries = m_waves.ContainsKey(1) ? m_waves[1] : new List<WaveEntry>();
				m_currentWave = 1;
				m_spawnInterval = 2.0f;
			}
		}

		private void StartBossBattle()
		{
			if (m_bossBattleActive) return;

			m_hasSpawnedBossThisNight = true;
			m_bossBattleActive = true;
			m_bossQueue.Clear();

			var bosses = new List<string>();
			foreach (var entry in m_currentWaveEntries)
			{
				if (BossTemplates.Contains(entry.TemplateName) && !bosses.Contains(entry.TemplateName))
					bosses.Add(entry.TemplateName);
			}

			if (bosses.Count == 0)
			{
				m_bossBattleActive = false;
				return;
			}

			foreach (string boss in bosses)
				m_bossQueue.Enqueue(boss);
		}

		private void SpawnNextBoss()
		{
			if (m_bossQueue.Count == 0)
			{
				m_bossBattleActive = false;
				m_currentBossEntity = null;
				return;
			}

			string bossTemplate = m_bossQueue.Dequeue();
			Vector3 spawnPos = Vector3.Zero;

			for (int attempt = 0; attempt < 3; attempt++)
			{
				spawnPos = GetBossSpawnPoint();
				if (spawnPos != Vector3.Zero)
					break;
			}

			if (spawnPos == Vector3.Zero)
			{
				spawnPos = GetAlternativeBossSpawnPoint();
			}

			if (spawnPos == Vector3.Zero)
			{
				var player = m_subsystemPlayers.ComponentPlayers.FirstOrDefault();
				if (player != null)
				{
					spawnPos = player.ComponentBody.Position + new Vector3(0, 2, 0);
				}
				else
				{
					m_bossQueue.Enqueue(bossTemplate);
					return;
				}
			}

			m_currentBossEntity = m_subsystemCreatureSpawn.SpawnCreature(bossTemplate, spawnPos, false);
			if (m_currentBossEntity != null)
			{
				string messageKey = GetBossMessageKey(bossTemplate);
				SendMessageToAllPlayers("ZombiesSpawn", messageKey, new Color(255, 0, 0));
			}
			else
			{
				AdvanceBossBattle();
			}
		}

		private Vector3 GetAlternativeBossSpawnPoint()
		{
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				Vector3 playerPos = player.ComponentBody.Position;
				for (int i = 0; i < 30; i++)
				{
					float angle = m_random.Float(0, 2 * MathUtils.PI);
					float distance = m_random.Float(30, 80);
					int x = (int)(playerPos.X + MathF.Cos(angle) * distance);
					int z = (int)(playerPos.Z + MathF.Sin(angle) * distance);
					int y = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
					if (y > 0 && y < 255)
					{
						int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
						int contents = Terrain.ExtractContents(cellValue);
						Block block = BlocksManager.Blocks[contents];
						string blockName = block.GetType().Name;
						if (!m_forbiddenBlockNames.Contains(blockName) && block.IsCollidable)
						{
							return new Vector3(x + 0.5f, y, z + 0.5f);
						}
					}
				}
			}
			return Vector3.Zero;
		}

		private void AdvanceBossBattle()
		{
			m_currentBossEntity = null;

			if (m_bossQueue.Count == 0)
			{
				m_bossBattleActive = false;
			}
			else
			{
				m_bossSpawnDelayed = true;
				m_bossSpawnDelayTimer = BossSpawnDelay;
			}
		}

		private bool IsEntityAlive(Entity entity)
		{
			if (entity == null) return false;
			if (!Project.Entities.Contains(entity))
				return false;
			var health = entity.FindComponent<ComponentHealth>();
			return health != null && health.Health > 0f;
		}

		private void TrySpawnCreature()
		{
			int totalCreatures = m_subsystemCreatureSpawn.CountCreatures(false);
			if (totalCreatures >= MaxGlobalCreatures)
				return;

			var entry = GetRandomWeightedEntry(m_currentWaveEntries);
			if (entry == null)
				return;

			if (BossTemplates.Contains(entry.TemplateName))
				return;

			Vector3 spawnPos;

			if (FlyingTemplates.Contains(entry.TemplateName))
			{
				spawnPos = GetRandomFlyingSpawnPoint();
			}
			else
			{
				spawnPos = GetValidSpawnPoint();
			}

			if (spawnPos == Vector3.Zero)
				return;

			if (entry.TemplateName == "InfectedFreezer")
			{
				bool canSpawn = false;

				if (m_subsystemSeasons.Season == Season.Winter)
				{
					canSpawn = true;
				}
				else
				{
					int x = Terrain.ToCell(spawnPos.X);
					int z = Terrain.ToCell(spawnPos.Z);
					int temperature = m_subsystemTerrain.Terrain.GetTemperature(x, z);
					if (temperature < 8)
					{
						canSpawn = true;
					}
				}

				if (!canSpawn)
				{
					return;
				}
			}

			Vector2 areaMin = new Vector2(spawnPos.X - 16, spawnPos.Z - 16);
			Vector2 areaMax = new Vector2(spawnPos.X + 16, spawnPos.Z + 16);
			int nearby = m_subsystemCreatureSpawn.CountCreaturesInArea(areaMin, areaMax, false);
			if (nearby >= MaxCreaturesPerArea)
				return;

			m_subsystemCreatureSpawn.SpawnCreature(entry.TemplateName, spawnPos, false);
		}

		private Vector3 GetValidSpawnPoint()
		{
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				var camera = player.GameWidget.ActiveCamera;
				for (int i = 0; i < 10; i++)
				{
					var point = m_subsystemCreatureSpawn.GetRandomSpawnPoint(camera, SpawnLocationType.Surface);
					if (point.HasValue)
					{
						int x = point.Value.X;
						int y = point.Value.Y - 1;
						int z = point.Value.Z;

						int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
						int contents = Terrain.ExtractContents(cellValue);
						Block block = BlocksManager.Blocks[contents];

						string blockName = block.GetType().Name;
						if (!m_forbiddenBlockNames.Contains(blockName))
						{
							return new Vector3(point.Value.X + 0.5f, point.Value.Y, point.Value.Z + 0.5f);
						}
					}
				}
			}
			return Vector3.Zero;
		}

		private Vector3 GetRandomFlyingSpawnPoint()
		{
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				var camera = player.GameWidget.ActiveCamera;
				for (int i = 0; i < 8; i++)
				{
					var point = m_subsystemCreatureSpawn.GetRandomSpawnPoint(camera, SpawnLocationType.Surface);
					if (point.HasValue)
					{
						int groundY = point.Value.Y;
						int airY = groundY + m_random.Int(10, 30);

						if (airY >= 1 && airY <= 255)
						{
							return new Vector3(point.Value.X + 0.5f, airY, point.Value.Z + 0.5f);
						}
					}
				}

				Vector3 playerPos = player.ComponentBody.Position;
				for (int i = 0; i < 5; i++)
				{
					float angle = m_random.Float(0, 2 * MathUtils.PI);
					float distance = m_random.Float(20, 40);
					int x = (int)(playerPos.X + MathF.Cos(angle) * distance);
					int z = (int)(playerPos.Z + MathF.Sin(angle) * distance);
					int y = m_random.Int(70, 110);

					return new Vector3(x + 0.5f, y, z + 0.5f);
				}
			}
			return Vector3.Zero;
		}

		private WaveEntry GetRandomWeightedEntry(List<WaveEntry> entries)
		{
			int totalWeight = entries.Sum(e => e.Weight);
			if (totalWeight <= 0) return null;

			int r = m_random.Int(0, totalWeight - 1);
			int cumulative = 0;

			foreach (var e in entries)
			{
				cumulative += e.Weight;
				if (r < cumulative)
					return e;
			}

			return entries.LastOrDefault();
		}

		private void SendMessageToAllPlayers(string className, string key, Color color)
		{
			string message = LanguageControl.Get(className, key);
			if (string.IsNullOrEmpty(message))
				message = key;

			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				player.ComponentGui.DisplaySmallMessage(message, color, false, true);
			}
		}

		private string GetBossMessageKey(string bossTemplate)
		{
			if (bossTemplate.StartsWith("Tank"))
				return "BossTank";
			if (bossTemplate.StartsWith("GhostTank") || bossTemplate.StartsWith("TankGhost"))
				return "BossGhostTank";
			if (bossTemplate == "MachineGunInfected")
				return "BossMachineGun";
			if (bossTemplate == "FlyingInfectedBoss")
				return "BossFlying";
			return "BossGeneric";
		}
	}
}
