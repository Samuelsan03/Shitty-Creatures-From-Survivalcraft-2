using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// IA defensiva que usa armas a distancia sin necesidad de munición en inventario.
	/// Las animaciones y el ciclo de disparo son gestionados por los subsistemas originales.
	/// Variedad de municiones: arco (todas las flechas), ballesta (pernos de hierro, diamante, explosivos), mosquete (balas).
	/// </summary>
	public class ComponentDefensiveAI : ComponentBehavior, IUpdateable
	{
		public override float ImportanceLevel => 0f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// Único parámetro configurable desde la plantilla
		public bool CanUseInventory = true;

		// Rangos de combate (privados, no configurables desde plantilla)
		private float m_meleeRange = 5f;
		private float m_rangedRange = 100f;

		// Cooldowns después de disparar (segundos)
		private const double BOW_COOLDOWN = 1.5;
		private const double CROSSBOW_COOLDOWN = 1.5;
		private const double MUSKET_COOLDOWN = 0.8;

		// Tiempo mínimo de apuntado antes de poder disparar (segundos)
		private const double CROSSBOW_MIN_AIM_TIME = 0.3;
		private const double MUSKET_MIN_AIM_TIME = 0.5; // el martillo se amartilla tras ~0.5s

		// Componentes
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentInventory m_componentInventory;
		private ComponentNewChaseBehavior m_componentChase;
		private ComponentCreatureModel m_componentCreatureModel;

		// Subsistemas
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;

		// Estado de apuntado
		private bool m_isAiming = false;
		private double m_aimStartTime;
		private Ray3 m_aimRay;
		private int m_currentWeaponType; // 0=none, 1=bow, 2=crossbow, 3=musket

		// Último tiempo de disparo para cada arma
		private double m_lastBowShotTime = -1000;
		private double m_lastCrossbowShotTime = -1000;
		private double m_lastMusketShotTime = -1000;

		// Índices de bloques (obtenidos dinámicamente)
		private int m_bowBlockIndex;
		private int m_crossbowBlockIndex;
		private int m_musketBlockIndex;
		private int m_arrowBlockIndex;
		private int m_bulletBlockIndex;

		// Aleatoriedad
		private Random m_random = new Random();

		// Lista de todos los tipos de flecha
		private ArrowBlock.ArrowType[] m_allArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.CopperArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.FireArrow
		};

		// Lista de pernos para ballesta
		private ArrowBlock.ArrowType[] m_crossbowBolts = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
		};

		// Lista de tipos de bala
		private BulletBlock.BulletType[] m_bulletTypes = new BulletBlock.BulletType[]
		{
			BulletBlock.BulletType.MusketBall,
			BulletBlock.BulletType.Buckshot,
			BulletBlock.BulletType.BuckshotBall
		};

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentInventory = Entity.FindComponent<ComponentInventory>();
			m_componentChase = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);

			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);

			// Obtener índices mediante BlocksManager (sin números mágicos)
			m_bowBlockIndex = BlocksManager.GetBlockIndex<BowBlock>(false);
			m_crossbowBlockIndex = BlocksManager.GetBlockIndex<CrossbowBlock>(false);
			m_musketBlockIndex = BlocksManager.GetBlockIndex<MusketBlock>(false);
			m_arrowBlockIndex = BlocksManager.GetBlockIndex<ArrowBlock>(false);
			m_bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false);

			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", CanUseInventory);

			// Suscribirse al evento de creación de proyectiles para que no dejen loot
			if (m_subsystemProjectiles != null)
				m_subsystemProjectiles.ProjectileAdded += OnProjectileAdded;
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue("CanUseInventory", CanUseInventory);
		}

		public override void Dispose()
		{
			if (m_subsystemProjectiles != null)
				m_subsystemProjectiles.ProjectileAdded -= OnProjectileAdded;
			base.Dispose();
		}

		public void Update(float dt)
		{
			if (!CanUseInventory || m_componentMiner == null || m_componentInventory == null)
				return;

			if (m_componentChase == null || m_componentChase.Target == null)
			{
				CancelAiming();
				return;
			}

			ComponentCreature target = m_componentChase.Target;
			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);

			bool useMelee = distance <= m_meleeRange;
			bool useRanged = distance > m_meleeRange && distance <= m_rangedRange;

			if (useMelee)
			{
				CancelAiming();
				EquipMeleeWeapon();
			}
			else if (useRanged)
			{
				if (!EquipAndLoadRangedWeapon())
				{
					CancelAiming();
					return;
				}
				UpdateAiming(target);
			}
			else
			{
				CancelAiming();
			}
		}

		// ===== GESTIÓN DE ARMAS =====

		private void EquipMeleeWeapon()
		{
			int activeSlot = m_componentInventory.ActiveSlotIndex;
			int activeValue = m_componentInventory.GetSlotValue(activeSlot);
			if (IsMeleeWeapon(activeValue)) return;

			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int value = m_componentInventory.GetSlotValue(i);
				if (IsMeleeWeapon(value))
				{
					m_componentInventory.ActiveSlotIndex = i;
					break;
				}
			}
		}

		private bool EquipAndLoadRangedWeapon()
		{
			int activeSlot = m_componentInventory.ActiveSlotIndex;
			int activeValue = m_componentInventory.GetSlotValue(activeSlot);

			if (IsRangedWeapon(activeValue))
			{
				if (IsWeaponLoaded(activeValue))
				{
					m_currentWeaponType = Terrain.ExtractContents(activeValue);
					return true;
				}
				// El arma equipada está descargada, intentar recargarla
				if (LoadWeapon(activeSlot))
				{
					m_currentWeaponType = Terrain.ExtractContents(activeValue);
					return true;
				}
			}

			// Buscar otra arma a distancia que se pueda cargar
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int value = m_componentInventory.GetSlotValue(i);
				if (IsRangedWeapon(value) && LoadWeapon(i))
				{
					m_componentInventory.ActiveSlotIndex = i;
					m_currentWeaponType = Terrain.ExtractContents(value);
					return true;
				}
			}
			return false;
		}

		private bool LoadWeapon(int slot)
		{
			int weaponValue = m_componentInventory.GetSlotValue(slot);
			int contents = Terrain.ExtractContents(weaponValue);
			int data = Terrain.ExtractData(weaponValue);

			if (contents == m_bowBlockIndex)
			{
				// Si ya tiene flecha, no recargar
				if (BowBlock.GetArrowType(data) != null)
					return true;

				// Seleccionar aleatoriamente un tipo de flecha
				ArrowBlock.ArrowType arrowType = m_allArrowTypes[m_random.Int(m_allArrowTypes.Length)];
				int newData = BowBlock.SetArrowType(data, arrowType);
				// NO modificar el draw: el subsistema se encargará de aumentarlo con Aim(InProgress)
				int newValue = Terrain.MakeBlockValue(m_bowBlockIndex, 0, newData);
				m_componentInventory.RemoveSlotItems(slot, 1);
				m_componentInventory.AddSlotItems(slot, newValue, 1);
				return true;
			}
			else if (contents == m_crossbowBlockIndex)
			{
				int draw = CrossbowBlock.GetDraw(data);
				ArrowBlock.ArrowType? arrowType = CrossbowBlock.GetArrowType(data);
				if (draw == 15 && arrowType != null)
					return true;

				// Seleccionar aleatoriamente un perno
				ArrowBlock.ArrowType boltType = m_crossbowBolts[m_random.Int(m_crossbowBolts.Length)];
				int newData = CrossbowBlock.SetArrowType(data, boltType);
				newData = CrossbowBlock.SetDraw(newData, 15);
				int newValue = Terrain.MakeBlockValue(m_crossbowBlockIndex, 0, newData);
				m_componentInventory.RemoveSlotItems(slot, 1);
				m_componentInventory.AddSlotItems(slot, newValue, 1);
				return true;
			}
			else if (contents == m_musketBlockIndex)
			{
				MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
				if (loadState == MusketBlock.LoadState.Loaded)
					return true;

				// Seleccionar aleatoriamente un tipo de bala
				BulletBlock.BulletType bulletType = m_bulletTypes[m_random.Int(m_bulletTypes.Length)];
				int newData = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
				newData = MusketBlock.SetBulletType(newData, bulletType);
				int newValue = Terrain.MakeBlockValue(m_musketBlockIndex, 0, newData);
				m_componentInventory.RemoveSlotItems(slot, 1);
				m_componentInventory.AddSlotItems(slot, newValue, 1);
				return true;
			}
			return false;
		}

		private bool IsWeaponLoaded(int weaponValue)
		{
			int contents = Terrain.ExtractContents(weaponValue);
			int data = Terrain.ExtractData(weaponValue);

			if (contents == m_bowBlockIndex)
				return BowBlock.GetArrowType(data) != null;
			if (contents == m_crossbowBlockIndex)
			{
				int draw = CrossbowBlock.GetDraw(data);
				return draw == 15 && CrossbowBlock.GetArrowType(data) != null;
			}
			if (contents == m_musketBlockIndex)
			{
				MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
				return loadState == MusketBlock.LoadState.Loaded;
			}
			return false;
		}

		private bool IsMeleeWeapon(int value)
		{
			int contents = Terrain.ExtractContents(value);
			Block block = BlocksManager.Blocks[contents];
			float meleePower = block.GetMeleePower(value);
			if (meleePower <= 0) return false;
			return !IsRangedWeapon(value);
		}

		private bool IsRangedWeapon(int value)
		{
			int contents = Terrain.ExtractContents(value);
			return contents == m_bowBlockIndex ||
				   contents == m_crossbowBlockIndex ||
				   contents == m_musketBlockIndex;
		}

		// ===== GESTIÓN DE APUNTADO Y DISPARO =====

		private void UpdateAiming(ComponentCreature target)
		{
			// Verificar cooldown según el arma actual
			double currentTime = m_subsystemTime.GameTime;
			bool canFireCooldown = true;

			if (m_currentWeaponType == m_bowBlockIndex)
				canFireCooldown = currentTime - m_lastBowShotTime >= BOW_COOLDOWN;
			else if (m_currentWeaponType == m_crossbowBlockIndex)
				canFireCooldown = currentTime - m_lastCrossbowShotTime >= CROSSBOW_COOLDOWN;
			else if (m_currentWeaponType == m_musketBlockIndex)
				canFireCooldown = currentTime - m_lastMusketShotTime >= MUSKET_COOLDOWN;

			if (!canFireCooldown)
			{
				CancelAiming();
				return;
			}

			// Calcular rayo hacia el objetivo
			Vector3 eye = m_componentCreatureModel.EyePosition;
			Vector3 targetCenter = target.ComponentBody.BoundingBox.Center();
			Vector3 aimDir = Vector3.Normalize(targetCenter - eye);
			m_aimRay = new Ray3(eye, aimDir);

			if (!m_isAiming)
			{
				m_isAiming = true;
				m_aimStartTime = currentTime;
			}

			// Llamar a Aim con InProgress cada frame para actualizar el estado del arma
			m_componentMiner.Aim(m_aimRay, AimState.InProgress);

			// Obtener el arma actual para verificar si está lista para disparar
			int activeSlot = m_componentInventory.ActiveSlotIndex;
			int weaponValue = m_componentInventory.GetSlotValue(activeSlot);
			int data = Terrain.ExtractData(weaponValue);
			bool readyToFire = false;
			double aimTime = currentTime - m_aimStartTime;

			if (m_currentWeaponType == m_bowBlockIndex)
			{
				// Arco listo cuando el draw ha llegado a 15 (máxima tensión)
				int draw = BowBlock.GetDraw(data);
				readyToFire = draw == 15;
			}
			else if (m_currentWeaponType == m_crossbowBlockIndex)
			{
				// Ballesta: necesita un tiempo mínimo de apuntado para mostrar la animación
				readyToFire = aimTime >= CROSSBOW_MIN_AIM_TIME;
			}
			else if (m_currentWeaponType == m_musketBlockIndex)
			{
				// Mosquete: necesita que el martillo esté amartillado (tarda ~0.5s)
				bool hammerCocked = MusketBlock.GetHammerState(data);
				MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
				readyToFire = hammerCocked && loadState == MusketBlock.LoadState.Loaded && aimTime >= MUSKET_MIN_AIM_TIME;
			}

			if (readyToFire)
			{
				m_componentMiner.Aim(m_aimRay, AimState.Completed);
				m_isAiming = false;

				// Registrar el momento del disparo para el cooldown
				if (m_currentWeaponType == m_bowBlockIndex)
					m_lastBowShotTime = currentTime;
				else if (m_currentWeaponType == m_crossbowBlockIndex)
					m_lastCrossbowShotTime = currentTime;
				else if (m_currentWeaponType == m_musketBlockIndex)
					m_lastMusketShotTime = currentTime;
			}

			// Evitar que ComponentNewChaseBehavior intente atacar cuerpo a cuerpo mientras apuntamos
			if (m_componentCreatureModel != null)
				m_componentCreatureModel.AttackOrder = false;
		}

		private void CancelAiming()
		{
			if (m_isAiming)
			{
				m_componentMiner.Aim(m_aimRay, AimState.Cancelled);
				m_isAiming = false;
			}
		}

		// ===== EVITAR QUE LOS PROYECTILES DEJEN LOOT =====
		private void OnProjectileAdded(Projectile projectile)
		{
			// Solo los proyectiles disparados por esta criatura
			if (projectile.Owner == m_componentCreature)
			{
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			}
		}
	}
}
