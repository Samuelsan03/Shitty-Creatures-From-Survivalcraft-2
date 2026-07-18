using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using Engine.Media;
using GameEntitySystem;
using TemplatesDatabase;
using XmlUtilities;

namespace Game
{
	public class ShittyCreaturesModLoader : ModLoader
	{
		// ---------------------------------------------------------------------------------
		// Campos estáticos (heredados de los distintos ModLoaders originales)
		// ---------------------------------------------------------------------------------

		private double m_lastCombatStatsUpdateTime;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;

		private double m_lastMountBlockMessageTime = -10.0;

		private Dictionary<ComponentPlayer, bool> m_previousToggleMountState = new Dictionary<ComponentPlayer, bool>();
		private Dictionary<ComponentPlayer, double> m_lastMountAttemptMessageTime = new Dictionary<ComponentPlayer, double>();

		// Añadir campo para almacenar los botones creados (opcional, solo para referencia)
		private Dictionary<ComponentPlayer, ButtonWidget> m_achievementButtons = new Dictionary<ComponentPlayer, ButtonWidget>();

		// Diccionarios para controlar el ritmo de golpes
		private Dictionary<ComponentPlayer, double> m_lastHitGameTime = new Dictionary<ComponentPlayer, double>();
		private Dictionary<ComponentPlayer, ComponentBody> m_lastHitTarget = new Dictionary<ComponentPlayer, ComponentBody>();
		private Dictionary<ComponentPlayer, bool> m_fastHitMode = new Dictionary<ComponentPlayer, bool>();

		private Dictionary<ComponentCreature, BloodParticleSystem> m_bleedingSystems = new Dictionary<ComponentCreature, BloodParticleSystem>();
		private SubsystemModelsRenderer m_healthBarModelsRenderer;
		private SubsystemCreatureSpawn m_healthBarCreatureSpawn;
		private SubsystemPlayers m_healthBarPlayers;
		public float HealthBarVisibilityRadius = 50f;

		private static Dictionary<string, (float AttackPower, float WalkSpeed, float FlySpeed)> m_baseStatsCache = new Dictionary<string, (float, float, float)>();

		// Campos para celebración de logros
		private bool m_celebrationActive = false;
		private Dictionary<ComponentCreature, bool> m_originalSuppressedState = new Dictionary<ComponentCreature, bool>();
		// NUEVO: Guardar el estado de invulnerabilidad original
		private Dictionary<ComponentHealth, bool> m_originalInvulnerableState = new Dictionary<ComponentHealth, bool>();

		// ShittyModLoader (original)
		static FieldInfo m_cachesField;

		private static readonly HashSet<int> m_banditArmorClothes = new HashSet<int>
{
	2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 18, 19, 20, 21, 22, 23, 27, 28, 29, 31, 33, 34, 35, 36
};

		// Lista de bandidos que se verán afectados por la dificultad
		private static readonly HashSet<string> m_banditTemplates = new HashSet<string>
{
	"Bandit1", "Bandit2", "Bandit3", "Bandit4", "Bandit5",
	"Bandit6", "Bandit8", "Bandit9", "Bandit10",
    "Bandit13", "Bandit14", "Bandit15", "Bandit16", "LaBandida"
};

		// Índices de ropa sin protección (para dificultades bajas)
		private static readonly HashSet<int> m_lowTierClothes = new HashSet<int>
{
	0, 1, 13, 14, 15, 16, 17, 24, 25, 26, 30, 32, 37
};

		// Índices de armaduras (para dificultades altas)
		private static readonly HashSet<int> m_highTierClothes = new HashSet<int>
{
	2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 18, 19, 20, 21, 22, 23, 27, 28, 29, 31, 33, 34, 35, 36
};

		private static readonly HashSet<string> m_infectedWithClothes = new HashSet<string>
{
	"InfectedNormal1",
	"InfectedNormal2",
	"GhostNormal",
	"GhostFast",
	"HumanoidSkeleton",
	"InfectedFreezer",
	"FrozenGhost"
};

		private static readonly HashSet<string> s_bossNames = new HashSet<string>
{
	"Tank1", "Tank2", "Tank3",
	"TankGhost1", "TankGhost2", "TankGhost3",
	"MachineGunInfected", "FlyingInfectedBoss",
	"FrozenTank", "FrozenTankGhost",
	"LaBandida",
	"Bandit1", "Bandit2", "Bandit3", "Bandit4", "Bandit5",
	"Bandit6", "Bandit8", "Bandit9", "Bandit10",
	"Bandit13", "Bandit14", "Bandit15", "Bandit16"
};

		private static Random _random;
		private static int _lastSongIndex = -1;

		// RemoteControlModLoader
		private static double s_lastProhibitionMessageTime = -10.0;
		private static bool s_isMatchingRemoteControlRecipe = false;

		// NewPanoramaModLoader
		private static bool s_panoramaHookRegistered = false;

		// Coordenadas HUD
		private Dictionary<ComponentPlayer, LabelWidget> m_coordinateLabels = new Dictionary<ComponentPlayer, LabelWidget>();
		private bool m_greenNightHooksRegistered = false;

		// ---------------------------------------------------------------------------------
		// Inicialización del ModLoader (registro de hooks)
		// ---------------------------------------------------------------------------------
		public override void __ModInitialize()
		{
			// Hooks originales de ShittyModLoader
			ModsManager.RegisterHook("OnMainMenuScreenCreated", this);
			ModsManager.RegisterHook("BeforeWidgetUpdate", this);

			// ChaseMusicModLoader
			ModsManager.RegisterHook("OnSettingsScreenCreated", this);
			ShittyCreaturesSettingsManager.Load();

			// CreatureInventoryModLoader (prioridad 0)
			ModsManager.RegisterHook("OnPlayerInputInteract", this);

			// GreenNightSkyModLoader
			ModsManager.RegisterHook("ChangeSkyColor", this);
			ModsManager.RegisterHook("OnVitalStatsUpdateSleep", this);

			// MusicModLoader
			ModsManager.RegisterHook("MenuPlayMusic", this);
			ModsManager.RegisterHook("PlayInGameMusic", this);
			_random = new Random();

			// RemoteControlModLoader
			RegisterRemoteControlBlock();
			ModsManager.RegisterHook("MatchRecipe", this);

			// Hook para cuando el jugador golpea cuerpo a cuerpo (prioridad alta)
			ModsManager.RegisterHook("OnPlayerInputHit", this);
			// Hook para cuando el jugador recibe daño (se usa evento Injured, no hook directo)
			// La suscripción se realiza en OnPlayerInputHit cuando el jugador está disponible

			// Hook para detectar ataques al jugador incluso en modo Creativo
			ModsManager.RegisterHook("OnMinerHit", this);

			ModsManager.RegisterHook("ManageCameras", this);

			ModsManager.RegisterHook("OnPlayerSpawned", this);

			ModsManager.RegisterHook("OnPlayerDead", this);
			ModsManager.RegisterHook("OnProjectLoaded", this);
			ModsManager.RegisterHook("OnCreatureDied", this);
			ModsManager.RegisterHook("OnCreatureDying", this);
			ModsManager.RegisterHook("ChangeVisualEffectOnInjury", this);
			ModsManager.RegisterHook("OnProjectileRaycastBody", this);
			ModsManager.RegisterHook("OnProjectileHitBody", this);
			ModsManager.RegisterHook("SetHitInterval", this);
			ModsManager.RegisterHook("OnChaseBehaviorAttacked", this);  // Prioridad alta para cancelar

			// Bloquear montura zombi para jugadores
			ModsManager.RegisterHook("ScoreMount", this);
			ModsManager.RegisterHook("OnEatPickable", this);
			ModsManager.RegisterHook("ProcessAttackment", this);
			ModsManager.RegisterHook("OnWidgetConstruct", this);
			ModsManager.RegisterHook("OnVitalStatsUpdateTemperature", this);
			// Reemplazar overlay de captura de pantalla
			ReplaceScreenCaptureOverlay();
		}

		// ---------------------------------------------------------------------------------
		// Métodos auxiliares privados
		// ---------------------------------------------------------------------------------

		public override void OnProjectLoaded(Project project)
		{
			// Limpiar caché de stats base al cargar un mundo nuevo
			m_baseStatsCache.Clear();

			// ========== LIMPIAR ESTADO DE CELEBRACIÓN DE MUNDOS ANTERIORES ==========
			if (m_celebrationActive)
			{
				foreach (var kvp in m_originalSuppressedState)
				{
					if (kvp.Key != null)
					{
						try
						{
							var chaseBehavior = kvp.Key.Entity?.FindComponent<ComponentChaseBehavior>();
							if (chaseBehavior != null)
							{
								chaseBehavior.Suppressed = kvp.Value;
							}
						}
						catch (Exception ex)
						{
							Log.Warning($"[ShittyCreatures] Error restaurando estado de criatura: {ex.Message}");
						}
					}
				}
				m_originalSuppressedState.Clear();
				m_originalInvulnerableState.Clear();
				m_celebrationActive = false;
			}
			// ========== FIN LIMPIEZA ==========

			if (!m_greenNightHooksRegistered)
			{
				m_greenNightHooksRegistered = true;
				SubsystemGreenNightSky greenNight = project.FindSubsystem<SubsystemGreenNightSky>(true);
				if (greenNight != null)
				{
					greenNight.GreenNightStarted += () => CancelGreenNightChaseDelay(project);
				}
			}
			UpdateCoordinateLabelsPosition(project);

			m_healthBarModelsRenderer = project.FindSubsystem<SubsystemModelsRenderer>(true);
			m_healthBarCreatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_healthBarPlayers = project.FindSubsystem<SubsystemPlayers>(true);
			HealthBarDrawable healthBarDrawable = new HealthBarDrawable(this);
			project.FindSubsystem<SubsystemDrawing>(true).AddDrawable(healthBarDrawable);

			var playersSubsystem = project.FindSubsystem<SubsystemPlayers>(true);
			if (playersSubsystem != null)
			{
				foreach (ComponentPlayer player in playersSubsystem.ComponentPlayers)
				{
					SubscribeToPlayerInjured(player);
				}
			}

			// ===== SUSCRIBIRSE A ENTIDADES NUEVAS PARA APLICAR DIFICULTAD =====
			// 1. Suscribirse al evento estático de Project (cuando se añade una entidad al proyecto)
			Project.EntityAdded += Project_EntityAdded;

			// 2. Suscribirse a las entidades ya existentes
			foreach (var entity in project.Entities)
			{
				entity.EntityAdded += Entity_EntityAdded;
			}
			// ===== FIN SUSCRIPCIÓN =====

			AchievementsManager.Initialize(project);
			AddAchievementButtonToPlayers(project);
			EnforceBlockBreakingByDifficulty(project);
			EnforceCombatStatsByDifficulty(project);

			m_subsystemGreenNightSky = project.FindSubsystem<SubsystemGreenNightSky>(true);

			AchievementsManager.OnCelebrationStarted += OnCelebrationStarted;
			AchievementsManager.OnCelebrationEnded += OnCelebrationEnded;

			var greenNightSky = project.FindSubsystem<SubsystemGreenNightSky>(true);
			var zombiesSpawn = project.FindSubsystem<SubsystemZombiesSpawn>(true);
			if (greenNightSky != null && zombiesSpawn != null)
			{
				if (greenNightSky.DifficultyMode == DifficultyMode.Impossible && !zombiesSpawn.HasExtremeCompleted)
				{
					greenNightSky.DifficultyMode = DifficultyMode.Normal;
					ShittyCreaturesModLoader.NotifyDifficultyChanged(greenNightSky);
					zombiesSpawn.ForceUpdateDifficultyLabel();
				}
			}
		}

		private void AddAchievementButtonToPlayers(Project project)
		{
			var playersSubsystem = project.FindSubsystem<SubsystemPlayers>(true);
			if (playersSubsystem == null) return;

			foreach (var player in playersSubsystem.ComponentPlayers)
			{
				AddAchievementButton(player);
			}
		}

		private void AddAchievementButton(ComponentPlayer player)
		{
			if (player == null || player.GuiWidget == null) return;
			if (m_achievementButtons.ContainsKey(player)) return;

			var timeOfDayButton = player.GuiWidget.Children.Find<ButtonWidget>("TimeOfDayButton", true);
			if (timeOfDayButton == null) return;

			ContainerWidget parentContainer = timeOfDayButton.ParentWidget as ContainerWidget;
			if (parentContainer == null) return;

			Subtexture logroTexture = ContentManager.Get<Subtexture>("Textures/Gui/logro");
			if (logroTexture == null)
			{
				Log.Warning("[ShittyCreatures] No se pudo cargar la textura 'Textures/Gui/logro'");
				return;
			}

			// Usar BevelledButtonWidget (concreto, no abstracto)
			BevelledButtonWidget iconButton = new BevelledButtonWidget
			{
				Name = "AchievementButton",
				Size = new Vector2(60f, 60f),
				Margin = timeOfDayButton.Margin,
				// Hacer el fondo transparente
				BevelColor = Color.Transparent,
				CenterColor = Color.Transparent,
				Color = Color.White,
				Text = ""  // Sin texto
			};

			// Agregar la imagen como hijo
			RectangleWidget icon = new RectangleWidget
			{
				Size = new Vector2(50f, 50f),
				TextureLinearFilter = true,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				Subtexture = logroTexture,
				OutlineColor = Color.Transparent,
				FillColor = Color.White,
				IsVisible = true,
				TextureAnisotropicFilter = true,
				BlendState = BlendState.NonPremultiplied
			};
			iconButton.Children.Add(icon);

			int index = parentContainer.Children.IndexOf(timeOfDayButton);
			if (index >= 0 && index < parentContainer.Children.Count)
				parentContainer.Children.Insert(index + 1, iconButton);
			else
				parentContainer.Children.Add(iconButton);

			m_achievementButtons[player] = iconButton;
		}

