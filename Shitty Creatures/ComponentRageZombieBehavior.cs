using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Comportamiento de furia para zombis: cuando la salud cae por debajo del 30%,
	/// el zombi entra en modo furia, aumentando poder de ataque, velocidades,
	/// y modificando comportamientos de persecución y huida.
	/// </summary>
	public class ComponentRageZombieBehavior : ComponentBehavior, IUpdateable
	{
		// --- Constantes fijas ---
		private const float RageHealthThreshold = 0.3f;
		private const float AttackPowerMultiplier = 2.5f;
		private const float SpeedMultiplier = 2.5f;

		// --- Componentes específicos de zombi ---
		private ComponentCreature m_componentCreature;
		private ComponentHealth m_componentHealth;
		private ComponentLocomotion m_componentLocomotion;
		private ComponentMiner m_componentMiner;
		private ComponentZombieChaseBehavior m_componentZombieChaseBehavior;
		private ComponentZombieRunAwayBehavior m_componentZombieRunAwayBehavior;

		// --- State Machine ---
		private StateMachine m_stateMachine;

		// --- Valores originales para restaurar ---
		private float m_originalAttackPower;
		private float m_originalWalkSpeed;
		private float m_originalSwimSpeed;
		private float m_originalFlySpeed;
		private float m_originalJumpSpeed;
		private bool m_originalRunAwayEnabled;

		// --- Estado interno ---
		private bool m_isRaging;

		// --- Propiedades de comportamiento ---
		public override float ImportanceLevel => m_isRaging ? 20f : 0f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// ------------------------------------------------------------------------
		// Carga: solo busca componentes, no lee ValuesDictionary
		// ------------------------------------------------------------------------
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentLocomotion = Entity.FindComponent<ComponentLocomotion>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>();
			m_componentZombieChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>();
			m_componentZombieRunAwayBehavior = Entity.FindComponent<ComponentZombieRunAwayBehavior>();

			m_stateMachine = new StateMachine();
			InitializeStateMachine();

			m_isRaging = false;
			m_stateMachine.TransitionTo("Normal");
		}

		// ------------------------------------------------------------------------
		// Configuración de la máquina de estados
		// ------------------------------------------------------------------------
		private void InitializeStateMachine()
		{
			m_stateMachine.AddState("Normal",
				enter: () =>
				{
					RestoreOriginalValues();
					m_isRaging = false;
				},
				update: () =>
				{
					if (ShouldEnterRage())
					{
						m_stateMachine.TransitionTo("Rage");
					}
				},
				leave: null
			);

			m_stateMachine.AddState("Rage",
				enter: () =>
				{
					ApplyRageBonuses();
					m_isRaging = true;
				},
				update: () =>
				{
					if (!ShouldEnterRage())
					{
						m_stateMachine.TransitionTo("Normal");
					}
				},
				leave: () =>
				{
					RestoreOriginalValues();
					m_isRaging = false;
				}
			);
		}

		// ------------------------------------------------------------------------
		// Condición de furia
		// ------------------------------------------------------------------------
		private bool ShouldEnterRage()
		{
			if (m_componentHealth == null)
				return false;

			float health = m_componentHealth.Health;
			return health <= RageHealthThreshold && health > 0f;
		}

		// ------------------------------------------------------------------------
		// Aplicar bonificaciones de furia (ataque, locomoción, chase, huida)
		// ------------------------------------------------------------------------
		private void ApplyRageBonuses()
		{
			// 1. Ataque (ComponentMiner)
			if (m_componentMiner != null)
			{
				m_originalAttackPower = m_componentMiner.AttackPower;
				m_componentMiner.AttackPower *= AttackPowerMultiplier;
			}

			// 2. Locomoción
			if (m_componentLocomotion != null)
			{
				m_originalWalkSpeed = m_componentLocomotion.WalkSpeed;
				m_originalSwimSpeed = m_componentLocomotion.SwimSpeed;
				m_originalFlySpeed = m_componentLocomotion.FlySpeed;
				m_originalJumpSpeed = m_componentLocomotion.JumpSpeed;

				m_componentLocomotion.WalkSpeed *= SpeedMultiplier;
				m_componentLocomotion.SwimSpeed *= SpeedMultiplier;
				m_componentLocomotion.FlySpeed *= SpeedMultiplier;
				m_componentLocomotion.JumpSpeed *= SpeedMultiplier;
			}

			// 3. Persecución (ZombieChaseBehavior)
			if (m_componentZombieChaseBehavior != null)
			{
				// Aumentar agresividad: reducir tiempo necesario para empezar a perseguir
				m_componentZombieChaseBehavior.TargetInRangeTimeToChase = 0f;
				// Si ya tiene un objetivo, forzar un ataque inmediato
				if (m_componentZombieChaseBehavior.Target != null)
				{
					m_componentZombieChaseBehavior.Attack(
						m_componentZombieChaseBehavior.Target,
						40f,   // Rango típico usado en otras partes del código
						120f,  // Tiempo de persecución largo durante furia
						true
					);
				}
			}

			// 4. Huida (ZombieRunAwayBehavior)
			if (m_componentZombieRunAwayBehavior != null)
			{
				// En furia, el zombi NO debe huir bajo ninguna circunstancia.
				m_originalRunAwayEnabled = (m_componentZombieRunAwayBehavior.LowHealthToEscape > 0f);
				m_componentZombieRunAwayBehavior.LowHealthToEscape = 0f;
			}
		}

		// ------------------------------------------------------------------------
		// Restaurar valores originales
		// ------------------------------------------------------------------------
		private void RestoreOriginalValues()
		{
			if (m_componentMiner != null && m_originalAttackPower > 0f)
			{
				m_componentMiner.AttackPower = m_originalAttackPower;
			}

			if (m_componentLocomotion != null)
			{
				if (m_originalWalkSpeed > 0f) m_componentLocomotion.WalkSpeed = m_originalWalkSpeed;
				if (m_originalSwimSpeed > 0f) m_componentLocomotion.SwimSpeed = m_originalSwimSpeed;
				if (m_originalFlySpeed > 0f) m_componentLocomotion.FlySpeed = m_originalFlySpeed;
				if (m_originalJumpSpeed > 0f) m_componentLocomotion.JumpSpeed = m_originalJumpSpeed;
			}

			if (m_componentZombieChaseBehavior != null)
			{
				// Restaurar tiempo de persecución normal (valor por defecto 3f según código original)
				m_componentZombieChaseBehavior.TargetInRangeTimeToChase = 3f;
			}

			if (m_componentZombieRunAwayBehavior != null)
			{
				// Restaurar umbral de huida original
				m_componentZombieRunAwayBehavior.LowHealthToEscape = m_originalRunAwayEnabled ? 0.2f : 0f;
			}
		}

		// ------------------------------------------------------------------------
		// Update
		// ------------------------------------------------------------------------
		public void Update(float dt)
		{
			if (m_componentHealth == null || m_componentHealth.Health <= 0f)
				return;

			m_stateMachine.Update();
		}

		public bool IsRaging => m_isRaging;
	}
}
