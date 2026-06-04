using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentDefensiveCreatureAI : Component, IUpdateable
	{
		public bool CanUseInventory = false;
		public bool CanEquipClothing = false;
		public bool CanBeMounted = false;
		public Vector2 RangedAttackRange = new Vector2(5f, 100f);

		// Rangos para objetos lanzables
		public Vector2 ThrowableAttackRange = new Vector2(5f, 15f);

		// Rango de seguridad para virotes explosivos (X = distancia mínima para usar explosivos, Y = distancia máxima de ataque)
		public Vector2 ExplosiveSafeRange = new Vector2(20f, 100f);

		// Tiempos del mosquete
		private float MusketAimTime = 1.5f;
		private float MusketCooldown = 0.02f;

		// Tiempos de la ballesta
		private float CrossbowAimTime = 1.5f;
		private float CrossbowCooldown = 0.01f;

		// Tiempos del arco
		private float BowAimTime = 1.5f;
		private float BowCooldown = 0.01f;

		// Tiempos para el lanzador de ítems
		private float ItemsLauncherAimTime = 1.5f;
		private float ItemsLauncherCooldown = 0.02f;

		// Tiempos para la ballesta repetidora
		private float RepeatCrossbowAimTime = 1.5f;
		private float RepeatCrossbowCooldown = 0.01f;

		// Tiempos para el lanzallamas
		private float FlameThrowerAimTime = 1.5f;
		private float FlameThrowerCooldown = 0.01f;

		// Tiempos para el mosquete de doble cañón
		private float DoubleMusketAimTime = 1.5f;
		private float DoubleMusketCooldown = 0.02f;

		// Tiempos para lanzables
		private float ThrowableAimTime = 1.55f;
		private float ThrowableCooldown = 0.01f;

		private ComponentRider m_componentRider;

		private enum MountState { None, Searching, Mounting }
		private MountState m_mountState = MountState.None;
		private float m_mountTimer = 0f;
		private ComponentMount m_targetMount = null;
		private float m_mountSearchCooldown = 0f;
		private bool m_mountedCombatActive = false;

		// Lista de nombres de plantillas de criaturas montables (ANTIGUAS + NUEVAS)
		private static readonly HashSet<string> MountableCreatureTemplates = new HashSet<string>
{
    // Monturas antiguas
    "Horse_Black_Saddled",
	"Horse_Palomino_Saddled",
	"Camel_Saddled",
	"Horse_Chestnut_Saddled",
	"Horse_White_Saddled",
	"Donkey_Saddled",
	"Horse_Bay_Saddled",
    // Monturas nuevas
    "InfectedFlyTamed1",
	"InfectedBearTamed",
	"FlyingInfectedBossTamed"
};

		private const float MountNearDistance = 1.2f;   // Distancia para comenzar a montar

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTime m_subsystemTime;
		private ComponentMiner m_componentMiner;
		private ComponentNewChaseBehavior m_componentChase;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentCreatureClothing m_componentCreatureClothing;
		private ComponentDefensiveRunAwayBehavior m_componentDefensiveRunAway;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemNoise m_subsystemNoise;
		private Random m_random = new Random();

		private bool m_isAiming;
		private float m_aimTimer;
		private float m_cooldownTimer;

		private float m_flankTimer;
		private Vector3 m_flankDirection;
		private bool m_isFlanking;

		// Apuntado personalizado para criaturas que no levantan el brazo
		private bool m_isCustomAiming;
		private float m_customAimTimer;
		private int m_customWeaponContents;

		// Equipamiento de ropa
		private float m_clothingEquipTimer;
		private bool m_clothingEquipPending;
		private int m_pendingClothingMinerSlot;
		private int m_pendingClothingValue;
		private int m_pendingClothingSlotIndex;

		// Conjunto de índices de bloques lanzables
		private HashSet<int> m_throwableIndices = new HashSet<int>();

		// Rotaciones base que el zombie usa para apuntar cada arma (en radianes)
		private static readonly Dictionary<int, Vector3> BaseWeaponRotations = new Dictionary<int, Vector3>
		{
			{ MusketBlock.Index, new Vector3(-1.7f, 0f, 0f) },
			{ CrossbowBlock.Index, new Vector3(-1.55f, 0f, 0f) },
			{ BowBlock.Index, new Vector3(0f, -0.2f, 0f) },
			{ ItemsLauncherBlock.Index, new Vector3(-1.7f, 0f, 0f) },
			{ RepeatCrossbowBlock.Index, new Vector3(-1.55f, 0f, 0f) },
			{ FlameThrowerBlock.Index, new Vector3(-1.7f, 0f, 0f) },
			{ DoubleMusketBlock.Index, new Vector3(-1.7f, 0f, 0f) }
		};

		// ========== CONFIGURACIÓN INTERNA DE ARMAS DE FUEGO ==========
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

		private static readonly Dictionary<int, FirearmDefConfig> FirearmDefensiveConfigs = new Dictionary<int, FirearmDefConfig>();

		static ComponentDefensiveCreatureAI()
		{
			void Add(Type weaponType, Type bulletType, string sound, double fireRate, float bulletSpeed, int projPerShot, Vector3 spread, int maxShots = 30, bool sniper = false, bool automatic = false)
			{
				int idx = BlocksManager.GetBlockIndex(weaponType, true, false);
				FirearmDefensiveConfigs[idx] = new FirearmDefConfig
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
		// ========== FIN CONFIGURACIÓN ARMAS DE FUEGO ==========

		// Estado para armas automáticas
		private bool m_hasCompletedInitialAim = false;
		private double m_lastFirearmShotTime;

		// Sistema de recarga
		private bool m_isFirearmReloading = false;
		private float m_firearmReloadTimer = 0f;
		private int m_firearmShotsSinceReload = 0;
		private const float FirearmReloadTime = 1.0f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_componentRider = Entity.FindComponent<ComponentRider>();

			// NUEVO: Si ya estaba montado al cargar, restaurar el estado de combate montado
			if (m_componentRider != null && m_componentRider.Mount != null)
			{
				m_mountedCombatActive = true;
				if (m_componentChase != null) m_componentChase.Suppressed = false;
			}

			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentChase = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentCreatureClothing = Entity.FindComponent<ComponentCreatureClothing>();
			m_componentDefensiveRunAway = Entity.FindComponent<ComponentDefensiveRunAwayBehavior>();
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);
			CanEquipClothing = valuesDictionary.GetValue<bool>("CanEquipClothing", false);
			CanBeMounted = valuesDictionary.GetValue<bool>("CanBeMounted", false);

			// Inicializar lista de bloques lanzables
			InitializeThrowableIndices();
		}

		// Buscar la montura más cercana (sin temporizador, evaluación inmediata)
		private ComponentMount FindNearestMount(float maxDistance)
		{
			if (m_subsystemBodies == null || m_componentCreature == null)
				return null;

			DynamicArray<ComponentBody> bodies = new DynamicArray<ComponentBody>();
			Vector2 center = new Vector2(m_componentCreature.ComponentBody.Position.X, m_componentCreature.ComponentBody.Position.Z);
			m_subsystemBodies.FindBodiesAroundPoint(center, maxDistance, bodies);

			ComponentMount bestMount = null;
			float bestScore = -1f;

			for (int i = 0; i < bodies.Count; i++)
			{
				ComponentBody body = bodies.Array[i];
				if (body == null || body.Entity == null) continue;

				ComponentMount mount = body.Entity.FindComponent<ComponentMount>();
				if (mount == null) continue;

				string templateName = mount.Entity.ValuesDictionary.DatabaseObject.Name;
				if (!MountableCreatureTemplates.Contains(templateName)) continue;

				ComponentHealth health = mount.Entity.FindComponent<ComponentHealth>();
				if (health != null && health.Health <= 0f) continue;
				if (mount.Rider != null) continue;

				// Calcular distancia al punto de montura (con offset)
				Vector3 mountOffsetPos = mount.ComponentBody.Position + Vector3.Transform(mount.MountOffset, mount.ComponentBody.Rotation);
				Vector3 toMount = mountOffsetPos - m_componentCreature.ComponentCreatureModel.EyePosition;
				float distance = toMount.Length();
				if (distance < maxDistance)
				{
					float score = maxDistance - distance; // más cerca = mejor
					if (score > bestScore)
					{
						bestScore = score;
						bestMount = mount;
					}
				}
			}
			return bestMount;
		}

		private void StartMountingAttempt(ComponentMount mount)
		{
			if (mount == null) return;
			if (m_componentRider == null) return;

			m_mountState = MountState.Mounting;
			m_mountTimer = 0.55f;
			m_targetMount = mount;
			CancelAiming();               // Cancelar cualquier apuntado en curso
			m_componentPathfinding.Stop(); // Detener pathfinding
		}

		private void CancelMounting()
		{
			m_mountState = MountState.None;
			m_mountTimer = 0f;
			m_targetMount = null;
			m_mountedCombatActive = false;
		}

		private void UpdateMounting(float dt)
		{
			if (!CanBeMounted || m_componentRider == null) return;
			if (m_componentRider.Mount != null) return;      // Ya montado
			if (m_componentCreature.ComponentHealth.Health <= 0f) return;

			// Buscar montura periódicamente
			m_mountSearchCooldown -= dt;
			if (m_mountSearchCooldown <= 0f)
			{
				m_mountSearchCooldown = 0.2f;
				ComponentMount nearestMount = FindNearestMount(4f); // radio 4 bloques
				if (nearestMount != null)
				{
					m_componentRider.StartMounting(nearestMount);
					m_mountedCombatActive = true;
					m_componentPathfinding.Stop();
				}
			}
		}

		private void HandleMountedCombat(float dt)
		{
			if (m_componentRider == null || m_componentRider.Mount == null)
			{
				// Si perdimos la montura, desactivar el flag de combate montado
				if (m_mountedCombatActive) m_mountedCombatActive = false;
				return;
			}

			ComponentMount mountComp = m_componentRider.Mount;
			Entity mountEntity = mountComp.Entity;

			// Intentar obtener ComponentNewSteedBehavior, si no, el base ComponentSteedBehavior
			ComponentSteedBehavior steedBase = mountEntity.FindComponent<ComponentNewSteedBehavior>();
			ComponentNewSteedBehavior newSteed = steedBase as ComponentNewSteedBehavior;
			if (newSteed == null) steedBase = mountEntity.FindComponent<ComponentSteedBehavior>();
			if (steedBase == null) return;

			ComponentCreature target = m_componentChase?.Target;
			if (target == null || target.ComponentHealth.Health <= 0f)
			{
				steedBase.SpeedOrder = 0;
				steedBase.TurnOrder = 0f;
				steedBase.JumpOrder = 0f;
				if (newSteed != null) newSteed.ExternalVerticalInput = 0f;
				return;
			}

			Vector3 mountPos = mountComp.ComponentBody.Position;
			Vector3 mountForward = mountComp.ComponentBody.Matrix.Forward;
			Vector3 targetPos = target.ComponentBody.BoundingBox.Center();
			Vector3 toTarget = targetPos - mountPos;
			toTarget.Y = 0f;
			if (toTarget.LengthSquared() < 0.01f) return;

			Vector3 dirToTarget = Vector3.Normalize(toTarget);

			float currentAngle = MathF.Atan2(mountForward.X, mountForward.Z);
			float targetAngle = MathF.Atan2(dirToTarget.X, dirToTarget.Z);
			float angleDifference = MathUtils.NormalizeAngle(targetAngle - currentAngle);
			float turn = -Math.Clamp(angleDifference / (MathF.PI / 2f), -0.5f, 0.5f);
			steedBase.TurnOrder = turn;

			float distance = toTarget.Length();
			float desiredSpeed = 0f;

			// Si el ángulo es grande (>30°), frenar para girar en el sitio
			if (MathF.Abs(angleDifference) > 0.5f)
			{
				desiredSpeed = 0f;
			}
			else
			{
				if (distance > RangedAttackRange.X)
					desiredSpeed = 1f;
				else
					desiredSpeed = 0.5f;
			}

			steedBase.SpeedOrder = Math.Sign(desiredSpeed);

			// ===== NUEVO: Control vertical para monturas voladoras =====
			if (newSteed != null)
			{
				// Verificar si la montura puede volar (FlySpeed > 0)
				ComponentLocomotion loco = mountEntity.FindComponent<ComponentLocomotion>();
				bool canFly = (loco != null && loco.FlySpeed > 0f);
				if (canFly)
				{
					float heightDiff = targetPos.Y - mountPos.Y;
					const float maxHeightDiff = 3f;
					float verticalInput = Math.Clamp(heightDiff / maxHeightDiff, -1f, 1f);
					if (Math.Abs(verticalInput) < 0.1f) verticalInput = 0f;
					newSteed.ExternalVerticalInput = verticalInput;
				}
				else
				{
					newSteed.ExternalVerticalInput = 0f;
				}
			}

			// Salto si está atascado
			if (m_componentPathfinding != null && m_componentPathfinding.IsStuck && m_random.Float(0f, 1f) < 0.02f)
				steedBase.JumpOrder = 1f;
			else
				steedBase.JumpOrder = 0f;
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
				   templateName == "InfectedMuscleTamed2" ||
				   templateName == "InfectedFreezerTamed";
		}

		public void Update(float dt)
		{
			// Si la celebración está activa, solo bloquear combate, pero permitir equipar ropa
			bool celebrationActive = AchievementsManager.IsCelebrationActive;

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

			// ===== LÓGICA DE EQUIPAMIENTO DE ROPA (INDEPENDIENTE) =====
			if (CanEquipClothing && m_componentCreatureClothing != null && !m_isAiming && !m_isCustomAiming && !m_isFirearmReloading)
			{
				if (m_clothingEquipPending)
				{
					m_clothingEquipTimer -= dt;
					if (m_clothingEquipTimer <= 0f)
					{
						IInventory minerInv = m_componentMiner.Inventory;
						int slotValue = minerInv.GetSlotValue(m_pendingClothingMinerSlot);
						if (slotValue == m_pendingClothingValue)
						{
							int processedCount;
							int processedValue;
							m_componentCreatureClothing.ProcessSlotItems(
								m_pendingClothingSlotIndex, m_pendingClothingValue, 1, 1,
								out processedValue, out processedCount);
							if (processedCount > 0)
							{
								minerInv.RemoveSlotItems(m_pendingClothingMinerSlot, 1);
							}
						}
						m_clothingEquipPending = false;
					}
				}
				else
				{
					IInventory minerInv = m_componentMiner.Inventory;
					for (int i = 0; i < minerInv.SlotsCount; i++)
					{
						int value = minerInv.GetSlotValue(i);
						if (value == 0) continue;
						Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
						if (!(block is ClothingBlock)) continue;
						ClothingData data = block.GetClothingData(value);
						if (data == null) continue;

						int clothingSlotIndex = -1;
						if (data.Slot == ClothingSlot.Head) clothingSlotIndex = 0;
						else if (data.Slot == ClothingSlot.Torso) clothingSlotIndex = 1;
						else if (data.Slot == ClothingSlot.Legs) clothingSlotIndex = 2;
						else if (data.Slot == ClothingSlot.Feet) clothingSlotIndex = 3;
						if (clothingSlotIndex == -1) continue;

						if (m_componentCreatureClothing.GetSlotProcessCapacity(clothingSlotIndex, value) > 0)
						{
							m_pendingClothingMinerSlot = i;
							m_pendingClothingValue = value;
							m_pendingClothingSlotIndex = clothingSlotIndex;
							m_clothingEquipTimer = 0.55f;
							m_clothingEquipPending = true;
							break;
						}
					}
				}
			}

			// ===== NUEVO: Lógica de montura (antes del combate) =====
			if (!celebrationActive)
			{
				UpdateMounting(dt);
				if (m_mountState != MountState.None) return;

				// Si ya está montado y el combate montado está activo, manejamos la persecución con la montura
				if (m_componentRider != null && m_componentRider.Mount != null && m_mountedCombatActive)
				{
					HandleMountedCombat(dt);
					// No retornamos: el combate normal (armas) también debe ejecutarse
				}
			}

			// Si la celebración está activa, NO hacer nada de combate después de esto
			if (celebrationActive) return;

			if (!CanUseInventory || m_componentMiner == null || m_componentCreature == null)
				return;

			// Si el comportamiento de huida está activo, no detener el disparo ni apunte, seguir normal
			if (m_componentDefensiveRunAway != null && m_componentDefensiveRunAway.IsActive)
			{
				// No se cancela nada, se sigue con la lógica normal de disparo y apunte
			}

			// Si estamos atascados: cancelar cualquier apuntado, no iniciar nuevos,
			// no cambiar de ítem y salir inmediatamente.
			if (m_componentPathfinding != null && m_componentPathfinding.IsStuck)
			{
				if (m_isAiming) CancelAiming();
				if (m_isCustomAiming) StopCustomAiming();
				m_cooldownTimer = 0f;
				return;
			}

			// Recarga de arma de fuego (bloquea cualquier otra acción)
			if (m_isFirearmReloading)
			{
				m_firearmReloadTimer -= dt;
				if (m_firearmReloadTimer <= 0f)
				{
					m_isFirearmReloading = false;
					m_firearmShotsSinceReload = 0;
					// Partículas de fin de recarga
					if (m_subsystemParticles != null && m_subsystemTerrain != null)
					{
						try
						{
							Vector3 basePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
							Vector3 readyPosition = basePosition + new Vector3(0f, 0.2f, 0f);
							KillParticleSystem readyParticles = new KillParticleSystem(m_subsystemTerrain, readyPosition, 0.5f);
							m_subsystemParticles.AddParticleSystem(readyParticles, false);
							for (int i = 0; i < 3; i++)
							{
								Vector3 offset = new Vector3(m_random.Float(-0.2f, 0.2f), m_random.Float(0.1f, 0.4f), m_random.Float(-0.2f, 0.2f));
								KillParticleSystem additionalParticles = new KillParticleSystem(m_subsystemTerrain, basePosition + offset, 0.5f);
								m_subsystemParticles.AddParticleSystem(additionalParticles, false);
							}
						}
						catch (Exception)
						{
						}
					}
					m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, 0f, m_componentCreature.ComponentCreatureModel.EyePosition, 10f, true);
					ResetModelRotation();
					m_cooldownTimer = 0f;
					m_hasCompletedInitialAim = false;
				}
				else
				{
					// Animación de recarga
					ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
					if (model != null)
					{
						model.AimHandAngleOrder = 0f;
						model.InHandItemOffsetOrder = Vector3.Zero;
						model.InHandItemRotationOrder = Vector3.Zero;
						model.LookAtOrder = null;
					}
				}
				return;
			}

			float distance = GetTargetDistance();
			bool hasTarget = IsTargetValidForRangedAttack();

			// Manejar apuntado personalizado (sin levantar brazo)
			if (m_isCustomAiming)
			{
				bool hasLineOfSight = true;
				if (IsThrowable(m_customWeaponContents))
				{
					hasLineOfSight = HasLineOfSightToTarget();
				}

				if (!hasTarget || (IsThrowable(m_customWeaponContents) && !hasLineOfSight))
				{
					StopCustomAiming();

					if (IsThrowable(m_customWeaponContents) && !hasLineOfSight && !m_isFlanking && m_componentChase?.Target != null)
					{
						StartFlanking(m_componentChase.Target.ComponentBody.Position);
					}
					return;
				}
				else
				{
					if (m_isFlanking) StopFlanking();
				}

				// ItemsLauncher: animación manual, sin Miner.Aim
				if (m_customWeaponContents == ItemsLauncherBlock.Index)
				{
					m_customAimTimer += dt;
					Ray3 aimRay = CalculateAimRay();

					UpdateWeaponRotation(aimRay);

					float aimTime = GetAimTime(ItemsLauncherBlock.Index);
					if (m_customAimTimer >= aimTime)
					{
						FireItemsLauncher(aimRay);
						m_isCustomAiming = false;
						m_cooldownTimer = GetCooldown(ItemsLauncherBlock.Index);
						ResetModelRotation();
					}
					return;
				}

				// Para ballesta, arco, mosquete, lanzallamas, mosquete doble y armas de fuego (criaturas especiales)
				float aimTimeWeapon = GetAimTime(m_customWeaponContents);
				m_customAimTimer += dt;
				if (!TryCalculateAimRay(out Ray3 aimRayWeapon))
				{
					// No se puede calcular el rayo, cancelar apuntado
					StopCustomAiming();
					return;
				}

				int sniperIndex = BlocksManager.GetBlockIndex(typeof(SniperBlock), true, false);
				if (m_customWeaponContents == sniperIndex)
				{
					// Animación de apunte del sniper para criaturas especiales: SIN levantar el brazo
					ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
					if (model != null)
					{
						model.AimHandAngleOrder = 0f;
						model.InHandItemOffsetOrder = new Vector3(-0.1f, -0.15f, 0.25f);
						model.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
						if (m_componentChase != null && m_componentChase.Target != null)
						{
							model.LookAtOrder = m_componentChase.Target.ComponentCreatureModel.EyePosition;
						}
					}
				}
				else if (FirearmDefensiveConfigs.ContainsKey(m_customWeaponContents))
				{
					// Otras armas de fuego para criaturas especiales: SIN levantar el brazo
					ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
					if (model != null)
					{
						model.AimHandAngleOrder = 0f;
						model.InHandItemOffsetOrder = new Vector3(-0.1f, -0.15f, 0.25f);
						model.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
						if (m_componentChase != null && m_componentChase.Target != null)
						{
							model.LookAtOrder = m_componentChase.Target.ComponentCreatureModel.EyePosition;
						}
					}
				}
				else
				{
					// Armas primitivas (ballesta, arco, mosquete, etc.): usan Miner.Aim y UpdateWeaponRotation
					m_componentMiner.Aim(aimRayWeapon, AimState.InProgress);
					UpdateWeaponRotation(aimRayWeapon);
				}

				// Fase de apuntado personalizado para armas de fuego
				if (FirearmDefensiveConfigs.TryGetValue(m_customWeaponContents, out FirearmDefConfig firearmCfg))
				{
					// Animación manual SIN Miner.Aim
					UpdateWeaponRotation(aimRayWeapon);

					if (!m_hasCompletedInitialAim)
					{
						if (m_customAimTimer < aimTimeWeapon)
							return;

						FireFirearm(aimRayWeapon, firearmCfg);
						m_lastFirearmShotTime = m_subsystemTime.GameTime;

						if (m_firearmShotsSinceReload >= firearmCfg.MaxShotsBeforeReload)
						{
							m_isCustomAiming = false;
							m_hasCompletedInitialAim = false;
							StartFirearmReloading();
							ResetModelRotation();
						}
						else
						{
							m_hasCompletedInitialAim = true;
						}
					}
					else
					{
						if ((m_subsystemTime.GameTime - m_lastFirearmShotTime) >= firearmCfg.FireRate)
						{
							FireFirearm(aimRayWeapon, firearmCfg);
							m_lastFirearmShotTime = m_subsystemTime.GameTime;
							if (m_firearmShotsSinceReload >= firearmCfg.MaxShotsBeforeReload)
							{
								m_isCustomAiming = false;
								m_hasCompletedInitialAim = false;
								StartFirearmReloading();
								ResetModelRotation();
							}
						}
					}
				}
				else
				{
					// Lógica original para armas primitivas (mosquete, ballesta, etc.)
					if (m_customAimTimer < aimTimeWeapon)
						return;

					BulletBlock.BulletType? musketBulletBeforeFire = null;
					if (m_customWeaponContents == MusketBlock.Index)
					{
						int data = Terrain.ExtractData(m_componentMiner.Inventory.GetSlotValue(m_componentMiner.Inventory.ActiveSlotIndex));
						musketBulletBeforeFire = MusketBlock.GetBulletType(data);
					}

					m_componentMiner.Aim(aimRayWeapon, AimState.Completed);
					m_cooldownTimer = GetCooldown(m_customWeaponContents);

					if (m_customWeaponContents == FlameThrowerBlock.Index)
					{
						m_customAimTimer = 0f;
					}
					else
					{
						m_isCustomAiming = false;
					}

					if (musketBulletBeforeFire != null && m_random.Float(0f, 1f) < 0.05f)
					{
						FireMusketExtraShots(aimRayWeapon, musketBulletBeforeFire.Value);
					}

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
					else if (m_customWeaponContents == RepeatCrossbowBlock.Index)
					{
						EnsureRepeatCrossbowEquipped();
					}
					else if (m_customWeaponContents == FlameThrowerBlock.Index)
					{
						EnsureFlameThrowerEquipped();
					}
					else if (m_customWeaponContents == DoubleMusketBlock.Index)
					{
						EnsureDoubleMusketEquipped();
					}

					ResetModelRotation();
				}
				return;
			}

			// Manejar apuntado normal (comportamiento original)
			if (m_isAiming)
			{
				bool hasLineOfSight = true;
				int activeContents = Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(m_componentMiner.Inventory.ActiveSlotIndex));
				if (IsThrowable(activeContents))
				{
					hasLineOfSight = HasLineOfSightToTarget();
				}

				if (!hasTarget || (IsThrowable(activeContents) && !hasLineOfSight))
				{
					CancelAiming();

					if (IsThrowable(activeContents) && !hasLineOfSight && !m_isFlanking)
					{
						StartFlanking(m_componentChase.Target.ComponentBody.Position);
					}
					return;
				}
				else
				{
					if (m_isFlanking) StopFlanking();
				}

				// CORRECCIÓN: Verificar distancia para lanzables durante el apuntado
				if (IsThrowable(activeContents))
				{
					if (distance < ThrowableAttackRange.X || distance > ThrowableAttackRange.Y)
					{
						CancelAiming();
						return;
					}
				}

				if (distance <= RangedAttackRange.X && SwitchToMeleeWeapon())
				{
					CancelAiming();
					return;
				}

				if (!TryCalculateAimRay(out Ray3 aimRay))
				{
					CancelAiming();
					return;
				}

				if (IsThrowable(activeContents))
				{
					StopMovement();
				}

				// Manejo especial para armas de fuego
				if (FirearmDefensiveConfigs.TryGetValue(activeContents, out FirearmDefConfig firearmConfig))
				{
					float aimTime = GetAimTime(activeContents);
					int sniperIndex2 = BlocksManager.GetBlockIndex(typeof(SniperBlock), true, false);

					// Animación manual (sin Miner.Aim)
					ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
					if (model != null)
					{
						if (IsSpecialNoRaiseCreature())
						{
							// Criaturas especiales: NO levantan el brazo, solo rotan el arma
							model.AimHandAngleOrder = 0f;
							model.InHandItemOffsetOrder = new Vector3(-0.1f, -0.15f, 0.25f);
							model.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
						}
						else
						{
							// Criaturas normales: levantan el brazo
							if (activeContents == sniperIndex2)
							{
								model.AimHandAngleOrder = 1.2f;
								model.InHandItemOffsetOrder = new Vector3(-0.1f, -0.06f, 0.08f);
								model.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
							}
							else
							{
								model.AimHandAngleOrder = 1.4f;
								model.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
								model.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
							}
						}
						if (m_componentChase != null && m_componentChase.Target != null)
							model.LookAtOrder = m_componentChase.Target.ComponentCreatureModel.EyePosition;
					}

					if (!m_hasCompletedInitialAim)
					{
						m_aimTimer += dt;
						if (m_aimTimer >= aimTime)
						{
							FireFirearm(aimRay, firearmConfig);
							m_lastFirearmShotTime = m_subsystemTime.GameTime;
							if (m_firearmShotsSinceReload >= firearmConfig.MaxShotsBeforeReload)
							{
								m_isAiming = false;
								m_hasCompletedInitialAim = false;
								StartFirearmReloading();
								ResetModelRotation();
							}
							else
							{
								m_hasCompletedInitialAim = true;
								m_aimTimer = aimTime;
							}
						}
					}
					else
					{
						if ((m_subsystemTime.GameTime - m_lastFirearmShotTime) >= firearmConfig.FireRate)
						{
							FireFirearm(aimRay, firearmConfig);
							m_lastFirearmShotTime = m_subsystemTime.GameTime;
							if (m_firearmShotsSinceReload >= firearmConfig.MaxShotsBeforeReload)
							{
								m_isAiming = false;
								m_hasCompletedInitialAim = false;
								StartFirearmReloading();
								ResetModelRotation();
							}
						}
					}
					return;
				}

				// Para el resto de armas (comportamiento original)
				float aimTimeForWeapon = GetAimTime(activeContents);
				m_aimTimer += dt;

				if (m_aimTimer < aimTimeForWeapon)
				{
					m_componentMiner.Aim(aimRay, AimState.InProgress);
				}
				else
				{
					// Capturar la munición del mosquete antes de disparar
					BulletBlock.BulletType? musketBulletBeforeFire = null;
					if (activeContents == MusketBlock.Index)
					{
						int data = Terrain.ExtractData(m_componentMiner.Inventory.GetSlotValue(m_componentMiner.Inventory.ActiveSlotIndex));
						musketBulletBeforeFire = MusketBlock.GetBulletType(data);
					}

					m_componentMiner.Aim(aimRay, AimState.Completed);
					m_cooldownTimer = GetCooldown(activeContents);

					if (activeContents == FlameThrowerBlock.Index)
					{
						m_aimTimer = 0f;
					}
					else
					{
						m_isAiming = false;
					}

					// Probabilidad del 5% de triple disparo para el mosquete
					if (musketBulletBeforeFire != null && m_random.Float(0f, 1f) < 0.05f)
					{
						FireMusketExtraShots(aimRay, musketBulletBeforeFire.Value);
					}

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
					else if (activeContents == RepeatCrossbowBlock.Index)
					{
						EnsureRepeatCrossbowEquipped();
					}
					else if (activeContents == FlameThrowerBlock.Index)
					{
						EnsureFlameThrowerEquipped();
					}
					else if (activeContents == DoubleMusketBlock.Index)
					{
						EnsureDoubleMusketEquipped();
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

				// *** PRIORIDAD MODIFICADA: LANZABLES PRIMERO ***
				// 1. Lanzables (primera prioridad) - CON VERIFICACIÓN DE VISIBILIDAD
				if (distance >= ThrowableAttackRange.X && distance <= ThrowableAttackRange.Y && HasThrowableInInventory() && HasLineOfSightToTarget())
				{
					if (EnsureThrowableEquipped())
					{
						m_isAiming = true;
						m_aimTimer = 0f;
						m_hasCompletedInitialAim = false;
						m_componentMiner.Aim(CalculateAimRay(), AimState.InProgress);
						return;
					}
				}

				// 2. Armas de fuego modernas (sin verificación de visibilidad)
				if (HasFirearmInInventory() && EnsureFirearmEquipped())
				{
					int activeContents = Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(m_componentMiner.Inventory.ActiveSlotIndex));
					// Siempre usar apuntado personalizado (sin Miner.Aim) para armas de fuego
					StartCustomAiming(activeContents);
					return;
				}

				// 3. Ballesta repetidora (sin verificación de visibilidad)
				if (HasRepeatCrossbowInInventory() && EnsureRepeatCrossbowEquipped())
				{
					if (IsSpecialNoRaiseCreature())
					{
						StartCustomAiming(RepeatCrossbowBlock.Index);
						return;
					}
					else
					{
						m_isAiming = true;
						m_aimTimer = 0f;
						m_componentMiner.Aim(CalculateAimRay(), AimState.InProgress);
						return;
					}
				}

				// 4. Lanzallamas (sin verificación de visibilidad)
				if (HasFlameThrowerInInventory() && EnsureFlameThrowerEquipped())
				{
					if (IsSpecialNoRaiseCreature())
					{
						StartCustomAiming(FlameThrowerBlock.Index);
						return;
					}
					else
					{
						m_isAiming = true;
						m_aimTimer = GetAimTime(FlameThrowerBlock.Index);
						m_componentMiner.Aim(CalculateAimRay(), AimState.InProgress);
						return;
					}
				}

				// 5. Mosquete de doble cañón (sin verificación de visibilidad)
				if (HasDoubleMusketInInventory() && EnsureDoubleMusketEquipped())
				{
					if (IsSpecialNoRaiseCreature())
					{
						StartCustomAiming(DoubleMusketBlock.Index);
						return;
					}
					else
					{
						m_isAiming = true;
						m_aimTimer = 0f;
						m_componentMiner.Aim(CalculateAimRay(), AimState.InProgress);
						return;
					}
				}

				// 6. Lanzador de ítems (sin verificación de visibilidad) – apuntado manual para todos
				if (HasItemsLauncherInInventory() && EnsureItemsLauncherEquipped())
				{
					StartCustomAiming(ItemsLauncherBlock.Index);
					return;
				}

				// Para criaturas especiales: ballesta, arco, mosquete doble, mosquete con apuntado personalizado
				if (IsSpecialNoRaiseCreature())
				{
					// Ballesta y arco normales (sin verificación de visibilidad)
					if (HasCrossbowInInventory() && EnsureCrossbowEquipped())
					{
						StartCustomAiming(CrossbowBlock.Index);
						return;
					}
					if (HasBowInInventory() && EnsureBowEquipped())
					{
						StartCustomAiming(BowBlock.Index);
						return;
					}
					if (HasDoubleMusketInInventory() && EnsureDoubleMusketEquipped())
					{
						StartCustomAiming(DoubleMusketBlock.Index);
						return;
					}
					if (EnsureMusketEquipped())
					{
						StartCustomAiming(MusketBlock.Index);
						return;
					}
				}
				else
				{
					// Comportamiento normal para otras criaturas (respeta distancia mínima) - SIN VERIFICACIÓN DE VISIBILIDAD
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
					if (HasDoubleMusketInInventory() && EnsureDoubleMusketEquipped())
					{
						m_isAiming = true;
						m_aimTimer = 0f;
						m_componentMiner.Aim(CalculateAimRay(), AimState.InProgress);
						return;
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

		private void StartFirearmReloading()
		{
			m_isFirearmReloading = true;
			m_firearmReloadTimer = FirearmReloadTime;
			m_isAiming = false;
			m_isCustomAiming = false;
			m_hasCompletedInitialAim = false;
			// Partículas de inicio de recarga
			if (m_subsystemParticles != null && m_subsystemTerrain != null)
			{
				try
				{
					Vector3 basePosition = m_componentCreature.ComponentBody.Position + new Vector3(0f, 1f, 0f);
					KillParticleSystem reloadParticles = new KillParticleSystem(m_subsystemTerrain, basePosition, 0.5f);
					m_subsystemParticles.AddParticleSystem(reloadParticles, false);
					for (int i = 0; i < 3; i++)
					{
						Vector3 offset = new Vector3(m_random.Float(-0.2f, 0.2f), m_random.Float(0.1f, 0.4f), m_random.Float(-0.2f, 0.2f));
						KillParticleSystem additionalParticles = new KillParticleSystem(m_subsystemTerrain, basePosition + offset, 0.5f);
						m_subsystemParticles.AddParticleSystem(additionalParticles, false);
					}
				}
				catch (Exception)
				{
				}
			}
			m_subsystemAudio.PlaySound("Audio/Armas/reload", 0.8f, 0f, m_componentCreature.ComponentCreatureModel.EyePosition, 10f, true);
			ResetModelRotation();
		}

		// Dispara una bola de mosquete manualmente (sin Miner.Aim)
		private void FireItemsLauncher(Ray3 aimRay)
		{
			Vector3 eyePos = aimRay.Position;
			Vector3 direction = aimRay.Direction;

			// Crear el valor del proyectil: BulletBlock con tipo MusketBall
			int bulletData = BulletBlock.SetBulletType(0, BulletBlock.BulletType.MusketBall);
			int bulletValue = Terrain.MakeBlockValue(BulletBlock.Index, 0, bulletData);

			SubsystemProjectiles subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			subsystemProjectiles.FireProjectile(bulletValue, eyePos, direction * 60f, Vector3.Zero, m_componentCreature);

			m_subsystemAudio.PlaySound("Audio/Items/ItemLauncher/Item Cannon Fire", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 15f, false);

			SubsystemTerrain subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			SubsystemParticles subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			if (subsystemParticles != null && subsystemTerrain != null)
			{
				subsystemParticles.AddParticleSystem(
					new GunSmokeParticleSystem(subsystemTerrain, eyePos + direction * 0.5f, direction),
					false
				);
			}

			// Ruido
			SubsystemNoise subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			if (subsystemNoise != null)
			{
				subsystemNoise.MakeNoise(eyePos, 1f, 40f);
			}

			// Retroceso
			m_componentCreature.ComponentBody.ApplyImpulse(-2f * direction);
		}

		private void StartCustomAiming(int weaponContents)
		{
			m_isCustomAiming = true;
			m_customWeaponContents = weaponContents;

			// Para el lanzallamas, el primer disparo es inmediato
			if (weaponContents == FlameThrowerBlock.Index)
			{
				m_customAimTimer = GetAimTime(weaponContents);
			}
			else
			{
				m_customAimTimer = 0f;
			}

			// No forzar AimHandAngleOrder a 0 aquí, lo hará UpdateWeaponRotation
		}

		private void StopCustomAiming()
		{
			if (m_isCustomAiming)
			{
				int sniperIndex = BlocksManager.GetBlockIndex(typeof(SniperBlock), true, false);
				// Solo cancelar Miner.Aim si NO es arma de fuego y NO es ItemsLauncher
				if (m_customWeaponContents != ItemsLauncherBlock.Index &&
					m_customWeaponContents != sniperIndex &&
					!FirearmDefensiveConfigs.ContainsKey(m_customWeaponContents))
				{
					if (TryCalculateAimRay(out Ray3 aimRay))
					{
						m_componentMiner.Aim(aimRay, AimState.Cancelled);
					}
				}
				ResetModelRotation();
				m_isCustomAiming = false;
				m_customAimTimer = 0f;
			}
		}

		private void ResetModelRotation()
		{
			ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
			if (model != null)
			{
				model.InHandItemRotationOrder = Vector3.Zero;
				model.InHandItemOffsetOrder = Vector3.Zero;
				model.AimHandAngleOrder = 0f;
			}
		}

		private void UpdateWeaponRotation(Ray3 aimRay)
		{
			ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
			if (model == null) return;

			if (m_customWeaponContents == ItemsLauncherBlock.Index)
			{
				if (IsSpecialNoRaiseCreature())
				{
					// Special creatures: no arm raise, just weapon rotation
					model.AimHandAngleOrder = 0f;
					model.InHandItemOffsetOrder = new Vector3(-0.1f, -0.15f, 0.25f);
					model.InHandItemRotationOrder = BaseWeaponRotations[ItemsLauncherBlock.Index];
				}
				else
				{
					// Normal creatures: full arm raise (simulate Miner.Aim)
					model.AimHandAngleOrder = 1.4f;
					model.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					model.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
				}
			}
			else
			{
				// Para las otras armas en apuntado personalizado (solo criaturas especiales)
				model.AimHandAngleOrder = 0f; // Nunca levantan el brazo

				// Rotación y offset según el tipo de arma
				if (BaseWeaponRotations.TryGetValue(m_customWeaponContents, out Vector3 baseRot))
				{
					model.InHandItemRotationOrder = baseRot;
				}
				else if (FirearmDefensiveConfigs.ContainsKey(m_customWeaponContents))
				{
					// Armas de fuego: animación manual SIN Miner.Aim
					if (IsSpecialNoRaiseCreature())
					{
						model.AimHandAngleOrder = 0f;
						model.InHandItemOffsetOrder = new Vector3(-0.1f, -0.15f, 0.25f);
						model.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
					}
					else
					{
						// Criaturas normales: levantan el brazo
						var config = FirearmDefensiveConfigs[m_customWeaponContents];
						if (config.IsSniper)
						{
							model.AimHandAngleOrder = 1.2f;
							model.InHandItemOffsetOrder = new Vector3(-0.1f, -0.06f, 0.08f);
							model.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
						}
						else
						{
							model.AimHandAngleOrder = 1.4f;
							model.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
							model.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
						}
					}
					if (m_componentChase != null && m_componentChase.Target != null)
						model.LookAtOrder = m_componentChase.Target.ComponentCreatureModel.EyePosition;
				}
			}

				// Forzar que la criatura mire al objetivo
				if (m_componentChase != null && m_componentChase.Target != null)
			{
				model.LookAtOrder = m_componentChase.Target.ComponentCreatureModel.EyePosition;
				model.LookRandomOrder = false;
			}
		}

		private float GetAimTime(int contents)
		{
			if (FirearmDefensiveConfigs.ContainsKey(contents))
				return FirearmDefensiveConfigs[contents].IsSniper ? 1.0f : 0.5f;
			if (contents == CrossbowBlock.Index) return CrossbowAimTime;
			if (contents == BowBlock.Index) return BowAimTime;
			if (IsThrowable(contents)) return ThrowableAimTime;
			if (contents == ItemsLauncherBlock.Index) return ItemsLauncherAimTime;
			if (contents == RepeatCrossbowBlock.Index) return RepeatCrossbowAimTime;
			if (contents == FlameThrowerBlock.Index) return FlameThrowerAimTime;
			if (contents == DoubleMusketBlock.Index) return DoubleMusketAimTime;
			return MusketAimTime;
		}

		private float GetCooldown(int contents)
		{
			if (FirearmDefensiveConfigs.ContainsKey(contents))
				return FirearmDefensiveConfigs[contents].FireRate;
			if (contents == CrossbowBlock.Index) return CrossbowCooldown;
			if (contents == BowBlock.Index) return BowCooldown;
			if (IsThrowable(contents)) return ThrowableCooldown;
			if (contents == ItemsLauncherBlock.Index) return ItemsLauncherCooldown;
			if (contents == RepeatCrossbowBlock.Index) return RepeatCrossbowCooldown;
			if (contents == FlameThrowerBlock.Index) return FlameThrowerCooldown;
			if (contents == DoubleMusketBlock.Index) return DoubleMusketCooldown;
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

		// Nuevo método seguro para calcular el rayo de apuntado
		private bool TryCalculateAimRay(out Ray3 ray)
		{
			ray = default;
			if (m_componentCreature == null || m_componentCreature.ComponentCreatureModel == null)
				return false;
			if (m_componentChase == null || m_componentChase.Target == null || m_componentChase.Target.ComponentCreatureModel == null)
				return false;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetEye = m_componentChase.Target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetEye - eyePos);
			ray = new Ray3(eyePos, direction);
			return true;
		}

		// Método original se mantiene por compatibilidad, pero ahora delega en TryCalculateAimRay
		private Ray3 CalculateAimRay()
		{
			if (!TryCalculateAimRay(out Ray3 ray))
			{
				// Si no se puede calcular, devolvemos un rayo por defecto (evita null, aunque no debería usarse)
				return new Ray3(m_componentCreature.ComponentCreatureModel.EyePosition, Vector3.UnitZ);
			}
			return ray;
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
				if (slotValue != 0 &&
					contents != MusketBlock.Index &&
					contents != CrossbowBlock.Index &&
					contents != BowBlock.Index &&
					contents != ItemsLauncherBlock.Index &&
					contents != RepeatCrossbowBlock.Index &&
					contents != FlameThrowerBlock.Index &&
					contents != DoubleMusketBlock.Index &&
					!IsThrowable(contents) &&
					!FirearmDefensiveConfigs.ContainsKey(contents))
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

		// ========== ARMAS DE FUEGO MODERNAS ==========
		private bool HasFirearmInInventory()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int value = inventory.GetSlotValue(i);
				if (value != 0 && FirearmDefensiveConfigs.ContainsKey(Terrain.ExtractContents(value)))
					return true;
			}
			return false;
		}

		private bool EnsureFirearmEquipped()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;
			int activeSlot = inventory.ActiveSlotIndex;
			int activeValue = inventory.GetSlotValue(activeSlot);
			if (activeValue != 0 && FirearmDefensiveConfigs.ContainsKey(Terrain.ExtractContents(activeValue)))
				return true;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int value = inventory.GetSlotValue(i);
				if (value != 0 && FirearmDefensiveConfigs.ContainsKey(Terrain.ExtractContents(value)))
				{
					inventory.ActiveSlotIndex = i;
					return true;
				}
			}
			return false;
		}

		private void FireFirearm(Ray3 aimRay, FirearmDefConfig config)
		{
			Vector3 eyePos = aimRay.Position;
			Vector3 direction = aimRay.Direction;
			SubsystemProjectiles subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);

			Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
			Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));

			for (int i = 0; i < config.ProjectilesPerShot; i++)
			{
				Vector3 spread = m_random.Float(-config.SpreadVector.X, config.SpreadVector.X) * right
							   + m_random.Float(-config.SpreadVector.Y, config.SpreadVector.Y) * up
							   + m_random.Float(-config.SpreadVector.Z, config.SpreadVector.Z) * direction;

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
				subsystemProjectiles.FireProjectile(bulletValue, eyePos, config.BulletSpeed * (direction + spread), Vector3.Zero, m_componentCreature);
			}

			m_subsystemAudio.PlaySound(config.ShootSound, 1f, m_random.Float(-0.1f, 0.1f), eyePos, 15f, false);

			if (m_subsystemParticles != null && m_subsystemTerrain != null)
			{
				m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + direction * 1.3f, direction), false);
			}

			if (m_subsystemNoise != null)
			{
				m_subsystemNoise.MakeNoise(eyePos, 0.8f, 40f);
			}

			m_componentCreature.ComponentBody.ApplyImpulse(-1f * direction);

			m_firearmShotsSinceReload++;
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

			float distanceToTarget = GetTargetDistance();
			List<ArrowBlock.ArrowType> boltTypesList = new List<ArrowBlock.ArrowType>
			{
				ArrowBlock.ArrowType.IronBolt,
				ArrowBlock.ArrowType.DiamondBolt
			};
			if (distanceToTarget >= ExplosiveSafeRange.X)
			{
				boltTypesList.Add(ArrowBlock.ArrowType.ExplosiveBolt);
			}
			ArrowBlock.ArrowType[] boltTypes = boltTypesList.ToArray();

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

		// ========== BALLESTA REPETIDORA ==========
		private bool HasRepeatCrossbowInInventory()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;
			for (int i = 0; i < inventory.SlotsCount; i++)
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == RepeatCrossbowBlock.Index) return true;
			return false;
		}

		private bool EnsureRepeatCrossbowEquipped()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			int activeSlot = inventory.ActiveSlotIndex;
			int activeValue = inventory.GetSlotValue(activeSlot);
			int repeatCrossbowIndex = RepeatCrossbowBlock.Index;

			float distanceToTarget = GetTargetDistance();
			List<RepeatArrowBlock.ArrowType> boltTypesList = new List<RepeatArrowBlock.ArrowType>
			{
				RepeatArrowBlock.ArrowType.CopperArrow,
				RepeatArrowBlock.ArrowType.IronArrow,
				RepeatArrowBlock.ArrowType.DiamondArrow,
				RepeatArrowBlock.ArrowType.PoisonArrow,
				RepeatArrowBlock.ArrowType.SeriousPoisonArrow
			};
			if (distanceToTarget >= ExplosiveSafeRange.X)
			{
				boltTypesList.Add(RepeatArrowBlock.ArrowType.ExplosiveArrow);
			}
			RepeatArrowBlock.ArrowType[] boltTypes = boltTypesList.ToArray();

			if (Terrain.ExtractContents(activeValue) == repeatCrossbowIndex)
			{
				int data = Terrain.ExtractData(activeValue);
				int draw = RepeatCrossbowBlock.GetDraw(data);
				RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
				int loadCount = RepeatCrossbowBlock.GetLoadCount(activeValue);

				if (draw != 15 || arrowType == null)
				{
					RepeatArrowBlock.ArrowType randomBolt = boltTypes[m_random.Int(0, boltTypes.Length)];
					data = RepeatCrossbowBlock.SetDraw(data, 15);
					data = RepeatCrossbowBlock.SetArrowType(data, randomBolt);

					if (loadCount < 1)
					{
						loadCount = 1;
					}

					int newValue = Terrain.MakeBlockValue(repeatCrossbowIndex, loadCount, data);
					inventory.RemoveSlotItems(activeSlot, 1);
					inventory.AddSlotItems(activeSlot, newValue, 1);
				}
				return true;
			}

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == repeatCrossbowIndex)
				{
					inventory.ActiveSlotIndex = i;
					return EnsureRepeatCrossbowEquipped();
				}
			}
			return false;
		}

		// ========== LANZALLAMAS ==========
		private bool HasFlameThrowerInInventory()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == FlameThrowerBlock.Index)
					return true;
			}
			return false;
		}

		private bool EnsureFlameThrowerEquipped()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			int activeSlot = inventory.ActiveSlotIndex;
			int activeValue = inventory.GetSlotValue(activeSlot);
			int flamethrowerIndex = FlameThrowerBlock.Index;

			if (Terrain.ExtractContents(activeValue) == flamethrowerIndex)
			{
				int data = Terrain.ExtractData(activeValue);
				FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
				int loadCount = FlameThrowerBlock.GetLoadCount(activeValue);
				if (loadState != FlameThrowerBlock.LoadState.Loaded || loadCount <= 0)
				{
					FlameBulletBlock.FlameBulletType randomType = m_random.Bool() ? FlameBulletBlock.FlameBulletType.Poison : FlameBulletBlock.FlameBulletType.Flame;
					int newData = 0;
					newData = FlameThrowerBlock.SetLoadState(newData, FlameThrowerBlock.LoadState.Loaded);
					newData = FlameThrowerBlock.SetBulletType(newData, randomType);
					newData = FlameThrowerBlock.SetSwitchState(newData, false);
					int newValue = Terrain.MakeBlockValue(flamethrowerIndex, 0, newData);
					newValue = FlameThrowerBlock.SetLoadCount(newValue, 15);
					inventory.RemoveSlotItems(activeSlot, 1);
					inventory.AddSlotItems(activeSlot, newValue, 1);
				}
				return true;
			}

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == flamethrowerIndex)
				{
					inventory.ActiveSlotIndex = i;
					return EnsureFlameThrowerEquipped();
				}
			}
			return false;
		}

		// ========== MOSQUETE DE DOBLE CAÑÓN ==========
		private bool HasDoubleMusketInInventory()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == DoubleMusketBlock.Index)
					return true;
			}
			return false;
		}

		private bool EnsureDoubleMusketEquipped()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			int activeSlot = inventory.ActiveSlotIndex;
			int activeValue = inventory.GetSlotValue(activeSlot);
			int doubleMusketIndex = DoubleMusketBlock.Index;

			if (Terrain.ExtractContents(activeValue) == doubleMusketIndex)
			{
				int data = Terrain.ExtractData(activeValue);
				bool isLoaded = DoubleMusketBlock.IsLoaded(data);
				int shotsRemaining = DoubleMusketBlock.GetShotsRemaining(data);
				if (!isLoaded || shotsRemaining < 2)
				{
					int newData = data;
					newData = DoubleMusketBlock.SetLoaded(newData, true);
					newData = DoubleMusketBlock.SetShotsRemaining(newData, 2);
					newData = DoubleMusketBlock.SetAntiTanksBullet(newData, true);
					newData = DoubleMusketBlock.SetHammerState(newData, false);
					int newValue = Terrain.MakeBlockValue(doubleMusketIndex, 0, newData);
					inventory.RemoveSlotItems(activeSlot, 1);
					inventory.AddSlotItems(activeSlot, newValue, 1);
				}
				return true;
			}

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == doubleMusketIndex)
				{
					inventory.ActiveSlotIndex = i;
					return EnsureDoubleMusketEquipped();
				}
			}
			return false;
		}

		// ========== LANZADOR DE ÍTEMS ==========
		private bool HasItemsLauncherInInventory()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == ItemsLauncherBlock.Index)
					return true;
			}
			return false;
		}

		private bool EnsureItemsLauncherEquipped()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return false;

			int activeSlot = inventory.ActiveSlotIndex;
			int activeValue = inventory.GetSlotValue(activeSlot);
			if (Terrain.ExtractContents(activeValue) == ItemsLauncherBlock.Index)
				return true;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == ItemsLauncherBlock.Index)
				{
					inventory.ActiveSlotIndex = i;
					return true;
				}
			}
			return false;
		}

		private void CancelAiming()
		{
			if (m_isAiming)
			{
				int activeContents = Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(m_componentMiner.Inventory.ActiveSlotIndex));
				int sniperIndex = BlocksManager.GetBlockIndex(typeof(SniperBlock), true, false);
				if (activeContents != sniperIndex && !FirearmDefensiveConfigs.ContainsKey(activeContents))
				{
					if (TryCalculateAimRay(out Ray3 aimRay))
						m_componentMiner.Aim(aimRay, AimState.Cancelled);
				}
				else
				{
					ResetModelRotation();
				}
				m_isAiming = false;
				m_aimTimer = 0f;
				m_cooldownTimer = 0f;
				m_hasCompletedInitialAim = false;
			}
		}

		private void FireMusketExtraShots(Ray3 aimRay, BulletBlock.BulletType firedType)
		{
			Vector3 eyePos = aimRay.Position;
			Vector3 direction = aimRay.Direction;
			SubsystemProjectiles subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);

			List<BulletBlock.BulletType> otherTypes = new List<BulletBlock.BulletType>();
			foreach (BulletBlock.BulletType type in Enum.GetValues(typeof(BulletBlock.BulletType)))
			{
				if (type != firedType)
				{
					otherTypes.Add(type);
				}
			}

			foreach (BulletBlock.BulletType type in otherTypes)
			{
				int bulletData = BulletBlock.SetBulletType(0, type);
				int bulletValue = Terrain.MakeBlockValue(BulletBlock.Index, 0, bulletData);
				subsystemProjectiles.FireProjectile(bulletValue, eyePos, direction * 60f, Vector3.Zero, m_componentCreature);
			}
		}

		private bool HasLineOfSightToTarget()
		{
			if (m_componentChase == null || m_componentChase.Target == null)
				return false;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetEye = m_componentChase.Target.ComponentCreatureModel.EyePosition;
			Vector3 toTarget = targetEye - eyePos;
			float distance = toTarget.Length();
			if (distance < 0.1f) return true;
			Vector3 direction = toTarget / distance;

			Vector3 bodyForward = m_componentCreature.ComponentBody.Matrix.Forward;
			float dot = Vector3.Dot(bodyForward, direction);
			float minDot = MathF.Cos(MathUtils.DegToRad(45f));
			if (dot < minDot) return false;

			Func<int, float, bool> terrainFilter = (int value, float dist) =>
			{
				int contents = Terrain.ExtractContents(value);
				Block block = BlocksManager.Blocks[contents];
				return block.IsCollidable_(value);
			};
			TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(
				eyePos, eyePos + direction * distance, false, true, terrainFilter);
			if (terrainHit != null && terrainHit.Value.Distance < distance - 0.1f)
				return false;

			BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(
				eyePos, eyePos + direction * distance, 0f,
				(ComponentBody body, float dist) =>
				{
					if (body == m_componentCreature.ComponentBody) return false;
					if (body.Entity == m_componentChase.Target.Entity) return false;
					return true;
				});
			if (bodyHit != null && bodyHit.Value.Distance < distance - 0.1f)
				return false;

			return true;
		}

		private void StartFlanking(Vector3 targetPos)
		{
			m_isFlanking = true;
			m_flankTimer = 2.0f;

			Vector3 toTarget = targetPos - m_componentCreature.ComponentBody.Position;
			Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, toTarget));
			float randomSign = m_random.Float(-1f, 1f) > 0 ? 1f : -1f;
			m_flankDirection = right * randomSign;

			CancelAiming();
			// Desactivar chase behavior para que no intervenga
			if (m_componentChase != null)
			{
				m_componentChase.Suppressed = true;
			}

			Vector3 flankTarget = m_componentCreature.ComponentBody.Position + m_flankDirection * 5f;
			m_componentPathfinding.SetDestination(flankTarget, 3f, 1f, 50, false, false, false, null);
		}

		private void StopFlanking()
		{
			m_isFlanking = false;
			m_flankTimer = 0f;
			m_flankDirection = Vector3.Zero;
			m_componentPathfinding.Stop();

			// Reactivar chase behavior
			if (m_componentChase != null)
			{
				m_componentChase.Suppressed = false;
				// Opcional: forzar reevaluación del objetivo si existe
				if (m_componentChase.Target != null && m_componentChase.Target.ComponentHealth.Health > 0f)
				{
					m_componentChase.StopAttack();
					m_componentChase.Attack(m_componentChase.Target, 100f, 10f, false);
				}
			}
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue("CanUseInventory", CanUseInventory);
		}

		public override void Dispose()
		{
			if (m_isAiming) CancelAiming();
			if (m_isCustomAiming) StopCustomAiming();
			base.Dispose();
		}
	}
}
