using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemPoisonExplosions : Subsystem, IUpdateable
	{
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public void AddPoisonExplosion(int x, int y, int z, float pressure, float poisonIntensity, bool noExplosionSound)
		{
			if (pressure > 0f)
			{
				this.m_queuedExplosions.Add(new SubsystemPoisonExplosions.PoisonExplosionData
				{
					X = x,
					Y = y,
					Z = z,
					Pressure = pressure,
					PoisonIntensity = poisonIntensity,
					NoExplosionSound = noExplosionSound
				});
			}
		}

		public virtual void Update(float dt)
		{
			if (this.m_queuedExplosions.Count <= 0)
			{
				return;
			}

			List<SubsystemPoisonExplosions.PoisonExplosionData> processedExplosions = new List<SubsystemPoisonExplosions.PoisonExplosionData>();

			foreach (SubsystemPoisonExplosions.PoisonExplosionData explosionData in this.m_queuedExplosions)
			{
				this.ProcessPoisonExplosion(explosionData.X, explosionData.Y, explosionData.Z,
					explosionData.Pressure, explosionData.PoisonIntensity, explosionData.NoExplosionSound);
				processedExplosions.Add(explosionData);
			}

			// Remover explosiones procesadas
			foreach (var explosion in processedExplosions)
			{
				this.m_queuedExplosions.Remove(explosion);
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_poisonExplosionParticleSystem = new PoisonExplosionParticleSystem();
			this.m_subsystemParticles.AddParticleSystem(this.m_poisonExplosionParticleSystem, false);
		}

		public void ProcessPoisonExplosion(int x, int y, int z, float pressure, float poisonIntensity, bool noExplosionSound)
		{
			// Radio de efecto del veneno
			int radius = (int)MathUtils.Clamp(pressure / 10f, 3f, 10f);

			// Crear partículas de explosión de veneno usando PoisonExplosionParticleSystem
			for (int i = -radius; i <= radius; i++)
			{
				for (int j = -radius; j <= radius; j++)
				{
					for (int k = -radius; k <= radius; k++)
					{
						float distance = MathUtils.Sqrt(i * i + j * j + k * k);
						if (distance <= radius)
						{
							float strength = MathUtils.Max(0f, 1f - distance / radius) * pressure / 50f;
							if (strength > 0.1f)
							{
								this.m_poisonExplosionParticleSystem.SetExplosionCell(
									new Point3(x + i, y + j, z + k), strength);
							}
						}
					}
				}
			}

			// Aplicar efectos de veneno a entidades en el área
			this.ApplyPoisonToEntities(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), radius, poisonIntensity);

			// Reproducir sonido
			if (!noExplosionSound)
			{
				Vector3 position = new Vector3((float)x, (float)y, (float)z);
				float delay = this.m_subsystemAudio.CalculateDelay(0f);
				this.m_subsystemAudio.PlaySound("Audio/Explosion De Mierda/Explosion Smoke",
					1f, this.m_random.Float(-0.1f, 0.1f), position, 15f, delay);
			}
		}

		public void ApplyPoisonToEntities(Vector3 center, float radius, float poisonIntensity)
		{
			if (!this.m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
			{
				return;
			}

			foreach (ComponentBody componentBody in this.m_subsystemBodies.Bodies)
			{
				float distance = Vector3.Distance(componentBody.Position, center);
				if (distance <= radius)
				{
					// Aplicar veneno basado en la distancia
					float intensityMultiplier = MathUtils.Max(0f, 1f - distance / radius);
					float appliedIntensity = poisonIntensity * intensityMultiplier;

					ComponentCreature componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						// Para jugadores
						ComponentPlayer componentPlayer = componentCreature as ComponentPlayer;
						if (componentPlayer != null)
						{
							if (!componentPlayer.ComponentSickness.IsSick)
							{
								componentPlayer.ComponentSickness.StartSickness();
							}
							componentPlayer.ComponentSickness.m_sicknessDuration = MathUtils.Max(
								componentPlayer.ComponentSickness.m_sicknessDuration,
								appliedIntensity);
						}
						// Para otras criaturas
						else
						{
							ComponentPoisonInfected componentPoisonInfected = componentCreature.Entity.FindComponent<ComponentPoisonInfected>();
							if (componentPoisonInfected != null)
							{
								if (!componentPoisonInfected.IsInfected || componentPoisonInfected.m_InfectDuration < appliedIntensity)
								{
									componentPoisonInfected.StartInfect(appliedIntensity);
								}
							}
							else if (componentCreature.ComponentHealth != null && appliedIntensity > 30f)
							{
								float damage = MathUtils.Min(0.5f, appliedIntensity / 100f);
								componentCreature.ComponentHealth.Injure(damage, null, false, "PoisonExplosion");

								if (componentCreature.ComponentCreatureSounds != null)
								{
									componentCreature.ComponentCreatureSounds.PlayPainSound();
								}
							}
						}
					}
				}
			}

			// También afectar a objetos recogibles
			foreach (Pickable pickable in this.m_subsystemPickables.Pickables)
			{
				float distance = Vector3.Distance(pickable.Position + new Vector3(0f, 0.5f, 0f), center);
				if (distance <= radius)
				{
					// Aplicar pequeño impulso a objetos
					Vector3 direction = Vector3.Normalize(pickable.Position - center);
					float force = MathUtils.Max(0f, 1f - distance / radius) * 2f;
					pickable.Velocity += direction * force;
				}
			}
		}

		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemPickables m_subsystemPickables;
		public SubsystemProjectiles m_subsystemProjectiles;
		public SubsystemGameInfo m_subsystemGameInfo;
		public List<SubsystemPoisonExplosions.PoisonExplosionData> m_queuedExplosions = new List<SubsystemPoisonExplosions.PoisonExplosionData>();
		public Random m_random = new Random();
		public PoisonExplosionParticleSystem m_poisonExplosionParticleSystem;

		public struct PoisonExplosionData
		{
			public int X;
			public int Y;
			public int Z;
			public float Pressure;
			public float PoisonIntensity;
			public bool NoExplosionSound;
		}
	}
}
