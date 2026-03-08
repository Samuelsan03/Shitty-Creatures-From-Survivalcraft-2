using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewChaseBehavior : ComponentChaseBehavior, IUpdateable
	{
		// Referencias a componentes y subsistemas necesarios
		private ComponentNewHerdBehavior m_componentNewHerd;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private ComponentCreature m_lastAttacker;
		private double m_lastAttackTime;
		private float m_switchTargetCooldown;
		private const float AttackerMemoryDuration = 30f;

		// Variables para la protección extrema
		private bool m_isExtremeModeActive;
		private ComponentCreature m_threatToPlayer;
		private float m_threatScanTimer;

		// Bandera para saber si estamos en modo retaliación (solo atacar a quien nos atacó)
		private bool m_isRetaliating;
		private ComponentCreature m_retaliationTarget;

		public new UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Obtener componentes necesarios
			m_componentNewHerd = Entity.FindComponent<ComponentNewHerdBehavior>();
			m_subsystemGreenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>(true);

			// NO SOBREESCRIBIR los valores del XML - respetar lo configurado
			// Solo aseguramos que los valores mínimos sean razonables
			m_dayChaseRange = Math.Max(m_dayChaseRange, 1f);
			m_nightChaseRange = Math.Max(m_nightChaseRange, 1f);
			m_dayChaseTime = Math.Max(m_dayChaseTime, 1f);
			m_nightChaseTime = Math.Max(m_nightChaseTime, 1f);

			// Inicializar variables
			m_lastAttacker = null;
			m_lastAttackTime = 0f;
			m_switchTargetCooldown = 0f;
			m_threatScanTimer = 0f;
			m_isRetaliating = false;
			m_retaliationTarget = null;

			// --- Manejador de daño recibido ---
			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			Action<Injury> originalInjured = componentHealth.Injured;
			componentHealth.Injured = delegate (Injury injury)
			{
				originalInjured?.Invoke(injury);

				ComponentCreature attacker = injury.Attacker;

				// Ignorar si el atacante es de la misma manada
				if (attacker != null && m_componentNewHerd != null && !m_componentNewHerd.ShouldAttackCreature(attacker))
					return;

				// Registrar al atacante
				if (attacker != null)
				{
					m_lastAttacker = attacker;
					m_lastAttackTime = m_subsystemTime.GameTime;

					// Activar modo retaliación - solo atacar a este agresor
					m_isRetaliating = true;
					m_retaliationTarget = attacker;
				}

				// Contraatacar al agresor si la probabilidad lo permite
				if (attacker != null && m_random.Float(0f, 1f) < m_chaseWhenAttackedProbability)
				{
					float range, chaseTime;
					bool isPersistent;

					if (m_chaseWhenAttackedProbability >= 1f)
					{
						range = 30f;
						chaseTime = 60f;
						isPersistent = true;
					}
					else
					{
						range = 7f;
						chaseTime = 7f;
						isPersistent = false;
					}

					range = ChaseRangeOnAttacked.GetValueOrDefault(range);
					chaseTime = ChaseTimeOnAttacked.GetValueOrDefault(chaseTime);
					isPersistent = ChasePersistentOnAttacked.GetValueOrDefault(isPersistent);

					Attack(attacker, range, chaseTime, isPersistent);
				}
			};

			// --- Manejador de colisión (respetando m_autoChaseMask) ---
			ComponentBody componentBody = m_componentCreature.ComponentBody;
			Action<ComponentBody> originalCollided = componentBody.CollidedWithBody;
			componentBody.CollidedWithBody = delegate (ComponentBody body)
			{
				originalCollided?.Invoke(body);

				// Solo si no tenemos objetivo y no estamos en modo retaliación
				if (m_target != null || m_isRetaliating || m_autoChaseSuppressionTime > 0f)
					return;

				if (m_random.Float(0f, 1f) >= m_chaseOnTouchProbability)
					return;

				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature == null) return;

				// Ignorar si es de la misma manada
				if (m_componentNewHerd != null && !m_componentNewHerd.ShouldAttackCreature(creature))
					return;

				bool isPlayer = m_subsystemPlayers.IsPlayer(body.Entity);
				bool isValidTarget = (creature.Category & m_autoChaseMask) > 0;

				// Verificar según configuraciones del XML
				if ((AttacksPlayer && isPlayer && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
					(AttacksNonPlayerCreature && !isPlayer && isValidTarget))
				{
					Attack(creature, ChaseRangeOnTouch, ChaseTimeOnTouch, false);
				}
			};
		}

		public new void Update(float dt)
		{
			// Llamar al Update base para mantener la máquina de estados original
			base.Update(dt);

			// Actualizar temporizador de escaneo de amenazas
			m_threatScanTimer -= dt;
			if (m_threatScanTimer <= 0f)
			{
				m_threatScanTimer = 0.5f;

				// Verificar si la noche verde está activa
				bool greenNightActive = m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive;
				m_isExtremeModeActive = greenNightActive;

				// Escanear amenazas solo si no estamos en modo retaliación
				if (!m_isRetaliating)
				{
					if (m_isExtremeModeActive)
					{
						ScanForZombieThreats();
					}
					ScanForBanditThreats();
				}
			}

			// Actualizar cooldown de cambio de objetivo
			if (m_switchTargetCooldown > 0f)
				m_switchTargetCooldown -= dt;

			// Verificar si el objetivo de retaliación sigue siendo válido
			if (m_isRetaliating && m_retaliationTarget != null)
			{
				// Si el objetivo murió o pasó el tiempo de memoria, salir del modo retaliación
				if (m_retaliationTarget.ComponentHealth.Health <= 0f ||
					(m_subsystemTime.GameTime - m_lastAttackTime) > AttackerMemoryDuration)
				{
					m_isRetaliating = false;
					m_retaliationTarget = null;

					// Detener el ataque actual
					if (m_target != null && m_target == m_retaliationTarget)
					{
						StopAttack();
					}
				}
			}

			// Priorizar amenazas detectadas SOLO si no estamos en modo retaliación
			if (!m_isRetaliating && m_switchTargetCooldown <= 0f)
			{
				if (m_threatToPlayer != null && m_threatToPlayer != m_target)
				{
					// Verificar que la amenaza sea válida según las máscaras
					if (IsValidTargetByMask(m_threatToPlayer))
					{
						Attack(m_threatToPlayer, 40f, 120f, true);
						m_switchTargetCooldown = 2f;
						m_threatToPlayer = null;
					}
				}
			}

			// Asegurar que no se ataque a miembros de la propia manada
			if (m_target != null && m_componentNewHerd != null && !m_componentNewHerd.ShouldAttackCreature(m_target))
			{
				StopAttack();
			}
		}

		private bool IsValidTargetByMask(ComponentCreature target)
		{
			if (target == null) return false;

			bool isPlayer = target.Entity.FindComponent<ComponentPlayer>() != null;
			bool isValidCategory = (target.Category & m_autoChaseMask) > 0;

			if (isPlayer)
			{
				return AttacksPlayer && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
			}
			else
			{
				return AttacksNonPlayerCreature && isValidCategory;
			}
		}

		private void ScanForZombieThreats()
		{
			// Solo durante noche verde
			if (!m_isExtremeModeActive) return;

			SubsystemPlayers subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			if (subsystemPlayers == null || subsystemPlayers.ComponentPlayers.Count == 0)
				return;

			ComponentPlayer player = subsystemPlayers.ComponentPlayers[0];
			if (player == null || player.ComponentHealth.Health <= 0f)
				return;

			Vector3 playerPos = player.ComponentBody.Position;
			float scanRange = 30f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(playerPos.X, playerPos.Z), scanRange, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature == null || creature == m_componentCreature)
					continue;

				// Detectar zombies
				if (creature.Entity.FindComponent<ComponentZombieHerdBehavior>() != null)
				{
					// Verificar si el zombie está en rango de atacar al jugador
					float distToPlayer = Vector3.Distance(playerPos, creature.ComponentBody.Position);
					if (distToPlayer <= 15f) // Rango de ataque de zombie
					{
						m_threatToPlayer = creature;
						return;
					}
				}
			}
		}

		private void ScanForBanditThreats()
		{
			SubsystemPlayers subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			if (subsystemPlayers == null || subsystemPlayers.ComponentPlayers.Count == 0)
				return;

			ComponentPlayer player = subsystemPlayers.ComponentPlayers[0];
			if (player == null || player.ComponentHealth.Health <= 0f)
				return;

			Vector3 playerPos = player.ComponentBody.Position;
			float scanRange = 25f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(playerPos.X, playerPos.Z), scanRange, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature == null || creature == m_componentCreature)
					continue;

				// Detectar bandidos
				if (creature.Entity.FindComponent<ComponentBanditHerdBehavior>() != null)
				{
					// Verificar si el bandido tiene al jugador como objetivo
					ComponentChaseBehavior chase = creature.Entity.FindComponent<ComponentChaseBehavior>();
					if (chase != null && chase.Target == player)
					{
						float distToPlayer = Vector3.Distance(playerPos, creature.ComponentBody.Position);
						if (distToPlayer <= 20f) // Bandido en rango de ataque
						{
							m_threatToPlayer = creature;
							return;
						}
					}
				}
			}
		}

		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			// Excluir miembros de la misma manada
			if (m_componentNewHerd != null && !m_componentNewHerd.ShouldAttackCreature(componentCreature))
				return 0f;

			float score = base.ScoreTarget(componentCreature);
			if (score <= 0f) return 0f;

			// Durante noche verde, los zombies obtienen bonificación
			if (m_isExtremeModeActive)
			{
				bool isZombie = componentCreature.Entity.FindComponent<ComponentZombieHerdBehavior>() != null;
				if (isZombie)
				{
					score *= 2f; // Bonificación moderada, no masiva
				}
			}

			// Bonificación para amenazas al jugador
			if (componentCreature == m_threatToPlayer)
			{
				score *= 3f;
			}

			// Bonificación para el último atacante (solo si estamos en modo retaliación)
			if (m_isRetaliating && componentCreature == m_retaliationTarget)
			{
				score *= 5f;
			}
			else if (componentCreature == m_lastAttacker && (m_subsystemTime.GameTime - m_lastAttackTime) <= AttackerMemoryDuration)
			{
				// Recordar al último atacante pero con menor prioridad si no es retaliación activa
				score *= 2f;
			}

			return score;
		}

		public override ComponentCreature FindTarget()
		{
			// Si estamos en modo retaliación, solo buscar a nuestro objetivo
			if (m_isRetaliating && m_retaliationTarget != null)
			{
				if (m_retaliationTarget.ComponentHealth.Health > 0f)
				{
					float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position, m_retaliationTarget.ComponentBody.Position);
					if (dist <= m_range * 1.5f)
					{
						return m_retaliationTarget;
					}
				}
				else
				{
					m_isRetaliating = false;
					m_retaliationTarget = null;
				}
			}

			// Si hay amenaza al jugador y no estamos en retaliación, priorizarla
			if (!m_isRetaliating && m_threatToPlayer != null && IsValidTargetByMask(m_threatToPlayer))
			{
				return m_threatToPlayer;
			}

			// Buscar objetivo normalmente respetando AutoChaseMask
			return base.FindTarget();
		}

		public override void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			// Verificar que el objetivo sea válido según las máscaras
			if (!IsValidTargetByMask(componentCreature))
				return;

			// Verificar que no sea de la misma manada
			if (m_componentNewHerd != null && !m_componentNewHerd.ShouldAttackCreature(componentCreature))
				return;

			// Llamar al método base
			base.Attack(componentCreature, maxRange, maxChaseTime, isPersistent);

			// Solo llamar ayuda si no estamos en retaliación (evitar spam)
			if (!m_isRetaliating && m_componentNewHerd != null)
			{
				m_componentNewHerd.CallNearbyCreaturesHelp(componentCreature, 20f, 30f, false, false);
			}
		}

		public override void StopAttack()
		{
			base.StopAttack();

			// No resetear el modo retaliación aquí, se maneja en Update
		}

		public void RespondToCommandImmediately(ComponentCreature target)
		{
			if (target == null) return;
			if (!IsValidTargetByMask(target)) return;
			if (m_componentNewHerd != null && !m_componentNewHerd.CanAttackCreature(target))
				return;

			// Resetear modo retaliación al recibir orden directa
			m_isRetaliating = false;
			m_retaliationTarget = null;

			// Atacar con parámetros agresivos
			Attack(target, 40f, 120f, true);

			// Llamar a la manada
			m_componentNewHerd?.CallNearbyCreaturesHelp(target, 15f, 30f, false, true);
		}
	}
}
