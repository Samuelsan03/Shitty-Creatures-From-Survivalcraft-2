using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentFluInfected : Component, IUpdateable
	{
		// Propiedades configurables desde template
		public bool IsJumpMove { get; set; }
		public string CoughSoundPath { get; set; }
		public string SneezeSoundPath { get; set; }
		public float FluResistance { get; set; }

		// Estado interno
		public bool IsInfected => m_fluDuration > 0f;
		public bool IsCoughing => m_coughDuration > 0f;
		public bool IsSneezing => m_sneezeDuration > 0f;

		private float m_fluDuration;
		private float m_coughDuration;
		private float m_sneezeDuration;
		private double m_lastEffectTime = -1000.0;
		private double m_lastCoughTime = -1000.0;
		private double m_lastSneezeTime = -1000.0;
		private double m_lastWobbleTime = -1000.0; // Control de tambaleo

		private float oldWalkSpeed, oldFlySpeed, oldSwimSpeed, oldJumpSpeed;

		// Referencias a subsistemas y componentes
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemTime m_subsystemTime;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemNoise m_subsystemNoise;
		private ComponentCreature m_componentCreature;
		private readonly Game.Random m_random = new Game.Random();

		public const float SeriousFluPeriod = 150f;

		public void StartFlu(float fluDuration)
		{
			m_fluDuration = MathUtils.Max(fluDuration - FluResistance, 0f);
			m_lastEffectTime = m_subsystemTime.GameTime;
			m_lastCoughTime = m_subsystemTime.GameTime;
			m_lastSneezeTime = m_subsystemTime.GameTime;
			m_lastWobbleTime = m_subsystemTime.GameTime;
		}

		public void Cough()
		{
			m_coughDuration = 2f;
			m_lastCoughTime = m_subsystemTime.GameTime;

			if (!string.IsNullOrEmpty(CoughSoundPath))
			{
				m_subsystemAudio.PlaySound(CoughSoundPath, 1f, 0f, m_componentCreature.ComponentBody.Position, 2f, true);
			}

			m_subsystemNoise?.MakeNoise(m_componentCreature.ComponentBody.Position, 0.25f, 10f);

			// IMPULSO MÁS FUERTE (similar al veneno pero con dirección hacia atrás)
			Vector3 impulse = -1.8f * m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
			// Añadir algo de aleatoriedad
			impulse += new Vector3(m_random.Float(-0.3f, 0.3f), m_random.Float(-0.1f, 0.1f), m_random.Float(-0.3f, 0.3f));
			m_componentCreature.ComponentBody.ApplyImpulse(impulse);
		}

		public void Sneeze()
		{
			m_sneezeDuration = 1.5f;
			m_lastSneezeTime = m_subsystemTime.GameTime;

			if (!string.IsNullOrEmpty(SneezeSoundPath))
			{
				m_subsystemAudio.PlaySound(SneezeSoundPath, 1f, 0f, m_componentCreature.ComponentBody.Position, 2f, true);
			}

			m_subsystemNoise?.MakeNoise(m_componentCreature.ComponentBody.Position, 0.25f, 10f);

			// IMPULSO MÁS FUERTE (similar al veneno pero con dirección hacia atrás)
			Vector3 impulse = -1.8f * m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
			impulse += new Vector3(m_random.Float(-0.3f, 0.3f), m_random.Float(-0.1f, 0.1f), m_random.Float(-0.3f, 0.3f));
			m_componentCreature.ComponentBody.ApplyImpulse(impulse);
		}

		private void FluEffect()
		{
			m_lastEffectTime = m_subsystemTime.GameTime;

			// Causar daño similar al componente original del jugador
			float injury = MathUtils.Min(0.1f, m_componentCreature.ComponentHealth.Health - 0.175f);
			if (injury > 0f)
			{
				// Programar el daño después de 0.75 segundos para sincronizar con la tos/estornudo
				m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + 0.75, delegate
				{
					if (m_componentCreature != null &&
						m_componentCreature.ComponentHealth != null &&
						m_componentCreature.ComponentHealth.Health > 0f)
					{
						m_componentCreature.ComponentHealth.Injure(injury, null, false, "Flu");
					}
				});
			}

			// Decidir si toser o estornudar (igual que antes)
			if (m_coughDuration == 0f && (m_subsystemTime.GameTime - m_lastCoughTime > 40.0 || m_random.Bool(0.5f)))
			{
				Cough();
			}
			else if (m_sneezeDuration == 0f)
			{
				Sneeze();
			}
		}

		// MÉTODO DE TAMBALEO SIMILAR AL VENENO
		private void ApplyWobble(ComponentBody body)
		{
			if (m_subsystemTime.GameTime - m_lastWobbleTime > 1.5) // Cada 1.5 segundos
			{
				m_lastWobbleTime = m_subsystemTime.GameTime;

				Vector3 velocity = body.Velocity;

				// Tambaleo en todas direcciones (similar al veneno)
				velocity.X += m_random.Float(-0.15f, 0.15f);
				velocity.Y += m_random.Float(-0.08f, 0.08f); // También vertical para más realismo
				velocity.Z += m_random.Float(-0.15f, 0.15f);

				body.Velocity = velocity;
			}
		}

		public void Update(float dt)
		{
			if (m_componentCreature == null || m_componentCreature.ComponentHealth == null)
				return;

			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			if (!m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
			{
				m_fluDuration = 0f;
				return;
			}

			var locomotion = m_componentCreature.ComponentLocomotion;
			var health = m_componentCreature.ComponentHealth;
			var body = m_componentCreature.ComponentBody;
			var model = m_componentCreature.ComponentCreatureModel;

			if (locomotion == null || health == null || body == null || model == null)
				return;

			// Reducir duración de la gripe
			if (m_fluDuration > 0f)
			{
				float reductionFactor = 1f;
				m_fluDuration = MathUtils.Max(m_fluDuration - reductionFactor * dt, 0f);

				// Llamar a FluEffect periódicamente
				if (health.Health > 0f && m_subsystemTime.PeriodicGameTimeEvent(5.0, -0.01) &&
					m_subsystemTime.GameTime - m_lastEffectTime > 13.0)
				{
					FluEffect();
				}

				// TAMBALEO CONTINUO MIENTRAS ESTÉ INFECTADO (como en el veneno)
				ApplyWobble(body);
			}

			// Ajuste de velocidad según la duración de la gripe
			if (m_fluDuration > 0f)
			{
				float factor;
				if (m_fluDuration <= SeriousFluPeriod)
				{
					float progress = m_fluDuration / SeriousFluPeriod;
					factor = MathUtils.Lerp(0.6f, 0.4f, progress);
				}
				else
				{
					float progress = MathUtils.Min((m_fluDuration - SeriousFluPeriod) / SeriousFluPeriod, 1f);
					factor = MathUtils.Lerp(0.4f, 0.2f, progress);
				}

				locomotion.WalkSpeed = factor * oldWalkSpeed;
				locomotion.FlySpeed = factor * oldFlySpeed;
				locomotion.SwimSpeed = factor * oldSwimSpeed;
				locomotion.JumpSpeed = factor * oldJumpSpeed;
			}
			else
			{
				locomotion.WalkSpeed = oldWalkSpeed;
				locomotion.FlySpeed = oldFlySpeed;
				locomotion.SwimSpeed = oldSwimSpeed;
				locomotion.JumpSpeed = oldJumpSpeed;
			}

			// Efecto visual de tos/estornudo: inclinar la cabeza
			if (m_coughDuration > 0f || m_sneezeDuration > 0f)
			{
				m_coughDuration = MathUtils.Max(m_coughDuration - dt, 0f);
				m_sneezeDuration = MathUtils.Max(m_sneezeDuration - dt, 0f);

				float noise = SimplexNoise.Noise(4f * (float)MathUtils.Remainder(m_subsystemTime.GameTime, 10000.0));
				float targetPitch = MathUtils.DegToRad(MathUtils.Lerp(-35f, -65f, noise));
				float currentPitch = locomotion.LookAngles.Y;
				float delta = targetPitch - currentPitch;
				locomotion.LookOrder = new Vector2(locomotion.LookOrder.X, Math.Clamp(delta, -3f, 3f));
			}

			// Tambaleo opcional basado en IsJumpMove (lo dejamos por compatibilidad)
			if (IsJumpMove && m_fluDuration > 0f && m_subsystemTime.PeriodicGameTimeEvent(3.0, 0.0) && m_random.Float(0f, 1f) < 0.15f)
			{
				Vector3 velocity = body.Velocity;
				velocity.X += m_random.Float(-0.05f, 0.05f);
				velocity.Z += m_random.Float(-0.05f, 0.05f);
				body.Velocity = velocity;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);

			IsJumpMove = valuesDictionary.GetValue<bool>("IsJumpMove", false);
			m_fluDuration = valuesDictionary.GetValue<float>("FluDuration", 0f);
			FluResistance = valuesDictionary.GetValue<float>("FluResistance", 0f);

			CoughSoundPath = valuesDictionary.GetValue<string>("CoughSound", "Audio/Creatures/MaleCough");
			SneezeSoundPath = valuesDictionary.GetValue<string>("SneezeSound", "Audio/Creatures/MaleSneeze");

			if (m_componentCreature?.ComponentLocomotion != null)
			{
				oldWalkSpeed = m_componentCreature.ComponentLocomotion.WalkSpeed;
				oldFlySpeed = m_componentCreature.ComponentLocomotion.FlySpeed;
				oldSwimSpeed = m_componentCreature.ComponentLocomotion.SwimSpeed;
				oldJumpSpeed = m_componentCreature.ComponentLocomotion.JumpSpeed;
			}
			else
			{
				oldWalkSpeed = oldFlySpeed = oldSwimSpeed = oldJumpSpeed = 1f;
			}
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue("FluDuration", m_fluDuration);
			valuesDictionary.SetValue("FluResistance", FluResistance);
		}
	}
}
