using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemBigStoneFlameChunkBlockBehavior : SubsystemBlockBehavior, IUpdateable
	{
		public SubsystemProjectiles m_subsystemProjectiles;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemAmbientSounds m_subsystemAmbientSounds;
		public SubsystemTime m_subsystemTime;
		public Random m_random = new Random();
		public List<Projectile> m_activeProjectiles = new List<Projectile>();

		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { BigStoneFlameChunkBlock.Index };
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			// Usar SmokeTrailParticleSystem con color de FUEGO (naranja/rojo)
			this.m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new SmokeTrailParticleSystem(35, 3f, float.MaxValue, new Color(255, 100, 0)));

			// Agregar un segundo trail para más efecto de fuego
			this.m_subsystemProjectiles.AddTrail(projectile, new Vector3(0f, 0.1f, 0f), new SmokeTrailParticleSystem(25, 2f, float.MaxValue, new Color(255, 150, 50)));

			projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			projectile.IsIncendiary = true;
			this.m_activeProjectiles.Add(projectile);
		}

		public void Update(float dt)
		{
			for (int i = this.m_activeProjectiles.Count - 1; i >= 0; i--)
			{
				Projectile projectile = this.m_activeProjectiles[i];
				bool toRemove = projectile.ToRemove;
				if (toRemove)
				{
					this.m_activeProjectiles.RemoveAt(i);
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemAmbientSounds = base.Project.FindSubsystem<SubsystemAmbientSounds>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
		}
	}
}
