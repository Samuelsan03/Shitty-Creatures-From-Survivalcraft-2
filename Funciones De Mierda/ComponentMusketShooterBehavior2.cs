using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentMusketShooterBehavior2 : ComponentBehavior, IUpdateable
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
		private SubsystemTerrain m_subsystemTerrain;
		private ComponentMiner m_componentMiner;
		private SubsystemBodies m_subsystemBodies;
		private ComponentPathfinding m_componentPathfinding;

		// Configuración
		public float MaxDistance = 25f;
		public float MeleeSwitchDistance = 5f;
		public float ReloadTime = 0.55f;
		public float AimTime = 1f;
		public float CockTime = 0.5f;
		public float MeleeAttackTime = 0.8f;
		public float FireSoundDistance = 15f;
		public float Accuracy = 0.02f;
		public bool UseRecoil = true;
		public float BulletSpeed = 120f;
		public bool RequireCocking = true;

		// Estado de animación
		private bool m_isAiming = false;
		private bool m_isFiring = false;
		private bool m_isReloading = false;
		private bool m_isCocking = false;
		private bool m_isMelee = false;
		private double m_animationStartTime;
		private double m_cockStartTime;
		private double m_fireTime;
		private double m_meleeAttackTime;
		private int m_musketSlot = -1;
		private Random m_random = new Random();

		// UpdateOrder
		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 25f);
			MeleeSwitchDistance = valuesDictionary.GetValue<float>("MeleeSwitchDistance", 5f);
			ReloadTime = valuesDictionary.GetValue<float>("ReloadTime", 0.55f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 1f);
			CockTime = valuesDictionary.GetValue<float>("CockTime", 0.5f);
			MeleeAttackTime = valuesDictionary.GetValue<float>("MeleeAttackTime", 0.8f);
			FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 15f);
			Accuracy = valuesDictionary.GetValue<float>("Accuracy", 0.02f);
			UseRecoil = valuesDictionary.GetValue<bool>("UseRecoil", true);
			BulletSpeed = valuesDictionary.GetValue<float>("BulletSpeed", 120f);
			RequireCocking = valuesDictionary.GetValue<bool>("RequireCocking", true);

			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
		}

		public void Update(float dt)
		{
			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			if (m_componentChaseBehavior.Target == null)
			{
				ResetAnimations();
				return;
			}

			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				m_componentChaseBehavior.Target.ComponentBody.Position
			);

			// SOLO VERIFICAR DISTANCIA MÁXIMA
			if (distance <= MaxDistance)
			{
				if (distance <= MeleeSwitchDistance)
				{
					if (!m_isMelee && !m_isFiring && !m_isReloading)
					{
						SwitchToMeleeModeImmediately();
					}
				}
				else
				{
					if (m_isMelee)
					{
						m_isMelee = false;
						m_componentModel.AttackOrder = false;
					}

					if (!m_isAiming && !m_isFiring && !m_isReloading && !m_isCocking)
					{
						StartAiming();
					}
				}
			}
			else
			{
				ResetAnimations();
				return;
			}

			// MIRAR AL OBJETIVO SIEMPRE
			if (!m_isMelee && !m_isReloading && m_componentModel != null && m_componentChaseBehavior.Target != null)
			{
				m_componentModel.LookAtOrder = new Vector3?(
					m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
				);
			}

			if (m_isMelee)
			{
				UpdateMeleeModeImproved(dt);
			}
			else
			{
				UpdateRangedMode(dt);
			}
		}

		private void UpdateRangedMode(float dt)
		{
			if (m_isCocking)
			{
				ApplyCockingAnimation(dt);

				if (m_subsystemTime.GameTime - m_cockStartTime >= CockTime)
				{
					m_isCocking = false;
					m_isAiming = true;
					m_animationStartTime = m_subsystemTime.GameTime;

					UpdateMusketHammerState(true);
				}
			}
			else if (m_isAiming)
			{
				ApplyAimingAnimation(dt);

				if (m_subsystemTime.GameTime - m_animationStartTime >= AimTime)
				{
					Fire();
				}
			}
			else if (m_isFiring)
			{
				ApplyFiringAnimation(dt);

				if (m_subsystemTime.GameTime - m_fireTime >= 0.2)
				{
					m_isFiring = false;
					StartReloading();
				}
			}
			else if (m_isReloading)
			{
				ApplyReloadingAnimation(dt);

				if (m_subsystemTime.GameTime - m_animationStartTime >= ReloadTime)
				{
					m_isReloading = false;

					UpdateMusketLoadState(MusketBlock.LoadState.Loaded);
					UpdateMusketBulletType(BulletBlock.BulletType.MusketBall);

					StartAiming();
				}
			}
		}

		private void UpdateMeleeModeImproved(float dt)
		{
			if (FindHitToolImproved())
			{
				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}

				if (m_subsystemTime.GameTime - m_meleeAttackTime >= MeleeAttackTime)
				{
					m_componentModel.AttackOrder = true;
					m_meleeAttackTime = m_subsystemTime.GameTime;

					if (m_componentModel.IsAttackHitMoment)
					{
						Vector3 hitPoint;
						ComponentBody hitBody = GetHitBodyImproved(m_componentChaseBehavior.Target.ComponentBody, out hitPoint);
						if (hitBody != null)
						{
							m_componentMiner.Hit(hitBody, hitPoint, m_componentCreature.ComponentBody.Matrix.Forward);
							m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						}
					}
				}
			}
			else
			{
				if (m_componentPathfinding != null && m_componentChaseBehavior.Target != null)
				{
					Vector3 retreatDirection = Vector3.Normalize(
						m_componentCreature.ComponentBody.Position - m_componentChaseBehavior.Target.ComponentBody.Position
					);
					Vector3 retreatPosition = m_componentCreature.ComponentBody.Position + retreatDirection * 3f;
					m_componentPathfinding.SetDestination(new Vector3?(retreatPosition), 1f, 1f, 0, false, true, false, null);
				}
			}
		}

		private void SwitchToMeleeModeImmediately()
		{
			m_isMelee = true;
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = false;
			m_isCocking = false;
			m_meleeAttackTime = m_subsystemTime.GameTime;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
			}

			FindHitToolImproved();
		}

		private bool FindHitToolImproved()
		{
			if (m_componentMiner.Inventory == null)
				return false;

			int activeBlockValue = m_componentMiner.ActiveBlockValue;
			if (BlocksManager.Blocks[Terrain.ExtractContents(activeBlockValue)].GetMeleePower(activeBlockValue) > 1f)
			{
				return true;
			}

			float bestPower = 1f;
			int bestSlot = -1;

			for (int i = 0; i < 6; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				float meleePower = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)].GetMeleePower(slotValue);
				if (meleePower > bestPower)
				{
					bestPower = meleePower;
					bestSlot = i;
				}
			}

			if (bestSlot >= 0)
			{
				m_componentMiner.Inventory.ActiveSlotIndex = bestSlot;
				return true;
			}

			return false;
		}

		private ComponentBody GetHitBodyImproved(ComponentBody target, out Vector3 hitPoint)
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

		private void StartCocking()
		{
			m_isCocking = true;
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = false;
			m_cockStartTime = m_subsystemTime.GameTime;

			m_subsystemAudio.PlaySound("Audio/HammerCock", 1f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);
		}

		private void StartAiming()
		{
			FindMusket();

			if (m_musketSlot == -1)
				return;

			int slotValue = m_componentInventory.GetSlotValue(m_musketSlot);
			int data = Terrain.ExtractData(slotValue);
			MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);

			if (loadState != MusketBlock.LoadState.Loaded)
			{
				UpdateMusketLoadState(MusketBlock.LoadState.Loaded);
				UpdateMusketBulletType(BulletBlock.BulletType.MusketBall);
			}

			m_isAiming = true;
			m_isFiring = false;
			m_isReloading = false;
			m_isCocking = false;
			m_animationStartTime = m_subsystemTime.GameTime;

			if (RequireCocking)
			{
				bool hammerState = MusketBlock.GetHammerState(Terrain.ExtractData(slotValue));
				if (!hammerState)
				{
					StartCocking();
				}
				else
				{
					m_isAiming = true;
					m_isCocking = false;
				}
			}
		}

		private void FindMusket()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					if (block is MusketBlock)
					{
						m_musketSlot = i;
						m_componentInventory.ActiveSlotIndex = i;
						break;
					}
				}
			}
		}

		private void UpdateMusketHammerState(bool hammerState)
		{
			if (m_musketSlot >= 0)
			{
				int slotValue = m_componentInventory.GetSlotValue(m_musketSlot);
				int contents = Terrain.ExtractContents(slotValue);
				if (BlocksManager.Blocks[contents] is MusketBlock)
				{
					int data = Terrain.ExtractData(slotValue);
					data = MusketBlock.SetHammerState(data, hammerState);
					int newValue = Terrain.ReplaceData(slotValue, data);
					m_componentInventory.RemoveSlotItems(m_musketSlot, 1);
					m_componentInventory.AddSlotItems(m_musketSlot, newValue, 1);
				}
			}
		}

		private void UpdateMusketLoadState(MusketBlock.LoadState loadState)
		{
			if (m_musketSlot >= 0)
			{
				int slotValue = m_componentInventory.GetSlotValue(m_musketSlot);
				int contents = Terrain.ExtractContents(slotValue);
				if (BlocksManager.Blocks[contents] is MusketBlock)
				{
					int data = Terrain.ExtractData(slotValue);
					data = MusketBlock.SetLoadState(data, loadState);
					int newValue = Terrain.ReplaceData(slotValue, data);
					m_componentInventory.RemoveSlotItems(m_musketSlot, 1);
					m_componentInventory.AddSlotItems(m_musketSlot, newValue, 1);
				}
			}
		}

		private void UpdateMusketBulletType(BulletBlock.BulletType bulletType)
		{
			if (m_musketSlot >= 0)
			{
				int slotValue = m_componentInventory.GetSlotValue(m_musketSlot);
				int contents = Terrain.ExtractContents(slotValue);
				if (BlocksManager.Blocks[contents] is MusketBlock)
				{
					int data = Terrain.ExtractData(slotValue);
					data = MusketBlock.SetBulletType(data, bulletType);
					int newValue = Terrain.ReplaceData(slotValue, data);
					m_componentInventory.RemoveSlotItems(m_musketSlot, 1);
					m_componentInventory.AddSlotItems(m_musketSlot, newValue, 1);
				}
			}
		}

		private void ApplyCockingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				float cockProgress = (float)((m_subsystemTime.GameTime - m_cockStartTime) / CockTime);

				m_componentModel.AimHandAngleOrder = 1.2f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.08f + (0.03f * cockProgress),
					-0.08f,
					0.07f
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.6f, 0f, 0f);
			}
		}

		private void ApplyAimingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
			}
		}

		private void Fire()
		{
			m_isAiming = false;
			m_isFiring = true;
			m_isReloading = false;
			m_isCocking = false;
			m_fireTime = m_subsystemTime.GameTime;

			if (m_musketSlot == -1)
			{
				FindMusket();
				if (m_musketSlot == -1)
					return;
			}

			int slotValue = m_componentInventory.GetSlotValue(m_musketSlot);
			int data = Terrain.ExtractData(slotValue);
			MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);

			if (loadState != MusketBlock.LoadState.Loaded)
			{
				m_subsystemAudio.PlaySound("Audio/MusketMisfire", 1f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 3f, false);
				m_isFiring = false;
				StartReloading();
				return;
			}

			float immersion = m_componentCreature.ComponentBody.ImmersionFactor;
			if (immersion > 0.4f)
			{
				m_subsystemAudio.PlaySound("Audio/MusketMisfire", 1f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 3f, false);

				UpdateMusketLoadState(MusketBlock.LoadState.Empty);
				UpdateMusketHammerState(false);
				UpdateMusketBulletType(BulletBlock.BulletType.MusketBall);

				m_isFiring = false;
				StartReloading();
				return;
			}

			m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, FireSoundDistance, false);

			m_subsystemAudio.PlaySound("Audio/HammerUncock", 1f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);

			ShootBullet();

			UpdateMusketLoadState(MusketBlock.LoadState.Empty);
			UpdateMusketHammerState(false);
			UpdateMusketBulletType(BulletBlock.BulletType.MusketBall);

			if (UseRecoil && m_componentChaseBehavior.Target != null)
			{
				Vector3 direction = Vector3.Normalize(
					m_componentChaseBehavior.Target.ComponentBody.Position -
					m_componentCreature.ComponentBody.Position
				);
				m_componentCreature.ComponentBody.ApplyImpulse(-direction * 3f);
			}
		}

		private void ApplyFiringAnimation(float dt)
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

		private void StartReloading()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = true;
			m_isCocking = false;
			m_animationStartTime = m_subsystemTime.GameTime;

			m_subsystemAudio.PlaySound("Audio/Reload", 1.5f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 5f, false);
		}

		private void ApplyReloadingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / ReloadTime);

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
			m_isCocking = false;
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

		private void ShootBullet()
		{
			if (m_componentChaseBehavior.Target == null)
				return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;

				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				direction += new Vector3(
					m_random.Float(-Accuracy, Accuracy),
					m_random.Float(-Accuracy * 0.5f, Accuracy * 0.5f),
					m_random.Float(-Accuracy, Accuracy)
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
						direction * BulletSpeed,
						Vector3.Zero,
						m_componentCreature
					);

					Vector3 smokePosition = firePosition + direction * 0.3f;

					if (m_subsystemParticles != null)
					{
						if (m_subsystemTerrain != null)
						{
							m_subsystemParticles.AddParticleSystem(
								new GunSmokeParticleSystem(m_subsystemTerrain, smokePosition, direction),
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
			catch { }
		}
	}
}
