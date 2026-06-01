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
		private static float m_currentPlaybackPosition;

		public static bool IsPlaying => m_sound != null && m_sound.State > SoundState.Stopped;
		public static string CurrentTrack => m_currentTrackName;
		public static float CurrentPosition => m_currentPlaybackPosition;

		public static float Volume
		{
			get => m_volume ?? SettingsManager.MusicVolume * 1f;
			set => m_volume = value;
		}

		/// <summary>
		/// Verifica si la reproducción actual ha terminado completamente.
		/// Esto es más confiable que solo verificar IsPlaying porque algunos
		/// StreamingSound no cambian su estado a Stopped automáticamente.
		/// </summary>
		public static bool IsPlaybackComplete()
		{
			if (m_sound == null || m_currentSource == null)
				return true;

			// Si el sonido ya no está reproduciéndose, está completo
			if (m_sound.State <= SoundState.Stopped)
				return true;

			// Verificar si llegamos al final del stream por posición
			try
			{
				long totalBytes = m_currentSource.BytesCount;
				if (totalBytes <= 0) return false;

				long currentPos = m_currentSource.Position;
				float progress = (float)currentPos / totalBytes;
				return progress >= 0.98f; // 98% o más = al final
			}
			catch
			{
				return true;
			}
		}

		public static void FadeOutAndStop(float fadeDuration = 3f)
		{
			if (m_sound == null) return;
			float startVolume = m_sound.Volume; // Usar volumen actual del sonido
			double startTime = Time.RealTime;
			Action fadeStep = null;
			fadeStep = () => {
				if (m_sound == null) return;
				float elapsed = (float)(Time.RealTime - startTime);
				if (elapsed >= fadeDuration)
				{
					StopMusic();
					return;
				}
				float t = elapsed / fadeDuration;
				float newVol = MathUtils.Lerp(startVolume, 0f, t);
				m_sound.Volume = newVol;
				GameManager.SyncDispatcher.Add(() => { fadeStep(); return true; });
			};
			fadeStep();
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

				StopMusic(); // Limpia el sonido anterior

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
