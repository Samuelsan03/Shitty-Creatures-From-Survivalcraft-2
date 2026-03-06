// ========================================================
// ComponentFluInfected.cs
// ========================================================

using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Componente que aplica los efectos de la gripe a una criatura.
	/// Reduce la velocidad de movimiento y provoca tos periódica.
	/// </summary>
	public class ComponentFluInfected : Component, IUpdateable
	{
		// Indica si la criatura está infectada
		public bool IsInfected => m_fluDuration > 0f;

		// Indica si la criatura está tosiendo
		public bool IsCoughing => m_isCoughing;

		/// <summary>
		/// Inicia la infección con una duración determinada, descontando la resistencia.
		/// </summary>
		public void StartFlu(float duration)
		{
			m_fluDuration = MathUtils.Max(duration - FluResistance, 0f);
			m_lastCoughTime = m_subsystemTime.GameTime;
		}

		/// <summary>
		/// Provoca un efecto de tos: reproduce sonido y genera una pequeña alerta de ruido.
		/// </summary>
		public void Cough()
		{
			if (m_componentCreature?.ComponentHealth == null)
				return;

			m_lastCoughTime = m_subsystemTime.GameTime;
			m_isCoughing = true;

			// Reproducir sonido de tos
			m_componentCreature.ComponentCreatureSounds?.PlayCoughSound();

			// Generar ruido para alertar a otras criaturas
			Project.FindSubsystem<SubsystemNoise>(true)?.MakeNoise(
				m_componentCreature.ComponentBody.Position, 0.25f, 10f);
		}

		public void Update(float dt)
		{
			if (m_componentCreature?.ComponentHealth == null)
				return;

			// Si la criatura está muerta, detener cualquier efecto activo
			if (m_componentCreature.ComponentHealth.Health <= 0f)
			{
				m_fluDuration = 0f;
				m_isCoughing = false;
				return;
			}

			// Solo aplicar efectos si las mecánicas de supervivencia están activadas
			if (!m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
			{
				m_fluDuration = 0f;
				return;
			}

			var creature = m_componentCreature;
			if (creature.ComponentLocomotion == null || creature.ComponentBody == null)
				return;

			var locomotion = creature.ComponentLocomotion;

			if (m_fluDuration > 0f)
			{
				m_fluDuration = MathUtils.Max(m_fluDuration - dt, 0f);

				// Tos periódica (cada ~8 segundos de juego, con intervalo mínimo de 15s entre tos)
				if (creature.ComponentHealth.Health > 0f &&
					m_subsystemTime.PeriodicGameTimeEvent(8.0, -0.01) &&
					m_subsystemTime.GameTime - m_lastCoughTime > 15.0)
				{
					Cough();
				}
			}
			else
			{
				m_isCoughing = false;
			}

			// Ajuste de velocidad según la duración restante de la gripe
			float duration = m_fluDuration;
			if (duration <= 0f)
			{
				// Restaurar velocidades originales
				locomotion.WalkSpeed = oldWalkSpeed;
				locomotion.FlySpeed = oldFlySpeed;
				locomotion.SwimSpeed = oldSwimSpeed;
				locomotion.JumpSpeed = oldJumpSpeed;
			}
			else
			{
				// Dos fases: leve (hasta 150s) y grave (más de 150s)
				float factor;
				if (duration > SeriousFluPeriod)
				{
					float progress = MathUtils.Min((duration - SeriousFluPeriod) / SeriousFluPeriod, 1f);
					factor = MathUtils.Lerp(0.4f, 0.2f, progress); // Grave: 40% → 20%
				}
				else
				{
					float progress = duration / SeriousFluPeriod;
					factor = MathUtils.Lerp(0.6f, 0.4f, progress); // Leve: 60% → 40%
				}

				locomotion.WalkSpeed = factor * oldWalkSpeed;
				locomotion.FlySpeed = factor * oldFlySpeed;
				locomotion.SwimSpeed = factor * oldSwimSpeed;
				locomotion.JumpSpeed = factor * oldJumpSpeed;
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

			// Guardar velocidades base del componente de locomoción
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

		// Subsistemas
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemAudio m_subsystemAudio;
		private ComponentCreature m_componentCreature;

		private readonly Random m_random = new Random();
		private bool m_isCoughing;
		public float m_fluDuration;
		public float FluResistance;
		private double m_lastCoughTime = -1000.0;

		// Velocidades originales
		private float oldWalkSpeed;
		private float oldFlySpeed;
		private float oldSwimSpeed;
		private float oldJumpSpeed;

		public const float SeriousFluPeriod = 150f;
	}
}
