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
		// Configuraciones de armas - SIMPLIFICADO
		private static Dictionary<int, FirearmConfig> m_firearmConfigs;

		// Parámetros
		public float MaxShootingDistance = 25f;
		public float SpreadFactor = 0.05f;
		public float ReloadChance = 0.05f;
		public float MinReloadInterval = 5f;
		public float SoundVolume = 1f;
		public float SoundRange = 10f;
		public float TargetHeightOffset = 0.5f;
		public float ReloadTime = 1.0f;

		// Referencias a subsistemas
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemTerrain m_subsystemTerrain;

		// Referencias a componentes
		private ComponentCreature m_componentCreature;
		private ComponentInventory m_componentInventory;
		private ComponentCreatureModel m_componentModel;
		private ComponentNewChaseBehavior m_componentNewChaseBehavior;
		private ComponentChaseBehavior m_componentChaseBehavior;

		// Estado
		private Random m_random = new Random();
		private int m_currentWeaponIndex = -1;
		private double m_lastShootTime;
		private double m_lastReloadTime;
		private int m_shotsSinceLastReload = 0;
		private bool m_isReloading = false;
		private double m_reloadStartTime;

		public ComponentFirearmsShooters()
		{
			// Inicializar configuraciones estáticas solo una vez
			if (m_firearmConfigs == null)
			{
				InitializeFirearmConfigs();
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Inicializar subsistemas
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);

			// Buscar componentes necesarios
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);

			// Buscar comportamientos de persecución
			m_componentNewChaseBehavior = base.Entity.FindComponent<ComponentNewChaseBehavior>();
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>();

			if (m_componentCreature == null || m_componentInventory == null)
			{
				Log.Warning("ComponentFirearmsShooters: Falta ComponentCreature o ComponentInventory");
			}
		}

		private void InitializeFirearmConfigs()
		{
			m_firearmConfigs = new Dictionary<int, FirearmConfig>();

			try
			{
				// Configuraciones básicas para todas las armas
				// AK-47
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
					IsAutomatic = true
				};

				// M4
				int m4Index = BlocksManager.GetBlockIndex(typeof(M4Block), true, false);
				m_firearmConfigs[m4Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala2),
					ShootSound = "Audio/Armas/M6 fuego",
					FireRate = 0.15,
					BulletSpeed = 300f,
					MaxShotsBeforeReload = 22,
					ProjectilesPerShot = 3,
					SpreadVector = new Vector3(0.008f, 0.008f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true
				};

				// Mac10
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
					IsAutomatic = true
				};

				// SWM500
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
					IsAutomatic = false
				};

				// G3
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
					IsAutomatic = true
				};

				// Izh43
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
					IsAutomatic = false
				};

				// Minigun
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
					IsAutomatic = true
				};

				// SPAS12
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
					IsAutomatic = false
				};

				// Uzi
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
					IsAutomatic = true
				};

				// Sniper
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
					IsSniper = true
				};
			}
			catch (Exception ex)
			{
				Log.Error($"Error inicializando configuraciones de armas: {ex.Message}");
			}
		}

		public void Update(float dt)
		{
			if (m_componentCreature == null || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			// Verificar si estamos recargando
			if (m_isReloading)
			{
				if (m_subsystemTime.GameTime - m_reloadStartTime >= ReloadTime)
				{
					m_isReloading = false;
					m_shotsSinceLastReload = 0;
				}
				return;
			}

			// Obtener el objetivo actual
			ComponentCreature target = GetCurrentTarget();
			if (target == null)
			{
				ResetAnimation();
				return;
			}

			// Verificar si tenemos un arma de fuego equipada
			FindWeaponInInventory();
			if (m_currentWeaponIndex == -1)
			{
				ResetAnimation();
				return;
			}

			// Obtener configuración del arma
			FirearmConfig config = GetCurrentConfig();
			if (config == null)
			{
				ResetAnimation();
				return;
			}

			// Calcular distancia al objetivo
			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				target.ComponentBody.Position
			);

			// Ajustar distancia máxima para sniper
			float maxDistance = MaxShootingDistance;
			if (config.IsSniper)
			{
				maxDistance *= 3f;
			}

			// Verificar si está en rango
			if (distance > maxDistance)
			{
				ResetAnimation();
				return;
			}

			// Aplicar animación de apuntado
			ApplyAimingAnimation(target, config);

			// Verificar si puede disparar
			double currentTime = m_subsystemTime.GameTime;
			double fireRate = config.FireRate;

			// Para armas automáticas, disparar continuamente
			if (config.IsAutomatic || (currentTime - m_lastShootTime >= fireRate))
			{
				// Verificar línea de visión
				if (HasLineOfSight(target))
				{
					Fire(target, config);
					m_lastShootTime = currentTime;
					m_shotsSinceLastReload++;

					// Verificar si necesita recargar
					if (m_shotsSinceLastReload >= config.MaxShotsBeforeReload)
					{
						StartReloading();
					}
				}
			}
		}

		private ComponentCreature GetCurrentTarget()
		{
			// Priorizar ComponentNewChaseBehavior
			if (m_componentNewChaseBehavior != null && m_componentNewChaseBehavior.IsActive)
			{
				return m_componentNewChaseBehavior.Target;
			}

			// Usar ComponentChaseBehavior si está disponible
			if (m_componentChaseBehavior != null && m_componentChaseBehavior.IsActive)
			{
				return m_componentChaseBehavior.Target;
			}

			return null;
		}

		private void FindWeaponInInventory()
		{
			if (m_componentInventory == null)
				return;

			// Verificar slot activo actual
			int activeSlotValue = m_componentInventory.GetSlotValue(m_componentInventory.ActiveSlotIndex);
			if (activeSlotValue != 0)
			{
				int blockIndex = Terrain.ExtractContents(activeSlotValue);
				if (m_firearmConfigs.ContainsKey(blockIndex))
				{
					m_currentWeaponIndex = blockIndex;
					return;
				}
			}

			// Buscar en todos los slots
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					int blockIndex = Terrain.ExtractContents(slotValue);
					if (m_firearmConfigs.ContainsKey(blockIndex))
					{
						m_currentWeaponIndex = blockIndex;
						m_componentInventory.ActiveSlotIndex = i;
						return;
					}
				}
			}

			m_currentWeaponIndex = -1;
		}

		private void ApplyAimingAnimation(ComponentCreature target, FirearmConfig config)
		{
			if (m_componentModel != null)
			{
				// Animación de apuntado básica
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

				// Mirar al objetivo
				if (target != null)
				{
					Vector3 lookAtPosition = target.ComponentCreatureModel.EyePosition;

					// Para sniper, apuntar al centro del cuerpo
					if (config.IsSniper)
					{
						lookAtPosition = target.ComponentBody.Position + new Vector3(0f, 0.5f, 0f);
					}

					m_componentModel.LookAtOrder = new Vector3?(lookAtPosition);
				}
			}
		}

		private void ResetAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private bool HasLineOfSight(ComponentCreature target)
		{
			if (target == null || m_componentCreature == null)
				return false;

			try
			{
				Vector3 startPos = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPos = target.ComponentCreatureModel.EyePosition;

				// Verificación simple de línea de visión
				float distance = Vector3.Distance(startPos, targetPos);
				if (distance > MaxShootingDistance * 2f) // Doble de rango para margen
					return false;

				return true;
			}
			catch
			{
				return false;
			}
		}

		private void Fire(ComponentCreature target, FirearmConfig config)
		{
			if (target == null || m_componentCreature == null)
				return;

			try
			{
				// Posición de disparo
				Vector3 shootPosition = m_componentCreature.ComponentCreatureModel.EyePosition +
					m_componentCreature.ComponentBody.Matrix.Right * 0.3f -
					m_componentCreature.ComponentBody.Matrix.Up * 0.2f;

				// Posición del objetivo
				Vector3 targetPosition = target.ComponentCreatureModel.EyePosition;
				targetPosition.Y -= TargetHeightOffset;

				// Para sniper, apuntar al centro del cuerpo
				if (config.IsSniper)
				{
					targetPosition = target.ComponentBody.Position + new Vector3(0f, 0.5f, 0f);
				}

				// Dirección de disparo
				Vector3 direction = Vector3.Normalize(targetPosition - shootPosition);

				// Vectores para dispersión
				Vector3 rightVector = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
				Vector3 upVector = Vector3.Normalize(Vector3.Cross(direction, rightVector));

				// Disparar múltiples proyectiles si es necesario
				for (int i = 0; i < config.ProjectilesPerShot; i++)
				{
					Vector3 spread = m_random.Float(-config.SpreadVector.X, config.SpreadVector.X) * rightVector +
									m_random.Float(-config.SpreadVector.Y, config.SpreadVector.Y) * upVector +
									m_random.Float(-config.SpreadVector.Z, config.SpreadVector.Z) * direction;

					// Crear bala
					int bulletBlockIndex = BlocksManager.GetBlockIndex(config.BulletBlockType, true, false);
					if (bulletBlockIndex != 0)
					{
						int bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, 2);

						// Disparar proyectil
						m_subsystemProjectiles.FireProjectile(
							bulletValue,
							shootPosition,
							config.BulletSpeed * (direction + spread * SpreadFactor),
							Vector3.Zero,
							m_componentCreature
						);
					}
				}

				// Efectos de partículas
				if (m_subsystemParticles != null)
				{
					Vector3 particlePosition = shootPosition + direction * 1.3f;
					m_subsystemParticles.AddParticleSystem(
						new GunFireParticleSystem(m_subsystemTerrain, particlePosition, direction),
						false
					);
				}

				// Sonido
				if (m_subsystemAudio != null && !string.IsNullOrEmpty(config.ShootSound))
				{
					float pitch = m_random.Float(0.9f, 1.1f);
					m_subsystemAudio.PlaySound(config.ShootSound, SoundVolume, pitch,
						shootPosition, SoundRange, true);
				}

				// Ruido
				if (m_subsystemNoise != null)
				{
					m_subsystemNoise.MakeNoise(shootPosition, 0.8f, config.NoiseRadius);
				}
			}
			catch (Exception ex)
			{
				Log.Warning($"Error disparando arma: {ex.Message}");
			}
		}

		private void StartReloading()
		{
			m_isReloading = true;
			m_reloadStartTime = m_subsystemTime.GameTime;
			m_lastReloadTime = m_subsystemTime.GameTime;

			// Sonido de recarga
			if (m_subsystemAudio != null)
			{
				m_subsystemAudio.PlaySound("Audio/Armas/reload", SoundVolume * 0.8f, 0f,
					m_componentCreature.ComponentCreatureModel.EyePosition, SoundRange, true);
			}
		}

		private FirearmConfig GetCurrentConfig()
		{
			if (m_currentWeaponIndex == -1 || m_firearmConfigs == null)
				return null;

			if (m_firearmConfigs.TryGetValue(m_currentWeaponIndex, out FirearmConfig config))
			{
				return config;
			}

			return null;
		}

		// Clase de configuración de arma
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