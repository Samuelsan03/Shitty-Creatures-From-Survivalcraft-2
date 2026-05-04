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

		// Tiempos del mosquete
		private float MusketAimTime = 1.5f;
		private float MusketCooldown = 0.5f;

		// Tiempos de la ballesta
		private float CrossbowAimTime = 1.5f;
		private float CrossbowCooldown = 0.5f;

		// Tiempos del arco
		private float BowAimTime = 1.5f;
		private float BowCooldown = 0.5f;

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
				if (!hasTarget)
				{
					CancelAiming();
					return;
				}

				if (distance <= RangedAttackRange.X && SwitchToMeleeWeapon())
				{
					CancelAiming();
					return;
				}

				int activeContents = Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(m_componentMiner.Inventory.ActiveSlotIndex));
				float aimTime = GetAimTime(activeContents);
				m_aimTimer += dt;
				Ray3 aimRay = CalculateAimRay();

				if (m_aimTimer < aimTime)
				{
					m_componentMiner.Aim(aimRay, AimState.InProgress);
				}
				else
				{
					m_componentMiner.Aim(aimRay, AimState.Completed);
					m_isAiming = false;
					m_cooldownTimer = GetCooldown(activeContents);

					// Disparo realizado: gestionar desaparición de proyectiles y recarga inmediata
					if (activeContents == CrossbowBlock.Index || activeContents == BowBlock.Index)
					{
						// Hacer desaparecer todos los proyectiles de tipo flecha/virote
						SubsystemProjectiles subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
						foreach (Projectile p in subsystemProjectiles.Projectiles)
						{
							if (p != null && Terrain.ExtractContents(p.Value) == ArrowBlock.Index)
							{
								p.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}
						}
						if (activeContents == BowBlock.Index)
							EnsureBowEquipped();
						else
							EnsureCrossbowEquipped();
					}
					else if (activeContents == MusketBlock.Index)
					{
						EnsureMusketEquipped(); // recargar mágicamente
					}
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

				if (distance <= RangedAttackRange.X)
				{
					if (SwitchToMeleeWeapon())
						return;
				}

				if (distance <= RangedAttackRange.Y)
				{
					if (HasCrossbowInInventory())
					{
						if (EnsureCrossbowEquipped())
						{
							m_isAiming = true;
							m_aimTimer = 0f;
							m_componentMiner.Aim(CalculateAimRay(), AimState.InProgress);
							return;
						}
					}
					if (HasBowInInventory())
					{
						if (EnsureBowEquipped())
						{
							m_isAiming = true;
							m_aimTimer = 0f;
							m_componentMiner.Aim(CalculateAimRay(), AimState.InProgress);
							return;
						}
					}
					if (EnsureMusketEquipped())
					{
						m_isAiming = true;
						m_aimTimer = 0f;
						m_componentMiner.Aim(CalculateAimRay(), AimState.InProgress);
					}
				}
			}
		}

		private float GetAimTime(int contents)
		{
			if (contents == CrossbowBlock.Index) return CrossbowAimTime;
			if (contents == BowBlock.Index) return BowAimTime;
			return MusketAimTime;
		}

		private float GetCooldown(int contents)
		{
			if (contents == CrossbowBlock.Index) return CrossbowCooldown;
			if (contents == BowBlock.Index) return BowCooldown;
			return MusketCooldown;
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

		private bool SwitchToMeleeWeapon()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(slotValue);
				if (slotValue != 0 && contents != MusketBlock.Index && contents != CrossbowBlock.Index && contents != BowBlock.Index)
				{
					inventory.ActiveSlotIndex = i;
					return true;
				}
			}
			return false;
		}

		// ========== MOSQUETE ==========
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

		// ========== BALLESTA ==========
		private bool HasCrossbowInInventory()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;
			for (int i = 0; i < inventory.SlotsCount; i++)
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == CrossbowBlock.Index) return true;
			return false;
		}

		private bool EnsureCrossbowEquipped()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			int activeSlot = inventory.ActiveSlotIndex;
			int activeValue = inventory.GetSlotValue(activeSlot);
			int crossbowIndex = CrossbowBlock.Index;

			ArrowBlock.ArrowType[] boltTypes = {
				ArrowBlock.ArrowType.IronBolt,
				ArrowBlock.ArrowType.DiamondBolt,
				ArrowBlock.ArrowType.ExplosiveBolt
			};

			if (Terrain.ExtractContents(activeValue) == crossbowIndex)
			{
				int data = Terrain.ExtractData(activeValue);
				if (CrossbowBlock.GetDraw(data) != 15 || CrossbowBlock.GetArrowType(data) == null)
				{
					ArrowBlock.ArrowType randomBolt = boltTypes[m_random.Int(0, boltTypes.Length)];
					int newData = CrossbowBlock.SetDraw(data, 15);
					newData = CrossbowBlock.SetArrowType(newData, randomBolt);
					int newValue = Terrain.MakeBlockValue(crossbowIndex, 0, newData);
					inventory.RemoveSlotItems(activeSlot, 1);
					inventory.AddSlotItems(activeSlot, newValue, 1);
				}
				return true;
			}

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == crossbowIndex)
				{
					inventory.ActiveSlotIndex = i;
					return EnsureCrossbowEquipped();
				}
			}
			return false;
		}

		// ========== ARCO ==========
		private bool HasBowInInventory()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;
			for (int i = 0; i < inventory.SlotsCount; i++)
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == BowBlock.Index) return true;
			return false;
		}

		private bool EnsureBowEquipped()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			int activeSlot = inventory.ActiveSlotIndex;
			int activeValue = inventory.GetSlotValue(activeSlot);
			int bowIndex = BowBlock.Index;

			// Flechas disponibles para el arco
			ArrowBlock.ArrowType[] arrowTypes = {
				ArrowBlock.ArrowType.WoodenArrow,
				ArrowBlock.ArrowType.StoneArrow,
				ArrowBlock.ArrowType.CopperArrow,
				ArrowBlock.ArrowType.IronArrow,
				ArrowBlock.ArrowType.DiamondArrow,
				ArrowBlock.ArrowType.FireArrow
			};

			if (Terrain.ExtractContents(activeValue) == bowIndex)
			{
				int data = Terrain.ExtractData(activeValue);
				// El arco debe estar completamente tensado (draw = 15) y tener una flecha
				if (BowBlock.GetDraw(data) != 15 || BowBlock.GetArrowType(data) == null)
				{
					ArrowBlock.ArrowType randomArrow = arrowTypes[m_random.Int(0, arrowTypes.Length)];
					int newData = BowBlock.SetDraw(data, 15);
					newData = BowBlock.SetArrowType(newData, randomArrow);
					int newValue = Terrain.MakeBlockValue(bowIndex, 0, newData);
					inventory.RemoveSlotItems(activeSlot, 1);
					inventory.AddSlotItems(activeSlot, newValue, 1);
				}
				return true;
			}

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == bowIndex)
				{
					inventory.ActiveSlotIndex = i;
					return EnsureBowEquipped();
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
				m_cooldownTimer = 0f;
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
