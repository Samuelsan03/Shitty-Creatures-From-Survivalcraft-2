using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBoomerPoisonExplosion : Component, IUpdateable
	{
		// ===== CONFIGURACIÓN =====
		public float PoisonRadius = 8f;
		public float PoisonIntensity = 200f;
		public float CloudDuration = 15f;
		public float CloudRadius = 6f;
		public float ExplosionPressure = 40f;

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

			// 1. REPRODUCIR SONIDO DE EXPLOSIÓN
			if (m_subsystemAudio != null)
			{
				m_subsystemAudio.PlaySound("Audio/Explosion De Mierda/Explosion Smoke", 1f, 0f, position, 4f, true);
			}

			// 2. CREAR EFECTO DE PRESIÓN SIN DESTRUIR BLOQUES
			CreatePressureEffect(position);

			// 3. CREAR EFECTO VISUAL DE ONDA EXPANSIVA VENENOSA
			CreatePoisonShockwaveEffect(position);

			// 4. INFECTAR ENTIDADES CERCANAS
			InfectNearbyEntities(position);

			// 5. CREAR NUBE VENENOSA PERSISTENTE
			CreatePersistentPoisonCloud(position);
		}

		public void CreatePressureEffect(Vector3 center)
		{
			if (m_subsystemBodies == null) return;

			float radius = CloudRadius;
			float pressure = ExplosionPressure;

			// Aplicar fuerza a cuerpos cercanos SIN DESTRUIR BLOQUES
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body == m_componentBody || body.Entity == null) continue;

				Vector3 bodyPos = body.Position;
				float distance = Vector3.Distance(bodyPos, center);

				if (distance <= radius && distance > 0.5f)
				{
					float forceMultiplier = 1f - (distance / radius);
					Vector3 direction = Vector3.Normalize(bodyPos - center);

					// Aplicar fuerza de explosión (empuje)
					float force = pressure * forceMultiplier * 3f;
					body.ApplyImpulse(direction * force);

					// También aplicar un poco de fuerza ascendente
					body.ApplyImpulse(new Vector3(0f, force * 0.3f, 0f));
				}
			}
		}

		public void CreatePoisonShockwaveEffect(Vector3 center)
		{
			if (m_subsystemParticles == null) return;

			// Crear onda expansiva de veneno
			PoisonShockwaveParticleSystem shockwaveSystem = new PoisonShockwaveParticleSystem(center, CloudRadius);
			m_subsystemParticles.AddParticleSystem(shockwaveSystem, false);

			// Crear humo verde
			for (int i = 0; i < 25; i++) // Más partículas
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
					Vector3.Normalize(direction) * 3f // Más rápido
				);

				// Ajustar parámetros para efecto de explosión
				smokeSystem.m_time = 0f;
				smokeSystem.m_toGenerate = 50f; // Más partículas

				m_subsystemParticles.AddParticleSystem(smokeSystem, false);
			}
		}

		public void CreatePersistentPoisonCloud(Vector3 center)
		{
			if (m_subsystemParticles == null) return;

			// Crear nube venenosa persistente que dura 4 segundos
			for (int i = 0; i < 12; i++) // Más nubes
			{
				float angle = (float)i * (MathUtils.PI * 2f / 12f);
				float distance = m_random.Float(CloudRadius * 0.4f, CloudRadius * 0.9f);

				Vector3 cloudPosition = center + new Vector3(
					MathUtils.Cos(angle) * distance,
					m_random.Float(0.5f, 3f), // Más alto
					MathUtils.Sin(angle) * distance
				);

				PoisonSmokeParticleSystem poisonCloud = new PoisonSmokeParticleSystem(
					m_subsystemTerrain,
					cloudPosition,
					new Vector3(
						m_random.Float(-0.02f, 0.02f),
						m_random.Float(0.01f, 0.04f), // Movimiento más lento
						m_random.Float(-0.02f, 0.02f)
					)
				);

				// Ajustar para nube que dura 4 segundos
				poisonCloud.m_time = 0f;
				poisonCloud.m_toGenerate = 30f; // Más partículas

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

					// Aplicar envenenamiento USANDO SOLO ComponentPoisonInfected
					ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
					if (creature != null)
					{
						// Buscar ComponentPoisonInfected en la entidad
						ComponentPoisonInfected poisonInfected = body.Entity.FindComponent<ComponentPoisonInfected>();

						// Si no existe el componente, no podemos añadirlo dinámicamente en tiempo de ejecución
						// Solo infectar si ya tiene el componente
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
						// Si la entidad no tiene ComponentPoisonInfected, simplemente no hacer nada
						// o puedes agregar un efecto alternativo si lo deseas
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

	// ===== SISTEMA DE PARTÍCULAS DE ONDA EXPANSIVA VENENOSA =====
	public class PoisonShockwaveParticleSystem : ParticleSystem<PoisonShockwaveParticleSystem.Particle>
	{
		public Vector3 m_center;
		public float m_radius;
		public float m_time;
		public Random m_random = new Random();
		public const float m_duration = 4.0f;

		public PoisonShockwaveParticleSystem(Vector3 center, float radius) : base(1000) // Más partículas
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/Puke Particle Remake");
			base.TextureSlotsCount = 3;
			this.m_center = center;
			this.m_radius = radius;

			// Generar partículas iniciales
			for (int i = 0; i < 800; i++)
			{
				AddShockwaveParticle();
			}
		}

		public override bool Simulate(float dt)
		{
			m_time += dt;

			// Generar más partículas solo al inicio
			if (m_time < 0.5f)
			{
				for (int i = 0; i < 10; i++)
				{
					AddShockwaveParticle();
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
						// MOVIMIENTO DE ONDA EXPANSIVA
						particle.Velocity *= 0.92f; // Desaceleración
						particle.Velocity.Y += 0.1f * dt; // Flotación
						particle.Position += particle.Velocity * dt;

						// Cambiar tamaño
						float lifeRatio = particle.Time / particle.Duration;

						if (lifeRatio < 0.2f)
						{
							particle.Size = new Vector2(0.2f + lifeRatio * 1.0f); // Crecer
						}
						else if (lifeRatio < 0.6f)
						{
							particle.Size = new Vector2(1.2f); // Mantener
						}
						else
						{
							particle.Size = new Vector2(1.2f * (1f - (lifeRatio - 0.6f) * 2.5f)); // Encoger
						}

						// Color verde (sin error)
						float alpha = 220f * (1f - lifeRatio * lifeRatio); // Desvanecimiento cuadrático
						particle.Color = new Color(
							(byte)40,
							(byte)200,
							(byte)40,
							(byte)(int)alpha
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

		private void AddShockwaveParticle()
		{
			for (int i = 0; i < base.Particles.Length; i++)
			{
				if (!base.Particles[i].IsActive)
				{
					Particle particle = base.Particles[i];

					// Dirección radial
					Vector3 direction = m_random.Vector3(0f, 1f);
					direction.Y *= 0.3f; // Más plano
					direction = Vector3.Normalize(direction);

					// Posición inicial en el centro
					particle.Position = m_center;

					// Velocidad para efecto explosivo
					float speed = m_random.Float(5f, 15f);
					particle.Velocity = direction * speed;

					// Variación aleatoria
					particle.Velocity += m_random.Vector3(0f, 1.5f);

					// Color verde
					particle.Color = new Color(
						(byte)m_random.Int(30, 60),
						(byte)m_random.Int(190, 230),
						(byte)m_random.Int(30, 60),
						(byte)240
					);

					// Tamaño
					particle.Size = new Vector2(m_random.Float(0.1f, 0.4f));

					particle.Time = 0f;
					particle.Duration = m_random.Float(3f, 4.5f); // Duración similar al audio
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
