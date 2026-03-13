using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentDefensiveAI : ComponentBehavior, IUpdateable
	{
		// Subsistemas
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemTerrain m_subsystemTerrain;

		// Componentes
		private ComponentCreature m_componentCreature;
		private ComponentCreatureModel m_componentModel;
		private ComponentNewHumanModel m_componentNewHumanModel;
		private ComponentInventory m_componentInventory;
		private ComponentNewChaseBehavior m_componentChase;
		private ComponentNewHerdBehavior m_componentHerd;

		// Configuración
		public bool CanUseInventory = true;

		// Armas
		private enum WeaponType { None, Bow, Crossbow, Musket }
		private WeaponType m_currentWeapon = WeaponType.None;
		private int m_weaponSlot = -1;
		private int m_lastWeaponSlot = -1;
		private int m_lastWeaponValue = 0; // Para detectar cambios en el contenido del slot

		// Estados de disparo
		private enum FiringState
		{
			Idle,
			Aiming,
			Drawing,      // Arco y ballesta
			Cocking,      // Mosquete
			Reloading,    // Mosquete y ballesta (carga de virote)
			Firing,
			Cooldown
		}
		private FiringState m_firingState = FiringState.Idle;
		private double m_stateStartTime;
		private float m_currentDraw; // 0-1 para arco/ballesta

		// Listas de munición (infinita, como en los shooters)
		private ArrowBlock.ArrowType[] m_availableArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.CopperArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.FireArrow
		};

		private ArrowBlock.ArrowType[] m_availableBoltTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
		};

		// Constantes de tiempo (valores de los shooters)
		private const float BowAimTime = 0.5f;
		private const float BowDrawTime = 1.2f;
		private const float BowAccuracy = 0.03f;
		private const float BowSpeed = 35f;

		private const float CrossbowAimTime = 0.5f;
		private const float CrossbowDrawTime = 1.5f;
		private const float CrossbowReloadTime = 0.3f;
		private const float CrossbowAccuracy = 0.02f;
		private const float CrossbowSpeed = 45f;

		private const float MusketAimTime = 1f;
		private const float MusketCockTime = 0.5f;
		private const float MusketReloadTime = 0.55f;
		private const float MusketAccuracy = 0.02f;
		private const float MusketSpeed = 120f;

		private const float CooldownTime = 0.8f;

		private Random m_random = new Random();
		private float m_importanceLevel = 200f;
		private bool m_initialized;

		public int UpdateOrder => 0;
		public override float ImportanceLevel => m_importanceLevel;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory");

			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentNewHumanModel = Entity.FindComponent<ComponentNewHumanModel>(false);
			if (m_componentNewHumanModel != null)
			{
				m_componentModel = m_componentNewHumanModel;
				m_componentNewHumanModel.SetAimSmoothness(0.4f);
			}
			else
			{
				m_componentModel = Entity.FindComponent<ComponentCreatureModel>(true);
			}
			m_componentInventory = Entity.FindComponent<ComponentInventory>(true);
			m_componentChase = Entity.FindComponent<ComponentNewChaseBehavior>(true);
			m_componentHerd = Entity.FindComponent<ComponentNewHerdBehavior>(true);
		}

		public override void OnEntityAdded()
		{
			base.OnEntityAdded();
			m_initialized = true;
			if (CanUseInventory)
			{
				FindBestWeapon();
				m_lastWeaponSlot = m_weaponSlot;
				if (m_weaponSlot >= 0)
					m_lastWeaponValue = m_componentInventory.GetSlotValue(m_weaponSlot);
			}
		}

		public void Update(float dt)
		{
			if (!m_initialized || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = m_componentChase?.Target;
			if (target == null)
			{
				ResetToIdle();
				return;
			}

			// Mirar al objetivo siempre
			if (m_componentModel != null)
				m_componentModel.LookAtOrder = target.ComponentCreatureModel.EyePosition;

			if (!CanUseInventory)
				return;

			// Verificar arma actual
			if (m_weaponSlot < 0 || !IsWeaponInSlot(m_weaponSlot))
			{
				FindBestWeapon();
				if (m_weaponSlot < 0)
					return;
				m_lastWeaponValue = m_componentInventory.GetSlotValue(m_weaponSlot);
			}

			// Verificar si el contenido del slot ha cambiado (por ejemplo, reemplazo del arma)
			int currentValue = m_componentInventory.GetSlotValue(m_weaponSlot);
			if (currentValue != m_lastWeaponValue)
			{
				// El arma en el slot cambió, reiniciar y buscar de nuevo
				ResetToIdle();
				FindBestWeapon();
				if (m_weaponSlot >= 0)
					m_lastWeaponValue = m_componentInventory.GetSlotValue(m_weaponSlot);
				else
					return;
			}

			// Si el arma cambió (índice de slot), reiniciamos la máquina
			if (m_weaponSlot != m_lastWeaponSlot)
			{
				ResetToIdle();
				m_lastWeaponSlot = m_weaponSlot;
				m_lastWeaponValue = currentValue;
			}

			EnsureWeaponSelected();
			UpdateFiringState(target, dt);
		}

		private void UpdateFiringState(ComponentCreature target, float dt)
		{
			double now = m_subsystemTime.GameTime;

			switch (m_firingState)
			{
				case FiringState.Idle:
					StartAiming(target);
					break;

				case FiringState.Aiming:
					ApplyAimingAnimation();
					if (now - m_stateStartTime >= GetAimTime())
					{
						if (m_currentWeapon == WeaponType.Musket)
							OnAimingCompleteMusket();
						else if (m_currentWeapon == WeaponType.Crossbow)
							StartDrawing();
						else if (m_currentWeapon == WeaponType.Bow)
							StartDrawing();
					}
					break;

				case FiringState.Drawing:
					ApplyDrawingAnimation();
					m_currentDraw = MathUtils.Clamp((float)((now - m_stateStartTime) / GetDrawTime()), 0f, 1f);
					UpdateWeaponDraw();
					if (now - m_stateStartTime >= GetDrawTime())
					{
						if (m_currentWeapon == WeaponType.Crossbow)
							StartReloading(); // Ballesta: después de tensar, cargar virote
						else if (m_currentWeapon == WeaponType.Bow)
							Fire(target);     // Arco: después de tensar, disparar
					}
					break;

				case FiringState.Cocking:
					ApplyCockingAnimation();
					if (now - m_stateStartTime >= MusketCockTime)
					{
						SetMusketHammerState(true);
						// Después de amartillar, verificar si está cargado
						int slotValue = m_componentInventory.GetSlotValue(m_weaponSlot);
						int data = Terrain.ExtractData(slotValue);
						if (MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded)
							Fire(target);
						else
							StartReloading();
					}
					break;

				case FiringState.Reloading:
					ApplyReloadingAnimation();
					if (now - m_stateStartTime >= GetReloadTime())
					{
						if (m_currentWeapon == WeaponType.Musket)
						{
							SetMusketLoaded(); // Cargar bala (martillo sigue abajo)
											   // Volver a apuntar (el ciclo se encargará de amartillar si es necesario)
							StartAiming(target);
						}
						else if (m_currentWeapon == WeaponType.Crossbow)
						{
							// Elegir un virote aleatorio y ponerlo en la ballesta (tensión 15)
							ArrowBlock.ArrowType boltType = SelectRandomBoltType();
							SetCrossbowBolt(boltType);
							Fire(target);
						}
					}
					break;

				case FiringState.Firing:
					ApplyFiringAnimation();
					if (now - m_stateStartTime >= 0.2)
					{
						if (m_currentWeapon == WeaponType.Musket)
						{
							// Mosquete: después de disparar, recargar
							m_firingState = FiringState.Reloading;
							m_stateStartTime = now;
						}
						else
						{
							// Arco y ballesta: cooldown
							m_firingState = FiringState.Cooldown;
							m_stateStartTime = now;
							ClearProjectileFromWeapon(); // Quitar flecha/virote del arma
						}
					}
					break;

				case FiringState.Cooldown:
					if (now - m_stateStartTime >= CooldownTime)
						m_firingState = FiringState.Idle;
					break;
			}
		}

		private void OnAimingCompleteMusket()
		{
			int slotValue = m_componentInventory.GetSlotValue(m_weaponSlot);
			int data = Terrain.ExtractData(slotValue);
			bool hammerState = MusketBlock.GetHammerState(data);
			MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);

			if (!hammerState)
			{
				StartCocking();
			}
			else if (loadState == MusketBlock.LoadState.Loaded)
			{
				Fire(m_componentChase.Target);
			}
			else
			{
				StartReloading();
			}
		}

		private void StartAiming(ComponentCreature target)
		{
			m_firingState = FiringState.Aiming;
			m_stateStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;
			ApplyAimingAnimation();

			// Preparar el arma según el tipo
			if (m_currentWeapon == WeaponType.Bow)
			{
				// Elegir una flecha aleatoria y ponerla en el arco (draw 0)
				ArrowBlock.ArrowType arrowType = SelectRandomArrowType();
				SetBowArrow(arrowType, 0);
			}
			else if (m_currentWeapon == WeaponType.Crossbow)
			{
				// Asegurar que la ballesta esté sin virote y tensión 0
				SetCrossbowBolt(null, 0);
			}
			// Mosquete no necesita preparación aquí
		}

		private void StartDrawing()
		{
			m_firingState = FiringState.Drawing;
			m_stateStartTime = m_subsystemTime.GameTime;
			m_subsystemAudio.PlaySound("Audio/BowDraw", 0.5f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);
			ApplyDrawingAnimation();
		}

		private void StartCocking()
		{
			m_firingState = FiringState.Cocking;
			m_stateStartTime = m_subsystemTime.GameTime;
			m_subsystemAudio.PlaySound("Audio/HammerCock", 1f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);
			ApplyCockingAnimation();
		}

		private void StartReloading()
		{
			m_firingState = FiringState.Reloading;
			m_stateStartTime = m_subsystemTime.GameTime;
			if (m_currentWeapon == WeaponType.Musket)
				m_subsystemAudio.PlaySound("Audio/Reload", 1.5f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 5f, false);
			else if (m_currentWeapon == WeaponType.Crossbow)
				m_subsystemAudio.PlaySound("Audio/Reload", 1f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 3f, false);
			ApplyReloadingAnimation();
		}

		private void Fire(ComponentCreature target)
		{
			m_firingState = FiringState.Firing;
			m_stateStartTime = m_subsystemTime.GameTime;
			ApplyFiringAnimation();

			switch (m_currentWeapon)
			{
				case WeaponType.Bow: ShootBow(target); break;
				case WeaponType.Crossbow: ShootCrossbow(target); break;
				case WeaponType.Musket: ShootMusket(target); break;
			}
		}

		// ===== SELECCIÓN DE MUNICIÓN (INFINITA) =====
		private ArrowBlock.ArrowType SelectRandomArrowType()
		{
			return m_availableArrowTypes[m_random.Int(0, m_availableArrowTypes.Length - 1)];
		}

		private ArrowBlock.ArrowType SelectRandomBoltType()
		{
			return m_availableBoltTypes[m_random.Int(0, m_availableBoltTypes.Length - 1)];
		}

		// ===== MANIPULACIÓN DEL INVENTARIO (CRÍTICA) =====
		// Nota: Después de cada modificación, actualizamos m_lastWeaponValue.
		private void UpdateWeaponDraw()
		{
			if (m_weaponSlot < 0) return;
			int val = m_componentInventory.GetSlotValue(m_weaponSlot);
			int contents = Terrain.ExtractContents(val);
			if (contents != BowBlock.Index && contents != CrossbowBlock.Index) return;
			int data = Terrain.ExtractData(val);
			int draw = (int)(m_currentDraw * 15f);
			int newData = (contents == BowBlock.Index) ? BowBlock.SetDraw(data, draw) : CrossbowBlock.SetDraw(data, draw);
			if (newData != data)
			{
				int newVal = Terrain.ReplaceData(val, newData);
				m_componentInventory.RemoveSlotItems(m_weaponSlot, 1);
				m_componentInventory.AddSlotItems(m_weaponSlot, newVal, 1);
				m_componentInventory.ActiveSlotIndex = m_weaponSlot;
				m_lastWeaponValue = newVal; // Actualizar valor guardado
			}
		}

		private void SetBowArrow(ArrowBlock.ArrowType type, int draw)
		{
			if (m_weaponSlot < 0) return;
			int val = m_componentInventory.GetSlotValue(m_weaponSlot);
			int data = Terrain.ExtractData(val);
			int newData = BowBlock.SetArrowType(data, type);
			newData = BowBlock.SetDraw(newData, draw);
			int newVal = Terrain.ReplaceData(val, newData);
			m_componentInventory.RemoveSlotItems(m_weaponSlot, 1);
			m_componentInventory.AddSlotItems(m_weaponSlot, newVal, 1);
			m_componentInventory.ActiveSlotIndex = m_weaponSlot;
			m_lastWeaponValue = newVal;
		}

		private void SetCrossbowBolt(ArrowBlock.ArrowType? type, int draw = 0)
		{
			if (m_weaponSlot < 0) return;
			int val = m_componentInventory.GetSlotValue(m_weaponSlot);
			int data = Terrain.ExtractData(val);
			int newData = CrossbowBlock.SetArrowType(data, type);
			newData = CrossbowBlock.SetDraw(newData, draw);
			int newVal = Terrain.ReplaceData(val, newData);
			m_componentInventory.RemoveSlotItems(m_weaponSlot, 1);
			m_componentInventory.AddSlotItems(m_weaponSlot, newVal, 1);
			m_componentInventory.ActiveSlotIndex = m_weaponSlot;
			m_lastWeaponValue = newVal;
		}

		private void SetCrossbowBolt(ArrowBlock.ArrowType type) => SetCrossbowBolt(type, 15);

		private void SetMusketHammerState(bool cocked)
		{
			if (m_weaponSlot < 0) return;
			int val = m_componentInventory.GetSlotValue(m_weaponSlot);
			int data = Terrain.ExtractData(val);
			int newData = MusketBlock.SetHammerState(data, cocked);
			int newVal = Terrain.ReplaceData(val, newData);
			m_componentInventory.RemoveSlotItems(m_weaponSlot, 1);
			m_componentInventory.AddSlotItems(m_weaponSlot, newVal, 1);
			m_componentInventory.ActiveSlotIndex = m_weaponSlot;
			m_lastWeaponValue = newVal;
		}

		private void SetMusketLoaded()
		{
			if (m_weaponSlot < 0) return;
			int val = m_componentInventory.GetSlotValue(m_weaponSlot);
			int data = Terrain.ExtractData(val);
			int newData = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
			newData = MusketBlock.SetBulletType(newData, BulletBlock.BulletType.MusketBall);
			// El martillo no se toca (sigue abajo)
			int newVal = Terrain.ReplaceData(val, newData);
			m_componentInventory.RemoveSlotItems(m_weaponSlot, 1);
			m_componentInventory.AddSlotItems(m_weaponSlot, newVal, 1);
			m_componentInventory.ActiveSlotIndex = m_weaponSlot;
			m_lastWeaponValue = newVal;
		}

		private void ClearProjectileFromWeapon()
		{
			if (m_weaponSlot < 0) return;
			int val = m_componentInventory.GetSlotValue(m_weaponSlot);
			if (val == 0) return;
			int contents = Terrain.ExtractContents(val);
			int data = Terrain.ExtractData(val);
			int newData = data;

			if (contents == BowBlock.Index)
			{
				newData = BowBlock.SetArrowType(BowBlock.SetDraw(data, 0), null);
			}
			else if (contents == CrossbowBlock.Index)
			{
				newData = CrossbowBlock.SetArrowType(CrossbowBlock.SetDraw(data, 0), null);
			}
			else if (contents == MusketBlock.Index)
			{
				// Dejar el mosquete vacío y con el martillo abatido
				newData = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Empty);
				newData = MusketBlock.SetHammerState(newData, false);
				// El tipo de bala no es relevante cuando está vacío
			}
			else
			{
				return;
			}

			if (newData != data)
			{
				int newVal = Terrain.ReplaceData(val, newData);
				m_componentInventory.RemoveSlotItems(m_weaponSlot, 1);
				m_componentInventory.AddSlotItems(m_weaponSlot, newVal, 1);
				m_componentInventory.ActiveSlotIndex = m_weaponSlot;
				m_lastWeaponValue = newVal;
			}
		}


		// ===== DISPAROS (igual que los shooters) =====
		private void ShootBow(ComponentCreature target)
		{
			Vector3 firePos = m_componentCreature.ComponentCreatureModel.EyePosition - new Vector3(0f, 0.1f, 0f);
			Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
			Vector3 dir = Vector3.Normalize(targetPos - firePos);
			float acc = BowAccuracy * (1.5f - m_currentDraw);
			dir += new Vector3(m_random.Float(-acc, acc), m_random.Float(-acc * 0.5f, acc * 0.5f), m_random.Float(-acc, acc));
			dir = Vector3.Normalize(dir);

			int val = m_componentInventory.GetSlotValue(m_weaponSlot);
			ArrowBlock.ArrowType? arrowType = BowBlock.GetArrowType(Terrain.ExtractData(val));
			if (arrowType == null) return; // No debería pasar

			int arrowVal = Terrain.MakeBlockValue(ArrowBlock.Index, 0, ArrowBlock.SetArrowType(0, arrowType.Value));
			float speed = BowSpeed * (0.5f + m_currentDraw * 1.5f);
			var proj = m_subsystemProjectiles.FireProjectile(arrowVal, firePos, dir * speed, Vector3.Zero, m_componentCreature);
			if (proj != null)
			{
				proj.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				if (arrowType == ArrowBlock.ArrowType.FireArrow)
				{
					m_subsystemProjectiles.AddTrail(proj, Vector3.Zero,
						new SmokeTrailParticleSystem(15, 0.5f, float.MaxValue, Color.White));
					proj.IsIncendiary = true;
				}
			}
			m_subsystemAudio.PlaySound("Audio/Bow", 0.8f, m_random.Float(-0.1f, 0.1f), firePos, 15f, false);
		}

		private void ShootCrossbow(ComponentCreature target)
		{
			Vector3 firePos = m_componentCreature.ComponentCreatureModel.EyePosition - new Vector3(0f, 0.1f, 0f);
			Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
			Vector3 dir = Vector3.Normalize(targetPos - firePos);
			dir += new Vector3(m_random.Float(-CrossbowAccuracy, CrossbowAccuracy),
								m_random.Float(-CrossbowAccuracy * 0.5f, CrossbowAccuracy * 0.5f),
								m_random.Float(-CrossbowAccuracy, CrossbowAccuracy));
			dir = Vector3.Normalize(dir);

			int val = m_componentInventory.GetSlotValue(m_weaponSlot);
			ArrowBlock.ArrowType? boltType = CrossbowBlock.GetArrowType(Terrain.ExtractData(val));
			if (boltType == null) return;

			int boltVal = Terrain.MakeBlockValue(ArrowBlock.Index, 0, ArrowBlock.SetArrowType(0, boltType.Value));
			float speed = CrossbowSpeed * (0.8f + m_currentDraw * 0.4f);
			var proj = m_subsystemProjectiles.FireProjectile(boltVal, firePos, dir * speed, Vector3.Zero, m_componentCreature);
			if (proj != null)
				proj.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;

			m_subsystemAudio.PlaySound("Audio/Bow", 0.8f, m_random.Float(-0.1f, 0.1f), firePos, 15f, false);
			m_subsystemNoise?.MakeNoise(firePos, 0.5f, 20f);
		}

		private void ShootMusket(ComponentCreature target)
		{
			Vector3 firePos = m_componentCreature.ComponentCreatureModel.EyePosition - new Vector3(0f, 0.1f, 0f);
			Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
			Vector3 dir = Vector3.Normalize(targetPos - firePos);
			dir += new Vector3(m_random.Float(-MusketAccuracy, MusketAccuracy),
								m_random.Float(-MusketAccuracy * 0.5f, MusketAccuracy * 0.5f),
								m_random.Float(-MusketAccuracy, MusketAccuracy));
			dir = Vector3.Normalize(dir);

			int bulletIdx = BlocksManager.GetBlockIndex<BulletBlock>(false, false);
			if (bulletIdx > 0)
			{
				int bulletData = BulletBlock.SetBulletType(0, BulletBlock.BulletType.MusketBall);
				int bulletVal = Terrain.MakeBlockValue(bulletIdx, 0, bulletData);
				m_subsystemProjectiles.FireProjectile(bulletVal, firePos, dir * MusketSpeed, Vector3.Zero, m_componentCreature);

				if (m_subsystemParticles != null && m_subsystemTerrain != null)
				{
					m_subsystemParticles.AddParticleSystem(
						new GunSmokeParticleSystem(m_subsystemTerrain, firePos + dir * 0.3f, dir), false);
				}
				m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, m_random.Float(-0.1f, 0.1f), firePos, 15f, false);
				m_subsystemNoise?.MakeNoise(firePos, 1f, 40f);
				m_componentCreature.ComponentBody.ApplyImpulse(-dir * 3f);
			}

			// Vaciar el mosquete (load state empty, hammer false)
			int val = m_componentInventory.GetSlotValue(m_weaponSlot);
			int data = Terrain.ExtractData(val);
			int newData = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Empty);
			newData = MusketBlock.SetHammerState(newData, false);
			int newVal = Terrain.ReplaceData(val, newData);
			m_componentInventory.RemoveSlotItems(m_weaponSlot, 1);
			m_componentInventory.AddSlotItems(m_weaponSlot, newVal, 1);
			m_componentInventory.ActiveSlotIndex = m_weaponSlot;
			m_lastWeaponValue = newVal;
		}

		// ===== ANIMACIONES (idénticas a los shooters) =====
		private void ApplyAimingAnimation()
		{
			if (m_componentModel == null) return;
			switch (m_currentWeapon)
			{
				case WeaponType.Bow:
					m_componentModel.AimHandAngleOrder = 0.5f;
					m_componentModel.InHandItemOffsetOrder = new Vector3(0.02f, 0.12f, 0.08f);
					m_componentModel.InHandItemRotationOrder = new Vector3(-0.05f, 0.25f, 0.01f);
					break;
				case WeaponType.Crossbow:
				case WeaponType.Musket:
					m_componentModel.AimHandAngleOrder = 1.4f;
					m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
					break;
			}
		}

		private void ApplyDrawingAnimation()
		{
			if (m_componentModel == null) return;
			float f = m_currentDraw;
			if (m_currentWeapon == WeaponType.Bow)
			{
				m_componentModel.AimHandAngleOrder = 0.5f + 0.4f * f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(0.02f - 0.01f * f, 0.12f + 0.05f * f, 0.08f - 0.03f * f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-0.05f - 0.15f * f, 0.25f - 0.08f * f, 0.01f - 0.005f * f);
			}
			else if (m_currentWeapon == WeaponType.Crossbow)
			{
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f + 0.05f * f, -0.08f, 0.07f - 0.03f * f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
			}
		}

		private void ApplyCockingAnimation()
		{
			if (m_componentModel == null || m_currentWeapon != WeaponType.Musket) return;
			float p = (float)((m_subsystemTime.GameTime - m_stateStartTime) / MusketCockTime);
			m_componentModel.AimHandAngleOrder = 1.2f;
			m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f + 0.03f * p, -0.08f, 0.07f);
			m_componentModel.InHandItemRotationOrder = new Vector3(-1.6f, 0f, 0f);
		}

		private void ApplyReloadingAnimation()
		{
			if (m_componentModel == null) return;
			float p = (float)((m_subsystemTime.GameTime - m_stateStartTime) / GetReloadTime());
			if (m_currentWeapon == WeaponType.Musket)
			{
				m_componentModel.AimHandAngleOrder = MathUtils.Lerp(1.0f, 0.5f, p);
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f - 0.1f * p);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f + 0.5f * p, 0f, 0f);
				m_componentModel.LookAtOrder = null; // No mira al objetivo mientras recarga
			}
			else if (m_currentWeapon == WeaponType.Crossbow)
			{
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f - 0.05f * p, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
			}
		}

		private void ApplyFiringAnimation()
		{
			if (m_componentModel == null) return;
			float p = (float)((m_subsystemTime.GameTime - m_stateStartTime) / 0.2);
			if (m_currentWeapon == WeaponType.Bow)
			{
				if (p < 0.5f)
				{
					float r = 0.01f * (1f - p * 2f);
					m_componentModel.InHandItemOffsetOrder += new Vector3(r, 0f, 0f);
					m_componentModel.InHandItemRotationOrder += new Vector3(r * 1.5f, 0f, 0f);
				}
				else
				{
					float ret = (p - 0.5f) / 0.5f;
					m_componentModel.AimHandAngleOrder = 0.5f * (1f - ret);
					m_componentModel.InHandItemOffsetOrder = new Vector3(0.02f * (1f - ret), 0.12f * (1f - ret), 0.08f * (1f - ret));
					m_componentModel.InHandItemRotationOrder = new Vector3(-0.05f * (1f - ret), 0.25f * (1f - ret), 0.01f * (1f - ret));
				}
			}
			else if (m_currentWeapon == WeaponType.Crossbow)
			{
				if (p < 0.5f)
				{
					float r = 0.05f * (1f - p * 2f);
					m_componentModel.InHandItemOffsetOrder += new Vector3(r, 0f, 0f);
					m_componentModel.InHandItemRotationOrder += new Vector3(r * 2f, 0f, 0f);
				}
				else
				{
					float ret = (p - 0.5f) / 0.5f;
					m_componentModel.AimHandAngleOrder = 1.4f * (1f - ret);
					m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f * (1f - ret), -0.08f * (1f - ret), 0.07f * (1f - ret));
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f * (1f - ret), 0f, 0f);
				}
			}
			else if (m_currentWeapon == WeaponType.Musket)
			{
				float r = MathUtils.Max(1.5f - (float)(m_subsystemTime.GameTime - m_stateStartTime) * 5f, 1f);
				m_componentModel.AimHandAngleOrder = 1.4f * r;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f - 0.05f * (1.5f - r));
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f + 0.3f * (1.5f - r), 0f, 0f);
			}
		}

		// ===== UTILIDADES =====
		private float GetAimTime()
		{
			switch (m_currentWeapon)
			{
				case WeaponType.Bow: return BowAimTime;
				case WeaponType.Crossbow: return CrossbowAimTime;
				case WeaponType.Musket: return MusketAimTime;
				default: return 0f;
			}
		}

		private float GetDrawTime()
		{
			switch (m_currentWeapon)
			{
				case WeaponType.Bow: return BowDrawTime;
				case WeaponType.Crossbow: return CrossbowDrawTime;
				default: return 0f;
			}
		}

		private float GetReloadTime()
		{
			switch (m_currentWeapon)
			{
				case WeaponType.Crossbow: return CrossbowReloadTime;
				case WeaponType.Musket: return MusketReloadTime;
				default: return 0f;
			}
		}

		private void FindBestWeapon()
		{
			m_weaponSlot = -1;
			m_currentWeapon = WeaponType.None;
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int val = m_componentInventory.GetSlotValue(i);
				if (val == 0) continue;
				int contents = Terrain.ExtractContents(val);
				if (contents == MusketBlock.Index)
				{
					m_weaponSlot = i;
					m_currentWeapon = WeaponType.Musket;
					break;
				}
				else if (contents == CrossbowBlock.Index && m_currentWeapon != WeaponType.Musket)
				{
					m_weaponSlot = i;
					m_currentWeapon = WeaponType.Crossbow;
				}
				else if (contents == BowBlock.Index && m_currentWeapon == WeaponType.None)
				{
					m_weaponSlot = i;
					m_currentWeapon = WeaponType.Bow;
				}
			}
			if (m_weaponSlot >= 0)
				m_lastWeaponValue = m_componentInventory.GetSlotValue(m_weaponSlot);
		}

		private bool IsWeaponInSlot(int slot)
		{
			if (slot < 0) return false;
			int val = m_componentInventory.GetSlotValue(slot);
			if (val == 0) return false;
			int c = Terrain.ExtractContents(val);
			return c == BowBlock.Index || c == CrossbowBlock.Index || c == MusketBlock.Index;
		}

		private void EnsureWeaponSelected()
		{
			if (m_weaponSlot >= 0 && m_componentInventory.ActiveSlotIndex != m_weaponSlot)
				m_componentInventory.ActiveSlotIndex = m_weaponSlot;
		}

		private void ResetToIdle()
		{
			m_firingState = FiringState.Idle;
			m_currentDraw = 0f;
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
			// Limpiar el proyectil del arma al quedar inactivo
			ClearProjectileFromWeapon();
		}
	}
}
