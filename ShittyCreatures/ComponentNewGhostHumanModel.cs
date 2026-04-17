using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewGhostHumanModel : ComponentNewHumanModel, IUpdateable
	{
		// Variable para la opacidad del fantasma
		public float m_opacity = 0.7f;

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
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Primero cargar la configuración base (incluye parámetros de suavizado)
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar la opacidad desde XML
			if (valuesDictionary.ContainsKey("Opacity"))
			{
				m_opacity = valuesDictionary.GetValue<float>("Opacity");
			}

			// Ajustar parámetros adicionales para mejor apariencia fantasma
			if (m_opacity < 0.9f)
			{
				// Reducir aún más la brusquedad para fantasmas
				this.m_walkAnimationSpeed *= 0.95f;
				this.m_walkBobHeight *= 0.7f;

				// Aumentar suavizado para movimientos más etéreos
				SetSmoothFactor(m_smoothFactor * 1.3f);
				SetAnimationResponsiveness(m_animationResponsiveness * 0.8f);
			}

			}

		// Método para cambiar dinámicamente la opacidad
		public void SetOpacity(float opacity)
		{
			m_opacity = MathUtils.Clamp(opacity, 0.1f, 1f);
		}

		// Sobrescribir Update para agregar efectos visuales adicionales para fantasmas
		public override void Update(float dt)
		{
			// Actualizar animación base
			base.Update(dt);

			// Agregar efecto de parpadeo sutil para fantasmas con opacidad media
			if (m_opacity > 0.3f && m_opacity < 0.9f)
			{
				float flicker = 0.05f * (float)Math.Sin(m_animationTime * 2f);
				base.Opacity = new float?(m_opacity + flicker);
			}
		}
	}
}