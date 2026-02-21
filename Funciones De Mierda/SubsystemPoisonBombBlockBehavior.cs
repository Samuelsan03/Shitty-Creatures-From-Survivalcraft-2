using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemPoisonBombBlockBehavior : SubsystemBlockBehavior, IUpdateable
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { PoisonBombBlock.Index }; // 328
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);

			m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemPoisonExplosions = base.Project.FindSubsystem<SubsystemPoisonExplosions>(false); // Cambiado a false
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true); // Añadido para infectar entidades

			foreach (Projectile projectile in m_subsystemProjectiles.Projectiles)
			{
				ScanProjectile(projectile);
			}

			SubsystemProjectiles subsystemProjectiles = m_subsystemProjectiles;
			subsystemProjectiles.ProjectileAdded += delegate (Projectile projectile)
			{
				ScanProjectile(projectile);
			};

			SubsystemProjectiles subsystemProjectiles2 = m_subsystemProjectiles;
			subsystemProjectiles2.ProjectileRemoved += delegate (Projectile projectile)
			{
				m_projectiles.Remove(projectile);
			};
		}

		public void ScanProjectile(Projectile projectile)
		{
			if (!m_projectiles.ContainsKey(projectile))
			{
				int blockId = Terrain.ExtractContents(projectile.Value);

				// Solo manejar bombas de veneno
				if (blockId == PoisonBombBlock.Index)
				{
					m_projectiles.Add(projectile, true);
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.DoNothing;

					// Añadir partículas de rastro verdes
					Color greenColor = new Color(51, 255, 51);
					m_subsystemProjectiles.AddTrail(projectile,
						new Vector3(0f, 0.25f, 0.1f),
						new SmokeTrailParticleSystem(15, 0.25f, float.MaxValue, greenColor));
				}
			}
		}

		public void Update(float dt)
		{
			if (m_subsystemTime.PeriodicGameTimeEvent(0.1, 0.0))
			{
				List<Projectile> projectilesToRemove = new List<Projectile>();

				foreach (Projectile projectile in m_projectiles.Keys)
				{
					// Explotar después de 5 segundos
					if (m_subsystemGameInfo.TotalElapsedGameTime - projectile.CreationTime > 5.0)
					{
						CreatePoisonExplosion(projectile);
						projectilesToRemove.Add(projectile);
						projectile.ToRemove = true;
					}
				}

				// Eliminar proyectiles procesados
				foreach (Projectile projectile in projectilesToRemove)
				{
					m_projectiles.Remove(projectile);
				}
			}
		}

		private void CreatePoisonExplosion(Projectile projectile)
		{
			Vector3 position = projectile.Position;
			int x = Terrain.ToCell(position.X);
			int y = Terrain.ToCell(position.Y);
			int z = Terrain.ToCell(position.Z);

			// Reproducir sonido de explosión
			if (m_subsystemAudio != null)
			{
				m_subsystemAudio.PlaySound("Audio/Explosion De Mierda/Smoke Explosion", 1f, 0f, position, 12f, true);
			}

			// Usar el sistema de explosiones de veneno si existe
			if (m_subsystemPoisonExplosions != null)
			{
				// Parámetros ajustados para bomba de veneno
				float explosionPressure = 25f; // Presión moderada
				float poisonIntensity = 180f;  // Intensidad moderada

				m_subsystemPoisonExplosions.AddPoisonExplosion(
					x, y, z,
					explosionPressure,
					poisonIntensity,
					false // Ya reproducimos el sonido arriba
				);
			}
			else
			{
				// Si no existe el sistema de explosiones de veneno, usar método manual
				// que incluya partículas e infección de entidades
				CreateManualPoisonExplosion(position, x, y, z);
			}
		}

		private void CreateManualPoisonExplosion(Vector3 position, int x, int y, int z)
		{
			// Crear partículas de explosión
			CreateSimplePoisonEffect(position);

			// Infectar entidades cercanas (similar al boomer)
			InfectNearbyEntities(position, 8f, 180f); // Radio 8, Intensidad 180

			// x ELIMINADO: Crear presión/empuje en cuerpos cercanos
			// CreatePressureEffect(position, 25f, 8f);
		}

		private void CreatePressureEffect(Vector3 center, float pressure, float radius)
		{
			if (m_subsystemBodies == null) return;

			// Aplicar fuerza a cuerpos cercanos SIN DESTRUIR BLOQUES
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body.Entity == null) continue;

				Vector3 bodyPos = body.Position;
				float distance = Vector3.Distance(bodyPos, center);

				if (distance <= radius && distance > 0.5f)
				{
					float forceMultiplier = 1f - (distance / radius);
					Vector3 direction = Vector3.Normalize(bodyPos - center);

					// x ELIMINADO: Aplicar fuerza de explosión (empuje)
					// float force = pressure * forceMultiplier * 3f;
					// body.ApplyImpulse(direction * force);

					// x ELIMINADO: También aplicar un poco de fuerza ascendente
					// body.ApplyImpulse(new Vector3(0f, force * 0.3f, 0f));
				}
			}
		}

		private bool IsEntityInsideSmoke(Vector3 entityPosition, Vector3 explosionCenter, float smokeRadius)
		{
			// Esta es una comprobación simplificada.  Una comprobación más precisa
			// requeriría iterar sobre las partículas individuales y verificar si la entidad
			// está dentro de la bounding box de cada partícula.

			// Por ahora, simplemente verificamos si la entidad está dentro de un radio
			// del centro de la explosión.  Esto es solo una aproximación.
			float distanceSquared = Vector3.DistanceSquared(entityPosition, explosionCenter);
			return distanceSquared <= (smokeRadius * smokeRadius);
		}

		private void InfectNearbyEntities(Vector3 center, float poisonRadius, float poisonIntensity)
		{
			if (m_subsystemBodies == null || poisonRadius <= 0) return;

			float poisonRadiusSquared = poisonRadius * poisonRadius;

			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body.Entity == null) continue;

				Vector3 offset = body.Position - center;
				float distanceSquared = offset.LengthSquared();

				if (distanceSquared <= poisonRadiusSquared)
				{
					// x NUEVA COMPROBACIÓN: Verificar si la entidad está dentro del área de las partículas
					if (IsEntityInsideSmoke(body.Position, center, poisonRadius))
					{
						float distance = MathUtils.Sqrt(distanceSquared);
						float intensityMultiplier = 1f - (distance / poisonRadius);
						float finalIntensity = poisonIntensity * intensityMultiplier;

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
									// Si ya está infectado, extender la duración si la nueva es mayor
									poisonInfected.m_InfectDuration = MathUtils.Max(
										poisonInfected.m_InfectDuration, finalIntensity);
								}
							}
						}
					}
				}
			}
		}

		// Método para manejar cuando una bomba de veneno es golpeada por un proyectil
		public void TriggerPoisonExplosion(int x, int y, int z, int value)
		{
			// Destruir el bloque
			m_subsystemTerrain.DestroyCell(0, x, y, z, value, false, false);

			Vector3 position = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);

			// Crear explosión de veneno usando el sistema si existe
			if (m_subsystemPoisonExplosions != null)
			{
				m_subsystemPoisonExplosions.AddPoisonExplosion(
					x, y, z,
					20f,   // Presión
					150f,  // Intensidad
					true
				);
			}
			else
			{
				// Método manual con partículas e infección
				CreateManualPoisonExplosion(position, x, y, z);

				// Reproducir sonido
				if (m_subsystemAudio != null)
				{
					m_subsystemAudio.PlaySound("Audio/Explosion De Mierda/Smoke Explosion", 1f, 0f, position, 10f, true);
				}
			}
		}

		public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
		{
			// Cuando la bomba es golpeada por un proyectil
			// Obtener el valor del bloque
			int value = m_subsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
			int blockId = Terrain.ExtractContents(value);

			// Verificar que sea una bomba de veneno
			if (blockId == PoisonBombBlock.Index)
			{
				TriggerPoisonExplosion(cellFace.X, cellFace.Y, cellFace.Z, value);
			}
		}

		public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
		{
			// Permitir recoger la bomba sin que explote
			return false;
		}

		public override void OnItemHarvested(int x, int y, int z, int blockValue, ref BlockDropValue dropValue, ref int newBlockValue)
		{
			// Cuando se mina la bomba, asegurarse de que no explote
			// La bomba se puede recoger normalmente
		}

		// Método para manejar cuando la bomba es destruida por una explosión
		public void HandleExplosionDamage(int x, int y, int z)
		{
			// Verificar si hay una bomba de veneno en esta posición
			int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
			int blockId = Terrain.ExtractContents(cellValue);

			if (blockId == PoisonBombBlock.Index)
			{
				TriggerPoisonExplosion(x, y, z, cellValue);
			}
		}

		private void CreateSimplePoisonEffect(Vector3 position)
		{
			// Crear una explosión de partículas verde simple
			if (m_subsystemParticles != null)
			{
				// Sistema de partículas para explosión simple de veneno
				var poisonExplosionSystem = new SimplePoisonExplosionParticleSystem(position);
				m_subsystemParticles.AddParticleSystem(poisonExplosionSystem, false);
			}
		}

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemPoisonExplosions m_subsystemPoisonExplosions;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemBodies m_subsystemBodies; // Añadido
		private Dictionary<Projectile, bool> m_projectiles = new Dictionary<Projectile, bool>();
	}

	// Sistema de partículas simple para explosión de veneno
	public class SimplePoisonExplosionParticleSystem : ParticleSystem<SimplePoisonExplosionParticleSystem.Particle>
	{
		private Vector3 m_position;
		private float m_time;
		private Random m_random = new Random();
		private const float m_duration = 3.0f;

		public SimplePoisonExplosionParticleSystem(Vector3 position) : base(100)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/Puke Particle Remake");
			base.TextureSlotsCount = 3;
			m_position = position;

			// Crear partículas iniciales
			for (int i = 0; i < 80; i++)
			{
				AddParticle();
			}
		}

		public override bool Simulate(float dt)
		{
			m_time += dt;

			// Añadir más partículas en el primer segundo
			if (m_time < 1.0f)
			{
				for (int i = 0; i < 3; i++)
				{
					AddParticle();
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
						// Movimiento
						particle.Velocity *= 0.96f;
						particle.Velocity.Y += 0.05f * dt; // Flotación
						particle.Position += particle.Velocity * dt;

						// Cambiar tamaño y opacidad
						float lifeRatio = particle.Time / particle.Duration;
						particle.Size = new Vector2(particle.InitialSize * (1f - lifeRatio * lifeRatio));

						// Color verde que se desvanece
						float alpha = 200f * (1f - lifeRatio);
						particle.Color = new Color(
							(byte)51,
							(byte)255,
							(byte)51,
							(byte)(int)alpha
						);

						// Animación de textura
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

		private void AddParticle()
		{
			for (int i = 0; i < base.Particles.Length; i++)
			{
				if (!base.Particles[i].IsActive)
				{
					Particle particle = base.Particles[i];

					// Posición inicial
					particle.Position = m_position + m_random.Vector3(-0.2f, 0.2f);

					// Dirección aleatoria
					Vector3 direction = m_random.Vector3(-1f, 1f);
					direction.Y = MathUtils.Abs(direction.Y);
					direction = Vector3.Normalize(direction);

					// Velocidad
					float speed = m_random.Float(2f, 6f);
					particle.Velocity = direction * speed;

					// Color verde
					particle.Color = new Color(51, 255, 51, 200);

					// Tamaño
					particle.InitialSize = m_random.Float(0.3f, 0.7f);
					particle.Size = new Vector2(particle.InitialSize);

					// Duración
					particle.Time = 0f;
					particle.Duration = m_random.Float(2.0f, 3.0f);
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
