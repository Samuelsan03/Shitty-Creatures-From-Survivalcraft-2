using System;
using Engine;
using Engine.Graphics;
using Game;

namespace WonderfulEra
{
	// Token: 0x020000DC RID: 220
	public class FlameSmokeParticleSystem : ParticleSystem<FlameSmokeParticleSystem.Particle>
	{
		// Token: 0x060006CA RID: 1738 RVA: 0x00029074 File Offset: 0x00027274
		public FlameSmokeParticleSystem(SubsystemTerrain terrain, Vector3 position, Vector3 direction) : base(50)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
			base.TextureSlotsCount = 3;
			this.m_position = position;
			this.m_direction = Vector3.Normalize(direction);
			this.m_color = new Color(255, 215, 0);
		}

		// Token: 0x060006CB RID: 1739 RVA: 0x000290D4 File Offset: 0x000272D4
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

		// Token: 0x0400043C RID: 1084
		public Game.Random m_random = new Game.Random();

		// Token: 0x0400043D RID: 1085
		public float m_time;

		// Token: 0x0400043E RID: 1086
		public float m_toGenerate;

		// Token: 0x0400043F RID: 1087
		public Vector3 m_position;

		// Token: 0x04000440 RID: 1088
		public Vector3 m_direction;

		// Token: 0x04000441 RID: 1089
		public Color m_color;

		// Token: 0x0200018E RID: 398
		public class Particle : Game.Particle
		{
			// Token: 0x04000760 RID: 1888
			public Vector3 Velocity;

			// Token: 0x04000761 RID: 1889
			public float Time;

			// Token: 0x04000762 RID: 1890
			public float Duration;
		}
	}
}
