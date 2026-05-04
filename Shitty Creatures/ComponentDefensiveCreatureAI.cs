using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentDefensiveCreatureAI : Component, IUpdateable
	{
		public bool CanUseInventory;
		public Vector2 RangedAttackRange = new Vector2(5f, 100f);

		private float MusketAimTime = 1.5f;
		private float MusketCooldown = 0.5f;

		private SubsystemTime m_subsystemTime;
		private ComponentMiner m_componentMiner;
		private ComponentNewChaseBehavior m_componentChase;
		private ComponentCreature m_componentCreature;
		private Random m_random = new Random();

		private bool m_isAiming;
		private float m_aimTimer;
		private float m_cooldownTimer;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentChase = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);
		}

		public void Update(float dt)
		{
			if (!CanUseInventory || m_componentMiner == null || m_componentCreature == null)
				return;

			float distance = GetTargetDistance();
			bool hasTarget = IsTargetValidForRangedAttack();

			if (m_isAiming)
			{
				// Si el objetivo muere, cancelar
				if (!hasTarget)
				{
					CancelAiming();
					return;
				}

				// Si está cerca y tenemos arma cuerpo a cuerpo, cambiar a ella y cancelar apuntado
				if (distance <= RangedAttackRange.X && SwitchToMeleeWeapon())
				{
					CancelAiming();
					return;
				}

				// Si no hay arma cuerpo a cuerpo o está lejos, continuar apuntando
				m_aimTimer += dt;
				Ray3 aimRay = CalculateAimRay();
				if (m_aimTimer < MusketAimTime)
				{
					m_componentMiner.Aim(aimRay, AimState.InProgress);
				}
				else
				{
					m_componentMiner.Aim(aimRay, AimState.Completed);
					m_isAiming = false;
					m_cooldownTimer = MusketCooldown;
				}
			}
			else
			{
				if (m_cooldownTimer > 0f)
				{
					m_cooldownTimer -= dt;
					return;
				}

				if (!hasTarget)
					return;

				// Si está cerca, intentar cambiar a cuerpo a cuerpo
				if (distance <= RangedAttackRange.X)
				{
					// Solo si se pudo cambiar a un arma melee dejamos de disparar
					if (SwitchToMeleeWeapon())
						return;
					// Si no, seguimos con el mosquete (caemos en el siguiente bloque)
				}

				// Intentar disparar con mosquete (incluso si está cerca y no hay arma cuerpo a cuerpo)
				if (distance <= RangedAttackRange.Y && EnsureMusketEquipped())
				{
					m_isAiming = true;
					m_aimTimer = 0f;
					m_componentMiner.Aim(CalculateAimRay(), AimState.InProgress);
				}
			}
		}

		private float GetTargetDistance()
		{
			if (m_componentChase == null || m_componentChase.Target == null)
				return float.MaxValue;
			return Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				m_componentChase.Target.ComponentBody.Position);
		}

		private bool IsTargetValidForRangedAttack()
		{
			if (m_componentChase == null || m_componentChase.Target == null)
				return false;
			return m_componentChase.Target.ComponentHealth.Health > 0f;
		}

		private Ray3 CalculateAimRay()
		{
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetEye = m_componentChase.Target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetEye - eyePos);
			return new Ray3(eyePos, direction);
		}

		// Devuelve true si se cambió a un arma cuerpo a cuerpo, false en caso contrario
		private bool SwitchToMeleeWeapon()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			int currentContents = Terrain.ExtractContents(inventory.GetSlotValue(inventory.ActiveSlotIndex));
			// Buscar cualquier objeto que no sea mosquete (priorizar el primer slot con algo)
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (slotValue != 0 && Terrain.ExtractContents(slotValue) != MusketBlock.Index)
				{
					inventory.ActiveSlotIndex = i;
					return true; // Sí se cambió
				}
			}
			return false; // No hay arma cuerpo a cuerpo
		}

		private bool EnsureMusketEquipped()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			int activeSlot = inventory.ActiveSlotIndex;
			int activeValue = inventory.GetSlotValue(activeSlot);
			int musketIndex = MusketBlock.Index;

			if (Terrain.ExtractContents(activeValue) == musketIndex)
			{
				int data = Terrain.ExtractData(activeValue);
				MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
				if (loadState != MusketBlock.LoadState.Loaded)
				{
					BulletBlock.BulletType randomBullet = (BulletBlock.BulletType)m_random.Int(0, 2);
					int newData = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
					newData = MusketBlock.SetBulletType(newData, randomBullet);
					int newValue = Terrain.MakeBlockValue(musketIndex, 0, newData);
					inventory.RemoveSlotItems(activeSlot, 1);
					inventory.AddSlotItems(activeSlot, newValue, 1);
				}
				return true;
			}

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == musketIndex)
				{
					inventory.ActiveSlotIndex = i;
					return EnsureMusketEquipped();
				}
			}
			return false;
		}

		private void CancelAiming()
		{
			if (m_isAiming)
			{
				m_componentMiner.Aim(CalculateAimRay(), AimState.Cancelled);
				m_isAiming = false;
				m_aimTimer = 0f;
			}
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap) { }

		public override void Dispose()
		{
			if (m_isAiming) CancelAiming();
			base.Dispose();
		}
	}
}
