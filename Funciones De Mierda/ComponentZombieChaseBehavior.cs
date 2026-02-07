using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieChaseBehavior : ComponentChaseBehavior, IUpdateable
	{
		// Nuevos campos específicos para zombis
		public bool AttacksSameHerd { get; set; } = false;
		public bool AttacksAllCategories { get; set; } = true;
		public bool FleeFromSameHerd { get; set; } = true;
		public float FleeDistance { get; set; } = 10f;
		public bool ForceAttackDuringGreenNight { get; set; } = true;

		// Campos para rastrear miembros de la misma manada y Noche Verde
		private ComponentZombieHerdBehavior m_zombieHerdBehavior;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private SubsystemTimeOfDay m_subsystemTimeOfDay;
		private float m_greenNightAttackTimer;

		// Factores de mejora durante Noche Verde
		private float m_greenNightRangeMultiplier = 2.0f; // Doble rango durante Noche Verde
		private float m_greenNightSpeedMultiplier = 1.5f; // 50% más rápido durante Noche Verde

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Cargar primero el comportamiento base
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros específicos de zombis
			this.AttacksSameHerd = valuesDictionary.GetValue<bool>("AttacksSameHerd", false);
			this.AttacksAllCategories = valuesDictionary.GetValue<bool>("AttacksAllCategories", true);
			this.FleeFromSameHerd = valuesDictionary.GetValue<bool>("FleeFromSameHerd", true);
			this.FleeDistance = valuesDictionary.GetValue<float>("FleeDistance", 10f);
			this.ForceAttackDuringGreenNight = valuesDictionary.GetValue<bool>("ForceAttackDuringGreenNight", true);

			// Obtener el componente de comportamiento de manada de zombis
			this.m_zombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();

			// Obtener subsistemas necesarios
			this.m_subsystemGreenNightSky = base.Project.FindSubsystem<SubsystemGreenNightSky>(false);
			this.m_subsystemTimeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(false);

			// NO DESACTIVAR AttacksPlayer aquí - eso previene que ataquen cuando son provocados
			// Modificar el event handler de Injured para manejar la lógica de manada
			this.SetupZombieInjuryHandler();
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
				// Primero, manejar la lógica de manada si existe
				if (this.m_zombieHerdBehavior != null)
				{
					ComponentCreature attacker = injury.Attacker;

					// Verificar si el atacante es de la misma manada
					if (attacker != null && this.m_zombieHerdBehavior.IsSameZombieHerd(attacker))
					{
						if (this.FleeFromSameHerd)
						{
							// Activar huida de miembros de la misma manada
							this.ActivateFleeFromSameHerd(attacker);
						}

						// Si no atacamos a la misma manada, no proceder con el ataque
						if (!this.AttacksSameHerd)
						{
							// No llamar al handler original en este caso
							return;
						}
					}
				}

				// Llamar al handler original para mantener la funcionalidad base
				if (originalHandler != null)
				{
					originalHandler(injury);
				}
			};

			// Asignar el nuevo handler
			componentHealth.Injured = zombieInjuryHandler;
		}

		// Método para activar huida de miembros de la misma manada
		private void ActivateFleeFromSameHerd(ComponentCreature attacker)
		{
			if (attacker == null || this.m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			// Calcular dirección de huida
			Vector3 fleeDirection = this.m_componentCreature.ComponentBody.Position - attacker.ComponentBody.Position;

			if (fleeDirection.LengthSquared() > 0.01f)
			{
				fleeDirection = Vector3.Normalize(fleeDirection);
				Vector3 destination = this.m_componentCreature.ComponentBody.Position + fleeDirection * this.FleeDistance;

				// Establecer destino para huir
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
		}

		// Sobrescribir el método FindTarget para incluir lógica de zombis
		public override ComponentCreature FindTarget()
		{
			// Si ForceAttackDuringGreenNight está activado y es Noche Verde, buscar agresivamente al jugador
			if (this.ForceAttackDuringGreenNight && this.IsGreenNight())
			{
				ComponentPlayer player = this.FindNearestPlayer();
				if (player != null && player.ComponentHealth.Health > 0f)
				{
					// Verificar si el jugador está en rango (rango extendido durante Noche Verde)
					float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
													 player.ComponentBody.Position);
					float chaseRange = this.GetGreenNightChaseRange();

					if (distance < chaseRange)
					{
						// ComponentPlayer hereda de ComponentCreature
						return player;
					}
				}

				// Si no hay jugador cerca, buscar cualquier objetivo
				return this.FindAnyTargetInExtendedRange();
			}

			// FUERA DE NOCHE VERDE: Usar el comportamiento base normal
			// El método base ya maneja la lógica de ataque solo cuando es provocado
			return base.FindTarget();
		}

		// Método para encontrar cualquier objetivo en rango extendido durante Noche Verde
		private ComponentCreature FindAnyTargetInExtendedRange()
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentCreature result = null;
			float bestScore = 0f;

			// Usar rango extendido durante Noche Verde
			float extendedRange = this.GetGreenNightChaseRange();

			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), extendedRange, this.m_componentBodies);

			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentCreature creature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();

				if (creature != null && creature != this.m_componentCreature)
				{
					// Aplicar lógica de manada si existe
					if (this.m_zombieHerdBehavior != null)
					{
						// Verificar si el objetivo es de la misma manada
						if (this.m_zombieHerdBehavior.IsSameZombieHerd(creature))
						{
							// Si no atacamos a la misma manada, saltar este objetivo
							if (!this.AttacksSameHerd)
							{
								continue;
							}
						}
					}

					// Dar puntuación extra a los jugadores durante Noche Verde
					float score = this.ScoreTargetForGreenNight(creature);
					if (score > bestScore)
					{
						bestScore = score;
						result = creature;
					}
				}
			}

			return result;
		}

		// Método para puntuar objetivos durante Noche Verde
		private float ScoreTargetForGreenNight(ComponentCreature componentCreature)
		{
			// Verificar si es un objetivo válido
			if (componentCreature != this.m_componentCreature &&
				componentCreature.Entity.IsAddedToProject &&
				componentCreature.ComponentHealth.Health > 0f)
			{
				// Aplicar lógica de manada si existe
				if (this.m_zombieHerdBehavior != null)
				{
					// Verificar si el objetivo es de la misma manada
					if (this.m_zombieHerdBehavior.IsSameZombieHerd(componentCreature))
					{
						// Si no atacamos a la misma manada, retornar 0
						if (!this.AttacksSameHerd)
						{
							return 0f;
						}
					}
				}

				// Calcular distancia
				float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
												 componentCreature.ComponentBody.Position);
				float extendedRange = this.GetGreenNightChaseRange();

				if (distance < extendedRange)
				{
					// Puntuación base: inversamente proporcional a la distancia
					float baseScore = extendedRange - distance;

					// Bonus extra para jugadores durante Noche Verde
					ComponentPlayer player = componentCreature as ComponentPlayer;
					if (player != null)
					{
						baseScore *= 2.0f; // Doble puntuación para jugadores
					}

					return baseScore;
				}
			}
			return 0f;
		}

		// Sobrescribir ScoreTarget para manejar AttacksAllCategories
		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			// Si AttacksAllCategories está activado, ignorar las categorías y puntuar por distancia
			if (this.AttacksAllCategories)
			{
				// Verificar si es un objetivo válido
				if (componentCreature != this.m_componentCreature &&
					componentCreature.Entity.IsAddedToProject &&
					componentCreature.ComponentHealth.Health > 0f)
				{
					// Aplicar lógica de manada si existe
					if (this.m_zombieHerdBehavior != null)
					{
						// Verificar si el objetivo es de la misma manada
						if (this.m_zombieHerdBehavior.IsSameZombieHerd(componentCreature))
						{
							// Si no atacamos a la misma manada, retornar 0
							if (!this.AttacksSameHerd)
							{
								return 0f;
							}
						}
					}

					// Calcular puntuación basada en distancia
					float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
													 componentCreature.ComponentBody.Position);
					if (distance < this.m_range)
					{
						return this.m_range - distance;
					}
				}
				return 0f;
			}

			// Si AttacksAllCategories no está activado, usar la lógica base
			return base.ScoreTarget(componentCreature);
		}

		// Método para encontrar un objetivo alternativo (no de la misma manada)
		private ComponentCreature FindAlternativeTarget(ComponentCreature excludedTarget)
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentCreature result = null;
			float bestScore = 0f;

			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), this.m_range, this.m_componentBodies);

			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentCreature creature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();

				if (creature != null && creature != excludedTarget)
				{
					// Aplicar lógica de manada si existe
					if (this.m_zombieHerdBehavior != null)
					{
						// Verificar si el objetivo es de la misma manada
						if (this.m_zombieHerdBehavior.IsSameZombieHerd(creature))
						{
							// Si no atacamos a la misma manada, saltar este objetivo
							if (!this.AttacksSameHerd)
							{
								continue;
							}
						}
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

		// Método para encontrar al jugador más cercano
		private ComponentPlayer FindNearestPlayer()
		{
			ComponentPlayer nearestPlayer = null;
			float nearestDistance = float.MaxValue;

			foreach (ComponentPlayer player in this.m_subsystemPlayers.ComponentPlayers)
			{
				if (player.ComponentHealth.Health > 0f)
				{
					float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
													 player.ComponentBody.Position);
					if (distance < nearestDistance)
					{
						nearestDistance = distance;
						nearestPlayer = player;
					}
				}
			}

			return nearestPlayer;
		}

		// Método para verificar si es Noche Verde
		private bool IsGreenNight()
		{
			// Usar el subsistema GreenNightSky para verificar
			if (this.m_subsystemGreenNightSky != null)
			{
				return this.m_subsystemGreenNightSky.IsGreenNightActive;
			}

			// Fallback: verificar si es de noche usando SkyLightIntensity
			return this.m_subsystemSky.SkyLightIntensity < 0.2f;
		}

		// Obtener rango de persecución durante Noche Verde
		private float GetGreenNightChaseRange()
		{
			float baseRange = this.m_subsystemSky.SkyLightIntensity < 0.2f ?
							this.m_nightChaseRange : this.m_dayChaseRange;

			if (this.IsGreenNight() && this.ForceAttackDuringGreenNight)
			{
				return baseRange * this.m_greenNightRangeMultiplier;
			}

			return baseRange;
		}

		// Sobrescribir Update para añadir lógica de Noche Verde
		public new void Update(float dt)
		{
			bool isGreenNight = this.IsGreenNight();

			// Comportamiento agresivo durante Noche Verde
			if (isGreenNight && this.ForceAttackDuringGreenNight)
			{
				this.m_greenNightAttackTimer += dt;

				// Forzar ataque al jugador durante Noche Verde si no hay objetivo
				if (this.m_greenNightAttackTimer > 2f && this.m_target == null)
				{
					ComponentPlayer player = this.FindNearestPlayer();
					if (player != null)
					{
						float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
														 player.ComponentBody.Position);
						float chaseRange = this.GetGreenNightChaseRange();

						if (distance < chaseRange)
						{
							// ComponentPlayer hereda de ComponentCreature
							this.Attack(player, chaseRange, this.m_nightChaseTime * 2.0f, true);
							this.m_greenNightAttackTimer = 0f;
						}
					}
				}

				// Aumentar velocidad de movimiento durante Noche Verde
				if (this.m_componentPathfinding != null)
				{
					this.m_componentPathfinding.Speed = this.m_greenNightSpeedMultiplier;
				}
			}
			else
			{
				this.m_greenNightAttackTimer = 0f;

				// Velocidad normal fuera de Noche Verde
				if (this.m_componentPathfinding != null)
				{
					this.m_componentPathfinding.Speed = 1.0f;
				}
			}

			// Llamar al Update base
			base.Update(dt);
		}

		// Sobrescribir Attack para incluir lógica de coordinación de manada y Noche Verde
		public override void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			// Verificar lógica de manada antes de atacar
			if (componentCreature != null && this.m_zombieHerdBehavior != null)
			{
				// Verificar si el objetivo es de la misma manada
				if (this.m_zombieHerdBehavior.IsSameZombieHerd(componentCreature))
				{
					// Si no atacamos a la misma manada, activar huida en lugar de ataque
					if (!this.AttacksSameHerd && this.FleeFromSameHerd)
					{
						this.ActivateFleeFromSameHerd(componentCreature);
						return;
					}
				}

				// Coordinar ataque con otros miembros de la manada
				this.m_zombieHerdBehavior.CoordinateGroupAttack(componentCreature);
			}

			// Durante Noche Verde, aumentar la persistencia del ataque
			if (this.IsGreenNight() && this.ForceAttackDuringGreenNight)
			{
				maxChaseTime *= 2.0f; // Doble tiempo de persecución
				isPersistent = true; // Siempre persistente durante Noche Verde
			}

			// Llamar al método base
			base.Attack(componentCreature, maxRange, maxChaseTime, isPersistent);
		}
	}
}
