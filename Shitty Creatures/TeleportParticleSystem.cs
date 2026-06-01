using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class TeleportParticleSystem : ParticleSystem<TeleportParticleSystem.Particle>
	{
		private static readonly Color TeleportColorInner = new Color(180, 60, 255, 255);
		private static readonly Color TeleportColorOuter = new Color(100, 20, 180, 200);

		public TeleportParticleSystem(SubsystemTerrain terrain, Vector3 position, float size, bool isAppearEffect = false, float particleDuration = 3f)
			: base(25)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/KillParticle");

			int num = Terrain.ToCell(position.X);
			int num2 = Terrain.ToCell(position.Y);
			int num3 = Terrain.ToCell(position.Z);
			int num4 = 0;
			num4 = MathUtils.Max(num4, terrain.Terrain.GetCellLight(num + 1, num2, num3));
			num4 = MathUtils.Max(num4, terrain.Terrain.GetCellLight(num - 1, num2, num3));
			num4 = MathUtils.Max(num4, terrain.Terrain.GetCellLight(num, num2 + 1, num3));
			num4 = MathUtils.Max(num4, terrain.Terrain.GetCellLight(num, num2 - 1, num3));
			num4 = MathUtils.Max(num4, terrain.Terrain.GetCellLight(num, num2, num3 + 1));
			num4 = MathUtils.Max(num4, terrain.Terrain.GetCellLight(num, num2, num3 - 1));

			base.TextureSlotsCount = 2;

			float lightIntensity = LightingManager.LightIntensityByLightValue[num4];

			for (int i = 0; i < base.Particles.Length; i++)
			{
				TeleportParticleSystem.Particle particle = base.Particles[i];
				particle.IsActive = true;

				particle.Position = position + 0.4f * size * new Vector3(
					m_random.Float(-1f, 1f),
					m_random.Float(-1f, 1f),
					m_random.Float(-1f, 1f));

				float colorVariation = m_random.Float(0.7f, 1.0f);
				Color baseColor = m_random.Bool() ? TeleportColorInner : TeleportColorOuter;
				particle.Color = new Color(
					(byte)(baseColor.R * colorVariation * lightIntensity),
					(byte)(baseColor.G * colorVariation * lightIntensity),
					(byte)(baseColor.B * colorVariation * lightIntensity),
					baseColor.A);

				particle.Size = new Vector2(0.3f * size * m_random.Float(0.8f, 1.2f));
				particle.TimeToLive = m_random.Float(particleDuration * 0.6f, particleDuration);

				Vector3 direction = new Vector3(
					m_random.Float(-1f, 1f),
					m_random.Float(-1f, 1f),
					m_random.Float(-1f, 1f));
				if (direction.LengthSquared() > 0.001f)
					direction = Vector3.Normalize(direction);
				else
					direction = Vector3.UnitY;

				if (isAppearEffect)
				{
					particle.Velocity = -1.5f * size * direction;
					particle.Position = position + 1.5f * size * direction;
				}
				else
				{
					particle.Velocity = 1.5f * size * direction;
				}

				particle.FlipX = m_random.Bool();
				particle.FlipY = m_random.Bool();
			}
		}

		public override bool Simulate(float dt)
		{
			dt = Math.Clamp(dt, 0f, 0.1f);
			float s = MathF.Pow(0.1f, dt);
			bool flag = false;

			for (int i = 0; i < base.Particles.Length; i++)
			{
				TeleportParticleSystem.Particle particle = base.Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.TimeToLive -= dt;
					if (particle.TimeToLive > 0f)
					{
						particle.Position += particle.Velocity * dt;
						particle.Velocity *= s;
						particle.TextureSlot = (int)(3.99f * MathUtils.Saturate(2f - particle.TimeToLive));
					}
					else
					{
						particle.IsActive = false;
					}
				}
			}
			return !flag;
		}

		private Random m_random = new Random();

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float TimeToLive;
		}
	}
}
