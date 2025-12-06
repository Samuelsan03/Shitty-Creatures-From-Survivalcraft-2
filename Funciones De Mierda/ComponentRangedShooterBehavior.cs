using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentRangedShooterBehavior : ComponentBehavior, IUpdateable
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
		private Random m_random = new Random();

		// Tipos de armas soportadas
		private enum WeaponType
		{
			None,
			Bow,
			Crossbow,
			RepeatCrossbow,
			Musket,
			ItemsLauncher
		}

		// Configuración general
		public float MaxDistance = 25f;

		// Configuración específica por arma
		// Para arco
		public float BowDrawTime = 1.2f;
		public float BowAimTime = 0.5f;
		public string BowDrawSound = "Audio/BowDraw";
		public string BowFireSound = "Audio/Bow";
		public float BowAccuracy = 0.03f;
		public float BowArrowSpeed = 35f;
		public bool CycleArrowTypes = true;
		public bool ShowArrowWhenIdle = true;

		// Para ballesta normal
		public float CrossbowDrawTime = 1.5f;
		public float CrossbowAimTime = 0.5f;
		public float CrossbowReloadTime = 0.8f;
		public string CrossbowDrawSound = "Audio/CrossbowDraw";
		public string CrossbowFireSound = "Audio/Bow";
		public string CrossbowReloadSound = "Audio/Reload";
		public float CrossbowAccuracy = 0.02f;
		public float CrossbowBoltSpeed = 45f;
		public bool CycleBoltTypes = true;
		public bool ShowBoltWhenIdle = false;

		// Para ballesta repetidora
		public float RepeatCrossbowDrawTime = 2.5f;
		public float RepeatCrossbowAimTime = 0.5f;
		public float RepeatCrossbowTimeBetweenShots = 1.0f;
		public float RepeatCrossbowMaxInaccuracy = 0.03f;
		public string RepeatCrossbowDrawSound = "Audio/CrossbowDraw";
		public string RepeatCrossbowFireSound = "Audio/Bow";
		public string RepeatCrossbowReleaseSound = "Audio/CrossbowBoing";
		public float RepeatCrossbowBoltSpeed = 40f;
		public bool RepeatCrossbowUseRecoil = true;

		// Para mosquete
		public float MusketReloadTime = 0.55f;
		public float MusketAimTime = 1f;
		public float MusketCockTime = 0.5f;
		public string MusketFireSound = "Audio/MusketFire";
		public float MusketFireSoundDistance = 15f;
		public string MusketCockSound = "Audio/HammerCock";
		public string MusketReloadSound = "Audio/Reload";
		public float MusketAccuracy = 0.02f;
		public bool MusketUseRecoil = true;
		public float MusketBulletSpeed = 120f;
		public bool MusketRequireCocking = true;

		// Para ItemsLauncher
		public float ItemsLauncherReloadTime = 0.55f;
		public float ItemsLauncherAimTime = 1f;
		public string ItemsLauncherFireSound = "Audio/Items/ItemLauncher/Item Cannon Fire";
		public float ItemsLauncherFireSoundDistance = 15f;
		public float ItemsLauncherAccuracy = 0.05f;
		public bool ItemsLauncherUseRecoil = true;
		public float ItemsLauncherBulletSpeed = 60f;
		public bool ItemsLauncherRequireCocking = true;
		public int ItemsLauncherSpeedLevel = 2;
		public int ItemsLauncherRateLevel = 2;
		public int ItemsLauncherSpreadLevel = 2;

		// Estado general
		private WeaponType m_currentWeapon = WeaponType.None;
		private int m_weaponSlot = -1;
		private double m_nextFireTime;
		private bool m_initialized = false;

		// Estado específico para arco/ballesta
		private bool m_isAiming = false;
		private bool m_isDrawing = false;
		private bool m_isFiring = false;
		private bool m_isReloading = false;
		private bool m_isCocking = false;
		private double m_animationStartTime;
		private double m_drawStartTime;
		private double m_fireTime;
		private double m_cockStartTime;
		private float m_currentDraw = 0f;

		// Para ballesta repetidora
		private int m_arrowsInCurrentVolley = 0;
		private int m_arrowsFired = 0;

		// Tipos de proyectiles
		private ArrowBlock.ArrowType[] m_bowArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.CopperArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.FireArrow
		};

		private ArrowBlock.ArrowType[] m_crossbowBoltTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
		};

		private RepeatArrowBlock.ArrowType[] m_repeatArrowTypes = new RepeatArrowBlock.ArrowType[]
		{
			RepeatArrowBlock.ArrowType.CopperArrow,
			RepeatArrowBlock.ArrowType.IronArrow,
			RepeatArrowBlock.ArrowType.DiamondArrow,
			RepeatArrowBlock.ArrowType.ExplosiveArrow
		};

		private int m_currentProjectileTypeIndex = 0;
		private bool m_hasCycledForNextShot = false;

		// IDs de bloques
		private const int BowBlockIndex = 191;
		private const int ArrowBlockIndex = 192;
		private const int CrossbowBlockIndex = 200;
		private const int RepeatCrossbowIndex = 805;
		private const int RepeatArrowIndex = 804;
		private const int MusketBlockIndex = 212;
		private const int ItemsLauncherIndex = 301; // Asumiendo este ID

		// UpdateOrder
		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar configuración general
			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 25f);

			// Cargar configuración específica para cada arma
			BowDrawTime = valuesDictionary.GetValue<float>("BowDrawTime", 1.2f);
			BowAimTime = valuesDictionary.GetValue<float>("BowAimTime", 0.5f);
			BowDrawSound = valuesDictionary.GetValue<string>("BowDrawSound", "Audio/BowDraw");
			BowFireSound = valuesDictionary.GetValue<string>("BowFireSound", "Audio/Bow");
			BowAccuracy = valuesDictionary.GetValue<float>("BowAccuracy", 0.03f);
			BowArrowSpeed = valuesDictionary.GetValue<float>("BowArrowSpeed", 35f);
			CycleArrowTypes = valuesDictionary.GetValue<bool>("CycleArrowTypes", true);
			ShowArrowWhenIdle = valuesDictionary.GetValue<bool>("ShowArrowWhenIdle", true);

			CrossbowDrawTime = valuesDictionary.GetValue<float>("CrossbowDrawTime", 1.5f);
			CrossbowAimTime = valuesDictionary.GetValue<float>("CrossbowAimTime", 0.5f);
			CrossbowReloadTime = valuesDictionary.GetValue<float>("CrossbowReloadTime", 0.8f);
			CrossbowDrawSound = valuesDictionary.GetValue<string>("CrossbowDrawSound", "Audio/CrossbowDraw");
			CrossbowFireSound = valuesDictionary.GetValue<string>("CrossbowFireSound", "Audio/Bow");
			CrossbowReloadSound = valuesDictionary.GetValue<string>("CrossbowReloadSound", "Audio/Reload");
			CrossbowAccuracy = valuesDictionary.GetValue<float>("CrossbowAccuracy", 0.02f);
			CrossbowBoltSpeed = valuesDictionary.GetValue<float>("CrossbowBoltSpeed", 45f);
			CycleBoltTypes = valuesDictionary.GetValue<bool>("CycleBoltTypes", true);
			ShowBoltWhenIdle = valuesDictionary.GetValue<bool>("ShowBoltWhenIdle", false);

			RepeatCrossbowDrawTime = valuesDictionary.GetValue<float>("RepeatCrossbowDrawTime", 2.5f);
			RepeatCrossbowAimTime = valuesDictionary.GetValue<float>("RepeatCrossbowAimTime", 0.5f);
			RepeatCrossbowTimeBetweenShots = valuesDictionary.GetValue<float>("RepeatCrossbowTimeBetweenShots", 1.0f);
			RepeatCrossbowMaxInaccuracy = valuesDictionary.GetValue<float>("RepeatCrossbowMaxInaccuracy", 0.03f);
			RepeatCrossbowDrawSound = valuesDictionary.GetValue<string>("RepeatCrossbowDrawSound", "Audio/CrossbowDraw");
			RepeatCrossbowFireSound = valuesDictionary.GetValue<string>("RepeatCrossbowFireSound", "Audio/Bow");
			RepeatCrossbowReleaseSound = valuesDictionary.GetValue<string>("RepeatCrossbowReleaseSound", "Audio/CrossbowBoing");
			RepeatCrossbowBoltSpeed = valuesDictionary.GetValue<float>("RepeatCrossbowBoltSpeed", 40f);
			RepeatCrossbowUseRecoil = valuesDictionary.GetValue<bool>("RepeatCrossbowUseRecoil", true);

			MusketReloadTime = valuesDictionary.GetValue<float>("MusketReloadTime", 0.55f);
			MusketAimTime = valuesDictionary.GetValue<float>("MusketAimTime", 1f);
			MusketCockTime = valuesDictionary.GetValue<float>("MusketCockTime", 0.5f);
			MusketFireSound = valuesDictionary.GetValue<string>("MusketFireSound", "Audio/MusketFire");
			MusketFireSoundDistance = valuesDictionary.GetValue<float>("MusketFireSoundDistance", 15f);
			MusketCockSound = valuesDictionary.GetValue<string>("MusketCockSound", "Audio/HammerCock");
			MusketReloadSound = valuesDictionary.GetValue<string>("MusketReloadSound", "Audio/Reload");
			MusketAccuracy = valuesDictionary.GetValue<float>("MusketAccuracy", 0.02f);
			MusketUseRecoil = valuesDictionary.GetValue<bool>("MusketUseRecoil", true);
			MusketBulletSpeed = valuesDictionary.GetValue<float>("MusketBulletSpeed", 120f);
			MusketRequireCocking = valuesDictionary.GetValue<bool>("MusketRequireCocking", true);

			ItemsLauncherReloadTime = valuesDictionary.GetValue<float>("ItemsLauncherReloadTime", 0.55f);
			ItemsLauncherAimTime = valuesDictionary.GetValue<float>("ItemsLauncherAimTime", 1f);
			ItemsLauncherFireSound = valuesDictionary.GetValue<string>("ItemsLauncherFireSound", "Audio/Items/ItemLauncher/Item Cannon Fire");
			ItemsLauncherFireSoundDistance = valuesDictionary.GetValue<float>("ItemsLauncherFireSoundDistance", 15f);
			ItemsLauncherAccuracy = valuesDictionary.GetValue<float>("ItemsLauncherAccuracy", 0.05f);
			ItemsLauncherUseRecoil = valuesDictionary.GetValue<bool>("ItemsLauncherUseRecoil", true);
			ItemsLauncherBulletSpeed = valuesDictionary.GetValue<float>("ItemsLauncherBulletSpeed", 60f);
			ItemsLauncherRequireCocking = valuesDictionary.GetValue<bool>("ItemsLauncherRequireCocking", true);
			ItemsLauncherSpeedLevel = valuesDictionary.GetValue<int>("ItemsLauncherSpeedLevel", 2);
			ItemsLauncherRateLevel = valuesDictionary.GetValue<int>("ItemsLauncherRateLevel", 2);
			ItemsLauncherSpreadLevel = valuesDictionary.GetValue<int>("ItemsLauncherSpreadLevel", 2);

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

		public override void OnEntityAdded()
		{
			base.OnEntityAdded();

			// Inicializar con tipo de proyectil aleatorio
			m_currentProjectileTypeIndex = m_random.Int(0, m_bowArrowTypes.Length);
			m_initialized = true;

			// Buscar arma inicial
			FindWeapon();

			// Mostrar flecha/virote inicialmente si está configurado
			if (m_currentWeapon == WeaponType.Bow && ShowArrowWhenIdle && m_weaponSlot >= 0)
			{
				SetWeaponWithProjectile(0, false);
			}
			else if (m_currentWeapon == WeaponType.Crossbow && ShowBoltWhenIdle && m_weaponSlot >= 0)
			{
				SetWeaponWithProjectile(0, false);
			}
		}

		public void Update(float dt)
		{
			if (!m_initialized || m_componentCreature.ComponentHealth.Health <= 0f)
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

			// Lógica de ataque - Solo verifica distancia máxima
			if (distance <= MaxDistance)
			{
				// Asegurarse de que tenemos un arma
				if (m_currentWeapon == WeaponType.None)
				{
					FindWeapon();
				}

				if (m_currentWeapon != WeaponType.None)
				{
					ProcessAttackState();
				}
			}
			else
			{
				// Fuera de rango, resetear
				ResetAnimations();
				return;
			}
		}

		private void ProcessAttackState()
		{
			switch (m_currentWeapon)
			{
				case WeaponType.Bow:
					ProcessBowAttack();
					break;
				case WeaponType.Crossbow:
					ProcessCrossbowAttack();
					break;
				case WeaponType.RepeatCrossbow:
					ProcessRepeatCrossbowAttack();
					break;
				case WeaponType.Musket:
					ProcessMusketAttack();
					break;
				case WeaponType.ItemsLauncher:
					ProcessItemsLauncherAttack();
					break;
			}
		}

		#region Métodos para arco

		private void ProcessBowAttack()
		{
			if (!m_isAiming && !m_isDrawing && !m_isFiring)
			{
				StartBowAiming();
			}

			if (m_isAiming)
			{
				ApplyBowAimingAnimation();
				if (m_subsystemTime.GameTime - m_animationStartTime >= BowAimTime)
				{
					m_isAiming = false;
					StartBowDrawing();
				}
			}
			else if (m_isDrawing)
			{
				ApplyBowDrawingAnimation();
				m_currentDraw = MathUtils.Clamp((float)((m_subsystemTime.GameTime - m_drawStartTime) / BowDrawTime), 0f, 1f);
				SetWeaponWithProjectile((int)(m_currentDraw * 15f), true);

				if (m_subsystemTime.GameTime - m_drawStartTime >= BowDrawTime)
				{
					FireBow();
				}
			}
			else if (m_isFiring)
			{
				ApplyBowFiringAnimation();
				if (m_subsystemTime.GameTime - m_fireTime >= 0.2)
				{
					m_isFiring = false;
					ClearProjectileFromWeapon();

					if (CycleArrowTypes && m_bowArrowTypes.Length > 1)
					{
						m_currentProjectileTypeIndex = (m_currentProjectileTypeIndex + 1) % m_bowArrowTypes.Length;
					}

					if (m_subsystemTime.GameTime - m_fireTime >= 0.8)
					{
						StartBowAiming();
					}
				}
			}
		}

		private void StartBowAiming()
		{
			m_isAiming = true;
			m_isDrawing = false;
			m_isFiring = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;
			SetWeaponWithProjectile(0, true);
		}

		private void StartBowDrawing()
		{
			m_isAiming = false;
			m_isDrawing = true;
			m_isFiring = false;
			m_drawStartTime = m_subsystemTime.GameTime;

			if (!string.IsNullOrEmpty(BowDrawSound))
			{
				m_subsystemAudio.PlaySound(BowDrawSound, 0.5f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 3f, false);
			}
		}

		private void FireBow()
		{
			m_isDrawing = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			ShootBowArrow();

			if (!string.IsNullOrEmpty(BowFireSound))
			{
				m_subsystemAudio.PlaySound(BowFireSound, 0.8f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 15f, false);
			}
		}

		private void ShootBowArrow()
		{
			if (m_componentChaseBehavior.Target == null)
				return;

			try
			{
				ArrowBlock.ArrowType arrowType = m_bowArrowTypes[m_currentProjectileTypeIndex];
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				firePosition.Y -= 0.1f;

				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				float currentAccuracy = BowAccuracy * (1.5f - m_currentDraw);
				direction += new Vector3(
					m_random.Float(-currentAccuracy, currentAccuracy),
					m_random.Float(-currentAccuracy * 0.5f, currentAccuracy * 0.5f),
					m_random.Float(-currentAccuracy, currentAccuracy)
				);
				direction = Vector3.Normalize(direction);

				float speedMultiplier = 0.5f + (m_currentDraw * 1.5f);
				float currentSpeed = BowArrowSpeed * speedMultiplier;

				int arrowData = ArrowBlock.SetArrowType(0, arrowType);
				int arrowValue = Terrain.MakeBlockValue(ArrowBlockIndex, 0, arrowData);

				var projectile = m_subsystemProjectiles.FireProjectile(
					arrowValue,
					firePosition,
					direction * currentSpeed,
					Vector3.Zero,
					m_componentCreature
				);

				if (arrowType == ArrowBlock.ArrowType.FireArrow && projectile != null)
				{
					m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero,
						new SmokeTrailParticleSystem(15, 0.5f, float.MaxValue, Color.White));
					projectile.IsIncendiary = true;
				}
			}
			catch
			{
				// Ignorar errores
			}
		}

		#endregion

		#region Métodos para ballesta normal

		private void ProcessCrossbowAttack()
		{
			if (!m_isAiming && !m_isDrawing && !m_isFiring && !m_isReloading)
			{
				StartCrossbowAiming();
			}

			if (m_isAiming)
			{
				ApplyCrossbowAimingAnimation();
				if (m_subsystemTime.GameTime - m_animationStartTime >= CrossbowAimTime)
				{
					m_isAiming = false;
					StartCrossbowDrawing();
				}
			}
			else if (m_isDrawing)
			{
				ApplyCrossbowDrawingAnimation();
				m_currentDraw = MathUtils.Clamp((float)((m_subsystemTime.GameTime - m_drawStartTime) / CrossbowDrawTime), 0f, 1f);
				SetWeaponWithProjectile((int)(m_currentDraw * 15f), false);

				if (m_subsystemTime.GameTime - m_drawStartTime >= CrossbowDrawTime)
				{
					LoadCrossbowBolt();
				}
			}
			else if (m_isReloading)
			{
				ApplyCrossbowReloadingAnimation();
				if (m_subsystemTime.GameTime - m_animationStartTime >= 0.3f)
				{
					m_isReloading = false;
					FireCrossbow();
				}
			}
			else if (m_isFiring)
			{
				ApplyCrossbowFiringAnimation();
				if (m_subsystemTime.GameTime - m_fireTime >= 0.2f)
				{
					m_isFiring = false;
					ClearProjectileFromWeapon();

					if (CycleBoltTypes && m_crossbowBoltTypes.Length > 1)
					{
						m_currentProjectileTypeIndex = (m_currentProjectileTypeIndex + 1) % m_crossbowBoltTypes.Length;
						m_hasCycledForNextShot = true;
					}

					if (m_subsystemTime.GameTime - m_fireTime >= 0.8f)
					{
						StartCrossbowAiming();
					}
				}
			}
		}

		private void StartCrossbowAiming()
		{
			m_isAiming = true;
			m_isDrawing = false;
			m_isFiring = false;
			m_isReloading = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;
			m_hasCycledForNextShot = false;
			SetWeaponWithProjectile(0, false);
		}

		private void StartCrossbowDrawing()
		{
			m_isAiming = false;
			m_isDrawing = true;
			m_isFiring = false;
			m_isReloading = false;
			m_drawStartTime = m_subsystemTime.GameTime;

			if (!string.IsNullOrEmpty(CrossbowDrawSound))
			{
				m_subsystemAudio.PlaySound(CrossbowDrawSound, 0.5f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 3f, false);
			}
		}

		private void LoadCrossbowBolt()
		{
			m_isDrawing = false;
			m_isReloading = true;
			m_animationStartTime = m_subsystemTime.GameTime;
			SetWeaponWithProjectile(15, true);

			if (!string.IsNullOrEmpty(CrossbowReloadSound))
			{
				m_subsystemAudio.PlaySound(CrossbowReloadSound, 1f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 3f, false);
			}
		}

		private void FireCrossbow()
		{
			m_isReloading = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			ShootCrossbowBolt();

			if (!string.IsNullOrEmpty(CrossbowFireSound))
			{
				m_subsystemAudio.PlaySound(CrossbowFireSound, 0.8f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 15f, false);
			}

			if (m_componentChaseBehavior.Target != null)
			{
				Vector3 direction = Vector3.Normalize(
					m_componentChaseBehavior.Target.ComponentBody.Position -
					m_componentCreature.ComponentBody.Position
				);
				m_componentCreature.ComponentBody.ApplyImpulse(-direction * 1.5f);
			}
		}

		private void ShootCrossbowBolt()
		{
			if (m_componentChaseBehavior.Target == null)
				return;

			try
			{
				int indexToUse = m_currentProjectileTypeIndex;
				if (m_hasCycledForNextShot && CycleBoltTypes && m_crossbowBoltTypes.Length > 1)
				{
					indexToUse = (m_currentProjectileTypeIndex - 1 + m_crossbowBoltTypes.Length) % m_crossbowBoltTypes.Length;
				}

				if (indexToUse < 0 || indexToUse >= m_crossbowBoltTypes.Length)
				{
					indexToUse = 0;
				}

				ArrowBlock.ArrowType boltType = m_crossbowBoltTypes[indexToUse];
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				firePosition.Y -= 0.1f;

				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				direction += new Vector3(
					m_random.Float(-CrossbowAccuracy, CrossbowAccuracy),
					m_random.Float(-CrossbowAccuracy * 0.5f, CrossbowAccuracy * 0.5f),
					m_random.Float(-CrossbowAccuracy, CrossbowAccuracy)
				);
				direction = Vector3.Normalize(direction);

				float speed = CrossbowBoltSpeed * (0.8f + (m_currentDraw * 0.4f));
				int boltData = ArrowBlock.SetArrowType(0, boltType);
				int boltValue = Terrain.MakeBlockValue(ArrowBlockIndex, 0, boltData);

				var projectile = m_subsystemProjectiles.FireProjectile(
					boltValue,
					firePosition,
					direction * speed,
					Vector3.Zero,
					m_componentCreature
				);

				if (boltType == ArrowBlock.ArrowType.ExplosiveBolt && projectile != null)
				{
					projectile.IsIncendiary = false;
				}

				if (m_subsystemNoise != null)
				{
					m_subsystemNoise.MakeNoise(firePosition, 0.5f, 20f);
				}
			}
			catch
			{
				// Ignorar errores
			}
		}

		#endregion

		#region Métodos para ballesta repetidora

		private void ProcessRepeatCrossbowAttack()
		{
			if (!m_isAiming && !m_isDrawing && !m_isFiring)
			{
				StartRepeatCrossbowAiming();
			}

			if (m_isAiming)
			{
				ApplyRepeatCrossbowAimingAnimation();
				if (m_subsystemTime.GameTime - m_animationStartTime >= RepeatCrossbowAimTime)
				{
					m_isAiming = false;
					StartRepeatCrossbowDrawing();
				}
			}
			else if (m_isDrawing)
			{
				ApplyRepeatCrossbowDrawingAnimation();
				m_currentDraw = MathUtils.Clamp((float)((m_subsystemTime.GameTime - m_drawStartTime) / RepeatCrossbowDrawTime), 0f, 1f);
				SetWeaponWithProjectile((int)(m_currentDraw * 15f), false);

				if (m_subsystemTime.GameTime - m_drawStartTime >= RepeatCrossbowDrawTime)
				{
					StartRepeatCrossbowFiring();
				}
			}
			else if (m_isFiring)
			{
				ApplyRepeatCrossbowFiringAnimation();

				if (m_arrowsInCurrentVolley == 0)
				{
					m_arrowsInCurrentVolley = m_random.Int(1, 9); // 1-8 flechas
					m_arrowsFired = 0;
					m_currentProjectileTypeIndex = m_random.Int(0, m_repeatArrowTypes.Length);
				}

				if (m_subsystemTime.GameTime - m_fireTime >= RepeatCrossbowTimeBetweenShots / m_arrowsInCurrentVolley)
				{
					FireRepeatCrossbowArrow();
					m_arrowsFired++;

					if (m_arrowsFired >= m_arrowsInCurrentVolley)
					{
						m_arrowsInCurrentVolley = 0;
						m_isFiring = false;
						SetWeaponWithProjectile(0, false);
						StartRepeatCrossbowAiming();
					}
					else
					{
						m_fireTime = m_subsystemTime.GameTime;
					}
				}
			}
		}

		private void StartRepeatCrossbowAiming()
		{
			m_isAiming = true;
			m_isDrawing = false;
			m_isFiring = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;
			SetWeaponWithProjectile(0, false);
		}

		private void StartRepeatCrossbowDrawing()
		{
			m_isAiming = false;
			m_isDrawing = true;
			m_isFiring = false;
			m_drawStartTime = m_subsystemTime.GameTime;

			if (!string.IsNullOrEmpty(RepeatCrossbowDrawSound))
			{
				m_subsystemAudio.PlaySound(RepeatCrossbowDrawSound, 0.5f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 3f, false);
			}
		}

		private void StartRepeatCrossbowFiring()
		{
			m_isDrawing = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;
			SetWeaponWithProjectile(15, true);
		}

		private void FireRepeatCrossbowArrow()
		{
			if (m_componentChaseBehavior.Target == null)
				return;

			try
			{
				RepeatArrowBlock.ArrowType arrowType = m_repeatArrowTypes[m_currentProjectileTypeIndex];
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				firePosition.Y -= 0.1f;

				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				float inaccuracyFactor = MathUtils.Lerp(0.5f, 1.0f,
					Vector3.Distance(firePosition, targetPosition) / MaxDistance);

				direction += new Vector3(
					m_random.Float(-RepeatCrossbowMaxInaccuracy, RepeatCrossbowMaxInaccuracy) * inaccuracyFactor,
					m_random.Float(-RepeatCrossbowMaxInaccuracy * 0.5f, RepeatCrossbowMaxInaccuracy * 0.5f) * inaccuracyFactor,
					m_random.Float(-RepeatCrossbowMaxInaccuracy, RepeatCrossbowMaxInaccuracy) * inaccuracyFactor
				);
				direction = Vector3.Normalize(direction);

				int arrowData = RepeatArrowBlock.SetArrowType(0, arrowType);
				int arrowValue = Terrain.MakeBlockValue(RepeatArrowIndex, 0, arrowData);

				var projectile = m_subsystemProjectiles.FireProjectile(
					arrowValue,
					firePosition,
					direction * RepeatCrossbowBoltSpeed,
					Vector3.Zero,
					m_componentCreature
				);

				if (arrowType == RepeatArrowBlock.ArrowType.ExplosiveArrow && projectile != null)
				{
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				}

				if (!string.IsNullOrEmpty(RepeatCrossbowFireSound))
				{
					m_subsystemAudio.PlaySound(RepeatCrossbowFireSound, 1f, m_random.Float(-0.1f, 0.1f),
						m_componentCreature.ComponentBody.Position, 15f, false);
				}

				if (m_subsystemNoise != null)
				{
					m_subsystemNoise.MakeNoise(firePosition, 0.5f, 20f);
				}

				if (RepeatCrossbowUseRecoil)
				{
					m_componentCreature.ComponentBody.ApplyImpulse(-direction * 1.0f);
				}
			}
			catch
			{
				// Ignorar errores
			}
		}

		#endregion

		#region Métodos para mosquete

		private void ProcessMusketAttack()
		{
			if (!m_isAiming && !m_isFiring && !m_isReloading && !m_isCocking)
			{
				StartMusketAiming();
			}

			if (m_isCocking)
			{
				ApplyMusketCockingAnimation();
				if (m_subsystemTime.GameTime - m_cockStartTime >= MusketCockTime)
				{
					m_isCocking = false;
					m_isAiming = true;
					m_animationStartTime = m_subsystemTime.GameTime;
				}
			}
			else if (m_isAiming)
			{
				ApplyMusketAimingAnimation();
				if (m_subsystemTime.GameTime - m_animationStartTime >= MusketAimTime)
				{
					FireMusket();
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
				if (m_subsystemTime.GameTime - m_animationStartTime >= MusketReloadTime)
				{
					m_isReloading = false;
					StartMusketAiming();
				}
			}
		}

		private void StartMusketCocking()
		{
			m_isCocking = true;
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = false;
			m_cockStartTime = m_subsystemTime.GameTime;

			if (!string.IsNullOrEmpty(MusketCockSound))
			{
				m_subsystemAudio.PlaySound(MusketCockSound, 1f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 3f, false);
			}
		}

		private void StartMusketAiming()
		{
			m_isAiming = true;
			m_isFiring = false;
			m_isReloading = false;
			m_isCocking = false;
			m_animationStartTime = m_subsystemTime.GameTime;

			if (MusketRequireCocking)
			{
				StartMusketCocking();
			}
		}

		private void FireMusket()
		{
			m_isAiming = false;
			m_isFiring = true;
			m_isReloading = false;
			m_isCocking = false;
			m_fireTime = m_subsystemTime.GameTime;

			if (!string.IsNullOrEmpty(MusketFireSound))
			{
				m_subsystemAudio.PlaySound(MusketFireSound, 1f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, MusketFireSoundDistance, false);
			}

			ShootMusketBullet();

			if (MusketUseRecoil && m_componentChaseBehavior.Target != null)
			{
				Vector3 direction = Vector3.Normalize(
					m_componentChaseBehavior.Target.ComponentBody.Position -
					m_componentCreature.ComponentBody.Position
				);
				m_componentCreature.ComponentBody.ApplyImpulse(-direction * 3f);
			}
		}

		private void StartMusketReloading()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = true;
			m_isCocking = false;
			m_animationStartTime = m_subsystemTime.GameTime;

			if (!string.IsNullOrEmpty(MusketReloadSound))
			{
				m_subsystemAudio.PlaySound(MusketReloadSound, 1.5f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 5f, false);
			}
		}

		private void ShootMusketBullet()
		{
			if (m_componentChaseBehavior.Target == null)
				return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;

				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);
				direction += new Vector3(
					m_random.Float(-MusketAccuracy, MusketAccuracy),
					m_random.Float(-MusketAccuracy * 0.5f, MusketAccuracy * 0.5f),
					m_random.Float(-MusketAccuracy, MusketAccuracy)
				);
				direction = Vector3.Normalize(direction);

				int bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false, false);
				if (bulletBlockIndex > 0)
				{
					int bulletData = BulletBlock.SetBulletType(0, BulletBlock.BulletType.MusketBall);
					int bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, bulletData);

					m_subsystemProjectiles.FireProjectile(
						bulletValue,
						firePosition,
						direction * MusketBulletSpeed,
						Vector3.Zero,
						m_componentCreature
					);

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

					if (m_subsystemNoise != null)
					{
						m_subsystemNoise.MakeNoise(firePosition, 1f, 40f);
					}
				}
			}
			catch
			{
				// Ignorar errores
			}
		}

		#endregion

		#region Métodos para ItemsLauncher

		private void ProcessItemsLauncherAttack()
		{
			if (!m_isAiming && !m_isFiring && !m_isReloading && !m_isCocking)
			{
				StartItemsLauncherAiming();
			}

			if (m_isCocking)
			{
				ApplyItemsLauncherCockingAnimation();
				if (m_subsystemTime.GameTime - m_cockStartTime >= 0.5)
				{
					m_isCocking = false;
					m_isAiming = true;
					m_animationStartTime = m_subsystemTime.GameTime;
				}
			}
			else if (m_isAiming)
			{
				ApplyItemsLauncherAimingAnimation();
				if (m_subsystemTime.GameTime >= m_nextFireTime)
				{
					FireItemsLauncher();
					float fireRate = GetItemsLauncherRateValue(ItemsLauncherRateLevel);
					m_nextFireTime = m_subsystemTime.GameTime + (1.0 / fireRate);
				}
			}
			else if (m_isFiring)
			{
				ApplyItemsLauncherFiringAnimation();
				if (m_subsystemTime.GameTime - m_fireTime >= 0.2)
				{
					m_isFiring = false;
					StartItemsLauncherReloading();
				}
			}
			else if (m_isReloading)
			{
				ApplyItemsLauncherReloadingAnimation();
				if (m_subsystemTime.GameTime - m_animationStartTime >= ItemsLauncherReloadTime)
				{
					m_isReloading = false;
					StartItemsLauncherAiming();
				}
			}
		}

		private void StartItemsLauncherAiming()
		{
			m_isAiming = true;
			m_isFiring = false;
			m_isReloading = false;
			m_isCocking = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_nextFireTime = m_subsystemTime.GameTime + 0.1;
		}

		private void FireItemsLauncher()
		{
			m_isAiming = false;
			m_isFiring = true;
			m_isReloading = false;
			m_isCocking = false;
			m_fireTime = m_subsystemTime.GameTime;

			if (!string.IsNullOrEmpty(ItemsLauncherFireSound))
			{
				m_subsystemAudio.PlaySound(ItemsLauncherFireSound, 0.7f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, ItemsLauncherFireSoundDistance, false);
			}

			ShootItemsLauncherProjectile();

			if (ItemsLauncherUseRecoil && m_componentChaseBehavior.Target != null)
			{
				Vector3 direction = Vector3.Normalize(
					m_componentChaseBehavior.Target.ComponentBody.Position -
					m_componentCreature.ComponentBody.Position
				);
				m_componentCreature.ComponentBody.ApplyImpulse(-direction * 2.5f);
			}
		}

		private void StartItemsLauncherReloading()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = true;
			m_isCocking = false;
			m_animationStartTime = m_subsystemTime.GameTime;
		}

		private void ShootItemsLauncherProjectile()
		{
			if (m_componentChaseBehavior.Target == null)
				return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;

				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);
				float spreadValue = GetItemsLauncherSpreadValue(ItemsLauncherSpreadLevel);
				direction += new Vector3(
					m_random.Float(-spreadValue, spreadValue),
					m_random.Float(-spreadValue * 0.5f, spreadValue * 0.5f),
					m_random.Float(-spreadValue, spreadValue)
				);
				direction = Vector3.Normalize(direction);

				int bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false, false);
				if (bulletBlockIndex > 0)
				{
					int bulletData = BulletBlock.SetBulletType(0, BulletBlock.BulletType.MusketBall);
					int bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, bulletData);

					float speedValue = GetItemsLauncherSpeedValue(ItemsLauncherSpeedLevel);

					m_subsystemProjectiles.FireProjectile(
						bulletValue,
						firePosition,
						direction * speedValue,
						Vector3.Zero,
						m_componentCreature
					);

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

					if (m_subsystemNoise != null)
					{
						m_subsystemNoise.MakeNoise(firePosition, 1f, 40f);
					}
				}
			}
			catch
			{
				// Ignorar errores
			}
		}

		private float GetItemsLauncherSpeedValue(int level)
		{
			switch (level)
			{
				case 1: return 10f;
				case 2: return 35f;
				case 3: return 60f;
				default: return 35f;
			}
		}

		private float GetItemsLauncherRateValue(int level)
		{
			float[] rateValues = { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f, 14f, 15f };
			if (level >= 1 && level <= 15)
				return rateValues[level - 1];
			return 2f;
		}

		private float GetItemsLauncherSpreadValue(int level)
		{
			switch (level)
			{
				case 1: return 0.01f;
				case 2: return 0.1f;
				case 3: return 0.5f;
				default: return 0.1f;
			}
		}

		#endregion

		#region Métodos comunes

		private void FindWeapon()
		{
			m_currentWeapon = WeaponType.None;
			m_weaponSlot = -1;

			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue == 0) continue;

				int blockIndex = Terrain.ExtractContents(slotValue);

				if (blockIndex == BowBlockIndex)
				{
					m_currentWeapon = WeaponType.Bow;
					m_weaponSlot = i;
					break;
				}
				else if (blockIndex == CrossbowBlockIndex)
				{
					m_currentWeapon = WeaponType.Crossbow;
					m_weaponSlot = i;
					break;
				}
				else if (blockIndex == RepeatCrossbowIndex)
				{
					m_currentWeapon = WeaponType.RepeatCrossbow;
					m_weaponSlot = i;
					break;
				}
				else if (blockIndex == MusketBlockIndex)
				{
					m_currentWeapon = WeaponType.Musket;
					m_weaponSlot = i;
					break;
				}
				else if (blockIndex == ItemsLauncherIndex)
				{
					m_currentWeapon = WeaponType.ItemsLauncher;
					m_weaponSlot = i;
					break;
				}
			}

			if (m_weaponSlot >= 0)
			{
				m_componentInventory.ActiveSlotIndex = m_weaponSlot;
			}
		}

		private void SetWeaponWithProjectile(int drawValue, bool hasProjectile)
		{
			if (m_weaponSlot < 0 || m_currentWeapon == WeaponType.None)
				return;

			try
			{
				int currentValue = m_componentInventory.GetSlotValue(m_weaponSlot);
				if (currentValue == 0) return;

				int currentData = Terrain.ExtractData(currentValue);
				int newData = currentData;

				switch (m_currentWeapon)
				{
					case WeaponType.Bow:
						ArrowBlock.ArrowType? arrowType = null;
						if (hasProjectile && m_bowArrowTypes.Length > 0)
						{
							arrowType = m_bowArrowTypes[m_currentProjectileTypeIndex];
						}
						newData = BowBlock.SetArrowType(currentData, arrowType);
						newData = BowBlock.SetDraw(newData, MathUtils.Clamp(drawValue, 0, 15));
						break;

					case WeaponType.Crossbow:
						ArrowBlock.ArrowType? boltType = null;
						if (hasProjectile && m_crossbowBoltTypes.Length > 0)
						{
							int indexToUse = m_currentProjectileTypeIndex;
							if (m_hasCycledForNextShot && CycleBoltTypes && m_crossbowBoltTypes.Length > 1)
							{
								indexToUse = (m_currentProjectileTypeIndex - 1 + m_crossbowBoltTypes.Length) % m_crossbowBoltTypes.Length;
							}
							boltType = m_crossbowBoltTypes[indexToUse];
						}
						newData = CrossbowBlock.SetDraw(currentData, MathUtils.Clamp(drawValue, 0, 15));
						newData = CrossbowBlock.SetArrowType(newData, boltType);
						break;

					case WeaponType.RepeatCrossbow:
						RepeatArrowBlock.ArrowType? repeatArrowType = null;
						if (hasProjectile && m_repeatArrowTypes.Length > 0)
						{
							repeatArrowType = m_repeatArrowTypes[m_currentProjectileTypeIndex];
						}
						newData = RepeatCrossbowBlock.SetDraw(currentData, MathUtils.Clamp(drawValue, 0, 15));
						newData = RepeatCrossbowBlock.SetArrowType(newData, repeatArrowType);
						break;
				}

				int newValue = Terrain.ReplaceData(currentValue, newData);
				m_componentInventory.RemoveSlotItems(m_weaponSlot, 1);
				m_componentInventory.AddSlotItems(m_weaponSlot, newValue, 1);
			}
			catch
			{
				// Ignorar errores
			}
		}

		private void ClearProjectileFromWeapon()
		{
			SetWeaponWithProjectile(0, false);
		}

		private void ResetAnimations()
		{
			m_isAiming = false;
			m_isDrawing = false;
			m_isFiring = false;
			m_isReloading = false;
			m_isCocking = false;
			m_currentDraw = 0f;
			m_hasCycledForNextShot = false;
			m_arrowsInCurrentVolley = 0;
			m_arrowsFired = 0;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}

			if (m_currentWeapon == WeaponType.Bow && ShowArrowWhenIdle && m_weaponSlot >= 0)
			{
				SetWeaponWithProjectile(0, true);
			}
			else if (m_currentWeapon == WeaponType.Crossbow && ShowBoltWhenIdle && m_weaponSlot >= 0)
			{
				SetWeaponWithProjectile(0, false);
			}
			else if (m_currentWeapon == WeaponType.RepeatCrossbow && m_weaponSlot >= 0)
			{
				SetWeaponWithProjectile(0, false);
			}
		}

		#endregion

		#region Animaciones (simplificadas)

		private void ApplyBowAimingAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0.2f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(0.05f, 0.05f, 0.05f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-0.05f, 0.3f, 0.05f);

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void ApplyBowDrawingAnimation()
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
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					horizontalOffset,
					verticalOffset,
					depthOffset
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					pitchRotation,
					yawRotation,
					rollRotation
				);

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
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

		private void ApplyCrossbowAimingAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void ApplyCrossbowDrawingAnimation()
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

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void ApplyCrossbowReloadingAnimation()
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

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
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

		private void ApplyRepeatCrossbowAimingAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 1.3f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void ApplyRepeatCrossbowDrawingAnimation()
		{
			if (m_componentModel != null)
			{
				float drawProgress = m_currentDraw;
				m_componentModel.AimHandAngleOrder = 1.3f + drawProgress * 0.1f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.08f + (0.05f * drawProgress),
					-0.1f,
					0.07f - (0.03f * drawProgress)
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void ApplyRepeatCrossbowFiringAnimation()
		{
			if (m_componentModel != null)
			{
				float fireProgress = (float)((m_subsystemTime.GameTime - m_fireTime) / 0.2f);
				float recoil = 0.08f * (1f - fireProgress);

				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.03f + recoil,
					-0.1f,
					0.04f
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					-1.55f + recoil * 1.5f,
					0f,
					0f
				);
			}
		}

		private void ApplyMusketCockingAnimation()
		{
			if (m_componentModel != null)
			{
				float cockProgress = (float)((m_subsystemTime.GameTime - m_cockStartTime) / MusketCockTime);
				m_componentModel.AimHandAngleOrder = 1.2f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.08f + (0.03f * cockProgress),
					-0.08f,
					0.07f
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.6f, 0f, 0f);

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void ApplyMusketAimingAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void ApplyMusketFiringAnimation()
		{
			if (m_componentModel != null)
			{
				float recoilFactor = (float)(1.5f - (m_subsystemTime.GameTime - m_fireTime) * 5f);
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

		private void ApplyMusketReloadingAnimation()
		{
			if (m_componentModel != null)
			{
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / MusketReloadTime);
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

		private void ApplyItemsLauncherCockingAnimation()
		{
			if (m_componentModel != null)
			{
				float cockProgress = (float)((m_subsystemTime.GameTime - m_cockStartTime) / 0.5);
				m_componentModel.AimHandAngleOrder = 1.2f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.1f + (0.04f * cockProgress),
					-0.08f,
					0.08f
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void ApplyItemsLauncherAimingAnimation()
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.08f, 0.08f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.6f, 0f, 0f);

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void ApplyItemsLauncherFiringAnimation()
		{
			if (m_componentModel != null)
			{
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

		private void ApplyItemsLauncherReloadingAnimation()
		{
			if (m_componentModel != null)
			{
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / ItemsLauncherReloadTime);

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

				m_componentModel.LookAtOrder = null;
			}
		}

		#endregion
	}
}
