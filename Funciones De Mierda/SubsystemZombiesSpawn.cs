using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
		private SubsystemSeasons m_subsystemSeasons;   // NUEVO
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

		// Estado de jefes
		private bool m_bossBattleActive;
		private Queue<string> m_bossQueue = new Queue<string>();
		private Entity m_currentBossEntity;
		private bool m_hasSpawnedBossThisNight;

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
			m_subsystemSeasons = Project.FindSubsystem<SubsystemSeasons>(true);   // NUEVO

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

			if (!m_wasGreenNightActive && isGreenNightActive)
			{
				SendWaveMessage();
			}
			m_wasGreenNightActive = isGreenNightActive;

			if (!isGreenNightActive)
				return;

			float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
			float midnight = m_subsystemTimeOfDay.Midnight;
			bool isMidnight = Math.Abs(timeOfDay - midnight) < 0.01f;

			if (!m_hasSpawnedBossThisNight && isMidnight && !m_bossBattleActive)
			{
				StartBossBattle();
			}

			if (m_bossBattleActive)
			{
				if (m_currentBossEntity == null || !IsEntityAlive(m_currentBossEntity))
				{
					AdvanceBossBattle();
				}
			}

			float effectiveInterval = m_bossBattleActive ? m_spawnInterval * 2f : m_spawnInterval;
			m_spawnTimer += dt;

			while (m_spawnTimer >= effectiveInterval)
			{
				m_spawnTimer -= effectiveInterval;
				TrySpawnCreature();
			}
		}

		private void SendWaveMessage()
		{
			string message;
			int maxWave = m_waves.Keys.Max();

			if (m_currentWave == maxWave)
			{
				message = LanguageControl.Get("ZombiesSpawn", "FinalWave");
				foreach (var player in m_subsystemPlayers.ComponentPlayers)
				{
					player.ComponentGui.DisplaySmallMessage(message, Color.White, false, true);
				}
			}
			else
			{
				message = string.Format(LanguageControl.Get("ZombiesSpawn", "WaveMessage"), m_currentWave);
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
				for (int i = 0; i < 30; i++)
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
			for (int i = 1; i <= 28; i++)
			{
				try
				{
					string content = ContentManager.Get<string>("Waves/" + i);
					if (!string.IsNullOrEmpty(content))
					{
						var entries = new List<WaveEntry>();
						string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

						foreach (string line in lines)
						{
							string[] parts = line.Split(';');
							if (parts.Length != 2) continue;

							string name = parts[0].Trim();
							if (int.TryParse(parts[1], out int weight))
							{
								entries.Add(new WaveEntry(name, weight));
							}
						}

						if (entries.Count > 0)
						{
							m_waves[i] = entries;
						}
					}
				}
				catch
				{
					// Ignorar
				}
			}

			if (m_waves.Count == 0)
			{
				LoadDefaultWaves();
			}
		}

		private void LoadDefaultWaves()
		{
			// ... (contenido por defecto, pero ya se carga desde archivos)
			// Nota: No es necesario modificar los defaults porque ahora los archivos .txt ya incluyen Freezer.
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
			m_hasSpawnedBossThisNight = true;

			var bosses = new List<string>();
			foreach (var entry in m_currentWaveEntries)
			{
				if (BossTemplates.Contains(entry.TemplateName) && !bosses.Contains(entry.TemplateName))
					bosses.Add(entry.TemplateName);
			}

			if (bosses.Count == 0)
				return;

			m_bossQueue.Clear();
			foreach (string boss in bosses)
				m_bossQueue.Enqueue(boss);

			m_bossBattleActive = true;
			SpawnNextBoss();
		}

		private void SpawnNextBoss()
		{
			if (m_bossQueue.Count == 0)
			{
				m_bossBattleActive = false;
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
					for (int i = 0; i < 10; i++)
					{
						float angle = m_random.Float(0, 2 * MathUtils.PI);
						float distance = m_random.Float(20, 30);
						int x = (int)(player.ComponentBody.Position.X + MathF.Cos(angle) * distance);
						int z = (int)(player.ComponentBody.Position.Z + MathF.Sin(angle) * distance);
						int y = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
						if (y > 0 && y < 255)
						{
							int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
							int contents = Terrain.ExtractContents(cellValue);
							Block block = BlocksManager.Blocks[contents];
							string blockName = block.GetType().Name;
							if (!m_forbiddenBlockNames.Contains(blockName) && block.IsCollidable)
							{
								spawnPos = new Vector3(x + 0.5f, y, z + 0.5f);
								break;
							}
						}
					}
					if (spawnPos == Vector3.Zero)
					{
						spawnPos = player.ComponentBody.Position + new Vector3(0, 2, 0);
					}
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
				for (int i = 0; i < 50; i++)
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
			SpawnNextBoss();
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

			// Los jefes no se spawnen aquí
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

			// --- NUEVO: Condición para InfectedFreezer ---
			if (entry.TemplateName == "InfectedFreezer")
			{
				bool canSpawn = false;

				// 1. Si es invierno, siempre puede aparecer
				if (m_subsystemSeasons.Season == Season.Winter)
				{
					canSpawn = true;
				}
				else
				{
					// 2. Comprobar temperatura en la posición de spawn
					int x = Terrain.ToCell(spawnPos.X);
					int z = Terrain.ToCell(spawnPos.Z);
					int temperature = m_subsystemTerrain.Terrain.GetTemperature(x, z);
					if (temperature < 8)  // Frío (umbral similar al oso polar)
					{
						canSpawn = true;
					}
				}

				if (!canSpawn)
				{
					// No se spawneará este Freezer en esta ocasión
					return;
				}
			}
			// ---------------------------------------------

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
				for (int i = 0; i < 30; i++)
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
				for (int i = 0; i < 20; i++)
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
				for (int i = 0; i < 10; i++)
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

		private Vector3 GetRandomSpawnPointAroundPlayers(bool forceSurface = false)
		{
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				var camera = player.GameWidget.ActiveCamera;
				for (int i = 0; i < 5; i++)
				{
					var point = m_subsystemCreatureSpawn.GetRandomSpawnPoint(camera, SpawnLocationType.Surface);
					if (point.HasValue)
					{
						return new Vector3(point.Value.X + 0.5f, point.Value.Y, point.Value.Z + 0.5f);
					}
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
			if (bossTemplate.StartsWith("GhostTank"))
				return "BossGhostTank";
			if (bossTemplate == "MachineGunInfected")
				return "BossMachineGun";
			if (bossTemplate == "FlyingInfectedBoss")
				return "BossFlying";
			return "BossGeneric";
		}

		private class WaveEntry
		{
			public string TemplateName { get; }
			public int Weight { get; }

			public WaveEntry(string name, int weight)
			{
				TemplateName = name;
				Weight = weight;
			}
		}
	}
}
