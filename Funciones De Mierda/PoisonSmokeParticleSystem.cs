using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	// Token: 0x020000B3 RID: 179
	public class PoisonSmokeParticleSystem : ParticleSystem<PoisonSmokeParticleSystem.Particle>
	{
		// Token: 0x06000572 RID: 1394 RVA: 0x00022E68 File Offset: 0x00021068
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

			// CAMBIO AQUÍ: En lugar de usar solo la intensidad de luz (gris),
			// multiplicamos por el color verde deseado (51, 255, 51)
			// Dividimos por 255 para convertir de 0-255 a 0-1
			float greenR = 51f / 255f;
			float greenG = 255f / 255f;
			float greenB = 51f / 255f;

			this.m_color = new Color(
				num5 * greenR,
				num5 * greenG,
				num5 * greenB
			);
		}

		// Token: 0x06000573 RID: 1395 RVA: 0x00022F7C File Offset: 0x0002117C
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

		// Token: 0x04000322 RID: 802
		public Game.Random m_random = new Game.Random();

		// Token: 0x04000323 RID: 803
		public float m_time;

		// Token: 0x04000324 RID: 804
		public float m_toGenerate;

		// Token: 0x04000325 RID: 805
		public Vector3 m_position;

		// Token: 0x04000326 RID: 806
		public Vector3 m_direction;

		// Token: 0x04000327 RID: 807
		public Color m_color;

		// Token: 0x0200013F RID: 319
		public class Particle : Game.Particle
		{
			// Token: 0x04000544 RID: 1348
			public Vector3 Velocity;

			// Token: 0x04000545 RID: 1349
			public float Time;

			// Token: 0x04000546 RID: 1350
			public float Duration;
		}
	}
}