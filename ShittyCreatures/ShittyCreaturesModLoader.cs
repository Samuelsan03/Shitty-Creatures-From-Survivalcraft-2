using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
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

		// ShittyModLoader (original)
		private static FieldInfo m_cachesField;

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
			// Reemplazar overlay de captura de pantalla
			ReplaceScreenCaptureOverlay();
		}

		// ---------------------------------------------------------------------------------
		// Métodos auxiliares privados
		// ---------------------------------------------------------------------------------

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
					Log.Information("[ShittyCreatures] Overlay de captura personalizado cargado (620x220)");
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
			}

			// ─── Efecto arcoíris en el botón de ajustes del mod (fuera del bloque MainMenuScreen) ───
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

			// ---------- HUD de coordenadas ----------
			if (!ShittyCreaturesSettingsManager.CoordinateDisplayEnabled)
				return;

			GameWidget gameWidget = widget as GameWidget;
			if (gameWidget == null)
				return;

			ComponentPlayer player = gameWidget.PlayerData?.ComponentPlayer;
			if (player == null || player.ComponentHealth.Health <= 0f)
				return;

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
			Vector3 pos = player.ComponentBody.Position;
			string format = LanguageControl.Get(new string[] { "Coordinates", "0" });
			coordLabel.Text = string.Format(format, pos.X.ToString("F1"), pos.Z.ToString("F1"), pos.Y.ToString("F1"));
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
					player.ComponentGui.ModalPanelWidget = dialog;
					AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
					playerOperated = true;
					return;
				}
			}

			// Comprobación común: si se sostiene un objeto curativo, no se interactúa.
			if (IsHealingItem(activeBlockIndex))
				return;

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
			if (ShittyCreaturesSettingsManager.MenuMusicEnabled)
				contentMusicPath = GetRandomSong();
			else
				contentMusicPath = string.Empty; // Retorna vacío para que MusicManager use la música original
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
							string smallMessage = LanguageControl.Get("RemoteControlAchievement", "CraftingLocked");
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

			// Suscribir al evento Injured del jugador si no lo está ya
			if (player != null && player.ComponentHealth != null)
			{
				player.ComponentHealth.Injured -= OnPlayerInjuredForAllies;
				player.ComponentHealth.Injured += OnPlayerInjuredForAllies;
			}

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
						// ✅ Solo se activa si está habilitada la opción de comando por puñetazo
						if (ShittyCreaturesSettingsManager.PunchCommandEnabled)
						{
							// SUSCRIBIRSE TEMPORALMENTE al evento Injured del objetivo
							// para detectar CUANDO REALMENTE RECIBE DAÑO
							targetCreature.ComponentHealth.Injured += (Injury injury) =>
							{
								// Verificar que el daño fue causado por el jugador
								if (injury.Attacker == player)
								{
									CommandAlliesToAttack(player, targetCreature);
								}
							};
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

				// Verificar si es aliado del jugador
				bool isAlly = false;

				// 1. Contratado por el jugador
				ComponentHireableNPC hireable = creature.Entity.FindComponent<ComponentHireableNPC>();
				if (hireable != null && hireable.IsHired)
				{
					isAlly = true;
				}

				// 2. Pertenece a manada "player" o "guardian"
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

				// Ordenar ataque sin límites
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

			// 1. Verificar que el atacante NO sea un jugador
			if (miner.ComponentPlayer != null)
				return;

			// 2. Verificar que el objetivo SÍ sea un jugador
			ComponentPlayer targetPlayer = targetBody.Entity.FindComponent<ComponentPlayer>();
			if (targetPlayer == null)
				return;

			// 3. Obtener el GameMode actual
			SubsystemGameInfo gameInfo = targetPlayer.Project.FindSubsystem<SubsystemGameInfo>(true);

			// 4. Solo aplicar esta lógica en modo Creativo.
			//    En otros modos, el evento 'Injured' se encargará de la defensa.
			if (gameInfo.WorldSettings.GameMode != GameMode.Creative)
				return;

			// 5. Verificar si la defensa en Creativo está habilitada
			if (!ShittyCreaturesSettingsManager.CreativeDefenseEnabled)
				return;

			// 6. Ordenar a los aliados atacar al agresor
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
			// Solo entregar en la primera aparición del mundo (no en respawns)
			// SpawnsCount == 1 indica el primer spawn real; 0 es antes de spawnear.
			if (componentPlayer.PlayerData.SpawnsCount != 1)
				return true;

			// Obtener el modo de juego actual
			SubsystemGameInfo gameInfo = componentPlayer.Project.FindSubsystem<SubsystemGameInfo>(true);
			if (gameInfo.WorldSettings.GameMode == GameMode.Creative)
				return true; // No entregar ítems en Creativo

			// Obtener inventario del jugador
			IInventory inventory = componentPlayer.ComponentMiner.Inventory;
			if (inventory == null)
				return true;

			// Usar nombres reales para obtener índices de bloque
			int ironMacheteIndex = GetBlockIndexByName("IronMacheteBlock");
			int boiledWaterBucketIndex = GetBlockIndexByName("BoiledWaterBucketBlock");
			int nuclearCoinIndex = GetBlockIndexByName("NuclearCoinBlock");
			int swm500Index = GetBlockIndexByName("SWM500Block");
			int swm500BulletIndex = GetBlockIndexByName("SWM500Bullet");
			int stoneAxeBlockIndex = GetBlockIndexByName("StoneAxeBlock");
			int mediumFirstAidKitIndex = GetBlockIndexByName("MediumFirstAidKitBlock");

			// Añadir ítems
			GiveItemToPlayer(inventory, ironMacheteIndex, 1);
			GiveItemToPlayer(inventory, boiledWaterBucketIndex, 1);
			GiveItemToPlayer(inventory, nuclearCoinIndex, 100);
			GiveItemToPlayer(inventory, stoneAxeBlockIndex, 1);

			// Desert Eagle con 8 balas cargadas
			int swm500Value = Terrain.MakeBlockValue(swm500Index, 0, SWM500Block.SetBulletNum(8));
			GiveItemToPlayer(inventory, swm500Value, 1);

			// 12 balas extra
			GiveItemToPlayer(inventory, swm500BulletIndex, 12);
			GiveItemToPlayer(inventory, mediumFirstAidKitIndex, 5);

			return true;
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

		// ---------------------------------------------------------------------------------
		// SaveSettings / LoadSettings (heredados de ChaseMusicModLoader, vacíos)
		// ---------------------------------------------------------------------------------
		public override void SaveSettings(XElement xElement) { }
		public override void LoadSettings(XElement xElement) { }
	}
}
