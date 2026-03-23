using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentFlameThrowerShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Componentes necesarios
		private ComponentCreature m_componentCreature;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentMiner m_componentMiner;
		private SubsystemTime m_subsystemTime;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;

		// Configuración
		public float MaxDistance = 20f;
		public float AimTime = 0.5f;
		public float BurstTime = 2.0f;
		public float CooldownTime = 1.0f;

		// Tipo de munición por defecto (configurable desde el editor)
		public FlameBulletBlock.FlameBulletType DefaultAmmoType = FlameBulletBlock.FlameBulletType.Flame;

		// Estado
		private bool m_isAiming = false;
		private bool m_isFiring = false;
		private bool m_isReloading = false;
		private double m_stateStartTime;
		private Random m_random = new Random();
		private int m_flameThrowerBlockIndex = -1;
		private int m_flameBulletBlockIndex = -1;

		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 20f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 0.5f);
			BurstTime = valuesDictionary.GetValue<float>("BurstTime", 5.0f);
			CooldownTime = valuesDictionary.GetValue<float>("CooldownTime", 1.0f);

			int ammoTypeValue = valuesDictionary.GetValue<int>("DefaultAmmoType", 0);
			DefaultAmmoType = (FlameBulletBlock.FlameBulletType)Math.Clamp(ammoTypeValue, 0, 1);

			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);

			m_flameThrowerBlockIndex = BlocksManager.GetBlockIndex<FlameThrowerBlock>(false, false);
			m_flameBulletBlockIndex = BlocksManager.GetBlockIndex<FlameBulletBlock>(false, false);
		}

		public void Update(float dt)
		{
			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			if (m_componentChaseBehavior.Target == null)
			{
				Reset();
				return;
			}

			Vector3 shooterEye = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetEye = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;

			float distance = Vector3.Distance(shooterEye, targetEye);
			bool hasLOS = HasLineOfSight(shooterEye, targetEye);

			if (distance > MaxDistance || !hasLOS)
			{
				Reset();
				return;
			}

			// Siempre mirar al objetivo
			m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(targetEye);

			// Asegurar que el lanzallamas está en el slot activo
			EnsureFlameThrowerActive();

			// Si el lanzallamas está vacío, recargar manteniendo el tipo de munición
			if (IsFlameThrowerEmpty())
			{
				ReloadFlameThrower();
			}

			// Máquina de estados
			if (m_isAiming)
			{
				if (m_subsystemTime.GameTime - m_stateStartTime >= AimTime)
				{
					m_isAiming = false;
					StartFiring();
				}
				else
				{
					CallAim(AimState.InProgress);
				}
			}
			else if (m_isFiring)
			{
				CallAim(AimState.InProgress);

				if (m_subsystemTime.GameTime - m_stateStartTime >= BurstTime)
				{
					m_isFiring = false;
					StartReloading();
				}
			}
			else if (m_isReloading)
			{
				CallAim(AimState.Cancelled);

				if (m_subsystemTime.GameTime - m_stateStartTime >= CooldownTime)
				{
					m_isReloading = false;
					StartAiming();
				}
			}
			else
			{
				StartAiming();
			}
		}

		private bool HasLineOfSight(Vector3 from, Vector3 to)
		{
			TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(from, to, false, true, (value, distance) =>
			{
				int contents = Terrain.ExtractContents(value);
				Block block = BlocksManager.Blocks[contents];
				return block.IsCollidable_(value) && block.BlockIndex != 0;
			});

			if (terrainHit != null && terrainHit.Value.Distance < Vector3.Distance(from, to) - 0.1f)
				return false;

			BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(from, to, 0.2f, (body, distance) =>
			{
				if (body.Entity == m_componentCreature.Entity || body.Entity == m_componentChaseBehavior.Target.Entity)
					return false;
				if (body.IsChildOfBody(m_componentCreature.ComponentBody))
					return false;
				return true;
			});

			if (bodyHit != null && bodyHit.Value.Distance < Vector3.Distance(from, to) - 0.1f)
				return false;

			return true;
		}

		private void EnsureFlameThrowerActive()
		{
			if (m_componentMiner.Inventory == null)
				return;

			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					int blockIndex = Terrain.ExtractContents(slotValue);
					if (blockIndex == m_flameThrowerBlockIndex)
					{
						if (m_componentMiner.Inventory.ActiveSlotIndex != i)
							m_componentMiner.Inventory.ActiveSlotIndex = i;
						return;
					}
				}
			}
		}

		private bool IsFlameThrowerEmpty()
		{
			if (m_componentMiner.Inventory == null)
				return true;

			int activeSlot = m_componentMiner.Inventory.ActiveSlotIndex;
			if (activeSlot < 0)
				return true;

			int slotValue = m_componentMiner.Inventory.GetSlotValue(activeSlot);
			int contents = Terrain.ExtractContents(slotValue);

			if (contents != m_flameThrowerBlockIndex)
				return true;

			int data = Terrain.ExtractData(slotValue);
			FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);

			return loadState == FlameThrowerBlock.LoadState.Empty;
		}

		private FlameBulletBlock.FlameBulletType? GetCurrentBulletType()
		{
			if (m_componentMiner.Inventory == null)
				return null;

			int activeSlot = m_componentMiner.Inventory.ActiveSlotIndex;
			if (activeSlot < 0)
				return null;

			int slotValue = m_componentMiner.Inventory.GetSlotValue(activeSlot);
			int contents = Terrain.ExtractContents(slotValue);

			if (contents != m_flameThrowerBlockIndex)
				return null;

			int data = Terrain.ExtractData(slotValue);
			return FlameThrowerBlock.GetBulletType(data);
		}

		private void ReloadFlameThrower()
		{
			if (m_componentMiner.Inventory == null)
				return;

			int activeSlot = m_componentMiner.Inventory.ActiveSlotIndex;
			if (activeSlot < 0)
				return;

			int slotValue = m_componentMiner.Inventory.GetSlotValue(activeSlot);
			int contents = Terrain.ExtractContents(slotValue);

			if (contents != m_flameThrowerBlockIndex)
				return;

			// Obtener el tipo de munición actual del lanzallamas (si está vacío, usar el tipo por defecto)
			FlameBulletBlock.FlameBulletType? currentBulletType = GetCurrentBulletType();
			FlameBulletBlock.FlameBulletType bulletTypeToUse = currentBulletType ?? DefaultAmmoType;

			// Crear un lanzallamas cargado con el mismo tipo de munición que tenía
			int newValue = Terrain.MakeBlockValue(
				m_flameThrowerBlockIndex,
				0,
				FlameThrowerBlock.SetLoadState(
					FlameThrowerBlock.SetBulletType(
						FlameThrowerBlock.SetLoadCount(0, 15),
						bulletTypeToUse
					),
					FlameThrowerBlock.LoadState.Loaded
				)
			);

			// Reemplazar el item en el inventario
			m_componentMiner.Inventory.RemoveSlotItems(activeSlot, 1);
			m_componentMiner.Inventory.AddSlotItems(activeSlot, newValue, 1);
		}

		private void CallAim(AimState state)
		{
			EnsureFlameThrowerActive();

			Vector3 from = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 to = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(to - from);
			Ray3 ray = new Ray3(from, direction);

			m_componentMiner.Aim(ray, state);
		}

		private void StartAiming()
		{
			m_isAiming = true;
			m_isFiring = false;
			m_isReloading = false;
			m_stateStartTime = m_subsystemTime.GameTime;

			CallAim(AimState.InProgress);
		}

		private void StartFiring()
		{
			m_isAiming = false;
			m_isFiring = true;
			m_isReloading = false;
			m_stateStartTime = m_subsystemTime.GameTime;

			CallAim(AimState.InProgress);
		}

		private void StartReloading()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = true;
			m_stateStartTime = m_subsystemTime.GameTime;

			CallAim(AimState.Cancelled);
		}

		private void Reset()
		{
			if (m_isAiming || m_isFiring || m_isReloading)
			{
				CallAim(AimState.Cancelled);
			}

			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = false;
			m_componentCreature.ComponentCreatureModel.LookAtOrder = null;
		}
	}
}