		private void CancelGreenNightChaseDelay(Project project)
		{
			SubsystemCreatureSpawn creatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(true);
			if (creatureSpawn == null)
				return;

			int cancelledCount = 0;
			foreach (ComponentCreature creature in creatureSpawn.Creatures)
			{
				if (creature == null || creature.ComponentHealth.Health <= 0f)
					continue;

				ComponentZombieChaseBehavior zombieChase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
				if (zombieChase == null)
					continue;

				if (!zombieChase.ForceAttackDuringGreenNight)
					continue;

				try
				{
					// Acceder al campo m_targetInRangeTime mediante reflexión
					FieldInfo targetTimeField = typeof(ComponentChaseBehavior).GetField("m_targetInRangeTime",
						BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					if (targetTimeField != null)
					{
						targetTimeField.SetValue(zombieChase, 1f);
					}

					// Acceder a la propiedad TargetInRangeTimeToChase mediante reflexión
					PropertyInfo chaseTimeProp = typeof(ComponentChaseBehavior).GetProperty("TargetInRangeTimeToChase",
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (chaseTimeProp != null && chaseTimeProp.CanWrite)
					{
						chaseTimeProp.SetValue(zombieChase, 0f);
					}

					// Establecer Suppressed a false (es público en ComponentChaseBehavior)
					zombieChase.Suppressed = false;

					// Acceder al StateMachine interno para verificar el estado actual
					FieldInfo stateMachineField = typeof(ComponentChaseBehavior).GetField("m_stateMachine",
						BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					if (stateMachineField != null)
					{
						StateMachine stateMachine = stateMachineField.GetValue(zombieChase) as StateMachine;
						if (stateMachine != null && stateMachine.CurrentState == "Fleeing")
						{
							// Usar reflexión para llamar a TransitionTo en el StateMachine
							MethodInfo transitionMethod = typeof(StateMachine).GetMethod("TransitionTo",
								BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							if (transitionMethod != null)
							{
								transitionMethod.Invoke(stateMachine, new object[] { "LookingForTarget" });
							}
						}
					}

					cancelledCount++;
				}
				catch (Exception ex)
				{
					Log.Warning($"[ShittyCreatures] Error al cancelar delay de caza en criatura: {ex.Message}");
				}
			}

			if (cancelledCount > 0)
			{
				// Opcional: Log.Information($"[ShittyCreatures] Se canceló el delay de caza para {cancelledCount} criaturas durante la noche verde.");
			}
		}

		private void ReplaceScreenCaptureOverlay()
		{
			try
			{
				if (m_cachesField == null)
				{
					m_cachesField = typeof(ContentManager).GetField("Caches",
						BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
					if (m_cachesField == null)
					{
						Log.Error("[ShittyCreatures] No se pudo encontrar ContentManager.Caches");
						return;
					}
				}

				var caches = m_cachesField.GetValue(null) as System.Collections.IDictionary;
				if (caches == null)
				{
					Log.Error("[ShittyCreatures] ContentManager.Caches es null");
					return;
				}

				Texture2D customOverlay = ContentManager.Get<Texture2D>("Textures/Gui/ScreenCaptureOverlay");
				if (customOverlay == null)
				{
					Log.Error("[ShittyCreatures] No se pudo cargar la textura personalizada");
					return;
				}

				string key = "Textures/Gui/ScreenCaptureOverlay";
				if (!caches.Contains(key))
					caches[key] = new List<object>();

				var cacheList = caches[key] as List<object>;
				if (cacheList != null)
				{
					for (int i = cacheList.Count - 1; i >= 0; i--)
						if (cacheList[i] is Texture2D)
							cacheList.RemoveAt(i);
					cacheList.Add(customOverlay);
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[ShittyCreatures] Error al cargar overlay personalizado: {ex.Message}");
			}
		}

		private void RegisterRemoteControlBlock()
		{
			Type remoteControlType = typeof(RemoteControlBlock);
			bool blockExists = false;
			foreach (var block in BlocksManager.Blocks)
				if (block != null && block.GetType() == remoteControlType)
				{
					blockExists = true;
					break;
				}

			if (!blockExists)
			{
				int freeIndex = -1;
				for (int i = 300; i < 1024; i++)
					if (BlocksManager.Blocks[i] == null || BlocksManager.Blocks[i] is AirBlock)
					{
						freeIndex = i;
						break;
					}

				if (freeIndex >= 0)
				{
					RemoteControlBlock block = new RemoteControlBlock();
					BlocksManager.m_blocks[freeIndex] = block;
					block.BlockIndex = freeIndex;
					BlocksManager.BlockNameToIndex["RemoteControlBlock"] = freeIndex;
					BlocksManager.BlockTypeToIndex[remoteControlType] = freeIndex;
				}
			}
		}

		private bool IsHealingItem(int blockIndex)
		{
			int antidoteIndex = BlocksManager.GetBlockIndex<AntidoteBucketBlock>(false, false);
			int teaIndex = BlocksManager.GetBlockIndex<TeaAntifluBucketBlock>(false, false);
			int largeKitIndex = BlocksManager.GetBlockIndex<LargeFirstAidKitBlock>(false, false);
			int mediumKitIndex = BlocksManager.GetBlockIndex<MediumFirstAidKitBlock>(false, false);

			return blockIndex == antidoteIndex ||
				   blockIndex == teaIndex ||
				   blockIndex == largeKitIndex ||
				   blockIndex == mediumKitIndex;
		}

		// ---------------------------------------------------------------------------------
		// Hook: OnMainMenuScreenCreated (ShittyModLoader + ShittyButtonModLoader fusionado)
		// ---------------------------------------------------------------------------------
		public override void OnMainMenuScreenCreated(MainMenuScreen mainMenuScreen, StackPanelWidget leftBottomBar, StackPanelWidget rightBottomBar)
		{
			// ===== NUEVO BOTÓN BESTIARIO ZOMBI (verde) =====
			BevelledButtonWidget zombieBestiaryButton = new BevelledButtonWidget
			{
				Name = "ZombieBestiaryButton",
				Size = new Vector2(60f, 60f),
				BevelColor = new Color(0, 128, 0),   // verde oscuro
				CenterColor = new Color(0, 128, 0)   // mismo color para que el botón sea uniforme
			};

			RectangleWidget iconZombie = new RectangleWidget
			{
				Size = new Vector2(28f, 28f),
				TextureLinearFilter = true,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/zombi icono"),
				OutlineColor = Color.Transparent,
				FillColor = Color.White,
				IsVisible = true,
				TextureAnisotropicFilter = true,
				BlendState = BlendState.NonPremultiplied
			};
			zombieBestiaryButton.Children.Add(iconZombie);

			// Insertar al final de la barra derecha (o donde prefieras)
			rightBottomBar.Children.Add(zombieBestiaryButton);

			// ----- Nuevo: enlace de TikTok (encima del copyright) -----
			var bottomInfos = mainMenuScreen.Children.Find<StackPanelWidget>("BottomInfos", true);
			if (bottomInfos != null)
			{

				// Línea de copyright del mod (primero, índice 0)
				if (bottomInfos.Children.Find<StackPanelWidget>("ModCopyrightRow", false) == null)
				{
					var modCopyrightRow = new StackPanelWidget
					{
						Name = "ModCopyrightRow",
						Direction = LayoutDirection.Horizontal,
						HorizontalAlignment = WidgetAlignment.Center,
						Margin = new Vector2(0, 2)
					};
					var modCopyrightLabel = new LabelWidget
					{
						Text = "© 2025-2026 Shitty Creatures",
						FontScale = 0.7f,
						DropShadow = true,
						Name = "ModCopyrightLabel"
					};
					modCopyrightRow.Children.Add(modCopyrightLabel);
					bottomInfos.Children.Insert(0, modCopyrightRow);
				}

				// Solo añadir si no existe ya
				if (bottomInfos.Children.Find<StackPanelWidget>("TikTokLinkRow", false) == null)
				{
					// Fila horizontal para el texto TikTok + link
					var tikTokRow = new StackPanelWidget
					{
						Name = "TikTokLinkRow",
						Direction = LayoutDirection.Horizontal,
						HorizontalAlignment = WidgetAlignment.Center,
						Margin = new Vector2(0, 2) // pequeño margen vertical
					};

					// Nombre de usuario fijo, sin traducir
					LinkWidget tikTokLink = new LinkWidget
					{
						Text = " @athormi",
						Color = new Color(255, 181, 31),
						FontScale = 0.7f,
						VerticalAlignment = WidgetAlignment.Center,
						Url = "https://www.tiktok.com/@athormi",
						DropShadow = true
					};

					// Añadir etiqueta y link a la fila
					tikTokRow.Children.Add(new LabelWidget
					{
						Text = "TikTok:",
						Color = Color.DarkRed,
						VerticalAlignment = WidgetAlignment.Center,
						FontScale = 0.7f,
						DropShadow = true
					});
					tikTokRow.Children.Add(tikTokLink);

					// Insertar al principio (índice 0), encima del copyright
					int insertIndex = bottomInfos.Children.Find<StackPanelWidget>("ModCopyrightRow", false) != null ? 1 : 0;
					if (insertIndex <= bottomInfos.Children.Count)
						bottomInfos.Children.Insert(insertIndex, tikTokRow);
					else
						bottomInfos.Children.Add(tikTokRow);
				}
			}

			// Ajustar logo principal
			RectangleWidget logo = mainMenuScreen.Children.Find<RectangleWidget>("Logo", true);
			if (logo != null)
			{
				logo.Size = new Vector2(336f, 128f);
				logo.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/Logo");
				logo.HorizontalAlignment = WidgetAlignment.Center;
				logo.TextureLinearFilter = true;
				logo.Margin = new Vector2(0f, 5f);
			}

			// Añadir etiqueta de versión del mod bajo el logo, imitando el estilo de la versión original
			StackPanelWidget topArea = mainMenuScreen.Children.Find<StackPanelWidget>("TopArea", true);
			if (topArea != null)
			{
				LabelWidget versionLabel = topArea.Children.Find<LabelWidget>("Version", false);
				if (versionLabel != null)
				{
					LabelWidget modVersionLabel = new LabelWidget
					{
						Text = "Shitty Creatures v1.0.6",
						FontScale = versionLabel.FontScale,  // 0.5
						HorizontalAlignment = versionLabel.HorizontalAlignment, // Center
						Color = Color.Red,      // rojo para el mod
						DropShadow = versionLabel.DropShadow,
						Name = "ModVersionLabel"
					};
					int index = topArea.Children.IndexOf(versionLabel);
					if (index >= 0 && index + 1 <= topArea.Children.Count)
						topArea.Children.Insert(index + 1, modVersionLabel);
					else
						topArea.Children.Add(modVersionLabel);
				}
			}

			// Botones centrales "Acerca del Mod" y "Salir"
			StackPanelWidget centerButtons = mainMenuScreen.Children.Find<StackPanelWidget>("CenterButtons", true);
			if (centerButtons != null && centerButtons.Children.Find<StackPanelWidget>("ShittyButtonRow", false) == null)
			{
				StackPanelWidget buttonRow = new StackPanelWidget
				{
					Name = "ShittyButtonRow",
					Direction = LayoutDirection.Horizontal,
					HorizontalAlignment = WidgetAlignment.Center,
					Margin = new Vector2(0f, 0f)   // Sin margen vertical extra (como las filas originales)
				};

				string aboutButtonText = LanguageControl.Get(new string[] { "ShittyCreaturesAbout", "AboutButton" });
				BevelledButtonWidget aboutButton = new BevelledButtonWidget
				{
					Name = "ShittyAboutButton",
					Text = aboutButtonText,
					Size = new Vector2(310f, 60f),
					BevelColor = new Color(128, 0, 128),
					CenterColor = new Color(128, 0, 128),
					Margin = new Vector2(0f, 0f)   // Sin separación horizontal entre botones
				};

				string exitButtonText = LanguageControl.Get(new string[] { "ShittyCreaturesAbout", "ExitButton" });
				BevelledButtonWidget exitButton = new BevelledButtonWidget
				{
					Name = "ShittyExitButton",
					Text = exitButtonText,
					Size = new Vector2(310f, 60f),
					BevelColor = new Color(181, 172, 154),  // Color estándar de bevel
					CenterColor = new Color(181, 172, 154),  // Color estándar del centro
					Margin = new Vector2(0f, 0f)
				};

				buttonRow.Children.Add(aboutButton);
				buttonRow.Children.Add(exitButton);
				centerButtons.Children.Add(buttonRow);
			}

			// ---------- NUEVO: Botón de Agradecimientos en la barra izquierda ----------
			BevelledButtonWidget thanksButton = leftBottomBar.Children.Find<BevelledButtonWidget>("SpecialThanksButton", false);
			if (thanksButton == null)
			{
				thanksButton = new BevelledButtonWidget
				{
					Name = "SpecialThanksButton",
					Size = new Vector2(60f, 60f)
				};
				RectangleWidget icon = new RectangleWidget
				{
					Size = new Vector2(28f, 28f),
					TextureLinearFilter = true,
					HorizontalAlignment = WidgetAlignment.Center,
					VerticalAlignment = WidgetAlignment.Center,
					Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/Xros_heart_emblem"),
					OutlineColor = new Color(0, 0, 0, 0),
					FillColor = Color.White,
					IsVisible = true,
					TextureAnisotropicFilter = true,
					BlendState = BlendState.NonPremultiplied
				};
				thanksButton.Children.Add(icon);
				leftBottomBar.Children.Insert(0, thanksButton); // Inserta al principio para que quede arriba del todo
			}

			// ===== NUEVO BOTÓN PERSONALIZADO (encima del Veemon) =====
			BevelledButtonWidget newButton = new BevelledButtonWidget
			{
				Name = "CustomShittyButton",
				Size = new Vector2(60f, 60f),
				// El fondo se pintará con efecto arcoíris en BeforeWidgetUpdate
				BevelColor = Color.White,
				CenterColor = Color.White
			};

			RectangleWidget newIcon = new RectangleWidget
			{
				Size = new Vector2(28f, 28f),
				TextureLinearFilter = true,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/veemon te pela la verga"),
				OutlineColor = new Color(0, 0, 0, 0),
				FillColor = Color.White,
				IsVisible = true,
				TextureAnisotropicFilter = true,
				BlendState = BlendState.NonPremultiplied
			};
			newButton.Children.Add(newIcon);

			// Insertar al PRINCIPIO de la barra (índice 0) para que quede encima del Veemon
			rightBottomBar.Children.Insert(0, newButton);

			// Botón Veemon en la barra inferior derecha (comportamiento original)
			BevelledButtonWidget existing = rightBottomBar.Children.Find<BevelledButtonWidget>("ShittyButton", false);
			if (existing == null)
			{
				BevelledButtonWidget button = new BevelledButtonWidget
				{
					Name = "ShittyButton",
					Size = new Vector2(60f, 60f)
				};
				RectangleWidget icon = new RectangleWidget
				{
					Size = new Vector2(28f, 28f),
					TextureLinearFilter = true,
					HorizontalAlignment = WidgetAlignment.Center,
					VerticalAlignment = WidgetAlignment.Center,
					Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/Veemon Logo"),
					OutlineColor = new Color(0, 0, 0, 0),
					FillColor = Color.White,
					IsVisible = true,
					TextureAnisotropicFilter = true,
					BlendState = BlendState.NonPremultiplied
				};
				button.Children.Add(icon);
				rightBottomBar.Children.Add(button);
			}

			// NOTA: El intento de ShittyButtonModLoader de agregar otro botón se omite,
			// pues ya existe el botón con el mismo nombre y función.
		}

		// ---------------------------------------------------------------------------------
		// Hook: BeforeWidgetUpdate (ShittyModLoader + ShittyButtonModLoader)
		// ---------------------------------------------------------------------------------
		public override void BeforeWidgetUpdate(Widget widget)
		{
			// ---------- Lógica del menú principal ----------
			MainMenuScreen mainMenu = widget as MainMenuScreen;
			if (mainMenu != null)
			{
				// Botón de bestiario infectado
				BevelledButtonWidget zombieButton = mainMenu.Children.Find<BevelledButtonWidget>("ZombieBestiaryButton", false);
				if (zombieButton != null && zombieButton.IsClicked)
				{
					if (ScreensManager.FindScreen<BestiaryInfectedScreen>("BestiaryInfected") == null)
						ScreensManager.AddScreen("BestiaryInfected", new BestiaryInfectedScreen());
					ScreensManager.SwitchScreen("BestiaryInfected");
				}
				// Botón de Agradecimientos
				BevelledButtonWidget thanksButton = mainMenu.Children.Find<BevelledButtonWidget>("SpecialThanksButton", false);
				if (thanksButton != null && thanksButton.IsClicked)
				{
					if (ScreensManager.FindScreen<SpecialThanksScreen>("SpecialThanks") == null)
						ScreensManager.AddScreen("SpecialThanks", new SpecialThanksScreen());
					ScreensManager.SwitchScreen("SpecialThanks");
				}
				// ===== NUEVO BOTÓN PERSONALIZADO (efecto arcoíris en fondo) =====
				BevelledButtonWidget customButton = mainMenu.Children.Find<BevelledButtonWidget>("CustomShittyButton", false);
				if (customButton != null)
				{
					// Fondo arcoíris (sin desfase)
					float hue = (float)((Time.RealTime * 60.0) % 360.0);
					Vector3 rgb = Color.HsvToRgb(new Vector3(hue, 1f, 1f));
					customButton.BevelColor = new Color(rgb);
					customButton.CenterColor = new Color(rgb);

					// Acción al hacer clic (puedes cambiar a lo que quieras)
					if (customButton.IsClicked)
					{
						// Ejemplo: abrir tu pantalla personalizada
						if (ScreensManager.FindScreen<ShittyCreaturesSettingsScreen>("CustomScreen") == null)
							ScreensManager.AddScreen("CustomScreen", new ShittyCreaturesSettingsScreen());
						ScreensManager.SwitchScreen("CustomScreen");
					}
				}
				// Botón Veemon (changelog)
				BevelledButtonWidget shittyButton = mainMenu.Children.Find<BevelledButtonWidget>("ShittyButton", false);
				if (shittyButton != null && shittyButton.IsClicked)
				{
					if (ScreensManager.FindScreen<ShittyCreaturesReleasesScreen>("ShittyCreaturesReleases") == null)
						ScreensManager.AddScreen("ShittyCreaturesReleases", new ShittyCreaturesReleasesScreen());
					ScreensManager.SwitchScreen("ShittyCreaturesReleases");
				}

				// Efecto arcoíris en "Acerca del Mod"
				BevelledButtonWidget aboutButton = mainMenu.Children.Find<BevelledButtonWidget>("ShittyAboutButton", false);
				if (aboutButton != null)
				{
					// Sin desfase
					float hue = (float)((Time.RealTime * 60.0) % 360.0);
					Vector3 rgb = Color.HsvToRgb(new Vector3(hue, 1f, 1f));
					aboutButton.Color = new Color(rgb);
				}
				if (aboutButton != null && aboutButton.IsClicked)
					DialogsManager.ShowDialog(null, new ShittyCreaturesAboutDialog());

				// Botón "Salir"
				BevelledButtonWidget exitButton = mainMenu.Children.Find<BevelledButtonWidget>("ShittyExitButton", false);
				if (exitButton != null && exitButton.IsClicked)
					Environment.Exit(0);

				// Efecto arcoíris en el copyright del mod (se busca dentro de mainMenu)
				LabelWidget modCopyrightLabel = mainMenu.Children.Find<LabelWidget>("ModCopyrightLabel", true);
				if (modCopyrightLabel != null)
				{
					float hue = (float)((Time.RealTime * 30.0) % 360.0);
					Vector3 rgb = Color.HsvToRgb(new Vector3(hue, 1f, 1f));
					modCopyrightLabel.Color = new Color(rgb);
				}
			}

			// ---------- HUD de coordenadas y sangrado ----------
			GameWidget gameWidget = widget as GameWidget;
			if (gameWidget != null)
			{
				ComponentPlayer player = gameWidget.PlayerData?.ComponentPlayer;

				// ─── Botón de logros (SIEMPRE, independientemente de coordenadas) ───
				if (player != null && m_achievementButtons.TryGetValue(player, out var btn) && btn != null && btn.IsClicked)
				{
					// Toggle: si ya está abierto, lo cierra; si no, lo abre
					if (player.ComponentGui.ModalPanelWidget is AchievementsWidget)
						player.ComponentGui.ModalPanelWidget = null;
					else
						player.ComponentGui.ModalPanelWidget = new AchievementsWidget(player);
				}

				// ─── Sistema de sangrado (siempre se ejecuta) ───
				UpdateBleedingSystems(gameWidget);

				// ─── Hacer bailar a las criaturas durante la celebración ───
				if (m_celebrationActive)
				{
					MakeCreaturesDance(gameWidget);
					// Obtener el Project a través del subsistema (GameWidget no tiene .Project)
					var celebrationProject = m_subsystemGreenNightSky?.Project;
					if (celebrationProject != null)
					{
						EnsureCelebrationInvulnerability(celebrationProject);
					}
				}

				// ─── Coordenadas (solo si están activadas) ───
				if (ShittyCreaturesSettingsManager.CoordinateDisplayEnabled)
				{
					if (player != null && player.ComponentHealth.Health > 0f)
					{
						// Elegir la posición según la cámara activa
						Vector3 displayPos;
						Camera activeCam = gameWidget.ActiveCamera;
						if (activeCam is FreeCamera || activeCam is DebugCamera)
						{
							displayPos = activeCam.ViewPosition; // posición de la cámara
						}
						else
						{
							displayPos = player.ComponentBody.Position; // posición del jugador
						}

						if (!m_coordinateLabels.ContainsKey(player))
						{
							LabelWidget label = new LabelWidget
							{
								FontScale = 0.5f,
								HorizontalAlignment = WidgetAlignment.Far,
								VerticalAlignment = WidgetAlignment.Far,
								Margin = new Vector2(0f, 0f),
								Color = Color.White,
								DropShadow = true,
								IsHitTestVisible = false
							};
							gameWidget.GuiWidget.Children.Add(label);
							m_coordinateLabels[player] = label;
						}

						LabelWidget coordLabel = m_coordinateLabels[player];
						string format = LanguageControl.Get(new string[] { "Coordinates", "0" });
						coordLabel.Text = string.Format(format,
							displayPos.X.ToString("F1"),
							displayPos.Z.ToString("F1"),
							displayPos.Y.ToString("F1"));

						AchievementsManager.UpdateDayAchievements();
						// ===== ACTUALIZACIÓN PERIÓDICA DE ESTADÍSTICAS POR DIFICULTAD =====
						if (m_subsystemGreenNightSky != null)
						{
							{
								var project = m_subsystemGreenNightSky.Project;
								if (project != null)
								{
									EnforceCombatStatsByDifficulty(project);
									EnforceBlockBreakingByDifficulty(project);
								}

								// Detectar intento activo de montar (tecla o botón GUI)
								if (player != null && player.ComponentInput != null)
								{
									PlayerInput input = player.ComponentInput.PlayerInput;
									bool currentToggle = input.ToggleMount;

									// También comprobar si el botón de montar fue clickeado
									ButtonWidget mountButton = player.GuiWidget?.Children.Find<ButtonWidget>("MountButton", true);
									bool buttonClicked = mountButton != null && mountButton.IsClicked;

									bool mountingRequested = currentToggle || buttonClicked;

									// Detectar flanco ascendente
									bool previous = m_previousToggleMountState.GetValueOrDefault(player);
									if (mountingRequested && !previous)
									{
										// El jugador acaba de intentar montar
										double now = Time.RealTime;
										if (!m_lastMountAttemptMessageTime.ContainsKey(player) || now - m_lastMountAttemptMessageTime[player] > 1.0)
										{
											// Buscar monturas zombi no montables cerca
											ComponentMount nearestZombieMount = FindNearestUnrideableZombieMount(player);
											if (nearestZombieMount != null)
											{
												m_lastMountAttemptMessageTime[player] = now;
												string message = LanguageControl.Get("MountLock", 0);
												player.ComponentGui?.DisplaySmallMessage(message, new Color(255, 110, 110), false, true);
											}
										}
									}
									m_previousToggleMountState[player] = mountingRequested;
								}
							}
						}
					}
				}
			}
		}

		public override void OnWidgetConstruct(ref Widget widget)
		{
			if (widget is PanoramaWidget && !(widget is NewPanoramaWidget))
			{
				widget = new NewPanoramaWidget();
			}
		}

		private void UpdateCoordinateLabelsPosition(Project project)
		{
			var playersSubsystem = project.FindSubsystem<SubsystemPlayers>(true);
			if (playersSubsystem == null) return;

			foreach (var player in playersSubsystem.ComponentPlayers)
			{
				if (player == null || player.GuiWidget == null) continue;

				// Si ya existe un label para este jugador
				if (m_coordinateLabels.TryGetValue(player, out LabelWidget existingLabel))
				{
					// Cambiar su alineación y margen a los nuevos valores
					existingLabel.HorizontalAlignment = WidgetAlignment.Near;  // izquierda
					existingLabel.VerticalAlignment = WidgetAlignment.Near;    // arriba
					existingLabel.Margin = new Vector2(70f, 10f);              // mismo margen
																			   // No necesitas QueueLayout, el cambio de propiedades se aplica automáticamente
				}
				else
				{
					// Si por alguna razón no existe, créalo
					LabelWidget newLabel = new LabelWidget
					{
						FontScale = 0.5f,
						HorizontalAlignment = WidgetAlignment.Near,
						VerticalAlignment = WidgetAlignment.Near,
						Margin = new Vector2(70f, 10f),
						Color = Color.White,
						DropShadow = true,
						IsHitTestVisible = false
					};
					player.GuiWidget.Children.Add(newLabel);
					m_coordinateLabels[player] = newLabel;
				}
			}
		}

		public override void OnPlayerDead(PlayerData playerData)
		{
			// Eliminar label de coordenadas
			foreach (var kvp in m_coordinateLabels.ToArray())
			{
				if (kvp.Key.PlayerData == playerData)
				{
					kvp.Value.ParentWidget?.Children.Remove(kvp.Value);
					m_coordinateLabels.Remove(kvp.Key);
				}
			}

			// --- NUEVO: Eliminar el botón de logros asociado al ComponentPlayer que muere ---
			ComponentPlayer deadPlayer = playerData.ComponentPlayer;
			if (deadPlayer != null && m_achievementButtons.ContainsKey(deadPlayer))
			{
				var btn = m_achievementButtons[deadPlayer];
				btn.ParentWidget?.Children.Remove(btn);  // opcional: eliminar visualmente
				m_achievementButtons.Remove(deadPlayer);
			}

			// Limpiar seguimiento de golpes rápidos
			foreach (var hitKvp in m_lastHitGameTime.ToArray())
			{
				if (hitKvp.Key.PlayerData == playerData)
				{
					m_lastHitGameTime.Remove(hitKvp.Key);
					m_lastHitTarget.Remove(hitKvp.Key);
					m_fastHitMode.Remove(hitKvp.Key);
				}
			}
		}

		// ---------------------------------------------------------------------------------
		// Hook: OnSettingsScreenCreated (ChaseMusicModLoader)
		// ---------------------------------------------------------------------------------
		public override void OnSettingsScreenCreated(SettingsScreen settingsScreen, out Dictionary<ButtonWidget, Action> buttonsToAdd)
		{
			buttonsToAdd = null; // No agregamos ningún botón
		}

		// ---------------------------------------------------------------------------------
		// Hook: OnPlayerInputInteract (unificado: HireableNPC, CreatureInventory, GunsTrader, PirateTrader, RemoteControl)
		// Se respeta el orden de prioridad manual: primero Hireable (alta), luego el resto.
		// ---------------------------------------------------------------------------------
		public override void OnPlayerInputInteract(
	ComponentPlayer player,
	ref bool playerOperated,
	ref double timeIntervalLastActionTime,
	ref int priorityUse,
	ref int priorityInteract,
	ref int priorityPlace)
		{
			if (playerOperated) return;

			var input = player.ComponentInput.PlayerInput;
			if (input.Interact == null) return;

			// 1. RemoteControl: interacción con el bloque en mano
			int activeBlockValue = player.ComponentMiner.ActiveBlockValue;
			int activeBlockIndex = Terrain.ExtractContents(activeBlockValue);
			Block activeBlock = BlocksManager.Blocks[activeBlockIndex];
			if (activeBlock is RemoteControlBlock)
			{
				var greenNightSky = player.Project.FindSubsystem<SubsystemGreenNightSky>(true);
				if (greenNightSky != null)
				{
					GreenNightToggleDialog dialog = new GreenNightToggleDialog(greenNightSky, player);
					DialogsManager.ShowDialog(player.GuiWidget, dialog);
					AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
					playerOperated = true;
					return;
				}
			}

			// Comprobación común: si se sostiene un objeto curativo, no se interactúa.
			if (IsHealingItem(activeBlockIndex))
				return;

			// --- CARTA DE GUERRA (LetterWarBlock) ---
			if (activeBlock is LetterWarBlock)
			{
				// Durante celebración: impedir activar la guerra de narcos
				if (AchievementsManager.IsCelebrationActive)
				{
					player.ComponentGui.DisplaySmallMessage(LanguageControl.Get("AchievementsMessages", 8), new Color(255, 200, 0), false, true);
					playerOperated = true;
					return;
				}

				LetterWarDialog dialog = new LetterWarDialog(player);
				DialogsManager.ShowDialog(player.GuiWidget, dialog);
				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				playerOperated = true;
				return;
			}

			// Raycast común
			var result = player.ComponentMiner.Raycast<BodyRaycastResult>(
				input.Interact.Value,
				RaycastMode.Interaction,
				raycastTerrain: false,
				raycastBodies: true,
				raycastMovingBlocks: false,
				reach: null);

			if (!result.HasValue) return;

			var targetBody = result.Value.ComponentBody;
			if (targetBody == null || targetBody.Entity == player.Entity) return;
			Entity target = targetBody.Entity;

			// Verificar si el objetivo está muerto
			var health = target.FindComponent<ComponentHealth>();
			if (health != null && (health.Health <= 0f || health.DeathTime.HasValue)) return;
			Entity targetEntity = targetBody.Entity;

			// --- DESAFÍO DE INFINITE (solo para InfiniteTheJackal) ---
			string targetEntityName = targetEntity.ValuesDictionary?.DatabaseObject?.Name;
			if (targetEntityName == "InfiniteTheJackal")
			{
				ComponentInfiniteChallenge challenge = targetEntity.FindComponent<ComponentInfiniteChallenge>();
				if (challenge != null)
				{
					// ✅ Duelo activo → convertir interacción en ataque SOLO EN MÓVIL
					if (challenge.IsDuelActive && player.ComponentInput.IsControlledByTouch)
					{
						// Usamos el mismo rango de ataque melee que el botón de golpe normal (2 bloques)
						float meleeAttackRange = 2f;
						var hitResult = player.ComponentMiner.Raycast<BodyRaycastResult>(
							input.Interact.Value,
							RaycastMode.Interaction,
							true, true, true, meleeAttackRange);

						if (hitResult.HasValue && hitResult.Value.ComponentBody != null)
						{
							ComponentBody hitBody = hitResult.Value.ComponentBody;
							Vector3 hitPoint = hitResult.Value.HitPoint();
							Vector3 hitDirection = input.Interact.Value.Direction;

							// Verificar que el objetivo sea el oponente del duelo (o cualquier enemigo, pero aquí solo interesa el oponente)
							// Podemos comparar la entidad o simplemente procesar el golpe como siempre.
							// ProcessHit ya contiene la lógica completa.
							double dummyTimeInterval = 0.33;
							ProcessHit(player, hitBody, hitPoint, hitDirection, ref dummyTimeInterval);
						}
						playerOperated = true;
						return;
					}

					// Si no ha sido derrotado, iniciar desafío (como estaba originalmente)
					if (!challenge.HasBeenDefeated)
					{
						challenge.StartChallenge(player);
						playerOperated = true;
						return;
					}
				}
			}

			// --- VehicleInventory (criaturas montables) ---
			var vehicleInv = target.FindComponent<ComponentVehicleInventory>();
			if (vehicleInv != null)
			{
				vehicleInv.OpenInventory(player);
				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				player.ComponentMiner.Poke(false);
				playerOperated = true;
				return;
			}

			// 2. HireableNPC (prioridad alta simulada)
			var hireable = target.FindComponent<ComponentHireableNPC>();
			if (hireable != null)
			{
				if (!hireable.IsHired)
				{
					AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
					player.ComponentMiner.Poke(false);
					player.ComponentGui.ModalPanelWidget = new HireWidget(player, hireable);
					playerOperated = true;
					return;
				}
				// Si está contratado, no abrimos inventario aquí; lo manejará CreatureInventory.
			}

			// 3. Comerciantes (GunsTrader y PirateTrader)
			var trader = target.FindComponent<ComponentTrader>();
			if (trader != null)
			{
				string entityName = target.ValuesDictionary?.DatabaseObject?.Name;
				if (entityName == "FirearmsDealer")
				{
					player.ComponentGui.ModalPanelWidget = new GunsTradeWidget(
						player.ComponentMiner.Inventory, trader, player);
					AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
					player.ComponentMiner.Poke(false);
					playerOperated = true;
					return;
				}
				else if (entityName == "PirataHostilComerciante")
				{
					player.ComponentGui.ModalPanelWidget = new PirateTradeWidget(
						player.ComponentMiner.Inventory, trader, player);
					AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
					player.ComponentMiner.Poke(false);
					playerOperated = true;
					return;
				}
			}

			// 4. CreatureInventory (abrir inventario de criatura)
			var creatureInv = target.FindComponent<ComponentCreatureInventory>();
			if (creatureInv != null)
			{
				// Si tiene hireable y no está contratado, ya fue manejado arriba.
				// Si está contratado o no tiene hireable, abrimos inventario.
				creatureInv.OpenInventory(player);
				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				player.ComponentMiner.Poke(false);
				playerOperated = true;
			}
		}

		// ---------------------------------------------------------------------------------
		// Hook: ChangeSkyColor (GreenNightSkyModLoader)
		// ---------------------------------------------------------------------------------
		public override Color ChangeSkyColor(Color oldColor, Vector3 direction, float timeOfDay, int temperature)
		{
			var greenNight = SubsystemGreenNightSky.Instance;
			if (greenNight != null && greenNight.IsGreenNightActive)
				return new Color(0, 50, 0);
			return oldColor;
		}

		// ---------------------------------------------------------------------------------
		// Hook: OnVitalStatsUpdateSleep (GreenNightSkyModLoader)
		// ---------------------------------------------------------------------------------
		public override void OnVitalStatsUpdateSleep(ComponentVitalStats vitalStats, ref float sleep, ref float gameTimeDelta, ref bool skipVanilla)
		{
			var greenNight = SubsystemGreenNightSky.Instance;
			if (greenNight != null && greenNight.IsGreenNightActive)
				skipVanilla = true;

			var banditInvasion = vitalStats.Entity.Project.FindSubsystem<SubsystemBanditInvasion>(false);
			if (banditInvasion != null && banditInvasion.IsInvasionActive)
				skipVanilla = true;
		}

		// ---------------------------------------------------------------------------------
		// Hook: MenuPlayMusic (MusicModLoader)
		// ---------------------------------------------------------------------------------
		public override void MenuPlayMusic(out string contentMusicPath)
		{
			// Selección aleatoria como en MusicManager
			int songIndex = _random.Int(0, 41);
			while (songIndex == _lastSongIndex)
			{
				songIndex = _random.Int(0, 41);
			}
			_lastSongIndex = songIndex;

			switch (songIndex)
			{
				case 0: contentMusicPath = "MenuMusic/Dragon Quest NES Title Theme"; break;
				case 1: contentMusicPath = "MenuMusic/Digimon 02 Target Wada Kouji"; break;
				case 2: contentMusicPath = "MenuMusic/Touhou 2 Mimas Theme Complete Darkness"; break;
				case 3: contentMusicPath = "MenuMusic/Touhou 2 Eastern Wind"; break;
				case 4: contentMusicPath = "MenuMusic/Touhou 2 Record of the Sealing of an Oriental Demon"; break;
				case 5: contentMusicPath = "MenuMusic/Digimon 02 Evolution Break Up Ayumi Miyazaki"; break;
				case 6: contentMusicPath = "MenuMusic/Digimon Adventure 01 Brave Heart Wada Kouji"; break;
				case 7: contentMusicPath = "MenuMusic/Digimon Adventure 01 Butterfly Wada Kouji"; break;
				case 8: contentMusicPath = "MenuMusic/Digimon Savers OP1 Theme Song Gouing Going My Soul Dynamite SHU"; break;
				case 9: contentMusicPath = "MenuMusic/Digimon Savers OP2 Hirari Wada Kouji"; break;
				case 10: contentMusicPath = "MenuMusic/EoSD Credits Theme Crimson Belvedere Eastern Dream"; break;
				case 11: contentMusicPath = "MenuMusic/Digimon Tamers The Biggest Dreamer Wada Kouji"; break;
				case 12: contentMusicPath = "MenuMusic/Touhou 6 Flandre Scarlets Theme U.N. Owen was her"; break;
				case 13: contentMusicPath = "MenuMusic/Digimon Frontiers FIRE Wada Kouji"; break;
				case 14: contentMusicPath = "MenuMusic/Rocket Knight Adventures Stage 1-1"; break;
				case 15: contentMusicPath = "MenuMusic/Rocket Knight Adventures Stage 1-2"; break;
				case 16: contentMusicPath = "MenuMusic/Sparkster (SEGA Genesis) Stage 1-1"; break;
				case 17: contentMusicPath = "MenuMusic/Sparkster (SNES) Stage Lakeside"; break;
				case 18: contentMusicPath = "MenuMusic/Space Harrier Theme"; break;
				case 19: contentMusicPath = "MenuMusic/MAGICAL SOUND SHOWER OutRun"; break;
				case 20: contentMusicPath = "MenuMusic/Super Hang-On Outride A Crisis"; break;
				case 21: contentMusicPath = "MenuMusic/Super Hang-On Sprinter"; break;
				case 22: contentMusicPath = "MenuMusic/Super Hang-On Winning Run"; break;
				case 23: contentMusicPath = "MenuMusic/Nichijou Koigokoro Wa Dangan Mo Yawarakakusuru"; break;
				case 24: contentMusicPath = "MenuMusic/SEGA Mega CD Japanese European Gamerip BIOS"; break;
				case 25: contentMusicPath = "MenuMusic/SEGA CD American BIOS Gamerip Version 01"; break;
				case 26: contentMusicPath = "MenuMusic/SEGA CD American BIOS Gamerip Version 02"; break;
				case 27: contentMusicPath = "MenuMusic/Sonic The Hedgehog 1991 Spring Yard Zone"; break;
				case 28: contentMusicPath = "MenuMusic/Sonic The Hedgehog 1991 Marble Zone"; break;
				case 29: contentMusicPath = "MenuMusic/Sonic The Hedgehog 2 1992 Hill Top Zone"; break;
				case 30: contentMusicPath = "MenuMusic/Beat Hit! Ayumi Miyazaki"; break;
				case 31: contentMusicPath = "MenuMusic/Chrono Trigger Main Theme"; break;
				case 32: contentMusicPath = "MenuMusic/Twill STAND UP Digimon Xros Wars Hunters"; break;
				case 33: contentMusicPath = "MenuMusic/Sonar Pocket Never Give Up! Digimon Fusion"; break;
				case 34: contentMusicPath = "MenuMusic/Prince Of Persia (SNES) Recap"; break;
				case 35: contentMusicPath = "MenuMusic/Prince Of Persia (SNES) Staff Roll"; break;
				case 36: contentMusicPath = "MenuMusic/FIELD OF VIEW 渇いた叫び - 捨てられた物。"; break;
				case 37: contentMusicPath = "MenuMusic/Power Rangers The Movie Title Theme SNES"; break;
				case 38: contentMusicPath = "MenuMusic/Sonic Boom Closing Theme Sonic CD"; break;
				case 39: contentMusicPath = "MenuMusic/Sonic Boom Sonic CD"; break;
				case 40: contentMusicPath = "MenuMusic/You Can Do Anything Sonic CD"; break;
				default: contentMusicPath = "MenuMusic/Dragon Quest NES Title Theme"; break;
			}
		}

		// ---------------------------------------------------------------------------------
		// Hook: PlayInGameMusic (MusicModLoader)
		// ---------------------------------------------------------------------------------
		public override void PlayInGameMusic()
		{
			// No se modifica la música del juego (comportamiento original)
		}

		// ---------------------------------------------------------------------------------
		// Hook: MatchRecipe (RemoteControlModLoader)
		// ---------------------------------------------------------------------------------
		public override bool MatchRecipe(string[] requiredIngredients, string[] actualIngredients, out bool skipVanilla)
		{
			skipVanilla = false;

			if (IsRemoteControlRecipe(requiredIngredients))
			{
				if (s_isMatchingRemoteControlRecipe) return false;

				s_isMatchingRemoteControlRecipe = true;
				bool playerMatches = MatchRemoteControlPattern(actualIngredients);
				s_isMatchingRemoteControlRecipe = false;

				if (playerMatches)
				{
					var zombiesSpawn = SubsystemZombiesSpawn.Instance;
					if (zombiesSpawn == null || !zombiesSpawn.IsAllWavesCompleted)
					{
						skipVanilla = true;
						double now = Time.RealTime;
						if (now - s_lastProhibitionMessageTime > 1.0)
						{
							s_lastProhibitionMessageTime = now;
							string smallMessage = LanguageControl.Get("UnlockedItems", "CraftingLocked");

							var playersSubsystem = zombiesSpawn?.Project.FindSubsystem<SubsystemPlayers>(true);
							if (playersSubsystem != null)
								foreach (var p in playersSubsystem.ComponentPlayers)
									p.ComponentGui.DisplaySmallMessage(smallMessage, Color.White, false, true);
						}
						return false;
					}
				}
			}
			return false;
		}

		private bool IsRemoteControlRecipe(string[] ingredients)
		{
			if (ingredients == null || ingredients.Length != 9) return false;
			return ingredients[0] == null &&
				   ingredients[1] == "semiconductorblock" &&
				   ingredients[2] == null &&
				   ingredients[3] == "wire" &&
				   ingredients[4] == "battery" &&
				   ingredients[5] == "wire" &&
				   ingredients[6] == null &&
				   ingredients[7] == "semiconductorblock" &&
				   ingredients[8] == null;
		}

		private bool MatchRemoteControlPattern(string[] actualIngredients)
		{
			if (actualIngredients == null || actualIngredients.Length != 9) return false;

			string[] remotePattern = new string[9];
			remotePattern[0] = null;
			remotePattern[1] = "semiconductorblock";
			remotePattern[2] = null;
			remotePattern[3] = "wire";
			remotePattern[4] = "battery";
			remotePattern[5] = "wire";
			remotePattern[6] = null;
			remotePattern[7] = "semiconductorblock";
			remotePattern[8] = null;

			string[] transformed = new string[9];
			for (int i = 0; i < 2; i++)
			{
				bool flip = i != 0;
				for (int shiftX = -4; shiftX <= 2; shiftX++)
					for (int shiftY = -4; shiftY <= 2; shiftY++)
						if (TransformRecipe(transformed, remotePattern, shiftX, shiftY, flip))
						{
							bool match = true;
							for (int j = 0; j < 9; j++)
								if (!CompareIngredients(transformed[j], actualIngredients[j]))
								{
									match = false;
									break;
								}
							if (match) return true;
						}
			}
			return false;
		}

		private bool TransformRecipe(string[] transformedIngredients, string[] ingredients, int shiftX, int shiftY, bool flip)
		{
			for (int i = 0; i < 9; i++)
				transformedIngredients[i] = null;

			for (int j = 0; j < 3; j++)
				for (int k = 0; k < 3; k++)
				{
					int x = (flip ? (3 - k - 1) : k) + shiftX;
					int y = j + shiftY;
					string ingredient = ingredients[k + j * 3];
					if (x >= 0 && y >= 0 && x < 3 && y < 3)
						transformedIngredients[x + y * 3] = ingredient;
					else if (!string.IsNullOrEmpty(ingredient))
						return false;
				}
			return true;
		}

		public override IEnumerable<KeyValuePair<string, int>> GetCameraList()
		{
			// La clave debe coincidir con el nombre completo de la clase (Game.FreeCamera)
			// El valor es el orden por defecto (puede ser cualquier número, se reordenará después)
			yield return new KeyValuePair<string, int>("Game.FreeCamera", 5);
		}

		private bool CompareIngredients(string required, string actual)
		{
			if (required == null) return actual == null;
			if (actual == null) return false;

			CraftingRecipesManager.DecodeIngredient(required, out string requiredId, out int? requiredData);
			CraftingRecipesManager.DecodeIngredient(actual, out string actualId, out int? actualData);
			if (actualData == null)
				throw new InvalidOperationException("Actual ingredient data not specified.");

			return requiredId == actualId && (requiredData == null || requiredData.Value == actualData.Value);
		}

		// Hook: OnPlayerInputHit (cuando el jugador golpea cuerpo a cuerpo)
		public override void OnPlayerInputHit(
	ComponentPlayer player,
	ref bool playerOperated,
	ref double timeIntervalHit,
	ref float meleeAttackRange,
	bool skipVanilla,
	out bool flag)
		{
			flag = false;
			if (playerOperated || skipVanilla || player == null) return;

			PlayerInput input = player.ComponentInput.PlayerInput;
			if (input.Hit == null) return;

			ComponentMiner miner = player.ComponentMiner;
			if (miner == null) return;

			BodyRaycastResult? result = miner.Raycast<BodyRaycastResult>(
				input.Hit.Value, RaycastMode.Interaction, true, true, true, meleeAttackRange);

			if (result.HasValue)
			{
				ComponentBody hitBody = result.Value.ComponentBody;
				if (hitBody != null && hitBody.Entity != player.Entity)
				{
					Vector3 hitPoint = result.Value.HitPoint();
					Vector3 hitDirection = input.Hit.Value.Direction;
					ProcessHit(player, hitBody, hitPoint, hitDirection, ref timeIntervalHit);
				}
			}
		}

		// Manejador del evento Injured del jugador
		private void OnPlayerInjuredForAllies(Injury injury)
		{
			if (!ShittyCreaturesSettingsManager.PunchCommandEnabled) return;

			ComponentHealth health = injury.ComponentHealth;
			if (health == null)
				return;

			ComponentPlayer player = health.Entity.FindComponent<ComponentPlayer>();
			if (player == null)
				return;

			ComponentCreature attacker = injury.Attacker;
			if (attacker == null)
				return;

			CommandAlliesToAttack(player, attacker);
		}

		// Ordena a todas las criaturas aliadas atacar al objetivo sin límites
		private void CommandAlliesToAttack(ComponentPlayer player, ComponentCreature target)
		{
			if (player == null || target == null) return;
			if (BossFightBlocker.IsAttackBlocked(target)) return;

			// NUEVO: No ordenar atacar a Infinite durante el duelo
			var infiniteChallenge = target.Entity.FindComponent<ComponentInfiniteChallenge>();
			if (infiniteChallenge != null && infiniteChallenge.IsDuelActive)
				return;

			if (player == null || target == null)
				return;

			var project = player.Project;
			if (project == null)
				return;

			SubsystemCreatureSpawn creatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(true);
			if (creatureSpawn == null)
				return;

			foreach (ComponentCreature creature in creatureSpawn.Creatures)
			{
				if (creature == null || creature.ComponentHealth.Health <= 0f)
					continue;

				bool isAlly = false;

				ComponentHireableNPC hireable = creature.Entity.FindComponent<ComponentHireableNPC>();
				if (hireable != null && hireable.IsHired)
				{
					isAlly = true;
				}

				if (!isAlly)
				{
					ComponentNewHerdBehavior herd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
					if (herd != null && !string.IsNullOrEmpty(herd.HerdName))
					{
						string herdName = herd.HerdName.ToLower();
						if (herdName == "player" || herdName.Contains("guardian"))
						{
							isAlly = true;
						}
					}
				}

				if (!isAlly)
					continue;

				if (creature == target)
					continue;

				bool targetIsAlly = false;
				ComponentNewHerdBehavior targetHerd = target.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName))
				{
					string targetHerdName = targetHerd.HerdName.ToLower();
					if (targetHerdName == "player" || targetHerdName.Contains("guardian"))
						targetIsAlly = true;
				}
				ComponentHireableNPC targetHireable = target.Entity.FindComponent<ComponentHireableNPC>();
				if (targetHireable != null && targetHireable.IsHired)
					targetIsAlly = true;

				if (targetIsAlly)
					continue;

				ComponentNewChaseBehavior chase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
				if (chase != null && !chase.Suppressed)
				{
					chase.Attack(target, float.MaxValue, float.MaxValue, true, true);
				}
			}
		}
		public override void OnMinerHit(ComponentMiner miner, ComponentBody targetBody, Vector3 hitPoint, Vector3 hitDirection, ref float attackPower, ref float hitProbability, ref float hitProbability2, out bool skipVanilla)
		{
			skipVanilla = false;

			// ===== NUEVO: Cancelar golpes del JUGADOR durante la celebración =====
			if (m_celebrationActive && miner.ComponentPlayer != null)
			{
				attackPower = 0f;
				hitProbability = 0f;
				hitProbability2 = 0f;
				return;
			}

			// ===== NUEVO: Cancelar golpes de CRIATURAS durante la celebración =====
			if (m_celebrationActive && miner.ComponentPlayer == null)
			{
				attackPower = 0f;
				hitProbability = 0f;
				hitProbability2 = 0f;
				skipVanilla = true;
				return;
			}

			// ===== Lógica original: defensa en modo Creativo =====
			if (miner.ComponentPlayer != null)
				return;

			ComponentPlayer targetPlayer = targetBody.Entity.FindComponent<ComponentPlayer>();
			if (targetPlayer == null)
				return;

			SubsystemGameInfo gameInfo = targetPlayer.Project.FindSubsystem<SubsystemGameInfo>(true);
			if (gameInfo.WorldSettings.GameMode != GameMode.Creative)
				return;

			if (!ShittyCreaturesSettingsManager.CreativeDefenseEnabled)
				return;

			ComponentCreature attackerCreature = miner.ComponentCreature;
			if (attackerCreature != null)
			{
				CommandAlliesToAttack(targetPlayer, attackerCreature);
			}

			// ===== NUEVO: Daño letal en dificultad Impossible para ataques de criaturas =====
			// Usar un nombre diferente para evitar conflicto con la variable targetPlayer de arriba
			ComponentPlayer playerTarget = targetBody.Entity.FindComponent<ComponentPlayer>();
			if (miner.ComponentCreature != null && miner.ComponentPlayer == null && playerTarget != null)
			{
				var greenNight = playerTarget.Project.FindSubsystem<SubsystemGreenNightSky>(true);
				if (greenNight != null && greenNight.DifficultyMode == DifficultyMode.Impossible)
				{
					attackPower = 999999f;
					hitProbability = 1f;
					hitProbability2 = 1f;
				}
			}
		}

		public override void ManageCameras(GameWidget gameWidget)
		{
			// Condición dinámica que se evalúa cada vez que se intenta activar
			bool FreeCameraCondition(GameWidget gw)
			{
				if (!ShittyCreaturesSettingsManager.FreeCameraEnabled)
					return false;

				if (SettingsManager.GetCameraManageSetting("Game.FreeCamera", false) < 0)
					return false;

				var gameInfo = gw.PlayerData.SubsystemPlayers.Project.FindSubsystem<SubsystemGameInfo>(true);
				return gameInfo.WorldSettings.GameMode != GameMode.Creative;
			}

			gameWidget.AddCamera(new FreeCamera(gameWidget), FreeCameraCondition);
		}

		public override bool OnPlayerSpawned(PlayerData.SpawnMode spawnMode, ComponentPlayer componentPlayer, Vector3 spawnPosition)
		{
			// Crear label de coordenadas para este jugador
			if (!m_coordinateLabels.ContainsKey(componentPlayer))
			{
				LabelWidget label = new LabelWidget
				{
					FontScale = 0.5f,
					HorizontalAlignment = WidgetAlignment.Near,  // ← izquierda (cerca del triángulo)
					VerticalAlignment = WidgetAlignment.Near,    // ← arriba
					Margin = new Vector2(70f, 10f),              // ← desplazado a la derecha para no tapar el botón
					Color = Color.White,
					DropShadow = true,
					IsHitTestVisible = false
				};
				componentPlayer.GuiWidget.Children.Add(label);
				m_coordinateLabels[componentPlayer] = label;
			}

			// --- NUEVO: Añadir botón de logros al jugador recién aparecido ---
			AddAchievementButton(componentPlayer);

			// Suscribir al evento de daño del jugador
			SubscribeToPlayerInjured(componentPlayer);

			// El diálogo debe mostrarse en TODOS los modos, incluyendo Creativo
			if (componentPlayer.PlayerData.SpawnsCount == 1)
			{
				var greenNight = componentPlayer.Project.FindSubsystem<SubsystemGreenNightSky>(true);
				if (greenNight != null)
				{
					Dispatcher.Dispatch(delegate
					{
						var dialog = new GreenNightIntervalDialog(greenNight, componentPlayer, true, true);
						DialogsManager.ShowDialog(componentPlayer.GuiWidget, dialog);
					}, false);
				}
			}

			// Solo entregar en la primera aparición del mundo (no en respawns)
			if (componentPlayer.PlayerData.SpawnsCount != 1)
				return true;

			SubsystemGameInfo gameInfo = componentPlayer.Project.FindSubsystem<SubsystemGameInfo>(true);
			if (gameInfo.WorldSettings.GameMode == GameMode.Creative)
				return true;

			IInventory inventory = componentPlayer.ComponentMiner.Inventory;
			if (inventory == null)
				return true;

			int ironMacheteIndex = GetBlockIndexByName("IronMacheteBlock");
			int boiledWaterBucketIndex = GetBlockIndexByName("BoiledWaterBucketBlock");
			int nuclearCoinIndex = GetBlockIndexByName("NuclearCoinBlock");
			int swm500Index = GetBlockIndexByName("SWM500Block");
			int swm500BulletIndex = GetBlockIndexByName("SWM500Bullet");
			int stoneAxeBlockIndex = GetBlockIndexByName("StoneAxeBlock");
			int mediumFirstAidKitIndex = GetBlockIndexByName("MediumFirstAidKitBlock");
			int CookedMeatBlock = GetBlockIndexByName("CookedMeatBlock");

			GiveItemToPlayer(inventory, ironMacheteIndex, 1);
			GiveItemToPlayer(inventory, boiledWaterBucketIndex, 1);
			GiveItemToPlayer(inventory, nuclearCoinIndex, 100);
			GiveItemToPlayer(inventory, stoneAxeBlockIndex, 1);

			int swm500Value = Terrain.MakeBlockValue(swm500Index, 0, SWM500Block.SetBulletNum(8));
			GiveItemToPlayer(inventory, swm500Value, 1);
			GiveItemToPlayer(inventory, swm500BulletIndex, 12);
			GiveItemToPlayer(inventory, mediumFirstAidKitIndex, 5);
			GiveItemToPlayer(inventory, CookedMeatBlock, 5);

			return true;
		}

		private void SubscribeToPlayerInjured(ComponentPlayer player)
		{
			if (player != null && player.ComponentHealth != null)
			{
				player.ComponentHealth.Injured -= OnPlayerInjuredForAllies;
				player.ComponentHealth.Injured += OnPlayerInjuredForAllies;
			}
		}

		// Método auxiliar para obtener índice de bloque por nombre de clase
		private int GetBlockIndexByName(string blockClassName)
		{
			// Buscar en todos los bloques registrados
			foreach (Block block in BlocksManager.Blocks)
			{
				if (block != null && block.GetType().Name == blockClassName)
					return block.BlockIndex;
			}
			Log.Warning($"[ShittyCreatures] No se encontró el bloque: {blockClassName}");
			return 0;
		}

		private (float attackPower, float walkSpeed, float flySpeed) GetBaseStatsForTemplate(string templateName)
		{
			if (string.IsNullOrEmpty(templateName))
				return (1f, 1f, 1f);

			if (m_baseStatsCache.TryGetValue(templateName, out var stats))
				return stats;

			try
			{
				var dict = DatabaseManager.FindEntityValuesDictionary(templateName, true);
				float attackPower = 1f, walkSpeed = 1f, flySpeed = 1f;

				var minerDict = dict?.GetValue<ValuesDictionary>("ComponentMiner", null);
				if (minerDict != null)
					attackPower = minerDict.GetValue<float>("AttackPower", 1f);

				var locomotionDict = dict?.GetValue<ValuesDictionary>("ComponentLocomotion", null);
				if (locomotionDict != null)
				{
					walkSpeed = locomotionDict.GetValue<float>("WalkSpeed", 1f);
					flySpeed = locomotionDict.GetValue<float>("FlySpeed", 1f);
				}

				stats = (attackPower, walkSpeed, flySpeed);
				m_baseStatsCache[templateName] = stats;
				return stats;
			}
			catch (Exception ex)
			{
				Log.Warning($"[ShittyCreatures] Error obteniendo stats base para {templateName}: {ex.Message}");
				return (1f, 1f, 1f);
			}
		}

		// Método auxiliar para dar ítems al jugador (busca slot disponible y añade)
		private void GiveItemToPlayer(IInventory inventory, int value, int count)
		{
			if (value == 0) return;

			int remaining = count;
			while (remaining > 0)
			{
				int slot = ComponentInventoryBase.FindAcquireSlotForItem(inventory, value);
				if (slot < 0)
				{
					Log.Warning($"[ShittyCreatures] No hay espacio para añadir {count}x {value}");
					break;
				}
				int capacity = inventory.GetSlotCapacity(slot, value);
				int existing = inventory.GetSlotCount(slot);
				int canAdd = Math.Min(capacity - existing, remaining);
				if (canAdd <= 0) continue;

				inventory.AddSlotItems(slot, value, canAdd);
				remaining -= canAdd;
			}
		}

		private void DrawHealthBar(Camera camera)
		{
			if (!ShittyCreaturesSettingsManager.HealthBarEnabled)
				return;
			if (m_healthBarModelsRenderer == null || m_healthBarCreatureSpawn == null)
				return;
			var allCreatures = new List<ComponentCreature>(m_healthBarCreatureSpawn.Creatures);
			if (m_healthBarPlayers != null)
			{
				foreach (var player in m_healthBarPlayers.ComponentPlayers)
				{
					if (player != null && player.ComponentHealth.Health > 0f)
						allCreatures.Add(player);
				}
			}
			foreach (var creature in allCreatures)
			{
				if (creature == null) continue;
				var health = creature.ComponentHealth;
				if (health == null) continue;
				var body = creature.ComponentBody;
				if (body == null) continue;
				if (creature is ComponentPlayer player && camera.GameWidget.IsEntityFirstPersonTarget(player.Entity) && (body.ImmersionFactor > 0f || body.IsCrouching))
					continue;
				Vector3 center = (body.BoundingBox.Min + body.BoundingBox.Max) * 0.5f;
				float height = body.BoundingBox.Max.Y - body.BoundingBox.Min.Y;
				if (Vector3.Distance(camera.ViewPosition, center) > HealthBarVisibilityRadius) continue;
				Vector3 textPos = new Vector3(center.X, body.BoundingBox.Max.Y + height * 0.3f, center.Z);
				Vector3 barPos = new Vector3(center.X, body.BoundingBox.Max.Y + height * 0.2f, center.Z);
				if (Vector3.Dot(camera.ViewDirection, textPos - camera.ViewPosition) <= 0f) continue;
				if (Vector3.Dot(camera.ViewDirection, barPos - camera.ViewPosition) <= 0f) continue;
				Vector3 textViewPos = Vector3.Transform(textPos, camera.ViewMatrix);
				Vector3 barViewPos = Vector3.Transform(barPos, camera.ViewMatrix);
				Vector3 horizontalOffset = Vector3.TransformNormal(0.005f * Vector3.Normalize(Vector3.Cross(camera.ViewDirection, camera.ViewUp)), camera.ViewMatrix);
				Vector3 verticalOffset = Vector3.TransformNormal(-0.005f * Vector3.UnitY, camera.ViewMatrix);
				float healthPercent = MathUtils.Saturate(health.Health);
				float attackResilience = health.AttackResilience;
				float displayedHealth = health.Health * attackResilience;
				Color color = (healthPercent < 0.3f) ? Color.Red : ((healthPercent < 0.7f) ? Color.Yellow : Color.Green);
				if (health.Health <= 0f)
					color = Color.White;
				string hpText = LanguageControl.Get("HealthBar", "HP", "HP");
				string text = creature.DisplayName + " " + displayedHealth.ToString("0") + " " + hpText;
				BitmapFont bitmapFont = ContentManager.Get<BitmapFont>("Fonts/Pericles");
				FontBatch3D fontBatch = m_healthBarModelsRenderer.PrimitivesRenderer.FontBatch(bitmapFont, 1, DepthStencilState.DepthRead, RasterizerState.CullNoneScissor, BlendState.AlphaBlend, SamplerState.LinearClamp);
				fontBatch.QueueText(text, textViewPos, horizontalOffset, verticalOffset, color, TextAnchor.Center);
				fontBatch.Flush(camera.ViewProjectionMatrix, false);
				float barWidth = 120f;
				float barHeight = 12.5f;
				Vector3 barStart = barViewPos - horizontalOffset * (barWidth * 0.5f);
				Vector3 barEnd = barViewPos + horizontalOffset * (barWidth * 0.5f);
				Vector3 barTop = barStart + verticalOffset * barHeight;
				Vector3 barTopEnd = barEnd + verticalOffset * barHeight;
				FlatBatch3D flatBatch = m_healthBarModelsRenderer.PrimitivesRenderer.FlatBatch(0, null, null, null);
				flatBatch.QueueQuad(barStart, barTop, Vector3.Lerp(barTop, barTopEnd, healthPercent), Vector3.Lerp(barStart, barEnd, healthPercent), Color.Lerp(Color.Red, Color.Green, healthPercent));
				if (healthPercent < 1f)
				{
					flatBatch.QueueQuad(Vector3.Lerp(barStart, barEnd, healthPercent), Vector3.Lerp(barTop, barTopEnd, healthPercent), barTopEnd, barEnd, new Color(0, 0, 0, 180));
				}
				flatBatch.Flush(camera.ViewProjectionMatrix, false);
			}
		}

		private void UpdateBleedingSystems(GameWidget gameWidget)
		{
			if (!ShittyCreaturesSettingsManager.BleedingEnabled)
			{
				// Detener todos los sistemas de sangrado existentes
				foreach (var kvp in m_bleedingSystems)
				{
					kvp.Value.IsStopped = true;
				}
				m_bleedingSystems.Clear();
				return;
			}
			var project = gameWidget.PlayerData?.SubsystemPlayers?.Project;
			if (project == null) return;

			var creatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(true);
			var particles = project.FindSubsystem<SubsystemParticles>(true);
			var terrain = project.FindSubsystem<SubsystemTerrain>(true);
			var players = project.FindSubsystem<SubsystemPlayers>(true);

			if (creatureSpawn == null || particles == null || terrain == null || players == null)
				return;

			// Lista de nombres de plantilla que NO sangran
			var noBleedCreatures = new HashSet<string>
	{
		"HumanoidSkeleton",
		"HumanoidSkeletonTamed",
		"TankGhost1",
		"TankGhost2",
		"TankGhost3",
		"FrozenTankGhost",
		"GhostNormal",
		"GhostFast",
		"PoisonousGhost",
		"GhostBoomer1",
		"GhostBoomer2",
		"GhostBoomer3",
		"GhostCharger",
		"FrozenGhost",
		"FrozenGhostBoomer",
		"LaMuerteX",
		"ElSenorDeLasTumbasMoradas",
		"HombreLava",
		"HombreAgua",
		"LiderCalavericoSupremo",
		"LiderCalavericoSupremoAlfa"
	};

			var allCreatures = new List<ComponentCreature>(creatureSpawn.Creatures);
			foreach (var p in players.ComponentPlayers)
				if (p != null) allCreatures.Add(p);

			var toRemove = new List<ComponentCreature>();

			foreach (var creature in allCreatures)
			{
				if (creature == null || creature.Entity == null) continue;
				var health = creature.ComponentHealth;
				if (health == null) continue;

				string templateName = creature.Entity.ValuesDictionary?.DatabaseObject?.Name;
				bool canBleed = !noBleedCreatures.Contains(templateName ?? string.Empty);
				bool isAlive = health.Health > 0f && health.DeathTime == null;

				// Verificar si la criatura tiene una enfermedad o veneno que cause daño interno (sin sangrado externo)
				bool hasNonPhysicalAilment = false;
				if (creature is ComponentPlayer player)
				{
					// Jugador: gripe o enfermedad (náuseas)
					if ((player.ComponentSickness != null && player.ComponentSickness.IsSick) ||
						(player.ComponentFlu != null && player.ComponentFlu.HasFlu))
					{
						hasNonPhysicalAilment = true;
					}
				}
				else
				{
					// Criatura no jugador: veneno o gripe de criatura
					var poison = creature.Entity.FindComponent<ComponentPoisonInfected>();
					if (poison != null && poison.IsInfected)
						hasNonPhysicalAilment = true;

					var flu = creature.Entity.FindComponent<ComponentFluInfected>();
					if (flu != null && flu.IsInfected)
						hasNonPhysicalAilment = true;
				}

				// Solo iniciar sangrado si:
				// - Está viva
				// - Puede sangrar
				// - Su salud es menor del 20%
				// - NO tiene una enfermedad que cause daño interno
				bool shouldStartBleeding = isAlive && canBleed && health.Health < 0.2f && !hasNonPhysicalAilment;

				if (m_bleedingSystems.TryGetValue(creature, out var bps))
				{
					// Si la criatura está viva y su salud ya NO está baja, detenemos el sangrado (se curó)
					if (isAlive && health.Health >= 0.3f)
					{
						bps.IsStopped = true;
						toRemove.Add(creature);
					}
					else
					{
						// Mantenemos el sangrado activo (vivo con poca vida, muerto, o incluso enfermo si ya estaba sangrando antes)
						bps.Position = creature.ComponentBody.Position + new Vector3(0f, 0.4f, 0f);
					}
				}
				else if (shouldStartBleeding)
				{
					// Iniciar sangrado solo para criaturas vivas con poca vida por heridas físicas
					try
					{
						var newBps = new BloodParticleSystem(terrain);
						newBps.Position = creature.ComponentBody.Position + new Vector3(0f, 0.4f, 0f);
						particles.AddParticleSystem(newBps, false);
						m_bleedingSystems[creature] = newBps;
					}
					catch (Exception ex)
					{
						Log.Warning($"[ShittyCreatures] No se pudo crear sangre: {ex.Message}");
					}
				}
				else if (!isAlive && canBleed) // Criatura muerta que puede sangrar (heridas físicas)
				{
					// Iniciar sangrado post-mortem (explosiones, proyectiles, etc.)
					try
					{
						var newBps = new BloodParticleSystem(terrain);
						newBps.Position = creature.ComponentBody.Position + new Vector3(0f, 0.4f, 0f);
						particles.AddParticleSystem(newBps, false);
						m_bleedingSystems[creature] = newBps;
					}
					catch (Exception ex)
					{
						Log.Warning($"[ShittyCreatures] No se pudo crear sangre: {ex.Message}");
					}
				}
			}

			foreach (var c in toRemove)
				m_bleedingSystems.Remove(c);

			// Limpiar sistemas cuyas criaturas ya no existen (despawned)
			var activeCreaturesSet = new HashSet<ComponentCreature>(allCreatures);
			toRemove.Clear();
			foreach (var kvp in m_bleedingSystems)
			{
				if (kvp.Key == null || kvp.Key.Entity == null || !activeCreaturesSet.Contains(kvp.Key))
				{
					kvp.Value.IsStopped = true;
					toRemove.Add(kvp.Key);
				}
			}
			foreach (var c in toRemove)
				m_bleedingSystems.Remove(c);
		}

		public override void OnCreatureDied(ComponentHealth health, Injury injury, ref int experienceOrbDropCount, ref bool calculateInKill)
		{
			// Llamar siempre a la lógica de logros
			AchievementsManager.OnCreatureDied(health, injury);

			// Lógica específica de experiencia para jefes (opcional)
			if (health?.Entity?.ValuesDictionary?.DatabaseObject?.Name is string entityName &&
				s_bossNames.Contains(entityName))
			{
				experienceOrbDropCount = 0; // Los jefes no sueltan experiencia
			}

			// Mantener el sonido especial para piratas
			Entity deadEntity = health.Entity;
			if (deadEntity == null) return;
			ComponentCreature creature = deadEntity.FindComponent<ComponentCreature>();
			if (creature == null) return;
			string templateName = deadEntity.ValuesDictionary?.DatabaseObject?.Name;

			// Logros de piratas
			ComponentPlayer killer = null;
			if (injury != null && injury.Attacker != null)
				killer = injury.Attacker.Entity.FindComponent<ComponentPlayer>();

			if (killer != null)
			{
				if (templateName == "PirataNormal" || templateName == "PirataElite")
				{
					AchievementsManager.OnPirateKill(killer);
				}
				else if (templateName == "PirataHostilComerciante")
				{
					AchievementsManager.OnKillPirateTrader(killer);
				}
				else if (templateName == "CapitanPirata")
				{
					AchievementsManager.OnKillPirateCaptain(killer);
				}
			}

			if (templateName == "CapitanPirata" || templateName == "PirataHostilComerciante")
			{
				string deathSoundPath = "Audio/Die(1)";
				SubsystemAudio audio = health.Project.FindSubsystem<SubsystemAudio>(true);
				if (audio != null && creature.ComponentBody != null)
				{
					audio.PlaySound(deathSoundPath, 1f, 0f, creature.ComponentBody.Position, 10f, false);
				}
				else
				{
					AudioManager.PlaySound(deathSoundPath, 1f, 0f, 0f);
				}
			}
		}


		public override void OnCreatureDying(ComponentHealth health, Injury injury)
		{
			Entity deadEntity = health.Entity;
			if (deadEntity == null) return;

			ComponentCreature creature = deadEntity.FindComponent<ComponentCreature>();
			if (creature == null) return;

			string templateName = deadEntity.ValuesDictionary?.DatabaseObject?.Name;
			// Verificar ambas criaturas
			if (templateName != "CapitanPirata" && templateName != "PirataHostilComerciante") return;

			ComponentCreatureSounds sounds = creature.ComponentCreatureSounds;
			if (sounds == null) return;

			// Usar reflexión para acceder a los campos privados
			var painSoundField = typeof(ComponentCreatureSounds).GetField("m_painSound", BindingFlags.Instance | BindingFlags.NonPublic);
			var moanSoundField = typeof(ComponentCreatureSounds).GetField("m_moanSound", BindingFlags.Instance | BindingFlags.NonPublic);
			var attackSoundField = typeof(ComponentCreatureSounds).GetField("m_attackSound", BindingFlags.Instance | BindingFlags.NonPublic);
			var lastSoundField = typeof(ComponentCreatureSounds).GetField("m_lastSoundTime", BindingFlags.Instance | BindingFlags.NonPublic);

			// Anular los sonidos que podrían reproducirse al morir
			if (painSoundField != null) painSoundField.SetValue(sounds, string.Empty);
			if (moanSoundField != null) moanSoundField.SetValue(sounds, string.Empty);
			if (attackSoundField != null) attackSoundField.SetValue(sounds, string.Empty);
			if (lastSoundField != null) lastSoundField.SetValue(sounds, double.MaxValue);
		}

		public override void ChangeVisualEffectOnInjury(ComponentHealth health, float lastHealth, ref float redScreenFactor, ref bool playPainSound, ref int healthBarFlashCount, ref float creatureModelRedFactor)
		{
			// Solo suprimir el sonido de dolor si la criatura está muriendo (salud <= 0)
			if (health.Health <= 0f)
			{
				Entity injuredEntity = health.Entity;
				if (injuredEntity != null)
				{
					string templateName = injuredEntity.ValuesDictionary?.DatabaseObject?.Name;
					// Verificar si es CapitanPirata O PirataHostilComerciante
					if (templateName == "CapitanPirata" || templateName == "PirataHostilComerciante")
					{
						// Suprimir el sonido de dolor SOLO cuando muere
						playPainSound = false;
					}
				}
			}
		}

		public override void OnProjectileRaycastBody(ComponentBody body, Projectile projectile, float distance, out bool ignore)
		{
			ignore = false;
			if (projectile.Owner == null) return;

			ComponentCreature ownerCreature = projectile.Owner;

			// -----------------------------------------------------------------
			// 1. Manada del jugador (ComponentNewHerdBehavior)
			// -----------------------------------------------------------------
			bool isOwnerPlayerHerd = false;
			if (ownerCreature is ComponentPlayer)
			{
				isOwnerPlayerHerd = true;
			}
			else
			{
				var ownerHerd = ownerCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (ownerHerd != null && (ownerHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
										  ownerHerd.HerdName.ToLower().Contains("guardian")))
				{
					isOwnerPlayerHerd = true;
				}
			}

			if (isOwnerPlayerHerd)
			{
				// Prevención de fuego amigo para aliados del jugador
				if (body.Entity == ownerCreature.Entity)
				{
					ignore = true;
					return;
				}
				if (body.Entity.FindComponent<ComponentPlayer>() != null)
				{
					ignore = true;
					return;
				}
				var targetHerd = body.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (targetHerd != null && targetHerd.IsSameHerdOrGuardian(ownerCreature))
				{
					ignore = true;
					return;
				}
				return; // No ignorar a otros
			}

			// -----------------------------------------------------------------
			// 2. Manadas de zombis (ComponentZombieHerdBehavior)
			// -----------------------------------------------------------------
			ComponentZombieHerdBehavior ownerZombieHerd = ownerCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();
			if (ownerZombieHerd != null)
			{
				ComponentZombieHerdBehavior targetZombieHerd = body.Entity.FindComponent<ComponentZombieHerdBehavior>();
				if (targetZombieHerd != null && ownerZombieHerd.IsSameZombieHerd(ownerCreature))
				{
					ignore = true;
					return;
				}
			}

			// -----------------------------------------------------------------
			// 3. MANADAS ORIGINALES "Alianza" y "Muerte" (ComponentHerdBehavior)
			// -----------------------------------------------------------------
			ComponentHerdBehavior ownerOriginalHerd = ownerCreature.Entity.FindComponent<ComponentHerdBehavior>();
			if (ownerOriginalHerd != null)
			{
				string ownerHerdName = ownerOriginalHerd.HerdName;
				// Solo aplicar si el nombre de la manada es "Alianza" o "Muerte"
				if (ownerHerdName == "Alianza" || ownerHerdName == "Muerte" || ownerHerdName == "Pirate")
				{
					ComponentHerdBehavior targetOriginalHerd = body.Entity.FindComponent<ComponentHerdBehavior>();
					if (targetOriginalHerd != null && targetOriginalHerd.HerdName == ownerHerdName)
					{
						ignore = true;
						return;
					}
				}
			}

			// -----------------------------------------------------------------
			// 4. Bandidos (ComponentBanditHerdBehavior) - lógica original
			// -----------------------------------------------------------------
			ComponentBanditHerdBehavior ownerBanditHerd = ownerCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
			if (ownerBanditHerd == null) return;
			ComponentBanditHerdBehavior targetBanditHerd = body.Entity.FindComponent<ComponentBanditHerdBehavior>();
			if (targetBanditHerd == null) return;
			if (!string.IsNullOrEmpty(ownerBanditHerd.HerdName) &&
				string.Equals(ownerBanditHerd.HerdName, targetBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
			{
				ignore = true;
			}
		}

		public override void OnProjectileHitBody(Projectile projectile, BodyRaycastResult bodyRaycastResult, ref Attackment attackment, ref Vector3 velocityAfterAttack, ref Vector3 angularVelocityAfterAttack, ref bool ignoreBody)
		{
			// Obtener el nombre del bloque del proyectil
			int blockIndex = Terrain.ExtractContents(projectile.Value);
			Block block = BlocksManager.Blocks[blockIndex];
			string blockName = block.GetType().Name;

			// Verificar si es una de las balas personalizadas
			if (blockName == "NuevaBala" ||
				blockName == "NuevaBala2" ||
				blockName == "NuevaBala3" ||
				blockName == "NuevaBala4" ||
				blockName == "NuevaBala5" ||
				blockName == "NuevaBala6" ||
				blockName == "BigStoneChunkBlock" ||
				blockName == "BigStoneFlameChunkBlock" ||
				blockName == "BigStoneFrozenChunkBlock" ||
				blockName == "BigStonePoisonChunkBlock")
			{
				attackment.ImpulseFactor = 0f;
				attackment.StunTimeSet = 0f;
			}

			// ----- Ordenar ataque de aliados cuando el jugador DISPARA y golpea a una criatura -----
			if (ShittyCreaturesSettingsManager.PunchCommandEnabled)
			{
				ComponentCreature targetCreature = bodyRaycastResult.ComponentBody.Entity.FindComponent<ComponentCreature>();
				if (targetCreature != null && targetCreature.ComponentHealth.Health > 0f)
				{
					ComponentPlayer ownerPlayer = projectile.Owner as ComponentPlayer;
					if (ownerPlayer != null)
					{
						CommandAlliesToAttack(ownerPlayer, targetCreature);
					}
				}
			}

			// ----- NUEVO: Ordenar ataque de aliados cuando el jugador RECIBE un proyectil -----
			ComponentPlayer hitPlayer = bodyRaycastResult.ComponentBody.Entity.FindComponent<ComponentPlayer>();
			if (hitPlayer != null && projectile.Owner != null && hitPlayer != projectile.Owner)
			{
				ComponentCreature attackerCreature = projectile.Owner;
				CommandAlliesToAttack(hitPlayer, attackerCreature);
			}
		}

		public override void SetHitInterval(ComponentMiner miner, ref double hitInterval)
		{
			if (miner.ComponentPlayer == null) return;

			if (ShittyCreaturesSettingsManager.FastMeleeEnabled &&
				m_fastHitMode.TryGetValue(miner.ComponentPlayer, out bool fastMode) && fastMode)
			{
				hitInterval = 0.1;
			}
		}

		// Método para que todas las criaturas reaccionen al cambio de dificultad
		public static void NotifyDifficultyChanged(SubsystemGreenNightSky greenNightSky)
		{
			if (greenNightSky == null) return;
			var project = greenNightSky.Project;
			var creatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(true);
			if (creatureSpawn == null) return;

			var modLoader = ModsManager.ModLoaders.OfType<ShittyCreaturesModLoader>().FirstOrDefault();
			if (modLoader == null) return;

			foreach (ComponentCreature creature in creatureSpawn.Creatures)
			{
				if (creature == null) continue;
				if (!modLoader.IsDifficultyAffectedCreature(creature)) continue;

				var pathBreaker = creature.Entity.FindComponent<ComponentPathBreaker>();
				pathBreaker?.ApplyDifficultyToPathBreaker();
			}

			modLoader.EnforceCombatStatsByDifficulty(project);
		}

		// Método para forzar la configuración de ruptura de bloques según dificultad en TODAS las criaturas
		public void EnforceBlockBreakingByDifficulty(Project project)
		{
			var greenNight = project.FindSubsystem<SubsystemGreenNightSky>(true);
			if (greenNight == null) return;

			DifficultyMode mode = greenNight.DifficultyMode;
			bool shouldBreak = (mode == DifficultyMode.Medium || mode == DifficultyMode.Hard || mode == DifficultyMode.Extreme || mode == DifficultyMode.Impossible);
			float probabilityMultiplier = 1f;
			switch (mode)
			{
				case DifficultyMode.Medium: probabilityMultiplier = 0.3f; break;
				case DifficultyMode.Hard: probabilityMultiplier = 0.7f; break;
				case DifficultyMode.Extreme: probabilityMultiplier = 1.0f; break;
				case DifficultyMode.Impossible: probabilityMultiplier = 1.5f; break;   // Nuevo: más destrucción
				default: shouldBreak = false; break;
			}

			var creatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(true);
			if (creatureSpawn == null) return;

			foreach (ComponentCreature creature in creatureSpawn.Creatures)
			{
				if (creature == null) continue;

				// SOLO aplicar a las criaturas de la lista
				if (!IsDifficultyAffectedCreature(creature)) continue;

				var pathBreaker = creature.Entity.FindComponent<ComponentPathBreaker>();
				if (pathBreaker != null)
				{
					pathBreaker.CanBreakBlocks = shouldBreak;
					if (shouldBreak)
					{
						if (pathBreaker.BreakProbability <= 0f)
							pathBreaker.BreakProbability = 0.5f;
						pathBreaker.BreakProbability = pathBreaker.BreakProbability * probabilityMultiplier;
					}
					else
					{
						pathBreaker.BreakProbability = 0f;
					}
				}
			}
		}

		// Ajusta salud (resistencia), daño y velocidad según dificultad y tipo de criatura
		private void EnforceCombatStatsByDifficulty(Project project)
		{
			var greenNight = project.FindSubsystem<SubsystemGreenNightSky>(true);
			if (greenNight == null) return;

			DifficultyMode mode = greenNight.DifficultyMode;

			float baseResilienceFactor = 1f;
			float baseDamageMult = 1f;
			float baseSpeedMult = 1f;

			float bossResilienceFactor = 1f;
			float bossDamageMult = 1f;
			float bossSpeedMult = 1f;

			float flyingResilienceFactor = 1f;
			float flyingDamageMult = 1f;
			float flyingSpeedMult = 1f;

			// Factores para bandidos (se aplican igual que las criaturas base)
			float banditResilienceFactor = 1f;
			float banditDamageMult = 1f;
			float banditSpeedMult = 1f;

			switch (mode)
			{
				case DifficultyMode.VeryEasy:
					baseResilienceFactor = 0.5f; baseDamageMult = 0.3f; baseSpeedMult = 0.8f;
					bossResilienceFactor = 0.6f; bossDamageMult = 0.4f; bossSpeedMult = 0.9f;
					flyingResilienceFactor = 0.4f; flyingDamageMult = 0.3f; flyingSpeedMult = 0.9f;
					banditResilienceFactor = 0.6f; banditDamageMult = 0.4f; banditSpeedMult = 0.8f;
					break;
				case DifficultyMode.Easy:
					baseResilienceFactor = 0.7f; baseDamageMult = 0.5f; baseSpeedMult = 0.9f;
					bossResilienceFactor = 0.8f; bossDamageMult = 0.6f; bossSpeedMult = 0.95f;
					flyingResilienceFactor = 0.5f; flyingDamageMult = 0.4f; flyingSpeedMult = 1.0f;
					banditResilienceFactor = 0.8f; banditDamageMult = 0.6f; banditSpeedMult = 0.9f;
					break;
				case DifficultyMode.Normal:
					baseResilienceFactor = 1.0f; baseDamageMult = 1.0f; baseSpeedMult = 1.0f;
					bossResilienceFactor = 1.0f; bossDamageMult = 1.0f; bossSpeedMult = 1.0f;
					flyingResilienceFactor = 1.0f; flyingDamageMult = 1.0f; flyingSpeedMult = 1.0f;
					banditResilienceFactor = 1.0f; banditDamageMult = 1.0f; banditSpeedMult = 1.0f;
					break;
				case DifficultyMode.Medium:
					baseResilienceFactor = 1.2f; baseDamageMult = 1.2f; baseSpeedMult = 1.1f;
					bossResilienceFactor = 1.5f; bossDamageMult = 1.4f; bossSpeedMult = 1.05f;
					flyingResilienceFactor = 1.0f; flyingDamageMult = 1.1f; flyingSpeedMult = 1.2f;
					banditResilienceFactor = 1.3f; banditDamageMult = 1.3f; banditSpeedMult = 1.1f;
					break;
				case DifficultyMode.Hard:
					baseResilienceFactor = 1.5f; baseDamageMult = 1.5f; baseSpeedMult = 1.2f;
					bossResilienceFactor = 2.0f; bossDamageMult = 1.8f; bossSpeedMult = 1.1f;
					flyingResilienceFactor = 1.2f; flyingDamageMult = 1.3f; flyingSpeedMult = 1.4f;
					banditResilienceFactor = 1.6f; banditDamageMult = 1.6f; banditSpeedMult = 1.2f;
					break;
				case DifficultyMode.Extreme:
					baseResilienceFactor = 2.0f; baseDamageMult = 2.0f; baseSpeedMult = 1.4f;
					bossResilienceFactor = 3.0f; bossDamageMult = 2.5f; bossSpeedMult = 1.2f;
					flyingResilienceFactor = 1.5f; flyingDamageMult = 1.6f; flyingSpeedMult = 1.6f;
					banditResilienceFactor = 2.0f; banditDamageMult = 2.0f; banditSpeedMult = 1.4f;
					break;
				case DifficultyMode.Impossible:
					baseResilienceFactor = 3.0f; baseDamageMult = 3.0f; baseSpeedMult = 1.8f;
					bossResilienceFactor = 4.0f; bossDamageMult = 3.5f; bossSpeedMult = 1.5f;
					flyingResilienceFactor = 2.0f; flyingDamageMult = 2.5f; flyingSpeedMult = 2.0f;
					banditResilienceFactor = 3.0f; banditDamageMult = 3.0f; banditSpeedMult = 1.8f;
					break;
			}

			var creatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(true);
			if (creatureSpawn == null) return;

			foreach (ComponentCreature creature in creatureSpawn.Creatures)
			{
				if (creature == null) continue;

				string templateName = creature.Entity.ValuesDictionary?.DatabaseObject?.Name;
				if (string.IsNullOrEmpty(templateName)) continue;

				float resilienceFactor = baseResilienceFactor;
				float damageMult = baseDamageMult;
				float speedMult = baseSpeedMult;
				float flySpeedMult = baseSpeedMult;

				bool isFlying = m_flyingTemplates.Contains(templateName);
				bool isBoss = m_bossTemplates.Contains(templateName);
				bool isBandit = m_banditTemplates.Contains(templateName);

				if (isBoss)
				{
					resilienceFactor = bossResilienceFactor;
					damageMult = bossDamageMult;
					speedMult = bossSpeedMult;
					flySpeedMult = bossSpeedMult;
				}
				else if (isFlying)
				{
					resilienceFactor = flyingResilienceFactor;
					damageMult = flyingDamageMult;
					speedMult = 1f;
					flySpeedMult = flyingSpeedMult;
				}
				else if (isBandit)
				{
					resilienceFactor = banditResilienceFactor;
					damageMult = banditDamageMult;
					speedMult = banditSpeedMult;
					flySpeedMult = banditSpeedMult;
				}
				else if (!m_difficultyAffectedCreatures.Contains(templateName))
				{
					continue;
				}

				var baseStats = GetBaseStatsForTemplate(templateName);

				var health = creature.ComponentHealth;
				if (health != null && creature.Entity.FindComponent<ComponentPlayer>() == null)
				{
					health.AttackResilienceFactor = resilienceFactor;
				}

				var miner = creature.Entity.FindComponent<ComponentMiner>();
				if (miner != null && creature.Entity.FindComponent<ComponentPlayer>() == null)
				{
					miner.AttackPower = baseStats.attackPower * damageMult;
				}

				var locomotion = creature.Entity.FindComponent<ComponentLocomotion>();
				if (locomotion != null && creature.Entity.FindComponent<ComponentPlayer>() == null)
				{
					locomotion.WalkSpeed = baseStats.walkSpeed * speedMult;
					if (isFlying)
					{
						locomotion.FlySpeed = baseStats.flySpeed * flySpeedMult;
					}
				}
			}
		}

		private void ApplyDifficultyToSingleCreature(ComponentCreature creature, DifficultyMode mode)
		{
			if (creature == null) return;

			string templateName = creature.Entity.ValuesDictionary?.DatabaseObject?.Name;
			if (string.IsNullOrEmpty(templateName)) return;

			float resilienceFactor = 1f, damageMult = 1f, speedMult = 1f, flySpeedMult = 1f;

			bool isFlying = m_flyingTemplates.Contains(templateName);
			bool isBoss = m_bossTemplates.Contains(templateName);
			bool isBandit = m_banditTemplates.Contains(templateName);

			switch (mode)
			{
				case DifficultyMode.VeryEasy:
					resilienceFactor = isBoss ? 0.6f : (isBandit ? 0.6f : (isFlying ? 0.4f : 0.5f));
					damageMult = isBoss ? 0.4f : (isBandit ? 0.4f : (isFlying ? 0.3f : 0.3f));
					speedMult = isBoss ? 0.9f : (isBandit ? 0.8f : 1f);
					flySpeedMult = isBoss ? 0.9f : (isBandit ? 0.8f : 0.9f);
					break;
				case DifficultyMode.Easy:
					resilienceFactor = isBoss ? 0.8f : (isBandit ? 0.8f : (isFlying ? 0.5f : 0.7f));
					damageMult = isBoss ? 0.6f : (isBandit ? 0.6f : (isFlying ? 0.4f : 0.5f));
					speedMult = isBoss ? 0.95f : (isBandit ? 0.9f : 1f);
					flySpeedMult = isBoss ? 0.95f : (isBandit ? 0.9f : 1.0f);
					break;
				case DifficultyMode.Normal:
					resilienceFactor = isBoss ? 1.0f : (isBandit ? 1.0f : (isFlying ? 1.0f : 1.0f));
					damageMult = isBoss ? 1.0f : (isBandit ? 1.0f : (isFlying ? 1.0f : 1.0f));
					speedMult = isBoss ? 1.0f : (isBandit ? 1.0f : 1f);
					flySpeedMult = isBoss ? 1.0f : (isBandit ? 1.0f : 1.0f);
					break;
				case DifficultyMode.Medium:
					resilienceFactor = isBoss ? 1.5f : (isBandit ? 1.3f : (isFlying ? 1.0f : 1.2f));
					damageMult = isBoss ? 1.4f : (isBandit ? 1.3f : (isFlying ? 1.1f : 1.2f));
					speedMult = isBoss ? 1.05f : (isBandit ? 1.1f : 1f);
					flySpeedMult = isBoss ? 1.05f : (isBandit ? 1.1f : 1.2f);
					break;
				case DifficultyMode.Hard:
					resilienceFactor = isBoss ? 2.0f : (isBandit ? 1.6f : (isFlying ? 1.2f : 1.5f));
					damageMult = isBoss ? 1.8f : (isBandit ? 1.6f : (isFlying ? 1.3f : 1.5f));
					speedMult = isBoss ? 1.1f : (isBandit ? 1.2f : 1f);
					flySpeedMult = isBoss ? 1.1f : (isBandit ? 1.2f : 1.4f);
					break;
				case DifficultyMode.Extreme:
					resilienceFactor = isBoss ? 3.0f : (isBandit ? 2.0f : (isFlying ? 1.5f : 2.0f));
					damageMult = isBoss ? 2.5f : (isBandit ? 2.0f : (isFlying ? 1.6f : 2.0f));
					speedMult = isBoss ? 1.2f : (isBandit ? 1.4f : 1f);
					flySpeedMult = isBoss ? 1.2f : (isBandit ? 1.4f : 1.6f);
					break;
				case DifficultyMode.Impossible:
					resilienceFactor = isBoss ? 4.0f : (isBandit ? 3.0f : (isFlying ? 2.0f : 3.0f));
					damageMult = isBoss ? 3.5f : (isBandit ? 3.0f : (isFlying ? 2.5f : 3.0f));
					speedMult = isBoss ? 1.5f : (isBandit ? 1.8f : 1f);
					flySpeedMult = isBoss ? 1.5f : (isBandit ? 1.8f : 2.0f);
					break;
				default:
					return;
			}

			var baseStats = GetBaseStatsForTemplate(templateName);

			var health = creature.ComponentHealth;
			if (health != null && creature.Entity.FindComponent<ComponentPlayer>() == null)
			{
				health.AttackResilienceFactor = resilienceFactor;
			}

			var miner = creature.Entity.FindComponent<ComponentMiner>();
			if (miner != null && creature.Entity.FindComponent<ComponentPlayer>() == null)
			{
				miner.AttackPower = baseStats.attackPower * damageMult;
			}

			var locomotion = creature.Entity.FindComponent<ComponentLocomotion>();
			if (locomotion != null && creature.Entity.FindComponent<ComponentPlayer>() == null)
			{
				locomotion.WalkSpeed = baseStats.walkSpeed * speedMult;
				if (isFlying)
				{
					locomotion.FlySpeed = baseStats.flySpeed * flySpeedMult;
				}
			}
		}

		// Lista de nombres de plantillas que se ven afectadas por los cambios de dificultad (genérico)
		private static readonly HashSet<string> m_difficultyAffectedCreatures = new HashSet<string>
{
	"InfectedNormal1",
	"InfectedNormal2",
	"InfectedMuscle1",
	"InfectedMuscle2",
	"GhostNormal",
	"GhostFast",
	"Boomer1",
	"Boomer2",
	"Boomer3",
	"GhostBoomer1",
	"GhostBoomer2",
	"GhostBoomer3",
	"InfectedFast1",
	"InfectedFast2",
	"PoisonousInfected1",
	"PoisonousInfected2",
	"PoisonousGhost",
	"InfectedBear",
	"InfectedWildboar",
	"PredatoryChameleon",
	"InfectedFreezer",
	"HumanoidSkeleton",
	"Charger1",
	"Charger2",
	"GhostCharger",
	"InfectedHyena",
	"InfectedWerewolf",
	"InfectedWolf",
	"InfectedSpider"
};

		// ===== NUEVOS CONJUNTOS SEPARADOS =====
		private static readonly HashSet<string> m_bossTemplates = new HashSet<string>
{
	"Tank1",
	"Tank2",
	"Tank3",
	"TankGhost1",
	"TankGhost2",
	"TankGhost3",
	"MachineGunInfected",
	"FlyingInfectedBoss",
	"FrozenTank",
	"FrozenTankGhost"
};

		private static readonly HashSet<string> m_flyingTemplates = new HashSet<string>
{
	"InfectedFly1",
	"InfectedFly2",
	"InfectedFly3",
	"InfectedBird",
	"FlyingInfectedBoss"  // También es jefe, prioridad jefe
};

		private bool IsDifficultyAffectedCreature(ComponentCreature creature)
		{
			if (creature == null || creature.Entity == null) return false;
			string templateName = creature.Entity.ValuesDictionary?.DatabaseObject?.Name;
			if (string.IsNullOrEmpty(templateName)) return false;

			// Verificar si está en alguna de las listas (incluyendo bandidos)
			return m_difficultyAffectedCreatures.Contains(templateName) ||
				   m_bossTemplates.Contains(templateName) ||
				   m_flyingTemplates.Contains(templateName) ||
				   m_banditTemplates.Contains(templateName);
		}

		public override void OnProjectDisposed()
		{
			// Desuscribirse de eventos de celebración
			AchievementsManager.OnCelebrationStarted -= OnCelebrationStarted;
			AchievementsManager.OnCelebrationEnded -= OnCelebrationEnded;

			// Limpiar caché al descargar el mundo
			m_baseStatsCache.Clear();

			// Restaurar comportamiento de criaturas
			RestoreCreaturesBehavior();
		}

		private void OnCelebrationStarted()
		{
			m_celebrationActive = true;
			var project = m_subsystemGreenNightSky?.Project;
			if (project == null) return;

			// Aplicar invulnerabilidad a todas las entidades existentes (guardando estado)
			ApplyInvulnerability(project, true);

			// Asegurar que cualquier entidad nueva también sea invulnerable (ya guarda estado)
			// Esto se llama periódicamente en BeforeWidgetUpdate, pero lo dejamos aquí por si acaso.
			EnsureCelebrationInvulnerability(project);

			var creatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(true);
			if (creatureSpawn == null) return;

			m_originalSuppressedState.Clear();

			foreach (ComponentCreature creature in creatureSpawn.Creatures)
			{
				if (creature == null || creature.Entity.FindComponent<ComponentPlayer>() != null)
					continue;

				// 1. ComponentNewChaseBehavior (propio)
				var chaseNew = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
				if (chaseNew != null)
				{
					m_originalSuppressedState[creature] = chaseNew.Suppressed;
					chaseNew.Suppressed = true;
					chaseNew.StopAttack();
				}

				// 2. ComponentZombieChaseBehavior
				var chaseZombie = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
				if (chaseZombie != null)
				{
					m_originalSuppressedState[creature] = chaseZombie.Suppressed;
					chaseZombie.Suppressed = true;
					chaseZombie.StopAttack();
				}

				// 3. ComponentBanditChaseBehavior
				var chaseBandit = creature.Entity.FindComponent<ComponentBanditChaseBehavior>();
				if (chaseBandit != null)
				{
					m_originalSuppressedState[creature] = chaseBandit.Suppressed;
					chaseBandit.Suppressed = true;
					chaseBandit.StopAttack();
				}

				// 4. ComponentChaseBehavior (base, por si alguna criatura lo usa directamente)
				var chaseBase = creature.Entity.FindComponent<ComponentChaseBehavior>();
				if (chaseBase != null && !m_originalSuppressedState.ContainsKey(creature))
				{
					// Guardar estado original (no tenemos acceso directo a Suppressed? En ComponentChaseBehavior es público)
					m_originalSuppressedState[creature] = chaseBase.Suppressed;
					chaseBase.Suppressed = true;
					chaseBase.StopAttack();
				}

				// Detener pathfinding para evitar movimiento aleatorio
				var pathfinding = creature.Entity.FindComponent<ComponentPathfinding>();
				if (pathfinding != null)
					pathfinding.Stop();
			}
		}

		private void OnCelebrationEnded()
		{
			m_celebrationActive = false;

			var project = m_subsystemGreenNightSky?.Project;
			if (project != null)
			{
				// Restaurar invulnerabilidad a su estado original
				ApplyInvulnerability(project, false);
				// Restaurar comportamientos de persecución
				RestoreCreaturesBehavior();
			}
		}

		private void RestoreCreaturesBehavior()
		{
			if (m_subsystemGreenNightSky?.Project == null) return;
			var creatureSpawn = m_subsystemGreenNightSky.Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			if (creatureSpawn == null) return;

			foreach (var kvp in m_originalSuppressedState)
			{
				ComponentCreature creature = kvp.Key;
				if (creature == null || creature.Entity == null) continue;

				var chaseNew = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
				if (chaseNew != null)
					chaseNew.Suppressed = kvp.Value;

				var chaseZombie = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
				if (chaseZombie != null)
					chaseZombie.Suppressed = kvp.Value;

				var chaseBandit = creature.Entity.FindComponent<ComponentBanditChaseBehavior>();
				if (chaseBandit != null)
					chaseBandit.Suppressed = kvp.Value;

				var chaseBase = creature.Entity.FindComponent<ComponentChaseBehavior>();
				if (chaseBase != null)
					chaseBase.Suppressed = kvp.Value;
			}
			m_originalSuppressedState.Clear();
		}

		private void MakeCreaturesDance(GameWidget gameWidget)
		{
			var project = gameWidget.PlayerData?.SubsystemPlayers?.Project;
			if (project == null) return;

			var creatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(true);
			if (creatureSpawn == null) return;

			float time = (float)Time.RealTime;
			float danceSpeed = 1.2f;      // Velocidad de giro
			float moveSpeed = 1.5f;       // Velocidad de movimiento
			float jumpInterval = 1.5f;    // Saltos cada ~1.5 segundos

			// ===== LISTAS EXPLÍCITAS (TÚ LAS COMPLETAS MANUALMENTE) =====
			HashSet<string> flyingTemplates = new HashSet<string>
	{
		"InfectedFly1",
		"InfectedFly2",
		"InfectedFly3",
		"InfectedBird",
		"FlyingInfectedBoss",
		"Seagull",
		"Raven",
		"Sparrow",
		"Duck",
		"Pigeon"
	};

			HashSet<string> aquaticTemplates = new HashSet<string>
	{
		"Shark_GreatWhite",
		"Bass_Sea",
		"Ray_Brown",
		"Ray_Yellow",
		"Piranha",
		"Shark_Tiger",
		"Shark_Bull",
		"Orca",
		"Barracuda",
		"Beluga",
		"Bass_Freshwater"
	};

			foreach (ComponentCreature creature in creatureSpawn.Creatures)
			{
				if (creature == null || creature.Entity.FindComponent<ComponentPlayer>() != null)
					continue;

				var locomotion = creature.ComponentLocomotion;
				if (locomotion == null) continue;

				string templateName = creature.Entity.ValuesDictionary?.DatabaseObject?.Name ?? "";

				bool isFlyer = flyingTemplates.Contains(templateName);
				bool isAquatic = aquaticTemplates.Contains(templateName);

				// Ángulo de baile
				float angle = time * 1.2f;

				// 1. Giro rítmico
				locomotion.TurnOrder = new Vector2(danceSpeed * MathF.Sin(time * 2f), 0f);

				// 2. Mirada arriba/abajo (ondulación)
				float lookY = MathF.Sin(time * 3f) * 0.6f;
				locomotion.LookOrder = new Vector2(0f, lookY);

				// 3. Movimiento según el tipo
				if (isFlyer)
				{
					// Voladores: vuelo circular + oscilación vertical (NO saltan)
					float flyX = MathF.Cos(angle);
					float flyZ = MathF.Sin(angle);
					float flyY = MathF.Sin(angle * 2f) * 0.5f;
					locomotion.FlyOrder = new Vector3(flyX, flyY, flyZ);
					locomotion.WalkOrder = null;
					locomotion.SwimOrder = null;
					locomotion.JumpOrder = 0f;   // NUNCA saltan
				}
				else if (isAquatic)
				{
					// Acuáticos: nadan en círculo (pueden saltar si están fuera del agua)
					float swimX = MathF.Cos(angle);
					float swimZ = MathF.Sin(angle);
					locomotion.SwimOrder = new Vector3(swimX, 0f, swimZ);
					locomotion.WalkOrder = null;
					locomotion.FlyOrder = null;

					// Salto periódico (igual que terrestres)
					if (Math.Floor(time / jumpInterval) % 2 == 0)
						locomotion.JumpOrder = 0.8f;
					else
						locomotion.JumpOrder = 0f;
				}
				else
				{
					// Terrestres: caminan en círculo + saltos
					float walkX = MathF.Cos(angle);
					float walkZ = MathF.Sin(angle);
					locomotion.WalkOrder = new Vector2(walkX, walkZ);
					locomotion.FlyOrder = null;
					locomotion.SwimOrder = null;

					if (Math.Floor(time / jumpInterval) % 2 == 0)
						locomotion.JumpOrder = 0.8f;
					else
						locomotion.JumpOrder = 0f;
				}

				// 4. Desactivar cualquier ataque (por si acaso)
				var creatureModel = creature.ComponentCreatureModel;
				if (creatureModel != null)
					creatureModel.AttackOrder = false;
			}
		}

		public override void OnChaseBehaviorAttacked(ComponentChaseBehavior chaseBehavior, float chaseTimeBefore, ref float chaseTime, ref bool bodyToHit, ref bool playAttackSound)
		{
			if (m_celebrationActive)
			{
				// Cancelar el golpe por completo
				bodyToHit = false;
				playAttackSound = false;
				// Reducir tiempo de persecución para que se detenga pronto
				chaseTime = Math.Min(chaseTime, 0.1f);
			}
		}

		public override void ScoreMount(ComponentRider rider, ComponentMount mount, out float? score)
		{
			score = null;

			// Solo interesar si el jinete es un jugador
			ComponentPlayer player = rider.Entity.FindComponent<ComponentPlayer>();
			if (player == null) return;

			// Verificar si la montura es un ComponentZombieMount
			ComponentZombieMount zombieMount = mount as ComponentZombieMount;
			if (zombieMount == null)
				zombieMount = mount.Entity.FindComponent<ComponentZombieMount>();

			if (zombieMount != null && !zombieMount.CanPlayersRide)
			{
				// Puntuación negativa para que no sea seleccionada
				score = -1f;
				// NO mostrar mensaje aquí
			}
		}

		private ComponentMount FindNearestUnrideableZombieMount(ComponentPlayer player)
		{
			if (player == null || player.Project == null) return null;
			var bodiesSubsystem = player.Project.FindSubsystem<SubsystemBodies>(true);
			if (bodiesSubsystem == null) return null;

			Vector3 center = player.ComponentBody.Position;
			float radius = 4f;
			DynamicArray<ComponentBody> bodies = new DynamicArray<ComponentBody>();
			bodiesSubsystem.FindBodiesAroundPoint(new Vector2(center.X, center.Z), radius, bodies);

			ComponentMount bestMount = null;
			float bestDistSq = radius * radius;

			for (int i = 0; i < bodies.Count; i++)
			{
				ComponentBody body = bodies.Array[i];
				if (body?.Entity == null) continue;

				ComponentZombieMount zombieMount = body.Entity.FindComponent<ComponentZombieMount>();
				if (zombieMount == null) continue;

				if (!zombieMount.CanPlayersRide)
				{
					float distSq = Vector3.DistanceSquared(center, body.Position);
					if (distSq < bestDistSq)
					{
						bestDistSq = distSq;
						bestMount = zombieMount;
					}
				}
			}
			return bestMount;
		}

		private void ProcessHit(ComponentPlayer player, ComponentBody hitBody, Vector3 hitPoint, Vector3 hitDirection, ref double timeIntervalHit)
		{
			if (player == null || hitBody == null) return;

			ComponentCreature targetCreature = hitBody.Entity.FindComponent<ComponentCreature>();
			if (targetCreature == null || targetCreature.ComponentHealth.Health <= 0f) return;

			// Lógica de "PunchCommandEnabled" (aliados atacan)
			if (ShittyCreaturesSettingsManager.PunchCommandEnabled)
			{
				Action<Injury> handler = null;
				handler = (Injury injury) =>
				{
					if (injury.Attacker == player)
					{
						CommandAlliesToAttack(player, targetCreature);
						targetCreature.ComponentHealth.Injured -= handler;
					}
				};
				targetCreature.ComponentHealth.Injured += handler;
			}

			// Lógica de golpes rápidos (FastMeleeEnabled)
			if (ShittyCreaturesSettingsManager.FastMeleeEnabled)
			{
				double currentGameTime = player.Project.FindSubsystem<SubsystemTime>(true).GameTime;
				bool isFastHit = false;

				if (m_lastHitTarget.TryGetValue(player, out ComponentBody lastBody) &&
					lastBody == hitBody &&
					(currentGameTime - m_lastHitGameTime.GetValueOrDefault(player)) < 0.3)
				{
					isFastHit = true;
				}

				m_lastHitGameTime[player] = currentGameTime;
				m_lastHitTarget[player] = hitBody;
				m_fastHitMode[player] = isFastHit;

				if (isFastHit)
				{
					timeIntervalHit = 0.1;
				}
				else
				{
					m_fastHitMode[player] = false;
				}
			}
			else
			{
				m_fastHitMode[player] = false;
			}

			// Finalmente, ejecutar el golpe
			player.ComponentMiner.Hit(hitBody, hitPoint, hitDirection);
		}

		public override void OnEatPickable(ComponentEatPickableBehavior behavior, Pickable pickable, out bool result)
		{
			result = false;

			ComponentCreature creature = behavior.m_componentCreature;
			if (creature == null) return;

			// Verificar que sea Aimep3
			string templateName = creature.Entity.ValuesDictionary?.DatabaseObject?.Name;
			if (templateName != "Aimep3") return;

			// Verificar que sea pan
			int contents = Terrain.ExtractContents(pickable.Value);
			if (contents != BreadBlock.Index) return;

			// Obtener el componente de evolución
			ComponentAimep3Evolution evolution = creature.Entity.FindComponent<ComponentAimep3Evolution>();
			if (evolution == null) return;

			// Intentar evolucionar
			bool evolved = evolution.TryEvolve();

			if (evolved)
			{
				result = true; // Marcar que ya se procesó
			}
		}

		private void ApplyInvulnerability(Project project, bool makeInvulnerable)
		{
			var creatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(true);
			if (creatureSpawn != null)
			{
				foreach (var creature in creatureSpawn.Creatures)
				{
					if (creature?.ComponentHealth != null)
					{
						if (makeInvulnerable)
						{
							if (!m_originalInvulnerableState.ContainsKey(creature.ComponentHealth))
								m_originalInvulnerableState[creature.ComponentHealth] = creature.ComponentHealth.IsInvulnerable;
							creature.ComponentHealth.IsInvulnerable = true;
						}
						else
						{
							if (m_originalInvulnerableState.TryGetValue(creature.ComponentHealth, out bool originalState))
								creature.ComponentHealth.IsInvulnerable = originalState;
							// Si no tiene estado guardado, NO lo modificamos (mantiene el que tenía)
						}
					}
				}
			}

			var players = project.FindSubsystem<SubsystemPlayers>(true);
			if (players != null)
			{
				foreach (var player in players.ComponentPlayers)
				{
					if (player?.ComponentHealth != null)
					{
						if (makeInvulnerable)
						{
							if (!m_originalInvulnerableState.ContainsKey(player.ComponentHealth))
								m_originalInvulnerableState[player.ComponentHealth] = player.ComponentHealth.IsInvulnerable;
							player.ComponentHealth.IsInvulnerable = true;
						}
						else
						{
							if (m_originalInvulnerableState.TryGetValue(player.ComponentHealth, out bool originalState))
								player.ComponentHealth.IsInvulnerable = originalState;
							// Si no tiene estado guardado, NO lo modificamos
						}
					}
				}
			}

			// Limpiar el diccionario SOLO después de restaurar (makeInvulnerable == false)
			if (!makeInvulnerable)
			{
				m_originalInvulnerableState.Clear();
			}
		}

		// Asegura que criaturas o jugadores que aparezcan DURANTE la celebración también sean inmortales
		private void EnsureCelebrationInvulnerability(Project project)
		{
			if (project == null) return;

			var creatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(true);
			if (creatureSpawn != null)
			{
				foreach (var creature in creatureSpawn.Creatures)
				{
					if (creature?.ComponentHealth != null && !creature.ComponentHealth.IsInvulnerable)
					{
						// Guardar estado original si no existe
						if (!m_originalInvulnerableState.ContainsKey(creature.ComponentHealth))
							m_originalInvulnerableState[creature.ComponentHealth] = creature.ComponentHealth.IsInvulnerable;
						creature.ComponentHealth.IsInvulnerable = true;
					}
				}
			}

			var players = project.FindSubsystem<SubsystemPlayers>(true);
			if (players != null)
			{
				foreach (var player in players.ComponentPlayers)
				{
					if (player?.ComponentHealth != null && !player.ComponentHealth.IsInvulnerable)
					{
						if (!m_originalInvulnerableState.ContainsKey(player.ComponentHealth))
							m_originalInvulnerableState[player.ComponentHealth] = player.ComponentHealth.IsInvulnerable;
						player.ComponentHealth.IsInvulnerable = true;
					}
				}
			}
		}

		// ===== MÉTODO PÚBLICO PARA CONSULTAR MODO IMPOSSIBLE ACTIVO =====
		public static bool IsImpossibleModeActive(Project project)
		{
			if (project == null) return false;
			var greenNight = project.FindSubsystem<SubsystemGreenNightSky>(true);
			if (greenNight == null) return false;
			if (greenNight.DifficultyMode != DifficultyMode.Impossible) return false;
			if (!greenNight.IsGreenNightActive) return false;

			// Si ya se completaron todas las oleadas, se desactiva el bloqueo
			var zombiesSpawn = project.FindSubsystem<SubsystemZombiesSpawn>(true);
			if (zombiesSpawn != null && zombiesSpawn.IsAllWavesCompleted) return false;

			return true;
		}

		private void OnEntityAddedToProject(Entity entity)
		{
			// Obtener templateName UNA SOLA VEZ al inicio
			string templateName = entity.ValuesDictionary?.DatabaseObject?.Name;

			var creature = entity.FindComponent<ComponentCreature>();

			// ===== NUEVO: Detectar La Bandida y aplicar armadura antibalas =====
			if (templateName == "LaBandida")
			{
				ApplyBulletproofArmorToLaBandida(entity);
			}

			if (creature != null && IsDifficultyAffectedCreature(creature))
			{
				var project = creature.Project;
				if (project != null)
				{
					var greenNight = project.FindSubsystem<SubsystemGreenNightSky>(true);
					if (greenNight != null)
					{
						ApplyDifficultyToSingleCreature(creature, greenNight.DifficultyMode);
					}
				}
			}

			// Asignar ropa/armadura a criaturas que tengan ComponentCreatureClothing
			if (creature != null)
			{
				if (!string.IsNullOrEmpty(templateName))
				{
					var project = creature.Project;
					if (project != null)
					{
						var greenNight = project.FindSubsystem<SubsystemGreenNightSky>(true);
						if (greenNight != null)
						{
							DifficultyMode difficulty = greenNight.DifficultyMode;

							// Para infectados: ropa normal en todas las dificultades, armadura en altas
							if (m_infectedWithClothes.Contains(templateName))
							{
								AssignClothesToCreature(creature, difficulty);
							}
							// Para bandidos: SOLO ARMADURA en dificultades altas, NADA en bajas
							// EXCLUIR a LaBandida porque ya tiene su armadura antibalas fija
							else if (m_banditTemplates.Contains(templateName) && templateName != "LaBandida")
							{
								AssignBanditClothes(creature, difficulty);
							}
						}
					}
				}
			}
		}

		// Manejador para el evento estático Project.EntityAdded
		private void Project_EntityAdded(object sender, EntityAddRemoveEventArgs e)
		{
			if (e?.Entity != null)
			{
				// Suscribirse al evento de instancia de esa entidad
				e.Entity.EntityAdded += Entity_EntityAdded;
				// Procesar la entidad recién añadida
				OnEntityAddedToProject(e.Entity);
			}
		}

		// Manejador para el evento de instancia Entity.EntityAdded
		private void Entity_EntityAdded(object sender, EventArgs e)
		{
			if (sender is Entity entity)
			{
				OnEntityAddedToProject(entity);
			}
		}

		private void AssignClothesToCreature(ComponentCreature creature, DifficultyMode difficulty)
		{
			if (creature == null) return;
			var clothing = creature.Entity.FindComponent<ComponentCreatureClothing>();
			if (clothing == null) return;

			// No sobreescribir si ya tiene ropa
			bool hasClothes = false;
			foreach (var slot in new[] { ClothingSlot.Head, ClothingSlot.Torso, ClothingSlot.Legs })
			{
				if (clothing.GetClothes(slot).Count > 0) { hasClothes = true; break; }
			}
			if (hasClothes) return;

			var clothingBlock = BlocksManager.GetBlock<ClothingBlock>();
			if (clothingBlock == null) return;

			var rand = new Random();

			// Probabilidades fijas (NO dependen de la dificultad)
			// - Media: sin ropa (~35%)
			// - Media: ropa incompleta (1-2 piezas) (~50%)
			// - Baja: ropa completa (3 piezas) (~15%)
			float r = rand.Float();
			int numPieces;
			if (r < 0.35f)
				numPieces = 0;           // Sin ropa
			else if (r < 0.85f)
				numPieces = 1 + rand.Int(0, 1); // 1 o 2 piezas
			else
				numPieces = 3;           // Ropa completa

			if (numPieces == 0) return;

			// Seleccionar aleatoriamente qué slots se llenarán
			var slots = new List<ClothingSlot> { ClothingSlot.Head, ClothingSlot.Torso, ClothingSlot.Legs };
			for (int i = slots.Count - 1; i > 0; i--)
			{
				int j = rand.Int(0, i);
				var temp = slots[i];
				slots[i] = slots[j];
				slots[j] = temp;
			}
			var selectedSlots = slots.Take(numPieces).ToList();

			// Decidir si se usan armaduras (solo en dificultades >= Medium)
			bool useArmor = false;
			if (difficulty >= DifficultyMode.Medium)
			{
				float armorChance = 0f;
				switch (difficulty)
				{
					case DifficultyMode.Medium: armorChance = 0.3f; break;
					case DifficultyMode.Hard: armorChance = 0.5f; break;
					case DifficultyMode.Extreme: armorChance = 0.7f; break;
					case DifficultyMode.Impossible: armorChance = 0.9f; break;
					default: armorChance = 0f; break;
				}
				useArmor = rand.Float() < armorChance;
			}

			// Conjunto de prendas disponibles
			HashSet<int> availableClothes = useArmor ? m_highTierClothes : m_lowTierClothes;
			if (availableClothes.Count == 0) availableClothes = m_lowTierClothes;

			// Asignar una prenda a cada slot seleccionado
			foreach (var slot in selectedSlots)
			{
				int index = availableClothes.ElementAt(rand.Int(0, availableClothes.Count - 1));
				int value = MakeClothingValue(clothingBlock, index);
				clothing.SetClothes(slot, new[] { value });
			}
		}

		private void AssignBanditClothes(ComponentCreature creature, DifficultyMode difficulty)
		{
			if (creature == null) return;
			var clothing = creature.Entity.FindComponent<ComponentCreatureClothing>();
			if (clothing == null) return;

			// ============================================================
			// SIEMPRE LIMPIAR TODA LA ROPA DE LOS SLOTS PRINCIPALES
			// ============================================================
			foreach (var slot in new[] { ClothingSlot.Head, ClothingSlot.Torso, ClothingSlot.Legs })
			{
				if (clothing.GetClothes(slot).Count > 0)
				{
					clothing.SetClothes(slot, new List<int>());
				}
			}

			// ============================================================
			// EN DIFICULTADES BAJAS: NO ASIGNAR NADA (YA LIMPIO)
			// ============================================================
			if (difficulty < DifficultyMode.Medium)
			{
				return;
			}

			// ============================================================
			// EN DIFICULTADES ALTAS: INTENTAR ASIGNAR ARMADURAS
			// ============================================================

			var clothingBlock = BlocksManager.GetBlock<ClothingBlock>();
			if (clothingBlock == null) return;

			var rand = new Random();

			// Probabilidad de tener armadura según dificultad
			float armorChance = 0f;
			switch (difficulty)
			{
				case DifficultyMode.Medium: armorChance = 0.3f; break;
				case DifficultyMode.Hard: armorChance = 0.5f; break;
				case DifficultyMode.Extreme: armorChance = 0.7f; break;
				case DifficultyMode.Impossible: armorChance = 0.9f; break;
				default: return;
			}

			// Si NO sale armadura, ya se limpió todo, así que queda sin ropa
			if (rand.Float() >= armorChance)
			{
				return;
			}

			// Decidir cuántas piezas de armadura
			float r = rand.Float();
			int numPieces;
			if (r < 0.2f)      // 20% 1 pieza
				numPieces = 1;
			else if (r < 0.6f) // 40% 2 piezas
				numPieces = 2;
			else               // 40% 3 piezas (completo)
				numPieces = 3;

			// Seleccionar slots aleatorios
			var slots = new List<ClothingSlot> { ClothingSlot.Head, ClothingSlot.Torso, ClothingSlot.Legs };
			for (int i = slots.Count - 1; i > 0; i--)
			{
				int j = rand.Int(0, i);
				var temp = slots[i];
				slots[i] = slots[j];
				slots[j] = temp;
			}
			var selectedSlots = slots.Take(numPieces).ToList();

			// ============================================================
			// USAR SOLO EL HASHSET DE ARMADURAS PARA BANDIDOS
			// NUNCA ROPA NORMAL
			// ============================================================
			var availableClothes = m_banditArmorClothes;
			if (availableClothes.Count == 0) return;

			// Asignar SOLO ARMADURAS a los slots seleccionados
			foreach (var slot in selectedSlots)
			{
				int index = availableClothes.ElementAt(rand.Int(0, availableClothes.Count - 1));
				int value = MakeClothingValue(clothingBlock, index);
				clothing.SetClothes(slot, new[] { value });
			}
		}

		private int MakeClothingValue(ClothingBlock clothingBlock, int clothingIndex)
		{
			int data = ClothingBlock.SetClothingIndex(0, clothingIndex);
			return Terrain.MakeBlockValue(clothingBlock.BlockIndex, 0, data);
		}

		private void ApplyBulletproofArmorToLaBandida(Entity entity)
		{
			ComponentCreatureClothing clothing = entity.FindComponent<ComponentCreatureClothing>();
			if (clothing == null) return;

			ClothingBlock clothingBlock = BlocksManager.GetBlock<ClothingBlock>();
			if (clothingBlock == null) return;

			// Índices de armadura antibalas: 51, 52 53, 54
			int headValue = MakeClothingValue(clothingBlock, 51);
			int torsoValue = MakeClothingValue(clothingBlock, 52);
			int legsValue = MakeClothingValue(clothingBlock, 53);
			int feetValue = MakeClothingValue(clothingBlock, 54);

			clothing.SetClothes(ClothingSlot.Head, new[] { headValue });
			clothing.SetClothes(ClothingSlot.Torso, new[] { torsoValue });
			clothing.SetClothes(ClothingSlot.Legs, new[] { legsValue });
			clothing.SetClothes(ClothingSlot.Feet, new[] { feetValue });
		}

		public override void ProcessAttackment(Attackment attackment)
		{
			if (attackment?.Target == null || attackment.AttackPower <= 0f)
				return;

			ComponentCreatureClothing creatureClothing = attackment.Target.FindComponent<ComponentCreatureClothing>();
			if (creatureClothing != null)
			{
				float originalPower = attackment.AttackPower;

				// Aplicar protección ANTES de que Attackment calcule el daño
				creatureClothing.ApplyArmorProtection(attackment);

				// Verificar que el ataque provenga de una criatura (no caídas, explosiones, etc.)
				ComponentCreature attackerCreature = attackment.Attacker?.FindComponent<ComponentCreature>();
				if (attackerCreature == null)
					return;

				// Si el daño se redujo (incluso parcialmente), forzar reacción
				if (attackment.AttackPower < originalPower)
				{
					ComponentCreature targetCreature = attackment.Target.FindComponent<ComponentCreature>();
					if (targetCreature != null && targetCreature.ComponentHealth != null)
					{
						// Crear un Injury con daño 0 para simular el impacto
						Injury zeroInjury = new AttackInjury(0f, attackment);
						targetCreature.ComponentHealth.Injured?.Invoke(zeroInjury);
					}
				}
			}
		}

		public static bool ShouldIgnoreBodyForFriendlyFire(ComponentCreature owner, ComponentBody targetBody)
		{
			if (owner == null || targetBody == null) return false;
			if (targetBody.Entity == owner.Entity) return true; // no te dañas a ti mismo

			// --- Lógica idéntica a OnProjectileRaycastBody ---

			// 1. Manada del jugador (ComponentNewHerdBehavior)
			bool isOwnerPlayerHerd = false;
			if (owner is ComponentPlayer)
			{
				isOwnerPlayerHerd = true;
			}
			else
			{
				var ownerHerd = owner.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (ownerHerd != null && (ownerHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
										  ownerHerd.HerdName.ToLower().Contains("guardian")))
				{
					isOwnerPlayerHerd = true;
				}
			}

			if (isOwnerPlayerHerd)
			{
				// Si el objetivo es un jugador, es amigo
				if (targetBody.Entity.FindComponent<ComponentPlayer>() != null) return true;
				// Si pertenece a la misma manada o es guardián
				var targetHerd = targetBody.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (targetHerd != null && targetHerd.IsSameHerdOrGuardian(owner)) return true;
				return false; // no es amigo
			}

			// 2. Manadas de zombis (ComponentZombieHerdBehavior)
			var ownerZombieHerd = owner.Entity.FindComponent<ComponentZombieHerdBehavior>();
			if (ownerZombieHerd != null)
			{
				var targetZombieHerd = targetBody.Entity.FindComponent<ComponentZombieHerdBehavior>();
				if (targetZombieHerd != null && ownerZombieHerd.IsSameZombieHerd(owner))
				{
					return true;
				}
			}

			// 3. Manadas originales "Alianza", "Muerte", "Pirate" (ComponentHerdBehavior)
			var ownerOriginalHerd = owner.Entity.FindComponent<ComponentHerdBehavior>();
			if (ownerOriginalHerd != null)
			{
				string ownerHerdName = ownerOriginalHerd.HerdName;
				if (ownerHerdName == "Alianza" || ownerHerdName == "Muerte" || ownerHerdName == "Pirate")
				{
					var targetOriginalHerd = targetBody.Entity.FindComponent<ComponentHerdBehavior>();
					if (targetOriginalHerd != null && targetOriginalHerd.HerdName == ownerHerdName)
					{
						return true;
					}
				}
			}

			// 4. Bandidos (ComponentBanditHerdBehavior)
			var ownerBanditHerd = owner.Entity.FindComponent<ComponentBanditHerdBehavior>();
			if (ownerBanditHerd != null)
			{
				var targetBanditHerd = targetBody.Entity.FindComponent<ComponentBanditHerdBehavior>();
				if (targetBanditHerd != null &&
					!string.IsNullOrEmpty(ownerBanditHerd.HerdName) &&
					string.Equals(ownerBanditHerd.HerdName, targetBanditHerd.HerdName, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		public override void OnVitalStatsUpdateTemperature(
	ComponentVitalStats vitalStats,
	ref float temperature,
	ref float gameTimeDelta,
	ref bool skipVanilla)
		{
			// Solo aplicar en modos de supervivencia
			var gameInfo = vitalStats.Project.FindSubsystem<SubsystemGameInfo>(true);
			if (gameInfo.WorldSettings.GameMode == GameMode.Creative)
				return;

			ComponentPlayer player = vitalStats.m_componentPlayer;
			if (player == null || player.ComponentBody == null)
				return;

			Vector3 pos = player.ComponentBody.Position;
			int range = AirConditionerManager.GetMaxCoverageRangeAt(pos.X, pos.Y, pos.Z);

			if (range > 0)
			{
				// Calentar al jugador (rango 0-15 → 12°C a 24°C)
				float heatFactor = range / 15f; // 0.0 a 1.0
				float targetTemp = 12f + heatFactor * 12f;

				float speed = 2.0f; // grados por segundo
				float diff = targetTemp - temperature;
				float maxChange = speed * gameTimeDelta;

				if (Math.Abs(diff) < maxChange)
					temperature = targetTemp;
				else
					temperature += Math.Sign(diff) * maxChange;

				// ★ ASIGNAR DIRECTAMENTE AL COMPONENTE PARA QUE PERSISTA ★
				vitalStats.Temperature = temperature;

				// Anular la lógica vanilla para que no sobrescriba
				skipVanilla = true;
			}
			// Si no hay cobertura, no intervenimos (dejamos que el juego maneje la temperatura)
		}

		// ---------------------------------------------------------------------------------
		// SaveSettings / LoadSettings (heredados de ChaseMusicModLoader, vacíos)
		// ---------------------------------------------------------------------------------
		public override void SaveSettings(XElement xElement) { }
		public override void LoadSettings(XElement xElement) { }

		private class HealthBarDrawable : IDrawable
		{
			public int[] DrawOrders { get { return new int[] { 1000 }; } }
			private ShittyCreaturesModLoader m_owner;
			public HealthBarDrawable(ShittyCreaturesModLoader owner) { m_owner = owner; }
			public void Draw(Camera camera, int drawOrder) { m_owner.DrawHealthBar(camera); }
		}
	}
}
