using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieAI : ComponentBehavior, IUpdateable
	{
		public static float MusketCooldown = 0.5f;
		public static float MusketAimTime = 1.5f;
		public Vector2 AttackRange = new Vector2(5f, 100f);
		private bool m_canUseInventory;
		private ComponentMiner m_componentMiner;
		private ComponentCreature m_componentCreature;
		private ComponentBody m_componentBody;
		private IInventory m_inventory;
		private ComponentZombieChaseBehavior m_chaseBehavior;
		private SubsystemTime m_subsystemTime;
		private ComponentCreatureModel m_creatureModel;
		private float m_aimTimer;
		private bool m_isAiming;
		private float m_cooldownTimer;

		public override float ImportanceLevel
		{
			get { return 100f; }
		}

		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_canUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			m_inventory = m_componentMiner.Inventory;
			m_creatureModel = m_componentCreature.ComponentCreatureModel;
			m_chaseBehavior = base.Entity.FindComponent<ComponentZombieChaseBehavior>();
			if (m_chaseBehavior == null)
			{
				Log.Warning("ComponentZombieAI: No se encontró ComponentZombieChaseBehavior en la entidad. IA desactivada.");
			}
		}

		public virtual void Update(float dt)
		{
			if (!m_canUseInventory || m_componentMiner == null || m_chaseBehavior == null || m_inventory == null)
			{
				return;
			}
			if (m_componentCreature.ComponentHealth.Health <= 0f)
			{
				StopAiming();
				return;
			}
			ComponentCreature target = m_chaseBehavior.m_target;
			if (target == null || target.ComponentHealth.Health <= 0f)
			{
				StopAiming();
				return;
			}
			int activeValue = m_inventory.GetSlotValue(m_inventory.ActiveSlotIndex);
			if (activeValue == 0)
			{
				return;
			}
			Block activeBlock = BlocksManager.Blocks[Terrain.ExtractContents(activeValue)];
			bool hasRangedWeapon = activeBlock is MusketBlock;
			bool hasMeleeWeapon = !hasRangedWeapon && activeBlock.GetMeleePower(activeValue) > 0f;
			if (!hasRangedWeapon && !hasMeleeWeapon)
			{
				EquipRangedWeapon();
				return;
			}
			float distance = GetMeleeDistanceToTarget(target.ComponentBody);
			if (distance <= AttackRange.X)
			{
				if (!hasMeleeWeapon)
				{
					if (TryEquipBestMeleeWeapon())
					{
						PerformMeleeAttack(target);
						return;
					}
				}
				else
				{
					PerformMeleeAttack(target);
					return;
				}
				UpdateRangedCombat(dt, target.ComponentBody.Position);
				PerformMeleeAttack(target);
				return;
			}
			if (hasMeleeWeapon && distance > AttackRange.X)
			{
				EquipRangedWeapon();
				return;
			}
			float distToTarget = Vector3.Distance(m_componentBody.Position, target.ComponentBody.Position);
			if (distToTarget > AttackRange.Y)
			{
				StopAiming();
				return;
			}
			UpdateRangedCombat(dt, target.ComponentBody.Position);
		}

		private float GetMeleeDistanceToTarget(ComponentBody targetBody)
		{
			BoundingBox myBox = m_componentBody.BoundingBox;
			BoundingBox targetBox = targetBody.BoundingBox;
			float dx = Math.Max(0f, Math.Max(myBox.Min.X - targetBox.Max.X, targetBox.Min.X - myBox.Max.X));
			float dy = Math.Max(0f, Math.Max(myBox.Min.Y - targetBox.Max.Y, targetBox.Min.Y - myBox.Max.Y));
			float dz = Math.Max(0f, Math.Max(myBox.Min.Z - targetBox.Max.Z, targetBox.Min.Z - myBox.Max.Z));
			return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
		}

		private void UpdateRangedCombat(float dt, Vector3 targetPos)
		{
			if (m_cooldownTimer > 0f)
			{
				m_cooldownTimer -= dt;
			}
			if (!m_isAiming && m_cooldownTimer <= 0f)
			{
				StartAiming();
			}
			if (m_isAiming)
			{
				m_aimTimer += dt;
				Vector3 eyePos = m_creatureModel.EyePosition;
				// Calcula el rayo cada frame, no solo al completar
				Vector3 dir = Vector3.Normalize(targetPos + new Vector3(0f, 1f, 0f) - eyePos);
				Ray3 aimRay = new Ray3(eyePos, dir);

				if (m_aimTimer < MusketAimTime)
				{
					// ⚠️ Llamada esencial: Inicia el apuntado en el subsistema (amartilla el percutor, etc.)
					m_componentMiner.Aim(aimRay, AimState.InProgress);

					// ⚠️ Ahora sobreescribe la pose para rotar solo el arma (manos quietas)
					if (m_creatureModel != null)
					{
						m_creatureModel.AimHandAngleOrder = 0f;                // brazo quieto
						m_creatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
						m_creatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
					}
				}
				else
				{
					// Disparo real
					m_componentMiner.Aim(aimRay, AimState.Completed);
					ReloadMusketInstantly();
					m_isAiming = false;
					m_cooldownTimer = MusketCooldown;
					ResetModelPose();
				}
			}
		}

		private void StopAiming()
		{
			if (m_isAiming)
			{
				// ⚠️ Cancela el apuntado en el subsistema (baja el percutor, libera el diccionario)
				Vector3 eyePos = m_creatureModel.EyePosition;
				Ray3 dummyRay = new Ray3(eyePos, Vector3.UnitZ);
				m_componentMiner.Aim(dummyRay, AimState.Cancelled);

				m_isAiming = false;
				m_aimTimer = 0f;
				ResetModelPose();
			}
		}

		private void StartAiming()
		{
			StopAiming();
			m_isAiming = true;
			m_aimTimer = 0f;
		}

		private void ResetModelPose()
		{
			if (m_creatureModel != null)
			{
				m_creatureModel.AimHandAngleOrder = 0f;
				m_creatureModel.InHandItemOffsetOrder = Vector3.Zero;
				m_creatureModel.InHandItemRotationOrder = Vector3.Zero;
			}
		}

		private void EquipRangedWeapon()
		{
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				int slotCount = m_inventory.GetSlotCount(i);
				if (slotCount > 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					if (block is MusketBlock)
					{
						m_inventory.ActiveSlotIndex = i;
						return;
					}
				}
			}
		}

		private bool TryEquipBestMeleeWeapon()
		{
			int bestSlot = -1;
			float bestPower = 0f;
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) == 0)
				{
					continue;
				}
				Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
				if (block is MusketBlock)
				{
					continue;
				}
				float power = block.GetMeleePower(slotValue);
				if (power > bestPower)
				{
					bestPower = power;
					bestSlot = i;
				}
			}
			if (bestSlot >= 0)
			{
				m_inventory.ActiveSlotIndex = bestSlot;
				return true;
			}
			return false;
		}

		private void PerformMeleeAttack(ComponentCreature target)
		{
			ComponentBody targetBody = target.ComponentBody;
			if (targetBody == null)
			{
				return;
			}
			if (GetMeleeDistanceToTarget(targetBody) > AttackRange.X)
			{
				return;
			}
			Vector3 myPos = m_componentBody.Position;
			BoundingBox targetBox = targetBody.BoundingBox;
			Vector3 hitPoint = new Vector3(
				Math.Clamp(myPos.X, targetBox.Min.X, targetBox.Max.X),
				Math.Clamp(myPos.Y, targetBox.Min.Y, targetBox.Max.Y),
				Math.Clamp(myPos.Z, targetBox.Min.Z, targetBox.Max.Z)
			);
			Vector3 hitDir = Vector3.Normalize(hitPoint - myPos);
			m_componentMiner.Hit(targetBody, hitPoint, hitDir);
		}

		private void ReloadMusketInstantly()
		{
			int activeSlot = m_inventory.ActiveSlotIndex;
			int currentValue = m_inventory.GetSlotValue(activeSlot);
			Block activeBlock = BlocksManager.Blocks[Terrain.ExtractContents(currentValue)];
			if (!(activeBlock is MusketBlock))
			{
				return;
			}
			m_inventory.RemoveSlotItems(activeSlot, 1);
			int bulletType = new Random().Int(0, 2);
			int data = 0;
			data = MusketBlock.SetHammerState(data, false);
			data = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
			data = MusketBlock.SetBulletType(data, (BulletBlock.BulletType)bulletType);
			int musketIndex = activeBlock.BlockIndex;
			int loadedValue = Terrain.MakeBlockValue(musketIndex, 0, data);
			m_inventory.AddSlotItems(activeSlot, loadedValue, 1);
		}
	}
}
