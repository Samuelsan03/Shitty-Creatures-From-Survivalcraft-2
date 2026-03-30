using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Comportamiento de rabia para zombis usando máquina de estados.
	/// Cuando la salud cae por debajo del 30% entra en estado de rabia (aumenta daño y velocidad).
	/// Al recuperarse por encima del 40% vuelve al estado normal.
	/// No requiere parámetros adicionales en la plantilla.
	/// </summary>
	public class ComponentRageZombieBehavior : ComponentBehavior, IUpdateable
	{
		// Umbrales de activación y desactivación de la rabia
		private const float RageActivationThreshold = 0.3f;   // 30% de vida o menos activa la rabia
		private const float RageDeactivationThreshold = 0.4f; // 40% o más desactiva la rabia

		// Multiplicadores de la rabia (valores por defecto)
		private const float RageSpeedMultiplier = 2.5f;      // Velocidad de movimiento x2.5
		private const float RageDamageMultiplier = 2.5f;     // Daño de ataque x2.5

		// Referencias a componentes necesarios
		private ComponentHealth m_componentHealth;
		private ComponentLocomotion m_componentLocomotion;
		private ComponentMiner m_componentMiner;
		private ComponentCreature m_componentCreature;
		private ComponentZombieChaseBehavior m_componentZombieChase;

		// Referencias a subsistemas
		private SubsystemTime m_subsystemTime;
		private SubsystemGameInfo m_subsystemGameInfo;

		// Valores originales (para restaurar al salir de la rabia)
		private float m_originalWalkSpeed;
		private float m_originalAttackPower;
		private float m_originalTargetInRangeTimeToChase; // Tiempo de persecución original
		private bool m_originalValuesStored;

		// Vida máxima (para calcular el porcentaje)
		private float m_maxHealth;

		// Máquina de estados
		private StateMachine m_stateMachine;

		// Propiedades de la interfaz IUpdateable
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// Importancia del comportamiento (siempre activo)
		public override float ImportanceLevel => 10f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Obtener subsistemas
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);

			// Obtener referencias a los componentes necesarios
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			if (m_componentCreature == null)
				return;

			m_componentHealth = m_componentCreature.ComponentHealth;
			m_componentLocomotion = Entity.FindComponent<ComponentLocomotion>();
			m_componentMiner = Entity.FindComponent<ComponentMiner>();
			m_componentZombieChase = Entity.FindComponent<ComponentZombieChaseBehavior>();

			if (m_componentHealth == null)
				return;

			// Almacenar la vida máxima
			m_maxHealth = m_componentHealth.Health;

			// Guardar valores originales
			StoreOriginalValues();

			// Inicializar la máquina de estados
			InitializeStateMachine();

			// Determinar el estado inicial según la salud actual
			if (m_maxHealth > 0f && (m_componentHealth.Health / m_maxHealth) <= RageActivationThreshold)
			{
				m_stateMachine.TransitionTo("Rage");
			}
			else
			{
				m_stateMachine.TransitionTo("Normal");
			}
		}

		private void StoreOriginalValues()
		{
			if (m_originalValuesStored)
				return;

			if (m_componentLocomotion != null)
			{
				m_originalWalkSpeed = m_componentLocomotion.WalkSpeed;
			}

			if (m_componentMiner != null)
			{
				m_originalAttackPower = m_componentMiner.AttackPower;
			}

			if (m_componentZombieChase != null)
			{
				// Guardar el tiempo de persecución original (TargetInRangeTimeToChase)
				// Usamos reflexión o asumimos que existe un campo accesible
				var field = typeof(ComponentZombieChaseBehavior).GetField("m_defaultTargetInRangeTime",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (field != null)
				{
					m_originalTargetInRangeTimeToChase = (float)field.GetValue(m_componentZombieChase);
				}
				else
				{
					// Valor por defecto si no se puede obtener
					m_originalTargetInRangeTimeToChase = 3f;
				}
			}

			m_originalValuesStored = true;
		}

		private void InitializeStateMachine()
		{
			m_stateMachine = new StateMachine();

			// Estado Normal
			m_stateMachine.AddState("Normal",
				enter: OnEnterNormal,
				update: OnUpdateNormal,
				leave: OnLeaveNormal
			);

			// Estado Rage (rabia)
			m_stateMachine.AddState("Rage",
				enter: OnEnterRage,
				update: OnUpdateRage,
				leave: OnLeaveRage
			);
		}

		// --------------------------------------------------
		// Estado Normal
		// --------------------------------------------------
		private void OnEnterNormal()
		{
			// Restaurar valores originales si veníamos de rabia
			if (m_componentLocomotion != null)
			{
				m_componentLocomotion.WalkSpeed = m_originalWalkSpeed;
			}
			if (m_componentMiner != null)
			{
				m_componentMiner.AttackPower = m_originalAttackPower;
			}
			if (m_componentZombieChase != null)
			{
				// Restaurar tiempo de persecución original
				var field = typeof(ComponentZombieChaseBehavior).GetField("m_defaultTargetInRangeTime",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (field != null)
				{
					field.SetValue(m_componentZombieChase, m_originalTargetInRangeTimeToChase);
				}
				// También restaurar TargetInRangeTimeToChase si es accesible
				var prop = typeof(ComponentZombieChaseBehavior).GetProperty("TargetInRangeTimeToChase",
					System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
				if (prop != null && prop.CanWrite)
				{
					prop.SetValue(m_componentZombieChase, m_originalTargetInRangeTimeToChase);
				}
			}
		}

		private void OnUpdateNormal()
		{
			// Verificar si debemos entrar en rabia
			if (m_componentHealth == null || m_maxHealth <= 0f)
				return;

			float healthRatio = m_componentHealth.Health / m_maxHealth;
			if (healthRatio <= RageActivationThreshold)
			{
				m_stateMachine.TransitionTo("Rage");
			}
		}

		private void OnLeaveNormal()
		{
			// No es necesario hacer nada al salir del estado normal
		}

		// --------------------------------------------------
		// Estado Rage
		// --------------------------------------------------
		private void OnEnterRage()
		{
			// Aplicar multiplicadores de rabia
			if (m_componentLocomotion != null)
			{
				m_componentLocomotion.WalkSpeed = m_originalWalkSpeed * RageSpeedMultiplier;
			}
			if (m_componentMiner != null)
			{
				m_componentMiner.AttackPower = m_originalAttackPower * RageDamageMultiplier;
			}
			if (m_componentZombieChase != null)
			{
				// En rabia, reducir el tiempo de persecución a 0 (ataque inmediato)
				var prop = typeof(ComponentZombieChaseBehavior).GetProperty("TargetInRangeTimeToChase",
					System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
				if (prop != null && prop.CanWrite)
				{
					prop.SetValue(m_componentZombieChase, 0f);
				}
			}

			// Aquí se podrían añadir efectos visuales o sonidos
			// Por ejemplo, hacer que el zombi grite al activar la rabia
			if (m_componentCreature != null && m_componentCreature.ComponentCreatureSounds != null)
			{
				m_componentCreature.ComponentCreatureSounds.PlayPainSound();
			}
		}

		private void OnUpdateRage()
		{
			// Verificar si debemos salir de la rabia
			if (m_componentHealth == null || m_maxHealth <= 0f)
				return;

			float healthRatio = m_componentHealth.Health / m_maxHealth;
			if (healthRatio >= RageDeactivationThreshold)
			{
				m_stateMachine.TransitionTo("Normal");
			}
		}

		private void OnLeaveRage()
		{
			// No es necesario hacer nada específico al salir de rabia,
			// la restauración se hará al entrar a Normal.
		}

		// --------------------------------------------------
		// Update del componente (IUpdateable)
		// --------------------------------------------------
		public void Update(float dt)
		{
			// Si no hay salud o la criatura está muerta, no hacemos nada
			if (m_componentHealth == null || m_componentHealth.Health <= 0f || m_maxHealth <= 0f)
				return;

			// Asegurar que los valores originales están almacenados
			if (!m_originalValuesStored)
				StoreOriginalValues();

			// Actualizar la máquina de estados
			m_stateMachine?.Update();
		}
	}
}
