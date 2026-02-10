using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentTankLauncherBehavior : Component, IUpdateable
	{
		// Propiedades configurables desde XML
		private List<int> m_throwableItems = new List<int>();
		private float m_minDistance = 5f;
		private float m_maxDistance = 100f;
		private float m_reloadTime = 0.5f;
		private bool m_usesInventory = true;

		// Estado interno
		private float m_reloadCountdown;
		private float m_launchAnimationPhase;
		private bool m_isLaunching;
		private Vector3 m_targetPosition;
		private int m_selectedItemIndex = 0;
		private float m_attackCooldown;
		private const float AttackInterval = 2f;
		private Random m_random = new Random();

		// Referencias a otros componentes
		private ComponentTankModel m_tankModel;
		private ComponentInventory m_inventory;
		private ComponentCreature m_creature;
		private ComponentZombieChaseBehavior m_ZombiechaseBehavior;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemTime m_subsystemTime;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public bool CanLaunch => m_reloadCountdown <= 0f && !m_isLaunching;

		public bool IsLaunching => m_isLaunching;

		public float LaunchAnimationPhase => m_launchAnimationPhase;

		private bool HasAmmo()
		{
			if (!m_usesInventory) return true; // Si no usa inventario, siempre puede lanzar

			if (m_inventory == null) return true; // Si no tiene inventario, puede lanzar igual

			// Si la lista de items lanzables está vacía, puede lanzar cualquier cosa
			if (m_throwableItems.Count == 0) return true;

			// Buscar cualquier item lanzable en el inventario
			for (int i = 0; i < m_inventory.VisibleSlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				if (value != 0)
				{
					int blockIndex = Terrain.ExtractContents(value);
					if (m_throwableItems.Contains(blockIndex))
					{
						m_selectedItemIndex = i;
						return true;
					}
				}
			}

			// Si no encuentra items específicos, puede lanzar con inventario vacío
			return true;
		}

		private int GetCurrentAmmoValue()
		{
			if (!m_usesInventory || m_inventory == null)
			{
				// Si no usa inventario o no tiene, usar el primer item de la lista o 0
				return m_throwableItems.Count > 0 ? m_throwableItems[0] : 0;
			}

			// Intentar usar el item seleccionado
			if (m_selectedItemIndex < m_inventory.VisibleSlotsCount)
			{
				int value = m_inventory.GetSlotValue(m_selectedItemIndex);
				if (value != 0) return value;
			}

			// Buscar cualquier item en el inventario
			for (int i = 0; i < m_inventory.VisibleSlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				if (value != 0) return value;
			}

			// Inventario vacío o sin items específicos, usar 0 (puede lanzar "nada")
			return 0;
		}

		private void ConsumeAmmo()
		{
			if (!m_usesInventory || m_inventory == null) return;

			// Solo consumir si tiene el item específico
			if (m_throwableItems.Count > 0)
			{
				int count = m_inventory.GetSlotCount(m_selectedItemIndex);
				if (count > 0)
				{
					m_inventory.RemoveSlotItems(m_selectedItemIndex, 1);
				}
			}
		}

		private void LaunchAtTarget(Vector3 targetPosition)
		{
			if (!CanLaunch) return;

			// Validar distancia
			float distance = Vector3.Distance(m_creature.ComponentBody.Position, targetPosition);
			if (distance < m_minDistance || distance > m_maxDistance)
			{
				return;
			}

			m_targetPosition = targetPosition;
			m_isLaunching = true;
			m_launchAnimationPhase = 0f;

			// Activar animación en el modelo del tanque
			if (m_tankModel != null)
			{
				// Levantar brazos para animación
				m_tankModel.SetTurretRotation(MathUtils.DegToRad(30f));
				m_tankModel.SetCannonElevation(MathUtils.DegToRad(20f));
			}
		}

		private void ExecuteLaunch()
		{
			Matrix matrix = m_creature.ComponentBody.Matrix;
			Vector3 position = m_creature.ComponentBody.Position +
				Vector3.Transform(new Vector3(0f, 1.5f, 2f), matrix);

			// Calcular dirección hacia el objetivo
			Vector3 direction = Vector3.Normalize(m_targetPosition - position);

			// Ajustar altura según distancia
			float distance = Vector3.Distance(position, m_targetPosition);
			float optimalAngle = 45f;
			float force = MathUtils.Sqrt(distance * 9.8f / MathF.Sin(2 * MathUtils.DegToRad(optimalAngle)));
			force = MathUtils.Clamp(force, distance * 0.3f, distance * 1.5f);

			Vector3 velocity = direction * force;
			velocity.Y += MathUtils.Sqrt(distance * 4.9f); // Componente vertical para trayectoria parabólica

			// Usar Random del sistema o crear uno
			Vector3 angularVelocity = new Vector3(
				m_random.Float(-5f, 5f),
				m_random.Float(-5f, 5f),
				m_random.Float(-5f, 5f));

			// Obtener el item a lanzar
			int projectileValue = GetCurrentAmmoValue();

			if (projectileValue != 0 || true) // SIEMPRE puede lanzar, incluso con valor 0
			{
				// Crear y lanzar el proyectil
				Projectile projectile = m_subsystemProjectiles.FireProjectile(
					projectileValue, position, velocity, angularVelocity, m_creature);

				if (projectile != null)
				{
					projectile.Gravity = 9.8f;
					projectile.IsIncendiary = false;
					projectile.MinVelocityToAttack = 5f;
					projectile.AttackPower = 10f;

					// Efecto de retroceso
					if (m_tankModel != null)
					{
						m_tankModel.FireMainCannon();
					}

					// Consumir munición solo si tiene
					ConsumeAmmo();
				}
			}
			else
			{
				// Lanzar un proyectil invisible o de efecto
				Projectile projectile = m_subsystemProjectiles.FireProjectile(
					0, position, velocity, angularVelocity, m_creature);

				if (projectile != null)
				{
					projectile.Gravity = 9.8f;
					projectile.IsIncendiary = false;
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;

					if (m_tankModel != null)
					{
						m_tankModel.FireMainCannon();
					}
				}
			}

			// Iniciar recarga
			m_reloadCountdown = m_reloadTime;
			m_isLaunching = false;
			m_attackCooldown = AttackInterval;

			// Restaurar animación
			if (m_tankModel != null)
			{
				m_tankModel.SetTurretRotation(0f);
				m_tankModel.SetCannonElevation(MathUtils.DegToRad(15f));
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar configuración desde XML
			string throwableItemsStr = valuesDictionary.GetValue<string>("ThrowableItems", "");
			if (!string.IsNullOrEmpty(throwableItemsStr))
			{
				string[] items = throwableItemsStr.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

				foreach (string itemName in items)
				{
					string trimmedName = itemName.Trim();
					if (trimmedName == "0" || string.IsNullOrEmpty(trimmedName))
					{
						m_throwableItems.Add(0); // ACEPTA 0 (AirBlock)
					}
					else
					{
						int blockIndex = BlocksManager.GetBlockIndex(trimmedName, false);
						if (blockIndex >= 0) // ACEPTA CUALQUIER ÍNDICE VÁLIDO
						{
							m_throwableItems.Add(blockIndex);
						}
						else
						{
							// Si no encuentra el bloque por nombre, agregar 0
							m_throwableItems.Add(0);
						}
					}
				}
			}
			else
			{
				// Si no se especifican items, agregar 0 por defecto
				m_throwableItems.Add(0);
			}

			string distanceRange = valuesDictionary.GetValue<string>("MinMaxDistance", "5;100");
			string[] distances = distanceRange.Split(';');
			if (distances.Length >= 2)
			{
				float.TryParse(distances[0], out m_minDistance);
				float.TryParse(distances[1], out m_maxDistance);
			}

			m_reloadTime = valuesDictionary.GetValue<float>("ReloadTime", 0.5f);
			m_usesInventory = valuesDictionary.GetValue<bool>("UsesInventory", true);

			// Obtener referencias a otros componentes
			m_creature = Entity.FindComponent<ComponentCreature>(true);
			m_tankModel = Entity.FindComponent<ComponentTankModel>();
			m_inventory = Entity.FindComponent<ComponentInventory>();
			m_ZombiechaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>();
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);

			// Inicializar estado
			m_reloadCountdown = 0f;
			m_launchAnimationPhase = 0f;
			m_isLaunching = false;
			m_attackCooldown = 0f;
			m_random = new Random();
		}

		public void Update(float dt)
		{
			// Actualizar contador de recarga
			if (m_reloadCountdown > 0f)
			{
				m_reloadCountdown -= dt;
			}

			// Actualizar cooldown de ataque
			if (m_attackCooldown > 0f)
			{
				m_attackCooldown -= dt;
			}

			// Actualizar animación de lanzamiento
			if (m_isLaunching)
			{
				m_launchAnimationPhase += dt * (1f / m_reloadTime) * 3f;

				// Punto de lanzamiento a la mitad de la animación
				if (m_launchAnimationPhase >= 0.5f && m_launchAnimationPhase - dt < 0.5f)
				{
					ExecuteLaunch();
				}

				// Finalizar animación
				if (m_launchAnimationPhase >= 1f)
				{
					m_launchAnimationPhase = 0f;
					m_isLaunching = false;
				}
			}
			else if (m_attackCooldown <= 0f && m_creature != null && m_ZombiechaseBehavior != null)
			{
				// Lanzar automáticamente cuando tenga un objetivo
				ComponentCreature target = m_ZombiechaseBehavior.Target;
				if (target != null && target.ComponentHealth.Health > 0f)
				{
					float distance = Vector3.Distance(m_creature.ComponentBody.Position, target.ComponentBody.Position);
					if (distance >= m_minDistance && distance <= m_maxDistance)
					{
						LaunchAtTarget(target.ComponentBody.Position);
					}
				}
			}
		}

		// Método público para lanzamiento manual
		public void LaunchAtPosition(Vector3 targetPosition)
		{
			LaunchAtTarget(targetPosition);
		}

		// Método para lanzamiento forzado
		public void ForceLaunchAtPosition(Vector3 targetPosition)
		{
			m_reloadCountdown = 0f;
			m_attackCooldown = 0f;
			LaunchAtTarget(targetPosition);
		}

		// Métodos de configuración
		public void AddThrowableItem(int blockIndex)
		{
			if (!m_throwableItems.Contains(blockIndex))
			{
				m_throwableItems.Add(blockIndex);
			}
		}

		public void RemoveThrowableItem(int blockIndex)
		{
			m_throwableItems.Remove(blockIndex);
		}

		public void ClearThrowableItems()
		{
			m_throwableItems.Clear();
		}

		public List<int> GetThrowableItems()
		{
			return new List<int>(m_throwableItems);
		}

		public void SetThrowableItems(List<int> items)
		{
			m_throwableItems = new List<int>(items);
		}

		public void SetDistanceRange(float min, float max)
		{
			m_minDistance = Math.Max(1f, min);
			m_maxDistance = Math.Max(m_minDistance + 1f, max);
		}

		public void SetReloadTime(float reloadTime)
		{
			m_reloadTime = Math.Max(0.1f, reloadTime);
		}

		public void SetUsesInventory(bool usesInventory)
		{
			m_usesInventory = usesInventory;
		}
	}
}