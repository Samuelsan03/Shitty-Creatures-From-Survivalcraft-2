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

		// Datos de oleadas
		private Dictionary<int, List<WaveEntry>> m_waves = new Dictionary<int, List<WaveEntry>>();
		private int m_currentWave = 1;
		private List<WaveEntry> m_currentWaveEntries;

		// Control de spawn (noche verde)
		private float m_spawnTimer;
		private float m_spawnInterval = 2f;
		private const int MaxCreaturesPerArea = 60;
		private const int MaxGlobalCreatures = 80;
		private const int MaxSpawnsPerFrame = 3;
		private const int GroupSpawnCount = 2;

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

		private static readonly HashSet<string> ColdOnlyTemplates = new HashSet<string>
		{
			"InfectedFreezer",
			"BoomerFrozen",
			"FrozenGhost",
			"FrozenGhostBoomer",
			"FrozenTankGhost",
			"FrozenTank"
		};

		// CAMBIADO: Lista de bloques PERMITIDOS para spawnear
		private HashSet<int> m_allowedBlockIndices = new HashSet<int>();

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

			if (m_subsystemSpawn != null)
			{
				m_subsystemSpawn.SpawningChunk += OnSpawningChunk;
			}

			InitializeAllowedBlockIndices();
			LoadWavesFromResources();
			m_currentWave = valuesDictionary.GetValue<int>("CurrentWave", 1);
			SetCurrentWave(m_currentWave);
			m_wasGreenNightActive = m_subsystemGreenNightSky.IsGreenNightActive;
			m_constantSpawnCooldown = ConstantSpawnCooldownTime;
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

		// CAMBIADO: Ahora inicializa bloques PERMITIDOS
		private void InitializeAllowedBlockIndices()
		{
			// Lista de bloques DONDE SE PUEDEN SPAWNEAR las criaturas
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
			m_labelInitialized = attached;
		}

		private void UpdateCountdownLabel()
		{
			if (m_countdownLabel == null)
			{
				CreateCountdownLabel();
				return;
			}

			if (!m_labelInitialized || m_countdownLabel.ParentWidget == null)
			{
				AttachLabelToPlayers();
			}

			if (!m_labelInitialized)
				return;

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
			int waveBefore = m_currentWave;
			int maxWave = m_waves.Keys.Max();

			AdvanceToNextWave();

			if (waveBefore == maxWave && !m_hasShownUnlockMessage)
			{
				m_hasShownUnlockMessage = true;

				string largeMessage = LanguageControl.Get("UnlockedItems", "Unlocked");
				if (string.IsNullOrEmpty(largeMessage))
					largeMessage = "Remote Control unlocked!";

				string smallMessage = LanguageControl.Get("UnlockedItems", "UnlockedInfo");
				if (string.IsNullOrEmpty(smallMessage))
					smallMessage = "You can now craft the Remote Control to manage the Green Nights.";

				foreach (var player in m_subsystemPlayers.ComponentPlayers)
				{
					player.ComponentGui.DisplayLargeMessage(largeMessage, smallMessage, 5f, 0f);
				}

				if (!m_letterWarSpawned && m_subsystemTime != null)
				{
					m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + 5.0, () =>
					{
						if (!m_letterWarSpawned)
							ShowLetterAnnouncementAndSpawn();
					});
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

				if (groundY <= 0 || groundY >= 255) continue;

				// CAMBIADO: Verificar que el bloque del suelo esté en la lista PERMITIDA
				int cellGround = m_subsystemTerrain.Terrain.GetCellValue(x, groundY, z);
				int contentsGround = Terrain.ExtractContents(cellGround);
				if (!m_allowedBlockIndices.Contains(contentsGround)) continue;

				int data = GetChestFacingData(playerPos, new Vector3(x + 0.5f, groundY + 1, z + 0.5f));
				int chestValue = Terrain.MakeBlockValue(ChestBlock.Index, 0, data);
				m_subsystemTerrain.ChangeCell(x, groundY + 1, z, chestValue);

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

				m_subsystemTerrain.ChangeCell(x, groundY + 1, z, 0);
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
		}

		public void Update(float dt)
		{
			UpdateCountdownLabel();

			bool isGreenNightActive = m_subsystemGreenNightSky.IsGreenNightActive;
			int maxWave = m_waves.Keys.Max();

			bool isNormalNight = IsNormalNight();

			if (ShittyCreaturesSettingsManager.SkeletonSpawnEnabled)
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
		}

		// ===== SPAWN NORMAL DE ESQUELETOS (100% de noche) =====
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

			// CAMBIADO: Verificar que el bloque esté en la lista PERMITIDA
			if (!m_allowedBlockIndices.Contains(belowContents))
				return 0f;

			int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(x, z);
			if (y > topHeight + 2)
				return 0f;

			return SkeletonNormalSuitability;
		}
		// ===== FIN SPAWN NORMAL =====

		// ===== SPAWN CONSTANTE DE ESQUELETOS (50% de noche) =====
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

			// CAMBIADO: Verificar que el bloque esté en la lista PERMITIDA
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

			return m_subsystemSky.SkyLightIntensity < NightLightThreshold;
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
						// CAMBIADO: Verificar que el bloque esté en la lista PERMITIDA
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
						// CAMBIADO: Verificar que el bloque esté en la lista PERMITIDA
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
						// CAMBIADO: Verificar que el bloque esté en la lista PERMITIDA
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

					// Los voladores aparecen en el aire, no verifican bloques del suelo
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
						// CAMBIADO: Verificar que el bloque esté en la lista PERMITIDA
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
						// CAMBIADO: Verificar que el bloque esté en la lista PERMITIDA
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

						// Los voladores aparecen en el aire, no verifican bloques del suelo
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

					// Los voladores aparecen en el aire, no verifican bloques del suelo
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
	}
}
