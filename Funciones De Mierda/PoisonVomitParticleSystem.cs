using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;

namespace Game
{
	public class PoisonVomitParticleSystem : ParticleSystem<PoisonVomitParticleSystem.Particle>
	{
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;
		private SubsystemTime m_subsystemTime;
		private SubsystemParticles m_subsystemParticles;
		private ComponentCreature m_componentCreature;

		public Vector3 Position { get; set; }
		public Vector3 Direction { get; set; }
		public float PoisonIntensity { get; set; } = 180f;
		public bool IsStopped { get; set; }

		private Random m_random = new Random();
		private float m_duration;
		private float m_toGenerate;
		private double m_lastPoisonApplyTime;

		public PoisonVomitParticleSystem(SubsystemTerrain terrain, SubsystemBodies bodies, SubsystemSoundMaterials soundMaterials,
			SubsystemTime time, SubsystemParticles particles, ComponentCreature creature)
			: base(200) // Capacidad suficiente para un chorro denso
		{
			m_subsystemTerrain = terrain;
			m_subsystemBodies = bodies;
			m_subsystemSoundMaterials = soundMaterials;
			m_subsystemTime = time;
			m_subsystemParticles = particles;
			m_componentCreature = creature;

			Texture = ContentManager.Get<Texture2D>("Textures/Gui/vomito venenoso mejorado");
			TextureSlotsCount = 3;
		}

		public override bool Simulate(float dt)
		{
			// Cálculo de luz ambiente (igual que PukeParticleSystem)
			int x = Terrain.ToCell(Position.X);
			int y = Terrain.ToCell(Position.Y);
			int z = Terrain.ToCell(Position.Z);
			int light = 0;
			light = MathUtils.Max(light, m_subsystemTerrain.Terrain.GetCellLight(x + 1, y, z));
			light = MathUtils.Max(light, m_subsystemTerrain.Terrain.GetCellLight(x - 1, y, z));
			light = MathUtils.Max(light, m_subsystemTerrain.Terrain.GetCellLight(x, y + 1, z));
			light = MathUtils.Max(light, m_subsystemTerrain.Terrain.GetCellLight(x, y - 1, z));
			light = MathUtils.Max(light, m_subsystemTerrain.Terrain.GetCellLight(x, y, z + 1));
			light = MathUtils.Max(light, m_subsystemTerrain.Terrain.GetCellLight(x, y, z - 1));
			Color baseColor = Color.White;
			float intensity = LightingManager.LightIntensityByLightValue[light];
			baseColor *= intensity;
			baseColor.A = 255;

			dt = Math.Clamp(dt, 0f, 0.1f);
			m_duration += dt;

			// Auto-stop después de 3.5 segundos (como el Puke original)
			if (m_duration > 3.5f)
			{
				IsStopped = true;
			}

			// Generación de partículas basada en ruido (similar al Puke)
			float noise = MathUtils.Saturate(1.3f * SimplexNoise.Noise(3f * m_duration + (float)(GetHashCode() % 100)) - 0.3f);
			float generationRate = 60f * noise; // Tasa alta para chorro denso
			m_toGenerate += generationRate * dt;

			bool anyActive = false;
			Vector3 normalizedDir = Direction.LengthSquared() > 0f ? Vector3.Normalize(Direction) : Vector3.UnitZ;

			for (int i = 0; i < Particles.Length; i++)
			{
				Particle particle = Particles[i];
				if (particle.IsActive)
				{
					anyActive = true;
					particle.TimeToLive -= dt;
					if (particle.TimeToLive > 0f)
					{
						Vector3 oldPos = particle.Position;
						Vector3 newPos = oldPos + particle.Velocity * dt;

						// Colisión con cuerpos (excepto el dueño)
						BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(oldPos, newPos, 0.1f, (body, d) =>
						{
							if (body.Entity == m_componentCreature.Entity) return false;
							return true;
						});

						if (bodyHit != null)
						{
							ComponentBody hitBody = bodyHit.Value.ComponentBody;
							if (hitBody != null)
							{
								ApplyPoisonToBody(hitBody);
							}
							// Sonido de impacto
							m_subsystemSoundMaterials.PlayImpactSound(bodyHit.Value.ComponentBody.StandingOnValue ?? 0, bodyHit.Value.HitPoint(), 0.5f);
							particle.IsActive = false;
							continue;
						}

						// Colisión con terreno
						TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(oldPos, newPos, false, true,
							(value, d) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable_(value));

						if (terrainHit != null)
						{
							m_subsystemSoundMaterials.PlayImpactSound(terrainHit.Value.Value, terrainHit.Value.HitPoint(), 0.5f);
							particle.IsActive = false;
							continue;
						}

						particle.Position = newPos;
						// Sin gravedad, el chorro es recto
						particle.Color = baseColor * MathUtils.Saturate(particle.TimeToLive);
						particle.TextureSlot = (int)(8.99f * MathUtils.Saturate(2f - particle.TimeToLive));
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (!IsStopped && m_toGenerate >= 1f)
				{
					// Crear nueva partícula con mínima dispersión para que el chorro sea recto
					Vector3 offset = m_random.Vector3(0.04f); // dispersión muy pequeña
					particle.IsActive = true;
					particle.Position = Position + offset;
					particle.Color = baseColor;
					// Velocidad en la dirección principal, con muy poca variación
					Vector3 dirOffset = m_random.Vector3(0.02f);
					particle.Velocity = normalizedDir + dirOffset;
					particle.Velocity = Vector3.Normalize(particle.Velocity) * MathUtils.Lerp(8f, 12f, noise);
					particle.TimeToLive = m_random.Float(1.5f, 2.2f);
					particle.Size = new Vector2(0.55f);
					particle.FlipX = m_random.Bool();
					particle.FlipY = m_random.Bool();
					m_toGenerate -= 1f;
				}
			}

			return IsStopped && !anyActive;
		}

		private void ApplyPoisonToBody(ComponentBody body)
		{
			// Evitar múltiples aplicaciones en el mismo frame
			if (m_subsystemTime.GameTime - m_lastPoisonApplyTime < 0.2)
				return;

			Entity entity = body.Entity;
			if (entity == null) return;

			// Jugador
			ComponentPlayer player = entity.FindComponent<ComponentPlayer>();
			if (player != null)
			{
				ComponentSickness sickness = player.ComponentSickness;
				if (sickness != null && !sickness.IsSick)
				{
					sickness.StartSickness();
					sickness.m_sicknessDuration = PoisonIntensity;
				}
				m_lastPoisonApplyTime = m_subsystemTime.GameTime;
				return;
			}

			// Criatura
			ComponentCreature creature = entity.FindComponent<ComponentCreature>();
			if (creature != null && creature != m_componentCreature)
			{
				ComponentPoisonInfected poison = entity.FindComponent<ComponentPoisonInfected>();
				if (poison != null && !poison.IsInfected)
				{
					poison.StartInfect(PoisonIntensity);
				}
				m_lastPoisonApplyTime = m_subsystemTime.GameTime;
			}
		}

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float TimeToLive;
		}
	}
}
