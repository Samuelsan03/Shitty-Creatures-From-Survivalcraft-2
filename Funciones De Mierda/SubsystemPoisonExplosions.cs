using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000375 RID: 885
	public class SubsystemPoisonExplosions : Subsystem, IUpdateable
	{
		// Token: 0x17000435 RID: 1077
		// (get) Token: 0x06001CD3 RID: 7379 RVA: 0x000DE24C File Offset: 0x000DC44C
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x06001CD4 RID: 7380 RVA: 0x000DE250 File Offset: 0x000DC450
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
				this.ApplyPoisonEffect(new Vector3((float)x + 0.5f, (float)y + 0.5f, (float)z + 0.5f), pressure, poisonIntensity);
			}
		}

		// Token: 0x06001CD5 RID: 7381 RVA: 0x000DE2D4 File Offset: 0x000DC4D4
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

		// Token: 0x06001CD6 RID: 7382 RVA: 0x000DE384 File Offset: 0x000DC584
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

		// Token: 0x06001CD7 RID: 7383 RVA: 0x000DE450 File Offset: 0x000DC650
		public void ProcessPoisonExplosion(int x, int y, int z, float pressure, float poisonIntensity, bool noExplosionSound)
		{
			// Radio de efecto del veneno (ajustable)
			int radius = (int)MathUtils.Clamp(pressure / 10f, 3f, 10f);

			// Crear partículas de explosión de veneno
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

		// Token: 0x06001CD8 RID: 7384 RVA: 0x000DE594 File Offset: 0x000DC794
		public void ApplyPoisonEffect(Vector3 center, float pressure, float poisonIntensity)
		{
			// Sacudir cuerpos cercanos
			foreach (ComponentBody componentBody in this.m_subsystemBodies.Bodies)
			{
				float distance = Vector3.Distance(componentBody.Position, center);
				if (distance < 10f)
				{
					// Aplicar pequeño impulso
					Vector3 direction = Vector3.Normalize(componentBody.Position - center);
					float force = MathUtils.Max(0f, 1f - distance / 10f) * pressure * 0.5f;
					componentBody.ApplyImpulse(direction * force);
				}
			}
		}

		// Token: 0x06001CD9 RID: 7385 RVA: 0x000DE62C File Offset: 0x000DC82C
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
						// Para otras criaturas - solo si ya tienen el componente ComponentPoisonInfected
						else
						{
							ComponentPoisonInfected componentPoisonInfected = componentCreature.Entity.FindComponent<ComponentPoisonInfected>();
							if (componentPoisonInfected != null)
							{
								// Si ya tiene el componente, actualizar la duración
								if (!componentPoisonInfected.IsInfected || componentPoisonInfected.m_InfectDuration < appliedIntensity)
								{
									componentPoisonInfected.StartInfect(appliedIntensity);
								}
							}
							// Para criaturas sin ComponentPoisonInfected, aplicar daño directo
							else if (componentCreature.ComponentHealth != null && appliedIntensity > 30f)
							{
								float damage = MathUtils.Min(0.5f, appliedIntensity / 100f);
								componentCreature.ComponentHealth.Injure(damage, null, false, "PoisonExplosion");

								// Reproducir sonido de dolor si es posible
								if (componentCreature.ComponentCreatureSounds != null)
								{
									componentCreature.ComponentCreatureSounds.PlayPainSound();
								}
							}
						}

						// Efecto visual adicional - partículas de veneno alrededor de la criatura
						if (appliedIntensity > 50f)
						{
							for (int i = 0; i < 5; i++)
							{
								Vector3 particlePos = componentBody.Position +
									new Vector3(
										this.m_random.Float(-0.5f, 0.5f),
										this.m_random.Float(0f, 1.5f),
										this.m_random.Float(-0.5f, 0.5f));

								this.m_poisonExplosionParticleSystem.SetExplosionCell(
									Terrain.ToCell(particlePos),
									this.m_random.Float(0.3f, 0.7f));
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

		// Token: 0x040013A4 RID: 5028
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x040013A5 RID: 5029
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x040013A6 RID: 5030
		public SubsystemParticles m_subsystemParticles;

		// Token: 0x040013A7 RID: 5031
		public SubsystemBodies m_subsystemBodies;

		// Token: 0x040013A8 RID: 5032
		public SubsystemPickables m_subsystemPickables;

		// Token: 0x040013A9 RID: 5033
		public SubsystemProjectiles m_subsystemProjectiles;

		// Token: 0x040013AA RID: 5034
		public SubsystemGameInfo m_subsystemGameInfo;

		// Token: 0x040013AB RID: 5035
		public List<SubsystemPoisonExplosions.PoisonExplosionData> m_queuedExplosions = new List<SubsystemPoisonExplosions.PoisonExplosionData>();

		// Token: 0x040013AC RID: 5036
		public Random m_random = new Random();

		// Token: 0x040013AD RID: 5037
		public PoisonExplosionParticleSystem m_poisonExplosionParticleSystem;

		// Token: 0x02000688 RID: 1672
		public struct PoisonExplosionData
		{
			// Token: 0x0400209B RID: 8347
			public int X;

			// Token: 0x0400209C RID: 8348
			public int Y;

			// Token: 0x0400209D RID: 8349
			public int Z;

			// Token: 0x0400209E RID: 8350
			public float Pressure;

			// Token: 0x0400209F RID: 8351
			public float PoisonIntensity;

			// Token: 0x040020A0 RID: 8352
			public bool NoExplosionSound;
		}
	}
}
