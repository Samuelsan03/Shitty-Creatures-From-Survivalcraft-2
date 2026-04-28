using System;
using Engine;
using Engine.Audio;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentMusicWalkAroundBehavior : ComponentBehavior, IUpdateable
	{
		public override float ImportanceLevel
		{
			get
			{
				// Forzar importancia alta si hay una canción pendiente de reanudar
				if (m_pendingMusicResume)
					return 100f;
				return m_importanceLevel;
			}
		}
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentHealth m_componentHealth;
		private ComponentNewChaseBehavior m_chaseBehavior;
		private StateMachine m_stateMachine = new StateMachine();
		private Random m_random = new Random();
		private float m_importanceLevel;

		private float m_musicTimer;
		private Sound m_currentMusicSound;
		private int m_currentTrackIndex = -1;

		// Radio fijo para el área de baile (puedes ajustarlo aquí si es necesario)
		private float m_danceRadius = 5f;
		// Punto central del área de baile
		private Vector3 m_danceCenter;

		private static readonly Color[] m_noteColors = new Color[]
		{
			Color.Pink,
			Color.Blue,
			new Color(152, 34, 255)
		};

		private static readonly string[] m_musicTracks = new string[]
		{
			"MenuMusic/MusicWalkAround/Aaron-Smith-Dancin",
			"MenuMusic/MusicWalkAround/Bajo_el_Lente",
			"MenuMusic/MusicWalkAround/L'amour Toujours",
			"MenuMusic/MusicWalkAround/la la la"
		};
		private static readonly float[] m_musicDurations = new float[] { 18f, 30f, 27f, 34f };

		private bool m_attackedWhilePlaying;
		private bool m_pendingMusicResume;

		public virtual void Update(float dt)
		{
			// Actualizar el estado de la persecución para detectar cambios
			bool chaseActive = m_chaseBehavior != null && m_chaseBehavior.IsActive;
			if (!chaseActive && m_pendingMusicResume && !m_attackedWhilePlaying)
			{
				// Forzar importancia para que el comportamiento se active si no lo está
				m_importanceLevel = 100f;
			}

			m_stateMachine.Update();
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			m_componentHealth = base.Entity.FindComponent<ComponentHealth>(true);
			m_chaseBehavior = base.Entity.FindComponent<ComponentNewChaseBehavior>();

			if (m_componentHealth != null)
			{
				m_componentHealth.Injured += OnInjured;
			}

			// No se cargan nuevos parámetros desde el diccionario, se usa el radio por defecto (5)

			m_stateMachine.AddState("Inactive", delegate
			{
				m_importanceLevel = m_random.Float(0f, 1f);
			}, delegate
			{
				// Si hay una canción pendiente y ya no hay persecución/ataque, reanudar de inmediato
				if (m_pendingMusicResume)
				{
					bool chaseActive = m_chaseBehavior != null && m_chaseBehavior.IsActive;
					if (!chaseActive && !m_attackedWhilePlaying)
					{
						m_pendingMusicResume = false;
						m_stateMachine.TransitionTo("SingAndDance");
						return;
					}
				}

				if (m_random.Float(0f, 1f) < 0.1f * m_subsystemTime.GameTimeDelta)
				{
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}

				if (m_random.Float(0f, 1f) < 0.05f * m_subsystemTime.GameTimeDelta)
				{
					m_importanceLevel = m_random.Float(1f, 2f);
				}
				if (IsActive)
				{
					if (m_random.Float(0f, 1f) < 0.05f * m_subsystemTime.GameTimeDelta)
					{
						m_stateMachine.TransitionTo("SingAndDance");
					}
					else
					{
						m_stateMachine.TransitionTo("Walk");
					}
				}
			}, null);

			m_stateMachine.AddState("Walk", delegate
			{
				float speed = (m_componentCreature.ComponentBody.ImmersionFactor > 0.5f) ? 1f : m_random.Float(0.25f, 0.35f);
				m_componentPathfinding.SetDestination(new Vector3?(FindDestination()), speed, 1f, 0, false, true, false, null);
			}, delegate
			{
				if (m_componentPathfinding.IsStuck || !IsActive)
				{
					m_stateMachine.TransitionTo("Inactive");
					return;
				}
				if (m_componentPathfinding.Destination == null)
				{
					if (m_random.Float(0f, 1f) < 0.5f)
					{
						m_stateMachine.TransitionTo("Inactive");
					}
					else
					{
						m_stateMachine.TransitionTo(null);
						m_stateMachine.TransitionTo("Walk");
					}
					return;
				}
				if (m_random.Float(0f, 1f) < 0.1f * m_subsystemTime.GameTimeDelta)
				{
					m_stateMachine.TransitionTo("SingAndDance");
					return;
				}
				if (m_random.Float(0f, 1f) < 0.1f * m_subsystemTime.GameTimeDelta)
				{
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}
				m_componentCreature.ComponentCreatureModel.LookRandomOrder = true;
			}, null);

			m_stateMachine.AddState("SingAndDance", delegate
			{
				m_attackedWhilePlaying = false;

				// Detener cualquier desplazamiento anterior
				m_componentPathfinding.Stop();

				// Establecer el centro del baile en la posición actual
				m_danceCenter = m_componentCreature.ComponentBody.Position;

				string track;
				float duration;
				if (m_pendingMusicResume && m_currentTrackIndex >= 0 && m_currentTrackIndex < m_musicTracks.Length)
				{
					// Reanudar la misma pista con el tiempo restante
					track = m_musicTracks[m_currentTrackIndex];
					duration = m_musicDurations[m_currentTrackIndex];
					// m_musicTimer ya contiene el tiempo restante, no se modifica
				}
				else
				{
					int idx = m_random.Int(0, m_musicTracks.Length - 1);
					track = m_musicTracks[idx];
					duration = m_musicDurations[idx];
					m_musicTimer = duration;
					m_currentTrackIndex = idx;
				}

				StopMusic();
				try
				{
					Vector3 pos = m_componentCreature.ComponentBody.Position;
					m_currentMusicSound = m_subsystemAudio.CreateSound(track);
					if (m_currentMusicSound != null)
					{
						float distance = m_subsystemAudio.CalculateListenerDistance(pos);
						m_currentMusicSound.Volume = m_subsystemAudio.CalculateVolume(distance, 8f, 2f);
						m_currentMusicSound.Pitch = 1f;
						m_currentMusicSound.Pan = 0f;
						m_currentMusicSound.Play(pos);
					}
					else
					{
						Log.Warning("[ComponentMusicWalkAroundBehavior] Could not create sound for track: " + track);
					}
				}
				catch (Exception ex)
				{
					Log.Warning("[ComponentMusicWalkAroundBehavior] Failed to create/play music: " + ex.Message);
				}

				// Asignar un destino inicial dentro del radio de baile, a velocidad normal (1.0)
				Vector3 danceDest = FindDanceDestination();
				m_componentPathfinding.SetDestination(new Vector3?(danceDest), 1.0f, 1f, 0, false, true, false, null);
			}, delegate
			{
				float dt = m_subsystemTime.GameTimeDelta;
				m_musicTimer -= dt;

				if (m_attackedWhilePlaying || (m_chaseBehavior != null && m_chaseBehavior.IsActive))
				{
					m_pendingMusicResume = true;
					m_stateMachine.TransitionTo("Inactive");
					return;
				}

				if (m_musicTimer <= 0f)
				{
					m_pendingMusicResume = false;
					m_stateMachine.TransitionTo("Inactive");
					return;
				}

				// Efectos visuales (notas musicales)
				if (m_subsystemTime.PeriodicGameTimeEvent(0.2, -0.01))
				{
					try
					{
						Vector3 headPos = GetHeadPosition() + Vector3.UnitY * 0.2f;
						SoundParticleSystem noteSystem = new SoundParticleSystem(m_subsystemTerrain, headPos, Vector3.UnitY);
						Color color = m_noteColors[m_random.Int(0, m_noteColors.Length - 1)];
						noteSystem.AddNote(color);
						m_subsystemParticles.AddParticleSystem(noteSystem, false);
					}
					catch (Exception ex)
					{
						Log.Warning("[ComponentMusicWalkAroundBehavior] Failed to emit particle: " + ex.Message);
					}
				}

				// Movimiento de baile dentro del área: reasignar destino si es necesario
				if (m_componentPathfinding.IsStuck || m_componentPathfinding.Destination == null)
				{
					Vector3 newDest = FindDanceDestination();
					m_componentPathfinding.SetDestination(new Vector3?(newDest), 1.0f, 1f, 0, false, true, false, null);
				}

				// Giros rápidos y aleatorios
				if (m_subsystemTime.PeriodicGameTimeEvent(0.25, -0.05))
				{
					m_componentCreature.ComponentLocomotion.TurnOrder = new Vector2(m_random.Float(-1.2f, 1.2f), 0f);
				}

				// Saltos frecuentes para animar el baile
				if (m_random.Float(0f, 1f) < 0.5f * dt)
				{
					m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
				}

				m_componentCreature.ComponentCreatureModel.LookRandomOrder = true;
			}, delegate
			{
				StopMusic();
				m_attackedWhilePlaying = false;
				if (!m_pendingMusicResume)
				{
					m_currentTrackIndex = -1;
				}
			});

			m_stateMachine.TransitionTo("Inactive");
		}

		private void OnInjured(Injury injury)
		{
			m_attackedWhilePlaying = true;
		}

		private void StopMusic()
		{
			if (m_currentMusicSound != null)
			{
				try
				{
					if (m_currentMusicSound.State == SoundState.Playing)
					{
						m_currentMusicSound.Stop();
					}
					m_currentMusicSound.Dispose();
				}
				catch { }
				m_currentMusicSound = null;
			}
		}

		private Vector3 GetHeadPosition()
		{
			if (m_componentCreature.ComponentCreatureModel != null)
			{
				try
				{
					return m_componentCreature.ComponentCreatureModel.EyePosition;
				}
				catch { }
			}
			return m_componentCreature.ComponentBody.Position + Vector3.UnitY * m_componentCreature.ComponentBody.BoxSize.Y * 0.9f;
		}

		// Buscar un destino aleatorio dentro del radio de baile alrededor de m_danceCenter
		private Vector3 FindDanceDestination()
		{
			Vector2 randomCircle = Vector2.Normalize(m_random.Vector2(1f)) * m_random.Float(0f, m_danceRadius);
			Vector3 candidate = new Vector3(m_danceCenter.X + randomCircle.X, 0f, m_danceCenter.Z + randomCircle.Y);
			candidate.Y = (float)(m_subsystemTerrain.Terrain.GetTopHeight(Terrain.ToCell(candidate.X), Terrain.ToCell(candidate.Z)) + 1);
			return candidate;
		}

		// El método original FindDestination (para el estado "Walk") se mantiene igual
		public virtual Vector3 FindDestination()
		{
			Vector3 position = m_componentCreature.ComponentBody.Position;
			float num = 0f;
			Vector3 result = position;
			for (int i = 0; i < 16; i++)
			{
				Vector2 vector = Vector2.Normalize(m_random.Vector2(1f)) * m_random.Float(6f, 12f);
				Vector3 vector2 = new Vector3(position.X + vector.X, 0f, position.Z + vector.Y);
				vector2.Y = (float)(m_subsystemTerrain.Terrain.GetTopHeight(Terrain.ToCell(vector2.X), Terrain.ToCell(vector2.Z)) + 1);
				float num2 = ScoreDestination(vector2);
				if (num2 > num)
				{
					num = num2;
					result = vector2;
				}
			}
			return result;
		}

		public virtual float ScoreDestination(Vector3 destination)
		{
			float num = 8f - MathF.Abs(m_componentCreature.ComponentBody.Position.Y - destination.Y);
			if (m_subsystemTerrain.Terrain.GetCellContents(Terrain.ToCell(destination.X), Terrain.ToCell(destination.Y) - 1, Terrain.ToCell(destination.Z)) == 18)
			{
				num -= 5f;
			}
			return num;
		}

		public override void Dispose()
		{
			StopMusic();
			if (m_componentHealth != null)
			{
				m_componentHealth.Injured -= OnInjured;
			}
			base.Dispose();
		}
	}
}
