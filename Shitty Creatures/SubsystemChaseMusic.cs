using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using System.Collections.Generic;

namespace Game
{
	/// <summary>
	/// Subsystem unificado que maneja la música de persecución tanto para fantasmas
	/// como para Tanks (zombis jefe). Conserva la lógica original de cada uno.
	/// </summary>
	public class SubsystemChaseMusic : Subsystem, IUpdateable
	{
		/// <summary>
		/// Tipo de persecución con música. Determina qué lógica y pista se utiliza.
		/// </summary>
		public enum ChaseMusicType
		{
			Ghost,
			Tank
		}

		#region Ghost Constants

		private const string GhostMusicPath = "MenuMusic/ChaseTheme/Hotel Insanity Chase Theme";
		private const float GhostMusicDuration = 32.0f;
		private const float GhostCheckInterval = 0.1f;
		private const float GhostDetectionRadius = 50f;

		#endregion

		#region Tank Constants

		private const string TankMusicPath = "MenuMusic/ChaseTheme/Tank Theme";
		private const double TankMusicDuration = 52.0;
		private const float TankChaseRadius = 60f;

		#endregion

		#region Entity Name Sets

		private static readonly HashSet<string> GhostEntityNames = new HashSet<string>
		{
			"GhostNormal", "GhostFast", "PoisonousGhost", "GhostCharger",
			"GhostBoomer1", "GhostBoomer2", "GhostBoomer3",
			"FrozenGhost", "FrozenGhostBoomer"
		};

		private static readonly HashSet<string> TankEntityNames = new HashSet<string>
		{
			"Tank1", "Tank2", "Tank3",
			"TankGhost1", "TankGhost2", "TankGhost3",
			"FrozenTank", "FrozenTankGhost"
		};

		#endregion

		#region Ghost Fields

		private bool m_isGhostChaseActive = false;
		private float m_ghostTimeSinceLastCheck = 0f;
		private float m_ghostTimeSinceMusicStarted = 0f;
		private bool m_ghostMusicPlaying = false;
		private bool m_ghostWasMusicEnabled = true;
		private bool m_ghostWasPaused = false;

		#endregion

		#region Tank Fields

		private bool m_tankMusicPlaying = false;
		private double m_tankMusicStartTime = 0;
		private bool m_tankWasPaused = false;
		private double m_tankPauseStartTime = 0.0;

		#endregion

		#region Shared Fields

		private SubsystemTime m_subsystemTime;
		private SubsystemPlayers m_subsystemPlayers;

		#endregion

		#region Properties

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public bool IsGhostChaseActive => m_isGhostChaseActive;
		public bool IsGhostMusicPlaying => m_ghostMusicPlaying;
		public bool IsTankMusicPlaying => m_tankMusicPlaying;

		#endregion

		#region Initialization

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_ghostWasMusicEnabled = ChaseMusicConfig.GhostMusicEnabled;
		}

		#endregion

		#region Update Loop

		public void Update(float dt)
		{
			// Asegura que el fade-out de la música se procese cada frame,
			// independientemente de si otro hook del mod llama a Update().
			InGameMusicManager.Update();

			if (Project == null || m_subsystemTime == null || m_subsystemPlayers == null)
				return;

			UpdateGhostChase(dt);
			UpdateTankChase(dt);
		}

		#endregion

		#region Ghost Update

		private void UpdateGhostChase(float dt)
		{
			if (m_ghostWasMusicEnabled != ChaseMusicConfig.GhostMusicEnabled)
			{
				m_ghostWasMusicEnabled = ChaseMusicConfig.GhostMusicEnabled;
				if (!ChaseMusicConfig.GhostMusicEnabled && m_ghostMusicPlaying)
				{
					StopGhostMusicImmediately();
				}
			}

			m_ghostTimeSinceLastCheck += dt;

			bool isPaused = InGameMusicManager.IsPaused;
			if (m_ghostMusicPlaying && !isPaused)
			{
				m_ghostTimeSinceMusicStarted += dt;

				if (m_ghostTimeSinceMusicStarted >= GhostMusicDuration * 0.98f)
				{
					RestartGhostMusicImmediately();
				}
			}
			m_ghostWasPaused = isPaused;

			if (m_ghostTimeSinceLastCheck >= GhostCheckInterval)
			{
				m_ghostTimeSinceLastCheck = 0f;

				bool wasChaseActive = m_isGhostChaseActive;
				m_isGhostChaseActive = CheckForActiveGhosts();

				if (wasChaseActive != m_isGhostChaseActive)
				{
					if (m_isGhostChaseActive)
						StartGhostMusicImmediately();
					else
						StopGhostMusicImmediately();
				}
			}

			if (m_isGhostChaseActive && !m_ghostMusicPlaying && ChaseMusicConfig.GhostMusicEnabled)
			{
				StartGhostMusicImmediately();
			}
		}

