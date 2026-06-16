using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class TeleportParticleSystem : ParticleSystem<TeleportParticleSystem.Particle>
	{
		// Color fijo de teletransporte
		private static readonly Color TeleportColor = new Color(180, 60, 255, 255);

		public TeleportParticleSystem(SubsystemTerrain terrain, Vector3 position, float size, bool isAppearEffect) : base(60)
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
			Color color = TeleportColor;
			float s = LightingManager.LightIntensityByLightValue[num4];
			color *= s;
			color.A = byte.MaxValue;
			for (int i = 0; i < base.Particles.Length; i++)
			{
				TeleportParticleSystem.Particle particle = base.Particles[i];
				particle.IsActive = true;
				particle.Position = position + 0.6f * size * new Vector3(this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f));
				particle.Color = color;
				particle.Size = new Vector2(0.3f * size * this.m_random.Float(0.8f, 1.2f));
				particle.TimeToLive = this.m_random.Float(0.5f, 3.5f);
				if (isAppearEffect)
				{
					particle.Velocity = -1.2f * size * new Vector3(this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f));
				}
				else
				{
					particle.Velocity = 1.2f * size * new Vector3(this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f));
				}
				particle.FlipX = this.m_random.Bool();
				particle.FlipY = this.m_random.Bool();
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
						TeleportParticleSystem.Particle particle2 = particle;
						particle2.Velocity.Y = particle2.Velocity.Y + 1f * dt;
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

		public Random m_random = new Random();

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float TimeToLive;
		}
	}
}
