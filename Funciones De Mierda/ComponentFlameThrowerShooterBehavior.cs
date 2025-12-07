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
		private ComponentInventory m_componentInventory;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private ComponentCreatureModel m_componentModel;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemTerrain m_subsystemTerrain;

		// Configuración
		public float MaxDistance = 20f;
		public float MinDistance = 5f;
		public float AimTime = 0.5f;
		public float BurstTime = 2f;
		public float CooldownTime = 1f;
		public string FireSound = "Audio/Flamethrower/Flamethrower Fire";
		public float FireSoundDistance = 25f;
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

		// UpdateOrder
		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros
			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 20f);
			MinDistance = valuesDictionary.GetValue<float>("MinDistance", 5f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 0.5f);
			BurstTime = valuesDictionary.GetValue<float>("BurstTime", 2f);
			CooldownTime = valuesDictionary.GetValue<float>("CooldownTime", 1f);
			FireSound = valuesDictionary.GetValue<string>("FireSound", "Audio/Flamethrower/Flamethrower Fire");
			FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 25f);
			Accuracy = valuesDictionary.GetValue<float>("Accuracy", 0.1f);
			UseRecoil = valuesDictionary.GetValue<bool>("UseRecoil", true);
			FlameSpeed = valuesDictionary.GetValue<float>("FlameSpeed", 40f);
			BurstCount = valuesDictionary.GetValue<int>("BurstCount", 15);
			SpreadAngle = valuesDictionary.GetValue<float>("SpreadAngle", 15f);

			// Inicializar componentes
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
		}

		public void Update(float dt)
		{
			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			// Verificar objetivo
			if (m_componentChaseBehavior.Target == null)
			{
				ResetAnimations();
				return;
			}

			// Calcular distancia
			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				m_componentChaseBehavior.Target.ComponentBody.Position
			);

			// Lógica de ataque - Solo dentro del rango permitido
			if (distance >= MinDistance && distance <= MaxDistance)
			{
				// Si no está haciendo nada, empezar a apuntar
				if (!m_isAiming && !m_isFiring && !m_isCooldown)
				{
					StartAiming();
				}
			}
			else
			{
				// Fuera de rango, resetear
				ResetAnimations();
				return;
			}

			// Aplicar animaciones
			if (m_isAiming)
			{
				ApplyAimingAnimation(dt);

				// Después de tiempo de apuntado, empezar a disparar
				if (m_subsystemTime.GameTime - m_animationStartTime >= AimTime)
				{
					StartFiring();
				}
			}
			else if (m_isFiring)
			{
				ApplyFiringAnimation(dt);

				// Controlar ráfaga de disparos
				double burstElapsed = m_subsystemTime.GameTime - m_burstStartTime;

				// Desvanecer el sonido al final de la ráfaga
				float timeLeft = (float)(BurstTime - burstElapsed);
				if (timeLeft < 0.3f)
				{
					m_soundVolume = MathUtils.Lerp(0f, 1f, timeLeft / 0.3f);
				}
				else
				{
					m_soundVolume = 1f;
				}

				// Disparar proyectiles durante la ráfaga
				if (burstElapsed < BurstTime)
				{
					// Calcular cuántos proyectiles deberíamos haber disparado hasta ahora
					int expectedBullets = (int)(burstElapsed * (BurstCount / BurstTime));

					// Disparar proyectiles adicionales si es necesario
					while (m_bulletsFired < expectedBullets && m_bulletsFired < BurstCount)
					{
						ShootFlame();
						m_bulletsFired++;
					}
				}
				else
				{
					// Terminar ráfaga y empezar enfriamiento
					m_isFiring = false;
					StartCooldown();
				}
			}
			else if (m_isCooldown)
			{
				ApplyCooldownAnimation(dt);

				// Después de tiempo de enfriamiento, volver a apuntar
				if (m_subsystemTime.GameTime - m_animationStartTime >= CooldownTime)
				{
					m_isCooldown = false;
					StartAiming();
				}
			}
		}

		private void StartAiming()
		{
			m_isAiming = true;
			m_isFiring = false;
			m_isCooldown = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_bulletsFired = 0;
			m_soundVolume = 0f;
		}

		private void ApplyAimingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE APUNTADO (LANZALLAMAS) - POSTURA ALTA
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.21f, 0.15f, 0.08f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-0.7f, 0f, 0f);

				// Mirar al objetivo
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
			m_isCooldown = false;
			m_burstStartTime = m_subsystemTime.GameTime;
			m_bulletsFired = 0;
			m_soundVolume = 1f;

			// Sonido de lanzallamas (se reproduce cada frame durante el disparo)
		}

		private void ApplyFiringAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE DISPARO CON RETROCESO
				float fireProgress = (float)((m_subsystemTime.GameTime - m_burstStartTime) / BurstTime);
				float recoilFactor = 1f + 0.3f * MathUtils.Sin(fireProgress * 20f);

				m_componentModel.AimHandAngleOrder = 1.4f * recoilFactor;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.21f + 0.02f * MathUtils.Sin(fireProgress * 15f),
					0.15f + 0.01f * MathUtils.Sin(fireProgress * 17f),
					0.08f
				);

				// Mirar al objetivo
				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}

				// Aplicar retroceso físico
				if (UseRecoil && m_componentChaseBehavior.Target != null)
				{
					Vector3 direction = Vector3.Normalize(
						m_componentChaseBehavior.Target.ComponentBody.Position -
						m_componentCreature.ComponentBody.Position
					);
					m_componentCreature.ComponentBody.ApplyImpulse(-direction * 0.5f * dt);
				}
			}

			// Reproducir sonido de lanzallamas con volumen controlado
			if (m_soundVolume > 0.01f && !string.IsNullOrEmpty(FireSound))
			{
				// Reproducir sonido cada cierto tiempo para simular continuidad
				if (m_subsystemTime.GameTime - m_burstStartTime < BurstTime - 0.1f)
				{
					m_subsystemAudio.PlaySound(FireSound, m_soundVolume, m_random.Float(-0.1f, 0.1f),
						m_componentCreature.ComponentBody.Position, FireSoundDistance, true);
				}
			}
		}

		private void StartCooldown()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_isCooldown = true;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_soundVolume = 0f;
		}

		private void ApplyCooldownAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE ENFRIAMIENTO - BRAZOS BAJOS
				float cooldownProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / CooldownTime);

				m_componentModel.AimHandAngleOrder = MathUtils.Lerp(1.4f, 0.8f, cooldownProgress);
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.21f,
					0.15f - 0.1f * cooldownProgress,
					0.08f - 0.05f * cooldownProgress
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					-0.7f + 0.3f * cooldownProgress,
					0f,
					0f
				);
			}
		}

		private void ResetAnimations()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_isCooldown = false;
			m_bulletsFired = 0;
			m_soundVolume = 0f;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ShootFlame()
		{
			if (m_componentChaseBehavior.Target == null)
				return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;

				Vector3 baseDirection = Vector3.Normalize(targetPosition - firePosition);

				// Aplicar dispersión de disparo
				float spreadRad = MathUtils.DegToRad(SpreadAngle);
				Vector3 direction = Vector3.Normalize(baseDirection + new Vector3(
					m_random.Float(-spreadRad, spreadRad),
					m_random.Float(-spreadRad * 0.5f, spreadRad * 0.5f),
					m_random.Float(-spreadRad, spreadRad)
				));

				// Crear bala de fuego
				int flameBulletBlockIndex = BlocksManager.GetBlockIndex<FlameBulletBlock>(false, false);
				if (flameBulletBlockIndex > 0)
				{
					// Solo tipo Flame (0)
					int flameData = FlameBulletBlock.SetBulletType(0, FlameBulletBlock.FlameBulletType.Flame);
					int flameValue = Terrain.MakeBlockValue(flameBulletBlockIndex, 0, flameData);

					// Disparar
					m_subsystemProjectiles.FireProjectile(
						flameValue,
						firePosition + direction * 0.3f,
						direction * FlameSpeed,
						Vector3.Zero,
						m_componentCreature
					);

					// AGREGAR HUMO Y LLAMAS
					Vector3 particlePosition = firePosition + direction * 0.3f;

					if (m_subsystemParticles != null && m_subsystemTerrain != null)
					{
						m_subsystemParticles.AddParticleSystem(
							new FlameSmokeParticleSystem(m_subsystemTerrain, particlePosition, direction),
							false
						);
					}

					// AGREGAR RUIDO
					if (m_subsystemNoise != null)
					{
						m_subsystemNoise.MakeNoise(firePosition, 0.5f, 30f);
					}
				}
			}
			catch
			{
				// Ignorar errores de disparo
			}
		}
	}

}
