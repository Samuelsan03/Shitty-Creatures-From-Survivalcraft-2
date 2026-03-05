using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFreezingSnowballBlockBehavior : SubsystemBlockBehavior
	{
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemTime m_subsystemTime;
		private SubsystemTerrain m_subsystemTerrain;
		private Random m_random = new Random();

		// Contador de impactos solo para jugadores
		private Dictionary<Entity, int> m_playerImpactCount = new Dictionary<Entity, int>();

		public override int[] HandledBlocks => new int[] { FreezingSnowballBlock.Index };

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			// Estelas más grandes y vistosas
			var particleSystem = new FreezingTrailParticleSystem(projectile.Position, 0.5f, float.MaxValue);
			m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, particleSystem);

			var particleSystem2 = new FreezingTrailParticleSystem(projectile.Position, 0.3f, float.MaxValue);
			m_subsystemProjectiles.AddTrail(projectile, new Vector3(0f, 0.05f, 0f), particleSystem2);

			m_subsystemAudio.PlaySound("Audio/Throw", 0.3f, m_random.Float(-0.2f, 0.2f), projectile.Position, 2f, true);
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			if (componentBody != null)
			{
				ComponentCreature creature = componentBody.Entity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					if (creature is ComponentPlayer player)
					{
						// === Lógica para JUGADOR: progresión de 4 impactos ===
						Entity targetEntity = player.Entity;
						int impactNumber = 0;
						if (m_playerImpactCount.ContainsKey(targetEntity))
						{
							impactNumber = m_playerImpactCount[targetEntity];
							m_playerImpactCount[targetEntity] = impactNumber + 1;
						}
						else
						{
							m_playerImpactCount.Add(targetEntity, 1);
							impactNumber = 1;
						}

						ApplyFreezingEffectsToPlayer(player, impactNumber);

						if (impactNumber >= 4)
						{
							m_playerImpactCount[targetEntity] = 0; // Reiniciar ciclo
						}
					}
					else
					{
						// === Lógica para CRIATURA: un impacto = gripe (si no infectada) ===
						var infected = creature.Entity.FindComponent<ComponentFluInfected>();
						if (infected == null || !infected.IsInfected)
						{
							StartFluOnTarget(creature, 45f); // Duración de gripe para NPC
															 // Pequeño empuje para feedback visual
							creature.ComponentBody?.ApplyImpulse(new Vector3(
								m_random.Float(-0.5f, 0.5f),
								0.2f,
								m_random.Float(-0.5f, 0.5f)
							));
						}
					}
				}
			}

			// Efecto visual de impacto (común para todos)
			var impactParticles = new FreezingTrailParticleSystem(worldItem.Position, 0.5f, float.MaxValue);
			impactParticles.IsStopped = true;
			Project.FindSubsystem<SubsystemParticles>(true)?.AddParticleSystem(impactParticles, false);

			m_subsystemAudio.PlaySound("Audio/congelado", 1.0f, m_random.Float(-0.2f, 0.2f), worldItem.Position, 2f, true);

			return false;
		}

		// Aplica la progresión de efectos al jugador según el número de impacto
		private void ApplyFreezingEffectsToPlayer(ComponentPlayer player, int impactNumber)
		{
			// Empuje común en todos los impactos
			if (player.ComponentBody != null)
			{
				player.ComponentBody.ApplyImpulse(new Vector3(
					m_random.Float(-0.3f, 0.3f),
					0.1f,
					m_random.Float(-0.3f, 0.3f)
				));
			}

			switch (impactNumber)
			{
				case 1:
					// Solo frío (temperatura baja, sin congelamiento visible)
					SetPlayerTemperature(player, 6f);
					break;

				case 2:
					// Congelamiento parcial: pantalla congelada ~50%
					SetPlayerTemperature(player, 3f);
					player.ComponentBody?.ApplyImpulse(new Vector3(
						m_random.Float(-0.8f, 0.8f),
						0.3f,
						m_random.Float(-0.8f, 0.8f)
					));
					break;

				case 3:
					// Gripe + frío extremo
					StartFluOnTarget(player, 45f);
					SetPlayerTemperature(player, 1f);
					player.ComponentBody?.ApplyImpulse(new Vector3(
						m_random.Float(-1.2f, 1.2f),
						0.5f,
						m_random.Float(-1.2f, 1.2f)
					));
					break;

				default: // 4 o más
						 // Muerte por hipotermia
					SetPlayerTemperature(player, 0.1f);
					KillTarget(player);
					player.ComponentBody?.ApplyImpulse(new Vector3(
						m_random.Float(-1.5f, 1.5f),
						0.7f,
						m_random.Float(-1.5f, 1.5f)
					));
					break;
			}
		}

		private void SetPlayerTemperature(ComponentPlayer player, float temperature)
		{
			if (player.ComponentVitalStats != null)
			{
				player.ComponentVitalStats.Temperature = temperature;
			}
		}

		private void KillTarget(ComponentCreature target)
		{
			if (target.ComponentHealth != null)
			{
				// Causa de muerte localizada (NO ELIMINAR)
				string causeOfDeath = LanguageControl.Get("Injury", "FrozenToDeath");
				target.ComponentHealth.Injure(1f, null, false, causeOfDeath);
			}
		}

		private void StartFluOnTarget(ComponentCreature target, float duration)
		{
			// Intentar con ComponentFluInfected (criaturas)
			var targetFluInfected = target.Entity.FindComponent<ComponentFluInfected>();
			if (targetFluInfected != null)
			{
				targetFluInfected.StartFlu(duration);
				return;
			}

			// Para jugador: usar ComponentFlu
			if (target is ComponentPlayer player && player.ComponentFlu != null)
			{
				if (!player.ComponentFlu.HasFlu)
				{
					player.ComponentFlu.StartFlu(); // Pone 900s
				}
				// Sobrescribir duración mediante reflexión
				var fluField = typeof(ComponentFlu).GetField("m_fluDuration",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				fluField?.SetValue(player.ComponentFlu, duration);
			}
		}
	}
}
