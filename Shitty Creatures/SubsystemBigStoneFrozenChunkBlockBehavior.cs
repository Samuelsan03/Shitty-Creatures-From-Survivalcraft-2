using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemBigStoneFrozenChunkBlockBehavior : SubsystemBlockBehavior
	{
		private SubsystemProjectiles m_subsystemProjectiles;
		private Random m_random = new Random();

		public override int[] HandledBlocks => new int[] { BlocksManager.GetBlockIndex<BigStoneFrozenChunkBlock>() };

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new FreezingTrailParticleSystem(50, 4.5f, float.MaxValue, Color.White));
			projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			if (componentBody == null) return false;

			var player = componentBody.Entity.FindComponent<ComponentPlayer>();
			if (player != null)
			{
				var flu = player.Entity.FindComponent<ComponentFlu>();
				if (flu != null && !flu.HasFlu)
				{
					flu.StartFlu();
				}
				return false;
			}

			var creature = componentBody.Entity.FindComponent<ComponentCreature>();
			if (creature != null)
			{
				var infected = creature.Entity.FindComponent<ComponentFluInfected>();
				if (infected != null && !infected.IsInfected)
				{
					infected.StartFlu(m_random.Float(180f, 300f));
				}
			}

			return false;
		}
	}
}
