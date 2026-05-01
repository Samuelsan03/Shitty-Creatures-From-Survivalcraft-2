using System;
using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemPoisonBulletBlockBehavior : SubsystemBlockBehavior, IUpdateable
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { PoisonBulletBlock.Index }; // Usar el bloque directamente
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTime m_subsystemTime;
		private bool m_initialized;
		private Dictionary<Projectile, double> m_activePoisonProjectiles = new Dictionary<Projectile, double>();

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_initialized = true;
		}

		public void Update(float dt)
		{
			if (!m_initialized)
				return;

			double currentTime = m_subsystemTime.GameTime;

			// Registrar y rastrear proyectiles de veneno
			foreach (Projectile projectile in m_subsystemProjectiles.Projectiles)
			{
				int blockValue = projectile.Value;
				int blockId = Terrain.ExtractContents(blockValue);

				// Verificar si es un PoisonBulletBlock usando el nombre de clase
				if (BlocksManager.Blocks[blockId] is PoisonBulletBlock)
				{
					if (!m_activePoisonProjectiles.ContainsKey(projectile))
					{
						m_activePoisonProjectiles[projectile] = currentTime;
					}
				}
			}

			// Verificar impactos de proyectiles de veneno
			List<Projectile> projectilesToRemove = new List<Projectile>();

			foreach (var kvp in m_activePoisonProjectiles)
			{
				Projectile projectile = kvp.Key;

				// Si el proyectil fue eliminado o está marcado para remover
				if (!m_subsystemProjectiles.Projectiles.Contains(projectile) || projectile.ToRemove)
				{
					// Verificar que realmente sea un poison bullet
					int blockValue = projectile.Value;
					int blockId = Terrain.ExtractContents(blockValue);

					if (BlocksManager.Blocks[blockId] is PoisonBulletBlock)
					{
						ApplyPoisonEffectOnImpact(projectile);
					}

					projectilesToRemove.Add(projectile);
				}
				// Limpieza: remover proyectiles muy antiguos (por seguridad)
				else if (currentTime - kvp.Value > 15.0)
				{
					projectilesToRemove.Add(projectile);
				}
			}

			// Limpiar proyectiles procesados
			foreach (Projectile projectile in projectilesToRemove)
			{
				m_activePoisonProjectiles.Remove(projectile);
			}
		}

		private void ApplyPoisonEffectOnImpact(Projectile projectile)
		{
			if (projectile == null)
				return;

			Vector3 impactPosition = projectile.Position;

			// Buscar cuerpos cercanos al impacto
			float effectRadius = 2.0f;
			Vector2 areaCorner1 = new Vector2(impactPosition.X - effectRadius, impactPosition.Z - effectRadius);
			Vector2 areaCorner2 = new Vector2(impactPosition.X + effectRadius, impactPosition.Z + effectRadius);

			DynamicArray<ComponentBody> bodiesInArea = new DynamicArray<ComponentBody>();
			m_subsystemBodies.FindBodiesInArea(areaCorner1, areaCorner2, bodiesInArea);

			for (int i = 0; i < bodiesInArea.Count; i++)
			{
				ComponentBody body = bodiesInArea.Array[i];
				if (body != null && body.Entity != null)
				{
					// Calcular distancia real en 3D
					float distance = Vector3.Distance(body.Position, impactPosition);

					// Aplicar efecto si está dentro del radio
					if (distance <= effectRadius)
					{
						ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
						if (creature != null)
						{
							// Aplicar el efecto de veneno
							ApplyPoisonToTarget(creature);
						}
					}
				}
			}
		}

		private void ApplyPoisonToTarget(ComponentCreature targetCreature)
		{
			if (targetCreature == null)
				return;

			ComponentPlayer targetPlayer = targetCreature as ComponentPlayer;
			ComponentPoisonInfected poisonComponent = targetCreature.Entity.FindComponent<ComponentPoisonInfected>();

			float poisonDuration = 15f; // Duración base del veneno

			if (targetPlayer != null)
			{
				// Para jugadores: usar el sistema de enfermedad
				if (!targetPlayer.ComponentSickness.IsSick)
				{
					targetPlayer.ComponentSickness.StartSickness();

					// Aplicar resistencia si existe
					if (poisonComponent != null)
					{
						targetPlayer.ComponentSickness.m_sicknessDuration =
							Math.Max(poisonDuration - poisonComponent.PoisonResistance, 0f);
					}
					else
					{
						targetPlayer.ComponentSickness.m_sicknessDuration = poisonDuration;
					}
				}
			}
			else if (poisonComponent != null)
			{
				// Para otras criaturas: usar el sistema de infección por veneno
				poisonComponent.StartInfect(poisonDuration);
			}
		}
	}
}
