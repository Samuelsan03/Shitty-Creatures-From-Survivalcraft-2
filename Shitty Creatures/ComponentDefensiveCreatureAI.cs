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

				// Determinar aimTime y cooldown según el arma actual
				int activeContents = Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(m_componentMiner.Inventory.ActiveSlotIndex));
				float aimTime = (activeContents == CrossbowBlock.Index) ? CrossbowAimTime : MusketAimTime;

				m_aimTimer += dt;
				Ray3 aimRay = CalculateAimRay();

				if (m_aimTimer < aimTime)
				{
					m_componentMiner.Aim(aimRay, AimState.InProgress);
				}
				else
				{
					// Al completar el aim, la ballesta dispara normalmente y luego nosotros gestionamos la desaparición del proyectil
					m_componentMiner.Aim(aimRay, AimState.Completed);
					m_isAiming = false;
					m_cooldownTimer = (activeContents == CrossbowBlock.Index) ? CrossbowCooldown : MusketCooldown;

					// Si es ballesta, hacer desaparecer los virotes del suelo (ya que se acaban de crear)
					if (activeContents == CrossbowBlock.Index)
					{
						SubsystemProjectiles subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
						// Recorremos los proyectiles y marcamos los que sean flechas/vitores para desaparecer
						foreach (Projectile p in subsystemProjectiles.Projectiles)
						{
							if (p != null && Terrain.ExtractContents(p.Value) == ArrowBlock.Index)
							{
								p.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}
						}
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
					if (EnsureMusketEquipped())
					{
						m_isAiming = true;
						m_aimTimer = 0f;
						m_componentMiner.Aim(CalculateAimRay(), AimState.InProgress);
					}
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

		private bool SwitchToMeleeWeapon()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(slotValue);
				if (slotValue != 0 && contents != MusketBlock.Index && contents != CrossbowBlock.Index)
				{
					inventory.ActiveSlotIndex = i;
					return true;
				}
			}
			return false;
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

		private bool HasCrossbowInInventory()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;
			int crossbowIndex = CrossbowBlock.Index;
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == crossbowIndex)
					return true;
			}
			return false;
		}

		private bool EnsureCrossbowEquipped()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			int activeSlot = inventory.ActiveSlotIndex;
			int activeValue = inventory.GetSlotValue(activeSlot);
			int crossbowIndex = CrossbowBlock.Index;

			// Tipos de virote posibles
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
