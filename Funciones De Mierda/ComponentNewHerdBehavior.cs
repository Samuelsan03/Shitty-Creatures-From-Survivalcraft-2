using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewHerdBehavior : ComponentBehavior, IUpdateable
	{
		// ===== PROPIEDADES PÚBLICAS =====
		public string HerdName { get; set; }  // ÚNICA propiedad editable

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		// Propiedades de solo lectura
		public bool AutoNearbyCreaturesHelp => true;  // Fijo
		public float HerdingRange => 20f;             // Fijo
		public float HelpCallRange => 16f;             // Fijo
		public float MaxHelpChaseTime => 30f;          // Fijo

		// ===== MÉTODOS PÚBLICOS =====
		public void CallNearbyCreaturesHelp(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent, bool forceResponse = false)
		{
			if (target == null) return;

			ComponentNewHerdBehavior targetHerdBehavior = target.Entity.FindComponent<ComponentNewHerdBehavior>();

			if (targetHerdBehavior != null && !string.IsNullOrEmpty(targetHerdBehavior.HerdName)
				&& IsSameHerdOrGuardian(target))
			{
				return;
			}

			Vector3 position = target.ComponentBody.Position;
			float rangeSquared = maxRange * maxRange;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (Vector3.DistanceSquared(position, creature.ComponentBody.Position) > rangeSquared)
					continue;

				ComponentNewHerdBehavior herdBehavior = creature.Entity.FindComponent<ComponentNewHerdBehavior>();

				if (herdBehavior != null && !string.IsNullOrEmpty(herdBehavior.HerdName) &&
					(IsSameHerdOrGuardian(creature) || herdBehavior.HerdName.Equals(HerdName, StringComparison.OrdinalIgnoreCase)) &&
					(true || forceResponse)) // AutoNearbyCreaturesHelp siempre true
				{
					ComponentChaseBehavior chaseBehavior = creature.Entity.FindComponent<ComponentChaseBehavior>();

					if (chaseBehavior != null && (forceResponse || chaseBehavior.Target == null))
					{
						ComponentNewHerdBehavior targetHerd = target.Entity.FindComponent<ComponentNewHerdBehavior>();
						if (targetHerd == null || !IsSameHerdOrGuardian(target))
						{
							chaseBehavior.Attack(target, maxRange, maxChaseTime, isPersistent);
						}
					}
				}
			}
		}

		public void RespondToCommandImmediately(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (target == null || !CanAttackCreature(target))
				return;

			m_componentChase?.Attack(target, maxRange, maxChaseTime, isPersistent);
			CallNearbyCreaturesHelp(target, 15f, 30f, false, true);
		}

		public Vector3? FindHerdCenter()
		{
			if (string.IsNullOrEmpty(HerdName))
				return null;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			Vector3 center = Vector3.Zero;
			int count = 0;
			float herdingRangeSquared = 20f * 20f; // Fijo

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature.ComponentHealth.Health <= 0f)
					continue;

				ComponentNewHerdBehavior herdBehavior = creature.Entity.FindComponent<ComponentNewHerdBehavior>();

				if (herdBehavior != null &&
					(IsSameHerdOrGuardian(creature) || herdBehavior.HerdName.Equals(HerdName, StringComparison.OrdinalIgnoreCase)))
				{
					if (Vector3.DistanceSquared(position, creature.ComponentBody.Position) <= herdingRangeSquared)
					{
						center += creature.ComponentBody.Position;
						count++;
					}
				}
			}

			return count > 0 ? center / count : (Vector3?)null;
		}

		public bool IsSameHerdOrGuardian(ComponentCreature otherCreature)
		{
			if (otherCreature == null || string.IsNullOrEmpty(HerdName))
				return false;

			ComponentNewHerdBehavior otherHerd = otherCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (otherHerd == null || string.IsNullOrEmpty(otherHerd.HerdName))
				return false;

			if (HerdName.Equals(otherHerd.HerdName, StringComparison.OrdinalIgnoreCase))
				return true;

			bool isPlayer = HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
			bool isGuardian = HerdName.ToLower().Contains("guardian");
			bool otherIsPlayer = otherHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
			bool otherIsGuardian = otherHerd.HerdName.ToLower().Contains("guardian");

			return (isPlayer && otherIsGuardian) || (isGuardian && otherIsPlayer);
		}

		public bool IsSameHerd(ComponentCreature otherCreature)
		{
			if (otherCreature == null || string.IsNullOrEmpty(HerdName))
				return false;

			ComponentNewHerdBehavior otherHerd = otherCreature.Entity.FindComponent<ComponentNewHerdBehavior>();

			return otherHerd != null &&
				   !string.IsNullOrEmpty(otherHerd.HerdName) &&
				   HerdName.Equals(otherHerd.HerdName, StringComparison.OrdinalIgnoreCase);
		}

		public bool ShouldAttackCreature(ComponentCreature target)
		{
			if (target == null || string.IsNullOrEmpty(HerdName))
				return true;

			if (IsSameHerdOrGuardian(target))
				return false;

			if (HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
			{
				if (target.Entity.FindComponent<ComponentPlayer>() != null)
					return false;
			}

			if (HerdName.ToLower().Contains("guardian"))
			{
				if (target.Entity.FindComponent<ComponentPlayer>() != null)
					return false;
			}

			return true;
		}

		public bool CanAttackCreature(ComponentCreature target) => ShouldAttackCreature(target);

		public void HelpHerdMember(ComponentCreature herdMemberInCombat)
		{
			if (herdMemberInCombat == null || !IsSameHerdOrGuardian(herdMemberInCombat))
				return;

			ComponentChaseBehavior memberChase = herdMemberInCombat.Entity.FindComponent<ComponentChaseBehavior>();

			if (memberChase?.Target != null && ShouldAttackCreature(memberChase.Target))
			{
				m_componentChase?.Attack(memberChase.Target, 20f, 30f, false);
			}
		}

		public void PreventFriendlyFire()
		{
			if (m_componentChase?.Target != null && !ShouldAttackCreature(m_componentChase.Target))
			{
				m_componentChase.StopAttack();
			}
		}

		// ===== IMPLEMENTACIÓN DE IUpdateable =====
		public virtual void Update(float dt)
		{
			if (string.IsNullOrEmpty(m_stateMachine.CurrentState) || !IsActive)
				m_stateMachine.TransitionTo("Inactive");

			m_dt = dt;
			m_stateMachine.Update();
			CheckChaseBehaviorTarget();
		}

		// ===== MÉTODOS PRIVADOS =====
		private void CheckChaseBehaviorTarget()
		{
			if (m_componentChase?.Target != null && !ShouldAttackCreature(m_componentChase.Target))
			{
				m_componentChase.StopAttack();
			}
		}

		private void OnInjured(Injury injury)
		{
			ComponentCreature attacker = injury.Attacker;
			if (attacker != null && ShouldAttackCreature(attacker))
			{
				CallNearbyCreaturesHelp(attacker, 20f, 30f, false, true);
			}
		}

		private float CalculateImportanceLevel(float distanceToCenter)
		{
			if (distanceToCenter > 20f) return 250f;
			if (distanceToCenter > 16f) return 50f;
			if (distanceToCenter > 12f) return 3f;
			if (distanceToCenter > 10f) return 1f;
			return 0f;
		}

		// ===== CONFIGURACIÓN DE ESTADOS =====
		private void SetupStateMachine()
		{
			m_stateMachine.AddState("Inactive", null, () =>
			{
				if (m_subsystemTime.PeriodicGameTimeEvent(1.0, (GetHashCode() % 256) / 256.0))
				{
					Vector3? herdCenter = FindHerdCenter();
					if (herdCenter != null && !string.IsNullOrEmpty(HerdName))
					{
						float distance = Vector3.Distance(herdCenter.Value, m_componentCreature.ComponentBody.Position);
						m_importanceLevel = CalculateImportanceLevel(distance);
					}
				}

				if (IsActive)
					m_stateMachine.TransitionTo("Herd");
			}, null);

			m_stateMachine.AddState("Stuck", () =>
			{
				m_stateMachine.TransitionTo("Herd");
				if (m_random.Bool(0.5f))
				{
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
					m_importanceLevel = 0f;
				}
			}, null, null);

			m_stateMachine.AddState("Herd", () =>
			{
				Vector3? herdCenter = FindHerdCenter();
				if (herdCenter != null && !string.IsNullOrEmpty(HerdName))
				{
					float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, herdCenter.Value);

					if (distance > 6f)
					{
						float speed = (m_importanceLevel > 10f) ? m_random.Float(0.9f, 1f) : m_random.Float(0.25f, 0.35f);
						int maxPathfinding = (m_importanceLevel > 200f) ? 100 : 0;
						m_componentPathfinding.SetDestination(herdCenter, speed, 7f, maxPathfinding, false, true, false, null);
						return;
					}
				}
				m_importanceLevel = 0f;
			}, () =>
			{
				m_componentCreature.ComponentLocomotion.LookOrder = m_look - m_componentCreature.ComponentLocomotion.LookAngles;

				if (m_componentPathfinding.IsStuck)
					m_stateMachine.TransitionTo("Stuck");

				if (m_componentPathfinding.Destination == null)
					m_importanceLevel = 0f;

				if (m_random.Float(0f, 1f) < 0.05f * m_dt)
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);

				if (m_random.Float(0f, 1f) < 1.5f * m_dt)
				{
					m_look = new Vector2(
						MathUtils.DegToRad(45f) * m_random.Float(-1f, 1f),
						MathUtils.DegToRad(10f) * m_random.Float(-1f, 1f)
					);
				}
			}, null);
		}

		// ===== LOAD - SOLO LEE HerdName =====
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Subsistemas
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);

			// Componentes
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentChase = Entity.FindComponent<ComponentChaseBehavior>();

			// SOLO LEER HERDNAME - NADA MÁS
			HerdName = valuesDictionary.GetValue<string>("HerdName", "");

			// Evento de daño
			m_componentCreature.ComponentHealth.Injured += OnInjured;

			// Inicializar máquina de estados
			SetupStateMachine();
		}

		// ===== SAVE - SOLO GUARDA HerdName =====
		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			// SOLO GUARDAR HERDNAME - NADA MÁS
			if (!string.IsNullOrEmpty(HerdName))
			{
				valuesDictionary.SetValue("HerdName", HerdName);
			}
		}

		// ===== CAMPOS =====
		protected SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		protected SubsystemTime m_subsystemTime;
		protected ComponentCreature m_componentCreature;
		protected ComponentPathfinding m_componentPathfinding;
		protected ComponentChaseBehavior m_componentChase;
		protected StateMachine m_stateMachine = new StateMachine();

		protected float m_dt;
		protected float m_importanceLevel;
		protected Random m_random = new Random();
		protected Vector2 m_look;
	}
}
