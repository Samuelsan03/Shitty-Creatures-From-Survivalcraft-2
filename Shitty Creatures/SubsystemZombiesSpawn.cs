using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;
using static Game.SubsystemGreenNightSky;

namespace Game
{
	public class SubsystemZombiesSpawn : Subsystem, IUpdateable
	{
		// Dependencias
		private SubsystemBlockEntities m_subsystemBlockEntities;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private SubsystemSpawn m_subsystemSpawn;
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

		public bool HasExtremeCompleted => m_extremeCompletionDialogShown;

		private bool m_extremeCompletionDialogShown = false;

		public bool HasAcceptedImpossibleChallenge => m_hasAcceptedImpossibleChallenge;
		private bool m_hasAcceptedImpossibleChallenge = false;

		// ===== RETRASO DE SPAWN AL INICIO DE NOCHE VERDE =====
		private float m_greenNightSpawnDelayTimer = 0f;
		private bool m_greenNightSpawnDelayActive = false;
		private const float GreenNightSpawnDelaySeconds = 5f;
		// ===== FIN RETRASO DE SPAWN =====

		// ===== SPAWN DE JEFES A MEDIANOCHE (OLEADAS NORMALES) =====
		private bool m_midnightBossesSpawnedThisNight = false;
		private float m_lastTimeOfDay = 0f;
		private const float MidnightDetectionTolerance = 0.005f;
		// ===== FIN SPAWN DE JEFES A MEDIANOCHE =====

		private bool m_nightEndProcessed;

		// ===== SPAWN DE ZOMBIS MONTADOS =====
		private const bool MountedZombiesEnabled = false;
		private float m_horseMountProbability = 0.1f;
		private float m_bearMountProbability = 0.3f;
		private float m_flyMountProbability = 0.3f;
		private const float MountedSpawnProbability = 0.2f;

		private static readonly HashSet<string> MountableHorseTemplates = new HashSet<string>
		{
			"Horse_Black_Saddled",
			"Horse_Palomino_Saddled",
			"Camel_Saddled",
			"Horse_Chestnut_Saddled",
			"Horse_White_Saddled",
			"Donkey_Saddled",
			"Horse_Bay_Saddled"
		};

		// ===== LÍMITES DE SPAWN (valores fijos, como SubsystemCreatureSpawn) =====
		private const int MaxCreaturesPerArea = 60;
		private const int MaxGlobalCreatures = 80;
		private const int MaxSpawnsPerFrame = 3;
		private const int GroupSpawnCount = 2;
		private const float BaseSpawnInterval = 2.5f;

		// Datos de oleadas
		private Dictionary<int, List<WaveEntry>> m_waves = new Dictionary<int, List<WaveEntry>>();
		private int m_currentWave = 1;
		private List<WaveEntry> m_currentWaveEntries;

		// Control de spawn (noche verde)
		private float m_spawnTimer;
		private float m_spawnInterval = BaseSpawnInterval;

		public event Action<int, int> WaveAdvanced;

		// ===== SISTEMA DE SPAWN DE ESQUELETOS =====
		private List<SpawnChunk> m_skeletonNewSpawnChunks = new List<SpawnChunk>();
		private List<SpawnChunk> m_skeletonSpawnChunks = new List<SpawnChunk>();

		// Spawn NORMAL - 100% de noche
		private const int SkeletonNormalTotalLimit = 8;
		private const int SkeletonNormalAreaLimit = 1;
		private const int SkeletonNormalNewChunkAttempts = 3;
		private const float SkeletonNormalSuitability = 1.0f;

		// Spawn CONSTANTE - 50% de noche
		private const int SkeletonConstantTotalLimitNormal = 3;
		private const int SkeletonConstantTotalLimitChallenging = 5;
		private const int SkeletonConstantAreaLimit = 1;
		private const float SkeletonConstantAreaRadius = 32f;
		private const int SkeletonConstantChunkAttempts = 1;
		private const float SkeletonConstantSuitability = 0.5f;

		private const float NightLightThreshold = 0.1f;
		private const string SkeletonTemplateName = "HumanoidSkeleton";

		private float m_constantSpawnCooldown;
		private const float ConstantSpawnCooldownTime = 15f;

		private bool m_letterWarSpawned;
		private const string LetterWarBlockName = "LetterWarBlock";

		private DynamicArray<ComponentBody> m_tempBodiesArray = new DynamicArray<ComponentBody>();
		// ===== FIN SISTEMA DE SPAWN DE ESQUELETOS =====

		// ===== SISTEMA DE SPAWN DE INFECTED SPIDER (INDEPENDIENTE) =====
		private const string InfectedSpiderTemplateName = "InfectedSpider";

		// Spawn NORMAL SUPERFICIE - noche (independiente de SkeletonSpawnEnabled)
		private const int SpiderNormalTotalLimit = 5;
		private const int SpiderNormalAreaLimit = 1;
		private const int SpiderNormalNewChunkAttempts = 2;
		private const float SpiderNormalSuitability = 0.8f;

		// Spawn CONSTANTE SUPERFICIE - noche
		private const int SpiderConstantTotalLimitNormal = 2;
		private const int SpiderConstantTotalLimitChallenging = 3;
		private const int SpiderConstantAreaLimit = 1;
		private const float SpiderConstantAreaRadius = 32f;
		private const int SpiderConstantChunkAttempts = 1;
		private const float SpiderConstantSuitability = 0.4f;

		// Spawn CUEVAS - siempre activo (independiente de todo)
		private const int SpiderCaveTotalLimit = 6;
		private const int SpiderCaveAreaLimit = 1;
		private const int SpiderCaveNewChunkAttempts = 3;
		private const float SpiderCaveSuitability = 0.75f;

		// Spawn CONSTANTE CUEVAS - siempre activo
		private const int SpiderCaveConstantTotalLimit = 3;
		private const int SpiderCaveConstantAreaLimit = 1;
		private const int SpiderCaveConstantChunkAttempts = 1;
		private const float SpiderCaveConstantSuitability = 0.4f;

		private float m_spiderConstantSpawnCooldown;
		private const float SpiderConstantSpawnCooldownTime = 18f;
		// ===== FIN SISTEMA DE SPAWN DE INFECTED SPIDER =====

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
		private LabelWidget m_difficultyLabel;
		private bool m_labelInitialized = false;

		// Flag para indicar que necesitamos verificar el estado del jefe al cargar
		private bool m_needsBossStateVerification = false;

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

		private static readonly HashSet<string> ColdOnlyTemplates = new HashSet<string>
		{
			"InfectedFreezer",
			"BoomerFrozen",
			"FrozenGhost",
			"FrozenGhostBoomer",
			"FrozenTankGhost",
			"FrozenTank"
		};

		// Lista de bloques PERMITIDOS para spawnear
		private HashSet<int> m_allowedBlockIndices = new HashSet<int>();

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public static SubsystemZombiesSpawn Instance { get; private set; }
		public int MaxWave => m_waves.Keys.Max();
		public bool IsAllWavesCompleted => m_currentWave >= MaxWave && !m_bossBattleActive;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_nightEndProcessed = false;

			m_subsystemGreenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>(true);
			if (m_subsystemGreenNightSky != null)
			{
				m_subsystemGreenNightSky.NaturalNightEnded += OnNaturalNightEnded;
			}

			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_subsystemSpawn = Project.FindSubsystem<SubsystemSpawn>(true);
			m_subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemGameWidgets = Project.FindSubsystem<SubsystemGameWidgets>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemSeasons = Project.FindSubsystem<SubsystemSeasons>(true);
			m_subsystemBlockEntities = Project.FindSubsystem<SubsystemBlockEntities>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);

			m_letterWarSpawned = valuesDictionary.GetValue<bool>("LetterWarSpawned", false);
			m_hasShownUnlockMessage = valuesDictionary.GetValue<bool>("HasShownUnlockMessage", false);
			m_midnightBossesSpawnedThisNight = valuesDictionary.GetValue<bool>("MidnightBossesSpawnedThisNight", false);
			m_lastTimeOfDay = m_subsystemTimeOfDay.TimeOfDay;

			if (m_subsystemSpawn != null)
			{
				m_subsystemSpawn.SpawningChunk += OnSpawningChunk;
			}

			InitializeAllowedBlockIndices();
			LoadWavesFromResources();
			m_currentWave = valuesDictionary.GetValue<int>("CurrentWave", 1);
			SetCurrentWave(m_currentWave);

			// ===== CARGAR ESTADO COMPLETO DE LA BATALLA DE JEFES =====
			m_bossBattleActive = valuesDictionary.GetValue<bool>("BossBattleActive", false);
			m_hasSpawnedBossThisNight = valuesDictionary.GetValue<bool>("HasSpawnedBossThisNight", false);
			m_bossSpawnDelayed = valuesDictionary.GetValue<bool>("BossSpawnDelayed", false);
			m_bossSpawnDelayTimer = valuesDictionary.GetValue<float>("BossSpawnDelayTimer", 0f);
			m_extremeCompletionDialogShown = valuesDictionary.GetValue<bool>("ExtremeCompletionDialogShown", false);
			m_hasAcceptedImpossibleChallenge = valuesDictionary.GetValue<bool>("HasAcceptedImpossibleChallenge", false);

			// Al cargar, si había un retraso de jefe pendiente, usar el tiempo normal (0.5f) y no los 5s iniciales
			if (m_bossSpawnDelayed)
			{
				m_bossSpawnDelayTimer = BossSpawnDelay;
			}

			string bossQueueStr = valuesDictionary.GetValue<string>("BossQueue", "");

			// Ignorar retraso guardado para que solo aplique en el inicio real de la noche verde, no al cargar
			m_greenNightSpawnDelayActive = false;
			m_greenNightSpawnDelayTimer = 0f;