		#endregion

		#region Tank Update

		private void UpdateTankChase(float dt)
		{
			// Si no hay jugadores, detener música
			if (m_subsystemPlayers.ComponentPlayers.Count == 0)
			{
				if (m_tankMusicPlaying)
				{
					InGameMusicManager.FadeOutAndStop();
					m_tankMusicPlaying = false;
				}
				return;
			}

			// Si el juego está pausado, no hacer nada
			if (m_subsystemTime.GameTimeFactor == 0f)
				return;

			// Ajuste del temporizador durante pausa
			bool isPaused = InGameMusicManager.IsPaused;
			if (m_tankMusicPlaying)
			{
				if (!m_tankWasPaused && isPaused)
				{
					m_tankPauseStartTime = Time.RealTime;
				}
				else if (m_tankWasPaused && !isPaused)
				{
					m_tankMusicStartTime += (Time.RealTime - m_tankPauseStartTime);
				}
			}
			m_tankWasPaused = isPaused;

			// Verificar si hay algún Tank persiguiendo a un JUGADOR dentro del radio
			bool hasChasingTank = false;

			foreach (var entity in Project.Entities)
			{
				string entityName = entity.ValuesDictionary?.DatabaseObject?.Name;
				if (string.IsNullOrEmpty(entityName) || !TankEntityNames.Contains(entityName))
					continue;

				var health = entity.FindComponent<ComponentHealth>();
				if (health == null || health.Health <= 0f)
					continue;

				ComponentZombieChaseBehavior chaseBehavior = entity.FindComponent<ComponentZombieChaseBehavior>();
				if (chaseBehavior == null)
					continue;

				ComponentCreature target = chaseBehavior.Target;
				if (target == null)
					continue;

				// *** CAMBIO CLAVE: solo cuenta si el objetivo es un JUGADOR ***
				if (m_subsystemPlayers == null || !m_subsystemPlayers.IsPlayer(target.Entity))
					continue;

				if (target.ComponentHealth == null || target.ComponentHealth.Health <= 0f)
					continue;

				if (chaseBehavior.Suppressed)
					continue;

				float distance = Vector3.Distance(
					entity.FindComponent<ComponentBody>()?.Position ?? Vector3.Zero,
					target.ComponentBody?.Position ?? Vector3.Zero
				);

				if (distance <= TankChaseRadius)
				{
					hasChasingTank = true;
					break;
				}
			}

			bool shouldPlayMusic = hasChasingTank && ShittyCreaturesSettingsManager.TankMusicEnabled;

			if (shouldPlayMusic)
			{
				if (!m_tankMusicPlaying)
				{
					InGameMusicManager.PlayMusic(TankMusicPath, 0f, InGameMusicManager.MusicContext.Chase);
					m_tankMusicStartTime = Time.RealTime;
					m_tankMusicPlaying = true;
					m_tankWasPaused = false;
				}
				else
				{
					double elapsed = Time.RealTime - m_tankMusicStartTime;
					if (elapsed >= TankMusicDuration)
					{
						InGameMusicManager.PlayMusic(TankMusicPath, 0f, InGameMusicManager.MusicContext.Chase);
						m_tankMusicStartTime = Time.RealTime;
						m_tankWasPaused = false;
					}
				}
			}
			else
			{
				if (m_tankMusicPlaying)
				{
					// La persecución del Tank terminó (presa muerta, jugador muerto,
					// o el Tank dejó de perseguir): fade-out.
					InGameMusicManager.FadeOutAndStop();
					m_tankMusicPlaying = false;
				}
			}
		}

		#endregion

		#region Ghost Detection

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
					if (!GhostEntityNames.Contains(entityName))
						continue;

					ComponentHealth health = entity.FindComponent<ComponentHealth>();
					if (health != null && health.Health <= 0f)
						continue;

