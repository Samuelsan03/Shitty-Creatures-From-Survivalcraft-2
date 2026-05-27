using System;
using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Componente que permite a una criatura usar el Arco como arma a distancia,
	/// con munición ilimitada, tiempo de apuntado configurable y tipo de flecha aleatorio.
	/// El arco se tensa inmediatamente al empezar a apuntar.
	/// </summary>
	public class ComponentBowShooterBehavior : ComponentBehavior, IUpdateable
	{
		public float BowCooldown = 0.02f;
		public float BowAimTime = 1.5f;
		public Vector2 BowRange = new Vector2(5f, 100f);

		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles; // NUEVO
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentChaseBehavior m_componentChase;

		private ComponentCreature m_target;
		private double m_aimStartTime;
		private double m_lastShootTime;
		private bool m_isAiming;

		private Random m_random = new Random();

		private ArrowBlock.ArrowType m_currentArrowType; // NUEVO: tipo de flecha actual

		public override float ImportanceLevel => 200f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true); // NUEVO
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentChase = Entity.FindComponent<ComponentChaseBehavior>();
		}

		public void Update(float dt)
		{
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

			if (!HasBowEquipped())
			{
				StopAiming();
				return;
			}

			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, m_target.ComponentBody.Position);

			if (distance > BowRange.Y)
			{
				StopAiming();
				return;
			}

			double currentTime = m_subsystemTime.GameTime;

			if (!m_isAiming)
			{
				// Al empezar a apuntar, tensamos el arco inmediatamente y asignamos flecha
				PrepareBowForShoot();
				StartAiming();
				m_aimStartTime = currentTime;
				m_isAiming = true;
			}

			UpdateAimingModel();

			if (currentTime - m_aimStartTime >= BowAimTime &&
				currentTime - m_lastShootTime >= BowCooldown)
			{
				// Ya está tenso, solo disparar
				ShootAtTarget();

				m_lastShootTime = currentTime;
				m_aimStartTime = currentTime;

				// Volver a tensar el arco para el siguiente disparo
				PrepareBowForShoot();
			}
		}

		private bool HasBowEquipped()
		{
			if (m_componentMiner.Inventory == null)
				return false;
			int activeSlot = m_componentMiner.Inventory.ActiveSlotIndex;
			int value = m_componentMiner.Inventory.GetSlotValue(activeSlot);
			return Terrain.ExtractContents(value) == BowBlock.Index;
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

		private ArrowBlock.ArrowType GetRandomArrowType()
		{
			ArrowBlock.ArrowType[] supportedTypes = new ArrowBlock.ArrowType[]
			{
				ArrowBlock.ArrowType.WoodenArrow,
				ArrowBlock.ArrowType.StoneArrow,
				ArrowBlock.ArrowType.CopperArrow,
				ArrowBlock.ArrowType.IronArrow,
				ArrowBlock.ArrowType.DiamondArrow,
				ArrowBlock.ArrowType.FireArrow
			};
			int index = m_random.Int(0, supportedTypes.Length - 1);
			return supportedTypes[index];
		}

		private void PrepareBowForShoot()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return;

			int activeSlot = inventory.ActiveSlotIndex;
			int value = inventory.GetSlotValue(activeSlot);
			int data = Terrain.ExtractData(value);

			// Elegir tipo de flecha aleatorio (SOLO UNA VEZ por disparo)
			m_currentArrowType = GetRandomArrowType();

			// Configurar tensión máxima (15) y tipo de flecha
			int newData = BowBlock.SetDraw(data, 15);
			newData = BowBlock.SetArrowType(newData, m_currentArrowType);
			int newValue = Terrain.MakeBlockValue(BowBlock.Index, 0, newData);

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

			// Hacer que la flecha desaparezca al caer al suelo
			Action<Projectile> onProjectileAdded = null;
			onProjectileAdded = (Projectile p) =>
			{
				if (p.Owner == m_componentCreature && Terrain.ExtractContents(p.Value) == ArrowBlock.Index)
				{
					p.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				}
				if (m_subsystemProjectiles != null)
				{
					m_subsystemProjectiles.ProjectileAdded -= onProjectileAdded;
				}
			};
			if (m_subsystemProjectiles != null)
			{
				m_subsystemProjectiles.ProjectileAdded += onProjectileAdded;
			}

			// El SubsystemBowBlockBehavior disparará con el draw=15 que ya tenemos
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
