using System;
using Engine;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x0200033F RID: 831
	public class SubsystemArrowBlockBehavior : SubsystemBlockBehavior
	{
		// Token: 0x17000397 RID: 919
		// (get) Token: 0x06001934 RID: 6452 RVA: 0x000C6ABF File Offset: 0x000C4CBF
		public override int[] HandledBlocks
		{
			get
			{
				return new int[0];
			}
		}

		// Token: 0x06001935 RID: 6453 RVA: 0x000C6AC8 File Offset: 0x000C4CC8
		public override void OnFiredAsProjectile(Projectile projectile)
		{
			if (ArrowBlock.GetArrowType(Terrain.ExtractData(projectile.Value)) == ArrowBlock.ArrowType.FireArrow)
			{
				this.m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new SmokeTrailParticleSystem(20, 0.5f, float.MaxValue, Color.White));
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				projectile.IsIncendiary = true;
			}
		}

		// Token: 0x06001936 RID: 6454 RVA: 0x000C6B20 File Offset: 0x000C4D20
		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			ArrowBlock.ArrowType arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(worldItem.Value));
			if (worldItem.Velocity.Length() > 10f)
			{
				float num = 0.1f;
				if (arrowType == ArrowBlock.ArrowType.FireArrow)
				{
					num = 0.5f;
				}
				if (arrowType == ArrowBlock.ArrowType.WoodenArrow)
				{
					num = 0.2f;
				}
				if (arrowType == ArrowBlock.ArrowType.DiamondArrow)
				{
					num = 0f;
				}
				if (arrowType == ArrowBlock.ArrowType.IronBolt)
				{
					num = 0.05f;
				}
				if (arrowType == ArrowBlock.ArrowType.DiamondBolt)
				{
					num = 0f;
				}
				if (this.m_random.Float(0f, 1f) < num)
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x06001937 RID: 6455 RVA: 0x000C6BA2 File Offset: 0x000C4DA2
		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
		}

		// Token: 0x040011E3 RID: 4579
		public SubsystemProjectiles m_subsystemProjectiles;

		// Token: 0x040011E4 RID: 4580
		public Random m_random = new Random();
	}
}
