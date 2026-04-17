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

			m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemPoisonExplosions = base.Project.FindSubsystem<SubsystemPoisonExplosions>(false);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);

			foreach (Projectile projectile in m_subsystemProjectiles.Projectiles)
			{
				ScanProjectile(projectile);
			}

			SubsystemProjectiles subsystemProjectiles = m_subsystemProjectiles;
			subsystemProjectiles.ProjectileAdded += delegate (Projectile projectile)
			{
				ScanProjectile(projectile);
			};

			SubsystemProjectiles subsystemProjectiles2 = m_subsystemProjectiles;
			subsystemProjectiles2.ProjectileRemoved += delegate (Projectile projectile)
			{
				m_projectiles.Remove(projectile);
			};
		}

		public void ScanProjectile(Projectile projectile)
		{
			if (!m_projectiles.ContainsKey(projectile))
			{
				int blockId = Terrain.ExtractContents(projectile.Value);

				// Solo manejar bombas de veneno
				if (blockId == PoisonBombBlock.Index)
				{
					m_projectiles.Add(projectile, true);
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.DoNothing;

					// Añadir partículas de rastro verdes
					Color greenColor = new Color(51, 255, 51);
					m_subsystemProjectiles.AddTrail(projectile,
						new Vector3(0f, 0.25f, 0.1f),
						new SmokeTrailParticleSystem(15, 0.25f, float.MaxValue, greenColor));
				}
			}
		}

		public void Update(float dt)
		{
			if (m_subsystemTime.PeriodicGameTimeEvent(0.1, 0.0))
			{
				List<Projectile> projectilesToRemove = new List<Projectile>();

				foreach (Projectile projectile in m_projectiles.Keys)
				{
					// Explotar después de 5 segundos
					if (m_subsystemGameInfo.TotalElapsedGameTime - projectile.CreationTime > 5.0)
					{
						CreatePoisonExplosion(projectile);
						projectilesToRemove.Add(projectile);
						projectile.ToRemove = true;
					}
				}

				// Eliminar proyectiles procesados
				foreach (Projectile projectile in projectilesToRemove)
				{
					m_projectiles.Remove(projectile);
				}
			}
		}

		private void CreatePoisonExplosion(Projectile projectile)
		{
			Vector3 position = projectile.Position;
			int x = Terrain.ToCell(position.X);
			int y = Terrain.ToCell(position.Y);
			int z = Terrain.ToCell(position.Z);

			// Usar el sistema de explosiones de veneno si existe
			if (m_subsystemPoisonExplosions != null)
			{
				float explosionPressure = 25f;
				float poisonIntensity = 180f;

				m_subsystemPoisonExplosions.AddPoisonExplosion(
					x, y, z,
					explosionPressure,
					poisonIntensity,
					false
				);
			}
			else
			{
				// Si no existe el sistema de explosiones de veneno, reproducir sonido
				if (m_subsystemAudio != null)
				{
					m_subsystemAudio.PlaySound("Audio/Explosion De Mierda/Smoke Explosion", 1f, 0f, position, 12f, true);
				}
			}
		}

		// Método para manejar cuando una bomba de veneno es golpeada por un proyectil
		public void TriggerPoisonExplosion(int x, int y, int z, int value)
		{
			// Destruir el bloque
			m_subsystemTerrain.DestroyCell(0, x, y, z, value, false, false);

			// Crear explosión de veneno usando el sistema si existe
			if (m_subsystemPoisonExplosions != null)
			{
				m_subsystemPoisonExplosions.AddPoisonExplosion(
					x, y, z,
					20f,   // Presión
					150f,  // Intensidad
					true
				);
			}
			else if (m_subsystemAudio != null)
			{
				Vector3 position = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
				m_subsystemAudio.PlaySound("Audio/Explosion De Mierda/Smoke Explosion", 1f, 0f, position, 10f, true);
			}
		}

		public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
		{
			// Cuando la bomba es golpeada por un proyectil
			int value = m_subsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
			int blockId = Terrain.ExtractContents(value);

			// Verificar que sea una bomba de veneno
			if (blockId == PoisonBombBlock.Index)
			{
				TriggerPoisonExplosion(cellFace.X, cellFace.Y, cellFace.Z, value);
			}
		}

		public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
		{
			// Permitir recoger la bomba sin que explote
			return false;
		}

		public override void OnItemHarvested(int x, int y, int z, int blockValue, ref BlockDropValue dropValue, ref int newBlockValue)
		{
			// Cuando se mina la bomba, asegurarse de que no explote
		}

		// Método para manejar cuando la bomba es destruida por una explosión
		public void HandleExplosionDamage(int x, int y, int z)
		{
			// Verificar si hay una bomba de veneno en esta posición
			int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
			int blockId = Terrain.ExtractContents(cellValue);

			if (blockId == PoisonBombBlock.Index)
			{
				TriggerPoisonExplosion(x, y, z, cellValue);
			}
		}

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemPoisonExplosions m_subsystemPoisonExplosions;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemBodies m_subsystemBodies;
		private Dictionary<Projectile, bool> m_projectiles = new Dictionary<Projectile, bool>();
	}
}
