using System;
using Game;
using TemplatesDatabase;

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
			// Solo manejar llamas regulares, no poison
			if (cellFace != null)
			{
				int cellValue = m_subsystemTerrain.Terrain.GetCellValue(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z);

				// Si la bala va muy rápido, puede causar explosión
				if (worldItem.Velocity.Length() > 30f)
				{
					m_subsystemExplosions.TryExplodeBlock(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z, cellValue);
				}

				// Encender el bloque donde impactó
				m_subsystemFireBlockBehavior.SetCellOnFire(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z, 1f);
			}

			// Si impactó en una entidad, prenderla fuego
			ComponentOnFire componentOnFire = (componentBody != null) ? componentBody.Entity.FindComponent<ComponentOnFire>() : null;
			if (componentOnFire != null)
			{
				Projectile projectile = worldItem as Projectile;
				if (projectile != null)
				{
					// Prender fuego a la entidad
					componentOnFire.SetOnFire(projectile.Owner, m_random.Float(4f, 6f));

					// Causar daño por fuego
					ComponentHealth componentHealth = componentBody.Entity.FindComponent<ComponentHealth>();
					if (componentHealth != null)
					{
						componentHealth.Injure(new FireInjury(5f / componentHealth.FireResilience, projectile.Owner));
					}
				}
			}

			return true;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true);
			m_subsystemFireBlockBehavior = Project.FindSubsystem<SubsystemFireBlockBehavior>(true);
		}

		// Campos
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemExplosions m_subsystemExplosions;
		public SubsystemFireBlockBehavior m_subsystemFireBlockBehavior;
		public Random m_random = new Random();
	}
}
