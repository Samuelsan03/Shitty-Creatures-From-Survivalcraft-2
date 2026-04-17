// SubsystemFreezeExplosions.cs (corregido)
using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFreezeExplosions : Subsystem, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemPickables m_subsystemPickables;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemPlayers m_subsystemPlayers;
		private Random m_random = new Random();
		private FreezeExplosionParticleSystem m_freezeExplosionParticleSystem;
		private List<FreezeExplosionData> m_queuedExplosions = new List<FreezeExplosionData>();

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);

			m_freezeExplosionParticleSystem = new FreezeExplosionParticleSystem();
			m_subsystemParticles.AddParticleSystem(m_freezeExplosionParticleSystem);
		}

		public void AddFreezeExplosion(int x, int y, int z, float pressure, float fluDuration, bool noExplosionSound)
		{
			if (pressure > 0f)
			{
				m_queuedExplosions.Add(new FreezeExplosionData
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

		public void Update(float dt)
		{
			if (m_queuedExplosions.Count == 0) return;

			List<FreezeExplosionData> processed = new List<FreezeExplosionData>();
			foreach (var data in m_queuedExplosions)
			{
				ProcessFreezeExplosion(data.X, data.Y, data.Z, data.Pressure, data.FluDuration, data.NoExplosionSound);
				processed.Add(data);
			}
			foreach (var p in processed)
				m_queuedExplosions.Remove(p);
		}

		private void ProcessFreezeExplosion(int x, int y, int z, float pressure, float fluDuration, bool noExplosionSound)
		{
			int radius = (int)MathUtils.Clamp(pressure / 10f, 3f, 10f);
			Vector3 center = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);

			// Partículas
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					for (int dz = -radius; dz <= radius; dz++)
					{
						float dist = MathUtils.Sqrt(dx * dx + dy * dy + dz * dz);
						if (dist <= radius)
						{
							float strength = MathUtils.Max(0f, 1f - dist / radius) * pressure / 50f;
							if (strength > 0.1f)
							{
								m_freezeExplosionParticleSystem.SetExplosionCell(new Point3(x + dx, y + dy, z + dz), strength);
							}
						}
					}
				}
			}

			// Aplicar efectos a entidades (solo dentro del radio)
			ApplyFreezeEffects(center, radius, fluDuration);

			// Sonido
			if (!noExplosionSound)
			{
				Vector3 pos = new Vector3(x, y, z);
				float delay = m_subsystemAudio.CalculateDelay(0f);
				m_subsystemAudio.PlaySound("Audio/explosion congelante", 1f, m_random.Float(-0.1f, 0.1f), pos, 15f, delay);
			}
		}

		private void ApplyFreezeEffects(Vector3 center, float radius, float fluDuration)
		{
			if (!m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
				return;

			// Afectar cuerpos (criaturas, jugadores) solo si están dentro del radio
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				float dist = Vector3.Distance(body.Position, center);
				if (dist <= radius)
				{
					float intensity = MathUtils.Max(0f, 1f - dist / radius);
					// Duración más larga (2.5 veces la original)
					float appliedDuration = fluDuration * intensity * 2.5f;

					Entity entity = body.Entity;
					ComponentCreature creature = entity.FindComponent<ComponentCreature>();
					if (creature != null)
					{
						// Jugador
						ComponentPlayer player = creature as ComponentPlayer;
						if (player != null)
						{
							ComponentFlu flu = entity.FindComponent<ComponentFlu>();
							if (flu != null && !flu.HasFlu)
								flu.StartFlu();

							ComponentVitalStats vital = entity.FindComponent<ComponentVitalStats>();
							if (vital != null)
								vital.Temperature = MathUtils.Max(vital.Temperature - 8f * intensity, 0f);
						}
						// Otras criaturas - solo efectos de gripe, sin daño
						else
						{
							ComponentFluInfected infected = entity.FindComponent<ComponentFluInfected>();
							if (infected != null)
							{
								if (!infected.IsInfected || infected.m_fluDuration < appliedDuration)
									infected.StartFlu(appliedDuration);
							}
							// Se eliminó el daño por salud (Injure)
						}
					}
				}
			}

			// Se eliminó completamente el bloque que afectaba objetos recogibles (Pickable)
		}

		private struct FreezeExplosionData
		{
			public int X, Y, Z;
			public float Pressure;
			public float FluDuration;
			public bool NoExplosionSound;
		}
	}
}
