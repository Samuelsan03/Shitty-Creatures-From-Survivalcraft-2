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

		// Variables para mostrar mensajes con delay
		private double m_nextTrackNameDisplayTime = 0.0;
		private string m_pendingTrackName = "";

		// Variables para manejar los botones
		private SubsystemPlayers m_subsystemPlayers;
		private Dictionary<ComponentPlayer, BevelledButtonWidget> m_playerButtons = new Dictionary<ComponentPlayer, BevelledButtonWidget>();
		private HashSet<BevelledButtonWidget> m_clickedButtons = new HashSet<BevelledButtonWidget>();

		private void ShowMessageToAllPlayers(string message, Color? color = null)
		{
			if (m_subsystemPlayers == null)
				return;

			var componentPlayers = m_subsystemPlayers.ComponentPlayers;
			if (componentPlayers.Count == 0)
				return;

			// Color por defecto es blanco
			Color messageColor = color ?? Color.White;

			foreach (ComponentPlayer componentPlayer in componentPlayers)
			{
				if (componentPlayer != null && componentPlayer.ComponentGui != null)
				{
					// Mostrar mensaje normal
					componentPlayer.ComponentGui.DisplaySmallMessage(message, messageColor, false, false);
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

			// Manejar la visualización del nombre de la canción con delay
			if (m_pendingTrackName != "" && m_subsystemTime != null &&
				m_subsystemTime.GameTime >= m_nextTrackNameDisplayTime)
			{
				ShowMessageToAllPlayers(m_pendingTrackName, Color.Green);
				m_pendingTrackName = "";
			}

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
				// Programar la siguiente canción inmediatamente
				this.m_nextMusicTime = this.m_subsystemTime.GameTime;
			}

			// Programar y reproducir música si es necesario
			if (m_musicEnabled)
			{
				bool flag = this.m_nextMusicTime == 0.0;
				if (flag)
				{
					this.ScheduleNextMusic();
				}
				bool flag2 = this.m_subsystemTime.GameTime >= this.m_nextMusicTime && !m_isPlaying;
				if (flag2)
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
						UpdateButtonText(button);
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

		// Actualizar el texto del botón según el idioma y estado
		private void UpdateButtonText(BevelledButtonWidget button)
		{
			if (button == null) return;

			// Obtener el texto localizado para el botón
			string buttonText = GetLocalizedString("MusicToggleButton", "Music");
			string statusText = m_musicEnabled ?
				GetLocalizedString("MusicOn", "ON") :
				GetLocalizedString("MusicOff", "OFF");

			button.Text = $"{buttonText} {statusText}";
		}

		// Método auxiliar para obtener cadenas localizadas
		private string GetLocalizedString(string key, string defaultValue)
		{
			// Usar el método estático Get de LanguageControl
			return LanguageControl.Get(key, defaultValue);
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
				UpdateButtonText(musicButton);
				return;
			}

			// Crear nuevo botón
			musicButton = new BevelledButtonWidget
			{
				Name = "InGameMusicButton",
				Text = "", // Se establecerá con UpdateButtonText
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
			UpdateButtonText(musicButton);
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
			bool oldState = m_musicEnabled;
			m_musicEnabled = !m_musicEnabled;

			// Guardar el estado en ValuesDictionary
			if (m_valuesDictionary != null)
			{
				m_valuesDictionary.SetValue<bool>("MusicEnabled", m_musicEnabled);
			}

			Log.Information($"Music toggled from {oldState} to {m_musicEnabled}");

			if (m_musicEnabled)
			{
				string message = GetLocalizedString("MusicEnabled", "Music enabled");
				ShowMessageToAllPlayers(message);

				// Si estamos activando la música y no hay música reproduciéndose, iniciar inmediatamente
				if (!m_isPlaying)
				{
					Log.Information("Starting music playback");
					this.m_nextMusicTime = this.m_subsystemTime.GameTime; // Reproducir inmediatamente
				}
				else
				{
					Log.Information($"Music is already playing: {m_currentTrackPath}");
				}
			}
			else
			{
				string message = GetLocalizedString("MusicDisabled", "Music disabled");
				ShowMessageToAllPlayers(message);
				// Si se desactiva la música, detener la reproducción actual
				if (m_currentMusic != null && m_currentMusic.State > SoundState.Stopped)
				{
					StopCurrentMusic();
				}
				// Establecer un tiempo futuro lejano para no programar más música
				this.m_nextMusicTime = double.MaxValue;
			}

			// Actualizar texto de todos los botones
			foreach (var button in m_playerButtons.Values)
			{
				if (button != null)
				{
					UpdateButtonText(button);
				}
			}
		}

		// Método para obtener un nombre de display amigable para la pista
		private string GetTrackDisplayName(string trackPath)
		{
			try
			{
				// Extraer el nombre del archivo del path
				string fileName = System.IO.Path.GetFileName(trackPath);

				// Si el path está vacío, retornar string vacío
				if (string.IsNullOrEmpty(fileName))
					return "Unknown Track";

				// Remover la extensión si existe
				int dotIndex = fileName.LastIndexOf('.');
				if (dotIndex > 0)
				{
					fileName = fileName.Substring(0, dotIndex);
				}

				// Reemplazar guiones bajos y otros caracteres con espacios
				fileName = fileName.Replace('_', ' ').Replace('-', ' ');

				// Capitalizar cada palabra
				string[] words = fileName.Split(' ');
				for (int i = 0; i < words.Length; i++)
				{
					if (!string.IsNullOrEmpty(words[i]))
					{
						words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
					}
				}

				return string.Join(" ", words);
			}
			catch
			{
				// En caso de error, retornar el nombre simple
				return "Music Track";
			}
		}

		// Método para reproducir un track específico
		private void PlayTrack(string trackPath)
		{
			try
			{
				StopCurrentMusic();

				Log.Information($"Attempting to play track: {trackPath}");

				// Obtener el streaming source
				var streamingSource = ContentManager.Get<StreamingSource>(trackPath);
				if (streamingSource == null)
				{
					Log.Error($"StreamingSource not found for: {trackPath}");
					string errorMessage = GetLocalizedString("MusicFileNotFound", "Music file not found");
					ShowMessageToAllPlayers(errorMessage, Color.Red);
					return;
				}

				// Crear un nuevo StreamingSound
				m_currentMusic = new StreamingSound(
					streamingSource,
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

				Log.Information($"Playing music: {trackPath}, State: {m_currentMusic.State}, IsPlaying: {m_isPlaying}");

				// Mostrar "Now playing:" en blanco usando la cadena localizada
				string nowPlayingText = GetLocalizedString("NowPlaying", "Now playing:");
				ShowMessageToAllPlayers(nowPlayingText);

				// Programar mostrar el nombre de la canción en verde después de 0.5 segundos
				string displayName = GetTrackDisplayName(trackPath);
				m_pendingTrackName = displayName;
				m_nextTrackNameDisplayTime = m_subsystemTime.GameTime + 0.5;
			}
			catch (Exception ex)
			{
				Log.Error($"Error playing music \"{trackPath}\": {ex.Message}");
				m_currentMusic = null;
				m_isPlaying = false;
				m_currentTrackPath = "";
				string errorMessage = GetLocalizedString("MusicPlayError", "Error playing music");
				ShowMessageToAllPlayers(errorMessage, Color.Red);
			}
		}

		private void PlayRandomMusic()
		{
			if (m_tracks.Length == 0)
			{
				Log.Error("No tracks available to play");
				return;
			}

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

			if (m_availableTracks.Count == 0)
			{
				Log.Error("No available tracks to play");
				return;
			}

			int index = this.m_random.Int(0, this.m_availableTracks.Count - 1);
			int num = this.m_availableTracks[index];
			SubsystemInGameMusic.TrackInfo trackInfo = this.m_tracks[num];
			this.UpdateTrackHistory(num);

			Log.Information($"Selected track {num}: {trackInfo.Path}");
			PlayTrack(trackInfo.Path);
			this.m_musicDuration = (double)trackInfo.Duration;
		}

		private void ScheduleNextMusic()
		{
			if (!m_musicEnabled)
			{
				this.m_nextMusicTime = double.MaxValue;
				return;
			}

			bool flag = SettingsManager.MusicVolume > 0f;
			if (flag && m_musicDuration > 0)
			{
				this.m_nextMusicTime = this.m_subsystemTime.GameTime + this.m_musicDuration;
				Log.Information($"Next music scheduled in {m_musicDuration} seconds");
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

			Log.Information($"SubsystemInGameMusic loaded. Music enabled: {m_musicEnabled}");

			for (int i = 0; i < this.m_tracks.Length; i++)
			{
				this.m_availableTracks.Add(i);
			}

			// Solo programar música si está activada
			if (m_musicEnabled)
			{
				this.m_nextMusicTime = this.m_subsystemTime.GameTime + 2.0; // Esperar 2 segundos al inicio
				Log.Information("Music enabled on load, will start playing soon");
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
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Digimon 02 Target Wada Kouji", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Nichijou Koigokoro Wa Dangan Mo Yawarakakusuru", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Touhou 2 Mimas Theme Complete Darkness", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Touhou 2 Eastern Wind", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Touhou 2 Record of the Sealing of an Oriental Demon", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Digimon 02 Evolution Break Up Ayumi Miyazaki", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Digimon Adventure 01 Brave Heart Wada Kouji", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Digimon Adventure 01 Butterfly Wada Kouji", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Digimon Savers OP1 Theme Song Gouing Going My Soul Dynamite SHU", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Digimon Savers OP2 Hirari Wada Kouji", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/EoSD Credits Theme Crimson Belvedere Eastern Dream", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Digimon Tamers The Biggest Dreamer Wada Kouji", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Touhou 6 Flandre Scarlets Theme U.N. Owen was her", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Digimon Frontiers FIRE Wada Kouji", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Rocket Knight Adventures Stage 1-1", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Rocket Knight Adventures Stage 1-2", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Sparkster (SEGA Genesis) Stage 1-1", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Sparkster (SNES) Stage Lakeside", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Space Harrier Theme", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/MAGICAL SOUND SHOWER OutRun", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Super Hang-On Outride A Crisis", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Super Hang-On Sprinter", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Super Hang-On Winning Run", 177f),
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
