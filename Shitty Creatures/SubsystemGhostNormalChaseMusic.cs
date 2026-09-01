using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using System.Collections.Generic;

namespace Game
{
	public class SubsystemGhostNormalChaseMusic : Subsystem, IUpdateable
	{
		#region Constants

		private const string MUSIC_PATH = "MenuMusic/ChaseTheme/Hotel Insanity Chase Theme";
		private const float MUSIC_DURATION = 32.0f;
		private const float CHECK_INTERVAL = 0.1f;
		private const float DETECTION_RADIUS = 50f;

		#endregion

		#region Fields

		private bool m_isChaseActive = false;
		private float m_timeSinceLastCheck = 0f;
		private float m_timeSinceMusicStarted = 0f;
		private bool m_musicPlaying = false;
		private bool m_wasMusicEnabled = true;

		// Timer adjustment for pause
		private bool m_wasPaused = false;

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
			if (m_wasMusicEnabled != ChaseMusicConfig.GhostMusicEnabled)
			{
				m_wasMusicEnabled = ChaseMusicConfig.GhostMusicEnabled;
				if (!ChaseMusicConfig.GhostMusicEnabled && m_musicPlaying)
				{
					StopChaseMusicImmediately();
				}
			}

			m_timeSinceLastCheck += dt;

			// Only update music timer when not paused
			bool isPaused = InGameMusicManager.IsPaused;
			if (m_musicPlaying && !isPaused)
			{
				m_timeSinceMusicStarted += dt;

				if (m_timeSinceMusicStarted >= MUSIC_DURATION * 0.98f)
				{
					RestartMusicImmediately();
				}
			}
			m_wasPaused = isPaused;

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
						entityName != "GhostBoomer3" && entityName != "FrozenGhost" && entityName != "FrozenGhostBoomer")
						continue;

					ComponentHealth health = entity.FindComponent<ComponentHealth>();
					if (health != null && health.Health <= 0f)
						continue;

					ComponentZombieChaseBehavior chaseBehavior = entity.FindComponent<ComponentZombieChaseBehavior>();
					if (chaseBehavior != null && chaseBehavior.IsActive)
					{
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
				}
				catch (System.Exception)
				{
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
			if (!ChaseMusicConfig.GhostMusicEnabled)
			{
				Log.Debug("[GhostMusic] Música desactivada por configuración");
				return;
			}

			if (!InGameMusicManager.CanPlayInContext(InGameMusicManager.MusicContext.Chase))
			{
				Log.Debug("[GhostMusic] No se puede reproducir: contexto de mayor prioridad activo");
				return;
			}

			try
			{
				InGameMusicManager.PlayMusic(MUSIC_PATH, 0f, InGameMusicManager.MusicContext.Chase);
				m_musicPlaying = true;
				m_timeSinceMusicStarted = 0f;
				m_wasPaused = false;

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

			if (!ChaseMusicConfig.GhostMusicEnabled)
			{
				StopChaseMusicImmediately();
				return;
			}

			if (!InGameMusicManager.CanPlayInContext(InGameMusicManager.MusicContext.Chase))
				return;

			try
			{
				Log.Debug($"[GhostMusic] Reiniciando música a los {m_timeSinceMusicStarted:F2}s");
				InGameMusicManager.PlayMusic(MUSIC_PATH, 0f, InGameMusicManager.MusicContext.Chase);
				m_timeSinceMusicStarted = 0f;
				m_wasPaused = false;
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
					if (InGameMusicManager.CurrentContext == InGameMusicManager.MusicContext.Chase)
					{
						InGameMusicManager.StopMusic();
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
