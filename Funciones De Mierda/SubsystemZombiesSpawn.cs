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
		private Random m_random = new Random();

		// Datos de oleadas
		private Dictionary<int, List<WaveEntry>> m_waves = new Dictionary<int, List<WaveEntry>>();
		private int m_currentWave = 1;
		private List<WaveEntry> m_currentWaveEntries;

		// Control de spawn
		private float m_spawnTimer;
		private float m_spawnInterval = 2f;
		private const int MaxCreaturesPerArea = 80;
		private const int MaxGlobalCreatures = 50;

		// Estado de jefes
		private bool m_bossBattleActive;
		private Queue<string> m_bossQueue = new Queue<string>();
		private Entity m_currentBossEntity;
		private bool m_hasSpawnedBossThisNight;

		// Control de avance de oleada
		private bool m_wasGreenNightActive;

		// Listas estáticas de templates
		private static readonly HashSet<string> BossTemplates = new HashSet<string>
		{
			"Tank1", "Tank2", "Tank3",
			"GhostTank1", "GhostTank2", "GhostTank3",
			"MachineGunInfected", "FlyingInfectedBoss"
		};

		private static readonly HashSet<string> MiniBossTemplates = new HashSet<string>
		{
			"InfectedBear", "InfectedWildboar"
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
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemGameWidgets = Project.FindSubsystem<SubsystemGameWidgets>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);

			LoadWavesFromResources();
			m_currentWave = valuesDictionary.GetValue<int>("CurrentWave", 1);
			SetCurrentWave(m_currentWave);
			m_wasGreenNightActive = m_subsystemGreenNightSky.IsGreenNightActive;
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			valuesDictionary.SetValue("CurrentWave", m_currentWave);
		}

		public void Update(float dt)
		{
			bool isGreenNightActive = m_subsystemGreenNightSky.IsGreenNightActive;

			if (m_wasGreenNightActive && !isGreenNightActive)
			{
				AdvanceToNextWave();
			}
			m_wasGreenNightActive = isGreenNightActive;

			if (!isGreenNightActive)
				return;

			float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
			bool isMidnight = Math.Abs(timeOfDay - 0.5f) < 0.005f;

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

		private void AdvanceToNextWave()
		{
			m_hasSpawnedBossThisNight = false;
			m_bossBattleActive = false;
			m_bossQueue.Clear();
			m_currentBossEntity = null;

			int nextWave = m_currentWave + 1;
			if (nextWave <= 19 && m_waves.ContainsKey(nextWave))
			{
				m_currentWave = nextWave;
				SetCurrentWave(m_currentWave);
				SendMessageToAllPlayers("ZombiesSpawn", "WaveAdvanced", new Color(0, 255, 0));
				Log.Information($"ZombiesSpawn: Advanced to wave {m_currentWave}");
			}
			else if (m_currentWave == 19)
			{
				SendMessageToAllPlayers("ZombiesSpawn", "FinalWave", new Color(255, 0, 0));
				Log.Information("ZombiesSpawn: Already at max wave 19");
			}
			else
			{
				Log.Warning($"ZombiesSpawn: Could not advance to wave {nextWave} - not found in waves dictionary");
			}
		}

		private void LoadWavesFromResources()
		{
			for (int i = 1; i <= 19; i++)
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
							Log.Information($"ZombiesSpawn: Loaded wave {i} with {entries.Count} entries");
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
				Log.Warning("ZombiesSpawn: No wave resources found, loading default waves");
				LoadDefaultWaves();
			}
		}

		private void LoadDefaultWaves()
		{
			var defaultWaves = new Dictionary<int, string>
			{
				{1, "HumanoidSkeleton;40\nInfectedBird;35\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFly1;4"},
				{2, "HumanoidSkeleton;40\nInfectedBird;35\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedFly1;6"},
				{3, "HumanoidSkeleton;35\nInfectedBird;30\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedMuscle1;35\nInfectedMuscle2;35\nInfectedFly1;6"},
				{4, "HumanoidSkeleton;35\nInfectedBird;30\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nInfectedFly1;6"},
				{5, "HumanoidSkeleton;30\nInfectedBird;25\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nInfectedFly1;6\nInfectedFly2;8\nBoomer1;10\nInfectedHyena;20"},
				{6, "HumanoidSkeleton;30\nInfectedBird;25\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nInfectedFly1;6\nInfectedFly2;8\nBoomer1;10\nBoomer2;10\nInfectedHyena;22\nPredatoryChameleon;5"},
				{7, "HumanoidSkeleton;30\nInfectedBird;25\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nInfectedHyena;25\nPredatoryChameleon;7"},
				{8, "HumanoidSkeleton;30\nInfectedBird;25\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nCharger1;6\nInfectedHyena;28\nPredatoryChameleon;8"},
				{9, "HumanoidSkeleton;30\nInfectedBird;25\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nCharger2;6\nInfectedHyena;30\nPredatoryChameleon;10"},
				{10, "HumanoidSkeleton;25\nInfectedBird;20\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nCharger1;6\nCharger2;6\nInfectedHyena;30\nInfectedWildboar;12\nPredatoryChameleon;12"},
				{11, "HumanoidSkeleton;25\nInfectedBird;20\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nGhostBoomer2;6\nCharger1;6\nCharger2;6\nTank1;1\nInfectedHyena;30\nInfectedWildboar;15\nInfectedBear;8\nPredatoryChameleon;15"},
				{12, "HumanoidSkeleton;25\nInfectedBird;20\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nGhostBoomer2;6\nCharger1;6\nCharger2;6\nTankGhost1;1\nInfectedHyena;30\nInfectedWildboar;15\nInfectedBear;10\nPredatoryChameleon;18"},
				{13, "HumanoidSkeleton;25\nInfectedBird;20\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nGhostBoomer2;6\nGhostBoomer3;6\nGhostCharger;4\nCharger1;6\nCharger2;6\nTank2;1\nInfectedHyena;30\nInfectedWildboar;15\nInfectedBear;12\nPredatoryChameleon;20"},
				{14, "HumanoidSkeleton;25\nInfectedBird;20\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nGhostBoomer2;6\nGhostBoomer3;6\nGhostCharger;4\nCharger1;6\nCharger2;6\nTank3;1\nInfectedHyena;30\nInfectedWildboar;15\nInfectedBear;15\nPredatoryChameleon;22"},
				{15, "HumanoidSkeleton;20\nInfectedBird;15\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nGhostBoomer2;6\nGhostBoomer3;6\nGhostCharger;4\nCharger1;6\nCharger2;6\nTankGhost3;1\nInfectedHyena;30\nInfectedWildboar;15\nInfectedBear;18\nPredatoryChameleon;25"},
				{16, "HumanoidSkeleton;20\nInfectedBird;15\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nGhostBoomer2;6\nGhostBoomer3;6\nGhostCharger;4\nCharger1;6\nCharger2;6\nFlyingInfectedBoss;1\nInfectedHyena;30\nInfectedWildboar;15\nInfectedBear;20\nPredatoryChameleon;28"},
				{17, "HumanoidSkeleton;20\nInfectedBird;15\nInfectedNormal1;35\nInfectedNormal2;35\nGhostNormal;20\nInfectedFast1;30\nInfectedFast2;30\nGhostFast;15\nInfectedMuscle1;40\nInfectedMuscle2;40\nPoisonousInfected1;25\nPoisonousInfected2;25\nPoisonousGhost;15\nInfectedFly1;8\nInfectedFly2;10\nInfectedFly3;7\nBoomer1;12\nBoomer2;12\nBoomer3;12\nGhostBoomer1;8\nGhostBoomer2;8\nGhostBoomer3;8\nGhostCharger;6\nCharger1;8\nCharger2;8\nInfectedHyena;35\nInfectedWildboar;18\nInfectedBear;22\nPredatoryChameleon;30"},
				{18, "HumanoidSkeleton;20\nInfectedBird;15\nInfectedNormal1;35\nInfectedNormal2;35\nGhostNormal;20\nInfectedFast1;30\nInfectedFast2;30\nGhostFast;15\nInfectedMuscle1;40\nInfectedMuscle2;40\nPoisonousInfected1;25\nPoisonousInfected2;25\nPoisonousGhost;15\nInfectedFly1;8\nInfectedFly2;10\nInfectedFly3;6\nBoomer1;12\nBoomer2;12\nBoomer3;12\nGhostBoomer1;8\nGhostBoomer2;8\nGhostBoomer3;8\nGhostCharger;6\nCharger1;8\nCharger2;8\nMachineGunInfected;1\nInfectedHyena;35\nInfectedWildboar;18\nInfectedBear;25\nPredatoryChameleon;32"},
				{19, "HumanoidSkeleton;15\nInfectedBird;10\nInfectedNormal1;40\nInfectedNormal2;40\nGhostNormal;25\nInfectedFast1;35\nInfectedFast2;35\nGhostFast;20\nInfectedMuscle1;45\nInfectedMuscle2;45\nPoisonousInfected1;30\nPoisonousInfected2;30\nPoisonousGhost;20\nInfectedFly1;10\nInfectedFly2;12\nInfectedFly3;8\nBoomer1;15\nBoomer2;15\nBoomer3;15\nGhostBoomer1;10\nGhostBoomer2;10\nGhostBoomer3;10\nCharger1;12\nCharger2;12\nGhostCharger;8\nTank1;1\nTank2;1\nTank3;1\nTankGhost1;1\nTankGhost2;1\nTankGhost3;1\nMachineGunInfected;1\nFlyingInfectedBoss;1\nInfectedHyena;40\nInfectedWildboar;20\nInfectedBear;28\nPredatoryChameleon;35"}
			};

			foreach (var kvp in defaultWaves)
			{
				var entries = new List<WaveEntry>();
				string[] lines = kvp.Value.Split('\n');
				foreach (string line in lines)
				{
					if (string.IsNullOrWhiteSpace(line)) continue;
					string[] parts = line.Split(';');
					if (parts.Length != 2) continue;
					string name = parts[0].Trim();
					if (int.TryParse(parts[1], out int weight))
					{
						entries.Add(new WaveEntry(name, weight));
					}
				}
				if (entries.Count > 0)
					m_waves[kvp.Key] = entries;
			}
		}

		private void SetCurrentWave(int wave)
		{
			if (m_waves.TryGetValue(wave, out var entries))
			{
				m_currentWaveEntries = entries;
				m_currentWave = wave;
				Log.Information($"ZombiesSpawn: Now using wave {wave} with {entries.Count} enemy types");
			}
			else
			{
				m_currentWaveEntries = m_waves.ContainsKey(1) ? m_waves[1] : new List<WaveEntry>();
				m_currentWave = 1;
				Log.Warning($"ZombiesSpawn: Wave {wave} not found, defaulting to wave 1");
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
			SendMessageToAllPlayers("ZombiesSpawn", "BossesAppear", new Color(255, 0, 0));
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
			Vector3 spawnPos = GetRandomSpawnPointAroundPlayers(true);
			if (spawnPos == Vector3.Zero)
			{
				m_bossQueue.Enqueue(bossTemplate);
				return;
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

		private void AdvanceBossBattle()
		{
			m_currentBossEntity = null;
			SpawnNextBoss();
		}

		private bool IsEntityAlive(Entity entity)
		{
			var health = entity.FindComponent<ComponentHealth>();
			return health != null && health.Health > 0f;
		}

		private void TrySpawnCreature()
		{
			// Verificar límite global de criaturas
			int totalCreatures = m_subsystemCreatureSpawn.CountCreatures(false);
			if (totalCreatures >= MaxGlobalCreatures)
				return;

			// Obtener punto de spawn válido (evitando bloques prohibidos)
			Vector3 spawnPos = GetValidSpawnPoint();
			if (spawnPos == Vector3.Zero)
				return;

			// Verificar límite de área
			Vector2 areaMin = new Vector2(spawnPos.X - 16, spawnPos.Z - 16);
			Vector2 areaMax = new Vector2(spawnPos.X + 16, spawnPos.Z + 16);
			int nearby = m_subsystemCreatureSpawn.CountCreaturesInArea(areaMin, areaMax, false);
			if (nearby >= MaxCreaturesPerArea)
				return;

			// Seleccionar criatura aleatoria ponderada
			var entry = GetRandomWeightedEntry(m_currentWaveEntries);
			if (entry == null)
				return;

			// Si es un jefe y ya estamos en batalla de jefes, no spawnear otro jefe aquí
			if (BossTemplates.Contains(entry.TemplateName) && m_bossBattleActive)
				return;

			// Spawnear la criatura
			m_subsystemCreatureSpawn.SpawnCreature(entry.TemplateName, spawnPos, false);

			// Mensaje para minijefes
			if (MiniBossTemplates.Contains(entry.TemplateName))
			{
				SendMessageToAllPlayers("ZombiesSpawn", "MiniBossAppear", new Color(255, 100, 0));
			}
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

		private Vector3 GetRandomSpawnPointAroundPlayers(bool forceSurface = false)
		{
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				var camera = player.GameWidget.ActiveCamera;
				for (int i = 0; i < 5; i++)
				{
					var point = m_subsystemCreatureSpawn.GetRandomSpawnPoint(camera, forceSurface ? SpawnLocationType.Surface : SpawnLocationType.Surface);
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
