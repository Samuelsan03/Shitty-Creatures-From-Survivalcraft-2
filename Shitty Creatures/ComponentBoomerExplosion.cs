using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBoomerExplosion : Component, IUpdateable
	{
		// ===== CONFIGURACIÓN =====
		public float ActivationRange = 3f;
		public bool UseStandardExplosion = true;
		public bool UseCustomShockwave = false;
		public float ExplosionPressure = 80f;
		public bool IsIncendiary = false;
		public float ExplosionRadius = 10f;
		public float BlockDamageRadius = 8f;
		public float EntityDamageRadius = 10f;
		public float ShockwaveDamage = 100f;
		public float ShockwaveForce = 50f;
		public bool DestroyBlocks = true;

		public bool PreventExplosion = false;

		// ===== REFERENCIAS =====
		public SubsystemExplosions m_subsystemExplosions;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemTime m_subsystemTime;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemTerrain m_subsystemTerrain;
		public ComponentHealth m_componentHealth;
		public ComponentBody m_componentBody;

		public bool m_exploded = false;
		public float m_lastHealth = 0f;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			ActivationRange = valuesDictionary.GetValue<float>("ActivationRange", ActivationRange);
			UseStandardExplosion = valuesDictionary.GetValue<bool>("UseStandardExplosion", UseStandardExplosion);
			UseCustomShockwave = valuesDictionary.GetValue<bool>("UseCustomShockwave", UseCustomShockwave);
			ExplosionPressure = valuesDictionary.GetValue<float>("ExplosionPressure", ExplosionPressure);
			IsIncendiary = valuesDictionary.GetValue<bool>("IsIncendiary", IsIncendiary);
			ExplosionRadius = valuesDictionary.GetValue<float>("ExplosionRadius", ExplosionRadius);
			BlockDamageRadius = valuesDictionary.GetValue<float>("BlockDamageRadius", BlockDamageRadius);
			EntityDamageRadius = valuesDictionary.GetValue<float>("EntityDamageRadius", EntityDamageRadius);
			ShockwaveDamage = valuesDictionary.GetValue<float>("ShockwaveDamage", ShockwaveDamage);
			ShockwaveForce = valuesDictionary.GetValue<float>("ShockwaveForce", ShockwaveForce);
			DestroyBlocks = valuesDictionary.GetValue<bool>("DestroyBlocks", DestroyBlocks);

			m_subsystemExplosions = base.Project.FindSubsystem<SubsystemExplosions>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);

			m_componentHealth = base.Entity.FindComponent<ComponentHealth>(true);
			m_componentBody = base.Entity.FindComponent<ComponentBody>(true);

			if (m_componentHealth != null)
			{
				m_lastHealth = m_componentHealth.Health;
			}

			ActivationRange = MathUtils.Clamp(ActivationRange, 0.5f, 20f);
			ExplosionRadius = MathUtils.Clamp(ExplosionRadius, 1f, 50f);
			BlockDamageRadius = MathUtils.Clamp(BlockDamageRadius, 0f, ExplosionRadius);
			EntityDamageRadius = MathUtils.Clamp(EntityDamageRadius, 0f, ExplosionRadius * 1.5f);

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

			// Obtener dificultad actual
			DifficultyMode difficulty = DifficultyMode.Normal;
			if (SubsystemGreenNightSky.Instance != null)
				difficulty = SubsystemGreenNightSky.Instance.DifficultyMode;

			// Factores de escala según dificultad
			float pressureMult = 1f, damageMult = 1f, forceMult = 1f, radiusMult = 1f;
			switch (difficulty)
			{
				case DifficultyMode.VeryEasy:
					pressureMult = 0.4f;
					damageMult = 0.3f;
					forceMult = 0.5f;
					radiusMult = 0.7f;
					break;
				case DifficultyMode.Easy:
					pressureMult = 0.6f;
					damageMult = 0.5f;
					forceMult = 0.7f;
					radiusMult = 0.8f;
					break;
				case DifficultyMode.Normal:
					pressureMult = 1.0f;
					damageMult = 1.0f;
					forceMult = 1.0f;
					radiusMult = 1.0f;
					break;
				case DifficultyMode.Medium:
					pressureMult = 1.2f;
					damageMult = 1.3f;
					forceMult = 1.2f;
					radiusMult = 1.1f;
					break;
				case DifficultyMode.Hard:
					pressureMult = 1.5f;
					damageMult = 1.6f;
					forceMult = 1.5f;
					radiusMult = 1.25f;
					break;
				case DifficultyMode.Extreme:
					pressureMult = 2.0f;
					damageMult = 2.0f;
					forceMult = 2.0f;
					radiusMult = 1.5f;
					break;
			}

			float finalPressure = ExplosionPressure * pressureMult;
			float finalShockwaveDamage = ShockwaveDamage * damageMult;
			float finalShockwaveForce = ShockwaveForce * forceMult;
			float finalEntityRadius = EntityDamageRadius * radiusMult;
			float finalBlockRadius = BlockDamageRadius * radiusMult;

			Vector3 position = m_componentBody.Position;
			int x = (int)MathUtils.Floor(position.X);
			int y = (int)MathUtils.Floor(position.Y);
			int z = (int)MathUtils.Floor(position.Z);

			if (UseStandardExplosion && m_subsystemExplosions != null && finalPressure > 0)
			{
				m_subsystemExplosions.AddExplosion(x, y, z, finalPressure, IsIncendiary, false);
				CreateScaledExplosion(x, y, z, finalPressure);
			}

			if (UseCustomShockwave)
			{
				if (finalShockwaveDamage > 0 && finalEntityRadius > 0)
				{
					DamageNearbyEntities(position, finalShockwaveDamage, finalShockwaveForce, finalEntityRadius);
				}

				if (DestroyBlocks && finalBlockRadius > 0 && m_subsystemTerrain != null)
				{
					DamageNearbyBlocks(position, finalBlockRadius);
				}
			}
		}

		public void CreateScaledExplosion(int centerX, int centerY, int centerZ, float basePressure)
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
					float secondaryPressure = basePressure * MathUtils.Lerp(0.7f, 0.3f, distance / ExplosionRadius);
					if (secondaryPressure > 10f)
					{
						m_subsystemExplosions.AddExplosion(centerX + offsetX, centerY, centerZ + offsetZ, secondaryPressure, IsIncendiary, false);
					}
				}
			}
		}

		public void DamageNearbyEntities(Vector3 center, float damage, float force, float radius)
		{
			if (m_subsystemBodies == null || radius <= 0) return;
			float radiusSquared = radius * radius;

			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body == m_componentBody || body.Entity == null) continue;

				Vector3 offset = body.Position - center;
				float distanceSquared = offset.LengthSquared();

				if (distanceSquared <= radiusSquared)
				{
					float distance = MathUtils.Sqrt(distanceSquared);
					float damageMultiplier = 1f - (distance / radius);
					float finalDamage = damage * damageMultiplier;

					ComponentHealth health = body.Entity.FindComponent<ComponentHealth>();
					if (health != null && finalDamage > 1f)
					{
						string deathCause = LanguageControl.Get("DeathByBoomer", "Blown to pieces by a Boomer");
						health.Injure(finalDamage, null, false, deathCause);
					}

					if (force > 0 && body.Entity != base.Entity && distance > 0.1f)
					{
						Vector3 forceDirection = offset / distance;
						forceDirection.Y += 0.3f;
						float forceMultiplier = 1f - (distance / radius);
						body.ApplyImpulse(forceDirection * force * forceMultiplier);
					}
				}
			}
		}

		public void DamageNearbyBlocks(Vector3 center, float radius)
		{
			if (m_subsystemTerrain == null || radius <= 0) return;

			int centerX = (int)center.X;
			int centerY = (int)center.Y;
			int centerZ = (int)center.Z;
			int r = (int)MathUtils.Ceiling(radius);
			float radiusSquared = radius * radius;

			for (int dx = -r; dx <= r; dx++)
			{
				for (int dy = -r; dy <= r; dy++)
				{
					for (int dz = -r; dz <= r; dz++)
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
								float destructionChance = 1f - (distance / radius);
								if (destructionChance > 0.5f)
									m_subsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
								else if (destructionChance > 0.2f)
									m_subsystemTerrain.ChangeCell(x, y, z, 0, false);
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
				CreateExplosionImmediately();
			}
		}
	}
}
