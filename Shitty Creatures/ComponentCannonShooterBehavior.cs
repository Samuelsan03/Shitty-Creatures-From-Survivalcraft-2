using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCannonShooterBehavior : Component, IUpdateable
	{
		// Configuración de combate
		public Vector2 AttackRange = new Vector2(5f, 100f);
		public float AimTime = 1.5f;
		public float CooldownTime = 0.02f;

		// Componentes
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private IInventory m_inventory;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private int m_cannonBlockIndex = -1;

		// Estado de combate
		private bool m_isAiming;
		private float m_aimTimer;
		private float m_cooldownTimer;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);

			if (m_componentMiner != null)
			{
				m_inventory = m_componentMiner.Inventory;
			}

			m_cannonBlockIndex = BlocksManager.GetBlockIndex("CannonBlock", false);
		}

		public void Update(float dt)
		{
			if (AchievementsManager.IsCelebrationActive) return;
			if (m_componentCreature == null || m_componentCreature.ComponentHealth.Health <= 0f)
			{
				StopAiming();
				return;
			}

			if (m_inventory == null || m_cannonBlockIndex < 0) return;

			ComponentCreature target = m_componentChaseBehavior?.Target;
			if (target == null || target.ComponentHealth.Health <= 0f)
			{
				StopAiming();
				return;
			}

			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				target.ComponentBody.Position
			);

			// Distancia máxima: si se aleja, cancelar apuntado
			if (distance > AttackRange.Y)
			{
				StopAiming();
				return;
			}

			// Distancia mínima: si tenemos armas a cuerpo, cambiar a melee
			if (distance <= AttackRange.X)
			{
				if (HasMeleeWeapon() && EquipMeleeWeapon())
				{
					StopAiming();
					return; // Cambió a melee, no continúa con el cañón
				}
			}

			// Si no tenemos cañón en el inventario, no hacer nada
			if (!HasCannon())
			{
				StopAiming();
				return;
			}

			EnsureCannonEquipped();
			EnsureCannonLoaded();

			// Sistema de enfriamiento (Cooldown)
			if (m_cooldownTimer > 0f)
			{
				m_cooldownTimer -= dt;
				if (m_cooldownTimer < 0f) m_cooldownTimer = 0f;
				return;
			}

			// Iniciar apuntado si no está activo
			if (!m_isAiming)
			{
				m_isAiming = true;
				m_aimTimer = 0f;
			}

			// Lógica de apuntado usando Miner.Aim (disparo nativo)
			if (m_isAiming)
			{
				m_aimTimer += dt;
				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
				Vector3 dir = Vector3.Normalize(targetPos - eyePos);
				Ray3 aimRay = new Ray3(eyePos, dir);

				if (m_aimTimer < AimTime)
				{
					// Progreso de apuntado (maneja animaciones nativas)
					m_componentMiner.Aim(aimRay, AimState.InProgress);
				}
				else
				{
					// Disparo completado (maneja audio, proyectil y recarga nativa)
					m_componentMiner.Aim(aimRay, AimState.Completed);
					m_isAiming = false;
					m_cooldownTimer = CooldownTime;
				}
			}
		}

		private void StopAiming()
		{
			if (m_isAiming)
			{
				m_isAiming = false;
				m_aimTimer = 0f;
				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				// Cancelar apuntado nativo
				m_componentMiner.Aim(new Ray3(eyePos, Vector3.UnitZ), AimState.Cancelled);
			}
		}

		private bool HasCannon()
		{
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) > 0 && Terrain.ExtractContents(slotValue) == m_cannonBlockIndex)
					return true;
			}
			return false;
		}

		private void EnsureCannonEquipped()
		{
			int activeSlot = m_inventory.ActiveSlotIndex;
			int activeValue = m_inventory.GetSlotValue(activeSlot);
			if (Terrain.ExtractContents(activeValue) == m_cannonBlockIndex) return;

			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) > 0 && Terrain.ExtractContents(slotValue) == m_cannonBlockIndex)
				{
					m_inventory.ActiveSlotIndex = i;
					return;
				}
			}
		}

		private void EnsureCannonLoaded()
		{
			int activeSlot = m_inventory.ActiveSlotIndex;
			int slotValue = m_inventory.GetSlotValue(activeSlot);
			if (slotValue == 0 || Terrain.ExtractContents(slotValue) != m_cannonBlockIndex) return;

			int data = Terrain.ExtractData(slotValue);
			CannonBlock.LoadState loadState = CannonBlock.GetLoadState(data);
			bool hammerState = CannonBlock.GetHammerState(data);

			if (loadState != CannonBlock.LoadState.Loaded || !hammerState)
			{
				m_inventory.RemoveSlotItems(activeSlot, 1);
				data = CannonBlock.SetLoadState(data, CannonBlock.LoadState.Loaded);
				data = CannonBlock.SetCannonBallType(data, CannonBallBlock.CannonBallType.CannonBall);
				data = CannonBlock.SetHammerState(data, true);
				m_inventory.AddSlotItems(activeSlot, Terrain.MakeBlockValue(m_cannonBlockIndex, 0, data), 1);
			}
		}

		private bool HasMeleeWeapon()
		{
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) == 0) continue;
				int blockIndex = Terrain.ExtractContents(slotValue);
				Block block = BlocksManager.Blocks[blockIndex];
				if (blockIndex == m_cannonBlockIndex) continue;

				if (block.GetMeleePower(slotValue) > 0f)
					return true;
			}
			return false;
		}

		private bool EquipMeleeWeapon()
		{
			int bestSlot = -1;
			float bestPower = 0f;
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) == 0) continue;
				int blockIndex = Terrain.ExtractContents(slotValue);
				Block block = BlocksManager.Blocks[blockIndex];
				if (blockIndex == m_cannonBlockIndex) continue;

				float power = block.GetMeleePower(slotValue);
				if (power > bestPower)
				{
					bestPower = power;
					bestSlot = i;
				}
			}
			if (bestSlot >= 0)
			{
				if (m_inventory.ActiveSlotIndex != bestSlot)
				{
					m_inventory.ActiveSlotIndex = bestSlot;
					StopAiming();
				}
				return true;
			}
			return false;
		}
	}
}
