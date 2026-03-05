using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FreezingTrailParticleSystem : ParticleSystem<FreezingTrailParticleSystem.Particle>, ITrailParticleSystem
	{
		private Random m_random = new Random();
		private Vector3 m_position;
		private float m_size;
		private float m_toGenerate;
		private float m_age;

		public bool IsStopped { get; set; }

		Vector3 ITrailParticleSystem.Position
		{
			get => m_position;
			set => m_position = value;
		}

		bool ITrailParticleSystem.IsStopped
		{
			get => IsStopped;
			set => IsStopped = value;
		}

		public FreezingTrailParticleSystem(Vector3 position, float size, float maxVisibilityDistance) : base(500) // Aumentado a 500 partículas
		{
			m_position = position;
			m_size = size;
			Texture = ContentManager.Get<Texture2D>("Textures/Gui/congelante particulas");
			TextureSlotsCount = 3;
		}

		public override bool Simulate(float dt)
		{
			m_age += dt;
			bool anyActive = false;

			if (!IsStopped || m_age < 2f)
			{
				// Tasa de generación reducida a 50 por segundo para equilibrar con la mayor capacidad
				m_toGenerate += (IsStopped ? 0f : (50f * dt));

				for (int i = 0; i < Particles.Length; i++)
				{
					Particle p = Particles[i];
					if (p.IsActive)
					{
						anyActive = true;
						p.Time += dt;
						p.TimeToLive -= dt;

						if (p.TimeToLive > 0f)
						{
							p.TextureSlot = (int)MathUtils.Min(9f * p.Time / p.TimeToLive, 8f);
							p.Color = Color.Lerp(new Color(180, 230, 255, 220), Color.Transparent, p.Time / p.TimeToLive);
						}
						else
						{
							p.IsActive = false;
						}
					}
					else if (m_toGenerate >= 1f)
					{
						p.IsActive = true;
						Vector3 offset = new Vector3(
							m_random.Float(-0.1f, 0.1f),
							m_random.Float(-0.1f, 0.1f),
							m_random.Float(-0.1f, 0.1f)
						);
						p.Position = m_position + offset * m_size;

						p.Color = new Color(180, 230, 255, 220);
						float s = m_size * m_random.Float(0.5f, 1.5f);
						p.Size = new Vector2(s, s);
						p.Velocity = Vector3.Zero;
						p.Time = 0f;
						// Aumentar vida útil a 8-12 segundos para una estela más larga
						p.TimeToLive = m_random.Float(8f, 12f);
						p.FlipX = (m_random.Int(0, 1) == 0);
						p.FlipY = (m_random.Int(0, 1) == 0);
						m_toGenerate -= 1f;
					}
				}

				m_toGenerate = MathUtils.Remainder(m_toGenerate, 1f);
			}

			return IsStopped && !anyActive;
		}

		public override void Draw(Camera camera)
		{
			base.Draw(camera);
		}

		public new class Particle : Game.Particle
		{
			public float Time;
			public float TimeToLive;
			public Vector3 Velocity;
		}
	}
}
