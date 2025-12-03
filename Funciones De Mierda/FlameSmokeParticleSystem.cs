using System;
using Engine;
using Engine.Graphics;
using Game;

namespace WonderfulEra
{
	// Token: 0x020000AB RID: 171
	public class FlameSmokeParticleSystem : ParticleSystem<FlameSmokeParticleSystem.Particle>
	{
		// Token: 0x06000561 RID: 1377 RVA: 0x00021868 File Offset: 0x0001FA68
		public FlameSmokeParticleSystem(SubsystemTerrain terrain, Vector3 position, Vector3 direction) : base(50)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
			base.TextureSlotsCount = 3;
			this.m_position = position;
			this.m_direction = Vector3.Normalize(direction);
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
			float num5 = LightingManager.LightIntensityByLightValue[num4];
			this.m_color = new Color(num5, num5, num5);
		}

		// Token: 0x06000562 RID: 1378 RVA: 0x0002197C File Offset: 0x0001FB7C
		public override bool Simulate(float dt)
		{
			this.m_time += dt;
			float num = MathUtils.Lerp(150f, 20f, MathUtils.Saturate(2f * this.m_time / 0.5f));
			float s = MathF.Pow(0.01f, dt);
			float s2 = MathUtils.Lerp(100f, 50f, MathUtils.Saturate(2f * this.m_time / 0.5f));
			Vector3 v = new Vector3(4f, 4f, 3f);
			if (this.m_time < 0.5f)
			{
				this.m_toGenerate += num * dt;
			}
			else
			{
				this.m_toGenerate = 0f;
			}
			bool flag = false;
			for (int i = 0; i < base.Particles.Length; i++)
			{
				FlameSmokeParticleSystem.Particle particle = base.Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.Time += dt;
					if (particle.Time <= particle.Duration)
					{
						particle.Position += particle.Velocity * dt;
						particle.Velocity *= s;
						particle.Velocity += v * dt;
						particle.TextureSlot = (int)MathUtils.Min(9f * particle.Time / particle.Duration, 8f);
						particle.Size = new Vector2(0.5f);
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (this.m_toGenerate >= 1f)
				{
					particle.IsActive = true;
					Vector3 v2 = this.m_random.Vector3(0f, 1f);
					particle.Position = this.m_position + 0.3f * v2;
					particle.Color = this.m_color;
					particle.Velocity = s2 * (this.m_direction + this.m_random.Vector3(0f, 0.1f)) + 2.5f * v2;
					particle.Size = Vector2.Zero;
					particle.Time = 0f;
					particle.Duration = this.m_random.Float(0.5f, 2f);
					particle.FlipX = this.m_random.Bool();
					particle.FlipY = this.m_random.Bool();
					this.m_toGenerate -= 1f;
				}
			}
			this.m_toGenerate = MathUtils.Remainder(this.m_toGenerate, 1f);
			return !flag && this.m_time >= 0.5f;
		}

		// Token: 0x04000304 RID: 772
		public Game.Random m_random = new Game.Random();

		// Token: 0x04000305 RID: 773
		public float m_time;

		// Token: 0x04000306 RID: 774
		public float m_toGenerate;

		// Token: 0x04000307 RID: 775
		public Vector3 m_position;

		// Token: 0x04000308 RID: 776
		public Vector3 m_direction;

		// Token: 0x04000309 RID: 777
		public Color m_color;

		// Token: 0x0200013A RID: 314
		public class Particle : Game.Particle
		{
			// Token: 0x04000530 RID: 1328
			public Vector3 Velocity;

			// Token: 0x04000531 RID: 1329
			public float Time;

			// Token: 0x04000532 RID: 1330
			public float Duration;
		}
	}
}
