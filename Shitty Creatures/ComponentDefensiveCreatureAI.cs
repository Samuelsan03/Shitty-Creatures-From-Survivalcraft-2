using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentDefensiveCreatureAI : Component, IUpdateable
	{
		public bool CanUseInventory;
		public Vector2 RangedAttackRange = new Vector2(5f, 100f);

		// Rangos para objetos lanzables
		public Vector2 ThrowableAttackRange = new Vector2(5f, 15f);

		// Tiempos del mosquete
		private float MusketAimTime = 1.5f;
		private float MusketCooldown = 0.5f;

		// Tiempos de la ballesta
		private float CrossbowAimTime = 1.5f;
		private float CrossbowCooldown = 0.5f;

		// Tiempos del arco
		private float BowAimTime = 1.5f;
		private float BowCooldown = 0.5f;

		// Tiempos para lanzables
		private float ThrowableAimTime = 1.55f;
		private float ThrowableCooldown = 0.25f;

		private SubsystemTime m_subsystemTime;
		private ComponentMiner m_componentMiner;
		private ComponentNewChaseBehavior m_componentChase;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private SubsystemAudio m_subsystemAudio;
		private Random m_random = new Random();

		private bool m_isAiming;
		private float m_aimTimer;
		private float m_cooldownTimer;

		// Apuntado personalizado para criaturas que no levantan el brazo
		private bool m_isCustomAiming;
		private float m_customAimTimer;
		private int m_customWeaponContents;

		// Conjunto de índices de bloques lanzables
		private HashSet<int> m_throwableIndices = new HashSet<int>();

		// Rotaciones base que el zombie usa para apuntar cada arma (en radianes)
		private static readonly Dictionary<int, Vector3> BaseWeaponRotations = new Dictionary<int, Vector3>
		{
			{ MusketBlock.Index, new Vector3(-1.7f, 0f, 0f) },
			{ CrossbowBlock.Index, new Vector3(-1.55f, 0f, 0f) },
			{ BowBlock.Index, new Vector3(0f, -0.2f, 0f) }
		};

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentChase = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);

			// Inicializar lista de bloques lanzables
			InitializeThrowableIndices();
		}

		private void InitializeThrowableIndices()
		{
			// Añadir todos los tipos de bloques lanzables
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<StoneChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<SulphurChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<CoalChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<DiamondChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<GermaniumChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<GermaniumOreChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<IronOreChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<MalachiteChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<SaltpeterChunkBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<GunpowderBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<BombBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<IncendiaryBombBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<PoisonBombBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<BrickBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<SnowballBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<EggBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<CopperSpearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<DiamondSpearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<IronSpearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<WoodenSpearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<WoodenLongspearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<StoneSpearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<StoneLongspearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<IronLongspearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<LavaLongspearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<LavaSpearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<DiamondLongspearBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<FreezingSnowballBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<FreezeBombBlock>(false, false));
			m_throwableIndices.Add(BlocksManager.GetBlockIndex<FireworksBlock>(false, false));
		}

		private bool IsSpecialNoRaiseCreature()
		{
			string templateName = m_componentCreature.Entity.ValuesDictionary.DatabaseObject.Name;
			return templateName == "InfectedNormalTamed1" ||
				   templateName == "InfectedNormalTamed2" ||
				   templateName == "InfectedMuscleTamed1" ||
				   templateName == "InfectedMuscleTamed2";
		}

		public void Update(float dt)
		{
			if (!CanUseInventory || m_componentMiner == null || m_componentCreature == null)
				return;

			float distance = GetTargetDistance();
			bool hasTarget = IsTargetValidForRangedAttack();

			// Manejar apuntado personalizado (sin levantar brazo)
			if (m_isCustomAiming)
			{
				if (!hasTarget)
				{
					StopCustomAiming();
					return;
				}

				if (distance <= RangedAttackRange.X)
				{
					StopCustomAiming();
					SwitchToMeleeWeapon();
					return;
				}

				float aimTime = GetAimTime(m_customWeaponContents);
				m_customAimTimer += dt;
				Ray3 aimRay = CalculateAimRay();

				// Llamar a InProgress para que los subsistemas preparen el arma (tensar, amartillar, etc.)
				m_componentMiner.Aim(aimRay, AimState.InProgress);

				// Sobrescribir la rotación del modelo para evitar que levante el brazo
				UpdateWeaponRotation(aimRay);

				if (m_customAimTimer < aimTime)
				{
					// Seguir apuntando (no detenemos el movimiento)
				}
				else
				{
					// Disparar
					m_componentMiner.Aim(aimRay, AimState.Completed);
					m_isCustomAiming = false;
					m_cooldownTimer = GetCooldown(m_customWeaponContents);

					// Acciones post-disparo
					if (m_customWeaponContents == CrossbowBlock.Index || m_customWeaponContents == BowBlock.Index)
					{
						SubsystemProjectiles subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
						foreach (Projectile p in subsystemProjectiles.Projectiles)
						{
							if (p != null && Terrain.ExtractContents(p.Value) == ArrowBlock.Index)
							{
								p.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}
						}
						if (m_customWeaponContents == BowBlock.Index)
							EnsureBowEquipped();
						else
							EnsureCrossbowEquipped();
					}
					else if (m_customWeaponContents == MusketBlock.Index)
					{
						EnsureMusketEquipped();
					}

					// Restaurar rotación del arma tras disparar
					ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
					if (model != null)
					{
						model.InHandItemRotationOrder = Vector3.Zero;
						model.InHandItemOffsetOrder = Vector3.Zero;
						model.AimHandAngleOrder = 0f;
					}
				}
				return;
			}

			// Manejar apuntado normal (comportamiento original)
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

				if (IsThrowable(activeContents))
				{
					StopMovement();
				}

				if (m_aimTimer < aimTime)
				{
					m_componentMiner.Aim(aimRay, AimState.InProgress);
				}
				else
				{
					m_componentMiner.Aim(aimRay, AimState.Completed);
					m_isAiming = false;
					m_cooldownTimer = GetCooldown(activeContents);

					if (activeContents == CrossbowBlock.Index || activeContents == BowBlock.Index)
					{
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
						EnsureMusketEquipped();
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

				// 1. Lanzables (máxima prioridad) - siempre usan apuntado normal
				if (distance >= ThrowableAttackRange.X && distance <= ThrowableAttackRange.Y && HasThrowableInInventory())
				{
					if (EnsureThrowableEquipped())
					{
						m_isAiming = true;
						m_aimTimer = 0f;
						m_componentMiner.Aim(CalculateAimRay(), AimState.InProgress);
						return;
					}
				}

				// Para criaturas especiales: apuntado sin levantar brazo
				if (IsSpecialNoRaiseCreature())
				{
					// 2. Ballesta
					if (HasCrossbowInInventory() && EnsureCrossbowEquipped())
					{
						StartCustomAiming(CrossbowBlock.Index);
						return;
					}

					// 3. Arco
					if (HasBowInInventory() && EnsureBowEquipped())
					{
						StartCustomAiming(BowBlock.Index);
						return;
					}

					// 4. Mosquete
					if (EnsureMusketEquipped())
					{
						StartCustomAiming(MusketBlock.Index);
						return;
					}
				}
				else
				{
					// Comportamiento normal para otras criaturas
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

		private void StartCustomAiming(int weaponContents)
		{
			m_isCustomAiming = true;
			m_customAimTimer = 0f;
			m_customWeaponContents = weaponContents;

			if (m_componentCreature.ComponentCreatureModel != null)
				m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
		}

		private void StopCustomAiming()
		{
			if (m_isCustomAiming)
			{
				// Cancelar la puntería en el subsistema (para que suelte el martillo, etc.)
				Ray3 aimRay = CalculateAimRay();
				m_componentMiner.Aim(aimRay, AimState.Cancelled);

				ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
				if (model != null)
				{
					model.InHandItemRotationOrder = Vector3.Zero;
					model.InHandItemOffsetOrder = Vector3.Zero;
					model.AimHandAngleOrder = 0f;
				}
				m_isCustomAiming = false;
				m_customAimTimer = 0f;
			}
		}

		private void UpdateWeaponRotation(Ray3 aimRay)
		{
			ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
			if (model == null) return;

			// Rotación base del arma (la misma que usa el zombie, sin añadir offsets)
			Vector3 baseRot = BaseWeaponRotations.ContainsKey(m_customWeaponContents)
				? BaseWeaponRotations[m_customWeaponContents]
				: Vector3.Zero;

			// Fijar la rotación del arma en la mano, sin cambios dinámicos
			model.InHandItemRotationOrder = baseRot;
			model.AimHandAngleOrder = 0f;

			// Ajustar posición del arma en la mano (opcional)
			if (m_customWeaponContents == BowBlock.Index)
				model.InHandItemOffsetOrder = new Vector3(0.15f, -0.15f, 0.15f);
			else
				model.InHandItemOffsetOrder = new Vector3(-0.1f, -0.15f, 0.25f);

			// Forzar que la criatura mire al objetivo (el cuerpo gira para apuntar)
			if (m_componentChase != null && m_componentChase.Target != null)
			{
				model.LookAtOrder = m_componentChase.Target.ComponentCreatureModel.EyePosition;
				model.LookRandomOrder = false;
			}
		}

		private float GetAimTime(int contents)
		{
			if (contents == CrossbowBlock.Index) return CrossbowAimTime;
			if (contents == BowBlock.Index) return BowAimTime;
			if (IsThrowable(contents)) return ThrowableAimTime;
			return MusketAimTime;
		}

		private float GetCooldown(int contents)
		{
			if (contents == CrossbowBlock.Index) return CrossbowCooldown;
			if (contents == BowBlock.Index) return BowCooldown;
			if (IsThrowable(contents)) return ThrowableCooldown;
			return MusketCooldown;
		}

		private bool IsThrowable(int contents)
		{
			return m_throwableIndices.Contains(contents);
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

		private void StopMovement()
		{
			m_componentPathfinding?.Stop();
		}

		private bool SwitchToMeleeWeapon()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(slotValue);
				if (slotValue != 0 && contents != MusketBlock.Index && contents != CrossbowBlock.Index && contents != BowBlock.Index && !IsThrowable(contents))
				{
					inventory.ActiveSlotIndex = i;
					return true;
				}
			}
			return false;
		}

		// ========== LANZABLES ==========
		private bool HasThrowableInInventory()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (slotValue != 0 && IsThrowable(Terrain.ExtractContents(slotValue)))
					return true;
			}
			return false;
		}

		private bool EnsureThrowableEquipped()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			int activeSlot = inventory.ActiveSlotIndex;
			int activeValue = inventory.GetSlotValue(activeSlot);
			if (activeValue != 0 && IsThrowable(Terrain.ExtractContents(activeValue)))
				return true;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (slotValue != 0 && IsThrowable(Terrain.ExtractContents(slotValue)))
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
			if (m_isCustomAiming) StopCustomAiming();
			base.Dispose();
		}
	}
}
