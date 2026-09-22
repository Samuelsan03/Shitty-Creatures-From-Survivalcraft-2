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
			Achievement,
			Death
		}

		// ─────────────────────────────────────────────
		//  Fade — estilo MusicManager original
		// ─────────────────────────────────────────────

		/// <summary>
		/// Velocidad de fade en unidades de volumen por segundo.
		/// Se calcula desde la duración que pasa el subsistema en
		/// FadeOutAndStop(double fadeDuration).  No es un campo
		/// "por defecto" expuesto: es estado interno que se
		/// sobrescribe cada vez que el subsistema lo solicita.
		/// </summary>
		private static float m_fadeSpeed = 0.66f;

		/// <summary>
		/// Espera (en segundos) antes de que arranque el fade-in
		/// del track nuevo cuando hay un fade-out en curso.
		/// Equivalente al m_fadeWait del MusicManager original.
		/// 0 = crossfade inmediato.
		/// </summary>
		private static float m_fadeWait = 0f;

		/// <summary>
		/// Momento (FrameStartTime) a partir del cual el track
		/// actual debe empezar a subir volumen.  Equivalente al
		/// m_fadeStartTime del original.
		/// </summary>
		private static double m_fadeStartTime;

		// ─────────────────────────────────────────────
		//  Estado de reproducción
		// ─────────────────────────────────────────────

		private static StreamingSound m_sound;
		private static StreamingSound m_fadeSound;
		private static StreamingSource m_currentSource;
		private static float? m_volume;
		private static string m_currentTrackName;
		private static float m_currentPlaybackPosition;
		private static bool m_isFadingOut;
		private static MusicContext m_currentContext;
		private static bool m_isPausedByScreenChange;
		private static bool m_loopCurrentTrack = false;

		// ─────────────────────────────────────────────
		//  Propiedades públicas
		// ─────────────────────────────────────────────

		public static bool IsPlaying => m_sound != null && m_sound.State > SoundState.Stopped;
		public static bool IsFadingOut => m_isFadingOut;
		public static bool IsPaused => m_isPausedByScreenChange;
		public static string CurrentTrack => m_currentTrackName;
		public static float CurrentPosition => m_currentPlaybackPosition;
		public static MusicContext CurrentContext => m_currentContext;
		public static bool IsLooping => m_loopCurrentTrack;

		public static float Volume
		{
			get => m_volume ?? SettingsManager.MusicVolume * 1f;
			set => m_volume = value;
		}

		// ─────────────────────────────────────────────
		//  Prioridad de contextos
		// ─────────────────────────────────────────────

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
				case MusicContext.Death: return 3;   // prioridad máxima
				default: return -1;
			}
		}

		// ─────────────────────────────────────────────
		//  Comprobación de fin de pista
		// ─────────────────────────────────────────────

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

		// ─────────────────────────────────────────────
		//  Update
		// ─────────────────────────────────────────────

		public static void Update()
		{
			// ── Fade-OUT (igual que MusicManager original) ──
			//
			//  Volumen baja:  m_fadeSound.Volume -= m_fadeSpeed * Volume * FrameDuration
			//
			//  Usa Time.FrameDuration (tiempo real de frame), igual que el original.
			//  No depende de RealTime ni de un timestamp congelado.
			//
			if (m_fadeSound != null)
			{
				m_fadeSound.Volume = MathUtils.Min(
					m_fadeSound.Volume - m_fadeSpeed * Volume * Time.FrameDuration,
					Volume);

				if (m_fadeSound.Volume <= 0f)
				{
					try { m_fadeSound.Dispose(); } catch { }
					m_fadeSound = null;

					// Solo resetear contexto si estábamos fading-out para parar
					// y no hay un track nuevo sonando.
					if (m_isFadingOut && m_sound == null)
						m_currentContext = MusicContext.None;

					m_isFadingOut = false;
				}
			}

			// ── Fade-IN (igual que MusicManager original) ──
			//
			//  Tras m_fadeWait segundos, el volumen sube:
			//    m_sound.Volume += m_fadeSpeed * Volume * FrameDuration
			//
			if (m_sound != null && Time.FrameStartTime >= m_fadeStartTime)
			{
				m_sound.Volume = MathUtils.Min(
					m_sound.Volume + m_fadeSpeed * Volume * Time.FrameDuration,
					Volume);
			}

			// ── Loop ──
			if (m_loopCurrentTrack && !m_isFadingOut && !m_isPausedByScreenChange && m_sound != null)
			{
				if (IsPlaybackComplete())
				{
					string trackToRestart = m_currentTrackName;
					MusicContext contextRestart = m_currentContext;
					PlayMusic(trackToRestart, 0f, contextRestart, true);
				}
			}

			// ── Pausa / reanudación por cambio de pantalla ──
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

		// ─────────────────────────────────────────────
		//  FadeOutAndStop
		// ─────────────────────────────────────────────

		/// <summary>
		/// Fade-out con duración personalizada (en segundos).
		/// Convierte la duración a velocidad de fade y delega
		/// al FadeOutAndStop() sin parámetros.
		/// </summary>
		public static void FadeOutAndStop(double fadeDuration)
		{
			// speed = 1 / duración  →  volumen por segundo
			m_fadeSpeed = 1f / MathUtils.Max((float)fadeDuration, 0.001f);
			FadeOutAndStop();
		}

		/// <summary>
		/// Fade-out con la velocidad de fade actual (m_fadeSpeed).
		/// Mueve m_sound a m_fadeSound para que Update() lo vaya
		/// bajando, igual que StopMusic() del MusicManager original.
		/// </summary>
		public static void FadeOutAndStop()
		{
			if (m_sound == null && m_fadeSound == null) return;

			m_isFadingOut = true;
			m_isPausedByScreenChange = false;

			// Al iniciar el fade-out, desactivamos el loop para que
			// no se reinicie a mitad del fade.
			m_loopCurrentTrack = false;

			if (m_sound != null)
			{
				if (m_fadeSound != null)
				{
					// Ya había un fade en curso: lo matamos para no acumular.
					try { m_fadeSound.Stop(); m_fadeSound.Dispose(); } catch { }
					m_fadeSound = null;
				}

				// Mover el sonido actual a fade (como en el original).
				m_fadeSound = m_sound;
				m_sound = null;
			}

			m_currentSource = null;
		}

		// ─────────────────────────────────────────────
		//  PlayMusic
		// ─────────────────────────────────────────────

		public static void PlayMusic(string name, float startPercentage)
		{
			PlayMusic(name, startPercentage, MusicContext.InGame, false);
		}

		public static void PlayMusic(string name, float startPercentage, MusicContext context)
		{
			PlayMusic(name, startPercentage, context, false);
		}

		public static void PlayMusic(string name, float startPercentage, MusicContext context, bool loop)
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
				m_loopCurrentTrack = loop;

				// Mover el sonido actual a fade (como StopMusic del original).
				// El track viejo baja volumen mientras el nuevo sube.
				if (m_sound != null)
				{
					if (m_fadeSound != null)
					{
						try { m_fadeSound.Stop(); m_fadeSound.Dispose(); } catch { }
					}
					m_fadeSound = m_sound;
					m_sound = null;
				}
				m_currentSource = null;

				// Como en el original: si hay un fade en curso, el track
				// nuevo arranca a volumen 0 y sube; si no, arranca a tope.
				m_fadeStartTime = Time.FrameStartTime + (double)m_fadeWait;
				float volume = (m_fadeSound != null) ? 0f : Volume;

				StreamingSource source = ContentManager.Get<StreamingSource>(name);
				source = source.Duplicate();
				m_currentSource = source;

				if (startPercentage > 0f)
				{
					long position = (long)(MathUtils.Saturate(startPercentage)
									 * (float)(source.BytesCount / (long)source.ChannelsCount / 2L));
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

		// ─────────────────────────────────────────────
		//  StopMusic — parada dura (sin fade)
		// ─────────────────────────────────────────────

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
			m_loopCurrentTrack = false;
		}

		// ─────────────────────────────────────────────
		//  Guardar / reanudar posición (cambio de pantalla)
		// ─────────────────────────────────────────────

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
				PlayMusic(m_currentTrackName, m_currentPlaybackPosition, m_currentContext, m_loopCurrentTrack);
			}
		}
	}
}
