using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Engine.Graphics;
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
		private SubsystemSky m_subsystemSky;
		private SubsystemAudio m_subsystemAudio;
		private Random m_random = new Random();

		// Datos de oleadas
		private Dictionary<int, List<WaveEntry>> m_waves = new Dictionary<int, List<WaveEntry>>();
		private int m_currentWave = 1;
		private List<WaveEntry> m_currentWaveEntries;

		// Control de spawn
		private float m_spawnTimer;
		private float m_spawnInterval = 2f;
		private const int MaxCreaturesPerArea = 60;
		private const int MaxGlobalCreatures = 80;
		private const int MaxSpawnsPerFrame = 3;
		private const int GroupSpawnCount = 2;

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

		// Control de mensaje de desbloqueo
		private bool m_hasShownUnlockMessage = false;

		// Label estático para la cuenta regresiva
		private LabelWidget m_countdownLabel;
		private bool m_labelInitialized = false;

		// Listas estáticas de templates
		private static readonly HashSet<string> BossTemplates = new HashSet<string>
		{
			"Tank1", "Tank2", "Tank3",
			"TankGhost1", "TankGhost2", "TankGhost3",
			"MachineGunInfected", "FlyingInfectedBoss", "FrozenTank", "FrozenTankGhost"
		};

		private static readonly HashSet<string> MiniBossTemplates = new HashSet<string>
		{
			"InfectedBear", "InfectedWildboar"
		};

		private static readonly HashSet<string> FlyingTemplates = new HashSet<string>
		{
			"InfectedFly1", "InfectedFly2", "InfectedFly3",
			"FlyingInfectedBoss", "InfectedBird"
		};

		// Nuevo: NPCs que solo aparecen en invierno o temperaturas bajas
		private static readonly HashSet<string> ColdOnlyTemplates = new HashSet<string>
		{
			"InfectedFreezer",
			"BoomerFrozen",
			"FrozenGhost",
			"FrozenGhostBoomer",
			"FrozenTankGhost",
			"FrozenTank"
		};

		private HashSet<int> m_forbiddenBlockIndices = new HashSet<int>();

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public static SubsystemZombiesSpawn Instance { get; private set; }
		public int MaxWave => m_waves.Keys.Max();
		public bool IsAllWavesCompleted => m_currentWave >= MaxWave && !m_bossBattleActive;

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
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);

			InitializeForbiddenBlockIndices();
			LoadWavesFromResources();
			m_currentWave = valuesDictionary.GetValue<int>("CurrentWave", 1);
			SetCurrentWave(m_currentWave);
			m_wasGreenNightActive = m_subsystemGreenNightSky.IsGreenNightActive;
			Instance = this;

			CreateCountdownLabel();
		}

		private void InitializeForbiddenBlockIndices()
		{
			string[] forbiddenNames = {
				nameof(BedrockBlock), nameof(IronBlock), nameof(CopperBlock),
				nameof(DiamondBlock), nameof(BrickBlock), nameof(MalachiteBlock),
				nameof(WaterBlock), nameof(MagmaBlock), nameof(GraniteBlock),
				nameof(BasaltBlock), nameof(BasaltFenceBlock), nameof(BasaltSlabBlock),
				nameof(BasaltStairsBlock), nameof(LimestoneBlock)
			};

			foreach (var block in BlocksManager.Blocks)
			{
				if (block != null && forbiddenNames.Contains(block.GetType().Name))
				{
					m_forbiddenBlockIndices.Add(block.BlockIndex);
				}
			}
		}

		private void CreateCountdownLabel()
		{
			if (m_countdownLabel != null) return;

			m_countdownLabel = new LabelWidget
			{
				DropShadow = true,
				FontScale = 0.8f,
				Margin = new Vector2(5f, 0f),
				IsHitTestVisible = false,
				TextAnchor = TextAnchor.HorizontalCenter,
				HorizontalAlignment = WidgetAlignment.Far,
				VerticalAlignment = WidgetAlignment.Center,
				Color = new Color(255, 255, 0)
			};

			m_labelInitialized = false;
			AttachLabelToPlayers();
		}

		private void AttachLabelToPlayers()
		{
			if (m_countdownLabel == null) return;

			bool attached = false;
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				var controlsContainer = player.GuiWidget.Children.Find<ContainerWidget>("ControlsContainer", true);
				if (controlsContainer == null) continue;

				if (!controlsContainer.Children.Contains(m_countdownLabel))
				{
					controlsContainer.AddChildren(m_countdownLabel);
					attached = true;
				}
			}
			if (attached)
			{
				m_labelInitialized = true;
			}
		}

		private void UpdateCountdownLabel()
		{
			if (m_countdownLabel == null)
			{
				CreateCountdownLabel();
				return;
			}

			if (!m_labelInitialized)
			{
				AttachLabelToPlayers();
				if (!m_labelInitialized) return;
			}

			if (!m_subsystemGreenNightSky.GreenNightEnabled ||
				m_subsystemGameInfo.WorldSettings.TimeOfDayMode != TimeOfDayMode.Changing)
			{
				m_countdownLabel.IsVisible = false;
				return;
			}

			int daysLeft = GetDaysUntilNextGreenNight();
			if (daysLeft == 0)
				m_countdownLabel.Text = LanguageControl.Get("ZombiesSpawn", "TheyComeTonight");
			else
				m_countdownLabel.Text = string.Format(LanguageControl.Get("ZombiesSpawn", "TheyComeInXDays"), daysLeft);

			m_countdownLabel.IsVisible = true;
		}

		private int GetDaysUntilNextGreenNight()
		{
			int phase = m_subsystemSky.MoonPhase;
			float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;

			if (!m_subsystemGreenNightSky.GreenNightEnabled)
			{
				if (phase == 0 || phase == 4)
					return 4;
				else if (phase < 4)
					return 4 - phase;
				else
					return 8 - phase;
			}

			if (phase == 0 || phase == 4)
			{
				if (m_subsystemGreenNightSky.IsGreenNightActive)
					return 0;
				if (m_subsystemGreenNightSky.DaysSinceLastGreenNight == 0)
					return 4;
				return 0;
			}
			else if (phase < 4)
				return 4 - phase;
			else
				return 8 - phase;
		}

		private void PlayEvilLaugh()
		{
			if (m_subsystemAudio != null)
			{
				m_subsystemAudio.PlaySound("Audio/UI/mk-evil-laugh", 1f, 0f, 0f, 0f);
			}
		}

		private void OnNaturalNightEnded()
		{
			int waveBefore = m_currentWave;
			int maxWave = m_waves.Keys.Max();

			AdvanceToNextWave();

			if (waveBefore == maxWave && !m_hasShownUnlockMessage)
			{
				m_hasShownUnlockMessage = true;

				string largeMessage = LanguageControl.Get("RemoteControlAchievement", "Unlocked");
				if (string.IsNullOrEmpty(largeMessage))
					largeMessage = "Remote Control unlocked!";

				string smallMessage = LanguageControl.Get("RemoteControlAchievement", "UnlockedInfo");
				if (string.IsNullOrEmpty(smallMessage))
					smallMessage = "You can now craft the Remote Control to manage the Green Nights.";

				foreach (var player in m_subsystemPlayers.ComponentPlayers)
				{
					player.ComponentGui.DisplayLargeMessage(largeMessage, smallMessage, 5f, 0f);
				}
			}
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			valuesDictionary.SetValue("CurrentWave", m_currentWave);
		}

		public void Update(float dt)
		{
			UpdateCountdownLabel();

			bool isGreenNightActive = m_subsystemGreenNightSky.IsGreenNightActive;
			int maxWave = m_waves.Keys.Max();

			if (!m_wasGreenNightActive && isGreenNightActive)
			{
				PlayEvilLaugh();
				SendWaveMessage();

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

			if (m_currentWave != maxWave)
			{
				if (!m_hasSpawnedBossThisNight && isMidnight && !m_bossBattleActive && !m_bossSpawnDelayed)
				{
					StartBossBattle();
					m_bossSpawnDelayed = true;
					m_bossSpawnDelayTimer = 0.5f;
				}
			}

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

			int totalCreatures = m_subsystemCreatureSpawn.CountCreatures(false);
			float dynamicInterval = m_bossBattleActive ? m_spawnInterval * 2f : m_spawnInterval;
			if (totalCreatures > MaxGlobalCreatures * 0.8f)
				dynamicInterval *= 1.5f;
			else if (totalCreatures < MaxGlobalCreatures * 0.3f)
				dynamicInterval *= 0.8f;

			m_spawnTimer += dt;
			int spawnsThisFrame = 0;

			while (m_spawnTimer >= dynamicInterval && spawnsThisFrame < MaxSpawnsPerFrame)
			{
				m_spawnTimer -= dynamicInterval;

				int spawnedThisIteration = TrySpawnGroup();
				spawnsThisFrame += spawnedThisIteration;

				if (spawnedThisIteration == 0)
					break;
			}
		}

		private int TrySpawnGroup()
		{
			int spawned = 0;
			int totalCreatures = m_subsystemCreatureSpawn.CountCreatures(false);
			if (totalCreatures >= MaxGlobalCreatures)
				return 0;

			var entry = GetRandomWeightedEntry(m_currentWaveEntries);
			if (entry == null || BossTemplates.Contains(entry.TemplateName))
				return 0;

			Vector3 spawnPos;
			bool isFlying = FlyingTemplates.Contains(entry.TemplateName);
			if (isFlying)
				spawnPos = GetRandomFlyingSpawnPoint();
			else
				spawnPos = GetValidSpawnPoint();

			if (spawnPos == Vector3.Zero)
				return 0;

			Vector2 areaMin = new Vector2(spawnPos.X - 16, spawnPos.Z - 16);
			Vector2 areaMax = new Vector2(spawnPos.X + 16, spawnPos.Z + 16);
			int nearby = m_subsystemCreatureSpawn.CountCreaturesInArea(areaMin, areaMax, false);
			if (nearby >= MaxCreaturesPerArea)
				return 0;

			if (CanSpawnCreature(entry.TemplateName, spawnPos))
			{
				m_subsystemCreatureSpawn.SpawnCreature(entry.TemplateName, spawnPos, false);
				spawned++;
			}
			else
			{
				return 0;
			}

			int groupCount = m_random.Int(1, GroupSpawnCount);
			for (int i = 0; i < groupCount && spawned < MaxSpawnsPerFrame; i++)
			{
				var extraEntry = GetRandomWeightedEntry(m_currentWaveEntries);
				if (extraEntry == null || BossTemplates.Contains(extraEntry.TemplateName))
					continue;

				bool extraIsFlying = FlyingTemplates.Contains(extraEntry.TemplateName);
				Vector3 extraPos = GetNearbySpawnPoint(spawnPos, 5f, 15f, extraIsFlying);
				if (extraPos == Vector3.Zero)
					continue;

				Vector2 extraAreaMin = new Vector2(extraPos.X - 16, extraPos.Z - 16);
				Vector2 extraAreaMax = new Vector2(extraPos.X + 16, extraPos.Z + 16);
				int extraNearby = m_subsystemCreatureSpawn.CountCreaturesInArea(extraAreaMin, extraAreaMax, false);
				if (extraNearby >= MaxCreaturesPerArea)
					continue;

				if (CanSpawnCreature(extraEntry.TemplateName, extraPos))
				{
					m_subsystemCreatureSpawn.SpawnCreature(extraEntry.TemplateName, extraPos, false);
					spawned++;
				}
			}

			return spawned;
		}

		// Método modificado para incluir los nuevos NPCs de frío
		private bool CanSpawnCreature(string templateName, Vector3 pos)
		{
			if (ColdOnlyTemplates.Contains(templateName))
			{
				// Solo en invierno o temperatura baja
				if (m_subsystemSeasons.Season == Season.Winter)
					return true;

				int x = Terrain.ToCell(pos.X);
				int z = Terrain.ToCell(pos.Z);
				int temperature = m_subsystemTerrain.Terrain.GetTemperature(x, z);
				return temperature < 8;
			}
			return true;
		}

		private Vector3 GetNearbySpawnPoint(Vector3 center, float minDistance, float maxDistance, bool isFlying)
		{
			for (int i = 0; i < 5; i++)
			{
				float angle = m_random.Float(0, 2 * MathUtils.PI);
				float distance = m_random.Float(minDistance, maxDistance);
				int x = (int)(center.X + MathF.Cos(angle) * distance);
				int z = (int)(center.Z + MathF.Sin(angle) * distance);

				if (isFlying)
				{
					int y = (int)center.Y + m_random.Int(-5, 5);
					if (y >= 10 && y <= 255)
						return new Vector3(x + 0.5f, y, z + 0.5f);
				}
				else
				{
					int y = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
					if (y > 0 && y < 255)
					{
						int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
						int contents = Terrain.ExtractContents(cellValue);
						if (!m_forbiddenBlockIndices.Contains(contents))
						{
							Block block = BlocksManager.Blocks[contents];
							if (block.IsCollidable)
								return new Vector3(x + 0.5f, y, z + 0.5f);
						}
					}
				}
			}
			return Vector3.Zero;
		}

		private Vector3 GetBossSpawnPoint(float minDistance = 40f, float maxDistance = 70f)
		{
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				Vector3 playerPos = player.ComponentBody.Position;
				for (int i = 0; i < 15; i++)
				{
					float angle = m_random.Float(0, 2 * MathUtils.PI);
					float distance = m_random.Float(minDistance, maxDistance);
					int x = (int)(playerPos.X + MathF.Cos(angle) * distance);
					int z = (int)(playerPos.Z + MathF.Sin(angle) * distance);
					int y = m_subsystemTerrain.Terrain.GetTopHeight(x, z);

					if (y > 0 && y < 255)
					{
						int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
						int contents = Terrain.ExtractContents(cellValue);
						if (!m_forbiddenBlockIndices.Contains(contents))
						{
							Block block = BlocksManager.Blocks[contents];
							if (block.IsCollidable)
								return new Vector3(x + 0.5f, y, z + 0.5f);
						}
					}
				}
			}
			return Vector3.Zero;
		}

		private Vector3 GetAlternativeBossSpawnPoint(float minDistance = 30f, float maxDistance = 80f)
		{
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				Vector3 playerPos = player.ComponentBody.Position;
				for (int i = 0; i < 30; i++)
				{
					float angle = m_random.Float(0, 2 * MathUtils.PI);
					float distance = m_random.Float(minDistance, maxDistance);
					int x = (int)(playerPos.X + MathF.Cos(angle) * distance);
					int z = (int)(playerPos.Z + MathF.Sin(angle) * distance);
					int y = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
					if (y > 0 && y < 255)
					{
						int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
						int contents = Terrain.ExtractContents(cellValue);
						if (!m_forbiddenBlockIndices.Contains(contents))
						{
							Block block = BlocksManager.Blocks[contents];
							if (block.IsCollidable)
								return new Vector3(x + 0.5f, y, z + 0.5f);
						}
					}
				}
			}
			return Vector3.Zero;
		}

		private Vector3 GetRandomFlyingBossSpawnPoint()
		{
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				Vector3 playerPos = player.ComponentBody.Position;
				for (int i = 0; i < 8; i++)
				{
					float angle = m_random.Float(0, 2 * MathUtils.PI);
					float distance = m_random.Float(70, 100);
					int x = (int)(playerPos.X + MathF.Cos(angle) * distance);
					int z = (int)(playerPos.Z + MathF.Sin(angle) * distance);
					int y = m_random.Int(80, 120);

					int groundY = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
					if (groundY > 0 && groundY < 255)
					{
						int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, groundY - 1, z);
						int contents = Terrain.ExtractContents(cellValue);
						if (!m_forbiddenBlockIndices.Contains(contents))
						{
							Block block = BlocksManager.Blocks[contents];
							if (block.IsCollidable)
								return new Vector3(x + 0.5f, y, z + 0.5f);
						}
					}
					else
					{
						return new Vector3(x + 0.5f, y, z + 0.5f);
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
				m_spawnInterval = Math.Max(1.2f, 2.5f - (wave * 0.04f));
			}
			else
			{
				m_currentWaveEntries = m_waves.ContainsKey(1) ? m_waves[1] : new List<WaveEntry>();
				m_currentWave = 1;
				m_spawnInterval = 2.5f;
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

			if (m_currentWave == MaxWave)
			{
				foreach (string boss in bosses)
					m_bossQueue.Enqueue(boss);
			}
			else
			{
				m_bossQueue.Enqueue(bosses[0]);
			}
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

			bool isFlying = FlyingTemplates.Contains(bossTemplate);

			if (isFlying)
			{
				for (int attempt = 0; attempt < 3; attempt++)
				{
					spawnPos = GetRandomFlyingBossSpawnPoint();
					if (spawnPos != Vector3.Zero)
						break;
				}
				if (spawnPos == Vector3.Zero)
				{
					spawnPos = GetRandomFlyingSpawnPoint();
				}
			}
			else
			{
				for (int attempt = 0; attempt < 3; attempt++)
				{
					spawnPos = GetBossSpawnPoint(60f, 90f);
					if (spawnPos != Vector3.Zero)
						break;
				}
				if (spawnPos == Vector3.Zero)
				{
					spawnPos = GetAlternativeBossSpawnPoint(50f, 100f);
				}
			}

			if (spawnPos == Vector3.Zero)
			{
				foreach (var player in m_subsystemPlayers.ComponentPlayers)
				{
					var camera = player.GameWidget.ActiveCamera;
					var point = m_subsystemCreatureSpawn.GetRandomSpawnPoint(camera, SpawnLocationType.Surface);
					if (point.HasValue)
					{
						int x = point.Value.X;
						int y = point.Value.Y - 1;
						int z = point.Value.Z;
						int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
						int contents = Terrain.ExtractContents(cellValue);
						if (!m_forbiddenBlockIndices.Contains(contents))
						{
							Block block = BlocksManager.Blocks[contents];
							if (block.IsCollidable)
							{
								spawnPos = new Vector3(point.Value.X + 0.5f, point.Value.Y, point.Value.Z + 0.5f);
								break;
							}
						}
					}
				}
			}

			if (spawnPos == Vector3.Zero)
			{
				m_bossQueue.Enqueue(bossTemplate);
				m_bossSpawnDelayed = true;
				m_bossSpawnDelayTimer = BossSpawnDelay;
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

		private void SendWaveMessage()
		{
			int maxWave = m_waves.Keys.Max();

			string waveMessage = string.Format(LanguageControl.Get("ZombiesSpawn", "WaveMessage"), m_currentWave);
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				player.ComponentGui.DisplayLargeMessage(waveMessage, "", 3f, 0f);
			}

			if (m_currentWave == maxWave)
			{
				string finalMessage = LanguageControl.Get("ZombiesSpawn", "FinalWave");
				foreach (var player in m_subsystemPlayers.ComponentPlayers)
				{
					player.ComponentGui.DisplaySmallMessage(finalMessage, new Color(255, 0, 0), true, true);
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

		private Vector3 GetValidSpawnPoint()
		{
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				var camera = player.GameWidget.ActiveCamera;
				for (int i = 0; i < 5; i++)
				{
					var point = m_subsystemCreatureSpawn.GetRandomSpawnPoint(camera, SpawnLocationType.Surface);
					if (point.HasValue)
					{
						int x = point.Value.X;
						int y = point.Value.Y - 1;
						int z = point.Value.Z;

						int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
						int contents = Terrain.ExtractContents(cellValue);
						if (!m_forbiddenBlockIndices.Contains(contents))
						{
							Block block = BlocksManager.Blocks[contents];
							if (block.IsCollidable)
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
				for (int i = 0; i < 5; i++)
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
				for (int i = 0; i < 3; i++)
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
			if (bossTemplate.StartsWith("Tank") ||bossTemplate.StartsWith("FrozenTank"))
				return "BossTank";
			if (bossTemplate.StartsWith("GhostTank") || bossTemplate.StartsWith("TankGhost")|| bossTemplate.StartsWith("FrozenTankGhost"))
				return "BossGhostTank";
			if (bossTemplate == "MachineGunInfected")
				return "BossMachineGun";
			if (bossTemplate == "FlyingInfectedBoss")
				return "BossFlying";
			return "BossGeneric";
		}
	}
}
