using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentFluInfected : Component, IUpdateable
	{
		// Propiedad requerida por IUpdateable (¡esto es lo que faltaba!)
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public bool IsInfected => m_fluDuration > 0f;
		public bool IsCoughing => m_coughingTimer > 0f;

		public void StartFlu(float duration)
		{
			m_fluDuration = MathUtils.Max(duration - FluResistance, 0f);
			m_lastCoughTime = m_subsystemTime.GameTime;
			Sneeze();

			if (m_componentCreature is ComponentPlayer player)
			{
				player.ComponentGui?.DisplaySmallMessage("¡Te has resfriado!", Color.White, true, true);
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
		}

		public void Update(float dt)
		{
			if (m_componentCreature?.ComponentHealth == null) return;

			if (m_componentCreature.ComponentHealth.Health <= 0f)
			{
				m_fluDuration = 0f;
				m_coughingTimer = 0f;
				return;
			}

			if (!m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
			{
				m_fluDuration = 0f;
				return;
			}

			var creature = m_componentCreature;
			if (creature.ComponentLocomotion == null || creature.ComponentBody == null) return;

			var locomotion = creature.ComponentLocomotion;

			if (m_fluDuration > 0f)
			{
				m_fluDuration = MathUtils.Max(m_fluDuration - dt, 0f);

				if (creature.ComponentHealth.Health > 0f &&
					m_subsystemTime.PeriodicGameTimeEvent(8.0, -0.01) &&
					m_subsystemTime.GameTime - m_lastCoughTime > 15.0)
				{
					Cough();
				}
			}

			// Factor de velocidad: visiblemente más lento
			float factor;
			if (m_fluDuration <= 0f)
			{
				factor = 1f; // sano
			}
			else
			{
				// Cuanto más dura la infección, más lento se mueve (0.4 → 0.1)
				float t = MathUtils.Saturate(m_fluDuration / MaxFluDuration);
				factor = MathUtils.Lerp(0.1f, 0.4f, t);
			}

			locomotion.WalkSpeed = factor * oldWalkSpeed;
			locomotion.FlySpeed = factor * oldFlySpeed;
			locomotion.SwimSpeed = factor * oldSwimSpeed;
			locomotion.JumpSpeed = factor * oldJumpSpeed;

			// Efecto visual de tos
			if (m_coughingTimer > 0f)
			{
				m_coughingTimer -= dt;
				float pitchAngle = MathUtils.DegToRad(-45f) * MathUtils.Saturate(m_coughingTimer * 2f);
				locomotion.LookOrder = new Vector2(locomotion.LookOrder.X, pitchAngle);
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
			valuesDictionary.SetValue("CoughSound", m_coughSound);
			valuesDictionary.SetValue("SneezeSound", m_sneezeSound);
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

		private string m_coughSound;
		private string m_sneezeSound;

		private float oldWalkSpeed;
		private float oldFlySpeed;
		private float oldSwimSpeed;
		private float oldJumpSpeed;

		public const float SeriousFluPeriod = 150f;
		public const float MaxFluDuration = 300f;
	}
}
