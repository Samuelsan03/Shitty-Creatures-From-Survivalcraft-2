// FreezeExplosionParticleSystem.cs
using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FreezeExplosionParticleSystem : ParticleSystem<FreezeExplosionParticleSystem.Particle>
	{
		public const float Duration = 1.5f;

		private Dictionary<Point3, Particle> m_particlesByPoint = new Dictionary<Point3, Particle>();
		private List<Particle> m_inactiveParticles = new List<Particle>();
		private Random m_random = new Random();
		private bool m_isEmpty = true;

		public FreezeExplosionParticleSystem() : base(1000)
		{
			Texture = ContentManager.Get<Texture2D>("Textures/Gui/congelante particulas");
			TextureSlotsCount = 3;
			m_inactiveParticles.AddRange(Particles);
		}

		public void SetExplosionCell(Point3 point, float strength)
		{
			Particle particle = null;

			if (!m_particlesByPoint.TryGetValue(point, out particle))
			{
				if (m_inactiveParticles.Count > 0)
				{
					particle = m_inactiveParticles[m_inactiveParticles.Count - 1];
					m_inactiveParticles.RemoveAt(m_inactiveParticles.Count - 1);
				}
				else
				{
					for (int i = 0; i < 5; i++)
					{
						int index = m_random.Int(0, Particles.Length - 1);
						if (strength > Particles[index].Strength)
						{
							particle = Particles[index];
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
				particle.Color = new Color(200, 230, 255);
				m_isEmpty = false;
			}
		}

		public override bool Simulate(float dt)
		{
			if (!m_isEmpty)
			{
				m_isEmpty = true;

				for (int i = 0; i < Particles.Length; i++)
				{
					Particle p = Particles[i];
					if (p.IsActive)
					{
						m_isEmpty = false;
						p.Strength -= dt / Duration;

						if (p.Strength > 0f)
						{
							p.TextureSlot = (int)MathUtils.Min(9f * (1f - p.Strength) * 0.6f, 8f);
							p.Position.Y += 2f * MathUtils.Max(1f - p.Strength - 0.25f, 0f) * dt;
							p.Color.A = (byte)(255 * p.Strength);
						}
						else
						{
							p.IsActive = false;
							m_inactiveParticles.Add(p);
						}
					}
				}
			}
			return false;
		}

		public override void Draw(Camera camera)
		{
			if (!m_isEmpty)
				base.Draw(camera);
		}

		public class Particle : Game.Particle
		{
			public float Strength;
		}
	}
}
