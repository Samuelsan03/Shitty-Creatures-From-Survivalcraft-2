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
							m_playerImpactCount[targetEntity] = 0;
					}
					else
					{
						var infected = creature.Entity.FindComponent<ComponentFluInfected>();
						if (infected == null || !infected.IsInfected)
						{
							StartFluOnTarget(creature, 45f);
							creature.ComponentBody?.ApplyImpulse(new Vector3(
								m_random.Float(-0.5f, 0.5f),
								0.2f,
								m_random.Float(-0.5f, 0.5f)
							));
						}
					}
				}
			}

			// === EFECTO DE IMPACTO CON TRANSICIÓN (como el fuego) ===
			var impactParticles = new FreezingTrailParticleSystem(worldItem.Position, 0.6f, float.MaxValue);
			impactParticles.IsStopped = true;

			int particlesToSpawn = 25;
			for (int i = 0; i < impactParticles.Particles.Length && particlesToSpawn > 0; i++)
			{
				var p = impactParticles.Particles[i];
				if (!p.IsActive)
				{
					p.IsActive = true;
					p.Position = worldItem.Position + new Vector3(
						m_random.Float(-0.8f, 0.8f),
						m_random.Float(-0.2f, 0.8f),
						m_random.Float(-0.8f, 0.8f)
					);
					p.Color = new Color(200, 240, 255, 255);
					float s = 0.5f * m_random.Float(0.8f, 1.5f);
					p.Size = new Vector2(s, s);
					p.Velocity = new Vector3(
						m_random.Float(-0.2f, 0.2f),
						m_random.Float(0.3f, 0.8f),
						m_random.Float(-0.2f, 0.2f)
					);
					p.Time = 0f;
					p.TimeToLive = m_random.Float(1.5f, 3f);
					p.FlipX = (m_random.Int(0, 1) == 0);
					p.FlipY = (m_random.Int(0, 1) == 0);
					particlesToSpawn--;
				}
			}

			Project.FindSubsystem<SubsystemParticles>(true)?.AddParticleSystem(impactParticles, false);
			m_subsystemAudio.PlaySound("Audio/congelado", 3.0f, m_random.Float(-0.2f, 0.2f), worldItem.Position, 2f, true);

			return false;
		}

		private void ApplyFreezingEffectsToPlayer(ComponentPlayer player, int impactNumber)
		{
			// Impulso base en todos los impactos (como en el original)
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
					// Primer impacto: temperatura 4°C (hielo ~33%, visible pero no 50%)
					SetPlayerTemperature(player, 4f);
					break;

				case 2:
					// Segundo impacto: temperatura 3°C (hielo 50%)
					SetPlayerTemperature(player, 3f);
					player.ComponentBody?.ApplyImpulse(new Vector3(
						m_random.Float(-0.8f, 0.8f),
						0.3f,
						m_random.Float(-0.8f, 0.8f)
					));
					break;

				case 3:
					// Tercer impacto: gripe + temperatura 1°C (hielo ~83%)
					StartFluOnTarget(player, 45f);
					SetPlayerTemperature(player, 1f);
					player.ComponentBody?.ApplyImpulse(new Vector3(
						m_random.Float(-1.2f, 1.2f),
						0.5f,
						m_random.Float(-1.2f, 1.2f)
					));
					break;

				default: // impactNumber >= 4
						 // Cuarto impacto: mata por hipotermia (temperatura casi 0)
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
				player.ComponentVitalStats.Temperature = temperature;
		}

		private void KillTarget(ComponentCreature target)
		{
			if (target.ComponentHealth != null)
			{
				string causeOfDeath = LanguageControl.Get("Injury", "FrozenToDeath");
				target.ComponentHealth.Injure(1f, null, false, causeOfDeath);
			}
		}

		private void StartFluOnTarget(ComponentCreature target, float duration)
		{
			var targetFluInfected = target.Entity.FindComponent<ComponentFluInfected>();
			if (targetFluInfected != null)
			{
				targetFluInfected.StartFlu(duration);
				return;
			}

			if (target is ComponentPlayer player && player.ComponentFlu != null)
			{
				if (!player.ComponentFlu.HasFlu)
					player.ComponentFlu.StartFlu();
				var fluField = typeof(ComponentFlu).GetField("m_fluDuration",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				fluField?.SetValue(player.ComponentFlu, duration);
			}
		}
	}
}
