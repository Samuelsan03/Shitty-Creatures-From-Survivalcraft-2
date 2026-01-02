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
		private bool m_isChaseActive = false;
		private bool m_chaseMusicPlaying = false;
		private float m_checkInterval = 0.1f;
		private float m_timeSinceLastCheck = 0f;
		private float m_musicCheckInterval = 1f;
		private float m_timeSinceMusicCheck = 0f;

		private SubsystemTime m_subsystemTime;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemAudio m_subsystemAudio;

		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);

			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
		}

		public void Update(float dt)
		{
			m_timeSinceLastCheck += dt;
			m_timeSinceMusicCheck += dt;

			// Verificar periódicamente si hay tanques persiguiendo
			if (m_timeSinceLastCheck >= m_checkInterval)
			{
				m_timeSinceLastCheck = 0f;

				bool wasChaseActive = m_isChaseActive;
				m_isChaseActive = CheckForActiveTanks();

				if (wasChaseActive != m_isChaseActive)
				{
					if (m_isChaseActive)
					{
						PlayChaseMusic();
						PlayAlertSound();
						ShowTankAlertMessage();
					}
					else
					{
						StopChaseMusic();
					}
				}
			}

			// Verificar periódicamente si la música necesita repetirse
			if (m_timeSinceMusicCheck >= m_musicCheckInterval)
			{
				m_timeSinceMusicCheck = 0f;

				// Si hay persecución activa pero la música no está sonando, reproducirla de nuevo
				if (m_isChaseActive && !MusicManager.IsPlaying && !m_chaseMusicPlaying)
				{
					PlayChaseMusic();
				}
			}
		}

		private bool CheckForActiveTanks()
		{
			if (Project == null || m_subsystemPlayers == null)
				return false;

			bool foundActiveTank = false;

			foreach (Entity entity in Project.Entities)
			{
				try
				{
					string entityName = entity.ValuesDictionary.DatabaseObject.Name;

					if (entityName == "Tank1" || entityName == "Tank2" || entityName == "Tank3")
					{
						ComponentHealth health = entity.FindComponent<ComponentHealth>();

						// Si el tanque está muerto, ignorarlo completamente
						if (health != null && health.Health <= 0f)
							continue;

						// Si el tanque está vivo, verificar si está activo o bajo ataque
						bool isTankActive = false;

						// Verificar si está persiguiendo
						ComponentZombieChaseBehavior chaseBehavior = entity.FindComponent<ComponentZombieChaseBehavior>();
						if (chaseBehavior != null && chaseBehavior.IsActive)
						{
							isTankActive = true;
						}

						// Verificar si tiene otro comportamiento de persecución
						ComponentChaseBehavior chaseBehavior2 = entity.FindComponent<ComponentChaseBehavior>();
						if (chaseBehavior2 != null && chaseBehavior2.IsActive)
						{
							isTankActive = true;
						}

						// Si el tanque no tiene comportamientos de persecución activos,
						// verificar si algún jugador está lo suficientemente cerca
						if (!isTankActive && health != null && health.Health > 0f)
						{
							ComponentBody tankBody = entity.FindComponent<ComponentBody>();
							if (tankBody != null)
							{
								bool playerNearby = false;
								foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
								{
									ComponentHealth playerHealth = player.Entity.FindComponent<ComponentHealth>();
									if (playerHealth != null && playerHealth.Health > 0f)
									{
										ComponentBody playerBody = player.Entity.FindComponent<ComponentBody>();
										if (playerBody != null)
										{
											float distance = (tankBody.Position - playerBody.Position).Length();
											// Si el jugador está a menos de 20 metros del tanque, mantener la música
											if (distance < 20f)
											{
												playerNearby = true;
												break;
											}
										}
									}
								}

								isTankActive = playerNearby;
							}
						}

						if (isTankActive)
						{
							foundActiveTank = true;
							// Si encontramos al menos un tanque activo, podemos salir del bucle
							// pero continuamos para verificar todos los tanques
						}
					}
				}
				catch (System.Exception)
				{
					// Ignorar errores en entidades individuales
				}
			}

			return foundActiveTank;
		}

		private void PlayChaseMusic()
		{
			try
			{
				if (MusicManager.IsPlaying)
				{
					MusicManager.StopMusic();
				}

				try
				{
					MusicManager.PlayMusic("MenuMusic/ChaseTheme/Tank Theme", 0f);
					m_chaseMusicPlaying = true;
				}
				catch
				{
					string[] alternativePaths = {
					};

					foreach (string altPath in alternativePaths)
					{
						try
						{
							MusicManager.PlayMusic(altPath, 0f);
							m_chaseMusicPlaying = true;
							return;
						}
						catch
						{
						}
					}
				}
			}
			catch (System.Exception)
			{
				m_chaseMusicPlaying = false;
			}
		}

		private void PlayAlertSound()
		{
			if (m_subsystemAudio == null)
				return;

			try
			{
				m_subsystemAudio.PlaySound("Audio/UI/Tank Warning Sound", 1f, 0f, 0f, 0.0001f);
			}
			catch (System.Exception)
			{
				try
				{
					m_subsystemAudio.PlaySound("Audio/UI/Tank Warning Sound", 1f, 0f, 0f, 0.0001f);
				}
				catch
				{
					try
					{
						m_subsystemAudio.PlaySound("Audio/UI/Tank Warning Sound", 1f, 0f, 0f, 0.0001f);
					}
					catch
					{
					}
				}
			}
		}

		private void StopChaseMusic()
		{
			try
			{
				if (m_chaseMusicPlaying)
				{
					if (MusicManager.IsPlaying)
					{
						MusicManager.StopMusic();
					}
					m_chaseMusicPlaying = false;
				}
			}
			catch (System.Exception)
			{
			}
		}

		private void ShowTankAlertMessage()
		{
			if (m_subsystemPlayers == null)
				return;

			var componentPlayers = m_subsystemPlayers.ComponentPlayers;
			if (componentPlayers.Count == 0)
				return;

			string message;
			bool translationFound;

			// Intentar obtener la traducción del mensaje de alerta
			message = LanguageControl.Get(out translationFound, "Messages", "TankChaseAlert");

			// Si no se encuentra la traducción, usar el texto en inglés por defecto
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

		public override void Dispose()
		{
			StopChaseMusic();
			base.Dispose();
		}

		public void ForcePlayChaseMusic()
		{
			PlayChaseMusic();
			PlayAlertSound();
			m_isChaseActive = true;
			m_chaseMusicPlaying = true;
		}

		public void ForceStopChaseMusic()
		{
			StopChaseMusic();
			m_isChaseActive = false;
		}

		public bool IsChaseActive
		{
			get { return m_isChaseActive; }
		}

		public bool IsMusicPlaying
		{
			get { return m_chaseMusicPlaying; }
		}
	}
}
