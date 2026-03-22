using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Versión modificada de ComponentGlowingEyes que hace que los ojos brillen
	/// incluso durante el día, manteniendo la misma intensidad que en la noche.
	/// </summary>
	public class ComponentNewGlowingEyes : Component, IDrawable
	{
		// Propiedades públicas igual que el original
		public Vector3 GlowingEyesOffset { get; set; }
		public Color GlowingEyesColor { get; set; }
		public int[] DrawOrders => m_drawOrders;

		// Campos privados
		private SubsystemGlow m_subsystemGlow;
		private SubsystemTerrain m_subsystemTerrain;
		private ComponentCreatureModel m_componentCreatureModel;
		private GlowPoint[] m_eyeGlowPoints = new GlowPoint[2];
		private static int[] m_drawOrders = new int[1];

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemGlow = Project.FindSubsystem<SubsystemGlow>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			GlowingEyesOffset = valuesDictionary.GetValue<Vector3>("GlowingEyesOffset");
			GlowingEyesColor = valuesDictionary.GetValue<Color>("GlowingEyesColor");
		}

		public override void OnEntityAdded()
		{
			for (int i = 0; i < m_eyeGlowPoints.Length; i++)
				m_eyeGlowPoints[i] = m_subsystemGlow.AddGlowPoint();
		}

		public override void OnEntityRemoved()
		{
			for (int i = 0; i < m_eyeGlowPoints.Length; i++)
				m_subsystemGlow.RemoveGlowPoint(m_eyeGlowPoints[i]);
		}

		public virtual void Draw(Camera camera, int drawOrder)
		{
			if (m_eyeGlowPoints[0] == null || !m_componentCreatureModel.IsVisibleForCamera)
				return;

			// Resetear colores
			m_eyeGlowPoints[0].Color = Color.Transparent;
			m_eyeGlowPoints[1].Color = Color.Transparent;

			ModelBone headBone = m_componentCreatureModel.Model.FindBone("Head", false);
			if (headBone == null)
				return;

			Matrix transform = m_componentCreatureModel.AbsoluteBoneTransformsForCamera[headBone.Index];
			transform *= camera.InvertedViewMatrix;

			Vector3 upDirection = Vector3.Normalize(transform.Up);
			float distanceAlongView = Vector3.Dot(transform.Translation - camera.ViewPosition, camera.ViewDirection);

			if (distanceAlongView > 0f)
			{
				Vector3 headPos = transform.Translation;

				// --- CAMBIO IMPORTANTE: Se elimina la dependencia de la luz ambiental ---
				// Originalmente se usaba el factor (1 - num2 - 0.5)/0.5 para atenuar en días luminosos.
				// Ahora se ignora, así que los ojos brillan igual de intensos siempre.

				// Orientación: ¿la cabeza está mirando hacia la cámara?
				Vector3 cameraDirection = Vector3.Normalize(transform.Translation - camera.ViewPosition);
				float dot = Vector3.Dot(upDirection, cameraDirection);
				float orientationFactor = (dot < -0.7f) ? 1f : 0f;

				// Factor de distancia: cuanto más lejos, menos intenso (mínimo 0, máximo 1)
				float distanceFactor = MathUtils.Saturate(1f * (distanceAlongView - 1f));

				// Factor total = orientación * distancia
				float intensity = orientationFactor * distanceFactor;

				if (intensity > 0.25f)
				{
					Vector3 right = Vector3.Normalize(transform.Right);
					Vector3 forward = -Vector3.Normalize(transform.Forward);

					Color finalColor = GlowingEyesColor * intensity;

					// Ojo izquierdo
					m_eyeGlowPoints[0].Position = headPos
						+ right * GlowingEyesOffset.X
						+ forward * GlowingEyesOffset.Y
						+ upDirection * GlowingEyesOffset.Z;
					m_eyeGlowPoints[0].Right = right;
					m_eyeGlowPoints[0].Up = forward;
					m_eyeGlowPoints[0].Forward = upDirection;
					m_eyeGlowPoints[0].Size = 0.01f;
					m_eyeGlowPoints[0].FarSize = 0.06f;
					m_eyeGlowPoints[0].FarDistance = 14f;
					m_eyeGlowPoints[0].Color = finalColor;

					// Ojo derecho
					m_eyeGlowPoints[1].Position = headPos
						- right * GlowingEyesOffset.X
						+ forward * GlowingEyesOffset.Y
						+ upDirection * GlowingEyesOffset.Z;
					m_eyeGlowPoints[1].Right = right;
					m_eyeGlowPoints[1].Up = forward;
					m_eyeGlowPoints[1].Forward = upDirection;
					m_eyeGlowPoints[1].Size = 0.01f;
					m_eyeGlowPoints[1].FarSize = 0.06f;
					m_eyeGlowPoints[1].FarDistance = 14f;
					m_eyeGlowPoints[1].Color = finalColor;
				}
			}
		}
	}
}
