using Engine;
using Engine.Audio;
using Engine.Media;
using TemplatesDatabase;
using System.Collections.Generic;
using GameEntitySystem;

namespace Game
{
	public class SubsystemGhostNormalChaseMusic : Subsystem, IUpdateable
	{
		#region Constants

		private const string MUSIC_PATH = "MenuMusic/ChaseTheme/Hotel Insanity Chase Theme";
		private const float MUSIC_DURATION = 32.0f; // 00:32 segundos exactos
		private const float CHECK_INTERVAL = 0.1f;
		private const float DETECTION_RADIUS = 20f;

		#endregion

		#region Fields

		private bool m_isChaseActive = false;
		private float m_timeSinceLastCheck = 0f;
		private float m_timeSinceMusicStarted = 0f;
		private bool m_musicPlaying = false;

		private SubsystemTime m_subsystemTime;
		private SubsystemPlayers m_subsystemPlayers;

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
				// Para música de 32s, verificar a los 31.36s (98%)
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
				m_isChaseActive = CheckForActiveGhosts();

				// Cambio de estado
				if (wasChaseActive != m_isChaseActive)
				{
					if (m_isChaseActive)
					{
						StartChaseMusicImmediately();
					}
					else
					{
						StopChaseMusicImmediately();
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

		private bool CheckForActiveGhosts()
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

					if (entityName != "GhostNormal" && entityName != "GhostFast" && entityName !="PoisonousGhost" && entityName != "GhostCharger" && entityName != "GhostBoomer1" && entityName != "GhostBoomer2" && entityName != "GhostBoomer2" && entityName != "GhostBoomer3")
						continue;

					ComponentHealth health = entity.FindComponent<ComponentHealth>();
					if (health != null && health.Health <= 0f)
						continue;

					ComponentZombieChaseBehavior chaseBehavior = entity.FindComponent<ComponentZombieChaseBehavior>();
					if (chaseBehavior != null && chaseBehavior.IsActive)
					{
						return true;
					}

					ComponentBody ghostBody = entity.FindComponent<ComponentBody>();
					if (ghostBody != null)
					{
						foreach (ComponentPlayer player in activePlayers)
						{
							ComponentBody playerBody = player.Entity.FindComponent<ComponentBody>();
							if (playerBody != null)
							{
								float distance = (ghostBody.Position - playerBody.Position).Length();
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

				Log.Debug($"Ghost chase music started IMMEDIATELY (no fade)");
			}
			catch (System.Exception ex)
			{
				Log.Error($"Error starting ghost chase music: {ex.Message}");
				m_musicPlaying = false;
			}
		}

		private void RestartMusicImmediately()
		{
			if (!m_isChaseActive || !m_musicPlaying)
				return;

			try
			{
				Log.Debug($"Restarting ghost music at {m_timeSinceMusicStarted:F2}s");

				// Usar el mismo método para reinicio - SIN FADE
				StartChaseMusicImmediately();

				Log.Debug($"Ghost chase music restarted IMMEDIATELY");
			}
			catch (System.Exception ex)
			{
				Log.Error($"Error restarting ghost chase music: {ex.Message}");
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

					Log.Debug("Ghost chase music stopped IMMEDIATELY (no fade)");
				}
				catch (System.Exception ex)
				{
					Log.Error($"Error stopping ghost chase music: {ex.Message}");
				}
			}
		}

		#endregion

		#region Public API

		public void ForcePlayChaseMusic()
		{
			m_isChaseActive = true;
			StartChaseMusicImmediately();
		}

		public void ForceStopChaseMusic()
		{
			m_isChaseActive = false;
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
