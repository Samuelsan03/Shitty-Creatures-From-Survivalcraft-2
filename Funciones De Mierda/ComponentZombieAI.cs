using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieAI : ComponentBehavior, IUpdateable
	{
		private ComponentCreature m_componentCreature;
		private ComponentZombieChaseBehavior m_componentZombieChaseBehavior;
		private ComponentInventory m_componentInventory;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private ComponentCreatureModel m_componentModel;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemTerrain m_subsystemTerrain;
		private ComponentMiner m_componentMiner;
		private ComponentPathfinding m_componentPathfinding; // AÑADIR ESTA LÍNEA
		private SubsystemBodies m_subsystemBodies; // AÑADIR junto a los otros subsystems
		private Random m_random = new Random();

		public float AimHandAngleOrder = 0f;

		private class FirearmConfig
		{
			public Type BulletBlockType { get; set; }
			public string ShootSound { get; set; }
			public double FireRate { get; set; }
			public float BulletSpeed { get; set; }
			public int MaxShotsBeforeReload { get; set; }
			public int ProjectilesPerShot { get; set; }
			public Vector3 SpreadVector { get; set; }
			public float NoiseRadius { get; set; }
			public bool IsAutomatic { get; set; }
			public bool IsSniper { get; set; }
			public bool IsShotgun { get; set; }
		}

		private static readonly Dictionary<int, FirearmConfig> m_firearmConfigs = new Dictionary<int, FirearmConfig>();

		private ArrowBlock.ArrowType[] m_bowArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.FireArrow,
			ArrowBlock.ArrowType.CopperArrow
		};

		private ArrowBlock.ArrowType[] m_crossbowBoltTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
		};

		private RepeatArrowBlock.ArrowType[] m_repeatCrossbowArrowTypes = new RepeatArrowBlock.ArrowType[]
		{
			RepeatArrowBlock.ArrowType.CopperArrow,
			RepeatArrowBlock.ArrowType.IronArrow,
			RepeatArrowBlock.ArrowType.DiamondArrow,
			RepeatArrowBlock.ArrowType.ExplosiveArrow,
			RepeatArrowBlock.ArrowType.PoisonArrow,
			RepeatArrowBlock.ArrowType.SeriousPoisonArrow
		};

		private int m_currentRepeatArrowTypeIndex = 0;

		public bool CanUseInventory = false;

		private bool m_isAiming = false;
		private bool m_isDrawing = false;
		private bool m_isFiring = false;
		private bool m_isReloading = false;
		private bool m_isCocking = false;
		private bool m_isFlameFiring = false;
		private bool m_isFlameCocking = false;
		private bool m_isFirearmAiming = false;
		private bool m_isFirearmFiring = false;
		private bool m_isFirearmReloading = false;
		private double m_animationStartTime;
		private double m_drawStartTime;
		private double m_fireTime;
		private double m_flameStartTime;
		private double m_nextFlameShotTime;
		private double m_cockStartTime;
		private double m_nextFlameSoundTime;
		private double m_lastFirearmShotTime;
		private double m_firearmReloadStartTime;
		private bool m_isThrowableAiming = false;
		private bool m_isThrowableThrowing = false;
		private double m_throwableThrowTime;

		// AÑADIDO: variables para ataque cuerpo a cuerpo
		private bool m_isMeleeAttacking = false;
		private double m_nextMeleeAttackTime = 0.0;
		private float m_meleeAttackInterval = 0.5f;

		// AÑADIDO: distancias para cambio de arma
		private float m_meleeSwitchDistance = 3f;   // Si está a menos de 3, cambia a melee
		private float m_rangedSwitchDistance = 5f;  // Si está a más de 5, cambia a distancia

		private int m_currentWeaponSlot = -1;
		private int m_weaponType = -1;
		private float m_currentDraw = 0f;
		private ArrowBlock.ArrowType m_currentArrowType;
		private ArrowBlock.ArrowType m_currentBoltType;
		private RepeatArrowBlock.ArrowType m_currentRepeatArrowType;
		private float m_currentTargetDistance = 0f;
		private bool m_arrowVisible = false;
		private bool m_hasArrowInBow = false;
		private FlameBulletBlock.FlameBulletType m_currentFlameBulletType = FlameBulletBlock.FlameBulletType.Flame;
		private bool m_flameSwitchState = false;
		private int m_shotsSinceLastReload = 0;
		private bool m_hasFirearmAimed = false;

		private float m_maxDistance = 100f;
		private float m_drawTime = 0.5f;
		private float m_aimTime = 0.5f;
		private float m_reloadTime = 0.8f;
		private float m_cockTime = 0.5f;
		private float m_flameShotInterval = 0.3f;
		private float m_flameSoundInterval = 0.2f;
		private float m_flameMaxDistance = 20f;
		private float m_flameCockTime = 0.5f;
		private float m_firearmAimTime = 0.5f;
		private float m_firearmReloadTime = 1.0f;
		private float m_sniperAimTime = 1.0f;
		private float m_throwableAimTime = 1.55f;
		private float m_throwableMinRange = 3f;
		private float m_throwableMaxRange = 15.5f;

		private float m_explosiveBoltMinDistance = 15f;
		private float m_explosiveRepeatArrowMinDistance = 15f;

		private float m_repeatCrossbowDrawTime = 0.85f;
		private float m_repeatCrossbowTimeBetweenShots = 0.5f;
		private float m_repeatCrossbowMaxInaccuracy = 0.04f;
		private float m_repeatCrossbowBoltSpeed = 35f;

		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);
			AimHandAngleOrder = valuesDictionary.GetValue<float>("AimHandAngleOrder");
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentZombieChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>();
			m_componentInventory = Entity.FindComponent<ComponentInventory>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentModel = Entity.FindComponent<ComponentCreatureModel>();
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true); // AÑADIR

			m_currentRepeatArrowTypeIndex = m_random.Int(0, m_repeatCrossbowArrowTypes.Length - 1);
			m_currentRepeatArrowType = m_repeatCrossbowArrowTypes[m_currentRepeatArrowTypeIndex];

			InitializeFirearmConfigs();
		}

		private void InitializeFirearmConfigs()
		{
			try
			{
				int kaIndex = BlocksManager.GetBlockIndex(typeof(KABlock), true, false);
				m_firearmConfigs[kaIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala5),
					ShootSound = "Audio/Armas/KA fuego",
					FireRate = 0.1,
					BulletSpeed = 320f,
					MaxShotsBeforeReload = 40,
					ProjectilesPerShot = 3,
					SpreadVector = new Vector3(0.007f, 0.007f, 0.03f),
					NoiseRadius = 35f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int bk43Index = BlocksManager.GetBlockIndex(typeof(Game.BK43Block), true, false);
				m_firearmConfigs[bk43Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala3),
					ShootSound = "Audio/Armas/bk 43",
					FireRate = 1.5,
					BulletSpeed = 280f,
					MaxShotsBeforeReload = 2,
					ProjectilesPerShot = 8,
					SpreadVector = new Vector3(0.1f, 0.1f, 0.03f),
					NoiseRadius = 50f,
					IsAutomatic = false
				};

				int akIndex = BlocksManager.GetBlockIndex(typeof(AKBlock), true, false);
				m_firearmConfigs[akIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/ak 47 fuego",
					FireRate = 0.17,
					BulletSpeed = 280f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.01f, 0.01f, 0.05f),
					NoiseRadius = 40f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int augIndex = BlocksManager.GetBlockIndex(typeof(AUGBlock), true, false);
				m_firearmConfigs[augIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/AUG fuego",
					FireRate = 0.17,
					BulletSpeed = 280f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.01f, 0.01f, 0.05f),
					NoiseRadius = 40f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int g3Index = BlocksManager.GetBlockIndex(typeof(G3Block), true, false);
				m_firearmConfigs[g3Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/FX05",
					FireRate = 0.12,
					BulletSpeed = 290f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.009f, 0.009f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int izh43Index = BlocksManager.GetBlockIndex(typeof(Izh43Block), true, false);
				m_firearmConfigs[izh43Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/shotgun fuego",
					FireRate = 1.0,
					BulletSpeed = 280f,
					MaxShotsBeforeReload = 2,
					ProjectilesPerShot = 8,
					SpreadVector = new Vector3(0.09f, 0.09f, 0.09f),
					NoiseRadius = 45f,
					IsAutomatic = false,
					IsSniper = false,
					IsShotgun = true
				};

				int m4Index = BlocksManager.GetBlockIndex(typeof(M4Block), true, false);
				m_firearmConfigs[m4Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala2),
					ShootSound = "Audio/Armas/M4 fuego",
					FireRate = 0.15,
					BulletSpeed = 300f,
					MaxShotsBeforeReload = 22,
					ProjectilesPerShot = 3,
					SpreadVector = new Vector3(0.008f, 0.008f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int mac10Index = BlocksManager.GetBlockIndex(typeof(Mac10Block), true, false);
				m_firearmConfigs[mac10Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala3),
					ShootSound = "Audio/Armas/mac 10 fuego",
					FireRate = 0.1,
					BulletSpeed = 300f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.012f, 0.012f, 0.035f),
					NoiseRadius = 30f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int minigunIndex = BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false);
				m_firearmConfigs[minigunIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala6),
					ShootSound = "Audio/Armas/Chaingun fuego",
					FireRate = 0.08,
					BulletSpeed = 260f,
					MaxShotsBeforeReload = 100,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.02f, 0.02f, 0.08f),
					NoiseRadius = 50f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int p90Index = BlocksManager.GetBlockIndex(typeof(P90Block), true, false);
				m_firearmConfigs[p90Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/FN P90 fuego",
					FireRate = 0.067,
					BulletSpeed = 320f,
					MaxShotsBeforeReload = 50,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.012f, 0.012f, 0.04f),
					NoiseRadius = 35f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int scarIndex = BlocksManager.GetBlockIndex(typeof(SCARBlock), true, false);
				m_firearmConfigs[scarIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/FN Scar fuego",
					FireRate = 0.1,
					BulletSpeed = 310f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.01f, 0.01f, 0.03f),
					NoiseRadius = 45f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int spas12Index = BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false);
				m_firearmConfigs[spas12Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/SPAS 12 fuego",
					FireRate = 0.8,
					BulletSpeed = 280f,
					MaxShotsBeforeReload = 8,
					ProjectilesPerShot = 8,
					SpreadVector = new Vector3(0.09f, 0.09f, 0.09f),
					NoiseRadius = 40f,
					IsAutomatic = false,
					IsSniper = false,
					IsShotgun = true
				};

				int sniperIndex = BlocksManager.GetBlockIndex(typeof(SniperBlock), true, false);
				m_firearmConfigs[sniperIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala6),
					ShootSound = "Audio/Armas/Sniper fuego",
					FireRate = 2.0,
					BulletSpeed = 450f,
					MaxShotsBeforeReload = 1,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.001f, 0.001f, 0.001f),
					NoiseRadius = 80f,
					IsAutomatic = false,
					IsSniper = true,
					IsShotgun = false
				};

				int revolverIndex = BlocksManager.GetBlockIndex(typeof(RevolverBlock), true, false);
				m_firearmConfigs[revolverIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala4),
					ShootSound = "Audio/Armas/Revolver fuego",
					FireRate = 0.6,
					BulletSpeed = 320f,
					MaxShotsBeforeReload = 6,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.02f, 0.02f, 0.05f),
					NoiseRadius = 40f,
					IsAutomatic = false,
					IsSniper = false,
					IsShotgun = false
				};

				int swm500Index = BlocksManager.GetBlockIndex(typeof(SWM500Block), true, false);
				m_firearmConfigs[swm500Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala4),
					ShootSound = "Audio/Armas/desert eagle fuego",
					FireRate = 0.5,
					BulletSpeed = 320f,
					MaxShotsBeforeReload = 5,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.02f, 0.02f, 0.05f),
					NoiseRadius = 40f,
					IsAutomatic = false,
					IsSniper = false,
					IsShotgun = false
				};

				int uziIndex = BlocksManager.GetBlockIndex(typeof(UziBlock), true, false);
				m_firearmConfigs[uziIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala2),
					ShootSound = "Audio/Armas/Uzi fuego",
					FireRate = 0.08,
					BulletSpeed = 320f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.015f, 0.015f, 0.06f),
					NoiseRadius = 40f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int famasIndex = BlocksManager.GetBlockIndex(typeof(FamasBlock), true, false);
				m_firearmConfigs[famasIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala4),
					ShootSound = "Audio/Armas/FAMAS fuego",
					FireRate = 0.09,
					BulletSpeed = 450f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.012f, 0.012f, 0.04f),
					NoiseRadius = 35f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int aa12Index = BlocksManager.GetBlockIndex(typeof(AA12Block), true, false);
				m_firearmConfigs[aa12Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala6),
					ShootSound = "Audio/Armas/AA12 fuego",
					FireRate = 0.2,
					BulletSpeed = 350f,
					MaxShotsBeforeReload = 20,
					ProjectilesPerShot = 8,
					SpreadVector = new Vector3(0.03f, 0.03f, 0.06f),
					NoiseRadius = 45f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = true
				};

				int m249Index = BlocksManager.GetBlockIndex(typeof(M249Block), true, false);
				m_firearmConfigs[m249Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala5),
					ShootSound = "Audio/Armas/M249 fuego",
					FireRate = 0.08,
					BulletSpeed = 400f,
					MaxShotsBeforeReload = 100,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.01f, 0.01f, 0.03f),
					NoiseRadius = 50f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int newG3Index = BlocksManager.GetBlockIndex(typeof(NewG3Block), true, false);
				m_firearmConfigs[newG3Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala3),
					ShootSound = "Audio/Armas/G3 fuego",
					FireRate = 0.12,
					BulletSpeed = 290f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.009f, 0.009f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int mp5ssdIndex = BlocksManager.GetBlockIndex(typeof(MP5SSDBlock), true, false);
				m_firearmConfigs[mp5ssdIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala3),
					ShootSound = "Audio/Armas/MP5SSD fuego",
					FireRate = 0.12,
					BulletSpeed = 290f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.009f, 0.009f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int mendozaIndex = BlocksManager.GetBlockIndex(typeof(MendozaBlock), true, false);
				m_firearmConfigs[mendozaIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala3),
					ShootSound = "Audio/Armas/Mendoza fuego",
					FireRate = 0.12,
					BulletSpeed = 290f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.009f, 0.009f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};

				int grozaIndex = BlocksManager.GetBlockIndex(typeof(GrozaBlock), true, false);
				m_firearmConfigs[grozaIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala3),
					ShootSound = "Audio/Armas/Groza fuego",
					FireRate = 0.12,
					BulletSpeed = 290f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.009f, 0.009f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true,
					IsSniper = false,
					IsShotgun = false
				};
			}
			catch (Exception)
			{
			}
		}

		private bool HasClearLineOfSight(ComponentCreature target)
		{
			if (target == null || target.ComponentBody == null) return false;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
			Vector3 direction = targetPos - eyePos;
			float distance = direction.Length();

			if (distance < 0.1f) return true;

			direction = Vector3.Normalize(direction);

			// Raycast contra el terreno (bloques sólidos)
			TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(
				eyePos,
				targetPos,
				false,
				true,
				(int value, float d) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable
			);

			// Si hay un bloque sólido entre medias, no hay línea de visión
			if (terrainHit != null && terrainHit.Value.Distance < distance - 0.5f)
			{
				return false;
			}

			return true;
		}

		private bool IsThrowableBlock(Block block)
		{
			if (block is SpearBlock)
				return true;

			if (block is LongspearBlock)
				return true;

			Type blockType = block.GetType();
			if (blockType == typeof(StoneChunkBlock)) return true;
			if (blockType == typeof(SulphurChunkBlock)) return true;
			if (blockType == typeof(CoalChunkBlock)) return true;
			if (blockType == typeof(DiamondChunkBlock)) return true;
			if (blockType == typeof(GermaniumChunkBlock)) return true;
			if (blockType == typeof(GermaniumOreChunkBlock)) return true;
			if (blockType == typeof(IronOreChunkBlock)) return true;
			if (blockType == typeof(MalachiteChunkBlock)) return true;
			if (blockType == typeof(SaltpeterChunkBlock)) return true;
			if (blockType == typeof(GunpowderBlock)) return true;
			if (blockType == typeof(BombBlock)) return true;
			if (blockType == typeof(IncendiaryBombBlock)) return true;
			if (blockType == typeof(PoisonBombBlock)) return true;
			if (blockType == typeof(BrickBlock)) return true;
			if (blockType == typeof(SnowballBlock)) return true;

			return false;
		}

		public void Update(float dt)
		{
			if (!CanUseInventory || m_componentCreature == null || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = m_componentZombieChaseBehavior?.Target;

			if (target != null && target.ComponentHealth.Health > 0f)
			{
				float distance = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					target.ComponentBody.Position
				);

				m_currentTargetDistance = distance;

				// AÑADIDO: Lógica de cambio de arma según distancia
				if (distance <= m_meleeSwitchDistance)
				{
					// Modo cuerpo a cuerpo - distancia corta
					if (m_weaponType != 7) // Si no estamos ya en modo melee con un arma melee real
					{
						// Buscar arma cuerpo a cuerpo real
						int meleeSlot = FindMeleeWeapon();
						if (meleeSlot != -1)
						{
							// Hay arma melee, equiparla y cambiar a modo melee
							m_currentWeaponSlot = meleeSlot;
							m_weaponType = 7;
							m_componentInventory.ActiveSlotIndex = meleeSlot;
							ResetWeaponState();
							m_isMeleeAttacking = false;
						}
						else
						{
							// No hay arma melee, usaremos el arma actual para golpear cuerpo a cuerpo
							// Pero NO cambiamos el tipo de arma - sigue siendo distancia
							// Solo nos aseguramos de que NO esté en modo melee falso
							if (m_weaponType == 7)
							{
								// Si por alguna razón está en modo melee sin arma melee, restaurar
								m_weaponType = -1;
								FindRangedWeapon();
							}
							// El arma actual (tipo 0-6) se usará para golpear en ProcessWeaponBehavior
						}
					}
				}
				else if (distance >= m_rangedSwitchDistance)
				{
					// Modo distancia
					if (m_weaponType != 7) // Ya estamos en modo distancia, no hacer nada
					{
						// Si estamos en melee, buscar arma a distancia
						if (m_weaponType == 7 || m_currentWeaponSlot == -1)
						{
							FindRangedWeapon(); // Esto ya actualiza m_weaponType
						}
					}
				}
				else
				{
					// Entre 3 y 5: mantener el arma actual (histéresis)
				}

				// Procesar el comportamiento según el tipo de arma actual
				float maxDistance = GetWeaponMaxDistance();

				if (distance <= maxDistance || m_weaponType == 7) // Para melee siempre atacamos si estamos en rango melee
				{
					if (m_currentWeaponSlot == -1 && m_weaponType != 7)
					{
						FindRangedWeapon();
					}

					if (m_currentWeaponSlot != -1 || m_weaponType == 7)
					{
						ProcessWeaponBehavior(target, distance);
					}
				}
				else
				{
					// Si el arma es lanzable, no reseteamos el slot, solo limpiamos animaciones
					if (m_weaponType == 6)
					{
						m_isThrowableAiming = false;
						m_isThrowableThrowing = false;
						if (m_componentModel != null)
						{
							m_componentModel.AimHandAngleOrder = 0f;
							m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
							m_componentModel.InHandItemRotationOrder = Vector3.Zero;
							m_componentModel.LookAtOrder = null;
						}
					}
					else if (m_weaponType != 7) // No resetear si es melee
					{
						ResetWeaponState();
						if (m_componentModel != null)
						{
							m_componentModel.AimHandAngleOrder = 0f;
							m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
							m_componentModel.InHandItemRotationOrder = Vector3.Zero;
							m_componentModel.LookAtOrder = null;
						}
					}
				}
			}
			else
			{
				ResetWeaponState();
				if (m_componentModel != null)
				{
					m_componentModel.AimHandAngleOrder = 0f;
					m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
					m_componentModel.InHandItemRotationOrder = Vector3.Zero;
					m_componentModel.LookAtOrder = null;
				}
			}
		}

		// AÑADIDO: Busca un arma cuerpo a cuerpo en el inventario
		private int FindMeleeWeapon()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];

					// Consideramos arma cuerpo a cuerpo cualquier bloque que NO sea de distancia
					bool isRanged = (block is BowBlock || block is CrossbowBlock || block is MusketBlock ||
									block is FlameThrowerBlock || block is RepeatCrossbowBlock ||
									IsThrowableBlock(block) || m_firearmConfigs.ContainsKey(BlocksManager.GetBlockIndex(block.GetType(), true, false)));

					if (!isRanged)
					{
						return i;
					}
				}
			}
			return -1;
		}

		private float GetWeaponMaxDistance()
		{
			if (m_weaponType == 3) return m_flameMaxDistance;
			if (m_weaponType == 5)
			{
				if (m_currentWeaponSlot >= 0)
				{
					int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
					int blockIndex = Terrain.ExtractContents(slotValue);
					if (m_firearmConfigs.ContainsKey(blockIndex) && m_firearmConfigs[blockIndex].IsSniper)
					{
						return m_maxDistance * 2f;
					}
				}
			}
			if (m_weaponType == 6) return m_throwableMaxRange;
			return m_maxDistance;
		}

		private ArrowBlock.ArrowType SelectBoltTypeBasedOnDistance(float distance)
		{
			List<ArrowBlock.ArrowType> availableBolts = new List<ArrowBlock.ArrowType>(m_crossbowBoltTypes);

			if (distance < m_explosiveBoltMinDistance)
			{
				availableBolts.Remove(ArrowBlock.ArrowType.ExplosiveBolt);
			}

			if (availableBolts.Count == 0)
			{
				availableBolts.Add(ArrowBlock.ArrowType.IronBolt);
			}

			return availableBolts[m_random.Int(0, availableBolts.Count - 1)];
		}

		private RepeatArrowBlock.ArrowType SelectRepeatArrowTypeBasedOnDistance(float distance)
		{
			List<RepeatArrowBlock.ArrowType> availableArrows = new List<RepeatArrowBlock.ArrowType>(m_repeatCrossbowArrowTypes);

			if (distance < m_explosiveRepeatArrowMinDistance)
			{
				availableArrows.Remove(RepeatArrowBlock.ArrowType.ExplosiveArrow);
			}

			if (availableArrows.Count == 0)
			{
				availableArrows.Add(RepeatArrowBlock.ArrowType.IronArrow);
			}

			return availableArrows[m_random.Int(0, availableArrows.Count - 1)];
		}

		private void FindRangedWeapon()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];

					if (block is BowBlock)
					{
						m_currentWeaponSlot = i;
						m_weaponType = 0;
						m_componentInventory.ActiveSlotIndex = i;
						StartAiming();
						break;
					}
					else if (block is CrossbowBlock)
					{
						m_currentWeaponSlot = i;
						m_weaponType = 1;
						m_componentInventory.ActiveSlotIndex = i;
						StartAiming();
						break;
					}
					else if (block is MusketBlock)
					{
						m_currentWeaponSlot = i;
						m_weaponType = 2;
						m_componentInventory.ActiveSlotIndex = i;
						StartAiming();
						break;
					}
					else if (block is FlameThrowerBlock)
					{
						m_currentWeaponSlot = i;
						m_weaponType = 3;
						m_componentInventory.ActiveSlotIndex = i;
						StartAiming();
						break;
					}
					else if (block is RepeatCrossbowBlock)
					{
						m_currentWeaponSlot = i;
						m_weaponType = 4;
						m_componentInventory.ActiveSlotIndex = i;
						StartAiming();
						break;
					}
					else if (m_firearmConfigs.ContainsKey(BlocksManager.GetBlockIndex(block.GetType(), true, false)))
					{
						m_currentWeaponSlot = i;
						m_weaponType = 5;
						m_componentInventory.ActiveSlotIndex = i;
						StartFirearmAiming();
						break;
					}
					else if (IsThrowableBlock(block))
					{
						m_currentWeaponSlot = i;
						m_weaponType = 6;
						m_componentInventory.ActiveSlotIndex = i;
						StartThrowableAiming();
						break;
					}
				}
			}
		}

		// AÑADIDO: Golpear cuerpo a cuerpo con arma a distancia
		private void ProcessRangedWeaponAsMelee(ComponentCreature target, float distance)
		{
			if (target == null) return;

			// Si estamos demasiado lejos para atacar cuerpo a cuerpo
			if (distance > 4f) return;

			// Detener el movimiento para atacar
			if (m_componentPathfinding != null)
				m_componentPathfinding.Destination = null;

			// Mirar al objetivo
			if (m_componentModel != null)
				m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);

			// Atacar con el intervalo definido
			if (m_subsystemTime.GameTime >= m_nextMeleeAttackTime)
			{
				if (m_componentMiner != null)
				{
					ComponentBody targetBody = target.ComponentBody;
					Vector3 hitPoint = targetBody.Position;
					Vector3 hitDirection = Vector3.Normalize(targetBody.Position - m_componentCreature.ComponentBody.Position);

					m_componentMiner.Hit(targetBody, hitPoint, hitDirection);

					m_nextMeleeAttackTime = m_subsystemTime.GameTime + m_meleeAttackInterval;
				}
			}
		}

		private void ProcessWeaponBehavior(ComponentCreature target, float distance)
		{
			// Si estamos en distancia corta y el arma es de distancia, usar ataque cuerpo a cuerpo con ella
			if (distance <= m_meleeSwitchDistance && m_weaponType >= 0 && m_weaponType <= 6)
			{
				ProcessRangedWeaponAsMelee(target, distance);
				return;
			}

			switch (m_weaponType)
			{
				case 0: ProcessBowBehavior(target); break;
				case 1: ProcessCrossbowBehavior(target, distance); break;
				case 2: ProcessMusketBehavior(target); break;
				case 3: ProcessFlameThrowerBehavior(target, distance); break;
				case 4: ProcessRepeatCrossbowBehavior(target, distance); break;
				case 5: ProcessFirearmBehavior(target, distance); break;
				case 6: ProcessThrowableBehavior(target, distance); break;
				case 7: ProcessMeleeBehavior(target, distance); break;
			}
		}

		// AÑADIDO: Comportamiento de ataque cuerpo a cuerpo
		private void ProcessMeleeBehavior(ComponentCreature target, float distance)
		{
			if (target == null) return;

			// Si estamos demasiado lejos para atacar cuerpo a cuerpo, no hacer nada (la persecución nos acercará)
			if (distance > 4f) return;

			// Detener el movimiento para atacar
			if (m_componentPathfinding != null)
			{
				m_componentPathfinding.Destination = null;
			}

			// Mirar al objetivo
			if (m_componentModel != null && target != null)
			{
				m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
			}

			// Atacar con el intervalo definido
			if (m_subsystemTime.GameTime >= m_nextMeleeAttackTime)
			{
				// Usar ComponentMiner para golpear (ataque cuerpo a cuerpo)
				if (m_componentMiner != null)
				{
					// CORREGIDO: Pasar los tres parámetros requeridos
					ComponentBody targetBody = target.ComponentBody;
					Vector3 hitPoint = targetBody.Position; // Punto de impacto (puedes usar alguna parte del cuerpo si existe)
					Vector3 hitDirection = Vector3.Normalize(targetBody.Position - m_componentCreature.ComponentBody.Position);

					// ¡FALTABA ESTA LÍNEA!
					m_componentMiner.Hit(targetBody, hitPoint, hitDirection);

					m_nextMeleeAttackTime = m_subsystemTime.GameTime + m_meleeAttackInterval;
				}
			}
		}

		private void StartThrowableAiming()
		{
			m_isThrowableAiming = true;
			m_isThrowableThrowing = false;
			m_animationStartTime = m_subsystemTime.GameTime;
		}

		private void ProcessThrowableBehavior(ComponentCreature target, float distance)
		{
			if (target == null) return;

			// Verificar línea de visión
			bool hasLineOfSight = HasClearLineOfSight(target);

			// Si NO hay línea de visión, NO detenerse - dejar que la persecución mueva al zombie
			if (!hasLineOfSight)
			{
				// Cancelar cualquier estado de lanzamiento en progreso
				m_isThrowableAiming = false;
				m_isThrowableThrowing = false;
				// Resetear el modelo
				if (m_componentModel != null)
				{
					m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
					m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
					m_componentModel.InHandItemRotationOrder = Vector3.Zero;
					m_componentModel.LookAtOrder = null;
				}
				// IMPORTANTE: NO detener el movimiento
				return;
			}

			// Verificar rango: si está fuera de la distancia óptima, cancelar lanzamiento
			if (distance < m_throwableMinRange || distance > m_throwableMaxRange)
			{
				// Cancelar cualquier estado de lanzamiento en progreso
				m_isThrowableAiming = false;
				m_isThrowableThrowing = false;
				// Resetear el modelo
				if (m_componentModel != null)
				{
					m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
					m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
					m_componentModel.InHandItemRotationOrder = Vector3.Zero;
					m_componentModel.LookAtOrder = null;
				}
				// IMPORTANTE: NO detener el movimiento
				return;
			}

			// SOLO cuando hay línea de visión Y estamos en rango: detener el movimiento para lanzar
			if (m_componentPathfinding != null)
			{
				m_componentPathfinding.Destination = null;
			}

			// Resto de la lógica de lanzamiento igual...
			if (!m_isThrowableAiming && !m_isThrowableThrowing)
			{
				StartThrowableAiming();
			}

			if (m_isThrowableAiming)
			{
				ApplyThrowableAimingAnimation(target);

				if (m_subsystemTime.GameTime - m_animationStartTime >= m_throwableAimTime)
				{
					m_isThrowableAiming = false;
					ThrowThrowableWeapon(target);
				}
			}
			else if (m_isThrowableThrowing)
			{
				ApplyThrowableThrowingAnimation();

				if (m_subsystemTime.GameTime - m_throwableThrowTime >= 0.3f)
				{
					m_isThrowableThrowing = false;

					int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
					if (slotValue != 0)
					{
						StartThrowableAiming();
					}
					else
					{
						ResetWeaponState();
					}
				}
				if (m_componentModel != null)
				{
					m_componentModel.AimHandAngleOrder = 0f;
					m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
					m_componentModel.InHandItemRotationOrder = Vector3.Zero;
					m_componentModel.LookAtOrder = null;
				}
			}
		}

		private void ApplyThrowableAimingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				// Solo para lanzables permitimos un ángulo mayor
				m_componentModel.AimHandAngleOrder = 2f; // Esto está bien porque es específico para lanzar

				if (m_currentWeaponSlot >= 0)
				{
					int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];

					if (block is SpearBlock)
					{
						m_componentModel.InHandItemOffsetOrder = new Vector3(0f, -0.25f, 0f);
						m_componentModel.InHandItemRotationOrder = new Vector3(3.14159f, 0f, 0f);
					}
					else
					{
						m_componentModel.InHandItemOffsetOrder = new Vector3(0f, 0f, 0f);
						m_componentModel.InHandItemRotationOrder = new Vector3(0f, 0f, 0f);
					}
				}

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void ApplyThrowableThrowingAnimation()
		{
			if (m_componentModel != null)
			{
				float throwProgress = (float)((m_subsystemTime.GameTime - m_throwableThrowTime) / 0.3f);

				m_componentModel.AimHandAngleOrder = 2f * (1f - throwProgress);
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
			}
		}

		private void ThrowThrowableWeapon(ComponentCreature target)
		{
			if (target == null) return;

			try
			{
				int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				if (slotValue == 0)
				{
					ResetWeaponState();
					return;
				}

				m_isThrowableThrowing = true;
				m_throwableThrowTime = m_subsystemTime.GameTime;

				Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];

				Vector3 throwPosition = m_componentCreature.ComponentCreatureModel.EyePosition +
									   m_componentCreature.ComponentBody.Matrix.Right * 0.4f;

				Vector3 targetPosition = target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - throwPosition);

				direction += new Vector3(
					m_random.Float(-0.05f, 0.05f),
					m_random.Float(-0.03f, 0.03f),
					m_random.Float(-0.05f, 0.05f)
				);
				direction = Vector3.Normalize(direction);

				float speed = block.GetProjectileSpeed(slotValue);

				if (speed < 10f)
				{
					speed = 25f;
				}

				Vector3 angularVelocity = new Vector3(
					m_random.Float(5f, 10f),
					m_random.Float(5f, 10f),
					m_random.Float(5f, 10f)
				);

				if (m_subsystemProjectiles.FireProjectile(
					slotValue,
					throwPosition,
					direction * speed,
					angularVelocity,
					m_componentCreature) != null)
				{
					m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);

					m_subsystemAudio.PlaySound("Audio/Throw", 0.25f, m_random.Float(-0.2f, 0.2f),
						m_componentCreature.ComponentBody.Position, 2f, false);
				}
			}
			catch (Exception)
			{
				ResetWeaponState();
			}
		}

		private void StartFirearmAiming()
		{
			m_isFirearmAiming = true;
			m_isFirearmFiring = false;
			m_isFirearmReloading = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_shotsSinceLastReload = 0;
			m_hasFirearmAimed = false;
			m_lastFirearmShotTime = 0;
		}

		private void ProcessFirearmBehavior(ComponentCreature target, float distance)
		{
			if (m_currentWeaponSlot < 0) return;

			int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
			int blockIndex = Terrain.ExtractContents(slotValue);

			if (!m_firearmConfigs.ContainsKey(blockIndex))
			{
				ResetWeaponState();
				return;
			}

			FirearmConfig config = m_firearmConfigs[blockIndex];

			if (m_isFirearmReloading)
			{
				ApplyFirearmReloadingAnimation();

				if (m_subsystemTime.GameTime - m_firearmReloadStartTime >= m_firearmReloadTime)
				{
					m_isFirearmReloading = false;
					m_shotsSinceLastReload = 0;

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
					m_isFirearmAiming = true;
					m_animationStartTime = m_subsystemTime.GameTime;
					m_hasFirearmAimed = false;
				}
				return;
			}

			if (m_isFirearmFiring)
			{
				ApplyFirearmFiringAnimation();

				float fireAnimationTime = config.IsAutomatic ? 0.1f : 0.2f;
				if (config.IsSniper)
				{
					fireAnimationTime = 0.5f;
				}
				else if (config.IsShotgun)
				{
					fireAnimationTime = 0.3f;
				}

				if (m_subsystemTime.GameTime - m_fireTime >= fireAnimationTime)
				{
					m_isFirearmFiring = false;

					if (m_shotsSinceLastReload >= config.MaxShotsBeforeReload)
					{
						StartFirearmReloading();
						return;
					}

					m_isFirearmAiming = true;
					m_animationStartTime = m_subsystemTime.GameTime;
				}
				return;
			}

			if (m_isFirearmAiming)
			{
				float aimTime = config.IsSniper ? m_sniperAimTime : m_firearmAimTime;

				if (!m_hasFirearmAimed)
				{
					ApplyFirearmAimingAnimation(target, config);
					if (m_subsystemTime.GameTime - m_animationStartTime >= aimTime)
					{
						m_hasFirearmAimed = true;
					}
					else
					{
						return;
					}
				}

				ApplyFirearmAimingAnimation(target, config);

				if (m_subsystemTime.GameTime - m_lastFirearmShotTime >= config.FireRate)
				{
					FireFirearm(target, config);
					m_lastFirearmShotTime = m_subsystemTime.GameTime;
					m_shotsSinceLastReload++;

					m_isFirearmAiming = false;
					m_isFirearmFiring = true;
					m_fireTime = m_subsystemTime.GameTime;
				}
			}
		}

		private void ApplyFirearmAimingAnimation(ComponentCreature target, FirearmConfig config)
		{
			if (m_componentModel != null)
			{
				// SOLO usar AimHandAngleOrder + un pequeño offset si es necesario
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder + ((AimHandAngleOrder != 0f) ? 0.1f : 0f);

				// El resto de offsets y rotaciones para el arma
				if (config.IsSniper)
				{
					m_componentModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.06f, 0.08f);
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
				}
				else
				{
					m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
				}

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void ApplyFirearmFiringAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder + ((AimHandAngleOrder != 0f) ? 0.1f : 0f);
				// Mantener las mismas rotaciones que en aiming
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
			}
		}

		private void ApplyFirearmReloadingAnimation()
		{
			if (m_componentModel != null)
			{
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_firearmReloadStartTime) / m_firearmReloadTime);

				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
				m_componentModel.LookAtOrder = null;
			}
		}

		private void StartFirearmReloading()
		{
			m_isFirearmAiming = false;
			m_isFirearmFiring = false;
			m_isFirearmReloading = true;
			m_firearmReloadStartTime = m_subsystemTime.GameTime;

			m_subsystemAudio.PlaySound("Audio/Armas/reload", 0.8f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 5f, false);

			if (m_subsystemParticles != null && m_subsystemTerrain != null)
			{
				try
				{
					Vector3 basePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
					KillParticleSystem reloadParticles = new KillParticleSystem(m_subsystemTerrain, basePosition, 0.5f);
					m_subsystemParticles.AddParticleSystem(reloadParticles, false);
					for (int i = 0; i < 3; i++)
					{
						Vector3 offset = new Vector3(
							m_random.Float(-0.2f, 0.2f),
							m_random.Float(0.1f, 0.4f),
							m_random.Float(-0.2f, 0.2f)
						);
						KillParticleSystem additionalParticles = new KillParticleSystem(m_subsystemTerrain, basePosition + offset, 0.5f);
						m_subsystemParticles.AddParticleSystem(additionalParticles, false);
					}
				}
				catch (Exception)
				{
				}
			}

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void FireFirearm(ComponentCreature target, FirearmConfig config)
		{
			if (target == null) return;

			try
			{
				Vector3 shootPosition = m_componentCreature.ComponentCreatureModel.EyePosition +
					m_componentCreature.ComponentBody.Matrix.Right * 0.3f -
					m_componentCreature.ComponentBody.Matrix.Up * 0.2f;

				Vector3 targetPosition = target.ComponentCreatureModel.EyePosition;
				if (config.IsSniper)
				{
					targetPosition = target.ComponentBody.Position;
					targetPosition.Y += 0.5f;
				}

				Vector3 direction = Vector3.Normalize(targetPosition - shootPosition);
				Vector3 rightVector = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
				Vector3 upVector = Vector3.Normalize(Vector3.Cross(direction, rightVector));

				for (int i = 0; i < config.ProjectilesPerShot; i++)
				{
					Vector3 spread = m_random.Float(-config.SpreadVector.X, config.SpreadVector.X) * rightVector +
						m_random.Float(-config.SpreadVector.Y, config.SpreadVector.Y) * upVector +
						m_random.Float(-config.SpreadVector.Z, config.SpreadVector.Z) * direction;

					int bulletBlockIndex = BlocksManager.GetBlockIndex(config.BulletBlockType, true, false);
					if (bulletBlockIndex > 0)
					{
						int bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, 0);

						m_subsystemProjectiles.FireProjectile(
							bulletValue,
							shootPosition,
							config.BulletSpeed * (direction + spread),
							Vector3.Zero,
							m_componentCreature
						);
					}
				}

				Vector3 particlePosition = shootPosition + direction * 1.3f;
				if (m_subsystemParticles != null && m_subsystemTerrain != null)
				{
					m_subsystemParticles.AddParticleSystem(
						new GunFireParticleSystem(m_subsystemTerrain, particlePosition, direction),
						false
					);
				}

				if (m_subsystemNoise != null)
				{
					m_subsystemNoise.MakeNoise(shootPosition, 0.8f, config.NoiseRadius);
				}

				float pitchVariation = config.IsSniper ?
					m_random.Float(-0.05f, 0.05f) :
					m_random.Float(-0.1f, 0.1f);

				m_subsystemAudio.PlaySound(config.ShootSound, 1f, pitchVariation,
					m_componentCreature.ComponentBody.Position, 15f, false);

				if (target != null && !config.IsAutomatic)
				{
					Vector3 recoilDirection = Vector3.Normalize(
						target.ComponentBody.Position - m_componentCreature.ComponentBody.Position
					);
					float recoilForce = config.IsShotgun ? 2f : 1f;
					m_componentCreature.ComponentBody.ApplyImpulse(-recoilDirection * recoilForce);
				}
			}
			catch { }
		}

		private void ProcessBowBehavior(ComponentCreature target)
		{
			if (!m_isAiming && !m_isDrawing && !m_isFiring)
			{
				StartAiming();
			}

			if (m_isAiming)
			{
				ApplyBowAimingAnimation(target);

				if (m_subsystemTime.GameTime - m_animationStartTime >= m_aimTime)
				{
					m_isAiming = false;
					StartBowDrawing();
				}
			}
			else if (m_isDrawing)
			{
				ApplyBowDrawingAnimation(target);

				m_currentDraw = MathUtils.Clamp((float)((m_subsystemTime.GameTime - m_drawStartTime) / m_drawTime), 0f, 1f);

				if (!m_hasArrowInBow)
				{
					SetBowWithArrow((int)(m_currentDraw * 15f));
					m_hasArrowInBow = true;
				}
				else
				{
					UpdateBowDraw((int)(m_currentDraw * 15f));
				}

				if (m_subsystemTime.GameTime - m_drawStartTime >= m_drawTime)
				{
					FireBow(target);
				}
			}
			else if (m_isFiring)
			{
				ApplyBowFiringAnimation();

				if (m_subsystemTime.GameTime - m_fireTime >= 0.2)
				{
					m_isFiring = false;

					if (m_hasArrowInBow)
					{
						ClearArrowFromBow();
						m_hasArrowInBow = false;
					}

					if (m_subsystemTime.GameTime - m_fireTime >= 0.8)
					{
						StartAiming();
					}
				}
			}
		}

		private void SetBowWithArrow(int drawValue)
		{
			if (m_currentWeaponSlot < 0) return;

			try
			{
				int currentBowValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				if (currentBowValue == 0) return;

				int currentData = Terrain.ExtractData(currentBowValue);
				m_currentArrowType = m_bowArrowTypes[m_random.Int(0, m_bowArrowTypes.Length - 1)];

				int newData = BowBlock.SetArrowType(currentData, m_currentArrowType);
				newData = BowBlock.SetDraw(newData, MathUtils.Clamp(drawValue, 0, 15));

				int newBowValue = Terrain.ReplaceData(currentBowValue, newData);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newBowValue, 1);
				m_arrowVisible = true;
			}
			catch { }
		}

		private void UpdateBowDraw(int drawValue)
		{
			if (m_currentWeaponSlot < 0 || !m_arrowVisible) return;

			try
			{
				int currentBowValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				if (currentBowValue == 0) return;

				int currentData = Terrain.ExtractData(currentBowValue);
				int newData = BowBlock.SetDraw(currentData, MathUtils.Clamp(drawValue, 0, 15));

				int newBowValue = Terrain.ReplaceData(currentBowValue, newData);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newBowValue, 1);
			}
			catch { }
		}

		private void ClearArrowFromBow()
		{
			if (m_currentWeaponSlot < 0) return;

			try
			{
				int currentBowValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				if (currentBowValue == 0) return;

				int currentData = Terrain.ExtractData(currentBowValue);
				int newData = BowBlock.SetArrowType(currentData, null);
				newData = BowBlock.SetDraw(newData, 0);

				int newBowValue = Terrain.ReplaceData(currentBowValue, newData);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newBowValue, 1);
				m_arrowVisible = false;
			}
			catch { }
		}

		private void StartBowDrawing()
		{
			m_isDrawing = true;
			m_drawStartTime = m_subsystemTime.GameTime;
			m_subsystemAudio.PlaySound("Audio/BowDraw", 0.5f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);
		}

		private void ApplyBowAimingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder + ((AimHandAngleOrder != 0f) ? 0.1f : 0f);
				m_componentModel.InHandItemOffsetOrder = new Vector3(0.05f, 0.05f, 0.05f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-0.05f, 0.3f, 0.05f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void ApplyBowDrawingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				float drawFactor = m_currentDraw;

				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(0.05f, 0.05f, 0.05f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-0.05f, 0.3f, 0.05f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void FireBow(ComponentCreature target)
		{
			m_isDrawing = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;
			ShootBowArrow(target);
			m_subsystemAudio.PlaySound("Audio/Bow", 0.8f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 15f, false);
		}

		private void ApplyBowFiringAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(0.05f, 0.05f, 0.05f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-0.05f, 0.3f, 0.05f);
			}
		}

		private void ProcessCrossbowBehavior(ComponentCreature target, float distance)
		{
			if (!m_isAiming && !m_isDrawing && !m_isFiring && !m_isReloading)
			{
				StartAiming();
			}

			if (m_isAiming)
			{
				ApplyCrossbowAimingAnimation(target);

				if (m_subsystemTime.GameTime - m_animationStartTime >= m_aimTime)
				{
					m_isAiming = false;
					StartCrossbowDrawing(distance);
				}
			}
			else if (m_isDrawing)
			{
				ApplyCrossbowDrawingAnimation(target);

				m_currentDraw = MathUtils.Clamp((float)((m_subsystemTime.GameTime - m_drawStartTime) / m_drawTime), 0f, 1f);
				SetCrossbowWithBolt((int)(m_currentDraw * 15f), false, distance);

				if (m_subsystemTime.GameTime - m_drawStartTime >= m_drawTime)
				{
					LoadCrossbowBolt(distance);
				}
			}
			else if (m_isReloading)
			{
				ApplyCrossbowReloadingAnimation(target);

				if (m_subsystemTime.GameTime - m_animationStartTime >= 0.3f)
				{
					m_isReloading = false;
					FireCrossbow(target);
				}
			}
			else if (m_isFiring)
			{
				ApplyCrossbowFiringAnimation();

				if (m_subsystemTime.GameTime - m_fireTime >= 0.2f)
				{
					m_isFiring = false;
					ClearBoltFromCrossbow();

					if (m_subsystemTime.GameTime - m_fireTime >= 0.8f)
					{
						StartAiming();
					}
				}
			}
		}

		private void SetCrossbowWithBolt(int drawValue, bool hasBolt, float distance)
		{
			if (m_currentWeaponSlot < 0) return;

			try
			{
				int currentCrossbowValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				if (currentCrossbowValue == 0) return;

				int currentData = Terrain.ExtractData(currentCrossbowValue);

				if (hasBolt)
				{
					m_currentBoltType = SelectBoltTypeBasedOnDistance(distance);
				}

				ArrowBlock.ArrowType? boltType = hasBolt ? m_currentBoltType : (ArrowBlock.ArrowType?)null;

				int newData = CrossbowBlock.SetDraw(currentData, MathUtils.Clamp(drawValue, 0, 15));
				newData = CrossbowBlock.SetArrowType(newData, boltType);

				int newCrossbowValue = Terrain.ReplaceData(currentCrossbowValue, newData);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newCrossbowValue, 1);
			}
			catch { }
		}

		private void ClearBoltFromCrossbow()
		{
			SetCrossbowWithBolt(0, false, m_currentTargetDistance);
		}

		private void StartCrossbowDrawing(float distance)
		{
			m_isDrawing = true;
			m_drawStartTime = m_subsystemTime.GameTime;
			m_subsystemAudio.PlaySound("Audio/CrossbowDraw", 0.5f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);
		}

		private void LoadCrossbowBolt(float distance)
		{
			m_isDrawing = false;
			m_isReloading = true;
			m_animationStartTime = m_subsystemTime.GameTime;
			SetCrossbowWithBolt(15, true, distance);
			m_subsystemAudio.PlaySound("Audio/Reload", 1f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);
		}

		private void ApplyCrossbowAimingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder + ((AimHandAngleOrder != 0f) ? 0.1f : 0f);
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void ApplyCrossbowDrawingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void ApplyCrossbowReloadingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void FireCrossbow(ComponentCreature target)
		{
			m_isReloading = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;
			ShootCrossbowBolt(target);
			m_subsystemAudio.PlaySound("Audio/Bow", 0.8f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 15f, false);

			if (target != null)
			{
				Vector3 direction = Vector3.Normalize(
					target.ComponentBody.Position - m_componentCreature.ComponentBody.Position
				);
				m_componentCreature.ComponentBody.ApplyImpulse(-direction * 1.5f);
			}
		}

		private void ApplyCrossbowFiringAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
			}
		}

		private void ProcessMusketBehavior(ComponentCreature target)
		{
			if (!m_isAiming && !m_isFiring && !m_isReloading && !m_isCocking)
			{
				StartAiming();
			}

			if (m_isCocking)
			{
				if (m_componentModel != null)
				{
					m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
					m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

					if (target != null)
					{
						m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
					}
				}

				if (m_subsystemTime.GameTime - m_drawStartTime >= m_cockTime)
				{
					m_isCocking = false;
					m_isAiming = true;
					m_animationStartTime = m_subsystemTime.GameTime;
					UpdateMusketHammerState(true);
				}
			}
			else if (m_isAiming)
			{
				ApplyMusketAimingAnimation(target);

				if (m_subsystemTime.GameTime - m_animationStartTime >= m_aimTime)
				{
					FireMusket(target);
				}
			}
			else if (m_isFiring)
			{
				ApplyMusketFiringAnimation();

				if (m_subsystemTime.GameTime - m_fireTime >= 0.2)
				{
					m_isFiring = false;
					StartMusketReloading();
				}
			}
			else if (m_isReloading)
			{
				ApplyMusketReloadingAnimation();

				if (m_subsystemTime.GameTime - m_animationStartTime >= m_reloadTime)
				{
					m_isReloading = false;
					UpdateMusketLoadState(MusketBlock.LoadState.Loaded);
					UpdateMusketBulletType(BulletBlock.BulletType.MusketBall);
					StartMusketCocking();
				}
			}
		}

		private void StartMusketCocking()
		{
			m_isCocking = true;
			m_isAiming = false;
			m_drawStartTime = m_subsystemTime.GameTime;
			UpdateMusketHammerState(true);
			m_subsystemAudio.PlaySound("Audio/HammerCock", 1.5f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.6f, 0f, 0f);
			}
		}

		private void StartMusketReloading()
		{
			m_isReloading = true;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_subsystemAudio.PlaySound("Audio/Reload", 1.5f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 5f, false);
		}

		private void UpdateMusketHammerState(bool hammerState)
		{
			if (m_currentWeaponSlot >= 0)
			{
				int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				int contents = Terrain.ExtractContents(slotValue);
				if (BlocksManager.Blocks[contents] is MusketBlock)
				{
					int data = Terrain.ExtractData(slotValue);
					data = MusketBlock.SetHammerState(data, hammerState);
					int newValue = Terrain.ReplaceData(slotValue, data);
					m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
					m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
				}
			}
		}

		private void UpdateMusketLoadState(MusketBlock.LoadState loadState)
		{
			if (m_currentWeaponSlot >= 0)
			{
				int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				int contents = Terrain.ExtractContents(slotValue);
				if (BlocksManager.Blocks[contents] is MusketBlock)
				{
					int data = Terrain.ExtractData(slotValue);
					data = MusketBlock.SetLoadState(data, loadState);
					int newValue = Terrain.ReplaceData(slotValue, data);
					m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
					m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
				}
			}
		}

		private void UpdateMusketBulletType(BulletBlock.BulletType bulletType)
		{
			if (m_currentWeaponSlot >= 0)
			{
				int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				int contents = Terrain.ExtractContents(slotValue);
				if (BlocksManager.Blocks[contents] is MusketBlock)
				{
					int data = Terrain.ExtractData(slotValue);
					data = MusketBlock.SetBulletType(data, bulletType);
					int newValue = Terrain.ReplaceData(slotValue, data);
					m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
					m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
				}
			}
		}

		private void ApplyMusketAimingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder + ((AimHandAngleOrder != 0f) ? 0.1f : 0f);
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void FireMusket(ComponentCreature target)
		{
			m_isAiming = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;
			UpdateMusketHammerState(true);
			double fireDelay = 0.05;

			m_subsystemAudio.PlaySound("Audio/HammerUncock", 1f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);

			m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + fireDelay, delegate
			{
				m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 15f, false);
			});

			UpdateMusketHammerState(false);
			UpdateMusketLoadState(MusketBlock.LoadState.Empty);
			ShootMusketBullet(target);

			if (target != null)
			{
				Vector3 direction = Vector3.Normalize(
					target.ComponentBody.Position - m_componentCreature.ComponentBody.Position
				);
				m_componentCreature.ComponentBody.ApplyImpulse(-direction * 3f);
			}
		}

		private void ApplyMusketFiringAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
			}
		}

		private void ApplyMusketReloadingAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ProcessFlameThrowerBehavior(ComponentCreature target, float distance)
		{
			if (!m_isAiming && !m_isFlameFiring && !m_isReloading && !m_isFlameCocking)
			{
				StartAiming();
			}

			int currentSlotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
			int currentData = Terrain.ExtractData(currentSlotValue);
			FlameThrowerBlock.LoadState currentLoadState = FlameThrowerBlock.GetLoadState(currentData);
			int currentLoadCount = FlameThrowerBlock.GetLoadCount(currentSlotValue);
			bool currentSwitchState = FlameThrowerBlock.GetSwitchState(currentData);
			var currentBulletType = FlameThrowerBlock.GetBulletType(currentData);

			if (m_isFlameCocking)
			{
				ApplyFlameThrowerCockingAnimation(target);

				if (m_subsystemTime.GameTime - m_cockStartTime >= m_flameCockTime)
				{
					m_isFlameCocking = false;
					m_flameSwitchState = true;

					if (m_currentWeaponSlot >= 0)
					{
						int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
						int data = Terrain.ExtractData(slotValue);
						data = FlameThrowerBlock.SetSwitchState(data, true);
						int newValue = Terrain.ReplaceData(slotValue, data);
						m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
						m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
					}

					m_subsystemAudio.PlaySound("Audio/Items/Hammer Cock Remake", 1f, m_random.Float(-0.1f, 0.1f),
						m_componentCreature.ComponentBody.Position, 3f, false);

					StartFlameThrowerFiring();
				}
			}
			else if (m_isAiming)
			{
				ApplyFlameThrowerAimingAnimation(target);

				float aimProgress = (float)(m_subsystemTime.GameTime - m_animationStartTime);

				if (aimProgress > 0.5f && !currentSwitchState)
				{
					m_isAiming = false;
					StartFlameThrowerCocking();
				}
				else if (aimProgress >= 0.3f && currentSwitchState && currentLoadState == FlameThrowerBlock.LoadState.Loaded && currentLoadCount > 0)
				{
					StartFlameThrowerFiring();
				}
				else if (currentLoadState != FlameThrowerBlock.LoadState.Loaded || currentLoadCount <= 0)
				{
					m_isAiming = false;
					StartFlameThrowerReloading();
				}
			}
			else if (m_isFlameFiring)
			{
				ApplyFlameThrowerFiringAnimation(target);

				if (currentLoadCount <= 0 || currentLoadState != FlameThrowerBlock.LoadState.Loaded)
				{
					StopFlameThrowerFiring();
					StartFlameThrowerReloading();
					return;
				}

				if (m_subsystemTime.GameTime >= m_nextFlameShotTime)
				{
					FireFlameThrowerShot(target);
					m_nextFlameShotTime = m_subsystemTime.GameTime + m_flameShotInterval;

					if (currentLoadCount > 1)
					{
						int newValue = FlameThrowerBlock.SetLoadCount(currentSlotValue, currentLoadCount - 1);
						m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
						m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
					}
					else
					{
						currentData = FlameThrowerBlock.SetLoadState(currentData, FlameThrowerBlock.LoadState.Empty);
						currentData = FlameThrowerBlock.SetBulletType(currentData, null);
						int newValue = Terrain.ReplaceData(currentSlotValue, currentData);
						newValue = FlameThrowerBlock.SetLoadCount(newValue, 0);
						m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
						m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
					}
				}

				if (m_subsystemTime.GameTime >= m_nextFlameSoundTime)
				{
					PlayFlameThrowerSound();
					m_nextFlameSoundTime = m_subsystemTime.GameTime + m_flameSoundInterval;
				}

				if (m_subsystemTime.GameTime - m_flameStartTime >= 3.0)
				{
					StopFlameThrowerFiring();
					m_animationStartTime = m_subsystemTime.GameTime;
					m_isAiming = true;
				}
			}
			else if (m_isReloading)
			{
				ApplyFlameThrowerReloadingAnimation();

				if (m_subsystemTime.GameTime - m_animationStartTime >= 1.5f)
				{
					m_isReloading = false;

					m_currentFlameBulletType = (m_random.Int(0, 100) < 70) ?
						FlameBulletBlock.FlameBulletType.Flame :
						FlameBulletBlock.FlameBulletType.Poison;

					int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
					int data = Terrain.ExtractData(slotValue);

					data = FlameThrowerBlock.SetLoadState(data, FlameThrowerBlock.LoadState.Loaded);
					data = FlameThrowerBlock.SetBulletType(data, m_currentFlameBulletType);
					data = FlameThrowerBlock.SetSwitchState(data, false);

					int newValue = Terrain.ReplaceData(slotValue, data);
					newValue = FlameThrowerBlock.SetLoadCount(newValue, 15);

					m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
					m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);

					m_flameSwitchState = false;
					m_isAiming = true;
					m_animationStartTime = m_subsystemTime.GameTime;
				}
			}
		}

		private void PlayFlameThrowerSound()
		{
			if (m_currentFlameBulletType == FlameBulletBlock.FlameBulletType.Flame)
			{
				m_subsystemAudio.PlaySound("Audio/Flamethrower/Flamethrower Fire", 0.8f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 10f, false);
			}
			else
			{
				m_subsystemAudio.PlaySound("Audio/Flamethrower/PoisonSmoke", 0.8f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 8f, false);
			}
		}

		private void StartFlameThrowerCocking()
		{
			m_isFlameCocking = true;
			m_cockStartTime = m_subsystemTime.GameTime;

			m_subsystemAudio.PlaySound("Audio/Items/Hammer Cock Remake", 0.7f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 2f, false);
		}

		private void StartFlameThrowerFiring()
		{
			int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
			int data = Terrain.ExtractData(slotValue);
			FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
			int loadCount = FlameThrowerBlock.GetLoadCount(slotValue);

			var bulletType = FlameThrowerBlock.GetBulletType(data);
			if (bulletType.HasValue)
			{
				m_currentFlameBulletType = bulletType.Value;
			}
			else
			{
				m_currentFlameBulletType = FlameBulletBlock.FlameBulletType.Flame;
			}

			if (loadState != FlameThrowerBlock.LoadState.Loaded || loadCount <= 0)
			{
				StartFlameThrowerReloading();
				return;
			}

			m_isAiming = false;
			m_isFlameFiring = true;
			m_flameStartTime = m_subsystemTime.GameTime;
			m_nextFlameShotTime = m_subsystemTime.GameTime;
			m_nextFlameSoundTime = m_subsystemTime.GameTime;

			if (!m_flameSwitchState)
			{
				m_flameSwitchState = true;
				data = FlameThrowerBlock.SetSwitchState(data, true);
				int newValue = Terrain.ReplaceData(slotValue, data);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
			}

			PlayFlameThrowerSound();
		}

		private void StopFlameThrowerFiring()
		{
			m_isFlameFiring = false;

			if (m_flameSwitchState && m_currentWeaponSlot >= 0)
			{
				m_flameSwitchState = false;
				int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				int data = Terrain.ExtractData(slotValue);
				data = FlameThrowerBlock.SetSwitchState(data, false);
				int newValue = Terrain.ReplaceData(slotValue, data);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);

				m_subsystemAudio.PlaySound("Audio/Items/Hammer Uncock Remake", 1f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 2f, false);
			}
		}

		private void StartFlameThrowerReloading()
		{
			m_isFlameFiring = false;
			m_isReloading = true;
			m_animationStartTime = m_subsystemTime.GameTime;

			m_subsystemAudio.PlaySound("Audio/Reload", 1.5f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 5f, false);
		}

		private void ApplyFlameThrowerCockingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.07f, -0.06f, 0.06f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void ApplyFlameThrowerAimingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder + ((AimHandAngleOrder != 0f) ? 0.1f : 0f);
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.07f, -0.06f, 0.06f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void ApplyFlameThrowerFiringAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.07f, -0.06f, 0.06f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void ApplyFlameThrowerReloadingAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.07f, -0.06f, 0.06f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ProcessRepeatCrossbowBehavior(ComponentCreature target, float distance)
		{
			if (!m_isAiming && !m_isDrawing && !m_isFiring && !m_isReloading)
			{
				StartAiming();
			}

			if (m_isAiming)
			{
				ApplyRepeatCrossbowAimingAnimation(target);

				if (m_subsystemTime.GameTime - m_animationStartTime >= m_aimTime)
				{
					m_isAiming = false;

					m_currentRepeatArrowType = SelectRepeatArrowTypeBasedOnDistance(distance);
					StartRepeatCrossbowDrawing();
				}
			}
			else if (m_isDrawing)
			{
				ApplyRepeatCrossbowDrawingAnimation(target);

				m_currentDraw = MathUtils.Clamp((float)((m_subsystemTime.GameTime - m_drawStartTime) / m_repeatCrossbowDrawTime), 0f, 1f);

				UpdateRepeatCrossbowDraw((int)(m_currentDraw * 15f));

				if (m_subsystemTime.GameTime - m_drawStartTime >= m_repeatCrossbowDrawTime)
				{
					m_isDrawing = false;
					LoadRepeatCrossbowArrow();
				}
			}
			else if (m_isReloading)
			{
				ApplyRepeatCrossbowReloadingAnimation(target);

				if (m_subsystemTime.GameTime - m_animationStartTime >= 0.2f)
				{
					m_isReloading = false;
					FireRepeatCrossbow(target);
				}
			}
			else if (m_isFiring)
			{
				ApplyRepeatCrossbowFiringAnimation();

				if (m_subsystemTime.GameTime - m_fireTime >= 0.2f)
				{
					m_isFiring = false;

					ClearArrowFromRepeatCrossbow();

					m_currentRepeatArrowType = SelectRepeatArrowTypeBasedOnDistance(distance);
					m_currentRepeatArrowTypeIndex = Array.IndexOf(m_repeatCrossbowArrowTypes, m_currentRepeatArrowType);

					if (m_subsystemTime.GameTime - m_fireTime >= m_repeatCrossbowTimeBetweenShots)
					{
						StartAiming();
					}
				}
			}
		}

		private void UpdateRepeatCrossbowDraw(int draw)
		{
			if (m_currentWeaponSlot < 0) return;

			try
			{
				int currentValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				if (currentValue == 0) return;

				int currentData = Terrain.ExtractData(currentValue);
				int newData = RepeatCrossbowBlock.SetDraw(currentData, MathUtils.Clamp(draw, 0, 15));

				int newValue = Terrain.ReplaceData(currentValue, newData);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
			}
			catch { }
		}

		private void UpdateRepeatCrossbowArrowType(RepeatArrowBlock.ArrowType? arrowType)
		{
			if (m_currentWeaponSlot < 0) return;

			try
			{
				int currentValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				if (currentValue == 0) return;

				int currentData = Terrain.ExtractData(currentValue);

				var method = typeof(RepeatCrossbowBlock).GetMethod("SetArrowType");
				int newData = (int)method.Invoke(null, new object[] { currentData, arrowType });

				int newValue = Terrain.ReplaceData(currentValue, newData);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
			}
			catch { }
		}

		private void ClearArrowFromRepeatCrossbow()
		{
			UpdateRepeatCrossbowDraw(0);
			UpdateRepeatCrossbowArrowType(null);
		}

		private void StartRepeatCrossbowDrawing()
		{
			m_isDrawing = true;
			m_drawStartTime = m_subsystemTime.GameTime;

			m_subsystemAudio.PlaySound("Audio/Crossbow Remake/Crossbow Loading Remake", 0.5f,
				m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 3f, false);
		}

		private void LoadRepeatCrossbowArrow()
		{
			m_isDrawing = false;
			m_isReloading = true;
			m_animationStartTime = m_subsystemTime.GameTime;

			UpdateRepeatCrossbowDraw(15);
			UpdateRepeatCrossbowArrowType(m_currentRepeatArrowType);
		}

		private void ApplyRepeatCrossbowAimingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder + ((AimHandAngleOrder != 0f) ? 0.1f : 0f);
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void ApplyRepeatCrossbowDrawingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void ApplyRepeatCrossbowReloadingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.03f, -0.1f, 0.04f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void FireRepeatCrossbow(ComponentCreature target)
		{
			m_isReloading = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			ShootRepeatCrossbowArrow(target);

			m_subsystemAudio.PlaySound("Audio/Crossbow Remake/Crossbow Shoot", 1f,
				m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 15f, false);

			if (target != null)
			{
				Vector3 direction = Vector3.Normalize(
					target.ComponentBody.Position - m_componentCreature.ComponentBody.Position
				);
				m_componentCreature.ComponentBody.ApplyImpulse(-direction * 1.0f);
			}
		}

		private void ApplyRepeatCrossbowFiringAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.03f, -0.1f, 0.04f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);
			}
		}

		private void StartAiming()
		{
			m_isAiming = true;
			m_isDrawing = false;
			m_isFiring = false;
			m_isReloading = false;
			m_isCocking = false;
			m_isFlameFiring = false;
			m_isFlameCocking = false;
			m_isFirearmAiming = false;
			m_isThrowableAiming = false;
			m_isThrowableThrowing = false;
			m_isFirearmFiring = false;
			m_isFirearmReloading = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;
			m_arrowVisible = false;
			m_hasArrowInBow = false;
			m_hasFirearmAimed = false;
			m_lastFirearmShotTime = 0;

			if (m_weaponType == 0)
			{
				SetBowWithArrow(0);
				m_hasArrowInBow = true;
			}
			else if (m_weaponType == 1)
			{
				SetCrossbowWithBolt(0, false, m_currentTargetDistance);
			}
			else if (m_weaponType == 2 && m_currentWeaponSlot >= 0)
			{
				int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				int data = Terrain.ExtractData(slotValue);
				MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
				bool hammerState = MusketBlock.GetHammerState(data);

				if (loadState != MusketBlock.LoadState.Loaded)
				{
					UpdateMusketLoadState(MusketBlock.LoadState.Loaded);
					UpdateMusketBulletType(BulletBlock.BulletType.MusketBall);
				}

				if (loadState == MusketBlock.LoadState.Loaded && !hammerState)
				{
					StartMusketCocking();
				}
				else if (loadState == MusketBlock.LoadState.Loaded && hammerState)
				{
					m_isAiming = true;
					m_animationStartTime = m_subsystemTime.GameTime;
				}
			}
			else if (m_weaponType == 3 && m_currentWeaponSlot >= 0)
			{
				int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				int data = Terrain.ExtractData(slotValue);
				FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
				int loadCount = FlameThrowerBlock.GetLoadCount(slotValue);
				m_flameSwitchState = FlameThrowerBlock.GetSwitchState(data);

				var bulletType = FlameThrowerBlock.GetBulletType(data);
				if (bulletType.HasValue)
				{
					m_currentFlameBulletType = bulletType.Value;
				}
				else
				{
					m_currentFlameBulletType = (m_random.Int(0, 100) < 70) ?
						FlameBulletBlock.FlameBulletType.Flame :
						FlameBulletBlock.FlameBulletType.Poison;
				}

				if (loadState != FlameThrowerBlock.LoadState.Loaded || loadCount <= 0)
				{
					m_isAiming = false;
					StartFlameThrowerReloading();
					return;
				}

				if (m_flameSwitchState)
				{
					m_animationStartTime = m_subsystemTime.GameTime - 0.2f;
				}
			}
			else if (m_weaponType == 4 && m_currentWeaponSlot >= 0)
			{
				UpdateRepeatCrossbowDraw(0);
				UpdateRepeatCrossbowArrowType(null);
			}
			else if (m_weaponType == 5 && m_currentWeaponSlot >= 0)
			{
				StartFirearmAiming();
			}
			else if (m_weaponType == 6 && m_currentWeaponSlot >= 0)
			{
				StartThrowableAiming();
			}
		}

		private void ResetWeaponState()
		{
			m_isAiming = false;
			m_isDrawing = false;
			m_isFiring = false;
			m_isReloading = false;
			m_isCocking = false;
			m_isFlameFiring = false;
			m_isFlameCocking = false;
			m_isFirearmAiming = false;
			m_isFirearmFiring = false;
			m_isFirearmReloading = false;
			m_isThrowableAiming = false;
			m_isThrowableThrowing = false;

			m_currentDraw = 0f;
			m_currentWeaponSlot = -1;
			m_weaponType = -1;
			m_currentTargetDistance = 0f;
			m_arrowVisible = false;
			m_hasArrowInBow = false;
			m_flameSwitchState = false;
			m_shotsSinceLastReload = 0;
			m_hasFirearmAimed = false;
			m_lastFirearmShotTime = 0;

			// Resetear el estado de todas las armas en la mano
			if (m_componentInventory != null && m_componentInventory.ActiveSlotIndex >= 0)
			{
				int slotValue = m_componentInventory.GetSlotValue(m_componentInventory.ActiveSlotIndex);
				if (slotValue != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];

					// Resetear estado del arco
					if (block is BowBlock)
					{
						try
						{
							int currentData = Terrain.ExtractData(slotValue);
							int newData = BowBlock.SetArrowType(currentData, null);
							newData = BowBlock.SetDraw(newData, 0);
							int newBowValue = Terrain.ReplaceData(slotValue, newData);
							m_componentInventory.RemoveSlotItems(m_componentInventory.ActiveSlotIndex, 1);
							m_componentInventory.AddSlotItems(m_componentInventory.ActiveSlotIndex, newBowValue, 1);
						}
						catch { }
					}
					// Resetear estado de la ballesta
					else if (block is CrossbowBlock)
					{
						try
						{
							int currentData = Terrain.ExtractData(slotValue);
							int newData = CrossbowBlock.SetDraw(currentData, 0);
							newData = CrossbowBlock.SetArrowType(newData, null);
							int newCrossbowValue = Terrain.ReplaceData(slotValue, newData);
							m_componentInventory.RemoveSlotItems(m_componentInventory.ActiveSlotIndex, 1);
							m_componentInventory.AddSlotItems(m_componentInventory.ActiveSlotIndex, newCrossbowValue, 1);
						}
						catch { }
					}
					// Resetear estado del mosquete
					else if (block is MusketBlock)
					{
						try
						{
							int currentData = Terrain.ExtractData(slotValue);
							currentData = MusketBlock.SetHammerState(currentData, false);
							currentData = MusketBlock.SetLoadState(currentData, MusketBlock.LoadState.Empty);
							int newValue = Terrain.ReplaceData(slotValue, currentData);
							m_componentInventory.RemoveSlotItems(m_componentInventory.ActiveSlotIndex, 1);
							m_componentInventory.AddSlotItems(m_componentInventory.ActiveSlotIndex, newValue, 1);
						}
						catch { }
					}
					// Resetear estado del lanzallamas
					else if (block is FlameThrowerBlock)
					{
						try
						{
							int currentData = Terrain.ExtractData(slotValue);
							currentData = FlameThrowerBlock.SetSwitchState(currentData, false);
							int newValue = Terrain.ReplaceData(slotValue, currentData);
							m_componentInventory.RemoveSlotItems(m_componentInventory.ActiveSlotIndex, 1);
							m_componentInventory.AddSlotItems(m_componentInventory.ActiveSlotIndex, newValue, 1);
						}
						catch { }
					}
					// Resetear estado de la ballesta repetitiva
					else if (block is RepeatCrossbowBlock)
					{
						try
						{
							int currentData = Terrain.ExtractData(slotValue);
							var method = typeof(RepeatCrossbowBlock).GetMethod("SetArrowType");
							int newData = (int)method.Invoke(null, new object[] { currentData, null });
							newData = RepeatCrossbowBlock.SetDraw(newData, 0);
							int newValue = Terrain.ReplaceData(slotValue, newData);
							m_componentInventory.RemoveSlotItems(m_componentInventory.ActiveSlotIndex, 1);
							m_componentInventory.AddSlotItems(m_componentInventory.ActiveSlotIndex, newValue, 1);
						}
						catch { }
					}
				}
			}

			// Resetear animaciones del modelo
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = AimHandAngleOrder;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ShootBowArrow(ComponentCreature target)
		{
			if (target == null) return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				firePosition.Y -= 0.1f;

				Vector3 targetPosition = target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				float currentAccuracy = 0.03f * (1.5f - m_currentDraw);
				direction += new Vector3(
					m_random.Float(-currentAccuracy, currentAccuracy),
					m_random.Float(-currentAccuracy * 0.5f, currentAccuracy * 0.5f),
					m_random.Float(-currentAccuracy, currentAccuracy)
				);
				direction = Vector3.Normalize(direction);

				float speedMultiplier = 0.5f + (m_currentDraw * 1.5f);
				float currentSpeed = 35f * speedMultiplier;

				int arrowData = ArrowBlock.SetArrowType(0, m_currentArrowType);
				int arrowValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(), 0, arrowData);

				var projectile = m_subsystemProjectiles.FireProjectile(
					arrowValue,
					firePosition,
					direction * currentSpeed,
					Vector3.Zero,
					m_componentCreature
				);

				// Configurar para que desaparezca al detenerse
				if (projectile != null)
				{
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				}
			}
			catch { }
		}

		private void ShootCrossbowBolt(ComponentCreature target)
		{
			if (target == null) return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				firePosition.Y -= 0.1f;

				Vector3 targetPosition = target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				direction += new Vector3(
					m_random.Float(-0.02f, 0.02f),
					m_random.Float(-0.01f, 0.01f),
					m_random.Float(-0.02f, 0.02f)
				);
				direction = Vector3.Normalize(direction);

				float speed = 45f * (0.8f + (m_currentDraw * 0.4f));

				int boltData = ArrowBlock.SetArrowType(0, m_currentBoltType);
				int boltValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(), 0, boltData);

				var projectile = m_subsystemProjectiles.FireProjectile(
					boltValue,
					firePosition,
					direction * speed,
					Vector3.Zero,
					m_componentCreature
				);

				// Configurar para que desaparezca al detenerse
				if (projectile != null)
				{
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				}

				if (m_subsystemNoise != null)
				{
					m_subsystemNoise.MakeNoise(firePosition, 0.5f, 20f);
				}
			}
			catch { }
		}

		private void ShootMusketBullet(ComponentCreature target)
		{
			if (target == null) return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPosition = target.ComponentCreatureModel.EyePosition;

				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);
				direction += new Vector3(
					m_random.Float(-0.02f, 0.02f),
					m_random.Float(-0.01f, 0.01f),
					m_random.Float(-0.02f, 0.02f)
				);
				direction = Vector3.Normalize(direction);

				int bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>();
				if (bulletBlockIndex > 0)
				{
					int bulletData = BulletBlock.SetBulletType(0, BulletBlock.BulletType.MusketBall);
					int bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, bulletData);

					m_subsystemProjectiles.FireProjectile(
						bulletValue,
						firePosition,
						direction * 120f,
						Vector3.Zero,
						m_componentCreature
					);

					Vector3 smokePosition = firePosition + direction * 0.3f;
					if (m_subsystemParticles != null && m_subsystemTerrain != null)
					{
						m_subsystemParticles.AddParticleSystem(
							new GunSmokeParticleSystem(m_subsystemTerrain, smokePosition, direction),
							false
						);
					}

					if (m_subsystemNoise != null)
					{
						m_subsystemNoise.MakeNoise(firePosition, 1f, 40f);
					}
				}
			}
			catch { }
		}

		private void FireFlameThrowerShot(ComponentCreature target)
		{
			if (target == null) return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				firePosition.Y -= 0.1f;

				Vector3 targetPosition = target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				direction += new Vector3(
					m_random.Float(-0.05f, 0.05f),
					m_random.Float(-0.03f, 0.03f),
					m_random.Float(-0.05f, 0.05f)
				);
				direction = Vector3.Normalize(direction);

				int bulletData = FlameBulletBlock.SetBulletType(0, m_currentFlameBulletType);
				int bulletValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<FlameBulletBlock>(), 0, bulletData);

				float speed = 35f;

				m_subsystemProjectiles.FireProjectile(
					bulletValue,
					firePosition,
					direction * speed,
					Vector3.Zero,
					m_componentCreature
				);

				if (m_currentFlameBulletType == FlameBulletBlock.FlameBulletType.Flame)
				{
					m_subsystemParticles.AddParticleSystem(new FlameSmokeParticleSystem(m_subsystemTerrain, firePosition + direction * 0.3f, direction), false);
				}
				else
				{
					m_subsystemParticles.AddParticleSystem(new PoisonSmokeParticleSystem(m_subsystemTerrain, firePosition + direction * 0.3f, direction), false);
				}

				if (m_subsystemNoise != null)
				{
					m_subsystemNoise.MakeNoise(firePosition, 0.4f, 15f);
				}
			}
			catch { }
		}

		private void ShootRepeatCrossbowArrow(ComponentCreature target)
		{
			if (target == null) return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;

				Vector3 targetPosition = target.ComponentBody.Position;
				targetPosition.Y += target.ComponentBody.BoxSize.Y * 0.5f;

				Vector3 direction = targetPosition - firePosition;
				float distance = direction.Length();

				if (distance > 0.001f)
				{
					direction /= distance;
				}
				else
				{
					direction = Vector3.UnitX;
				}

				float baseInaccuracy = m_repeatCrossbowMaxInaccuracy * 0.2f;

				float distanceFactor = MathUtils.Clamp(distance / m_maxDistance, 0.1f, 1.0f);
				float inaccuracy = baseInaccuracy * distanceFactor;

				direction += new Vector3(
					m_random.Float(-inaccuracy, inaccuracy),
					m_random.Float(-inaccuracy * 0.2f, inaccuracy * 0.2f),
					m_random.Float(-inaccuracy, inaccuracy)
				);

				direction = Vector3.Normalize(direction);

				float speed = m_repeatCrossbowBoltSpeed * 1.2f;

				int arrowData = RepeatArrowBlock.SetArrowType(0, m_currentRepeatArrowType);
				int arrowValue = Terrain.MakeBlockValue(RepeatArrowBlock.Index, 0, arrowData);

				Vector3 adjustedFirePosition = firePosition + direction * 0.2f;

				var projectile = m_subsystemProjectiles.FireProjectile(
					arrowValue,
					adjustedFirePosition,
					direction * speed,
					Vector3.Zero,
					m_componentCreature
				);

				if (projectile != null)
				{
					// Configurar para que desaparezca al detenerse (para todos los tipos de flechas)
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;

					// Mantener la configuración específica para flechas explosivas
					if (m_currentRepeatArrowType == RepeatArrowBlock.ArrowType.ExplosiveArrow)
					{
						projectile.IsIncendiary = false;
					}
				}

				if (m_subsystemNoise != null)
				{
					m_subsystemNoise.MakeNoise(firePosition, 0.5f, 20f);
				}
			}
			catch (Exception)
			{
				m_currentRepeatArrowTypeIndex = 0;
				m_currentRepeatArrowType = m_repeatCrossbowArrowTypes[0];
			}
		}
	}
}
