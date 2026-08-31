using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemPoisonBombBlockBehavior : SubsystemBlockBehavior, IUpdateable
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { PoisonBombBlock.Index };
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemPoisonExplosions = base.Project.FindSubsystem<SubsystemPoisonExplosions>(false);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			foreach (Projectile projectile in this.m_subsystemProjectiles.Projectiles)
			{
				this.ScanProjectile(projectile);
			}
			this.m_subsystemProjectiles.ProjectileAdded += delegate (Projectile projectile)
			{
				this.ScanProjectile(projectile);
			};
			this.m_subsystemProjectiles.ProjectileRemoved += delegate (Projectile projectile)
			{
				this.m_projectiles.Remove(projectile);
			};
		}

		public void ScanProjectile(Projectile projectile)
		{
			if (!this.m_projectiles.ContainsKey(projectile))
			{
				int blockId = Terrain.ExtractContents(projectile.Value);
				if (blockId == PoisonBombBlock.Index)
				{
					this.m_projectiles.Add(projectile, true);
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.DoNothing;
					Color greenColor = new Color(51, 255, 51);
					this.m_subsystemProjectiles.AddTrail(projectile, new Vector3(0f, 0.25f, 0.1f), new SmokeTrailParticleSystem(15, 0.25f, float.MaxValue, greenColor));
				}
			}
		}

		public void Update(float dt)
		{
			if (this.m_subsystemTime.PeriodicGameTimeEvent(0.1, 0.0))
			{
				int i = 0;
				while (i < this.m_projectiles.Count)
				{
					Projectile projectile = this.m_projectiles.Keys.ElementAt(i);
					if (this.m_subsystemGameInfo.TotalElapsedGameTime - projectile.CreationTime > 5.0)
					{
						this.CreatePoisonExplosion(projectile);
						projectile.ToRemove = true;
						this.m_projectiles.Remove(projectile);
					}
					else
					{
						i++;
					}
				}
			}
		}

		public void CreatePoisonExplosion(Projectile projectile)
		{
			Vector3 position = projectile.Position;
			int x = Terrain.ToCell(position.X);
			int y = Terrain.ToCell(position.Y);
			int z = Terrain.ToCell(position.Z);
			if (this.m_subsystemPoisonExplosions != null)
			{
				this.m_subsystemPoisonExplosions.AddPoisonExplosion(x, y, z, 25f, 180f, false);
			}
		}

		public void TriggerPoisonExplosion(int x, int y, int z, int value)
		{
			this.m_subsystemTerrain.DestroyCell(0, x, y, z, value, false, false);
			if (this.m_subsystemPoisonExplosions != null)
			{
				this.m_subsystemPoisonExplosions.AddPoisonExplosion(x, y, z, 20f, 150f, true);
			}
		}

		public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
		{
			int value = this.m_subsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
			int blockId = Terrain.ExtractContents(value);
			if (blockId == PoisonBombBlock.Index)
			{
				this.TriggerPoisonExplosion(cellFace.X, cellFace.Y, cellFace.Z, value);
			}
		}

		public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
		{
			return false;
		}

		public override void OnItemHarvested(int x, int y, int z, int blockValue, ref BlockDropValue dropValue, ref int newBlockValue)
		{
		}

		public void HandleExplosionDamage(int x, int y, int z)
		{
			int cellValue = this.m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
			int blockId = Terrain.ExtractContents(cellValue);
			if (blockId == PoisonBombBlock.Index)
			{
				this.TriggerPoisonExplosion(x, y, z, cellValue);
			}
		}

		public SubsystemGameInfo m_subsystemGameInfo;

		public SubsystemTime m_subsystemTime;

		public SubsystemProjectiles m_subsystemProjectiles;

		public SubsystemPoisonExplosions m_subsystemPoisonExplosions;

		public SubsystemParticles m_subsystemParticles;

		public SubsystemTerrain m_subsystemTerrain;

		public SubsystemBodies m_subsystemBodies;

		public Dictionary<Projectile, bool> m_projectiles = new Dictionary<Projectile, bool>();
	}
}
