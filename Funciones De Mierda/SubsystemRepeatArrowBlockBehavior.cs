using System;
using Engine;
using Game;
using TemplatesDatabase;
using WonderfulEra;

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
				float poisonIntensity = (arrowType == RepeatArrowBlock.ArrowType.PoisonArrow) ? 150f : 300f;
				ComponentPoisonInfected componentPoisonInfected = (componentBody != null) ? componentBody.Entity.FindComponent<ComponentPoisonInfected>() : null;
				ComponentPlayer componentPlayer = ((componentBody != null) ? componentBody.Entity.FindComponent<ComponentCreature>() : null) as ComponentPlayer;

				if (componentPlayer != null)
				{
					if (!componentPlayer.ComponentSickness.IsSick)
					{
						componentPlayer.ComponentSickness.StartSickness();
						componentPlayer.ComponentSickness.m_sicknessDuration = Math.Max(0f, poisonIntensity - (componentPoisonInfected != null ? componentPoisonInfected.PoisonResistance : 0f));
					}
				}
				else if (componentPoisonInfected != null)
				{
					componentPoisonInfected.StartInfect(poisonIntensity);
				}

				// Aplicar daño de veneno
				ComponentHealth componentHealth = (componentBody != null) ? componentBody.Entity.FindComponent<ComponentHealth>() : null;
				Projectile projectile = worldItem as Projectile;
				if (projectile != null && componentHealth != null)
				{
					float damage = (arrowType == RepeatArrowBlock.ArrowType.PoisonArrow) ? 0.2f : 0.4f;
					componentHealth.Injure(new FireInjury(damage / componentHealth.FireResilience, projectile.Owner));
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
