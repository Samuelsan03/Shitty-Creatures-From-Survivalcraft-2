using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewGhostTankModel : ComponentTankModel
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

			// Ajustar efectos específicos de fantasma después de la animación base
			if (m_opacity < 0.8f)
			{
				// Podemos hacer ajustes adicionales aquí si es necesario
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

			Log.Warning($"ComponentNewGhostTankModel cargado - Opacidad: {m_opacity}");
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

			base.Update(dt);

			// Efecto de parpadeo para fantasmas
			if (m_opacity > 0.3f && m_opacity < 0.9f)
			{
				float flicker = 0.03f * (float)Math.Sin(m_ghostAnimationTime * 1.5f);
				base.Opacity = new float?(m_opacity + flicker);
			}
		}

		// Métodos fantasma específicos
		public void FireGhostCannon()
		{
			m_ghostRecoilPhase = m_opacity < 0.7f ? 0.6f : 1f;

			// Si el método base está disponible, también lo llamamos
			// Nota: No podemos llamar a base.FireMainCannon() porque no es virtual
			// Tendríamos que usar reflexión o tener una referencia al método
		}

		public void SetGhostTurretRotation(float angle)
		{
			m_ghostTurretRotation = MathUtils.Clamp(angle, -MathUtils.DegToRad(180f), MathUtils.DegToRad(180f));

			// Intentar establecer también en el sistema base si es posible
			// Esto podría requerir reflexión
		}

		public void SetGhostCannonElevation(float angle)
		{
			m_ghostCannonElevation = MathUtils.Clamp(angle, -MathUtils.DegToRad(10f), MathUtils.DegToRad(45f));
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
