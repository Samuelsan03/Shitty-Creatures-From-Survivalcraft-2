using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewGhostTankModel : ComponentTankModel, IUpdateable
	{
		// Variable para la opacidad del fantasma tanque
		public float m_opacity = 0.7f;

		// Campo para almacenar el efecto de retroceso fantasma
		private float m_ghostRecoilPhase = 0f;

		// Campos para rotación de torreta fantasma
		private float m_ghostTurretRotation = 0f;
		private float m_ghostCannonElevation = MathUtils.DegToRad(15f);

		// Campo para tiempo de animación específico de fantasma
		private float m_ghostAnimationTime = 0f;

		// Campos para animación de lanzamiento
		private bool m_isLaunchAnimation = false;
		private float m_launchAnimationPhase = 0f;
		private float m_launchShakeIntensity = 0f;

		public override void Animate()
		{
			// Primero, ejecutar la animación suavizada de la clase base
			base.Animate();

			// Aplicar la opacidad del fantasma
			base.Opacity = new float?(this.m_opacity);

			// Determinar el modo de renderizado basado en la opacidad
			float? opacity = base.Opacity;
			float num = 1f;
			bool isFullyOpaque = opacity.GetValueOrDefault() >= num & opacity != null;

			if (isFullyOpaque)
			{
				this.RenderingMode = ModelRenderingMode.AlphaThreshold;
			}
			else
			{
				this.RenderingMode = ModelRenderingMode.TransparentAfterWater;
			}

			// Aplicar efectos de animación de lanzamiento
			if (m_isLaunchAnimation)
			{
				ApplyLaunchAnimation();
			}
		}

		private void ApplyLaunchAnimation()
		{
			// Calcular fase de levantamiento
			float raisePhase = MathUtils.Sigmoid(m_launchAnimationPhase * 2f, 3f);
			float lowerPhase = MathUtils.Sigmoid((m_launchAnimationPhase - 0.5f) * 2f, 3f);

			// Levantar ambas manos durante la animación
			float raiseAmount = MathUtils.Lerp(0f, MathUtils.DegToRad(60f), raisePhase);
			float prepareAmount = MathUtils.Lerp(0f, MathUtils.DegToRad(30f),
				MathUtils.Min(m_launchAnimationPhase * 2f, 1f));

			// Aplicar a los ángulos objetivo
			if (m_targetHandAngles1 != null && m_targetHandAngles2 != null)
			{
				m_targetHandAngles1 = new Vector2(
					m_targetHandAngles1.X + raiseAmount,
					m_targetHandAngles1.Y + prepareAmount);

				m_targetHandAngles2 = new Vector2(
					m_targetHandAngles2.X + raiseAmount,
					m_targetHandAngles2.Y - prepareAmount);
			}

			// Aplicar sacudida durante la preparación
			if (m_launchAnimationPhase > 0.3f && m_launchAnimationPhase < 0.7f)
			{
				m_launchShakeIntensity = 0.1f * (1f - m_launchAnimationPhase);
				float shakeX = m_launchShakeIntensity * (float)Math.Sin(m_ghostAnimationTime * 20f);
				float shakeY = m_launchShakeIntensity * (float)Math.Cos(m_ghostAnimationTime * 15f);

				if (m_targetHeadAngles != null)
				{
					m_targetHeadAngles = new Vector2(
						m_targetHeadAngles.X + shakeX * 0.5f,
						m_targetHeadAngles.Y + shakeY * 0.5f);
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Primero cargar la configuración base
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar la opacidad desde XML
			if (valuesDictionary.ContainsKey("Opacity"))
			{
				m_opacity = valuesDictionary.GetValue<float>("Opacity");
			}

			// NOTA: NO ajustamos parámetros de animación aquí para mantener la misma fluidez
			// que el ComponentTankModel. Los parámetros de suavizado y responsividad
			// ya fueron cargados y ajustados por la clase base.
		}

		// Método para cambiar dinámicamente la opacidad
		public void SetOpacity(float opacity)
		{
			m_opacity = MathUtils.Clamp(opacity, 0.1f, 1f);
		}

		// Sobrescribir Update para agregar efectos visuales
		public override void Update(float dt)
		{
			// Actualizar tiempo de animación fantasma
			m_ghostAnimationTime += dt;

			// Actualizar fase de retroceso fantasma
			if (m_ghostRecoilPhase > 0f)
			{
				m_ghostRecoilPhase = MathUtils.Max(m_ghostRecoilPhase - dt * 2f, 0f);
			}

			// Actualizar animación de lanzamiento
			if (m_isLaunchAnimation)
			{
				m_launchAnimationPhase += dt * 3f; // Velocidad de animación

				if (m_launchAnimationPhase >= 1f)
				{
					m_launchAnimationPhase = 0f;
					m_isLaunchAnimation = false;
					m_launchShakeIntensity = 0f;
				}
			}

			base.Update(dt);

			// Efecto de parpadeo para fantasmas
			if (m_opacity > 0.3f && m_opacity < 0.9f)
			{
				float flicker = 0.03f * (float)Math.Sin(m_ghostAnimationTime * 1.5f);
				base.Opacity = new float?(m_opacity + flicker);
			}
		}

		// Métodos para animación de lanzamiento
		public void SetLaunchAnimation(bool enabled)
		{
			m_isLaunchAnimation = enabled;
			m_launchAnimationPhase = 0f;

			if (enabled)
			{
				// Aumentar parpadeo durante la animación
				m_opacity = MathUtils.Clamp(m_opacity * 1.2f, 0.3f, 0.9f);
			}
		}

		public void UpdateLaunchAnimation(float progress)
		{
			m_launchAnimationPhase = MathUtils.Clamp(progress, 0f, 1f);

			// Efecto de parpadeo intenso durante el lanzamiento
			if (progress > 0.3f && progress < 0.7f)
			{
				float flicker = 0.3f * (float)Math.Sin(progress * 20f);
				SetOpacity(MathUtils.Clamp(0.7f + flicker, 0.3f, 0.9f));
			}
		}

		public bool IsLaunchAnimationActive()
		{
			return m_isLaunchAnimation;
		}

		public float GetLaunchAnimationPhase()
		{
			return m_launchAnimationPhase;
		}

		// Métodos fantasma específicos
		public void FireGhostCannon()
		{
			m_ghostRecoilPhase = m_opacity < 0.7f ? 0.6f : 1f;

			// Efecto visual adicional para disparo fantasma
			if (m_opacity > 0.5f)
			{
				// Flash brillante momentáneo
				SetOpacity(MathUtils.Min(m_opacity * 1.5f, 1f));
			}
		}

		public void SetGhostTurretRotation(float angle)
		{
			m_ghostTurretRotation = MathUtils.Clamp(angle, -MathUtils.DegToRad(180f), MathUtils.DegToRad(180f));

			// Aplicar también a los ángulos de cabeza del modelo base
			if (m_targetHeadAngles != null)
			{
				m_targetHeadAngles = new Vector2(m_ghostTurretRotation, m_targetHeadAngles.Y);
			}
		}

		public void SetGhostCannonElevation(float angle)
		{
			m_ghostCannonElevation = MathUtils.Clamp(angle, -MathUtils.DegToRad(10f), MathUtils.DegToRad(45f));

			// Aplicar también a los ángulos de mano del modelo base
			if (m_targetHandAngles2 != null) // Mano derecha como cañón
			{
				m_targetHandAngles2 = new Vector2(m_ghostCannonElevation, m_targetHandAngles2.Y);
			}
		}

		// Método para obtener la opacidad actual
		public float GetOpacity()
		{
			return m_opacity;
		}

		// Método para activar/desactivar efectos de fantasma
		public void SetGhostEffectsEnabled(bool enabled)
		{
			if (enabled && m_opacity > 0.8f)
			{
				m_opacity = 0.7f; // Hacer más transparente
			}
			else if (!enabled && m_opacity < 0.9f)
			{
				m_opacity = 1f; // Hacer completamente opaco
			}
		}
	}
}