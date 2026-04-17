using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentFluInfected : Component, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public bool IsInfected => m_fluDuration > 0f;
		public bool IsCoughing => m_coughingTimer > 0f;

		/// <summary>
		/// Si es true, la criatura infectada saltará erráticamente (inspirado en el veneno).
		/// </summary>
		public bool IsJumpMove { get; set; }

		public void StartFlu(float duration)
		{
			// Guardar velocidades originales solo si no estaba infectado
			if (m_fluDuration <= 0f && m_componentCreature?.ComponentLocomotion != null)
			{
				m_originalWalkSpeed = m_componentCreature.ComponentLocomotion.WalkSpeed;
				m_originalFlySpeed = m_componentCreature.ComponentLocomotion.FlySpeed;
				m_originalSwimSpeed = m_componentCreature.ComponentLocomotion.SwimSpeed;
				m_originalJumpSpeed = m_componentCreature.ComponentLocomotion.JumpSpeed;
			}

			m_fluDuration = MathUtils.Max(duration - FluResistance, 0f);
			m_lastCoughTime = m_subsystemTime.GameTime;
			Sneeze(); // Estornudo inicial

			if (m_componentCreature is ComponentPlayer player)
			{
				// Los jugadores no usan este componente; su gripe es manejada por ComponentFlu
				// Este bloque se mantiene por compatibilidad pero no se ejecuta porque StartInfect ya excluye jugadores.
			}
		}

		public void Cough()
		{
			if (m_componentCreature?.ComponentHealth == null) return;

			m_lastCoughTime = m_subsystemTime.GameTime;
			m_coughingTimer = 1.2f;

			if (!string.IsNullOrEmpty(m_coughSound))
				PlayRandomSound(m_coughSound, 0.8f, m_random.Float(-0.2f, 0.2f));
			else
				m_componentCreature.ComponentCreatureSounds?.PlayCoughSound();

			Project.FindSubsystem<SubsystemNoise>(true)?.MakeNoise(
				m_componentCreature.ComponentBody.Position, 0.25f, 10f);

			if (m_componentCreature.ComponentBody != null)
			{
				Vector3 impulse = -1.2f * m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
				m_componentCreature.ComponentBody.ApplyImpulse(impulse);
			}
		}

		public void Sneeze()
		{
			if (m_componentCreature?.ComponentHealth == null) return;

			if (!string.IsNullOrEmpty(m_sneezeSound))
				PlayRandomSound(m_sneezeSound, 0.8f, m_random.Float(-0.2f, 0.2f));

			Project.FindSubsystem<SubsystemNoise>(true)?.MakeNoise(
				m_componentCreature.ComponentBody.Position, 0.25f, 10f);

			// Daño al estornudar (no letal)
			if (m_fluDuration > 0f && m_componentCreature.ComponentHealth.Health > 0.05f)
			{
				float damage = 0.02f;
				if (m_componentCreature.ComponentHealth.Health - damage <= 0.05f)
					damage = m_componentCreature.ComponentHealth.Health - 0.05f;

				if (damage > 0f)
				{
					m_componentCreature.ComponentHealth.Injure(damage, null, false, "Flu");
				}
			}
		}

		/// <summary>
		/// Efecto periódico de la gripe (daño, tos fuerte, etc.) inspirado en NauseaEffect del veneno.
		/// </summary>
		private void FluEffect()
		{
			if (m_componentCreature?.ComponentHealth == null) return;

			m_lastFluEffectTime = m_subsystemTime.GameTime;

			// Daño leve
			float injury = MathUtils.Min(0.05f, m_componentCreature.ComponentHealth.Health - 0.05f);
			if (injury > 0f)
			{
				m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + 0.5, delegate
				{
					m_componentCreature.ComponentHealth.Injure(injury, null, false, "Flu");
					m_componentCreature.ComponentCreatureSounds.PlayPainSound();
				});
			}

			// Activar tos fuerte (puede ser un sonido especial)
			if (!string.IsNullOrEmpty(m_coughSound))
				PlayRandomSound(m_coughSound, 1f, m_random.Float(-0.1f, 0.1f));
			else
				m_componentCreature.ComponentCreatureSounds?.PlayCoughSound();

			// Reiniciar el temporizador de tos para la animación
			m_coughingTimer = 1.2f;

			// Opcional: añadir un efecto de partículas (por simplicidad no se incluye, pero podría agregarse)
		}

		public void Update(float dt)
		{
			if (m_componentCreature?.ComponentHealth == null) return;

			// Si es un jugador, no aplicamos este componente (su gripe es manejada por ComponentFlu)
			if (m_componentCreature is ComponentPlayer)
			{
				m_fluDuration = 0f;
				RestoreOriginalSpeeds();
				return;
			}

			if (m_componentCreature.ComponentHealth.Health <= 0f)
			{
				m_fluDuration = 0f;
				m_coughingTimer = 0f;
				RestoreOriginalSpeeds();
				return;
			}

			if (!m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
			{
				m_fluDuration = 0f;
				RestoreOriginalSpeeds();
				return;
			}

			var locomotion = m_componentCreature.ComponentLocomotion;
			if (locomotion == null || m_componentCreature.ComponentBody == null) return;

			// Actualizar duración de la gripe
			if (m_fluDuration > 0f)
			{
				m_fluDuration = MathUtils.Max(m_fluDuration - dt, 0f);

				// Tos periódica (como antes)
				if (m_componentCreature.ComponentHealth.Health > 0f &&
					m_subsystemTime.PeriodicGameTimeEvent(8.0, -0.01) &&
					m_subsystemTime.GameTime - m_lastCoughTime > 15.0)
				{
					Cough();
				}

				// Efecto periódico adicional (daño, tos fuerte) - similar al veneno
				if (m_componentCreature.ComponentHealth.Health > 0f)
				{
					double period = (m_fluDuration > SeriousFluPeriod) ? 5.0 : 12.0;
					if (m_subsystemTime.PeriodicGameTimeEvent(period, -0.01) &&
						(m_lastFluEffectTime == null || m_subsystemTime.GameTime - m_lastFluEffectTime.Value > period))
					{
						FluEffect();
					}
				}
			}

			// Aplicar ralentización (mantenemos el lerp original, que es más suave)
			if (m_fluDuration > 0f)
			{
				float t = MathUtils.Saturate(m_fluDuration / MaxFluDuration);
				float factor = MathUtils.Lerp(0.1f, 0.4f, t);

				locomotion.WalkSpeed = factor * m_originalWalkSpeed;
				locomotion.FlySpeed = factor * m_originalFlySpeed;
				locomotion.SwimSpeed = factor * m_originalSwimSpeed;
				locomotion.JumpSpeed = factor * m_originalJumpSpeed;
			}
			else
			{
				RestoreOriginalSpeeds();
			}

			// Efecto visual de tos
			if (m_coughingTimer > 0f)
			{
				m_coughingTimer -= dt;
				float pitchAngle = MathUtils.DegToRad(-45f) * MathUtils.Saturate(m_coughingTimer * 2f);
				locomotion.LookOrder = new Vector2(locomotion.LookOrder.X, pitchAngle);
			}

			// Movimiento de salto errático (inspirado en el veneno)
			if (IsJumpMove && m_fluDuration > 0f)
			{
				var pilot = m_componentCreature.Entity.FindComponent<ComponentPilot>();
				if (pilot != null && pilot.Speed > 0f &&
					(m_componentCreature.ComponentBody.StandingOnValue != null || m_componentCreature.ComponentBody.StandingOnBody != null) &&
					m_subsystemTime.PeriodicGameTimeEvent(2.0, 0.0))
				{
					float jumpSpeed = locomotion.JumpSpeed;
					locomotion.JumpOrder = 1f;
					Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
					m_componentCreature.ComponentBody.Velocity = new Vector3(forward.X * jumpSpeed, jumpSpeed, forward.Z * jumpSpeed);
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);

			m_fluDuration = valuesDictionary.GetValue<float>("FluDuration", 0f);
			FluResistance = valuesDictionary.GetValue<float>("FluResistance", 0f);
			m_coughSound = valuesDictionary.GetValue<string>("CoughSound", "Audio/Creatures/FemaleCough/FemaleCough1");
			m_sneezeSound = valuesDictionary.GetValue<string>("SneezeSound", "Audio/Creatures/FemaleSneeze/FemaleSneeze1");
			IsJumpMove = valuesDictionary.GetValue<bool>("IsJumpMove", false); // Nuevo parámetro

			// Cargar velocidades originales guardadas, o usar las actuales
			m_originalWalkSpeed = valuesDictionary.GetValue<float>("OriginalWalkSpeed", m_componentCreature.ComponentLocomotion?.WalkSpeed ?? 1f);
			m_originalFlySpeed = valuesDictionary.GetValue<float>("OriginalFlySpeed", m_componentCreature.ComponentLocomotion?.FlySpeed ?? 1f);
			m_originalSwimSpeed = valuesDictionary.GetValue<float>("OriginalSwimSpeed", m_componentCreature.ComponentLocomotion?.SwimSpeed ?? 1f);
			m_originalJumpSpeed = valuesDictionary.GetValue<float>("OriginalJumpSpeed", m_componentCreature.ComponentLocomotion?.JumpSpeed ?? 1f);
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue("FluDuration", m_fluDuration);
			valuesDictionary.SetValue("FluResistance", FluResistance);
		}

		private void RestoreOriginalSpeeds()
		{
			var locomotion = m_componentCreature?.ComponentLocomotion;
			if (locomotion == null) return;

			locomotion.WalkSpeed = m_originalWalkSpeed;
			locomotion.FlySpeed = m_originalFlySpeed;
			locomotion.SwimSpeed = m_originalSwimSpeed;
			locomotion.JumpSpeed = m_originalJumpSpeed;
		}

		private void PlayRandomSound(string sounds, float volume = 1f, float pitch = 0f)
		{
			if (string.IsNullOrEmpty(sounds)) return;
			string[] array = sounds.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			string soundName = array[m_random.Int(0, array.Length - 1)];
			m_subsystemAudio.PlaySound(soundName, volume, pitch, 0f, 0f);
		}

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemAudio m_subsystemAudio;
		private ComponentCreature m_componentCreature;
		private readonly Random m_random = new Random();

		private float m_coughingTimer;
		public float m_fluDuration;
		public float FluResistance;
		private double m_lastCoughTime = -1000.0;
		private double? m_lastFluEffectTime; // Para el efecto periódico

		private string m_coughSound;
		private string m_sneezeSound;

		// Velocidades base previas a la infección
		private float m_originalWalkSpeed = 1f;
		private float m_originalFlySpeed = 1f;
		private float m_originalSwimSpeed = 1f;
		private float m_originalJumpSpeed = 1f;

		public const float SeriousFluPeriod = 150f;
		public const float MaxFluDuration = 300f;
	}
}
