using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class BloodParticleSystem : ParticleSystem<BloodParticleSystem.Particle>
	{
		public Vector3 Position { get; set; }
		public Vector3 Direction { get; set; }
		public bool IsStopped { get; set; }

		private Random m_random = new Random();
		private SubsystemTerrain m_subsystemTerrain;
		private float m_time;
		private bool m_large;

		public BloodParticleSystem(SubsystemTerrain terrain, Vector3 position, bool large = false) : base(30)
		{
			m_subsystemTerrain = terrain;
			Position = position;
			m_large = large;
			Texture = ContentManager.Get<Texture2D>("Textures/Gui/sangre");
			TextureSlotsCount = 3;

			int x = Terrain.ToCell(position.X);
			int y = Terrain.ToCell(position.Y);
			int z = Terrain.ToCell(position.Z);
			int light = 0;
			light = MathUtils.Max(light, terrain.Terrain.GetCellLight(x + 1, y, z));
			light = MathUtils.Max(light, terrain.Terrain.GetCellLight(x - 1, y, z));
			light = MathUtils.Max(light, terrain.Terrain.GetCellLight(x, y + 1, z));
			light = MathUtils.Max(light, terrain.Terrain.GetCellLight(x, y - 1, z));
			light = MathUtils.Max(light, terrain.Terrain.GetCellLight(x, y, z + 1));
			light = MathUtils.Max(light, terrain.Terrain.GetCellLight(x, y, z - 1));

			Color baseColor = Color.White;
			float lightIntensity = LightingManager.LightIntensityByLightValue[light];
			baseColor *= lightIntensity;
			baseColor.A = byte.MaxValue;

			float sizeMultiplier = large ? 1.5f : 1f;

			for (int i = 0; i < Particles.Length; i++)
			{
				var particle = Particles[i];
				particle.IsActive = true;
				particle.Position = position;
				particle.Color = baseColor;
				particle.Size = new Vector2(0.14f * sizeMultiplier);
				particle.Duration = m_random.Float(2f, 5f);
				particle.TimeToLive = particle.Duration;

				Vector3 horizontal = new Vector3(m_random.Float(-1f, 1f), 0f, m_random.Float(-1f, 1f));
				horizontal = Vector3.Normalize(horizontal) * m_random.Float(0f, 1.5f);
				particle.Velocity = sizeMultiplier * (horizontal + new Vector3(0f, m_random.Float(0f, 5f), 0f));
			}
		}

		public override bool Simulate(float dt)
		{
			dt = Math.Clamp(dt, 0f, 0.1f);
			float damping = MathF.Pow(0.1f, dt);
			m_time += dt;

			bool anyActive = false;

			for (int i = 0; i < Particles.Length; i++)
			{
				var particle = Particles[i];

				if (particle.IsActive)
				{
					anyActive = true;
					particle.TimeToLive -= dt;

					if (particle.TimeToLive > 0f)
					{
						// Solo mover si tiene velocidad (no es charco)
						if (particle.Velocity.Length() > 0.1f)
						{
							Vector3 oldPos = particle.Position;
							Vector3 newPos = oldPos + particle.Velocity * dt;

							int x = Terrain.ToCell(newPos.X);
							int y = Terrain.ToCell(newPos.Y);
							int z = Terrain.ToCell(newPos.Z);

							int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
							int contents = Terrain.ExtractContents(cellValue);

							if (contents != 0 && BlocksManager.Blocks[contents].IsCollidable_(cellValue))
							{
								particle.Velocity = Vector3.Zero;
							}
							else
							{
								particle.Position = newPos;
								particle.Velocity.Y += -10f * dt;
								particle.Velocity *= damping;
							}
						}

						particle.Color *= MathUtils.Saturate(particle.TimeToLive / particle.Duration);
						particle.TextureSlot = (int)(3.99f * (1f - particle.TimeToLive / particle.Duration));
						particle.FlipX = m_random.Bool();
						particle.FlipY = m_random.Bool();
					}
					else
					{
						particle.IsActive = false;
					}
				}
			}

			// Detectar si es un charco (todas las partículas con velocidad cero)
			bool isBloodPool = true;
			for (int i = 0; i < Particles.Length; i++)
			{
				if (Particles[i].IsActive && Particles[i].Velocity.Length() > 0.1f)
				{
					isBloodPool = false;
					break;
				}
			}

			if (isBloodPool)
			{
				// Para charcos: detener solo cuando no haya partículas activas
				return !anyActive;
			}
			else
			{
				// Para sangre normal: detener después de 11 segundos o cuando no haya partículas
				if (m_time > 11f)
				{
					IsStopped = true;
				}
				return IsStopped && !anyActive;
			}
		}

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float TimeToLive;
			public float Duration;
		}
	}
}
