using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using System.Collections.Generic;

namespace Game
{
	public class ComponentSuicideBomber : Component, IUpdateable
	{
		// ===== CONFIGURACIÓN COMPLETA DEL JUGADOR =====

		// 1. PARÁMETROS DE ACTIVACIÓN
		public float ActivationRange = 3f;
		public float CountdownDuration = 3f;
		public string SparkSound = "Audio/Explosion De Mierda/Cuenta Regresiva Explosion";

		// 2. TIPO DE EXPLOSIÓN
		public bool UseStandardExplosion = true;
		public bool UseCustomShockwave = false;

		// 3. EXPLOSIÓN ESTÁNDAR - CORREGIDO: AHORA USA LOS RADIOS
		public float ExplosionPressure = 80f;
		public bool IsIncendiary = true;

		// 4. RADIOS CONFIGURABLES - ESTOS SON LOS QUE IMPORTAN
		public float ExplosionRadius = 10f;         // Radio TOTAL de la explosión (visual + daño)
		public float BlockDamageRadius = 8f;        // Radio de destrucción de bloques
		public float EntityDamageRadius = 10f;      // Radio de daño a entidades

		// 5. ONDA EXPANSIVA PERSONALIZADA
		public float ShockwaveDamage = 100f;        // Aumenté el daño por defecto
		public float ShockwaveForce = 50f;          // Aumenté la fuerza por defecto
		public bool DestroyBlocks = true;

		// ===== REFERENCIAS =====
		public SubsystemExplosions m_subsystemExplosions;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemTime m_subsystemTime;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemPickables m_subsystemPickables; // Añadido para loot
		public ComponentHealth m_componentHealth;
		public ComponentBody m_componentBody;
		public ComponentCreature m_componentCreature; // Añadido para obtener ComponentLoot

		// ===== ESTADO INTERNO =====
		public bool m_countdownActive = false;
		public double m_countdownStartTime = 0;
		public bool m_exploded = false;
		public float m_lastHealth = 0f;
		public float m_explosionTimer = 0f;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// CARGAR PARÁMETROS
			ActivationRange = valuesDictionary.GetValue<float>("ActivationRange", ActivationRange);
			CountdownDuration = valuesDictionary.GetValue<float>("CountdownDuration", CountdownDuration);
			SparkSound = valuesDictionary.GetValue<string>("SparkSound", SparkSound);

			UseStandardExplosion = valuesDictionary.GetValue<bool>("UseStandardExplosion", UseStandardExplosion);
			UseCustomShockwave = valuesDictionary.GetValue<bool>("UseCustomShockwave", UseCustomShockwave);

			ExplosionPressure = valuesDictionary.GetValue<float>("ExplosionPressure", ExplosionPressure);
			IsIncendiary = valuesDictionary.GetValue<bool>("IsIncendiary", IsIncendiary);

			// PARÁMETROS CRÍTICOS - ESTOS DEFINEN EL TAMAÑO
			ExplosionRadius = valuesDictionary.GetValue<float>("ExplosionRadius", ExplosionRadius);
			BlockDamageRadius = valuesDictionary.GetValue<float>("BlockDamageRadius", BlockDamageRadius);
			EntityDamageRadius = valuesDictionary.GetValue<float>("EntityDamageRadius", EntityDamageRadius);

			// Parámetros de onda expansiva
			ShockwaveDamage = valuesDictionary.GetValue<float>("ShockwaveDamage", ShockwaveDamage);
			ShockwaveForce = valuesDictionary.GetValue<float>("ShockwaveForce", ShockwaveForce);
			DestroyBlocks = valuesDictionary.GetValue<bool>("DestroyBlocks", DestroyBlocks);

			// Obtener referencias
			m_subsystemExplosions = base.Project.FindSubsystem<SubsystemExplosions>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true); // Añadido

