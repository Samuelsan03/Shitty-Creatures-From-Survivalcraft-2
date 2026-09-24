using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class ChatterParticleSystem : ParticleSystem<ChatterParticleSystem.Particle>
	{
		public ChatterParticleSystem(Vector3 position, Color color, string text, float duration)
			: base(1)
		{
			ChatterParticleSystem.Particle particle = base.Particles[0];
			particle.IsActive = true;
			particle.Position = position;
			particle.TimeToLive = duration;
			particle.Velocity = new Vector3(0f, 0.6f, 0f);
			particle.BaseColor = color;
			particle.Text = text;
		}

		public override bool Simulate(float dt)
		{
			dt = Math.Clamp(dt, 0f, 0.1f);
			bool flag = false;
			for (int i = 0; i < base.Particles.Length; i++)
			{
				ChatterParticleSystem.Particle particle = base.Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.TimeToLive -= dt;
					if (particle.TimeToLive > 0f)
					{
						particle.Position += particle.Velocity * dt;
						float fade = MathUtils.Saturate(particle.TimeToLive / 0.5f);
						particle.Color = particle.BaseColor * fade;
					}
					else
					{
						particle.IsActive = false;
					}
				}
			}
			return !flag;
		}

		public override void Draw(Camera camera)
		{
			if (this.m_batch == null)
			{
				this.m_batch = this.SubsystemParticles.PrimitivesRenderer.FontBatch(LabelWidget.BitmapFont, 0, DepthStencilState.None, null, null, null);
			}
			Vector3 viewDirection = camera.ViewDirection;
			Vector3 vector = Vector3.Normalize(Vector3.Cross(viewDirection, Vector3.UnitY));
			Vector3 v = -Vector3.Normalize(Vector3.Cross(vector, viewDirection));
			for (int i = 0; i < base.Particles.Length; i++)
			{
				ChatterParticleSystem.Particle particle = base.Particles[i];
				if (particle.IsActive)
				{
					float num = Vector3.Distance(camera.ViewPosition, particle.Position);
					float num2 = MathUtils.Saturate(3f * (num - 0.2f));
					float num3 = MathUtils.Saturate(0.2f * (30f - num));
					float num4 = num2 * num3;
					if (num4 > 0f)
					{
						float s = 0.006f * MathF.Sqrt(num);
						Color color = particle.Color * num4;
						this.m_batch.QueueText(particle.Text, particle.Position, vector * s, v * s, color, TextAnchor.Center, Vector2.Zero);
					}
				}
			}
		}

		public FontBatch3D m_batch;

		public class Particle : Game.Particle
		{
			public float TimeToLive;
			public Vector3 Velocity;
			public Color BaseColor;
			public Color Color;
			public string Text;
		}
	}
}
