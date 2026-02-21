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
		public float PoisonRadius = 15f;  // Cambiado de 8f a 15f
		public float PoisonIntensity = 300f;  // Cambiado de 200f a 300f
		public float CloudDuration = 20.0f;  // Cambiado de 4.0f a 20.0f
		public float CloudRadius = 12f;  // Cambiado de 6f a 12f
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
		public SubsystemPoisonExplosions m_subsystemPoisonExplosions;
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
			m_subsystemPoisonExplosions = base.Project.FindSubsystem<SubsystemPoisonExplosions>(false);

			m_componentHealth = base.Entity.FindComponent<ComponentHealth>(true);
			m_componentBody = base.Entity.FindComponent<ComponentBody>(true);

			if (m_componentHealth != null)
			{
				m_lastHealth = m_componentHealth.Health;
			}

			// VALIDACIÓN Y AJUSTES
			PoisonRadius = MathUtils.Clamp(PoisonRadius, 1f, 20f);
			PoisonIntensity = MathUtils.Clamp(PoisonIntensity, 10f, 600f);
			CloudDuration = 4.0f; // FORZAR 4 SEGUNDOS EXACTOS
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

			// ===== USANDO EL NUEVO SISTEMA DE EXPLOSIONES DE VENENO =====
			if (m_subsystemPoisonExplosions != null)
			{
				// Usar el sistema centralizado para la explosión de veneno
				m_subsystemPoisonExplosions.AddPoisonExplosion(
					x, y, z,
					ExplosionPressure,
					PoisonIntensity,
					false // Reproducir sonido
				);
			}
			else
			{
				// Método de respaldo si no existe el sistema
				CreatePoisonExplosionLegacy(position, x, y, z);
			}

			// ===== EFECTOS ADICIONALES ESPECÍFICOS DEL BOOMER =====

			// 2. CREAR EFECTO VISUAL DE ONDA EXPANSIVA VENENOSA
			CreatePoisonShockwaveEffect(position);

			// 3. CREAR NUBE VENENOSA PERSISTENTE
			CreatePersistentPoisonCloud(position);
		}

		// Método de respaldo si no existe SubsystemPoisonExplosions
		public void CreatePoisonExplosionLegacy(Vector3 position, int x, int y, int z)
		{
			// 1. REPRODUCIR SONIDO DE EXPLOSIÓN (4 SEGUNDOS)
			if (m_subsystemAudio != null)
			{
				m_subsystemAudio.PlaySound("Audio/Explosion De Mierda/Smoke Explosion", 1f, 0f, position, 15f, true);
			}

			// 2. CREAR EFECTO DE PRESIÓN SIN DESTRUIR BLOQUES
			CreatePressureEffect(position);

			// 3. INFECTAR ENTIDADES CERCANAS
			InfectNearbyEntities(position);

			// 4. CREAR PARTÍCULAS DE EXPLOSIÓN DE VENENO MANUALMENTE
			CreatePoisonExplosionParticles(x, y, z);
		}

		public void CreatePoisonExplosionParticles(int x, int y, int z)
		{
			if (m_subsystemParticles == null) return;

			// Crear sistema de partículas de explosión de veneno
			PoisonExplosionParticleSystem poisonExplosionSystem = new PoisonExplosionParticleSystem();

			// Crear partículas en un área alrededor del punto de explosión
			int radius = (int)MathUtils.Clamp(CloudRadius, 3f, 10f);

			for (int i = -radius; i <= radius; i++)
			{
				for (int j = -radius; j <= radius; j++)
				{
					for (int k = -radius; k <= radius; k++)
					{
						float distance = MathUtils.Sqrt(i * i + j * j + k * k);
						if (distance <= radius)
						{
							float strength = MathUtils.Max(0f, 1f - distance / radius) * ExplosionPressure / 50f;
							if (strength > 0.1f)
							{
								poisonExplosionSystem.SetExplosionCell(
									new Point3(x + i, y + j, z + k), strength * 1.5f);
							}
						}
					}
				}
			}

			m_subsystemParticles.AddParticleSystem(poisonExplosionSystem, false);
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

			// Crear onda expansiva de veneno (DURACIÓN 4 SEGUNDOS)
			PoisonShockwaveParticleSystem shockwaveSystem = new PoisonShockwaveParticleSystem(center, CloudRadius);
			m_subsystemParticles.AddParticleSystem(shockwaveSystem, false);

			// Crear explosiones secundarias de veneno
			for (int i = 0; i < 8; i++)
			{
				float angle = m_random.Float(0f, MathUtils.PI * 2f);
				float distance = m_random.Float(1f, CloudRadius * 0.5f);

				Vector3 explosionPos = center + new Vector3(
					MathUtils.Cos(angle) * distance,
					m_random.Float(0f, 1f),
					MathUtils.Sin(angle) * distance
				);

				CreateSecondaryPoisonExplosion(explosionPos);
			}
		}

		private void CreateSecondaryPoisonExplosion(Vector3 position)
		{
			if (m_subsystemParticles == null) return;

			PoisonExplosionParticleSystem secondarySystem = new PoisonExplosionParticleSystem();

			int x = (int)MathUtils.Floor(position.X);
			int y = (int)MathUtils.Floor(position.Y);
			int z = (int)MathUtils.Floor(position.Z);

			// Crear una pequeña explosión secundaria
			int smallRadius = 2;

			for (int i = -smallRadius; i <= smallRadius; i++)
			{
				for (int j = -smallRadius; j <= smallRadius; j++)
				{
					for (int k = -smallRadius; k <= smallRadius; k++)
					{
						float distance = MathUtils.Sqrt(i * i + j * j + k * k);
						if (distance <= smallRadius)
						{
							float strength = MathUtils.Max(0f, 1f - distance / smallRadius) * 0.7f;
							if (strength > 0.1f)
							{
								secondarySystem.SetExplosionCell(
									new Point3(x + i, y + j, z + k), strength);
							}
						}
					}
				}
			}

			m_subsystemParticles.AddParticleSystem(secondarySystem, false);
		}

		public void CreatePersistentPoisonCloud(Vector3 center)
		{
			if (m_subsystemParticles == null) return;

			// Crear nubes persistentes de veneno que duren 4 segundos
			for (int i = 0; i < 5; i++)
			{
				float angle = (float)i * (MathUtils.PI * 2f / 5f);
				float distance = m_random.Float(CloudRadius * 0.3f, CloudRadius * 0.7f);

				Vector3 cloudPosition = center + new Vector3(
					MathUtils.Cos(angle) * distance,
					m_random.Float(1f, 2.5f),
					MathUtils.Sin(angle) * distance
				);

				// Crear nube de veneno que dure 4 segundos exactos
				PoisonCloudParticleSystem poisonCloud = new PoisonCloudParticleSystem(
					cloudPosition,
					4.0f // DURACIÓN EXACTA 4 SEGUNDOS
				);

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

				// x NUEVA COMPROBACIÓN: Verificar si la entidad está dentro del área de la nube
				if (distanceSquared <= poisonRadiusSquared && IsEntityInsideCloud(body.Position, center, CloudRadius))
				{
					float distance = MathUtils.Sqrt(distanceSquared);
					float intensityMultiplier = 1f - (distance / PoisonRadius);
					float finalIntensity = PoisonIntensity * intensityMultiplier;

					ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
					if (creature != null)
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

		private bool IsEntityInsideCloud(Vector3 entityPosition, Vector3 cloudCenter, float cloudRadius)
		{
			// Comprueba si la entidad está dentro del radio de la nube
			float distanceSquared = Vector3.DistanceSquared(entityPosition, cloudCenter);
			return distanceSquared <= (cloudRadius * cloudRadius);
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
		public const float m_duration = 4.0f; // DURACIÓN 4 SEGUNDOS

		public PoisonShockwaveParticleSystem(Vector3 center, float radius) : base(800)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/Puke Particle Remake");
			base.TextureSlotsCount = 3;
			this.m_center = center;
			this.m_radius = radius;

			// Generar partículas iniciales
			for (int i = 0; i < 600; i++)
			{
				AddShockwaveParticle();
			}
		}

		public override bool Simulate(float dt)
		{
			m_time += dt;

			// Generar más partículas solo en el primer segundo
			if (m_time < 1.0f)
			{
				for (int i = 0; i < 5; i++)
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
						particle.Velocity *= 0.94f; // Desaceleración más rápida
						particle.Velocity.Y += 0.08f * dt; // Flotación
						particle.Position += particle.Velocity * dt;

						// Cambiar tamaño
						float lifeRatio = particle.Time / particle.Duration;

						if (lifeRatio < 0.3f)
						{
							particle.Size = new Vector2(0.1f + lifeRatio * 1.1f); // Crecer
						}
						else if (lifeRatio < 0.7f)
						{
							particle.Size = new Vector2(1.0f); // Mantener
						}
						else
						{
							particle.Size = new Vector2(1.0f * (1f - (lifeRatio - 0.7f) * 3.33f)); // Encoger rápido
						}

						// Color verde con desvanecimiento
						float alpha = 240f * (1f - lifeRatio * lifeRatio * lifeRatio); // Desvanecimiento cúbico más rápido
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
					direction.Y *= 0.4f;
					direction = Vector3.Normalize(direction);

					// Posición inicial en el centro
					particle.Position = m_center;

					// Velocidad para efecto explosivo de 4 segundos
					float speed = m_random.Float(4f, 10f);
					particle.Velocity = direction * speed;

					// Variación aleatoria
					particle.Velocity += m_random.Vector3(0f, 1.0f);

					// Color verde
					particle.Color = new Color(
						(byte)m_random.Int(40, 70),
						(byte)m_random.Int(200, 240),
						(byte)m_random.Int(40, 70),
						(byte)220
					);

					// Tamaño inicial más pequeño
					particle.Size = new Vector2(m_random.Float(0.08f, 0.25f));

					particle.Time = 0f;
					particle.Duration = m_random.Float(3.5f, 4.0f); // DURACIÓN 4 SEGUNDOS
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

	// ===== SISTEMA DE PARTÍCULAS PARA NUBES DE VENENO PERSISTENTES =====
	public class PoisonCloudParticleSystem : ParticleSystem<PoisonCloudParticleSystem.Particle>
	{
		public Vector3 m_position;
		public float m_duration;
		public float m_time;
		public Random m_random = new Random();
		public float m_particlesToGenerate = 40f;

		public PoisonCloudParticleSystem(Vector3 position, float duration) : base(300)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/Puke Particle Remake");
			base.TextureSlotsCount = 3;
			this.m_position = position;
			this.m_duration = 4.0f; // DURACIÓN FIJA 4 SEGUNDOS
		}

		public override bool Simulate(float dt)
		{
			m_time += dt;

			// Generar partículas durante los primeros 3 segundos
			if (m_time < 3.0f)
			{
				m_particlesToGenerate += 12f * dt;
				while (m_particlesToGenerate >= 1f)
				{
					AddCloudParticle();
					m_particlesToGenerate -= 1f;
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
						float lifeRatio = particle.Time / particle.Duration;

						// Movimiento lento y flotante
						particle.Velocity.Y += 0.04f * dt;
						particle.Velocity.X += m_random.Float(-0.015f, 0.015f) * dt;
						particle.Velocity.Z += m_random.Float(-0.015f, 0.015f) * dt;
						particle.Position += particle.Velocity * dt;

						// Cambiar tamaño (crece y luego se encoge)
						float sizeMultiplier = MathUtils.Sin(lifeRatio * MathUtils.PI);
						particle.Size = new Vector2(particle.InitialSize * sizeMultiplier);

						// Color verde con desvanecimiento
						float alpha = 160f * (1f - lifeRatio * lifeRatio);
						particle.Color = new Color(
							(byte)51,
							(byte)255,
							(byte)51,
							(byte)(int)alpha
						);

						// Animación de textura lenta
						particle.TextureSlot = (int)(lifeRatio * 6f) % 3;
					}
					else
					{
						particle.IsActive = false;
					}
				}
			}

			return m_time < m_duration || hasActiveParticles;
		}

		private void AddCloudParticle()
		{
			for (int i = 0; i < base.Particles.Length; i++)
			{
				if (!base.Particles[i].IsActive)
				{
					Particle particle = base.Particles[i];

					// Posición aleatoria cerca del centro de la nube
					particle.Position = m_position + new Vector3(
						m_random.Float(-0.8f, 0.8f),
						m_random.Float(-0.3f, 0.3f),
						m_random.Float(-0.8f, 0.8f)
					);

					// Velocidad lenta y aleatoria
					particle.Velocity = new Vector3(
						m_random.Float(-0.03f, 0.03f),
						m_random.Float(0.008f, 0.02f),
						m_random.Float(-0.03f, 0.03f)
					);

					// Color verde RGB: 51,255,51
					particle.Color = new Color(
						(byte)51,
						(byte)255,
						(byte)51,
						(byte)180
					);

					// Tamaño
					particle.InitialSize = m_random.Float(0.25f, 0.5f);
					particle.Size = new Vector2(particle.InitialSize);

					particle.Time = 0f;
					particle.Duration = m_random.Float(2.5f, 4.0f); // DURACIÓN HASTA 4 SEGUNDOS
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
			public float InitialSize;
		}
	}
}
