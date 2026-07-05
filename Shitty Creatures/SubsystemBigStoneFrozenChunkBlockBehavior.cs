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
			// La escala del bloque es 4.5f. Multiplicamos el tamaño base (0.4f) por esa escala para que la nube envuelva el bloque.
			// En SmokeTrail, el tamaño máximo de la partícula es: m_size * (0.15f + 0.8f) = m_size * 0.95f
			// Con 1.8f, las partículas medirán aprox 1.71 de radio, envolviendo bien la piedra.
			float trailSize = 1.8f;

			var trail = new FreezingTrailParticleSystem(150, trailSize, float.MaxValue, new Color(200, 240, 255, 220));
			m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, trail);
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
