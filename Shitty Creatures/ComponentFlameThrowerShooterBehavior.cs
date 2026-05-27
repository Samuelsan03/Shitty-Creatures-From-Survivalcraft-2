using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Comportamiento que permite a una criatura usar un lanzallamas (FlameThrowerBlock)
	/// equipado en su inventario. Dispara usando ComponentMiner.Aim.
	/// Munición infinita con alternancia entre fuego y veneno cuando se agota un tipo.
	/// </summary>
	public class ComponentFlameThrowerShooterBehavior : ComponentBehavior, IUpdateable
	{
		public float FlameThrowerAimTime = 1.5f;
		public float FlameThrowerCooldown = 0.02f;
		public Vector2 Range = new Vector2(5f, 100f);

		private ComponentMiner m_componentMiner;
		private ComponentCreature m_componentCreature;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private SubsystemTime m_subsystemTime;
		private IInventory m_inventory;

		private float m_cooldownRemaining;
		private float m_aimStartTime;
		private bool m_isAiming;
		private ComponentCreature m_currentTarget;

		public override float ImportanceLevel => 100f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentChaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_inventory = Entity.FindComponent<ComponentInventory>();

			m_cooldownRemaining = 0f;
			m_isAiming = false;
		}

		public void Update(float dt)
		{
			if (m_cooldownRemaining > 0f)
				m_cooldownRemaining -= dt;

			ComponentCreature target = m_componentChaseBehavior?.Target;
			if (target == null || target.ComponentHealth.Health <= 0f)
			{
				CancelAim();
				return;
			}

			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);

			// Cambio a melee si hay y distancia es menor al mínimo
			if (distance < Range.X && EquipMeleeWeapon())
			{
				CancelAim();
				return;
			}

			if (distance > Range.Y)
			{
				CancelAim();
				return;
			}

			if (!EquipFlameThrower())
			{
				CancelAim();
				return;
			}

			// Asegurar que el lanzallamas tenga munición infinita y variada
			EnsureFlameThrowerHasAmmo();

			if (m_cooldownRemaining > 0f)
			{
				if (m_isAiming) CancelAim();
				return;
			}

			if (!m_isAiming)
			{
				StartAiming(target);
			}
			else
			{
				if (m_currentTarget != target)
				{
					CancelAim();
					StartAiming(target);
					return;
				}

				float aimTime = (float)m_subsystemTime.GameTime - m_aimStartTime;
				if (aimTime >= FlameThrowerAimTime)
				{
					ShootAt(target);
					CancelAim();
					m_cooldownRemaining = FlameThrowerCooldown;
				}
				else
				{
					Vector3 eyePos = m_componentCreatureModel.EyePosition;
					Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
					Vector3 direction = Vector3.Normalize(targetPos - eyePos);
					Ray3 aimRay = new Ray3(eyePos, direction);
					m_componentMiner.Aim(aimRay, AimState.InProgress);
				}
			}
		}

		private bool EquipFlameThrower()
		{
			if (m_inventory == null) return false;

			int activeSlot = m_inventory.ActiveSlotIndex;
			if (activeSlot >= 0)
			{
				int activeValue = m_inventory.GetSlotValue(activeSlot);
				if (Terrain.ExtractContents(activeValue) == FlameThrowerBlock.Index)
					return true;
			}

			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(value) == FlameThrowerBlock.Index)
				{
					m_inventory.ActiveSlotIndex = i;
					return true;
				}
			}

			return false;
		}

		private bool EquipMeleeWeapon()
		{
			if (m_inventory == null) return false;

			int activeSlot = m_inventory.ActiveSlotIndex;
			if (activeSlot >= 0)
			{
				int activeValue = m_inventory.GetSlotValue(activeSlot);
				if (activeValue != 0 && Terrain.ExtractContents(activeValue) != FlameThrowerBlock.Index)
					return true;
			}

			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				if (value != 0 && Terrain.ExtractContents(value) != FlameThrowerBlock.Index)
				{
					m_inventory.ActiveSlotIndex = i;
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Garantiza que el lanzallamas tenga munición. Si se acabó (loadCount == 0 o estado Empty),
		/// cambia al otro tipo de bala y recarga a 15.
		/// También asegura que el estado sea Loaded y que el tipo sea válido.
		/// </summary>
		private void EnsureFlameThrowerHasAmmo()
		{
			int slot = m_inventory.ActiveSlotIndex;
			int value = m_inventory.GetSlotValue(slot);
			int data = Terrain.ExtractData(value);
			int loadCount = FlameThrowerBlock.GetLoadCount(value);
			FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
			FlameBulletBlock.FlameBulletType? currentType = FlameThrowerBlock.GetBulletType(data);

			// Si no tiene balas o está vacío, cambiar tipo y recargar
			if (loadState != FlameThrowerBlock.LoadState.Loaded || loadCount <= 0)
			{
				// Determinar el nuevo tipo (si no hay tipo actual, elegir aleatoriamente entre fuego o veneno)
				FlameBulletBlock.FlameBulletType newType;
				if (currentType == null)
				{
					// Primera carga: elegir aleatoriamente fuego o veneno
					Random random = new Random();
					newType = random.Bool()
						? FlameBulletBlock.FlameBulletType.Flame
						: FlameBulletBlock.FlameBulletType.Poison;
				}
				else
				{
					// Alternar siempre al otro tipo
					newType = (currentType.Value == FlameBulletBlock.FlameBulletType.Flame)
						? FlameBulletBlock.FlameBulletType.Poison
						: FlameBulletBlock.FlameBulletType.Flame;
				}

				int newData = FlameThrowerBlock.SetLoadState(data, FlameThrowerBlock.LoadState.Loaded);
				newData = FlameThrowerBlock.SetBulletType(newData, newType);
				int newValue = Terrain.MakeBlockValue(FlameThrowerBlock.Index, 15, newData);
				m_inventory.RemoveSlotItems(slot, 1);
				m_inventory.AddSlotItems(slot, newValue, 1);
			}
		}

		private void StartAiming(ComponentCreature target)
		{
			m_isAiming = true;
			m_aimStartTime = (float)m_subsystemTime.GameTime;
			m_currentTarget = target;
		}

		private void ShootAt(ComponentCreature target)
		{
			Vector3 eyePos = m_componentCreatureModel.EyePosition;
			Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetPos - eyePos);
			Ray3 aimRay = new Ray3(eyePos, direction);

			m_componentMiner.Aim(aimRay, AimState.InProgress);
			m_componentMiner.Aim(aimRay, AimState.Completed);
		}

		private void CancelAim()
		{
			if (!m_isAiming) return;
			Ray3 dummyRay = new Ray3(m_componentCreatureModel.EyePosition, Vector3.Zero);
			m_componentMiner.Aim(dummyRay, AimState.Cancelled);
			m_isAiming = false;
			m_currentTarget = null;
		}
	}
}
