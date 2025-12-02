using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentSuicideBomber : Component, IUpdateable
	{
		// Parámetros configurables
		public float ActivationRange = 1f;
		public float CountdownDuration = 0.1f;
		public string SparkSound = "Audio/Fuse";
		public float ExplosionPressure = 155f;
		public bool IsIncendiary = true;
		public string TargetCategories = "LandPredator,LandOther,WaterPredator,WaterOther";

		// NUEVOS parámetros para onda explosiva
		public float ExplosionRadius = 20f;
		public float ShockwaveDamage = 100f;
		public float ShockwaveForce = 50f;
		public bool DestroyBlocks = true;
		public float BlockDamageRadius = 15f;
		public float EntityDamageRadius = 25f;

		// Referencias a subsistemas
		public SubsystemExplosions m_subsystemExplosions;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemTime m_subsystemTime;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemProjectiles m_subsystemProjectiles;

		// Referencias a componentes
		public ComponentHealth m_componentHealth;
		public ComponentBody m_componentBody;

		// Estado interno
		public bool m_countdownActive = false;
		public double m_countdownStartTime = 0;
		public bool m_exploded = false;
		public float m_lastHealth = 0f;
		public float m_explosionTimer = 0f;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros básicos
			ActivationRange = valuesDictionary.GetValue<float>("ActivationRange", ActivationRange);
			CountdownDuration = valuesDictionary.GetValue<float>("CountdownDuration", CountdownDuration);
			SparkSound = valuesDictionary.GetValue<string>("SparkSound", SparkSound);
			ExplosionPressure = valuesDictionary.GetValue<float>("ExplosionPressure", ExplosionPressure);
			IsIncendiary = valuesDictionary.GetValue<bool>("IsIncendiary", IsIncendiary);
			TargetCategories = valuesDictionary.GetValue<string>("TargetCategories", TargetCategories);

			// Cargar NUEVOS parámetros (con valores por defecto)
			ExplosionRadius = valuesDictionary.GetValue<float>("ExplosionRadius", ExplosionRadius);
			ShockwaveDamage = valuesDictionary.GetValue<float>("ShockwaveDamage", ShockwaveDamage);
			ShockwaveForce = valuesDictionary.GetValue<float>("ShockwaveForce", ShockwaveForce);
			DestroyBlocks = valuesDictionary.GetValue<bool>("DestroyBlocks", DestroyBlocks);
			BlockDamageRadius = valuesDictionary.GetValue<float>("BlockDamageRadius", BlockDamageRadius);
			EntityDamageRadius = valuesDictionary.GetValue<float>("EntityDamageRadius", EntityDamageRadius);

			// Obtener referencias
			m_subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);

			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);

			if (m_componentHealth != null)
			{
				m_lastHealth = m_componentHealth.Health;
			}

			// Validar valores
			ExplosionPressure = MathUtils.Clamp(ExplosionPressure, 1f, 200f);
			ExplosionRadius = MathUtils.Clamp(ExplosionRadius, 1f, 50f);
			ShockwaveDamage = MathUtils.Clamp(ShockwaveDamage, 0f, 500f);
			ShockwaveForce = MathUtils.Clamp(ShockwaveForce, 0f, 200f);
			BlockDamageRadius = MathUtils.Clamp(BlockDamageRadius, 0f, ExplosionRadius);
			EntityDamageRadius = MathUtils.Clamp(EntityDamageRadius, 0f, ExplosionRadius * 2f);
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);

			// Guardar parámetros básicos
			valuesDictionary.SetValue("ActivationRange", ActivationRange);
			valuesDictionary.SetValue("CountdownDuration", CountdownDuration);
			valuesDictionary.SetValue("SparkSound", SparkSound);
			valuesDictionary.SetValue("ExplosionPressure", ExplosionPressure);
			valuesDictionary.SetValue("IsIncendiary", IsIncendiary);
			valuesDictionary.SetValue("TargetCategories", TargetCategories);

			// Guardar nuevos parámetros
			valuesDictionary.SetValue("ExplosionRadius", ExplosionRadius);
			valuesDictionary.SetValue("ShockwaveDamage", ShockwaveDamage);
			valuesDictionary.SetValue("ShockwaveForce", ShockwaveForce);
			valuesDictionary.SetValue("DestroyBlocks", DestroyBlocks);
			valuesDictionary.SetValue("BlockDamageRadius", BlockDamageRadius);
			valuesDictionary.SetValue("EntityDamageRadius", EntityDamageRadius);
		}

		public void Update(float dt)
		{
			// Verificar referencias nulas
			if (m_componentHealth == null || m_componentBody == null)
				return;

			// Si ya explotó, no hacer nada
			if (m_exploded)
				return;

			// Verificar si murió
			CheckForDeath();

			// Si el countdown está activo, actualizar el timer
			if (m_countdownActive)
			{
				UpdateCountdown(dt);
			}
		}

		public void CheckForDeath()
		{
			if (m_componentHealth == null)
				return;

			// Verificar si acaba de morir (transición de vivo a muerto)
			if (m_lastHealth > 0 && m_componentHealth.Health <= 0 && !m_countdownActive)
			{
				StartDeathExplosion();
			}

			// Verificar si ya está muerto y no hemos iniciado countdown
			if (m_componentHealth.Health <= 0 && !m_countdownActive && !m_exploded)
			{
				StartDeathExplosion();
			}

			// Actualizar registro de salud
			m_lastHealth = m_componentHealth.Health;
		}

		public void StartDeathExplosion()
		{
			if (m_countdownActive || m_exploded)
				return;

			m_countdownActive = true;
			m_countdownStartTime = m_subsystemTime.GameTime;
			m_explosionTimer = CountdownDuration;

			// Reproducir sonido de chispa
			if (m_subsystemAudio != null && !string.IsNullOrEmpty(SparkSound))
			{
				m_subsystemAudio.PlaySound(SparkSound, 1f, 0f, m_componentBody.Position, 15f, 0f);
			}
		}

		public void UpdateCountdown(float dt)
		{
			if (m_subsystemTime == null || m_exploded)
				return;

			// Reducir timer
			m_explosionTimer -= dt;

			// Si el timer llegó a 0, explotar
			if (m_explosionTimer <= 0f)
			{
				CreateCustomExplosion();
				return;
			}

			// Si CountdownDuration es > 1 segundo, hacer sonidos intermitentes
			double elapsedTime = m_subsystemTime.GameTime - m_countdownStartTime;
			if (CountdownDuration > 1f && m_subsystemAudio != null && elapsedTime % 1.0 < 0.1)
			{
				float volume = 1f - (float)(elapsedTime / CountdownDuration) * 0.5f;
				m_subsystemAudio.PlaySound(SparkSound, volume, 0f, m_componentBody.Position, 12f, 0f);
			}
		}

		public void CreateCustomExplosion()
		{
			if (m_exploded || m_componentBody == null || m_subsystemExplosions == null)
				return;

			m_exploded = true;
			m_countdownActive = false;

			Vector3 position = m_componentBody.Position;
			int x = (int)MathUtils.Floor(position.X);
			int y = (int)MathUtils.Floor(position.Y);
			int z = (int)MathUtils.Floor(position.Z);

			// 1. EXPLOSIÓN PRINCIPAL
			m_subsystemExplosions.AddExplosion(x, y, z, ExplosionPressure, IsIncendiary, false);

			// 2. DAÑO ADICIONAL A ENTIDADES (onda expansiva)
			if (ShockwaveDamage > 0 && EntityDamageRadius > 0)
			{
				DamageNearbyEntities(position);
			}

			// 3. SONIDO DE EXPLOSIÓN
			if (m_subsystemAudio != null)
			{
				m_subsystemAudio.PlaySound("Audio/ExplosionLarge", 2f, 0f, position, 40f, 0f);
			}
		}

		public void DamageNearbyEntities(Vector3 center)
		{
			if (m_subsystemBodies == null)
				return;

			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body == m_componentBody || body.Entity == null)
					continue;

				float distance = Vector3.Distance(center, body.Position);
				if (distance <= EntityDamageRadius)
				{
					// Calcular daño basado en distancia
					float damageMultiplier = 1f - (distance / EntityDamageRadius);
					float damage = ShockwaveDamage * damageMultiplier;

					ComponentHealth health = body.Entity.FindComponent<ComponentHealth>();
					if (health != null && damage > 1f)
					{
						health.Injure(damage, null, false, "Explosión");
					}

					// Aplicar fuerza de empuje
					if (ShockwaveForce > 0 && body.Entity != Entity)
					{
						Vector3 forceDirection = body.Position - center;
						if (forceDirection.LengthSquared() > 0.01f)
						{
							// CORRECCIÓN AQUÍ: Normalize es estático, devuelve nuevo vector
							forceDirection = Vector3.Normalize(forceDirection);
							forceDirection.Y += 0.3f; // Levantar un poco

							float forceMultiplier = 1f - (distance / EntityDamageRadius);
							body.ApplyImpulse(forceDirection * ShockwaveForce * forceMultiplier);
						}
					}
				}
			}
		}

		public override void OnEntityRemoved()
		{
			base.OnEntityRemoved();

			// Si fue removida sin explotar, explotar ahora
			if (!m_exploded && m_componentBody != null && m_subsystemExplosions != null)
			{
				CreateCustomExplosion();
			}
		}
	}
}
