using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class BloodParticleSystem : ParticleSystem<BloodParticleSystem.Particle>
	{
		public Vector3 Position { get; set; }
		public bool IsStopped { get; set; }

		private SubsystemTerrain m_subsystemTerrain;
		private Random m_random = new Random();
		private float m_duration;
		private float m_toGenerate;

		public BloodParticleSystem(SubsystemTerrain terrain) : base(80)
		{
			m_subsystemTerrain = terrain;
			Texture = ContentManager.Get<Texture2D>("Textures/Gui/blood");
			TextureSlotsCount = 3;
		}

		public override bool Simulate(float dt)
		{
			dt = Math.Clamp(dt, 0f, 0.1f);
			float s2 = MathF.Pow(0.02f, dt);
			m_duration += dt;

			float num5 = MathUtils.Saturate(1.3f * SimplexNoise.Noise(3f * m_duration + (float)(GetHashCode() % 100)) - 0.3f);
			float num6 = 30f * num5;
			m_toGenerate += num6 * dt;

			bool flag = false;
			for (int i = 0; i < Particles.Length; i++)
			{
				Particle particle = Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.TimeToLive -= dt;
					if (particle.TimeToLive > 0f)
					{
						Vector3 position = particle.Position;
						Vector3 vector = position + particle.Velocity * dt;
						TerrainRaycastResult? terrainRaycastResult = m_subsystemTerrain.Raycast(position, vector, false, true, (int value, float _) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable_(value));
						if (terrainRaycastResult != null)
						{
							Plane plane = terrainRaycastResult.Value.CellFace.CalculatePlane();
							vector = position;
							if (plane.Normal.X != 0f)
								particle.Velocity *= new Vector3(-0.05f, 0.05f, 0.05f);
							if (plane.Normal.Y != 0f)
								particle.Velocity *= new Vector3(0.05f, -0.05f, 0.05f);
							if (plane.Normal.Z != 0f)
								particle.Velocity *= new Vector3(0.05f, 0.05f, -0.05f);
						}
						particle.Position = vector;
						particle.Velocity.Y += -9.81f * dt;
						particle.Velocity *= s2;
						particle.Color *= MathUtils.Saturate(particle.TimeToLive);
						particle.TextureSlot = (int)(8.99f * MathUtils.Saturate(3f - particle.TimeToLive));
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (!IsStopped && m_toGenerate >= 1f)
				{
					Vector3 v = m_random.Vector3(0f, 1f);
					particle.IsActive = true;
					particle.Position = Position + 0.05f * v;
					particle.Color = Color.White;
					particle.Velocity = MathUtils.Lerp(1f, 2.5f, num5) * Vector3.Normalize(new Vector3(v.X, -Math.Abs(v.Y), v.Z) + new Vector3(0f, -1f, 0f));
					particle.TimeToLive = 3f;
					particle.Size = new Vector2(0.35f);
					particle.FlipX = m_random.Bool();
					particle.FlipY = m_random.Bool();
					m_toGenerate -= 1f;
				}
			}
			return IsStopped && !flag;
		}

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float TimeToLive;
		}
	}
}
