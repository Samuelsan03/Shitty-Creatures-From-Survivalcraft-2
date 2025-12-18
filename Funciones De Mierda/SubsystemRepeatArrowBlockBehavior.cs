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
						// Determinar duración del veneno
						float poisonDuration = (arrowType == RepeatArrowBlock.ArrowType.PoisonArrow) ? 30f : 100f;

						ComponentPoisonInfected componentPoisonInfected = targetCreature.Entity.FindComponent<ComponentPoisonInfected>();
						ComponentPlayer componentPlayer = targetCreature as ComponentPlayer;

						// Aplicar veneno a jugadores
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

								if (effectiveDuration > 0f)
								{
									if (!componentPlayer.ComponentSickness.IsSick)
									{
										componentPlayer.ComponentSickness.StartSickness();
									}

									// Asegurar que la duración sea suficiente
									componentPlayer.ComponentSickness.m_sicknessDuration = MathUtils.Max(
										componentPlayer.ComponentSickness.m_sicknessDuration,
										effectiveDuration
									);
								}
							}
						}
						// Aplicar veneno a criaturas
						else if (componentPoisonInfected != null)
						{
							float resistance = componentPoisonInfected.PoisonResistance;
							float effectiveDuration = MathUtils.Max(0f, poisonDuration - resistance);

							if (effectiveDuration > 0f)
							{
								componentPoisonInfected.StartInfect(effectiveDuration);
							}
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
