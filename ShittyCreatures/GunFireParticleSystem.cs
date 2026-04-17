using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x02000086 RID: 134
	public class GunFireParticleSystem : ParticleSystem<GunFireParticleSystem.Particle>
	{
		// Token: 0x170000B6 RID: 182
		// (get) Token: 0x0600065C RID: 1628 RVA: 0x0004EA1F File Offset: 0x0004CC1F
		// (set) Token: 0x0600065D RID: 1629 RVA: 0x0004EA27 File Offset: 0x0004CC27
		public bool IsStopped { get; set; }

		// Token: 0x0600065E RID: 1630 RVA: 0x0004EA30 File Offset: 0x0004CC30
		public GunFireParticleSystem(Vector3 position, Vector3 direction, float maxVisibilityDistance) : base(25)
		{
			this.m_position = position;
			this.m_direction = direction;
			this.m_maxVisibilityDistance = maxVisibilityDistance;
			this.m_isOneShot = true;
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/NewFireParticle");
			base.TextureSlotsCount = 3;
		}

		// Token: 0x0600065F RID: 1631 RVA: 0x0004EA88 File Offset: 0x0004CC88
		public GunFireParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, Vector3 direction) : base(25)
		{
			this.subsystemTerrain = subsystemTerrain;
			this.m_position = position;
			this.m_direction = direction;
			this.m_isOneShot = true;
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/NewFireParticle");
			base.TextureSlotsCount = 3;
			this.m_maxVisibilityDistance = 50f;
		}

		// Token: 0x06000660 RID: 1632 RVA: 0x0004EAEC File Offset: 0x0004CCEC
		public override bool Simulate(float dt)
		{
			this.m_age += dt;
			bool flag = false;
			bool flag2 = this.m_visible || this.m_age < 3f;
			bool flag3 = flag2;
			if (flag3)
			{
				bool flag4 = this.m_age < 0.3f;
				bool flag5 = flag4;
				if (flag5)
				{
					this.m_toGenerate += 12f * dt;
				}
				for (int i = 0; i < base.Particles.Length; i++)
				{
					GunFireParticleSystem.Particle particle = base.Particles[i];
					bool isActive = particle.IsActive;
					bool flag6 = isActive;
					if (flag6)
					{
						flag = true;
						particle.Time += dt;
						particle.TimeToLive -= dt;
						bool flag7 = particle.TimeToLive > 0f;
						bool flag8 = flag7;
						if (flag8)
						{
							particle.Position += particle.Velocity * dt;
							particle.Velocity *= 0.92f;
							GunFireParticleSystem.Particle particle2 = particle;
							particle2.Velocity.Y = particle2.Velocity.Y + 0.5f * dt;
							particle.TextureSlot = (int)MathUtils.Min(9f * particle.Time / 1.2f, 8f);
							float num = particle.Time / particle.TimeToLive;
							float num2 = MathUtils.Max(1f - num * num, 0f);
							particle.Size = new Vector2(0.08f * num2);
							particle.Color = Color.White * num2;
						}
						else
						{
							particle.IsActive = false;
						}
					}
					else
					{
						bool flag9 = this.m_toGenerate >= 1f && this.m_age < 0.4f;
						bool flag10 = flag9;
						if (flag10)
						{
							particle.IsActive = true;
							particle.Position = this.m_position;
							particle.Color = Color.White;
							particle.Size = new Vector2(0.08f);
							Vector3 v = this.m_direction * this.m_random.Float(2f, 4f);
							Vector3 v2 = new Vector3(this.m_random.Float(-0.5f, 0.5f), this.m_random.Float(-0.2f, 0.6f), this.m_random.Float(-0.5f, 0.5f));
							particle.Velocity = v + v2;
							particle.Time = 0f;
							particle.TimeToLive = this.m_random.Float(0.8f, 1.5f);
							particle.FlipX = this.m_random.Bool();
							particle.FlipY = this.m_random.Bool();
							this.m_toGenerate -= 1f;
						}
					}
				}
				this.m_toGenerate = MathUtils.Remainder(this.m_toGenerate, 1f);
			}
			this.m_visible = false;
			bool flag11 = this.m_age > 2.5f && !flag;
			bool flag12 = flag11;
			bool result;
			if (flag12)
			{
				result = true;
			}
			else
			{
				bool flag13 = this.m_age > 5f;
				result = flag13;
			}
			return result;
		}

		// Token: 0x06000661 RID: 1633 RVA: 0x0004EE50 File Offset: 0x0004D050
		public override void Draw(Camera camera)
		{
			float num = Vector3.Dot(this.m_position - camera.ViewPosition, camera.ViewDirection);
			bool flag = num > -0.5f && num <= this.m_maxVisibilityDistance && Vector3.DistanceSquared(this.m_position, camera.ViewPosition) <= this.m_maxVisibilityDistance * this.m_maxVisibilityDistance;
			bool flag2 = flag;
			if (flag2)
			{
				this.m_visible = true;
				base.Draw(camera);
			}
		}

		// Token: 0x040005F5 RID: 1525
		public Random m_random = new Random();

		// Token: 0x040005F6 RID: 1526
		public Vector3 m_position;

		// Token: 0x040005F7 RID: 1527
		public Vector3 m_direction;

		// Token: 0x040005F8 RID: 1528
		public float m_toGenerate;

		// Token: 0x040005F9 RID: 1529
		public bool m_visible;

		// Token: 0x040005FA RID: 1530
		public float m_maxVisibilityDistance;

		// Token: 0x040005FB RID: 1531
		public float m_age;

		// Token: 0x040005FC RID: 1532
		public bool m_isOneShot;

		// Token: 0x040005FD RID: 1533
		private SubsystemTerrain subsystemTerrain;

		// Token: 0x0200017A RID: 378
		public class Particle : Game.Particle
		{
			// Token: 0x04000A64 RID: 2660
			public float Time;

			// Token: 0x04000A65 RID: 2661
			public float TimeToLive;

			// Token: 0x04000A66 RID: 2662
			public Vector3 Velocity;
		}
	}
}
