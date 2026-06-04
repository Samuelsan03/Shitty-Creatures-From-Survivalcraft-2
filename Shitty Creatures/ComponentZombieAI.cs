using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using static Game.SubsystemGreenNightSky;

namespace Game
{
	public class ComponentZombieAI : ComponentBehavior, IUpdateable
	{
		public static float MusketCooldown = 0.02f;
		public static float MusketAimTime = 1.5f;
		public static float CrossbowCooldown = 0.01f;
		public static float CrossbowAimTime = 1.5f;
		public static float BowCooldown = 0.01f;
		public static float BowAimTime = 1.5f;
		public static float RepeatCrossbowCooldown = 0.01f;
		public static float RepeatCrossbowAimTime = 1.5f;
		public static float FlameThrowerCooldown = 0f;
		public static float FlameThrowerAimTime = 1.5f;
		public static float DoubleMusketCooldown = 0.02f;
		public static float DoubleMusketAimTime = 1.5f;
		public static float ItemsLauncherCooldown = 0.02f;
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
		private ComponentZombieRunAwayBehavior m_zombieRunAwayBehavior;
		private SubsystemTime m_subsystemTime;
		private ComponentCreatureModel m_creatureModel;
		private SubsystemProjectiles m_subsystemProjectiles;
		private ComponentCreatureClothing m_componentClothing;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private ComponentPathfinding m_pathfinding;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;

		private float m_aimTimer;
		private bool m_isAiming;
		private float m_cooldownTimer;

		private bool m_isEquipping;
		private float m_equipTimer;
		private int m_pendingClothingValue;

		private Random m_random = new Random();

		// Flanqueo lateral para lanzables
		private float m_flankTimer;
		private Vector3 m_flankDirection;
		private bool m_isFlanking;

		private List<int> m_throwableIndices = new List<int>();

		private DifficultyMode m_currentDifficulty;
		private bool m_flankingEnabled;

		// ========== ARMAS DE FUEGO MODERNAS ==========
		private class FirearmDefConfig
		{
			public Type BulletBlockType;
			public string ShootSound;
			public float FireRate;
			public float BulletSpeed;
			public int ProjectilesPerShot;
			public Vector3 SpreadVector;
			public bool IsSniper;
			public bool IsAutomatic;
			public int MaxShotsBeforeReload;
		}

		private static Dictionary<int, FirearmDefConfig> m_firearmConfigs;

		private bool m_isFirearmReloading = false;
		private float m_firearmReloadTimer = 0f;
		private int m_firearmShotsSinceReload = 0;
		private const float FirearmReloadTime = 1.0f;
		private double m_lastFirearmShotTime;
		private bool m_hasCompletedInitialAim = false;
		// ============================================

		// Lista de criaturas "normales" (humanoides) que deben usar Miner.Aim para la mayoría de armas
		private static readonly HashSet<string> s_normalCreatureNames = new HashSet<string>
		{
			"GhostNormal", "GhostFast", "Boomer1", "Boomer2", "Boomer3",
			"FrozenGhost", "FrozenGhostBoomer", "BoomerFrozen",
			"GhostBoomer1", "GhostBoomer2", "GhostBoomer3", "HumanoidSkeleton"
		};

