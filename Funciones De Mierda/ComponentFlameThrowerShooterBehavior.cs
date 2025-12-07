using System;
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

		// Configuración
		public float MaxDistance = 20f;
		public float AimTime = 0.5f;
		public float BurstTime = 2f;
		public float CooldownTime = 1f;
		public string FireSound = "Audio/Flamethrower/Flamethrower Fire";
		public string HammerSound = "Audio/HammerCock"; // Sonido del martillo/hammercock
		public float FireSoundDistance = 30f; // AUMENTADO de 25f a 30f
		public float HammerSoundDistance = 20f; // Distancia específica para sonido del martillo
		public float Accuracy = 0.1f;
		public bool UseRecoil = true;
		public float FlameSpeed = 40f;
		public int BurstCount = 15;
		public float SpreadAngle = 15f;

		// Estado de animación
		private bool m_isAiming = false;
		private bool m_isFiring = false;
		private bool m_isCooldown = false;
		private double m_animationStartTime;
		private double m_burstStartTime;
		private int m_bulletsFired = 0;
		private Random m_random = new Random();
		private float m_soundVolume = 0f;
		private bool m_hammerSoundPlayed = false; // Para controlar que el sonido del martillo se reproduzca una vez

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
				return 0.5f;
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
			this.FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 30f); // AUMENTADO
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
		}

		public void Update(float dt)
		{
			if (this.m_componentCreature.ComponentHealth.Health <= 0f)
			{
				return;
			}
			if (this.m_componentChaseBehavior.Target == null)
			{
				this.ResetAnimations();
				return;
			}
			float num = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_componentChaseBehavior.Target.ComponentBody.Position);
			if (num <= this.MaxDistance)
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
				double num2 = this.m_subsystemTime.GameTime - this.m_burstStartTime;
				float num3 = (float)((double)this.BurstTime - num2);
				if (num3 < 0.3f)
				{
					this.m_soundVolume = MathUtils.Lerp(0f, 1f, num3 / 0.3f);
				}
				else
				{
					this.m_soundVolume = 1f;
				}
				if (num2 < (double)this.BurstTime)
				{
					int num4 = (int)(num2 * (double)((float)this.BurstCount / this.BurstTime));
					while (this.m_bulletsFired < num4 && this.m_bulletsFired < this.BurstCount)
					{
						// Reproducir sonido del martillo solo en el primer disparo
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
					this.StartAiming();
				}
			}
		}

		private void StartAiming()
		{
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
				// ANIMACIÓN DE LANZALLAMAS EN POSICIÓN VERTICAL
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
			if (this.m_componentModel != null)
			{
				float num = (float)((this.m_subsystemTime.GameTime - this.m_burstStartTime) / (double)this.BurstTime);
				float num2 = 1f + 0.2f * MathUtils.Sin(num * 25f);

				this.m_componentModel.AimHandAngleOrder = 1.1f * num2;
				this.m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.12f + 0.01f * MathUtils.Sin(num * 20f),
					-0.05f + 0.005f * MathUtils.Sin(num * 22f),
					0.1f
				);
				this.m_componentModel.InHandItemRotationOrder = new Vector3(
					-1.3f + 0.15f * MathUtils.Sin(num * 18f),
					-0.2f,
					0f
				);

				if (this.m_componentChaseBehavior.Target != null)
				{
					this.m_componentModel.LookAtOrder = new Vector3?(this.m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition);
				}

				if (this.UseRecoil && this.m_componentChaseBehavior.Target != null)
				{
					Vector3 v = Vector3.Normalize(this.m_componentChaseBehavior.Target.ComponentBody.Position - this.m_componentCreature.ComponentBody.Position);
					this.m_componentCreature.ComponentBody.ApplyImpulse(-v * 0.3f * dt);
				}
			}

			// AUMENTADO: Reproducir sonido de fuego con volumen más alto
			if (this.m_soundVolume > 0.01f && !string.IsNullOrEmpty(this.FireSound) && this.m_subsystemTime.GameTime - this.m_burstStartTime < (double)this.BurstTime - 0.1)
			{
				// Usar volumen completo (1.0) en lugar de m_soundVolume para que sea más fuerte
				float fireVolume = MathUtils.Max(this.m_soundVolume, 0.9f); // Mínimo 90% de volumen
				this.m_subsystemAudio.PlaySound(
					this.FireSound,
					fireVolume, // AUMENTADO: Usar volumen más alto
					this.m_random.Float(-0.1f, 0.1f),
					this.m_componentCreature.ComponentBody.Position,
					this.FireSoundDistance, // AUMENTADO: 30f en lugar de 25f
					true
				);
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
				float num = (float)((this.m_subsystemTime.GameTime - this.m_animationStartTime) / (double)this.CooldownTime);

				this.m_componentModel.AimHandAngleOrder = MathUtils.Lerp(1.1f, 0.7f, num);
				this.m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.12f,
					-0.05f - 0.08f * num,
					0.1f - 0.05f * num
				);
				this.m_componentModel.InHandItemRotationOrder = new Vector3(
					-1.3f + 0.4f * num,
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
				// AUMENTADO: Volumen más alto para el sonido del martillo
				this.m_subsystemAudio.PlaySound(
					this.HammerSound,
					1.0f, // AUMENTADO: Volumen máximo (1.0) en lugar de 0.8f
					this.m_random.Float(-0.05f, 0.05f),
					this.m_componentCreature.ComponentBody.Position,
					this.HammerSoundDistance, // 20f para sonido del martillo
					false
				);

				// AUMENTADO: Añadir un pequeño retraso y reproducir un segundo sonido de impacto
				// para hacerlo más audible y realista
				this.m_subsystemAudio.PlaySound(
					"Audio/Impacts/MetalImpact",
					0.7f, // Volumen moderado para el impacto
					this.m_random.Float(-0.03f, 0.03f),
					this.m_componentCreature.ComponentBody.Position,
					15f,
					false
				);
			}
		}

		private void ShootFlame()
		{
			if (this.m_componentChaseBehavior.Target == null)
			{
				return;
			}
			try
			{
				Vector3 vector = this.m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 vector2 = this.m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;

				// DIRECCIÓN RECTA Y LINEAL - MEJORADA
				Vector3 v = Vector3.Normalize(vector2 - vector);

				// Usar solo un pequeño factor de precisión para mantener dirección recta
				float accuracyFactor = this.Accuracy * 0.3f; // Reducido aún más
				Vector3 v2 = Vector3.Normalize(v + new Vector3(
					this.m_random.Float(-accuracyFactor, accuracyFactor),
					this.m_random.Float(-accuracyFactor * 0.3f, accuracyFactor * 0.3f), // Vertical más precisa
					this.m_random.Float(-accuracyFactor, accuracyFactor)
				));

				int num2 = BlocksManager.GetBlockIndex<FlameBulletBlock>(false, false);
				if (num2 > 0)
				{
					int num3 = FlameBulletBlock.SetBulletType(0, FlameBulletBlock.FlameBulletType.Flame);
					int num4 = Terrain.MakeBlockValue(num2, 0, num3);

					// Disparar con dirección recta
					this.m_subsystemProjectiles.FireProjectile(
						num4,
						vector + v2 * 0.3f,
						v2 * this.FlameSpeed,
						Vector3.Zero,
						this.m_componentCreature
					);

					Vector3 position = vector + v2 * 0.3f;

					if (this.m_subsystemParticles != null && this.m_subsystemTerrain != null)
					{
						this.m_subsystemParticles.AddParticleSystem(
							new FlameSmokeParticleSystem(this.m_subsystemTerrain, position, v2),
							false
						);
					}

					// AUMENTADO: Ruido más fuerte cuando dispara
					if (this.m_subsystemNoise != null)
					{
						this.m_subsystemNoise.MakeNoise(vector, 0.8f, 35f); // AUMENTADO: de 0.5f a 0.8f y de 30f a 35f
					}
				}
			}
			catch
			{
			}
		}
	}
}
