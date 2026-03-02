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
		private const int MaxGlobalCreatures = 100;

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

		// Voladores (spawnean en aire)
		private static readonly HashSet<string> FlyingTemplates = new HashSet<string>
		{
			"InfectedFly1", "InfectedFly2", "InfectedFly3",
			"FlyingInfectedBoss", "InfectedBird"
		};

		// Bloques prohibidos para spawnear (reducida)
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
			float midnight = (m_subsystemTimeOfDay.Middusk + m_subsystemTimeOfDay.Middawn) / 2f;
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
				if (m_currentWave == 19)
				{
					SendMessageToAllPlayers("ZombiesSpawn", "FinalWave", new Color(255, 0, 0));
				}
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
			var defaultWaves = new Dictionary<int, string>
			{
				{1, "HumanoidSkeleton;40\nInfectedBird;35\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFly1;4"},
				{2, "HumanoidSkeleton;40\nInfectedBird;35\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedFly1;6"},
				{3, "HumanoidSkeleton;35\nInfectedBird;30\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedMuscle1;35\nInfectedMuscle2;35\nInfectedFly1;6"},
				{4, "HumanoidSkeleton;35\nInfectedBird;30\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nInfectedFly1;6"},
				{5, "HumanoidSkeleton;30\nInfectedBird;25\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nInfectedFly1;6\nInfectedFly2;8\nBoomer1;10\nInfectedHyena;20\nInfectedWolf;5"},
				{6, "HumanoidSkeleton;30\nInfectedBird;25\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nInfectedFly1;6\nInfectedFly2;8\nBoomer1;10\nBoomer2;10\nInfectedHyena;22\nPredatoryChameleon;5\nInfectedWolf;7\nInfectedWerewolf;2"},
				{7, "HumanoidSkeleton;30\nInfectedBird;25\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nInfectedHyena;25\nPredatoryChameleon;7\nInfectedWolf;8\nInfectedWerewolf;3"},
				{8, "HumanoidSkeleton;30\nInfectedBird;25\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nCharger1;6\nInfectedHyena;28\nPredatoryChameleon;8\nInfectedWolf;10\nInfectedWerewolf;4"},
				{9, "HumanoidSkeleton;30\nInfectedBird;25\nInfectedNormal1;30\nInfectedNormal2;30\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nCharger2;6\nInfectedHyena;30\nPredatoryChameleon;10\nInfectedWolf;12\nInfectedWerewolf;5"},
				{10, "HumanoidSkeleton;25\nInfectedBird;20\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nCharger1;6\nCharger2;6\nInfectedHyena;30\nInfectedWildboar;12\nPredatoryChameleon;12\nInfectedWolf;15\nInfectedWerewolf;6"},
				{11, "HumanoidSkeleton;25\nInfectedBird;20\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nGhostBoomer2;6\nCharger1;6\nCharger2;6\nTank1;1\nInfectedHyena;30\nInfectedWildboar;15\nInfectedBear;8\nPredatoryChameleon;15\nInfectedWolf;18\nInfectedWerewolf;7"},
				{12, "HumanoidSkeleton;25\nInfectedBird;20\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nGhostBoomer2;6\nGhostBoomer3;6\nCharger1;6\nCharger2;6\nTankGhost1;1\nInfectedHyena;30\nInfectedWildboar;15\nInfectedBear;10\nPredatoryChameleon;18\nInfectedWolf;20\nInfectedWerewolf;8"},
				{13, "HumanoidSkeleton;25\nInfectedBird;20\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nGhostBoomer2;6\nGhostBoomer3;6\nGhostCharger;4\nCharger1;6\nCharger2;6\nTank2;1\nInfectedHyena;30\nInfectedWildboar;15\nInfectedBear;12\nPredatoryChameleon;20\nInfectedWolf;22\nInfectedWerewolf;9"},
				{14, "HumanoidSkeleton;25\nInfectedBird;20\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nGhostBoomer2;6\nGhostBoomer3;6\nGhostCharger;4\nCharger1;6\nCharger2;6\nTank3;1\nInfectedHyena;30\nInfectedWildboar;15\nInfectedBear;15\nPredatoryChameleon;22\nInfectedWolf;25\nInfectedWerewolf;10"},
				{15, "HumanoidSkeleton;20\nInfectedBird;15\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nGhostBoomer2;6\nGhostBoomer3;6\nGhostCharger;4\nCharger1;6\nCharger2;6\nTankGhost3;1\nInfectedHyena;30\nInfectedWildboar;15\nInfectedBear;18\nPredatoryChameleon;25\nInfectedWolf;28\nInfectedWerewolf;12"},
				{16, "HumanoidSkeleton;20\nInfectedBird;15\nInfectedNormal1;30\nInfectedNormal2;30\nGhostNormal;18\nInfectedFast1;25\nInfectedFast2;25\nGhostFast;12\nInfectedMuscle1;35\nInfectedMuscle2;35\nPoisonousInfected1;20\nPoisonousInfected2;20\nPoisonousGhost;12\nInfectedFly1;6\nInfectedFly2;8\nInfectedFly3;5\nBoomer1;10\nBoomer2;10\nBoomer3;10\nGhostBoomer1;6\nGhostBoomer2;6\nGhostBoomer3;6\nGhostCharger;4\nCharger1;6\nCharger2;6\nFlyingInfectedBoss;1\nInfectedHyena;30\nInfectedWildboar;15\nInfectedBear;20\nPredatoryChameleon;28\nInfectedWolf;30\nInfectedWerewolf;14"},
				{17, "HumanoidSkeleton;20\nInfectedBird;15\nInfectedNormal1;35\nInfectedNormal2;35\nGhostNormal;20\nInfectedFast1;30\nInfectedFast2;30\nGhostFast;15\nInfectedMuscle1;40\nInfectedMuscle2;40\nPoisonousInfected1;25\nPoisonousInfected2;25\nPoisonousGhost;15\nInfectedFly1;8\nInfectedFly2;10\nInfectedFly3;7\nBoomer1;12\nBoomer2;12\nBoomer3;12\nGhostBoomer1;8\nGhostBoomer2;8\nGhostBoomer3;8\nGhostCharger;6\nCharger1;8\nCharger2;8\nInfectedHyena;35\nInfectedWildboar;18\nInfectedBear;22\nPredatoryChameleon;30\nInfectedWolf;32\nInfectedWerewolf;15"},
				{18, "HumanoidSkeleton;20\nInfectedBird;15\nInfectedNormal1;35\nInfectedNormal2;35\nGhostNormal;20\nInfectedFast1;30\nInfectedFast2;30\nGhostFast;15\nInfectedMuscle1;40\nInfectedMuscle2;40\nPoisonousInfected1;25\nPoisonousInfected2;25\nPoisonousGhost;15\nInfectedFly1;8\nInfectedFly2;10\nInfectedFly3;6\nBoomer1;12\nBoomer2;12\nBoomer3;12\nGhostBoomer1;8\nGhostBoomer2;8\nGhostBoomer3;8\nGhostCharger;6\nCharger1;8\nCharger2;8\nMachineGunInfected;1\nInfectedHyena;35\nInfectedWildboar;18\nInfectedBear;25\nPredatoryChameleon;32\nInfectedWolf;35\nInfectedWerewolf;16"},
				{19, "HumanoidSkeleton;15\nInfectedBird;10\nInfectedNormal1;40\nInfectedNormal2;40\nGhostNormal;25\nInfectedFast1;35\nInfectedFast2;35\nGhostFast;20\nInfectedMuscle1;45\nInfectedMuscle2;45\nPoisonousInfected1;30\nPoisonousInfected2;30\nPoisonousGhost;20\nInfectedFly1;10\nInfectedFly2;12\nInfectedFly3;8\nBoomer1;15\nBoomer2;15\nBoomer3;15\nGhostBoomer1;10\nGhostBoomer2;10\nGhostBoomer3;10\nCharger1;12\nCharger2;12\nGhostCharger;8\nTank1;1\nTank2;1\nTank3;1\nTankGhost1;1\nTankGhost2;1\nTankGhost3;1\nMachineGunInfected;1\nFlyingInfectedBoss;1\nInfectedHyena;40\nInfectedWildboar;20\nInfectedBear;28\nPredatoryChameleon;35\nInfectedWolf;40\nInfectedWerewolf;20"}
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
			}
			else
			{
				m_currentWaveEntries = m_waves.ContainsKey(1) ? m_waves[1] : new List<WaveEntry>();
				m_currentWave = 1;
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

			// Intentar varias veces si no se encuentra punto
			int attempts = 0;
			while (spawnPos == Vector3.Zero && attempts < 3)
			{
				spawnPos = GetRandomSpawnPointAroundPlayers(true);
				attempts++;
			}

			if (spawnPos == Vector3.Zero)
			{
				// Si falla, spawnear cerca del primer jugador
				var player = m_subsystemPlayers.ComponentPlayers.FirstOrDefault();
				if (player != null)
				{
					Vector3 playerPos = player.ComponentBody.Position;
					spawnPos = new Vector3(playerPos.X + m_random.Float(-10, 10), playerPos.Y + 2, playerPos.Z + m_random.Float(-10, 10));
				}
				else
				{
					// Si no hay jugadores, reintentar
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

		private void AdvanceBossBattle()
		{
			m_currentBossEntity = null;
			SpawnNextBoss();
		}

		private bool IsEntityAlive(Entity entity)
		{
			if (entity == null) return false;

			// Verificar si la entidad todavía existe en el proyecto
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

			// Los jefes solo deben aparecer mediante el sistema de batalla de jefes, no en spawn normal
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
				for (int i = 0; i < 20; i++)
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

				// Si no se encontró con bloque permitido, intentar aceptar cualquier bloque sólido
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

						if (!(block is WaterBlock) && !(block is MagmaBlock))
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

				// Si no se encuentra punto de superficie, generar posición aleatoria en el aire
				Vector3 playerPos = player.ComponentBody.Position;
				for (int i = 0; i < 10; i++)
				{
					float angle = m_random.Float(0, 2 * MathUtils.PI);
					float distance = m_random.Float(20, 40);
					int x = (int)(playerPos.X + MathF.Cos(angle) * distance);
					int z = (int)(playerPos.Z + MathF.Sin(angle) * distance);
					int y = m_random.Int(70, 110); // Altura segura sobre el terreno

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
