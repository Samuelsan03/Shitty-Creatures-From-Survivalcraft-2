using System;
using Engine;
using Game;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemRepeatArrowBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					RepeatArrowBlock.Index
				};
			}
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			if (RepeatArrowBlock.GetArrowType(Terrain.ExtractData(projectile.Value)) == RepeatArrowBlock.ArrowType.ExplosiveArrow)
			{
				this.m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new SmokeTrailParticleSystem(20, 0.5f, float.MaxValue, Color.White));
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			}
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			RepeatArrowBlock.ArrowType arrowType = RepeatArrowBlock.GetArrowType(Terrain.ExtractData(worldItem.Value));

			// Aplicar efectos de veneno si la flecha es de veneno
			if (arrowType == RepeatArrowBlock.ArrowType.PoisonArrow || arrowType == RepeatArrowBlock.ArrowType.SeriousPoisonArrow)
			{
				// Obtener el objetivo
				if (componentBody != null && componentBody.Entity != null)
				{
					ComponentCreature targetCreature = componentBody.Entity.FindComponent<ComponentCreature>();

					if (targetCreature != null)
					{
						// USAR LA MISMA DURACIÓN QUE FLAMEBULLET POISON: 300 segundos (5 minutos)
						// Según el código de SubsystemFlameBulletBlockBehavior, usa 300f para Poison
						float poisonDuration = (arrowType == RepeatArrowBlock.ArrowType.PoisonArrow) ? 100f : 400f;
						// Nota: Ambas flechas de veneno ahora usan 300 segundos para igualar a FlameBullet Poison
						// Si quieres mantener la diferencia, puedes usar: 300f para PoisonArrow y 450f para SeriousPoisonArrow

						ComponentPoisonInfected componentPoisonInfected = targetCreature.Entity.FindComponent<ComponentPoisonInfected>();
						ComponentPlayer componentPlayer = targetCreature as ComponentPlayer;

						// Aplicar veneno a jugadores (MISMA LÓGICA QUE FLAMEBULLET)
						if (componentPlayer != null)
						{
							// Verificar si el jugador está en modo creativo o si las mecánicas de supervivencia están deshabilitadas
							SubsystemGameInfo subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>();
							if (subsystemGameInfo != null &&
								(subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
								!subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled))
							{
								return false; // No aplicar veneno en modo creativo
							}

							if (componentPlayer.ComponentSickness != null)
							{
								float resistance = (componentPoisonInfected != null) ? componentPoisonInfected.PoisonResistance : 0f;
								float effectiveDuration = MathUtils.Max(0f, poisonDuration - resistance);

								if (!componentPlayer.ComponentSickness.IsSick)
								{
									componentPlayer.ComponentSickness.StartSickness();
									componentPlayer.ComponentSickness.m_sicknessDuration = effectiveDuration;
								}
								else
								{
									// Si ya está enfermo, sumar la duración (acumulativo como FlameBullet)
									componentPlayer.ComponentSickness.m_sicknessDuration += effectiveDuration;
								}
							}
						}
						// Aplicar veneno a criaturas (MISMA LÓGICA QUE FLAMEBULLET)
						else if (componentPoisonInfected != null)
						{
							float resistance = componentPoisonInfected.PoisonResistance;
							float effectiveDuration = MathUtils.Max(0f, poisonDuration - resistance);

							if (effectiveDuration > 0f)
							{
								componentPoisonInfected.StartInfect(effectiveDuration);
							}
						}

						// APLICAR DAÑO INICIAL (igual que FlameBullet)
						ComponentHealth componentHealth = targetCreature.Entity.FindComponent<ComponentHealth>();
						Projectile projectile = worldItem as Projectile;
						if (projectile != null && componentHealth != null)
						{
							// Usar el mismo daño que FlameBullet Poison: 0.4f / fireResilience
							// Pero ajustado al daño base de las flechas (3f o 4f según m_weaponPowers)
							float baseDamage = (arrowType == RepeatArrowBlock.ArrowType.PoisonArrow) ? 3f : 4f;
							float adjustedDamage = baseDamage / componentHealth.FireResilience;

							// Usar FireInjury como placeholder (igual que FlameBullet)
							// O crear un PoisonInjury si existe
							componentHealth.Injure(new FireInjury(adjustedDamage, projectile.Owner));
						}
					}
				}

				// Las flechas de veneno no se rompen
				return false;
			}

			// Probabilidad de rotura para flechas normales
			if (worldItem.Velocity.Length() > 10f)
			{
				float breakChance = 0f;

				switch (arrowType)
				{
					case RepeatArrowBlock.ArrowType.CopperArrow:
						breakChance = 0.20f;
						break;
					case RepeatArrowBlock.ArrowType.IronArrow:
						breakChance = 0.10f;
						break;
					case RepeatArrowBlock.ArrowType.DiamondArrow:
						breakChance = 0f;
						break;
					case RepeatArrowBlock.ArrowType.ExplosiveArrow:
						breakChance = 0.08f;
						break;
				}

				if (this.m_random.Float(0f, 1f) < breakChance)
				{
					return true;
				}
			}
			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
		}

		public SubsystemProjectiles m_subsystemProjectiles;
		public Engine.Random m_random = new Engine.Random();
	}
}
