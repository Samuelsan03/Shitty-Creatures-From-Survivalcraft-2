using System;
using Game;
using TemplatesDatabase;
using WonderfulEra;

namespace Game
{
	public class SubsystemFlameBulletBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex<FlameBulletBlock>(false, false)
				};
			}
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			FlameBulletBlock.FlameBulletType bulletType = FlameBulletBlock.GetBulletType(Terrain.ExtractData(worldItem.Value));

			if (bulletType == FlameBulletBlock.FlameBulletType.Poison)
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
					// Crear un tipo de daño de veneno si no existe, o usar FireInjury como placeholder
					componentHealth.Injure(new FireInjury(0.4f / componentHealth.FireResilience, projectile.Owner));
				}
			}
			else
			{
				// Lógica original para balas de fuego
				if (cellFace != null)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValue(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z);
					if (worldItem.Velocity.Length() > 30f)
					{
						this.m_subsystemExplosions.TryExplodeBlock(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z, cellValue);
					}
					this.m_subsystemFireBlockBehavior.SetCellOnFire(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z, 1f);
				}
				ComponentOnFire componentOnFire = (componentBody != null) ? componentBody.Entity.FindComponent<ComponentOnFire>() : null;
				if (componentOnFire != null)
				{
					Projectile projectile = worldItem as Projectile;
					if (projectile != null)
					{
						componentOnFire.SetOnFire(projectile.Owner, this.m_random.Float(4f, 6f));
						ComponentHealth componentHealth = componentBody.Entity.FindComponent<ComponentHealth>();
						if (componentHealth != null)
						{
							componentHealth.Injure(new FireInjury(5f / componentHealth.FireResilience, projectile.Owner));
						}
					}
				}
			}
			return true;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemExplosions = base.Project.FindSubsystem<SubsystemExplosions>(true);
			this.m_subsystemFireBlockBehavior = base.Project.FindSubsystem<SubsystemFireBlockBehavior>(true);
		}

		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemExplosions m_subsystemExplosions;
		public SubsystemFireBlockBehavior m_subsystemFireBlockBehavior;
		public Random m_random = new Random();
	}
}
