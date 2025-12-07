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
				return Array.Empty<int>();
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

			// Probabilidad de rotura según el tipo de flecha
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
					// Si se rompe la flecha, desaparece
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
