using System;
using Engine;
using Engine.Audio;
using Engine.Media;

namespace Game
{
	public static class InGameMusicManager
	{
		private static StreamingSound m_sound;
		private static StreamingSound m_fadeSound;
		private static StreamingSource m_currentSource;
		private static float? m_volume;
		private static string m_currentTrackName;
		private static float m_currentPlaybackPosition;
		private static bool m_isFadingOut;

		public static bool IsPlaying => m_sound != null && m_sound.State > SoundState.Stopped;
		public static bool IsFadingOut => m_isFadingOut;
		public static string CurrentTrack => m_currentTrackName;
		public static float CurrentPosition => m_currentPlaybackPosition;

		public static float Volume
		{
			get => m_volume ?? SettingsManager.MusicVolume * 1f;
			set => m_volume = value;
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
			if (m_fadeSound != null)
			{
				// Reducir volumen gradualmente
				float newVolume = m_fadeSound.Volume - 0.33f * Volume * Time.FrameDuration;

				if (newVolume <= 0f)
				{
					// Fade terminado - ahora sí detener y liberar
					m_fadeSound.Stop();
					m_fadeSound.Dispose();
					m_fadeSound = null;
					m_isFadingOut = false;
				}
				else
				{
					m_fadeSound.Volume = newVolume;
				}
			}
		}

		public static void FadeOutAndStop()
		{
			if (m_sound == null && m_fadeSound == null) return;

			m_isFadingOut = true;

			if (m_sound != null)
			{
				// Limpiar fade anterior si existe
				if (m_fadeSound != null)
				{
					m_fadeSound.Stop();
					m_fadeSound.Dispose();
					m_fadeSound = null;
				}

				// MOVER sin llamar Stop() - el sonido sigue reproduciéndose
				m_fadeSound = m_sound;
				m_sound = null;
			}

			m_currentSource = null;
		}

		public static void PlayMusic(string name, float startPercentage)
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

				// No llamar StopMusic() completo si hay fade activo
				if (m_sound != null)
				{
					m_sound.Stop();
					m_sound.Dispose();
					m_sound = null;
				}
				m_currentSource = null;

				// Si hay fade, no interrumpirlo (igual que MusicManager)
				float volume = (m_fadeSound != null) ? 0f : Volume;

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
			StopMusic();
		}

		public static void RestartFromSavedPosition()
		{
			if (!string.IsNullOrEmpty(m_currentTrackName))
			{
				PlayMusic(m_currentTrackName, m_currentPlaybackPosition);
			}
		}
	}
}
