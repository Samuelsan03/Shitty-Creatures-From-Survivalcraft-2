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
		private const float MUSIC_DURATION = 32.0f;
		private const float CHECK_INTERVAL = 0.1f;
		private const float DETECTION_RADIUS = 20f;

		#endregion

		#region Fields

		private bool m_isChaseActive = false;
		private float m_timeSinceLastCheck = 0f;
		private float m_timeSinceMusicStarted = 0f;
		private bool m_musicPlaying = false;
		private bool m_wasMusicEnabled = true;

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
			m_wasMusicEnabled = ChaseMusicConfig.GhostMusicEnabled;
		}

		#endregion

		#region Update Loop

		public void Update(float dt)
		{
			// Verificar si la configuración cambió mientras la música sonaba
			if (m_wasMusicEnabled != ChaseMusicConfig.GhostMusicEnabled)
			{
				m_wasMusicEnabled = ChaseMusicConfig.GhostMusicEnabled;
				if (!ChaseMusicConfig.GhostMusicEnabled && m_musicPlaying)
				{
					StopChaseMusicImmediately();
				}
			}

			m_timeSinceLastCheck += dt;

			if (m_musicPlaying)
			{
				m_timeSinceMusicStarted += dt;

				if (m_timeSinceMusicStarted >= MUSIC_DURATION * 0.98f)
				{
					RestartMusicImmediately();
				}
			}

			if (m_timeSinceLastCheck >= CHECK_INTERVAL)
			{
				m_timeSinceLastCheck = 0f;

				bool wasChaseActive = m_isChaseActive;
				m_isChaseActive = CheckForActiveGhosts();

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

			if (m_isChaseActive && !m_musicPlaying && ChaseMusicConfig.GhostMusicEnabled)
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

					if (entityName != "GhostNormal" && entityName != "GhostFast" && entityName != "PoisonousGhost" &&
						entityName != "GhostCharger" && entityName != "GhostBoomer1" && entityName != "GhostBoomer2" &&
						entityName != "GhostBoomer3")
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

		#region Music Control

		private void StartChaseMusicImmediately()
		{
			// NUEVO: Verificar si la música está habilitada
			if (!ChaseMusicConfig.GhostMusicEnabled)
			{
				Log.Debug("[GhostMusic] Música desactivada por configuración");
				return;
			}

			try
			{
				if (MusicManager.m_sound != null)
				{
					MusicManager.m_sound.Stop();
					MusicManager.m_sound.Dispose();
					MusicManager.m_sound = null;
				}

				if (MusicManager.m_fadeSound != null)
				{
					MusicManager.m_fadeSound.Dispose();
					MusicManager.m_fadeSound = null;
				}

				var streamingSource = ContentManager.Get<StreamingSource>(MUSIC_PATH);
				if (streamingSource == null)
				{
					Log.Warning($"[GhostMusic] Música no encontrada: {MUSIC_PATH}");
					return;
				}

				var duplicateSource = streamingSource.Duplicate();
				var sound = new StreamingSound(duplicateSource, 1f, 1f, 0f, false, true, 1f);

				MusicManager.m_sound = sound;
				MusicManager.m_currentMix = MusicManager.Mix.Other;
				MusicManager.m_fadeStartTime = 0.0;

				sound.Play();
				m_musicPlaying = true;
				m_timeSinceMusicStarted = 0f;

				Log.Debug("[GhostMusic] Música de persecución iniciada");
			}
			catch (System.Exception ex)
			{
				Log.Error($"[GhostMusic] Error al iniciar música: {ex.Message}");
				m_musicPlaying = false;
			}
		}

		private void RestartMusicImmediately()
		{
			if (!m_isChaseActive || !m_musicPlaying)
				return;

			// Verificar configuración antes de reiniciar
			if (!ChaseMusicConfig.GhostMusicEnabled)
			{
				StopChaseMusicImmediately();
				return;
			}

			try
			{
				Log.Debug($"[GhostMusic] Reiniciando música a los {m_timeSinceMusicStarted:F2}s");
				StartChaseMusicImmediately();
			}
			catch (System.Exception ex)
			{
				Log.Error($"[GhostMusic] Error al reiniciar música: {ex.Message}");
				m_musicPlaying = false;
			}
		}

		private void StopChaseMusicImmediately()
		{
			if (m_musicPlaying)
			{
				try
				{
					if (MusicManager.m_sound != null)
					{
						MusicManager.m_sound.Stop();
						MusicManager.m_sound.Dispose();
						MusicManager.m_sound = null;
					}

					if (MusicManager.m_fadeSound != null)
					{
						MusicManager.m_fadeSound.Dispose();
						MusicManager.m_fadeSound = null;
					}

					m_musicPlaying = false;
					m_timeSinceMusicStarted = 0f;

					Log.Debug("[GhostMusic] Música de persecución detenida");
				}
				catch (System.Exception ex)
				{
					Log.Error($"[GhostMusic] Error al detener música: {ex.Message}");
				}
			}
		}

		#endregion

		#region Public API

		public void ForcePlayChaseMusic()
		{
			if (ChaseMusicConfig.GhostMusicEnabled)
			{
				m_isChaseActive = true;
				StartChaseMusicImmediately();
			}
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
