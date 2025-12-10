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
			if (RepeatArrowBlock.GetArrowType(Terrain.ExtractData(projectile.Value)) != RepeatArrowBlock.ArrowType.ExplosiveArrow)
			{
				return;
			}
			this.m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new SmokeTrailParticleSystem(20, 0.5f, float.MaxValue, Color.White));
			projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			RepeatArrowBlock.ArrowType arrowType = RepeatArrowBlock.GetArrowType(Terrain.ExtractData(worldItem.Value));

			// Aplicar efectos de veneno si la flecha es de veneno
			if (arrowType == RepeatArrowBlock.ArrowType.PoisonArrow || arrowType == RepeatArrowBlock.ArrowType.SeriousPoisonArrow)
			{
				// Intensidades de veneno
				float poisonIntensity = (arrowType == RepeatArrowBlock.ArrowType.PoisonArrow) ? 60f : 100f;

				// Obtener el objetivo
				if (componentBody != null && componentBody.Entity != null)
				{
					ComponentCreature targetCreature = componentBody.Entity.FindComponent<ComponentCreature>();

					if (targetCreature != null)
					{
						// Buscar componente de veneno
						ComponentPoisonInfected componentPoisonInfected = targetCreature.Entity.FindComponent<ComponentPoisonInfected>();
						ComponentPlayer componentPlayer = targetCreature as ComponentPlayer;

						// Aplicar veneno a jugadores
						if (componentPlayer != null)
						{
							if (componentPlayer.ComponentSickness != null && !componentPlayer.ComponentSickness.IsSick)
							{
								componentPlayer.ComponentSickness.StartSickness();
								float resistance = (componentPoisonInfected != null) ? componentPoisonInfected.PoisonResistance : 0f;
								componentPlayer.ComponentSickness.m_sicknessDuration = MathUtils.Max(0f, poisonIntensity - resistance);
							}
						}
						// Aplicar veneno a criaturas
						else if (componentPoisonInfected != null)
						{
							componentPoisonInfected.StartInfect(poisonIntensity);
						}
					}
				}

				// DAÑO INICIAL REDUCIDO: Solo 3-4 de daño en lugar de 11-13
				ComponentHealth componentHealth = (componentBody != null) ? componentBody.Entity.FindComponent<ComponentHealth>() : null;
				Projectile projectile = worldItem as Projectile;
				if (projectile != null && componentHealth != null)
				{
					// Daño MUY reducido: 3 para Poison, 4 para Serious Poison
					float damage = (arrowType == RepeatArrowBlock.ArrowType.PoisonArrow) ? 3f : 4f;

					// Aplicar daño reducido
					componentHealth.Injure(damage, projectile.Owner, false, "ShotByArrow");
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
