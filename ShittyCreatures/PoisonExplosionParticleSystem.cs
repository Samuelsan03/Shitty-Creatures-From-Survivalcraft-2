using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class PoisonExplosionParticleSystem : ParticleSystem<PoisonExplosionParticleSystem.Particle>
	{
		public const float Duration = 1.5f;

		private Dictionary<Point3, Particle> m_particlesByPoint = new Dictionary<Point3, Particle>();
		private List<Particle> m_inactiveParticles = new List<Particle>();
		private Random m_random = new Random();
		private bool m_isEmpty = true;

		public PoisonExplosionParticleSystem() : base(1000)
		{
			// Set the poison texture
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/vomito venenoso");
			base.TextureSlotsCount = 3;

			// Initialize inactive list with all particles
			m_inactiveParticles.AddRange(base.Particles);
		}

		public void SetExplosionCell(Point3 point, float strength)
		{
			Particle particle = null;

			// Try to reuse an existing particle for this point
			if (!m_particlesByPoint.TryGetValue(point, out particle))
			{
				// Get an inactive particle if available
				if (m_inactiveParticles.Count > 0)
				{
					particle = m_inactiveParticles[m_inactiveParticles.Count - 1];
					m_inactiveParticles.RemoveAt(m_inactiveParticles.Count - 1);
				}
				else
				{
					// Otherwise try to steal a particle with lower strength
					for (int i = 0; i < 5; i++)
					{
						int index = m_random.Int(0, base.Particles.Length - 1);
						if (strength > base.Particles[index].Strength)
						{
							particle = base.Particles[index];
						}
					}
				}

				if (particle != null)
				{
					m_particlesByPoint.Add(point, particle);
				}
			}

			if (particle != null)
			{
				particle.IsActive = true;
				particle.Position = new Vector3(point.X, point.Y, point.Z) + new Vector3(0.5f)
									+ 0.2f * new Vector3(
										m_random.Float(-1f, 1f),
										m_random.Float(-1f, 1f),
										m_random.Float(-1f, 1f));
				particle.Size = new Vector2(m_random.Float(0.6f, 0.9f));
				particle.Strength = strength;
				particle.Color = Color.White;
				m_isEmpty = false;
			}
		}

		public override bool Simulate(float dt)
		{
			if (!m_isEmpty)
			{
				m_isEmpty = true;

				for (int i = 0; i < base.Particles.Length; i++)
				{
					Particle particle = base.Particles[i];
					if (particle.IsActive)
					{
						m_isEmpty = false;
						particle.Strength -= dt / Duration;

						if (particle.Strength > 0f)
						{
							// Texture slot calculation (matching original explosion behavior)
							particle.TextureSlot = (int)MathUtils.Min(9f * (1f - particle.Strength) * 0.6f, 8f);

							// Rise effect
							particle.Position.Y += 2f * MathUtils.Max(1f - particle.Strength - 0.25f, 0f) * dt;
						}
						else
						{
							particle.IsActive = false;
							m_inactiveParticles.Add(particle);
						}
					}
				}
			}

			return false;
		}

		public override void Draw(Camera camera)
		{
			if (!m_isEmpty)
			{
				base.Draw(camera);
			}
		}

		public class Particle : Game.Particle
		{
			public float Strength;
		}
	}
}
