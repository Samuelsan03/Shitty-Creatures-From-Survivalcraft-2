using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBoomerFireExplosion : Component, IUpdateable
	{
		// ===== CONFIGURACIÓN =====
		public float ActivationRange = 3f;
		public bool UseStandardExplosion = true;
		public bool UseCustomShockwave = false;
		public float ExplosionPressure = 80f;
		public bool IsIncendiary = true; // AHORA ES TRUE POR DEFECTO
		public float ExplosionRadius = 10f;
		public float BlockDamageRadius = 8f;
		public float EntityDamageRadius = 10f;
		public float ShockwaveDamage = 100f;
		public float ShockwaveForce = 50f;
		public bool DestroyBlocks = true;
		public float FireSpreadChance = 0.4f; // NUEVO: Probabilidad de propagar fuego
		public float FireDuration = 10f; // NUEVO: Duración del fuego en segundos

		// ===== NUEVA VARIABLE PARA PREVENIR EXPLOSIÓN =====
		public bool PreventExplosion = false;

		// ===== REFERENCIAS =====
		public SubsystemExplosions m_subsystemExplosions;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemTime m_subsystemTime;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemFireBlockBehavior m_subsystemFire; // NUEVA REFERENCIA
		public ComponentHealth m_componentHealth;
		public ComponentBody m_componentBody;

		// ===== ESTADO INTERNO =====
		public bool m_exploded = false;
		public float m_lastHealth = 0f;
		public Random m_random = new Random(); // NUEVO: Para aleatoriedad

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// CARGAR PARÁMETROS (con valores incendiarios por defecto)
			ActivationRange = valuesDictionary.GetValue<float>("ActivationRange", ActivationRange);
			UseStandardExplosion = valuesDictionary.GetValue<bool>("UseStandardExplosion", UseStandardExplosion);
			UseCustomShockwave = valuesDictionary.GetValue<bool>("UseCustomShockwave", UseCustomShockwave);
			ExplosionPressure = valuesDictionary.GetValue<float>("ExplosionPressure", ExplosionPressure);
			IsIncendiary = valuesDictionary.GetValue<bool>("IsIncendiary", IsIncendiary); // True por defecto
			ExplosionRadius = valuesDictionary.GetValue<float>("ExplosionRadius", ExplosionRadius);
			BlockDamageRadius = valuesDictionary.GetValue<float>("BlockDamageRadius", BlockDamageRadius);
			EntityDamageRadius = valuesDictionary.GetValue<float>("EntityDamageRadius", EntityDamageRadius);
			ShockwaveDamage = valuesDictionary.GetValue<float>("ShockwaveDamage", ShockwaveDamage);
			ShockwaveForce = valuesDictionary.GetValue<float>("ShockwaveForce", ShockwaveForce);
			DestroyBlocks = valuesDictionary.GetValue<bool>("DestroyBlocks", DestroyBlocks);
			FireSpreadChance = valuesDictionary.GetValue<float>("FireSpreadChance", FireSpreadChance);
			FireDuration = valuesDictionary.GetValue<float>("FireDuration", FireDuration);

			// Obtener referencias
			m_subsystemExplosions = base.Project.FindSubsystem<SubsystemExplosions>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemFire = base.Project.FindSubsystem<SubsystemFireBlockBehavior>(true); // Nueva referencia

			m_componentHealth = base.Entity.FindComponent<ComponentHealth>(true);
			m_componentBody = base.Entity.FindComponent<ComponentBody>(true);

			if (m_componentHealth != null)
			{
				m_lastHealth = m_componentHealth.Health;
			}

			// VALIDACIÓN Y AJUSTES
			ActivationRange = MathUtils.Clamp(ActivationRange, 0.5f, 20f);
			ExplosionRadius = MathUtils.Clamp(ExplosionRadius, 1f, 50f);
			BlockDamageRadius = MathUtils.Clamp(BlockDamageRadius, 0f, ExplosionRadius);
			EntityDamageRadius = MathUtils.Clamp(EntityDamageRadius, 0f, ExplosionRadius * 1.5f);
			FireSpreadChance = MathUtils.Clamp(FireSpreadChance, 0f, 1f);
			FireDuration = MathUtils.Clamp(FireDuration, 1f, 60f);

			if (ExplosionRadius > 15f)
			{
				ExplosionPressure = MathUtils.Max(ExplosionPressure, 60f);
			}
			else if (ExplosionRadius > 8f)
			{
				ExplosionPressure = MathUtils.Max(ExplosionPressure, 40f);
			}

			ExplosionPressure = MathUtils.Clamp(ExplosionPressure, 10f, 200f);
			ShockwaveDamage = MathUtils.Clamp(ShockwaveDamage, 0f, 1000f);
			ShockwaveForce = MathUtils.Clamp(ShockwaveForce, 0f, 300f);
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			// NO guardar ningún valor en el diccionario
			// Los valores se cargarán desde la plantilla pero no se persistirán
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
				CreateExplosionImmediately();
			}

			m_lastHealth = m_componentHealth.Health;
		}

		public void CreateExplosionImmediately()
		{
			if (m_exploded || m_componentBody == null) return;

			m_exploded = true;

			Vector3 position = m_componentBody.Position;
			int x = (int)MathUtils.Floor(position.X);
			int y = (int)MathUtils.Floor(position.Y);
			int z = (int)MathUtils.Floor(position.Z);

			// 1. EXPLOSIÓN ESTÁNDAR (INCENDIARIA)
			if (UseStandardExplosion && m_subsystemExplosions != null && ExplosionPressure > 0)
			{
				m_subsystemExplosions.AddExplosion(x, y, z, ExplosionPressure, IsIncendiary, false);
				CreateScaledExplosion(x, y, z, IsIncendiary);
			}

			// 2. ONDA EXPANSIVA PERSONALIZADA
			if (UseCustomShockwave)
			{
				if (ShockwaveDamage > 0 && EntityDamageRadius > 0)
				{
					DamageNearbyEntities(position);
				}

				if (DestroyBlocks && BlockDamageRadius > 0 && m_subsystemTerrain != null)
				{
					DamageNearbyBlocks(position);
				}
			}

			// 3. PROPAGACIÓN DE FUEGO (solo si es incendiario)
			if (IsIncendiary && m_subsystemFire != null)
			{
				SpreadFire(position);
			}
		}

		public void CreateScaledExplosion(int centerX, int centerY, int centerZ, bool isIncendiary)
		{
			if (m_subsystemExplosions == null) return;

			if (ExplosionRadius > 6f)
			{
				int extraExplosions = (int)(ExplosionRadius / 4f);

				for (int i = 0; i < extraExplosions; i++)
				{
					float angle = (float)i * (MathUtils.PI * 2f / extraExplosions);
					float distance = MathUtils.Lerp(2f, ExplosionRadius * 0.7f, (float)i / extraExplosions);

					int offsetX = (int)(MathUtils.Cos(angle) * distance);
					int offsetZ = (int)(MathUtils.Sin(angle) * distance);

					float secondaryPressure = ExplosionPressure * MathUtils.Lerp(0.7f, 0.3f, distance / ExplosionRadius);

					if (secondaryPressure > 10f)
					{
						m_subsystemExplosions.AddExplosion(
							centerX + offsetX,
							centerY,
							centerZ + offsetZ,
							secondaryPressure,
							isIncendiary, // Pasar el parámetro incendiario
							false
						);
					}
				}
			}
		}

		public void DamageNearbyEntities(Vector3 center)
		{
			if (m_subsystemBodies == null || EntityDamageRadius <= 0) return;

			float entityRadiusSquared = EntityDamageRadius * EntityDamageRadius;

			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body == m_componentBody || body.Entity == null) continue;

				Vector3 offset = body.Position - center;
				float distanceSquared = offset.LengthSquared();

				if (distanceSquared <= entityRadiusSquared)
				{
					float distance = MathUtils.Sqrt(distanceSquared);
					float damageMultiplier = 1f - (distance / EntityDamageRadius);
					float damage = ShockwaveDamage * damageMultiplier;

					// Aplicar daño a CUALQUIER entidad con salud
					ComponentHealth health = body.Entity.FindComponent<ComponentHealth>();
					if (health != null && damage > 1f)
					{
						// Usar LanguageControl para la causa de muerte INCENDIARIA
						string deathCause = LanguageControl.Get("DeathByBoomerFire", "Incinerated by a fiery Boomer explosion");

						// Para explosiones incendiarias, agregar daño extra de fuego
						if (IsIncendiary)
						{
							damage *= 1.3f; // 30% más de daño por fuego
						}

						health.Injure(damage, null, false, deathCause);
					}

					// Aplicar fuerza de empuje
					if (ShockwaveForce > 0 && body.Entity != base.Entity && distance > 0.1f)
					{
						Vector3 forceDirection = offset / distance;
						forceDirection.Y += 0.3f;

						float forceMultiplier = 1f - (distance / EntityDamageRadius);
						body.ApplyImpulse(forceDirection * ShockwaveForce * forceMultiplier);
					}
				}
			}
		}

		public void DamageNearbyBlocks(Vector3 center)
		{
			if (m_subsystemTerrain == null || BlockDamageRadius <= 0) return;

			int centerX = (int)center.X;
			int centerY = (int)center.Y;
			int centerZ = (int)center.Z;

			int radius = (int)MathUtils.Ceiling(BlockDamageRadius);
			float radiusSquared = BlockDamageRadius * BlockDamageRadius;

			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					for (int dz = -radius; dz <= radius; dz++)
					{
						float distanceSquared = dx * dx + dy * dy + dz * dz;

						if (distanceSquared <= radiusSquared)
						{
							int x = centerX + dx;
							int y = centerY + dy;
							int z = centerZ + dz;

							int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
							if (cellValue != 0)
							{
								float distance = MathUtils.Sqrt(distanceSquared);
								float destructionChance = 1f - (distance / BlockDamageRadius);

								if (destructionChance > 0.5f)
								{
									m_subsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
								}
								else if (destructionChance > 0.2f)
								{
									m_subsystemTerrain.ChangeCell(x, y, z, 0, false);
								}
							}
						}
					}
				}
			}
		}

		// NUEVO MÉTODO: Propagar fuego alrededor de la explosión
		public void SpreadFire(Vector3 center)
		{
			if (m_subsystemFire == null || m_subsystemTerrain == null || FireSpreadChance <= 0) return;

			int centerX = (int)center.X;
			int centerY = (int)center.Y;
			int centerZ = (int)center.Z;

			int fireRadius = (int)MathUtils.Ceiling(BlockDamageRadius * 1.2f); // Radio de fuego ligeramente mayor
			float radiusSquared = fireRadius * fireRadius;

			for (int dx = -fireRadius; dx <= fireRadius; dx++)
			{
				for (int dy = -fireRadius; dy <= fireRadius; dy++)
				{
					for (int dz = -fireRadius; dz <= fireRadius; dz++)
					{
						float distanceSquared = dx * dx + dy * dy + dz * dz;

						if (distanceSquared <= radiusSquared)
						{
							int x = centerX + dx;
							int y = centerY + dy;
							int z = centerZ + dz;

							// Verificar si ya hay fuego en esta posición
							if (!m_subsystemFire.IsCellOnFire(x, y, z))
							{
								// Calcular probabilidad basada en la distancia
								float distance = MathUtils.Sqrt(distanceSquared);
								float fireChance = FireSpreadChance * (1f - (distance / fireRadius));

								// Aleatorizar para propagación natural
								if (m_random.Float(0f, 1f) < fireChance)
								{
									// Verificar si el bloque puede ser incendiado
									int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
									int blockId = Terrain.ExtractContents(cellValue);

									if (blockId != 0) // No aire
									{
										Block block = BlocksManager.Blocks[blockId];

										// Solo incendiar bloques que puedan arder
										if (block.GetFireDuration(cellValue) > 0f)
										{
											m_subsystemFire.SetCellOnFire(x, y, z, FireDuration);
										}
										// También intentar incendiar bloques adyacentes al aire
										else if (IsNextToAir(x, y, z))
										{
											// Para bloques no inflamables, crear fuego en posición adyacente
											m_subsystemFire.SetCellOnFire(x, y + 1, z, FireDuration * 0.5f);
										}
									}
									else // Es aire, colocar fuego directamente
									{
										m_subsystemFire.SetCellOnFire(x, y, z, FireDuration);
									}
								}
							}
						}
					}
				}
			}
		}

		// Método auxiliar para verificar si un bloque está junto al aire
		private bool IsNextToAir(int x, int y, int z)
		{
			if (m_subsystemTerrain == null) return false;

			// Verificar las 6 direcciones
			if (m_subsystemTerrain.Terrain.GetCellContents(x + 1, y, z) == 0) return true;
			if (m_subsystemTerrain.Terrain.GetCellContents(x - 1, y, z) == 0) return true;
			if (m_subsystemTerrain.Terrain.GetCellContents(x, y + 1, z) == 0) return true;
			if (m_subsystemTerrain.Terrain.GetCellContents(x, y - 1, z) == 0) return true;
			if (m_subsystemTerrain.Terrain.GetCellContents(x, y, z + 1) == 0) return true;
			if (m_subsystemTerrain.Terrain.GetCellContents(x, y, z - 1) == 0) return true;

			return false;
		}

		public override void OnEntityRemoved()
		{
			base.OnEntityRemoved();

			if (!PreventExplosion && !m_exploded && m_componentBody != null)
			{
				CreateExplosionImmediately();
			}
		}
	}
}
