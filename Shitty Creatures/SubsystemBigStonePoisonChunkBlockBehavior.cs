using System;
using System.Collections.Generic;
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

		// Para cada proyectil rastreado guardamos su posición y velocidad del frame anterior,
		// de modo que al detectar el impacto podamos hacer un raycast PRECISO desde la
		// posición previa en la dirección de viaje y detectar ÚNICAMENTE el cuerpo golpeado.
		private Dictionary<Projectile, Vector3> m_prevPositions = new Dictionary<Projectile, Vector3>();
		private Dictionary<Projectile, Vector3> m_prevVelocities = new Dictionary<Projectile, Vector3>();
		private HashSet<Projectile> m_processedProjectiles = new HashSet<Projectile>();

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_initialized = true;
		}

		// Se llama cuando el bloque es disparado como proyectil
		public override void OnFiredAsProjectile(Projectile projectile)
		{
			// CAMBIO AQUÍ: Tamaño de 4.5f para que coincida con la escala del bloque (Matrix.CreateScale(4.5f))
			// Aumentamos la cantidad de partículas a 40 para que no se vea vacío al ser tan grande
			m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new PukeTrailParticleSystem(40, 4.5f, float.MaxValue, Color.White));
			projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;

			// Inicializamos el seguimiento de posición/velocidad para este proyectil
			m_prevPositions[projectile] = projectile.Position;
			m_prevVelocities[projectile] = projectile.Velocity;
		}

		public void Update(float dt)
		{
			if (!m_initialized)
				return;

			foreach (Projectile projectile in m_subsystemProjectiles.Projectiles)
			{
				if (Terrain.ExtractContents(projectile.Value) != BigStonePoisonChunkBlock.Index)
					continue;

				Vector3 prevPos;
				Vector3 prevVel;
				if (!m_prevPositions.TryGetValue(projectile, out prevPos))
				{
					// Proyectil recién aparecido (cargado de save, no disparado via OnFiredAsProjectile)
					m_prevPositions[projectile] = projectile.Position;
					m_prevVelocities[projectile] = projectile.Velocity;
					continue;
				}
				m_prevVelocities.TryGetValue(projectile, out prevVel);

				if (projectile.ToRemove && !m_processedProjectiles.Contains(projectile))
				{
					// El proyectil acaba de impactar y está marcado para remover.
					// Hacemos un raycast PRECISO desde la posición previa hasta la actual
					// (extendido hacia delante por si la posición no avanzó al colisionar)
					// para detectar ÚNICAMENTE el cuerpo golpeado.
					TryApplyPoisonToHitBody(projectile, prevPos, prevVel);
					m_processedProjectiles.Add(projectile);
				}
				else
				{
					m_prevPositions[projectile] = projectile.Position;
					m_prevVelocities[projectile] = projectile.Velocity;
				}
			}

			// Limpieza de entradas obsoletas
			List<Projectile> dead = new List<Projectile>();
			foreach (KeyValuePair<Projectile, Vector3> kvp in m_prevPositions)
			{
				if (!m_subsystemProjectiles.Projectiles.Contains(kvp.Key))
					dead.Add(kvp.Key);
			}
			foreach (Projectile p in dead)
			{
				m_prevPositions.Remove(p);
				m_prevVelocities.Remove(p);
			}
			m_processedProjectiles.RemoveWhere(p => !m_subsystemProjectiles.Projectiles.Contains(p));
		}

		private void TryApplyPoisonToHitBody(Projectile projectile, Vector3 prevPos, Vector3 prevVel)
		{
			if (projectile == null)
				return;

			Vector3 currPos = projectile.Position;

			// Dirección de viaje del proyectil. Preferimos (currPos - prevPos); si la longitud
			// es casi cero (la colisión no avanzó la posición), usamos la velocidad previa.
			Vector3 dir = currPos - prevPos;
			float len = dir.Length();
			if (len < 0.001f)
			{
				dir = prevVel;
				len = dir.Length();
				if (len < 0.001f)
					return; // No podemos determinar dirección, abortamos sin envenenar a nadie
			}
			Vector3 rayDir = dir / len;

			// Extendemos el raycast un poco por delante de la posición actual, por si la
			// colisión frenó el proyectil antes de alcanzar exactamente el cuerpo.
			float extend = MathUtils.Max(len, 2f);
			Vector3 rayStart = prevPos;
			Vector3 rayEnd = currPos + rayDir * extend;

			// Raycast contra cuerpos. Solo se aplica veneno al cuerpo REALMENTE golpeado.
			// Si el raycast no impacta ningún cuerpo (null), el proyectil golpeó el terreno
			// y no se envenena a ninguna criatura (comportamiento correcto, sin "ola").
			BodyRaycastResult? result = m_subsystemBodies.Raycast(
				rayStart, rayEnd, 0.2f,
				(ComponentBody body, float distance) => true
			);

			if (result.HasValue)
			{
				ComponentBody hitBody = result.Value.ComponentBody;
				if (hitBody != null && hitBody.Entity != null)
				{
					ComponentCreature creature = hitBody.Entity.FindComponent<ComponentCreature>();
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
	}
}
