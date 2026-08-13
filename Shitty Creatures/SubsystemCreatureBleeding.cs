using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemCreatureBleeding : Subsystem, IUpdateable
	{
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;

		private Dictionary<ComponentCreature, BloodParticleSystem> m_bleedingCreatures = new Dictionary<ComponentCreature, BloodParticleSystem>();
		private List<ComponentCreature> m_creaturesToStopBleeding = new List<ComponentCreature>();

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
		}

		public override void OnEntityRemoved(Entity entity)
		{
			// Al desaparecer el cuerpo, desaparece el sangrado
			ComponentCreature creature = entity.FindComponent<ComponentCreature>();
			if (creature != null)
			{
				StopBleeding(creature);
			}
		}

		public virtual void Update(float dt)
		{
			m_creaturesToStopBleeding.Clear();

			if (m_subsystemCreatureSpawn == null)
			{
				return;
			}

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature == null || creature.Entity == null)
				{
					continue;
				}

				ComponentHealth componentHealth = creature.ComponentHealth;
				if (componentHealth == null)
				{
					continue;
				}

				float health = componentHealth.Health;

				// Condición de sangrado: vida <= 0.2 (incluye criaturas muertas)
				if (health <= 0.2f)
				{
					// Si ya está sangrando, actualizar posición del efecto
					if (m_bleedingCreatures.TryGetValue(creature, out BloodParticleSystem bloodPs))
					{
						if (bloodPs != null && creature.ComponentBody != null)
						{
							bloodPs.Position = creature.ComponentBody.Position;
						}
					}
					// Si no está sangrando, comenzar el efecto
					else
					{
						StartBleeding(creature);
					}
				}
				// Si la vida es mayor a 0.2, la criatura se recuperó - detener sangrado
				else
				{
					if (m_bleedingCreatures.ContainsKey(creature))
					{
						m_creaturesToStopBleeding.Add(creature);
					}
				}
			}

			// Detener el sangrado de criaturas que ya no deben sangrar
			foreach (ComponentCreature creature in m_creaturesToStopBleeding)
			{
				StopBleeding(creature);
			}
		}

		private void StartBleeding(ComponentCreature creature)
		{
			if (creature.ComponentBody == null || m_subsystemParticles == null || m_subsystemTerrain == null)
			{
				return;
			}

			BloodParticleSystem bloodParticleSystem = new BloodParticleSystem(m_subsystemTerrain);
			bloodParticleSystem.Position = creature.ComponentBody.Position;
			m_subsystemParticles.AddParticleSystem(bloodParticleSystem, false);
			m_bleedingCreatures.Add(creature, bloodParticleSystem);
		}

		private void StopBleeding(ComponentCreature creature)
		{
			if (m_bleedingCreatures.TryGetValue(creature, out BloodParticleSystem bloodParticleSystem))
			{
				if (bloodParticleSystem != null)
				{
					bloodParticleSystem.IsStopped = true;
				}
				m_bleedingCreatures.Remove(creature);
			}
		}

		public override void Dispose()
		{
			foreach (var kvp in m_bleedingCreatures)
			{
				if (kvp.Value != null)
				{
					kvp.Value.IsStopped = true;
				}
			}
			m_bleedingCreatures.Clear();
			base.Dispose();
		}
	}
}
