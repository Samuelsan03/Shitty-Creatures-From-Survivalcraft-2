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

			// Cargar las olas usando ContentManager como las texturas
			LoadWaves();

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
			if (!m_subsystemGreenNightSky.GreenNightEnabled || !m_subsystemGreenNightSky.IsGreenNightActive)
				return;

			double now = m_subsystemTime.GameTime;
			int currentPhase = m_subsystemSky.MoonPhase;

			// Avanzar de ola cuando cambia la fase lunar (durante la noche verde)
			if (!m_advancedThisNight && m_subsystemGreenNightSky.IsGreenNightActive)
			{
				// Solo verificar cuando es de noche (entre dusk y dawn)
				float time = m_subsystemTimeOfDay.TimeOfDay;
				if (time > 0.5f || time < 0.01f) // Es de noche
				{
					// Si la fase lunar actual es la que esperamos (0 o 4)
					if (currentPhase == m_nextWavePhase)
					{
						if (m_currentWaveIndex < m_maxWaveIndex)
						{
							m_currentWaveIndex++;
							// Alternar entre luna llena (0) y luna nueva (4)
							m_nextWavePhase = (m_nextWavePhase == 0) ? 4 : 0;
						}
						m_advancedThisNight = true;
					}
				}
			}

			// Resetear el flag al amanecer
			if (IsDawn())
				m_advancedThisNight = false;

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

			if (now >= m_nextSpawnTime)
			{
				m_nextSpawnTime = now + m_spawnInterval;
				SpawnZombie();
			}
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
						// Usar ContentManager.Get<string> para obtener el archivo como string
						// Esto busca en la carpeta "Waves" automáticamente
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
								Log.Information($"Ola {i} cargada correctamente");
							}
						}
					}
					catch
					{
						// Si no encuentra el archivo, simplemente continuamos
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
			Log.Information("Cargando olas por defecto...");

			m_waves[1] = ParseWaveData(new string[]
			{
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"InfectedFly1;6"
			});

			m_waves[2] = ParseWaveData(new string[]
			{
				"InfectedNormal1;30",
				"InfectedNormal2;30",
				"InfectedFast1;25",
				"InfectedFast2;25",
				"InfectedFly1;6"
			});

			m_waves[3] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;15"
			});

			m_waves[6] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;18"
			});

			m_waves[7] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;20"
			});

			m_waves[8] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;25"
			});

			m_waves[9] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;25"
			});

			m_waves[10] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;25",
				"InfectedWerewolf;12"
			});

			m_waves[11] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;25",
				"InfectedWerewolf;12"
			});

			m_waves[12] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;25",
				"InfectedWerewolf;12"
			});

			m_waves[13] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;25",
				"InfectedWerewolf;12"
			});

			m_waves[14] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;25",
				"InfectedWerewolf;12"
			});

			m_waves[15] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;25",
				"InfectedWerewolf;12"
			});

			m_waves[16] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;25",
				"InfectedWerewolf;12"
			});

			m_waves[17] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;30",
				"InfectedWerewolf;15"
			});

			m_waves[18] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;30",
				"InfectedWerewolf;15"
			});

			m_waves[19] = ParseWaveData(new string[]
			{
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
				"InfectedWolf;35",
				"InfectedWerewolf;20"
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
			// Solo considerar realmente voladores a los que tienen "Fly" o "Flying" en el nombre
			bool isFlying = zombieType.Contains("Fly") || zombieType.Contains("Flying");

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
	}
}
