using System;
using System.Collections.Generic;
using Engine;
using Engine.Audio;
using Engine.Media;
using TemplatesDatabase;
using GameEntitySystem;

namespace Game
{
    public class SubsystemInGameMusic : Subsystem, IUpdateable
    {
        public SubsystemTime m_subsystemTime;
        public Random m_random = new Random();
        private double m_nextMusicTime = 1.0;
        private double m_musicDuration = 0.0;
        public SubsystemPlayers m_subsystemPlayers;
        private readonly Queue<int> m_recentTracks = new Queue<int>(2);
        private readonly List<int> m_availableTracks = new List<int>();

        // Variables adicionales
        private bool m_musicEnabled = false;
        private ValuesDictionary m_valuesDictionary;
        private StreamingSound m_currentMusic = null;
        private bool m_isPlaying = false;
        private bool m_waitingForCompletion = false; // Nuevo: indica si estamos esperando que termine la canción

        // Variables para mostrar mensajes
        private double m_nextTrackNameDisplayTime = 0.0;
        private string m_pendingTrackName = "";

        // Variables para manejar los botones
        private List<ComponentMusic> m_playerComponents = new List<ComponentMusic>();

        // Lista de canciones con duraciones en segundos (solo para información)
        private readonly SubsystemInGameMusic.TrackInfo[] m_tracks = new SubsystemInGameMusic.TrackInfo[]
        {
            new SubsystemInGameMusic.TrackInfo("MenuMusic/Dragon Quest NES Title Theme", 180f),
            new SubsystemInGameMusic.TrackInfo("MenuMusic/Digimon 02 Target Wada Kouji", 268f),
            // ... (todos los tracks, mantén los que ya tenías)
        };

        private void ShowMessageToAllPlayers(string message, Color? color = null)
        {
            if (m_subsystemPlayers == null)
                return;

            var componentPlayers = m_subsystemPlayers.ComponentPlayers;
            if (componentPlayers.Count == 0)
                return;

            Color messageColor = color ?? Color.White;

            foreach (ComponentPlayer componentPlayer in componentPlayers)
            {
                if (componentPlayer != null && componentPlayer.ComponentGui != null)
                {
                    componentPlayer.ComponentGui.DisplaySmallMessage(message, messageColor, false, false);
                }
            }
        }

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public void Update(float dt)
        {
            // Manejar botones de música para cada jugador
            HandleMusicButtons();

            // Mostrar nombre de la canción con retraso
            if (m_pendingTrackName != "" && m_subsystemTime != null &&
                m_subsystemTime.GameTime >= m_nextTrackNameDisplayTime)
            {
                ShowMessageToAllPlayers(m_pendingTrackName, Color.Green);
                m_pendingTrackName = "";
            }

            // Si la música está desactivada, detener cualquier reproducción
            if (!m_musicEnabled)
            {
                if (m_currentMusic != null && m_currentMusic.State == SoundState.Playing)
                {
                    StopCurrentMusic();
                }
                m_waitingForCompletion = false;
                return;
            }

            // Si no hay música sonando, iniciar una nueva (solo al principio o si falló)
            if (m_currentMusic == null)
            {
                if (!m_waitingForCompletion)
                {
                    PlayRandomMusic();
                }
                return;
            }

            // Detectar si la música ha terminado naturalmente
            if (m_currentMusic.State == SoundState.Stopped)
            {
                // La canción actual ha terminado
                StopCurrentMusic(); // Libera recursos
                PlayRandomMusic();  // Reproduce la siguiente
                return;
            }

            // Si la música sigue sonando, no hacemos nada
        }

        private void HandleMusicButtons()
        {
            if (m_subsystemPlayers == null)
                return;

            var componentPlayers = m_subsystemPlayers.ComponentPlayers;

            for (int i = 0; i < componentPlayers.Count; i++)
            {
                ComponentPlayer player = componentPlayers[i];
                if (player != null && player.ComponentGui != null)
                {
                    ComponentMusic existingComponent = null;
                    foreach (var component in m_playerComponents)
                    {
                        if (component != null && component.m_componentPlayer == player)
                        {
                            existingComponent = component;
                            break;
                        }
                    }

                    if (existingComponent == null)
                    {
                        existingComponent = new ComponentMusic(this, player);
                        m_playerComponents.Add(existingComponent);
                    }

                    existingComponent.Update(0f);
                }
            }

            CleanupPlayerComponents();
        }

