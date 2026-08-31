using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FreezeExplosionParticleSystem : ParticleSystem<FreezeExplosionParticleSystem.Particle>
	{
		public FreezeExplosionParticleSystem() : base(1000)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/congelante particulas");
			base.TextureSlotsCount = 3;
			this.m_inactiveParticles.AddRange(base.Particles);
		}

		public void SetExplosionCell(Point3 point, float strength)
		{
			FreezeExplosionParticleSystem.Particle particle;
			if (!this.m_particlesByPoint.TryGetValue(point, out particle))
			{
				if (this.m_inactiveParticles.Count > 0)
				{
					List<FreezeExplosionParticleSystem.Particle> inactiveParticles = this.m_inactiveParticles;
					particle = inactiveParticles[inactiveParticles.Count - 1];
					this.m_inactiveParticles.RemoveAt(this.m_inactiveParticles.Count - 1);
				}
				else
				{
					for (int i = 0; i < 5; i++)
					{
						int num = this.m_random.Int(0, base.Particles.Length - 1);
						if (strength > base.Particles[num].Strength)
						{
							particle = base.Particles[num];
						}
					}
				}
				if (particle != null)
				{
					this.m_particlesByPoint.Add(point, particle);
				}
			}
			if (particle != null)
			{
				particle.IsActive = true;
				particle.Position = new Vector3((float)point.X, (float)point.Y, (float)point.Z) + new Vector3(0.5f) + 0.2f * new Vector3(this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f));
				particle.Size = new Vector2(this.m_random.Float(0.6f, 0.9f));
				particle.Strength = strength;
				particle.Color = new Color(200, 230, 255);
				this.m_isEmpty = false;
			}
		}

		public override bool Simulate(float dt)
		{
			if (!this.m_isEmpty)
			{
				this.m_isEmpty = true;
				for (int i = 0; i < base.Particles.Length; i++)
				{
					FreezeExplosionParticleSystem.Particle particle = base.Particles[i];
					if (particle.IsActive)
					{
						this.m_isEmpty = false;
						particle.Strength -= dt / 1.5f;
						if (particle.Strength > 0f)
						{
							particle.TextureSlot = (int)MathUtils.Min(9f * (1f - particle.Strength) * 0.6f, 8f);
							FreezeExplosionParticleSystem.Particle particle2 = particle;
							particle2.Position.Y = particle2.Position.Y + 2f * MathUtils.Max(1f - particle.Strength - 0.25f, 0f) * dt;
						}
						else
						{
							particle.IsActive = false;
							this.m_inactiveParticles.Add(particle);
						}
					}
				}
			}
			return false;
		}

		public override void Draw(Camera camera)
		{
			if (!this.m_isEmpty)
			{
				base.Draw(camera);
			}
		}

		public Dictionary<Point3, FreezeExplosionParticleSystem.Particle> m_particlesByPoint = new Dictionary<Point3, FreezeExplosionParticleSystem.Particle>();

		public List<FreezeExplosionParticleSystem.Particle> m_inactiveParticles = new List<FreezeExplosionParticleSystem.Particle>();

		public Random m_random = new Random();

		public const float m_duration = 1.5f;

		public bool m_isEmpty;

		public class Particle : Game.Particle
		{
			public float Strength;
		}
	}
}
