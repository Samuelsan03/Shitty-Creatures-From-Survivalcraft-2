using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class ReloadStatusParticleSystem : ParticleSystem<ReloadStatusParticleSystem.Particle>
	{
		private FontBatch3D m_batch;

		public ReloadStatusParticleSystem(Vector3 position, Vector3 velocity, string text)
			: base(1)
		{
			Random random = new Random();
			Particle particle = Particles[0];
			particle.IsActive = true;
			particle.Position = position;
			particle.TimeToLive = 1.0f;          // Duración un poco mayor
			particle.Velocity = velocity + random.Vector3(0.5f) * new Vector3(1f, 0f, 1f) + 0.3f * Vector3.UnitY;
			particle.Text = text;
			// El color base se actualizará en cada simulación para crear el arcoíris
			particle.BaseColor = Color.White;
		}

		public override bool Simulate(float dt)
		{
			dt = Math.Clamp(dt, 0f, 0.1f);
			float s = MathF.Pow(0.1f, dt);
			bool flag = false;

			for (int i = 0; i < Particles.Length; i++)
			{
				Particle particle = Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.TimeToLive -= dt;
					if (particle.TimeToLive > 0f)
					{
						// Movimiento suave hacia arriba
						particle.Velocity += new Vector3(0f, 0.4f, 0f) * dt;
						particle.Velocity *= s;
						particle.Position += particle.Velocity * dt;

						// Efecto arcoíris: el matiz varía con el tiempo restante
						float hue = (1.0f - particle.TimeToLive / 1.2f) * 2.0f; // dos ciclos completos
						hue = hue - (float)Math.Floor(hue); // normalizar a [0,1)
						Vector3 hsv = new Vector3(hue * 360f, 0.9f, 1.0f);
						Vector3 rgb = Color.HsvToRgb(hsv);
						Color rainbowColor = new Color(rgb.X, rgb.Y, rgb.Z, 1f);

						// Atenuación al final de la vida
						float alpha = MathUtils.Saturate(2f * particle.TimeToLive);
						particle.Color = rainbowColor * alpha;
					}
					else
					{
						particle.IsActive = false;
					}
				}
			}
			return !flag;
		}

		public override void Draw(Camera camera)
		{
			if (m_batch == null)
			{
				m_batch = SubsystemParticles.PrimitivesRenderer.FontBatch(
					LabelWidget.BitmapFont, 0, DepthStencilState.None, null, null, null);
			}

			Vector3 viewDirection = camera.ViewDirection;
			Vector3 right = Vector3.Normalize(Vector3.Cross(viewDirection, Vector3.UnitY));
			Vector3 up = -Vector3.Normalize(Vector3.Cross(right, viewDirection));

			for (int i = 0; i < Particles.Length; i++)
			{
				Particle particle = Particles[i];
				if (particle.IsActive)
				{
					float distance = Vector3.Distance(camera.ViewPosition, particle.Position);
					float fadeNear = MathUtils.Saturate(3f * (distance - 0.2f));
					float fadeFar = MathUtils.Saturate(0.2f * (20f - distance));
					float visibility = fadeNear * fadeFar;
					if (visibility > 0f)
					{
						float scale = 0.005f * MathF.Sqrt(distance);
						Color color = particle.Color * visibility;
						m_batch.QueueText(particle.Text, particle.Position, right * scale, up * scale,
										  color, TextAnchor.Center, Vector2.Zero);
					}
				}
			}
		}

		public class Particle : Game.Particle
		{
			public float TimeToLive;
			public Vector3 Velocity;
			public Color BaseColor;   // No se usa directamente, se mantiene por compatibilidad
			public string Text;
		}
	}
}
