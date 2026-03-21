using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// IA defensiva que usa armas a distancia sin necesidad de munición en inventario.
	/// Las animaciones y el ciclo de disparo son gestionados por los subsistemas originales.
	/// Soporte para arco, ballesta, mosquete, ballesta repetidora (RepeatCrossbow) y lanzallamas (FlameThrower).
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

		// Cooldowns después de disparar (segundos) – para armas no automáticas
		private const double BOW_COOLDOWN = 1.5;
		private const double CROSSBOW_COOLDOWN = 1.5;
		private const double MUSKET_COOLDOWN = 0.8;
		private const double REPEAT_CROSSBOW_COOLDOWN = 1.2; // Tiempo entre disparos de la ballesta repetidora
		private const double FLAMETHROWER_COOLDOWN = 0;    // Cooldown para el lanzallamas (coincide con el subsistema)

		// Tiempo mínimo de apuntado antes de poder disparar (segundos)
		private const double CROSSBOW_MIN_AIM_TIME = 0.3;
		private const double MUSKET_MIN_AIM_TIME = 1.5;     // el martillo se amartilla tras ~0.5s
		private const double REPEAT_CROSSBOW_MIN_AIM_TIME = 0.5; // Mínimo para que se vea la animación

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
		private int m_currentWeaponType; // 0=none, 1=bow, 2=crossbow, 3=musket, 4=repeatCrossbow, 5=flameThrower

		// Último tiempo de disparo para cada arma
		private double m_lastBowShotTime = -1000;
		private double m_lastCrossbowShotTime = -1000;
		private double m_lastMusketShotTime = -1000;
		private double m_lastRepeatCrossbowShotTime = -1000;
		private double m_lastFlameThrowerShotTime = -1000;

		// Índices de bloques (obtenidos dinámicamente)
		private int m_bowBlockIndex;
		private int m_crossbowBlockIndex;
		private int m_musketBlockIndex;
		private int m_repeatCrossbowBlockIndex;
		private int m_flameThrowerBlockIndex;
		private int m_arrowBlockIndex;
		private int m_bulletBlockIndex;
		private int m_repeatArrowBlockIndex;
		private int m_flameBulletBlockIndex;

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

		// Lista de tipos de bala para mosquete
		private BulletBlock.BulletType[] m_bulletTypes = new BulletBlock.BulletType[]
		{
			BulletBlock.BulletType.MusketBall,
			BulletBlock.BulletType.Buckshot,
			BulletBlock.BulletType.BuckshotBall
		};

		// Lista de tipos de flecha para ballesta repetidora (RepeatArrow)
		private RepeatArrowBlock.ArrowType[] m_repeatArrowTypes = new RepeatArrowBlock.ArrowType[]
		{
			RepeatArrowBlock.ArrowType.CopperArrow,
			RepeatArrowBlock.ArrowType.IronArrow,
			RepeatArrowBlock.ArrowType.DiamondArrow,
			RepeatArrowBlock.ArrowType.ExplosiveArrow,
			RepeatArrowBlock.ArrowType.PoisonArrow,
			RepeatArrowBlock.ArrowType.SeriousPoisonArrow
		};

		// Lista de tipos de bala para lanzallamas
		private FlameBulletBlock.FlameBulletType[] m_flameBulletTypes = new FlameBulletBlock.FlameBulletType[]
		{
			FlameBulletBlock.FlameBulletType.Flame,
			FlameBulletBlock.FlameBulletType.Poison
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
			m_repeatCrossbowBlockIndex = BlocksManager.GetBlockIndex<RepeatCrossbowBlock>(false);
			m_flameThrowerBlockIndex = BlocksManager.GetBlockIndex<FlameThrowerBlock>(false);
			m_arrowBlockIndex = BlocksManager.GetBlockIndex<ArrowBlock>(false);
			m_bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false);
			m_repeatArrowBlockIndex = BlocksManager.GetBlockIndex<RepeatArrowBlock>(false);
			m_flameBulletBlockIndex = BlocksManager.GetBlockIndex<FlameBulletBlock>(false);

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
					// Si el arma está cargada pero no completamente (para RepeatCrossbow y FlameThrower), intentar cargar más
					TryFullyLoadWeapon(activeSlot);
					return true;
				}
				// El arma equipada está descargada, intentar recargarla
				if (LoadWeapon(activeSlot))
				{
					// Recargar completamente si es posible
					TryFullyLoadWeapon(activeSlot);
					m_currentWeaponType = Terrain.ExtractContents(m_componentInventory.GetSlotValue(activeSlot));
					return true;
				}
			}

			// Buscar otra arma a distancia que se pueda cargar
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int value = m_componentInventory.GetSlotValue(i);
				if (IsRangedWeapon(value) && LoadWeapon(i))
				{
					// Recargar completamente
					TryFullyLoadWeapon(i);
					m_componentInventory.ActiveSlotIndex = i;
					m_currentWeaponType = Terrain.ExtractContents(m_componentInventory.GetSlotValue(i));
					return true;
				}
			}
			return false;
		}

		// Carga tantas municiones como sea posible para armas con cargador (RepeatCrossbow, FlameThrower)
		private void TryFullyLoadWeapon(int slot)
		{
			int maxAttempts = 20; // Evitar bucles infinitos
			for (int i = 0; i < maxAttempts; i++)
			{
				int weaponValue = m_componentInventory.GetSlotValue(slot);
				if (!IsRangedWeapon(weaponValue)) break;

				// Si el arma ya está completamente cargada (según su capacidad), salir
				if (IsWeaponFullyLoaded(weaponValue)) break;

				// Intentar cargar una munición más
				if (!LoadWeapon(slot)) break;
			}
		}

		private bool IsWeaponFullyLoaded(int weaponValue)
		{
			int contents = Terrain.ExtractContents(weaponValue);
			int data = Terrain.ExtractData(weaponValue);

			if (contents == m_repeatCrossbowBlockIndex)
			{
				int loadCount = RepeatCrossbowBlock.GetLoadCount(weaponValue);
				return loadCount >= 1; // Capacidad máxima
			}
			if (contents == m_flameThrowerBlockIndex)
			{
				int loadCount = FlameThrowerBlock.GetLoadCount(weaponValue);
				return loadCount >= 15; // Capacidad máxima
			}
			// Otras armas: se consideran completamente cargadas si tienen munición (una sola)
			return IsWeaponLoaded(weaponValue);
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
			else if (contents == m_repeatCrossbowBlockIndex)
			{
				int draw = RepeatCrossbowBlock.GetDraw(data);
				RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
				int loadCount = RepeatCrossbowBlock.GetLoadCount(weaponValue);

				// Si el arma no está tensada (draw < 15), tensarla
				if (draw < 15)
				{
					int newData = RepeatCrossbowBlock.SetDraw(data, 15);
					int newValue = Terrain.MakeBlockValue(m_repeatCrossbowBlockIndex, loadCount, newData);
					m_componentInventory.RemoveSlotItems(slot, 1);
					m_componentInventory.AddSlotItems(slot, newValue, 1);
					return true;
				}

				// Si está tensada pero no tiene tipo de flecha, cargar una flecha
				if (arrowType == null)
				{
					// Seleccionar tipo de flecha aleatorio
					RepeatArrowBlock.ArrowType newArrowType = m_repeatArrowTypes[m_random.Int(m_repeatArrowTypes.Length)];
					int newData = RepeatCrossbowBlock.SetArrowType(data, newArrowType);
					int newValue = Terrain.MakeBlockValue(m_repeatCrossbowBlockIndex, 1, newData);
					m_componentInventory.RemoveSlotItems(slot, 1);
					m_componentInventory.AddSlotItems(slot, newValue, 1);
					return true;
				}

				// Ya tiene flecha, si no está llena, incrementar carga
				if (loadCount < 8)
				{
					int newData = data; // mismo tipo de flecha
					int newValue = Terrain.MakeBlockValue(m_repeatCrossbowBlockIndex, loadCount + 1, newData);
					m_componentInventory.RemoveSlotItems(slot, 1);
					m_componentInventory.AddSlotItems(slot, newValue, 1);
					return true;
				}

				return true; // ya está llena
			}
			else if (contents == m_flameThrowerBlockIndex)
			{
				FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
				int loadCount = FlameThrowerBlock.GetLoadCount(weaponValue);
				FlameBulletBlock.FlameBulletType? bulletType = FlameThrowerBlock.GetBulletType(data);

				// Si está vacío, cargar una bala
				if (loadState == FlameThrowerBlock.LoadState.Empty)
				{
					// Seleccionar tipo de bala aleatorio
					FlameBulletBlock.FlameBulletType newBulletType = m_flameBulletTypes[m_random.Int(m_flameBulletTypes.Length)];
					int newData = FlameThrowerBlock.SetBulletType(data, newBulletType);
					newData = FlameThrowerBlock.SetLoadState(newData, FlameThrowerBlock.LoadState.Loaded);
					// Activar interruptor (switch) automáticamente
					newData = FlameThrowerBlock.SetSwitchState(newData, true);
					int newValue = Terrain.MakeBlockValue(m_flameThrowerBlockIndex, 1, newData);
					m_componentInventory.RemoveSlotItems(slot, 1);
					m_componentInventory.AddSlotItems(slot, newValue, 1);
					return true;
				}

				// Ya está cargado, si no está lleno, incrementar carga
				if (loadCount < 15)
				{
					int newData = data; // mismo tipo de bala
					int newValue = Terrain.MakeBlockValue(m_flameThrowerBlockIndex, loadCount + 1, newData);
					m_componentInventory.RemoveSlotItems(slot, 1);
					m_componentInventory.AddSlotItems(slot, newValue, 1);
					return true;
				}

				return true; // ya está lleno
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
			if (contents == m_repeatCrossbowBlockIndex)
			{
				int draw = RepeatCrossbowBlock.GetDraw(data);
				RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
				int loadCount = RepeatCrossbowBlock.GetLoadCount(weaponValue);
				return draw == 15 && arrowType != null && loadCount > 0;
			}
			if (contents == m_flameThrowerBlockIndex)
			{
				FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
				int loadCount = FlameThrowerBlock.GetLoadCount(weaponValue);
				return loadState == FlameThrowerBlock.LoadState.Loaded && loadCount > 0;
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
				   contents == m_musketBlockIndex ||
				   contents == m_repeatCrossbowBlockIndex ||
				   contents == m_flameThrowerBlockIndex;
		}

		// ===== GESTIÓN DE APUNTADO Y DISPARO =====

		private void UpdateAiming(ComponentCreature target)
		{
			// Verificar cooldown según el arma actual (para armas no automáticas)
			double currentTime = m_subsystemTime.GameTime;
			bool canFireCooldown = true;

			if (m_currentWeaponType == m_bowBlockIndex)
				canFireCooldown = currentTime - m_lastBowShotTime >= BOW_COOLDOWN;
			else if (m_currentWeaponType == m_crossbowBlockIndex)
				canFireCooldown = currentTime - m_lastCrossbowShotTime >= CROSSBOW_COOLDOWN;
			else if (m_currentWeaponType == m_musketBlockIndex)
				canFireCooldown = currentTime - m_lastMusketShotTime >= MUSKET_COOLDOWN;
			else if (m_currentWeaponType == m_repeatCrossbowBlockIndex)
				canFireCooldown = currentTime - m_lastRepeatCrossbowShotTime >= REPEAT_CROSSBOW_COOLDOWN;
			else if (m_currentWeaponType == m_flameThrowerBlockIndex)
				canFireCooldown = currentTime - m_lastFlameThrowerShotTime >= FLAMETHROWER_COOLDOWN;

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
			else if (m_currentWeaponType == m_repeatCrossbowBlockIndex)
			{
				// Ballesta repetidora: necesita draw = 15, tener tipo de flecha y tiempo mínimo de apuntado
				int draw = RepeatCrossbowBlock.GetDraw(data);
				RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
				readyToFire = draw == 15 && arrowType != null && aimTime >= REPEAT_CROSSBOW_MIN_AIM_TIME;
			}
			else if (m_currentWeaponType == m_flameThrowerBlockIndex)
			{
				// Lanzallamas: necesita estar cargado y el interruptor activado
				FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
				int loadCount = FlameThrowerBlock.GetLoadCount(weaponValue);
				bool switchState = FlameThrowerBlock.GetSwitchState(data);
				readyToFire = loadState == FlameThrowerBlock.LoadState.Loaded && loadCount > 0 && switchState;
			}

			if (readyToFire)
			{
				// Para armas automáticas (lanzallamas), no completamos el apuntado, solo continuamos apuntando.
				// Para las demás, completamos el apuntado para que dispare.
				bool isAutomatic = (m_currentWeaponType == m_flameThrowerBlockIndex);
				if (!isAutomatic)
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
					else if (m_currentWeaponType == m_repeatCrossbowBlockIndex)
						m_lastRepeatCrossbowShotTime = currentTime;
				}
				else
				{
					// Para lanzallamas, actualizamos el tiempo de último disparo para cooldown,
					// pero no cancelamos el apuntado.
					m_lastFlameThrowerShotTime = currentTime;
				}
			}

			// Para lanzallamas, si se quedó sin munición durante el apuntado, cancelar y recargar
			if (m_currentWeaponType == m_flameThrowerBlockIndex)
			{
				int loadCount = FlameThrowerBlock.GetLoadCount(weaponValue);
				FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
				if (loadCount <= 0 || loadState != FlameThrowerBlock.LoadState.Loaded)
				{
					CancelAiming();
				}
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
