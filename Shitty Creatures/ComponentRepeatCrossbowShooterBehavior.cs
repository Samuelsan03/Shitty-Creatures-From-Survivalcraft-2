using System;
using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Componente que permite a una criatura usar la Ballesta Repetidora como arma a distancia,
	/// con munición ilimitada, tiempo de apuntado configurable y tipo de flecha aleatorio.
	/// Los virotes explosivos solo se disparan si la distancia al objetivo supera el mínimo seguro.
	/// La criatura dispara siempre, sin importar si tiene arma cuerpo a cuerpo.
	/// </summary>
	public class ComponentRepeatCrossbowShooterBehavior : ComponentBehavior, IUpdateable
	{
		public float RepeatCrossbowCooldown = 0.02f;
		public float RepeatCrossbowAimTime = 1.5f;
		public Vector2 RepeatCrossbowRange = new Vector2(5f, 100f);
		public Vector2 ExplosiveArrowRange = new Vector2(20f, 100f); // Distancia mínima segura para flecha explosiva

		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentChaseBehavior m_componentChase;

		private ComponentCreature m_target;
		private double m_aimStartTime;
		private double m_lastShootTime;
		private bool m_isAiming;

		private Random m_random = new Random();

		public override float ImportanceLevel => 200f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentChase = Entity.FindComponent<ComponentChaseBehavior>();
		}

		public void Update(float dt)
		{
			// Obtener objetivo del chase behavior
			if (m_componentChase != null && m_componentChase.Target != null)
			{
				m_target = m_componentChase.Target;
				if (!IsActive)
					IsActive = true;
			}
			else
			{
				if (IsActive)
				{
					IsActive = false;
					StopAiming();
				}
				return;
			}

			if (!IsActive || m_target == null || m_target.ComponentHealth.Health <= 0f)
			{
				StopAiming();
				return;
			}

			if (!HasCrossbowEquipped())
			{
				StopAiming();
				return;
			}

			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, m_target.ComponentBody.Position);

			// Fuera de alcance máximo -> dejar de apuntar
			if (distance > RepeatCrossbowRange.Y)
			{
				StopAiming();
				return;
			}

			// Ya no se intenta cambiar a arma cuerpo a cuerpo.
			// La criatura siempre dispara mientras esté dentro del alcance máximo.

			double currentTime = m_subsystemTime.GameTime;

			if (!m_isAiming)
			{
				StartAiming();
				m_aimStartTime = currentTime;
				m_isAiming = true;
			}

			UpdateAimingModel();

			if (currentTime - m_aimStartTime >= RepeatCrossbowAimTime &&
				currentTime - m_lastShootTime >= RepeatCrossbowCooldown)
			{
				// Preparar la ballesta con munición infinita y tipo de flecha adecuado a la distancia
				PrepareCrossbowForShoot(distance);

				// Disparar usando el sistema del bloque
				ShootAtTarget();

				m_lastShootTime = currentTime;
				m_aimStartTime = currentTime;
			}
		}

		private bool HasCrossbowEquipped()
		{
			if (m_componentMiner.Inventory == null)
				return false;
			int activeSlot = m_componentMiner.Inventory.ActiveSlotIndex;
			int value = m_componentMiner.Inventory.GetSlotValue(activeSlot);
			int crossbowIndex = BlocksManager.GetBlockIndex("RepeatCrossbowBlock", false);
			return Terrain.ExtractContents(value) == crossbowIndex;
		}

		private void StartAiming()
		{
			Vector3 direction = Vector3.Normalize(m_target.ComponentCreatureModel.EyePosition - m_componentCreature.ComponentCreatureModel.EyePosition);
			Ray3 aimRay = new Ray3(m_componentCreature.ComponentCreatureModel.EyePosition, direction);
			m_componentMiner.Aim(aimRay, AimState.InProgress);
		}

		private void UpdateAimingModel()
		{
			if (m_isAiming && m_target != null)
			{
				Vector3 direction = Vector3.Normalize(m_target.ComponentCreatureModel.EyePosition - m_componentCreature.ComponentCreatureModel.EyePosition);
				Ray3 aimRay = new Ray3(m_componentCreature.ComponentCreatureModel.EyePosition, direction);
				m_componentMiner.Aim(aimRay, AimState.InProgress);
			}
		}

		/// <summary>
		/// Obtiene un tipo de flecha aleatorio, excluyendo la explosiva si la distancia es menor al mínimo seguro.
		/// </summary>
		private RepeatArrowBlock.ArrowType GetRandomArrowType(float distance)
		{
			Array allTypes = Enum.GetValues(typeof(RepeatArrowBlock.ArrowType));
			List<RepeatArrowBlock.ArrowType> availableTypes = new List<RepeatArrowBlock.ArrowType>();

			foreach (RepeatArrowBlock.ArrowType type in allTypes)
			{
				if (type == RepeatArrowBlock.ArrowType.ExplosiveArrow && distance < ExplosiveArrowRange.X)
					continue; // Excluir explosiva si estamos muy cerca
				availableTypes.Add(type);
			}

			int index = m_random.Int(0, availableTypes.Count - 1);
			return availableTypes[index];
		}

		private void PrepareCrossbowForShoot(float distanceToTarget)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return;

			int activeSlot = inventory.ActiveSlotIndex;
			int value = inventory.GetSlotValue(activeSlot);
			int data = Terrain.ExtractData(value);

			// Elegir tipo de flecha según la distancia
			RepeatArrowBlock.ArrowType selectedArrow = GetRandomArrowType(distanceToTarget);

			// Forzar ballesta tensada, con el tipo seleccionado y carga máxima (8)
			int newData = RepeatCrossbowBlock.SetDraw(data, 15);
			newData = RepeatCrossbowBlock.SetArrowType(newData, selectedArrow);
			int newValue = Terrain.MakeBlockValue(RepeatCrossbowBlock.Index, 0, newData);
			newValue = RepeatCrossbowBlock.SetLoadCount(newValue, 8);

			// Reemplazar en el inventario
			inventory.RemoveSlotItems(activeSlot, inventory.GetSlotCount(activeSlot));
			inventory.AddSlotItems(activeSlot, newValue, 1);
		}

		private void ShootAtTarget()
		{
			if (m_target == null) return;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = m_target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetPos - eyePos);
			Ray3 aimRay = new Ray3(eyePos, direction);

			// El bloque se encarga del disparo (SubsystemRepeatCrossbowBlockBehavior)
			m_componentMiner.Aim(aimRay, AimState.Completed);
		}

		private void StopAiming()
		{
			if (m_isAiming)
			{
				m_isAiming = false;
				Ray3 dummyRay = new Ray3(Vector3.Zero, Vector3.UnitX);
				m_componentMiner.Aim(dummyRay, AimState.Cancelled);
			}
		}
	}
}