			m_componentHealth = base.Entity.FindComponent<ComponentHealth>(true);
			m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true); // Añadido

			if (m_componentHealth != null)
			{
				m_lastHealth = m_componentHealth.Health;
			}

			// VALIDACIÓN Y AJUSTES
			ActivationRange = MathUtils.Clamp(ActivationRange, 0.5f, 20f);
			CountdownDuration = MathUtils.Clamp(CountdownDuration, 0.1f, 10f);

			// LOS RADIOS SON CRÍTICOS - PERMITIR VALORES ALTOS
			ExplosionRadius = MathUtils.Clamp(ExplosionRadius, 1f, 50f);
			BlockDamageRadius = MathUtils.Clamp(BlockDamageRadius, 0f, ExplosionRadius);
			EntityDamageRadius = MathUtils.Clamp(EntityDamageRadius, 0f, ExplosionRadius * 1.5f);

			// AJUSTAR PRESIÓN SEGÚN RADIO
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

			valuesDictionary.SetValue("ActivationRange", ActivationRange);
			valuesDictionary.SetValue("CountdownDuration", CountdownDuration);
			valuesDictionary.SetValue("SparkSound", SparkSound);
			valuesDictionary.SetValue("UseStandardExplosion", UseStandardExplosion);
			valuesDictionary.SetValue("UseCustomShockwave", UseCustomShockwave);
			valuesDictionary.SetValue("ExplosionPressure", ExplosionPressure);
			valuesDictionary.SetValue("IsIncendiary", IsIncendiary);
			valuesDictionary.SetValue("ExplosionRadius", ExplosionRadius);
			valuesDictionary.SetValue("BlockDamageRadius", BlockDamageRadius);
			valuesDictionary.SetValue("EntityDamageRadius", EntityDamageRadius);
			valuesDictionary.SetValue("ShockwaveDamage", ShockwaveDamage);
			valuesDictionary.SetValue("ShockwaveForce", ShockwaveForce);
			valuesDictionary.SetValue("DestroyBlocks", DestroyBlocks);
		}

		public void Update(float dt)
		{
			if (m_componentHealth == null || m_componentBody == null || m_exploded)
				return;

			CheckForDeath();

			if (m_countdownActive)
			{
				UpdateCountdown(dt);
			}
		}

		public void CheckForDeath()
		{
			if (m_componentHealth == null) return;

			if ((m_lastHealth > 0 && m_componentHealth.Health <= 0 && !m_countdownActive) ||
				(m_componentHealth.Health <= 0 && !m_countdownActive && !m_exploded))
			{
				StartDeathExplosion();
			}

			m_lastHealth = m_componentHealth.Health;
		}

		public void StartDeathExplosion()
		{
			if (m_countdownActive || m_exploded) return;

			m_countdownActive = true;
			m_countdownStartTime = m_subsystemTime.GameTime;
			m_explosionTimer = CountdownDuration;

			if (m_subsystemAudio != null && !string.IsNullOrEmpty(SparkSound))
			{
				m_subsystemAudio.PlaySound(SparkSound, 1f, 0f, m_componentBody.Position,
					MathUtils.Min(20f, ExplosionRadius), 0f);
			}
		}

		public void UpdateCountdown(float dt)
		{
			if (m_subsystemTime == null || m_exploded) return;

			m_explosionTimer -= dt;

			if (m_explosionTimer <= 0f)
			{
				CreateCustomExplosion();
				return;
			}
		}

		public void CreateCustomExplosion()
		{
			if (m_exploded || m_componentBody == null) return;

			m_exploded = true;
			m_countdownActive = false;

			Vector3 position = m_componentBody.Position;
			int x = (int)MathUtils.Floor(position.X);
			int y = (int)MathUtils.Floor(position.Y);
			int z = (int)MathUtils.Floor(position.Z);

			// 1. EXPLOSIÓN ESTÁNDAR - MEJORADA
			if (UseStandardExplosion && m_subsystemExplosions != null && ExplosionPressure > 0)
			{
				// Crear múltiples explosiones para simular radio más grande
				CreateScaledExplosion(x, y, z);
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

			// 3. GENERAR LOOTS AL MORIR (COMO LOS LOOTS NORMALES)
			GenerateLootsOnDeath();

			// 4. SONIDO DE EXPLOSIÓN - AJUSTADO AL RADIO
			if (m_subsystemAudio != null)
			{
				float explosionVolume = MathUtils.Clamp(ExplosionRadius / 20f, 1f, 4f);
				float explosionRange = MathUtils.Clamp(ExplosionRadius * 3f, 30f, 200f);
				m_subsystemAudio.PlaySound("Audio/Explosion De Mierda/Explosion Mejorada", explosionVolume, 0f,
					position, explosionRange, 0f);
			}
		}

		// NUEVO MÉTODO: GENERAR LOOTS AL MORIR
		public void GenerateLootsOnDeath()
		{
			if (m_componentCreature == null || m_subsystemPickables == null) return;

			// Buscar ComponentLoot en la entidad
			ComponentLoot componentLoot = base.Entity.FindComponent<ComponentLoot>();
			if (componentLoot != null && !componentLoot.m_lootDropped)
			{
				// Determinar si está en fuego
				ComponentOnFire componentOnFire = base.Entity.FindComponent<ComponentOnFire>();
				bool isOnFire = componentOnFire != null && componentOnFire.IsOnFire;

				// Usar la lista de loot correspondiente (normal o en fuego)
				List<ComponentLoot.Loot> lootList = isOnFire ? componentLoot.m_lootOnFireList : componentLoot.m_lootList;

				List<BlockDropValue> blockDropValues = new List<BlockDropValue>();

				// Generar los loots según probabilidades
				foreach (ComponentLoot.Loot loot in lootList)
				{
					if (componentLoot.m_random.Float(0f, 1f) < loot.Probability)
					{
						int count = componentLoot.m_random.Int(loot.MinCount, loot.MaxCount);
						for (int i = 0; i < count; i++)
						{
							blockDropValues.Add(new BlockDropValue
							{
								Value = loot.Value,
								Count = 1
							});
						}
					}
				}

				// Permitir a los mods modificar los loots
				ModsManager.HookAction("DecideLoot", (ModLoader loader) =>
				{
					loader.DecideLoot(componentLoot, blockDropValues);
					return false;
				});

				// Crear los pickables (loots) en la posición de la explosión
				Vector3 position = m_componentBody.Position;
				foreach (BlockDropValue blockDropValue in blockDropValues)
				{
					m_subsystemPickables.AddPickable(blockDropValue.Value, blockDropValue.Count,
						position, null, null, base.Entity);
				}

				// Marcar como que ya soltó los loots
				componentLoot.m_lootDropped = true;
			}
		}

		// NUEVO MÉTODO: CREAR EXPLOSIÓN ESCALADA SEGÚN EL RADIO
		public void CreateScaledExplosion(int centerX, int centerY, int centerZ)
		{
			if (m_subsystemExplosions == null) return;

			// Explosión principal en el centro
			m_subsystemExplosions.AddExplosion(centerX, centerY, centerZ, ExplosionPressure, IsIncendiary, false);

			// Si el radio es grande, crear explosiones adicionales
			if (ExplosionRadius > 6f)
			{
				int extraExplosions = (int)(ExplosionRadius / 4f);

				for (int i = 0; i < extraExplosions; i++)
				{
					// Posiciones aleatorias dentro del radio
					float angle = (float)i * (MathUtils.PI * 2f / extraExplosions);
					float distance = MathUtils.Lerp(2f, ExplosionRadius * 0.7f, (float)i / extraExplosions);

					int offsetX = (int)(MathUtils.Cos(angle) * distance);
					int offsetZ = (int)(MathUtils.Sin(angle) * distance);

					// Presión reducida para explosiones secundarias
					float secondaryPressure = ExplosionPressure * MathUtils.Lerp(0.7f, 0.3f, distance / ExplosionRadius);

					if (secondaryPressure > 10f)
					{
						m_subsystemExplosions.AddExplosion(
							centerX + offsetX,
							centerY,
							centerZ + offsetZ,
							secondaryPressure,
							IsIncendiary,
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

					// Aplicar daño
					ComponentHealth health = body.Entity.FindComponent<ComponentHealth>();
					if (health != null && damage > 1f)
					{
						health.Injure(damage, null, false, "Explosión suicida");
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

		// MÉTODO MEJORADO: DESTRUCCIÓN DE BLOQUES MÁS EFECTIVA PERO RESPETANDO BLOQUES INDESTRUCTIBLES
		public void DamageNearbyBlocks(Vector3 center)
		{
			if (m_subsystemTerrain == null || BlockDamageRadius <= 0) return;

			int centerX = (int)center.X;
			int centerY = (int)center.Y;
			int centerZ = (int)center.Z;

			int radius = (int)MathUtils.Ceiling(BlockDamageRadius);
			float radiusSquared = BlockDamageRadius * BlockDamageRadius;

			// DESTRUCCIÓN MÁS AGRESIVA PERO RESPETANDO BLOQUES INDESTRUCTIBLES
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
								// VERIFICAR SI EL BLOQUE ES DESTRUCTIBLE
								// Obtener el tipo de bloque
								int blockType = Terrain.ExtractContents(cellValue);
								Block block = BlocksManager.Blocks[blockType];

								// Verificar si el bloque puede ser destruido por explosiones
								// BedrockBlock y otros bloques indestructibles tienen resistencia infinita
								// Pasar el valor completo (cellValue) en lugar de solo el tipo
								if (block.GetExplosionResilience(cellValue) >= float.MaxValue)
								{
									continue; // Saltar bloques indestructibles
								}

								float distance = MathUtils.Sqrt(distanceSquared);
								float destructionChance = 1f - (distance / BlockDamageRadius);

								// MÁS PROBABLE LA DESTRUCCIÓN
								if (destructionChance > 0.5f) // Reducido de 0.8f a 0.5f
								{
									m_subsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
								}
								// Incluso con menos probabilidad, dañar el bloque
								else if (destructionChance > 0.2f)
								{
									// Dañar el bloque sin destruirlo completamente
									// Solo cambiar el bloque si no es indestructible
									m_subsystemTerrain.ChangeCell(x, y, z, 0, false);
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

			if (!m_exploded && m_componentBody != null)
			{
				CreateCustomExplosion();
			}
		}
	}
}
