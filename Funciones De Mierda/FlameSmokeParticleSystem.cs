using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class FlameSmokeParticleSystem : ParticleSystem<FlameSmokeParticleSystem.Particle>
	{
		public FlameSmokeParticleSystem(SubsystemTerrain terrain, Vector3 position, Vector3 direction) : base(50)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/Fire Flamethrower remastered");
			base.TextureSlotsCount = 3;
			this.m_position = position;
			this.m_direction = Vector3.Normalize(direction);

			// Cálculo de iluminación rápido
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

			// Colores más intensos para bordes duros
			float intensity = MathUtils.Max(num5 * 1.3f, 0.6f);

			// Color base más intenso para fuego
			this.m_color = new Color(
				intensity,        // R - muy rojo
				intensity * 0.6f, // G - menos verde
				intensity * 0.2f  // B - muy poco azul
			);

			// Color de humo más oscuro
			this.m_smokeColor = new Color(
				intensity * 0.4f,  // R
				intensity * 0.4f,  // G
				intensity * 0.4f,  // B
				intensity * 0.8f   // A
			);
		}

		public override bool Simulate(float dt)
		{
			this.m_time += dt;

			// Parámetros originales pero ajustados
			float num = MathUtils.Lerp(150f, 20f, MathUtils.Saturate(2f * this.m_time / 0.5f));
			float s = MathF.Pow(0.015f, dt); // Menos damping para partículas más largas
			float s2 = MathUtils.Lerp(100f, 50f, MathUtils.Saturate(2f * this.m_time / 0.5f));

			// Fuerza vertical más fuerte para levantar el humo
			Vector3 v = new Vector3(0f, 4.5f, 0f);

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
						// Movimiento con leve aleatoriedad
						float turbulence = this.m_random.Float(-0.5f, 0.5f) * dt;
						Vector3 turbulenceVec = new Vector3(turbulence, 0f, turbulence);

						particle.Position += particle.Velocity * dt + turbulenceVec;
						particle.Velocity *= s;
						particle.Velocity += v * dt;

						// Animación de textura más rápida al inicio
						float lifeProgress = particle.Time / particle.Duration;
						float textureProgress;

						if (lifeProgress < 0.3f)
						{
							// Fase rápida de fuego (primeros frames)
							textureProgress = 12f * lifeProgress;
						}
						else
						{
							// Fase lenta de humo
							textureProgress = 3f + 6f * (lifeProgress - 0.3f) / 0.7f;
						}

						particle.TextureSlot = (int)MathUtils.Min(textureProgress, 8f);

						// Color con transición definida
						if (lifeProgress < 0.4f)
						{
							// Fuego intenso
							float fireBlend = lifeProgress / 0.4f;
							particle.Color = Color.Lerp(this.m_color, this.m_color * 0.9f, fireBlend);
							particle.Color.A = 255; // Alpha completo para bordes duros
						}
						else if (lifeProgress < 0.7f)
						{
							// Transición a humo
							float smokeBlend = (lifeProgress - 0.4f) / 0.3f;
							particle.Color = Color.Lerp(this.m_color * 0.9f, this.m_smokeColor, smokeBlend);
						}
						else
						{
							// Humo con fade-out
							particle.Color = this.m_smokeColor;
							float fade = (lifeProgress - 0.7f) / 0.3f;
							particle.Color.A = (byte)(255 * (1f - fade));
						}

						// Tamaño que crece con el tiempo
						float sizeGrowth = 0.5f * (1f + lifeProgress * 1.8f);
						particle.Size = new Vector2(sizeGrowth);
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (this.m_toGenerate >= 1f)
				{
					// Crear nueva partícula - simplificado como original
					particle.IsActive = true;
					Vector3 v2 = this.m_random.Vector3(0f, 0.8f); // Mayor dispersión

					particle.Position = this.m_position + 0.4f * v2;
					particle.Color = this.m_color;

					// Velocidad con más variación vertical
					float verticalBoost = this.m_random.Float(0f, 3f);
					Vector3 velocityDir = this.m_direction + this.m_random.Vector3(0f, 0.12f);
					velocityDir = Vector3.Normalize(velocityDir);
					velocityDir.Y += verticalBoost * 0.1f;

					particle.Velocity = s2 * velocityDir + 2.8f * v2;
					particle.Size = Vector2.Zero; // Comienza pequeño
					particle.Time = 0f;
					particle.Duration = this.m_random.Float(0.8f, 2.2f);
					particle.FlipX = this.m_random.Bool();
					particle.FlipY = this.m_random.Bool();

					this.m_toGenerate -= 1f;
				}
			}

			this.m_toGenerate = MathUtils.Remainder(this.m_toGenerate, 1f);

			return !flag && this.m_time >= 0.5f;
		}

		// Campos privados
		public Game.Random m_random = new Game.Random();
		public float m_time;
		public float m_toGenerate;
		public Vector3 m_position;
		public Vector3 m_direction;
		public Color m_color;
		public Color m_smokeColor;

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float Time;
			public float Duration;
		}
	}
}
