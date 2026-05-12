using System;
using System.Reflection;
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
		// Componente de manada zombi (sin fallback al original)
		// ---------------------------------------------------------------
		private ComponentZombieHerdBehavior m_zombieHerdBehavior;

		// Control interno para aplicar la anulación de delay solo al inicio
		private bool m_greenNightForceApplied = false;

		// ---------------------------------------------------------------
		// Load: carga parámetros y reemplaza el manejador de daño
		// ---------------------------------------------------------------
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			AttacksSameHerd = valuesDictionary.GetValue<bool>("AttacksSameHerd", false);
			AttacksAllCategories = valuesDictionary.GetValue<bool>("AttacksAllCategories", false);
			FleeFromSameHerd = valuesDictionary.GetValue<bool>("FleeFromSameHerd", false);
			FleeDistance = valuesDictionary.GetValue<float>("FleeDistance", 15f);
			ForceAttackDuringGreenNight = valuesDictionary.GetValue<bool>("ForceAttackDuringGreenNight", false);

			m_zombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
			ReplaceInjuryHandler();
		}

		// ---------------------------------------------------------------
		// Update: agresividad durante la noche verde, cancelación de delay
		//         y vuelta a la normalidad al terminar el evento
		// ---------------------------------------------------------------
		public override void Update(float dt)
		{
			base.Update(dt);

			if (!ForceAttackDuringGreenNight)
				return;

			SubsystemGreenNightSky greenNight = base.Project.FindSubsystem<SubsystemGreenNightSky>(true);

			// Noche verde activa: forzar ataque sin esperas
			if (greenNight != null && greenNight.IsGreenNightActive)
			{
				if (!m_greenNightForceApplied)
				{
					// Cancelar tiempos de espera para que la persecución sea inmediata
					try
					{
						// Forzar que el tiempo en rango sea suficiente para activar la caza
						FieldInfo targetTimeField = typeof(ComponentChaseBehavior).GetField("m_targetInRangeTime",
							BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
						if (targetTimeField != null)
							targetTimeField.SetValue(this, 1f);

						// Reducir a cero el tiempo necesario para empezar a perseguir
						PropertyInfo chaseTimeProp = typeof(ComponentChaseBehavior).GetProperty("TargetInRangeTimeToChase",
							BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (chaseTimeProp != null && chaseTimeProp.CanWrite)
							chaseTimeProp.SetValue(this, 0f);

						// Eliminar cualquier supresión activa
						Suppressed = false;
						m_autoChaseSuppressionTime = 0f;
					}
					catch (Exception) { }
					m_greenNightForceApplied = true;
				}

				// Buscar al jugador más cercano y atacar sin límites
				SubsystemPlayers players = base.Project.FindSubsystem<SubsystemPlayers>(true);
				if (players == null)
					return;

				ComponentPlayer nearestPlayer = players.FindNearestPlayer(m_componentCreature.ComponentBody.Position);
				if (nearestPlayer == null || nearestPlayer.ComponentHealth.Health <= 0f)
					return;

				if (m_target == nearestPlayer && m_chaseTime > 0f)
					return;

				Attack(nearestPlayer, float.MaxValue, float.MaxValue, true);
			}
			else
			{
				// La noche verde terminó: restablecer el control y detener la persecución al jugador
				m_greenNightForceApplied = false;

				if (m_target != null && m_target.Entity.FindComponent<ComponentPlayer>() != null)
				{
					StopAttack();
				}
			}
		}

		// ---------------------------------------------------------------
		// FindTarget: NUNCA busca jugadores en estado normal.
		// Solo durante la noche verde se permite buscar jugadores.
		// ---------------------------------------------------------------
		public override ComponentCreature FindTarget()
		{
			ComponentCreature target = base.FindTarget();

			if (target == null)
				return null;

			bool isPlayer = target.Entity.FindComponent<ComponentPlayer>() != null;

			if (isPlayer)
			{
				if (ForceAttackDuringGreenNight)
				{
					SubsystemGreenNightSky greenNight = Project.FindSubsystem<SubsystemGreenNightSky>(true);
					if (greenNight != null && greenNight.IsGreenNightActive)
						return target;
				}
				return null;
			}

			return target;
		}

		// ---------------------------------------------------------------
		// Reemplaza el manejador de lesiones para respetar FleeFromSameHerd
		// ---------------------------------------------------------------
		private void ReplaceInjuryHandler()
		{
			ComponentHealth health = m_componentCreature.ComponentHealth;

			float chaseWhenAttackedProb = m_chaseWhenAttackedProbability;
			float? chaseRangeOnAttacked = ChaseRangeOnAttacked;
			float? chaseTimeOnAttacked = ChaseTimeOnAttacked;
			bool? chasePersistent = ChasePersistentOnAttacked;

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
		// Puntuación de objetivos personalizada
		// ---------------------------------------------------------------
		public override float ScoreTarget(ComponentCreature target)
		{
			if (target == m_componentCreature)
				return 0f;

			if (!AttacksSameHerd && IsSameHerd(target))
				return 0f;

			if (AttacksAllCategories)
			{
				if (target.ComponentHealth.Health <= 0f)
					return 0f;

				float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);
				if (dist < m_range)
					return m_range - dist + 1f;
				return 0f;
			}

			return base.ScoreTarget(target);
		}

		// ---------------------------------------------------------------
		// Verifica si otra criatura es de la misma manada zombi
		// ---------------------------------------------------------------
		private bool IsSameHerd(ComponentCreature other)
		{
			if (other == null || m_zombieHerdBehavior == null)
				return false;

			return m_zombieHerdBehavior.IsSameZombieHerd(other);
		}
	}
}
