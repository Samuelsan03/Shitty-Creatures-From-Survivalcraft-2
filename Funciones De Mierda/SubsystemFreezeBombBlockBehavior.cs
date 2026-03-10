// SubsystemFreezeBombBlockBehavior.cs
using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFreezeBombBlockBehavior : SubsystemBlockBehavior, IUpdateable
	{
		public override int[] HandledBlocks => new int[] { FreezeBombBlock.Index };
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemFreezeExplosions m_subsystemFreezeExplosions;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemBodies m_subsystemBodies;
		private Dictionary<Projectile, bool> m_projectiles = new Dictionary<Projectile, bool>();

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemFreezeExplosions = Project.FindSubsystem<SubsystemFreezeExplosions>(false);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);

			foreach (Projectile projectile in m_subsystemProjectiles.Projectiles)
			{
				ScanProjectile(projectile);
			}

			m_subsystemProjectiles.ProjectileAdded += (Projectile projectile) => ScanProjectile(projectile);
			m_subsystemProjectiles.ProjectileRemoved += (Projectile projectile) => m_projectiles.Remove(projectile);
		}

		private void ScanProjectile(Projectile projectile)
		{
			if (!m_projectiles.ContainsKey(projectile))
			{
				int blockId = Terrain.ExtractContents(projectile.Value);
				if (blockId == FreezeBombBlock.Index)
				{
					m_projectiles.Add(projectile, true);
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.DoNothing;
					Color blueColor = new Color(200, 230, 255);
					m_subsystemProjectiles.AddTrail(projectile, new Vector3(0f, 0.25f, 0.1f), new SmokeTrailParticleSystem(15, 0.25f, float.MaxValue, blueColor));
				}
			}
		}

		public void Update(float dt)
		{
			if (m_subsystemTime.PeriodicGameTimeEvent(0.1, 0.0))
			{
				List<Projectile> toRemove = new List<Projectile>();
				foreach (Projectile projectile in m_projectiles.Keys)
				{
					if (m_subsystemGameInfo.TotalElapsedGameTime - projectile.CreationTime > 5.0)
					{
						CreateFreezeExplosion(projectile);
						toRemove.Add(projectile);
						projectile.ToRemove = true;
					}
				}
				foreach (Projectile p in toRemove)
					m_projectiles.Remove(p);
			}
		}

		private void CreateFreezeExplosion(Projectile projectile)
		{
			Vector3 pos = projectile.Position;
			int x = Terrain.ToCell(pos.X);
			int y = Terrain.ToCell(pos.Y);
			int z = Terrain.ToCell(pos.Z);

			if (m_subsystemFreezeExplosions != null)
			{
				m_subsystemFreezeExplosions.AddFreezeExplosion(x, y, z, 25f, 300f, false);
			}
			else
			{
				m_subsystemAudio?.PlaySound("Audio/explosion congelante", 1f, 0f, pos, 12f, true);
			}
		}

		public void TriggerFreezeExplosion(int x, int y, int z, int value)
		{
			m_subsystemTerrain.DestroyCell(0, x, y, z, value, false, false);
			if (m_subsystemFreezeExplosions != null)
			{
				m_subsystemFreezeExplosions.AddFreezeExplosion(x, y, z, 20f, 250f, true);
			}
			else
			{
				Vector3 pos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
				m_subsystemAudio?.PlaySound("Audio/explosion congelante", 1f, 0f, pos, 10f, true);
			}
		}

		public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
		{
			int value = m_subsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
			int blockId = Terrain.ExtractContents(value);
			if (blockId == FreezeBombBlock.Index)
			{
				TriggerFreezeExplosion(cellFace.X, cellFace.Y, cellFace.Z, value);
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
			int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
			int blockId = Terrain.ExtractContents(cellValue);
			if (blockId == FreezeBombBlock.Index)
			{
				TriggerFreezeExplosion(x, y, z, cellValue);
			}
		}
	}
}
