using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieChaseBehavior : ComponentChaseBehavior, IUpdateable
	{
		// ---------------------------------------------------------------
		// Parámetros específicos del zombi (cargados desde XML)
		// ---------------------------------------------------------------
		public bool AttacksSameHerd { get; set; } = false;
		public bool AttacksAllCategories { get; set; } = false;
		public bool FleeFromSameHerd { get; set; } = false;
		public float FleeDistance { get; set; } = 15f;
		public bool ForceAttackDuringGreenNight { get; set; } = false;

		// ---------------------------------------------------------------
		// Componentes internos
		// ---------------------------------------------------------------
		private ComponentZombieHerdBehavior m_zombieHerdBehavior;
		private ComponentHerdBehavior m_herdBehavior;    // fallback para rebaños vanilla

		// ---------------------------------------------------------------
		// Load: carga parámetros y reemplaza el manejador de daño
		// ---------------------------------------------------------------
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Carga todo el comportamiento de persecución base
			base.Load(valuesDictionary, idToEntityMap);

			// Carga los parámetros específicos del zombi
			AttacksSameHerd = valuesDictionary.GetValue<bool>("AttacksSameHerd", false);
			AttacksAllCategories = valuesDictionary.GetValue<bool>("AttacksAllCategories", false);
			FleeFromSameHerd = valuesDictionary.GetValue<bool>("FleeFromSameHerd", false);
			FleeDistance = valuesDictionary.GetValue<float>("FleeDistance", 15f);
			ForceAttackDuringGreenNight = valuesDictionary.GetValue<bool>("ForceAttackDuringGreenNight", false);

			// Referencia al componente de manada
			m_zombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
			if (m_zombieHerdBehavior == null)
			{
				m_herdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();
			}

			// Sustituye el manejador de lesiones para incluir la huida entre miembros de la misma manada
			ReplaceInjuryHandler();
		}

		// ---------------------------------------------------------------
		// Update: añade la agresividad extrema durante la noche verde
		// ---------------------------------------------------------------
		public override void Update(float dt)
		{
			base.Update(dt);

			if (!ForceAttackDuringGreenNight)
				return;

			// Comprueba si la noche verde está activa
			SubsystemGreenNightSky greenNight = base.Project.FindSubsystem<SubsystemGreenNightSky>(true);
			if (greenNight == null || !greenNight.IsGreenNightActive)
				return;

			// Si la persecución está suprimida por cualquier motivo, la forzamos
			if (Suppressed)
				Suppressed = false;

			// Busca al jugador más cercano
			SubsystemPlayers players = base.Project.FindSubsystem<SubsystemPlayers>(true);
			if (players == null)
				return;

			ComponentPlayer nearestPlayer = players.FindNearestPlayer(m_componentCreature.ComponentBody.Position);
			if (nearestPlayer == null || nearestPlayer.ComponentHealth.Health <= 0f)
				return;

			// Si ya estamos persiguiendo a un jugador y la persecución sigue activa, no hacemos nada
			if (m_target == nearestPlayer && m_chaseTime > 0f)
				return;

			// De lo contrario, forzamos una persecución persistente con rango y tiempo infinitos
			Attack(nearestPlayer, float.MaxValue, float.MaxValue, true);
		}

		// ---------------------------------------------------------------
		// Reemplaza el manejador de lesiones para que respete FleeFromSameHerd
		// Si el atacante es de la misma manada y FleeFromSameHerd es true,
		// el zombi huirá en lugar de contraatacar.
		// ---------------------------------------------------------------
		private void ReplaceInjuryHandler()
		{
			ComponentHealth health = m_componentCreature.ComponentHealth;

			// Guardamos los valores originales leídos del XML
			float chaseWhenAttackedProb = m_chaseWhenAttackedProbability;
			float? chaseRangeOnAttacked = ChaseRangeOnAttacked;
			float? chaseTimeOnAttacked = ChaseTimeOnAttacked;
			bool? chasePersistent = ChasePersistentOnAttacked;

			// Nuevo manejador
			health.Injured = delegate (Injury injury)
			{
				if (injury.Attacker == null)
					return;

				bool isSameHerdAttacker = IsSameHerd(injury.Attacker);

				if (isSameHerdAttacker && FleeFromSameHerd)
				{
					StartFleeing(injury.Attacker);
					return;
				}

				// Si no hay huida, aplica la lógica de contraataque normal
				if (m_random.Float(0f, 1f) < chaseWhenAttackedProb)
				{
					bool persistent = false;
					float range = 7f;
					float time = 7f;

					if (chaseWhenAttackedProb >= 1f)
					{
						range = 30f;
						time = 60f;
						persistent = true;
					}

					range = chaseRangeOnAttacked.GetValueOrDefault(range);
					time = chaseTimeOnAttacked.GetValueOrDefault(time);
					persistent = chasePersistent.GetValueOrDefault(persistent);

					Attack(injury.Attacker, range, time, persistent);
				}
			};
		}

		// ---------------------------------------------------------------
		// Inicia la huida: detiene la persecución actual y huye
		// ---------------------------------------------------------------
		private void StartFleeing(ComponentCreature attacker)
		{
			base.StopAttack();
			m_autoChaseSuppressionTime = 5f;

			Vector3 myPos = m_componentCreature.ComponentBody.Position;
			Vector3 attackerPos = attacker.ComponentBody.Position;
			Vector3 away = myPos - attackerPos;

			if (away.LengthSquared() < 0.01f)
			{
				away = new Vector3(m_random.Float(-1f, 1f), 0f, m_random.Float(-1f, 1f));
			}

			away = Vector3.Normalize(away);
			Vector3 destination = myPos + away * FleeDistance;

			m_componentPathfinding.SetDestination(
				new Vector3?(destination),
				1f,
				1.5f,
				0,
				false,
				true,
				false,
				null
			);

			m_componentCreature.ComponentCreatureSounds.PlayPainSound();
		}

		// ---------------------------------------------------------------
		// Puntuación de objetivos personalizada:
		// - Respeta AttacksSameHerd y AttacksAllCategories
		// ---------------------------------------------------------------
		public override float ScoreTarget(ComponentCreature target)
		{
			if (target == m_componentCreature)
				return 0f;

			// ¿Puede atacar a miembros de su misma manada?
			if (!AttacksSameHerd && IsSameHerd(target))
				return 0f;

			if (AttacksAllCategories)
			{
				// Ataca a todo lo que esté vivo y dentro del rango
				if (target.ComponentHealth.Health <= 0f)
					return 0f;

				float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);
				if (dist < m_range)
					return m_range - dist + 1f; // pequeño bonus para que cualquier criatura sea válida
				return 0f;
			}

			// En cualquier otro caso usa la lógica base (respeta máscaras, jugadores, etc.)
			return base.ScoreTarget(target);
		}

		// ---------------------------------------------------------------
		// Comprueba si otra criatura pertenece a la misma manada
		// ---------------------------------------------------------------
		private bool IsSameHerd(ComponentCreature other)
		{
			if (other == null)
				return false;

			// Usa el comportamiento de manada zombi si está disponible
			if (m_zombieHerdBehavior != null)
			{
				return m_zombieHerdBehavior.IsSameZombieHerd(other);
			}

			// Fallback al comportamiento de manada genérico
			if (m_herdBehavior != null && !string.IsNullOrEmpty(m_herdBehavior.HerdName))
			{
				ComponentHerdBehavior otherHerd = other.Entity.FindComponent<ComponentHerdBehavior>();
				if (otherHerd != null && !string.IsNullOrEmpty(otherHerd.HerdName))
					return otherHerd.HerdName == m_herdBehavior.HerdName;
			}

			return false;
		}
	}
}
