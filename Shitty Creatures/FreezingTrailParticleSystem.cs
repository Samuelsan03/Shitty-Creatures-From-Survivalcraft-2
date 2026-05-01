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

		public FreezingTrailParticleSystem(Vector3 position, float size, float maxVisibilityDistance) : base(100) // Menos partículas
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

			// Generar partículas solo si no está detenido
			if (!IsStopped)
			{
				// Tasa de emisión reducida para una nube densa pero no excesiva
				m_toGenerate += 15f * dt;

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
							// Animación rápida de textura (3 slots)
							p.TextureSlot = (int)MathUtils.Min(2f * p.Time / 0.8f, 2f); // ciclo rápido
							p.Color = Color.Lerp(new Color(200, 240, 255, 220), Color.Transparent, p.Time / 0.8f);
							// Casi sin movimiento para que rodeen la bola
							p.Position += p.Velocity * dt;
						}
						else
						{
							p.IsActive = false;
						}
					}
					else if (m_toGenerate >= 1f)
					{
						p.IsActive = true;
						// Posición en una pequeña esfera alrededor del centro
						Vector3 offset = m_random.Float(-0.3f, 0.3f) * Vector3.UnitX
										+ m_random.Float(-0.3f, 0.3f) * Vector3.UnitY
										+ m_random.Float(-0.3f, 0.3f) * Vector3.UnitZ;
						p.Position = m_position + offset * m_size;
						p.Color = new Color(200, 240, 255, 220);
						float s = m_size * m_random.Float(0.8f, 1.2f);
						p.Size = new Vector2(s, s);
						// Velocidad casi nula para que no se alejen
						p.Velocity = new Vector3(
							m_random.Float(-0.05f, 0.05f),
							m_random.Float(-0.05f, 0.05f),
							m_random.Float(-0.05f, 0.05f)
						);
						p.Time = 0f;
						// Vida muy corta: 0.5 a 1 segundo (rastro corto)
						p.TimeToLive = m_random.Float(0.5f, 1f);
						p.FlipX = (m_random.Int(0, 1) == 0);
						p.FlipY = (m_random.Int(0, 1) == 0);
						m_toGenerate -= 1f;
					}
				}

				m_toGenerate = MathUtils.Remainder(m_toGenerate, 1f);
			}
			else
			{
				// Modo detenido: solo simular partículas existentes hasta que mueran
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
							p.TextureSlot = (int)MathUtils.Min(2f * p.Time / 0.8f, 2f);
							p.Color = Color.Lerp(new Color(200, 240, 255, 220), Color.Transparent, p.Time / 0.8f);
							p.Position += p.Velocity * dt;
						}
						else
						{
							p.IsActive = false;
						}
					}
				}
			}

			// El sistema se elimina cuando está detenido y no quedan partículas activas
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