		static ComponentZombieAI()
		{
			m_firearmConfigs = new Dictionary<int, FirearmDefConfig>();
			void Add(Type weaponType, Type bulletType, string sound, double fireRate, float bulletSpeed, int projPerShot, Vector3 spread, int maxShots = 30, bool sniper = false, bool automatic = false)
			{
				int idx = BlocksManager.GetBlockIndex(weaponType, true, false);
				m_firearmConfigs[idx] = new FirearmDefConfig
				{
					BulletBlockType = bulletType,
					ShootSound = sound,
					FireRate = (float)fireRate,
					BulletSpeed = bulletSpeed,
					ProjectilesPerShot = projPerShot,
					SpreadVector = spread,
					IsSniper = sniper,
					IsAutomatic = automatic,
					MaxShotsBeforeReload = maxShots
				};
			}

			Add(typeof(AK48Block), typeof(NuevaBala6), "Audio/Armas/AK48 fire", 0.17, 280f, 2, new Vector3(0.01f, 0.01f, 0.05f), 60, automatic: true);
			Add(typeof(Master308Block), typeof(NuevaBala4), "Audio/Armas/308 Master fire", 0.48, 300f, 1, new Vector3(0.001f, 0.001f, 0.001f), 8);
			Add(typeof(BK43Block), typeof(NuevaBala3), "Audio/Armas/bk 43", 1.5, 280f, 8, new Vector3(0.1f, 0.1f, 0.03f), 2);
			Add(typeof(AKBlock), typeof(NuevaBala2), "Audio/Armas/ak 47 fuego", 0.17, 280f, 2, new Vector3(0.01f, 0.01f, 0.05f), 30, automatic: true);
			Add(typeof(M4Block), typeof(NuevaBala2), "Audio/Armas/M4 fuego", 0.15, 300f, 3, new Vector3(0.008f, 0.008f, 0.04f), 22, automatic: true);
			Add(typeof(KABlock), typeof(NuevaBala5), "Audio/Armas/KA fuego", 0.1, 320f, 3, new Vector3(0.007f, 0.007f, 0.03f), 40, automatic: true);
			Add(typeof(Mac10Block), typeof(NuevaBala3), "Audio/Armas/mac 10 fuego", 0.1, 300f, 1, new Vector3(0.012f, 0.012f, 0.035f), 30, automatic: true);
			Add(typeof(SWM500Block), typeof(NuevaBala4), "Audio/Armas/desert eagle fuego", 0.5, 320f, 1, new Vector3(0.02f, 0.02f, 0.05f), 5);
			Add(typeof(G3Block), typeof(NuevaBala), "Audio/Armas/FX05", 0.12, 290f, 2, new Vector3(0.009f, 0.009f, 0.04f), 30, automatic: true);
			Add(typeof(Izh43Block), typeof(NuevaBala), "Audio/Armas/shotgun fuego", 1.0, 280f, 8, new Vector3(0.09f, 0.09f, 0.09f), 2);
			Add(typeof(MinigunBlock), typeof(NuevaBala6), "Audio/Armas/Chaingun fuego", 0.08, 260f, 1, new Vector3(0.02f, 0.02f, 0.08f), 100, automatic: true);
			Add(typeof(SPAS12Block), typeof(NuevaBala), "Audio/Armas/SPAS 12 fuego", 0.8, 280f, 8, new Vector3(0.09f, 0.09f, 0.09f), 8);
			Add(typeof(UziBlock), typeof(NuevaBala2), "Audio/Armas/Uzi fuego", 0.08, 320f, 2, new Vector3(0.015f, 0.015f, 0.06f), 30, automatic: true);
			Add(typeof(SniperBlock), typeof(NuevaBala6), "Audio/Armas/Sniper fuego", 2.0, 450f, 1, new Vector3(0.001f, 0.001f, 0.001f), 1, sniper: true);
			Add(typeof(AUGBlock), typeof(NuevaBala), "Audio/Armas/AUG fuego", 0.17, 280f, 2, new Vector3(0.01f, 0.01f, 0.05f), 30, automatic: true);
			Add(typeof(P90Block), typeof(NuevaBala4), "Audio/Armas/FN P90 fuego", 0.067, 320f, 1, new Vector3(0.012f, 0.012f, 0.04f), 50, automatic: true);
			Add(typeof(SCARBlock), typeof(NuevaBala3), "Audio/Armas/FN Scar fuego", 0.1, 310f, 1, new Vector3(0.01f, 0.01f, 0.03f), 30, automatic: true);
			Add(typeof(RevolverBlock), typeof(NuevaBala4), "Audio/Armas/Revolver fuego", 0.6, 320f, 1, new Vector3(0.02f, 0.02f, 0.05f), 6);
			Add(typeof(FamasBlock), typeof(NuevaBala4), "Audio/Armas/FAMAS fuego", 0.09, 450f, 1, new Vector3(0.012f, 0.012f, 0.04f), 30, automatic: true);
			Add(typeof(AA12Block), typeof(NuevaBala6), "Audio/Armas/AA12 fuego", 0.2, 350f, 8, new Vector3(0.03f, 0.03f, 0.06f), 20, automatic: true);
			Add(typeof(M249Block), typeof(NuevaBala5), "Audio/Armas/M249 fuego", 0.08, 400f, 1, new Vector3(0.01f, 0.01f, 0.01f), 100, automatic: true);
			Add(typeof(NewG3Block), typeof(NuevaBala3), "Audio/Armas/G3 fuego", 0.12, 290f, 2, new Vector3(0.009f, 0.009f, 0.04f), 30, automatic: true);
			Add(typeof(MP5SSDBlock), typeof(NuevaBala3), "Audio/Armas/MP5SSD fuego", 0.12, 290f, 2, new Vector3(0.009f, 0.009f, 0.04f), 30, automatic: true);
			Add(typeof(MendozaBlock), typeof(NuevaBala3), "Audio/Armas/Mendoza fuego", 0.12, 290f, 2, new Vector3(0.009f, 0.009f, 0.04f), 30, automatic: true);
			Add(typeof(GrozaBlock), typeof(NuevaBala3), "Audio/Armas/Groza fuego", 0.12, 290f, 2, new Vector3(0.009f, 0.009f, 0.04f), 30, automatic: true);
		}