        private void CleanupPlayerComponents()
        {
            List<ComponentMusic> componentsToRemove = new List<ComponentMusic>();

            foreach (var component in m_playerComponents)
            {
                if (component == null || component.m_componentPlayer == null || component.m_componentPlayer.ComponentGui == null)
                {
                    componentsToRemove.Add(component);
                }
            }

            foreach (var component in componentsToRemove)
            {
                m_playerComponents.Remove(component);
            }
        }

        private void StopCurrentMusic()
        {
            if (m_currentMusic == null)
                return;

            m_currentMusic.Stop();
            m_currentMusic.Dispose();
            m_currentMusic = null;
            m_isPlaying = false;
            m_waitingForCompletion = false;
        }

        public void ToggleMusic()
        {
            bool oldState = m_musicEnabled;
            m_musicEnabled = !m_musicEnabled;

            if (m_valuesDictionary != null)
            {
                m_valuesDictionary.SetValue<bool>("MusicEnabled", m_musicEnabled);
            }

            if (m_musicEnabled)
            {
                string message = LanguageControl.Get("InGameMusic", "MusicEnabled", "Music enabled");
                ShowMessageToAllPlayers(message);

                // Si no hay música sonando, iniciar una
                if (m_currentMusic == null)
                {
                    PlayRandomMusic();
                }
            }
            else
            {
                string message = LanguageControl.Get("InGameMusic", "MusicDisabled", "Music disabled");
                ShowMessageToAllPlayers(message);

                if (m_currentMusic != null && m_currentMusic.State > SoundState.Stopped)
                {
                    StopCurrentMusic();
                }
                m_waitingForCompletion = false;
            }

            foreach (var component in m_playerComponents)
            {
                if (component != null)
                {
                    component.UpdateButtonText();
                }
            }
        }

