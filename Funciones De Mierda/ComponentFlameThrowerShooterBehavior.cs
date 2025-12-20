using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentFlameThrowerShooterBehavior : ComponentBehavior, IUpdateable
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
		private SubsystemTerrain m_subsystemTerrain;
		private ComponentMiner m_componentMiner;
		private SubsystemNoise m_subsystemNoise;

		// Configuración
		public float MaxDistance = 20f;
		public float AimTime = 0.5f;
		public float BurstTime = 2.0f;      // Cambiado de FireTime a BurstTime
		public float CooldownTime = 1.0f;   // Cambiado de ReloadTime a CooldownTime
		public string FireSound = "Audio/Flamethrower/Flamethrower Fire";
		public string HammerSound = "Audio/Items/Hammer Cock Remake";
		public float FireSoundDistance = 25f;
		public float HammerSoundDistance = 20f; // Nuevo parámetro del XML
		public float Accuracy = 0.1f;
		public bool UseRecoil = true;
		public float FlameSpeed = 40f;
		public int BurstCount = 15;         // Cambiado de ShotsPerBurst a BurstCount
		public float SpreadAngle = 15f;     // Definido pero no implementado en código
		public float BurstInterval = 0.15f;
		public bool CycleBulletTypes = true;

		// Estado
		private bool m_isAiming = false;
		private bool m_isFiring = false;
		private bool m_isReloading = false;
		private double m_animationStartTime;
		private double m_fireStartTime;
		private double m_nextShotTime;
		private int m_shotsFired = 0;
		private Game.Random m_random = new Game.Random();
		private int m_flameThrowerSlot = -1;
		private int m_flameThrowerBlockIndex = 319;

		// Tipo de munición actual
		private FlameBulletBlock.FlameBulletType m_currentBulletType = FlameBulletBlock.FlameBulletType.Flame;

		// UpdateOrder
		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros del XML
			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 20f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 0.5f);
			BurstTime = valuesDictionary.GetValue<float>("BurstTime", 2.0f);      // Cambiado
			CooldownTime = valuesDictionary.GetValue<float>("CooldownTime", 1.0f); // Cambiado
			FireSound = valuesDictionary.GetValue<string>("FireSound", "Audio/Flamethrower/Flamethrower Fire");
			HammerSound = valuesDictionary.GetValue<string>("HammerSound", "Audio/Items/Hammer Cock Remake");
			FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 25f);
			HammerSoundDistance = valuesDictionary.GetValue<float>("HammerSoundDistance", 20f); // Nuevo
			Accuracy = valuesDictionary.GetValue<float>("Accuracy", 0.1f);
			UseRecoil = valuesDictionary.GetValue<bool>("UseRecoil", true);
			FlameSpeed = valuesDictionary.GetValue<float>("FlameSpeed", 40f);
			BurstCount = valuesDictionary.GetValue<int>("BurstCount", 15);         // Cambiado
			SpreadAngle = valuesDictionary.GetValue<float>("SpreadAngle", 15f);
			CycleBulletTypes = valuesDictionary.GetValue<bool>("CycleBulletTypes", true);

			// Calcular BurstInterval automáticamente basado en BurstTime y BurstCount
			if (BurstCount > 0 && BurstTime > 0)
			{
				BurstInterval = BurstTime / BurstCount;
			}
			else
			{
				BurstInterval = 0.15f; // Valor por defecto
			}

			// Inicializar componentes
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);

			// Inicializar tipo de bala aleatorio
			m_currentBulletType = m_random.Bool() ?
				FlameBulletBlock.FlameBulletType.Flame :
				FlameBulletBlock.FlameBulletType.Poison;
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
				if (!m_isAiming && !m_isFiring && !m_isReloading)
				{
					StartAiming();
				}
			}
			else
			{
				ResetAnimations();
				return;
			}

			if (m_isAiming)
			{
				ApplyAimingAnimation(dt);

				if (m_subsystemTime.GameTime - m_animationStartTime >= AimTime)
				{
					m_isAiming = false;
					StartFiring();
				}
			}
			else if (m_isFiring)
			{
				ApplyFiringAnimation(dt);

				if (m_subsystemTime.GameTime >= m_nextShotTime && m_shotsFired < BurstCount) // Cambiado
				{
					FireShot();
					m_shotsFired++;
					m_nextShotTime = m_subsystemTime.GameTime + BurstInterval;
				}

				if (m_subsystemTime.GameTime - m_fireStartTime >= BurstTime) // Cambiado
				{
					m_isFiring = false;
					StopFiring();
				}
			}
			else if (m_isReloading)
			{
				ApplyReloadingAnimation(dt);

				if (m_subsystemTime.GameTime - m_animationStartTime >= CooldownTime) // Cambiado
				{
					m_isReloading = false;

					if (CycleBulletTypes)
					{
						m_currentBulletType = (m_currentBulletType == FlameBulletBlock.FlameBulletType.Flame) ?
							FlameBulletBlock.FlameBulletType.Poison :
							FlameBulletBlock.FlameBulletType.Flame;
					}

					StartAiming();
				}
			}
		}

		private void FindFlameThrower()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0 && Terrain.ExtractContents(slotValue) == m_flameThrowerBlockIndex)
				{
					m_flameThrowerSlot = i;
					m_componentInventory.ActiveSlotIndex = i;
					break;
				}
			}
		}

		private void StartAiming()
		{
			FindFlameThrower();

			if (m_flameThrowerSlot == -1)
				return;

			m_isAiming = true;
			m_isFiring = false;
			m_isReloading = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_shotsFired = 0;

			// Sonido de preparación
			if (!string.IsNullOrEmpty(HammerSound))
			{
				m_subsystemAudio.PlaySound(HammerSound, 1f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, HammerSoundDistance, false); // Usa HammerSoundDistance
			}
		}

		private void ApplyAimingAnimation(float dt)
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

		private void StartFiring()
		{
			m_isAiming = false;
			m_isFiring = true;
			m_isReloading = false;
			m_fireStartTime = m_subsystemTime.GameTime;
			m_nextShotTime = m_subsystemTime.GameTime;
			m_shotsFired = 0;
		}

		private void ApplyFiringAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				float fireProgress = (float)((m_subsystemTime.GameTime - m_fireStartTime) / BurstTime); // Cambiado
				float vibration = (float)Math.Sin(fireProgress * 20f) * 0.02f;

				m_componentModel.AimHandAngleOrder = 1.4f + vibration;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.08f + vibration,
					-0.08f,
					0.07f
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					-1.7f + vibration * 2f,
					0f,
					0f
				);

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void StopFiring()
		{
			StartReloading();
		}

		private void StartReloading()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_isReloading = true;
			m_animationStartTime = m_subsystemTime.GameTime;
		}

		private void ApplyReloadingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / CooldownTime); // Cambiado

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
			m_shotsFired = 0;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void FireShot()
		{
			if (m_componentChaseBehavior.Target == null)
				return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;

				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				// Disparar recto como el mosquete
				direction += new Vector3(
					m_random.Float(-Accuracy, Accuracy),
					m_random.Float(-Accuracy * 0.5f, Accuracy * 0.5f),
					m_random.Float(-Accuracy, Accuracy)
				);
				direction = Vector3.Normalize(direction);

				int bulletBlockIndex = BlocksManager.GetBlockIndex<FlameBulletBlock>(false, false);
				if (bulletBlockIndex > 0)
				{
					FlameBulletBlock.FlameBulletType bulletType = m_currentBulletType;

					int bulletData = FlameBulletBlock.SetBulletType(0, bulletType);
					int bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, bulletData);

					m_subsystemProjectiles.FireProjectile(
						bulletValue,
						firePosition,
						direction * FlameSpeed,
						Vector3.Zero,
						m_componentCreature
					);

					// Audio y partículas según tipo
					Vector3 smokePosition = firePosition + direction * 0.3f;

					if (bulletType == FlameBulletBlock.FlameBulletType.Flame)
					{
						// Sonido de fuego
						if (!string.IsNullOrEmpty(FireSound))
						{
							m_subsystemAudio.PlaySound(FireSound, 1f, m_random.Float(-0.1f, 0.1f),
								m_componentCreature.ComponentBody.Position, FireSoundDistance, false);
						}

						// Partículas de fuego
						if (m_subsystemParticles != null && m_subsystemTerrain != null)
						{
							m_subsystemParticles.AddParticleSystem(
								new FlameSmokeParticleSystem(m_subsystemTerrain, smokePosition, direction),
								false
							);
						}
					}
					else
					{
						// Sonido de veneno
						m_subsystemAudio.PlaySound("Audio/Flamethrower/PoisonSmoke", 1f, m_random.Float(-0.1f, 0.1f),
							m_componentCreature.ComponentCreatureModel.EyePosition, 8f, true);

						// Partículas de veneno
						if (m_subsystemParticles != null && m_subsystemTerrain != null)
						{
							m_subsystemParticles.AddParticleSystem(
								new PoisonSmokeParticleSystem(m_subsystemTerrain, smokePosition + 0.3f * direction, direction),
								false
							);
						}
					}

					if (UseRecoil)
					{
						float recoilStrength = (bulletType == FlameBulletBlock.FlameBulletType.Flame) ? 0.8f : 0.5f;
						m_componentCreature.ComponentBody.ApplyImpulse(-direction * recoilStrength);
					}

					if (m_subsystemNoise != null)
					{
						m_subsystemNoise.MakeNoise(firePosition, 0.7f, 25f);
					}
				}
			}
			catch
			{
				// Ignorar errores
			}
		}
	}
}
