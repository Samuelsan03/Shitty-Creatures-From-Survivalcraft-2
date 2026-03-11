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

		private StateMachine m_stateMachine = new StateMachine();
		private string m_currentStateName;
		private double m_nextCombatUpdateTime;
		private double m_nextProactiveReloadTime;
		private double m_aimStartTime;
		private float m_aimDuration;
		private double m_reloadStartTime;
		private float m_reloadDuration;
		private double m_nextFlameThrowerShotTime;
		private double m_fireEndTime;
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
			ItemsLauncher
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

			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", true);

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
			if (m_currentWeapon.Type == WeaponType.FlameThrower)
			{
				int loadCount = FlameThrowerBlock.GetLoadCount(m_currentWeapon.Value);
				if (loadCount > 0)
				{
					m_nextFlameThrowerShotTime = m_subsystemTime.GameTime + 0.15;
					m_fireEndTime = double.MaxValue;
				}
				else
				{
					m_fireEndTime = m_subsystemTime.GameTime + 0.2;
				}
			}
			else
			{
				m_fireEndTime = m_subsystemTime.GameTime + 0.2;
			}
		}

		private void Firing_Update()
		{
			if (m_currentWeapon.Type == WeaponType.FlameThrower)
			{
				// Mantener la mira en el objetivo y la postura de apuntado
				if (m_componentChase.Target == null)
				{
					TransitionToState("Idle");
					return;
				}
				m_componentCreatureModel.LookAtOrder = new Vector3?(m_componentChase.Target.ComponentCreatureModel.EyePosition);
				ApplyAimingAnimation(); // Conservar la animación de apuntado

				int loadCount = FlameThrowerBlock.GetLoadCount(m_currentWeapon.Value);
				if (loadCount > 0 && m_subsystemTime.GameTime >= m_nextFlameThrowerShotTime)
				{
					PerformFire();
					ApplyRecoilAnimation(); // Retroceso solo al disparar
					int newLoadCount = FlameThrowerBlock.GetLoadCount(m_currentWeapon.Value);
					if (newLoadCount > 0)
					{
						m_nextFlameThrowerShotTime = m_subsystemTime.GameTime + 0.15;
					}
					else
					{
						m_nextCombatUpdateTime = m_subsystemTime.GameTime + m_random.Float(1.5f, 2.5f);
						TransitionToState("Reloading");
					}
				}
				else if (loadCount == 0)
				{
					m_nextCombatUpdateTime = m_subsystemTime.GameTime + m_random.Float(1.5f, 2.5f);
					TransitionToState("Reloading");
				}
			}
			else
			{
				ApplyRecoilAnimation();
				if (m_subsystemTime.GameTime >= m_fireEndTime)
				{
					m_nextCombatUpdateTime = m_subsystemTime.GameTime + m_random.Float(1.5f, 2.5f);
					TransitionToState("Reloading");
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
				TransitionToState("Idle");
			}
		}

		private void Reloading_Leave()
		{
			m_componentCreatureModel.AimHandAngleOrder = 0f;
			m_componentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
			m_componentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
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
			switch (type)
			{
				case WeaponType.Throwable: return distance >= 3f && distance <= 15f;
				case WeaponType.Bow: return distance >= 5f && distance <= 30f;
				case WeaponType.Crossbow: return distance >= 5f && distance <= 35f;
				case WeaponType.RepeatCrossbow: return distance >= 5f && distance <= 35f;
				case WeaponType.Musket: return distance >= 5f && distance <= 40f;
				case WeaponType.FlameThrower: return distance >= 3f && distance <= 20f;
				case WeaponType.ItemsLauncher: return distance >= 5f && distance <= 50f;
				default: return false;
			}
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
				default: return 1.0f;
			}
		}

		private float GetReloadDurationForWeapon(WeaponType type)
		{
			switch (type)
			{
				case WeaponType.Bow: return 1.0f;
				case WeaponType.Crossbow: return 1.5f;
				case WeaponType.RepeatCrossbow: return 1.5f;
				case WeaponType.Musket: return 2.0f;
				case WeaponType.FlameThrower: return 1.0f;
				case WeaponType.ItemsLauncher: return 1.0f;
				default: return 0.5f;
			}
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
					m_componentCreatureModel.AimHandAngleOrder = 1.4f;
					m_componentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					m_componentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
					break;
				case WeaponType.FlameThrower:
					m_componentCreatureModel.AimHandAngleOrder = 1.4f;
					m_componentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					m_componentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
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

				case WeaponType.FlameThrower:
					{
						if (FlameThrowerBlock.GetLoadState(data) == FlameThrowerBlock.LoadState.Loaded &&
							FlameThrowerBlock.GetSwitchState(data))
						{
							FlameBulletBlock.FlameBulletType bulletType = FlameThrowerBlock.GetBulletType(data) ?? FlameBulletBlock.FlameBulletType.Flame;
							int loadCount = FlameThrowerBlock.GetLoadCount(value);

							// Calcular dirección y spread
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

							// ACTUALIZAR EL VALOR EN m_currentWeapon PARA LOS SIGUIENTES DISPAROS
							m_currentWeapon.Value = newValue;
						}
						break;
					}
			}
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
