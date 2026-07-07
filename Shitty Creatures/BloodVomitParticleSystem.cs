using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;

namespace Game
{
	public class BloodVomitParticleSystem : ParticleSystem<BloodVomitParticleSystem.Particle>
	{
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;
		private SubsystemTime m_subsystemTime;
		private SubsystemParticles m_subsystemParticles;
		private ComponentCreature m_componentCreature;

		public Vector3 Position { get; set; }
		public Vector3 Direction { get; set; }
		public float BleedingIntensity { get; set; } = 180f;
		public bool IsStopped { get; set; }

		private Random m_random = new Random();
		private float m_duration;
		private float m_toGenerate;

		public BloodVomitParticleSystem(SubsystemTerrain terrain, SubsystemBodies bodies, SubsystemSoundMaterials soundMaterials,
			SubsystemTime time, SubsystemParticles particles, ComponentCreature creature)
			: base(60)
		{
			m_subsystemTerrain = terrain;
			m_subsystemBodies = bodies;
			m_subsystemSoundMaterials = soundMaterials;
			m_subsystemTime = time;
			m_subsystemParticles = particles;
			m_componentCreature = creature;

			Texture = ContentManager.Get<Texture2D>("Textures/Gui/blood");
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

			dt = Math.Clamp(dt, 0f, 0.05f);
			m_duration += dt;

			if (m_duration > 3.5f)
			{
				IsStopped = true;
			}

			float noise = MathUtils.Saturate(1.3f * SimplexNoise.Noise(3f * m_duration + (float)(GetHashCode() % 100)) - 0.3f);
			float generationRate = 60f * noise;
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

						int contents = m_subsystemTerrain.Terrain.GetCellContents(Terrain.ToCell(oldPos));
						Block block = BlocksManager.Blocks[contents];
						if (block.IsCollidable_(contents))
						{
							particle.IsActive = false;
							continue;
						}

						float radius = 0.15f;
						Vector3 direction = newPos - oldPos;
						float distance = direction.Length();
						if (distance > 0.001f)
						{
							direction /= distance;
							TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(
								oldPos,
								oldPos + direction * (distance + radius),
								false,
								true,
								(value, d) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable_(value));

							if (terrainHit != null && terrainHit.Value.Distance <= distance + radius)
							{
								int hitValue = terrainHit.Value.Value;
								int hitContents = Terrain.ExtractContents(hitValue);
								if (hitContents == GlassBlock.Index || hitContents == FramedGlassBlock.Index ||
									hitContents == WindowBlock.Index || hitContents == LightbulbBlock.Index)
								{
									m_subsystemTerrain.DestroyCell(0, terrainHit.Value.CellFace.X, terrainHit.Value.CellFace.Y, terrainHit.Value.CellFace.Z, 0, false, false, null);
								}

								m_subsystemSoundMaterials.PlayImpactSound(terrainHit.Value.Value, terrainHit.Value.HitPoint(), 0.5f);
								particle.IsActive = false;
								continue;
							}
						}

						BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(oldPos, newPos, 0.15f, (body, d) =>
						{
							if (body.Entity == m_componentCreature.Entity) return false;
							return !body.IsRaycastTransparent;
						});

						if (bodyHit != null)
						{
							ComponentBody hitBody = bodyHit.Value.ComponentBody;
							// --- COMPROBACIÓN DE FUEGO AMIGO ---
							if (ShittyCreaturesModLoader.ShouldIgnoreBodyForFriendlyFire(m_componentCreature, hitBody))
							{
								particle.IsActive = false;
								continue;
							}
							if (hitBody != null)
							{
								Entity entity = hitBody.Entity;
								if (entity != null)
								{
									ComponentHealth health = entity.FindComponent<ComponentHealth>();
									if (health != null && health.Health > 0f)
									{
										float damage = 0.01f;
										string cause = LanguageControl.Get("Injury", "BloodVomit");
										health.Injure(damage, m_componentCreature, false, cause);
									}
								}
							}
							// --- SONIDO SOLO SI NO ES FUEGO AMIGO (ya está después de la comprobación) ---
							m_subsystemSoundMaterials.PlayImpactSound(bodyHit.Value.ComponentBody.StandingOnValue ?? 0, bodyHit.Value.HitPoint(), 0.5f);
							particle.IsActive = false;
							continue;
						}

						particle.Position = newPos;
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
					Vector3 offset = m_random.Vector3(0.04f);
					particle.IsActive = true;
					particle.Position = Position + offset;
					particle.Color = baseColor;
					particle.Velocity = normalizedDir * 100f;
					particle.TimeToLive = m_random.Float(1.5f, 2.2f);
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
