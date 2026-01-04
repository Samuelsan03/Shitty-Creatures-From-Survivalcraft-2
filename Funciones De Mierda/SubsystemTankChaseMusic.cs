using Engine;
using Engine.Audio;
using Engine.Media;
using TemplatesDatabase;
using System.Collections.Generic;
using GameEntitySystem;

namespace Game
{
	public class SubsystemTankChaseMusic : Subsystem, IUpdateable
	{
		#region Constants

		private const string MUSIC_PATH = "MenuMusic/ChaseTheme/Tank Theme";
		private const string ALERT_SOUND_PATH = "Audio/UI/Tank Warning Sound";
		private const float MUSIC_DURATION = 52.0f; // 00:52 segundos exactos
		private const float CHECK_INTERVAL = 0.1f;
		private const float DETECTION_RADIUS = 20f;

		#endregion

		#region Fields

		private bool m_isChaseActive = false;
		private bool m_alertShown = false;
		private float m_timeSinceLastCheck = 0f;
		private float m_timeSinceMusicStarted = 0f;
		private bool m_musicPlaying = false;

		private SubsystemTime m_subsystemTime;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemAudio m_subsystemAudio;

		#endregion

		#region Properties

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public bool IsChaseActive => m_isChaseActive;

		public bool IsMusicPlaying => m_musicPlaying;

		#endregion

		#region Initialization

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
		}

		#endregion

		#region Update Loop

		public void Update(float dt)
		{
			m_timeSinceLastCheck += dt;

			// Si la música está sonando, actualizar el timer
			if (m_musicPlaying)
			{
				m_timeSinceMusicStarted += dt;

				// Verificar si la música está por terminar (98% de la duración)
				// Para música de 52s, verificar a los 50.96s (98%)
				if (m_timeSinceMusicStarted >= MUSIC_DURATION * 0.98f)
				{
					// Reiniciar la música desde el principio SIN FADE
					RestartMusicImmediately();
				}
			}

			if (m_timeSinceLastCheck >= CHECK_INTERVAL)
			{
				m_timeSinceLastCheck = 0f;

				bool wasChaseActive = m_isChaseActive;
				m_isChaseActive = CheckForActiveTanks();

				// Cambio de estado
				if (wasChaseActive != m_isChaseActive)
				{
					if (m_isChaseActive)
					{
						StartChaseMusicImmediately();
						PlayAlertSound();

						if (!m_alertShown)
						{
							ShowAlertMessage();
							m_alertShown = true;
						}
					}
					else
					{
						StopChaseMusicImmediately();
						m_alertShown = false;
					}
				}
			}

			// Verificación de seguridad
			if (m_isChaseActive && !m_musicPlaying)
			{
				StartChaseMusicImmediately();
			}
		}

		#endregion

		#region Chase Detection

		private bool CheckForActiveTanks()
		{
			if (Project == null || m_subsystemPlayers == null)
				return false;

			var activePlayers = GetActivePlayers();
			if (activePlayers.Count == 0)
				return false;

			foreach (Entity entity in Project.Entities)
			{
				try
				{
					string entityName = entity.ValuesDictionary.DatabaseObject.Name;

					if (entityName != "Tank1" && entityName != "Tank2" && entityName != "Tank3")
						continue;

					ComponentHealth health = entity.FindComponent<ComponentHealth>();
					if (health != null && health.Health <= 0f)
						continue;

					ComponentZombieChaseBehavior chaseBehavior = entity.FindComponent<ComponentZombieChaseBehavior>();
					if (chaseBehavior != null && chaseBehavior.IsActive)
					{
						return true;
					}

					ComponentBody tankBody = entity.FindComponent<ComponentBody>();
					if (tankBody != null)
					{
						foreach (ComponentPlayer player in activePlayers)
						{
							ComponentBody playerBody = player.Entity.FindComponent<ComponentBody>();
							if (playerBody != null)
							{
								float distance = (tankBody.Position - playerBody.Position).Length();
								if (distance < DETECTION_RADIUS)
								{
									return true;
								}
							}
						}
					}
				}
				catch (System.Exception)
				{
					// Ignorar errores
				}
			}

			return false;
		}

		private List<ComponentPlayer> GetActivePlayers()
		{
			var activePlayers = new List<ComponentPlayer>();

			if (m_subsystemPlayers == null)
				return activePlayers;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				ComponentHealth playerHealth = player.Entity.FindComponent<ComponentHealth>();
				if (playerHealth != null && playerHealth.Health > 0f)
				{
					activePlayers.Add(player);
				}
			}

