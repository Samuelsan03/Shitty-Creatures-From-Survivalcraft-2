using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBoomerPoisonExplosion : Component, IUpdateable
	{
		// ===== CONFIGURACIÓN =====
		public float PoisonRadius = 8f;
		public float PoisonIntensity = 200f; // Duración base del envenenamiento
		public float CloudDuration = 15f; // Duración de la nube venenosa
		public float CloudRadius = 6f; // Radio de la nube
		public float ExplosionPressure = 40f; // Presión de la explosión
		public bool PlaySound = true;

		// ===== NUEVA VARIABLE PARA PREVENIR EXPLOSIÓN =====
		public bool PreventExplosion = false;

		// ===== REFERENCIAS =====
		public SubsystemAudio m_subsystemAudio;
		public SubsystemTime m_subsystemTime;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemExplosions m_subsystemExplosions;
		public ComponentHealth m_componentHealth;
		public ComponentBody m_componentBody;

		// ===== ESTADO INTERNO =====
		public bool m_exploded = false;
		public float m_lastHealth = 0f;
		public Random m_random = new Random();

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// CARGAR PARÁMETROS
			PoisonRadius = valuesDictionary.GetValue<float>("PoisonRadius", PoisonRadius);
			PoisonIntensity = valuesDictionary.GetValue<float>("PoisonIntensity", PoisonIntensity);
			CloudDuration = valuesDictionary.GetValue<float>("CloudDuration", CloudDuration);
			CloudRadius = valuesDictionary.GetValue<float>("CloudRadius", CloudRadius);
			ExplosionPressure = valuesDictionary.GetValue<float>("ExplosionPressure", ExplosionPressure);
			PlaySound = valuesDictionary.GetValue<bool>("PlaySound", PlaySound);

			// Obtener referencias
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemExplosions = base.Project.FindSubsystem<SubsystemExplosions>(true);

			m_componentHealth = base.Entity.FindComponent<ComponentHealth>(true);
			m_componentBody = base.Entity.FindComponent<ComponentBody>(true);

			if (m_componentHealth != null)
			{
				m_lastHealth = m_componentHealth.Health;
			}

			// VALIDACIÓN Y AJUSTES
			PoisonRadius = MathUtils.Clamp(PoisonRadius, 1f, 20f);
			PoisonIntensity = MathUtils.Clamp(PoisonIntensity, 10f, 600f);
			CloudDuration = MathUtils.Clamp(CloudDuration, 5f, 60f);
			CloudRadius = MathUtils.Clamp(CloudRadius, 2f, 15f);
			ExplosionPressure = MathUtils.Clamp(ExplosionPressure, 10f, 100f);
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);

			valuesDictionary.SetValue("PoisonRadius", PoisonRadius);
			valuesDictionary.SetValue("PoisonIntensity", PoisonIntensity);
			valuesDictionary.SetValue("CloudDuration", CloudDuration);
			valuesDictionary.SetValue("CloudRadius", CloudRadius);
			valuesDictionary.SetValue("ExplosionPressure", ExplosionPressure);
			valuesDictionary.SetValue("PlaySound", PlaySound);
		}

		public void Update(float dt)
		{
			if (m_componentHealth == null || m_componentBody == null || m_exploded)
				return;

			CheckForDeath();
		}

		public void CheckForDeath()
		{
			if (m_componentHealth == null) return;

			if (!PreventExplosion && ((m_lastHealth > 0 && m_componentHealth.Health <= 0 && !m_exploded) ||
				(m_componentHealth.Health <= 0 && !m_exploded)))
			{
				CreatePoisonExplosion();
			}

			m_lastHealth = m_componentHealth.Health;
		}

		public void CreatePoisonExplosion()
		{
			if (m_exploded || m_componentBody == null) return;

			m_exploded = true;

			Vector3 position = m_componentBody.Position;
			int x = (int)MathUtils.Floor(position.X);
			int y = (int)MathUtils.Floor(position.Y);
			int z = (int)MathUtils.Floor(position.Z);

			// 1. EXPLOSIÓN ESTÁNDAR SIN FUEGO
			if (m_subsystemExplosions != null && ExplosionPressure > 0)
			{
				// IMPORTANTE: Usar false para isIncendiary para que no genere fuego
				m_subsystemExplosions.AddExplosion(x, y, z, ExplosionPressure, false, false);

				// También agregar explosiones secundarias para mejor efecto
				CreateScaledPoisonExplosion(x, y, z);
			}

			// 2. CREAR EFECTO VISUAL DE EXPLOSIÓN VENENOSA CON ONDA EXPANSIVA
			CreatePoisonExplosionEffect(position);

			// 3. INFECTAR ENTIDADES CERCANAS
			InfectNearbyEntities(position);

			// 4. CREAR NUBE VENENOSA PERSISTENTE
			CreatePersistentPoisonCloud(position);
		}

		public void CreateScaledPoisonExplosion(int centerX, int centerY, int centerZ)
		{
			if (m_subsystemExplosions == null) return;

			if (CloudRadius > 3f)
			{
				int extraExplosions = (int)(CloudRadius / 2f);

				for (int i = 0; i < extraExplosions; i++)
				{
					float angle = (float)i * (MathUtils.PI * 2f / extraExplosions);
					float distance = MathUtils.Lerp(1f, CloudRadius * 0.5f, (float)i / extraExplosions);

					int offsetX = (int)(MathUtils.Cos(angle) * distance);
					int offsetZ = (int)(MathUtils.Sin(angle) * distance);

					float secondaryPressure = ExplosionPressure * MathUtils.Lerp(0.6f, 0.2f, distance / CloudRadius);

					if (secondaryPressure > 5f)
					{
						// IMPORTANTE: Siempre false para isIncendiary
						m_subsystemExplosions.AddExplosion(
							centerX + offsetX,
							centerY,
							centerZ + offsetZ,
							secondaryPressure,
							false, // No incendaria
							false
						);
					}
				}
			}
		}

		public void CreatePoisonExplosionEffect(Vector3 center)
		{
			if (m_subsystemParticles == null) return;

			// Crear sistema de partículas principal con movimiento de onda expansiva
			PoisonWaveParticleSystem waveSystem = new PoisonWaveParticleSystem(center, CloudRadius);
			m_subsystemParticles.AddParticleSystem(waveSystem, false);

			// Crear partículas adicionales para efecto más denso
			for (int i = 0; i < 20; i++)
			{
				float angle = m_random.Float(0f, MathUtils.PI * 2f);
				float verticalAngle = m_random.Float(-MathUtils.PI / 4f, MathUtils.PI / 4f);

				Vector3 direction = new Vector3(
					MathUtils.Cos(angle) * MathUtils.Cos(verticalAngle),
					MathUtils.Sin(verticalAngle),
					MathUtils.Sin(angle) * MathUtils.Cos(verticalAngle)
				);

				PoisonSmokeParticleSystem smokeSystem = new PoisonSmokeParticleSystem(
					m_subsystemTerrain,
					center,
					Vector3.Normalize(direction)
				);

				// Ajustar parámetros para efecto de explosión
				smokeSystem.m_time = 0f;
				smokeSystem.m_toGenerate = 50f;

				m_subsystemParticles.AddParticleSystem(smokeSystem, false);
			}
		}

		public void CreatePersistentPoisonCloud(Vector3 center)
		{
			if (m_subsystemParticles == null) return;

			// Crear nube venenosa persistente que queda flotando
			for (int i = 0; i < 8; i++)
			{
				float angle = (float)i * (MathUtils.PI * 2f / 8f);
				float distance = m_random.Float(CloudRadius * 0.3f, CloudRadius * 0.8f);

				Vector3 cloudPosition = center + new Vector3(
					MathUtils.Cos(angle) * distance,
					m_random.Float(0.5f, 2f),
					MathUtils.Sin(angle) * distance
				);

				PoisonSmokeParticleSystem poisonCloud = new PoisonSmokeParticleSystem(
					m_subsystemTerrain,
					cloudPosition,
					new Vector3(0f, 0.05f, 0f) // Movimiento muy lento hacia arriba
				);

				// Ajustar para nube persistente
				poisonCloud.m_time = 0f;
				poisonCloud.m_toGenerate = 30f;
				poisonCloud.m_color = new Color(60, 220, 60, 180); // Verde más intenso

				m_subsystemParticles.AddParticleSystem(poisonCloud, false);
			}
		}

		public void InfectNearbyEntities(Vector3 center)
		{
			if (m_subsystemBodies == null || PoisonRadius <= 0) return;

			float poisonRadiusSquared = PoisonRadius * PoisonRadius;

			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body == m_componentBody || body.Entity == null) continue;

				Vector3 offset = body.Position - center;
				float distanceSquared = offset.LengthSquared();

				if (distanceSquared <= poisonRadiusSquared)
				{
					float distance = MathUtils.Sqrt(distanceSquared);
					float intensityMultiplier = 1f - (distance / PoisonRadius);
					float finalIntensity = PoisonIntensity * intensityMultiplier;

					// Aplicar envenenamiento
					ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
					if (creature != null)
					{
						// Para jugadores
						ComponentPlayer player = creature as ComponentPlayer;
						if (player != null)
						{
							if (!player.ComponentSickness.IsSick)
							{
								player.ComponentSickness.StartSickness();
								player.ComponentSickness.m_sicknessDuration = finalIntensity;
							}
							else
							{
								player.ComponentSickness.m_sicknessDuration = MathUtils.Max(
									player.ComponentSickness.m_sicknessDuration, finalIntensity);
							}
						}
						// Para otras criaturas
						else
						{
							ComponentPoisonInfected poisonInfected = body.Entity.FindComponent<ComponentPoisonInfected>();
							if (poisonInfected != null)
							{
								if (!poisonInfected.IsInfected)
								{
									poisonInfected.StartInfect(finalIntensity);
								}
								else
								{
									poisonInfected.m_InfectDuration = MathUtils.Max(
										poisonInfected.m_InfectDuration, finalIntensity);
								}
							}
						}
					}
				}
			}
		}

		public override void OnEntityRemoved()
		{
			base.OnEntityRemoved();

			if (!PreventExplosion && !m_exploded && m_componentBody != null)
			{
				CreatePoisonExplosion();
			}
		}
	}

	// ===== SISTEMA DE PARTÍCULAS DE ONDA VENENOSA =====
	// Similar a una explosión pero con movimiento radial de humo
	public class PoisonWaveParticleSystem : ParticleSystem<PoisonWaveParticleSystem.Particle>
	{
		public Vector3 m_center;
		public float m_radius;
		public float m_time;
		public Random m_random = new Random();
		public const float m_duration = 1.5f;

		public PoisonWaveParticleSystem(Vector3 center, float radius) : base(800)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/Items/Puke Particle Remake");
			base.TextureSlotsCount = 3;
			this.m_center = center;
			this.m_radius = radius;

			// Generar partículas iniciales
			for (int i = 0; i < 400; i++)
			{
				AddWaveParticle();
			}
		}

		public override bool Simulate(float dt)
		{
			m_time += dt;

			// Generar más partículas al inicio
			if (m_time < 0.3f)
			{
				for (int i = 0; i < 10; i++)
				{
					AddWaveParticle();
				}
			}

			bool hasActiveParticles = false;

			for (int i = 0; i < base.Particles.Length; i++)
			{
				Particle particle = base.Particles[i];

				if (particle.IsActive)
				{
					hasActiveParticles = true;
					particle.Time += dt;

					if (particle.Time < particle.Duration)
					{
						// MOVIMIENTO DE ONDA EXPANSIVA (radial hacia afuera)
						// Mantener dirección radial pero con desaceleración
						particle.Velocity *= 0.92f;

						// Ligero movimiento ascendente
						particle.Velocity.Y += 0.1f * dt;

						// Actualizar posición
						particle.Position += particle.Velocity * dt;

						// Cambiar tamaño (crecer y luego encoger)
						float lifeRatio = particle.Time / particle.Duration;
						if (lifeRatio < 0.3f)
						{
							// Crecer al inicio
							particle.Size = new Vector2(0.3f + lifeRatio * 0.4f);
						}
						else
						{
							// Encoger al final
							particle.Size = new Vector2(0.7f * (1f - lifeRatio));
						}

						// Cambiar opacidad (verde venenoso que se desvanece)
						float alpha = 200f * (1f - lifeRatio);
						particle.Color = new Color(
							(byte)MathUtils.Clamp(30 + lifeRatio * 50, 30, 80),
							(byte)MathUtils.Clamp(180 + lifeRatio * 40, 180, 220),
							(byte)MathUtils.Clamp(30 + lifeRatio * 50, 30, 80),
							(byte)alpha
						);

						// Animación de textura
						particle.TextureSlot = (int)MathUtils.Min(8f * lifeRatio, 7f);
					}
					else
					{
						particle.IsActive = false;
					}
				}
			}

			return m_time < m_duration || hasActiveParticles;
		}

		private void AddWaveParticle()
		{
			// Encontrar partícula inactiva
			for (int i = 0; i < base.Particles.Length; i++)
			{
				if (!base.Particles[i].IsActive)
				{
					Particle particle = base.Particles[i];

					// Dirección aleatoria radial desde el centro
					Vector3 direction = m_random.Vector3(0f, 1f);
					direction.Y *= 0.3f; // Menor componente vertical
					direction = Vector3.Normalize(direction);

					// Posición inicial cerca del centro
					float startDistance = m_random.Float(0f, m_radius * 0.2f);
					particle.Position = m_center + direction * startDistance;

					// Velocidad radial hacia afuera (movimiento de onda)
					float speed = m_random.Float(2f, 8f);
					particle.Velocity = direction * speed;

					// Agregar un poco de variación aleatoria
					particle.Velocity += m_random.Vector3(0f, 0.5f);

					// Color verde venenoso
					particle.Color = new Color(
						(byte)m_random.Int(20, 40),
						(byte)m_random.Int(180, 220),
						(byte)m_random.Int(20, 40),
						(byte)220
					);

					// Tamaño inicial
					particle.Size = new Vector2(m_random.Float(0.2f, 0.5f));

					particle.Time = 0f;
					particle.Duration = m_random.Float(0.8f, 1.5f);
					particle.IsActive = true;
					particle.FlipX = m_random.Bool();
					particle.FlipY = m_random.Bool();

					break;
				}
			}
		}

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float Time;
			public float Duration;
		}
	}
}
