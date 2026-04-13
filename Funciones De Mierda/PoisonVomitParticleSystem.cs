using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class PoisonVomitParticleSystem : ParticleSystem<PoisonVomitParticleSystem.Particle>
	{
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;
		private SubsystemTime m_subsystemTime;
		private SubsystemParticles m_subsystemParticles;
		private ComponentCreature m_owner;
		private Random m_random = new Random();

		private ComponentNewChaseBehavior m_newChaseBehavior;
		private ComponentChaseBehavior m_oldChaseBehavior;
		private ComponentZombieChaseBehavior m_zombieChaseBehavior;

		public Vector3 Position { get; set; }
		public Vector3 Direction { get; set; }
		public bool IsStopped { get; set; }
		public float PoisonIntensity { get; set; } = 150f;

		private float m_duration;
		private float m_toGenerate;
		private double m_lastImpactSoundTime;

		public PoisonVomitParticleSystem(SubsystemTerrain terrain, SubsystemBodies bodies, SubsystemSoundMaterials soundMaterials, SubsystemTime time, SubsystemParticles particles, ComponentCreature owner)
			: base(60)
		{
			m_subsystemTerrain = terrain;
			m_subsystemBodies = bodies;
			m_subsystemSoundMaterials = soundMaterials;
			m_subsystemTime = time;
			m_subsystemParticles = particles;
			m_owner = owner;

			if (m_owner != null)
			{
				m_newChaseBehavior = m_owner.Entity.FindComponent<ComponentNewChaseBehavior>();
				m_oldChaseBehavior = m_owner.Entity.FindComponent<ComponentChaseBehavior>();
				m_zombieChaseBehavior = m_owner.Entity.FindComponent<ComponentZombieChaseBehavior>();
			}

			Texture = ContentManager.Get<Texture2D>("Textures/Gui/vomito venenoso mejorado");
			TextureSlotsCount = 3;
		}

		public override bool Simulate(float dt)
		{
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
			// Velocidad constante (sin decaimiento)
			// float decay = MathF.Pow(0.03f, dt);   // <-- eliminado para efecto láser recto
			m_duration += dt;

			if (m_duration > 5f)
				IsStopped = true;

			float noise = MathUtils.Saturate(1.3f * SimplexNoise.Noise(3f * m_duration + (float)(GetHashCode() % 100)) - 0.3f);
			float generationRate = 30f * noise;
			m_toGenerate += generationRate * dt;

			bool anyActive = false;
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

						BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(oldPos, newPos, 0.1f, (body, d) =>
						{
							if (body.Entity == m_owner.Entity) return false;
							return body.Entity.FindComponent<ComponentCreature>() != null;
						});

						if (bodyHit != null)
						{
							ComponentCreature targetCreature = bodyHit.Value.ComponentBody.Entity.FindComponent<ComponentCreature>();
							if (targetCreature != null)
							{
								// Priorizar jugadores: usar ComponentSickness
								ComponentPlayer player = targetCreature as ComponentPlayer;
								if (player != null)
								{
									player.ComponentSickness.StartSickness();
								}
								else
								{
									// Para criaturas no jugadoras, usar ComponentPoisonInfected
									ComponentPoisonInfected poisonInfected = targetCreature.Entity.FindComponent<ComponentPoisonInfected>();
									poisonInfected?.StartInfect(PoisonIntensity);
								}

								if (m_subsystemTime.GameTime - m_lastImpactSoundTime > 0.5)
								{
									m_subsystemSoundMaterials.PlayImpactSound(bodyHit.Value.ComponentBody.Entity.FindComponent<ComponentBody>()?.StandingOnValue ?? 0, bodyHit.Value.HitPoint(), 1f);
									m_lastImpactSoundTime = m_subsystemTime.GameTime;
								}
							}
							particle.IsActive = false;
							continue;
						}

						TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(oldPos, newPos, false, true, (int value, float _) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable_(value));
						if (terrainHit != null)
						{
							if (m_subsystemTime.GameTime - m_lastImpactSoundTime > 0.3)
							{
								m_subsystemSoundMaterials.PlayImpactSound(terrainHit.Value.Value, terrainHit.Value.HitPoint(), 1f);
								m_lastImpactSoundTime = m_subsystemTime.GameTime;
							}
							// Al impactar con el terreno, la partícula desaparece (efecto láser)
							particle.IsActive = false;
							continue;
						}

						particle.Position = newPos;
						// Sin gravedad y sin decaimiento de velocidad
						// particle.Velocity.Y += -9.81f * dt;   // <-- eliminado
						// particle.Velocity *= decay;           // <-- eliminado
						particle.Color *= MathUtils.Saturate(particle.TimeToLive);
						particle.TextureSlot = (int)(8.99f * MathUtils.Saturate(3f - particle.TimeToLive));
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (!IsStopped && m_toGenerate >= 1f)
				{
					Vector3 randomOffset = m_random.Vector3(0.08f);
					particle.IsActive = true;
					particle.Position = Position + randomOffset;
					particle.Color = Color.MultiplyColorOnly(baseColor, m_random.Float(0.7f, 1f));
					Vector3 spread = m_random.Vector3(0.03f);
					particle.Velocity = MathUtils.Lerp(8f, 12f, noise) * Vector3.Normalize(Direction + 0.1f * spread);
					particle.TimeToLive = m_random.Float(1.5f, 2.5f);
					particle.Size = new Vector2(0.55f);
					particle.FlipX = m_random.Bool();
					particle.FlipY = m_random.Bool();
					m_toGenerate -= 1f;
				}
			}

			return IsStopped && !anyActive;
		}

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float TimeToLive;
		}
	}
}
