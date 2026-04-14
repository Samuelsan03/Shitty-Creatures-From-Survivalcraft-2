using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FrozenVomitParticleSystem : ParticleSystem<FrozenVomitParticleSystem.Particle>
	{
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;
		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_owner;

		private Random m_random = new Random();
		private float m_duration;
		private float m_toGenerate;

		public Vector3 Position { get; set; }
		public Vector3 Direction { get; set; }
		public bool IsStopped { get; set; }

		public FrozenVomitParticleSystem(SubsystemTerrain terrain, SubsystemBodies bodies,
			SubsystemSoundMaterials soundMaterials, SubsystemTime time, ComponentCreature owner)
			: base(150)
		{
			m_subsystemTerrain = terrain;
			m_subsystemBodies = bodies;
			m_subsystemSoundMaterials = soundMaterials;
			m_subsystemTime = time;
			m_owner = owner;

			Texture = ContentManager.Get<Texture2D>("Textures/Gui/congelante particulas");
			TextureSlotsCount = 3;
		}

		public override bool Simulate(float dt)
		{
			dt = Math.Clamp(dt, 0f, 0.1f);
			float dragFactor = MathF.Pow(0.02f, dt);
			m_duration += dt;

			if (m_duration > 3.5f)
			{
				IsStopped = true;
			}

			float emissionRate = 60f;
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

						TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(oldPos, newPos, false, true,
							(int value, float _) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable_(value));

						if (terrainHit.HasValue)
						{
							p.IsActive = false;
							m_subsystemSoundMaterials.PlayImpactSound(terrainHit.Value.Value, terrainHit.Value.HitPoint(), 0.5f);
							continue;
						}

						BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(oldPos, newPos, 0.1f, (body, distance) =>
						{
							if (body.Entity == m_owner.Entity)
								return false;
							return true;
						});

						if (bodyHit.HasValue)
						{
							ComponentBody hitBody = bodyHit.Value.ComponentBody;
							ApplyFrozenEffect(hitBody, bodyHit.Value.HitPoint());
							p.IsActive = false;
							continue;
						}

						p.Position = newPos;
						p.Velocity.Y -= 9.81f * dt;
						p.Velocity *= dragFactor;
						p.Color *= MathUtils.Saturate(p.TimeToLive * 2f);
					}
					else
					{
						p.IsActive = false;
					}
				}
				else if (!IsStopped && m_toGenerate >= 1f)
				{
					Vector3 randomOffset = m_random.Vector3(0.2f);
					p.IsActive = true;
					p.Position = Position + randomOffset * 0.1f;
					p.Velocity = Direction * m_random.Float(8f, 12f) + m_random.Vector3(1.5f);
					p.TimeToLive = m_random.Float(1.2f, 2.0f);
					p.Size = new Vector2(0.55f);
					p.TextureSlot = m_random.Int(0, TextureSlotsCount - 1);
					p.FlipX = m_random.Bool();
					p.FlipY = m_random.Bool();
					m_toGenerate -= 1f;
				}
			}

			return IsStopped && !anyActive;
		}

		private void ApplyFrozenEffect(ComponentBody targetBody, Vector3 hitPoint)
		{
			m_subsystemSoundMaterials.PlayImpactSound(0, hitPoint, 0.6f);

			ComponentHealth health = targetBody.Entity.FindComponent<ComponentHealth>();
			ComponentFluInfected fluInfected = targetBody.Entity.FindComponent<ComponentFluInfected>();
			ComponentPlayer player = targetBody.Entity.FindComponent<ComponentPlayer>();

			if (fluInfected != null)
			{
				fluInfected.StartFlu(300f);
			}

			if (player != null)
			{
				ComponentFlu playerFlu = player.ComponentFlu;
				if (playerFlu != null && !playerFlu.HasFlu)
				{
					playerFlu.StartFlu();
				}

				ComponentVitalStats vitalStats = player.ComponentVitalStats;
				if (vitalStats != null)
				{
					vitalStats.Temperature = Math.Max(0f, vitalStats.Temperature - 0.5f);
				}
			}

			if (health != null)
			{
				float damage = 0.01f / health.AttackResilience;
				health.Injure(damage, m_owner, false, "Frozen Vomit");
			}
		}

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float TimeToLive;
		}
	}
}
