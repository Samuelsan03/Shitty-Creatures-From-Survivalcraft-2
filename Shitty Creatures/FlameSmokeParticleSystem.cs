using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	// Token: 0x020000E5 RID: 229
	public class FlameSmokeParticleSystem : ParticleSystem<FlameSmokeParticleSystem.Particle>
	{
		// Token: 0x0600074A RID: 1866 RVA: 0x0002BF10 File Offset: 0x0002A110
		public FlameSmokeParticleSystem(SubsystemTerrain terrain, Vector3 position, Vector3 direction) : base(50)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
			base.TextureSlotsCount = 3;
			this.m_position = position;
			this.m_direction = Vector3.Normalize(direction);
			this.m_color = new Color(255, 179, 0);
		}

		// Token: 0x0600074B RID: 1867 RVA: 0x0002BF70 File Offset: 0x0002A170
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

		// Token: 0x04000482 RID: 1154
		public Game.Random m_random = new Game.Random();

		// Token: 0x04000483 RID: 1155
		public float m_time;

		// Token: 0x04000484 RID: 1156
		public float m_toGenerate;

		// Token: 0x04000485 RID: 1157
		public Vector3 m_position;

		// Token: 0x04000486 RID: 1158
		public Vector3 m_direction;

		// Token: 0x04000487 RID: 1159
		public Color m_color;

		// Token: 0x020001A1 RID: 417
		public class Particle : Game.Particle
		{
			// Token: 0x040007E2 RID: 2018
			public Vector3 Velocity;

			// Token: 0x040007E3 RID: 2019
			public float Time;

			// Token: 0x040007E4 RID: 2020
			public float Duration;
		}
	}
}
