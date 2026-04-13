using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;

namespace Game
{
	public class MagicVomitParticleSystem : ParticleSystem<MagicVomitParticleSystem.Particle>
	{
		public Vector3 Position { get; set; }
		public Vector3 Direction { get; set; }
		public bool IsStopped { get; set; }

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;
		private ComponentCreature m_owner;
		private Random m_random = new Random();
		private float m_duration;
		private float m_toGenerate;

		private const float PoisonDuration = 150f;
		private const float EffectRadius = 1.8f;

		public MagicVomitParticleSystem(SubsystemTerrain terrain, SubsystemBodies bodies, ComponentCreature owner)
			: base(80)
		{
			m_subsystemTerrain = terrain;
			m_subsystemBodies = bodies;
			m_owner = owner;
			m_subsystemSoundMaterials = terrain.Project.FindSubsystem<SubsystemSoundMaterials>(true);
			Texture = ContentManager.Get<Texture2D>("Textures/Gui/vomito venenoso mejorado");
			TextureSlotsCount = 3;
		}

		public override bool Simulate(float dt)
		{
			// Luz ambiental
			int cx = Terrain.ToCell(Position.X);
			int cy = Terrain.ToCell(Position.Y);
			int cz = Terrain.ToCell(Position.Z);
			int light = 0;
			light = MathUtils.Max(light, m_subsystemTerrain.Terrain.GetCellLight(cx + 1, cy, cz));
			light = MathUtils.Max(light, m_subsystemTerrain.Terrain.GetCellLight(cx - 1, cy, cz));
			light = MathUtils.Max(light, m_subsystemTerrain.Terrain.GetCellLight(cx, cy + 1, cz));
			light = MathUtils.Max(light, m_subsystemTerrain.Terrain.GetCellLight(cx, cy - 1, cz));
			light = MathUtils.Max(light, m_subsystemTerrain.Terrain.GetCellLight(cx, cy, cz + 1));
			light = MathUtils.Max(light, m_subsystemTerrain.Terrain.GetCellLight(cx, cy, cz - 1));
			Color c = Color.White * LightingManager.LightIntensityByLightValue[light];
			c.A = 255;

			dt = Math.Clamp(dt, 0f, 0.1f);
			float damp = MathF.Pow(0.99f, dt);
			m_duration += dt;
			if (m_duration > 3.5f)
				IsStopped = true;

			float noise = MathUtils.Saturate(1.3f * SimplexNoise.Noise(3f * m_duration + (GetHashCode() % 100)) - 0.3f);
			float emissionRate = 30f * noise;
			m_toGenerate += emissionRate * dt;

			bool anyActive = false;
			for (int i = 0; i < Particles.Length; i++)
			{
				Particle p = Particles[i];
				if (p.IsActive)
				{
					anyActive = true;
					p.TimeToLive -= dt;
					if (p.TimeToLive > 0f)
					{
						Vector3 oldPos = p.Position;
						Vector3 newPos = oldPos + p.Velocity * dt;
						Vector3 move = newPos - oldPos;
						float distance = move.Length();
						Vector3 direction = distance > 0f ? move / distance : Vector3.UnitY;

						// 1. Raycast contra terreno
						TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(oldPos, newPos, false, true,
							(v, d) => BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable_(v));
						if (terrainHit != null)
						{
							m_subsystemSoundMaterials.PlayImpactSound(terrainHit.Value.Value, terrainHit.Value.HitPoint(), 1f);
							p.IsActive = false;
							continue;
						}

						// 2. Raycast contra cuerpos (para detectar impacto en criaturas)
						BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(oldPos, newPos, 0.1f,
							(body, d) => body.Entity != m_owner?.Entity && d <= distance);
						if (bodyHit != null)
						{
							ComponentBody hitBody = bodyHit.Value.ComponentBody;
							ApplyEffectToBody(hitBody);
							// La partícula no se destruye al impactar un cuerpo, sigue su camino
						}

						p.Position = newPos;
						p.Velocity *= damp;
						p.Color *= MathUtils.Saturate(p.TimeToLive);
						p.TextureSlot = (int)(8.99f * MathUtils.Saturate(3f - p.TimeToLive));
					}
					else
					{
						p.IsActive = false;
					}
				}
				else if (!IsStopped && m_toGenerate >= 1f)
				{
					Vector3 randDir = m_random.Vector3(0f, 1f);
					p.IsActive = true;
					p.Position = Position + 0.05f * randDir;
					p.Color = Color.MultiplyColorOnly(c, m_random.Float(0.7f, 1f));
					p.Velocity = MathUtils.Lerp(9f, 13f, noise) * Vector3.Normalize(Direction + 0.03f * randDir);
					p.TimeToLive = 3f;
					p.Size = new Vector2(0.15f);
					p.FlipX = m_random.Bool();
					p.FlipY = m_random.Bool();
					m_toGenerate -= 1f;
				}
			}

			return IsStopped && !anyActive;
		}

		private void ApplyEffectToBody(ComponentBody body)
		{
			if (body == null || body.Entity == m_owner?.Entity)
				return;

			// Buscar ComponentPlayer primero
			ComponentPlayer player = body.Entity.FindComponent<ComponentPlayer>();
			if (player != null)
			{
				ComponentSickness sickness = player.ComponentSickness;
				if (sickness != null && !sickness.IsSick)
					sickness.StartSickness();
				return;
			}

			// Para criaturas, buscar ComponentPoisonInfected
			ComponentPoisonInfected poison = body.Entity.FindComponent<ComponentPoisonInfected>();
			if (poison != null && !poison.IsInfected)
			{
				poison.StartInfect(PoisonDuration);
			}
		}

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float TimeToLive;
		}
	}
}
