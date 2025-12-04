using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentItemsLauncherShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Componentes necesarios
		private ComponentCreature m_componentCreature;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentInventory m_componentInventory;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private ComponentCreatureModel m_componentModel;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemNoise m_subsystemNoise;

		// Configuración
		public float MaxDistance = 25f;
		public float ReloadTime = 0.55f;
		public float AimTime = 1f;
		public string FireSound = "Audio/Items/ItemLauncher/Item Cannon Fire";
		public float FireSoundDistance = 15f;
		public float Accuracy = 0.05f;
		public bool UseRecoil = true;
		public float BulletSpeed = 60f;
		public bool RequireCocking = true;

		// Niveles del ItemsLauncher
		public int SpeedLevel = 2; // 1-3
		public int RateLevel = 2;  // 1-15 (disparos por segundo)
		public int SpreadLevel = 2; // 1-3

		// Estado de animación
		private bool m_isAiming = false;
		private bool m_isFiring = false;
		private bool m_isReloading = false;
		private bool m_isCocking = false;
		private double m_animationStartTime;
		private double m_cockStartTime;
		private double m_fireTime;
		private int m_itemsLauncherSlot = -1;
		private Random m_random = new Random();
		private double m_nextFireTime;

		// UpdateOrder
		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros
			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 25f);
			ReloadTime = valuesDictionary.GetValue<float>("ReloadTime", 0.55f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 1f);
			FireSound = valuesDictionary.GetValue<string>("FireSound", "Audio/Items/ItemLauncher/Item Cannon Fire");
			FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 15f);
			Accuracy = valuesDictionary.GetValue<float>("Accuracy", 0.05f);
			UseRecoil = valuesDictionary.GetValue<bool>("UseRecoil", true);
			BulletSpeed = valuesDictionary.GetValue<float>("BulletSpeed", 60f);
			RequireCocking = valuesDictionary.GetValue<bool>("RequireCocking", true);
			SpeedLevel = valuesDictionary.GetValue<int>("SpeedLevel", 2);
			RateLevel = valuesDictionary.GetValue<int>("RateLevel", 2);
			SpreadLevel = valuesDictionary.GetValue<int>("SpreadLevel", 2);

			// Limitar valores
			SpeedLevel = MathUtils.Clamp(SpeedLevel, 1, 3);
			RateLevel = MathUtils.Clamp(RateLevel, 1, 15);
			SpreadLevel = MathUtils.Clamp(SpreadLevel, 1, 3);

			// Inicializar componentes
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
		}

		public void Update(float dt)
		{
			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			// Verificar objetivo
			if (m_componentChaseBehavior.Target == null)
			{
				ResetAnimations();
				return;
			}

			// Calcular distancia
			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				m_componentChaseBehavior.Target.ComponentBody.Position
			);

			// Lógica de ataque - MODIFICADO: Solo verifica distancia máxima como en ComponentMusketShooterBehavior
			if (distance <= MaxDistance)
			{
				// Si no está haciendo nada, empezar a apuntar
				if (!m_isAiming && !m_isFiring && !m_isReloading && !m_isCocking)
				{
					StartAiming();
				}
			}
			else
			{
				// Fuera de rango, resetear
				ResetAnimations();
				return;
			}

			// Aplicar animaciones
			if (m_isCocking)
			{
				ApplyCockingAnimation(dt);

				// Después de tiempo de amartillado, empezar a apuntar
				if (m_subsystemTime.GameTime - m_cockStartTime >= 0.5)
				{
					m_isCocking = false;
					m_isAiming = true;
					m_animationStartTime = m_subsystemTime.GameTime;
				}
			}
			else if (m_isAiming)
			{
				ApplyAimingAnimation(dt);

				// Disparar según la cadencia configurada
				if (m_subsystemTime.GameTime >= m_nextFireTime)
				{
					Fire();

					// Calcular próximo tiempo de disparo basado en RateLevel
					float fireRate = GetRateValue(RateLevel);
					m_nextFireTime = m_subsystemTime.GameTime + (1.0 / fireRate);
				}
			}
			else if (m_isFiring)
			{
				ApplyFiringAnimation(dt);

				// Después de corta animación de fuego, empezar recarga
				if (m_subsystemTime.GameTime - m_fireTime >= 0.2)
				{
					m_isFiring = false;
					StartReloading();
				}
			}
			else if (m_isReloading)
			{
				ApplyReloadingAnimation(dt);

				// Después de tiempo de recarga, volver a apuntar
				if (m_subsystemTime.GameTime - m_animationStartTime >= ReloadTime)
				{
					m_isReloading = false;
					StartAiming(); // Volver a apuntar para repetir ciclo
				}
			}
		}

		private void StartAiming()
		{
			m_isAiming = true;
			m_isFiring = false;
			m_isReloading = false;
			m_isCocking = false;
			m_animationStartTime = m_subsystemTime.GameTime;

			// Encontrar ItemsLauncher en inventario
			FindItemsLauncher();

			// Inicializar tiempo para primer disparo
			m_nextFireTime = m_subsystemTime.GameTime + 0.1;
		}

		private void FindItemsLauncher()
		{
			// Buscar ItemsLauncher por ID de bloque (301 es el ID del ItemsLauncher)
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0 && Terrain.ExtractContents(slotValue) == ItemsLauncherBlock.Index)
				{
					m_itemsLauncherSlot = i;
					m_componentInventory.ActiveSlotIndex = i;
					break;
				}
			}
		}

		private void ApplyCockingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE AMARTILLADO
				float cockProgress = (float)((m_subsystemTime.GameTime - m_cockStartTime) / 0.5);

				m_componentModel.AimHandAngleOrder = 1.2f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.1f + (0.04f * cockProgress),
					-0.08f,
					0.08f
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);

				// Mirar al objetivo
				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void ApplyAimingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE APUNTADO
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.08f, 0.08f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.6f, 0f, 0f);

				// Mirar al objetivo
				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void Fire()
		{
			m_isAiming = false;
			m_isFiring = true;
			m_isReloading = false;
			m_isCocking = false;
			m_fireTime = m_subsystemTime.GameTime;

			// Sonido de disparo
			if (!string.IsNullOrEmpty(FireSound))
			{
				m_subsystemAudio.PlaySound(FireSound, 0.7f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, FireSoundDistance, false);
			}

			// Disparar proyectil
			ShootItem();

			// Retroceso
			if (UseRecoil && m_componentChaseBehavior.Target != null)
			{
				Vector3 direction = Vector3.Normalize(
					m_componentChaseBehavior.Target.ComponentBody.Position -
					m_componentCreature.ComponentBody.Position
				);
				m_componentCreature.ComponentBody.ApplyImpulse(-direction * 2.5f);
			}
		}

		private void ApplyFiringAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE RETROCESO
				float timeSinceFire = (float)(m_subsystemTime.GameTime - m_fireTime);
				float recoilIntensity = MathUtils.Max(0f, 1f - (timeSinceFire * 5f));

				float recoilAngle = 0.25f * recoilIntensity;
				float recoilOffsetZ = 0.04f * recoilIntensity;

				m_componentModel.AimHandAngleOrder = 1.4f + (0.15f * recoilIntensity);
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.08f, 0.08f - recoilOffsetZ);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					-1.6f + recoilAngle,
					0f,
					0f
				);
			}
		}

		private void StartReloading()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = true;
			m_isCocking = false;
			m_animationStartTime = m_subsystemTime.GameTime;

			// NOTA: Se ha eliminado el sonido de recarga según la solicitud
		}

		private void ApplyReloadingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE RECARGA
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / ReloadTime);

				float aimHandAngle = MathUtils.Lerp(1.0f, 0.3f, reloadProgress);
				float itemOffsetZ = MathUtils.Lerp(0.08f, -0.08f, reloadProgress);
				float itemRotationX = MathUtils.Lerp(-1.6f, -1.1f, reloadProgress);

				m_componentModel.AimHandAngleOrder = aimHandAngle;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.1f,
					-0.08f,
					itemOffsetZ
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					itemRotationX,
					0f,
					0f
				);

				// No mirar al objetivo durante recarga
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ResetAnimations()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = false;
			m_isCocking = false;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ShootItem()
		{
			if (m_componentChaseBehavior.Target == null)
				return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;

				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				// Aplicar dispersión según configuración
				float spreadValue = GetSpreadValue(SpreadLevel);
				direction += new Vector3(
					m_random.Float(-spreadValue, spreadValue),
					m_random.Float(-spreadValue * 0.5f, spreadValue * 0.5f),
					m_random.Float(-spreadValue, spreadValue)
				);
				direction = Vector3.Normalize(direction);

				// USAR BALAS DE MOSQUETE (igual que en ComponentMusketShooterBehavior)
				int bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false, false);
				if (bulletBlockIndex > 0)
				{
					int bulletData = BulletBlock.SetBulletType(0, BulletBlock.BulletType.MusketBall);
					int bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, bulletData);

					float speedValue = GetSpeedValue(SpeedLevel);

					// Disparar la bala
					m_subsystemProjectiles.FireProjectile(
						bulletValue,
						firePosition,
						direction * speedValue,
						Vector3.Zero,
						m_componentCreature
					);

					// Partículas de humo (igual que en SubsystemMusketBlockBehavior)
					Vector3 smokePosition = firePosition + direction * 0.3f;
					if (m_subsystemParticles != null)
					{
						SubsystemTerrain subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
						if (subsystemTerrain != null)
						{
							m_subsystemParticles.AddParticleSystem(
								new GunSmokeParticleSystem(subsystemTerrain, smokePosition, direction),
								false
							);
						}
					}

					// Ruido (igual que en SubsystemMusketBlockBehavior)
					if (m_subsystemNoise != null)
					{
						m_subsystemNoise.MakeNoise(firePosition, 1f, 40f);
					}
				}
			}
			catch (Exception ex)
			{
				// Ignorar errores de disparo
			}
		}

		// Métodos para obtener valores según los niveles del ItemsLauncher
		private float GetSpeedValue(int level)
		{
			switch (level)
			{
				case 1: return 10f;
				case 2: return 35f;
				case 3: return 60f;
				default: return 35f;
			}
		}

		private float GetRateValue(int level)
		{
			// Valores de cadencia en disparos por segundo
			float[] rateValues = { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f, 14f, 15f };
			if (level >= 1 && level <= 15)
				return rateValues[level - 1];
			return 2f;
		}

		private float GetSpreadValue(int level)
		{
			switch (level)
			{
				case 1: return 0.01f;
				case 2: return 0.1f;
				case 3: return 0.5f;
				default: return 0.1f;
			}
		}
	}
}
