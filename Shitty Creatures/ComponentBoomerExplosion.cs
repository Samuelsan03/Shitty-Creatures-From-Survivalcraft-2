using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using System.Linq;

namespace Game
{
	public class ComponentBoomerExplosion : Component, IUpdateable
	{
		public enum BoomerExplosionType
		{
			Normal,
			Fire,
			Poison,
			Frozen
		}

		public static class BoomerExplosionCreatures
		{
			public static readonly string[] NormalExplosion = { "Boomer1", "BoomerTamed1", "GhostBoomer1", "GhostBoomerTamed1" };
			public static readonly string[] FireExplosion = { "Boomer2", "BoomerTamed2", "GhostBoomer2", "GhostBoomerTamed2" };
			public static readonly string[] PoisonExplosion = { "Boomer3", "GhostBoomer3", "GhostBoomerTamed3" };
			public static readonly string[] FrozenExplosion = { "BoomerFrozen", "BoomerFrozenTamed", "FrozenGhostBoomer", "FrozenGhostBoomerTamed" };

			public static BoomerExplosionType GetExplosionType(string creatureName)
			{
				if (NormalExplosion.Contains(creatureName)) return BoomerExplosionType.Normal;
				if (FireExplosion.Contains(creatureName)) return BoomerExplosionType.Fire;
				if (PoisonExplosion.Contains(creatureName)) return BoomerExplosionType.Poison;
				if (FrozenExplosion.Contains(creatureName)) return BoomerExplosionType.Frozen;
				return BoomerExplosionType.Normal;
			}
		}

		private BoomerExplosionType m_explosionType;

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

		public float FireSpreadChance = 0.4f;
		public float FireDuration = 10f;

		public float PoisonRadius = 15f;
		public float PoisonIntensity = 300f;
		public float CloudDuration = 20.0f;
		public float CloudRadius = 12f;

		public float FreezePressure = 50f;
		public float FluDuration = 300f;
		public bool NoExplosionSound = false;

		public SubsystemAttractNoise m_subsystemAttractNoise;
		public SubsystemExplosions m_subsystemExplosions;
		public SubsystemTime m_subsystemTime;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemFireBlockBehavior m_subsystemFire;
		public SubsystemFreezeExplosions m_subsystemFreezeExplosions;
		public SubsystemPoisonExplosions m_subsystemPoisonExplosions;
		public SubsystemParticles m_subsystemParticles;
		public ComponentHealth m_componentHealth;
		public ComponentBody m_componentBody;

		public bool m_exploded = false;
		public float m_lastHealth = 0f;
		public Random m_random = new Random();

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			string entityName = Entity.ValuesDictionary.DatabaseObject.Name;
			m_explosionType = BoomerExplosionCreatures.GetExplosionType(entityName);

