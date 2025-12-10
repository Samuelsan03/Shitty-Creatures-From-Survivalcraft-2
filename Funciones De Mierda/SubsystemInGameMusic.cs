using Engine;
using Engine.Audio;
using Engine.Media;
using GameEntitySystem;
using TemplatesDatabase;
using System.Collections.Generic;

namespace Game
{
	public class SubsystemInGameMusic : Subsystem, IUpdateable
	{
		// Variables para el estado de la música
		private bool m_musicEnabled = false;
		private ValuesDictionary m_valuesDictionary;
		private StreamingSound m_currentMusic = null;
		private string m_currentTrackPath = "";
		private bool m_isPlaying = false;

		// Variables para manejar los botones
		private SubsystemPlayers m_subsystemPlayers;
		private Dictionary<ComponentPlayer, BevelledButtonWidget> m_playerButtons = new Dictionary<ComponentPlayer, BevelledButtonWidget>();
		private HashSet<BevelledButtonWidget> m_clickedButtons = new HashSet<BevelledButtonWidget>();

		private void ShowMessageToAllPlayers(string message)
		{
			if (m_subsystemPlayers == null)
				return;

			var componentPlayers = m_subsystemPlayers.ComponentPlayers;
			// No comparamos con null, solo verificamos si hay jugadores
			if (componentPlayers.Count == 0)
				return;

			foreach (ComponentPlayer componentPlayer in componentPlayers)
			{
				if (componentPlayer != null && componentPlayer.ComponentGui != null)
				{
					componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.White, false, false);
				}
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public void Update(float dt)
		{
			// Manejar los botones de música para cada jugador
			HandleMusicButtons();

			// Solo reproducir música si está activada
			if (!m_musicEnabled)
			{
				// Si la música está desactivada pero se está reproduciendo, detenerla
				if (m_isPlaying && m_currentMusic != null && m_currentMusic.State > SoundState.Stopped)
				{
					StopCurrentMusic();
				}
				return;
			}

			// Verificar si la música actual terminó
			if (m_currentMusic != null && m_currentMusic.State == SoundState.Stopped)
			{
				m_isPlaying = false;
				m_currentMusic = null;
			}

			// Usar el MusicManager original para verificar el mix actual
			bool flag = (int)MusicManager.CurrentMix != 2;
			if (!flag)
			{
				bool flag2 = this.m_nextMusicTime == 0.0;
				if (flag2)
				{
					this.ScheduleNextMusic();
				}
				bool flag3 = this.m_subsystemTime.GameTime >= this.m_nextMusicTime && !m_isPlaying;
				if (flag3)
				{
					this.PlayRandomMusic();
					this.ScheduleNextMusic();
				}
			}
		}

		// Manejar la creación y clics de los botones
		private void HandleMusicButtons()
		{
			if (m_subsystemPlayers == null)
				return;

			var componentPlayers = m_subsystemPlayers.ComponentPlayers;

			// Para cada jugador, asegurarse de que tenga un botón de música
			for (int i = 0; i < componentPlayers.Count; i++)
			{
				ComponentPlayer player = componentPlayers[i];
				if (player != null && player.ComponentGui != null)
				{
					// Verificar si el jugador ya tiene un botón
					if (!m_playerButtons.ContainsKey(player) || m_playerButtons[player] == null)
					{
						AddMusicButtonToPlayer(player);
					}

					// Manejar clic en el botón
					BevelledButtonWidget button = m_playerButtons.ContainsKey(player) ? m_playerButtons[player] : null;
					if (button != null && button.IsClicked && !m_clickedButtons.Contains(button))
					{
						m_clickedButtons.Add(button);
						ToggleMusic();
						button.Text = m_musicEnabled ? "Music ON" : "Music OFF";
					}
					else if (button != null && !button.IsClicked && m_clickedButtons.Contains(button))
					{
						m_clickedButtons.Remove(button);
					}
				}
			}

			// Limpiar botones de jugadores que ya no existen
			CleanupPlayerButtons();
		}

		// Agregar botón a un jugador
		private void AddMusicButtonToPlayer(ComponentPlayer player)
		{
			if (player == null || player.ComponentGui == null)
				return;

			// Obtener el contenedor de controles derecho
			ContainerWidget rightControlsContainerWidget = player.ComponentGui.m_rightControlsContainerWidget;
			if (rightControlsContainerWidget == null)
				return;

			// Buscar si ya existe un botón de música
			BevelledButtonWidget musicButton = rightControlsContainerWidget.Children.Find<BevelledButtonWidget>("InGameMusicButton", false);

			if (musicButton != null)
			{
				m_playerButtons[player] = musicButton;
				return;
			}

			// Crear nuevo botón
			musicButton = new BevelledButtonWidget
			{
				Name = "InGameMusicButton",
				Text = m_musicEnabled ? "Music ON" : "Music OFF",
				Size = new Vector2(88f, 56f),
				IsEnabled = true,
				IsVisible = true,
				HorizontalAlignment = WidgetAlignment.Far,
				IsAutoCheckingEnabled = false
			};

			// Configurar el label del botón
			if (musicButton.m_labelWidget != null)
			{
				musicButton.m_labelWidget.FontScale = 0.8f;
			}

			rightControlsContainerWidget.Children.Add(musicButton);
			m_playerButtons[player] = musicButton;
		}

		// Limpiar botones de jugadores que ya no existen
		private void CleanupPlayerButtons()
		{
			if (m_subsystemPlayers == null)
			{
				m_playerButtons.Clear();
				return;
			}

			var componentPlayers = m_subsystemPlayers.ComponentPlayers;
			List<ComponentPlayer> playersToRemove = new List<ComponentPlayer>();

			foreach (var kvp in m_playerButtons)
			{
				ComponentPlayer player = kvp.Key;
				BevelledButtonWidget button = kvp.Value;

				// Verificar si el jugador todavía está en la lista de jugadores
				bool playerExists = false;
				for (int i = 0; i < componentPlayers.Count; i++)
				{
					if (componentPlayers[i] == player)
					{
						playerExists = true;
						break;
					}
				}

				if (!playerExists || player == null || player.ComponentGui == null)
				{
					playersToRemove.Add(player);

					// Intentar remover el botón de la UI
					if (button != null && player != null && player.ComponentGui != null)
					{
						ContainerWidget rightControlsContainerWidget = player.ComponentGui.m_rightControlsContainerWidget;
						if (rightControlsContainerWidget != null)
						{
							rightControlsContainerWidget.Children.Remove(button);
						}
					}
				}
			}

			// Remover de la lista
			foreach (ComponentPlayer player in playersToRemove)
			{
				m_playerButtons.Remove(player);
			}
		}

		// Método para detener la música actual
		private void StopCurrentMusic()
		{
			if (m_currentMusic == null)
			{
				return;
			}

			m_currentMusic.Stop();
			m_currentMusic = null;
			m_isPlaying = false;
			m_currentTrackPath = "";
			Log.Information("Music stopped");
		}

		// Método público para alternar la música
		public void ToggleMusic()
		{
			m_musicEnabled = !m_musicEnabled;

			// Guardar el estado en ValuesDictionary
			if (m_valuesDictionary != null)
			{
				m_valuesDictionary.SetValue<bool>("MusicEnabled", m_musicEnabled);
			}

			if (m_musicEnabled)
			{
				ShowMessageToAllPlayers("Music enabled");
				// Si se activa la música, reproducir inmediatamente si no hay música sonando
				if (!m_isPlaying && (int)MusicManager.CurrentMix != 2)
				{
					this.PlayRandomMusic();
					this.ScheduleNextMusic();
				}
				else if (m_currentTrackPath != "" && (m_currentMusic == null || m_currentMusic.State == SoundState.Stopped))
				{
					// Reanudar la última canción si había una
					try
					{
						PlayTrack(m_currentTrackPath);
						ShowMessageToAllPlayers("Resuming: " + System.IO.Path.GetFileName(m_currentTrackPath));
					}
					catch (Exception ex)
					{
						Log.Error("Error resuming music: " + ex.Message);
						this.PlayRandomMusic();
						this.ScheduleNextMusic();
					}
				}
			}
			else
			{
				ShowMessageToAllPlayers("Music disabled");
				// Si se desactiva la música, detener la reproducción actual
				if (m_currentMusic != null && m_currentMusic.State > SoundState.Stopped)
				{
					StopCurrentMusic();
				}
			}

			// Actualizar texto de todos los botones
			foreach (var button in m_playerButtons.Values)
			{
				if (button != null)
				{
					button.Text = m_musicEnabled ? "Music ON" : "Music OFF";
				}
			}
		}

		// Método para reproducir un track específico
		private void PlayTrack(string trackPath)
		{
			try
			{
				StopCurrentMusic();

				// Crear un nuevo StreamingSound
				m_currentMusic = new StreamingSound(
					ContentManager.Get<StreamingSource>(trackPath),
					SettingsManager.MusicVolume * 2f,
					1f,
					0f,
					false,
					true,
					1f
				);

				m_currentTrackPath = trackPath;
				m_isPlaying = true;
				m_currentMusic.Play();

				Log.Information("Playing music: " + trackPath);

				// Mostrar el nombre del archivo a los jugadores
				string fileName = System.IO.Path.GetFileName(trackPath);
				ShowMessageToAllPlayers("Now playing: " + fileName);
			}
			catch (Exception ex)
			{
				Log.Error("Error playing music \"" + trackPath + "\": " + ex.Message);
				m_currentMusic = null;
				m_isPlaying = false;
				m_currentTrackPath = "";
			}
		}

		private void PlayRandomMusic()
		{
			bool flag = this.m_availableTracks.Count == 0;
			if (flag)
			{
				for (int i = 0; i < this.m_tracks.Length; i++)
				{
					bool flag2 = !this.m_recentTracks.Contains(i);
					if (flag2)
					{
						this.m_availableTracks.Add(i);
					}
				}
				bool flag3 = this.m_availableTracks.Count == 0;
				if (flag3)
				{
					this.m_recentTracks.Clear();
					for (int j = 0; j < this.m_tracks.Length; j++)
					{
						this.m_availableTracks.Add(j);
					}
				}
			}
			int index = this.m_random.Int(0, this.m_availableTracks.Count - 1);
			int num = this.m_availableTracks[index];
			SubsystemInGameMusic.TrackInfo trackInfo = this.m_tracks[num];
			this.UpdateTrackHistory(num);

			PlayTrack(trackInfo.Path);
			this.m_musicDuration = (double)trackInfo.Duration;
		}

		private void ScheduleNextMusic()
		{
			if (!m_musicEnabled) return;

			bool flag = SettingsManager.MusicVolume > 0f;
			if (flag)
			{
				this.m_nextMusicTime = this.m_subsystemTime.GameTime + this.m_musicDuration;
			}
			else
			{
				this.m_nextMusicTime = this.m_subsystemTime.GameTime + 10.0;
			}
		}

		private void UpdateTrackHistory(int playedIndex)
		{
			this.m_recentTracks.Enqueue(playedIndex);
			this.m_availableTracks.Remove(playedIndex);
			bool flag = this.m_recentTracks.Count > 2;
			if (flag)
			{
				int item = this.m_recentTracks.Dequeue();
				bool flag2 = !this.m_availableTracks.Contains(item);
				if (flag2)
				{
					this.m_availableTracks.Add(item);
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);

			// Guardar referencia al ValuesDictionary
			m_valuesDictionary = valuesDictionary;

			// Leer el estado de la música (por defecto false para nuevos mundos)
			m_musicEnabled = valuesDictionary.GetValue<bool>("MusicEnabled", false);

			for (int i = 0; i < this.m_tracks.Length; i++)
			{
				this.m_availableTracks.Add(i);
			}

			// Solo programar música si está activada
			if (m_musicEnabled)
			{
				this.m_nextMusicTime = this.m_subsystemTime.GameTime + 5.0;
			}
			else
			{
				this.m_nextMusicTime = double.MaxValue; // Nunca programar música
			}
		}

		public override void Dispose()
		{
			// Detener la música al destruir el subsistema
			StopCurrentMusic();

			// Limpiar todos los botones
			foreach (var kvp in m_playerButtons)
			{
				ComponentPlayer player = kvp.Key;
				BevelledButtonWidget button = kvp.Value;

				if (player != null && player.ComponentGui != null && button != null)
				{
					ContainerWidget rightControlsContainerWidget = player.ComponentGui.m_rightControlsContainerWidget;
					if (rightControlsContainerWidget != null)
					{
						rightControlsContainerWidget.Children.Remove(button);
					}
				}
			}
			m_playerButtons.Clear();

			base.Dispose();
		}

		// Propiedad pública para verificar si la música está habilitada
		public bool IsMusicEnabled
		{
			get { return m_musicEnabled; }
		}

		// Campos existentes...
		public SubsystemTime m_subsystemTime;
		public Random m_random = new Random();
		private double m_nextMusicTime;
		private double m_musicDuration;
		private readonly Queue<int> m_recentTracks = new Queue<int>(2);
		private readonly List<int> m_availableTracks = new List<int>();
		private readonly SubsystemInGameMusic.TrackInfo[] m_tracks = new SubsystemInGameMusic.TrackInfo[]
		{
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Digimon02OpeningThemeSong", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Touhou2MimasThemeCompleteDarkness", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Touhou2EasternWind", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Touhou2RecordoftheSealingofanOrientalDemon", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Digimon02EvolutionThemeBreakUp", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/DigimonAdventure01BraveHeart", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/DigimonAdventure01Butterfly", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/DigimonSaversOP1ThemeSongGou-ingGoingMySoul", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/DigimonSaversOP2Hirari", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/EoSDCreditsThemeCrimsonBelvedereEasternDream", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/TheBiggestDreamerDigimonTamers", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Touhou6FlandreScarletsThemeU.N.Owenwasher", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/WadaKoujiFIREDigimonFrontiers", 177f),
		};

		private struct TrackInfo
		{
			public TrackInfo(string path, float duration)
			{
				this.Path = path;
				this.Duration = duration;
			}

			public string Path;
			public float Duration;
		}
	}
}
