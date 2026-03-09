using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewChaseBehavior : ComponentChaseBehavior, IUpdateable
	{
		// Referencia al comportamiento de manada (nuevo)
		private ComponentNewHerdBehavior m_newHerd;
		private SubsystemGreenNightSky m_greenNightSky;

		// Configuración: si es true, siempre tiene protección extrema (bandidos)
		public bool ExtremeProtectionAlways { get; set; }

		// Para recordar al último atacante y reaccionar rápidamente
		private ComponentCreature m_lastAttacker;
		private double m_lastAttackTime;
		private const float AttackerMemoryDuration = 30f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_newHerd = Entity.FindComponent<ComponentNewHerdBehavior>();
			m_greenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>(true);

			// Cargar configuración desde la base de datos
			ExtremeProtectionAlways = valuesDictionary.GetValue<bool>("ExtremeProtectionAlways", false);

			// Reemplazar el manejador de daño para reacción inmediata
			ComponentHealth health = m_componentCreature.ComponentHealth;
			Action<Injury> originalInjured = health.Injured;
			health.Injured = delegate (Injury injury)
			{
				originalInjured?.Invoke(injury);

				ComponentCreature attacker = injury.Attacker;
				if (attacker == null) return;

				// No reaccionar si el atacante es aliado
				if (m_newHerd != null && !m_newHerd.ShouldAttackCreature(attacker))
					return;

				// Recordar al atacante
				m_lastAttacker = attacker;
				m_lastAttackTime = m_subsystemTime.GameTime;

				// Contraatacar inmediatamente (sin esperar tiempos) y llamar ayuda
				if (m_target == null)
				{
					// Usar rango y tiempo generosos para asegurar persecución
					Attack(attacker, 40f, 120f, true);
				}

				// Llamar a otros miembros de la manada para que ayuden
				m_newHerd?.CallNearbyCreaturesHelp(attacker, 20f, 30f, false, true);
			};
		}

		public override void Update(float dt)
		{
			base.Update(dt);

			// Verificar si el objetivo actual es aliado (friendly fire prevention)
			if (m_target != null && m_newHerd != null && !m_newHerd.ShouldAttackCreature(m_target))
			{
				StopAttack();
			}

			// Protección extrema: si está activa, asegurar que el jugador sea el objetivo prioritario
			if (ShouldExtremeProtection() && m_target == null)
			{
				ComponentPlayer player = FindNearestPlayer(m_range);
				if (player != null && m_newHerd.ShouldAttackCreature(player))
				{
					Attack(player, m_range * 1.5f, 120f, true);
				}
			}

			// Recordar al último atacante y cambiar si es mejor objetivo
			if (m_lastAttacker != null && m_target != m_lastAttacker)
			{
				float timeSince = (float)(m_subsystemTime.GameTime - m_lastAttackTime);
				if (timeSince <= AttackerMemoryDuration && m_lastAttacker.ComponentHealth.Health > 0f)
				{
					float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position, m_lastAttacker.ComponentBody.Position);
					if (dist <= m_range * 1.2f)
					{
						Attack(m_lastAttacker, m_range, 120f, true);
					}
				}
				else
				{
					m_lastAttacker = null;
				}
			}
		}

		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			// No atacar a aliados
			if (m_newHerd != null && !m_newHerd.ShouldAttackCreature(componentCreature))
				return 0f;

			float baseScore = base.ScoreTarget(componentCreature);
			if (baseScore <= 0f) return 0f;

			// Bonus extremo para el jugador si la protección extrema está activa
			if (ShouldExtremeProtection() && componentCreature.Entity.FindComponent<ComponentPlayer>() != null)
			{
				baseScore *= 100f; // Prioridad absoluta
			}

			// Bonus para el último atacante
			if (componentCreature == m_lastAttacker)
			{
				float timeSince = (float)(m_subsystemTime.GameTime - m_lastAttackTime);
				if (timeSince <= AttackerMemoryDuration)
					baseScore *= 10f;
			}

			return baseScore;
		}

		public override ComponentCreature FindTarget()
		{
			// Si hay protección extrema, buscar primero al jugador
			if (ShouldExtremeProtection())
			{
				ComponentPlayer player = FindNearestPlayer(m_range);
				if (player != null && m_newHerd.ShouldAttackCreature(player))
					return player;
			}

			// Búsqueda normal
			return base.FindTarget();
		}

		// Método público para ser llamado por comandos (ej. bloque de control remoto)
		public void RespondToCommandImmediately(ComponentCreature target)
		{
			if (target == null || (m_newHerd != null && !m_newHerd.CanAttackCreature(target)))
				return;

			// Atacar inmediatamente
			Attack(target, 30f, 60f, true);

			// Llamar a otros miembros para que ayuden
			m_newHerd?.CallNearbyCreaturesHelp(target, 20f, 30f, false, true);
		}

		private bool ShouldExtremeProtection()
		{
			return ExtremeProtectionAlways ||
				   (m_greenNightSky != null && m_greenNightSky.IsGreenNightActive);
		}

		private ComponentPlayer FindNearestPlayer(float range)
		{
			if (m_componentCreature?.ComponentBody == null) return null;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			SubsystemPlayers players = Project.FindSubsystem<SubsystemPlayers>(true);
			ComponentPlayer nearest = null;
			float minDist = float.MaxValue;

			foreach (var player in players.ComponentPlayers)
			{
				if (player != null && player.ComponentHealth.Health > 0f)
				{
					float dist = Vector3.Distance(position, player.ComponentBody.Position);
					if (dist <= range && dist < minDist)
					{
						minDist = dist;
						nearest = player;
					}
				}
			}
			return nearest;
		}
	}
}
