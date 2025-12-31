using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieHerdBehavior : ComponentHerdBehavior
	{
		// Propiedades específicas para zombis
		public bool CallForHelpWhenAttacked { get; set; } = true;
		public float HelpCallRange { get; set; } = 25f;
		public float HelpChaseTime { get; set; } = 30f;
		public bool IsPersistentHelp { get; set; } = false;
		public bool ZombieAggressiveGrouping { get; set; } = false; // Si los zombis se agrupan agresivamente

		// Sobrescribir la propiedad HerdName para establecer valor por defecto
		public new string HerdName
		{
			get { return base.HerdName; }
			set { base.HerdName = value; }
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Llamar al método base primero
			base.Load(valuesDictionary, idToEntityMap);

			// Establecer nombre de manada por defecto para zombis
			if (string.IsNullOrEmpty(this.HerdName))
			{
				this.HerdName = "Zombie";
			}

			// Cargar propiedades específicas de zombis
			this.CallForHelpWhenAttacked = valuesDictionary.GetValue<bool>("CallForHelpWhenAttacked", true);
			this.HelpCallRange = valuesDictionary.GetValue<float>("HelpCallRange", 25f);
			this.HelpChaseTime = valuesDictionary.GetValue<float>("HelpChaseTime", 30f);
			this.IsPersistentHelp = valuesDictionary.GetValue<bool>("IsPersistentHelp", false);
			this.ZombieAggressiveGrouping = valuesDictionary.GetValue<bool>("ZombieAggressiveGrouping", false);
			this.m_herdingRange = valuesDictionary.GetValue<float>("HerdingRange", 40f); // Rango mayor por defecto para zombis

			// Configurar m_autoNearbyCreaturesHelp para zombis
			this.m_autoNearbyCreaturesHelp = valuesDictionary.GetValue<bool>("AutoNearbyCreaturesHelp", true);

			// Reemplazar el event handler de Injured para añadir lógica de zombis
			// Primero necesitamos acceder al ComponentHealth y reemplazar el handler
			this.SetupZombieInjuryHandler();

			// Añadir estados adicionales para comportamiento específico de zombis
			this.AddZombieSpecificStates();
		}

		// Configurar el handler de lesiones específico para zombis
		private void SetupZombieInjuryHandler()
		{
			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;

			// Crear un nuevo handler que incluya la lógica de zombis
			Action<Injury> zombieInjuryHandler = delegate (Injury injury)
			{
				// Llamar a otros zombis para ayudar cuando es atacado
				if (this.CallForHelpWhenAttacked && injury.Attacker != null)
				{
					this.CallZombiesForHelp(injury.Attacker);
				}

				// También mantener la funcionalidad base si es necesario
				// (el método base ya maneja esto a través de m_autoNearbyCreaturesHelp)
			};

			// Asignar el nuevo handler (reemplazando cualquier handler anterior)
			componentHealth.Injured = zombieInjuryHandler;
		}

		// Método específico para llamar a otros zombis
		public void CallZombiesForHelp(ComponentCreature attacker)
		{
			if (attacker == null || string.IsNullOrEmpty(this.HerdName))
				return;

			// Llamar al método base para que maneje la lógica de mods y la lógica base
			this.CallNearbyCreaturesHelp(attacker, this.HelpCallRange, this.HelpChaseTime, this.IsPersistentHelp);

			// También podemos añadir lógica adicional específica para zombis aquí
			// Por ejemplo, hacer ruidos especiales o efectos visuales
			this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);

			// Los zombis pueden tener un rango mayor para llamar ayuda
			if (this.ZombieAggressiveGrouping)
			{
				// Llamar a zombis adicionales en un rango mayor
				this.CallAdditionalZombies(attacker, this.HelpCallRange * 1.5f);
			}
		}

		// Método para llamar a zombis adicionales en un rango mayor
		private void CallAdditionalZombies(ComponentCreature attacker, float extendedRange)
		{
			if (attacker == null || string.IsNullOrEmpty(this.HerdName))
				return;

			Vector3 position = attacker.ComponentBody.Position;

			foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.Creatures)
			{
				if (creature == this.m_componentCreature) // Excluir a sí mismo
					continue;

				if (Vector3.DistanceSquared(position, creature.ComponentBody.Position) < extendedRange * extendedRange)
				{
					ComponentZombieHerdBehavior zombieHerdBehavior = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();

					// También verificar el ComponentHerdBehavior base para compatibilidad
					ComponentHerdBehavior herdBehavior = creature.Entity.FindComponent<ComponentHerdBehavior>();

					// Aceptar zombis que tengan ComponentZombieHerdBehavior O ComponentHerdBehavior con el mismo nombre
					bool isSameHerd = false;

					if (zombieHerdBehavior != null && !string.IsNullOrEmpty(zombieHerdBehavior.HerdName))
					{
						isSameHerd = (zombieHerdBehavior.HerdName == this.HerdName);
					}
					else if (herdBehavior != null && !string.IsNullOrEmpty(herdBehavior.HerdName))
					{
						isSameHerd = (herdBehavior.HerdName == this.HerdName);
					}

					if (isSameHerd)
					{
						ComponentZombieChaseBehavior chaseBehavior = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
						if (chaseBehavior != null && chaseBehavior.Target == null)
						{
							// Usar isRetaliation=true para permitir ataque incluso en modos de juego restrictivos
							chaseBehavior.Attack(attacker, this.HelpCallRange, this.HelpChaseTime, this.IsPersistentHelp, true);
						}
					}
				}
			}
		}

		// Añadir estados específicos para zombis
		private void AddZombieSpecificStates()
		{
			// Añadir un estado para comportamiento de búsqueda agresiva (solo si está habilitado)
			if (this.ZombieAggressiveGrouping)
			{
				this.m_stateMachine.AddState("ZombieRoam", delegate
				{
					// Los zombis deambulan más cuando no están agrupados
					if (this.m_random.Float(0f, 1f) < 0.1f)
					{
						Vector3 randomDirection = new Vector3(
							this.m_random.Float(-1f, 1f),
							0f,
							this.m_random.Float(-1f, 1f)
						);

						if (randomDirection.LengthSquared() > 0.01f)
						{
							// CORREGIDO: Usar Vector3.Normalize estático
							randomDirection = Vector3.Normalize(randomDirection);
							Vector3 destination = this.m_componentCreature.ComponentBody.Position + randomDirection * 15f;
							this.m_componentPathfinding.SetDestination(
								new Vector3?(destination),
								this.m_random.Float(0.3f, 0.5f),
								5f,
								0,
								false,
								true,
								false,
								null
							);
						}
					}
				}, delegate
				{
					// Transicionar a Herd si es necesario
					if (this.IsActive)
					{
						this.m_stateMachine.TransitionTo("Herd");
					}

					// Ocasionalmente hacer ruidos de zombi
					if (this.m_random.Float(0f, 1f) < 0.02f * this.m_dt)
					{
						this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
					}

					// Si no hay destino, volver a Inactive
					if (this.m_componentPathfinding.Destination == null)
					{
						this.m_stateMachine.TransitionTo("Inactive");
					}
				}, null);
			}
		}

		// Sobrescribir el método Update para añadir comportamiento específico de zombis
		public override void Update(float dt)
		{
			// Primero llamar al método base
			base.Update(dt);

			// Comportamiento adicional específico de zombis
			if (this.ZombieAggressiveGrouping && !this.IsActive)
			{
				// Los zombis ocasionalmente deambulan incluso cuando no están agrupados
				if (this.m_random.Float(0f, 1f) < 0.001f * dt &&
					this.m_stateMachine.CurrentState == "Inactive")
				{
					this.m_stateMachine.TransitionTo("ZombieRoam");
					this.m_importanceLevel = 5f; // Baja prioridad para deambular
				}
			}
		}

		// Método para encontrar el centro de la manada de zombis (versión optimizada para zombis)
		public new Vector3? FindHerdCenter()
		{
			// Llamar al método base para permitir hooks de mods
			bool skipVanilla = false;
			Vector3? herdCenterFromMod = null;
			ModsManager.HookAction("FindZombieHerdCenter", delegate (ModLoader modLoader)
			{
				modLoader.FindHerdCenter(this.m_componentCreature, out herdCenterFromMod, out skipVanilla);
				return false;
			});

			if (skipVanilla)
			{
				return herdCenterFromMod;
			}

			// Implementación específica para zombis
			if (string.IsNullOrEmpty(this.HerdName))
			{
				return null;
			}

			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			int count = 0;
			Vector3 sum = Vector3.Zero;

			// Usar el rango de manada específico para zombis
			float herdingRange = this.m_herdingRange;

			foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.Creatures)
			{
				if (creature.ComponentHealth.Health > 0f)
				{
					ComponentZombieHerdBehavior zombieHerdBehavior = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();

					// También verificar el ComponentHerdBehavior base para compatibilidad
					ComponentHerdBehavior herdBehavior = creature.Entity.FindComponent<ComponentHerdBehavior>();

					// Aceptar zombis que tengan ComponentZombieHerdBehavior O ComponentHerdBehavior con el mismo nombre
					bool isSameHerd = false;

					if (zombieHerdBehavior != null && !string.IsNullOrEmpty(zombieHerdBehavior.HerdName))
					{
						isSameHerd = (zombieHerdBehavior.HerdName == this.HerdName);
					}
					else if (herdBehavior != null && !string.IsNullOrEmpty(herdBehavior.HerdName))
					{
						isSameHerd = (herdBehavior.HerdName == this.HerdName);
					}

					if (isSameHerd)
					{
						Vector3 creaturePosition = creature.ComponentBody.Position;
						if (Vector3.DistanceSquared(position, creaturePosition) < herdingRange * herdingRange)
						{
							sum += creaturePosition;
							count++;
						}
					}
				}
			}

			if (count > 0)
			{
				return new Vector3?(sum / (float)count);
			}

			return null;
		}

		// Método para obtener todos los zombis de la misma manada en un rango
		public System.Collections.Generic.List<ComponentCreature> GetNearbyZombies(float range)
		{
			var nearbyZombies = new System.Collections.Generic.List<ComponentCreature>();

			if (string.IsNullOrEmpty(this.HerdName))
				return nearbyZombies;

			Vector3 position = this.m_componentCreature.ComponentBody.Position;

			foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.Creatures)
			{
				if (creature == this.m_componentCreature) // Excluir a sí mismo
					continue;

				if (creature.ComponentHealth.Health > 0f)
				{
					ComponentZombieHerdBehavior zombieHerdBehavior = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();

					// También verificar el ComponentHerdBehavior base para compatibilidad
					ComponentHerdBehavior herdBehavior = creature.Entity.FindComponent<ComponentHerdBehavior>();

					// Aceptar zombis que tengan ComponentZombieHerdBehavior O ComponentHerdBehavior con el mismo nombre
					bool isSameHerd = false;

					if (zombieHerdBehavior != null && !string.IsNullOrEmpty(zombieHerdBehavior.HerdName))
					{
						isSameHerd = (zombieHerdBehavior.HerdName == this.HerdName);
					}
					else if (herdBehavior != null && !string.IsNullOrEmpty(herdBehavior.HerdName))
					{
						isSameHerd = (herdBehavior.HerdName == this.HerdName);
					}

					if (isSameHerd && Vector3.DistanceSquared(position, creature.ComponentBody.Position) < range * range)
					{
						nearbyZombies.Add(creature);
					}
				}
			}

			return nearbyZombies;
		}

		// Método para verificar si una criatura es del mismo rebaño zombi
		public bool IsSameZombieHerd(ComponentCreature otherCreature)
		{
			if (otherCreature == null || string.IsNullOrEmpty(this.HerdName))
				return false;

			ComponentZombieHerdBehavior otherZombieHerd = otherCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();
			ComponentHerdBehavior otherHerd = otherCreature.Entity.FindComponent<ComponentHerdBehavior>();

			if (otherZombieHerd != null && !string.IsNullOrEmpty(otherZombieHerd.HerdName))
			{
				return (otherZombieHerd.HerdName == this.HerdName);
			}
			else if (otherHerd != null && !string.IsNullOrEmpty(otherHerd.HerdName))
			{
				return (otherHerd.HerdName == this.HerdName);
			}

			return false;
		}

		// Método para coordinación de ataques en grupo
		public void CoordinateGroupAttack(ComponentCreature target)
		{
			if (target == null || string.IsNullOrEmpty(this.HerdName))
				return;

			var nearbyZombies = this.GetNearbyZombies(this.HelpCallRange);

			foreach (var zombie in nearbyZombies)
			{
				ComponentZombieChaseBehavior chaseBehavior = zombie.Entity.FindComponent<ComponentZombieChaseBehavior>();
				if (chaseBehavior != null && chaseBehavior.Target == null)
				{
					// Distribuir los ataques para que no todos ataquen al mismo tiempo
					if (this.m_random.Float(0f, 1f) < 0.7f) // 70% de probabilidad de unirse al ataque
					{
						chaseBehavior.Attack(target, this.HelpCallRange, this.HelpChaseTime, this.IsPersistentHelp, true);
					}
				}
			}
		}
	}
}
