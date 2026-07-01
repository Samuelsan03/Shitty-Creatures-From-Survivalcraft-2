using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class HealTextParticleSystem : ParticleSystem<HealTextParticleSystem.Particle>
	{
		private float m_elapsedTime;

		public HealTextParticleSystem(Vector3 position, Vector3 velocity, string text) : base(1)
		{
			Random random = new Random();
			HealTextParticleSystem.Particle particle = base.Particles[0];
			particle.IsActive = true;
			particle.Position = position;
			particle.TimeToLive = 1.0f;
			particle.Velocity = velocity + random.Vector3(0.5f) * new Vector3(1f, 0f, 1f) + 0.8f * Vector3.UnitY;
			particle.Text = text;
			m_elapsedTime = 0f;
		}

		public override bool Simulate(float dt)
		{
			dt = Math.Clamp(dt, 0f, 0.1f);
			m_elapsedTime += dt;
			float s = MathF.Pow(0.1f, dt);
			bool flag = false;
			for (int i = 0; i < base.Particles.Length; i++)
			{
				HealTextParticleSystem.Particle particle = base.Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.TimeToLive -= dt;
					if (particle.TimeToLive > 0f)
					{
						particle.Velocity += new Vector3(0f, 0.5f, 0f) * dt;
						particle.Velocity *= s;
						particle.Position += particle.Velocity * dt;
						particle.Color = GetRainbowColor(m_elapsedTime) * MathUtils.Saturate(2f * particle.TimeToLive);
					}
					else
					{
						particle.IsActive = false;
					}
				}
			}
			return !flag;
		}

		private Color GetRainbowColor(float time)
		{
			float hue = (time * 1.5f) % 1f;
			return HsvToRgb(hue, 1f, 1f);
		}

		private Color HsvToRgb(float h, float s, float v)
		{
			float r, g, b;
			float i = (float)Math.Floor(h * 6f);
			float f = h * 6f - i;
			float p = v * (1f - s);
			float q = v * (1f - f * s);
			float t = v * (1f - (1f - f) * s);

			switch ((int)i % 6)
			{
				case 0:
					r = v; g = t; b = p;
					break;
				case 1:
					r = q; g = v; b = p;
					break;
				case 2:
					r = p; g = v; b = t;
					break;
				case 3:
					r = p; g = q; b = v;
					break;
				case 4:
					r = t; g = p; b = v;
					break;
				default:
					r = v; g = p; b = q;
					break;
			}

			return new Color(r, g, b);
		}

		public override void Draw(Camera camera)
		{
			if (this.m_batch == null)
			{
				this.m_batch = this.SubsystemParticles.PrimitivesRenderer.FontBatch(LabelWidget.BitmapFont, 0, DepthStencilState.None, null, null, null);
			}
			Vector3 viewDirection = camera.ViewDirection;
			Vector3 vector = Vector3.Normalize(Vector3.Cross(viewDirection, Vector3.UnitY));
			Vector3 v = -Vector3.Normalize(Vector3.Cross(vector, viewDirection));
			for (int i = 0; i < base.Particles.Length; i++)
			{
				HealTextParticleSystem.Particle particle = base.Particles[i];
				if (particle.IsActive)
				{
					float num = Vector3.Distance(camera.ViewPosition, particle.Position);
					float s = 0.006f * MathF.Sqrt(num);
					this.m_batch.QueueText(particle.Text, particle.Position, vector * s, v * s, particle.Color, TextAnchor.Center, Vector2.Zero);
				}
			}
		}

		public FontBatch3D m_batch;

		public class Particle : Game.Particle
		{
			public float TimeToLive;
			public Vector3 Velocity;
			public string Text;
		}
	}
}
