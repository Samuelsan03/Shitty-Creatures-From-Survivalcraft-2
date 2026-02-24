using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureAI : ComponentBehavior, IUpdateable
	{
		private ComponentPathfinding m_componentPathfinding;
		private SubsystemBodies m_subsystemBodies; // AÑADIR junto a los otros subsystems
		private ComponentCreature m_componentCreature;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentInventory m_componentInventory;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private ComponentCreatureModel m_componentModel;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemTerrain m_subsystemTerrain;
		private ComponentMiner m_componentMiner;
		private Random m_random = new Random();

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
		private bool m_isThrowableAiming = false;
		private bool m_isThrowableThrowing = false;
		private double m_animationStartTime;
		private double m_drawStartTime;
		private double m_fireTime;
		private double m_flameStartTime;
		private double m_nextFlameShotTime;
		private double m_cockStartTime;
		private double m_nextFlameSoundTime;
		private double m_lastFirearmShotTime;
		private double m_firearmReloadStartTime;
		private double m_throwableThrowTime;

		// DESPUÉS (añadir junto a las otras variables bool/double)
		private bool m_isItemsLauncherAiming = false;
		private bool m_isItemsLauncherFiring = false;
		private double m_itemsLauncherLastFireTime;
		private float m_itemsLauncherFireInterval = 0.5f;     // Se calculará según RateLevel
		private float m_itemsLauncherSpeed = 35f;            // Se calculará según SpeedLevel
		private float m_itemsLauncherSpread = 0.1f;          // Se calculará según SpreadLevel
		private float m_itemsLauncherReloadTime = 1.2f;     // ← NUEVO (valor del XML)
		private float m_itemsLauncherMaxShots = 0f;         // ← NUEVO (se calculará según algo, o fijo)
		private int m_itemsLauncherShotsFired = 0;          // ← NUEVO
		private bool m_isItemsLauncherReloading = false;    // ← NUEVO
		private double m_itemsLauncherReloadStartTime;      // ← NUEVO

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

		private bool m_isMelee = false;
		private double m_lastMeleeAttackTime = 0.0;

		private float m_maxDistance = 100f;
		private float m_drawTime = 0.5f;
		private float m_aimTime = 0.55f;
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
		private float m_throwableMinRange = 5f;      // AÑADIR
		private float m_throwableMaxRange = 15.5f;     // AÑADIR

		private float m_explosiveBoltMinDistance = 15f;
		private float m_explosiveRepeatArrowMinDistance = 15f;

		private float m_repeatCrossbowDrawTime = 0.3f;
		private float m_repeatCrossbowTimeBetweenShots = 0.5f;
		private float m_repeatCrossbowMaxInaccuracy = 0.04f;
		private float m_repeatCrossbowBoltSpeed = 35f;

		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
			m_componentInventory = Entity.FindComponent<ComponentInventory>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentModel = Entity.FindComponent<ComponentCreatureModel>();
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true); // AÑADIR
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
					BulletBlockType = typeof(NuevaBala3),  // Mismo tipo de bala que usa el BK43
					ShootSound = "Audio/Armas/bk 43",
					FireRate = 1.5,  // Cooldown de 1.5 segundos como en SubsystemBK43Behavior
					BulletSpeed = 280f,  // Velocidad ajustada para escopeta
					MaxShotsBeforeReload = 2,  // Capacidad de 2 cartuchos
					ProjectilesPerShot = 8,  // 8 perdigones como patrón de escopeta
					SpreadVector = new Vector3(0.1f, 0.1f, 0.03f),  // Dispersión de escopeta
					NoiseRadius = 50f,  // Ruido mayor por ser escopeta
					IsAutomatic = false  // No automático, es de bombeo/acción simple
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

		public void Update(float dt)
		{
			if (!CanUseInventory || m_componentCreature == null || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = m_componentChaseBehavior?.Target;

			if (target != null && target.ComponentHealth.Health > 0f)
			{
				float distance = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					target.ComponentBody.Position
				);

				m_currentTargetDistance = distance;

				const float MeleeRange = 5f;

				if (distance <= MeleeRange)
				{
					if (!m_isMelee)
					{
						if (!EnterMeleeMode())
						{
							m_isMelee = false;
						}
					}

					if (m_isMelee)
					{
						UpdateMeleeMode(target);
					}
					else
					{
						float maxDistance = GetWeaponMaxDistance();
						if (distance <= maxDistance)
						{
							if (m_currentWeaponSlot == -1)
							{
								FindRangedWeapon();
							}

							if (m_currentWeaponSlot != -1)
							{
								ProcessWeaponBehavior(target, distance);

								// Golpear con el arma a distancia
								const double MeleeAttackInterval = 0.8;
								double currentTime = m_subsystemTime.GameTime;
								if (currentTime - m_lastMeleeAttackTime >= MeleeAttackInterval)
								{
									AttackMelee(target);
									m_lastMeleeAttackTime = currentTime;
								}
							}
						}
					}
				}
				else
				{
					if (m_isMelee)
					{
						ExitMeleeMode();
					}

					// Siempre intentar usar el arma a distancia, sin límite de distancia
					if (m_currentWeaponSlot == -1)
					{
						FindRangedWeapon();
					}

					if (m_currentWeaponSlot != -1)
					{
						ProcessWeaponBehavior(target, distance);
					}
					else
					{
						// No hay arma a distancia, resetear
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
		}

		private bool EnterMeleeMode()
		{
			int bestSlot = FindBestMeleeWeaponSlot();
			if (bestSlot >= 0)
			{
				m_componentInventory.ActiveSlotIndex = bestSlot;

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

				if (m_componentModel != null)
				{
					m_componentModel.AimHandAngleOrder = 0f;
					m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
					m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				}

				m_isMelee = true;
				return true;
			}

			m_isMelee = false;
			return false;
		}

		private void ExitMeleeMode()
		{
			m_isMelee = false;
			// Fuerza a re‑buscar un arma a distancia en el próximo ciclo
			m_currentWeaponSlot = -1;
			m_weaponType = -1;
			m_lastMeleeAttackTime = 0.0;
		}

		private void UpdateMeleeMode(ComponentCreature target)
		{
			if (target == null || m_componentModel == null || m_componentMiner == null)
				return;

			// Mirar al objetivo
			m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);

			const double MeleeAttackInterval = 0.8;
			double currentTime = m_subsystemTime.GameTime;
			if (currentTime - m_lastMeleeAttackTime >= MeleeAttackInterval)
			{
				AttackMelee(target);
				m_lastMeleeAttackTime = currentTime;
			}
		}

		private void AttackMelee(ComponentCreature target)
		{
			if (m_componentMiner == null || target == null || m_componentModel == null)
				return;

			m_componentModel.AttackOrder = true;

			if (m_componentModel.IsAttackHitMoment)
			{
				Vector3 hitPoint;
				ComponentBody hitBody = GetHitBody(target.ComponentBody, out hitPoint);
				if (hitBody != null)
				{
					m_componentMiner.Hit(hitBody, hitPoint, m_componentCreature.ComponentBody.Matrix.Forward);
					m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
				}
			}
		}

		private ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 start = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 end = target.BoundingBox.Center();
			Ray3 ray = new Ray3(start, Vector3.Normalize(end - start));
			BodyRaycastResult? result = m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			if (result != null && result.Value.Distance < 1.75f &&
				(result.Value.ComponentBody == target ||
				 result.Value.ComponentBody.IsChildOfBody(target) ||
				 target.IsChildOfBody(result.Value.ComponentBody) ||
				 target.StandingOnBody == result.Value.ComponentBody))
			{
				hitPoint = result.Value.HitPoint();
				return result.Value.ComponentBody;
			}
			hitPoint = default(Vector3);
			return null;
		}

		private void FindMeleeWeapon()
		{
			if (m_componentInventory == null)
				return;

			float bestPower = 1f;
			int bestSlot = -1;
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					float power = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)].GetMeleePower(slotValue);
					if (power > bestPower)
					{
						bestPower = power;
						bestSlot = i;
					}
				}
			}
			if (bestSlot >= 0)
			{
				m_componentInventory.ActiveSlotIndex = bestSlot;
			}
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
			if (m_weaponType == 6) return m_throwableMaxRange; // AÑADIR
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
					// DESPUÉS (añadir un nuevo else if antes del cierre del bucle)
					else if (block is ItemsLauncherBlock)
					{
						m_currentWeaponSlot = i;
						m_weaponType = 7;                          // Nuevo tipo para ItemsLauncher
						m_componentInventory.ActiveSlotIndex = i;
						// Leer niveles del bloque y calcular parámetros
						int data = Terrain.ExtractData(slotValue);
						int speedLevel = ItemsLauncherBlock.GetSpeedLevel(data);
						int rateLevel = ItemsLauncherBlock.GetRateLevel(data);
						int spreadLevel = ItemsLauncherBlock.GetSpreadLevel(data);
						// Valores por defecto si el nivel es 0
						if (speedLevel == 0) speedLevel = 2;
						if (rateLevel == 0) rateLevel = 2;
						if (spreadLevel == 0) spreadLevel = 2;
						// Asignar según tablas (igual que en SubsystemItemsLauncherBlockBehavior)
						float[] speedValues = { 10f, 35f, 60f };
						float[] rateValues = { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f, 14f, 15f };
						float[] spreadValues = { 0.01f, 0.1f, 0.5f };
						m_itemsLauncherSpeed = speedValues[speedLevel - 1];
						m_itemsLauncherFireInterval = 1f / rateValues[rateLevel - 1];  // convertir disparos/segundo a intervalo
						m_itemsLauncherSpread = spreadValues[spreadLevel - 1];
						StartItemsLauncherAiming();
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

		private void StartItemsLauncherReloading()
		{
			m_isItemsLauncherAiming = false;
			m_isItemsLauncherFiring = false;
			m_isItemsLauncherReloading = true;
			m_itemsLauncherReloadStartTime = m_subsystemTime.GameTime;

			// Animación de recarga
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ApplyItemsLauncherReloadingAnimation()
		{
			if (m_componentModel != null)
			{
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_itemsLauncherReloadStartTime) / m_itemsLauncherReloadTime);

				// Animación simple de recarga (bajar el arma)
				m_componentModel.AimHandAngleOrder = MathUtils.Lerp(0f, 1.4f, reloadProgress);
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
			}
		}

		// NUEVO MÉTODO (insertar junto a los otros Start...)
		private void StartItemsLauncherAiming()
		{
			m_isItemsLauncherAiming = true;
			m_isItemsLauncherFiring = false;
			m_isItemsLauncherReloading = false;  // ← NUEVO
			m_animationStartTime = m_subsystemTime.GameTime;
			m_itemsLauncherLastFireTime = 0;
			m_itemsLauncherShotsFired = 0;       // ← NUEVO
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
			if (blockType == typeof(EggBlock)) return true;
			if (blockType == typeof(CopperSpearBlock)) return true;
			if (blockType == typeof(DiamondSpearBlock)) return true;
			if (blockType == typeof(IronSpearBlock)) return true;
			if (blockType == typeof(WoodenSpearBlock)) return true;
			if (blockType == typeof(WoodenLongspearBlock)) return true;
			if (blockType == typeof(StoneSpearBlock)) return true;
			if (blockType == typeof(StoneLongspearBlock)) return true;
			if (blockType == typeof(IronLongspearBlock)) return true;
			if (blockType == typeof(LavaLongspearBlock)) return true;
			if (blockType == typeof(LavaSpearBlock)) return true;
			if (blockType == typeof(DiamondLongspearBlock)) return true;

			return false;
		}

		private void ProcessWeaponBehavior(ComponentCreature target, float distance)
		{
			switch (m_weaponType)
			{
				case 0: ProcessBowBehavior(target); break;
				case 1: ProcessCrossbowBehavior(target, distance); break;
				case 2: ProcessMusketBehavior(target); break;
				case 3: ProcessFlameThrowerBehavior(target, distance); break;
				case 4: ProcessRepeatCrossbowBehavior(target, distance); break;
				case 5: ProcessFirearmBehavior(target, distance); break;
				case 6: ProcessThrowableBehavior(target, distance); break;
				case 7: ProcessItemsLauncherBehavior(target, distance); break;
			}
		}

		private void ProcessItemsLauncherBehavior(ComponentCreature target, float distance)
		{
			if (target == null) return;

			if (!HasClearLineOfSight(target))
			{
				m_isItemsLauncherAiming = false;
				m_isItemsLauncherFiring = false;
				if (m_componentModel != null)
				{
					m_componentModel.AimHandAngleOrder = 0f;
					m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
					m_componentModel.InHandItemRotationOrder = Vector3.Zero;
					m_componentModel.LookAtOrder = null;
				}
				return;
			}

			if (distance > m_maxDistance)
			{
				m_isItemsLauncherAiming = false;
				m_isItemsLauncherFiring = false;
				return;
			}

			if (!m_isItemsLauncherAiming && !m_isItemsLauncherFiring)
			{
				StartItemsLauncherAiming();
			}

			if (m_isItemsLauncherAiming)
			{
				if (m_componentModel != null)
				{
					m_componentModel.AimHandAngleOrder = 1.4f;
					m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}

				if (m_subsystemTime.GameTime - m_animationStartTime >= m_aimTime)
				{
					m_isItemsLauncherAiming = false;
					m_isItemsLauncherFiring = true;
					m_itemsLauncherLastFireTime = m_subsystemTime.GameTime;
				}
			}
			else if (m_isItemsLauncherFiring)
			{
				if (m_componentModel != null)
				{
					m_componentModel.AimHandAngleOrder = 1.4f;
					m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}

				else if (m_isItemsLauncherReloading)
				{
					ApplyItemsLauncherReloadingAnimation();

					if (m_subsystemTime.GameTime - m_itemsLauncherReloadStartTime >= m_itemsLauncherReloadTime)
					{
						m_isItemsLauncherReloading = false;
						m_itemsLauncherShotsFired = 0;
						m_isItemsLauncherAiming = true;
						m_animationStartTime = m_subsystemTime.GameTime;
					}
				}

				if (m_subsystemTime.GameTime - m_itemsLauncherLastFireTime >= m_itemsLauncherFireInterval)
				{
					FireItemsLauncher(target);
					m_itemsLauncherLastFireTime = m_subsystemTime.GameTime;
				}

				if (m_itemsLauncherShotsFired >= 10)
				{
					m_isItemsLauncherFiring = false;
					StartItemsLauncherReloading();
				}
			}
		}

		// NUEVO MÉTODO (insertar después del anterior)
		private void FireItemsLauncher(ComponentCreature target)
		{
			if (target == null) return;

			try
			{
				// Si está recargando, no disparar
				if (m_isItemsLauncherReloading) return;

				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPosition = target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				direction += new Vector3(
					m_random.Float(-m_itemsLauncherSpread, m_itemsLauncherSpread),
					m_random.Float(-m_itemsLauncherSpread * 0.5f, m_itemsLauncherSpread * 0.5f),
					m_random.Float(-m_itemsLauncherSpread, m_itemsLauncherSpread)
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
						direction * m_itemsLauncherSpeed,
						Vector3.Zero,
						m_componentCreature
					);

					// Contar disparo
					m_itemsLauncherShotsFired++;

					// Partículas y sonidos...
					if (m_subsystemParticles != null && m_subsystemTerrain != null)
					{
						Vector3 smokePos = firePosition + direction * 0.3f;
						m_subsystemParticles.AddParticleSystem(
							new GunSmokeParticleSystem(m_subsystemTerrain, smokePos, direction),
							false
						);
					}

					m_subsystemAudio.PlaySound("Audio/Items/ItemLauncher/Item Cannon Fire", 0.7f,
						m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 15f, false);

					if (m_subsystemNoise != null)
						m_subsystemNoise.MakeNoise(firePosition, 0.8f, 30f);
				}
			}
			catch { }
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
					m_componentModel.AimHandAngleOrder = 0f;
					m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
					m_componentModel.InHandItemRotationOrder = Vector3.Zero;
					m_componentModel.LookAtOrder = null;
				}
				// IMPORTANTE: NO detener el movimiento (NO tocar m_componentPathfinding.Destination)
				// La IA de persecución (ComponentChaseBehavior) se encargará de mover al zombie
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
					m_componentModel.AimHandAngleOrder = 0f;
					m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
					m_componentModel.InHandItemRotationOrder = Vector3.Zero;
					m_componentModel.LookAtOrder = null;
				}
				// IMPORTANTE: NO detener el movimiento - la persecución lo acercará/alejará
				return;
			}

			// SOLO cuando hay línea de visión Y estamos en rango: detener el movimiento para lanzar
			if (m_componentPathfinding != null)
			{
				m_componentPathfinding.Destination = null;
			}

			// Lógica de lanzamiento existente...
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

					// Después de lanzar, comprobar si aún quedan objetos en el slot
					int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
					if (slotValue != 0)
					{
						// Aún hay objetos, reiniciar puntería
						StartThrowableAiming();
					}
					else
					{
						// Se acabaron los objetos, buscar otra arma
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
				// Brazo elevado casi por completo (toque el cielo)
				m_componentModel.AimHandAngleOrder = 3f;

				if (m_currentWeaponSlot >= 0)
				{
					int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];

					// Ajustes específicos por tipo de objeto lanzable
					if (block is SpearBlock || block is LongspearBlock)
					{
						// Lanza: rotada para que apunte hacia atrás, ligeramente elevada
						m_componentModel.InHandItemOffsetOrder = new Vector3(0.05f, -0.2f, 0.1f);
						m_componentModel.InHandItemRotationOrder = new Vector3(3.14159f, 0.3f, 0f);
					}
					else if (block is BombBlock || block is IncendiaryBombBlock || block is PoisonBombBlock)
					{
						// Bombas: sostenidas sobre la cabeza, ligeramente inclinadas
						m_componentModel.InHandItemOffsetOrder = new Vector3(0.1f, -0.1f, 0.2f);
						m_componentModel.InHandItemRotationOrder = new Vector3(0.2f, 0.5f, 0.1f);
					}
					else if (block is SnowballBlock)
					{
						// Bola de nieve: cerca de la mano, rotación natural
						m_componentModel.InHandItemOffsetOrder = new Vector3(0.1f, -0.15f, 0.15f);
						m_componentModel.InHandItemRotationOrder = new Vector3(0.1f, 0.2f, 0f);
					}
					else
					{
						// Otros objetos (fragmentos, etc.): posición genérica elevada
						m_componentModel.InHandItemOffsetOrder = new Vector3(0.08f, -0.12f, 0.12f);
						m_componentModel.InHandItemRotationOrder = new Vector3(0.15f, 0.3f, 0f);
					}
				}

				// La cabeza sigue al objetivo (la otra mano queda como apuntador de forma natural en el modelo)
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
							// Usar posición del cuerpo para mayor seguridad
							Vector3 basePosition = m_componentCreature.ComponentBody.Position + new Vector3(0f, 1f, 0f);
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
						catch (Exception ex)
						{
							Log.Error($"Error al crear partículas de fin de recarga: {ex.Message}");
						}
					}

					m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, 0f, m_componentCreature.ComponentCreatureModel.EyePosition, 10f, true);
					// CORREGIDO: eliminar doble asignación, solo establecer a true
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
					m_isFirearmAiming = true;
					m_animationStartTime = m_subsystemTime.GameTime;
				}
				return;
			}

			if (m_shotsSinceLastReload >= config.MaxShotsBeforeReload)
			{
				StartFirearmReloading();
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
				if (config.IsSniper)
				{
					m_componentModel.AimHandAngleOrder = 1.2f;
					m_componentModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.06f, 0.08f);
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
				}
				else
				{
					m_componentModel.AimHandAngleOrder = 1.4f;
					m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
				}

				if (target != null)
				{
					Vector3 lookAtPosition = target.ComponentCreatureModel.EyePosition;
					if (config.IsSniper)
					{
						lookAtPosition = target.ComponentBody.Position;
						lookAtPosition.Y += 0.5f;
					}
					m_componentModel.LookAtOrder = new Vector3?(lookAtPosition);
				}
			}
		}

		private void ApplyFirearmFiringAnimation()
		{
			if (m_componentModel != null)
			{
				float timeSinceFire = (float)(m_subsystemTime.GameTime - m_fireTime);
				float recoilFactor = MathUtils.Max(1.5f - timeSinceFire * 8f, 1.0f);

				m_componentModel.AimHandAngleOrder = 1.4f * recoilFactor;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f - (0.05f * (1.5f - recoilFactor)));
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f + (0.3f * (1.5f - recoilFactor)), 0f, 0f);
			}
		}

		private void ApplyFirearmReloadingAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0.0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void StartFirearmReloading()
		{
			m_isFirearmAiming = false;
			m_isFirearmFiring = false;
			m_isFirearmReloading = true;
			m_firearmReloadStartTime = m_subsystemTime.GameTime;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}

			if (m_subsystemParticles != null && m_subsystemTerrain != null)
			{
				try
				{
					// Usar posición del cuerpo para mayor seguridad
					Vector3 basePosition = m_componentCreature.ComponentBody.Position + new Vector3(0f, 1f, 0f); // altura media
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
				catch (Exception ex)
				{
					Log.Error($"Error al crear partículas de inicio de recarga: {ex.Message}");
				}
			}

			m_subsystemAudio.PlaySound("Audio/Armas/reload", 0.8f, 0f, m_componentCreature.ComponentCreatureModel.EyePosition, 10f, true);
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
				m_componentModel.AimHandAngleOrder = 0.2f;
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
				float horizontalOffset = 0.05f - (0.03f * drawFactor);
				float verticalOffset = 0.05f + (0.02f * drawFactor);
				float depthOffset = 0.05f - (0.02f * drawFactor);
				float pitchRotation = -0.05f - (0.1f * drawFactor);
				float yawRotation = 0.3f - (0.05f * drawFactor);
				float rollRotation = 0.05f - (0.02f * drawFactor);

				m_componentModel.AimHandAngleOrder = 0.2f + (0.3f * drawFactor);
				m_componentModel.InHandItemOffsetOrder = new Vector3(horizontalOffset, verticalOffset, depthOffset);
				m_componentModel.InHandItemRotationOrder = new Vector3(pitchRotation, yawRotation, rollRotation);

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
				float fireProgress = (float)((m_subsystemTime.GameTime - m_fireTime) / 0.2);

				if (fireProgress < 0.5f)
				{
					float recoil = 0.02f * (1f - (fireProgress * 2f));
					m_componentModel.InHandItemOffsetOrder += new Vector3(recoil, 0f, 0f);
					m_componentModel.InHandItemRotationOrder += new Vector3(recoil * 2f, 0f, 0f);
				}
				else
				{
					float returnProgress = (fireProgress - 0.5f) / 0.5f;
					m_componentModel.AimHandAngleOrder = 0.2f * (1f - returnProgress);
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						0.05f * (1f - returnProgress),
						0.05f * (1f - returnProgress),
						0.05f * (1f - returnProgress)
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(
						-0.05f * (1f - returnProgress),
						0.3f * (1f - returnProgress),
						0.05f * (1f - returnProgress)
					);
				}
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
				m_componentModel.AimHandAngleOrder = 1.4f;
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
				float drawFactor = m_currentDraw;
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.08f + (0.05f * drawFactor),
					-0.08f,
					0.07f - (0.03f * drawFactor)
				);
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
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / 0.3f);
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.08f,
					-0.08f - (0.05f * reloadProgress),
					0.07f
				);
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
				float fireProgress = (float)((m_subsystemTime.GameTime - m_fireTime) / 0.2f);

				if (fireProgress < 0.5f)
				{
					float recoil = 0.05f * (1f - (fireProgress * 2f));
					m_componentModel.InHandItemOffsetOrder += new Vector3(recoil, 0f, 0f);
					m_componentModel.InHandItemRotationOrder += new Vector3(recoil * 2f, 0f, 0f);
				}
				else
				{
					float returnProgress = (fireProgress - 0.5f) / 0.5f;
					m_componentModel.AimHandAngleOrder = 1.4f * (1f - returnProgress);
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						-0.08f * (1f - returnProgress),
						-0.08f * (1f - returnProgress),
						0.07f * (1f - returnProgress)
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(
						-1.7f * (1f - returnProgress),
						0f,
						0f
					);
				}
			}
		}

		private void ProcessMusketBehavior(ComponentCreature target)
		{
			if (!m_isAiming && !m_isFiring && !m_isReloading && !m_isCocking)
			{
				StartAiming();
			}

			// Verificar inmersión en agua
			bool isInWater = (m_componentCreature.ComponentBody.ImmersionFactor > 0.4f);

			if (m_isCocking)
			{
				float cockProgress = (float)((m_subsystemTime.GameTime - m_drawStartTime) / m_cockTime);

				if (m_componentModel != null)
				{
					m_componentModel.AimHandAngleOrder = 1.2f + (0.2f * cockProgress);
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						-0.08f + (0.03f * cockProgress),
						-0.08f - (0.01f * cockProgress),
						0.07f + (0.01f * cockProgress)
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(
						-1.6f - (0.1f * cockProgress),
						0f,
						0f
					);

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
					// [CORREGIDO] Intentar disparar - SIEMPRE se completa el disparo, pero con efecto diferente
					m_isAiming = false;
					m_isFiring = true;
					m_fireTime = m_subsystemTime.GameTime;

					// Sonido de martillo (siempre suena)
					m_subsystemAudio.PlaySound("Audio/HammerUncock", 1f, m_random.Float(-0.1f, 0.1f),
						m_componentCreature.ComponentBody.Position, 3f, false);

					if (isInWater)
					{
						// [CORREGIDO] Igual que SubsystemMusketBlockBehavior: fallo bajo el agua
						// Programar sonido de fallo en lugar del disparo normal
						m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + 0.05, delegate
						{
							m_subsystemAudio.PlaySound("Audio/MusketMisfire", 1f, m_random.Float(-0.1f, 0.1f),
								m_componentCreature.ComponentBody.Position, 15f, false);
						});

						// NO disparar proyectil
					}
					else
					{
						// Disparo normal (programado con delay como en el original)
						m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + 0.05, delegate
						{
							m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, m_random.Float(-0.1f, 0.1f),
								m_componentCreature.ComponentBody.Position, 15f, false);
						});

						// Disparar la bala
						ShootMusketBullet(target);
					}

					// [CORREGIDO] SIEMPRE se descarga el arma (como en el original)
					UpdateMusketHammerState(false);
					UpdateMusketLoadState(MusketBlock.LoadState.Empty);

					// Aplicar retroceso (solo si no está en agua? El original aplica siempre)
					if (target != null)
					{
						Vector3 direction = Vector3.Normalize(
							target.ComponentBody.Position - m_componentCreature.ComponentBody.Position
						);
						m_componentCreature.ComponentBody.ApplyImpulse(-direction * 3f);
					}
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
			m_subsystemAudio.PlaySound("Audio/HammerCock", 1f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 1.2f;
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
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

				float breath = (float)Math.Sin(m_subsystemTime.GameTime * 3f) * 0.01f;
				m_componentModel.InHandItemOffsetOrder += new Vector3(0f, breath, 0f);

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
				float fireProgress = (float)((m_subsystemTime.GameTime - m_fireTime) / 0.2f);

				if (fireProgress < 0.5f)
				{
					float recoil = 0.1f * (1f - (fireProgress * 2f));
					m_componentModel.InHandItemOffsetOrder += new Vector3(recoil, 0f, 0f);
					m_componentModel.InHandItemRotationOrder += new Vector3(recoil * 3f, 0f, 0f);
				}
				else
				{
					float returnProgress = (fireProgress - 0.5f) / 0.5f;
					m_componentModel.AimHandAngleOrder = 1.4f * (1f - returnProgress);
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						-0.08f * (1f - returnProgress),
						-0.08f * (1f - returnProgress),
						0.07f * (1f - returnProgress)
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(
						-1.7f * (1f - returnProgress),
						0f,
						0f
					);
				}
			}
		}

		private void ApplyMusketReloadingAnimation()
		{
			if (m_componentModel != null)
			{
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / m_reloadTime);
				m_componentModel.AimHandAngleOrder = MathUtils.Lerp(1.0f, 0.5f, reloadProgress);
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.08f,
					-0.08f,
					0.07f - (0.1f * reloadProgress)
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					-1.7f + (0.5f * reloadProgress),
					0f,
					0f
				);
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ProcessFlameThrowerBehavior(ComponentCreature target, float distance)
		{
			if (!m_isAiming && !m_isFlameFiring && !m_isReloading && !m_isFlameCocking)
			{
				StartAiming();
			}

			// Verificar inmersión en agua
			bool isInWater = (m_componentCreature.ComponentBody.ImmersionFactor > 0.4f);

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

					// [CORREGIDO] Después de amartillar, verificar agua antes de disparar
					if (isInWater)
					{
						// Si está en agua, fallo inmediato sin entrar en m_isFlameFiring
						HandleFlameThrowerMisfire(target, currentSlotValue, currentLoadCount, currentData);
					}
					else
					{
						StartFlameThrowerFiring();
					}
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
					m_isAiming = false;

					// [CORREGIDO] Verificar agua ANTES de decidir si disparar
					if (isInWater)
					{
						// FALLASO BAJO EL AGUA - NO entrar en m_isFlameFiring
						HandleFlameThrowerMisfire(target, currentSlotValue, currentLoadCount, currentData);
					}
					else
					{
						// Disparo normal
						StartFlameThrowerFiring();
					}
				}
				else if (currentLoadState != FlameThrowerBlock.LoadState.Loaded || currentLoadCount <= 0)
				{
					m_isAiming = false;
					StartFlameThrowerReloading();
				}
			}
			else if (m_isFlameFiring)
			{
				// [CORREGIDO] Este estado SOLO se alcanza si NO está en agua
				// Por seguridad, verificar de nuevo
				if (isInWater)
				{
					// Si por algún motivo llegó aquí estando en agua, salir inmediatamente
					m_isFlameFiring = false;
					HandleFlameThrowerMisfire(target, currentSlotValue, currentLoadCount, currentData);
					return;
				}

				ApplyFlameThrowerFiringAnimation(target);

				// Disparar UNA vez
				FireFlameThrowerShot(target);

				// Consumir munición
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

				// Terminar el disparo y volver a apuntar
				m_isFlameFiring = false;

				// Animación de disparo (dura 0.2s)
				m_fireTime = m_subsystemTime.GameTime;

				// Programar vuelta a apuntar
				m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + 0.2, delegate
				{
					if (m_currentWeaponSlot != -1 && m_weaponType == 3)
					{
						StartAiming();
					}
				});
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

		// [NUEVO] Método auxiliar para manejar el fallo bajo el agua
		private void HandleFlameThrowerMisfire(ComponentCreature target, int slotValue, int loadCount, int data)
		{
			// Reproducir sonido de fallo
			m_subsystemAudio.PlaySound("Audio/MusketMisfire", 1f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentCreatureModel.EyePosition, 8f, true);

			// [CORREGIDO] En el original, bajo agua SÍ se consume munición (como en SubsystemFlameThrowerBlockBehavior.OnAim)
			if (loadCount > 1)
			{
				int newValue = FlameThrowerBlock.SetLoadCount(slotValue, loadCount - 1);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
			}
			else
			{
				data = FlameThrowerBlock.SetLoadState(data, FlameThrowerBlock.LoadState.Empty);
				data = FlameThrowerBlock.SetBulletType(data, null);
				int newValue = Terrain.ReplaceData(slotValue, data);
				newValue = FlameThrowerBlock.SetLoadCount(newValue, 0);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
			}

			// Volver a apuntar después del fallo
			StartAiming();
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
				float cockProgress = (float)((m_subsystemTime.GameTime - m_cockStartTime) / m_flameCockTime);

				m_componentModel.AimHandAngleOrder = 1.2f + (0.2f * cockProgress);
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.07f + (0.02f * cockProgress),
					-0.06f - (0.01f * cockProgress),
					0.06f + (0.01f * cockProgress)
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					-1.5f - (0.2f * cockProgress),
					0f,
					0f
				);

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
				m_componentModel.AimHandAngleOrder = 1.3f;
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
				float shake = (float)Math.Sin(m_subsystemTime.GameTime * 50f) * 0.005f;

				m_componentModel.AimHandAngleOrder = 1.3f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.07f + shake,
					-0.06f,
					0.06f
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.5f + shake * 2f, 0f, 0f);

				float fireProgress = (float)((m_subsystemTime.GameTime - m_flameStartTime) / 3.0);
				float recoil = MathUtils.Min(fireProgress * 0.02f, 0.01f);
				m_componentModel.InHandItemOffsetOrder += new Vector3(recoil, 0f, 0f);

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
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / 1.5f);

				m_componentModel.AimHandAngleOrder = MathUtils.Lerp(1.0f, 0.3f, reloadProgress);
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.07f,
					-0.06f - (0.1f * reloadProgress),
					0.06f - (0.05f * reloadProgress)
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					-1.5f + (0.5f * reloadProgress),
					0f,
					0f
				);
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
				m_componentModel.AimHandAngleOrder = 1.3f;
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
				float drawFactor = m_currentDraw;

				m_componentModel.AimHandAngleOrder = 1.3f + drawFactor * 0.1f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.08f + (0.05f * drawFactor),
					-0.1f,
					0.07f - (0.03f * drawFactor)
				);
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
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / 0.2f);

				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.03f,
					-0.1f - (0.05f * reloadProgress),
					0.04f
				);
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
				float fireProgress = (float)((m_subsystemTime.GameTime - m_fireTime) / 0.2f);

				if (fireProgress < 0.5f)
				{
					float recoil = 0.05f * (1f - (fireProgress * 2f));
					m_componentModel.InHandItemOffsetOrder += new Vector3(recoil, 0f, 0f);
					m_componentModel.InHandItemRotationOrder += new Vector3(recoil * 2f, 0f, 0f);
				}
				else
				{
					float returnProgress = (fireProgress - 0.5f) / 0.5f;
					m_componentModel.AimHandAngleOrder = 1.4f * (1f - returnProgress);
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						-0.03f * (1f - returnProgress),
						-0.1f * (1f - returnProgress),
						0.04f * (1f - returnProgress)
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(
						-1.55f * (1f - returnProgress),
						0f,
						0f
					);
				}
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
			m_isFirearmFiring = false;
			m_isFirearmReloading = false;
			m_isThrowableAiming = false;
			m_isThrowableThrowing = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;
			m_arrowVisible = false;
			m_hasArrowInBow = false;
			m_hasFirearmAimed = false;

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
			m_isItemsLauncherReloading = false;
			m_itemsLauncherShotsFired = 0;
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
				m_componentModel.AimHandAngleOrder = 0f;
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

		private int FindBestMeleeWeaponSlot()
		{
			if (m_componentInventory == null)
				return -1;

			float bestPower = 1f;
			int bestSlot = -1;
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					float power = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)].GetMeleePower(slotValue);
					if (power > bestPower)
					{
						bestPower = power;
						bestSlot = i;
					}
				}
			}
			return bestSlot;
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
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;

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
