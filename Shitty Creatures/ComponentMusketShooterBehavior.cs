using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentMusketShooterBehavior : ComponentBehavior, IUpdateable
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
		public float AimTime = 1f;
		public float MeleeAttackTime = 0.8f;
		public float FireSoundDistance = 15f;
		public bool UseRecoil = true;
		public float BulletSpeed = 120f;

		// Estado de animación
		private bool m_isAiming = false;
		private bool m_isFiring = false;
		private bool m_isMelee = false;
		private double m_animationStartTime;
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
			AimTime = valuesDictionary.GetValue<float>("AimTime", 1f);
			MeleeAttackTime = valuesDictionary.GetValue<float>("MeleeAttackTime", 0.8f);
			FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 15f);
			UseRecoil = valuesDictionary.GetValue<bool>("UseRecoil", true);
			BulletSpeed = valuesDictionary.GetValue<float>("BulletSpeed", 120f);

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

			if (distance <= MaxDistance)
			{
				if (distance < MeleeSwitchDistance)
				{
					if (!m_isMelee && !m_isFiring)
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

					if (!m_isAiming && !m_isFiring && !m_isMelee)
					{
						if (HasLineOfSightToTarget())
						{
							StartAiming();
						}
						else
						{
							MoveToGetLineOfSight();
						}
					}
				}
			}
			else
			{
				ResetAnimations();
				return;
			}

			if (!m_isMelee && m_componentModel != null && m_componentChaseBehavior.Target != null)
			{
				m_componentModel.LookAtOrder = new Vector3?(
					m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
				);
			}

			if (m_isMelee)
			{
				UpdateMeleeMode(dt);
			}
			else
			{
				UpdateRangedMode(dt);
			}
		}

		private void UpdateRangedMode(float dt)
		{
			if (m_isAiming)
			{
				ApplyAimingAnimation(dt);

				if (m_subsystemTime.GameTime - m_animationStartTime >= AimTime)
				{
					Fire();
				}
				else
				{
					UpdateAimState(AimState.InProgress);
				}
			}
			else if (m_isFiring)
			{
				ApplyFiringAnimation(dt);

				if (m_subsystemTime.GameTime - m_fireTime >= 0.2)
				{
					m_isFiring = false;
					m_isAiming = true;
					m_animationStartTime = m_subsystemTime.GameTime;
				}
			}
		}

		private void UpdateMeleeMode(float dt)
		{
			if (FindHitTool())
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
						ComponentBody hitBody = GetHitBody(m_componentChaseBehavior.Target.ComponentBody, out hitPoint);
						if (hitBody != null)
						{
							m_componentMiner.Hit(hitBody, hitPoint, m_componentCreature.ComponentBody.Matrix.Forward);
							m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						}
					}
				}
			}
		}

		private bool HasLineOfSightToTarget()
		{
			if (m_componentChaseBehavior.Target == null)
				return false;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetEyePos = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
			Vector3 direction = targetEyePos - eyePos;
			float distance = direction.Length();
			direction = Vector3.Normalize(direction);

			TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(eyePos, eyePos + direction * distance, false, true, (int value, float d) => true);

			return terrainHit == null;
		}

		private void MoveToGetLineOfSight()
		{
			if (m_componentPathfinding == null || m_componentChaseBehavior.Target == null)
				return;

			Vector3 targetPos = m_componentChaseBehavior.Target.ComponentBody.Position;
			Vector3 myPos = m_componentCreature.ComponentBody.Position;

			Vector3 directionToTarget = targetPos - myPos;
			directionToTarget.Y = 0;
			directionToTarget = Vector3.Normalize(directionToTarget);

			float angle = m_random.Float(-1.5f, 1.5f);
			Vector3 lateral = Vector3.Transform(directionToTarget, Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle));
			Vector3 targetDestination = targetPos + lateral * 3f;

			int groundY = m_subsystemTerrain.Terrain.CalculateTopmostCellHeight(Terrain.ToCell(targetDestination.X), Terrain.ToCell(targetDestination.Z));
			targetDestination.Y = groundY + 1.5f;

			m_componentPathfinding.SetDestination(new Vector3?(targetDestination), m_componentCreature.ComponentLocomotion.WalkSpeed, 1f, 20, false, false, false, null);
		}

		private void UpdateAimState(AimState state)
		{
			if (m_musketSlot == -1)
				FindMusket();

			if (m_musketSlot == -1)
				return;

			if (m_componentInventory.ActiveSlotIndex != m_musketSlot)
				m_componentInventory.ActiveSlotIndex = m_musketSlot;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetEyePos = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
			Ray3 aimRay = new Ray3(eyePos, Vector3.Normalize(targetEyePos - eyePos));

			m_componentMiner.Aim(aimRay, state);
		}

		private void SwitchToMeleeModeImmediately()
		{
			m_isMelee = true;
			m_isAiming = false;
			m_isFiring = false;
			m_meleeAttackTime = m_subsystemTime.GameTime;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
			}

			FindHitTool();
		}

		private bool FindHitTool()
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

		private ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
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

		private void StartAiming()
		{
			FindMusket();

			if (m_musketSlot == -1)
				return;

			m_componentInventory.ActiveSlotIndex = m_musketSlot;

			m_isAiming = true;
			m_isFiring = false;
			m_animationStartTime = m_subsystemTime.GameTime;
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

		// Métodos para cargar el mosquete (sin dependencia de munición)
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

		private void EnsureMusketLoaded()
		{
			// Establece el estado cargado
			UpdateMusketLoadState(MusketBlock.LoadState.Loaded);

			// Elige un tipo de bala aleatorio entre los tres disponibles
			BulletBlock.BulletType randomType = (BulletBlock.BulletType)m_random.Int(0, 2); // 0,1,2
			UpdateMusketBulletType(randomType);
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
			m_fireTime = m_subsystemTime.GameTime;

			if (m_musketSlot == -1)
			{
				FindMusket();
				if (m_musketSlot == -1)
					return;
			}

			// Asegurar que el mosquete esté cargado antes de disparar (munición infinita)
			EnsureMusketLoaded();

			UpdateAimState(AimState.Completed);

			// Bonus: 0.05% de probabilidad de disparar TRES BALAS JUNTAS
			if (m_random.Float(0f, 1f) < 0.0005f)
			{
				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetEyePos = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetEyePos - eyePos);

				Vector3 spread1 = direction + new Vector3(m_random.Float(-0.03f, 0.03f), m_random.Float(-0.03f, 0.03f), m_random.Float(-0.03f, 0.03f));
				Vector3 spread2 = direction + new Vector3(m_random.Float(-0.03f, 0.03f), m_random.Float(-0.03f, 0.03f), m_random.Float(-0.03f, 0.03f));
				spread1 = Vector3.Normalize(spread1);
				spread2 = Vector3.Normalize(spread2);

				ShootExtraBullet(spread1);
				ShootExtraBullet(spread2);
			}
		}

		private void ShootExtraBullet(Vector3 direction)
		{
			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;

				int bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false, false);
				if (bulletBlockIndex > 0)
				{
					int bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, 0); // Tipo MusketBall por defecto

					m_subsystemProjectiles.FireProjectile(
						bulletValue,
						firePosition,
						direction * BulletSpeed,
						Vector3.Zero,
						m_componentCreature
					);

					if (m_subsystemNoise != null)
					{
						m_subsystemNoise.MakeNoise(firePosition, 0.5f, 30f);
					}
				}
			}
			catch { }
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

		private void ResetAnimations()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_isMelee = false;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
				m_componentModel.AttackOrder = false;
			}

			if (m_musketSlot != -1)
			{
				UpdateAimState(AimState.Cancelled);
			}
		}
	}
}
