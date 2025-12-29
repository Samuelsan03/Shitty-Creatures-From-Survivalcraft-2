using System;
using Game;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemPoisonBulletBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex<PoisonBulletBlock>(false, false)
				};
			}
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			// Lógica para balas de veneno
			ComponentPoisonInfected componentPoisonInfected = (componentBody != null) ? componentBody.Entity.FindComponent<ComponentPoisonInfected>() : null;
			ComponentPlayer componentPlayer = ((componentBody != null) ? componentBody.Entity.FindComponent<ComponentCreature>() : null) as ComponentPlayer;

			if (componentPlayer != null)
			{
				if (!componentPlayer.ComponentSickness.IsSick)
				{
					componentPlayer.ComponentSickness.StartSickness();
					componentPlayer.ComponentSickness.m_sicknessDuration = Math.Max(0f, 300f - (componentPoisonInfected != null ? componentPoisonInfected.PoisonResistance : 0f));
				}
			}
			else if (componentPoisonInfected != null)
			{
				componentPoisonInfected.StartInfect(300f);
			}

			ComponentHealth componentHealth = (componentBody != null) ? componentBody.Entity.FindComponent<ComponentHealth>() : null;
			Projectile projectile = worldItem as Projectile;
			if (projectile != null && componentHealth != null)
			{
				componentHealth.Injure(0.4f, null, false, "PoisonBullet");
			}

			return true;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
		}

		public SubsystemTerrain m_subsystemTerrain;
	}
}
