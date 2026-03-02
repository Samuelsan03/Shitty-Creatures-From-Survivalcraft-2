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
		private const float MUSIC_DURATION = 52.0f;
		private const float CHECK_INTERVAL = 0.1f;
		private const float DETECTION_RADIUS = 20f;

		#endregion

		#region Fields

		private bool m_isChaseActive = false;
		private bool m_alertShown = false;
		private float m_timeSinceLastCheck = 0f;
		private float m_timeSinceMusicStarted = 0f;
		private bool m_musicPlaying = false;
		private bool m_wasMusicEnabled = true;

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
			m_wasMusicEnabled = ChaseMusicConfig.TankMusicEnabled;
		}

		#endregion

		#region Update Loop

		public void Update(float dt)
		{
			// Verificar si la configuración cambió mientras la música sonaba
			if (m_wasMusicEnabled != ChaseMusicConfig.TankMusicEnabled)
			{
				m_wasMusicEnabled = ChaseMusicConfig.TankMusicEnabled;
				if (!ChaseMusicConfig.TankMusicEnabled && m_musicPlaying)
				{
					StopChaseMusicImmediately();
					m_alertShown = false;
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
				m_isChaseActive = CheckForActiveTanks();

				if (wasChaseActive != m_isChaseActive)
				{
					if (m_isChaseActive)
					{
						StartChaseMusicImmediately();

						// Solo mostrar alertas si la música está habilitada
						if (ChaseMusicConfig.TankMusicEnabled)
						{
							PlayAlertSound();
							if (!m_alertShown)
							{
								ShowAlertMessage();
								m_alertShown = true;
							}
						}
					}
					else
					{
						StopChaseMusicImmediately();
						m_alertShown = false;
					}
				}
			}

			if (m_isChaseActive && !m_musicPlaying && ChaseMusicConfig.TankMusicEnabled)
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

					if (entityName != "Tank1" && entityName != "Tank2" && entityName != "Tank3" &&
						entityName != "TankGhost1" && entityName != "TankGhost2" && entityName != "TankGhost3")
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

		#region Music Control

		private void StartChaseMusicImmediately()
		{
			// NUEVO: Verificar si la música está habilitada
			if (!ChaseMusicConfig.TankMusicEnabled)
			{
				Log.Debug("[TankMusic] Música desactivada por configuración");
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
					Log.Warning($"[TankMusic] Música no encontrada: {MUSIC_PATH}");
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

				Log.Debug("[TankMusic] Música de persecución iniciada");
			}
			catch (System.Exception ex)
			{
				Log.Error($"[TankMusic] Error al iniciar música: {ex.Message}");
				m_musicPlaying = false;
			}
		}

		private void RestartMusicImmediately()
		{
			if (!m_isChaseActive || !m_musicPlaying)
				return;

			// Verificar configuración antes de reiniciar
			if (!ChaseMusicConfig.TankMusicEnabled)
			{
				StopChaseMusicImmediately();
				return;
			}

			try
			{
				Log.Debug($"[TankMusic] Reiniciando música a los {m_timeSinceMusicStarted:F2}s");
				StartChaseMusicImmediately();
			}
			catch (System.Exception ex)
			{
				Log.Error($"[TankMusic] Error al reiniciar música: {ex.Message}");
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

					Log.Debug("[TankMusic] Música de persecución detenida");
				}
				catch (System.Exception ex)
				{
					Log.Error($"[TankMusic] Error al detener música: {ex.Message}");
				}
			}
		}

		#endregion

		#region Alert System

		private void PlayAlertSound()
		{
			if (m_subsystemAudio == null || !ChaseMusicConfig.TankMusicEnabled) return;

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
			if (m_subsystemPlayers == null || !ChaseMusicConfig.TankMusicEnabled) return;

			var componentPlayers = m_subsystemPlayers.ComponentPlayers;
			if (componentPlayers.Count == 0) return;

			string message;
			bool translationFound;

			message = LanguageControl.Get(out translationFound, "Messages", "TankChaseAlert");

			if (!translationFound)
			{
				message = "⚠️ ALERTA!\n¡Ha aparecido un Tanque!\nRefúgiate, busca buenas armas o usa el collar zombie para calmarlo y convertirlo en aliado!";
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
			if (ChaseMusicConfig.TankMusicEnabled)
			{
				m_isChaseActive = true;
				m_alertShown = false;
				StartChaseMusicImmediately();
				PlayAlertSound();
				ShowAlertMessage();
			}
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
