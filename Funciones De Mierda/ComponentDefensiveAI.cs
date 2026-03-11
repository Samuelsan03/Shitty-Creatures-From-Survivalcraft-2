using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentDefensiveAI : ComponentBehavior, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;
		public bool CanUseInventory { get; set; } = true;

		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemGameInfo m_subsystemGameInfo;

		private ComponentCreature m_componentCreature;
		private ComponentNewChaseBehavior m_componentChase;
		private ComponentNewHerdBehavior m_componentHerd;
		private ComponentInventory m_componentInventory;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentMiner m_componentMiner;

		private int m_bowBlockIndex;
		private int m_crossbowBlockIndex;
		private int m_musketBlockIndex;
		private int m_arrowBlockIndex;
		private int m_bulletBlockIndex;
		private int m_repeatCrossbowBlockIndex;
		private int m_flameThrowerBlockIndex;
		private int m_itemsLauncherBlockIndex;
		private int m_repeatArrowBlockIndex;
		private int m_flameBulletBlockIndex;
		private int m_m4BlockIndex;
		private int m_akBlockIndex;
		private int m_revolverBlockIndex;
		private int m_sniperBlockIndex;
		private int m_bk43BlockIndex;
		private int m_mac10BlockIndex;
		private int m_swm500BlockIndex;
		private int m_kaBlockIndex;
		private int m_spas12BlockIndex;
		private int m_minigunBlockIndex;
		private int m_p90BlockIndex;
		private int m_augBlockIndex;
		private int m_uziBlockIndex;
		private int m_mendozaBlockIndex;
		private int m_grozaBlockIndex;
		private int m_izh43BlockIndex;
		private int m_aa12BlockIndex;
		private int m_g3BlockIndex;
		private int m_newG3BlockIndex;
		private int m_famasBlockIndex;
		private int m_scarBlockIndex;
		private int m_m249BlockIndex;
		private int m_mp5ssdBlockIndex;

		private StateMachine m_stateMachine = new StateMachine();
		private string m_currentStateName;
		private double m_nextCombatUpdateTime;
		private double m_nextProactiveReloadTime;
		private double m_aimStartTime;
		private float m_aimDuration;
		private double m_reloadStartTime;
		private float m_reloadDuration;
		private double m_nextAutoShotTime;       // Para armas automáticas (M4, AK, lanzallamas, Mac10, KA)
		private double m_fireEndTime;             // Cooldown tras disparo para no automáticas
		private int m_bowDraw;
		private float m_importanceLevel;
		private Random m_random = new Random();

		private enum WeaponType
		{
			None,
			Melee,
			Throwable,
			Bow,
			Crossbow,
			Musket,
			RepeatCrossbow,
			FlameThrower,
			ItemsLauncher,
			M4,
			AK,
			Revolver,
			Sniper,
			BK43,
			Mac10,
			SWM500,
			KA,
			SPAS12,
			Minigun,
			P90,
			AUG,
			Uzi,
			Mendoza,
			Groza,
			Izh43,
			AA12,
			G3,
			NewG3,
			Famas,
			SCAR,
			M249,
			MP5SSD
		}

		private struct WeaponInfo
		{
			public int Slot;
			public int Value;
			public WeaponType Type;
			public bool IsReady;
			public ArrowBlock.ArrowType? ArrowType;
			public RepeatArrowBlock.ArrowType? RepeatArrowType;
			public BulletBlock.BulletType? BulletType;
			public FlameBulletBlock.FlameBulletType? FlameBulletType;
		}

		private WeaponInfo m_currentWeapon;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentInventory = Entity.FindComponent<ComponentInventory>();
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentChase = Entity.FindComponent<ComponentNewChaseBehavior>(true);
			m_componentHerd = Entity.FindComponent<ComponentNewHerdBehavior>();

			m_bowBlockIndex = BlocksManager.GetBlockIndex<BowBlock>(false, false);
			m_crossbowBlockIndex = BlocksManager.GetBlockIndex<CrossbowBlock>(false, false);
			m_musketBlockIndex = BlocksManager.GetBlockIndex<MusketBlock>(false, false);
			m_arrowBlockIndex = BlocksManager.GetBlockIndex<ArrowBlock>(false, false);
			m_bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false, false);
			m_repeatCrossbowBlockIndex = BlocksManager.GetBlockIndex<RepeatCrossbowBlock>(false, false);
			m_flameThrowerBlockIndex = BlocksManager.GetBlockIndex<FlameThrowerBlock>(false, false);
			m_itemsLauncherBlockIndex = BlocksManager.GetBlockIndex<ItemsLauncherBlock>(false, false);
			m_repeatArrowBlockIndex = BlocksManager.GetBlockIndex<RepeatArrowBlock>(false, false);
			m_flameBulletBlockIndex = BlocksManager.GetBlockIndex<FlameBulletBlock>(false, false);
			m_m4BlockIndex = BlocksManager.GetBlockIndex<M4Block>(false, false);
			m_akBlockIndex = BlocksManager.GetBlockIndex<AKBlock>(false, false);
			m_revolverBlockIndex = BlocksManager.GetBlockIndex<RevolverBlock>(false, false);
			m_sniperBlockIndex = BlocksManager.GetBlockIndex<SniperBlock>(false, false);
			m_bk43BlockIndex = BlocksManager.GetBlockIndex<BK43Block>(false, false);
			m_mac10BlockIndex = BlocksManager.GetBlockIndex<Mac10Block>(false, false);
			m_swm500BlockIndex = BlocksManager.GetBlockIndex<SWM500Block>(false, false);
			m_kaBlockIndex = BlocksManager.GetBlockIndex<KABlock>(false, false);
			m_spas12BlockIndex = BlocksManager.GetBlockIndex<SPAS12Block>(false, false);
			m_minigunBlockIndex = BlocksManager.GetBlockIndex<MinigunBlock>(false, false);
			m_p90BlockIndex = BlocksManager.GetBlockIndex<P90Block>(false, false);
			m_augBlockIndex = BlocksManager.GetBlockIndex<AUGBlock>(false, false);
			m_uziBlockIndex = BlocksManager.GetBlockIndex<UziBlock>(false, false);
			m_mendozaBlockIndex = BlocksManager.GetBlockIndex<MendozaBlock>(false, false);
			m_grozaBlockIndex = BlocksManager.GetBlockIndex<GrozaBlock>(false, false);
			m_izh43BlockIndex = BlocksManager.GetBlockIndex<Izh43Block>(false, false);
			m_aa12BlockIndex = BlocksManager.GetBlockIndex<AA12Block>(false, false);
			m_g3BlockIndex = BlocksManager.GetBlockIndex<G3Block>(false, false);
			m_newG3BlockIndex = BlocksManager.GetBlockIndex<NewG3Block>(false, false);
			m_famasBlockIndex = BlocksManager.GetBlockIndex<FamasBlock>(false, false);
			m_scarBlockIndex = BlocksManager.GetBlockIndex<SCARBlock>(false, false);
			m_m249BlockIndex = BlocksManager.GetBlockIndex<M249Block>(false, false);
			m_mp5ssdBlockIndex = BlocksManager.GetBlockIndex<MP5SSDBlock>(false, false);

			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory");

			m_stateMachine.AddState("Idle", null, Idle_Update, null);
			m_stateMachine.AddState("Aiming", Aiming_Enter, Aiming_Update, null);
			m_stateMachine.AddState("Firing", Firing_Enter, Firing_Update, Firing_Leave);
			m_stateMachine.AddState("Reloading", Reloading_Enter, Reloading_Update, Reloading_Leave);
			m_stateMachine.TransitionTo("Idle");
		}

		public void Update(float dt)
		{
			if (m_subsystemTime.GameTime >= m_nextProactiveReloadTime)
			{
				m_nextProactiveReloadTime = m_subsystemTime.GameTime + 1.0;
				if (m_currentStateName == "Idle")
				{
					ProactiveReloadCheck();
				}
			}

			if (m_subsystemTime.GameTime >= m_nextCombatUpdateTime)
			{
				m_stateMachine.Update();
			}
		}

		private void TransitionToState(string stateName)
		{
			m_currentStateName = stateName;
			m_stateMachine.TransitionTo(stateName);
		}

		private void RefreshCurrentWeaponReadyState()
		{
			if (m_componentInventory == null || m_componentInventory.ActiveSlotIndex < 0)
			{
				m_currentWeapon.IsReady = false;
				return;
			}

			int value = m_componentInventory.GetSlotValue(m_componentInventory.ActiveSlotIndex);
			if (value == 0)
			{
				m_currentWeapon.IsReady = false;
				return;
			}

			int data = Terrain.ExtractData(value);
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];

			if (block is BowBlock)
				m_currentWeapon.IsReady = BowBlock.GetArrowType(data) != null;
			else if (block is CrossbowBlock)
				m_currentWeapon.IsReady = CrossbowBlock.GetArrowType(data) != null && CrossbowBlock.GetDraw(data) == 15;
			else if (block is RepeatCrossbowBlock)
				m_currentWeapon.IsReady = RepeatCrossbowBlock.GetArrowType(data) != null && RepeatCrossbowBlock.GetDraw(data) == 15 && RepeatCrossbowBlock.GetLoadCount(value) > 0;
			else if (block is MusketBlock)
				m_currentWeapon.IsReady = MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded;
			else if (block is FlameThrowerBlock)
				m_currentWeapon.IsReady = FlameThrowerBlock.GetLoadState(data) == FlameThrowerBlock.LoadState.Loaded && FlameThrowerBlock.GetLoadCount(value) > 0;
			else if (block is ItemsLauncherBlock)
				m_currentWeapon.IsReady = ItemsLauncherBlock.GetFuel(data) > 0;
			else if (block is M4Block)
				m_currentWeapon.IsReady = M4Block.GetBulletNum(data) > 0;
			else if (block is AKBlock)
				m_currentWeapon.IsReady = AKBlock.GetBulletNum(data) > 0;
			else if (block is RevolverBlock)
				m_currentWeapon.IsReady = RevolverBlock.GetBulletNum(data) > 0;
			else if (block is SniperBlock)
				m_currentWeapon.IsReady = SniperBlock.GetBulletNum(data) > 0;
			else if (block is SpearBlock)
				m_currentWeapon.IsReady = true;
			else if (block is BK43Block)
				m_currentWeapon.IsReady = BK43Block.GetBulletNum(data) > 0;
			else if (block is Mac10Block)
				m_currentWeapon.IsReady = Mac10Block.GetBulletNum(data) > 0;
			else if (block is SWM500Block)
				m_currentWeapon.IsReady = SWM500Block.GetBulletNum(data) > 0;
			else if (block is KABlock)
				m_currentWeapon.IsReady = KABlock.GetBulletNum(data) > 0;
			else if (block is SPAS12Block)
				m_currentWeapon.IsReady = SPAS12Block.GetBulletNum(data) > 0;
			else if (block is MinigunBlock)
				m_currentWeapon.IsReady = MinigunBlock.GetBulletNum(data) > 0;
			else if (block is P90Block)
				m_currentWeapon.IsReady = P90Block.GetBulletNum(data) > 0;
			else if (block is AUGBlock)
				m_currentWeapon.IsReady = AUGBlock.GetBulletNum(data) > 0;
			else if (block is UziBlock)
				m_currentWeapon.IsReady = UziBlock.GetBulletNum(data) > 0;
			else if (block is MendozaBlock)
				m_currentWeapon.IsReady = MendozaBlock.GetBulletNum(data) > 0;
			else if (block is GrozaBlock)
				m_currentWeapon.IsReady = GrozaBlock.GetBulletNum(data) > 0;
			else if (block is Izh43Block)
				m_currentWeapon.IsReady = Izh43Block.GetBulletNum(data) > 0;
			else if (block is AA12Block)
				m_currentWeapon.IsReady = AA12Block.GetBulletNum(Terrain.ExtractData(value)) > 0; // Usar ExtractData si es necesario
			else if (block is G3Block)
				m_currentWeapon.IsReady = G3Block.GetBulletNum(data) > 0;
			else if (block is NewG3Block)
				m_currentWeapon.IsReady = NewG3Block.GetBulletNum(data) > 0;
			else if (block is FamasBlock)
				m_currentWeapon.IsReady = FamasBlock.GetBulletNum(Terrain.ExtractData(value)) > 0;
			else if (block is SCARBlock)
				m_currentWeapon.IsReady = SCARBlock.GetBulletNum(data) > 0;
			else if (block is M249Block)
				m_currentWeapon.IsReady = M249Block.GetBulletNum(Terrain.ExtractData(value)) > 0;
			else if (block is MP5SSDBlock)
				m_currentWeapon.IsReady = MP5SSDBlock.GetBulletNum(data) > 0;
		}

		private void Idle_Update()
		{
			if (m_componentChase.Target == null || !CanUseInventory || m_componentInventory == null)
			{
				m_importanceLevel = 0f;
				return;
			}

			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				m_componentChase.Target.ComponentBody.Position
			);

			if (distance <= 5f)
			{
				WeaponInfo melee = FindMeleeWeapon();
				if (melee.Type != WeaponType.None)
				{
					m_currentWeapon = melee;
					m_componentInventory.ActiveSlotIndex = m_currentWeapon.Slot;
					PerformMeleeAttack();
					m_importanceLevel = 1f;
					return;
				}
			}

			WeaponInfo best = FindBestRangedWeapon(distance);
			if (best.Type != WeaponType.None)
			{
				m_currentWeapon = best;
				m_componentInventory.ActiveSlotIndex = m_currentWeapon.Slot;
				m_importanceLevel = 1f;

				if (m_currentWeapon.IsReady)
					TransitionToState("Aiming");
				else
					TransitionToState("Reloading");
			}
			else
			{
				m_importanceLevel = 0f;
			}
		}

		private void Aiming_Enter()
		{
			m_aimStartTime = m_subsystemTime.GameTime;
			m_aimDuration = GetAimDurationForWeapon(m_currentWeapon.Type);
			m_bowDraw = 0;
		}

		private void Aiming_Update()
		{
			if (m_componentChase.Target == null)
			{
				TransitionToState("Idle");
				return;
			}

			m_componentCreatureModel.LookAtOrder = new Vector3?(m_componentChase.Target.ComponentCreatureModel.EyePosition);
			ApplyAimingAnimation();
			UpdateWeaponDuringAim();

			if (m_subsystemTime.GameTime >= m_aimStartTime + m_aimDuration)
				TransitionToState("Firing");
		}

		private void Firing_Enter()
		{
			PerformFire();

			if (IsAutomatic(m_currentWeapon.Type))
			{
				m_nextAutoShotTime = m_subsystemTime.GameTime + GetFireInterval(m_currentWeapon.Type);
				m_fireEndTime = double.MaxValue;
			}
			else
			{
				m_fireEndTime = m_subsystemTime.GameTime + 0.2;

				if (!m_currentWeapon.IsReady)
				{
					m_nextCombatUpdateTime = m_subsystemTime.GameTime + m_random.Float(1.5f, 2.5f);
					TransitionToState("Reloading");
				}
			}
		}

		private void Firing_Update()
		{
			if (m_componentChase.Target == null)
			{
				TransitionToState("Idle");
				return;
			}

			m_componentCreatureModel.LookAtOrder = new Vector3?(m_componentChase.Target.ComponentCreatureModel.EyePosition);
			ApplyAimingAnimation();

			if (IsAutomatic(m_currentWeapon.Type))
			{
				if (m_subsystemTime.GameTime >= m_nextAutoShotTime)
				{
					PerformFire();
					RefreshCurrentWeaponReadyState();

					if (m_currentWeapon.IsReady)
					{
						m_nextAutoShotTime = m_subsystemTime.GameTime + GetFireInterval(m_currentWeapon.Type);
					}
					else
					{
						m_nextCombatUpdateTime = m_subsystemTime.GameTime + m_random.Float(1.5f, 2.5f);
						TransitionToState("Reloading");
						return;
					}
				}
			}
			else
			{
				ApplyRecoilAnimation();

				if (m_subsystemTime.GameTime >= m_fireEndTime)
				{
					if (m_currentWeapon.IsReady && !RequiresReloadAfterShot(m_currentWeapon.Type))
					{
						TransitionToState("Aiming");
					}
					else
					{
						m_nextCombatUpdateTime = m_subsystemTime.GameTime + m_random.Float(1.5f, 2.5f);
						TransitionToState("Reloading");
					}
					return;
				}
			}
		}

		private void Firing_Leave()
		{
			m_componentCreatureModel.AimHandAngleOrder = 0f;
			m_componentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
			m_componentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
		}

		private void Reloading_Enter()
		{
			m_reloadStartTime = m_subsystemTime.GameTime;
			m_reloadDuration = GetReloadDurationForWeapon(m_currentWeapon.Type);
			ApplyReloadAnimation();
		}

		private void Reloading_Update()
		{
			if (m_subsystemTime.GameTime >= m_reloadStartTime + m_reloadDuration)
			{
				TryReloadWeapon(m_currentWeapon);
				RefreshCurrentWeaponReadyState();

				bool isModernFirearm = m_currentWeapon.Type == WeaponType.M4 ||
					   m_currentWeapon.Type == WeaponType.AK ||
					   m_currentWeapon.Type == WeaponType.Revolver ||
					   m_currentWeapon.Type == WeaponType.Sniper ||
					   m_currentWeapon.Type == WeaponType.BK43 ||
					   m_currentWeapon.Type == WeaponType.Mac10 ||
					   m_currentWeapon.Type == WeaponType.SWM500 ||
					   m_currentWeapon.Type == WeaponType.KA ||
					   m_currentWeapon.Type == WeaponType.SPAS12 ||
					   m_currentWeapon.Type == WeaponType.Minigun ||
					   m_currentWeapon.Type == WeaponType.P90 ||
					   m_currentWeapon.Type == WeaponType.AUG ||
					   m_currentWeapon.Type == WeaponType.Uzi ||
					   m_currentWeapon.Type == WeaponType.Mendoza ||
					   m_currentWeapon.Type == WeaponType.Groza ||
					   m_currentWeapon.Type == WeaponType.Izh43 ||
					   m_currentWeapon.Type == WeaponType.AA12 ||
					   m_currentWeapon.Type == WeaponType.G3 ||
					   m_currentWeapon.Type == WeaponType.NewG3 ||
					   m_currentWeapon.Type == WeaponType.Famas ||
					   m_currentWeapon.Type == WeaponType.SCAR ||
					   m_currentWeapon.Type == WeaponType.M249 ||
					   m_currentWeapon.Type == WeaponType.MP5SSD;

				if (isModernFirearm)
				{
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
							Log.Warning($"Error mostrando partículas de recarga completada: {ex.Message}");
						}
					}

					m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f),
						m_componentCreature.ComponentCreatureModel.EyePosition, 5f, true);
				}

				TransitionToState("Idle");
			}
		}

		private void Reloading_Leave()
		{
			m_componentCreatureModel.AimHandAngleOrder = 0f;
			m_componentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
			m_componentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
		}

		// -------- Métodos auxiliares ----------
		private bool IsAutomatic(WeaponType type)
		{
			return type == WeaponType.FlameThrower || type == WeaponType.M4 || type == WeaponType.AK ||
				   type == WeaponType.Mac10 || type == WeaponType.KA || type == WeaponType.Minigun ||
				   type == WeaponType.P90 || type == WeaponType.AUG || type == WeaponType.Uzi ||
				   type == WeaponType.Mendoza || type == WeaponType.Groza || type == WeaponType.AA12 ||
				   type == WeaponType.G3 || type == WeaponType.NewG3 || type == WeaponType.Famas ||
				   type == WeaponType.SCAR || type == WeaponType.M249 || type == WeaponType.MP5SSD;
		}
		private float GetFireInterval(WeaponType type)
		{
			switch (type)
			{
				case WeaponType.FlameThrower: return 0.15f;
				case WeaponType.M4: return 0.15f;
				case WeaponType.AK: return 0.17f;
				case WeaponType.Mac10: return 0.1f;
				case WeaponType.KA: return 0.1f;
				case WeaponType.Minigun: return 0.08f;  // 750 RPM
				case WeaponType.P90: return 0.067f;     // 900 RPM
				case WeaponType.AUG: return 0.17f;       // 350 RPM (similar a AK)
				case WeaponType.Uzi: return 0.08f;       // 750 RPM
				case WeaponType.Mendoza: return 0.12f;   // ~500 RPM
				case WeaponType.Groza: return 0.12f;     // ~500 RPM
				case WeaponType.AA12: return 0.2f;       // 300 RPM
				case WeaponType.G3: return 0.12f;        // 500 RPM
				case WeaponType.NewG3: return 0.12f;     // 500 RPM
				case WeaponType.Famas: return 0.09f;     // ~667 RPM
				case WeaponType.SCAR: return 0.1f;       // 600 RPM
				case WeaponType.M249: return 0.08f;      // 750 RPM
				case WeaponType.MP5SSD: return 0.12f;    // 500 RPM
				default: return 0f;
			}
		}
		private bool RequiresReloadAfterShot(WeaponType type)
		{
			switch (type)
			{
				case WeaponType.Bow:
				case WeaponType.Crossbow:
				case WeaponType.Musket:
					return true;
				case WeaponType.RepeatCrossbow:
				case WeaponType.ItemsLauncher:
				case WeaponType.FlameThrower:
				case WeaponType.BK43:    // Escopeta de 2 cartuchos
				case WeaponType.SWM500:   // Revólver de 8 balas
				case WeaponType.Mac10:    // Automática
				case WeaponType.KA:       // Automática
				case WeaponType.SPAS12:   // Escopeta semiautomática de 8 cartuchos
				case WeaponType.Minigun:  // Ametralladora rotativa
				case WeaponType.P90:      // Automática
				case WeaponType.AUG:      // Automática
				case WeaponType.Uzi:      // Automática
				case WeaponType.Mendoza:  // Automática
				case WeaponType.Groza:    // Automática
				case WeaponType.Izh43:    // Escopeta de 2 cartuchos
				case WeaponType.AA12:     // Escopeta automática
				case WeaponType.G3:       // Automática
				case WeaponType.NewG3:    // Automática
				case WeaponType.Famas:    // Automática
				case WeaponType.SCAR:     // Automática
				case WeaponType.M249:     // Ametralladora ligera
				case WeaponType.MP5SSD:   // Automática
					return false;
				default:
					return false;
			}
		}
		private WeaponInfo FindMeleeWeapon()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int value = m_componentInventory.GetSlotValue(i);
				if (value == 0) continue;

				Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
				if (block is MacheteBlock || block is WoodenClubBlock || block is StoneClubBlock || block is AxeBlock || block is SpearBlock)
				{
					return new WeaponInfo { Slot = i, Value = value, Type = WeaponType.Melee, IsReady = true };
				}
			}
			return default;
		}

		private WeaponInfo FindBestRangedWeapon(float distance)
		{
			WeaponInfo best = default;
			best.Type = WeaponType.None;

			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int value = m_componentInventory.GetSlotValue(i);
				if (value == 0) continue;

				Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
				int data = Terrain.ExtractData(value);
				WeaponType type = WeaponType.None;
				bool isReady = false;
				ArrowBlock.ArrowType? arrowType = null;
				RepeatArrowBlock.ArrowType? repeatArrowType = null;
				BulletBlock.BulletType? bulletType = null;
				FlameBulletBlock.FlameBulletType? flameBulletType = null;

				if (block is BowBlock)
				{
					type = WeaponType.Bow;
					arrowType = BowBlock.GetArrowType(data);
					isReady = arrowType != null;
				}
				else if (block is CrossbowBlock)
				{
					type = WeaponType.Crossbow;
					arrowType = CrossbowBlock.GetArrowType(data);
					int draw = CrossbowBlock.GetDraw(data);
					isReady = arrowType != null && draw == 15;
				}
				else if (block is RepeatCrossbowBlock)
				{
					type = WeaponType.RepeatCrossbow;
					repeatArrowType = RepeatCrossbowBlock.GetArrowType(data);
					int draw = RepeatCrossbowBlock.GetDraw(data);
					int loadCount = RepeatCrossbowBlock.GetLoadCount(value);
					isReady = repeatArrowType != null && draw == 15 && loadCount > 0;
				}
				else if (block is MusketBlock)
				{
					type = WeaponType.Musket;
					bulletType = MusketBlock.GetBulletType(data);
					isReady = MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded;
				}
				else if (block is FlameThrowerBlock)
				{
					type = WeaponType.FlameThrower;
					flameBulletType = FlameThrowerBlock.GetBulletType(data);
					bool isLoaded = FlameThrowerBlock.GetLoadState(data) == FlameThrowerBlock.LoadState.Loaded;
					int loadCount = FlameThrowerBlock.GetLoadCount(value);
					isReady = isLoaded && flameBulletType != null && loadCount > 0;
				}
				else if (block is ItemsLauncherBlock)
				{
					type = WeaponType.ItemsLauncher;
					int fuel = ItemsLauncherBlock.GetFuel(data);
					isReady = fuel > 0;
				}
				else if (block is SpearBlock)
				{
					type = WeaponType.Throwable;
					isReady = true;
				}
				else if (block is M4Block)
				{
					type = WeaponType.M4;
					int bulletNum = M4Block.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is AKBlock)
				{
					type = WeaponType.AK;
					int bulletNum = AKBlock.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is RevolverBlock)
				{
					type = WeaponType.Revolver;
					int bulletNum = RevolverBlock.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is SniperBlock)
				{
					type = WeaponType.Sniper;
					int bulletNum = SniperBlock.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				// Nuevas armas
				else if (block is BK43Block)
				{
					type = WeaponType.BK43;
					int bulletNum = BK43Block.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is Mac10Block)
				{
					type = WeaponType.Mac10;
					int bulletNum = Mac10Block.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is SWM500Block)
				{
					type = WeaponType.SWM500;
					int bulletNum = SWM500Block.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is KABlock)
				{
					type = WeaponType.KA;
					int bulletNum = KABlock.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is SPAS12Block)
				{
					type = WeaponType.SPAS12;
					int bulletNum = SPAS12Block.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is MinigunBlock)
				{
					type = WeaponType.Minigun;
					int bulletNum = MinigunBlock.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is P90Block)
				{
					type = WeaponType.P90;
					int bulletNum = P90Block.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is AUGBlock)
				{
					type = WeaponType.AUG;
					int bulletNum = AUGBlock.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is UziBlock)
				{
					type = WeaponType.Uzi;
					int bulletNum = UziBlock.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is MendozaBlock)
				{
					type = WeaponType.Mendoza;
					int bulletNum = MendozaBlock.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is GrozaBlock)
				{
					type = WeaponType.Groza;
					int bulletNum = GrozaBlock.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is Izh43Block)
				{
					type = WeaponType.Izh43;
					int bulletNum = Izh43Block.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is AA12Block)
				{
					type = WeaponType.AA12;
					int bulletNum = AA12Block.GetBulletNum(Terrain.ExtractData(value));
					isReady = bulletNum > 0;
				}
				else if (block is G3Block)
				{
					type = WeaponType.G3;
					int bulletNum = G3Block.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is NewG3Block)
				{
					type = WeaponType.NewG3;
					int bulletNum = NewG3Block.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is FamasBlock)
				{
					type = WeaponType.Famas;
					int bulletNum = FamasBlock.GetBulletNum(Terrain.ExtractData(value));
					isReady = bulletNum > 0;
				}
				else if (block is SCARBlock)
				{
					type = WeaponType.SCAR;
					int bulletNum = SCARBlock.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				else if (block is M249Block)
				{
					type = WeaponType.M249;
					int bulletNum = M249Block.GetBulletNum(Terrain.ExtractData(value));
					isReady = bulletNum > 0;
				}
				else if (block is MP5SSDBlock)
				{
					type = WeaponType.MP5SSD;
					int bulletNum = MP5SSDBlock.GetBulletNum(data);
					isReady = bulletNum > 0;
				}
				if (type == WeaponType.None) continue;

				bool inRange = IsWeaponInRange(type, distance);
				if (!inRange) continue;

				if (type == WeaponType.RepeatCrossbow && repeatArrowType == RepeatArrowBlock.ArrowType.ExplosiveArrow && distance < 20f)
				{
					continue;
				}

				if (best.Type == WeaponType.None || (isReady && !best.IsReady))
				{
					best = new WeaponInfo
					{
						Slot = i,
						Value = value,
						Type = type,
						IsReady = isReady,
						ArrowType = arrowType,
						RepeatArrowType = repeatArrowType,
						BulletType = bulletType,
						FlameBulletType = flameBulletType
					};
				}
			}
			return best;
		}

		private bool IsWeaponInRange(WeaponType type, float distance)
		{
			if (type == WeaponType.None)
				return false;
			return true;
		}

		private float GetAimDurationForWeapon(WeaponType type)
		{
			switch (type)
			{
				case WeaponType.Bow: return m_random.Float(1.2f, 1.8f);
				case WeaponType.Crossbow: return m_random.Float(0.8f, 1.2f);
				case WeaponType.RepeatCrossbow: return m_random.Float(0.8f, 1.2f);
				case WeaponType.Musket: return m_random.Float(1.0f, 1.5f);
				case WeaponType.FlameThrower: return m_random.Float(0.5f, 0.8f);
				case WeaponType.ItemsLauncher: return m_random.Float(0.5f, 1.0f);
				case WeaponType.Throwable: return m_random.Float(0.3f, 0.6f);
				case WeaponType.M4:
				case WeaponType.AK:
				case WeaponType.Revolver:
				case WeaponType.Sniper:
				case WeaponType.BK43:
				case WeaponType.Mac10:
				case WeaponType.SWM500:
				case WeaponType.KA:
				case WeaponType.SPAS12:
				case WeaponType.Minigun:
				case WeaponType.P90:
				case WeaponType.AUG:
				case WeaponType.Uzi:
				case WeaponType.Mendoza:
				case WeaponType.Groza:
				case WeaponType.Izh43:
				case WeaponType.AA12:
				case WeaponType.G3:
				case WeaponType.NewG3:
				case WeaponType.Famas:
				case WeaponType.SCAR:
				case WeaponType.M249:
				case WeaponType.MP5SSD:
					return m_random.Float(0.6f, 1.0f);
				default: return 1.0f;
			}
		}

		private float GetReloadDurationForWeapon(WeaponType type)
		{
			return 0.5f;
		}

		private void ApplyAimingAnimation()
		{
			switch (m_currentWeapon.Type)
			{
				case WeaponType.Throwable:
					m_componentCreatureModel.AimHandAngleOrder = 1.6f;
					break;
				case WeaponType.Bow:
					m_componentCreatureModel.AimHandAngleOrder = 1.2f;
					m_componentCreatureModel.InHandItemRotationOrder = new Vector3(0f, -0.2f, 0f);
					break;
				case WeaponType.Crossbow:
				case WeaponType.RepeatCrossbow:
					m_componentCreatureModel.AimHandAngleOrder = 1.3f;
					m_componentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
					m_componentCreatureModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);
					break;
				case WeaponType.Musket:
				case WeaponType.ItemsLauncher:
				case WeaponType.M4:
				case WeaponType.AK:
				case WeaponType.Revolver:
				case WeaponType.Sniper:
				case WeaponType.BK43:
				case WeaponType.Mac10:
				case WeaponType.SWM500:
				case WeaponType.KA:
				case WeaponType.SPAS12:
				case WeaponType.P90:
				case WeaponType.AUG:
				case WeaponType.Uzi:
				case WeaponType.Mendoza:
				case WeaponType.Groza:
				case WeaponType.Izh43:
				case WeaponType.G3:
				case WeaponType.NewG3:
				case WeaponType.Famas:
				case WeaponType.SCAR:
				case WeaponType.MP5SSD:
					m_componentCreatureModel.AimHandAngleOrder = 1.4f;
					m_componentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					m_componentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
					break;
				case WeaponType.FlameThrower:
					m_componentCreatureModel.AimHandAngleOrder = 1.4f;
					m_componentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					m_componentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
					break;
				case WeaponType.Minigun:
					m_componentCreatureModel.AimHandAngleOrder = 1.3f;
					m_componentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.1f, 0.05f);
					m_componentCreatureModel.InHandItemRotationOrder = new Vector3(-1.8f, 0f, 0f);
					break;
				case WeaponType.AA12:
					m_componentCreatureModel.AimHandAngleOrder = 1.2f;
					m_componentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.06f, -0.06f, 0.05f);
					m_componentCreatureModel.InHandItemRotationOrder = new Vector3(-1.4f, 0f, 0f);
					break;
				case WeaponType.M249:
					m_componentCreatureModel.AimHandAngleOrder = 1.2f;
					m_componentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.06f, -0.06f, 0.05f);
					m_componentCreatureModel.InHandItemRotationOrder = new Vector3(-1.4f, 0f, 0f);
					break;
			}
		}

		private void ApplyReloadAnimation()
		{
			m_componentCreatureModel.InHandItemOffsetOrder = new Vector3(0f, 0.1f, 0.1f);
			m_componentCreatureModel.AimHandAngleOrder = 0.5f;
		}

		private void ApplyRecoilAnimation()
		{
			m_componentCreatureModel.AimHandAngleOrder *= 1.1f;
			m_componentCreatureModel.InHandItemOffsetOrder -= new Vector3(0f, 0f, 0.05f);
		}

		private void UpdateWeaponDuringAim()
		{
			int slot = m_componentInventory.ActiveSlotIndex;
			if (slot < 0) return;

			int value = m_componentInventory.GetSlotValue(slot);
			int data = Terrain.ExtractData(value);
			int newValue = value;

			if (m_currentWeapon.Type == WeaponType.Bow)
			{
				float progress = (float)((m_subsystemTime.GameTime - m_aimStartTime) / m_aimDuration);
				m_bowDraw = MathUtils.Min((int)(progress * 16f), 15);
				newValue = Terrain.ReplaceData(value, BowBlock.SetDraw(data, m_bowDraw));
			}
			else if (m_currentWeapon.Type == WeaponType.Crossbow)
			{
				float progress = (float)((m_subsystemTime.GameTime - m_aimStartTime) / m_aimDuration);
				int draw = MathUtils.Min((int)(progress * 16f), 15);
				newValue = Terrain.ReplaceData(value, CrossbowBlock.SetDraw(data, draw));
			}
			else if (m_currentWeapon.Type == WeaponType.RepeatCrossbow)
			{
				float progress = (float)((m_subsystemTime.GameTime - m_aimStartTime) / m_aimDuration);
				int draw = MathUtils.Min((int)(progress * 16f), 15);
				newValue = Terrain.ReplaceData(value, RepeatCrossbowBlock.SetDraw(data, draw));
				if (draw == 15 && m_currentWeapon.RepeatArrowType == null)
				{
					newValue = Terrain.ReplaceData(value, RepeatCrossbowBlock.SetArrowType(data, GetRandomRepeatBoltType()));
				}
			}
			else if (m_currentWeapon.Type == WeaponType.Musket)
			{
				if (!MusketBlock.GetHammerState(data) && m_subsystemTime.GameTime > m_aimStartTime + 0.5)
				{
					newValue = Terrain.ReplaceData(value, MusketBlock.SetHammerState(data, true));
					m_subsystemAudio.PlaySound("Audio/HammerCock", 1f, m_random.Float(-0.1f, 0.1f),
						m_componentCreature.ComponentBody.Position, 3f, false);
				}
			}
			else if (m_currentWeapon.Type == WeaponType.FlameThrower)
			{
				if (!FlameThrowerBlock.GetSwitchState(data) && m_subsystemTime.GameTime > m_aimStartTime + 0.5)
				{
					newValue = Terrain.ReplaceData(value, FlameThrowerBlock.SetSwitchState(data, true));
					m_subsystemAudio.PlaySound("Audio/Items/Hammer Cock Remake", 1f, m_random.Float(-0.1f, 0.1f),
						m_componentCreature.ComponentBody.Position, 3f, false);
				}
			}
			// Las nuevas armas no requieren cambios durante la puntería

			if (newValue != value)
			{
				m_componentInventory.RemoveSlotItems(slot, 1);
				m_componentInventory.AddSlotItems(slot, newValue, 1);
			}
		}

		private void PerformFire()
		{
			if (m_componentChase.Target == null) return;

			int slot = m_componentInventory.ActiveSlotIndex;
			int value = m_componentInventory.GetSlotValue(slot);
			if (value == 0) return;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = m_componentChase.Target.ComponentBody.Position +
				new Vector3(0f, m_componentChase.Target.ComponentBody.StanceBoxSize.Y * 0.75f, 0f);

			Vector3 direction = Vector3.Normalize(targetPos - eyePos);
			float distance = Vector3.Distance(eyePos, targetPos);
			int data = Terrain.ExtractData(value);

			switch (m_currentWeapon.Type)
			{
				case WeaponType.Throwable:
					{
						float speed = MathUtils.Lerp(20f, 35f, distance / 20f);
						Vector3 velocity = direction * speed + new Vector3(0f, 2f, 0f);
						m_subsystemProjectiles.FireProjectile(value, eyePos, velocity, Vector3.Zero, m_componentCreature);
						m_subsystemAudio.PlaySound("Audio/Throw", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 3f, false);
						m_componentInventory.RemoveSlotItems(slot, 1);
						break;
					}

				case WeaponType.Bow:
					{
						ArrowBlock.ArrowType? arrowType = BowBlock.GetArrowType(data);
						if (arrowType != null)
						{
							float power = MathUtils.Lerp(0f, 28f, (float)Math.Pow(m_bowDraw / 15.0, 0.75));

							Vector3 spread = GetArrowSpread(arrowType.Value);
							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));

							Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
												  m_random.Float(-spread.Y, spread.Y) * up +
												  m_random.Float(-spread.Z, spread.Z) * direction;

							Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * power;

							int arrowValue = Terrain.MakeBlockValue(m_arrowBlockIndex, 0, ArrowBlock.SetArrowType(0, arrowType.Value));

							Projectile arrowProjectile = m_subsystemProjectiles.FireProjectile(arrowValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
							if (arrowProjectile != null)
							{
								arrowProjectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
								m_subsystemAudio.PlaySound("Audio/Bow", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 3f, false);
							}

							int newValue = Terrain.ReplaceData(value, BowBlock.SetDraw(BowBlock.SetArrowType(data, null), 0));
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
						}
						break;
					}

				case WeaponType.Crossbow:
					{
						ArrowBlock.ArrowType? arrowType = CrossbowBlock.GetArrowType(data);
						if (arrowType != null)
						{
							Vector3 spread = GetBoltSpread(arrowType.Value);
							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));

							Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
												  m_random.Float(-spread.Y, spread.Y) * up +
												  m_random.Float(-spread.Z, spread.Z) * direction;

							Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 38f;

							int boltValue = Terrain.MakeBlockValue(m_arrowBlockIndex, 0, ArrowBlock.SetArrowType(0, arrowType.Value));

							Projectile boltProjectile = m_subsystemProjectiles.FireProjectile(boltValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
							if (boltProjectile != null)
							{
								boltProjectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
								m_subsystemAudio.PlaySound("Audio/Bow", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 3f, false);
							}

							int newValue = Terrain.ReplaceData(value, CrossbowBlock.SetDraw(CrossbowBlock.SetArrowType(data, null), 0));
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
						}
						break;
					}

				case WeaponType.RepeatCrossbow:
					{
						RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
						if (arrowType != null)
						{
							Vector3 spread = GetRepeatBoltSpread(arrowType.Value);
							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));

							Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
												  m_random.Float(-spread.Y, spread.Y) * up +
												  m_random.Float(-spread.Z, spread.Z) * direction;

							Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 38f;

							int boltValue = Terrain.MakeBlockValue(m_repeatArrowBlockIndex, 0, RepeatArrowBlock.SetArrowType(0, arrowType.Value));

							Projectile boltProjectile = m_subsystemProjectiles.FireProjectile(boltValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
							if (boltProjectile != null)
							{
								boltProjectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
								m_subsystemAudio.PlaySound("Audio/Bow", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 3f, false);
							}

							int loadCount = RepeatCrossbowBlock.GetLoadCount(value) - 1;
							int newValue;
							if (loadCount > 0)
							{
								newValue = Terrain.MakeBlockValue(m_repeatCrossbowBlockIndex, loadCount,
									RepeatCrossbowBlock.SetDraw(RepeatCrossbowBlock.SetArrowType(data, arrowType), 15));
							}
							else
							{
								newValue = Terrain.ReplaceData(value,
									RepeatCrossbowBlock.SetDraw(RepeatCrossbowBlock.SetArrowType(data, (RepeatArrowBlock.ArrowType?)null), 0));
							}
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
						}
						break;
					}

				case WeaponType.Musket:
					{
						if (MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded && MusketBlock.GetHammerState(data))
						{
							BulletBlock.BulletType bulletType = MusketBlock.GetBulletType(data) ?? BulletBlock.BulletType.MusketBall;

							int projectileCount = 1;
							float baseSpeed = 120f;
							Vector3 spread = Vector3.Zero;

							if (bulletType == BulletBlock.BulletType.Buckshot)
							{
								projectileCount = 8;
								spread = new Vector3(0.04f, 0.04f, 0.25f);
								baseSpeed = 80f;
							}
							else if (bulletType == BulletBlock.BulletType.BuckshotBall)
							{
								projectileCount = 1;
								spread = new Vector3(0.06f, 0.06f, 0f);
								baseSpeed = 60f;
							}

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													  m_random.Float(-spread.Y, spread.Y) * up +
													  m_random.Float(-spread.Z, spread.Z) * direction;

								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * baseSpeed;

								int bulletValue = Terrain.MakeBlockValue(m_bulletBlockIndex, 0,
									BulletBlock.SetBulletType(0, bulletType));

								Projectile projectile = m_subsystemProjectiles.FireProjectile(bulletValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunSmokeParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);
							m_componentCreature.ComponentBody.ApplyImpulse(-4f * direction);

							int newValue = Terrain.ReplaceData(value, MusketBlock.SetLoadState(data, MusketBlock.LoadState.Empty));
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
						}
						break;
					}
				case WeaponType.SPAS12:
					{
						int bulletNum = SPAS12Block.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.09f, 0.09f, 0.03f);
							int projectileCount = 8; // Escopeta: 8 perdigones
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 280f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/SPAS 12 fuego", 1.5f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

							int newBulletNum = bulletNum - 1;
							int newData = SPAS12Block.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_spas12BlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.Minigun:
					{
						int bulletNum = MinigunBlock.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.04f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.6f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.6f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.6f, false)
							};
							direction = Vector3.Normalize(direction + v * 2.5f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.02f, 0.02f, 0.08f);
							int projectileCount = 1;
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala6), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 0);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 260f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/Chaingun fuego", 1.3f, m_random.Float(-0.15f, 0.15f), eyePos, 12f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1.3f, 50f);

							int newBulletNum = bulletNum - 1;
							int newData = MinigunBlock.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_minigunBlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.P90:
					{
						int bulletNum = P90Block.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.025f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 1.5f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.012f, 0.012f, 0.04f);
							int projectileCount = 1;
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala4), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 0);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 320f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/FN P90 fuego", 0.9f, m_random.Float(-0.0001f, 0.00001f), eyePos, 12f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 0.8f, 35f);

							int newBulletNum = bulletNum - 1;
							int newData = P90Block.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_p90BlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.AUG:
					{
						int bulletNum = AUGBlock.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.01f, 0.01f, 0.05f);
							int projectileCount = 2; // Ráfaga de 2 balas
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala4), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 0);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 280f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/AUG fuego", 1f, m_random.Float(-0.0001f, 0.00001f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

							int newBulletNum = bulletNum - 1;
							int newData = AUGBlock.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_augBlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.G3:
					{
						int bulletNum = G3Block.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.009f, 0.009f, 0.04f);
							int projectileCount = 2;
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 290f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/FX05", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

							int newBulletNum = bulletNum - 1;
							int newData = G3Block.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_g3BlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.NewG3:
					{
						int bulletNum = NewG3Block.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.009f, 0.009f, 0.04f);
							int projectileCount = 2;
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala3), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 290f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/G3 fuego", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

							int newBulletNum = bulletNum - 1;
							int newData = NewG3Block.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_newG3BlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.Famas:
					{
						int bulletNum = FamasBlock.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.02f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 1.5f, 0.4f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 1.5f, 0.4f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 1.5f, 0.4f, false)
							};
							direction = Vector3.Normalize(direction + v * 1.5f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.012f, 0.012f, 0.04f);
							int projectileCount = 1;
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala4), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 450f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/FAMAS fuego", 1.2f, m_random.Float(-0.05f, 0.05f), eyePos, 12f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 0.8f, 35f);

							int newBulletNum = bulletNum - 1;
							int newData = FamasBlock.SetBulletNum(data, newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_famasBlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.SCAR:
					{
						int bulletNum = SCARBlock.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.022f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 1.2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.01f, 0.01f, 0.03f);
							int projectileCount = 1;
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala3), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 0);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 310f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/FN Scar fuego", 0.95f, m_random.Float(-0.0001f, 0.00001f), eyePos, 14f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 0.85f, 45f);

							int newBulletNum = bulletNum - 1;
							int newData = SCARBlock.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_scarBlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.M249:
					{
						int bulletNum = M249Block.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.04f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 1.5f, 0.4f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 1.5f, 0.4f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 1.5f, 0.4f, false)
							};
							direction = Vector3.Normalize(direction + v * 1.5f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));

							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala5), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							Vector3 randomSpread = m_random.Float(-0.01f, 0.01f) * right + m_random.Float(-0.01f, 0.01f) * up;
							Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 400f;
							Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
							if (projectile != null)
								projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;

							m_subsystemAudio.PlaySound("Audio/Armas/M249 fuego", 1.5f, m_random.Float(-0.05f, 0.05f), eyePos, 15f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1.5f, 50f);

							int newBulletNum = bulletNum - 1;
							int newData = M249Block.SetBulletNum(data, newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_m249BlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.MP5SSD:
					{
						int bulletNum = MP5SSDBlock.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.009f, 0.009f, 0.04f);
							int projectileCount = 2;
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala3), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 290f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/MP5SSD fuego", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

							int newBulletNum = bulletNum - 1;
							int newData = MP5SSDBlock.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_mp5ssdBlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.FlameThrower:
					{
						if (FlameThrowerBlock.GetLoadState(data) == FlameThrowerBlock.LoadState.Loaded &&
							FlameThrowerBlock.GetSwitchState(data))
						{
							FlameBulletBlock.FlameBulletType bulletType = FlameThrowerBlock.GetBulletType(data) ?? FlameBulletBlock.FlameBulletType.Flame;
							int loadCount = FlameThrowerBlock.GetLoadCount(value);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.02f, 0.02f, 0f);

							Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
												  m_random.Float(-spread.Y, spread.Y) * up +
												  m_random.Float(-spread.Z, spread.Z) * direction;

							Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 60f;

							int bulletValue = Terrain.MakeBlockValue(m_flameBulletBlockIndex, 0,
								FlameBulletBlock.SetBulletType(0, bulletType));

							Projectile projectile = m_subsystemProjectiles.FireProjectile(bulletValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
							if (projectile != null)
							{
								projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							if (bulletType == FlameBulletBlock.FlameBulletType.Flame)
							{
								m_subsystemAudio.PlaySound("Audio/Flamethrower/Flamethrower Fire", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, true);
								m_subsystemParticles.AddParticleSystem(new FlameSmokeParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							}
							else
							{
								m_subsystemAudio.PlaySound("Audio/Flamethrower/PoisonSmoke", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 8f, true);
								m_subsystemParticles.AddParticleSystem(new PoisonSmokeParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							}

							m_subsystemNoise.MakeNoise(eyePos, 1f, 20f);
							m_componentCreature.ComponentBody.ApplyImpulse(-2f * direction);

							int newValue;
							if (loadCount > 1)
							{
								newValue = Terrain.MakeBlockValue(m_flameThrowerBlockIndex, loadCount - 1,
									FlameThrowerBlock.SetSwitchState(
										FlameThrowerBlock.SetBulletType(data, bulletType), true));
							}
							else
							{
								newValue = Terrain.ReplaceData(value,
									FlameThrowerBlock.SetLoadState(
										FlameThrowerBlock.SetSwitchState(
											FlameThrowerBlock.SetBulletType(data, null), false),
										FlameThrowerBlock.LoadState.Empty));
							}
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);

							m_currentWeapon.Value = newValue;
						}
						break;
					}

				case WeaponType.M4:
					{
						int bulletNum = M4Block.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.008f, 0.008f, 0.04f);
							int projectileCount = 3;
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala2), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 300f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/M4 fuego", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

							int newBulletNum = bulletNum - 1;
							int newData = M4Block.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_m4BlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}

				case WeaponType.AK:
					{
						int bulletNum = AKBlock.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.01f, 0.01f, 0.05f);
							int projectileCount = 2;
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala2), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 280f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/ak 47 fuego", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

							int newBulletNum = bulletNum - 1;
							int newData = AKBlock.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_akBlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}

				case WeaponType.Revolver:
					{
						int bulletNum = RevolverBlock.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							float spreadAngle = 0.08f;
							Vector3 randomSpread = m_random.Float(-spreadAngle, spreadAngle) * up + m_random.Float(-spreadAngle, spreadAngle) * right;
							Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 320f;

							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala4), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);
							Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
							if (projectile != null)
								projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;

							m_subsystemAudio.PlaySound("Audio/Armas/Revolver fuego", 1.5f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

							int newBulletNum = bulletNum - 1;
							int newData = RevolverBlock.SetBulletNum(data, newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_revolverBlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}

				case WeaponType.Sniper:
					{
						int bulletNum = SniperBlock.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.015f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 1.5f, 2, 1.5f, 0.3f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 1.5f, 2, 1.5f, 0.3f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 1.5f, 2, 1.5f, 0.3f, false)
							};
							direction = Vector3.Normalize(direction + v * 0.5f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							float spreadAngle = 0.001f;
							Vector3 randomSpread = m_random.Float(-spreadAngle, spreadAngle) * up + m_random.Float(-spreadAngle, spreadAngle) * right;
							Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 450f;

							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala6), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 180);
							Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
							if (projectile != null)
								projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;

							m_subsystemAudio.PlaySound("Audio/Armas/Sniper fuego", 1.8f, m_random.Float(-0.05f, 0.05f), eyePos, 15f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.4f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1.5f, 80f);

							int newBulletNum = bulletNum - 1;
							int newData = SniperBlock.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_sniperBlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}

				// Nuevas armas
				case WeaponType.BK43:
					{
						int bulletNum = BK43Block.GetBulletNum(data);
						if (bulletNum > 0)
						{
							// Disparo de escopeta: 8 perdigones
							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.1f, 0.1f, 0.03f); // Dispersión similar a la original
							int projectileCount = 8;
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala3), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 0); // Datos 0 como en el subsystem

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 300f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/bk 43", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 15f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1.5f, 50f);

							int newBulletNum = bulletNum - 1;
							int newData = BK43Block.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_bk43BlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}

				case WeaponType.Mac10:
					{
						int bulletNum = Mac10Block.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.012f, 0.012f, 0.035f);
							int projectileCount = 1; // Un proyectil por disparo
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala3), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 300f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/mac 10 fuego", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 0.8f, 30f);

							int newBulletNum = bulletNum - 1;
							int newData = Mac10Block.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_mac10BlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}

				case WeaponType.SWM500:
					{
						int bulletNum = SWM500Block.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							float spreadAngle = 0.08f; // Similar a revólver
							Vector3 randomSpread = m_random.Float(-spreadAngle, spreadAngle) * up + m_random.Float(-spreadAngle, spreadAngle) * right;
							Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 320f;

							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala4), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);
							Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
							if (projectile != null)
								projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;

							m_subsystemAudio.PlaySound("Audio/Armas/desert eagle fuego", 1.5f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

							int newBulletNum = bulletNum - 1;
							int newData = SWM500Block.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_swm500BlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}

				case WeaponType.KA:
					{
						int bulletNum = KABlock.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.007f, 0.007f, 0.03f);
							int projectileCount = 3; // Ráfaga de 3 balas
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala5), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 0);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 320f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/KA fuego", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 35f);

							int newBulletNum = bulletNum - 1;
							int newData = KABlock.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_kaBlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.Uzi:
					{
						int bulletNum = UziBlock.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.015f, 0.015f, 0.06f);
							int projectileCount = 2; // Ráfaga de 2 balas por disparo
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala2), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 320f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/Uzi fuego", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

							int newBulletNum = bulletNum - 1;
							int newData = UziBlock.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_uziBlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.Mendoza:
					{
						int bulletNum = MendozaBlock.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.009f, 0.009f, 0.04f);
							int projectileCount = 2;
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala3), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 290f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/Mendoza fuego", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

							int newBulletNum = bulletNum - 1;
							int newData = MendozaBlock.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_mendozaBlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.Groza:
					{
						int bulletNum = GrozaBlock.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.009f, 0.009f, 0.04f);
							int projectileCount = 2;
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala3), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 290f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/Groza fuego", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

							int newBulletNum = bulletNum - 1;
							int newData = GrozaBlock.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_grozaBlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.Izh43:
					{
						int bulletNum = Izh43Block.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.03f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
							};
							direction = Vector3.Normalize(direction + v * 2f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.09f, 0.09f, 0.03f);
							int projectileCount = 8;
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 280f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/shotgun fuego", 1.5f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

							int newBulletNum = bulletNum - 1;
							int newData = Izh43Block.SetBulletNum(newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_izh43BlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
				case WeaponType.AA12:
					{
						int bulletNum = AA12Block.GetBulletNum(data);
						if (bulletNum > 0)
						{
							float num4 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
							Vector3 v = 0.04f * new Vector3
							{
								X = SimplexNoise.OctavedNoise(num4, 2f, 3, 1.5f, 0.4f, false),
								Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 1.5f, 0.4f, false),
								Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 1.5f, 0.4f, false)
							};
							direction = Vector3.Normalize(direction + v * 1.5f);

							Vector3 right = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(direction, right));
							Vector3 spread = new Vector3(0.03f, 0.03f, 0.06f);
							int projectileCount = 8; // 8 perdigones por disparo
							int projectileBlockIndex = BlocksManager.GetBlockIndex(typeof(NuevaBala6), true, false);
							int projectileValue = Terrain.MakeBlockValue(projectileBlockIndex, 0, 2);

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 randomSpread = m_random.Float(-spread.X, spread.X) * right +
													   m_random.Float(-spread.Y, spread.Y) * up +
													   m_random.Float(-spread.Z, spread.Z) * direction;
								Vector3 velocity = m_componentCreature.ComponentBody.Velocity + (direction + randomSpread) * 350f;
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, eyePos, velocity, Vector3.Zero, m_componentCreature);
								if (projectile != null)
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/Armas/AA12 fuego", 1.5f, m_random.Float(-0.05f, 0.05f), eyePos, 15f, false);
							m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(m_subsystemTerrain, eyePos + 0.3f * direction, direction), false);
							m_subsystemNoise.MakeNoise(eyePos, 1.2f, 45f);

							int newBulletNum = bulletNum - 1;
							int newData = AA12Block.SetBulletNum(data, newBulletNum);
							int newValue = Terrain.MakeBlockValue(m_aa12BlockIndex, 0, newData);
							m_componentInventory.RemoveSlotItems(slot, 1);
							m_componentInventory.AddSlotItems(slot, newValue, 1);
							m_currentWeapon.Value = newValue;

							if (newBulletNum == 0)
							{
								m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), m_componentCreature.ComponentBody.Position, 5f, false);
								m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, m_componentCreature.ComponentBody.Position, 1f), false);
							}
						}
						break;
					}
			}

			RefreshCurrentWeaponReadyState();
		}

		private float GetItemsLauncherSpeed(int speedLevel)
		{
			switch (speedLevel)
			{
				case 1: return 10f;
				case 2: return 35f;
				case 3: return 60f;
				default: return 35f;
			}
		}

		private Vector3 GetArrowSpread(ArrowBlock.ArrowType arrowType)
		{
			switch (arrowType)
			{
				case ArrowBlock.ArrowType.WoodenArrow:
					return new Vector3(0.025f, 0.025f, 0.025f);
				case ArrowBlock.ArrowType.StoneArrow:
					return new Vector3(0.01f, 0.01f, 0.01f);
				case ArrowBlock.ArrowType.CopperArrow:
				case ArrowBlock.ArrowType.IronArrow:
				case ArrowBlock.ArrowType.DiamondArrow:
					return new Vector3(0.005f, 0.005f, 0.005f);
				case ArrowBlock.ArrowType.FireArrow:
					return new Vector3(0.02f, 0.02f, 0.02f);
				default:
					return Vector3.Zero;
			}
		}

		private Vector3 GetBoltSpread(ArrowBlock.ArrowType boltType)
		{
			switch (boltType)
			{
				case ArrowBlock.ArrowType.IronBolt:
					return new Vector3(0.02f, 0.02f, 0.02f);
				case ArrowBlock.ArrowType.DiamondBolt:
					return new Vector3(0.01f, 0.01f, 0.01f);
				case ArrowBlock.ArrowType.ExplosiveBolt:
					return new Vector3(0.03f, 0.03f, 0.03f);
				default:
					return new Vector3(0.02f, 0.02f, 0.02f);
			}
		}

		private Vector3 GetRepeatBoltSpread(RepeatArrowBlock.ArrowType boltType)
		{
			switch (boltType)
			{
				case RepeatArrowBlock.ArrowType.CopperArrow:
					return new Vector3(0.02f, 0.02f, 0.02f);
				case RepeatArrowBlock.ArrowType.IronArrow:
					return new Vector3(0.015f, 0.015f, 0.015f);
				case RepeatArrowBlock.ArrowType.DiamondArrow:
					return new Vector3(0.01f, 0.01f, 0.01f);
				case RepeatArrowBlock.ArrowType.ExplosiveArrow:
					return new Vector3(0.03f, 0.03f, 0.03f);
				default:
					return new Vector3(0.02f, 0.02f, 0.02f);
			}
		}

		private void PerformMeleeAttack()
		{
			if (m_componentChase.Target == null || m_componentMiner == null) return;

			m_componentCreatureModel.AttackOrder = true;
			if (m_componentCreatureModel.IsAttackHitMoment)
			{
				Vector3 hitPoint;
				ComponentBody hitBody = GetHitBody(m_componentChase.Target.ComponentBody, out hitPoint);
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

			var result = m_componentMiner?.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			if (result != null && result.Value.Distance < 5f)
			{
				hitPoint = result.Value.HitPoint();
				return result.Value.ComponentBody;
			}

			hitPoint = default;
			return null;
		}

		private void ProactiveReloadCheck()
		{
			if (!CanUseInventory || m_componentInventory == null) return;

			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int value = m_componentInventory.GetSlotValue(i);
				if (value == 0) continue;

				Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
				int data = Terrain.ExtractData(value);
				WeaponType type = WeaponType.None;
				bool needsReload = false;

				if (block is BowBlock && BowBlock.GetArrowType(data) == null)
				{
					type = WeaponType.Bow;
					needsReload = true;
				}
				else if (block is CrossbowBlock && (CrossbowBlock.GetArrowType(data) == null || CrossbowBlock.GetDraw(data) < 15))
				{
					type = WeaponType.Crossbow;
					needsReload = true;
				}
				else if (block is RepeatCrossbowBlock && (RepeatCrossbowBlock.GetArrowType(data) == null || RepeatCrossbowBlock.GetDraw(data) < 15))
				{
					type = WeaponType.RepeatCrossbow;
					needsReload = true;
				}
				else if (block is MusketBlock && MusketBlock.GetLoadState(data) != MusketBlock.LoadState.Loaded)
				{
					type = WeaponType.Musket;
					needsReload = true;
				}
				else if (block is FlameThrowerBlock && (FlameThrowerBlock.GetLoadState(data) != FlameThrowerBlock.LoadState.Loaded || FlameThrowerBlock.GetLoadCount(value) == 0))
				{
					type = WeaponType.FlameThrower;
					needsReload = true;
				}
				else if (block is ItemsLauncherBlock && ItemsLauncherBlock.GetFuel(data) == 0)
				{
					type = WeaponType.ItemsLauncher;
					needsReload = true;
				}
				else if (block is M4Block && M4Block.GetBulletNum(data) == 0)
				{
					type = WeaponType.M4;
					needsReload = true;
				}
				else if (block is AKBlock && AKBlock.GetBulletNum(data) == 0)
				{
					type = WeaponType.AK;
					needsReload = true;
				}
				else if (block is RevolverBlock && RevolverBlock.GetBulletNum(data) == 0)
				{
					type = WeaponType.Revolver;
					needsReload = true;
				}
				else if (block is SniperBlock && SniperBlock.GetBulletNum(data) == 0)
				{
					type = WeaponType.Sniper;
					needsReload = true;
				}
				// Nuevas armas
				else if (block is BK43Block && BK43Block.GetBulletNum(data) == 0)
				{
					type = WeaponType.BK43;
					needsReload = true;
				}
				else if (block is Mac10Block && Mac10Block.GetBulletNum(data) == 0)
				{
					type = WeaponType.Mac10;
					needsReload = true;
				}
				else if (block is SWM500Block && SWM500Block.GetBulletNum(data) == 0)
				{
					type = WeaponType.SWM500;
					needsReload = true;
				}
				else if (block is KABlock && KABlock.GetBulletNum(data) == 0)
				{
					type = WeaponType.KA;
					needsReload = true;
				}
				else if (block is SPAS12Block && SPAS12Block.GetBulletNum(data) == 0)
				{
					type = WeaponType.SPAS12;
					needsReload = true;
				}
				else if (block is MinigunBlock && MinigunBlock.GetBulletNum(data) == 0)
				{
					type = WeaponType.Minigun;
					needsReload = true;
				}
				else if (block is P90Block && P90Block.GetBulletNum(data) == 0)
				{
					type = WeaponType.P90;
					needsReload = true;
				}
				else if (block is AUGBlock && AUGBlock.GetBulletNum(data) == 0)
				{
					type = WeaponType.AUG;
					needsReload = true;
				}
				else if (block is UziBlock && UziBlock.GetBulletNum(data) == 0)
{
    type = WeaponType.Uzi;
    needsReload = true;
}
else if (block is MendozaBlock && MendozaBlock.GetBulletNum(data) == 0)
{
    type = WeaponType.Mendoza;
    needsReload = true;
}
else if (block is GrozaBlock && GrozaBlock.GetBulletNum(data) == 0)
{
    type = WeaponType.Groza;
    needsReload = true;
}
else if (block is Izh43Block && Izh43Block.GetBulletNum(data) == 0)
{
    type = WeaponType.Izh43;
    needsReload = true;
}
else if (block is AA12Block && AA12Block.GetBulletNum(Terrain.ExtractData(value)) == 0)
{
    type = WeaponType.AA12;
    needsReload = true;
}
				else if (block is G3Block && G3Block.GetBulletNum(data) == 0)
				{
					type = WeaponType.G3;
					needsReload = true;
				}
				else if (block is NewG3Block && NewG3Block.GetBulletNum(data) == 0)
				{
					type = WeaponType.NewG3;
					needsReload = true;
				}
				else if (block is FamasBlock && FamasBlock.GetBulletNum(Terrain.ExtractData(value)) == 0)
				{
					type = WeaponType.Famas;
					needsReload = true;
				}
				else if (block is SCARBlock && SCARBlock.GetBulletNum(data) == 0)
				{
					type = WeaponType.SCAR;
					needsReload = true;
				}
				else if (block is M249Block && M249Block.GetBulletNum(Terrain.ExtractData(value)) == 0)
				{
					type = WeaponType.M249;
					needsReload = true;
				}
				else if (block is MP5SSDBlock && MP5SSDBlock.GetBulletNum(data) == 0)
				{
					type = WeaponType.MP5SSD;
					needsReload = true;
				}
				if (needsReload)
				{
					m_currentWeapon = new WeaponInfo { Slot = i, Value = value, Type = type, IsReady = false };
					m_componentInventory.ActiveSlotIndex = i;
					TransitionToState("Reloading");
					break;
				}
			}
		}

		private RepeatArrowBlock.ArrowType GetRandomRepeatArrowType()
		{
			RepeatArrowBlock.ArrowType[] arrowTypes = new RepeatArrowBlock.ArrowType[]
			{
				RepeatArrowBlock.ArrowType.CopperArrow,
				RepeatArrowBlock.ArrowType.IronArrow,
				RepeatArrowBlock.ArrowType.DiamondArrow
			};
			return arrowTypes[m_random.Int(0, arrowTypes.Length - 1)];
		}

		private RepeatArrowBlock.ArrowType GetRandomRepeatBoltType()
		{
			RepeatArrowBlock.ArrowType[] boltTypes = new RepeatArrowBlock.ArrowType[]
			{
				RepeatArrowBlock.ArrowType.CopperArrow,
				RepeatArrowBlock.ArrowType.IronArrow,
				RepeatArrowBlock.ArrowType.DiamondArrow,
				RepeatArrowBlock.ArrowType.ExplosiveArrow
			};
			return boltTypes[m_random.Int(0, boltTypes.Length - 1)];
		}

		private FlameBulletBlock.FlameBulletType GetRandomFlameBulletType()
		{
			FlameBulletBlock.FlameBulletType[] bulletTypes = new FlameBulletBlock.FlameBulletType[]
			{
				FlameBulletBlock.FlameBulletType.Flame,
				FlameBulletBlock.FlameBulletType.Poison
			};
			return bulletTypes[m_random.Int(0, bulletTypes.Length - 1)];
		}

		private ArrowBlock.ArrowType GetRandomArrowType()
		{
			ArrowBlock.ArrowType[] arrowTypes = new ArrowBlock.ArrowType[]
			{
				ArrowBlock.ArrowType.WoodenArrow,
				ArrowBlock.ArrowType.StoneArrow,
				ArrowBlock.ArrowType.IronArrow,
				ArrowBlock.ArrowType.CopperArrow,
				ArrowBlock.ArrowType.DiamondArrow,
				ArrowBlock.ArrowType.FireArrow
			};
			return arrowTypes[m_random.Int(0, arrowTypes.Length - 1)];
		}

		private ArrowBlock.ArrowType GetRandomBoltType()
		{
			ArrowBlock.ArrowType[] boltTypes = new ArrowBlock.ArrowType[]
			{
				ArrowBlock.ArrowType.IronBolt,
				ArrowBlock.ArrowType.DiamondBolt,
				ArrowBlock.ArrowType.ExplosiveBolt
			};
			return boltTypes[m_random.Int(0, boltTypes.Length - 1)];
		}

		private BulletBlock.BulletType GetRandomBulletType()
		{
			BulletBlock.BulletType[] bulletTypes = new BulletBlock.BulletType[]
			{
				BulletBlock.BulletType.MusketBall,
				BulletBlock.BulletType.Buckshot,
				BulletBlock.BulletType.BuckshotBall
			};
			return bulletTypes[m_random.Int(0, bulletTypes.Length - 1)];
		}

		private void TryReloadWeapon(WeaponInfo weaponInfo)
		{
			if (!CanUseInventory || weaponInfo.Type == WeaponType.None ||
				weaponInfo.Type == WeaponType.Throwable || weaponInfo.Type == WeaponType.Melee)
				return;

			int slot = weaponInfo.Slot;
			int value = m_componentInventory.GetSlotValue(slot);
			int data = Terrain.ExtractData(value);
			int newValue = value;

			switch (weaponInfo.Type)
			{
				case WeaponType.Bow:
					ArrowBlock.ArrowType arrowType = GetRandomArrowType();
					newValue = Terrain.ReplaceData(value, BowBlock.SetArrowType(data, arrowType));
					break;

				case WeaponType.Crossbow:
					ArrowBlock.ArrowType boltType = GetRandomBoltType();
					newValue = Terrain.ReplaceData(value,
						CrossbowBlock.SetDraw(CrossbowBlock.SetArrowType(data, boltType), 15));
					break;

				case WeaponType.RepeatCrossbow:
					RepeatArrowBlock.ArrowType repeatBoltType = GetRandomRepeatBoltType();
					newValue = Terrain.ReplaceData(value,
						RepeatCrossbowBlock.SetDraw(RepeatCrossbowBlock.SetArrowType(data, repeatBoltType), 15));
					newValue = RepeatCrossbowBlock.SetLoadCount(newValue, 8);
					break;

				case WeaponType.Musket:
					BulletBlock.BulletType bulletType = GetRandomBulletType();
					newValue = Terrain.ReplaceData(value,
						MusketBlock.SetBulletType(MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded), bulletType));
					break;

				case WeaponType.FlameThrower:
					FlameBulletBlock.FlameBulletType flameBulletType = GetRandomFlameBulletType();
					newValue = Terrain.ReplaceData(value,
						FlameThrowerBlock.SetLoadState(
							FlameThrowerBlock.SetBulletType(data, flameBulletType),
							FlameThrowerBlock.LoadState.Loaded));
					newValue = FlameThrowerBlock.SetLoadCount(newValue, 15);
					break;

				case WeaponType.ItemsLauncher:
					newValue = Terrain.ReplaceData(value, ItemsLauncherBlock.SetFuel(data, 15));
					break;

				case WeaponType.M4:
					newValue = Terrain.MakeBlockValue(m_m4BlockIndex, 0, M4Block.SetBulletNum(22));
					break;

				case WeaponType.AK:
					newValue = Terrain.MakeBlockValue(m_akBlockIndex, 0, AKBlock.SetBulletNum(30));
					break;

				case WeaponType.Revolver:
					int revolverData = RevolverBlock.SetBulletNum(data, 6);
					newValue = Terrain.MakeBlockValue(m_revolverBlockIndex, 0, revolverData);
					break;

				case WeaponType.Sniper:
					newValue = Terrain.MakeBlockValue(m_sniperBlockIndex, 0, SniperBlock.SetBulletNum(1));
					break;

				// Nuevas armas
				case WeaponType.BK43:
					newValue = Terrain.MakeBlockValue(m_bk43BlockIndex, 0, BK43Block.SetBulletNum(2));
					break;

				case WeaponType.Mac10:
					newValue = Terrain.MakeBlockValue(m_mac10BlockIndex, 0, Mac10Block.SetBulletNum(30));
					break;

				case WeaponType.SWM500:
					newValue = Terrain.MakeBlockValue(m_swm500BlockIndex, 0, SWM500Block.SetBulletNum(8));
					break;

				case WeaponType.KA:
					newValue = Terrain.MakeBlockValue(m_kaBlockIndex, 0, KABlock.SetBulletNum(40));
					break;
				case WeaponType.SPAS12:
					newValue = Terrain.MakeBlockValue(m_spas12BlockIndex, 0, SPAS12Block.SetBulletNum(8));
					break;

				case WeaponType.Minigun:
					newValue = Terrain.MakeBlockValue(m_minigunBlockIndex, 0, MinigunBlock.SetBulletNum(100));
					break;

				case WeaponType.P90:
					newValue = Terrain.MakeBlockValue(m_p90BlockIndex, 0, P90Block.SetBulletNum(50));
					break;

				case WeaponType.AUG:
					newValue = Terrain.MakeBlockValue(m_augBlockIndex, 0, AUGBlock.SetBulletNum(30));
					break;
				case WeaponType.Uzi:
					newValue = Terrain.MakeBlockValue(m_uziBlockIndex, 0, UziBlock.SetBulletNum(30));
					break;

				case WeaponType.Mendoza:
					newValue = Terrain.MakeBlockValue(m_mendozaBlockIndex, 0, MendozaBlock.SetBulletNum(30));
					break;

				case WeaponType.Groza:
					newValue = Terrain.MakeBlockValue(m_grozaBlockIndex, 0, GrozaBlock.SetBulletNum(30));
					break;

				case WeaponType.Izh43:
					newValue = Terrain.MakeBlockValue(m_izh43BlockIndex, 0, Izh43Block.SetBulletNum(2));
					break;

				case WeaponType.AA12:
					int aa12Data = Terrain.ExtractData(value);
					int newAa12Data = AA12Block.SetBulletNum(aa12Data, 20);
					newValue = Terrain.MakeBlockValue(m_aa12BlockIndex, 0, newAa12Data);
					break;
				case WeaponType.G3:
					newValue = Terrain.MakeBlockValue(m_g3BlockIndex, 0, G3Block.SetBulletNum(30));
					break;

				case WeaponType.NewG3:
					newValue = Terrain.MakeBlockValue(m_newG3BlockIndex, 0, NewG3Block.SetBulletNum(30));
					break;

				case WeaponType.Famas:
					int famasData = Terrain.ExtractData(value);
					int newFamasData = FamasBlock.SetBulletNum(famasData, 30);
					newValue = Terrain.MakeBlockValue(m_famasBlockIndex, 0, newFamasData);
					break;

				case WeaponType.SCAR:
					newValue = Terrain.MakeBlockValue(m_scarBlockIndex, 0, SCARBlock.SetBulletNum(30));
					break;

				case WeaponType.M249:
					int m249Data = Terrain.ExtractData(value);
					int newM249Data = M249Block.SetBulletNum(m249Data, 100);
					newValue = Terrain.MakeBlockValue(m_m249BlockIndex, 0, newM249Data);
					break;

				case WeaponType.MP5SSD:
					newValue = Terrain.MakeBlockValue(m_mp5ssdBlockIndex, 0, MP5SSDBlock.SetBulletNum(30));
					break;
			}
			if (newValue != value)
			{
				m_componentInventory.RemoveSlotItems(slot, 1);
				m_componentInventory.AddSlotItems(slot, newValue, 1);
				m_currentWeapon.IsReady = true;
			}
		}
	}
}