        private string GetTrackDisplayName(string trackPath)
        {
            try
            {
                string fileName = System.IO.Path.GetFileName(trackPath);
                if (string.IsNullOrEmpty(fileName))
                    return LanguageControl.Get("InGameMusic", "UnknownTrack", "Unknown Track");

                int dotIndex = fileName.LastIndexOf('.');
                if (dotIndex > 0)
                    fileName = fileName.Substring(0, dotIndex);

                fileName = fileName.Replace('_', ' ').Replace('-', ' ');
                string[] words = fileName.Split(' ');
                for (int i = 0; i < words.Length; i++)
                {
                    if (!string.IsNullOrEmpty(words[i]))
                        words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
                return string.Join(" ", words);
            }
            catch
            {
                return LanguageControl.Get("InGameMusic", "MusicTrack", "Music Track");
            }
        }

        private void PlayTrack(string trackPath)
        {
            try
            {
                StopCurrentMusic();

                var streamingSource = ContentManager.Get<StreamingSource>(trackPath);
                if (streamingSource == null)
                {
                    Log.Error($"StreamingSource not found for: {trackPath}");
                    string errorMessage = LanguageControl.Get("InGameMusic", "MusicFileNotFound", "Music file not found");
                    ShowMessageToAllPlayers(errorMessage, Color.Red);
                    return;
                }

                m_currentMusic = new StreamingSound(
                    streamingSource,
                    SettingsManager.MusicVolume,
                    1f,
                    0f,
                    false,
                    true,
                    1f
                );

                m_isPlaying = true;
                m_waitingForCompletion = true; // Esperamos a que termine
                m_currentMusic.Play();

                string nowPlayingText = LanguageControl.Get("InGameMusic", "NowPlaying", "Now playing:");
                ShowMessageToAllPlayers(nowPlayingText);

                string displayName = GetTrackDisplayName(trackPath);
                m_pendingTrackName = displayName;
                m_nextTrackNameDisplayTime = m_subsystemTime.GameTime + 0.5;
            }
            catch (Exception ex)
            {
                Log.Error($"Error playing music \"{trackPath}\": {ex.Message}");
                m_currentMusic = null;
                m_isPlaying = false;
                m_waitingForCompletion = false;
                string errorMessage = LanguageControl.Get("InGameMusic", "MusicPlayError", "Error playing music");
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

            bool flag = m_availableTracks.Count == 0;
            if (flag)
            {
                for (int i = 0; i < m_tracks.Length; i++)
                {
                    bool flag2 = !m_recentTracks.Contains(i);
                    if (flag2)
                    {
                        m_availableTracks.Add(i);
                    }
                }
                bool flag3 = m_availableTracks.Count == 0;
                if (flag3)
                {
                    m_recentTracks.Clear();
                    for (int j = 0; j < m_tracks.Length; j++)
                    {
                        m_availableTracks.Add(j);
                    }
                }
            }

            if (m_availableTracks.Count == 0)
            {
                Log.Error("No available tracks to play");
                return;
            }

            int index = m_random.Int(0, m_availableTracks.Count - 1);
            int num = m_availableTracks[index];
            SubsystemInGameMusic.TrackInfo trackInfo = m_tracks[num];
            UpdateTrackHistory(num);
            PlayTrack(trackInfo.Path);
            m_musicDuration = (double)trackInfo.Duration; // Solo para info, no se usa para temporización
        }

        private void UpdateTrackHistory(int playedIndex)
        {
            m_recentTracks.Enqueue(playedIndex);
            m_availableTracks.Remove(playedIndex);
            bool flag = m_recentTracks.Count > 2;
            if (flag)
            {
                int item = m_recentTracks.Dequeue();
                bool flag2 = !m_availableTracks.Contains(item);
                if (flag2)
                {
                    m_availableTracks.Add(item);
                }
            }
        }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
            m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
            m_valuesDictionary = valuesDictionary;
            m_musicEnabled = valuesDictionary.GetValue<bool>("MusicEnabled", false);

            for (int i = 0; i < m_tracks.Length; i++)
            {
                m_availableTracks.Add(i);
            }

            if (m_musicEnabled)
            {
                // Iniciamos la primera canción con un pequeño retraso
                m_nextMusicTime = m_subsystemTime.GameTime + 1.0;
            }
            else
            {
                m_nextMusicTime = double.MaxValue;
            }
        }

        public override void Dispose()
        {
            StopCurrentMusic();

            foreach (var component in m_playerComponents)
            {
                if (component != null)
                {
                    if (component.m_componentPlayer != null && component.m_componentPlayer.ComponentGui != null &&
                        component.MusicButton != null)
                    {
                        ContainerWidget rightControlsContainerWidget = component.m_componentPlayer.ComponentGui.m_rightControlsContainerWidget;
                        if (rightControlsContainerWidget != null)
                        {
                            rightControlsContainerWidget.Children.Remove(component.MusicButton);
                        }
                    }
                }
            }

            m_playerComponents.Clear();
            base.Dispose();
        }

        public bool IsMusicEnabled => m_musicEnabled;

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

    public class ComponentMusic
    {
        private SubsystemInGameMusic m_subsystemMusic;
        public ComponentPlayer m_componentPlayer;
        public BevelledButtonWidget MusicButton;
        private bool m_buttonClicked;

        public ComponentMusic(SubsystemInGameMusic subsystemMusic, ComponentPlayer player)
        {
            m_subsystemMusic = subsystemMusic;
            m_componentPlayer = player;
            CreateButton();
        }

        public void Update(float dt)
        {
            if (MusicButton == null || m_componentPlayer == null || m_componentPlayer.ComponentGui == null)
            {
                CreateButton();
                return;
            }

            if (MusicButton.IsClicked && !m_buttonClicked)
            {
                m_buttonClicked = true;
                m_subsystemMusic.ToggleMusic();
                UpdateButtonText();
            }
            else if (!MusicButton.IsClicked && m_buttonClicked)
            {
                m_buttonClicked = false;
            }
        }

        private void CreateButton()
        {
            if (m_componentPlayer == null || m_componentPlayer.ComponentGui == null)
                return;

            ContainerWidget rightControlsContainerWidget = m_componentPlayer.ComponentGui.m_rightControlsContainerWidget;
            if (rightControlsContainerWidget == null)
                return;

            MusicButton = rightControlsContainerWidget.Children.Find<BevelledButtonWidget>("InGameMusicButton", false);

            if (MusicButton != null)
            {
                UpdateButtonText();
                return;
            }

            MusicButton = new BevelledButtonWidget
            {
                Name = "InGameMusicButton",
                Text = "",
                Size = new Vector2(88f, 56f),
                IsEnabled = true,
                IsVisible = true,
                HorizontalAlignment = WidgetAlignment.Far,
                IsAutoCheckingEnabled = false
            };

            if (MusicButton.m_labelWidget != null)
            {
                MusicButton.m_labelWidget.FontScale = 0.8f;
            }

            rightControlsContainerWidget.Children.Add(MusicButton);
            UpdateButtonText();
        }

        public void UpdateButtonText()
        {
            if (MusicButton == null) return;

            string buttonText = LanguageControl.Get("InGameMusic", "MusicToggleButton", "Music");
            string statusText = m_subsystemMusic.IsMusicEnabled ?
                LanguageControl.Get("InGameMusic", "MusicOn", "ON") :
                LanguageControl.Get("InGameMusic", "MusicOff", "OFF");

            MusicButton.Text = $"{buttonText} {statusText}";
        }
    }
}
