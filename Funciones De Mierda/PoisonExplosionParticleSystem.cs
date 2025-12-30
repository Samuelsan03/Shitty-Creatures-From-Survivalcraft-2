using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x02000203 RID: 515
	public class PoisonExplosionParticleSystem : ParticleSystem<PoisonExplosionParticleSystem.Particle>
	{
		// Token: 0x06000FE5 RID: 4069 RVA: 0x0006A500 File Offset: 0x00068700
		public PoisonExplosionParticleSystem() : base(2000)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/Puke Particle Remake");
			base.TextureSlotsCount = 3;
			this.m_inactiveParticles.AddRange(base.Particles);
		}

		// Token: 0x06000FE6 RID: 4070 RVA: 0x0006A564 File Offset: 0x00068764
		public void SetExplosionCell(Point3 point, float strength)
		{
			PoisonExplosionParticleSystem.Particle particle;
			if (!this.m_particlesByPoint.TryGetValue(point, out particle))
			{
				if (this.m_inactiveParticles.Count > 0)
				{
					List<PoisonExplosionParticleSystem.Particle> inactiveParticles = this.m_inactiveParticles;
					particle = inactiveParticles[inactiveParticles.Count - 1];
					this.m_inactiveParticles.RemoveAt(this.m_inactiveParticles.Count - 1);
				}
				else
				{
					for (int i = 0; i < 5; i++)
					{
						int num = this.m_random.Int(0, base.Particles.Length - 1);
						if (strength > base.Particles[num].Strength)
						{
							particle = base.Particles[num];
						}
					}
				}
				if (particle != null)
				{
					this.m_particlesByPoint.Add(point, particle);
				}
			}
			if (particle != null)
			{
				particle.IsActive = true;
				particle.Position = new Vector3((float)point.X, (float)point.Y, (float)point.Z) + new Vector3(0.5f) + 0.2f * new Vector3(this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f));
				particle.Size = new Vector2(this.m_random.Float(0.6f, 0.9f));
				particle.Strength = strength;
				particle.Color = new Color(51, 255, 51, 255); // RGB: 51,255,51
				this.m_isEmpty = false;
			}
		}

		// Token: 0x06000FE7 RID: 4071 RVA: 0x0006A6D4 File Offset: 0x000688D4
		public override bool Simulate(float dt)
		{
			if (!this.m_isEmpty)
			{
				this.m_isEmpty = true;
				for (int i = 0; i < base.Particles.Length; i++)
				{
					PoisonExplosionParticleSystem.Particle particle = base.Particles[i];
					if (particle.IsActive)
					{
						this.m_isEmpty = false;
						particle.Strength -= dt / 3.5f; // Duración más larga para veneno
						if (particle.Strength > 0f)
						{
							particle.TextureSlot = (int)MathUtils.Min(9f * (1f - particle.Strength) * 0.6f, 8f);
							PoisonExplosionParticleSystem.Particle particle2 = particle;
							particle2.Position.Y = particle2.Position.Y + 1.5f * MathUtils.Max(1f - particle.Strength - 0.25f, 0f) * dt;

							// Movimiento más lento y flotante para partículas de veneno
							particle.Position.X += this.m_random.Float(-0.01f, 0.01f);
							particle.Position.Z += this.m_random.Float(-0.01f, 0.01f);
						}
						else
						{
							particle.IsActive = false;
							this.m_inactiveParticles.Add(particle);
						}
					}
				}
			}
			return false;
		}

		// Token: 0x06000FE8 RID: 4072 RVA: 0x0006A7BA File Offset: 0x000689BA
		public override void Draw(Camera camera)
		{
			if (!this.m_isEmpty)
			{
				// Aplicar mezcla especial para efecto de veneno
				BlendState blendState = BlendState.AlphaBlend;
				Display.BlendState = blendState;

				base.Draw(camera);
			}
		}

		// Token: 0x04000AB6 RID: 2742
		public Dictionary<Point3, PoisonExplosionParticleSystem.Particle> m_particlesByPoint = new Dictionary<Point3, PoisonExplosionParticleSystem.Particle>();

		// Token: 0x04000AB7 RID: 2743
		public List<PoisonExplosionParticleSystem.Particle> m_inactiveParticles = new List<PoisonExplosionParticleSystem.Particle>();

		// Token: 0x04000AB8 RID: 2744
		public Random m_random = new Random();

		// Token: 0x04000AB9 RID: 2745
		public const float m_duration = 3.5f; // Duración más larga

		// Token: 0x04000ABA RID: 2746
		public bool m_isEmpty;

		// Token: 0x02000520 RID: 1312
		public class Particle : Game.Particle
		{
			// Token: 0x04001BC0 RID: 7104
			public float Strength;
		}
	}
}
