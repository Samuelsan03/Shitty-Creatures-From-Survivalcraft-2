using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Subsystem que maneja la reproducción de música cuando un Tank (o variantes)
	/// persigue a un jugador o a cualquier otra criatura.
	/// </summary>
	public class SubsystemTankChaseMusic : Subsystem, IUpdateable
	{
		private static readonly HashSet<string> TankEntityNames = new HashSet<string>
		{
			"Tank1", "Tank2", "Tank3",
			"TankGhost1", "TankGhost2", "TankGhost3",
			"FrozenTank", "FrozenTankGhost"
		};

		private SubsystemTime m_subsystemTime;
		private SubsystemPlayers m_subsystemPlayers;

		private bool m_isMusicPlaying = false;
		private double m_musicStartTime = 0;
		private const double TANK_MUSIC_DURATION = 52.0;

		// Radio de distancia para considerar que está "cerca" de su objetivo
		private const float CHASE_RADIUS = 60f;

		private const string TankMusicPath = "MenuMusic/ChaseTheme/Tank Theme";

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
		}

		public void Update(float dt)
		{
			// Detener música si no estamos en la pantalla de juego (menú, loading, etc.)
			if (!(ScreensManager.CurrentScreen is GameScreen))
			{
				if (m_isMusicPlaying)
				{
					InGameMusicManager.StopMusic();
					m_isMusicPlaying = false;
				}
				return;
			}

			// Si el proyecto o el subsistema de tiempo son nulos (mundo descargado), no hacer nada
			if (Project == null || m_subsystemTime == null || m_subsystemPlayers == null)
				return;

			// Si no hay jugadores, detener música (el tank no debería perseguir nada si no hay jugadores)
			if (m_subsystemPlayers.ComponentPlayers.Count == 0)
			{
				if (m_isMusicPlaying)
				{
					InGameMusicManager.StopMusic();
					m_isMusicPlaying = false;
				}
				return;
			}

			// Si el juego está pausado, no hacer nada
			if (m_subsystemTime.GameTimeFactor == 0f)
				return;

			// Verificar si hay algún Tank persiguiendo a cualquier criatura dentro del radio
			bool hasChasingTank = false;

			foreach (var entity in Project.Entities)
			{
				string entityName = entity.ValuesDictionary?.DatabaseObject?.Name;
				if (string.IsNullOrEmpty(entityName) || !TankEntityNames.Contains(entityName))
					continue;

				var health = entity.FindComponent<ComponentHealth>();
				if (health == null || health.Health <= 0f)
					continue;

				ComponentChaseBehavior chaseBehavior = entity.FindComponent<ComponentZombieChaseBehavior>();
				if (chaseBehavior == null)
					chaseBehavior = entity.FindComponent<ComponentChaseBehavior>();

				if (chaseBehavior == null)
					continue;

				ComponentCreature target = chaseBehavior.Target;
				if (target == null)
					continue;

				// Verificar que el objetivo esté vivo (aplica a jugadores y cualquier criatura)
				if (target.ComponentHealth == null || target.ComponentHealth.Health <= 0f)
					continue;

				if (chaseBehavior.Suppressed)
					continue;

				// Verificar distancia real entre el Tank y su objetivo
				float distance = Vector3.Distance(
					entity.FindComponent<ComponentBody>()?.Position ?? Vector3.Zero,
					target.ComponentBody?.Position ?? Vector3.Zero
				);

				// Solo considerar si está dentro del radio de persecución
				if (distance <= CHASE_RADIUS)
				{
					hasChasingTank = true;
					break;
				}
			}

			// La música suena SOLO si hay al menos un Tank persiguiendo algo dentro del radio
			bool shouldPlayMusic = hasChasingTank && ShittyCreaturesSettingsManager.TankMusicEnabled;

			if (shouldPlayMusic)
			{
				if (!m_isMusicPlaying)
				{
					InGameMusicManager.PlayMusic(TankMusicPath, 0f);
					m_musicStartTime = Time.RealTime;
					m_isMusicPlaying = true;
				}
				else
				{
					double elapsed = Time.RealTime - m_musicStartTime;
					if (elapsed >= TANK_MUSIC_DURATION)
					{
						InGameMusicManager.PlayMusic(TankMusicPath, 0f);
						m_musicStartTime = Time.RealTime;
					}
				}
			}
			else
			{
				if (m_isMusicPlaying)
				{
					InGameMusicManager.StopMusic();
					m_isMusicPlaying = false;
				}
			}
		}

		public override void Dispose()
		{
			if (m_isMusicPlaying)
			{
				InGameMusicManager.StopMusic();
				m_isMusicPlaying = false;
			}
			base.Dispose();
		}
	}
}
