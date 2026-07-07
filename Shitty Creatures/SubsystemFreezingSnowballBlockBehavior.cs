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
		private Dictionary<Projectile, List<FreezingTrailParticleSystem>> m_projectileTrails = new Dictionary<Projectile, List<FreezingTrailParticleSystem>>();

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
			m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new FreezingTrailParticleSystem(120, 1.2f, float.MaxValue, Color.White));
			projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			m_subsystemAudio.PlaySound("Audio/Throw", 1.0f, m_random.Float(-0.2f, 0.2f), projectile.Position, 2f, true);
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
							StartFluOnTarget(creature, 645f);
							creature.ComponentBody?.ApplyImpulse(new Vector3(
								m_random.Float(-0.5f, 0.5f),
								0.2f,
								m_random.Float(-0.5f, 0.5f)
							));
						}
					}
				}
			}

			m_subsystemAudio.PlaySound("Audio/congelado", 3.0f, m_random.Float(-0.2f, 0.2f), worldItem.Position, 2f, true);

			return false;
		}

		private void ApplyFreezingEffectsToPlayer(ComponentPlayer player, int impactNumber)
		{
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
					SetPlayerTemperature(player, 6f);
					break;
				case 2:
					SetPlayerTemperature(player, 3f);
					player.ComponentBody?.ApplyImpulse(new Vector3(
						m_random.Float(-0.8f, 0.8f),
						0.3f,
						m_random.Float(-0.8f, 0.8f)
					));
					break;
				case 3:
					StartFluOnTarget(player, 645f);
					SetPlayerTemperature(player, 1f);
					player.ComponentBody?.ApplyImpulse(new Vector3(
						m_random.Float(-1.2f, 1.2f),
						0.5f,
						m_random.Float(-1.2f, 1.2f)
					));
					break;
				default:
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
				if (fluField != null)
				{
					float currentDuration = (float)fluField.GetValue(player.ComponentFlu);
					if (duration > currentDuration)
						fluField.SetValue(player.ComponentFlu, duration);
				}
			}
		}
	}
}
