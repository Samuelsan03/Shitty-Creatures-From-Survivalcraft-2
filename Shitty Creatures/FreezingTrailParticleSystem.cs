using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FreezingTrailParticleSystem : ParticleSystem<FreezingTrailParticleSystem.Particle>, ITrailParticleSystem
	{
		public Random m_random = new Random();
		public float m_toGenerate;
		public float m_textureSlotMultiplier;
		public float m_textureSlotOffset;
		public float m_duration;
		public float m_size;
		public float m_maxDuration;
		public Color m_color;

		public Vector3 Position { get; set; }
		public bool IsStopped { get; set; }

		public FreezingTrailParticleSystem(int particlesCount, float size, float maxDuration, Color color) : base(particlesCount)
		{
			m_size = size;
			m_maxDuration = maxDuration;
			Texture = ContentManager.Get<Texture2D>("Textures/Gui/congelante particulas");
			TextureSlotsCount = 3;
			m_textureSlotMultiplier = m_random.Float(1.1f, 1.9f);
			m_textureSlotOffset = (float)((m_random.Float(0f, 1f) < 0.33f) ? 3 : 0);
			m_color = color;
		}

		public override bool Simulate(float dt)
		{
			m_duration += dt;
			if (m_duration > m_maxDuration)
			{
				IsStopped = true;
			}
			float num = Math.Clamp(50f / m_size, 10f, 40f);
			m_toGenerate += num * dt;
			float s = MathF.Pow(0.1f, dt);
			bool flag = false;
			for (int i = 0; i < Particles.Length; i++)
			{
				FreezingTrailParticleSystem.Particle particle = Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.Time += dt;
					if (particle.Time <= particle.Duration)
					{
						particle.Position += particle.Velocity * dt;
						particle.Velocity *= s;
						FreezingTrailParticleSystem.Particle particle2 = particle;
						particle2.Velocity.Y = particle2.Velocity.Y + 10f * dt;
						particle.TextureSlot = (int)MathUtils.Min(9f * particle.Time / particle.Duration * m_textureSlotMultiplier + m_textureSlotOffset, 8f);
						particle.Size = new Vector2(m_size * (0.15f + 0.8f * particle.Time / particle.Duration));
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (!IsStopped && m_toGenerate >= 1f)
				{
					particle.IsActive = true;
					Vector3 v = new Vector3(m_random.Float(-1f, 1f), m_random.Float(-1f, 1f), m_random.Float(-1f, 1f));
					particle.Position = Position + 0.025f * v;
					particle.Color = m_color;
					particle.Velocity = 0.2f * v;
					particle.Time = 0f;
					particle.Size = new Vector2(0.15f * m_size);
					particle.Duration = (float)Particles.Length / num * m_random.Float(0.8f, 1.05f);
					particle.FlipX = m_random.Bool();
					particle.FlipY = m_random.Bool();
					m_toGenerate -= 1f;
				}
			}
			return IsStopped && !flag;
		}

		public new class Particle : Game.Particle
		{
			public float Time;
			public float Duration;
			public Vector3 Velocity;
		}
	}
}
