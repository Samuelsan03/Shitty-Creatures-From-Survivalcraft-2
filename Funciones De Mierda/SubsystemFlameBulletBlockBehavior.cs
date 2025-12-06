using System;
using Game;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x020000D1 RID: 209
	public class SubsystemFlameBulletBlockBehavior : SubsystemBlockBehavior
	{
		// Token: 0x17000093 RID: 147
		// (get) Token: 0x0600063C RID: 1596 RVA: 0x00029025 File Offset: 0x00027225
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

		// Token: 0x0600063D RID: 1597 RVA: 0x00029038 File Offset: 0x00027238
		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			// Todas las balas son de fuego ahora, no hay veneno
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
			return true;
		}

		// Token: 0x0600063E RID: 1598 RVA: 0x0002924A File Offset: 0x0002744A
		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemExplosions = base.Project.FindSubsystem<SubsystemExplosions>(true);
			this.m_subsystemFireBlockBehavior = base.Project.FindSubsystem<SubsystemFireBlockBehavior>(true);
		}

		// Token: 0x04000392 RID: 914
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x04000393 RID: 915
		public SubsystemExplosions m_subsystemExplosions;

		// Token: 0x04000394 RID: 916
		public SubsystemFireBlockBehavior m_subsystemFireBlockBehavior;

		// Token: 0x04000395 RID: 917
		public Random m_random = new Random();
	}
}
