using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentFirearmsShooters : Component, IUpdateable
	{
		private static readonly Dictionary<int, FirearmConfig> FirearmConfigs = new Dictionary<int, FirearmConfig>();

		public float MaxShootingDistance = 100f;
		public float SpreadFactor = 0.05f;
		public float ReloadChance = 0.05f;
		public float MinReloadInterval = 5f;
		public float SoundVolume = 1f;
		public float SoundRange = 10f;
		public bool UseRandomReloads = true;
		public float TargetHeightOffset = 0.5f;
		public float ReloadTime = 1.0f;
		public float PistolAimTime = 0.5f;
		public float SniperAimTime = 1.0f;
		public float MeleeRange = 5f;
		public bool UseMeleeSwitch = true;

		private bool m_isAiming = false;
		private bool m_isFiring = false;
		private bool m_isReloading = false;
		private double m_animationStartTime;
		private double m_fireTime;

		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemTerrain m_subsystemTerrain;
		private ComponentCreature m_componentCreature;
		private ComponentInventory m_componentInventory;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentNewChaseBehavior m_componentNewChaseBehavior;
		private ComponentNewChaseBehavior2 m_componentNewChaseBehavior2;
		private ComponentBanditChaseBehavior m_componentBanditChaseBehavior;
		private ComponentZombieChaseBehavior m_componentZombieChaseBehavior;
		private ComponentCreatureModel m_componentModel;
		private Game.Random m_random = new Game.Random();

		private double m_lastShootTime;
		private double m_lastReloadTime;
		private int m_currentWeaponIndex = -1;
		private int m_shotsSinceLastReload = 0;
		private bool m_isMelee = false;
		private double m_lastMeleeAttackTime;
		private ComponentMiner m_componentMiner;
		private ComponentPathfinding m_componentPathfinding;

		private ComponentCreature GetTarget()
		{
			if (m_componentChaseBehavior != null && m_componentChaseBehavior.Target != null)
				return m_componentChaseBehavior.Target;
			if (m_componentNewChaseBehavior != null && m_componentNewChaseBehavior.Target != null)
				return m_componentNewChaseBehavior.Target;
			if (m_componentNewChaseBehavior2 != null && m_componentNewChaseBehavior2.Target != null)
				return m_componentNewChaseBehavior2.Target;
			if (m_componentBanditChaseBehavior != null && m_componentBanditChaseBehavior.Target != null)
				return m_componentBanditChaseBehavior.Target;
			if (m_componentZombieChaseBehavior != null && m_componentZombieChaseBehavior.Target != null)
				return m_componentZombieChaseBehavior.Target;
			return null;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			MaxShootingDistance = valuesDictionary.GetValue<float>("MaxShootingDistance", 25f);
			SpreadFactor = valuesDictionary.GetValue<float>("SpreadFactor", 0.05f);
			ReloadChance = valuesDictionary.GetValue<float>("ReloadChance", 0.05f);
			MinReloadInterval = valuesDictionary.GetValue<float>("MinReloadInterval", 5f);
			UseRandomReloads = valuesDictionary.GetValue<bool>("UseRandomReloads", true);
			TargetHeightOffset = valuesDictionary.GetValue<float>("TargetHeightOffset", 0.5f);
			ReloadTime = valuesDictionary.GetValue<float>("ReloadTime", 1.0f);
			PistolAimTime = valuesDictionary.GetValue<float>("PistolAimTime", 0.5f);
			SniperAimTime = valuesDictionary.GetValue<float>("SniperAimTime", 1.0f);
			MeleeRange = valuesDictionary.GetValue<float>("MeleeRange", 5f);
			UseMeleeSwitch = valuesDictionary.GetValue<bool>("UseMeleeSwitch", true);

			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);

			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(false);
			m_componentNewChaseBehavior = base.Entity.FindComponent<ComponentNewChaseBehavior>(false);
			m_componentNewChaseBehavior2 = base.Entity.FindComponent<ComponentNewChaseBehavior2>(false);
			m_componentBanditChaseBehavior = base.Entity.FindComponent<ComponentBanditChaseBehavior>(false);
			m_componentZombieChaseBehavior = base.Entity.FindComponent<ComponentZombieChaseBehavior>(false);
			m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);

			if (FirearmConfigs.Count == 0)
			{
				InitializeFirearmConfigs();
			}

			if (m_componentCreature == null || m_componentInventory == null)
			{
				throw new InvalidOperationException("NPC necesita ComponentCreature y ComponentInventory para usar armas de fuego.");
			}
		}

		private void InitializeFirearmConfigs()
		{
			try
			{
				int akIndex = BlocksManager.GetBlockIndex(typeof(Game.AKBlock), true, false);
				FirearmConfigs[akIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala2),
					ShootSound = "Audio/Armas/ak 47 fuego",
					FireRate = 0.17,
					BulletSpeed = 280f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.01f, 0.01f, 0.05f),
					NoiseRadius = 40f,
					IsAutomatic = true
				};

				int m4Index = BlocksManager.GetBlockIndex(typeof(Game.M4Block), true, false);
				FirearmConfigs[m4Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala2),
					ShootSound = "Audio/Armas/M4 fuego",
					FireRate = 0.15,
					BulletSpeed = 300f,
					MaxShotsBeforeReload = 22,
					ProjectilesPerShot = 3,
					SpreadVector = new Vector3(0.008f, 0.008f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true
				};

				int kaIndex = BlocksManager.GetBlockIndex(typeof(Game.KABlock), true, false);
				FirearmConfigs[kaIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala5),
					ShootSound = "Audio/Armas/KA fuego",
					FireRate = 0.1,
					BulletSpeed = 320f,
					MaxShotsBeforeReload = 40,
					ProjectilesPerShot = 3,
					SpreadVector = new Vector3(0.007f, 0.007f, 0.03f),
					NoiseRadius = 35f,
					IsAutomatic = true
				};

				int mac10Index = BlocksManager.GetBlockIndex(typeof(Game.Mac10Block), true, false);
				FirearmConfigs[mac10Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala3),
					ShootSound = "Audio/Armas/mac 10 fuego",
					FireRate = 0.1,
					BulletSpeed = 300f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.012f, 0.012f, 0.035f),
					NoiseRadius = 30f,
					IsAutomatic = true
				};

				int swm500Index = BlocksManager.GetBlockIndex(typeof(Game.SWM500Block), true, false);
				FirearmConfigs[swm500Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala4),
					ShootSound = "Audio/Armas/desert eagle fuego",
					FireRate = 0.5,
					BulletSpeed = 320f,
					MaxShotsBeforeReload = 5,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.02f, 0.02f, 0.05f),
					NoiseRadius = 40f,
					IsAutomatic = false
				};

				int g3Index = BlocksManager.GetBlockIndex(typeof(Game.G3Block), true, false);
				FirearmConfigs[g3Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/FX05",
					FireRate = 0.12,
					BulletSpeed = 290f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.009f, 0.009f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true
				};

				int izh43Index = BlocksManager.GetBlockIndex(typeof(Game.Izh43Block), true, false);
				FirearmConfigs[izh43Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/shotgun fuego",
					FireRate = 1.0,
					BulletSpeed = 280f,
					MaxShotsBeforeReload = 2,
					ProjectilesPerShot = 8,
					SpreadVector = new Vector3(0.09f, 0.09f, 0.09f),
					NoiseRadius = 45f,
					IsAutomatic = false
				};

				int minigunIndex = BlocksManager.GetBlockIndex(typeof(Game.MinigunBlock), true, false);
				FirearmConfigs[minigunIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala6),
					ShootSound = "Audio/Armas/Chaingun fuego",
					FireRate = 0.08,
					BulletSpeed = 260f,
					MaxShotsBeforeReload = 100,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.02f, 0.02f, 0.08f),
					NoiseRadius = 50f,
					IsAutomatic = true
				};

				int spas12Index = BlocksManager.GetBlockIndex(typeof(Game.SPAS12Block), true, false);
				FirearmConfigs[spas12Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/SPAS 12 fuego",
					FireRate = 0.8,
					BulletSpeed = 280f,
					MaxShotsBeforeReload = 8,
					ProjectilesPerShot = 8,
					SpreadVector = new Vector3(0.09f, 0.09f, 0.09f),
					NoiseRadius = 40f,
					IsAutomatic = false
				};

				int uziIndex = BlocksManager.GetBlockIndex(typeof(Game.UziBlock), true, false);
				FirearmConfigs[uziIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala2),
					ShootSound = "Audio/Armas/Uzi fuego",
					FireRate = 0.08,
					BulletSpeed = 320f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.015f, 0.015f, 0.06f),
					NoiseRadius = 40f,
					IsAutomatic = true
				};

				int sniperIndex = BlocksManager.GetBlockIndex(typeof(Game.SniperBlock), true, false);
				FirearmConfigs[sniperIndex] = new FirearmConfig
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
					IsSniper = true
				};

				int augIndex = BlocksManager.GetBlockIndex(typeof(Game.AUGBlock), true, false);
				FirearmConfigs[augIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/AUG fuego",
					FireRate = 0.17,
					BulletSpeed = 280f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.01f, 0.01f, 0.05f),
					NoiseRadius = 40f,
					IsAutomatic = true
				};

				int p90Index = BlocksManager.GetBlockIndex(typeof(Game.P90Block), true, false);
				FirearmConfigs[p90Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/FN P90 fuego",
					FireRate = 0.067,
					BulletSpeed = 320f,
					MaxShotsBeforeReload = 50,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.012f, 0.012f, 0.04f),
					NoiseRadius = 35f,
					IsAutomatic = true
				};

				int scarIndex = BlocksManager.GetBlockIndex(typeof(Game.SCARBlock), true, false);
				FirearmConfigs[scarIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/FN Scar fuego",
					FireRate = 0.1,
					BulletSpeed = 310f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.01f, 0.01f, 0.03f),
					NoiseRadius = 45f,
					IsAutomatic = true
				};

				int revolverIndex = BlocksManager.GetBlockIndex(typeof(Game.RevolverBlock), true, false);
				FirearmConfigs[revolverIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala4),
					ShootSound = "Audio/Armas/Revolver fuego",
					FireRate = 0.6,
					BulletSpeed = 320f,
					MaxShotsBeforeReload = 6,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.02f, 0.02f, 0.05f),
					NoiseRadius = 40f,
					IsAutomatic = false
				};

				int famasIndex = BlocksManager.GetBlockIndex(typeof(Game.FamasBlock), true, false);
				FirearmConfigs[famasIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala4),
					ShootSound = "Audio/Armas/FAMAS fuego",
					FireRate = 0.09,
					BulletSpeed = 450f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.012f, 0.012f, 0.04f),
					NoiseRadius = 35f,
					IsAutomatic = true
				};

				int aa12Index = BlocksManager.GetBlockIndex(typeof(Game.AA12Block), true, false);
				FirearmConfigs[aa12Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala6),
					ShootSound = "Audio/Armas/AA12 fuego",
					FireRate = 0.2,
					BulletSpeed = 350f,
					MaxShotsBeforeReload = 20,
					ProjectilesPerShot = 8,
					SpreadVector = new Vector3(0.03f, 0.03f, 0.06f),
					NoiseRadius = 45f,
					IsAutomatic = true
				};

				int m249Index = BlocksManager.GetBlockIndex(typeof(Game.M249Block), true, false);
				FirearmConfigs[m249Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala5),
					ShootSound = "Audio/Armas/M249 fuego",
					FireRate = 0.08,
					BulletSpeed = 400f,
					MaxShotsBeforeReload = 100,
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.01f, 0.01f, 0.01f),
					NoiseRadius = 50f,
					IsAutomatic = true
				};

				int newG3Index = BlocksManager.GetBlockIndex(typeof(Game.NewG3Block), true, false);
				FirearmConfigs[newG3Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala3),
					ShootSound = "Audio/Armas/G3 fuego",
					FireRate = 0.12,
					BulletSpeed = 290f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.009f, 0.009f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true
				};

				int mp5ssdIndex = BlocksManager.GetBlockIndex(typeof(Game.MP5SSDBlock), true, false);
				FirearmConfigs[mp5ssdIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala3),
					ShootSound = "Audio/Armas/MP5SSD fuego",
					FireRate = 0.12,
					BulletSpeed = 290f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.009f, 0.009f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true
				};

				int mendozaIndex = BlocksManager.GetBlockIndex(typeof(Game.MendozaBlock), true, false);
				FirearmConfigs[mendozaIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala3),
					ShootSound = "Audio/Armas/Mendoza fuego",
					FireRate = 0.12,
					BulletSpeed = 290f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.009f, 0.009f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true
				};

				int grozaIndex = BlocksManager.GetBlockIndex(typeof(Game.GrozaBlock), true, false);
				FirearmConfigs[grozaIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala3),
					ShootSound = "Audio/Armas/Groza fuego",
					FireRate = 0.12,
					BulletSpeed = 290f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.009f, 0.009f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true
				};
			}
			catch (Exception ex)
			{
				Log.Error($"Error inicializando configuraciones de armas: {ex.Message}");
			}
		}

		public void Update(float dt)
		{
			double currentTime = m_subsystemTime.GameTime;
			if (m_componentCreature.ComponentHealth.Health <= 0f)
			{
				ResetAnimations();
				return;
			}
			if (m_isReloading)
			{
				ApplyReloadingAnimation(dt);
				if (currentTime - m_animationStartTime >= ReloadTime)
				{
					m_isReloading = false;
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
						catch (Exception ex)
						{
							Log.Warning($"Error mostrando partículas de balas listas: {ex.Message}");
						}
					}
					m_subsystemAudio.PlaySound("Audio/Armas/reload", SoundVolume, 0f, m_componentCreature.ComponentCreatureModel.EyePosition, SoundRange, true);
					m_isAiming = false;
					m_lastShootTime = currentTime;
				}
				return;
			}
			if (m_isFiring)
			{
				ApplyFiringAnimation(dt);
				float fireAnimationTime = IsCurrentWeaponAutomatic() ? 0.1f : 0.2f;
				if (IsCurrentWeaponSniper())
				{
					fireAnimationTime = 0.5f;
				}
				if (currentTime - m_fireTime >= fireAnimationTime)
				{
					m_isFiring = false;
				}
				return;
			}
			ComponentCreature target = GetTarget();
			if (target == null)
			{
				ResetAnimations();
				m_currentWeaponIndex = -1;
				return;
			}
			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);
			if (UseMeleeSwitch)
			{
				if (distance <= MeleeRange)
				{
					if (!m_isMelee)
					{
						SwitchToMeleeMode();
					}
					UpdateMeleeMode(dt, target);
					return;
				}
				else
				{
					if (m_isMelee)
					{
						SwitchToRangedMode();
					}
				}
			}
			float maxDistance = MaxShootingDistance;
			if (IsCurrentWeaponSniper() && m_currentWeaponIndex != -1)
			{
				maxDistance = MaxShootingDistance * 3f;
			}
			if (distance > maxDistance)
			{
				ResetAnimations();
				m_currentWeaponIndex = -1;
				return;
			}
			FindWeaponInInventory();
			if (m_currentWeaponIndex == -1)
			{
				ResetAnimations();
				return;
			}
			FirearmConfig config = GetCurrentConfig();
			if (config == null)
				return;
			if (config.IsSniper)
			{
				UpdateSniperWeapon(currentTime, target);
			}
			else if (config.IsAutomatic)
			{
				UpdateAutomaticWeapon(currentTime, target);
			}
			else
			{
				UpdatePistolWeapon(currentTime, target);
			}
		}

		private void UpdateAutomaticWeapon(double currentTime, ComponentCreature target)
		{
			if (!m_isAiming)
			{
				if (!m_isReloading)
				{
					m_isAiming = true;
					m_animationStartTime = currentTime;
				}
				return;
			}
			ApplyAimingAnimation(target);
			float aimTimeRequired = 0.3f;
			if (currentTime - m_animationStartTime >= aimTimeRequired)
			{
				if (currentTime - m_lastShootTime >= GetCurrentFireRate())
				{
					Fire(target);
					m_lastShootTime = currentTime;
					m_shotsSinceLastReload++;
					if (ShouldReload(currentTime))
					{
						StartReloading();
					}
				}
			}
		}

		private void UpdatePistolWeapon(double currentTime, ComponentCreature target)
		{
			if (!m_isAiming)
			{
				if (!m_isReloading)
				{
					StartAiming();
				}
				return;
			}
			ApplyAimingAnimation(target);
			float aimTime = PistolAimTime;
			if (IsCurrentWeaponShotgun())
			{
				aimTime *= 0.8f;
			}
			if (currentTime - m_animationStartTime >= aimTime)
			{
				if (currentTime - m_lastShootTime >= GetCurrentFireRate())
				{
					Fire(target);
					m_lastShootTime = currentTime;
					m_shotsSinceLastReload++;
					if (ShouldReload(currentTime))
					{
						StartReloading();
					}
					m_isAiming = false;
				}
			}
		}

		private void UpdateSniperWeapon(double currentTime, ComponentCreature target)
		{
			if (!m_isAiming)
			{
				if (!m_isReloading)
				{
					StartAiming();
				}
				return;
			}
			ApplySniperAimingAnimation(target);
			if (currentTime - m_animationStartTime >= SniperAimTime)
			{
				if (currentTime - m_lastShootTime >= GetCurrentFireRate())
				{
					Fire(target);
					m_lastShootTime = currentTime;
					m_shotsSinceLastReload++;
					if (m_shotsSinceLastReload >= 1)
					{
						StartReloading();
					}
					m_isAiming = false;
				}
			}
		}

		private void StartAiming()
		{
			m_isAiming = true;
			m_isFiring = false;
			m_isReloading = false;
			m_animationStartTime = m_subsystemTime.GameTime;
		}

		private void ApplyAimingAnimation(ComponentCreature target)
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

		private void ApplySniperAimingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 1.2f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.06f, 0.08f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
				if (target != null)
				{
					Vector3 targetPosition = target.ComponentBody.Position;
					targetPosition.Y += 0.5f;
					m_componentModel.LookAtOrder = new Vector3?(targetPosition);
				}
			}
		}

		private void ApplyFiringAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				float timeSinceFire = (float)(m_subsystemTime.GameTime - m_fireTime);
				float recoilFactor;
				if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Game.SWM500Block), true, false) ||
					m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Game.RevolverBlock), true, false))
				{
					recoilFactor = (float)(1.8f - timeSinceFire * 3f);
				}
				else if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Game.Izh43Block), true, false) ||
						m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Game.SPAS12Block), true, false))
				{
					recoilFactor = (float)(2.0f - timeSinceFire * 2.5f);
				}
				else if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Game.MinigunBlock), true, false))
				{
					recoilFactor = (float)(1.3f - timeSinceFire * 5f);
				}
				else if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Game.SniperBlock), true, false))
				{
					recoilFactor = (float)(2.5f - timeSinceFire * 1.5f);
				}
				else if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Game.FamasBlock), true, false))
				{
					recoilFactor = (float)(1.6f - timeSinceFire * 6f);
				}
				else
				{
					recoilFactor = (float)(1.5f - timeSinceFire * 8f);
				}
				recoilFactor = MathUtils.Max(recoilFactor, 1.0f);
				m_componentModel.AimHandAngleOrder = 1.4f * recoilFactor;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f - (0.05f * (1.5f - recoilFactor)));
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f + (0.3f * (1.5f - recoilFactor)), 0f, 0f);
			}
		}

		private void ApplyReloadingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / ReloadTime);
				if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Game.Izh43Block), true, false))
				{
					reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / (ReloadTime * 1.5f));
				}
				else if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Game.SniperBlock), true, false))
				{
					reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / (ReloadTime * 2.0f));
				}
				else if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Game.RevolverBlock), true, false))
				{
					reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / (ReloadTime * 1.2f));
				}
				m_componentModel.AimHandAngleOrder = 0.0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ResetAnimations()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = false;
			m_isMelee = false;
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
				m_componentModel.AttackOrder = false;
			}
		}

		private void FindWeaponInInventory()
		{
			if (m_componentInventory == null) return;
			int activeSlotValue = m_componentInventory.GetSlotValue(m_componentInventory.ActiveSlotIndex);
			if (activeSlotValue != 0)
			{
				int blockIndex = Terrain.ExtractContents(activeSlotValue);
				if (FirearmConfigs.ContainsKey(blockIndex))
				{
					m_currentWeaponIndex = blockIndex;
					return;
				}
			}
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					int blockIndex = Terrain.ExtractContents(slotValue);
					if (FirearmConfigs.ContainsKey(blockIndex))
					{
						m_currentWeaponIndex = blockIndex;
						m_componentInventory.ActiveSlotIndex = i;
						return;
					}
				}
			}
			m_currentWeaponIndex = -1;
		}

		private void Fire(ComponentCreature target)
		{
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;
			if (m_currentWeaponIndex == -1)
				return;
			FirearmConfig config = GetCurrentConfig();
			if (config == null || target == null)
				return;
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
				else
				{
					targetPosition.Y -= TargetHeightOffset;
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
					int bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, 2);
					m_subsystemProjectiles.FireProjectile(bulletValue, shootPosition, config.BulletSpeed * (direction + spread), Vector3.Zero, m_componentCreature);
				}
				Vector3 particlePosition = shootPosition + direction * 1.3f;
				if (m_subsystemParticles != null && m_subsystemTerrain != null)
				{
					m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, particlePosition, direction), false);
				}
				if (m_subsystemNoise != null)
				{
					m_subsystemNoise.MakeNoise(shootPosition, 0.8f, config.NoiseRadius);
				}
				float pitchVariation = m_random.Float(-0.1f, 0.1f);
				if (config.IsSniper)
				{
					pitchVariation = m_random.Float(-0.05f, 0.05f);
				}
				m_subsystemAudio.PlaySound(config.ShootSound, SoundVolume, pitchVariation, shootPosition, SoundRange, true);
			}
			catch (Exception ex)
			{
				Log.Error($"Error al disparar: {ex.Message}");
			}
		}

		private bool ShouldReload(double currentTime)
		{
			if (!UseRandomReloads)
				return false;
			if (currentTime - m_lastReloadTime < MinReloadInterval)
				return false;
			FirearmConfig config = GetCurrentConfig();
			if (config != null && m_shotsSinceLastReload >= config.MaxShotsBeforeReload)
				return true;
			float adjustedReloadChance = ReloadChance;
			if (config != null)
			{
				if (config.MaxShotsBeforeReload <= 2)
				{
					adjustedReloadChance *= 2.0f;
				}
				else if (config.IsSniper)
				{
					return m_shotsSinceLastReload >= 1;
				}
			}
			return m_random.Float(0f, 1f) < adjustedReloadChance;
		}

		private void StartReloading()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = true;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_lastReloadTime = m_subsystemTime.GameTime;
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
					Vector3 basePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
					KillParticleSystem reloadParticles = new KillParticleSystem(m_subsystemTerrain, basePosition, 0.5f);
					m_subsystemParticles.AddParticleSystem(reloadParticles, false);
					for (int i = 0; i < 3; i++)
					{
						Vector3 offset = new Vector3(m_random.Float(-0.2f, 0.2f), m_random.Float(0.1f, 0.4f), m_random.Float(-0.2f, 0.2f));
						KillParticleSystem additionalParticles = new KillParticleSystem(m_subsystemTerrain, basePosition + offset, 0.5f);
						m_subsystemParticles.AddParticleSystem(additionalParticles, false);
					}
				}
				catch (Exception ex)
				{
					Log.Warning($"Error mostrando partículas de recarga: {ex.Message}");
				}
			}
			m_subsystemAudio.PlaySound("Audio/Armas/reload", SoundVolume * 0.8f, 0f, m_componentCreature.ComponentCreatureModel.EyePosition, SoundRange, true);
		}

		private double GetCurrentFireRate()
		{
			FirearmConfig config = GetCurrentConfig();
			return config != null ? config.FireRate : 1.0;
		}

		private FirearmConfig GetCurrentConfig()
		{
			if (m_currentWeaponIndex == -1 || !FirearmConfigs.ContainsKey(m_currentWeaponIndex))
				return null;
			return FirearmConfigs[m_currentWeaponIndex];
		}

		private bool IsCurrentWeaponAutomatic()
		{
			FirearmConfig config = GetCurrentConfig();
			return config != null && config.IsAutomatic;
		}

		private bool IsCurrentWeaponSniper()
		{
			FirearmConfig config = GetCurrentConfig();
			return config != null && config.IsSniper;
		}

		private bool IsCurrentWeaponShotgun()
		{
			if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Game.Izh43Block), true, false) ||
				m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Game.SPAS12Block), true, false))
			{
				return true;
			}
			return false;
		}

		private void SwitchToMeleeMode()
		{
			m_isMelee = true;
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = false;
			m_lastMeleeAttackTime = m_subsystemTime.GameTime;
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
			FindMeleeWeapon();
		}

		private void SwitchToRangedMode()
		{
			m_isMelee = false;
			FindWeaponInInventory();
		}

		private void UpdateMeleeMode(float dt, ComponentCreature target)
		{
			if (!HasMeleeWeaponEquipped())
			{
				if (!FindMeleeWeapon())
				{
					if (m_componentPathfinding != null && target != null)
					{
						Vector3 retreatDirection = Vector3.Normalize(m_componentCreature.ComponentBody.Position - target.ComponentBody.Position);
						Vector3 retreatPosition = m_componentCreature.ComponentBody.Position + retreatDirection * 3f;
						m_componentPathfinding.SetDestination(new Vector3?(retreatPosition), 1f, 1f, 0, false, true, false, null);
					}
					return;
				}
			}
			if (target != null && m_componentModel != null)
			{
				m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
			}
			double currentTime = m_subsystemTime.GameTime;
			if (currentTime - m_lastMeleeAttackTime >= 0.8f)
			{
				AttackMelee(target);
				m_lastMeleeAttackTime = currentTime;
			}
		}

		private bool FindMeleeWeapon()
		{
			if (m_componentInventory == null)
				return false;
			float bestPower = 1f;
			int bestSlot = -1;
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					float meleePower = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)].GetMeleePower(slotValue);
					if (meleePower > bestPower)
					{
						bestPower = meleePower;
						bestSlot = i;
					}
				}
			}
			if (bestSlot >= 0)
			{
				m_componentInventory.ActiveSlotIndex = bestSlot;
				return true;
			}
			return false;
		}

		private bool HasMeleeWeaponEquipped()
		{
			if (m_componentInventory == null)
				return false;
			int activeSlotValue = m_componentInventory.GetSlotValue(m_componentInventory.ActiveSlotIndex);
			if (activeSlotValue == 0)
				return false;
			float meleePower = BlocksManager.Blocks[Terrain.ExtractContents(activeSlotValue)].GetMeleePower(activeSlotValue);
			return meleePower > 1f;
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
			Vector3 vector = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 v = target.BoundingBox.Center();
			Ray3 ray = new Ray3(vector, Vector3.Normalize(v - vector));
			BodyRaycastResult? bodyRaycastResult = m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			if (bodyRaycastResult != null && bodyRaycastResult.Value.Distance < 1.75f &&
				(bodyRaycastResult.Value.ComponentBody == target ||
				 bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) ||
				 target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody) ||
				 target.StandingOnBody == bodyRaycastResult.Value.ComponentBody))
			{
				hitPoint = bodyRaycastResult.Value.HitPoint();
				return bodyRaycastResult.Value.ComponentBody;
			}
			hitPoint = default(Vector3);
			return null;
		}

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
		}

		public int UpdateOrder => 1000;
	}
}