			return activePlayers;
		}

		#endregion

		#region Music Control - SIN FADE

		private void StartChaseMusicImmediately()
		{
			try
			{
				// Detener cualquier música previa SIN FADE
				if (MusicManager.m_sound != null)
				{
					MusicManager.m_sound.Stop();
					MusicManager.m_sound.Dispose();
					MusicManager.m_sound = null;
				}

				// Limpiar fade sound si existe
				if (MusicManager.m_fadeSound != null)
				{
					MusicManager.m_fadeSound.Dispose();
					MusicManager.m_fadeSound = null;
				}

				// Obtener y reproducir música SIN FADE
				var streamingSource = ContentManager.Get<StreamingSource>(MUSIC_PATH);
				if (streamingSource == null)
				{
					Log.Warning($"Music not found: {MUSIC_PATH}");
					return;
				}

				var duplicateSource = streamingSource.Duplicate();

				// Crear sonido con volumen COMPLETO desde el inicio (1f)
				var sound = new StreamingSound(duplicateSource, 1f, 1f, 0f, false, true, 1f);

				// Asignar directamente al MusicManager
				MusicManager.m_sound = sound;
				MusicManager.m_currentMix = MusicManager.Mix.Other; // Evitar que el MusicManager interfiera
				MusicManager.m_fadeStartTime = 0.0; // Desactivar fade

				sound.Play();
				m_musicPlaying = true;
				m_timeSinceMusicStarted = 0f;

				Log.Debug($"Tank chase music started IMMEDIATELY (no fade)");
			}
			catch (System.Exception ex)
			{
				Log.Error($"Error starting tank chase music: {ex.Message}");
				m_musicPlaying = false;
			}
		}

		private void RestartMusicImmediately()
		{
			if (!m_isChaseActive || !m_musicPlaying)
				return;

			try
			{
				Log.Debug($"Restarting tank music at {m_timeSinceMusicStarted:F2}s");

				// Usar el mismo método para reinicio - SIN FADE
				StartChaseMusicImmediately();

				Log.Debug($"Tank chase music restarted IMMEDIATELY");
			}
			catch (System.Exception ex)
			{
				Log.Error($"Error restarting tank chase music: {ex.Message}");
				m_musicPlaying = false;
			}
		}

		private void StopChaseMusicImmediately()
		{
			if (m_musicPlaying)
			{
				try
				{
					// Detener inmediatamente SIN FADE
					if (MusicManager.m_sound != null)
					{
						MusicManager.m_sound.Stop();
						MusicManager.m_sound.Dispose();
						MusicManager.m_sound = null;
					}

					// Limpiar fade sound
					if (MusicManager.m_fadeSound != null)
					{
						MusicManager.m_fadeSound.Dispose();
						MusicManager.m_fadeSound = null;
					}

					m_musicPlaying = false;
					m_timeSinceMusicStarted = 0f;
					m_alertShown = false;

					Log.Debug("Tank chase music stopped IMMEDIATELY (no fade)");
				}
				catch (System.Exception ex)
				{
					Log.Error($"Error stopping tank chase music: {ex.Message}");
				}
			}
		}

		#endregion

		#region Alert System

		private void PlayAlertSound()
		{
			if (m_subsystemAudio == null) return;

			try
			{
				m_subsystemAudio.PlaySound(ALERT_SOUND_PATH, 1f, 0f, 0f, 0.0001f);
			}
			catch (System.Exception)
			{
				// Ignorar errores de sonido
			}
		}

		private void ShowAlertMessage()
		{
			if (m_subsystemPlayers == null) return;

			var componentPlayers = m_subsystemPlayers.ComponentPlayers;
			if (componentPlayers.Count == 0) return;

			string message;
			bool translationFound;

			message = LanguageControl.Get(out translationFound, "Messages", "TankChaseAlert");

			if (!translationFound)
			{
				message = "ALERT!\n A Tank has appeared!\n Take refuge, find good weapons to kill it or use the zombie collar to calm its rage and turn it into an ally!";
			}

			foreach (ComponentPlayer componentPlayer in componentPlayers)
			{
				if (componentPlayer != null && componentPlayer.ComponentGui != null)
				{
					componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.Red, false, false);
				}
			}
		}

		#endregion

		#region Public API

		public void ForcePlayChaseMusic()
		{
			m_isChaseActive = true;
			m_alertShown = false;
			StartChaseMusicImmediately();
			PlayAlertSound();
			ShowAlertMessage();
		}

		public void ForceStopChaseMusic()
		{
			m_isChaseActive = false;
			m_alertShown = false;
			StopChaseMusicImmediately();
		}

		#endregion

		#region Cleanup

		public override void Dispose()
		{
			StopChaseMusicImmediately();
			base.Dispose();
		}

		#endregion
	}
}
