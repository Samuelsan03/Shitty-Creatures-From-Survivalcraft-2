using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentDefensiveCreatureAI : Component, IUpdateable
	{
		// Parámetro de carga
		public bool CanUseInventory;

		// Rangos de ataque para armas a distancia (NO expuestos en XML)
		// X = distancia mínima (ya no se usa para detener el disparo), Y = distancia máxima
		public Vector2 RangedAttackRange = new Vector2(5f, 100f);

		// Internos del mosquete (NO expuestos en XML)
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

			if (m_isAiming)
			{
				if (!IsTargetValidForRangedAttack())
				{
					CancelAiming();
					return;
				}
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
				if (IsTargetValidForRangedAttack() && EnsureMusketEquipped())
				{
					m_isAiming = true;
					m_aimTimer = 0f;
					m_componentMiner.Aim(CalculateAimRay(), AimState.InProgress);
				}
			}
		}

		// CORRECCIÓN: Solo se verifica la distancia máxima y la validez del objetivo.
		// La distancia mínima X ya no impide seguir disparando.
		private bool IsTargetValidForRangedAttack()
		{
			if (m_componentChase == null || m_componentChase.Target == null)
				return false;
			if (m_componentChase.Target.ComponentHealth.Health <= 0f)
				return false;
			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				m_componentChase.Target.ComponentBody.Position);
			// Solo el límite superior (Y); si está dentro del alcance máximo, se permite atacar.
			return distance <= RangedAttackRange.Y;
		}

		private Ray3 CalculateAimRay()
		{
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetEye = m_componentChase.Target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetEye - eyePos);
			return new Ray3(eyePos, direction);
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
