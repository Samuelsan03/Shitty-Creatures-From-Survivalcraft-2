using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewChaseBehavior : ComponentChaseBehavior, IUpdateable
	{
		// Referencias a subsistemas y componentes
		private ComponentNewHerdBehavior m_newHerdBehavior;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemGreenNightSky m_greenNightSky;
		private SubsystemTime m_subsystemTime;
		private new Random m_random = new Random(); // oculta el campo de la clase base

		// Configuración de defensa proactiva
		public float ProactiveDefenseRange { get; set; } = 20f;
		public float ProactiveDefenseInterval { get; set; } = 2f;
		public float GreenNightDefenseRangeMultiplier { get; set; } = 1.5f;
		public float GreenNightDefenseInterval { get; set; } = 1f;

		private double m_nextProactiveCheckTime;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Obtener componentes y subsistemas necesarios
			m_newHerdBehavior = Entity.FindComponent<ComponentNewHerdBehavior>();
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_greenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>();

			// Si la criatura pertenece a la manada del jugador, ajustar comportamiento por defecto
			if (IsPlayerHerd())
			{
				AttacksPlayer = false;               // No atacar al jugador
				AttacksNonPlayerCreature = true;      // Atacar a otras criaturas
													  // Configurar máscara para perseguir depredadores y otras criaturas hostiles
				m_autoChaseMask = CreatureCategory.LandPredator | CreatureCategory.LandOther |
								  CreatureCategory.WaterPredator | CreatureCategory.WaterOther |
								  CreatureCategory.Bird;
				// Aumentar probabilidades de respuesta a ataques y colisiones
				m_chaseWhenAttackedProbability = 1f;
				m_chaseOnTouchProbability = 1f;
			}

			// No es necesario reemplazar los manejadores de eventos, ya que la sobreescritura de Attack
			// filtra cualquier intento de atacar a miembros de la misma manada.
		}

		public override void Update(float dt)
		{
			base.Update(dt);

			// Defensa proactiva solo para criaturas de la manada del jugador
			if (m_newHerdBehavior != null && IsPlayerHerd())
			{
				bool isGreenNight = m_greenNightSky != null && m_greenNightSky.IsGreenNightActive;
				float checkInterval = isGreenNight ? GreenNightDefenseInterval : ProactiveDefenseInterval;
				float range = isGreenNight ? ProactiveDefenseRange * GreenNightDefenseRangeMultiplier : ProactiveDefenseRange;

				if (m_subsystemTime.GameTime >= m_nextProactiveCheckTime)
				{
					m_nextProactiveCheckTime = m_subsystemTime.GameTime + checkInterval;
					PerformProactiveDefense(range, isGreenNight);
				}
			}
		}

		/// <summary>
		/// Determina si la criatura pertenece a la manada del jugador (nombre "player" o "guardian").
		/// Maneja de forma segura el caso en que HerdName sea null.
		/// </summary>
		private bool IsPlayerHerd()
		{
			if (m_newHerdBehavior == null) return false;
			string herdName = m_newHerdBehavior.HerdName;
			if (string.IsNullOrEmpty(herdName)) return false;
			return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
				   herdName.IndexOf("guardian", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		/// <summary>
		/// Escanea en busca de amenazas cerca del jugador y ataca a la más relevante.
		/// </summary>
		private void PerformProactiveDefense(float range, bool isGreenNight)
		{
			// Si ya está persiguiendo un objetivo, no interrumpir
			if (m_target != null && m_importanceLevel > 0f)
				return;

			var players = m_subsystemPlayers.ComponentPlayers;
			if (players.Count == 0)
				return;

			// Encontrar el jugador más cercano a esta criatura
			Vector3 myPos = m_componentCreature.ComponentBody.Position;
			ComponentPlayer nearestPlayer = null;
			float minDistToPlayer = float.MaxValue;
			foreach (var player in players)
			{
				if (player.ComponentHealth.Health <= 0f) continue;
				float dist = Vector3.Distance(myPos, player.ComponentBody.Position);
				if (dist < minDistToPlayer)
				{
					minDistToPlayer = dist;
					nearestPlayer = player;
				}
			}
			if (nearestPlayer == null)
				return;

			// Solo defender si estamos razonablemente cerca del jugador
			if (minDistToPlayer > range * 2)
				return;

			Vector3 playerPos = nearestPlayer.ComponentBody.Position;
			ComponentCreature bestThreat = null;
			float bestScore = 0f;

			// Buscar cuerpos alrededor del jugador
			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(playerPos.X, playerPos.Z), range, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature candidate = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (candidate == null || candidate == m_componentCreature) continue;
				if (candidate.ComponentHealth.Health <= 0f) continue;

				// Verificar si es una criatura que debemos atacar
				if (!m_newHerdBehavior.ShouldAttackCreature(candidate))
					continue;

				float distToPlayer = Vector3.Distance(playerPos, candidate.ComponentBody.Position);
				if (distToPlayer > range) continue;

				float score = range - distToPlayer;

				// Incrementar puntuación si la criatura ya está persiguiendo al jugador o a un aliado
				ComponentChaseBehavior candidateChase = candidate.Entity.FindComponent<ComponentChaseBehavior>();
				if (candidateChase != null && candidateChase.Target != null)
				{
					ComponentCreature target = candidateChase.Target;
					if (target != null)
					{
						bool isTargetPlayer = target.Entity.FindComponent<ComponentPlayer>() != null;
						bool isTargetAlly = m_newHerdBehavior.IsSameHerdOrGuardian(target);
						if (isTargetPlayer || isTargetAlly)
						{
							score *= 2f; // Prioridad alta
						}
					}
				}

				// Durante noche verde, dar máxima prioridad a zombis
				if (isGreenNight && candidate.Entity.FindComponent<ComponentZombieChaseBehavior>() != null)
				{
					score *= 5f;
				}

				if (score > bestScore)
				{
					bestScore = score;
					bestThreat = candidate;
				}
			}

			if (bestThreat != null)
			{
				// Atacar a la amenaza con persistencia
				Attack(bestThreat, range, 60f, true);
				// Llamar a otros miembros de la manada cercanos para ayudar
				m_newHerdBehavior?.CallNearbyCreaturesHelp(bestThreat, range, 30f, true, true);
			}
		}

		/// <summary>
		/// Responde inmediatamente a una orden del jugador (usando el bloque TargetStick).
		/// Ataca al objetivo y convoca ayuda de la manada.
		/// </summary>
		public void RespondToCommandImmediately(ComponentCreature target)
		{
			if (target == null || m_newHerdBehavior == null || !m_newHerdBehavior.CanAttackCreature(target))
				return;

			// Atacar al objetivo marcado
			Attack(target, 30f, 45f, true);
			// Llamar a miembros cercanos de la manada para que también ataquen
			m_newHerdBehavior.CallNearbyCreaturesHelp(target, 20f, 30f, false, true);
		}

		public override void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			// Prevenir ataque a miembros de la misma manada o aliados
			if (m_newHerdBehavior != null && !m_newHerdBehavior.CanAttackCreature(target))
				return;

			base.Attack(target, maxRange, maxChaseTime, isPersistent);
		}

		public override float ScoreTarget(ComponentCreature creature)
		{
			// Excluir objetivos de la misma manada
			if (m_newHerdBehavior != null && !m_newHerdBehavior.CanAttackCreature(creature))
				return 0f;

			return base.ScoreTarget(creature);
		}

		public override ComponentCreature FindTarget()
		{
			ComponentCreature baseTarget = base.FindTarget();
			// Filtrar si el objetivo encontrado es de la misma manada
			if (baseTarget != null && m_newHerdBehavior != null && !m_newHerdBehavior.CanAttackCreature(baseTarget))
				return null;

			return baseTarget;
		}
	}
}
