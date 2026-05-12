using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	// Token: 0x020000F1 RID: 241
	public class PoisonSmokeParticleSystem : ParticleSystem<PoisonSmokeParticleSystem.Particle>
	{
		// Token: 0x06000768 RID: 1896 RVA: 0x0002D5A0 File Offset: 0x0002B7A0
		public PoisonSmokeParticleSystem(SubsystemTerrain terrain, Vector3 position, Vector3 direction) : base(50)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/Puke Particle Remake");
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
			this.m_color = new Color(0, 255, 34);
		}

		// Token: 0x06000769 RID: 1897 RVA: 0x0002D6B4 File Offset: 0x0002B8B4
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
				PoisonSmokeParticleSystem.Particle particle = base.Particles[i];
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

		// Token: 0x040004A6 RID: 1190
		public Game.Random m_random = new Game.Random();

		// Token: 0x040004A7 RID: 1191
		public float m_time;

		// Token: 0x040004A8 RID: 1192
		public float m_toGenerate;

		// Token: 0x040004A9 RID: 1193
		public Vector3 m_position;

		// Token: 0x040004AA RID: 1194
		public Vector3 m_direction;

		// Token: 0x040004AB RID: 1195
		public Color m_color;

		// Token: 0x020001A6 RID: 422
		public class Particle : Game.Particle
		{
			// Token: 0x040007F6 RID: 2038
			public Vector3 Velocity;

			// Token: 0x040007F7 RID: 2039
			public float Time;

			// Token: 0x040007F8 RID: 2040
			public float Duration;
		}
	}
}
