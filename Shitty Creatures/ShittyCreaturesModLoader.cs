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

		// Diccionarios para controlar el ritmo de golpes
		private Dictionary<ComponentPlayer, double> m_lastHitGameTime = new Dictionary<ComponentPlayer, double>();
		private Dictionary<ComponentPlayer, ComponentBody> m_lastHitTarget = new Dictionary<ComponentPlayer, ComponentBody>();
		private Dictionary<ComponentPlayer, bool> m_fastHitMode = new Dictionary<ComponentPlayer, bool>();

		private Dictionary<ComponentCreature, BloodParticleSystem> m_bleedingSystems = new Dictionary<ComponentCreature, BloodParticleSystem>();
		private SubsystemModelsRenderer m_healthBarModelsRenderer;
		private SubsystemCreatureSpawn m_healthBarCreatureSpawn;
		private SubsystemPlayers m_healthBarPlayers;
		public float HealthBarVisibilityRadius = 50f;

		// ShittyModLoader (original)
		static FieldInfo m_cachesField;

		// MusicModLoader
		private static readonly List<string> _menuSongs = new List<string>
		{
			"MenuMusic/Dragon Quest NES Title Theme",
			"MenuMusic/Digimon 02 Target Wada Kouji",
			"MenuMusic/Touhou 2 Mimas Theme Complete Darkness",
			"MenuMusic/Touhou 2 Eastern Wind",
			"MenuMusic/Touhou 2 Record of the Sealing of an Oriental Demon",
			"MenuMusic/Digimon 02 Evolution Break Up Ayumi Miyazaki",
			"MenuMusic/Digimon Adventure 01 Brave Heart Wada Kouji",
			"MenuMusic/Digimon Adventure 01 Butterfly Wada Kouji",
			"MenuMusic/Digimon Savers OP1 Theme Song Gouing Going My Soul Dynamite SHU",
			"MenuMusic/Digimon Savers OP2 Hirari Wada Kouji",
			"MenuMusic/EoSD Credits Theme Crimson Belvedere Eastern Dream",
			"MenuMusic/Digimon Tamers The Biggest Dreamer Wada Kouji",
			"MenuMusic/Touhou 6 Flandre Scarlets Theme U.N. Owen was her",
			"MenuMusic/Digimon Frontiers FIRE Wada Kouji",
			"MenuMusic/Rocket Knight Adventures Stage 1-1",
			"MenuMusic/Rocket Knight Adventures Stage 1-2",
			"MenuMusic/Sparkster (SEGA Genesis) Stage 1-1",
			"MenuMusic/Sparkster (SNES) Stage Lakeside",
			"MenuMusic/Space Harrier Theme",
			"MenuMusic/MAGICAL SOUND SHOWER OutRun",
			"MenuMusic/Super Hang-On Outride A Crisis",
			"MenuMusic/Super Hang-On Sprinter",
			"MenuMusic/Super Hang-On Winning Run",
			"MenuMusic/Nichijou Koigokoro Wa Dangan Mo Yawarakakusuru",
			"MenuMusic/SEGA Mega CD Japanese European Gamerip BIOS",
			"MenuMusic/SEGA CD American BIOS Gamerip Version 01",
			"MenuMusic/SEGA CD American BIOS Gamerip Version 02",
			"MenuMusic/Sonic The Hedgehog 1991 Spring Yard Zone",
			"MenuMusic/Sonic The Hedgehog 1991 Marble Zone",
			"MenuMusic/Sonic The Hedgehog 2 1992 Hill Top Zone",
			"MenuMusic/Mio Honda 本田未央 Step! ステップ",
			"MenuMusic/Yahpp Sorceress Elise",
			"MenuMusic/Chrono Trigger Main Theme",
			"MenuMusic/Twill STAND UP Digimon Xros Wars Hunters",
			"MenuMusic/Sonar Pocket Never Give Up! Digimon Fusion",
			"MenuMusic/Prince Of Persia (SNES) Recap",
			"MenuMusic/Prince Of Persia (SNES) Staff Roll",
			"MenuMusic/FIELD OF VIEW 渇いた叫び - 捨てられた物。",
			"MenuMusic/瞬間ときはファンタジー",
			"MenuMusic/Power Rangers The Movie Title Theme SNES",
			"MenuMusic/Sonic Boom Closing Theme Sonic CD",
			"MenuMusic/Sonic Boom Sonic CD",
			"MenuMusic/You Can Do Anything Sonic CD"
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
			ModsManager.RegisterHook("OnPlayerInputInteract", this, 0);

			// GreenNightSkyModLoader
			ModsManager.RegisterHook("ChangeSkyColor", this);
			ModsManager.RegisterHook("OnVitalStatsUpdateSleep", this);

			// MusicModLoader
			ModsManager.RegisterHook("MenuPlayMusic", this);
			ModsManager.RegisterHook("PlayInGameMusic", this);
			_random = new Random();

			// NewPanoramaModLoader
			if (!s_panoramaHookRegistered)
			{
				ModsManager.RegisterHook("OnWidgetConstruct", this, -100);
				s_panoramaHookRegistered = true;
			}

			// RemoteControlModLoader
			RegisterRemoteControlBlock();
			ModsManager.RegisterHook("MatchRecipe", this);

			// Hook para cuando el jugador golpea cuerpo a cuerpo (prioridad alta)
			ModsManager.RegisterHook("OnPlayerInputHit", this, -100);
			// Hook para cuando el jugador recibe daño (se usa evento Injured, no hook directo)
			// La suscripción se realiza en OnPlayerInputHit cuando el jugador está disponible

			// Hook para detectar ataques al jugador incluso en modo Creativo
			ModsManager.RegisterHook("OnMinerHit", this, -100);

			ModsManager.RegisterHook("ManageCameras", this);

			ModsManager.RegisterHook("OnPlayerSpawned", this);

			ModsManager.RegisterHook("OnPlayerDead", this);
			ModsManager.RegisterHook("OnProjectLoaded", this);
			ModsManager.RegisterHook("OnCreatureDied", this);
			ModsManager.RegisterHook("OnCreatureDying", this);
			ModsManager.RegisterHook("ChangeVisualEffectOnInjury", this);
			ModsManager.RegisterHook("InitializeCreatureTypes", this);
			ModsManager.RegisterHook("OnProjectileRaycastBody", this);
			ModsManager.RegisterHook("OnProjectileHitBody", this);
			ModsManager.RegisterHook("SetHitInterval", this);
			// Reemplazar overlay de captura de pantalla
			ReplaceScreenCaptureOverlay();
		}

		// ---------------------------------------------------------------------------------
		// Métodos auxiliares privados
		// ---------------------------------------------------------------------------------

		public override void OnProjectLoaded(Project project)
		{
			if (!m_greenNightHooksRegistered)
			{
				m_greenNightHooksRegistered = true;
				SubsystemGreenNightSky greenNight = project.FindSubsystem<SubsystemGreenNightSky>(true);
				if (greenNight != null)
				{
					greenNight.GreenNightStarted += () => CancelGreenNightChaseDelay(project);
				}
			}
			m_healthBarModelsRenderer = project.FindSubsystem<SubsystemModelsRenderer>(true);
			m_healthBarCreatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_healthBarPlayers = project.FindSubsystem<SubsystemPlayers>(true);
			HealthBarDrawable healthBarDrawable = new HealthBarDrawable(this);
			project.FindSubsystem<SubsystemDrawing>(true).AddDrawable(healthBarDrawable);

			// Suscribir a todos los jugadores existentes al evento Injured (por si ya estaban cargados)
			var playersSubsystem = project.FindSubsystem<SubsystemPlayers>(true);
			if (playersSubsystem != null)
			{
				foreach (ComponentPlayer player in playersSubsystem.ComponentPlayers)
				{
					SubscribeToPlayerInjured(player);
				}
			}
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
					Margin = new Vector2(0f, 5f)
				};

				string aboutButtonText = LanguageControl.Get(new string[] { "ShittyCreaturesAbout", "AboutButton" });
				BevelledButtonWidget aboutButton = new BevelledButtonWidget
				{
					Name = "ShittyAboutButton",
					Text = aboutButtonText,
					Size = new Vector2(310f, 60f),
					BevelColor = new Color(128, 0, 128),
					CenterColor = new Color(128, 0, 128),
					Margin = new Vector2(10f, 0f)
				};

				string exitButtonText = LanguageControl.Get(new string[] { "ShittyCreaturesAbout", "ExitButton" });
				BevelledButtonWidget exitButton = new BevelledButtonWidget
				{
					Name = "ShittyExitButton",
					Text = exitButtonText,
					Size = new Vector2(310f, 60f),
					BevelColor = new Color(128, 128, 128),
					CenterColor = new Color(128, 128, 128),
					Margin = new Vector2(10f, 0f)
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
				// Botón de Agradecimientos
				BevelledButtonWidget thanksButton = mainMenu.Children.Find<BevelledButtonWidget>("SpecialThanksButton", false);
				if (thanksButton != null && thanksButton.IsClicked)
				{
					if (ScreensManager.FindScreen<SpecialThanksScreen>("SpecialThanks") == null)
						ScreensManager.AddScreen("SpecialThanks", new SpecialThanksScreen());
					ScreensManager.SwitchScreen("SpecialThanks");
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

			// ─── Efecto arcoíris en el botón de ajustes del mod ───
			SettingsScreen settings = widget as SettingsScreen;
			if (settings != null)
			{
				BevelledButtonWidget modSettingsButton = settings.Children.Find<BevelledButtonWidget>("ShittyCreaturesSettingsButton", true);
				if (modSettingsButton != null)
				{
					// Desfasado 180° → color complementario
					float hue = (float)((Time.RealTime * 60.0 + 180.0) % 360.0);
					Vector3 rgb = Color.HsvToRgb(new Vector3(hue, 1f, 1f));
					modSettingsButton.Color = new Color(rgb);
				}
			}

			// ---------- HUD de coordenadas y sangrado ----------
			GameWidget gameWidget = widget as GameWidget;
			if (gameWidget != null)
			{
				// ─── Sistema de sangrado (siempre se ejecuta cada frame) ───
				UpdateBleedingSystems(gameWidget);

				// ─── Coordenadas (solo si están activadas) ───
				if (ShittyCreaturesSettingsManager.CoordinateDisplayEnabled)
				{
					ComponentPlayer player = gameWidget.PlayerData?.ComponentPlayer;
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
					}
				}
			}
		}

		public override void OnPlayerDead(PlayerData playerData)
		{
			// Eliminar cualquier label de coordenadas asociado a este PlayerData,
			// independientemente de la instancia de ComponentPlayer que esté registrada.
			foreach (var kvp in m_coordinateLabels.ToArray())
			{
				if (kvp.Key.PlayerData == playerData)
				{
					kvp.Value.ParentWidget?.Children.Remove(kvp.Value);
					m_coordinateLabels.Remove(kvp.Key);
				}
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
			buttonsToAdd = new Dictionary<ButtonWidget, Action>();
			try
			{
				var shittyButton = new BevelledButtonWidget
				{
					Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "SettingsButton" }),
					Size = new Vector2(310f, 60f),
					BevelColor = Color.DarkRed,
					CenterColor = Color.DarkRed,
					Name = "ShittyCreaturesSettingsButton"
				};

				buttonsToAdd.Add(shittyButton, () =>
				{
					if (ScreensManager.FindScreen<ShittyCreaturesSettingsScreen>("ShittyCreaturesSettings") == null)
						ScreensManager.AddScreen("ShittyCreaturesSettings", new ShittyCreaturesSettingsScreen());
					ScreensManager.SwitchScreen("ShittyCreaturesSettings");
				});
			}
			catch (Exception ex)
			{
				Log.Error($"[ChaseMusic] Error al añadir botón: {ex.Message}");
			}
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
		}

		// ---------------------------------------------------------------------------------
		// Hook: MenuPlayMusic (MusicModLoader)
		// ---------------------------------------------------------------------------------
		public override void MenuPlayMusic(out string contentMusicPath)
		{
			// Antes era: if (ShittyCreaturesSettingsManager.MenuMusicEnabled)
			// Ahora siempre se reproduce la música personalizada
			contentMusicPath = GetRandomSong();
		}

		// ---------------------------------------------------------------------------------
		// Hook: PlayInGameMusic (MusicModLoader)
		// ---------------------------------------------------------------------------------
		public override void PlayInGameMusic()
		{
			// No se modifica la música del juego (comportamiento original)
		}

		private string GetRandomSong()
		{
			if (_menuSongs.Count == 0)
				return string.Empty;
			if (_menuSongs.Count == 1)
				return _menuSongs[0];

			int newIndex;
			do
			{
				newIndex = _random.Int(0, _menuSongs.Count - 1);
			} while (newIndex == _lastSongIndex);

			_lastSongIndex = newIndex;
			return _menuSongs[newIndex];
		}

		// ---------------------------------------------------------------------------------
		// Hook: OnWidgetConstruct (NewPanoramaModLoader)
		// ---------------------------------------------------------------------------------
		public override void OnWidgetConstruct(ref Widget widget)
		{
			if (widget != null && widget.GetType().Name == "PanoramaWidget" && !(widget is NewPanoramaWidget))
				widget = new NewPanoramaWidget();
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
							if (string.IsNullOrEmpty(smallMessage))
								smallMessage = "You must survive all waves first!";

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

			if (playerOperated || skipVanilla || player == null)
				return;

			PlayerInput input = player.ComponentInput.PlayerInput;
			if (input.Hit == null)
				return;

			ComponentMiner miner = player.ComponentMiner;
			if (miner == null)
				return;

			BodyRaycastResult? result = miner.Raycast<BodyRaycastResult>(
				input.Hit.Value, RaycastMode.Interaction, true, true, true, meleeAttackRange);

			if (result.HasValue)
			{
				ComponentBody hitBody = result.Value.ComponentBody;
				if (hitBody != null && hitBody.Entity != player.Entity)
				{
					ComponentCreature targetCreature = hitBody.Entity.FindComponent<ComponentCreature>();
					if (targetCreature != null && targetCreature.ComponentHealth.Health > 0f)
					{
						if (ShittyCreaturesSettingsManager.PunchCommandEnabled)
						{
							// Suscribir al evento Injured para que solo se ordene ataque cuando el golpe sea exitoso (cause daño)
							Action<Injury> handler = null;
							handler = (Injury injury) =>
							{
								if (injury.Attacker == player)
								{
									CommandAlliesToAttack(player, targetCreature);
									targetCreature.ComponentHealth.Injured -= handler; // autodesuscripción
								}
							};
							targetCreature.ComponentHealth.Injured += handler;
						}

						if (ShittyCreaturesSettingsManager.FastMeleeEnabled)
						{
							double currentGameTime = player.Project.FindSubsystem<SubsystemTime>(true).GameTime;
							bool isFastHit = false;

							if (m_lastHitGameTime.TryGetValue(player, out double lastTime) &&
								m_lastHitTarget.TryGetValue(player, out ComponentBody lastBody))
							{
								if (lastBody == hitBody && (currentGameTime - lastTime) < 0.3)
								{
									isFastHit = true;
								}
							}

							m_lastHitGameTime[player] = currentGameTime;
							m_lastHitTarget[player] = hitBody;

							if (isFastHit)
							{
								m_fastHitMode[player] = true;
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
					}
				}
			}
		}

		// Manejador del evento Injured del jugador
		private void OnPlayerInjuredForAllies(Injury injury)
		{
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
					chase.Attack(target, float.MaxValue, float.MaxValue, true);
				}
			}
		}
		public override void OnMinerHit(ComponentMiner miner, ComponentBody targetBody, Vector3 hitPoint, Vector3 hitDirection, ref float attackPower, ref float hitProbability, ref float hitProbability2, out bool skipVanilla)
		{
			skipVanilla = false;

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
					HorizontalAlignment = WidgetAlignment.Far,
					VerticalAlignment = WidgetAlignment.Far,
					Margin = new Vector2(0f, 0f),
					Color = Color.White,
					DropShadow = true,
					IsHitTestVisible = false
				};
				componentPlayer.GuiWidget.Children.Add(label);
				m_coordinateLabels[componentPlayer] = label;
			}

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
		"FrozenGhostBoomer"
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
			// Obtener la entidad que murió
			Entity deadEntity = health.Entity;
			if (deadEntity == null) return;

			// Obtener el ComponentCreature para acceder a su nombre y posición
			ComponentCreature creature = deadEntity.FindComponent<ComponentCreature>();
			if (creature == null) return;

			// Obtener el nombre de la plantilla (template) de la criatura
			string templateName = deadEntity.ValuesDictionary?.DatabaseObject?.Name;
			if (string.IsNullOrEmpty(templateName)) return;

			// Verificar si es CapitanPirata O PirataHostilComerciante
			if (templateName == "CapitanPirata" || templateName == "PirataHostilComerciante")
			{
				// Ruta del sonido de muerte
				string deathSoundPath = "Audio/Die(1)";

				// Intentar reproducir usando SubsystemAudio (efecto 3D)
				SubsystemAudio audio = health.Project.FindSubsystem<SubsystemAudio>(true);
				if (audio != null && creature.ComponentBody != null)
				{
					// Reproducir sonido en la posición del cuerpo, con distancia mínima 10f
					audio.PlaySound(deathSoundPath, 1f, 0f, creature.ComponentBody.Position, 10f, false);
				}
				else
				{
					// Fallback: sonido global (sin efecto 3D)
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

		public override void InitializeCreatureTypes(SubsystemCreatureSpawn subsystemCreatureSpawn, List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
		{
			// Obtenemos los subsistemas necesarios para los cálculos (compartidos para todos)
			SubsystemTime time = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTime>(true);
			SubsystemTimeOfDay timeOfDay = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
			SubsystemGameInfo gameInfo = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemGameInfo>(true);
			SubsystemSky sky = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemSky>(true);

			// Lógica de cercanía al agua reutilizable (Lagos o Costas)
			Func<Point3, bool> isNearWater = delegate (Point3 point)
			{
				float shoreDistance = subsystemCreatureSpawn.m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance((float)point.X, (float)point.Z);
				int blockUnder = Terrain.ExtractContents(subsystemCreatureSpawn.m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

				bool isNearShore = (shoreDistance >= -5f && shoreDistance <= 15f);
				bool isOnWater = (BlocksManager.Blocks[blockUnder] is WaterBlock);

				return isNearShore || isOnWater;
			};

			// Lógica de cantidad reutilizable (50% de probabilidad de 3 o 5)
			Func<SubsystemCreatureSpawn, SubsystemCreatureSpawn.CreatureType, Point3, string, int> spawnGroup = delegate (SubsystemCreatureSpawn spawnSys, SubsystemCreatureSpawn.CreatureType ct, Point3 point, string templateName)
			{
				int count = spawnSys.m_random.Int(0, 1) == 0 ? 3 : 5;
				return spawnSys.SpawnCreatures(ct, templateName, point, count).Count;
			};


			// ==========================================
			// 1. PIRATA NORMAL (A partir del día 5) - En grupo
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("PirataNormal", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime) < 5.0) return 0f;

					return isNearWater(point) ? 2.5f : 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnGroup(subsystemCreatureSpawn, ct, point, "PirataNormal");
				}
			});


			// ==========================================
			// 2. PIRATA ELITE (A partir del día 15) - En grupo
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("PirataElite", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime) < 15.0) return 0f;

					return isNearWater(point) ? 2.5f : 0f;
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					return spawnGroup(subsystemCreatureSpawn, ct, point, "PirataElite");
				}
			});


			// ==========================================
			// 3. PIRATA HOSTIL COMERCIANTE (A partir del día 35) - SOLO
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("PirataHostilComerciante", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime) < 35.0) return 0f;

					return isNearWater(point) ? 2.5f : 0f; // Puedes bajar esto a 0.5f si quieres que sea más raro
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					// Se fuerza un 1 para que aparezca exactamente solo
					return subsystemCreatureSpawn.SpawnCreatures(ct, "PirataHostilComerciante", point, 1).Count;
				}
			});


			// ==========================================
			// 4. CAPITÁN PIRATA (A partir del día 55) - SOLO
			// ==========================================
			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("CapitanPirata", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					if (sky.SkyLightIntensity < 0.4f) return 0f;
					if (timeOfDay.CalculateDay(gameInfo.TotalElapsedGameTime) < 55.0) return 0f;

					return isNearWater(point) ? 2.5f : 0f; // Puedes bajar esto a 0.5f si quieres que sea más raro
				},
				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					// Se fuerza un 1 para que aparezca exactamente solo
					return subsystemCreatureSpawn.SpawnCreatures(ct, "CapitanPirata", point, 1).Count;
				}
			});

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Rayman", SpawnLocationType.Surface, true, false)
            {
                SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
                {
                    // 1. Solo de día
                    if (sky.SkyLightIntensity < 0.4f) return 0f;

                    // 2. Calcular el factor de montaña usando la misma fórmula que el generador de terreno
                    float mountainFactor = subsystemCreatureSpawn.m_subsystemTerrain.TerrainContentsGenerator.CalculateMountainRangeFactor((float)point.X, (float)point.Z);

                    // 3. Si es >= 0.9f, es una zona de alta montaña (piedra/caliza)
                    if (mountainFactor >= 0.9f)
                    {
                        return 2500f; // Peso altísimo para que aparezca inmediatamente en las montañas
                    }

                    return 0f;
                },
                SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
                {
                    // Aparece solo 1
                    return subsystemCreatureSpawn.SpawnCreatures(ct, "Rayman", point, 1).Count;
                }
            });
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
				if (ownerHerdName == "Alianza" || ownerHerdName == "Muerte")
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
				blockName == "NuevaBala6")
			{
				// Eliminar el empuje (impulso) completamente
				attackment.ImpulseFactor = 0f;
				// También eliminar el aturdimiento si lo hubiera
				attackment.StunTimeSet = 0f;
			}

			// ----- NUEVO: Ordenar a los aliados atacar al objetivo impactado por el proyectil -----
			// Si el dueño del proyectil es un jugador y el impacto es sobre una criatura viva,
			// se ordena a todos los aliados del jugador atacar a esa criatura.
			ComponentCreature targetCreature = bodyRaycastResult.ComponentBody.Entity.FindComponent<ComponentCreature>();
			if (targetCreature != null && targetCreature.ComponentHealth.Health > 0f)
			{
				ComponentPlayer ownerPlayer = projectile.Owner as ComponentPlayer;
				if (ownerPlayer != null)
				{
					// Llamar al método que ordena ataque a los aliados
					CommandAlliesToAttack(ownerPlayer, targetCreature);
				}
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
