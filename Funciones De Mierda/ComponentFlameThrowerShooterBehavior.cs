﻿using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentFlameThrowerShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Componentes necesarios
		private ComponentCreature m_componentCreature;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private ComponentCreatureModel m_componentModel;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemTerrain m_subsystemTerrain;
		private ComponentInventory m_componentInventory;

		// Índice del bloque FlameThrower
		private int m_flameThrowerBlockIndex;

		// Configuración
		public float MaxDistance = 20f;
		public float AimTime = 0.5f;
		public float BurstTime = 2f;
		public float CooldownTime = 1f;
		public string FireSound = "Audio/Flamethrower/Flamethrower Fire";
		public string PoisonSound = "Audio/Flamethrower/PoisonSmoke"; // Nuevo sonido para veneno
		public string HammerSound = "Audio/HammerCock";
		public float FireSoundDistance = 30f;
		public float HammerSoundDistance = 20f;
		public float Accuracy = 0.1f;
		public bool UseRecoil = true;
		public float FlameSpeed = 40f;
		public int BurstCount = 15;
		public float SpreadAngle = 15f;

		// Tipos de bala disponibles - INCLUYENDO VENENO
		private FlameBulletBlock.FlameBulletType[] m_availableBulletTypes = new FlameBulletBlock.FlameBulletType[]
		{
			FlameBulletBlock.FlameBulletType.Flame,
			FlameBulletBlock.FlameBulletType.Poison
		};

		// Estado de animación
		private bool m_isAiming = false;
		private bool m_isFiring = false;
		private bool m_isCooldown = false;
		private double m_animationStartTime;
		private double m_burstStartTime;
		private int m_bulletsFired = 0;
		private Random m_random = new Random();
		private float m_soundVolume = 0f;
		private bool m_hammerSoundPlayed = false;
		private int m_currentBulletTypeIndex = 0; // Índice del tipo de bala actual

		// UpdateOrder
		public int UpdateOrder
		{
			get
			{
				return 0;
			}
		}

		public override float ImportanceLevel
		{
			get
			{
				// Solo es importante si tiene un lanzallamas equipado
				if (HasFlameThrowerEquipped())
				{
					return 0.5f;
				}
				return 0f; // Si no tiene lanzallamas, no es importante
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 20f);
			this.AimTime = valuesDictionary.GetValue<float>("AimTime", 0.5f);
			this.BurstTime = valuesDictionary.GetValue<float>("BurstTime", 2f);
			this.CooldownTime = valuesDictionary.GetValue<float>("CooldownTime", 1f);
			this.FireSound = valuesDictionary.GetValue<string>("FireSound", "Audio/Flamethrower/Flamethrower Fire");
			this.HammerSound = valuesDictionary.GetValue<string>("HammerSound", "Audio/HammerCock");
			this.FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 30f);
			this.HammerSoundDistance = valuesDictionary.GetValue<float>("HammerSoundDistance", 20f);
			this.Accuracy = valuesDictionary.GetValue<float>("Accuracy", 0.1f);
			this.UseRecoil = valuesDictionary.GetValue<bool>("UseRecoil", true);
			this.FlameSpeed = valuesDictionary.GetValue<float>("FlameSpeed", 40f);
			this.BurstCount = valuesDictionary.GetValue<int>("BurstCount", 15);
			this.SpreadAngle = valuesDictionary.GetValue<float>("SpreadAngle", 15f);

			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);

			// Obtener el índice del bloque FlameThrower
			this.m_flameThrowerBlockIndex = BlocksManager.GetBlockIndex<FlameThrowerBlock>(false, false);

			// Inicializar tipo de bala aleatorio
			this.m_currentBulletTypeIndex = m_random.Int(0, m_availableBulletTypes.Length);
		}

		public void Update(float dt)
		{
			// Verificar si el NPC está vivo
			if (this.m_componentCreature.ComponentHealth.Health <= 0f)
			{
				this.ResetAnimations();
				return;
			}

			// Verificar si tiene un lanzallamas equipado
			if (!HasFlameThrowerEquipped())
			{
				this.ResetAnimations();
				return;
			}

			// Verificar si tiene un objetivo
			if (this.m_componentChaseBehavior.Target == null)
			{
				this.ResetAnimations();
				return;
			}

			float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
											this.m_componentChaseBehavior.Target.ComponentBody.Position);

			// Solo activar si está dentro del rango y tiene lanzallamas equipado
			if (distance <= this.MaxDistance)
			{
				if (!this.m_isAiming && !this.m_isFiring && !this.m_isCooldown)
				{
					this.StartAiming();
				}
			}
			else
			{
				this.ResetAnimations();
				return;
			}

			// Resto de la lógica de animación y disparo...
			if (this.m_isAiming)
			{
				this.ApplyAimingAnimation(dt);
				if (this.m_subsystemTime.GameTime - this.m_animationStartTime >= (double)this.AimTime)
				{
					this.StartFiring();
				}
			}
			else if (this.m_isFiring)
			{
				this.ApplyFiringAnimation(dt);
				double burstElapsed = this.m_subsystemTime.GameTime - this.m_burstStartTime;
				float timeLeft = (float)((double)this.BurstTime - burstElapsed);

				if (timeLeft < 0.3f)
				{
					this.m_soundVolume = MathUtils.Lerp(0f, 1f, timeLeft / 0.3f);
				}
				else
				{
					this.m_soundVolume = 1f;
				}

				if (burstElapsed < (double)this.BurstTime)
				{
					int expectedBullets = (int)(burstElapsed * (double)((float)this.BurstCount / this.BurstTime));
					while (this.m_bulletsFired < expectedBullets && this.m_bulletsFired < this.BurstCount)
					{
						if (!this.m_hammerSoundPlayed && this.m_bulletsFired == 0)
						{
							this.PlayHammerSound();
							this.m_hammerSoundPlayed = true;
						}
						this.ShootFlame();
						this.m_bulletsFired++;
					}
				}
				else
				{
					this.m_isFiring = false;
					this.StartCooldown();
				}
			}
			else if (this.m_isCooldown)
			{
				this.ApplyCooldownAnimation(dt);
				if (this.m_subsystemTime.GameTime - this.m_animationStartTime >= (double)this.CooldownTime)
				{
					this.m_isCooldown = false;

					// Cambiar tipo de bala para la próxima ráfaga
					this.m_currentBulletTypeIndex = (this.m_currentBulletTypeIndex + 1) % m_availableBulletTypes.Length;

					this.StartAiming();
				}
			}
		}

		// MÉTODO SIMPLIFICADO: Verifica si el NPC tiene un lanzallamas
		private bool HasFlameThrowerEquipped()
		{
			if (this.m_componentInventory == null)
				return false;

			// Enfoque más simple: verificar si hay algún lanzallamas en el inventario
			for (int slotIndex = 0; slotIndex < this.m_componentInventory.SlotsCount; slotIndex++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(slotIndex);
				if (slotValue != 0)
				{
					int blockIndex = Terrain.ExtractContents(slotValue);
					if (blockIndex == this.m_flameThrowerBlockIndex)
					{
						return true;
					}
				}
			}

			return false;
		}

		private void StartAiming()
		{
			// Solo iniciar apuntado si tiene lanzallamas equipado
			if (!HasFlameThrowerEquipped())
			{
				this.ResetAnimations();
				return;
			}

			this.m_isAiming = true;
			this.m_isFiring = false;
			this.m_isCooldown = false;
			this.m_animationStartTime = this.m_subsystemTime.GameTime;
			this.m_bulletsFired = 0;
			this.m_soundVolume = 0f;
			this.m_hammerSoundPlayed = false;
		}

		private void ApplyAimingAnimation(float dt)
		{
			if (this.m_componentModel != null)
			{
				this.m_componentModel.AimHandAngleOrder = 1.1f;
				this.m_componentModel.InHandItemOffsetOrder = new Vector3(-0.12f, -0.05f, 0.1f);
				this.m_componentModel.InHandItemRotationOrder = new Vector3(-1.3f, -0.2f, 0f);

				if (this.m_componentChaseBehavior.Target != null)
				{
					this.m_componentModel.LookAtOrder = new Vector3?(this.m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void StartFiring()
		{
			// Solo iniciar disparo si tiene lanzallamas equipado
			if (!HasFlameThrowerEquipped())
			{
				this.ResetAnimations();
				return;
			}

			this.m_isAiming = false;
			this.m_isFiring = true;
			this.m_isCooldown = false;
			this.m_burstStartTime = this.m_subsystemTime.GameTime;
			this.m_bulletsFired = 0;
			this.m_soundVolume = 1f;
			this.m_hammerSoundPlayed = false;
		}

		private void ApplyFiringAnimation(float dt)
		{
			// Solo animar si tiene lanzallamas equipado
			if (!HasFlameThrowerEquipped())
			{
				this.ResetAnimations();
				return;
			}

			if (this.m_componentModel != null)
			{
				float timeRatio = (float)((this.m_subsystemTime.GameTime - this.m_burstStartTime) / (double)this.BurstTime);
				float handShake = 1f + 0.2f * MathUtils.Sin(timeRatio * 25f);

				this.m_componentModel.AimHandAngleOrder = 1.1f * handShake;
				this.m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.12f + 0.01f * MathUtils.Sin(timeRatio * 20f),
					-0.05f + 0.005f * MathUtils.Sin(timeRatio * 22f),
					0.1f
				);
				this.m_componentModel.InHandItemRotationOrder = new Vector3(
					-1.3f + 0.15f * MathUtils.Sin(timeRatio * 18f),
					-0.2f,
					0f
				);

				if (this.m_componentChaseBehavior.Target != null)
				{
					this.m_componentModel.LookAtOrder = new Vector3?(this.m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition);
				}

				if (this.UseRecoil && this.m_componentChaseBehavior.Target != null)
				{
					Vector3 direction = Vector3.Normalize(this.m_componentChaseBehavior.Target.ComponentBody.Position - this.m_componentCreature.ComponentBody.Position);
					this.m_componentCreature.ComponentBody.ApplyImpulse(-direction * 0.3f * dt);
				}
			}

			if (this.m_soundVolume > 0.01f && !string.IsNullOrEmpty(this.FireSound) &&
				this.m_subsystemTime.GameTime - this.m_burstStartTime < (double)this.BurstTime - 0.1)
			{
				// Seleccionar sonido según tipo de bala
				string soundToPlay = (m_availableBulletTypes[m_currentBulletTypeIndex] == FlameBulletBlock.FlameBulletType.Flame) ?
					this.FireSound : this.PoisonSound;

				if (!string.IsNullOrEmpty(soundToPlay))
				{
					float fireVolume = MathUtils.Max(this.m_soundVolume, 0.9f);
					this.m_subsystemAudio.PlaySound(
						soundToPlay,
						fireVolume,
						this.m_random.Float(-0.1f, 0.1f),
						this.m_componentCreature.ComponentBody.Position,
						this.FireSoundDistance,
						true
					);
				}
			}
		}

		private void StartCooldown()
		{
			this.m_isAiming = false;
			this.m_isFiring = false;
			this.m_isCooldown = true;
			this.m_animationStartTime = this.m_subsystemTime.GameTime;
			this.m_soundVolume = 0f;
		}

		private void ApplyCooldownAnimation(float dt)
		{
			if (this.m_componentModel != null)
			{
				float cooldownRatio = (float)((this.m_subsystemTime.GameTime - this.m_animationStartTime) / (double)this.CooldownTime);

				this.m_componentModel.AimHandAngleOrder = MathUtils.Lerp(1.1f, 0.7f, cooldownRatio);
				this.m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.12f,
					-0.05f - 0.08f * cooldownRatio,
					0.1f - 0.05f * cooldownRatio
				);
				this.m_componentModel.InHandItemRotationOrder = new Vector3(
					-1.3f + 0.4f * cooldownRatio,
					-0.2f,
					0f
				);
			}
		}

		private void ResetAnimations()
		{
			this.m_isAiming = false;
			this.m_isFiring = false;
			this.m_isCooldown = false;
			this.m_bulletsFired = 0;
			this.m_soundVolume = 0f;
			this.m_hammerSoundPlayed = false;

			if (this.m_componentModel != null)
			{
				this.m_componentModel.AimHandAngleOrder = 0f;
				this.m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				this.m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				this.m_componentModel.LookAtOrder = null;
			}
		}

		private void PlayHammerSound()
		{
			if (!string.IsNullOrEmpty(this.HammerSound))
			{
				this.m_subsystemAudio.PlaySound(
					this.HammerSound,
					1.0f,
					this.m_random.Float(-0.05f, 0.05f),
					this.m_componentCreature.ComponentBody.Position,
					this.HammerSoundDistance,
					false
				);

				this.m_subsystemAudio.PlaySound(
					"Audio/Impacts/MetalImpact",
					0.7f,
					this.m_random.Float(-0.03f, 0.03f),
					this.m_componentCreature.ComponentBody.Position,
					15f,
					false
				);
			}
		}

		private void ShootFlame()
		{
			// Solo disparar si tiene lanzallamas equipado y hay objetivo
			if (!HasFlameThrowerEquipped() || this.m_componentChaseBehavior.Target == null)
			{
				return;
			}

			try
			{
				Vector3 eyePosition = this.m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetEyePosition = this.m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;

				Vector3 direction = Vector3.Normalize(targetEyePosition - eyePosition);
				float accuracyFactor = this.Accuracy * 0.3f;

				Vector3 adjustedDirection = Vector3.Normalize(direction + new Vector3(
					this.m_random.Float(-accuracyFactor, accuracyFactor),
					this.m_random.Float(-accuracyFactor * 0.3f, accuracyFactor * 0.3f),
					this.m_random.Float(-accuracyFactor, accuracyFactor)
				));

				int flameBulletIndex = BlocksManager.GetBlockIndex<FlameBulletBlock>(false, false);
				if (flameBulletIndex > 0)
				{
					// Obtener tipo de bala actual
					FlameBulletBlock.FlameBulletType currentBulletType = m_availableBulletTypes[m_currentBulletTypeIndex];

					int bulletData = FlameBulletBlock.SetBulletType(0, currentBulletType);
					int blockValue = Terrain.MakeBlockValue(flameBulletIndex, 0, bulletData);

					this.m_subsystemProjectiles.FireProjectile(
						blockValue,
						eyePosition + adjustedDirection * 0.3f,
						adjustedDirection * this.FlameSpeed,
						Vector3.Zero,
						this.m_componentCreature
					);

					Vector3 particlePosition = eyePosition + adjustedDirection * 0.3f;

					if (this.m_subsystemParticles != null && this.m_subsystemTerrain != null)
					{
						// Crear sistema de partículas según el tipo de bala
						if (currentBulletType == FlameBulletBlock.FlameBulletType.Flame)
						{
							this.m_subsystemParticles.AddParticleSystem(
								new FlameSmokeParticleSystem(this.m_subsystemTerrain, particlePosition, adjustedDirection),
								false
							);
						}
						else // Poison
						{
							this.m_subsystemParticles.AddParticleSystem(
								new PoisonSmokeParticleSystem(this.m_subsystemTerrain, particlePosition, adjustedDirection),
								false
							);
						}
					}

					if (this.m_subsystemNoise != null)
					{
						this.m_subsystemNoise.MakeNoise(eyePosition, 0.8f, 35f);
					}
				}
			}
			catch
			{
				// Ignorar errores al disparar
			}
		}
	}
}
