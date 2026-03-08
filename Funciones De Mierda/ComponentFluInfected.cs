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

		public void StartFlu(float duration)
		{
			// Guardar velocidades actuales como base (pre-infección) solo si no estaba ya infectado
			if (m_fluDuration <= 0f && m_componentCreature?.ComponentLocomotion != null)
			{
				m_originalWalkSpeed = m_componentCreature.ComponentLocomotion.WalkSpeed;
				m_originalFlySpeed = m_componentCreature.ComponentLocomotion.FlySpeed;
				m_originalSwimSpeed = m_componentCreature.ComponentLocomotion.SwimSpeed;
				m_originalJumpSpeed = m_componentCreature.ComponentLocomotion.JumpSpeed;
			}

			m_fluDuration = MathUtils.Max(duration - FluResistance, 0f);
			m_lastCoughTime = m_subsystemTime.GameTime;
			Sneeze(); // El primer estornudo al contagiarse

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

			// APLICAR DAÑO AL ESTORNUDAR
			if (m_fluDuration > 0f && m_componentCreature.ComponentHealth.Health > 0.05f)
			{
				float damage = 0.02f; // daño por estornudo (ajustable)
				m_componentCreature.ComponentHealth.Injure(damage, null, false, "Gripe");
			}
		}

		public void Update(float dt)
		{
			if (m_componentCreature?.ComponentHealth == null) return;

			if (m_componentCreature.ComponentHealth.Health <= 0f)
			{
				// Criatura muerta: eliminar efectos y restaurar velocidades originales
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

				// Tos periódica
				if (m_componentCreature.ComponentHealth.Health > 0f &&
					m_subsystemTime.PeriodicGameTimeEvent(8.0, -0.01) &&
					m_subsystemTime.GameTime - m_lastCoughTime > 15.0)
				{
					Cough();
				}

				// El daño se aplica SOLO en Sneeze(), no aquí
			}

			// Aplicar ralentización si está infectado
			if (m_fluDuration > 0f)
			{
				// Cuanto más dura la infección, más lento se mueve (de 0.4 a 0.1)
				float t = MathUtils.Saturate(m_fluDuration / MaxFluDuration);
				float factor = MathUtils.Lerp(0.1f, 0.4f, t);

				locomotion.WalkSpeed = factor * m_originalWalkSpeed;
				locomotion.FlySpeed = factor * m_originalFlySpeed;
				locomotion.SwimSpeed = factor * m_originalSwimSpeed;
				locomotion.JumpSpeed = factor * m_originalJumpSpeed;
			}
			else
			{
				// Ya no está infectado: restaurar velocidades originales
				RestoreOriginalSpeeds();
			}

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

			// Cargar velocidades originales guardadas, o si no existen, usar las actuales
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
