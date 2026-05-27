using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	///     Comportamiento: dispara mosquete a distancia.
	///     El cuerpo a cuerpo lo maneja ComponentChaseBehavior original.
	/// </summary>
	public class ComponentMusketShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Parámetros públicos (NO se cargan desde diccionario)
		public float MusketAimTime = 1.5f;
		public float MusketCooldown = 0.02f;
		public Vector2 RangeDistance = new Vector2(5f, 100f);

		// Dependencias
		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private IInventory m_inventory;

		// Estado del mosquete
		private double m_nextRangedTime;
		private double m_aimStartTime;
		private bool m_isAiming;

		public override float ImportanceLevel => (Target != null) ? 200f : 0f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		private ComponentCreature Target
		{
			get
			{
				var chase = Entity.FindComponent<ComponentChaseBehavior>();
				return (chase != null && chase.Target != null) ? chase.Target : null;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_inventory = m_componentMiner?.Inventory;
		}

		public virtual void Update(float dt)
		{
			// Validaciones
			if (m_componentMiner == null || m_inventory == null || m_componentCreature.ComponentHealth.Health <= 0f)
			{
				CancelAim();
				return;
			}

			var target = Target;
			if (target == null || target.Entity == Entity || !target.Entity.IsAddedToProject || target.ComponentHealth.Health <= 0f)
			{
				CancelAim();
				return;
			}

			float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);
			double now = m_subsystemTime.GameTime;

			// Fuera de rango máximo
			if (dist > RangeDistance.Y)
			{
				CancelAim();
				return;
			}

			// Modo cuerpo a cuerpo (dist < RangeDistance.X) - SOLO cambiar arma, NO atacar
			if (dist < RangeDistance.X)
			{
				CancelAim();
				TryEquipMeleeWeapon();
			}
			// Modo a distancia
			else
			{
				TryEquipMusket();
				PerformRangedAttack(target, now);
			}
		}

		private void PerformRangedAttack(ComponentCreature target, double now)
		{
			if (!EnsureMusketLoaded()) return;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = target.ComponentBody.BoundingBox.Center();
			Vector3 dir = Vector3.Normalize(targetPos - eyePos);
			Ray3 aimRay = new Ray3(eyePos, dir);

			if (!m_isAiming)
			{
				if (now < m_nextRangedTime) return;
				m_componentMiner.Aim(aimRay, AimState.InProgress);
				m_isAiming = true;
				m_aimStartTime = now;
			}
			else
			{
				m_componentMiner.Aim(aimRay, AimState.InProgress);
				if (now - m_aimStartTime >= MusketAimTime)
				{
					m_componentMiner.Aim(aimRay, AimState.Completed);
					m_nextRangedTime = now + MusketCooldown;
					m_isAiming = false;
				}
			}
		}

		private void TryEquipMeleeWeapon()
		{
			int current = m_inventory.ActiveSlotIndex;
			if (current >= 0)
			{
				int val = m_inventory.GetSlotValue(current);
				int content = Terrain.ExtractContents(val);
				if (content != BlocksManager.GetBlockIndex<MusketBlock>(false, false) &&
					BlocksManager.Blocks[content].GetMeleePower(val) > 0f)
					return;
			}

			int musketIdx = BlocksManager.GetBlockIndex<MusketBlock>(false, false);
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int v = m_inventory.GetSlotValue(i);
				int c = Terrain.ExtractContents(v);
				if (c != musketIdx && BlocksManager.Blocks[c].GetMeleePower(v) > 0f)
				{
					m_inventory.ActiveSlotIndex = i;
					return;
				}
			}
		}

		private void TryEquipMusket()
		{
			int current = m_inventory.ActiveSlotIndex;
			if (current >= 0)
			{
				int val = m_inventory.GetSlotValue(current);
				if (Terrain.ExtractContents(val) == BlocksManager.GetBlockIndex<MusketBlock>(false, false))
					return;
			}

			int musketIdx = BlocksManager.GetBlockIndex<MusketBlock>(false, false);
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(m_inventory.GetSlotValue(i)) == musketIdx)
				{
					m_inventory.ActiveSlotIndex = i;
					return;
				}
			}
		}

		private bool EnsureMusketLoaded()
		{
			int slot = m_inventory.ActiveSlotIndex;
			if (slot < 0) return false;
			int value = m_inventory.GetSlotValue(slot);
			if (Terrain.ExtractContents(value) != BlocksManager.GetBlockIndex<MusketBlock>(false, false))
				return false;

			int data = Terrain.ExtractData(value);
			if (MusketBlock.GetLoadState(data) != MusketBlock.LoadState.Loaded)
			{
				Random random = new Random();
				BulletBlock.BulletType bulletType = (BulletBlock.BulletType)random.Int(0, 2);
				if (random.Bool(0.05f))
				{
					bulletType = BulletBlock.BulletType.MusketBall | BulletBlock.BulletType.Buckshot | BulletBlock.BulletType.BuckshotBall;
				}
				data = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
				data = MusketBlock.SetBulletType(data, bulletType);
				int newValue = Terrain.MakeBlockValue(Terrain.ExtractContents(value), 0, data);
				m_inventory.RemoveSlotItems(slot, m_inventory.GetSlotCount(slot));
				m_inventory.AddSlotItems(slot, newValue, 1);
			}
			return true;
		}

		private void CancelAim()
		{
			if (m_isAiming)
			{
				m_componentMiner.Aim(new Ray3(Vector3.Zero, Vector3.Zero), AimState.Cancelled);
				m_isAiming = false;
			}
		}
	}
}
