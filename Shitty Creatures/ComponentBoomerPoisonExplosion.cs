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
		public float PoisonRadius = 15f;
		public float PoisonIntensity = 300f;
		public float CloudDuration = 20.0f;
		public float CloudRadius = 12f;
		public float ExplosionPressure = 40f;

		public bool PreventExplosion = false;

		public SubsystemAudio m_subsystemAudio;
		public SubsystemTime m_subsystemTime;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemExplosions m_subsystemExplosions;
		public SubsystemPoisonExplosions m_subsystemPoisonExplosions;
		public ComponentHealth m_componentHealth;
		public ComponentBody m_componentBody;

		public bool m_exploded = false;
		public float m_lastHealth = 0f;
		public Random m_random = new Random();

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			PoisonRadius = valuesDictionary.GetValue<float>("PoisonRadius", PoisonRadius);
			PoisonIntensity = valuesDictionary.GetValue<float>("PoisonIntensity", PoisonIntensity);
			CloudDuration = valuesDictionary.GetValue<float>("CloudDuration", CloudDuration);
			CloudRadius = valuesDictionary.GetValue<float>("CloudRadius", CloudRadius);
			ExplosionPressure = valuesDictionary.GetValue<float>("ExplosionPressure", ExplosionPressure);

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

			PoisonRadius = MathUtils.Clamp(PoisonRadius, 1f, 20f);
			PoisonIntensity = MathUtils.Clamp(PoisonIntensity, 10f, 600f);
			CloudDuration = MathUtils.Clamp(CloudDuration, 2f, 60f);
			CloudRadius = MathUtils.Clamp(CloudRadius, 2f, 15f);
			ExplosionPressure = MathUtils.Clamp(ExplosionPressure, 10f, 100f);
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
				CreatePoisonExplosion();
			}

			m_lastHealth = m_componentHealth.Health;
		}

		public void CreatePoisonExplosion()
		{
			if (m_exploded || m_componentBody == null) return;

			m_exploded = true;

			// Escalar por dificultad
			DifficultyMode difficulty = DifficultyMode.Normal;
			if (SubsystemGreenNightSky.Instance != null)
				difficulty = SubsystemGreenNightSky.Instance.DifficultyMode;

			float pressureMult = 1f, poisonIntensityMult = 1f, poisonRadiusMult = 1f;
			switch (difficulty)
			{
				case DifficultyMode.Easy:
					pressureMult = 0.6f;
					poisonIntensityMult = 0.5f;
					poisonRadiusMult = 0.8f;
					break;
				case DifficultyMode.Normal:
					pressureMult = 1.0f;
					poisonIntensityMult = 1.0f;
					poisonRadiusMult = 1.0f;
					break;
				case DifficultyMode.Medium:
					pressureMult = 1.2f;
					poisonIntensityMult = 1.3f;
					poisonRadiusMult = 1.1f;
					break;
				case DifficultyMode.Hard:
					pressureMult = 1.5f;
					poisonIntensityMult = 1.6f;
					poisonRadiusMult = 1.25f;
					break;
				case DifficultyMode.Extreme:
					pressureMult = 2.0f;
					poisonIntensityMult = 2.0f;
					poisonRadiusMult = 1.5f;
					break;
			}

			float finalPressure = ExplosionPressure * pressureMult;
			float finalPoisonIntensity = PoisonIntensity * poisonIntensityMult;

			Vector3 position = m_componentBody.Position;
			int x = (int)MathUtils.Floor(position.X);
			int y = (int)MathUtils.Floor(position.Y);
			int z = (int)MathUtils.Floor(position.Z);

			if (m_subsystemPoisonExplosions != null)
			{
				// Usar la firma correcta: AddPoisonExplosion(x, y, z, pressure, poisonIntensity, noExplosionSound)
				m_subsystemPoisonExplosions.AddPoisonExplosion(x, y, z, finalPressure, finalPoisonIntensity, false);
			}
			else
			{
				CreatePoisonExplosionLegacy(position, finalPoisonIntensity, poisonRadiusMult);
			}
		}

		public void CreatePoisonExplosionLegacy(Vector3 position, float poisonIntensity, float radiusMult)
		{
			if (m_subsystemAudio != null)
			{
				m_subsystemAudio.PlaySound("Audio/Explosion De Mierda/Smoke Explosion", 1f, 0f, position, 15f, true);
			}

			CreatePressureEffect(position);
			float finalPoisonRadius = PoisonRadius * radiusMult;
			InfectNearbyEntities(position, poisonIntensity, finalPoisonRadius);
		}

		public void CreatePressureEffect(Vector3 center)
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

		public override void OnEntityRemoved()
		{
			base.OnEntityRemoved();
			if (!PreventExplosion && !m_exploded && m_componentBody != null)
			{
				CreatePoisonExplosion();
			}
		}
	}
}
