using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x02000276 RID: 630
	public class NewShapeshiftParticleSystem : ParticleSystem<ShapeshiftParticleSystem.Particle>
	{
		// Token: 0x170002D3 RID: 723
		// (get) Token: 0x06001272 RID: 4722 RVA: 0x0007F538 File Offset: 0x0007D738
		// (set) Token: 0x06001273 RID: 4723 RVA: 0x0007F540 File Offset: 0x0007D740
		public bool Stopped { get; set; }

		// Token: 0x170002D4 RID: 724
		// (get) Token: 0x06001274 RID: 4724 RVA: 0x0007F549 File Offset: 0x0007D749
		// (set) Token: 0x06001275 RID: 4725 RVA: 0x0007F551 File Offset: 0x0007D751
		public Vector3 Position { get; set; }

		// Token: 0x170002D5 RID: 725
		// (get) Token: 0x06001276 RID: 4726 RVA: 0x0007F55A File Offset: 0x0007D75A
		// (set) Token: 0x06001277 RID: 4727 RVA: 0x0007F562 File Offset: 0x0007D762
		public BoundingBox BoundingBox { get; set; }

		// Token: 0x06001278 RID: 4728 RVA: 0x0007F56B File Offset: 0x0007D76B
		public NewShapeshiftParticleSystem() : base(40)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/NewShapeshiftParticle");
			base.TextureSlotsCount = 3;
		}

		// Token: 0x06001279 RID: 4729 RVA: 0x0007F598 File Offset: 0x0007D798
		public override bool Simulate(float dt)
		{
			bool flag = false;
			this.m_generationSpeed = MathUtils.Min(this.m_generationSpeed + 15f * dt, 35f);
			this.m_toGenerate += this.m_generationSpeed * dt;
			for (int i = 0; i < base.Particles.Length; i++)
			{
				ShapeshiftParticleSystem.Particle particle = base.Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.Time += dt;
					if (particle.Time <= particle.Duration)
					{
						particle.Position += particle.Velocity * dt;
						particle.FlipX = this.m_random.Bool();
						particle.FlipY = this.m_random.Bool();
						particle.TextureSlot = (int)MathUtils.Min(9.900001f * particle.Time / particle.Duration, 8f);
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (!this.Stopped)
				{
					while (this.m_toGenerate >= 1f)
					{
						particle.IsActive = true;
						particle.Position.X = this.m_random.Float(this.BoundingBox.Min.X, this.BoundingBox.Max.X);
						particle.Position.Y = this.m_random.Float(this.BoundingBox.Min.Y, this.BoundingBox.Max.Y);
						particle.Position.Z = this.m_random.Float(this.BoundingBox.Min.Z, this.BoundingBox.Max.Z);
						particle.Velocity = new Vector3(0f, this.m_random.Float(0.5f, 1.5f), 0f);
						particle.Color = Color.White;
						particle.Size = new Vector2(0.4f);
						particle.Time = 0f;
						particle.Duration = this.m_random.Float(0.75f, 1.5f);
						this.m_toGenerate -= 1f;
					}
				}
				else
				{
					this.m_toGenerate = 0f;
				}
			}
			this.m_toGenerate = MathUtils.Remainder(this.m_toGenerate, 1f);
			return this.Stopped && !flag;
		}

		// Token: 0x04000CFE RID: 3326
		public Random m_random = new Random();

		// Token: 0x04000CFF RID: 3327
		public float m_generationSpeed;

		// Token: 0x04000D00 RID: 3328
		public float m_toGenerate;

		// Token: 0x0200055C RID: 1372
		public class Particle : Game.Particle
		{
			// Token: 0x04001CA4 RID: 7332
			public float Time;

			// Token: 0x04001CA5 RID: 7333
			public float Duration;

			// Token: 0x04001CA6 RID: 7334
			public Vector3 Velocity;
		}
	}
}
