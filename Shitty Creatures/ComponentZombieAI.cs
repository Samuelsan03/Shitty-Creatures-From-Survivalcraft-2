using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieAI : ComponentBehavior, IUpdateable
	{
		public static float MusketCooldown = 0.5f;
		public static float MusketAimTime = 1.5f;
		public static float CrossbowCooldown = 0.01f;
		public static float CrossbowAimTime = 1.5f;
		public static float BowCooldown = 0.01f;
		public static float BowAimTime = 1.5f;
		public static float RepeatCrossbowCooldown = 0.01f;
		public static float RepeatCrossbowAimTime = 1.5f;
		public static float FlameThrowerCooldown = 0f;
		public static float FlameThrowerAimTime = 1.5f;
		public static float DoubleMusketCooldown = 0.5f;
		public static float DoubleMusketAimTime = 1.5f;
		// Nuevos valores para ItemsLauncher
		public static float ItemsLauncherCooldown = 0.55f;
		public static float ItemsLauncherAimTime = 1.0f;

		public Vector2 AttackRange = new Vector2(5f, 100f);
		public Vector2 ExplosiveRange = new Vector2(20f, 100f);

		public Vector2 ThrowableRange = new Vector2(5f, 15f);
		public float ThrowableAimTime = 1.5f;
		public float ThrowableCooldown = 0.02f;

		private bool m_canUseInventory;
		private bool m_canEquipClothing;
		private ComponentMiner m_componentMiner;
		private ComponentCreature m_componentCreature;
		private ComponentBody m_componentBody;
		private IInventory m_inventory;
		private ComponentZombieChaseBehavior m_chaseBehavior;
		private SubsystemTime m_subsystemTime;
		private ComponentCreatureModel m_creatureModel;
		private SubsystemProjectiles m_subsystemProjectiles;
		private ComponentCreatureClothing m_componentClothing;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private ComponentPathfinding m_pathfinding;

		private float m_aimTimer;
		private bool m_isAiming;
		private float m_cooldownTimer;

		private bool m_isEquipping;
		private float m_equipTimer;
		private int m_pendingClothingValue;

		private Random m_random = new Random();

		private List<int> m_throwableIndices = new List<int>();

		public override float ImportanceLevel => 100f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// Lista de nombres de criaturas humanoides (manos alzadas)
		private static readonly HashSet<string> HumanoidNames = new HashSet<string>
{
	"GhostNormal", "GhostFast", "Boomer1", "Boomer2", "Boomer3",
	"FrozenGhost", "FrozenGhostBoomer", "BoomerFrozen",
	"GhostBoomer1", "GhostBoomer2", "GhostBoomer3"
};

		private bool IsHumanoidCreature()
		{
			if (m_componentCreature == null) return false;
			string displayName = m_componentCreature.DisplayName;
			return !string.IsNullOrEmpty(displayName) && HumanoidNames.Contains(displayName);
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_canUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);
			m_canEquipClothing = valuesDictionary.GetValue<bool>("CanEquipClothing", false);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			m_inventory = m_componentMiner.Inventory;
			m_creatureModel = m_componentCreature.ComponentCreatureModel;
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_componentClothing = base.Entity.FindComponent<ComponentCreatureClothing>(false);
			m_chaseBehavior = base.Entity.FindComponent<ComponentZombieChaseBehavior>();
			m_pathfinding = base.Entity.FindComponent<ComponentPathfinding>();
			if (m_chaseBehavior == null)
			{
				Log.Warning("ComponentZombieAI: No se encontró ComponentZombieChaseBehavior. IA desactivada.");
			}
			m_subsystemProjectiles.ProjectileAdded += OnProjectileAdded;

			InitializeThrowableIndices();
		}

		private void InitializeThrowableIndices()
		{
			m_throwableIndices.Clear();
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

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<bool>("CanUseInventory", m_canUseInventory);
		}

		private void OnProjectileAdded(Projectile projectile)
		{
			if (projectile.Owner == m_componentCreature)
			{
				Block block = BlocksManager.Blocks[Terrain.ExtractContents(projectile.Value)];
				if (block is ArrowBlock || block is RepeatArrowBlock)
				{
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				}
			}
		}

		public virtual void Update(float dt)
		{
			if (m_canEquipClothing && m_componentClothing != null)
			{
				if (m_isEquipping)
				{
					m_equipTimer += dt;
					if (m_equipTimer >= 0.55f)
					{
						EquipPendingClothing();
						m_isEquipping = false;
					}
				}
				else if (m_subsystemTime.PeriodicGameTimeEvent(1.0, 0.0))
				{
					TryStartEquippingClothing();
				}
			}

			if (!m_canUseInventory || m_componentMiner == null || m_chaseBehavior == null || m_inventory == null)
				return;

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
				return;

			int activeBlockIndex = Terrain.ExtractContents(activeValue);
			Block activeBlock = BlocksManager.Blocks[activeBlockIndex];
			bool isThrowable = IsThrowableBlock(activeBlockIndex);
			bool isRanged = activeBlock is MusketBlock ||
							activeBlock is CrossbowBlock ||
							activeBlock is RepeatCrossbowBlock ||
							activeBlock is BowBlock ||
							activeBlock is FlameThrowerBlock ||
							activeBlock is DoubleMusketBlock ||
							activeBlock is ItemsLauncherBlock; // <-- Añadido
			bool isMelee = !isRanged && !isThrowable && activeBlock.GetMeleePower(activeValue) > 0f;

			float distToTarget = Vector3.Distance(m_componentBody.Position, target.ComponentBody.Position);
			bool hasLOS = HasLineOfSightToTarget(target);
			bool isStuck = (m_pathfinding != null && m_pathfinding.IsStuck);

			if ((!hasLOS || isStuck) && m_isAiming)
			{
				StopAiming();
			}

			if (isThrowable && distToTarget >= ThrowableRange.X && distToTarget <= ThrowableRange.Y && hasLOS && !isStuck)
			{
				StopMovement();
				PerformThrowableAttack(dt, target.ComponentBody.Position);
				return;
			}
			else if (!isThrowable && distToTarget >= ThrowableRange.X && distToTarget <= ThrowableRange.Y && HasThrowableInInventory() && hasLOS && !isStuck)
			{
				EquipBestThrowableWeapon();
				StopAiming();
				return;
			}

			float meleeDist = GetMeleeDistanceToTarget(target.ComponentBody);

			if (meleeDist <= AttackRange.X)
			{
				if (!isMelee)
				{
					if (TryEquipBestMeleeWeapon())
					{
						PerformMeleeAttack(target);
					}
					else if (isRanged && hasLOS && !isStuck)
					{
						UpdateRangedCombat(dt, target.ComponentBody.Position);
						PerformMeleeAttack(target);
					}
					else if (isThrowable && hasLOS && !isStuck)
					{
						if (TryEquipBestMeleeWeapon())
							PerformMeleeAttack(target);
					}
				}
				else
				{
					PerformMeleeAttack(target);
				}
				return;
			}

			if (distToTarget <= AttackRange.Y)
			{
				if (isMelee)
				{
					if (hasLOS && !isStuck)
						EquipBestRangedWeapon();
				}
				else if (isRanged)
				{
					if (hasLOS && !isStuck)
						UpdateRangedCombat(dt, target.ComponentBody.Position);
					else
						StopAiming();
				}
				else if (isThrowable)
				{
					if (distToTarget > ThrowableRange.Y && hasLOS && !isStuck)
						EquipBestRangedWeapon();
					else if (distToTarget < ThrowableRange.X && hasLOS && !isStuck)
						TryEquipBestMeleeWeapon();
				}
			}
			else
			{
				StopAiming();
			}
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

		private bool HasLineOfSightToTarget(ComponentCreature target)
		{
			if (target == null || target.ComponentBody == null) return false;

			Vector3 eyePos = m_creatureModel.EyePosition;
			Vector3 targetPos = target.ComponentBody.Position + new Vector3(0f, 0.5f, 0f);
			Vector3 directionToTarget = targetPos - eyePos;
			float distance = directionToTarget.Length();
			if (distance < 0.1f) return true;
			directionToTarget /= distance;

			Vector3 forwardDirection = m_componentBody.Rotation.GetForwardVector();
			if (Vector3.Dot(forwardDirection, directionToTarget) <= 0f)
				return false;

			TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(eyePos, targetPos, false, false, (int value, float d) =>
			{
				return d > 0.1f && BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable_(value);
			});
			if (terrainHit != null && terrainHit.Value.Distance < distance)
				return false;

			BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(eyePos, targetPos, 0f, (ComponentBody body, float d) =>
			{
				return body != m_componentBody && body != target.ComponentBody && d > 0.1f;
			});
			if (bodyHit != null && bodyHit.Value.Distance < distance)
				return false;

			return true;
		}

		private void UpdateRangedCombat(float dt, Vector3 targetPos)
		{
			if (!HasLineOfSightToTarget(m_chaseBehavior.m_target) || (m_pathfinding != null && m_pathfinding.IsStuck))
			{
				StopAiming();
				return;
			}

			int activeValue = m_inventory.GetSlotValue(m_inventory.ActiveSlotIndex);
			Block activeBlock = BlocksManager.Blocks[Terrain.ExtractContents(activeValue)];
			bool isMusket = activeBlock is MusketBlock;
			bool isCrossbow = activeBlock is CrossbowBlock;
			bool isRepeatCrossbow = activeBlock is RepeatCrossbowBlock;
			bool isBow = activeBlock is BowBlock;
			bool isFlameThrower = activeBlock is FlameThrowerBlock;
			bool isDoubleMusket = activeBlock is DoubleMusketBlock;
			bool isItemsLauncher = activeBlock is ItemsLauncherBlock;

			if (!isMusket && !isCrossbow && !isRepeatCrossbow && !isBow && !isFlameThrower && !isDoubleMusket && !isItemsLauncher)
				return;

			float aimTime = 0f;
			float cooldown = 0f;

			if (isMusket) { aimTime = MusketAimTime; cooldown = MusketCooldown; }
			else if (isCrossbow) { aimTime = CrossbowAimTime; cooldown = CrossbowCooldown; }
			else if (isRepeatCrossbow) { aimTime = RepeatCrossbowAimTime; cooldown = RepeatCrossbowCooldown; }
			else if (isBow) { aimTime = BowAimTime; cooldown = BowCooldown; }
			else if (isFlameThrower) { aimTime = FlameThrowerAimTime; cooldown = FlameThrowerCooldown; }
			else if (isDoubleMusket) { aimTime = DoubleMusketAimTime; cooldown = DoubleMusketCooldown; }
			else if (isItemsLauncher) { aimTime = ItemsLauncherAimTime; cooldown = ItemsLauncherCooldown; }

			if (m_cooldownTimer > 0f)
				m_cooldownTimer -= dt;

			if (!m_isAiming && m_cooldownTimer <= 0f)
				StartAiming();

			if (m_isAiming)
			{
				m_aimTimer += dt;
				Vector3 eyePos = m_creatureModel.EyePosition;
				Vector3 dir = Vector3.Normalize(targetPos + new Vector3(0f, 1f, 0f) - eyePos);
				Ray3 aimRay = new Ray3(eyePos, dir);

				// SOLO ItemsLauncher usa apuntado manual (para evitar el subsystem)
				// TODAS las demás armas usan ComponentMiner.Aim normalmente
				if (isItemsLauncher)
				{
					// Apuntado manual SIN usar ComponentMiner.Aim
					if (m_creatureModel != null)
					{
						m_creatureModel.AimHandAngleOrder = 0f;
						m_creatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
						m_creatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
					}

					if (m_aimTimer >= aimTime)
					{
						ReloadItemsLauncherInstantly();
						m_isAiming = false;
						m_cooldownTimer = cooldown;
						ResetModelPose();
					}
				}
				else
				{
					// Para TODAS las demás armas, usar el sistema normal de apuntado (ComponentMiner.Aim)
					if (m_aimTimer < aimTime)
					{
						m_componentMiner.Aim(aimRay, AimState.InProgress);
						// Las animaciones las maneja el SubsystemBlockBehavior correspondiente
					}
					else
					{
						m_componentMiner.Aim(aimRay, AimState.Completed);
						m_isAiming = false;
						m_cooldownTimer = cooldown;
						ResetModelPose();
					}
				}
			}
		}

		private void PerformThrowableAttack(float dt, Vector3 targetPos)
		{
			if (!HasLineOfSightToTarget(m_chaseBehavior.m_target) || (m_pathfinding != null && m_pathfinding.IsStuck))
			{
				StopAiming();
				return;
			}

			if (m_cooldownTimer > 0f)
			{
				m_cooldownTimer -= dt;
				return;
			}

			if (m_isAiming)
			{
				m_aimTimer += dt;
				Vector3 eyePos = m_creatureModel.EyePosition;
				Vector3 dir = Vector3.Normalize(targetPos + new Vector3(0f, 1f, 0f) - eyePos);
				Ray3 aimRay = new Ray3(eyePos, dir);

				if (m_aimTimer >= ThrowableAimTime)
				{
					m_componentMiner.Aim(aimRay, AimState.Completed);
					m_isAiming = false;
					m_cooldownTimer = ThrowableCooldown;
					m_aimTimer = 0f;
					ResetModelPose();
				}
				else
				{
					m_componentMiner.Aim(aimRay, AimState.InProgress);
					if (m_creatureModel != null)
					{
						m_creatureModel.AimHandAngleOrder = 3.2f;
						ComponentFirstPersonModel firstPerson = m_componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
						if (firstPerson != null)
						{
							firstPerson.ItemOffsetOrder = new Vector3(0f, 0.35f, 0.17f);
							firstPerson.ItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
						}
						m_creatureModel.InHandItemOffsetOrder = new Vector3(0f, -0.25f, 0f);
						m_creatureModel.InHandItemRotationOrder = new Vector3(3.14159f, 0f, 0f);
					}
				}
			}
			else
			{
				StopAiming();
				m_isAiming = true;
				m_aimTimer = 0f;
			}
		}

		private void StopAiming()
		{
			if (m_isAiming)
			{
				Vector3 eyePos = m_creatureModel.EyePosition;
				m_componentMiner.Aim(new Ray3(eyePos, Vector3.UnitZ), AimState.Cancelled);
				m_isAiming = false;
				m_aimTimer = 0f;
				ResetModelPose();
			}
		}

		private void StartAiming()
		{
			StopAiming();
			int activeValue = m_inventory.GetSlotValue(m_inventory.ActiveSlotIndex);
			Block activeBlock = BlocksManager.Blocks[Terrain.ExtractContents(activeValue)];
			if (activeBlock is CrossbowBlock)
			{
				int data = Terrain.ExtractData(activeValue);
				int draw = CrossbowBlock.GetDraw(data);
				ArrowBlock.ArrowType? arrow = CrossbowBlock.GetArrowType(data);
				if (draw != 15 || arrow == null)
					ReloadCrossbowInstantly(Vector3.Distance(m_componentBody.Position, m_chaseBehavior.m_target.ComponentBody.Position));
			}
			else if (activeBlock is RepeatCrossbowBlock)
			{
				int data = Terrain.ExtractData(activeValue);
				int draw = RepeatCrossbowBlock.GetDraw(data);
				RepeatArrowBlock.ArrowType? arrow = RepeatCrossbowBlock.GetArrowType(data);
				int loadCount = RepeatCrossbowBlock.GetLoadCount(activeValue);
				if (draw != 15 || arrow == null || loadCount == 0)
					ReloadRepeatCrossbowInstantly(Vector3.Distance(m_componentBody.Position, m_chaseBehavior.m_target.ComponentBody.Position));
			}
			else if (activeBlock is BowBlock)
			{
				int data = Terrain.ExtractData(activeValue);
				int draw = BowBlock.GetDraw(data);
				ArrowBlock.ArrowType? arrow = BowBlock.GetArrowType(data);
				if (draw != 15 || arrow == null)
					ReloadBowInstantly();
			}
			else if (activeBlock is FlameThrowerBlock)
			{
				int loadCount = FlameThrowerBlock.GetLoadCount(activeValue);
				if (loadCount == 0)
					ReloadFlameThrowerInstantly();
			}
			else if (activeBlock is DoubleMusketBlock)
			{
				int data = Terrain.ExtractData(activeValue);
				int shotsRemaining = DoubleMusketBlock.GetShotsRemaining(data);
				if (shotsRemaining == 0)
					ReloadDoubleMusketInstantly();
			}
			// ItemsLauncher no requiere recarga (no busca munición)
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

		private void StopMovement()
		{
			ComponentPathfinding pathfinding = base.Entity.FindComponent<ComponentPathfinding>();
			if (pathfinding != null)
			{
				pathfinding.Stop();
			}
		}

		private void EquipBestRangedWeapon()
		{
			// Prioridad: Mosquete, Doble Mosquete, Lanzador de Ítems, Lanzallamas, Ballesta, Ballesta Rápida, Arco
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) > 0 && BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is MusketBlock)
				{
					m_inventory.ActiveSlotIndex = i;
					return;
				}
			}
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) > 0 && BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is DoubleMusketBlock)
				{
					m_inventory.ActiveSlotIndex = i;
					return;
				}
			}
			// Añadido ItemsLauncher
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) > 0 && BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is ItemsLauncherBlock)
				{
					m_inventory.ActiveSlotIndex = i;
					return;
				}
			}
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) > 0 && BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is FlameThrowerBlock)
				{
					m_inventory.ActiveSlotIndex = i;
					return;
				}
			}
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) > 0 && BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is CrossbowBlock)
				{
					m_inventory.ActiveSlotIndex = i;
					return;
				}
			}
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) > 0 && BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is RepeatCrossbowBlock)
				{
					m_inventory.ActiveSlotIndex = i;
					return;
				}
			}
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) > 0 && BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is BowBlock)
				{
					m_inventory.ActiveSlotIndex = i;
					return;
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
				if (m_inventory.GetSlotCount(i) == 0) continue;
				int blockIndex = Terrain.ExtractContents(slotValue);
				Block block = BlocksManager.Blocks[blockIndex];
				if (block is MusketBlock || block is CrossbowBlock || block is RepeatCrossbowBlock || block is BowBlock ||
					block is FlameThrowerBlock || block is DoubleMusketBlock || block is ItemsLauncherBlock || IsThrowableBlock(blockIndex))
					continue;
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
			if (targetBody == null || GetMeleeDistanceToTarget(targetBody) > AttackRange.X)
				return;

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
			if (!(BlocksManager.Blocks[Terrain.ExtractContents(currentValue)] is MusketBlock))
				return;

			m_inventory.RemoveSlotItems(activeSlot, 1);
			int bulletType = m_random.Int(0, 2);
			int data = 0;
			data = MusketBlock.SetHammerState(data, false);
			data = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
			data = MusketBlock.SetBulletType(data, (BulletBlock.BulletType)bulletType);
			m_inventory.AddSlotItems(activeSlot, Terrain.MakeBlockValue(MusketBlock.Index, 0, data), 1);
		}

		private void ReloadCrossbowInstantly(float distanceToTarget)
		{
			int activeSlot = m_inventory.ActiveSlotIndex;
			int currentValue = m_inventory.GetSlotValue(activeSlot);
			if (!(BlocksManager.Blocks[Terrain.ExtractContents(currentValue)] is CrossbowBlock))
				return;

			m_inventory.RemoveSlotItems(activeSlot, 1);

			ArrowBlock.ArrowType boltType;
			bool useExplosive = (distanceToTarget >= ExplosiveRange.X && distanceToTarget <= ExplosiveRange.Y);
			if (useExplosive)
			{
				int r = m_random.Int(0, 2);
				if (r == 0) boltType = ArrowBlock.ArrowType.IronBolt;
				else if (r == 1) boltType = ArrowBlock.ArrowType.DiamondBolt;
				else boltType = ArrowBlock.ArrowType.ExplosiveBolt;
			}
			else
			{
				boltType = m_random.Bool() ? ArrowBlock.ArrowType.IronBolt : ArrowBlock.ArrowType.DiamondBolt;
			}

			int data = 0;
			data = CrossbowBlock.SetDraw(data, 15);
			data = CrossbowBlock.SetArrowType(data, boltType);
			m_inventory.AddSlotItems(activeSlot, Terrain.MakeBlockValue(CrossbowBlock.Index, 0, data), 1);
		}

		private void ReloadRepeatCrossbowInstantly(float distanceToTarget)
		{
			int activeSlot = m_inventory.ActiveSlotIndex;
			int currentValue = m_inventory.GetSlotValue(activeSlot);
			if (!(BlocksManager.Blocks[Terrain.ExtractContents(currentValue)] is RepeatCrossbowBlock))
				return;

			m_inventory.RemoveSlotItems(activeSlot, 1);

			RepeatArrowBlock.ArrowType[] allArrowTypes = new RepeatArrowBlock.ArrowType[]
			{
				RepeatArrowBlock.ArrowType.CopperArrow,
				RepeatArrowBlock.ArrowType.IronArrow,
				RepeatArrowBlock.ArrowType.DiamondArrow,
				RepeatArrowBlock.ArrowType.ExplosiveArrow,
				RepeatArrowBlock.ArrowType.PoisonArrow,
				RepeatArrowBlock.ArrowType.SeriousPoisonArrow
			};

			RepeatArrowBlock.ArrowType arrowType;
			bool useExplosive = (distanceToTarget >= ExplosiveRange.X && distanceToTarget <= ExplosiveRange.Y);

			if (useExplosive)
			{
				arrowType = allArrowTypes[m_random.Int(0, allArrowTypes.Length - 1)];
			}
			else
			{
				RepeatArrowBlock.ArrowType[] nonExplosiveTypes = new RepeatArrowBlock.ArrowType[]
				{
					RepeatArrowBlock.ArrowType.CopperArrow,
					RepeatArrowBlock.ArrowType.IronArrow,
					RepeatArrowBlock.ArrowType.DiamondArrow,
					RepeatArrowBlock.ArrowType.PoisonArrow,
					RepeatArrowBlock.ArrowType.SeriousPoisonArrow
				};
				arrowType = nonExplosiveTypes[m_random.Int(0, nonExplosiveTypes.Length - 1)];
			}

			int data = 0;
			data = RepeatCrossbowBlock.SetDraw(data, 15);
			data = RepeatCrossbowBlock.SetArrowType(data, arrowType);
			int value = RepeatCrossbowBlock.SetLoadCount(Terrain.MakeBlockValue(RepeatCrossbowBlock.Index, 0, data), 5);
			m_inventory.AddSlotItems(activeSlot, value, 1);
		}

		private void ReloadBowInstantly()
		{
			int activeSlot = m_inventory.ActiveSlotIndex;
			int currentValue = m_inventory.GetSlotValue(activeSlot);
			if (!(BlocksManager.Blocks[Terrain.ExtractContents(currentValue)] is BowBlock))
				return;

			m_inventory.RemoveSlotItems(activeSlot, 1);

			ArrowBlock.ArrowType[] arrowTypes = new ArrowBlock.ArrowType[]
			{
				ArrowBlock.ArrowType.WoodenArrow,
				ArrowBlock.ArrowType.StoneArrow,
				ArrowBlock.ArrowType.CopperArrow,
				ArrowBlock.ArrowType.IronArrow,
				ArrowBlock.ArrowType.DiamondArrow,
				ArrowBlock.ArrowType.FireArrow
			};

			ArrowBlock.ArrowType arrowType = arrowTypes[m_random.Int(0, arrowTypes.Length - 1)];

			int data = 0;
			data = BowBlock.SetDraw(data, 15);
			data = BowBlock.SetArrowType(data, arrowType);
			m_inventory.AddSlotItems(activeSlot, Terrain.MakeBlockValue(BowBlock.Index, 0, data), 1);
		}

		private void ReloadFlameThrowerInstantly()
		{
			int activeSlot = m_inventory.ActiveSlotIndex;
			int currentValue = m_inventory.GetSlotValue(activeSlot);
			if (!(BlocksManager.Blocks[Terrain.ExtractContents(currentValue)] is FlameThrowerBlock))
				return;

			m_inventory.RemoveSlotItems(activeSlot, 1);
			FlameBulletBlock.FlameBulletType bulletType = m_random.Bool()
				? FlameBulletBlock.FlameBulletType.Flame
				: FlameBulletBlock.FlameBulletType.Poison;

			int data = 0;
			data = FlameThrowerBlock.SetLoadState(data, FlameThrowerBlock.LoadState.Loaded);
			data = FlameThrowerBlock.SetBulletType(data, bulletType);
			int newValue = FlameThrowerBlock.SetLoadCount(Terrain.MakeBlockValue(FlameThrowerBlock.Index, 0, data), 15);
			m_inventory.AddSlotItems(activeSlot, newValue, 1);
		}

		private void ReloadDoubleMusketInstantly()
		{
			int activeSlot = m_inventory.ActiveSlotIndex;
			int currentValue = m_inventory.GetSlotValue(activeSlot);
			if (!(BlocksManager.Blocks[Terrain.ExtractContents(currentValue)] is DoubleMusketBlock))
				return;

			m_inventory.RemoveSlotItems(activeSlot, 1);

			int data = 0;
			data = DoubleMusketBlock.SetLoaded(data, true);
			data = DoubleMusketBlock.SetShotsRemaining(data, 2);
			data = DoubleMusketBlock.SetAntiTanksBullet(data, true);
			data = DoubleMusketBlock.SetHammerState(data, false);
			data = DoubleMusketBlock.SetBulletType(data, BulletBlock.BulletType.MusketBall);

			int newValue = Terrain.MakeBlockValue(DoubleMusketBlock.Index, 0, data);
			m_inventory.AddSlotItems(activeSlot, newValue, 1);
		}

		// Nuevo método para el lanzador de ítems
		private void ReloadItemsLauncherInstantly()
		{
			ComponentCreature target = m_chaseBehavior.m_target;
			if (target == null) return;

			// Posición de los ojos y dirección de la mira
			Vector3 eyePos = m_creatureModel.EyePosition;
			Vector3 aimDir = m_creatureModel.EyeRotation.GetForwardVector();

			// Calcular la boca del cañón (igual que en CreatureAI)
			Vector3 muzzlePos = eyePos + m_componentBody.Matrix.Right * 0.3f - m_componentBody.Matrix.Up * 0.2f;
			Vector3 dirNorm = Vector3.Normalize(muzzlePos + aimDir * 10f - muzzlePos);

			// Crear la bala MusketBall
			int bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false, false);
			if (bulletBlockIndex <= 0) return;

			int bulletData = BulletBlock.SetBulletType(0, BulletBlock.BulletType.MusketBall);
			int bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, bulletData);

			// Velocidad del proyectil = velocidad del zombie + velocidad de disparo (60)
			float speed = 100f;
			Vector3 velocity = m_componentCreature.ComponentBody.Velocity + speed * dirNorm;

			// Disparar
			m_subsystemProjectiles.FireProjectile(bulletValue, muzzlePos, velocity, Vector3.Zero, m_componentCreature);

			// Sonido (volumen 0.5f como en CreatureAI)
			SubsystemAudio audio = base.Project.FindSubsystem<SubsystemAudio>(true);
			if (audio != null)
			{
				audio.PlaySound("Audio/Items/ItemLauncher/Item Cannon Fire", 1f,
					m_random.Float(-0.1f, 0.1f), eyePos, 10f, true);
			}

			// Partículas de humo
			SubsystemParticles particles = base.Project.FindSubsystem<SubsystemParticles>(true);
			if (particles != null && m_subsystemTerrain != null)
			{
				particles.AddParticleSystem(
					new GunSmokeParticleSystem(m_subsystemTerrain, muzzlePos + 0.3f * dirNorm, dirNorm),
					false
				);
			}

			// Retroceso (igual que en CreatureAI: -4f * dirNorm)
			m_componentBody.ApplyImpulse(-4f * dirNorm);
		}

		private bool IsThrowableBlock(int blockIndex)
		{
			return m_throwableIndices.Contains(blockIndex);
		}

		private bool HasThrowableInInventory()
		{
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) > 0 && IsThrowableBlock(Terrain.ExtractContents(value)))
					return true;
			}
			return false;
		}

		private void EquipBestThrowableWeapon()
		{
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) > 0 && IsThrowableBlock(Terrain.ExtractContents(value)))
				{
					m_inventory.ActiveSlotIndex = i;
					return;
				}
			}
		}

		private void TryStartEquippingClothing()
		{
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				if (value != 0 && BlocksManager.Blocks[Terrain.ExtractContents(value)] is ClothingBlock)
				{
					ClothingData data = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetClothingData(value);
					if (data != null)
					{
						m_inventory.RemoveSlotItems(i, 1);
						m_pendingClothingValue = value;
						m_isEquipping = true;
						m_equipTimer = 0f;
						return;
					}
				}
			}
		}

		private void EquipPendingClothing()
		{
			if (m_pendingClothingValue == 0 || m_componentClothing == null) return;

			ClothingData data = BlocksManager.Blocks[Terrain.ExtractContents(m_pendingClothingValue)].GetClothingData(m_pendingClothingValue);
			if (data == null) return;

			List<int> clothes = new List<int>(m_componentClothing.GetClothes(data.Slot));
			clothes.Add(m_pendingClothingValue);
			clothes.Sort((a, b) =>
			{
				ClothingData da = BlocksManager.Blocks[Terrain.ExtractContents(a)].GetClothingData(a);
				ClothingData db = BlocksManager.Blocks[Terrain.ExtractContents(b)].GetClothingData(b);
				return (da?.Layer ?? 0) - (db?.Layer ?? 0);
			});
			m_componentClothing.SetClothes(data.Slot, clothes);
			m_pendingClothingValue = 0;
		}
	}
}
