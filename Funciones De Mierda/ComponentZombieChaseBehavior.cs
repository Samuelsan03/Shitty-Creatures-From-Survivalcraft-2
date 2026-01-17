using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieChaseBehavior : ComponentChaseBehavior
	{
		// Referencia al comportamiento de manada de zombis
		private ComponentZombieHerdBehavior m_componentZombieHerdBehavior;

		// Parámetros específicos de zombis
		private bool m_attacksSameHerd;
		private bool m_attacksAllCategories;
		private bool m_fleeFromSameHerd;
		private float m_fleeDistance = 10f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Llamar al método base primero para inicializar el comportamiento de persecución base
			base.Load(valuesDictionary, idToEntityMap);

			// Obtener referencia al ComponentZombieHerdBehavior en la misma entidad
			m_componentZombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();

			// Cargar parámetros específicos de zombis
			m_attacksSameHerd = valuesDictionary.GetValue<bool>("AttacksSameHerd", false);
			m_attacksAllCategories = valuesDictionary.GetValue<bool>("AttacksAllCategories", true);
			m_fleeFromSameHerd = valuesDictionary.GetValue<bool>("FleeFromSameHerd", true);
			m_fleeDistance = valuesDictionary.GetValue<float>("FleeDistance", 10f);

			// Configurar para atacar a todas las categorías si está habilitado
			if (m_attacksAllCategories)
			{
				// Combinar todas las categorías disponibles
				m_autoChaseMask = CreatureCategory.LandPredator |
								  CreatureCategory.LandOther |
								  CreatureCategory.WaterPredator |
								  CreatureCategory.WaterOther |
								  CreatureCategory.Bird;
				this.AttacksNonPlayerCreature = true;
				this.AttacksPlayer = true;
			}

			// Sobrescribir el manejador de lesiones para añadir lógica específica de zombis
			this.SetupZombieInjuryHandler();

			// Añadir estado de huida para cuando es atacado por miembros de la misma manada
			this.AddFleeState();
		}

		// Configurar el handler de lesiones específico para zombis
		private void SetupZombieInjuryHandler()
		{
			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;

			// Guardar el handler original
			Action<Injury> originalHandler = componentHealth.Injured;

			// Crear un nuevo handler que incluya la lógica de zombis
			Action<Injury> zombieInjuryHandler = delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;

				// Si el atacante es de la misma manada y no se permite atacar a la misma manada
				if (attacker != null && !m_attacksSameHerd && IsSameHerd(attacker))
				{
					// No atacar a miembros de la misma manada
					// En su lugar, llamar a otros zombis para ayuda si es atacado por algo externo
					if (m_componentZombieHerdBehavior != null && m_componentZombieHerdBehavior.CallForHelpWhenAttacked)
					{
						// Verificar si hay un agresor externo (no de la misma manada)
						ComponentCreature externalAttacker = FindExternalAttacker(injury);
						if (externalAttacker != null)
						{
							m_componentZombieHerdBehavior.CallZombiesForHelp(externalAttacker);
						}
					}

					// Si está configurado para huir de miembros de la misma manada, activar el estado de huida
					if (m_fleeFromSameHerd)
					{
						FleeFromTarget(attacker);
					}

					// No llamar al handler original para evitar perseguir a miembros de la misma manada
					return;
				}

				// Llamar al handler original si no es un miembro de la misma manada
				if (originalHandler != null)
				{
					originalHandler(injury);
				}
			};

			// Asignar el nuevo handler
			componentHealth.Injured = zombieInjuryHandler;
		}

		// Método para encontrar un atacante externo (no de la misma manada)
		private ComponentCreature FindExternalAttacker(Injury injury)
		{
			if (injury.Attacker == null) return null;

			// Si el atacante directo no es de la misma manada, es un atacante externo
			if (!IsSameHerd(injury.Attacker))
			{
				return injury.Attacker;
			}

			// Buscar otras fuentes de daño en la lesión
			// (en algunos casos, la lesión puede tener múltiples fuentes)
			return null;
		}

		// Verificar si una criatura es de la misma manada
		private bool IsSameHerd(ComponentCreature otherCreature)
		{
			if (otherCreature == null || m_componentZombieHerdBehavior == null)
				return false;

			return m_componentZombieHerdBehavior.IsSameZombieHerd(otherCreature);
		}

		// Método de ataque específico para zombis que incluye lógica de manada
		public virtual void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent, bool isRetaliation)
		{
			// Verificar si el objetivo es de la misma manada
			if (!isRetaliation && !m_attacksSameHerd && IsSameHerd(target))
			{
				// No atacar a miembros de la misma manada
				// En su lugar, coordinar un ataque grupal si hay un enemigo externo
				if (m_componentZombieHerdBehavior != null)
				{
					// Buscar un enemigo cercano que no sea de la misma manada
					ComponentCreature externalEnemy = FindExternalEnemyNearby(maxRange);
					if (externalEnemy != null)
					{
						m_componentZombieHerdBehavior.CoordinateGroupAttack(externalEnemy);
					}
				}
				return;
			}

			// Si es una represalia (llamado de ayuda), permitir atacar incluso en modos restrictivos
			if (isRetaliation)
			{
				// Forzar el ataque incluso si normalmente no atacaría
				this.Suppressed = false;
			}

			// Llamar al método base para el ataque normal
			base.Attack(target, maxRange, maxChaseTime, isPersistent);

			// Si este es un ataque inicial (no represalia) y hay comportamiento de manada,
			// llamar a otros zombis para que ayuden
			if (!isRetaliation && m_componentZombieHerdBehavior != null)
			{
				m_componentZombieHerdBehavior.CoordinateGroupAttack(target);
			}
		}

		// Sobrescribir el método Attack base para usar la versión de zombis
		public override void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			this.Attack(target, maxRange, maxChaseTime, isPersistent, false);
		}

		// Buscar un enemigo externo cercano
		private ComponentCreature FindExternalEnemyNearby(float range)
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentCreature bestTarget = null;
			float bestScore = 0f;

			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), range, this.m_componentBodies);

			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentCreature creature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature != this.m_componentCreature)
				{
					// Verificar que no sea de la misma manada
					if (!IsSameHerd(creature))
					{
						float distance = Vector3.Distance(position, creature.ComponentBody.Position);
						float score = range - distance; // Puntuación más alta para objetivos más cercanos

						if (score > bestScore)
						{
							bestScore = score;
							bestTarget = creature;
						}
					}
				}
			}

			return bestTarget;
		}

		// Sobrescribir FindTarget para excluir miembros de la misma manada
		public override ComponentCreature FindTarget()
		{
			// Si no se permite atacar a la misma manada, filtrar los objetivos
			if (!m_attacksSameHerd)
			{
				Vector3 position = this.m_componentCreature.ComponentBody.Position;
				ComponentCreature result = null;
				float bestScore = 0f;

				this.m_componentBodies.Clear();
				this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), this.m_range, this.m_componentBodies);

				for (int i = 0; i < this.m_componentBodies.Count; i++)
				{
					ComponentCreature creature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
					if (creature != null)
					{
						// Excluir miembros de la misma manada
						if (IsSameHerd(creature))
						{
							continue;
						}

						float score = this.ScoreTarget(creature);
						if (score > bestScore)
						{
							bestScore = score;
							result = creature;
						}
					}
				}

				return result;
			}

			// Si se permite atacar a la misma manada, usar la implementación base
			return base.FindTarget();
		}

		// Sobrescribir ScoreTarget para ajustar la puntuación basada en la manada
		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			// Si no se permite atacar a la misma manada y es un miembro de la misma, puntuación 0
			if (!m_attacksSameHerd && IsSameHerd(componentCreature))
			{
				return 0f;
			}

			// Llamar a la implementación base
			return base.ScoreTarget(componentCreature);
		}

		// Añadir estado de huida
		private void AddFleeState()
		{
			this.m_stateMachine.AddState("Fleeing", delegate
			{
				// Iniciar la huida
				this.m_importanceLevel = 150f; // Prioridad media-alta para huir
				this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
			}, delegate
			{
				// Actualizar estado de huida
				if (this.m_target == null || this.m_componentCreature.ComponentHealth.Health <= 0f)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
					return;
				}

				// Calcular dirección de huida (opuesta al objetivo)
				Vector3 fleeDirection = this.m_componentCreature.ComponentBody.Position - this.m_target.ComponentBody.Position;
				if (fleeDirection.LengthSquared() > 0.01f)
				{
					fleeDirection = Vector3.Normalize(fleeDirection);
					Vector3 destination = this.m_componentCreature.ComponentBody.Position + fleeDirection * m_fleeDistance;

					// Establecer destino de huida
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

				// Verificar si hemos huido lo suficiente
				float distanceToTarget = Vector3.Distance(
					this.m_componentCreature.ComponentBody.Position,
					this.m_target.ComponentBody.Position
				);

				if (distanceToTarget > m_fleeDistance * 1.5f)
				{
					// Hemos huido lo suficiente, volver a buscar objetivos
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}

				// Reproducir sonidos de dolor ocasionalmente
				if (this.m_random.Float(0f, 1f) < 0.05f * this.m_dt)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
				}
			}, delegate
			{
				// Limpiar al salir del estado
				this.m_componentPathfinding.Stop();
				this.m_importanceLevel = 0f;
			});
		}

		// Método para activar la huida de un objetivo
		private void FleeFromTarget(ComponentCreature target)
		{
			if (target == null || this.m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			this.m_target = target;
			this.m_stateMachine.TransitionTo("Fleeing");
		}

		// Sobrescribir Update para añadir lógica específica de zombis
		public override void Update(float dt)
		{
			// Llamar al método base primero
			base.Update(dt);

			// Lógica adicional para zombis
			// (puede añadirse comportamiento específico aquí si es necesario)
		}

		// Método para detener el ataque (sobrescrito para limpieza específica)
		public override void StopAttack()
		{
			// Llamar al método base
			base.StopAttack();

			// Limpieza adicional si es necesaria
		}
	}
}
