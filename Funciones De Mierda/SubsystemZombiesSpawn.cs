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

		private Random m_random = new Random();
		private Dictionary<ComponentCreature, bool> m_creatures = new Dictionary<ComponentCreature, bool>();
		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();

		private static int m_totalLimit = 80;

		private List<WaveData> m_waves = new List<WaveData>();
		private int m_currentWaveIndex = 0;
		private bool m_wavesLoaded = false;

		private Dictionary<string, int> m_currentWaveSpawns = new Dictionary<string, int>();
		private Dictionary<string, int> m_originalWaveSpawns = new Dictionary<string, int>();
		private bool m_isGreenNightActive = false;
		private double m_lastMoonDayForWave = 0;
		private bool m_waveAdvancedThisNight = false;
		private bool m_firstNightCompleted = false;

		// Control de fase de jefes
		private bool m_bossPhaseActive = false;
		private float m_bossPhaseStartTime = 0f;

		private HashSet<string> m_bossZombieTypes = new HashSet<string>
		{
			"Tank1", "Tank2", "Tank3",
			"TankGhost1", "TankGhost2", "TankGhost3",
			"MachineGunInfected", "FlyingInfectedBoss"
		};

		private HashSet<string> m_allZombieTypes = new HashSet<string>
		{
			"InfectedNormal1", "InfectedNormal2", "InfectedFast1", "InfectedFast2",
			"InfectedMuscle1", "InfectedMuscle2",
			"PoisonousInfected1", "PoisonousInfected2",
			"InfectedFly1", "InfectedFly2", "InfectedFly3",
			"Boomer1", "Boomer2", "Boomer3",
			"Charger1", "Charger2",
			"Tank1", "Tank2", "Tank3",
			"GhostNormal", "GhostFast", "PoisonousGhost",
			"GhostBoomer1", "GhostBoomer2", "GhostBoomer3",
			"GhostCharger",
			"TankGhost1", "TankGhost2", "TankGhost3",
			"MachineGunInfected", "FlyingInfectedBoss",
			"InfectedWolf", "InfectedWerewolf"
		};

		public class WaveData
		{
			public string Name { get; set; }
			public string FilePath { get; set; }
			public Dictionary<string, int> Spawns { get; set; } = new Dictionary<string, int>();
		}

		public virtual void Update(float dt)
		{
			if (!m_wavesLoaded)
			{
				LoadWaves();
				m_wavesLoaded = true;
				LoadCurrentWave();
			}

			bool greenNightActive = (m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive);

			if (greenNightActive != m_isGreenNightActive)
			{
				m_isGreenNightActive = greenNightActive;

				if (m_isGreenNightActive)
				{
					m_waveAdvancedThisNight = false;
					m_bossPhaseActive = false;
					LoadCurrentWave();
				}
				else
				{
					m_firstNightCompleted = true;
					m_bossPhaseActive = false;

					if (m_currentWaveIndex < m_waves.Count - 1)
					{
						m_currentWaveIndex++;
						LoadCurrentWave();
					}
				}
			}

			if (m_isGreenNightActive && this.m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
			{
				// Verificar si hay jefes en esta oleada
				bool hasBossesInWave = m_currentWaveSpawns.Keys.Any(key => m_bossZombieTypes.Contains(key));

				if (hasBossesInWave)
				{
					// Solo verificar activación de fase de jefes si hay jefes en la oleada
					CheckBossPhaseActivation();
					UpdateBossPhase();
				}

				if (this.m_subsystemTime.PeriodicGameTimeEvent(2.0, 0.0))
				{
					SpawnZombiesFromWave();
				}
			}
		}

		private void CheckBossPhaseActivation()
		{
			if (m_currentWaveSpawns.Count == 0) return;

			float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
			float nightProgress = GetNightProgress(timeOfDay);

			// Verificar si hay jefes pendientes
			var bossTemplates = m_currentWaveSpawns
				.Where(kv => kv.Value > 0 && m_bossZombieTypes.Contains(kv.Key))
				.ToList();

			// Solo activar si hay jefes por generar y estamos a medianoche
			if (!m_bossPhaseActive && bossTemplates.Count > 0 && nightProgress >= 0.45f && nightProgress <= 0.55f)
			{
				ActivateBossPhase();
			}
		}

		private float GetNightProgress(float timeOfDay)
		{
			float nightStart = m_subsystemTimeOfDay.NightStart;
			float dawnStart = m_subsystemTimeOfDay.DawnStart;

			if (timeOfDay < nightStart)
				return 0f;
			if (timeOfDay > dawnStart)
				return 1f;

			return (timeOfDay - nightStart) / (dawnStart - nightStart);
		}

		private void ActivateBossPhase()
		{
			m_bossPhaseActive = true;
			m_bossPhaseStartTime = m_subsystemTimeOfDay.TimeOfDay;

			if (m_subsystemPlayers != null)
			{
				foreach (ComponentPlayer componentPlayer in m_subsystemPlayers.ComponentPlayers)
				{
					if (componentPlayer?.ComponentGui != null)
					{
						componentPlayer.ComponentGui.DisplaySmallMessage(
							LanguageControl.Get("ZombiesSpawn", "BossesAppear"),
							new Color(139, 0, 0), false, true);
					}
				}
			}
		}

		private void UpdateBossPhase()
		{
			if (!m_bossPhaseActive)
				return;

			bool anyBossSpawnsLeft = false;
			foreach (var kv in m_currentWaveSpawns)
			{
				if (m_bossZombieTypes.Contains(kv.Key) && kv.Value > 0)
				{
					anyBossSpawnsLeft = true;
					break;
				}
			}

			if (!anyBossSpawnsLeft)
			{
				int aliveBosses = CountAliveBosses();
				if (aliveBosses == 0)
				{
					m_bossPhaseActive = false;
				}
			}
		}

		private int CountAliveBosses()
		{
			int count = 0;
			foreach (var creature in m_creatures.Keys)
			{
				string name = creature.Entity.ValuesDictionary.DatabaseObject.Name;
				if (m_bossZombieTypes.Contains(name))
				{
					count++;
				}
			}
			return count;
		}

		private void LoadCurrentWave()
		{
			if (m_waves.Count == 0)
			{
				return;
			}

			if (m_currentWaveIndex >= m_waves.Count)
			{
				m_currentWaveIndex = m_waves.Count - 1;
			}

			WaveData currentWave = m_waves[m_currentWaveIndex];

			m_originalWaveSpawns.Clear();
			foreach (var spawn in currentWave.Spawns)
			{
				m_originalWaveSpawns[spawn.Key] = spawn.Value;
			}

			ResetCurrentWaveSpawns();

			// Mensaje para la ola final (ola 19, índice 18) solo si es de noche verde
			if (m_currentWaveIndex == 18 && m_subsystemPlayers != null && m_isGreenNightActive)
			{
				foreach (ComponentPlayer componentPlayer in m_subsystemPlayers.ComponentPlayers)
				{
					if (componentPlayer?.ComponentGui != null)
					{
						componentPlayer.ComponentGui.DisplaySmallMessage(
							LanguageControl.Get("ZombiesSpawn", "FinalWave"),
							new Color(180, 0, 0), false, true);
					}
				}
			}
		}

		private void ResetCurrentWaveSpawns()
		{
			m_currentWaveSpawns.Clear();
			foreach (var spawn in m_originalWaveSpawns)
			{
				m_currentWaveSpawns[spawn.Key] = spawn.Value;
			}
		}

		private void SpawnZombiesFromWave()
		{
			if (m_currentWaveSpawns.Count == 0) return;

			int currentCount = CountZombies();
			if (currentCount >= m_totalLimit) return;

			if (m_currentWaveSpawns.Values.Sum() == 0)
			{
				return;
			}

			var regularTemplates = m_currentWaveSpawns
				.Where(kv => kv.Value > 0 && !m_bossZombieTypes.Contains(kv.Key))
				.ToList();

			var bossTemplates = m_currentWaveSpawns
				.Where(kv => kv.Value > 0 && m_bossZombieTypes.Contains(kv.Key))
				.ToList();

			List<KeyValuePair<string, int>> availableTemplates;

			if (m_bossPhaseActive)
			{
				availableTemplates = bossTemplates;
			}
			else
			{
				availableTemplates = regularTemplates;
			}

			if (availableTemplates.Count == 0) return;

			for (int attempt = 0; attempt < 3; attempt++)
			{
				var selected = availableTemplates[this.m_random.Int(0, availableTemplates.Count - 1)];
				string templateName = selected.Key;

				foreach (GameWidget gameWidget in this.m_subsystemViews.GameWidgets)
				{
					Point3? spawnPoint = this.GetRandomSpawnPoint(gameWidget.ActiveCamera, SpawnLocationType.Surface);
					if (spawnPoint.HasValue)
					{
						Vector3 position = new Vector3(
							spawnPoint.Value.X + 0.5f,
							spawnPoint.Value.Y + 1.1f,
							spawnPoint.Value.Z + 0.5f);

						Entity entity = this.SpawnZombie(templateName, position);
						if (entity != null)
						{
							m_currentWaveSpawns[templateName]--;

							if (m_bossZombieTypes.Contains(templateName) && m_subsystemPlayers != null)
							{
								string bossKey = GetBossMessageKey(templateName);
								foreach (ComponentPlayer componentPlayer in m_subsystemPlayers.ComponentPlayers)
								{
									if (componentPlayer?.ComponentGui != null)
									{
										componentPlayer.ComponentGui.DisplaySmallMessage(
											LanguageControl.Get("ZombiesSpawn", bossKey),
											new Color(180, 0, 0), false, true);
									}
								}
							}

							return;
						}
					}
				}
			}
		}

		private string GetBossMessageKey(string templateName)
		{
			if (templateName.StartsWith("Tank"))
			{
				if (templateName.Contains("Ghost"))
					return "BossGhostTank";
				return "BossTank";
			}

			switch (templateName)
			{
				case "MachineGunInfected":
					return "BossMachineGun";
				case "FlyingInfectedBoss":
					return "BossFlying";
				default:
					return "BossGeneric";
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemSpawn = base.Project.FindSubsystem<SubsystemSpawn>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemViews = base.Project.FindSubsystem<SubsystemGameWidgets>(true);
			this.m_subsystemGreenNightSky = base.Project.FindSubsystem<SubsystemGreenNightSky>(true);
			this.m_subsystemTimeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);

			this.m_lastMoonDayForWave = Math.Floor(this.m_subsystemTimeOfDay.Day);
			this.m_isGreenNightActive = false;
			this.m_wavesLoaded = false;
			this.m_firstNightCompleted = false;
			this.m_bossPhaseActive = false;

			if (valuesDictionary.ContainsKey("CurrentWaveIndex"))
			{
				this.m_currentWaveIndex = valuesDictionary.GetValue<int>("CurrentWaveIndex");
			}
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			valuesDictionary.SetValue("CurrentWaveIndex", m_currentWaveIndex);
		}

		private string GetWavesPath()
		{
			try
			{
				// Obtener la ruta del assembly del mod
				string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
				if (!string.IsNullOrEmpty(assemblyLocation))
				{
					string modPath = Path.GetDirectoryName(assemblyLocation);
					if (!string.IsNullOrEmpty(modPath))
					{
						return Path.Combine(modPath, "Waves");
					}
				}
			}
			catch { }

			// Último recurso: carpeta "Waves" en el directorio actual del juego
			return "Waves";
		}

		private void LoadWaves()
		{
			m_waves.Clear();

			try
			{
				string wavesPath = GetWavesPath();

				if (!Directory.Exists(wavesPath))
				{
					Directory.CreateDirectory(wavesPath);
				}

				CreateAllWaveFiles(wavesPath);

				// Leer todos los archivos de la carpeta (sin filtrar por extensión)
				string[] files = Directory.GetFiles(wavesPath);

				var sortedFiles = files.OrderBy(f =>
				{
					string fileName = Path.GetFileNameWithoutExtension(f);
					if (int.TryParse(fileName, out int number))
						return number;
					return int.MaxValue;
				});

				foreach (string file in sortedFiles)
				{
					string[] lines = File.ReadAllLines(file);

					WaveData wave = new WaveData
					{
						Name = $"Ola {Path.GetFileNameWithoutExtension(file)}",
						FilePath = file
					};

					foreach (string line in lines)
					{
						if (string.IsNullOrWhiteSpace(line)) continue;

						string[] parts = line.Split(';');
						if (parts.Length == 2)
						{
							string template = parts[0].Trim();
							if (int.TryParse(parts[1].Trim(), out int count))
							{
								wave.Spawns[template] = count;
							}
						}
					}

					if (wave.Spawns.Count > 0)
					{
						m_waves.Add(wave);
					}
				}

				if (m_waves.Count == 0)
				{
					CreateEmergencyWaves(wavesPath);
				}
			}
			catch (Exception e)
			{
				Log.Error($"Error cargando olas: {e.Message}");
				CreateEmergencyWaves(GetWavesPath());
			}
		}

		private void CreateEmergencyWaves(string wavesPath)
		{
			m_waves.Clear();

			WaveData wave1 = new WaveData { Name = "Ola 1", FilePath = Path.Combine(wavesPath, "1") };
			wave1.Spawns["InfectedNormal1"] = 20;
			wave1.Spawns["InfectedNormal2"] = 20;
			wave1.Spawns["InfectedFly1"] = 2;
			m_waves.Add(wave1);

			// Opcional: guardar el archivo de emergencia
			try
			{
				string path = Path.Combine(wavesPath, "1");
				if (!File.Exists(path))
				{
					File.WriteAllLines(path, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"InfectedFly1;2"
					});
				}
			}
			catch { }
		}

		private void CreateAllWaveFiles(string wavesPath)
		{
			try
			{
				// Ola 1
				string path1 = Path.Combine(wavesPath, "1");
				if (!File.Exists(path1))
				{
					File.WriteAllLines(path1, new string[]
					{
						"InfectedNormal1;30",
						"InfectedNormal2;30",
						"InfectedFly1;6"
					});
				}

				// Ola 2
				string path2 = Path.Combine(wavesPath, "2");
				if (!File.Exists(path2))
				{
					File.WriteAllLines(path2, new string[]
					{
						"InfectedNormal1;30",
						"InfectedNormal2;30",
						"InfectedFast1;25",
						"InfectedFast2;25",
						"InfectedFly1;6"
					});
				}

				// Ola 3
				string path3 = Path.Combine(wavesPath, "3");
				if (!File.Exists(path3))
				{
					File.WriteAllLines(path3, new string[]
					{
						"InfectedNormal1;30",
						"InfectedNormal2;30",
						"InfectedFast1;25",
						"InfectedFast2;25",
						"InfectedMuscle1;35",
						"InfectedMuscle2;35",
						"InfectedFly1;6"
					});
				}

				// Ola 4
				string path4 = Path.Combine(wavesPath, "4");
				if (!File.Exists(path4))
				{
					File.WriteAllLines(path4, new string[]
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
				}

				// Ola 5
				string path5 = Path.Combine(wavesPath, "5");
				if (!File.Exists(path5))
				{
					File.WriteAllLines(path5, new string[]
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
				}

				// Ola 6
				string path6 = Path.Combine(wavesPath, "6");
				if (!File.Exists(path6))
				{
					File.WriteAllLines(path6, new string[]
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
				}

				// Ola 7
				string path7 = Path.Combine(wavesPath, "7");
				if (!File.Exists(path7))
				{
					File.WriteAllLines(path7, new string[]
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
				}

				// Ola 8
				string path8 = Path.Combine(wavesPath, "8");
				if (!File.Exists(path8))
				{
					File.WriteAllLines(path8, new string[]
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
				}

				// Ola 9
				string path9 = Path.Combine(wavesPath, "9");
				if (!File.Exists(path9))
				{
					File.WriteAllLines(path9, new string[]
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
				}

				// Ola 10
				string path10 = Path.Combine(wavesPath, "10");
				if (!File.Exists(path10))
				{
					File.WriteAllLines(path10, new string[]
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
				}

				// Ola 11
				string path11 = Path.Combine(wavesPath, "11");
				if (!File.Exists(path11))
				{
					File.WriteAllLines(path11, new string[]
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
				}

				// Ola 12
				string path12 = Path.Combine(wavesPath, "12");
				if (!File.Exists(path12))
				{
					File.WriteAllLines(path12, new string[]
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
				}

				// Ola 13
				string path13 = Path.Combine(wavesPath, "13");
				if (!File.Exists(path13))
				{
					File.WriteAllLines(path13, new string[]
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
				}

				// Ola 14
				string path14 = Path.Combine(wavesPath, "14");
				if (!File.Exists(path14))
				{
					File.WriteAllLines(path14, new string[]
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
				}

				// Ola 15
				string path15 = Path.Combine(wavesPath, "15");
				if (!File.Exists(path15))
				{
					File.WriteAllLines(path15, new string[]
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
				}

				// Ola 16
				string path16 = Path.Combine(wavesPath, "16");
				if (!File.Exists(path16))
				{
					File.WriteAllLines(path16, new string[]
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
				}

				// Ola 17
				string path17 = Path.Combine(wavesPath, "17");
				if (!File.Exists(path17))
				{
					File.WriteAllLines(path17, new string[]
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
				}

				// Ola 18
				string path18 = Path.Combine(wavesPath, "18");
				if (!File.Exists(path18))
				{
					File.WriteAllLines(path18, new string[]
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
				}

				// Ola 19
				string path19 = Path.Combine(wavesPath, "19");
				if (!File.Exists(path19))
				{
					File.WriteAllLines(path19, new string[]
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
			}
			catch (Exception e)
			{
				Log.Error($"Error creando archivos: {e.Message}");
			}
		}

		public override void OnEntityAdded(Entity entity)
		{
			foreach (ComponentCreature key in entity.FindComponents<ComponentCreature>())
			{
				string name = entity.ValuesDictionary.DatabaseObject.Name;
				if (m_allZombieTypes.Contains(name))
				{
					this.m_creatures.TryAdd(key, true);
				}
			}
		}

		public override void OnEntityRemoved(Entity entity)
		{
			foreach (ComponentCreature key in entity.FindComponents<ComponentCreature>())
			{
				string name = entity.ValuesDictionary.DatabaseObject.Name;
				if (m_allZombieTypes.Contains(name))
				{
					this.m_creatures.Remove(key);
				}
			}
		}

		public virtual Entity SpawnZombie(string templateName, Vector3 position)
		{
			try
			{
				Entity entity = DatabaseManager.CreateEntity(base.Project, templateName, true);
				entity.FindComponent<ComponentBody>(true).Position = position;
				entity.FindComponent<ComponentBody>(true).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, this.m_random.Float(0f, 6.2831855f));
				base.Project.AddEntity(entity);
				return entity;
			}
			catch (Exception e)
			{
				Log.Error($"Error spawneando {templateName}: {e.Message}");
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

						int blockBelowId = Terrain.ExtractContents(cellValueFast);
						Block blockBelow = BlocksManager.Blocks[blockBelowId];

						HashSet<string> forbiddenBlockNames = new HashSet<string>
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

						string blockName = blockBelow.GetType().Name;
						if (forbiddenBlockNames.Contains(blockName))
						{
							return false;
						}

						Block block = blockBelow;
						Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
						Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];

						return (block.IsCollidable_(cellValueFast) || block is WaterBlock) &&
							   !block2.IsCollidable_(cellValueFast2) && !(block2 is WaterBlock) &&
							   !block3.IsCollidable_(cellValueFast3) && !(block3 is WaterBlock);
					}
				default:
					return false;
			}
		}

		public virtual int CountZombies()
		{
			int num = 0;
			foreach (ComponentBody body in this.m_subsystemBodies.Bodies)
			{
				string name = body.Entity.ValuesDictionary.DatabaseObject.Name;
				if (m_allZombieTypes.Contains(name))
				{
					num++;
				}
			}
			return num;
		}
	}
}
