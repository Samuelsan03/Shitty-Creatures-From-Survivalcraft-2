using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class FireVomitParticleSystem : ParticleSystem<FireVomitParticleSystem.Particle>
	{
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;
		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_owner;
		private Random m_random = new Random();

		private ComponentNewChaseBehavior m_newChaseBehavior;
		private ComponentChaseBehavior m_oldChaseBehavior;
		private ComponentZombieChaseBehavior m_zombieChaseBehavior;

		public Vector3 Position { get; set; }
		public Vector3 Direction { get; set; }
		public bool IsStopped { get; set; }
		public float FireDuration { get; set; } = 30f;

		private float m_duration;
		private float m_toGenerate;
		private double m_lastImpactSoundTime;

		public FireVomitParticleSystem(SubsystemTerrain terrain, SubsystemBodies bodies, SubsystemSoundMaterials soundMaterials, SubsystemTime time, ComponentCreature owner)
			: base(200)
		{
			m_subsystemTerrain = terrain;
			m_subsystemBodies = bodies;
			m_subsystemSoundMaterials = soundMaterials;
			m_subsystemTime = time;
			m_owner = owner;

			if (m_owner != null)
			{
				m_newChaseBehavior = m_owner.Entity.FindComponent<ComponentNewChaseBehavior>();
				m_oldChaseBehavior = m_owner.Entity.FindComponent<ComponentChaseBehavior>();
				m_zombieChaseBehavior = m_owner.Entity.FindComponent<ComponentZombieChaseBehavior>();
			}

			Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
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
			m_duration += dt;

			// ✅ CORRECCIÓN: Auto-stop después de 3.5 segundos (igual que PoisonVomit)
			if (m_duration > 3.5f)
			{
				IsStopped = true;
			}

			float noise = MathUtils.Saturate(1.3f * SimplexNoise.Noise(3f * m_duration + (float)(GetHashCode() % 100)) - 0.3f);
			float generationRate = 55f * noise;
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
							ComponentCreature target = bodyHit.Value.ComponentBody.Entity.FindComponent<ComponentCreature>();
							if (target != null)
							{
								ComponentOnFire onFire = target.Entity.FindComponent<ComponentOnFire>();
								onFire?.SetOnFire(m_owner, FireDuration);
							}

							if (m_subsystemTime.GameTime - m_lastImpactSoundTime > 0.5)
							{
								m_subsystemSoundMaterials.PlayImpactSound(bodyHit.Value.ComponentBody.Entity.FindComponent<ComponentBody>()?.StandingOnValue ?? 0, bodyHit.Value.HitPoint(), 1f);
								m_lastImpactSoundTime = m_subsystemTime.GameTime;
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
							particle.IsActive = false;
							continue;
						}

						particle.Position = newPos;
						particle.Color *= MathUtils.Saturate(particle.TimeToLive);
						particle.TextureSlot = (int)(8.99f * MathUtils.Saturate(2f - particle.TimeToLive));
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (!IsStopped && m_toGenerate >= 1f)
				{
					Vector3 randomOffset = m_random.Vector3(0.05f);
					particle.IsActive = true;
					particle.Position = Position + randomOffset;
					particle.Color = Color.MultiplyColorOnly(baseColor, m_random.Float(0.7f, 1f));
					Vector3 spread = m_random.Vector3(0.02f);
					particle.Velocity = MathUtils.Lerp(10f, 14f, noise) * Vector3.Normalize(Direction + 0.1f * spread);
					particle.TimeToLive = m_random.Float(1.2f, 2.0f);
					particle.Size = new Vector2(0.55f);
					particle.FlipX = m_random.Bool();
					particle.FlipY = m_random.Bool();
					m_toGenerate -= 1f;
				}
			}

			// ✅ CORRECCIÓN: Retorna true solo cuando está detenido Y no hay partículas activas
			return IsStopped && !anyActive;
		}

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float TimeToLive;
		}
	}
}