			m_bossQueue.Clear();
			if (!string.IsNullOrEmpty(bossQueueStr))
			{
				string[] bosses = bossQueueStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string boss in bosses)
				{
					string trimmed = boss.Trim();
					if (!string.IsNullOrWhiteSpace(trimmed))
					{
						m_bossQueue.Enqueue(trimmed);
					}
				}
			}

			m_wasGreenNightActive = m_subsystemGreenNightSky.IsGreenNightActive;
			m_needsBossStateVerification = m_bossBattleActive;
			m_currentBossEntity = null;
			// ===== FIN CARGAR ESTADO DE LA BATALLA DE JEFES =====

			m_constantSpawnCooldown = ConstantSpawnCooldownTime;
			m_spiderConstantSpawnCooldown = SpiderConstantSpawnCooldownTime;
			Instance = this;

			CreateCountdownLabel();
		}

		private void OnSpawningChunk(SpawnChunk chunk)
		{
			m_skeletonSpawnChunks.Add(chunk);
			if (!chunk.IsSpawned)
			{
				m_skeletonNewSpawnChunks.Add(chunk);
			}
		}

		private void InitializeAllowedBlockIndices()
		{
			foreach (var block in BlocksManager.Blocks)
			{
				if (block != null &&
					(block is GrassBlock ||
					 block is DirtBlock ||
					 block is SandBlock ||
					 block is GravelBlock))
				{
					m_allowedBlockIndices.Add(block.BlockIndex);
				}
			}
		}

		private void CreateCountdownLabel()
		{
			if (m_countdownLabel != null) return;

			var stackPanel = new StackPanelWidget
			{
				Direction = LayoutDirection.Vertical,
				IsInverted = false,
				HorizontalAlignment = WidgetAlignment.Far,
				VerticalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(5f, 0f),
				IsHitTestVisible = false
			};

			m_countdownLabel = new LabelWidget
			{
				DropShadow = true,
				FontScale = 0.8f,
				Color = new Color(255, 255, 0),
				HorizontalAlignment = WidgetAlignment.Center,
				IsHitTestVisible = false
			};

			m_difficultyLabel = new LabelWidget
			{
				DropShadow = true,
				FontScale = 0.7f,
				Color = new Color(200, 200, 200),
				HorizontalAlignment = WidgetAlignment.Center,
				IsHitTestVisible = false
			};

			stackPanel.Children.Add(m_countdownLabel);
			stackPanel.Children.Add(m_difficultyLabel);

			m_countdownLabel.Tag = stackPanel;
			m_labelInitialized = false;
		}

		private void AttachLabelToPlayers()
		{
			if (m_countdownLabel == null || m_difficultyLabel == null) return;

			var stackPanel = m_countdownLabel.Tag as StackPanelWidget;
			if (stackPanel == null) return;

			bool attached = false;
			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				var controlsContainer = player.GuiWidget.Children.Find<ContainerWidget>("ControlsContainer", true);
				if (controlsContainer == null) continue;

				if (!controlsContainer.Children.Contains(stackPanel))
				{
					controlsContainer.AddChildren(stackPanel);
					attached = true;
				}
			}
			m_labelInitialized = attached;
		}

		private string GetDifficultyLocalizedName()
		{
			DifficultyMode mode = m_subsystemGreenNightSky.DifficultyMode;
			string key = mode switch
			{
				DifficultyMode.VeryEasy => "VeryEasy_Name",
				DifficultyMode.Easy => "Easy_Name",
				DifficultyMode.Normal => "Normal_Name",
				DifficultyMode.Medium => "Medium_Name",
				DifficultyMode.Hard => "Hard_Name",
				DifficultyMode.Extreme => "Extreme_Name",
				DifficultyMode.Impossible => "Impossible_Name",
				_ => "Normal_Name"
			};
			string difficultyName = LanguageControl.GetContentWidgets("GreenNightDifficulty", key);
			return string.IsNullOrEmpty(difficultyName) ? mode.ToString() : difficultyName;
		}

		private void UpdateCountdownLabel()
		{
			if (m_countdownLabel == null || m_difficultyLabel == null)
			{
				CreateCountdownLabel();
				return;
			}

			var stackPanel = m_countdownLabel.Tag as StackPanelWidget;
			if (stackPanel == null) return;

			if (!m_labelInitialized || stackPanel.ParentWidget == null)
			{
				AttachLabelToPlayers();
			}

			if (!m_labelInitialized)
				return;

			bool visible = m_subsystemGreenNightSky.GreenNightEnabled &&
						   m_subsystemGameInfo.WorldSettings.TimeOfDayMode == TimeOfDayMode.Changing;

			if (!visible)
			{
				stackPanel.IsVisible = false;
				return;
			}

			int daysLeft = GetDaysUntilNextGreenNight();
			if (daysLeft == 0)
				m_countdownLabel.Text = LanguageControl.Get("ZombiesSpawn", "TheyComeTonight");
			else
				m_countdownLabel.Text = string.Format(LanguageControl.Get("ZombiesSpawn", "TheyComeInXDays"), daysLeft);

			string difficultyText = GetDifficultyLocalizedName();
			m_difficultyLabel.Text = string.Format(LanguageControl.Get("ZombiesSpawn", "CurrentDifficulty"), difficultyText);

			stackPanel.IsVisible = true;
		}

		public void ForceUpdateDifficultyLabel()
		{
			if (m_difficultyLabel == null) return;
			string difficultyText = GetDifficultyLocalizedName();
			string format = LanguageControl.Get("ZombiesSpawn", "CurrentDifficulty");
			m_difficultyLabel.Text = string.Format(format, difficultyText);
		}

		private int GetDaysUntilNextGreenNight()
		{
			if (!m_subsystemGreenNightSky.GreenNightEnabled)
				return m_subsystemGreenNightSky.GreenNightIntervalDays;

			if (m_subsystemGreenNightSky.IsGreenNightActive)
				return 0;

			int daysSince = m_subsystemGreenNightSky.DaysSinceLastGreenNight;
			int interval = m_subsystemGreenNightSky.GreenNightIntervalDays;
			int daysLeft = interval - daysSince;

			return Math.Max(0, daysLeft);
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
			m_nightEndProcessed = true;
			int oldWave = m_currentWave;
			int maxWave = m_waves.Keys.Max();
			bool wasLastWave = (oldWave == maxWave);

			bool completed = AdvanceToNextWave();
			int newWave = m_currentWave;

			WaveAdvanced?.Invoke(oldWave, newWave);

			// ===== DESBLOQUEO EXTREME (INDEPENDIENTE Y SIN RETRASO) =====
			// Se ejecuta al completar la última ola en Extremo, sin esperar mensajes ni temporizadores.
			if (oldWave == maxWave && m_subsystemGreenNightSky.DifficultyMode == DifficultyMode.Extreme && !m_extremeCompletionDialogShown)
			{
				m_extremeCompletionDialogShown = true;

				// Reproducir sonido de desbloqueo (solo una vez, de forma inmediata)
				if (m_subsystemAudio != null)
				{
					m_subsystemAudio.PlaySound("Audio/Rocket Knight Adventures Stage Clear", 1f, 0f, 0f, 0f);
				}
				// NO mostrar el diálogo aquí; solo se activa el flag para el icono en el toggle
			}

			if (oldWave == 1 && newWave == 2)
			{
				foreach (var player in m_subsystemPlayers.ComponentPlayers)
				{
					AchievementsManager.UnlockAchievementStatic(player, 6, "FirstWaveSurvived", LanguageControl.Get(AchievementsWidget.fName, 16));
				}
			}

			if (m_subsystemGreenNightSky.DifficultyMode == DifficultyMode.Extreme && wasLastWave && !completed)
			{
				foreach (var player in m_subsystemPlayers.ComponentPlayers)
				{
					AchievementsManager.UnlockAchievementStatic(player, 52, "ExtremeNightSurvived", LanguageControl.Get(AchievementsWidget.fName, 108));
				}
			}

			if (m_subsystemGreenNightSky.DifficultyMode == DifficultyMode.Extreme && wasLastWave && newWave > maxWave)
			{
				foreach (var player in m_subsystemPlayers.ComponentPlayers)
				{
					AchievementsManager.UnlockAchievementStatic(player, 52, "ExtremeNightSurvived", LanguageControl.Get(AchievementsWidget.fName, 108));
				}
			}

			if (oldWave == maxWave && !m_hasShownUnlockMessage)
			{
				foreach (var player in m_subsystemPlayers.ComponentPlayers)
				{
					AchievementsManager.UnlockAchievementStatic(player, 7, "AllWavesCompleted", LanguageControl.Get(AchievementsWidget.fName, 18));
				}

				if (m_subsystemTime != null)
				{
					System.Timers.Timer timer1 = new System.Timers.Timer(4000);
					timer1.Elapsed += (sender, e) =>
					{
						timer1.Stop();
						timer1.Dispose();
						Dispatcher.Dispatch(() =>
						{
							if (!m_hasShownUnlockMessage)
							{
								m_hasShownUnlockMessage = true;

								string largeMessage = LanguageControl.Get("UnlockedItems", "Unlocked");
								string smallMessage = LanguageControl.Get("UnlockedItems", "UnlockedInfo");

								foreach (var player in m_subsystemPlayers.ComponentPlayers)
								{
									player.ComponentGui.DisplayLargeMessage(largeMessage, smallMessage, 5f, 0f);
								}

								// Mostrar el cofre con la carta (después de 5 segundos)
								System.Timers.Timer timer2 = new System.Timers.Timer(5000);
								timer2.Elapsed += (sender2, e2) =>
								{
									timer2.Stop();
									timer2.Dispose();
									Dispatcher.Dispatch(() =>
									{
										if (!m_letterWarSpawned)
											ShowLetterAnnouncementAndSpawn();
									});
								};
								timer2.Start();
							}
						});
					};
					timer1.Start();
				}
				else
				{
					m_hasShownUnlockMessage = true;
					string largeMessage = LanguageControl.Get("UnlockedItems", "Unlocked");
					string smallMessage = LanguageControl.Get("UnlockedItems", "UnlockedInfo");
					foreach (var player in m_subsystemPlayers.ComponentPlayers)
					{
						player.ComponentGui.DisplayLargeMessage(largeMessage, smallMessage, 5f, 0f);
					}
					if (!m_letterWarSpawned)
						ShowLetterAnnouncementAndSpawn();
				}
			}
		}

		private void ShowLetterAnnouncementAndSpawn()
		{
			var player = m_subsystemPlayers.ComponentPlayers.FirstOrDefault();
			if (player == null) return;

			string largeMessage = LanguageControl.Get("UnlockedItems", "ChestMessage");
			string smallMessage = LanguageControl.Get("UnlockedItems", "ChestInfo");

			player.ComponentGui.DisplayLargeMessage(largeMessage, smallMessage, 5f, 1f);

			m_subsystemAudio.PlaySound("Audio/Throw", 1f, m_random.Float(-0.2f, 0.2f), player.ComponentBody.Position, 10f, false);

			SpawnLetterChest(player);
		}

		private void SpawnLetterChest(ComponentPlayer player)
		{
			Vector3 playerPos = player.ComponentBody.Position;
			for (int attempt = 0; attempt < 50; attempt++)
			{
				float angle = m_random.Float(0, 2 * MathUtils.PI);
				float distance = m_random.Float(3f, 12f);
				int x = (int)(playerPos.X + MathF.Cos(angle) * distance);
				int z = (int)(playerPos.Z + MathF.Sin(angle) * distance);
				int groundY = m_subsystemTerrain.Terrain.GetTopHeight(x, z);

				if (groundY <= 0 || groundY >= 254) continue; // dejar espacio arriba

				// Verificar que el bloque del suelo sea válido
				int cellGround = m_subsystemTerrain.Terrain.GetCellValue(x, groundY, z);
				int contentsGround = Terrain.ExtractContents(cellGround);
				if (!m_allowedBlockIndices.Contains(contentsGround)) continue;

				// Verificar que la celda donde se pondrá el cofre esté vacía
				int cellAbove = m_subsystemTerrain.Terrain.GetCellValue(x, groundY + 1, z);
				if (Terrain.ExtractContents(cellAbove) != 0) continue;

				// Colocar el cofre
				int data = GetChestFacingData(playerPos, new Vector3(x + 0.5f, groundY + 1, z + 0.5f));
				int chestValue = Terrain.MakeBlockValue(ChestBlock.Index, 0, data);
				m_subsystemTerrain.ChangeCell(x, groundY + 1, z, chestValue);

				// Programar la inserción del objeto con un pequeño retraso
				System.Timers.Timer timer = new System.Timers.Timer(100); // 0.1 segundos
				timer.Elapsed += (sender, e) =>
				{
					timer.Stop();
					timer.Dispose();
					Dispatcher.Dispatch(() =>
					{
						// Buscar la entidad del cofre después del retraso
						var blockEntities = Project.FindSubsystem<SubsystemBlockEntities>(true);
						var blockEntity = blockEntities.GetBlockEntity(x, groundY + 1, z);
						if (blockEntity != null)
						{
							var chest = blockEntity.Entity.FindComponent<ComponentChest>();
							if (chest != null)
							{
								int letterIndex = BlocksManager.GetBlockIndex<LetterWarBlock>(false, false);
								if (letterIndex < 0) letterIndex = LetterWarBlock.Index;
								int letterValue = Terrain.MakeBlockValue(letterIndex, 0, 0);
								chest.AddSlotItems(0, letterValue, 1);
								m_letterWarSpawned = true;
								return;
							}
						}

						// Si no se encontró, eliminar el cofre y registrar error
						m_subsystemTerrain.ChangeCell(x, groundY + 1, z, 0);
						Log.Error($"[SubsystemZombiesSpawn] No se pudo agregar la carta al cofre en ({x},{groundY + 1},{z})");
					});
				};
				timer.Start();
				return; // Éxito, se programó la inserción
			}

			Log.Error("[SubsystemZombiesSpawn] No se pudo colocar el cofre con la carta cerca del jugador.");
		}

		private int GetChestFacingData(Vector3 playerPos, Vector3 chestPos)
		{
			Vector3 dir = chestPos - playerPos;
			dir.Y = 0;
			dir = Vector3.Normalize(dir);
			float angle = MathF.Atan2(dir.X, dir.Z);
			if (angle < -0.785f) return 1;
			else if (angle < 0.785f) return 0;
			else if (angle < 2.356f) return 3;
			else return 2;
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			valuesDictionary.SetValue("CurrentWave", m_currentWave);
			valuesDictionary.SetValue("LetterWarSpawned", m_letterWarSpawned);
			valuesDictionary.SetValue("HasShownUnlockMessage", m_hasShownUnlockMessage);

			valuesDictionary.SetValue("BossBattleActive", m_bossBattleActive);
			valuesDictionary.SetValue("HasSpawnedBossThisNight", m_hasSpawnedBossThisNight);
			valuesDictionary.SetValue("BossSpawnDelayed", m_bossSpawnDelayed);
			valuesDictionary.SetValue("BossSpawnDelayTimer", m_bossSpawnDelayTimer);
			valuesDictionary.SetValue("MidnightBossesSpawnedThisNight", m_midnightBossesSpawnedThisNight);
			valuesDictionary.SetValue("ExtremeCompletionDialogShown", m_extremeCompletionDialogShown);
			valuesDictionary.SetValue("HasAcceptedImpossibleChallenge", m_hasAcceptedImpossibleChallenge);

			string bossQueueStr = m_bossQueue.Count > 0 ? string.Join(",", m_bossQueue) : "";
			valuesDictionary.SetValue("BossQueue", bossQueueStr);
		}

		private Entity FindAliveBoss()
		{
			foreach (Entity entity in Project.Entities)
			{
				if (entity.ValuesDictionary.DatabaseObject != null &&
					BossTemplates.Contains(entity.ValuesDictionary.DatabaseObject.Name))
				{
					var health = entity.FindComponent<ComponentHealth>();
					if (health != null && health.Health > 0f)
					{
						return entity;
					}
				}
			}
			return null;
		}

		private void VerifyAndRestoreBossState()
		{
			if (!m_needsBossStateVerification) return;
			m_needsBossStateVerification = false;

			if (!m_bossBattleActive) return;

			Entity aliveBoss = FindAliveBoss();

			if (aliveBoss != null)
			{
				m_currentBossEntity = aliveBoss;
				RebuildBossQueue();
			}
			else
			{
				m_currentBossEntity = null;

				if (m_bossQueue.Count > 0)
				{
					m_bossSpawnDelayed = true;
					m_bossSpawnDelayTimer = 0.5f;
				}
				else
				{
					m_bossBattleActive = false;
				}
			}
		}

		public void Update(float dt)
		{
			UpdateCountdownLabel();

			bool isGreenNightActive = m_subsystemGreenNightSky.IsGreenNightActive;
			int maxWave = m_waves.Keys.Max();
			bool isNormalNight = IsNormalNight();

			// ===== VERIFICAR ESTADO DEL JEFE DESPUÉS DE CARGAR =====
			if (m_needsBossStateVerification)
			{
				if (Project.Entities.Count > 0)
				{
					VerifyAndRestoreBossState();
				}
			}

			// ===== SPAWN DE INFECTED SPIDER (SOLO SI LA OPCIÓN ESTÁ ACTIVA) =====
			if (ShittyCreaturesSettingsManager.NightCreaturesSpawnEnabled)
			{
				if (m_skeletonNewSpawnChunks.Count > 0 && !isGreenNightActive)
				{
					// Spawn CUEVAS - activo excepto en noche verde
					foreach (SpawnChunk chunk in m_skeletonNewSpawnChunks)
					{
						SpawnCaveSpidersInChunk(chunk, SpiderCaveNewChunkAttempts);
					}

					// Spawn SUPERFICIE - solo noche normal
					if (isNormalNight)
					{
						foreach (SpawnChunk chunk in m_skeletonNewSpawnChunks)
						{
							SpawnNormalSpidersInChunk(chunk, SpiderNormalNewChunkAttempts);
						}
					}
				}

				// Control de cooldown para spawn constante de superficie
				if (!isNormalNight || isGreenNightActive)
				{
					m_spiderConstantSpawnCooldown = SpiderConstantSpawnCooldownTime;
				}
				else
				{
					m_spiderConstantSpawnCooldown -= dt;
				}

				if (m_skeletonSpawnChunks.Count > 0 && !isGreenNightActive)
				{
					// Spawn CONSTANTE CUEVAS - activo excepto en noche verde
					foreach (SpawnChunk chunk in m_skeletonSpawnChunks)
					{
						SpawnConstantCaveSpidersInChunk(chunk, SpiderCaveConstantChunkAttempts);
					}

					// Spawn CONSTANTE SUPERFICIE - solo noche normal con cooldown
					if (isNormalNight && m_spiderConstantSpawnCooldown <= 0f)
					{
						foreach (SpawnChunk chunk in m_skeletonSpawnChunks)
						{
							SpawnConstantSpidersInChunk(chunk, SpiderConstantChunkAttempts);
						}
						m_spiderConstantSpawnCooldown = SpiderConstantSpawnCooldownTime;
					}
				}
			}
			else
			{
				// Si la opción está desactivada, reseteamos el cooldown para que no se dispare al reactivar
				m_spiderConstantSpawnCooldown = SpiderConstantSpawnCooldownTime;
			}
			// ===== FIN SPAWN DE INFECTED SPIDER =====

			// ===== SPAWN DE ESQUELETOS NORMALES =====
			if (ShittyCreaturesSettingsManager.NightCreaturesSpawnEnabled)
			{
				if (m_skeletonNewSpawnChunks.Count > 0)
				{
					if (isNormalNight && !isGreenNightActive)
					{
						m_skeletonNewSpawnChunks.RandomShuffle((int max) => m_random.Int(0, max - 1));
						foreach (SpawnChunk chunk in m_skeletonNewSpawnChunks)
						{
							SpawnNormalSkeletonsInChunk(chunk, SkeletonNormalNewChunkAttempts);
						}
					}
					m_skeletonNewSpawnChunks.Clear();
				}

				if (!isNormalNight || isGreenNightActive)
				{
					m_constantSpawnCooldown = ConstantSpawnCooldownTime;
				}
				else
				{
					m_constantSpawnCooldown -= dt;
				}

				if (m_skeletonSpawnChunks.Count > 0)
				{
					if (isNormalNight && !isGreenNightActive && m_constantSpawnCooldown <= 0f)
					{
						m_skeletonSpawnChunks.RandomShuffle((int max) => m_random.Int(0, max - 1));
						foreach (SpawnChunk chunk in m_skeletonSpawnChunks)
						{
							SpawnConstantSkeletonsInChunk(chunk, SkeletonConstantChunkAttempts);
						}
						m_constantSpawnCooldown = ConstantSpawnCooldownTime;
					}
					m_skeletonSpawnChunks.Clear();
				}
			}
			else
			{
				m_skeletonNewSpawnChunks.Clear();
				m_skeletonSpawnChunks.Clear();
			}
			// ===== FIN SPAWN DE ESQUELETOS NORMALES =====

			// ===== LÓGICA DE NOCHE VERDE =====
			// Guardar el estado anterior antes de actualizar
			bool wasGreenNightActive = m_wasGreenNightActive;

			// Detectar INICIO de la noche verde (transición de inactivo a activo)
			if (!wasGreenNightActive && isGreenNightActive)
			{
				m_nightEndProcessed = false;
				m_midnightBossesSpawnedThisNight = false; // Resetear flag de jefes a medianoche
				PlayEvilLaugh();
				SendWaveMessage();

				// Activar retraso de 5 segundos antes del spawn
				m_greenNightSpawnDelayActive = true;
				m_greenNightSpawnDelayTimer = GreenNightSpawnDelaySeconds;

				// Si hay jefes vivos de noches anteriores, no hacer nada con ellos
				Entity existingBoss = FindAliveBoss();
				if (existingBoss != null)
				{
					// Jefe existente, no eliminar
				}
				else if (m_bossBattleActive)
				{
					// Batalla activa pero sin jefe, resetear
				}

				// Iniciar batalla de jefes SOLO si es la ola final
				if (m_currentWave == maxWave && !m_hasSpawnedBossThisNight && !m_bossBattleActive)
				{
					StartBossBattle();
					m_bossSpawnDelayed = true;
					m_bossSpawnDelayTimer = GreenNightSpawnDelaySeconds; // 5 segundos de retraso
				}
			}
			// ===== FIN VERIFICACIÓN =====

			// Detectar FIN de la noche verde (transición de activo a inactivo)
			if (wasGreenNightActive && !isGreenNightActive && !m_nightEndProcessed)
			{
				// Solo resetear el estado de batalla (no eliminar entidades)
				m_nightEndProcessed = true;
			}

			// Actualizar el estado para la próxima iteración
			m_wasGreenNightActive = isGreenNightActive;

			// Guardar la hora anterior ANTES de retornar (para detectar medianoche)
			float currentTimeOfDay = m_subsystemTimeOfDay.TimeOfDay;

			if (!isGreenNightActive)
			{
				m_lastTimeOfDay = currentTimeOfDay;
				return;
			}

			// ===== SPAWN DE JEFES A MEDIANOCHE (EXCEPTO OLA FINAL) =====
			if (!m_midnightBossesSpawnedThisNight && m_currentWave != maxWave)
			{
				float midnight = m_subsystemTimeOfDay.Midnight;
				bool passedMidnight = false;

				// Caso normal: pasamos de antes de medianoche a después
				if (m_lastTimeOfDay < midnight && currentTimeOfDay >= midnight)
				{
					passedMidnight = true;
				}
				// Caso especial: medianoche cerca del 0.0 (cambio de día)
				else if (m_lastTimeOfDay > 0.9f && currentTimeOfDay < 0.1f && midnight < 0.1f)
				{
					passedMidnight = true;
				}
				// Caso: estamos exactamente en medianoche (por si se perdió el momento exacto)
				else if (Math.Abs(currentTimeOfDay - midnight) < MidnightDetectionTolerance)
				{
					passedMidnight = true;
				}

				if (passedMidnight)
				{
					SpawnMidnightBosses();
					m_midnightBossesSpawnedThisNight = true;
				}
			}
			// ===== FIN SPAWN DE JEFES A MEDIANOCHE =====

			m_lastTimeOfDay = currentTimeOfDay;

			// ===== GESTIONAR RETRASO DE SPAWN DE JEFE =====
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
			// ===== FIN GESTIÓN RETRASO =====

			// ===== VERIFICAR SI EL JEFE ACTUAL MURIÓ =====
			if (m_bossBattleActive)
			{
				if (m_currentBossEntity != null && !IsEntityAlive(m_currentBossEntity))
				{
					m_currentBossEntity = null;
					AdvanceBossBattle();
				}
				else if (m_currentBossEntity == null && m_bossQueue.Count > 0 && !m_bossSpawnDelayed)
				{
					m_bossSpawnDelayed = true;
					m_bossSpawnDelayTimer = 0.5f;
				}
			}
			// ===== FIN VERIFICACIÓN MUERTE JEFE =====

			// ===== ACTUALIZAR RETRASO DE SPAWN (SOLO NOCHE VERDE) =====
			if (m_greenNightSpawnDelayActive)
			{
				m_greenNightSpawnDelayTimer -= dt;
				if (m_greenNightSpawnDelayTimer <= 0f)
				{
					m_greenNightSpawnDelayActive = false;
					m_greenNightSpawnDelayTimer = 0f;
				}
			}
			// ===== FIN ACTUALIZAR RETRASO =====

			// ===== SPAWN NORMAL DE CRIATURAS (SOLO NOCHE VERDE, DESPUÉS DEL RETRASO) =====
			if (!m_greenNightSpawnDelayActive)
			{
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
			// ===== FIN SPAWN NORMAL DE CRIATURAS =====
		}

		private void RebuildBossQueue()
		{
			m_bossQueue.Clear();
			if (m_currentBossEntity == null) return;

			string currentBossName = m_currentBossEntity.ValuesDictionary.DatabaseObject?.Name;
			if (string.IsNullOrEmpty(currentBossName)) return;

			var bosses = new List<string>();
			foreach (var entry in m_currentWaveEntries)
			{
				if (BossTemplates.Contains(entry.TemplateName) && !bosses.Contains(entry.TemplateName))
					bosses.Add(entry.TemplateName);
			}

			bool foundCurrent = false;
			foreach (string boss in bosses)
			{
				if (foundCurrent)
				{
					m_bossQueue.Enqueue(boss);
				}
				else if (boss == currentBossName)
				{
					foundCurrent = true;
				}
			}
		}

		// ===== SPAWN NORMAL DE ESQUELETOS =====
		private void SpawnNormalSkeletonsInChunk(SpawnChunk chunk, int maxAttempts)
		{
			int currentCount = CountSkeletons(false);
			if (currentCount >= SkeletonNormalTotalLimit)
				return;

			Vector2 c1 = new Vector2(chunk.Point.X * 16, chunk.Point.Y * 16) - new Vector2(16);
			Vector2 c2 = new Vector2((chunk.Point.X + 1) * 16, (chunk.Point.Y + 1) * 16) + new Vector2(16);
			int areaCount = CountSkeletonsInArea(c1, c2, false);

			for (int i = 0; i < maxAttempts; i++)
			{
				if (currentCount >= SkeletonNormalTotalLimit || areaCount >= SkeletonNormalAreaLimit)
					break;

				Point3? spawnPoint = GetRandomChunkSpawnPoint(chunk);
				if (!spawnPoint.HasValue)
					continue;

				Point3 point = spawnPoint.Value;

				float suitability = CalculateNormalSkeletonSuitability(point);
				if (suitability <= 0f)
					continue;

				int spawned = SpawnSkeletonsAtPoint(point, false, 1);
				currentCount += spawned;
				areaCount += spawned;
			}
		}

		private float CalculateNormalSkeletonSuitability(Point3 point)
		{
			int x = point.X;
			int y = point.Y;
			int z = point.Z;

			if (m_subsystemSky.SkyLightIntensity >= NightLightThreshold)
				return 0f;

			TerrainChunk chunk = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunk == null || chunk.State <= TerrainChunkState.InvalidPropagatedLight)
				return 0f;

			int cellValueFast = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
			int cellValueFast2 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
			int cellValueFast3 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

			Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
			Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
			Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];

			bool belowSolid = (block.IsCollidable_(cellValueFast) || block is WaterBlock);
			bool currentEmpty = (!block2.IsCollidable_(cellValueFast2) && !(block2 is WaterBlock));
			bool aboveEmpty = (!block3.IsCollidable_(cellValueFast3) && !(block3 is WaterBlock));

			if (!belowSolid || !currentEmpty || !aboveEmpty)
				return 0f;

			int belowContents = Terrain.ExtractContents(cellValueFast);

			if (!m_allowedBlockIndices.Contains(belowContents))
				return 0f;

			int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
			if (y > topHeight + 2)
				return 0f;

			return SkeletonNormalSuitability;
		}
		// ===== FIN SPAWN NORMAL =====

		// ===== SPAWN CONSTANTE DE ESQUELETOS =====
		private void SpawnConstantSkeletonsInChunk(SpawnChunk chunk, int maxAttempts)
		{
			int totalLimit = (m_subsystemGameInfo.WorldSettings.GameMode >= GameMode.Challenging)
				? SkeletonConstantTotalLimitChallenging
				: SkeletonConstantTotalLimitNormal;

			int currentCount = CountSkeletons(true);
			if (currentCount >= totalLimit)
				return;

			Vector2 c1 = new Vector2(chunk.Point.X * 16, chunk.Point.Y * 16) - new Vector2(SkeletonConstantAreaRadius);
			Vector2 c2 = new Vector2((chunk.Point.X + 1) * 16, (chunk.Point.Y + 1) * 16) + new Vector2(SkeletonConstantAreaRadius);
			int areaCount = CountSkeletonsInArea(c1, c2, true);

			for (int i = 0; i < maxAttempts; i++)
			{
				if (currentCount >= totalLimit || areaCount >= SkeletonConstantAreaLimit)
					break;

				Point3? spawnPoint = GetRandomChunkSpawnPoint(chunk);
				if (!spawnPoint.HasValue)
					continue;

				Point3 point = spawnPoint.Value;

				float suitability = CalculateConstantSkeletonSuitability(point);
				if (suitability <= 0f)
					continue;

				int spawned = SpawnSkeletonsAtPoint(point, true, 1);
				currentCount += spawned;
				areaCount += spawned;
			}
		}

		private float CalculateConstantSkeletonSuitability(Point3 point)
		{
			int x = point.X;
			int y = point.Y;
			int z = point.Z;

			if (m_subsystemSky.SkyLightIntensity >= NightLightThreshold)
				return 0f;

			TerrainChunk chunk = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunk == null || chunk.State <= TerrainChunkState.InvalidPropagatedLight)
				return 0f;

			int cellValueFast = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
			int cellValueFast2 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
			int cellValueFast3 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

			Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
			Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
			Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];

			bool belowSolid = (block.IsCollidable_(cellValueFast) || block is WaterBlock);
			bool currentEmpty = (!block2.IsCollidable_(cellValueFast2) && !(block2 is WaterBlock));
			bool aboveEmpty = (!block3.IsCollidable_(cellValueFast3) && !(block3 is WaterBlock));

			if (!belowSolid || !currentEmpty || !aboveEmpty)
				return 0f;

			int belowContents = Terrain.ExtractContents(cellValueFast);

			if (!m_allowedBlockIndices.Contains(belowContents))
				return 0f;

			int cellLightFast = m_subsystemTerrain.Terrain.GetCellLightFast(x, y + 1, z);
			if (cellLightFast > 7)
				return 0f;

			int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
			if (y > topHeight + 2)
				return 0f;

			int currentCount = CountSkeletons(true);
			int totalLimit = (m_subsystemGameInfo.WorldSettings.GameMode >= GameMode.Challenging)
				? SkeletonConstantTotalLimitChallenging
				: SkeletonConstantTotalLimitNormal;

			float limitFactor = 1f - ((float)currentCount / totalLimit * 0.7f);

			return SkeletonConstantSuitability * limitFactor;
		}
		// ===== FIN SPAWN CONSTANTE =====

		// ===== MÉTODOS COMPARTIDOS =====

		private Point3? GetRandomChunkSpawnPoint(SpawnChunk chunk)
		{
			for (int i = 0; i < 5; i++)
			{
				int x = 16 * chunk.Point.X + m_random.Int(0, 15);
				int y = m_random.Int(10, 246);
				int z = 16 * chunk.Point.Y + m_random.Int(0, 15);

				Point3? result = ProcessSkeletonSpawnPoint(new Point3(x, y, z));
				if (result.HasValue)
				{
					return result;
				}
			}
			return null;
		}

		private Point3? ProcessSkeletonSpawnPoint(Point3 spawnPoint)
		{
			int x = spawnPoint.X;
			int num = Math.Clamp(spawnPoint.Y, 1, 254);
			int z = spawnPoint.Z;

			TerrainChunk chunkAtCell = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunkAtCell == null || chunkAtCell.State <= TerrainChunkState.InvalidPropagatedLight)
				return null;

			for (int i = 0; i < 30; i++)
			{
				Point3 pointUp = new Point3(x, num + i, z);
				if (TestSkeletonSpawnPoint(pointUp))
				{
					return pointUp;
				}

				Point3 pointDown = new Point3(x, num - i, z);
				if (TestSkeletonSpawnPoint(pointDown))
				{
					return pointDown;
				}
			}
			return null;
		}

		private bool TestSkeletonSpawnPoint(Point3 spawnPoint)
		{
			int x = spawnPoint.X;
			int y = spawnPoint.Y;
			int z = spawnPoint.Z;

			if (y <= 3 || y >= 253)
				return false;

			int cellValueFast = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
			int cellValueFast2 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
			int cellValueFast3 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

			Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
			Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
			Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];

			return (block.IsCollidable_(cellValueFast) || block is WaterBlock)
				&& !block2.IsCollidable_(cellValueFast2) && !(block2 is WaterBlock)
				&& !block3.IsCollidable_(cellValueFast3) && !(block3 is WaterBlock);
		}

		private Point3? ProcessSpiderSpawnPoint(Point3 spawnPoint)
		{
			int x = spawnPoint.X;
			int num = Math.Clamp(spawnPoint.Y, 1, 254);
			int z = spawnPoint.Z;

			TerrainChunk chunkAtCell = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunkAtCell == null || chunkAtCell.State <= TerrainChunkState.InvalidPropagatedLight)
				return null;

			for (int i = 0; i < 30; i++)
			{
				Point3 pointUp = new Point3(x, num + i, z);
				if (TestSpiderSpawnPoint(pointUp))
				{
					return pointUp;
				}

				Point3 pointDown = new Point3(x, num - i, z);
				if (TestSpiderSpawnPoint(pointDown))
				{
					return pointDown;
				}
			}
			return null;
		}

		private bool TestSpiderSpawnPoint(Point3 spawnPoint)
		{
			int x = spawnPoint.X;
			int y = spawnPoint.Y;
			int z = spawnPoint.Z;

			if (y <= 3 || y >= 253)
				return false;

			int cellValueFast = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
			int cellValueFast2 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
			int cellValueFast3 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

			Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
			Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
			Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];

			if (block is WaterBlock || block is MagmaBlock)
				return false;
			if (block2 is WaterBlock || block2 is MagmaBlock)
				return false;
			if (block3 is WaterBlock || block3 is MagmaBlock)
				return false;

			int belowContents = Terrain.ExtractContents(cellValueFast);
			if (!m_allowedBlockIndices.Contains(belowContents))
				return false;

			return block.IsCollidable_(cellValueFast)
				&& !block2.IsCollidable_(cellValueFast2)
				&& !block3.IsCollidable_(cellValueFast3);
		}

		private int SpawnSkeletonsAtPoint(Point3 point, bool constantSpawn, int count)
		{
			int spawned = 0;
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

				Point3? processedPoint = ProcessSkeletonSpawnPoint(spawnPoint);
				if (processedPoint.HasValue)
				{
					Vector3 position = new Vector3(
						processedPoint.Value.X + m_random.Float(0.4f, 0.6f),
						processedPoint.Value.Y + 1.1f,
						processedPoint.Value.Z + m_random.Float(0.4f, 0.6f)
					);

					Entity entity = m_subsystemCreatureSpawn.SpawnCreature(
						SkeletonTemplateName,
						position,
						constantSpawn
					);

					if (entity != null)
					{
						spawned++;
						count--;
					}
				}
				attempts++;
			}
			return spawned;
		}

		private int CountSkeletons(bool constantSpawn)
		{
			int count = 0;
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature.ConstantSpawn == constantSpawn)
				{
					if (body.Entity.ValuesDictionary.DatabaseObject?.Name == SkeletonTemplateName)
					{
						count++;
					}
				}
			}
			return count;
		}

		private int CountSkeletonsInArea(Vector2 c1, Vector2 c2, bool constantSpawn)
		{
			int count = 0;
			m_tempBodiesArray.Clear();
			m_subsystemBodies.FindBodiesInArea(c1, c2, m_tempBodiesArray);

			for (int i = 0; i < m_tempBodiesArray.Count; i++)
			{
				ComponentBody body = m_tempBodiesArray.Array[i];
				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();

				if (creature != null && creature.ConstantSpawn == constantSpawn)
				{
					if (body.Entity.ValuesDictionary.DatabaseObject?.Name == SkeletonTemplateName)
					{
						Vector3 position = body.Position;
						if (position.X >= c1.X && position.X <= c2.X &&
							position.Z >= c1.Y && position.Z <= c2.Y)
						{
							count++;
						}
					}
				}
			}
			return count;
		}

		private bool IsNormalNight()
		{
			if (m_subsystemGreenNightSky.IsGreenNightActive)
				return false;

			TimeOfDayMode mode = m_subsystemGameInfo.WorldSettings.TimeOfDayMode;
			if (mode == TimeOfDayMode.Day || mode == TimeOfDayMode.Sunrise)
				return false;

			// Usar la hora del día para determinar si es noche
			float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
			float dusk = m_subsystemTimeOfDay.DuskStart;
			float dawn = m_subsystemTimeOfDay.DawnStart;
			return timeOfDay >= dusk || timeOfDay < dawn;
		}
		// ===== FIN MÉTODOS COMPARTIDOS =====

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
				bool spawnedMounted = false;
				if (MountedZombiesEnabled &&
					!BossTemplates.Contains(entry.TemplateName) &&
					!MiniBossTemplates.Contains(entry.TemplateName) &&
					m_random.Float(0f, 1f) < MountedSpawnProbability &&
					m_currentWaveEntries.Any(e => e.TemplateName == "InfectedBear"))
				{
					spawnedMounted = TrySpawnMountedCreature(entry.TemplateName, spawnPos);
				}
				if (spawnedMounted)
				{
					spawned = 1;
				}
				else
				{
					m_subsystemCreatureSpawn.SpawnCreature(entry.TemplateName, spawnPos, false);
					spawned++;
				}
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

		private bool CanSpawnCreature(string templateName, Vector3 pos)
		{
			if (ColdOnlyTemplates.Contains(templateName))
			{
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
						if (m_allowedBlockIndices.Contains(contents))
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
						if (m_allowedBlockIndices.Contains(contents))
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
						if (m_allowedBlockIndices.Contains(contents))
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

					if (y >= 10 && y <= 255)
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
				m_spawnInterval = Math.Max(1.2f, BaseSpawnInterval - (wave * 0.04f));
			}
			else
			{
				m_currentWaveEntries = m_waves.ContainsKey(1) ? m_waves[1] : new List<WaveEntry>();
				m_currentWave = 1;
				m_spawnInterval = BaseSpawnInterval;
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
						if (m_allowedBlockIndices.Contains(contents))
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

		private bool AdvanceToNextWave()
		{
			if (m_isAdvancingWave) return false;
			m_isAdvancingWave = true;

			m_hasSpawnedBossThisNight = false;
			m_bossBattleActive = false;
			m_bossSpawnDelayed = false;
			m_bossQueue.Clear();
			m_currentBossEntity = null;

			int nextWave = m_currentWave + 1;
			int maxWave = m_waves.Keys.Max();

			bool advanced = false;
			if (nextWave <= maxWave && m_waves.ContainsKey(nextWave))
			{
				m_currentWave = nextWave;
				SetCurrentWave(m_currentWave);
				advanced = true;
			}

			m_isAdvancingWave = false;
			return advanced;
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
						if (m_allowedBlockIndices.Contains(contents))
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
			if (bossTemplate.StartsWith("Tank") || bossTemplate.StartsWith("FrozenTank"))
				return "BossTank";
			if (bossTemplate.StartsWith("GhostTank") || bossTemplate.StartsWith("TankGhost") || bossTemplate.StartsWith("FrozenTankGhost"))
				return "BossGhostTank";
			if (bossTemplate == "MachineGunInfected")
				return "BossMachineGun";
			if (bossTemplate == "FlyingInfectedBoss")
				return "BossFlying";
			return "BossGeneric";
		}

		private string GetRandomMountTemplateForZombie()
		{
			float roll = m_random.Float(0f, 1f);
			if (roll < m_flyMountProbability)
				return "InfectedFly1";
			if (roll < m_flyMountProbability + m_bearMountProbability)
				return "InfectedBear";
			if (roll < m_flyMountProbability + m_bearMountProbability + m_horseMountProbability)
			{
				List<string> horseList = new List<string>(MountableHorseTemplates);
				return horseList[m_random.Int(0, horseList.Count - 1)];
			}
			return null;
		}

		private bool TrySpawnMountedCreature(string zombieTemplate, Vector3 spawnPos)
		{
			if (!MountedZombiesEnabled) return false;

			string mountTemplate = GetRandomMountTemplateForZombie();
			if (string.IsNullOrEmpty(mountTemplate)) return false;

			int totalCreatures = m_subsystemCreatureSpawn.CountCreatures(false);
			if (totalCreatures + 2 > MaxGlobalCreatures) return false;

			Vector2 areaMin = new Vector2(spawnPos.X - 16, spawnPos.Z - 16);
			Vector2 areaMax = new Vector2(spawnPos.X + 16, spawnPos.Z + 16);
			int areaCount = m_subsystemCreatureSpawn.CountCreaturesInArea(areaMin, areaMax, false);
			if (areaCount + 2 > MaxCreaturesPerArea) return false;

			Entity mountEntity = m_subsystemCreatureSpawn.SpawnCreature(mountTemplate, spawnPos, false);
			if (mountEntity == null) return false;

			ComponentMount mountComp = mountEntity.FindComponent<ComponentMount>();
			if (mountComp == null)
			{
				Project.RemoveEntity(mountEntity, true);
				return false;
			}

			Entity zombieEntity = m_subsystemCreatureSpawn.SpawnCreature(zombieTemplate, spawnPos, false);
			if (zombieEntity == null)
			{
				Project.RemoveEntity(mountEntity, true);
				return false;
			}

			ComponentRider rider = zombieEntity.FindComponent<ComponentRider>();
			if (rider == null)
			{
				Project.RemoveEntity(zombieEntity, true);
				Project.RemoveEntity(mountEntity, true);
				return false;
			}

			rider.StartMounting(mountComp);
			return true;
		}

		// ===== SPAWN NORMAL DE INFECTED SPIDER (SUPERFICIE) =====
		private void SpawnNormalSpidersInChunk(SpawnChunk chunk, int maxAttempts)
		{
			int currentCount = CountSpidersInWorld(false); // false = isCave (superficie)
			if (currentCount >= SpiderNormalTotalLimit)
				return;

			Vector2 c1 = new Vector2(chunk.Point.X * 16, chunk.Point.Y * 16) - new Vector2(16);
			Vector2 c2 = new Vector2((chunk.Point.X + 1) * 16, (chunk.Point.Y + 1) * 16) + new Vector2(16);
			int areaCount = CountSpidersInArea(c1, c2, false);

			for (int i = 0; i < maxAttempts; i++)
			{
				if (currentCount >= SpiderNormalTotalLimit || areaCount >= SpiderNormalAreaLimit)
					break;

				Point3? spawnPoint = GetRandomSurfaceChunkSpawnPoint(chunk); // Método específico superficie
				if (!spawnPoint.HasValue)
					continue;

				int spawned = SpawnSpidersAtPoint(spawnPoint.Value, false);
				currentCount += spawned;
				areaCount += spawned;
			}
		}

		private float CalculateNormalSpiderSuitability(Point3 point)
		{
			int x = point.X;
			int y = point.Y;
			int z = point.Z;

			// Reutilizar la lógica de TestSurfaceSpiderSpawnPoint para asegurar que es un punto válido
			if (!TestSurfaceSpiderSpawnPoint(point))
				return 0f;

			// Además, verificar que la luz del cielo sea baja (noche real)
			// Pero ya confiamos en que IsNormalNight() es true, así que no es necesario repetir.
			// Sin embargo, podemos verificar la luz global para mayor seguridad:
			if (m_subsystemSky.SkyLightIntensity >= NightLightThreshold)
				return 0f;

			return SpiderNormalSuitability;
		}
		// ===== FIN SPAWN NORMAL INFECTED SPIDER =====

		// ===== SPAWN CONSTANTE DE INFECTED SPIDER (SUPERFICIE) =====
		private void SpawnConstantSpidersInChunk(SpawnChunk chunk, int maxAttempts)
		{
			int totalLimit = (m_subsystemGameInfo.WorldSettings.GameMode >= GameMode.Challenging)
				? SpiderConstantTotalLimitChallenging
				: SpiderConstantTotalLimitNormal;

			int currentCount = CountSpidersInWorld(false); // false = isCave (superficie)
			if (currentCount >= totalLimit)
				return;

			Vector2 c1 = new Vector2(chunk.Point.X * 16, chunk.Point.Y * 16) - new Vector2(SpiderConstantAreaRadius);
			Vector2 c2 = new Vector2((chunk.Point.X + 1) * 16, (chunk.Point.Y + 1) * 16) + new Vector2(SpiderConstantAreaRadius);
			int areaCount = CountSpidersInArea(c1, c2, false);

			for (int i = 0; i < maxAttempts; i++)
			{
				if (currentCount >= totalLimit || areaCount >= SpiderConstantAreaLimit)
					break;

				Point3? spawnPoint = GetRandomSurfaceChunkSpawnPoint(chunk); // Método específico superficie
				if (!spawnPoint.HasValue)
					continue;

				int spawned = SpawnSpidersAtPoint(spawnPoint.Value, true);
				currentCount += spawned;
				areaCount += spawned;
			}
		}

		private int SpawnSpidersAtPoint(Point3 point, bool constantSpawn)
		{
			Vector3 position = new Vector3(
				point.X + m_random.Float(0.4f, 0.6f),
				point.Y + 1.1f,
				point.Z + m_random.Float(0.4f, 0.6f)
			);

			Entity entity = m_subsystemCreatureSpawn.SpawnCreature(
				InfectedSpiderTemplateName,
				position,
				constantSpawn
			);

			return entity != null ? 1 : 0;
		}

		private int CountSpidersInWorld(bool isCave)
		{
			int count = 0;
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body.Entity.ValuesDictionary.DatabaseObject?.Name == InfectedSpiderTemplateName)
				{
					Vector3 pos = body.Position;
					int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(Terrain.ToCell(pos.X), Terrain.ToCell(pos.Z));

					// Si está pidiendo cuevas, cuenta las que están bajo tierra
					// Si está pidiendo superficie, cuenta las que están en o sobre el topHeight
					bool creatureInCave = pos.Y < topHeight;

					if (creatureInCave == isCave)
					{
						count++;
					}
				}
			}
			return count;
		}

		private float CalculateSurfaceSpiderSuitability(Point3 point)
		{
			int x = point.X;
			int y = point.Y;
			int z = point.Z;

			if (m_subsystemSky.SkyLightIntensity >= NightLightThreshold)
				return 0f;

			TerrainChunk chunk = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunk == null || chunk.State <= TerrainChunkState.InvalidPropagatedLight)
				return 0f;

			int cellValueFast = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
			int cellValueFast2 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
			int cellValueFast3 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

			Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
			Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
			Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];

			// No agua/magma
			if (block is WaterBlock || block is MagmaBlock)
				return 0f;
			if (block2 is WaterBlock || block2 is MagmaBlock)
				return 0f;
			if (block3 is WaterBlock || block3 is MagmaBlock)
				return 0f;

			// Solo bloques de superficie permitidos
			int belowContents = Terrain.ExtractContents(cellValueFast);
			if (!m_allowedBlockIndices.Contains(belowContents))
				return 0f;

			// Sólido abajo, vacío actual y arriba
			if (!block.IsCollidable_(cellValueFast) ||
				block2.IsCollidable_(cellValueFast2) ||
				block3.IsCollidable_(cellValueFast3))
			{
				return 0f;
			}

			// Verificar que esté en o cerca de la superficie (no bajo tierra)
			int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
			if (y > topHeight + 2 || y < topHeight - 2)
				return 0f;

			return SpiderNormalSuitability;
		}

		private float CalculateConstantSpiderSuitability(Point3 point)
		{
			int x = point.X;
			int y = point.Y;
			int z = point.Z;

			if (m_subsystemSky.SkyLightIntensity >= NightLightThreshold)
				return 0f;

			TerrainChunk chunk = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunk == null || chunk.State <= TerrainChunkState.InvalidPropagatedLight)
				return 0f;

			int cellValueFast = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
			int cellValueFast2 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
			int cellValueFast3 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

			Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
			Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
			Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];

			bool belowSolid = (block.IsCollidable_(cellValueFast) || block is WaterBlock);
			bool currentEmpty = (!block2.IsCollidable_(cellValueFast2) && !(block2 is WaterBlock));
			bool aboveEmpty = (!block3.IsCollidable_(cellValueFast3) && !(block3 is WaterBlock));

			if (!belowSolid || !currentEmpty || !aboveEmpty)
				return 0f;

			int belowContents = Terrain.ExtractContents(cellValueFast);

			if (!m_allowedBlockIndices.Contains(belowContents))
				return 0f;

			int cellLightFast = m_subsystemTerrain.Terrain.GetCellLightFast(x, y + 1, z);
			if (cellLightFast > 7)
				return 0f;

			int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
			if (y > topHeight + 2)
				return 0f;

			int currentCount = CountSpiders(true);
			int totalLimit = (m_subsystemGameInfo.WorldSettings.GameMode >= GameMode.Challenging)
				? SpiderConstantTotalLimitChallenging
				: SpiderConstantTotalLimitNormal;

			float limitFactor = 1f - ((float)currentCount / totalLimit * 0.7f);

			return SpiderConstantSuitability * limitFactor;
		}

		private int SpawnSpidersAtPoint(Point3 point, bool constantSpawn, int count)
		{
			int spawned = 0;
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

				// Determinar si es cueva o superficie según la posición
				int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(spawnPoint.X, spawnPoint.Z);
				bool isCave = spawnPoint.Y < topHeight;

				Point3? processedPoint;
				if (isCave)
				{
					processedPoint = ProcessCaveSpiderSpawnPoint(spawnPoint, topHeight);
				}
				else
				{
					processedPoint = ProcessSpiderSpawnPoint(spawnPoint);
				}

				if (processedPoint.HasValue)
				{
					Vector3 position = new Vector3(
						processedPoint.Value.X + m_random.Float(0.4f, 0.6f),
						processedPoint.Value.Y + 1.1f,
						processedPoint.Value.Z + m_random.Float(0.4f, 0.6f)
					);

					Entity entity = m_subsystemCreatureSpawn.SpawnCreature(
						InfectedSpiderTemplateName,
						position,
						constantSpawn
					);

					if (entity != null)
					{
						spawned++;
						count--;
					}
				}
				attempts++;
			}
			return spawned;
		}

		private int CountSpiders(bool constantSpawn, bool isCave)
		{
			int count = 0;
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature.ConstantSpawn == constantSpawn)
				{
					if (body.Entity.ValuesDictionary.DatabaseObject?.Name == InfectedSpiderTemplateName)
					{
						// Verificar si está en cueva o superficie según posición
						Vector3 pos = body.Position;
						int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(
							Terrain.ToCell(pos.X),
							Terrain.ToCell(pos.Z));
						bool creatureInCave = pos.Y < topHeight;

						if (creatureInCave == isCave)
						{
							count++;
						}
					}
				}
			}
			return count;
		}

		private int CountSpidersInArea(Vector2 c1, Vector2 c2, bool constantSpawn, bool isCave)
		{
			int count = 0;
			m_tempBodiesArray.Clear();
			m_subsystemBodies.FindBodiesInArea(c1, c2, m_tempBodiesArray);

			for (int i = 0; i < m_tempBodiesArray.Count; i++)
			{
				ComponentBody body = m_tempBodiesArray.Array[i];
				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();

				if (creature != null && creature.ConstantSpawn == constantSpawn)
				{
					if (body.Entity.ValuesDictionary.DatabaseObject?.Name == InfectedSpiderTemplateName)
					{
						Vector3 position = body.Position;
						if (position.X >= c1.X && position.X <= c2.X &&
							position.Z >= c1.Y && position.Z <= c2.Y)
						{
							// Verificar cueva vs superficie
							int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(
								Terrain.ToCell(position.X),
								Terrain.ToCell(position.Z));
							bool creatureInCave = position.Y < topHeight;

							if (creatureInCave == isCave)
							{
								count++;
							}
						}
					}
				}
			}
			return count;
		}

		private int CountSpiders(bool constantSpawn)
		{
			int count = 0;
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature.ConstantSpawn == constantSpawn)
				{
					if (body.Entity.ValuesDictionary.DatabaseObject?.Name == InfectedSpiderTemplateName)
					{
						count++;
					}
				}
			}
			return count;
		}

		private int CountSpidersInArea(Vector2 c1, Vector2 c2, bool isCave)
		{
			int count = 0;
			m_tempBodiesArray.Clear();
			m_subsystemBodies.FindBodiesInArea(c1, c2, m_tempBodiesArray);

			for (int i = 0; i < m_tempBodiesArray.Count; i++)
			{
				ComponentBody body = m_tempBodiesArray.Array[i];

				if (body.Entity.ValuesDictionary.DatabaseObject?.Name == InfectedSpiderTemplateName)
				{
					Vector3 position = body.Position;
					if (position.X >= c1.X && position.X <= c2.X &&
						position.Z >= c1.Y && position.Z <= c2.Y)
					{
						int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(Terrain.ToCell(position.X), Terrain.ToCell(position.Z));
						bool creatureInCave = position.Y < topHeight;

						if (creatureInCave == isCave)
						{
							count++;
						}
					}
				}
			}
			return count;
		}

		// ===== SPAWN DE INFECTED SPIDER EN CUEVAS =====
		private void SpawnCaveSpidersInChunk(SpawnChunk chunk, int maxAttempts)
		{
			int currentCount = CountSpidersInWorld(true); // true = isCave
			if (currentCount >= SpiderCaveTotalLimit)
				return;

			Vector2 c1 = new Vector2(chunk.Point.X * 16, chunk.Point.Y * 16) - new Vector2(16);
			Vector2 c2 = new Vector2((chunk.Point.X + 1) * 16, (chunk.Point.Y + 1) * 16) + new Vector2(16);
			int areaCount = CountSpidersInArea(c1, c2, true);

			for (int i = 0; i < maxAttempts; i++)
			{
				if (currentCount >= SpiderCaveTotalLimit || areaCount >= SpiderCaveAreaLimit)
					break;

				Point3? spawnPoint = GetRandomCaveChunkSpawnPoint(chunk); // Método específico cueva
				if (!spawnPoint.HasValue)
					continue;

				int spawned = SpawnSpidersAtPoint(spawnPoint.Value, false);
				currentCount += spawned;
				areaCount += spawned;
			}
		}

		private void SpawnConstantCaveSpidersInChunk(SpawnChunk chunk, int maxAttempts)
		{
			int currentCount = CountSpidersInWorld(true); // true = isCave
			if (currentCount >= SpiderCaveConstantTotalLimit)
				return;

			Vector2 c1 = new Vector2(chunk.Point.X * 16, chunk.Point.Y * 16) - new Vector2(SpiderConstantAreaRadius);
			Vector2 c2 = new Vector2((chunk.Point.X + 1) * 16, (chunk.Point.Y + 1) * 16) + new Vector2(SpiderConstantAreaRadius);
			int areaCount = CountSpidersInArea(c1, c2, true);

			for (int i = 0; i < maxAttempts; i++)
			{
				if (currentCount >= SpiderCaveConstantTotalLimit || areaCount >= SpiderCaveConstantAreaLimit)
					break;

				Point3? spawnPoint = GetRandomCaveChunkSpawnPoint(chunk); // Método específico cueva
				if (!spawnPoint.HasValue)
					continue;

				int spawned = SpawnSpidersAtPoint(spawnPoint.Value, true);
				currentCount += spawned;
				areaCount += spawned;
			}
		}

		private float CalculateCaveSpiderSuitability(Point3 point)
		{
			int x = point.X;
			int y = point.Y;
			int z = point.Z;

			// Verificación doble de que esté bajo tierra
			int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
			if (y >= topHeight)
				return 0f;

			TerrainChunk chunk = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunk == null || chunk.State <= TerrainChunkState.InvalidPropagatedLight)
				return 0f;

			int cellValueFast = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
			int cellValueFast2 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
			int cellValueFast3 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

			Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
			Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
			Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];

			// No agua/magma
			if (block is WaterBlock || block is MagmaBlock)
				return 0f;
			if (block2 is WaterBlock || block2 is MagmaBlock)
				return 0f;
			if (block3 is WaterBlock || block3 is MagmaBlock)
				return 0f;

			// Solo bloques de cueva
			int belowContents = Terrain.ExtractContents(cellValueFast);
			if (belowContents != 2 && belowContents != 3 && belowContents != 4 &&
				belowContents != 66 && belowContents != 67 && belowContents != 7)
			{
				return 0f;
			}

			// Sólido abajo, vacío actual y arriba
			if (!block.IsCollidable_(cellValueFast) ||
				block2.IsCollidable_(cellValueFast2) ||
				block3.IsCollidable_(cellValueFast3))
			{
				return 0f;
			}

			return SpiderCaveSuitability;
		}

		private Point3? GetRandomCaveSpawnPoint(SpawnChunk chunk)
		{
			for (int i = 0; i < 8; i++)
			{
				int x = 16 * chunk.Point.X + m_random.Int(0, 15);
				int y = m_random.Int(5, 60);
				int z = 16 * chunk.Point.Y + m_random.Int(0, 15);

				Point3? result = ProcessCaveSpawnPoint(new Point3(x, y, z));
				if (result.HasValue)
				{
					return result;
				}
			}
			return null;
		}

		private Point3? ProcessCaveSpawnPoint(Point3 spawnPoint)
		{
			int x = spawnPoint.X;
			int num = Math.Clamp(spawnPoint.Y, 5, 120);
			int z = spawnPoint.Z;

			TerrainChunk chunkAtCell = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunkAtCell == null || chunkAtCell.State <= TerrainChunkState.InvalidPropagatedLight)
				return null;

			for (int i = 0; i < 30; i++)
			{
				Point3 pointUp = new Point3(x, num + i, z);
				Point3? cavePoint = TestCaveSpawnPoint(pointUp);
				if (cavePoint.HasValue)
				{
					return cavePoint;
				}

				Point3 pointDown = new Point3(x, num - i, z);
				if (num - i >= 5)
				{
					cavePoint = TestCaveSpawnPoint(pointDown);
					if (cavePoint.HasValue)
					{
						return cavePoint;
					}
				}
			}
			return null;
		}

		private Point3? TestCaveSpawnPoint(Point3 spawnPoint)
		{
			int x = spawnPoint.X;
			int y = spawnPoint.Y;
			int z = spawnPoint.Z;

			if (y <= 4 || y >= 119)
				return null;

			int cellBelow = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
			int cellCurrent = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
			int cellAbove = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

			Block blockBelow = BlocksManager.Blocks[Terrain.ExtractContents(cellBelow)];
			Block blockCurrent = BlocksManager.Blocks[Terrain.ExtractContents(cellCurrent)];
			Block blockAbove = BlocksManager.Blocks[Terrain.ExtractContents(cellAbove)];

			if (blockBelow is WaterBlock || blockBelow is MagmaBlock)
				return null;
			if (blockCurrent is WaterBlock || blockCurrent is MagmaBlock)
				return null;
			if (blockAbove is WaterBlock || blockAbove is MagmaBlock)
				return null;

			int belowContents = Terrain.ExtractContents(cellBelow);
			if (belowContents != 3 && belowContents != 67 && belowContents != 4 && belowContents != 66 && belowContents != 2 && belowContents != 7)
				return null;

			if (!blockBelow.IsCollidable_(cellBelow))
				return null;

			if (blockCurrent.IsCollidable_(cellCurrent))
				return null;

			if (blockAbove.IsCollidable_(cellAbove))
				return null;

			int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
			if (y >= topHeight - 2)
				return null;

			return spawnPoint;
		}

		private Point3? GetRandomCaveChunkSpawnPoint(SpawnChunk chunk)
		{
			for (int i = 0; i < 5; i++)
			{
				int x = 16 * chunk.Point.X + m_random.Int(0, 15);
				int z = 16 * chunk.Point.Y + m_random.Int(0, 15);

				int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(x, z);

				// La araña DEBE estar bajo tierra. Máximo 50 bloques de profundidad desde la superficie.
				int maxY = Math.Max(4, topHeight - 3);
				int minY = Math.Max(3, topHeight - 50);

				if (maxY <= minY)
					continue;

				int y = m_random.Int(minY, maxY);

				Point3? result = ProcessCaveSpiderSpawnPoint(new Point3(x, y, z), topHeight);
				if (result.HasValue)
				{
					return result;
				}
			}
			return null;
		}

		private Point3? GetRandomSurfaceChunkSpawnPoint(SpawnChunk chunk)
		{
			for (int i = 0; i < 5; i++)
			{
				int x = 16 * chunk.Point.X + m_random.Int(0, 15);
				int z = 16 * chunk.Point.Y + m_random.Int(0, 15);

				int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(x, z);

				// Empezar en la superficie exacta o 1-2 bloques arriba
				for (int offset = 0; offset <= 2; offset++)
				{
					int y = topHeight + offset;
					if (y > 0 && y < 255 && TestSurfaceSpiderSpawnPoint(new Point3(x, y, z)))
					{
						return new Point3(x, y, z);
					}
				}
			}
			return null;
		}

		private Point3? ProcessCaveSpiderSpawnPoint(Point3 spawnPoint, int surfaceHeight)
		{
			int x = spawnPoint.X;
			int num = Math.Clamp(spawnPoint.Y, 1, 254);
			int z = spawnPoint.Z;

			TerrainChunk chunkAtCell = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunkAtCell == null || chunkAtCell.State <= TerrainChunkState.InvalidPropagatedLight)
				return null;

			for (int i = 0; i < 30; i++)
			{
				// Buscar arriba, PERO limitar estrictamente bajo la superficie
				int yUp = Math.Min(num + i, surfaceHeight - 1);
				if (yUp >= num)
				{
					Point3 pointUp = new Point3(x, yUp, z);
					if (TestCaveSpiderSpawnPoint(pointUp))
					{
						return pointUp;
					}
				}

				// Buscar abajo
				Point3 pointDown = new Point3(x, num - i, z);
				if (TestCaveSpiderSpawnPoint(pointDown))
				{
					return pointDown;
				}
			}
			return null;
		}

		private bool TestCaveSpiderSpawnPoint(Point3 spawnPoint)
		{
			int x = spawnPoint.X;
			int y = spawnPoint.Y;
			int z = spawnPoint.Z;

			if (y <= 3 || y >= 253)
				return false;

			// VERIFICACIÓN CRÍTICA: No debe estar en la superficie
			int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
			if (y >= topHeight)
				return false;

			int cellBelow = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
			int cellCurrent = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
			int cellAbove = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

			Block blockBelow = BlocksManager.Blocks[Terrain.ExtractContents(cellBelow)];
			Block blockCurrent = BlocksManager.Blocks[Terrain.ExtractContents(cellCurrent)];
			Block blockAbove = BlocksManager.Blocks[Terrain.ExtractContents(cellAbove)];

			// No spawnear en agua ni magma
			if (blockBelow is WaterBlock || blockBelow is MagmaBlock) return false;
			if (blockCurrent is WaterBlock || blockCurrent is MagmaBlock) return false;
			if (blockAbove is WaterBlock || blockAbove is MagmaBlock) return false;

			// El piso DEBE ser un bloque de cueva (piedra, tierra, grava, etc.)
			int belowContents = Terrain.ExtractContents(cellBelow);
			if (!IsCaveFloorBlock(belowContents))
				return false;

			// Sólido abajo, vacío actual y arriba
			return blockBelow.IsCollidable_(cellBelow)
				&& !blockCurrent.IsCollidable_(cellCurrent)
				&& !blockAbove.IsCollidable_(cellAbove);
		}

		private bool TestSurfaceSpiderSpawnPoint(Point3 spawnPoint)
		{
			int x = spawnPoint.X;
			int y = spawnPoint.Y;
			int z = spawnPoint.Z;

			if (y <= 3 || y >= 253)
				return false;

			int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
			if (y > topHeight + 1 || y < topHeight - 1)
				return false;

			int cellBelow = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
			int cellCurrent = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
			int cellAbove = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

			Block blockBelow = BlocksManager.Blocks[Terrain.ExtractContents(cellBelow)];
			Block blockCurrent = BlocksManager.Blocks[Terrain.ExtractContents(cellCurrent)];
			Block blockAbove = BlocksManager.Blocks[Terrain.ExtractContents(cellAbove)];

			// No agua ni magma
			if (blockBelow is WaterBlock || blockBelow is MagmaBlock) return false;
			if (blockCurrent is WaterBlock || blockCurrent is MagmaBlock) return false;
			if (blockAbove is WaterBlock || blockAbove is MagmaBlock) return false;

			// El piso debe ser bloque de superficie permitido
			int belowContents = Terrain.ExtractContents(cellBelow);
			if (!m_allowedBlockIndices.Contains(belowContents))
				return false;

			// El bloque superior NO debe ser hoja
			if (blockAbove is LeavesBlock)
				return false;

			// Sólido abajo, vacío actual y arriba
			if (!blockBelow.IsCollidable_(cellBelow)) return false;
			if (blockCurrent.IsCollidable_(cellCurrent)) return false;
			if (blockAbove.IsCollidable_(cellAbove)) return false;

			// Verificar exposición al cielo (diferencia de luz del cielo)
			int cellLight = m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
			if (m_subsystemSky.SkyLightValue - cellLight > 3)
				return false;

			return true;
		}

		private bool TestCaveSpiderSpawnPoint(Point3 spawnPoint, int surfaceHeight)
		{
			int x = spawnPoint.X;
			int y = spawnPoint.Y;
			int z = spawnPoint.Z;

			// VERIFICACIÓN CRÍTICA: Debe estar BAJO TIERRA
			if (y >= surfaceHeight)
				return false;

			if (y <= 3 || y >= 253)
				return false;

			int cellValueFast = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
			int cellValueFast2 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
			int cellValueFast3 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);

			Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
			Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
			Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];

			// No spawnear en agua o magma
			if (block is WaterBlock || block is MagmaBlock)
				return false;
			if (block2 is WaterBlock || block2 is MagmaBlock)
				return false;
			if (block3 is WaterBlock || block3 is MagmaBlock)
				return false;

			// Bloques válidos para piso de CUEVA (diferente a superficie)
			// Stone=2, Dirt=3, Gravel=4, GravelBlock=66, Sandstone=67, Clay=7
			int belowContents = Terrain.ExtractContents(cellValueFast);
			if (belowContents != 2 && belowContents != 3 && belowContents != 4 &&
				belowContents != 66 && belowContents != 67 && belowContents != 7)
			{
				return false;
			}

			// Sólido abajo, vacío actual y arriba
			return block.IsCollidable_(cellValueFast)
				&& !block2.IsCollidable_(cellValueFast2)
				&& !block3.IsCollidable_(cellValueFast3);
		}

		private bool IsCaveFloorBlock(int blockContents)
		{
			// Stone=2, Dirt=3, Gravel=4, GravelBlock=66, Sandstone=67, Clay=7
			return blockContents == 2 || blockContents == 3 || blockContents == 4 ||
				   blockContents == 66 || blockContents == 67 || blockContents == 7;
		}
		// ===== FIN SPAWN CONSTANTE INFECTED SPIDER =====

		/// <summary>
		/// Spawnea los jefes definidos en la ola actual a medianoche.
		/// Solo se usa para oleadas normales (no la final).
		/// </summary>
		private void SpawnMidnightBosses()
		{
			if (m_currentWaveEntries == null || m_currentWaveEntries.Count == 0)
				return;

			var bossEntries = m_currentWaveEntries
				.Where(e => BossTemplates.Contains(e.TemplateName))
				.ToList();

			if (bossEntries.Count == 0)
				return;

			int bossesSpawned = 0;

			foreach (var bossEntry in bossEntries)
			{
				Vector3 spawnPos = GetBossSpawnPoint(40f, 70f);
				if (spawnPos == Vector3.Zero)
				{
					spawnPos = GetAlternativeBossSpawnPoint(20f, 100f);
				}

				if (spawnPos == Vector3.Zero)
					continue;

				if (!CanSpawnCreature(bossEntry.TemplateName, spawnPos))
					continue;

				Entity boss = m_subsystemCreatureSpawn.SpawnCreature(bossEntry.TemplateName, spawnPos, false);
				if (boss != null)
				{
					bossesSpawned++;
					string messageKey = GetBossMessageKey(bossEntry.TemplateName);
					SendMessageToAllPlayers("ZombiesSpawn", messageKey, new Color(255, 50, 50));
				}
			}

			if (bossesSpawned > 0 && m_subsystemAudio != null)
			{
				m_subsystemAudio.PlaySound("Audio/UI/Tank Warning Sound", 1f, 0f, 0f, 0f);
			}
		}

		// Método para reiniciar las oleadas
		public void ResetWaves()
		{
			m_currentWave = 1;
			SetCurrentWave(1);
			m_hasSpawnedBossThisNight = false;
			m_bossBattleActive = false;
			m_bossSpawnDelayed = false;
			m_bossQueue.Clear();
			m_currentBossEntity = null;
			// No reiniciamos m_hasShownUnlockMessage ni m_letterWarSpawned para que no se repitan
		}

		public void SetAcceptedImpossibleChallenge(bool accepted)
		{
			m_hasAcceptedImpossibleChallenge = accepted;
		}
	}
}
