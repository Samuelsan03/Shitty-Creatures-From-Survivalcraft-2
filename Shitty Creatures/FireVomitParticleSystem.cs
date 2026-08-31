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
			: base(80) // Aumentamos la cantidad para que el chorro sea denso como el del Puke
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
			// Cálculo de luz ambiental (estilo vanilla)
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

			Color baseColor = Color.White * LightingManager.LightIntensityByLightValue[light];
			baseColor.A = 255;

			dt = Math.Clamp(dt, 0f, 0.05f);
			m_duration += dt;

			if (m_duration > 3.5f)
			{
				IsStopped = true;
			}

			// Generación con ruido para que el chorro parezca saliva/vómito pulsante
			float noise = MathUtils.Saturate(1.3f * SimplexNoise.Noise(3f * m_duration + (float)(GetHashCode() % 100)) - 0.3f);
			float generationRate = 45f * noise; // Flujo constante pero pulsante
			m_toGenerate += generationRate * dt;

			bool anyActive = false;
			Vector3 normalizedDir = Direction.LengthSquared() > 0f ? Vector3.Normalize(Direction) : Vector3.UnitZ;

			// Físicas de desaceleración (Estilo Smoke) y flotación (Lava/Vómito caliente)
			float drag = MathF.Pow(0.15f, dt);

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

						// Físicas: Aplicamos inercia, rozamiento y ligera flotación
						particle.Velocity *= drag;
						particle.Velocity.Y += 4f * dt; // Flotación del fuego caliente

						Vector3 newPos = oldPos + particle.Velocity * dt;

						// --- RAYCAST DE TERRENO ---
						TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(
							oldPos,
							newPos,
							false,
							true,
							(value, d) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable_(value));

						if (terrainHit != null)
						{
							int hitContents = Terrain.ExtractContents(terrainHit.Value.Value);

							// Bloques frágiles
							if (hitContents == GlassBlock.Index || hitContents == FramedGlassBlock.Index || hitContents == WindowBlock.Index || hitContents == LightbulbBlock.Index)
							{
								TryBreakFragileBlock(terrainHit.Value);
							}
							else
							{
								TryIgniteBlock(terrainHit.Value);
							}

							if (m_subsystemTime.GameTime - m_lastImpactSoundTime > 0.3)
							{
								m_subsystemSoundMaterials.PlayImpactSound(terrainHit.Value.Value, terrainHit.Value.HitPoint(), 1f);
								m_lastImpactSoundTime = m_subsystemTime.GameTime;
							}

							// Al chocar, la partícula pierde velocidad frontal pero sigue ardiendo un poco (estilo puke que se queda en el suelo)
							particle.Velocity *= 0.1f;
							particle.Position = terrainHit.Value.HitPoint();

							// Transición visual a humo al quedarse en el suelo
							particle.TextureSlot = (int)MathUtils.Min(9f * (1f - (particle.TimeToLive / particle.Duration)) + 3f, 8f);
							particle.Size = new Vector2(0.6f * (1f + (1f - particle.TimeToLive / particle.Duration)));
							continue;
						}

						// --- RAYCAST DE CUERPOS ---
						BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(oldPos, newPos, 0.2f, (body, d) =>
						{
							if (body.Entity == m_owner.Entity) return false;
							return !body.IsRaycastTransparent;
						});

						if (bodyHit != null)
						{
							ComponentBody hitBody = bodyHit.Value.ComponentBody;

							// COMPROBACIÓN DE FUEGO AMIGO
							if (!ShittyCreaturesModLoader.ShouldIgnoreBodyForFriendlyFire(m_owner, hitBody))
							{
								ComponentCreature target = hitBody.Entity.FindComponent<ComponentCreature>();
								if (target != null)
								{
									ComponentHealth health = target.Entity.FindComponent<ComponentHealth>();
									if (health != null)
									{
										string cause = LanguageControl.Get("Injury", "FireVomit");
										health.Injure(ImpactDamage, m_owner, false, cause);
									}
									ComponentOnFire onFire = target.Entity.FindComponent<ComponentOnFire>();
									onFire?.SetOnFire(m_owner, FireDuration);
								}
							}

							if (m_subsystemTime.GameTime - m_lastImpactSoundTime > 0.5)
							{
								m_subsystemSoundMaterials.PlayImpactSound(bodyHit.Value.ComponentBody.StandingOnValue ?? 0, bodyHit.Value.HitPoint(), 1f);
								m_lastImpactSoundTime = m_subsystemTime.GameTime;
							}

							particle.IsActive = false;
							continue;
						}

						// Actualizar posición y color
						particle.Position = newPos;

						// Transición de textura: empieza como fuego brillante (0) y termina como humo oscuro (8)
						float lifeRatio = 1f - (particle.TimeToLive / particle.Duration);
						particle.TextureSlot = (int)MathUtils.Min(9f * lifeRatio * 1.2f, 8f);

						// Expansión de tamaño (estilo Smoke)
						particle.Size = new Vector2(0.35f + 0.45f * lifeRatio);

						// Atenuación del color basada en el tiempo de vida
						particle.Color = Color.MultiplyColorOnly(baseColor, MathUtils.Saturate(particle.TimeToLive * 1.5f));
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (!IsStopped && m_toGenerate >= 1f)
				{
					// --- GENERACIÓN DE NUEVAS PARTÍCULAS (Estilo Puke) ---
					Vector3 spread = m_random.Vector3(-1f, 1f);

					particle.IsActive = true;
					particle.Position = Position + 0.1f * spread;
					particle.Color = Color.MultiplyColorOnly(baseColor, m_random.Float(0.8f, 1f));

					// Velocidad inicial directa hacia el objetivo con dispersión
					particle.Velocity = MathUtils.Lerp(15f, 100f, noise) * Vector3.Normalize(normalizedDir + 0.15f * spread);

					particle.Duration = m_random.Float(2.8f, 3.5f);
					particle.TimeToLive = particle.Duration;
					particle.Size = new Vector2(0.3f);
					particle.FlipX = m_random.Bool();
					particle.FlipY = m_random.Bool();
					particle.TextureSlot = 0; // Inicia como fuego puro

					m_toGenerate -= 1f;
				}
			}

			return IsStopped && !anyActive;
		}

		private void TryBreakFragileBlock(TerrainRaycastResult hit)
		{
			m_subsystemTerrain.DestroyCell(0, hit.CellFace.X, hit.CellFace.Y, hit.CellFace.Z, 0, false, false, null);
		}

		private void TryIgniteBlock(TerrainRaycastResult hit)
		{
			m_subsystemFireBlockBehavior.SetCellOnFire(hit.CellFace.X, hit.CellFace.Y, hit.CellFace.Z, 1f);
		}

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float TimeToLive;
			public float Duration; // Necesario para calcular el ratio de vida exacto para la transición de textura
		}
	}
}