		public override float ImportanceLevel => 100f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_canUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);
			m_canEquipClothing = valuesDictionary.GetValue<bool>("CanEquipClothing", false);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemGreenNightSky = base.Project.FindSubsystem<SubsystemGreenNightSky>(true);
			m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			m_inventory = m_componentMiner.Inventory;
			m_creatureModel = m_componentCreature.ComponentCreatureModel;
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_componentClothing = base.Entity.FindComponent<ComponentCreatureClothing>(false);
			m_chaseBehavior = base.Entity.FindComponent<ComponentZombieChaseBehavior>();
			m_pathfinding = base.Entity.FindComponent<ComponentPathfinding>();
			m_zombieRunAwayBehavior = base.Entity.FindComponent<ComponentZombieRunAwayBehavior>();
			if (m_zombieRunAwayBehavior != null)
			{
				// El componente ya tiene ImportanceLevel = 0f, pero lo forzamos a nunca activarse
				// No hacemos nada aquí, solo aseguramos que no cancele el apuntado
			}
			if (m_chaseBehavior == null)
			{
				Log.Warning("ComponentZombieAI: No se encontró ComponentZombieChaseBehavior. IA desactivada.");
			}
			m_subsystemProjectiles.ProjectileAdded += OnProjectileAdded;

			InitializeThrowableIndices();

