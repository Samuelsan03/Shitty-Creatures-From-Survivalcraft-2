using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemBigStonePoisonChunkBlockBehavior : SubsystemBlockBehavior, IUpdateable
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { BigStonePoisonChunkBlock.Index };
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
		private bool m_initialized;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_initialized = true;
		}

		public void Update(float dt)
		{
			if (!m_initialized)
				return;

			// Revisar todos los proyectiles activos
			foreach (Projectile projectile in m_subsystemProjectiles.Projectiles)
			{
				if (Terrain.ExtractContents(projectile.Value) == BigStonePoisonChunkBlock.Index)
				{
					// Verificar si este proyectil impactó en este frame
					if (projectile.ToRemove && !m_processedProjectiles.Contains(projectile))
					{
						// Este proyectil impactó y está marcado para remover
						TryApplyPoisonEffect(projectile);
						m_processedProjectiles.Add(projectile);
					}
				}
			}

			// Limpiar proyectiles ya procesados
			m_processedProjectiles.RemoveWhere(p => !m_subsystemProjectiles.Projectiles.Contains(p));
		}

		private void TryApplyPoisonEffect(Projectile projectile)
		{
			if (projectile == null)
				return;

			// Buscar cuerpos cercanos al punto de impacto
			Vector3 impactPos = projectile.Position;
			Vector2 corner1 = new Vector2(impactPos.X - 2f, impactPos.Z - 2f);
			Vector2 corner2 = new Vector2(impactPos.X + 2f, impactPos.Z + 2f);

			DynamicArray<ComponentBody> bodies = new DynamicArray<ComponentBody>();
			m_subsystemBodies.FindBodiesInArea(corner1, corner2, bodies);

			for (int i = 0; i < bodies.Count; i++)
			{
				ComponentBody body = bodies.Array[i];
				if (body != null && body.Entity != null)
				{
					ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
					if (creature != null)
					{
						ApplyPoisonToCreature(creature);
					}
				}
			}
		}

		private void ApplyPoisonToCreature(ComponentCreature creature)
		{
			if (creature == null)
				return;

			ComponentPoisonInfected componentPoisonInfected = creature.Entity.FindComponent<ComponentPoisonInfected>();
			ComponentPlayer componentPlayer = creature as ComponentPlayer;

			if (componentPlayer != null)
			{
				// Para jugadores: usar sistema de enfermedad
				if (!componentPlayer.ComponentSickness.IsSick)
				{
					componentPlayer.ComponentSickness.StartSickness();
					if (componentPoisonInfected != null)
					{
						componentPlayer.ComponentSickness.m_sicknessDuration = 15f - componentPoisonInfected.PoisonResistance;
					}
				}
			}
			else if (componentPoisonInfected != null && !componentPoisonInfected.IsInfected)
			{
				// Para otras criaturas: usar sistema de infección de veneno
				componentPoisonInfected.StartInfect(15f);
			}
		}

		private HashSet<Projectile> m_processedProjectiles = new HashSet<Projectile>();
	}
}
