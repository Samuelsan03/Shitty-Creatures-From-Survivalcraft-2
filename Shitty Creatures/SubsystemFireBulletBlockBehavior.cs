using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFireBulletBlockBehavior : SubsystemBlockBehavior, IUpdateable
	{
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemBodies m_subsystemBodies;
		private Game.Random m_random = new Game.Random();
		private bool m_initialized;

		// Lista para controlar los fuegos creados por las balas
		private Dictionary<Point3, FireDurationInfo> m_activeFires = new Dictionary<Point3, FireDurationInfo>();

		private struct FireDurationInfo
		{
			public float StartTime;
			public float Duration;
		}

		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { FireBulletBlock.Index };
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// MODIFICADO: Se removieron los efectos de partículas de fuego al disparar
		public override void OnFiredAsProjectile(Projectile projectile)
		{
			// Se eliminaron las líneas que agregaban efectos de partículas de fuego
			// El proyectil ahora será visualmente normal

			projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			projectile.IsIncendiary = true; // Mantener propiedad incendiaria
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_initialized = true;
		}

		public void Update(float dt)
		{
			if (!m_initialized)
				return;

			float currentTime = (float)m_subsystemTime.GameTime;

			// Actualizar duración de los fuegos creados por balas (60 segundos)
			List<Point3> firesToRemove = new List<Point3>();
			foreach (var kvp in m_activeFires)
			{
				var fireInfo = kvp.Value;
				if (currentTime - fireInfo.StartTime >= fireInfo.Duration)
				{
					Point3 point = kvp.Key;

					// Verificar si todavía hay fuego en esa posición
					int cellValue = m_subsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
					if (Terrain.ExtractContents(cellValue) == 104) // 104 = FireBlock
					{
						m_subsystemTerrain.ChangeCell(point.X, point.Y, point.Z, 0);
					}
					firesToRemove.Add(point);
				}
			}

			// Eliminar los fuegos que ya expiraron
			foreach (Point3 point in firesToRemove)
			{
				m_activeFires.Remove(point);
			}

			// Verificar impactos de proyectiles
			CheckProjectileImpacts();
		}

		private void CheckProjectileImpacts()
		{
			// Revisar todos los proyectiles activos
			foreach (Projectile projectile in m_subsystemProjectiles.Projectiles)
			{
				if (projectile != null && Terrain.ExtractContents(projectile.Value) == FireBulletBlock.Index)
				{
					// Verificar si el proyectil impactó (posición no se mueve más o velocidad es cero)
					if (projectile.ToRemove || projectile.Velocity.LengthSquared() < 0.1f)
					{
						// Crear fuego en el impacto
						TryCreateFireOnImpact(projectile.Position, projectile.Velocity);

						// Marcar para remover si no está marcado
						if (!projectile.ToRemove)
							projectile.ToRemove = true;
					}
				}
			}
		}

		private void TryCreateFireOnImpact(Vector3 position, Vector3 velocity)
		{
			// Calcular la posición de impacto
			Vector3 impactPos = position;

			// Crear fuego en un área alrededor del impacto (60 segundos de duración)
			CreateFireArea(impactPos, 2f, 60f);

			// También intentar encender entidades cercanas
			IgniteNearbyEntities(impactPos);
		}

		private void CreateFireArea(Vector3 center, float radius, float duration)
		{
			int centerX = (int)Math.Round(center.X);
			int centerY = (int)Math.Round(center.Y);
			int centerZ = (int)Math.Round(center.Z);

			int radiusInt = (int)Math.Ceiling(radius);

			for (int x = centerX - radiusInt; x <= centerX + radiusInt; x++)
			{
				for (int z = centerZ - radiusInt; z <= centerZ + radiusInt; z++)
				{
					for (int y = centerY - 1; y <= centerY + 1; y++)
					{
						// Verificar distancia - conversión explícita de double a float
						float distance = (float)Vector3.Distance(new Vector3(x, y, z), center);
						if (distance <= radius)
						{
							// Probabilidad de crear fuego basada en la distancia
							float probability = 1f - (distance / radius);

							// Obtener número aleatorio - conversión explícita de double a float
							float randomFloat = (float)m_random.Float(0f, 1f);

							if (randomFloat < probability * 0.7f)
							{
								TryPlaceFireAt(x, y, z, duration);
							}
						}
					}
				}
			}
		}

		private void TryPlaceFireAt(int x, int y, int z, float duration)
		{
			// Verificar si se puede colocar fuego aquí
			int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
			int contents = Terrain.ExtractContents(cellValue);

			if (contents == 0) // Aire
			{
				// Verificar si hay un bloque debajo para sostener el fuego
				int belowValue = m_subsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
				int belowContents = Terrain.ExtractContents(belowValue);

				if (belowContents != 0 && belowContents != 104) // No es aire ni fuego
				{
					// Crear fuego
					int fireValue = Terrain.MakeBlockValue(104, 0, 0);
					m_subsystemTerrain.ChangeCell(x, y, z, fireValue);

					// Registrar este fuego para controlar su duración (60 segundos)
					Point3 point = new Point3(x, y, z);
					m_activeFires[point] = new FireDurationInfo
					{
						StartTime = (float)m_subsystemTime.GameTime,
						Duration = duration
					};
				}
			}
		}

		private void IgniteNearbyEntities(Vector3 center)
		{
			// Buscar criaturas cerca del impacto usando DynamicArray
			DynamicArray<ComponentBody> bodies = new DynamicArray<ComponentBody>();
			m_subsystemBodies.FindBodiesInArea(
				new Vector2(center.X - 3f, center.Z - 3f),
				new Vector2(center.X + 3f, center.Z + 3f),
				bodies);

			for (int i = 0; i < bodies.Count; i++)
			{
				ComponentBody body = bodies.Array[i];
				if (body != null && body.Entity != null)
				{
					ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
					if (creature != null)
					{
						// Aplicar daño de fuego
						creature.ComponentHealth.Injure(10f, null, false, "Burned by fire bullet");

						// Intentar encender a la criatura
						ComponentOnFire componentOnFire = body.Entity.FindComponent<ComponentOnFire>();
						if (componentOnFire != null && !componentOnFire.IsOnFire)
						{
							// Usar el método correcto para encender
							componentOnFire.SetOnFire(creature, 10f); // 10 segundos de fuego
						}
					}
				}
			}
		}

		public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
		{
			if (worldItem != null && Terrain.ExtractContents(worldItem.Value) == FireBulletBlock.Index)
			{
				TryCreateFireOnImpact(worldItem.Position, worldItem.Velocity);
			}
		}
	}
}