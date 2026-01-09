using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics; // Necesario para Texture2D
using Armas;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Armas
{
	public class ComponentFirearmsShooters : Component, IUpdateable
	{
		// Diccionario para mapear armas a sus configuraciones
		private static readonly Dictionary<int, FirearmConfig> FirearmConfigs = new Dictionary<int, FirearmConfig>();

		// PARÁMETROS CONFIGURABLES
		public float MaxShootingDistance = 25f;
		public float SpreadFactor = 0.05f;
		public float ReloadChance = 0.05f;
		public float MinReloadInterval = 5f;
		public float SoundVolume = 1f;
		public float SoundRange = 10f;
		public bool UseRandomReloads = true;
		public float TargetHeightOffset = 0.5f;
		public float ReloadTime = 1.0f;
		public float PistolAimTime = 0.5f; // Solo para pistolas como SWM500
		public float SniperAimTime = 1.0f; // Tiempo de apuntado para francotirador

		// Estado de animación
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
		private ComponentCreatureModel m_componentModel;
		private Game.Random m_random = new Game.Random();

		// Tiempos de disparo y recarga
		private double m_lastShootTime;
		private double m_lastReloadTime;
		private int m_currentWeaponIndex = -1;
		private int m_shotsSinceLastReload = 0;

		// Textura de partícula para recarga
		private Texture2D m_reloadParticleTexture;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// CARGAR PARÁMETROS DESDE XML
			MaxShootingDistance = valuesDictionary.GetValue<float>("MaxShootingDistance", 25f);
			SpreadFactor = valuesDictionary.GetValue<float>("SpreadFactor", 0.05f);
			ReloadChance = valuesDictionary.GetValue<float>("ReloadChance", 0.05f);
			MinReloadInterval = valuesDictionary.GetValue<float>("MinReloadInterval", 5f);
			SoundVolume = valuesDictionary.GetValue<float>("SoundVolume", 1f);
			SoundRange = valuesDictionary.GetValue<float>("SoundRange", 10f);
			UseRandomReloads = valuesDictionary.GetValue<bool>("UseRandomReloads", true);
			TargetHeightOffset = valuesDictionary.GetValue<float>("TargetHeightOffset", 0.5f);
			ReloadTime = valuesDictionary.GetValue<float>("ReloadTime", 1.0f);
			PistolAimTime = valuesDictionary.GetValue<float>("PistolAimTime", 0.5f);
			SniperAimTime = valuesDictionary.GetValue<float>("SniperAimTime", 1.0f);

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
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);

			// Cargar textura de partícula para recarga
			try
			{
				m_reloadParticleTexture = ContentManager.Get<Texture2D>("Textures/KillParticle", null);
			}
			catch (Exception ex)
			{
				Log.Warning($"No se pudo cargar la textura de partícula de recarga: {ex.Message}");
				m_reloadParticleTexture = null;
			}

			// Inicializar configuraciones de armas si es necesario
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
				// AK-47 - ARMA AUTOMÁTICA (dispara rápido sin apuntado)
				int akIndex = BlocksManager.GetBlockIndex(typeof(Armas.AKBlock), true, false);
				FirearmConfigs[akIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala),
					ShootSound = "Audio/Armas/ak 47 fuego",
					FireRate = 0.17,
					BulletSpeed = 280f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.01f, 0.01f, 0.05f),
					NoiseRadius = 40f,
					IsAutomatic = true // ¡ES AUTOMÁTICA!
				};

				// M4 - ARMA AUTOMÁTICA (dispara rápido sin apuntado)
				int m4Index = BlocksManager.GetBlockIndex(typeof(Armas.M4Block), true, false);
				FirearmConfigs[m4Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala2),
					ShootSound = "Audio/Armas/M6 fuego",
					FireRate = 0.15,
					BulletSpeed = 300f,
					MaxShotsBeforeReload = 22,
					ProjectilesPerShot = 3,
					SpreadVector = new Vector3(0.008f, 0.008f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true // ¡ES AUTOMÁTICA!
				};

				// Mac10 - ARMA AUTOMÁTICA (dispara rápido sin apuntado)
				int mac10Index = BlocksManager.GetBlockIndex(typeof(Armas.Mac10Block), true, false);
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
					IsAutomatic = true // ¡ES AUTOMÁTICA!
				};

				// SWM500 - PISTOLA (requiere tiempo de apuntado como antes)
				int swm500Index = BlocksManager.GetBlockIndex(typeof(Armas.SWM500Block), true, false);
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
					IsAutomatic = false // ¡NO ES AUTOMÁTICA! (pistola)
				};

				// G3 - FUSIL DE ASALTO SEMIAUTOMÁTICO/AUTOMÁTICO
				int g3Index = BlocksManager.GetBlockIndex(typeof(Armas.G3Block), true, false);
				FirearmConfigs[g3Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala), // Usar el mismo tipo de bala que AK-47
					ShootSound = "Audio/Armas/FX05",
					FireRate = 0.12,
					BulletSpeed = 290f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.009f, 0.009f, 0.04f),
					NoiseRadius = 40f,
					IsAutomatic = true // ¡ES AUTOMÁTICA!
				};

				// Izh43 - ESCOPETA DE DOS CAÑONES
				int izh43Index = BlocksManager.GetBlockIndex(typeof(Armas.Izh43Block), true, false);
				FirearmConfigs[izh43Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala), // Usar bala estándar o cambiar si hay una específica
					ShootSound = "Audio/Armas/shotgun fuego",
					FireRate = 1.0, // Recarga lenta para escopeta
					BulletSpeed = 280f,
					MaxShotsBeforeReload = 2, // Solo 2 disparos antes de recargar
					ProjectilesPerShot = 8, // Dispara múltiples proyectiles como escopeta
					SpreadVector = new Vector3(0.09f, 0.09f, 0.09f), // Dispersión amplia
					NoiseRadius = 45f,
					IsAutomatic = false // NO automática, es escopeta
				};

				// Minigun - AMETRALLADORA GIRATORIA
				int minigunIndex = BlocksManager.GetBlockIndex(typeof(Armas.MinigunBlock), true, false);
				FirearmConfigs[minigunIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala6), // Usar bala específica de minigun
					ShootSound = "Audio/Armas/Chaingun fuego",
					FireRate = 0.08, // Muy rápida
					BulletSpeed = 260f,
					MaxShotsBeforeReload = 100, // Gran capacidad
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.02f, 0.02f, 0.08f), // Dispersión mayor por alta cadencia
					NoiseRadius = 50f, // Muy ruidosa
					IsAutomatic = true // ¡ES AUTOMÁTICA!
				};

				// SPAS12 - ESCOPETA SEMIAUTOMÁTICA
				int spas12Index = BlocksManager.GetBlockIndex(typeof(Armas.SPAS12Block), true, false);
				FirearmConfigs[spas12Index] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala), // O bala específica para escopeta
					ShootSound = "Audio/Armas/SPAS 12 fuego",
					FireRate = 0.8, // Más rápida que Izh43 pero más lenta que armas automáticas
					BulletSpeed = 280f,
					MaxShotsBeforeReload = 8,
					ProjectilesPerShot = 8, // Múltiples proyectiles como escopeta
					SpreadVector = new Vector3(0.09f, 0.09f, 0.09f), // Dispersión amplia
					NoiseRadius = 40f,
					IsAutomatic = false // SEMIAUTOMÁTICA, no totalmente automática
				};

				// Uzi - SUBFUSIL AUTOMÁTICO
				int uziIndex = BlocksManager.GetBlockIndex(typeof(Armas.UziBlock), true, false);
				FirearmConfigs[uziIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala2), // Misma bala que M4
					ShootSound = "Audio/Armas/Uzi fuego",
					FireRate = 0.08, // Muy rápida
					BulletSpeed = 320f,
					MaxShotsBeforeReload = 30,
					ProjectilesPerShot = 2,
					SpreadVector = new Vector3(0.015f, 0.015f, 0.06f),
					NoiseRadius = 40f,
					IsAutomatic = true // ¡ES AUTOMÁTICA!
				};

				// SNIPER - FRANCOTIRADOR (Barret)
				int sniperIndex = BlocksManager.GetBlockIndex(typeof(Armas.SniperBlock), true, false);
				FirearmConfigs[sniperIndex] = new FirearmConfig
				{
					BulletBlockType = typeof(NuevaBala6), // Usar bala de alta potencia (sniper)
					ShootSound = "Audio/Armas/Sniper fuego",
					FireRate = 2.0, // Muy lento (recarga después de cada disparo)
					BulletSpeed = 450f, // Velocidad muy alta para precisión a larga distancia
					MaxShotsBeforeReload = 1, // Un solo disparo antes de recargar
					ProjectilesPerShot = 1,
					SpreadVector = new Vector3(0.001f, 0.001f, 0.001f), // Dispersión mínima (muy precisa)
					NoiseRadius = 80f, // Muy ruidoso
					IsAutomatic = false, // NO automática
					IsSniper = true // ¡NUEVO: identifica que es un francotirador!
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

			// Verificar si el NPC está vivo
			if (m_componentCreature.ComponentHealth.Health <= 0f)
			{
				ResetAnimations();
				return;
			}

			// Verificar si estamos recargando
			if (m_isReloading)
			{
				ApplyReloadingAnimation(dt);

				if (currentTime - m_animationStartTime >= ReloadTime)
				{
					m_isReloading = false;
					m_shotsSinceLastReload = 0;

					// Sonido de recarga completada
					m_subsystemAudio.PlaySound("Audio/Armas/reload", SoundVolume, 0f,
						m_componentCreature.ComponentCreatureModel.EyePosition, SoundRange, true);
				}
				return;
			}

			// Verificar si estamos disparando
			if (m_isFiring)
			{
				ApplyFiringAnimation(dt);

				float fireAnimationTime = IsCurrentWeaponAutomatic() ? 0.1f : 0.2f;
				// Animación más larga para francotirador
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

			// Verificar si hay objetivo
			if (m_componentChaseBehavior == null || m_componentChaseBehavior.Target == null)
			{
				ResetAnimations();
				m_currentWeaponIndex = -1;
				return;
			}

			// Calcular distancia al objetivo
			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				m_componentChaseBehavior.Target.ComponentBody.Position
			);

			// Para francotirador, aumentar la distancia máxima
			float maxDistance = MaxShootingDistance;
			if (IsCurrentWeaponSniper() && m_currentWeaponIndex != -1)
			{
				maxDistance = MaxShootingDistance * 3f; // Triplica la distancia para francotirador
			}

			// Si está fuera de rango, no disparar
			if (distance > maxDistance)
			{
				ResetAnimations();
				m_currentWeaponIndex = -1;
				return;
			}

			// Buscar un arma en el inventario
			FindWeaponInInventory();

			if (m_currentWeaponIndex == -1)
			{
				ResetAnimations();
				return;
			}

			FirearmConfig config = GetCurrentConfig();
			if (config == null)
				return;

			// LÓGICA DIFERENTE SEGÚN TIPO DE ARMA
			if (config.IsSniper)
			{
				UpdateSniperWeapon(currentTime);
			}
			else if (config.IsAutomatic)
			{
				UpdateAutomaticWeapon(currentTime);
			}
			else
			{
				UpdatePistolWeapon(currentTime);
			}
		}

		private void UpdateAutomaticWeapon(double currentTime)
		{
			// PARA ARMAS AUTOMÁTICAS (AK, M4, Mac10, G3, Minigun, Uzi)

			// Siempre apuntando mientras tiene objetivo
			if (!m_isAiming)
			{
				m_isAiming = true;
			}

			// Aplicar animación de apuntado
			ApplyAimingAnimation();

			// Disparar inmediatamente según cadencia
			if (currentTime - m_lastShootTime >= GetCurrentFireRate())
			{
				Fire();
				m_lastShootTime = currentTime;
				m_shotsSinceLastReload++;

				if (ShouldReload(currentTime))
				{
					StartReloading();
				}
			}
		}

		private void UpdatePistolWeapon(double currentTime)
		{
			// PARA PISTOLAS (SWM500) Y ESCOPETAS (Izh43, SPAS12) - CON TIEMPO DE APUNTado

			if (!m_isAiming)
			{
				// Empezar a apuntar
				StartAiming();
				return;
			}

			// Estamos apuntando
			ApplyAimingAnimation();

			// Verificar si ha pasado el tiempo de apuntado
			float aimTime = PistolAimTime;
			if (IsCurrentWeaponShotgun())
			{
				aimTime *= 0.8f; // Las escopetas apuntan un poco más rápido
			}

			if (currentTime - m_animationStartTime >= aimTime)
			{
				// Verificar cadencia de fuego
				if (currentTime - m_lastShootTime >= GetCurrentFireRate())
				{
					Fire();
					m_lastShootTime = currentTime;
					m_shotsSinceLastReload++;

					if (ShouldReload(currentTime))
					{
						StartReloading();
					}

					// Después de disparar, dejar de apuntar
					m_isAiming = false;
				}
			}
		}

		private void UpdateSniperWeapon(double currentTime)
		{
			// PARA FRANCOTIRADOR - REQUIERE MÁS TIEMPO DE APUNTADO Y MAYOR PRECISIÓN

			if (!m_isAiming)
			{
				// Empezar a apuntar
				StartAiming();
				return;
			}

			// Estamos apuntando
			ApplySniperAimingAnimation();

			// Verificar si ha pasado el tiempo de apuntado (más largo para sniper)
			if (currentTime - m_animationStartTime >= SniperAimTime)
			{
				// Verificar cadencia de fuego (muy lenta para sniper)
				if (currentTime - m_lastShootTime >= GetCurrentFireRate())
				{
					Fire();
					m_lastShootTime = currentTime;
					m_shotsSinceLastReload++;

					// El francotirador siempre recarga después de disparar (solo 1 bala)
					if (m_shotsSinceLastReload >= 1)
					{
						StartReloading();
					}

					// Después de disparar, dejar de apuntar
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

		private void ApplyAimingAnimation()
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE APUNTADO - BRAZOS ALTOS
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

				// Mirar al objetivo
				if (m_componentChaseBehavior != null && m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void ApplySniperAimingAnimation()
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN ESPECIAL PARA FRANCOTIRADOR - MÁS ESTABLE
				m_componentModel.AimHandAngleOrder = 1.2f; // Menos ángulo para mayor estabilidad
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.06f, 0.08f); // Posición más estable
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f); // Rotación más controlada

				// Mirar al objetivo con más precisión
				if (m_componentChaseBehavior != null && m_componentChaseBehavior.Target != null)
				{
					// Para sniper, apuntar al centro del cuerpo en lugar de a los ojos
					Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentBody.Position;
					targetPosition.Y += 0.5f; // Centro del cuerpo
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

				// Determinar fuerza del retroceso según tipo de arma (solo para animación visual)
				if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Armas.SWM500Block), true, false))
				{
					// Pistolas (SWM500)
					recoilFactor = (float)(1.8f - timeSinceFire * 3f);
				}
				else if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Armas.Izh43Block), true, false) ||
						m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Armas.SPAS12Block), true, false))
				{
					// Escopetas
					recoilFactor = (float)(2.0f - timeSinceFire * 2.5f);
				}
				else if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Armas.MinigunBlock), true, false))
				{
					// Minigun
					recoilFactor = (float)(1.3f - timeSinceFire * 5f);
				}
				else if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Armas.SniperBlock), true, false))
				{
					// Sniper
					recoilFactor = (float)(2.5f - timeSinceFire * 1.5f);
				}
				else
				{
					// Armas automáticas normales
					recoilFactor = (float)(1.5f - timeSinceFire * 8f);
				}

				recoilFactor = MathUtils.Max(recoilFactor, 1.0f);

				m_componentModel.AimHandAngleOrder = 1.4f * recoilFactor;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f - (0.05f * (1.5f - recoilFactor)));

				m_componentModel.InHandItemRotationOrder = new Vector3(
					-1.7f + (0.3f * (1.5f - recoilFactor)),
					0f,
					0f
				);
			}
		}

		private void ApplyReloadingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / ReloadTime);

				// Animación diferente según tipo de arma
				if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Armas.Izh43Block), true, false))
				{
					// Escopetas - recarga más lenta
					reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / (ReloadTime * 1.5f));
				}
				else if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Armas.SniperBlock), true, false))
				{
					// Sniper - recarga más lenta y cuidadosa
					reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / (ReloadTime * 2.0f));
				}

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

		private void ResetAnimations()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = false;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void FindWeaponInInventory()
		{
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

		private void Fire()
		{
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			if (m_currentWeaponIndex == -1)
				return;

			FirearmConfig config = GetCurrentConfig();
			if (config == null || m_componentChaseBehavior.Target == null)
				return;

			try
			{
				Vector3 shootPosition = m_componentCreature.ComponentCreatureModel.EyePosition +
					m_componentCreature.ComponentBody.Matrix.Right * 0.3f -
					m_componentCreature.ComponentBody.Matrix.Up * 0.2f;

				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;

				// Para sniper, apuntar al centro del cuerpo para mayor letalidad
				if (config.IsSniper)
				{
					targetPosition = m_componentChaseBehavior.Target.ComponentBody.Position;
					targetPosition.Y += 0.5f; // Centro del cuerpo
				}
				else
				{
					targetPosition.Y -= TargetHeightOffset;
				}

				Vector3 direction = Vector3.Normalize(targetPosition - shootPosition);

				Vector3 vector4 = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
				Vector3 v2 = Vector3.Normalize(Vector3.Cross(direction, vector4));

				for (int i = 0; i < config.ProjectilesPerShot; i++)
				{
					Vector3 v3 = m_random.Float(-config.SpreadVector.X, config.SpreadVector.X) * vector4 +
						m_random.Float(-config.SpreadVector.Y, config.SpreadVector.Y) * v2 +
						m_random.Float(-config.SpreadVector.Z, config.SpreadVector.Z) * direction;

					int bulletBlockIndex = BlocksManager.GetBlockIndex(config.BulletBlockType, true, false);
					int bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, 2);

					m_subsystemProjectiles.FireProjectile(
						bulletValue,
						shootPosition,
						config.BulletSpeed * (direction + v3),
						Vector3.Zero,
						m_componentCreature
					);
				}

				// Efectos
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

				float pitchVariation = m_random.Float(-0.1f, 0.1f);
				// Menor variación de tono para sniper (sonido más limpio)
				if (config.IsSniper)
				{
					pitchVariation = m_random.Float(-0.05f, 0.05f);
				}

				m_subsystemAudio.PlaySound(config.ShootSound, SoundVolume,
					pitchVariation,
					shootPosition, SoundRange, true);

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

			// Mayor probabilidad de recarga para armas con poca capacidad
			float adjustedReloadChance = ReloadChance;
			if (config != null)
			{
				if (config.MaxShotsBeforeReload <= 2) // Escopetas como Izh43
				{
					adjustedReloadChance *= 2.0f;
				}
				else if (config.IsSniper) // Sniper siempre recarga después de 1 disparo
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

			// Mostrar partícula al inicio de la recarga
			if (m_subsystemParticles != null && m_reloadParticleTexture != null)
			{
				try
				{
					Vector3 particlePosition = m_componentCreature.ComponentCreatureModel.EyePosition;

					// Usar un sistema de partículas simple que use la textura KillParticle
					// Si GunFireParticleSystem acepta textura personalizada, usarla
					// De lo contrario, usar el sistema estándar
					GunFireParticleSystem particleSystem = new GunFireParticleSystem(
						m_subsystemTerrain,
						particlePosition,
						Vector3.UnitY
					);

					// Intentar asignar la textura si es posible
					try
					{
						// Algunas implementaciones de ParticleSystem tienen propiedad Texture
						var textureProperty = particleSystem.GetType().GetProperty("Texture");
						if (textureProperty != null && textureProperty.CanWrite)
						{
							textureProperty.SetValue(particleSystem, m_reloadParticleTexture);
						}
					}
					catch
					{
						// Si no se puede asignar la textura, usar el sistema estándar
					}

					m_subsystemParticles.AddParticleSystem(particleSystem, false);
				}
				catch (Exception ex)
				{
					Log.Warning($"Error mostrando partícula de recarga: {ex.Message}");
				}
			}

			m_subsystemAudio.PlaySound("Audio/Armas/reload", SoundVolume * 0.8f, 0f,
				m_componentCreature.ComponentCreatureModel.EyePosition, SoundRange, true);
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
			if (m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Armas.Izh43Block), true, false) ||
				m_currentWeaponIndex == BlocksManager.GetBlockIndex(typeof(Armas.SPAS12Block), true, false))
			{
				return true;
			}
			return false;
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
			public bool IsAutomatic { get; set; } // ¡NUEVO: diferencia automáticas de pistolas!
			public bool IsSniper { get; set; } // ¡NUEVO: identifica francotiradores!
		}

		public int UpdateOrder => 1000;
	}
}
