using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class CloningParticleSystem : ParticleSystem<CloningParticleSystem.Particle>
	{
		public bool Stopped { get; set; }
		public Vector3 Position { get; set; }
		public BoundingBox BoundingBox { get; set; }

		public CloningParticleSystem() : base(40)
		{
			Texture = ContentManager.Get<Texture2D>("Textures/ShapeshiftParticle");
			TextureSlotsCount = 3;
		}

		public override bool Simulate(float dt)
		{
			bool flag = false;
			m_generationSpeed = MathUtils.Min(m_generationSpeed + 15f * dt, 35f);
			m_toGenerate += m_generationSpeed * dt;
			for (int i = 0; i < Particles.Length; i++)
			{
				Particle particle = Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.Time += dt;
					if (particle.Time <= particle.Duration)
					{
						particle.Position += particle.Velocity * dt;
						particle.FlipX = m_random.Bool();
						particle.FlipY = m_random.Bool();
						particle.TextureSlot = (int)MathUtils.Min(9.900001f * particle.Time / particle.Duration, 8f);
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (!Stopped)
				{
					while (m_toGenerate >= 1f)
					{
						particle.IsActive = true;
						particle.Position.X = m_random.Float(BoundingBox.Min.X, BoundingBox.Max.X);
						particle.Position.Y = m_random.Float(BoundingBox.Min.Y, BoundingBox.Max.Y);
						particle.Position.Z = m_random.Float(BoundingBox.Min.Z, BoundingBox.Max.Z);
						particle.Velocity = new Vector3(0f, m_random.Float(0.5f, 1.5f), 0f);
						particle.Color = new Color(9, 0, 255);
						particle.Size = new Vector2(0.4f);
						particle.Time = 0f;
						particle.Duration = m_random.Float(0.75f, 1.5f);
						m_toGenerate -= 1f;
					}
				}
				else
				{
					m_toGenerate = 0f;
				}
			}
			m_toGenerate = MathUtils.Remainder(m_toGenerate, 1f);
			return Stopped && !flag;
		}

		Random m_random = new Random();
		float m_generationSpeed;
		float m_toGenerate;

		public class Particle : Game.Particle
		{
			public float Time;
			public float Duration;
			public Vector3 Velocity;
		}
	}
}
