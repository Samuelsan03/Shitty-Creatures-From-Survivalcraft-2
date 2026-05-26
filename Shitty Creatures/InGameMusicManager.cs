using System;
using Engine;
using Engine.Audio;
using Engine.Media;

namespace Game
{
	public static class InGameMusicManager
	{
		private static StreamingSound m_sound;
		private static StreamingSource m_currentSource;
		private static float? m_volume;
		private static string m_currentTrackName;
		private static float m_currentPlaybackPosition; // posición en segundos

		public static bool IsPlaying => m_sound != null && m_sound.State > SoundState.Stopped;
		public static string CurrentTrack => m_currentTrackName;
		public static float CurrentPosition => m_currentPlaybackPosition;

		public static float Volume
		{
			get => m_volume ?? SettingsManager.MusicVolume * 1f;
			set => m_volume = value;
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
				// Guardar información de la pista
				m_currentTrackName = name;
				m_currentPlaybackPosition = startPercentage;

				StopMusic(); // esto limpia el sonido anterior

				float volume = m_volume ?? SettingsManager.MusicVolume * 1f;
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
			m_currentSource = null;
		}

		public static void SavePositionAndStop()
		{
			if (m_sound != null && m_currentSource != null && m_currentTrackName != null)
			{
				// Calcular porcentaje de reproducción actual
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
