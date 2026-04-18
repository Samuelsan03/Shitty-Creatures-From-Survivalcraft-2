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
		private SubsystemFireBlockBehavior m_subsystemFireBlockBehavior;
		private ComponentCreature m_owner;
		private Random m_random = new Random();

		public Vector3 Position { get; set; }
		public Vector3 Direction { get; set; }
		public bool IsStopped { get; set; }
		public float FireDuration { get; set; } = 30f;
		public float ImpactDamage { get; set; } = 0.01f;

		private float m_duration;
		private float m_toGenerate;
		private double m_lastImpactSoundTime;

		public FireVomitParticleSystem(SubsystemTerrain terrain, SubsystemBodies bodies, SubsystemSoundMaterials soundMaterials, SubsystemTime time, ComponentCreature owner)
			: base(400)
		{
			m_subsystemTerrain = terrain;
			m_subsystemBodies = bodies;
			m_subsystemSoundMaterials = soundMaterials;
			m_subsystemTime = time;
			m_owner = owner;

			m_subsystemFireBlockBehavior = terrain.Project.FindSubsystem<SubsystemFireBlockBehavior>(true);

			Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
			TextureSlotsCount = 3;
		}

		public override bool Simulate(float dt)
		{
			// Calcular luz ambiente
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
			float generationRate = 70f * noise;
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

						// Verificar si la partícula ya está dentro de un bloque sólido
						int contents = m_subsystemTerrain.Terrain.GetCellContents(Terrain.ToCell(oldPos));
						if (BlocksManager.Blocks[contents].IsCollidable_(contents))
						{
							particle.IsActive = false;
							continue;
						}

						// Colisión con terreno (con radio aumentado para mejor detección)
						float radius = 0.15f;
						Vector3 dir = newPos - oldPos;
						float dist = dir.Length();
						if (dist > 0.001f)
						{
							dir /= dist;
							TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(
								oldPos,
								oldPos + dir * (dist + radius),
								false,
								true,
								(value, d) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable_(value));

							if (terrainHit != null && terrainHit.Value.Distance <= dist + radius)
							{
								// Incendiar el bloque impactado (NO el vecino)
								TryIgniteBlock(terrainHit.Value);
								if (m_subsystemTime.GameTime - m_lastImpactSoundTime > 0.3)
								{
									m_subsystemSoundMaterials.PlayImpactSound(terrainHit.Value.Value, terrainHit.Value.HitPoint(), 1f);
									m_lastImpactSoundTime = m_subsystemTime.GameTime;
								}
								particle.IsActive = false;
								continue;
							}
						}

						// Colisión con cuerpos (cualquier cuerpo que no sea el dueño y no sea transparente al raycast)
						BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(oldPos, newPos, 0.15f, (body, d) =>
						{
							if (body.Entity == m_owner.Entity) return false;
							// Detenerse con cualquier cuerpo sólido (no transparente)
							return !body.IsRaycastTransparent;
						});

						if (bodyHit != null)
						{
							ComponentBody hitBody = bodyHit.Value.ComponentBody;
							ComponentCreature target = hitBody.Entity.FindComponent<ComponentCreature>();
							if (target != null)
							{
								ComponentHealth health = target.Entity.FindComponent<ComponentHealth>();
								if (health != null)
								{
									string causeOfDeath = LanguageControl.Get("Injury", "FireVomit");
									health.Injure(ImpactDamage, m_owner, false, causeOfDeath);
								}
								ComponentOnFire onFire = target.Entity.FindComponent<ComponentOnFire>();
								onFire?.SetOnFire(m_owner, FireDuration);
							}

							if (m_subsystemTime.GameTime - m_lastImpactSoundTime > 0.5)
							{
								m_subsystemSoundMaterials.PlayImpactSound(bodyHit.Value.ComponentBody.StandingOnValue ?? 0, bodyHit.Value.HitPoint(), 1f);
								m_lastImpactSoundTime = m_subsystemTime.GameTime;
							}
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
					particle.Position = Position;
					particle.Color = Color.MultiplyColorOnly(baseColor, m_random.Float(0.8f, 1f));
					Vector3 dirOffset = m_random.Vector3(0.02f);
					particle.Velocity = normalizedDir * 100f;
					particle.TimeToLive = m_random.Float(1.5f, 2.2f);
					particle.Size = new Vector2(1f);
					particle.FlipX = m_random.Bool();
					particle.FlipY = m_random.Bool();
					m_toGenerate -= 1f;
				}
			}

			return IsStopped && !anyActive;
		}

		private void TryIgniteBlock(TerrainRaycastResult hit)
		{
			// Incendiar la celda impactada directamente
			m_subsystemFireBlockBehavior.SetCellOnFire(hit.CellFace.X, hit.CellFace.Y, hit.CellFace.Z, 1f);
		}

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float TimeToLive;
		}
	}
}
