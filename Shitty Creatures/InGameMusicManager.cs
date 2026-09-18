using System;
using Engine;
using Engine.Audio;
using Engine.Media;

namespace Game
{
	public static class InGameMusicManager
	{
		public enum MusicContext
		{
			None,
			InGame,
			Chase,
			Achievement
		}

		// Duración total del fade-out en segundos (independiente de Volume y FPS).
		private const double FadeOutDuration = 1.5;

		private static StreamingSound m_sound;
		private static StreamingSound m_fadeSound;
		private static StreamingSource m_currentSource;
		private static float? m_volume;
		private static string m_currentTrackName;
		private static float m_currentPlaybackPosition;
		private static bool m_isFadingOut;
		private static MusicContext m_currentContext;
		private static bool m_isPausedByScreenChange;

		// Estado del fade basado en tiempo.
		private static double m_fadeStartTime;
		private static float m_fadeStartVolume;

		public static bool IsPlaying => m_sound != null && m_sound.State > SoundState.Stopped;
		public static bool IsFadingOut => m_isFadingOut;
		public static bool IsPaused => m_isPausedByScreenChange;
		public static string CurrentTrack => m_currentTrackName;
		public static float CurrentPosition => m_currentPlaybackPosition;
		public static MusicContext CurrentContext => m_currentContext;

		public static float Volume
		{
			get => m_volume ?? SettingsManager.MusicVolume * 1f;
			set => m_volume = value;
		}

		public static bool CanPlayInContext(MusicContext context)
		{
			if (!IsPlaying && !IsFadingOut && !m_isPausedByScreenChange)
				return true;

			int requestedPriority = GetContextPriority(context);
			int currentPriority = GetContextPriority(m_currentContext);

			return requestedPriority >= currentPriority;
		}

		private static int GetContextPriority(MusicContext context)
		{
			switch (context)
			{
				case MusicContext.InGame: return 0;
				case MusicContext.Chase: return 1;
				case MusicContext.Achievement: return 2;
				default: return -1;
			}
		}

		public static bool IsPlaybackComplete()
		{
			if (m_sound == null || m_currentSource == null)
				return true;

			if (m_sound.State <= SoundState.Stopped)
				return true;

			try
			{
				long totalBytes = m_currentSource.BytesCount;
				if (totalBytes <= 0) return false;

				long currentPos = m_currentSource.Position;
				float progress = (float)currentPos / totalBytes;
				return progress >= 0.98f;
			}
			catch
			{
				return true;
			}
		}

		public static void Update()
		{
			// ---------------------------------------------------------------
			// Fade out basado en TIEMPO REAL: no depende de Volume ni de FPS,
			// así que siempre progresa hasta terminar, incluso si el jugador
			// murió, la presa murió, o la persecución terminó.
			// ---------------------------------------------------------------
			if (m_fadeSound != null)
			{
				double elapsed = Time.RealTime - m_fadeStartTime;
				float t = MathUtils.Saturate((float)(elapsed / FadeOutDuration));
				float newVolume = m_fadeStartVolume * (1f - t);

				if (t >= 1f || newVolume <= 0.001f)
				{
					try
					{
						m_fadeSound.Stop();
						m_fadeSound.Dispose();
					}
					catch { }
					m_fadeSound = null;
					m_isFadingOut = false;

					// Solo resetear contexto si NO hay música nueva sonando ya.
					if (m_sound == null)
					{
						m_currentContext = MusicContext.None;
					}
				}
				else
				{
					m_fadeSound.Volume = newVolume;
				}
			}

			// Handle pause/resume based on screen state
			bool isGameScreenActive = ScreensManager.CurrentScreen is GameScreen;

			if (!m_isPausedByScreenChange && IsPlaying && !m_isFadingOut && !isGameScreenActive)
			{
				SavePositionAndStop();
				m_isPausedByScreenChange = true;
			}
			else if (m_isPausedByScreenChange && isGameScreenActive && !string.IsNullOrEmpty(m_currentTrackName))
			{
				RestartFromSavedPosition();
				m_isPausedByScreenChange = false;
			}
		}

		public static void FadeOutAndStop()
		{
			if (m_sound == null && m_fadeSound == null) return;

			m_isFadingOut = true;
			m_isPausedByScreenChange = false;

			if (m_sound != null)
			{
				if (m_fadeSound != null)
				{
					// Ya había un fade en curso: lo matamos para no acumular.
					try
					{
						m_fadeSound.Stop();
						m_fadeSound.Dispose();
					}
					catch { }
					m_fadeSound = null;
				}

				m_fadeSound = m_sound;
				m_fadeStartTime = Time.RealTime;
				m_fadeStartVolume = MathUtils.Max(m_sound.Volume, 0f);
				m_sound = null;
			}

			m_currentSource = null;
		}

		public static void PlayMusic(string name, float startPercentage)
		{
			PlayMusic(name, startPercentage, MusicContext.InGame);
		}

		public static void PlayMusic(string name, float startPercentage, MusicContext context)
		{
			if (string.IsNullOrEmpty(name))
			{
				StopMusic();
				return;
			}

			try
			{
				m_currentTrackName = name;
				m_currentPlaybackPosition = startPercentage;
				m_isFadingOut = false;
				m_isPausedByScreenChange = false;
				m_currentContext = context;

				if (m_sound != null)
				{
					m_sound.Stop();
					m_sound.Dispose();
					m_sound = null;
				}
				m_currentSource = null;

				// La música nueva arranca SIEMPRE a volumen completo.
				// Si hay un fade-out en curso, se deja correr en paralelo.
				float volume = Volume;

				StreamingSource source = ContentManager.Get<StreamingSource>(name);
				source = source.Duplicate();
				m_currentSource = source;

				if (startPercentage > 0f)
				{
					long position = (long)(MathUtils.Saturate(startPercentage) * (float)(source.BytesCount / (long)source.ChannelsCount / 2L));
					position = position / 16L * 16L;
					source.Position = position;
				}

				m_sound = new StreamingSound(source, volume, 1f, 0f, false, true, 1f);
				m_sound.Play();
			}
			catch
			{
				Log.Warning("Error playing music \"" + name + "\".");
			}
		}

		public static void StopMusic()
		{
			if (m_sound != null)
			{
				m_sound.Stop();
				m_sound.Dispose();
				m_sound = null;
			}
			if (m_fadeSound != null)
			{
				m_fadeSound.Stop();
				m_fadeSound.Dispose();
				m_fadeSound = null;
			}
			m_currentSource = null;
			m_isFadingOut = false;
			m_isPausedByScreenChange = false;
			m_currentContext = MusicContext.None;
		}

		public static void SavePositionAndStop()
		{
			if (m_sound != null && m_currentSource != null && m_currentTrackName != null)
			{
				long totalBytes = m_currentSource.BytesCount;
				long currentPos = m_currentSource.Position;
				float percent = (float)currentPos / (float)totalBytes;
				m_currentPlaybackPosition = MathUtils.Saturate(percent);
			}

			if (m_sound != null)
			{
				m_sound.Stop();
				m_sound.Dispose();
				m_sound = null;
			}
			if (m_fadeSound != null)
			{
				m_fadeSound.Stop();
				m_fadeSound.Dispose();
				m_fadeSound = null;
			}
			m_currentSource = null;
			m_isFadingOut = false;
		}

		public static void RestartFromSavedPosition()
		{
			if (!string.IsNullOrEmpty(m_currentTrackName))
			{
				PlayMusic(m_currentTrackName, m_currentPlaybackPosition, m_currentContext);
			}
		}
	}
}