			m_subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true);
			m_subsystemAttractNoise = Project.FindSubsystem<SubsystemAttractNoise>(false);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemFire = Project.FindSubsystem<SubsystemFireBlockBehavior>(true);
			m_subsystemFreezeExplosions = Project.FindSubsystem<SubsystemFreezeExplosions>(true);
			m_subsystemPoisonExplosions = Project.FindSubsystem<SubsystemPoisonExplosions>(false);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);

			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			if (m_componentHealth != null) m_lastHealth = m_componentHealth.Health;

			switch (m_explosionType)
			{
				case BoomerExplosionType.Normal:
					ActivationRange = MathUtils.Clamp(ActivationRange, 0.5f, 20f);
					ExplosionRadius = MathUtils.Clamp(ExplosionRadius, 1f, 50f);
					BlockDamageRadius = MathUtils.Clamp(BlockDamageRadius, 0f, ExplosionRadius);
					EntityDamageRadius = MathUtils.Clamp(EntityDamageRadius, 0f, ExplosionRadius * 1.5f);
					if (ExplosionRadius > 15f) ExplosionPressure = MathUtils.Max(ExplosionPressure, 60f);
					else if (ExplosionRadius > 8f) ExplosionPressure = MathUtils.Max(ExplosionPressure, 40f);
					ExplosionPressure = MathUtils.Clamp(ExplosionPressure, 10f, 200f);
					ShockwaveDamage = MathUtils.Clamp(ShockwaveDamage, 0f, 1000f);
					ShockwaveForce = MathUtils.Clamp(ShockwaveForce, 0f, 300f);
					break;

				case BoomerExplosionType.Fire:
					ActivationRange = MathUtils.Clamp(ActivationRange, 0.5f, 20f);
					ExplosionRadius = MathUtils.Clamp(ExplosionRadius, 1f, 50f);
					BlockDamageRadius = MathUtils.Clamp(BlockDamageRadius, 0f, ExplosionRadius);
					EntityDamageRadius = MathUtils.Clamp(EntityDamageRadius, 0f, ExplosionRadius * 1.5f);
					FireSpreadChance = MathUtils.Clamp(FireSpreadChance, 0f, 1f);
					FireDuration = MathUtils.Clamp(FireDuration, 1f, 60f);
					if (ExplosionRadius > 15f) ExplosionPressure = MathUtils.Max(ExplosionPressure, 60f);
					else if (ExplosionRadius > 8f) ExplosionPressure = MathUtils.Max(ExplosionPressure, 40f);
					ExplosionPressure = MathUtils.Clamp(ExplosionPressure, 10f, 200f);
					ShockwaveDamage = MathUtils.Clamp(ShockwaveDamage, 0f, 1000f);
					ShockwaveForce = MathUtils.Clamp(ShockwaveForce, 0f, 300f);
					break;

				case BoomerExplosionType.Poison:
					PoisonRadius = MathUtils.Clamp(PoisonRadius, 1f, 20f);
					PoisonIntensity = MathUtils.Clamp(PoisonIntensity, 10f, 600f);
					CloudDuration = MathUtils.Clamp(CloudDuration, 2f, 60f);
					CloudRadius = MathUtils.Clamp(CloudRadius, 2f, 15f);
					ExplosionPressure = MathUtils.Clamp(ExplosionPressure, 10f, 100f);
					break;

				case BoomerExplosionType.Frozen:
					FreezePressure = MathUtils.Clamp(FreezePressure, 10f, 200f);
					FluDuration = MathUtils.Clamp(FluDuration, 0f, 600f);
					break;
			}
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
		}

		public void Update(float dt)
		{
			if (m_componentHealth == null || m_componentBody == null || m_exploded) return;
			CheckForDeath();
		}

		public void CheckForDeath()
		{
			if (m_componentHealth == null) return;

			switch (m_explosionType)
			{
				case BoomerExplosionType.Normal:
					if (!PreventExplosion && ((m_lastHealth > 0 && m_componentHealth.Health <= 0 && !m_exploded) || (m_componentHealth.Health <= 0 && !m_exploded)))
						CreateNormalExplosion();
					break;

				case BoomerExplosionType.Fire:
					if (!PreventExplosion && ((m_lastHealth > 0 && m_componentHealth.Health <= 0 && !m_exploded) || (m_componentHealth.Health <= 0 && !m_exploded)))
						CreateFireExplosion();
					break;

				case BoomerExplosionType.Poison:
					if (!PreventExplosion && ((m_lastHealth > 0 && m_componentHealth.Health <= 0 && !m_exploded) || (m_componentHealth.Health <= 0 && !m_exploded)))
						CreatePoisonExplosion();
					break;

				case BoomerExplosionType.Frozen:
					bool isDead = (m_lastHealth > 0 && m_componentHealth.Health <= 0) || (m_componentHealth.Health <= 0 && !m_exploded);
					if (!PreventExplosion && isDead)
						CreateFrozenExplosion();
					break;
			}

			m_lastHealth = m_componentHealth.Health;
		}

		#region Normal Explosion
		public void CreateNormalExplosion()
		{
			if (m_exploded || m_componentBody == null) return;
			m_exploded = true;

			DifficultyMode difficulty = DifficultyMode.Normal;
			if (SubsystemGreenNightSky.Instance != null) difficulty = SubsystemGreenNightSky.Instance.DifficultyMode;

			float pressureMult = 1f, damageMult = 1f, forceMult = 1f, radiusMult = 1f;
			float lureRange = 15f;
			switch (difficulty)
			{
				case DifficultyMode.VeryEasy: pressureMult = 0.4f; damageMult = 0.3f; forceMult = 0.5f; radiusMult = 0.7f; lureRange = 5f; break;
				case DifficultyMode.Easy: pressureMult = 0.6f; damageMult = 0.5f; forceMult = 0.7f; radiusMult = 0.8f; lureRange = 10f; break;
				case DifficultyMode.Normal: pressureMult = 1.0f; damageMult = 1.0f; forceMult = 1.0f; radiusMult = 1.0f; lureRange = 15f; break;
				case DifficultyMode.Medium: pressureMult = 1.2f; damageMult = 1.3f; forceMult = 1.2f; radiusMult = 1.1f; lureRange = 22f; break;
				case DifficultyMode.Hard: pressureMult = 1.5f; damageMult = 1.6f; forceMult = 1.5f; radiusMult = 1.25f; lureRange = 35f; break;
				case DifficultyMode.Extreme: pressureMult = 2.0f; damageMult = 2.0f; forceMult = 2.0f; radiusMult = 1.5f; lureRange = 55f; break;
				case DifficultyMode.Impossible: pressureMult = 3.0f; damageMult = 3.0f; forceMult = 2.5f; radiusMult = 2.0f; lureRange = 100f; break;
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
				CreateNormalScaledExplosion(x, y, z, finalPressure);
			}

			if (UseCustomShockwave)
			{
				if (finalShockwaveDamage > 0 && finalEntityRadius > 0) DamageNormalNearbyEntities(position, finalShockwaveDamage, finalShockwaveForce, finalEntityRadius);
				if (DestroyBlocks && finalBlockRadius > 0 && m_subsystemTerrain != null) DamageNormalNearbyBlocks(position, finalBlockRadius);
			}

			if (m_subsystemAttractNoise != null)
			{
				m_subsystemAttractNoise.MakeLureNoise(position, 10f, lureRange);
			}
		}

		public void CreateNormalScaledExplosion(int centerX, int centerY, int centerZ, float basePressure)
		{
			if (m_subsystemExplosions == null || ExplosionRadius <= 6f) return;
			int extraExplosions = (int)(ExplosionRadius / 4f);
			for (int i = 0; i < extraExplosions; i++)
			{
				float angle = (float)i * (MathUtils.PI * 2f / extraExplosions);
				float distance = MathUtils.Lerp(2f, ExplosionRadius * 0.7f, (float)i / extraExplosions);
				int offsetX = (int)(MathUtils.Cos(angle) * distance);
				int offsetZ = (int)(MathUtils.Sin(angle) * distance);
				float secondaryPressure = basePressure * MathUtils.Lerp(0.7f, 0.3f, distance / ExplosionRadius);
				if (secondaryPressure > 10f) m_subsystemExplosions.AddExplosion(centerX + offsetX, centerY, centerZ + offsetZ, secondaryPressure, IsIncendiary, false);
			}
		}

		public void DamageNormalNearbyEntities(Vector3 center, float damage, float force, float radius)
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
					float finalDamage = damage * (1f - (distance / radius));
					ComponentHealth health = body.Entity.FindComponent<ComponentHealth>();
					if (health != null && finalDamage > 1f) health.Injure(finalDamage, null, false, LanguageControl.Get("DeathByBoomer", "Blown to pieces by a Boomer"));
					if (force > 0 && body.Entity != base.Entity && distance > 0.1f)
					{
						Vector3 forceDirection = offset / distance; forceDirection.Y += 0.3f;
						body.ApplyImpulse(forceDirection * force * (1f - (distance / radius)));
					}
				}
			}
		}

		public void DamageNormalNearbyBlocks(Vector3 center, float radius)
		{
			if (m_subsystemTerrain == null || radius <= 0) return;
			int centerX = (int)center.X, centerY = (int)center.Y, centerZ = (int)center.Z, r = (int)MathUtils.Ceiling(radius);
			float radiusSquared = radius * radius;
			for (int dx = -r; dx <= r; dx++) for (int dy = -r; dy <= r; dy++) for (int dz = -r; dz <= r; dz++)
			{
				float distanceSquared = dx * dx + dy * dy + dz * dz;
				if (distanceSquared <= radiusSquared)
				{
					int x = centerX + dx, y = centerY + dy, z = centerZ + dz;
					int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
					if (cellValue != 0)
					{
						float destructionChance = 1f - (MathUtils.Sqrt(distanceSquared) / radius);
						if (destructionChance > 0.5f) m_subsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
						else if (destructionChance > 0.2f) m_subsystemTerrain.ChangeCell(x, y, z, 0, false);
					}
				}
			}
		}
		#endregion

		#region Fire Explosion
		public void CreateFireExplosion()
		{
			if (m_exploded || m_componentBody == null) return;
			m_exploded = true;

			DifficultyMode difficulty = DifficultyMode.Normal;
			if (SubsystemGreenNightSky.Instance != null)
				difficulty = SubsystemGreenNightSky.Instance.DifficultyMode;

			float pressureMult = 1f, damageMult = 1f, forceMult = 1f, radiusMult = 1f;
			float fireChanceMult = 1f, fireDurationMult = 1f;
			float lureRange = 20f;
			switch (difficulty)
			{
				case DifficultyMode.VeryEasy: pressureMult = 0.4f; damageMult = 0.3f; forceMult = 0.5f; radiusMult = 0.7f; fireChanceMult = 0.5f; fireDurationMult = 0.6f; lureRange = 7f; break;
				case DifficultyMode.Easy: pressureMult = 0.6f; damageMult = 0.5f; forceMult = 0.7f; radiusMult = 0.8f; fireChanceMult = 0.7f; fireDurationMult = 0.8f; lureRange = 12f; break;
				case DifficultyMode.Normal: pressureMult = 1.0f; damageMult = 1.0f; forceMult = 1.0f; radiusMult = 1.0f; fireChanceMult = 1.0f; fireDurationMult = 1.0f; lureRange = 20f; break;
				case DifficultyMode.Medium: pressureMult = 1.2f; damageMult = 1.3f; forceMult = 1.2f; radiusMult = 1.1f; fireChanceMult = 1.2f; fireDurationMult = 1.2f; lureRange = 28f; break;
				case DifficultyMode.Hard: pressureMult = 1.5f; damageMult = 1.6f; forceMult = 1.5f; radiusMult = 1.25f; fireChanceMult = 1.5f; fireDurationMult = 1.4f; lureRange = 40f; break;
				case DifficultyMode.Extreme: pressureMult = 2.0f; damageMult = 2.0f; forceMult = 2.0f; radiusMult = 1.5f; fireChanceMult = 2.0f; fireDurationMult = 1.8f; lureRange = 60f; break;
				case DifficultyMode.Impossible: pressureMult = 3.0f; damageMult = 3.0f; forceMult = 2.5f; radiusMult = 2.0f; lureRange = 110f; break;
			}

			float finalPressure = ExplosionPressure * pressureMult;
			float finalDamage = ShockwaveDamage * damageMult;
			float finalForce = ShockwaveForce * forceMult;
			float finalEntityRadius = EntityDamageRadius * radiusMult;
			float finalBlockRadius = BlockDamageRadius * radiusMult;
			float finalFireChance = FireSpreadChance * fireChanceMult;
			float finalFireDuration = FireDuration * fireDurationMult;

			Vector3 position = m_componentBody.Position;
			int x = (int)MathUtils.Floor(position.X);
			int y = (int)MathUtils.Floor(position.Y);
			int z = (int)MathUtils.Floor(position.Z);

			if (UseStandardExplosion && m_subsystemExplosions != null && finalPressure > 0)
			{
				m_subsystemExplosions.AddExplosion(x, y, z, finalPressure, IsIncendiary, false);
				CreateFireScaledExplosion(x, y, z, finalPressure, IsIncendiary);
			}

			if (UseCustomShockwave)
			{
				if (finalDamage > 0 && finalEntityRadius > 0)
					DamageFireNearbyEntities(position, finalDamage, finalForce, finalEntityRadius);
				if (DestroyBlocks && finalBlockRadius > 0 && m_subsystemTerrain != null)
					DamageFireNearbyBlocks(position, finalBlockRadius);
			}

			if (IsIncendiary && m_subsystemFire != null)
				SpreadFire(position, finalFireChance, finalFireDuration);

			if (m_subsystemAttractNoise != null)
			{
				m_subsystemAttractNoise.MakeLureNoise(position, 15f, lureRange);
			}
		}

		public void CreateFireScaledExplosion(int centerX, int centerY, int centerZ, float basePressure, bool isIncendiary)
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
						m_subsystemExplosions.AddExplosion(centerX + offsetX, centerY, centerZ + offsetZ, secondaryPressure, isIncendiary, false);
				}
			}
		}

		public void DamageFireNearbyEntities(Vector3 center, float currentDamage, float currentForce, float radius)
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
					float damage = currentDamage * damageMultiplier;
					ComponentHealth health = body.Entity.FindComponent<ComponentHealth>();
					if (health != null && damage > 1f)
					{
						if (IsIncendiary) damage *= 1.3f;
						health.Injure(damage, null, false, "Incinerated by a fiery Boomer explosion");
					}
					if (currentForce > 0 && body.Entity != base.Entity && distance > 0.1f)
					{
						Vector3 forceDirection = offset / distance;
						forceDirection.Y += 0.3f;
						float forceMultiplier = 1f - (distance / radius);
						body.ApplyImpulse(forceDirection * currentForce * forceMultiplier);
					}
				}
			}
		}

		public void DamageFireNearbyBlocks(Vector3 center, float radius)
		{
			if (m_subsystemTerrain == null || radius <= 0) return;
			int centerX = (int)center.X;
			int centerY = (int)center.Y;
			int centerZ = (int)center.Z;
			int r = (int)MathUtils.Ceiling(radius);
			float radiusSquared = radius * radius;

			for (int dx = -r; dx <= r; dx++)
				for (int dy = -r; dy <= r; dy++)
					for (int dz = -r; dz <= r; dz++)
					{
						float distanceSquared = dx * dx + dy * dy + dz * dz;
						if (distanceSquared <= radiusSquared)
						{
							int x = centerX + dx, y = centerY + dy, z = centerZ + dz;
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

		public void SpreadFire(Vector3 center, float fireChance, float fireDur)
		{
			if (m_subsystemFire == null || m_subsystemTerrain == null || fireChance <= 0) return;
			int centerX = (int)center.X, centerY = (int)center.Y, centerZ = (int)center.Z;
			int fireRadius = (int)MathUtils.Ceiling(BlockDamageRadius * 1.2f);
			float radiusSquared = fireRadius * fireRadius;

			for (int dx = -fireRadius; dx <= fireRadius; dx++)
				for (int dy = -fireRadius; dy <= fireRadius; dy++)
					for (int dz = -fireRadius; dz <= fireRadius; dz++)
					{
						float distanceSquared = dx * dx + dy * dy + dz * dz;
						if (distanceSquared <= radiusSquared)
						{
							int x = centerX + dx, y = centerY + dy, z = centerZ + dz;
							if (!m_subsystemFire.IsCellOnFire(x, y, z))
							{
								float distance = MathUtils.Sqrt(distanceSquared);
								float chance = fireChance * (1f - (distance / fireRadius));
								if (m_random.Float(0f, 1f) < chance)
								{
									int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
									int blockId = Terrain.ExtractContents(cellValue);
									if (blockId != 0)
									{
										Block block = BlocksManager.Blocks[blockId];
										if (block.GetFireDuration(cellValue) > 0f)
											m_subsystemFire.SetCellOnFire(x, y, z, fireDur);
										else if (IsNextToAir(x, y, z))
											m_subsystemFire.SetCellOnFire(x, y + 1, z, fireDur * 0.5f);
									}
									else
									{
										m_subsystemFire.SetCellOnFire(x, y, z, fireDur);
									}
								}
							}
						}
					}
		}

		private bool IsNextToAir(int x, int y, int z)
		{
			if (m_subsystemTerrain == null) return false;
			return m_subsystemTerrain.Terrain.GetCellContents(x + 1, y, z) == 0 ||
				   m_subsystemTerrain.Terrain.GetCellContents(x - 1, y, z) == 0 ||
				   m_subsystemTerrain.Terrain.GetCellContents(x, y + 1, z) == 0 ||
				   m_subsystemTerrain.Terrain.GetCellContents(x, y - 1, z) == 0 ||
				   m_subsystemTerrain.Terrain.GetCellContents(x, y, z + 1) == 0 ||
				   m_subsystemTerrain.Terrain.GetCellContents(x, y, z - 1) == 0;
		}
		#endregion

		#region Poison Explosion
		public void CreatePoisonExplosion()
		{
			if (m_exploded || m_componentBody == null) return;

			m_exploded = true;

			DifficultyMode difficulty = DifficultyMode.Normal;
			if (SubsystemGreenNightSky.Instance != null)
				difficulty = SubsystemGreenNightSky.Instance.DifficultyMode;

			float pressureMult = 1f, poisonIntensityMult = 1f, poisonRadiusMult = 1f;
			float lureRange = 25f;
			switch (difficulty)
			{
				case DifficultyMode.VeryEasy: pressureMult = 0.4f; poisonIntensityMult = 0.3f; poisonRadiusMult = 0.7f; lureRange = 8f; break;
				case DifficultyMode.Easy: pressureMult = 0.6f; poisonIntensityMult = 0.5f; poisonRadiusMult = 0.8f; lureRange = 15f; break;
				case DifficultyMode.Normal: pressureMult = 1.0f; poisonIntensityMult = 1.0f; poisonRadiusMult = 1.0f; lureRange = 25f; break;
				case DifficultyMode.Medium: pressureMult = 1.2f; poisonIntensityMult = 1.3f; poisonRadiusMult = 1.1f; lureRange = 35f; break;
				case DifficultyMode.Hard: pressureMult = 1.5f; poisonIntensityMult = 1.6f; poisonRadiusMult = 1.25f; lureRange = 45f; break;
				case DifficultyMode.Extreme: pressureMult = 2.0f; poisonIntensityMult = 2.0f; poisonRadiusMult = 1.5f; lureRange = 65f; break;
				case DifficultyMode.Impossible: pressureMult = 3.0f; poisonIntensityMult = 3.0f; poisonRadiusMult = 2.0f; lureRange = 120f; break;
			}

			float finalPressure = ExplosionPressure * pressureMult;
			float finalPoisonIntensity = PoisonIntensity * poisonIntensityMult;

			Vector3 position = m_componentBody.Position;
			int x = (int)MathUtils.Floor(position.X);
			int y = (int)MathUtils.Floor(position.Y);
			int z = (int)MathUtils.Floor(position.Z);

			if (m_subsystemPoisonExplosions != null)
			{
				m_subsystemPoisonExplosions.AddPoisonExplosion(x, y, z, finalPressure, finalPoisonIntensity, false);
			}
			else
			{
				CreatePoisonPressureEffect(position);
				float finalPoisonRadius = PoisonRadius * poisonRadiusMult;
				InfectNearbyEntities(position, finalPoisonIntensity, finalPoisonRadius);
			}

			if (m_subsystemAttractNoise != null)
			{
				m_subsystemAttractNoise.MakeLureNoise(position, 20f, lureRange);
			}
		}

		public void CreatePoisonPressureEffect(Vector3 center)
		{
			if (m_subsystemBodies == null) return;

			float radius = CloudRadius;
			float pressure = ExplosionPressure;

			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body == m_componentBody || body.Entity == null) continue;

				Vector3 bodyPos = body.Position;
				float distance = Vector3.Distance(bodyPos, center);

				if (distance <= radius && distance > 0.5f)
				{
					float forceMultiplier = 1f - (distance / radius);
					Vector3 direction = Vector3.Normalize(bodyPos - center);

					float force = pressure * forceMultiplier * 3f;
					body.ApplyImpulse(direction * force);
					body.ApplyImpulse(new Vector3(0f, force * 0.3f, 0f));
				}
			}
		}

		public void InfectNearbyEntities(Vector3 center, float poisonIntensity, float poisonRadius)
		{
			if (m_subsystemBodies == null || poisonRadius <= 0) return;

			float radiusSquared = poisonRadius * poisonRadius;

			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body == m_componentBody || body.Entity == null) continue;

				Vector3 offset = body.Position - center;
				float distanceSquared = offset.LengthSquared();

				if (distanceSquared <= radiusSquared)
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
								poisonInfected.m_InfectDuration = MathUtils.Max(
									poisonInfected.m_InfectDuration, finalIntensity);
							}
						}
					}
				}
			}
		}
		#endregion

		#region Frozen Explosion
		private void CreateFrozenExplosion()
		{
			if (m_exploded || m_componentBody == null)
				return;

			m_exploded = true;

			DifficultyMode difficulty = DifficultyMode.Normal;
			if (SubsystemGreenNightSky.Instance != null)
				difficulty = SubsystemGreenNightSky.Instance.DifficultyMode;

			float pressureMult = 1f, fluDurationMult = 1f;
			float lureRange = 15f;
			switch (difficulty)
			{
				case DifficultyMode.VeryEasy: pressureMult = 0.4f; fluDurationMult = 0.4f; lureRange = 5f; break;
				case DifficultyMode.Easy: pressureMult = 0.6f; fluDurationMult = 0.5f; lureRange = 10f; break;
				case DifficultyMode.Normal: pressureMult = 1.0f; fluDurationMult = 1.0f; lureRange = 15f; break;
				case DifficultyMode.Medium: pressureMult = 1.2f; fluDurationMult = 1.2f; lureRange = 22f; break;
				case DifficultyMode.Hard: pressureMult = 1.5f; fluDurationMult = 1.5f; lureRange = 35f; break;
				case DifficultyMode.Extreme: pressureMult = 2.0f; fluDurationMult = 2.0f; lureRange = 55f; break;
				case DifficultyMode.Impossible: pressureMult = 3.0f; fluDurationMult = 3.0f; lureRange = 100f; break;
			}

			float finalPressure = FreezePressure * pressureMult;
			float finalFluDuration = FluDuration * fluDurationMult;

			Vector3 position = m_componentBody.Position;
			int x = (int)MathUtils.Floor(position.X);
			int y = (int)MathUtils.Floor(position.Y);
			int z = (int)MathUtils.Floor(position.Z);

			if (m_subsystemFreezeExplosions != null)
			{
				m_subsystemFreezeExplosions.AddFreezeExplosion(x, y, z, finalPressure, finalFluDuration, NoExplosionSound);
			}

			if (m_subsystemAttractNoise != null)
			{
				m_subsystemAttractNoise.MakeLureNoise(position, 10f, lureRange);
			}
		}
		#endregion

		public override void OnEntityRemoved()
		{
			base.OnEntityRemoved();
			if (!PreventExplosion && !m_exploded && m_componentBody != null)
			{
				switch (m_explosionType)
				{
					case BoomerExplosionType.Normal:
						CreateNormalExplosion();
						break;
					case BoomerExplosionType.Fire:
						CreateFireExplosion();
						break;
					case BoomerExplosionType.Poison:
						CreatePoisonExplosion();
						break;
					case BoomerExplosionType.Frozen:
						CreateFrozenExplosion();
						break;
				}
			}
		}
	}
}
