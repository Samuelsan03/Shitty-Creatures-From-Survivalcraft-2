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
					LoadCurrentWave();
				}
				else
				{
					m_firstNightCompleted = true;

					if (m_currentWaveIndex < m_waves.Count - 1)
					{
						m_currentWaveIndex++;
						LoadCurrentWave();
					}
				}
			}

			if (m_isGreenNightActive && this.m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
			{
				if (this.m_subsystemTime.PeriodicGameTimeEvent(2.0, 0.0))
				{
					SpawnZombiesFromWave();
				}
			}
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
				ResetCurrentWaveSpawns();
			}

			var availableTemplates = m_currentWaveSpawns.Where(kv => kv.Value > 0).ToList();
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
							return;
						}
						else
						{
							Log.Error($"No se pudo spawnear: {templateName}");
						}
					}
				}
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

			this.m_lastMoonDayForWave = Math.Floor(this.m_subsystemTimeOfDay.Day);
			this.m_isGreenNightActive = false;
			this.m_wavesLoaded = false;
			this.m_firstNightCompleted = false;
			this.m_currentWaveIndex = 0;
		}

		private string GetWavesPath()
		{
			try
			{
				string basePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
				if (!string.IsNullOrEmpty(basePath))
				{
					return Path.Combine(basePath, "Waves");
				}
			}
			catch { }

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

				string[] files = Directory.GetFiles(wavesPath, "*.txt");

				var sortedFiles = files.OrderBy(f =>
				{
					string fileName = Path.GetFileNameWithoutExtension(f);
					if (int.TryParse(fileName, out int number))
						return number;
					return int.MaxValue;
				});

				foreach (string file in sortedFiles)
				{
					string fileName = Path.GetFileName(file);
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
				CreateEmergencyWaves("Waves");
			}
		}

		private void CreateEmergencyWaves(string wavesPath)
		{
			m_waves.Clear();

			WaveData wave1 = new WaveData { Name = "Ola 1", FilePath = "emergency1" };
			wave1.Spawns["InfectedNormal1"] = 20;
			wave1.Spawns["InfectedNormal2"] = 20;
			wave1.Spawns["InfectedFly1"] = 2;
			m_waves.Add(wave1);
		}

		private void CreateAllWaveFiles(string wavesPath)
		{
			try
			{
				string path1 = Path.Combine(wavesPath, "1.txt");
				if (!File.Exists(path1))
				{
					File.WriteAllLines(path1, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"InfectedFly1;2"
					});
				}

				string path2 = Path.Combine(wavesPath, "2.txt");
				if (!File.Exists(path2))
				{
					File.WriteAllLines(path2, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"InfectedFly1;2"
					});
				}

				string path3 = Path.Combine(wavesPath, "3.txt");
				if (!File.Exists(path3))
				{
					File.WriteAllLines(path3, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"InfectedFly1;2"
					});
				}

				string path4 = Path.Combine(wavesPath, "4.txt");
				if (!File.Exists(path4))
				{
					File.WriteAllLines(path4, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"InfectedFly1;2"
					});
				}

				string path5 = Path.Combine(wavesPath, "5.txt");
				if (!File.Exists(path5))
				{
					File.WriteAllLines(path5, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"Boomer1;5",
						"InfectedWolf;2"
					});
				}

				string path6 = Path.Combine(wavesPath, "6.txt");
				if (!File.Exists(path6))
				{
					File.WriteAllLines(path6, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"Boomer1;5",
						"Boomer2;5",
						"InfectedWolf;3"
					});
				}

				string path7 = Path.Combine(wavesPath, "7.txt");
				if (!File.Exists(path7))
				{
					File.WriteAllLines(path7, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"InfectedFly3;1",
						"Boomer1;5",
						"Boomer2;5",
						"Boomer3;5",
						"InfectedWolf;4"
					});
				}

				string path8 = Path.Combine(wavesPath, "8.txt");
				if (!File.Exists(path8))
				{
					File.WriteAllLines(path8, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"PoisonousGhost;5",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"InfectedFly3;1",
						"Boomer1;5",
						"Boomer2;5",
						"Boomer3;5",
						"Charger1;3",
						"InfectedWolf;5"
					});
				}

				string path9 = Path.Combine(wavesPath, "9.txt");
				if (!File.Exists(path9))
				{
					File.WriteAllLines(path9, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"GhostFast;5",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"PoisonousGhost;5",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"InfectedFly3;1",
						"Boomer1;5",
						"Boomer2;5",
						"Boomer3;5",
						"Charger2;3",
						"InfectedWolf;6"
					});
				}

				string path10 = Path.Combine(wavesPath, "10.txt");
				if (!File.Exists(path10))
				{
					File.WriteAllLines(path10, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"GhostNormal;10",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"GhostFast;5",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"PoisonousGhost;5",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"InfectedFly3;1",
						"Boomer1;5",
						"Boomer2;5",
						"Boomer3;5",
						"GhostBoomer1;3",
						"Charger1;3",
						"Charger2;3",
						"InfectedWolf;7",
						"InfectedWerewolf;1"
					});
				}

				string path11 = Path.Combine(wavesPath, "11.txt");
				if (!File.Exists(path11))
				{
					File.WriteAllLines(path11, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"GhostNormal;10",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"GhostFast;5",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"PoisonousGhost;5",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"InfectedFly3;1",
						"Boomer1;5",
						"Boomer2;5",
						"Boomer3;5",
						"GhostBoomer1;3",
						"GhostBoomer2;3",
						"Charger1;3",
						"Charger2;3",
						"Tank1;2",
						"InfectedWolf;8",
						"InfectedWerewolf;2"
					});
				}

				string path12 = Path.Combine(wavesPath, "12.txt");
				if (!File.Exists(path12))
				{
					File.WriteAllLines(path12, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"GhostNormal;10",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"GhostFast;5",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"PoisonousGhost;5",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"InfectedFly3;1",
						"Boomer1;5",
						"Boomer2;5",
						"Boomer3;5",
						"GhostBoomer1;3",
						"GhostBoomer2;3",
						"GhostBoomer3;3",
						"Charger1;3",
						"Charger2;3",
						"TankGhost1;2",
						"InfectedWolf;9",
						"InfectedWerewolf;2"
					});
				}

				string path13 = Path.Combine(wavesPath, "13.txt");
				if (!File.Exists(path13))
				{
					File.WriteAllLines(path13, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"GhostNormal;10",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"GhostFast;5",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"PoisonousGhost;5",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"InfectedFly3;1",
						"Boomer1;5",
						"Boomer2;5",
						"Boomer3;5",
						"GhostBoomer1;3",
						"GhostBoomer2;3",
						"GhostBoomer3;3",
						"GhostCharger;2",
						"Charger1;3",
						"Charger2;3",
						"Tank2;2",
						"InfectedWolf;10",
						"InfectedWerewolf;3"
					});
				}

				string path14 = Path.Combine(wavesPath, "14.txt");
				if (!File.Exists(path14))
				{
					File.WriteAllLines(path14, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"GhostNormal;10",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"GhostFast;5",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"PoisonousGhost;5",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"InfectedFly3;1",
						"Boomer1;5",
						"Boomer2;5",
						"Boomer3;5",
						"GhostBoomer1;3",
						"GhostBoomer2;3",
						"GhostBoomer3;3",
						"GhostCharger;2",
						"Charger1;3",
						"Charger2;3",
						"Tank3;2",
						"InfectedWolf;10",
						"InfectedWerewolf;3"
					});
				}

				string path15 = Path.Combine(wavesPath, "15.txt");
				if (!File.Exists(path15))
				{
					File.WriteAllLines(path15, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"GhostNormal;10",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"GhostFast;5",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"PoisonousGhost;5",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"InfectedFly3;1",
						"Boomer1;5",
						"Boomer2;5",
						"Boomer3;5",
						"GhostBoomer1;3",
						"GhostBoomer2;3",
						"GhostBoomer3;3",
						"GhostCharger;2",
						"Charger1;3",
						"Charger2;3",
						"TankGhost3;2",
						"InfectedWolf;12",
						"InfectedWerewolf;4"
					});
				}

				string path16 = Path.Combine(wavesPath, "16.txt");
				if (!File.Exists(path16))
				{
					File.WriteAllLines(path16, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"GhostNormal;10",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"GhostFast;5",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"PoisonousGhost;5",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"InfectedFly3;1",
						"Boomer1;5",
						"Boomer2;5",
						"Boomer3;5",
						"GhostBoomer1;3",
						"GhostBoomer2;3",
						"GhostBoomer3;3",
						"GhostCharger;2",
						"Charger1;3",
						"Charger2;3",
						"FlyingInfectedBoss;1",
						"InfectedWolf;12",
						"InfectedWerewolf;4"
					});
				}

				string path17 = Path.Combine(wavesPath, "17.txt");
				if (!File.Exists(path17))
				{
					File.WriteAllLines(path17, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"GhostNormal;10",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"GhostFast;5",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"PoisonousGhost;5",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"InfectedFly3;2",
						"Boomer1;5",
						"Boomer2;5",
						"Boomer3;5",
						"GhostBoomer1;3",
						"GhostBoomer2;3",
						"GhostBoomer3;3",
						"GhostCharger;2",
						"Charger1;3",
						"Charger2;3",
						"InfectedWolf;15",
						"InfectedWerewolf;5"
					});
				}

				string path18 = Path.Combine(wavesPath, "18.txt");
				if (!File.Exists(path18))
				{
					File.WriteAllLines(path18, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"GhostNormal;10",
						"InfectedFast1;15",
						"InfectedFast2;15",
						"GhostFast;5",
						"InfectedMuscle1;25",
						"InfectedMuscle2;25",
						"PoisonousInfected1;10",
						"PoisonousInfected2;10",
						"PoisonousGhost;5",
						"InfectedFly1;2",
						"InfectedFly2;3",
						"InfectedFly3;1",
						"Boomer1;5",
						"Boomer2;5",
						"Boomer3;5",
						"GhostBoomer1;3",
						"GhostBoomer2;3",
						"GhostBoomer3;3",
						"GhostCharger;2",
						"Charger1;3",
						"Charger2;3",
						"MachineGunInfected;1",
						"InfectedWolf;15",
						"InfectedWerewolf;5"
					});
				}

				string path19 = Path.Combine(wavesPath, "19.txt");
				if (!File.Exists(path19))
				{
					File.WriteAllLines(path19, new string[]
					{
						"InfectedNormal1;25",
						"InfectedNormal2;25",
						"GhostNormal;15",
						"InfectedFast1;20",
						"InfectedFast2;20",
						"GhostFast;10",
						"InfectedMuscle1;30",
						"InfectedMuscle2;30",
						"PoisonousInfected1;15",
						"PoisonousInfected2;15",
						"PoisonousGhost;10",
						"InfectedFly1;4",
						"InfectedFly2;5",
						"InfectedFly3;3",
						"Boomer1;8",
						"Boomer2;8",
						"Boomer3;8",
						"GhostBoomer1;5",
						"GhostBoomer2;5",
						"GhostBoomer3;5",
						"Charger1;5",
						"Charger2;5",
						"GhostCharger;3",
						"Tank1;2",
						"Tank2;2",
						"Tank3;2",
						"TankGhost1;2",
						"TankGhost2;2",
						"TankGhost3;2",
						"MachineGunInfected;2",
						"FlyingInfectedBoss;1",
						"InfectedWolf;20",
						"InfectedWerewolf;10"
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
							nameof(MagmaBlock)
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
