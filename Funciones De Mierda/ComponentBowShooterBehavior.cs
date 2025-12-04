using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBowShooterBehavior : ComponentBehavior, IUpdateable
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

		// Configuración
		public float MinDistance = 5f;
		public float MaxDistance = 25f;
		public float DrawTime = 1.5f;  // Tiempo para tensar el arco
		public float AimTime = 0.8f;   // Tiempo de apuntado
		public string DrawSound = "Audio/BowDraw";
		public string FireSound = "Audio/Bow";
		public string ArrowSound = "Audio/ArrowFlight";
		public float FireSoundDistance = 15f;
		public float Accuracy = 0.03f;
		public bool UseRecoil = false;  // El arco no tiene mucho retroceso
		public float ArrowSpeed = 40f;
		public bool CycleArrowTypes = true;

		// Tipos de flechas a usar (excluyendo bolts que son para ballesta)
		public ArrowBlock.ArrowType[] AvailableArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.CopperArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.FireArrow
		};

		// Estado de animación
		private bool m_isAiming = false;
		private bool m_isDrawing = false;  // Tensando el arco
		private bool m_isFiring = false;
		private double m_animationStartTime;
		private double m_drawStartTime;
		private double m_fireTime;
		private int m_bowSlot = -1;
		private int m_currentArrowTypeIndex = 0;
		private float m_currentDraw = 0f;  // 0-1 para progreso de tensado
		private Random m_random = new Random();

		// UpdateOrder
		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros
			MinDistance = valuesDictionary.GetValue<float>("MinDistance", 5f);
			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 25f);
			DrawTime = valuesDictionary.GetValue<float>("DrawTime", 1.5f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 0.8f);
			DrawSound = valuesDictionary.GetValue<string>("DrawSound", "Audio/BowDraw");
			FireSound = valuesDictionary.GetValue<string>("FireSound", "Audio/Bow");
			ArrowSound = valuesDictionary.GetValue<string>("ArrowSound", "Audio/ArrowFlight");
			FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 15f);
			Accuracy = valuesDictionary.GetValue<float>("Accuracy", 0.03f);
			UseRecoil = valuesDictionary.GetValue<bool>("UseRecoil", false);
			ArrowSpeed = valuesDictionary.GetValue<float>("ArrowSpeed", 40f);
			CycleArrowTypes = valuesDictionary.GetValue<bool>("CycleArrowTypes", true);

			// Inicializar componentes
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
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

			// Lógica de ataque
			if (distance >= MinDistance && distance <= MaxDistance)
			{
				// Si no está haciendo nada, empezar a apuntar
				if (!m_isAiming && !m_isDrawing && !m_isFiring)
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

				// Después de tiempo de apuntado, empezar a tensar
				if (m_subsystemTime.GameTime - m_animationStartTime >= AimTime)
				{
					m_isAiming = false;
					StartDrawing();
				}
			}
			else if (m_isDrawing)
			{
				ApplyDrawingAnimation(dt);

				// Actualizar progreso de tensado (0 a 1)
				m_currentDraw = MathUtils.Clamp((float)((m_subsystemTime.GameTime - m_drawStartTime) / DrawTime), 0f, 1f);

				// Después de tiempo de tensado, disparar
				if (m_subsystemTime.GameTime - m_drawStartTime >= DrawTime)
				{
					Fire();
				}
			}
			else if (m_isFiring)
			{
				ApplyFiringAnimation(dt);

				// Después de animación de disparo, volver a apuntar
				if (m_subsystemTime.GameTime - m_fireTime >= 0.5)
				{
					m_isFiring = false;

					// Ciclar al siguiente tipo de flecha si está habilitado
					if (CycleArrowTypes && AvailableArrowTypes.Length > 1)
					{
						m_currentArrowTypeIndex = (m_currentArrowTypeIndex + 1) % AvailableArrowTypes.Length;
					}

					// Volver a apuntar para repetir ciclo
					StartAiming();
				}
			}
		}

		private void FindBow()
		{
			// Buscar arco por ID de bloque (191 es el ID del arco según BowBlock.Index)
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0 && Terrain.ExtractContents(slotValue) == 191)
				{
					m_bowSlot = i;
					m_componentInventory.ActiveSlotIndex = i;
					break;
				}
			}
		}

		private void StartAiming()
		{
			m_isAiming = true;
			m_isDrawing = false;
			m_isFiring = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;

			// Encontrar arco en inventario (opcional, para animaciones)
			FindBow();
		}

		private void ApplyAimingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE APUNTADO CON ARCO - POSICIÓN PREPARATORIA
				m_componentModel.AimHandAngleOrder = 0.8f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(0.1f, 0.1f, 0.05f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-0.5f, 0.3f, 0f);

				// Mirar al objetivo
				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void StartDrawing()
		{
			m_isAiming = false;
			m_isDrawing = true;
			m_isFiring = false;
			m_drawStartTime = m_subsystemTime.GameTime;

			// Sonido de tensar el arco
			if (!string.IsNullOrEmpty(DrawSound))
			{
				m_subsystemAudio.PlaySound(DrawSound, 0.7f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 5f, false);
			}
		}

		private void ApplyDrawingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE TENSAR EL ARCO - SIMULAR EL ESFUERZO
				// El arco se mueve hacia atrás a medida que se tensa
				float drawFactor = 0.5f + (m_currentDraw * 0.8f); // Aumenta el ángulo según tensión

				m_componentModel.AimHandAngleOrder = drawFactor;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					0.1f - (0.15f * m_currentDraw),  // Se mueve hacia atrás
					0.1f + (0.05f * m_currentDraw),   // Se eleva un poco
					0.05f - (0.02f * m_currentDraw)   // Se acerca al cuerpo
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					-0.5f - (0.3f * m_currentDraw),  // Inclina más
					0.3f,
					0f
				);

				// Aplicar pequeña vibración de esfuerzo
				if (m_currentDraw > 0.7f)
				{
					float shake = (float)Math.Sin(m_subsystemTime.GameTime * 30f) * 0.01f * (m_currentDraw - 0.7f) / 0.3f;
					m_componentModel.InHandItemOffsetOrder += new Vector3(shake, shake * 0.5f, 0f);
				}
			}
		}

		private void Fire()
		{
			m_isDrawing = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			// Sonido de disparo
			if (!string.IsNullOrEmpty(FireSound))
			{
				m_subsystemAudio.PlaySound(FireSound, 1f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, FireSoundDistance, false);
			}

			// Sonido de vuelo de flecha
			if (!string.IsNullOrEmpty(ArrowSound))
			{
				m_subsystemAudio.PlaySound(ArrowSound, 0.5f, m_random.Float(-0.2f, 0.2f),
					m_componentCreature.ComponentBody.Position, 10f, false);
			}

			// Disparar flecha real
			ShootArrow();
		}

		private void ApplyFiringAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE LIBERACIÓN - EL ARCO VUELVE A POSICIÓN RELAJADA
				float fireProgress = (float)((m_subsystemTime.GameTime - m_fireTime) / 0.5);
				float returnFactor = MathUtils.Lerp(1.0f, 0.0f, fireProgress * 2f); // Rápido al principio

				// Transición suave a posición de descanso
				m_componentModel.AimHandAngleOrder = 0.8f * returnFactor;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					0.1f * returnFactor,
					0.1f * returnFactor,
					0.05f * returnFactor
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					-0.5f * returnFactor,
					0.3f * returnFactor,
					0f
				);

				// Pequeño retroceso rápido
				if (fireProgress < 0.1f)
				{
					float recoil = 0.05f * (1f - (fireProgress * 10f));
					m_componentModel.InHandItemOffsetOrder += new Vector3(recoil, 0f, 0f);
				}
			}
		}

		private void ResetAnimations()
		{
			m_isAiming = false;
			m_isDrawing = false;
			m_isFiring = false;
			m_currentDraw = 0f;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ShootArrow()
		{
			if (m_componentChaseBehavior.Target == null)
				return;

			try
			{
				// Obtener el tipo de flecha actual
				ArrowBlock.ArrowType arrowType = AvailableArrowTypes[m_currentArrowTypeIndex];

				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;

				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				// Aplicar error de puntería según configuración y tensión actual
				// Mejor precisión cuando el arco está completamente tensado
				float currentAccuracy = Accuracy * (1.5f - m_currentDraw); // Mejora con tensión

				direction += new Vector3(
					m_random.Float(-currentAccuracy, currentAccuracy),
					m_random.Float(-currentAccuracy * 0.5f, currentAccuracy * 0.5f),
					m_random.Float(-currentAccuracy, currentAccuracy)
				);
				direction = Vector3.Normalize(direction);

				// Velocidad según tensión del arco
				float speedMultiplier = 0.5f + (m_currentDraw * 1.5f); // 0.5 a 2.0
				float currentSpeed = ArrowSpeed * speedMultiplier;

				// Crear flecha (dispara ilimitadamente sin consumir recursos)
				int arrowBlockIndex = 192; // ArrowBlock.Index según xd 4.cs

				// Usar el método SetArrowType para configurar el tipo de flecha
				int arrowData = ArrowBlock.SetArrowType(0, arrowType);
				int arrowValue = Terrain.MakeBlockValue(arrowBlockIndex, 0, arrowData);

				// Disparar flecha
				var projectile = m_subsystemProjectiles.FireProjectile(
					arrowValue,
					firePosition,
					direction * currentSpeed,
					Vector3.Zero,
					m_componentCreature
				);

				// Para flechas de fuego, agregar efectos especiales
				if (arrowType == ArrowBlock.ArrowType.FireArrow && projectile != null)
				{
					// Agregar rastro de humo para flechas de fuego (similar a SubsystemArrowBlockBehavior)
					m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero,
						new SmokeTrailParticleSystem(20, 0.5f, float.MaxValue, Color.White));
					projectile.IsIncendiary = true;
				}

				// AGREGAR PARTÍCULAS DE POLVO DE CUERDA (opcional)
				if (m_subsystemParticles != null)
				{
					Vector3 particlePosition = firePosition + direction * 0.5f;
					for (int i = 0; i < 3; i++)
					{
						Vector3 velocity = direction * 2f + new Vector3(
							m_random.Float(-0.5f, 0.5f),
							m_random.Float(-0.5f, 0.5f),
							m_random.Float(-0.5f, 0.5f)
						);

						// Podrías crear un sistema de partículas simple aquí
						// m_subsystemParticles.AddParticleSystem(...);
					}
				}
			}
			catch (Exception ex)
			{
				// Ignorar errores de disparo, pero podrías loguearlos
				// Log.Error($"Error disparando flecha: {ex.Message}");
			}
		}
	}
}
