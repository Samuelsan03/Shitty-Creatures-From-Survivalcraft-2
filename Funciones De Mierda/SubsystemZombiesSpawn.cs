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

		private static int m_totalLimit = 40;

		// Sistema de olas
		private List<WaveData> m_waves = new List<WaveData>();
		private int m_currentWaveIndex = 0;

		// Control de spawn continuo durante la noche verde
		private Dictionary<string, int> m_currentWaveSpawns = new Dictionary<string, int>();
		private Dictionary<string, int> m_originalWaveSpawns = new Dictionary<string, int>();
		private bool m_isGreenNightActive = false;
		private double m_lastMoonDayForWave = 0;

		public class WaveData
		{
			public string Name { get; set; }
			public string FilePath { get; set; }
			public Dictionary<string, int> Spawns { get; set; } = new Dictionary<string, int>();
		}

		public virtual void Update(float dt)
		{
			// Detectar si la noche verde está activa
			bool greenNightActive = (m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive);

			// Si cambia el estado de la noche verde
			if (greenNightActive != m_isGreenNightActive)
			{
				m_isGreenNightActive = greenNightActive;

				if (m_isGreenNightActive)
				{
					// Comenzó la noche verde - verificar si debemos avanzar de ola
					int moonPhase = this.m_subsystemSky.MoonPhase;
					double currentDay = Math.Floor(this.m_subsystemTimeOfDay.Day);

					// Detectar cambio de día para avanzar ola (solo en luna nueva)
					if (currentDay > this.m_lastMoonDayForWave)
					{
						this.m_lastMoonDayForWave = currentDay;

						if (moonPhase == 4 && m_currentWaveIndex < m_waves.Count - 1)
						{
							m_currentWaveIndex++;
							Log.Information($"=== NOCHE VERDE: AVANZANDO A OLA {m_currentWaveIndex + 1} ===");
						}
					}

					// Cargar la ola actual para spawnear durante toda la noche
					LoadCurrentWave();
					Log.Information($"=== NOCHE VERDE INICIADA - OLA {m_currentWaveIndex + 1} ACTIVADA ===");
					Log.Information($"Total de zombies por ciclo: {m_currentWaveSpawns.Values.Sum()}");
				}
				else
				{
					// Terminó la noche verde
					Log.Information($"=== NOCHE VERDE TERMINADA - OLA {m_currentWaveIndex + 1} PAUSADA ===");
				}
			}

			// Solo spawnear durante noche verde y en las fases correctas
			if (m_isGreenNightActive && this.m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
			{
				int moonPhase = this.m_subsystemSky.MoonPhase;

				// Solo spawnear en luna llena (0) o luna nueva (4)
				if (moonPhase == 0 || moonPhase == 4)
				{
					// Spawnear cada 2 segundos
					if (this.m_subsystemTime.PeriodicGameTimeEvent(2.0, 0.0))
					{
						SpawnZombiesFromWave();
					}
				}
			}
		}

		private void LoadCurrentWave()
		{
			if (m_waves.Count == 0 || m_currentWaveIndex >= m_waves.Count) return;

			WaveData currentWave = m_waves[m_currentWaveIndex];

			// Guardar los spawns originales
			m_originalWaveSpawns.Clear();
			foreach (var spawn in currentWave.Spawns)
			{
				m_originalWaveSpawns[spawn.Key] = spawn.Value;
			}

			// Inicializar los spawns actuales
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
			if (currentCount >= m_totalLimit)
			{
				// Si llegamos al límite, no spawnear más
				return;
			}

			// Verificar si ya no quedan zombies en el ciclo actual
			if (m_currentWaveSpawns.Values.Sum() == 0)
			{
				// Resetear el ciclo - vuelven a aparecer todos los del TXT
				ResetCurrentWaveSpawns();
				Log.Information($"=== CICLO COMPLETADO - RESETEANDO OLA {m_currentWaveIndex + 1} ===");
				Log.Information($"Volverán a aparecer: {m_currentWaveSpawns.Values.Sum()} zombies");
			}

			// Obtener lista de templates que aún tienen zombies por spawnear
			var availableTemplates = m_currentWaveSpawns.Where(kv => kv.Value > 0).ToList();
			if (availableTemplates.Count == 0) return;

			// Elegir un template aleatorio
			var selected = availableTemplates[this.m_random.Int(0, availableTemplates.Count - 1)];
			string templateName = selected.Key;

			// Spawnear para cada jugador
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
						// Reducir el contador de este tipo
						m_currentWaveSpawns[templateName]--;

						int remainingInCycle = m_currentWaveSpawns.Values.Sum();
						Log.Information($"Spawneado: {templateName} (quedan {remainingInCycle} en este ciclo)");
						break;
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

			LoadWaves();
		}

		private void LoadWaves()
		{
			m_waves.Clear();

			try
			{
				if (!Directory.Exists("Waves"))
				{
					Log.Warning("No existe carpeta 'Waves'. Creando...");
					Directory.CreateDirectory("Waves");
					CreateExampleWaveFiles();
					return;
				}

				string[] files = Directory.GetFiles("Waves", "*.txt");

				if (files.Length == 0)
				{
					Log.Warning("No hay archivos TXT. Creando ejemplos...");
					CreateExampleWaveFiles();
					files = Directory.GetFiles("Waves", "*.txt");
				}

				// Ordenar por número
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
						int total = wave.Spawns.Values.Sum();
						Log.Information($"Cargada {fileName}: {total} zombies por ciclo");

						foreach (var spawn in wave.Spawns)
						{
							Log.Information($"  - {spawn.Key}: {spawn.Value}");
						}
					}
				}

				Log.Information($"Total olas cargadas: {m_waves.Count}");

				if (m_waves.Count > 0)
				{
					m_currentWaveIndex = 0;
					Log.Information($"Ola inicial: 1");
				}
			}
			catch (Exception e)
			{
				Log.Error($"Error cargando olas: {e.Message}");
			}
		}

		private void CreateExampleWaveFiles()
		{
			try
			{
				// Ola 1: exactamente como tu 1.txt
				string path1 = Path.Combine("Waves", "1.txt");
				if (!File.Exists(path1))
				{
					File.WriteAllLines(path1, new string[]
					{
						"InfectedNormal1;20",
						"InfectedNormal2;20",
						"InfectedFly1;2"
					});
					Log.Information("Creado 1.txt: 20 Normal1, 20 Normal2, 2 Fly1");
				}

				// Ola 2: exactamente como tu 2.txt
				string path2 = Path.Combine("Waves", "2.txt");
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
					Log.Information("Creado 2.txt: 20 Normal1, 20 Normal2, 15 Fast1, 15 Fast2, 2 Fly1");
				}
			}
			catch (Exception e)
			{
				Log.Error($"Error creando ejemplos: {e.Message}");
			}
		}

		public override void OnEntityAdded(Entity entity)
		{
			foreach (ComponentCreature key in entity.FindComponents<ComponentCreature>())
			{
				string name = entity.ValuesDictionary.DatabaseObject.Name;
				if (name == "InfectedNormal1" || name == "InfectedNormal2" ||
					name == "InfectedFast1" || name == "InfectedFast2" ||
					name == "InfectedFly1")
				{
					this.m_creatures.Add(key, true);
				}
			}
		}

		public override void OnEntityRemoved(Entity entity)
		{
			foreach (ComponentCreature key in entity.FindComponents<ComponentCreature>())
			{
				string name = entity.ValuesDictionary.DatabaseObject.Name;
				if (name == "InfectedNormal1" || name == "InfectedNormal2" ||
					name == "InfectedFast1" || name == "InfectedFast2" ||
					name == "InfectedFly1")
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

						Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
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
				if (name == "InfectedNormal1" || name == "InfectedNormal2" ||
					name == "InfectedFast1" || name == "InfectedFast2" ||
					name == "InfectedFly1")
				{
					num++;
				}
			}
			return num;
		}
	}
}
