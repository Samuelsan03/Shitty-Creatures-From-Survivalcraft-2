using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewHerdBehavior : ComponentBehavior, IUpdateable
	{
		// Propiedades públicas
		public string HerdName { get; set; }

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		// Método para llamar ayuda de criaturas cercanas
		public void CallNearbyCreaturesHelp(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (target == null)
			{
				return;
			}

			// Verificar si el objetivo es de la misma manada
			ComponentNewHerdBehavior targetHerdBehavior = target.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (targetHerdBehavior != null && !string.IsNullOrEmpty(targetHerdBehavior.HerdName) &&
				targetHerdBehavior.HerdName.Equals(this.HerdName, StringComparison.OrdinalIgnoreCase))
			{
				// No llamar ayuda contra miembros de la misma manada
				return;
			}

			Vector3 position = target.ComponentBody.Position;
			float rangeSquared = maxRange * maxRange;

			foreach (ComponentCreature componentCreature in this.m_subsystemCreatureSpawn.Creatures)
			{
				if (Vector3.DistanceSquared(position, componentCreature.ComponentBody.Position) < rangeSquared)
				{
					ComponentNewHerdBehavior componentHerdBehavior = componentCreature.Entity.FindComponent<ComponentNewHerdBehavior>();

					// Verificar que sea de la misma manada y tenga ayuda automática habilitada
					if (componentHerdBehavior != null && !string.IsNullOrEmpty(componentHerdBehavior.HerdName) &&
						componentHerdBehavior.HerdName.Equals(this.HerdName, StringComparison.OrdinalIgnoreCase) &&
						componentHerdBehavior.m_autoNearbyCreaturesHelp)
					{
						// Usar el ComponentChaseBehavior original
						ComponentChaseBehavior componentChaseBehavior = componentCreature.Entity.FindComponent<ComponentChaseBehavior>();
						if (componentChaseBehavior != null && componentChaseBehavior.Target == null)
						{
							// Verificar nuevamente que el objetivo no sea de la misma manada
							if (targetHerdBehavior == null ||
								!targetHerdBehavior.HerdName.Equals(componentHerdBehavior.HerdName, StringComparison.OrdinalIgnoreCase))
							{
								componentChaseBehavior.Attack(target, maxRange, maxChaseTime, isPersistent);
							}
						}
					}
				}
			}
		}

		// Método para encontrar el centro de la manada
		public Vector3? FindHerdCenter()
		{
			if (string.IsNullOrEmpty(this.HerdName))
			{
				return null;
			}

			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			int count = 0;
			Vector3 center = Vector3.Zero;
			float herdingRangeSquared = this.m_herdingRange * this.m_herdingRange;

			foreach (ComponentCreature componentCreature in this.m_subsystemCreatureSpawn.Creatures)
			{
				if (componentCreature.ComponentHealth.Health > 0f)
				{
					ComponentNewHerdBehavior componentHerdBehavior = componentCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
					if (componentHerdBehavior != null &&
						componentHerdBehavior.HerdName.Equals(this.HerdName, StringComparison.OrdinalIgnoreCase))
					{
						Vector3 creaturePosition = componentCreature.ComponentBody.Position;
						if (Vector3.DistanceSquared(position, creaturePosition) < herdingRangeSquared)
						{
							center += creaturePosition;
							count++;
						}
					}
				}
			}

			if (count > 0)
			{
				return center / (float)count;
			}

			return null;
		}

		// Método para verificar si una criatura es de la misma manada
		public bool IsSameHerd(ComponentCreature otherCreature)
		{
			if (otherCreature == null || string.IsNullOrEmpty(this.HerdName))
			{
				return false;
			}

			ComponentNewHerdBehavior otherHerd = otherCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (otherHerd == null || string.IsNullOrEmpty(otherHerd.HerdName))
			{
				return false;
			}

			return this.HerdName.Equals(otherHerd.HerdName, StringComparison.OrdinalIgnoreCase);
		}

		// Método para verificar si debe atacar a una criatura
		public bool ShouldAttackCreature(ComponentCreature target)
		{
			if (target == null || string.IsNullOrEmpty(this.HerdName))
			{
				return true; // Por defecto, puede atacar
			}

			// Si el objetivo es de la misma manada, no atacar
			if (this.IsSameHerd(target))
			{
				return false;
			}

			// Caso especial: si la manada es "player", no atacar al jugador
			if (this.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
			{
				ComponentPlayer targetPlayer = target.Entity.FindComponent<ComponentPlayer>();
				if (targetPlayer != null)
				{
					return false; // No atacar al jugador
				}
			}

			return true;
		}

		// Actualización del comportamiento
		public virtual void Update(float dt)
		{
			if (string.IsNullOrEmpty(this.m_stateMachine.CurrentState) || !this.IsActive)
			{
				this.m_stateMachine.TransitionTo("Inactive");
			}

			this.m_dt = dt;
			this.m_stateMachine.Update();

			// Verificar si el objetivo actual del chase behavior es de la misma manada
			this.CheckChaseBehaviorTarget();
		}

		// Verificar el objetivo del ComponentChaseBehavior
		private void CheckChaseBehaviorTarget()
		{
			if (this.m_componentChase != null && this.m_componentChase.Target != null)
			{
				if (!this.ShouldAttackCreature(this.m_componentChase.Target))
				{
					// Detener el ataque si el objetivo es de la misma manada
					this.m_componentChase.StopAttack();
				}
			}
		}

		// Cargar configuración
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemCreatureSpawn = base.Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);

			// Obtener el ComponentChaseBehavior
			this.m_componentChase = base.Entity.FindComponent<ComponentChaseBehavior>();

			this.HerdName = valuesDictionary.GetValue<string>("HerdName", "");
			this.m_herdingRange = valuesDictionary.GetValue<float>("HerdingRange", 20f);
			this.m_autoNearbyCreaturesHelp = valuesDictionary.GetValue<bool>("AutoNearbyCreaturesHelp", true);

			// Configurar eventos
			this.SetupEventHooks();

			// Configurar máquina de estados
			this.SetupStateMachine();
		}

		// Configurar hooks de eventos
		private void SetupEventHooks()
		{
			// Configurar el evento de lesión para llamar ayuda
			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(this.OnInjured));

			// Si tenemos ComponentChaseBehavior, hookear sus eventos
			if (this.m_componentChase != null)
			{
				// Hookear el evento de ataque para verificar la manada antes de atacar
				this.m_chaseAttackDelegate = new Action<ComponentCreature, float, float, bool>(this.OnChaseAttack);

				// Crear un método alternativo para hookear el chase behavior
				this.HookChaseBehaviorEvents();
			}
		}

		// Hookear eventos del ComponentChaseBehavior
		private void HookChaseBehaviorEvents()
		{
			// No podemos reasignar métodos directamente, pero podemos usar un proxy
			// O mejor, simplemente verificar en nuestro Update
		}

		// Evento cuando la criatura es herida
		private void OnInjured(Injury injury)
		{
			ComponentCreature attacker = injury.Attacker;
			if (attacker != null && this.m_autoNearbyCreaturesHelp)
			{
				// Solo llamar ayuda si el atacante no es de la misma manada
				if (this.ShouldAttackCreature(attacker))
				{
					this.CallNearbyCreaturesHelp(attacker, 20f, 30f, false);
				}
			}
		}

		// Evento cuando el chase behavior intenta atacar
		private void OnChaseAttack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			// Verificar si debe atacar a este objetivo
			if (!this.ShouldAttackCreature(target))
			{
				// No atacar si es de la misma manada
				return;
			}

			// Si llegamos aquí, puede atacar
			// Llamar al método original si existe
			if (this.m_originalChaseAttack != null)
			{
				this.m_originalChaseAttack(target, maxRange, maxChaseTime, isPersistent);
			}
		}

		// Configurar la máquina de estados
		private void SetupStateMachine()
		{
			this.m_stateMachine.AddState("Inactive", null, delegate
			{
				// Verificar periódicamente si necesita unirse a la manada
				if (this.m_subsystemTime.PeriodicGameTimeEvent(1.0, (double)(1f * ((float)(this.GetHashCode() % 256) / 256f))))
				{
					Vector3? herdCenter = this.FindHerdCenter();
					if (herdCenter != null && !string.IsNullOrEmpty(this.HerdName))
					{
						float distanceToCenter = Vector3.Distance(herdCenter.Value, this.m_componentCreature.ComponentBody.Position);

						// Calcular importancia basada en la distancia al centro de la manada
						this.m_importanceLevel = this.CalculateImportanceLevel(distanceToCenter);
					}
				}

				if (this.IsActive)
				{
					this.m_stateMachine.TransitionTo("Herd");
				}
			}, null);

			this.m_stateMachine.AddState("Stuck", delegate
			{
				this.m_stateMachine.TransitionTo("Herd");
				if (this.m_random.Bool(0.5f))
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
					this.m_importanceLevel = 0f;
				}
			}, null, null);

			this.m_stateMachine.AddState("Herd", delegate
			{
				Vector3? herdCenter = this.FindHerdCenter();
				if (herdCenter != null && !string.IsNullOrEmpty(this.HerdName))
				{
					float distanceToCenter = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, herdCenter.Value);

					// Si está demasiado lejos del centro de la manada, moverse hacia él
					if (distanceToCenter > 6f)
					{
						float speed = (this.m_importanceLevel > 10f) ? this.m_random.Float(0.9f, 1f) : this.m_random.Float(0.25f, 0.35f);
						int maxPathfindingPositions = (this.m_importanceLevel > 200f) ? 100 : 0;
						this.m_componentPathfinding.SetDestination(new Vector3?(herdCenter.Value), speed, 7f, maxPathfindingPositions, false, true, false, null);
						return;
					}
				}
				this.m_importanceLevel = 0f;
			}, delegate
			{
				// Control de mirada aleatoria
				this.m_componentCreature.ComponentLocomotion.LookOrder = this.m_look - this.m_componentCreature.ComponentLocomotion.LookAngles;

				if (this.m_componentPathfinding.IsStuck)
				{
					this.m_stateMachine.TransitionTo("Stuck");
				}

				if (this.m_componentPathfinding.Destination == null)
				{
					this.m_importanceLevel = 0f;
				}

				// Sonidos aleatorios
				if (this.m_random.Float(0f, 1f) < 0.05f * this.m_dt)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}

				// Movimiento aleatorio de cabeza
				if (this.m_random.Float(0f, 1f) < 1.5f * this.m_dt)
				{
					this.m_look = new Vector2(MathUtils.DegToRad(45f) * this.m_random.Float(-1f, 1f), MathUtils.DegToRad(10f) * this.m_random.Float(-1f, 1f));
				}
			}, null);
		}

		// Calcular nivel de importancia basado en la distancia
		private float CalculateImportanceLevel(float distanceToCenter)
		{
			float importance = 0f;

			if (distanceToCenter > 10f)
			{
				importance = 1f;
			}
			if (distanceToCenter > 12f)
			{
				importance = 3f;
			}
			if (distanceToCenter > 16f)
			{
				importance = 50f;
			}
			if (distanceToCenter > 20f)
			{
				importance = 250f;
			}

			return importance;
		}

		// Método para ayudar a otra criatura de la misma manada
		public void HelpHerdMember(ComponentCreature herdMemberInCombat)
		{
			if (herdMemberInCombat == null || !this.m_autoNearbyCreaturesHelp)
			{
				return;
			}

			// Verificar que sea de la misma manada
			if (!this.IsSameHerd(herdMemberInCombat))
			{
				return;
			}

			// Buscar el objetivo del miembro de la manada que está en combate
			ComponentChaseBehavior herdMemberChase = herdMemberInCombat.Entity.FindComponent<ComponentChaseBehavior>();
			if (herdMemberChase != null && herdMemberChase.Target != null)
			{
				// Atacar al mismo objetivo que el miembro de la manada
				if (this.ShouldAttackCreature(herdMemberChase.Target))
				{
					this.m_componentChase?.Attack(herdMemberChase.Target, 20f, 30f, false);
				}
			}
		}

		// Método para prevenir ataques entre miembros de la misma manada
		public void PreventFriendlyFire()
		{
			if (this.m_componentChase != null && this.m_componentChase.Target != null)
			{
				ComponentCreature target = this.m_componentChase.Target;

				// Verificar si el objetivo es de la misma manada
				if (!this.ShouldAttackCreature(target))
				{
					// Detener el ataque
					this.m_componentChase.StopAttack();
				}
			}
		}

		// Método público para verificar si puede atacar (usado por otros componentes)
		public bool CanAttackCreature(ComponentCreature target)
		{
			return this.ShouldAttackCreature(target);
		}

		// Campos privados
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentChaseBehavior m_componentChase;
		private Action<ComponentCreature, float, float, bool> m_originalChaseAttack;
		private Action<ComponentCreature, float, float, bool> m_chaseAttackDelegate;
		private StateMachine m_stateMachine = new StateMachine();
		private float m_dt;
		private float m_importanceLevel;
		private Random m_random = new Random();
		private Vector2 m_look;
		private float m_herdingRange;
		private bool m_autoNearbyCreaturesHelp;
	}
}
