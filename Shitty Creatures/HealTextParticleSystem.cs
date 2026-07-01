using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class HealTextParticleSystem : ParticleSystem<HealTextParticleSystem.Particle>
	{
		private FontBatch3D m_batch;
		private float m_hue;
		public float TextScale; // Escala del texto (ajustable)

		public HealTextParticleSystem(Vector3 position, Vector3 velocity, string text, float scale = 0.012f) : base(1)
		{
			var random = new Random();
			Particle p = Particles[0];
			p.IsActive = true;
			p.Position = position;
			p.TimeToLive = 1.0f; // Duración 3 segundos
			p.Velocity = velocity + random.Vector3(0.5f) * new Vector3(1f, 0f, 1f) + 0.8f * Vector3.UnitY;
			p.Text = text;
			m_hue = 0f;
			TextScale = scale;
		}

		public override bool Simulate(float dt)
		{
			dt = Math.Clamp(dt, 0f, 0.1f);
			float fade = MathF.Pow(0.1f, dt);
			bool anyAlive = false;
			for (int i = 0; i < Particles.Length; i++)
			{
				Particle p = Particles[i];
				if (p.IsActive)
				{
					anyAlive = true;
					p.TimeToLive -= dt;
					if (p.TimeToLive > 0f)
					{
						// Cambio de color arcoíris
						m_hue += dt * 0.35f;
						if (m_hue > 1f) m_hue -= 1f;
						Vector3 hsv = new Vector3(m_hue, 1f, 1f);
						Vector3 rgb = Color.HsvToRgb(hsv);
						p.Color = new Color(rgb.X, rgb.Y, rgb.Z, 1f) * MathUtils.Saturate(2f * p.TimeToLive);

						// Física: sube y se frena
						p.Velocity += new Vector3(0f, 0.3f, 0f) * dt;
						p.Velocity *= fade;
						p.Position += p.Velocity * dt;
					}
					else
					{
						p.IsActive = false;
					}
				}
			}
			return !anyAlive;
		}

		public override void Draw(Camera camera)
		{
			if (m_batch == null)
				m_batch = SubsystemParticles.PrimitivesRenderer.FontBatch(LabelWidget.BitmapFont, 0, DepthStencilState.None, null, null, null);

			Vector3 viewDir = camera.ViewDirection;
			Vector3 right = Vector3.Normalize(Vector3.Cross(viewDir, Vector3.UnitY));
			Vector3 up = -Vector3.Normalize(Vector3.Cross(right, viewDir));

			for (int i = 0; i < Particles.Length; i++)
			{
				Particle p = Particles[i];
				if (!p.IsActive) continue;

				float dist = Vector3.Distance(camera.ViewPosition, p.Position);
				float fadeNear = MathUtils.Saturate(3f * (dist - 0.2f));
				float fadeFar = MathUtils.Saturate(0.2f * (20f - dist));
				float alpha = fadeNear * fadeFar;
				if (alpha <= 0f) continue;

				float scale = TextScale * MathF.Sqrt(dist);
				Color color = p.Color * alpha;
				m_batch.QueueText(p.Text, p.Position, right * scale, up * scale, color, TextAnchor.Center, Vector2.Zero);
			}
		}

		public class Particle : Game.Particle
		{
			public float TimeToLive;
			public Vector3 Velocity;
			public string Text;
		}
	}
}
