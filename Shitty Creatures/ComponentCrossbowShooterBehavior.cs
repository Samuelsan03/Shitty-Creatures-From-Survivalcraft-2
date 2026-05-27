using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Hace que una criatura dispare una ballesta (crossbow) contra su objetivo de persecución.
	/// Variedad de virote aleatorio entre los tipos soportados (IronBolt, DiamondBolt, ExplosiveBolt).
	/// Los virote desaparecen al impactar contra el suelo (no se convierten en pickable).
	/// </summary>
	public class ComponentCrossbowShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Tiempo que tarda la criatura en apuntar antes de disparar (segundos)
		public float CrossbowAimTime = 1.5f;

		// Tiempo de espera entre disparos (segundos)
		public float CrossbowCooldown = 0.02f;

		// Rango de disparo normal: X = distancia máxima, Y = distancia mínima
		public Vector2 Range = new Vector2(100f, 5f);

		// Rango para usar virote explosivo: Y = distancia mínima para usar explosivo, X = distancia máxima (debe ser <= Range.X)
		public Vector2 RangeExplosive = new Vector2(100f, 20f);

		// Referencias
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private Random m_random = new Random();

		// Estado interno
		private double m_aimStartTime;
		private double m_lastShotTime;
		private bool m_isAiming;
		private bool m_hasCrossbow;
		private float m_currentDistance;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => 0f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentChaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
		}

		public void Update(float dt)
		{
			if (Suppressed)
			{
				CancelAim();
				m_isAiming = false;
				return;
			}

			ComponentCreature target = m_componentChaseBehavior?.Target;

			m_hasCrossbow = HasCrossbowEquipped();

			if (!m_hasCrossbow || target == null || target.ComponentHealth.Health <= 0f)
			{
				CancelAim();
				m_isAiming = false;
				return;
			}

			m_currentDistance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				target.ComponentBody.Position);

			// Solo verificar distancia máxima, ignorar distancia mínima
			bool inRange = m_currentDistance <= Range.X;

			double currentTime = m_subsystemTime.GameTime;
			double timeSinceLastShot = currentTime - m_lastShotTime;

			if (inRange && timeSinceLastShot >= CrossbowCooldown)
			{
				if (!m_isAiming)
				{
					StartAiming(target);
					m_isAiming = true;
					m_aimStartTime = currentTime;
				}
				else
				{
					ContinueAiming(target);

					if (currentTime - m_aimStartTime >= CrossbowAimTime)
					{
						Shoot(target);
						m_lastShotTime = currentTime;
						m_isAiming = false;
					}
				}
			}
			else if (!inRange && m_isAiming)
			{
				CancelAim();
				m_isAiming = false;
			}
		}

		private bool HasCrossbowEquipped()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			int slotValue = inventory.GetSlotValue(inventory.ActiveSlotIndex);
			int contents = Terrain.ExtractContents(slotValue);

			int crossbowIndex = BlocksManager.GetBlockIndex<CrossbowBlock>(false, false);
			return contents == crossbowIndex;
		}

		private void StartAiming(ComponentCreature target)
		{
			// Cargar la ballesta: tensado máximo y un virote según la distancia
			LoadCrossbowWithRandomBolt(m_currentDistance);

			Vector3 eyePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = target.ComponentBody.BoundingBox.Center();
			Vector3 direction = Vector3.Normalize(targetCenter - eyePosition);
			Ray3 aimRay = new Ray3(eyePosition, direction);

			m_componentMiner.Aim(aimRay, AimState.InProgress);
		}

		private void ContinueAiming(ComponentCreature target)
		{
			Vector3 eyePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = target.ComponentBody.BoundingBox.Center();
			Vector3 direction = Vector3.Normalize(targetCenter - eyePosition);
			Ray3 aimRay = new Ray3(eyePosition, direction);

			m_componentMiner.Aim(aimRay, AimState.InProgress);
		}

		private void Shoot(ComponentCreature target)
		{
			// Registrar un manejador temporal para modificar el proyectil justo después de crearse
			Action<Projectile> projectileHandler = null;
			projectileHandler = (Projectile p) =>
			{
				// Solo modificar proyectiles disparados por esta criatura
				if (p.Owner == m_componentCreature)
				{
					// El virote desaparece al tocar el suelo (no se convierte en pickable)
					p.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
					// Evitar que deje ítems al destruirse
					p.TurnIntoPickableBlockValue = 0;
				}
				// Remover el manejador después de la primera ejecución
				if (m_subsystemProjectiles != null)
					m_subsystemProjectiles.ProjectileAdded -= projectileHandler;
			};
			if (m_subsystemProjectiles != null)
				m_subsystemProjectiles.ProjectileAdded += projectileHandler;

			Vector3 eyePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = target.ComponentBody.BoundingBox.Center();
			Vector3 direction = Vector3.Normalize(targetCenter - eyePosition);
			Ray3 aimRay = new Ray3(eyePosition, direction);

			// Disparar (el block behavior creará el proyectil y consumirá el virote)
			m_componentMiner.Aim(aimRay, AimState.Completed);

			// Asegurar que el manejador se quite en caso de que no se haya ejecutado
			if (m_subsystemProjectiles != null)
				m_subsystemProjectiles.ProjectileAdded -= projectileHandler;
		}

		private void CancelAim()
		{
			if (!m_isAiming) return;

			ComponentCreature target = m_componentChaseBehavior?.Target;
			if (target != null)
			{
				Vector3 eyePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetCenter = target.ComponentBody.BoundingBox.Center();
				Vector3 direction = Vector3.Normalize(targetCenter - eyePosition);
				Ray3 aimRay = new Ray3(eyePosition, direction);

				m_componentMiner.Aim(aimRay, AimState.Cancelled);
			}
		}

		private void LoadCrossbowWithRandomBolt(float distance)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return;

			int activeSlot = inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(activeSlot);
			int slotCount = inventory.GetSlotCount(activeSlot);
			if (slotCount == 0) return;

			int crossbowIndex = BlocksManager.GetBlockIndex<CrossbowBlock>(false, false);
			int contents = Terrain.ExtractContents(slotValue);
			if (contents != crossbowIndex) return;

			int data = Terrain.ExtractData(slotValue);
			int newData = data;

			// Tensar al máximo (draw = 15)
			newData = CrossbowBlock.SetDraw(newData, 15);

			// Seleccionar tipo de virote según la distancia
			ArrowBlock.ArrowType selectedBolt = GetBoltTypeByDistance(distance);
			newData = CrossbowBlock.SetArrowType(newData, selectedBolt);

			int newValue = Terrain.MakeBlockValue(crossbowIndex, 0, newData);
			inventory.RemoveSlotItems(activeSlot, slotCount);
			inventory.AddSlotItems(activeSlot, newValue, slotCount);
		}

		private ArrowBlock.ArrowType GetBoltTypeByDistance(float distance)
		{
			// Verificar si está dentro del rango explosivo (distancia >= minExplosive y <= maxExplosive)
			bool useExplosiveRange = (distance >= RangeExplosive.Y && distance <= RangeExplosive.X);

			if (useExplosiveRange)
			{
				// Distancia adecuada para explosivo: elegir aleatoriamente entre todos los tipos (incluyendo explosivo)
				ArrowBlock.ArrowType[] allBolts = new ArrowBlock.ArrowType[]
				{
					ArrowBlock.ArrowType.IronBolt,
					ArrowBlock.ArrowType.DiamondBolt,
					ArrowBlock.ArrowType.ExplosiveBolt
				};
				int index = m_random.Int(allBolts.Length);
				return allBolts[index];
			}
			else
			{
				// Distancia muy corta (o fuera del rango explosivo): solo virote normal (sin explosivo)
				ArrowBlock.ArrowType[] normalBolts = new ArrowBlock.ArrowType[]
				{
					ArrowBlock.ArrowType.IronBolt,
					ArrowBlock.ArrowType.DiamondBolt
				};
				int index = m_random.Int(normalBolts.Length);
				return normalBolts[index];
			}
		}

		public bool Suppressed { get; set; }
		public bool IsActive { get; set; }
	}
}