			var greenNight = Project.FindSubsystem<SubsystemGreenNightSky>(true);
			if (greenNight != null)
			{
				m_currentDifficulty = greenNight.DifficultyMode;
				ApplyDifficultyToAI();
			}
		}

		private void ApplyDifficultyToAI()
		{
			if (m_subsystemGreenNightSky == null) return;
			DifficultyMode mode = m_subsystemGreenNightSky.DifficultyMode;
			if (mode == m_currentDifficulty) return;
			m_currentDifficulty = mode;

			m_flankingEnabled = DifficultyModifiers.ShouldUseFlanking(mode);

			// Ajustar rango de ataque según dificultad
			float rangeMult = DifficultyModifiers.GetAggressionRangeMultiplier(mode);
			AttackRange = new Vector2(AttackRange.X, AttackRange.Y * rangeMult);
			ThrowableRange = new Vector2(ThrowableRange.X, ThrowableRange.Y * rangeMult);
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

		private bool IsNormalHumanoid()
		{
			if (m_componentCreature == null) return false;
			string templateName = Entity.ValuesDictionary.DatabaseObject.Name;
			bool result = !string.IsNullOrEmpty(templateName) && s_normalCreatureNames.Contains(templateName);
			return result;
		}

		public virtual void Update(float dt)
		{
			// Si la celebración está activa, solo bloquear combate, pero permitir equipar ropa
			bool celebrationActive = AchievementsManager.IsCelebrationActive;

			if (m_subsystemGreenNightSky != null)
			{
				ApplyDifficultyToAI();
			}

			// Si estamos flanqueando, actualizar temporizador y salir
			if (m_isFlanking)
			{
				m_flankTimer -= dt;
				if (m_flankTimer <= 0f)
				{
					StopFlanking();
				}
				return;
			}

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

			// Si la celebración está activa, NO hacer nada de combate después de esto
			if (celebrationActive) return;

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
			int activeCount = m_inventory.GetSlotCount(m_inventory.ActiveSlotIndex);

			float distToTarget = Vector3.Distance(m_componentBody.Position, target.ComponentBody.Position);

			// Si el slot activo está vacío, equipar el mejor arma según la distancia
			if (activeValue == 0 || activeCount == 0)
			{
				EquipBestWeaponForCurrentDistance(distToTarget);
				activeValue = m_inventory.GetSlotValue(m_inventory.ActiveSlotIndex);
				if (activeValue == 0) return;
			}

			int activeBlockIndex = Terrain.ExtractContents(activeValue);
			Block activeBlock = BlocksManager.Blocks[activeBlockIndex];
			bool isThrowable = IsThrowableBlock(activeBlockIndex);
			bool isRanged = activeBlock is MusketBlock ||
							activeBlock is CrossbowBlock ||
							activeBlock is RepeatCrossbowBlock ||
							activeBlock is BowBlock ||
							activeBlock is FlameThrowerBlock ||
							activeBlock is DoubleMusketBlock ||
							activeBlock is ItemsLauncherBlock ||
							m_firearmConfigs.ContainsKey(activeBlockIndex);
			bool isMelee = !isRanged && !isThrowable && activeBlock.GetMeleePower(activeValue) > 0f;

			float meleeDist = GetMeleeDistanceToTarget(target.ComponentBody);

			// ========== SOLO LOS LANZABLES VERIFICAN LÍNEA DE VISIÓN ==========
			bool hasLOS = true; // Por defecto, las armas no lanzables NO verifican visibilidad
			bool isStuck = (m_pathfinding != null && m_pathfinding.IsStuck);

			// No detener apuntado si estamos flanqueando
			if (!m_isFlanking && (isThrowable && (!HasLineOfSightToTarget(target) || isStuck)) && m_isAiming)
			{
				StopAiming();
			}
			// Para armas no lanzables, solo detener apuntado si está atascado
			else if (!isThrowable && isStuck && m_isAiming)
			{
				StopAiming();
			}

			// ========== MANEJO DE LANZABLES (rango específico) - CON VISIBILIDAD ==========
			bool throwableHasLOS = HasLineOfSightToTarget(target);
			if (isThrowable && distToTarget >= ThrowableRange.X && distToTarget <= ThrowableRange.Y && throwableHasLOS && !isStuck)
			{
				StopMovement();
				PerformThrowableAttack(dt, target.ComponentBody.Position);
				return;
			}
			else if (isThrowable && (!throwableHasLOS || isStuck))
			{
				if (m_flankingEnabled && !m_isFlanking)
				{
					StartFlanking(target.ComponentBody.Position);
				}
				else if (!m_flankingEnabled)
				{
					// Sin flanqueo: simplemente esperar o moverse aleatoriamente
					if (m_pathfinding != null && m_pathfinding.Destination == null)
					{
						Vector3 randomDir = new Vector3(m_random.Float(-1f, 1f), 0f, m_random.Float(-1f, 1f));
						if (randomDir.LengthSquared() > 0.01f)
						{
							randomDir = Vector3.Normalize(randomDir);
							Vector3 destination = m_componentBody.Position + randomDir * 5f;
							m_pathfinding.SetDestination(destination, 1f, 1f, 50, false, true, false, null);
						}
					}
				}
				return;
			}
			else if (!isThrowable && distToTarget >= ThrowableRange.X && distToTarget <= ThrowableRange.Y && HasThrowableInInventory() && throwableHasLOS && !isStuck)
			{
				EquipBestThrowableWeapon();
				StopAiming();
				return;
			}

			// ========== CAMBIO DE ARMA SEGÚN DISTANCIA ==========
			// Dentro del rango mínimo: intentar equipar arma cuerpo a cuerpo si no la tenemos
			if (meleeDist <= AttackRange.X)
			{
				if (!isMelee && TryEquipBestMeleeWeapon())
				{
					return; // Cambió de arma, esperar próximo frame
				}
			}
			// Dentro del rango máximo (pero fuera del mínimo) y el arma actual es melee: cambiar a distancia
			else if (distToTarget <= AttackRange.Y)
			{
				if (isMelee)
				{
					EquipBestRangedWeapon();
					return;
				}
			}

			// ========== COMBATE A DISTANCIA - SIN VERIFICACIÓN DE VISIBILIDAD (excepto lanzables ya manejados) ==========
			// Si el arma actual es a distancia (no lanzable), no verificamos línea de visión
			// Solo verificamos que no estemos atascados y que estemos dentro del rango
			if (isRanged && !isThrowable && !isStuck && distToTarget <= AttackRange.Y)
			{
				UpdateRangedCombat(dt, target.ComponentBody.Position);
				return;
			}
			// Si es arma de fuego o similar, también entra aquí (isRanged=true, isThrowable=false)

			// ========== MANEJO DE LANZABLES FUERA DE SU RANGO ÓPTIMO ==========
			if (isThrowable && distToTarget <= AttackRange.Y)
			{
				if (distToTarget > ThrowableRange.Y && throwableHasLOS && !isStuck)
				{
					EquipBestRangedWeapon();
				}
				else if (distToTarget < ThrowableRange.X && throwableHasLOS && !isStuck)
				{
					TryEquipBestMeleeWeapon();
				}
				return;
			}

			// Si estamos fuera del rango máximo, detener apuntado
			if (distToTarget > AttackRange.Y)
			{
				StopAiming();
			}
		}

		// Método auxiliar para equipar el mejor arma según la distancia actual
		private void EquipBestWeaponForCurrentDistance(float distToTarget)
		{
			if (m_chaseBehavior == null || m_chaseBehavior.m_target == null) return;

			float meleeDist = GetMeleeDistanceToTarget(m_chaseBehavior.m_target.ComponentBody);
			if (meleeDist <= AttackRange.X)
			{
				if (TryEquipBestMeleeWeapon())
					return;
			}

			if (distToTarget >= ThrowableRange.X && distToTarget <= ThrowableRange.Y && HasThrowableInInventory())
			{
				EquipBestThrowableWeapon();
				return;
			}

			EquipBestRangedWeapon();
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
			// ========== IMPORTANTE: Para armas NO lanzables, NO verificamos línea de visión ==========
			// Solo verificamos que no estemos atascados
			int activeValue = m_inventory.GetSlotValue(m_inventory.ActiveSlotIndex);
			int activeContents = Terrain.ExtractContents(activeValue);
			bool isThrowable = IsThrowableBlock(activeContents);

			// Si es lanzable, verificamos visibilidad (pero esto ya debería manejarse en el bloque de lanzables)
			if (isThrowable)
			{
				if (!HasLineOfSightToTarget(m_chaseBehavior.m_target) || (m_pathfinding != null && m_pathfinding.IsStuck))
				{
					StopAiming();
					return;
				}
			}
			else if (m_pathfinding != null && m_pathfinding.IsStuck)
			{
				StopAiming();
				return;
			}

			Block activeBlock = BlocksManager.Blocks[activeContents];

			// Detectar si es un arma de fuego moderna
			bool isFirearm = m_firearmConfigs.ContainsKey(activeContents);

			if (isFirearm)
			{
				FirearmDefConfig config = m_firearmConfigs[activeContents];

				if (m_isFirearmReloading)
				{
					m_firearmReloadTimer -= dt;
					if (m_firearmReloadTimer <= 0f)
					{
						m_isFirearmReloading = false;
						m_firearmShotsSinceReload = 0;
						SubsystemParticles particles = Project.FindSubsystem<SubsystemParticles>(true);
						if (particles != null && m_subsystemTerrain != null)
						{
							try
							{
								Vector3 basePosition = m_creatureModel.EyePosition;
								KillParticleSystem readyParticles = new KillParticleSystem(m_subsystemTerrain, basePosition, 0.5f);
								particles.AddParticleSystem(readyParticles, false);
								for (int i = 0; i < 3; i++)
								{
									Vector3 offset = new Vector3(m_random.Float(-0.2f, 0.2f), m_random.Float(0.1f, 0.4f), m_random.Float(-0.2f, 0.2f));
									KillParticleSystem additionalParticles = new KillParticleSystem(m_subsystemTerrain, basePosition + offset, 0.5f);
									particles.AddParticleSystem(additionalParticles, false);
								}
							}
							catch (Exception) { }
						}
						SubsystemAudio audio = Project.FindSubsystem<SubsystemAudio>(true);
						if (audio != null)
							audio.PlaySound("Audio/Armas/reload", 1f, 0f, m_creatureModel.EyePosition, 10f, true);
						ResetModelPose();
						m_isAiming = false;
						m_aimTimer = 0f;
						m_hasCompletedInitialAim = false;
						m_cooldownTimer = 0f;
					}
					else
					{
						if (m_creatureModel != null)
						{
							m_creatureModel.AimHandAngleOrder = 0f;
							m_creatureModel.InHandItemOffsetOrder = Vector3.Zero;
							m_creatureModel.InHandItemRotationOrder = Vector3.Zero;
							m_creatureModel.LookAtOrder = null;
						}
						if (m_isAiming) StopAiming();
					}
					return;
				}

				if (m_cooldownTimer > 0f)
				{
					m_cooldownTimer -= dt;
					if (m_cooldownTimer < 0f) m_cooldownTimer = 0f;
				}

				if (!m_isAiming && m_cooldownTimer <= 0f)
				{
					m_isAiming = true;
					m_aimTimer = 0f;
					m_hasCompletedInitialAim = false;
				}

				if (m_isAiming)
				{
					Vector3 eyePos = m_creatureModel.EyePosition;
					Vector3 aimDir = Vector3.Normalize(targetPos + new Vector3(0f, 1f, 0f) - eyePos);
					Ray3 aimRay = new Ray3(eyePos, aimDir);
					float aimDuration = config.IsSniper ? 1.0f : 0.5f;

					// Animación manual (sin Miner.Aim)
					bool isNormal = IsNormalHumanoid();
					if (m_creatureModel != null)
					{
						if (!isNormal)
						{
							// Criaturas NO normales (zombis especiales): NO levantan el brazo
							m_creatureModel.AimHandAngleOrder = 0f;
							m_creatureModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.15f, 0.25f);
							m_creatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
						}
						else
						{
							// Criaturas normales (humanoides): levantan el brazo
							if (config.IsSniper)
							{
								m_creatureModel.AimHandAngleOrder = 1.2f;
								m_creatureModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.06f, 0.08f);
								m_creatureModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
							}
							else
							{
								m_creatureModel.AimHandAngleOrder = 1.4f;
								m_creatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
								m_creatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
							}
						}
						if (m_chaseBehavior != null && m_chaseBehavior.m_target != null)
							m_creatureModel.LookAtOrder = m_chaseBehavior.m_target.ComponentCreatureModel.EyePosition;
					}

					if (!m_hasCompletedInitialAim)
					{
						m_aimTimer += dt;
						if (m_aimTimer >= aimDuration)
						{
							FireFirearm(aimRay, config);
							m_lastFirearmShotTime = m_subsystemTime.GameTime;
							m_hasCompletedInitialAim = true;
							m_aimTimer = aimDuration;
							if (m_firearmShotsSinceReload >= config.MaxShotsBeforeReload)
								StartFirearmReload();
						}
					}
					else
					{
						double currentTime = m_subsystemTime.GameTime;
						double timeSinceLastShot = currentTime - m_lastFirearmShotTime;
						if (timeSinceLastShot >= config.FireRate - 0.0001f && currentTime != m_lastFirearmShotTime)
						{
							FireFirearm(aimRay, config);
							m_lastFirearmShotTime = currentTime;
							if (m_firearmShotsSinceReload >= config.MaxShotsBeforeReload)
								StartFirearmReload();
						}
					}
				}
				return;
			}

			// ========== ITEMSLAUNCHER - MANEJO COMPLETAMENTE SEPARADO SIN MINER.AIM ==========
			if (activeBlock is ItemsLauncherBlock)
			{
				if (m_cooldownTimer > 0f)
					m_cooldownTimer -= dt;

				if (!m_isAiming && m_cooldownTimer <= 0f)
				{
					m_isAiming = true;
					m_aimTimer = 0f;
				}

				if (m_isAiming)
				{
					m_aimTimer += dt;
					Vector3 eyePos = m_creatureModel.EyePosition;
					Vector3 dir = Vector3.Normalize(targetPos + new Vector3(0f, 1f, 0f) - eyePos);

					bool isNormal = IsNormalHumanoid();

					if (m_aimTimer < ItemsLauncherAimTime)
					{
						// SOLO animaciones manuales, SIN llamar a miner.Aim
						if (isNormal && m_creatureModel != null)
						{
							// Imitar animación de apuntado para humanoides (como lo hace SubsystemItemsLauncherBlockBehavior)
							m_creatureModel.AimHandAngleOrder = 1.4f;
							m_creatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
							m_creatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
							// Hacer que mire al objetivo
							if (m_chaseBehavior != null && m_chaseBehavior.m_target != null)
								m_creatureModel.LookAtOrder = m_chaseBehavior.m_target.ComponentCreatureModel.EyePosition;
						}
						else if (m_creatureModel != null)
						{
							// Animación para zombis no humanoides (sin rotación de cabeza hacia el objetivo)
							m_creatureModel.AimHandAngleOrder = 0f;
							m_creatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
							m_creatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
						}
					}
					else
					{
						// Disparar manualmente SIN usar miner.Aim
						FireItemsLauncherManually(eyePos, dir);

						m_isAiming = false;
						m_cooldownTimer = ItemsLauncherCooldown;
						m_aimTimer = 0f;
						ResetModelPose();
					}
				}
				return; // Salir antes de llegar al código de armas clásicas
			}
			// ========== FIN ITEMSLAUNCHER ==========

			// Armas clásicas (mosquete, ballesta, etc.) - EXCLUYENDO ItemsLauncher
			bool isMusket = activeBlock is MusketBlock;
			bool isCrossbow = activeBlock is CrossbowBlock;
			bool isRepeatCrossbow = activeBlock is RepeatCrossbowBlock;
			bool isBow = activeBlock is BowBlock;
			bool isFlameThrower = activeBlock is FlameThrowerBlock;
			bool isDoubleMusket = activeBlock is DoubleMusketBlock;
			// ItemsLauncher ya fue manejado arriba, ya no se incluye aquí

			if (!isMusket && !isCrossbow && !isRepeatCrossbow && !isBow && !isFlameThrower && !isDoubleMusket)
				return;

			float aimTimeValue = 0f, cooldown = 0f;
			if (isMusket) { aimTimeValue = MusketAimTime; cooldown = MusketCooldown; }
			else if (isCrossbow) { aimTimeValue = CrossbowAimTime; cooldown = CrossbowCooldown; }
			else if (isRepeatCrossbow) { aimTimeValue = RepeatCrossbowAimTime; cooldown = RepeatCrossbowCooldown; }
			else if (isBow) { aimTimeValue = BowAimTime; cooldown = BowCooldown; }
			else if (isFlameThrower) { aimTimeValue = FlameThrowerAimTime; cooldown = FlameThrowerCooldown; }
			else if (isDoubleMusket) { aimTimeValue = DoubleMusketAimTime; cooldown = DoubleMusketCooldown; }

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

				bool isNormal = IsNormalHumanoid();

				if (m_aimTimer < aimTimeValue)
				{
					m_componentMiner.Aim(aimRay, AimState.InProgress);
					if (!isNormal && m_creatureModel != null)
					{
						m_creatureModel.AimHandAngleOrder = 0f;
						if (isMusket || isDoubleMusket || isFlameThrower)
						{
							m_creatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
							m_creatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
						}
						else if (isCrossbow || isRepeatCrossbow)
						{
							m_creatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
							m_creatureModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);
						}
						else if (isBow)
						{
							m_creatureModel.InHandItemOffsetOrder = Vector3.Zero;
							m_creatureModel.InHandItemRotationOrder = new Vector3(0f, -0.2f, 0f);
						}
					}
				}
				else
				{
					m_componentMiner.Aim(aimRay, AimState.Completed);
					m_isAiming = false;
					m_cooldownTimer = cooldown;
					m_aimTimer = 0f;
				}
			}
		}

		private void FireFirearm(Ray3 aimRay, FirearmDefConfig config)
		{
			Vector3 eyePos = aimRay.Position;
			Vector3 direction = aimRay.Direction;
			Vector3 muzzlePos = eyePos + m_componentBody.Matrix.Right * 0.3f - m_componentBody.Matrix.Up * 0.2f;
			Vector3 dirNorm = Vector3.Normalize(muzzlePos + direction * 10f - muzzlePos);
			Vector3 right = Vector3.Normalize(Vector3.Cross(dirNorm, Vector3.UnitY));
			Vector3 up = Vector3.Normalize(Vector3.Cross(dirNorm, right));

			for (int i = 0; i < config.ProjectilesPerShot; i++)
			{
				Vector3 spread = m_random.Float(-config.SpreadVector.X, config.SpreadVector.X) * right
							   + m_random.Float(-config.SpreadVector.Y, config.SpreadVector.Y) * up
							   + m_random.Float(-config.SpreadVector.Z, config.SpreadVector.Z) * dirNorm;
				int bulletBlockIndex = BlocksManager.GetBlockIndex(config.BulletBlockType, true, false);
				int bulletValue;
				if (config.IsSniper)
				{
					bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, 180);
				}
				else
				{
					bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, 0);
				}
				Vector3 velocity = m_componentCreature.ComponentBody.Velocity + config.BulletSpeed * (dirNorm + spread);
				m_subsystemProjectiles.FireProjectile(bulletValue, muzzlePos, velocity, Vector3.Zero, m_componentCreature);
			}

			SubsystemAudio audio = Project.FindSubsystem<SubsystemAudio>(true);
			if (audio != null)
				audio.PlaySound(config.ShootSound, 1f, m_random.Float(-0.1f, 0.1f), eyePos, 15f, false);

			SubsystemParticles particles = Project.FindSubsystem<SubsystemParticles>(true);
			if (particles != null && m_subsystemTerrain != null)
				particles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, muzzlePos + dirNorm * 0.5f, dirNorm), false);

			m_componentBody.ApplyImpulse(-1f * dirNorm);
			m_firearmShotsSinceReload++;
		}

		private void StartFirearmReload()
		{
			m_isFirearmReloading = true;
			m_firearmReloadTimer = FirearmReloadTime;
			m_isAiming = false;
			m_hasCompletedInitialAim = false;

			SubsystemParticles particles = Project.FindSubsystem<SubsystemParticles>(true);
			if (particles != null && m_subsystemTerrain != null)
			{
				try
				{
					Vector3 basePosition = m_componentBody.Position + new Vector3(0f, 1f, 0f);
					KillParticleSystem reloadParticles = new KillParticleSystem(m_subsystemTerrain, basePosition, 0.5f);
					particles.AddParticleSystem(reloadParticles, false);
					for (int i = 0; i < 3; i++)
					{
						Vector3 offset = new Vector3(m_random.Float(-0.2f, 0.2f), m_random.Float(0.1f, 0.4f), m_random.Float(-0.2f, 0.2f));
						KillParticleSystem additionalParticles = new KillParticleSystem(m_subsystemTerrain, basePosition + offset, 0.5f);
						particles.AddParticleSystem(additionalParticles, false);
					}
				}
				catch (Exception) { }
			}

			SubsystemAudio audio = Project.FindSubsystem<SubsystemAudio>(true);
			if (audio != null)
				audio.PlaySound("Audio/Armas/reload", 0.8f, 0f, m_creatureModel.EyePosition, 10f, true);

			ResetModelPose();
		}

		private void PerformThrowableAttack(float dt, Vector3 targetPos)
		{
			// Los lanzables ya verificaron visibilidad antes de entrar aquí
			if (m_pathfinding != null && m_pathfinding.IsStuck)
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
				int activeValue = m_inventory.GetSlotValue(m_inventory.ActiveSlotIndex);
				int activeContents = Terrain.ExtractContents(activeValue);
				// Solo cancelar con Miner.Aim si NO es arma de fuego
				if (!m_firearmConfigs.ContainsKey(activeContents))
				{
					Vector3 eyePos = m_creatureModel.EyePosition;
					m_componentMiner.Aim(new Ray3(eyePos, Vector3.UnitZ), AimState.Cancelled);
				}
				m_isAiming = false;
				m_aimTimer = 0f;
				ResetModelPose();
			}
		}

		private void StartAiming()
		{
			int activeValue = m_inventory.GetSlotValue(m_inventory.ActiveSlotIndex);
			Block activeBlock = BlocksManager.Blocks[Terrain.ExtractContents(activeValue)];

			// Cargar armas clásicas si es necesario
			if (activeBlock is MusketBlock)
			{
				int data = Terrain.ExtractData(activeValue);
				if (MusketBlock.GetLoadState(data) != MusketBlock.LoadState.Loaded)
					ReloadMusketInstantly();
			}
			else if (activeBlock is CrossbowBlock)
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
			// ItemsLauncher: NO llamar a ReloadItemsLauncherInstantly() aquí
			// El disparo se maneja completamente en UpdateRangedCombat sin usar miner.Aim()
			// Las armas de fuego modernas tampoco necesitan recarga manual aquí

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
			// Priorizar armas de fuego modernas
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int slotValue = m_inventory.GetSlotValue(i);
				if (m_inventory.GetSlotCount(i) > 0 && m_firearmConfigs.ContainsKey(Terrain.ExtractContents(slotValue)))
				{
					m_inventory.ActiveSlotIndex = i;
					return;
				}
			}
			// Luego mosquetes
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
					block is FlameThrowerBlock || block is DoubleMusketBlock || block is ItemsLauncherBlock || IsThrowableBlock(blockIndex) || m_firearmConfigs.ContainsKey(blockIndex))
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

		/// <summary>
		/// Dispara el ItemsLauncher manualmente sin usar miner.Aim, solo dispara MusketBall
		/// </summary>
		private void FireItemsLauncherManually(Vector3 eyePos, Vector3 aimDirection)
		{
			Vector3 muzzlePos = eyePos + m_componentBody.Matrix.Right * 0.3f - m_componentBody.Matrix.Up * 0.2f;
			Vector3 dirNorm = Vector3.Normalize(muzzlePos + aimDirection * 10f - muzzlePos);

			// SIEMPRE usar MusketBall como munición principal
			int bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false, false);
			if (bulletBlockIndex <= 0) return;

			int bulletData = BulletBlock.SetBulletType(0, BulletBlock.BulletType.MusketBall);
			int bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, bulletData);

			float speed = 100f;
			Vector3 velocity = m_componentCreature.ComponentBody.Velocity + speed * dirNorm;

			// Disparar el proyectil
			m_subsystemProjectiles.FireProjectile(bulletValue, muzzlePos, velocity, Vector3.Zero, m_componentCreature);

			// Reproducir sonido
			SubsystemAudio audio = Project.FindSubsystem<SubsystemAudio>(true);
			if (audio != null)
			{
				audio.PlaySound("Audio/Items/ItemLauncher/Item Cannon Fire", 1f,
					m_random.Float(-0.1f, 0.1f), eyePos, 10f, true);
			}

			// Efecto de humo del cañón
			SubsystemParticles particles = Project.FindSubsystem<SubsystemParticles>(true);
			if (particles != null && m_subsystemTerrain != null)
			{
				particles.AddParticleSystem(
					new GunSmokeParticleSystem(m_subsystemTerrain, muzzlePos + 0.3f * dirNorm, dirNorm),
					false
				);
			}

			// Retroceso
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

		private void StartFlanking(Vector3 targetPos)
		{
			m_isFlanking = true;
			m_flankTimer = 2.0f;

			Vector3 toTarget = targetPos - m_componentBody.Position;
			Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, toTarget));
			float randomSign = m_random.Float(-1f, 1f) > 0 ? 1f : -1f;
			m_flankDirection = right * randomSign;

			StopAiming();

			// Desactivar chase behavior
			if (m_chaseBehavior != null)
			{
				m_chaseBehavior.Suppressed = true;
			}

			Vector3 flankTarget = m_componentBody.Position + m_flankDirection * 5f;
			if (m_pathfinding != null)
			{
				m_pathfinding.SetDestination(flankTarget, 3f, 1f, 50, false, false, false, null);
			}
		}

		private void StopFlanking()
		{
			m_isFlanking = false;
			m_flankTimer = 0f;
			m_flankDirection = Vector3.Zero;
			if (m_pathfinding != null) m_pathfinding.Stop();

			// Reactivar chase behavior
			if (m_chaseBehavior != null)
			{
				m_chaseBehavior.Suppressed = false;
				if (m_chaseBehavior.m_target != null && m_chaseBehavior.m_target.ComponentHealth.Health > 0f)
				{
					m_chaseBehavior.StopAttack();
					m_chaseBehavior.Attack(m_chaseBehavior.m_target, 100f, 10f, false);
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