					ComponentZombieChaseBehavior chaseBehavior = entity.FindComponent<ComponentZombieChaseBehavior>();
					if (chaseBehavior != null && chaseBehavior.IsActive)
					{
						// *** CAMBIO CLAVE: el fantasma debe estar persiguiendo a un jugador ***
						if (chaseBehavior.Target == null || !m_subsystemPlayers.IsPlayer(chaseBehavior.Target.Entity))
							continue;

						ComponentBody ghostBody = entity.FindComponent<ComponentBody>();
						if (ghostBody != null)
						{
							// Solo verificar contra el jugador que está siendo perseguido.
							ComponentPlayer chasedPlayer = chaseBehavior.Target.Entity.FindComponent<ComponentPlayer>();
							if (chasedPlayer != null && chasedPlayer.ComponentHealth.Health > 0f)
							{
								ComponentBody playerBody = chasedPlayer.ComponentBody;
								if (playerBody != null)
								{
									float distance = (ghostBody.Position - playerBody.Position).Length();
									if (distance < GhostDetectionRadius)
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

		#region Ghost Music Control

		private void StartGhostMusicImmediately()
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
				InGameMusicManager.PlayMusic(GhostMusicPath, 0f, InGameMusicManager.MusicContext.Chase);
				m_ghostMusicPlaying = true;
				m_ghostTimeSinceMusicStarted = 0f;
				m_ghostWasPaused = false;

				Log.Debug("[GhostMusic] Música de persecución iniciada");
			}
			catch (System.Exception ex)
			{
				Log.Error($"[GhostMusic] Error al iniciar música: {ex.Message}");
				m_ghostMusicPlaying = false;
			}
		}

		private void RestartGhostMusicImmediately()
		{
			if (!m_isGhostChaseActive || !m_ghostMusicPlaying)
				return;

			if (!ChaseMusicConfig.GhostMusicEnabled)
			{
				StopGhostMusicImmediately();
				return;
			}

			if (!InGameMusicManager.CanPlayInContext(InGameMusicManager.MusicContext.Chase))
				return;

			try
			{
				Log.Debug($"[GhostMusic] Reiniciando música a los {m_ghostTimeSinceMusicStarted:F2}s");
				InGameMusicManager.PlayMusic(GhostMusicPath, 0f, InGameMusicManager.MusicContext.Chase);
				m_ghostTimeSinceMusicStarted = 0f;
				m_ghostWasPaused = false;
			}
			catch (System.Exception ex)
			{
				Log.Error($"[GhostMusic] Error al reiniciar música: {ex.Message}");
				m_ghostMusicPlaying = false;
			}
		}

		private void StopGhostMusicImmediately()
		{
			if (m_ghostMusicPlaying)
			{
				try
				{
					if (InGameMusicManager.CurrentContext == InGameMusicManager.MusicContext.Chase)
					{
						// Fade-out cuando termina la persecución.
						InGameMusicManager.FadeOutAndStop();
					}

					m_ghostMusicPlaying = false;
					m_ghostTimeSinceMusicStarted = 0f;

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

		public void ForcePlayChaseMusic(ChaseMusicType type)
		{
			switch (type)
			{
				case ChaseMusicType.Ghost:
					if (ChaseMusicConfig.GhostMusicEnabled)
					{
						m_isGhostChaseActive = true;
						StartGhostMusicImmediately();
					}
					break;

				case ChaseMusicType.Tank:
					if (ShittyCreaturesSettingsManager.TankMusicEnabled && !m_tankMusicPlaying)
					{
						InGameMusicManager.PlayMusic(TankMusicPath, 0f, InGameMusicManager.MusicContext.Chase);
						m_tankMusicStartTime = Time.RealTime;
						m_tankMusicPlaying = true;
						m_tankWasPaused = false;
					}
					break;
			}
		}

		public void ForceStopChaseMusic(ChaseMusicType type)
		{
			switch (type)
			{
				case ChaseMusicType.Ghost:
					m_isGhostChaseActive = false;
					StopGhostMusicImmediately();
					break;

				case ChaseMusicType.Tank:
					if (m_tankMusicPlaying)
					{
						InGameMusicManager.FadeOutAndStop();
						m_tankMusicPlaying = false;
					}
					break;
			}
		}

		#endregion

		#region Cleanup

		public override void Dispose()
		{
			// En Dispose se usa parada DURA (no fade): es limpieza del subsistema
			// y no debe solaparse con una nueva instancia al recargar.
			if (m_ghostMusicPlaying)
			{
				try
				{
					if (InGameMusicManager.CurrentContext == InGameMusicManager.MusicContext.Chase)
						InGameMusicManager.StopMusic();
				}
				catch { }
				m_ghostMusicPlaying = false;
			}

			if (m_tankMusicPlaying)
			{
				try { InGameMusicManager.StopMusic(); }
				catch { }
				m_tankMusicPlaying = false;
			}

			base.Dispose();
		}

		#endregion
	}
}
