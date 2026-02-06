using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieChaseBehavior : ComponentChaseBehavior
	{
		private ComponentZombieHerdBehavior m_componentZombieHerdBehavior;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private Dictionary<ComponentCreature, float> m_lastAttackTimes = new Dictionary<ComponentCreature, float>();
		private float m_retaliationMemoryDuration = 30f;
		private ComponentCreature m_lastAttacker;
		private float m_retaliationCooldown;

		private bool m_attacksSameHerd;
		private bool m_attacksAllCategories;
		private bool m_fleeFromSameHerd;
		private float m_fleeDistance = 10f;
		private bool m_forceAttackDuringGreenNight;

		private bool m_isGreenNightActive;
		private ComponentPlayer m_primaryGreenNightTarget;
		private float m_greenNightTargetSwitchCooldown;
		private float m_greenNightAggressionRange = 100f;
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemPlayers m_subsystemPlayers;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_componentZombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
			m_subsystemGreenNightSky = base.Project.FindSubsystem<SubsystemGreenNightSky>(true);
			m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);

			m_attacksSameHerd = valuesDictionary.GetValue<bool>("AttacksSameHerd", false);
			m_attacksAllCategories = valuesDictionary.GetValue<bool>("AttacksAllCategories", true);
			m_fleeFromSameHerd = valuesDictionary.GetValue<bool>("FleeFromSameHerd", true);
			m_fleeDistance = valuesDictionary.GetValue<float>("FleeDistance", 10f);
			m_forceAttackDuringGreenNight = valuesDictionary.GetValue<bool>("ForceAttackDuringGreenNight", false);

			if (m_attacksAllCategories)
			{
				m_autoChaseMask = CreatureCategory.LandPredator |
								  CreatureCategory.LandOther |
								  CreatureCategory.WaterPredator |
								  CreatureCategory.WaterOther |
								  CreatureCategory.Bird;
				this.AttacksNonPlayerCreature = true;
				this.AttacksPlayer = true;
			}

			this.SetupZombieInjuryHandler();
			this.AddFleeState();
		}

		private void SetupZombieInjuryHandler()
		{
			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
			Action<Injury> originalHandler = componentHealth.Injured;

			Action<Injury> zombieInjuryHandler = delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;

				if (attacker != null)
				{
					m_lastAttackTimes[attacker] = m_retaliationMemoryDuration;
					m_lastAttacker = attacker;

					// Si otro mob ataca durante Noche Verde, atacarlo DE VUELTA
					if (attacker != this.m_target && !IsSameHerd(attacker))
					{
						this.StopAttack();
						this.Attack(attacker, 30f, 60f, true);
						m_retaliationCooldown = 2f;
						return;
					}
				}

				if (attacker != null && !m_attacksSameHerd && IsSameHerd(attacker))
				{
					if (m_componentZombieHerdBehavior != null && m_componentZombieHerdBehavior.CallForHelpWhenAttacked)
					{
						ComponentCreature externalAttacker = FindExternalAttacker(injury);
						if (externalAttacker != null)
						{
							m_componentZombieHerdBehavior.CallZombiesForHelp(externalAttacker);
						}
					}

					if (m_fleeFromSameHerd)
					{
						FleeFromTarget(attacker);
					}

					return;
				}

				if (originalHandler != null)
				{
					originalHandler(injury);
				}
			};

			componentHealth.Injured = zombieInjuryHandler;
		}

		private ComponentCreature FindExternalAttacker(Injury injury)
		{
			if (injury.Attacker == null) return null;

			if (!IsSameHerd(injury.Attacker))
			{
				return injury.Attacker;
			}

			return null;
		}

		private bool IsSameHerd(ComponentCreature otherCreature)
		{
			if (otherCreature == null || m_componentZombieHerdBehavior == null)
				return false;

			return m_componentZombieHerdBehavior.IsSameZombieHerd(otherCreature);
		}

		private void GreenNightAttack(ComponentPlayer player)
		{
			if (player == null || player.ComponentHealth.Health <= 0f)
				return;

			// ATACAR JUGADOR SIN IMPORTAR MODO DE JUEGO
			this.Suppressed = false;
			this.AttacksPlayer = true;
			this.m_target = player;
			this.m_range = m_greenNightAggressionRange;
			this.m_chaseTime = 9999f;
			this.m_isPersistent = true;
			this.m_importanceLevel = 9999f;

			if (this.m_stateMachine.CurrentState != "Chasing")
			{
				this.m_stateMachine.TransitionTo("Chasing");
			}

			if (m_componentZombieHerdBehavior != null)
			{
				m_componentZombieHerdBehavior.CoordinateGroupAttack(player);
			}
		}

		public override void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			// DURANTE NOCHE VERDE: Comportamiento especial
			if (m_isGreenNightActive && m_forceAttackDuringGreenNight)
			{
				ComponentPlayer playerTarget = target.Entity.FindComponent<ComponentPlayer>();
				if (playerTarget != null)
				{
					GreenNightAttack(playerTarget);
					return;
				}

				// Si no es jugador, atacarlo normalmente
				base.Attack(target, maxRange, maxChaseTime, isPersistent);
				return;
			}

			// Comportamiento normal
			base.Attack(target, maxRange, maxChaseTime, isPersistent);
		}

		private ComponentPlayer FindNearestPlayer(float range)
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentPlayer nearestPlayer = null;
			float nearestDistance = float.MaxValue;

			if (m_subsystemPlayers != null)
			{
				foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
				{
					if (player != null && player.ComponentHealth.Health > 0f)
					{
						float distance = Vector3.Distance(position, player.ComponentBody.Position);
						if (distance <= range && distance < nearestDistance)
						{
							nearestDistance = distance;
							nearestPlayer = player;
						}
					}
				}
			}

			return nearestPlayer;
		}

		public override ComponentCreature FindTarget()
		{
			// DURANTE NOCHE VERDE: BUSCAR JUGADORES PRIMERO
			if (m_isGreenNightActive && m_forceAttackDuringGreenNight)
			{
				ComponentPlayer nearestPlayer = FindNearestPlayer(m_greenNightAggressionRange);
				if (nearestPlayer != null && nearestPlayer.ComponentHealth.Health > 0f)
				{
					return nearestPlayer;
				}

				// Si no hay jugadores, buscar otros objetivos normalmente
				return base.FindTarget();
			}

			// Comportamiento normal
			return base.FindTarget();
		}

		// ¡¡¡SOBREESCRIBIR COMPLETAMENTE ScoreTarget para Noche Verde!!!
		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			// DURANTE NOCHE VERDE: IGNORAR COMPLETAMENTE EL MODO DE JUEGO
			if (m_isGreenNightActive && m_forceAttackDuringGreenNight)
			{
				ComponentPlayer player = componentCreature.Entity.FindComponent<ComponentPlayer>();
				if (player != null)
				{
					// ¡¡¡ATACAR JUGADORES EN CUALQUIER MODO!!!
					float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, player.ComponentBody.Position);
					if (distance <= m_greenNightAggressionRange)
					{
						// Puntaje MÁXIMO para jugadores durante Noche Verde
						return 1000000f + (m_greenNightAggressionRange - distance);
					}
					return 0f;
				}

				// Para otros mobs, usar lógica simplificada que no verifica GameMode
				bool flag2 = this.m_componentCreature.Category != CreatureCategory.WaterPredator &&
							this.m_componentCreature.Category != CreatureCategory.WaterOther;
				bool flag4 = (componentCreature.Category & this.m_autoChaseMask) > (CreatureCategory)0;

				if (componentCreature != this.m_componentCreature && flag4 &&
					componentCreature.Entity.IsAddedToProject &&
					componentCreature.ComponentHealth.Health > 0f &&
					(flag2 || this.IsTargetInWater(componentCreature.ComponentBody)))
				{
					float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, componentCreature.ComponentBody.Position);
					if (distance < this.m_range) // Usar el rango actual
					{
						return this.m_range - distance;
					}
				}
				return 0f;
			}

			// Fuera de Noche Verde: comportamiento normal (respetar GameMode)
			return base.ScoreTarget(componentCreature);
		}

		private void AddFleeState()
		{
			this.m_stateMachine.AddState("Fleeing", delegate
			{
				this.m_importanceLevel = 150f;
				this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
			}, delegate
			{
				if (m_isGreenNightActive && m_forceAttackDuringGreenNight)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
					return;
				}

				if (this.m_componentCreature.ComponentHealth.Health < 0.2f)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
					return;
				}

				if (this.m_target == null || this.m_componentCreature.ComponentHealth.Health <= 0f)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
					return;
				}

				Vector3 fleeDirection = this.m_componentCreature.ComponentBody.Position - this.m_target.ComponentBody.Position;
				if (fleeDirection.LengthSquared() > 0.01f)
				{
					fleeDirection = Vector3.Normalize(fleeDirection);
					Vector3 destination = this.m_componentCreature.ComponentBody.Position + fleeDirection * m_fleeDistance;

					this.m_componentPathfinding.SetDestination(
						new Vector3?(destination),
						1f,
						1.5f,
						0,
						false,
						true,
						false,
						null
					);
				}

				float distanceToTarget = Vector3.Distance(
					this.m_componentCreature.ComponentBody.Position,
					this.m_target.ComponentBody.Position
				);

				if (distanceToTarget > m_fleeDistance * 1.5f)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}

				if (this.m_random.Float(0f, 1f) < 0.05f * this.m_dt)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
				}
			}, delegate
			{
				this.m_componentPathfinding.Stop();
				this.m_importanceLevel = 0f;
			});
		}

		private void FleeFromTarget(ComponentCreature target)
		{
			if (m_isGreenNightActive && m_forceAttackDuringGreenNight)
			{
				return;
			}

			if (this.m_componentCreature.ComponentHealth.Health < 0.2f)
			{
				return;
			}

			if (target == null || this.m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			this.m_target = target;
			this.m_stateMachine.TransitionTo("Fleeing");
		}

		public override void Update(float dt)
		{
			bool wasGreenNightActive = m_isGreenNightActive;
			m_isGreenNightActive = (m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive);

			if (!wasGreenNightActive && m_isGreenNightActive && m_forceAttackDuringGreenNight)
			{
				m_primaryGreenNightTarget = null;
				m_greenNightTargetSwitchCooldown = 0f;

				this.Suppressed = false;
				this.AttacksPlayer = true;

				// Buscar jugador inmediatamente al comenzar Noche Verde
				ComponentPlayer nearestPlayer = FindNearestPlayer(m_greenNightAggressionRange);
				if (nearestPlayer != null)
				{
					m_primaryGreenNightTarget = nearestPlayer;
					GreenNightAttack(nearestPlayer);
				}
			}

			if (wasGreenNightActive && !m_isGreenNightActive)
			{
				m_primaryGreenNightTarget = null;
				this.AttacksPlayer = m_attacksAllCategories;

				// Si estaba persiguiendo, detener
				if (this.m_target != null)
				{
					this.StopAttack();
				}
			}

			base.Update(dt);

			if (m_retaliationCooldown > 0f) m_retaliationCooldown -= dt;
			if (m_greenNightTargetSwitchCooldown > 0f) m_greenNightTargetSwitchCooldown -= dt;

			// Actualizar tiempos de ataque
			List<ComponentCreature> toRemove = new List<ComponentCreature>();
			foreach (var kvp in m_lastAttackTimes)
			{
				m_lastAttackTimes[kvp.Key] = kvp.Value - dt;
				if (m_lastAttackTimes[kvp.Key] <= 0f)
				{
					toRemove.Add(kvp.Key);
					if (kvp.Key == m_lastAttacker) m_lastAttacker = null;
				}
			}
			foreach (var creature in toRemove) m_lastAttackTimes.Remove(creature);

			if (m_isGreenNightActive && m_forceAttackDuringGreenNight)
			{
				this.AttacksPlayer = true;
				this.Suppressed = false;

				if (this.m_stateMachine.CurrentState == "Fleeing")
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}

				// Si está atacando a un no-jugador, verificar si hay jugadores cerca
				if (this.m_target != null && this.m_target.Entity.FindComponent<ComponentPlayer>() == null)
				{
					if (m_greenNightTargetSwitchCooldown <= 0f)
					{
						ComponentPlayer nearestPlayer = FindNearestPlayer(50f);
						if (nearestPlayer != null)
						{
							this.StopAttack();
							GreenNightAttack(nearestPlayer);
							m_greenNightTargetSwitchCooldown = 2f;
						}
					}
				}
				// Si no tiene objetivo, buscar jugador
				else if (this.m_target == null)
				{
					if (m_greenNightTargetSwitchCooldown <= 0f)
					{
						ComponentPlayer nearestPlayer = FindNearestPlayer(m_greenNightAggressionRange);
						if (nearestPlayer != null)
						{
							GreenNightAttack(nearestPlayer);
							m_greenNightTargetSwitchCooldown = 2f;
						}
					}
				}
			}
		}

		public override void StopAttack()
		{
			if (m_isGreenNightActive && m_forceAttackDuringGreenNight && m_primaryGreenNightTarget != null)
			{
				this.m_target = null;
				this.m_stateMachine.TransitionTo("LookingForTarget");
				return;
			}

			base.StopAttack();
		}
	}
}
