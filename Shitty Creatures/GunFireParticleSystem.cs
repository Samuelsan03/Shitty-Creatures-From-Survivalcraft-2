using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x02000100 RID: 256
	public class GunFireParticleSystem : ParticleSystem<GunSmokeParticleSystem.Particle>
	{
		// Token: 0x060006DF RID: 1759 RVA: 0x0003A434 File Offset: 0x00038634
		public GunFireParticleSystem(SubsystemTerrain terrain, Vector3 position, Vector3 direction, float scale) : base(50)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/NewFireParticle", null);
			base.TextureSlotsCount = 3;
			this.m_position = position;
			this.m_direction = Vector3.Normalize(direction);
			int num = Terrain.ToCell(position.X);
			int num2 = Terrain.ToCell(position.Y);
			int num3 = Terrain.ToCell(position.Z);
			int x = 0;
			x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num, num2 + 1, num3));
			float num4 = (float)MathUtils.Max(MathUtils.Max(MathUtils.Max(MathUtils.Max(MathUtils.Max(MathUtils.Max(0, terrain.Terrain.GetCellLight(num + 1, num2, num3)), terrain.Terrain.GetCellLight(num - 1, num2, num3)), terrain.Terrain.GetCellLight(num, num2 + 1, num3)), terrain.Terrain.GetCellLight(num, num2 - 1, num3)), terrain.Terrain.GetCellLight(num, num2, num3 + 1)), terrain.Terrain.GetCellLight(num, num2, num2 - 1));
			this.m_color = new Color(num4, num4, num4);
		}

		// Token: 0x060006E0 RID: 1760 RVA: 0x0003A550 File Offset: 0x00038750
		public GunFireParticleSystem(SubsystemTerrain terrain, Vector3 position, Vector3 direction) : base(50)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/NewFireParticle", null);
			base.TextureSlotsCount = 3;
			this.m_position = position;
			this.m_direction = Vector3.Normalize(direction);
			int num = Terrain.ToCell(position.X);
			int num2 = Terrain.ToCell(position.Y);
			int num3 = Terrain.ToCell(position.Z);
			int x = 0;
			x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num + 1, num2, num3));
			x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num - 1, num2, num3));
			x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num, num2 + 1, num3));
			x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num, num2 - 1, num3));
			x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num, num2, num3 + 1));
			x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num, num2, num3 - 1));
			this.m_scale = 0.1f;
		}

		// Token: 0x060006E1 RID: 1761 RVA: 0x0003A658 File Offset: 0x00038858
		public override bool Simulate(float dt)
		{
			this.m_time += dt;
			float num = MathUtils.Lerp(100f, 10f, MathUtils.Saturate(2f * this.m_time / 0.5f));
			float s = MathUtils.Pow(0.01f, dt);
			float s2 = MathUtils.Lerp(10f, 0f, MathUtils.Saturate(2f * this.m_time / 0.5f));
			Vector3 v = new Vector3(1f, 1f, 0.5f);
			if (this.m_time < 0.1f)
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
				GunSmokeParticleSystem.Particle particle = base.Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.Time += dt;
					if (particle.Time <= particle.Duration)
					{
						particle.Position += particle.Velocity * dt * 0.25f;
						particle.Velocity *= s;
						particle.Velocity += v * dt;
						particle.TextureSlot = (int)MathUtils.Min(20f * particle.Time / particle.Duration, 8f);
						particle.Size = new Vector2(this.m_scale);
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (this.m_toGenerate >= 1f)
				{
					particle.IsActive = true;
					Vector3 v2 = this.m_random.Vector3(0f, 0.5f);
					particle.Position = this.m_position + 0.3f * v2;
					particle.Color = Color.White;
					particle.Velocity = s2 * (this.m_direction + this.m_random.Vector3(0f, 0.1f)) + 2.5f * v2;
					particle.Size = Vector2.Zero;
					particle.Time = 0f;
					particle.Duration = this.m_random.Float(0.1f, 1f);
					particle.FlipX = this.m_random.Bool();
					particle.FlipY = this.m_random.Bool();
					this.m_toGenerate -= 1f;
				}
			}
			this.m_toGenerate = MathUtils.Remainder(this.m_toGenerate, 1f);
			return !flag && this.m_time >= 0.5f;
		}

		// Token: 0x04000446 RID: 1094
		public Game.Random m_random = new Game.Random();

		// Token: 0x04000447 RID: 1095
		public float m_time;

		// Token: 0x04000448 RID: 1096
		public float m_toGenerate;

		// Token: 0x04000449 RID: 1097
		public Vector3 m_position;

		// Token: 0x0400044A RID: 1098
		public Vector3 m_direction;

		// Token: 0x0400044B RID: 1099
		public float m_scale;

		// Token: 0x0400044C RID: 1100
		public Color m_color;
	}
}
