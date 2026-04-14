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

		private float m_duration;
		private float m_toGenerate;
		private double m_lastImpactSoundTime;

		public FireVomitParticleSystem(SubsystemTerrain terrain, SubsystemBodies bodies, SubsystemSoundMaterials soundMaterials, SubsystemTime time, ComponentCreature owner)
			: base(400) // Suficiente para chorro denso
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
			// Calcular luz ambiente (igual que PoisonVomit)
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

			// Auto-stop después de 3.5 segundos (como PoisonVomit)
			if (m_duration > 3.5f)
			{
				IsStopped = true;
			}

			// Tasa de generación con ruido (similar a PoisonVomit pero constante alta para chorro denso)
			float noise = MathUtils.Saturate(1.3f * SimplexNoise.Noise(3f * m_duration + (float)(GetHashCode() % 100)) - 0.3f);
			float generationRate = 70f * noise; // Alta tasa
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

						// Colisión con cuerpos (excepto dueño)
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
								m_subsystemSoundMaterials.PlayImpactSound(bodyHit.Value.ComponentBody.StandingOnValue ?? 0, bodyHit.Value.HitPoint(), 1f);
								m_lastImpactSoundTime = m_subsystemTime.GameTime;
							}
							particle.IsActive = false;
							continue;
						}

						// Colisión con terreno
						TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(oldPos, newPos, false, true,
							(int value, float _) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable_(value));

						if (terrainHit != null)
						{
							TryIgniteBlock(terrainHit.Value);

							if (m_subsystemTime.GameTime - m_lastImpactSoundTime > 0.3)
							{
								m_subsystemSoundMaterials.PlayImpactSound(terrainHit.Value.Value, terrainHit.Value.HitPoint(), 1f);
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
					// Crear nueva partícula con dispersión mínima para chorro recto
					Vector3 offset = m_random.Vector3(0.04f);
					particle.IsActive = true;
					particle.Position = Position;
					particle.Color = Color.MultiplyColorOnly(baseColor, m_random.Float(0.8f, 1f));
					// Dirección principal con muy poca variación (casi recta)
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
			int x = hit.CellFace.X;
			int y = hit.CellFace.Y;
			int z = hit.CellFace.Z;
			int face = hit.CellFace.Face;

			int neighborX = x;
			int neighborY = y;
			int neighborZ = z;
			switch (face)
			{
				case 0: neighborY--; break;
				case 1: neighborY++; break;
				case 2: neighborZ--; break;
				case 3: neighborZ++; break;
				case 4: neighborX--; break;
				case 5: neighborX++; break;
			}

			m_subsystemFireBlockBehavior.SetCellOnFire(neighborX, neighborY, neighborZ, 1f);
		}

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float TimeToLive;
		}
	}
}
