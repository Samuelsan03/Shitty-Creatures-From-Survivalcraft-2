using System;
using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemInGameMusic : Subsystem, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		private SubsystemPlayers m_subsystemPlayers;

		// ===== PLAYLIST =====
		private readonly List<(string Path, double Duration)> m_playlist = new List<(string, double)>
		{
			("MenuMusic/Twill STAND UP Digimon Xros Wars Hunters", 247),
			("MenuMusic/Sparkster (SEGA Genesis) Stage 1-1", 109),
			("MenuMusic/You Can Do Anything Sonic CD", 91),
			("MenuMusic/Digimon Frontiers FIRE Wada Kouji", 253),
			("MenuMusic/Dragon Quest NES Title Theme", 180),
			("MenuMusic/Power Rangers The Movie Title Theme SNES", 117),
			("MenuMusic/Prince Of Persia (SNES) Recap", 129),
			("MenuMusic/Prince Of Persia (SNES) Staff Roll", 185),
			("MenuMusic/Sonic Boom Closing Theme Sonic CD", 213),
			("MenuMusic/Sonic Boom Sonic CD", 184),
			("MenuMusic/Sonar Pocket Never Give Up! Digimon Fusion", 257),
			("MenuMusic/Sparkster (SNES) Stage Lakeside", 115),
			("MenuMusic/Rocket Knight Adventures Stage 1-2", 110),
			("MenuMusic/Rocket Knight Adventures Stage 1-1", 147),
			("MenuMusic/Sparkster (SEGA Genesis) Stage 1-1", 109),
			("MenuMusic/FIELD OF VIEW 渇いた叫び - 捨てられた物。", 254),
			("MenuMusic/Chrono Trigger Main Theme", 124),
			("MenuMusic/Beat Hit! Ayumi Miyazaki", 262),
			("MenuMusic/Sonic The Hedgehog 2 1992 Hill Top Zone", 108),
			("MenuMusic/Sonic The Hedgehog 1991 Marble Zone", 88),
			("MenuMusic/Sonic The Hedgehog 1991 Spring Yard Zone", 98),
			("MenuMusic/SEGA CD American BIOS Gamerip Version 01", 89),
			("MenuMusic/SEGA CD American BIOS Gamerip Version 02", 74),
			("MenuMusic/SEGA Mega CD Japanese European Gamerip BIOS", 76),
			("MenuMusic/Nichijou Koigokoro Wa Dangan Mo Yawarakakusuru", 82),
			("MenuMusic/Super Hang-On Winning Run", 377),
			("MenuMusic/Super Hang-On Sprinter", 296),
			("MenuMusic/Super Hang-On Outride A Crisis", 310),
			("MenuMusic/MAGICAL SOUND SHOWER OutRun", 348),
			("MenuMusic/Space Harrier Theme", 456),
			("MenuMusic/Touhou 6 Flandre Scarlets Theme U.N. Owen was her", 250),
			("MenuMusic/EoSD Credits Theme Crimson Belvedere Eastern Dream", 231),
			("MenuMusic/Digimon Adventure 01 Brave Heart Wada Kouji", 252),
			("MenuMusic/Digimon 02 Evolution Break Up Ayumi Miyazaki", 248),
			("MenuMusic/Digimon Adventure 01 Butterfly Wada Kouji", 258),
			("MenuMusic/Digimon Savers OP1 Theme Song Gouing Going My Soul Dynamite SHU", 231),
			("MenuMusic/Digimon Savers OP2 Hirari Wada Kouji", 224),
			("MenuMusic/Digimon Tamers The Biggest Dreamer Wada Kouji", 230),
			("MenuMusic/Digimon 02 Target Wada Kouji", 268),
			("MenuMusic/Touhou 2 Mimas Theme Complete Darkness", 236),
			("MenuMusic/Touhou 2 Eastern Wind", 218),
			("MenuMusic/Touhou 2 Record of the Sealing of an Oriental Demon", 173),
		};
		private int m_currentTrackIndex = 0;
		private Random m_random = new Random();
		// ==================

		private bool m_isPlaying = false;
		private bool m_isPaused = false;
		private double m_playStartTime = 0.0;

		private readonly List<InGameMusicWidget> m_widgets = new List<InGameMusicWidget>();

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);

			m_subsystemPlayers.PlayerAdded += OnPlayerAdded;
			m_subsystemPlayers.PlayerRemoved += OnPlayerRemoved;

			foreach (var playerData in m_subsystemPlayers.PlayersData)
				AddWidgetForPlayer(playerData);
		}

		public override void Dispose()
		{
			if (m_subsystemPlayers != null)
			{
				m_subsystemPlayers.PlayerAdded -= OnPlayerAdded;
				m_subsystemPlayers.PlayerRemoved -= OnPlayerRemoved;
			}

			foreach (var widget in m_widgets)
				widget?.Dispose();
			m_widgets.Clear();

			InGameMusicManager.StopMusic();
			m_isPlaying = false;
			m_isPaused = false;

			base.Dispose();
		}

		private void OnPlayerAdded(PlayerData playerData) => AddWidgetForPlayer(playerData);

		private void OnPlayerRemoved(PlayerData playerData)
		{
			InGameMusicWidget toRemove = null;
			foreach (var w in m_widgets)
				if (w.PlayerData == playerData) { toRemove = w; break; }

			if (toRemove != null && toRemove.ParentWidget != null)
			{
				toRemove.ParentWidget.Children.Remove(toRemove);
				m_widgets.Remove(toRemove);
				toRemove.Dispose();
			}
		}

		private void AddWidgetForPlayer(PlayerData playerData)
		{
			if (playerData?.GameWidget?.GuiWidget == null) return;
			if (!ShittyCreaturesSettingsManager.InGameMusicButtonEnabled) return;

			var rightControls = playerData.GameWidget.GuiWidget.Children.Find<ContainerWidget>("RightControlsContainer", true);
			if (rightControls == null)
				rightControls = playerData.GameWidget.GuiWidget.Children.Find<ContainerWidget>("ControlsContainer", true);

			if (rightControls != null)
			{
				var widget = new InGameMusicWidget(playerData, this);
				rightControls.Children.Add(widget);
				m_widgets.Add(widget);
			}
		}

		public void ToggleMusic(PlayerData playerData)
		{
			if (m_isPlaying)
				StopMusic(playerData);
			else
			{
				PickRandomTrack();
				PlayCurrentTrack(playerData);
			}
		}

		private void PickRandomTrack()
		{
			if (m_playlist.Count > 0)
				m_currentTrackIndex = m_random.Int(0, m_playlist.Count - 1);
		}

		private void PlayCurrentTrack(PlayerData playerData)
		{
			if (m_playlist.Count == 0) return;

			try
			{
				var track = m_playlist[m_currentTrackIndex];
				InGameMusicManager.PlayMusic(track.Path, 0f);
				m_isPlaying = true;
				m_isPaused = false;
				m_playStartTime = Time.RealTime;

				string nowPlaying = LanguageControl.Get("InGameMusic", "NowPlaying");
				string trackName = GetDisplayNameFromPath(track.Path);
				ShowMessage(playerData, $"{nowPlaying}{trackName}");
			}
			catch (Exception ex)
			{
				Log.Error($"Error playing in-game music: {ex.Message}");
				ShowMessage(playerData, LanguageControl.Get("InGameMusic", "MusicPlayError"));
			}
		}

		private void NextTrack(PlayerData playerData)
		{
			if (m_playlist.Count == 0) return;

			m_currentTrackIndex = (m_currentTrackIndex + 1) % m_playlist.Count;
			var track = m_playlist[m_currentTrackIndex];

			try
			{
				InGameMusicManager.PlayMusic(track.Path, 0f);
				m_playStartTime = Time.RealTime;
				m_isPaused = false;

				string nowPlaying = LanguageControl.Get("InGameMusic", "NowPlaying");
				string trackName = GetDisplayNameFromPath(track.Path);
				ShowMessage(playerData, $"{nowPlaying}{trackName}");
			}
			catch (Exception ex)
			{
				Log.Error($"Error playing next track: {ex.Message}");
				ShowMessage(playerData, LanguageControl.Get("InGameMusic", "MusicPlayError"));
				m_currentTrackIndex = (m_currentTrackIndex + 1) % m_playlist.Count;
			}
		}

		private void StopMusic(PlayerData playerData)
		{
			InGameMusicManager.StopMusic();
			m_isPlaying = false;
			m_isPaused = false;
			ShowMessage(playerData, LanguageControl.Get("InGameMusic", "MusicDisabled"));
		}

		private void ShowMessage(PlayerData playerData, string message)
		{
			var player = playerData?.ComponentPlayer;
			player?.ComponentGui?.DisplaySmallMessage(message, Color.White, true, false);
		}

		private string GetDisplayNameFromPath(string path)
		{
			string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
			if (string.IsNullOrEmpty(fileName))
				fileName = path;
			fileName = fileName.Replace('_', ' ');
			return fileName;
		}

		public void Update(float dt)
		{
			// Detectar si la pantalla actual es la del juego (mundo visible)
			bool isGameScreenActive = (ScreensManager.CurrentScreen is GameScreen);

			if (m_isPlaying)
			{
				if (!isGameScreenActive && !m_isPaused)
				{
					// Guardar posición actual de la canción y detener
					InGameMusicManager.SavePositionAndStop();
					m_isPaused = true;
				}
				else if (isGameScreenActive && m_isPaused)
				{
					// Reanudar desde la posición guardada
					InGameMusicManager.RestartFromSavedPosition();
					m_isPaused = false;
					m_playStartTime = Time.RealTime;
				}
			}

			foreach (var w in m_widgets)
				w?.UpdateState(m_isPlaying && !m_isPaused);

			if (!m_isPlaying || m_isPaused) return;

			double currentDuration = m_playlist[m_currentTrackIndex].Duration;
			if (Time.RealTime - m_playStartTime >= currentDuration)
			{
				PlayerData anyPlayer = m_subsystemPlayers.PlayersData.Count > 0
					? m_subsystemPlayers.PlayersData[0]
					: null;
				NextTrack(anyPlayer);
			}
		}
	}

	// Widget del botón de música (sin cambios)
	public class InGameMusicWidget : CanvasWidget
	{
		public PlayerData PlayerData { get; private set; }
		private SubsystemInGameMusic m_subsystem;
		private BevelledButtonWidget m_button;
		private bool m_currentState = false;
		private bool m_processedClick = false; // Evita procesar el mismo clic varias veces

		public InGameMusicWidget(PlayerData playerData, SubsystemInGameMusic subsystem)
		{
			PlayerData = playerData;
			m_subsystem = subsystem;

			HorizontalAlignment = WidgetAlignment.Far;
			VerticalAlignment = WidgetAlignment.Center;
			IsHitTestVisible = true;
			Margin = new Vector2(5f, 0f);

			m_button = new BevelledButtonWidget
			{
				Text = GetButtonLabel(false),
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				IsAutoCheckingEnabled = false,
				FontScale = 0.5f,
				Size = new Vector2(80f, 60f)
			};

			Children.Add(m_button);
		}

		public override void Update()
		{
			// Usar IsClicked con bandera para evitar múltiples activaciones
			if (m_button.IsClicked && !m_processedClick)
			{
				m_processedClick = true;
				m_subsystem.ToggleMusic(PlayerData);
			}
			else if (!m_button.IsClicked)
			{
				m_processedClick = false;
			}
		}

		public void UpdateState(bool isPlaying)
		{
			if (m_currentState != isPlaying)
			{
				m_currentState = isPlaying;
				m_button.Text = GetButtonLabel(isPlaying);
			}
		}

		private string GetButtonLabel(bool isPlaying)
		{
			string toggleText = LanguageControl.Get("InGameMusic", "MusicToggleButton");
			string stateText = isPlaying
				? LanguageControl.Get("InGameMusic", "MusicOn")
				: LanguageControl.Get("InGameMusic", "MusicOff");
			return toggleText + stateText;
		}
	}
}
