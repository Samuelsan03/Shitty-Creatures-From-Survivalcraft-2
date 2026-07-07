using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class PukeTrailParticleSystem : ParticleSystem<PukeTrailParticleSystem.Particle>, ITrailParticleSystem
	{
		public Vector3 Position { get; set; }

		public bool IsStopped { get; set; }

		public PukeTrailParticleSystem(int particlesCount, float size, float maxDuration, Color color) : base(particlesCount)
		{
			this.m_size = size;
			this.m_maxDuration = maxDuration;
			base.Texture = ContentManager.Get<Texture2D>("Textures/PukeParticle");
			base.TextureSlotsCount = 3;
			this.m_textureSlotMultiplier = this.m_random.Float(1.1f, 1.9f);
			this.m_textureSlotOffset = (float)((this.m_random.Float(0f, 1f) < 0.33f) ? 3 : 0);
			this.m_color = color;
		}

		public override bool Simulate(float dt)
		{
			this.m_duration += dt;
			if (this.m_duration > this.m_maxDuration)
			{
				this.IsStopped = true;
			}
			float num = Math.Clamp(50f / this.m_size, 10f, 40f);
			this.m_toGenerate += num * dt;
			float s = MathF.Pow(0.1f, dt);
			bool flag = false;
			for (int i = 0; i < base.Particles.Length; i++)
			{
				PukeTrailParticleSystem.Particle particle = base.Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.Time += dt;
					if (particle.Time <= particle.Duration)
					{
						particle.Position += particle.Velocity * dt;
						particle.Velocity *= s;
						PukeTrailParticleSystem.Particle particle2 = particle;
						// El vómito cae más rápido que el humo
						particle2.Velocity.Y = particle2.Velocity.Y + 15f * dt;
						particle.TextureSlot = (int)MathUtils.Min(9f * particle.Time / particle.Duration * this.m_textureSlotMultiplier + this.m_textureSlotOffset, 8f);
						particle.Size = new Vector2(this.m_size * (0.15f + 0.85f * particle.Time / particle.Duration));
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (!this.IsStopped && this.m_toGenerate >= 1f)
				{
					particle.IsActive = true;
					Vector3 v = new Vector3(this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f));
					particle.Position = this.Position + 0.025f * v;
					particle.Color = this.m_color;
					particle.Velocity = 0.2f * v;
					particle.Time = 0f;
					particle.Size = new Vector2(0.15f * this.m_size);
					particle.Duration = (float)base.Particles.Length / num * this.m_random.Float(0.8f, 1.05f);
					particle.FlipX = this.m_random.Bool();
					particle.FlipY = this.m_random.Bool();
					this.m_toGenerate -= 1f;
				}
			}
			return this.IsStopped && !flag;
		}

		public Random m_random = new Random();
		public float m_toGenerate;
		public float m_textureSlotMultiplier;
		public float m_textureSlotOffset;
		public float m_duration;
		public float m_size;
		public float m_maxDuration;
		public Color m_color;

		public class Particle : Game.Particle
		{
			public float Time;
			public float Duration;
			public Vector3 Velocity;
		}
	}
}
