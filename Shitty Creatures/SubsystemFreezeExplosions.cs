using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFreezeExplosions : Subsystem, IUpdateable
	{
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public void AddFreezeExplosion(int x, int y, int z, float pressure, float fluDuration, bool noExplosionSound)
		{
			if (pressure > 0f)
			{
				this.m_queuedExplosions.Add(new SubsystemFreezeExplosions.FreezeExplosionData
				{
					X = x,
					Y = y,
					Z = z,
					Pressure = pressure,
					FluDuration = fluDuration,
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
				SubsystemFreezeExplosions.FreezeExplosionData freezeExplosionData = this.m_queuedExplosions[i];
				this.m_queuedExplosions.RemoveAt(i);
				this.ProcessFreezeExplosion(freezeExplosionData.X, freezeExplosionData.Y, freezeExplosionData.Z, freezeExplosionData.Pressure, freezeExplosionData.FluDuration, freezeExplosionData.NoExplosionSound);
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_freezeExplosionParticleSystem = new FreezeExplosionParticleSystem();
			this.m_subsystemParticles.AddParticleSystem(this.m_freezeExplosionParticleSystem, false);
		}

		public void ProcessFreezeExplosion(int x, int y, int z, float pressure, float fluDuration, bool noExplosionSound)
		{
			int radius = (int)MathUtils.Clamp(pressure / 10f, 3f, 10f);
			Vector3 center = new Vector3((float)x + 0.5f, (float)y + 0.5f, (float)z + 0.5f);
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
								this.m_freezeExplosionParticleSystem.SetExplosionCell(new Point3(x + dx, y + dy, z + dz), strength);
							}
						}
					}
				}
			}
			this.ApplyFreezeEffects(center, radius, fluDuration);
			if (!noExplosionSound)
			{
				Vector3 position = new Vector3((float)x, (float)y, (float)z);
				float delay = this.m_subsystemAudio.CalculateDelay(0f);
				this.m_subsystemAudio.PlaySound("Audio/explosion congelante", 1f, this.m_random.Float(-0.1f, 0.1f), position, 15f, delay);
			}
		}

		public void ApplyFreezeEffects(Vector3 center, float radius, float fluDuration)
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
					float intensity = MathUtils.Max(0f, 1f - dist / radius);
					float appliedDuration = fluDuration * intensity * 2.5f;
					Entity entity = componentBody.Entity;
					ComponentCreature creature = entity.FindComponent<ComponentCreature>();
					if (creature != null)
					{
						ComponentPlayer player = creature as ComponentPlayer;
						if (player != null)
						{
							ComponentFlu flu = entity.FindComponent<ComponentFlu>();
							if (flu != null && !flu.HasFlu)
							{
								flu.StartFlu();
							}
							ComponentVitalStats vital = entity.FindComponent<ComponentVitalStats>();
							if (vital != null)
							{
								vital.Temperature = MathUtils.Max(vital.Temperature - 8f * intensity, 0f);
							}
						}
						else
						{
							ComponentFluInfected infected = entity.FindComponent<ComponentFluInfected>();
							if (infected != null)
							{
								if (!infected.IsInfected || infected.m_fluDuration < appliedDuration)
								{
									infected.StartFlu(appliedDuration);
								}
							}
						}
					}
				}
			}
		}

		public SubsystemAudio m_subsystemAudio;

		public SubsystemParticles m_subsystemParticles;

		public SubsystemBodies m_subsystemBodies;

		public SubsystemGameInfo m_subsystemGameInfo;

		public List<SubsystemFreezeExplosions.FreezeExplosionData> m_queuedExplosions = new List<SubsystemFreezeExplosions.FreezeExplosionData>();

		public Random m_random = new Random();

		public FreezeExplosionParticleSystem m_freezeExplosionParticleSystem;

		public struct FreezeExplosionData
		{
			public int X;

			public int Y;

			public int Z;

			public float Pressure;

			public float FluDuration;

			public bool NoExplosionSound;
		}
	}
}
