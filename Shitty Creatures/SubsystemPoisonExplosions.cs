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
			int i = 0;
			while (i < this.m_queuedExplosions.Count)
			{
				SubsystemPoisonExplosions.PoisonExplosionData poisonExplosionData = this.m_queuedExplosions[i];
				this.m_queuedExplosions.RemoveAt(i);
				this.ProcessPoisonExplosion(poisonExplosionData.X, poisonExplosionData.Y, poisonExplosionData.Z, poisonExplosionData.Pressure, poisonExplosionData.PoisonIntensity, poisonExplosionData.NoExplosionSound);
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_poisonExplosionParticleSystem = new PoisonExplosionParticleSystem();
			this.m_subsystemParticles.AddParticleSystem(this.m_poisonExplosionParticleSystem, false);
		}

		public void ProcessPoisonExplosion(int x, int y, int z, float pressure, float poisonIntensity, bool noExplosionSound)
		{
			int radius = (int)MathUtils.Clamp(pressure / 10f, 3f, 10f);
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					for (int dz = -radius; dz <= radius; dz++)
					{
						float dist = MathUtils.Sqrt((float)(dx * dx + dy * dy + dz * dz));
						if (dist <= (float)radius)
						{
							float strength = MathUtils.Max(0f, 1f - dist / (float)radius) * pressure / 50f;
							if (strength > 0.1f)
							{
								this.m_poisonExplosionParticleSystem.SetExplosionCell(new Point3(x + dx, y + dy, z + dz), strength);
							}
						}
					}
				}
			}
			this.ApplyPoisonToEntities(new Vector3((float)x + 0.5f, (float)y + 0.5f, (float)z + 0.5f), radius, poisonIntensity);
			if (!noExplosionSound)
			{
				Vector3 position = new Vector3((float)x, (float)y, (float)z);
				float delay = this.m_subsystemAudio.CalculateDelay(0f);
				this.m_subsystemAudio.PlaySound("Audio/Explosion De Mierda/Explosion Smoke", 1f, this.m_random.Float(-0.1f, 0.1f), position, 15f, delay);
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
				float dist = Vector3.Distance(componentBody.Position, center);
				if (dist <= radius)
				{
					float intensityMultiplier = MathUtils.Max(0f, 1f - dist / radius);
					float appliedIntensity = poisonIntensity * intensityMultiplier;
					ComponentCreature componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						ComponentPlayer componentPlayer = componentCreature as ComponentPlayer;
						if (componentPlayer != null)
						{
							if (!componentPlayer.ComponentSickness.IsSick)
							{
								componentPlayer.ComponentSickness.StartSickness();
							}
							componentPlayer.ComponentSickness.m_sicknessDuration = MathUtils.Max(componentPlayer.ComponentSickness.m_sicknessDuration, appliedIntensity);
						}
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
			foreach (Pickable pickable in this.m_subsystemPickables.Pickables)
			{
				float dist = Vector3.Distance(pickable.Position + new Vector3(0f, 0.5f, 0f), center);
				if (dist <= radius)
				{
					Vector3 direction = Vector3.Normalize(pickable.Position - center);
					float force = MathUtils.Max(0f, 1f - dist / radius) * 2f;
					pickable.Velocity += direction * force;
				}
			}
		}

		public SubsystemAudio m_subsystemAudio;

		public SubsystemParticles m_subsystemParticles;

		public SubsystemBodies m_subsystemBodies;

		public SubsystemPickables m_subsystemPickables;

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
