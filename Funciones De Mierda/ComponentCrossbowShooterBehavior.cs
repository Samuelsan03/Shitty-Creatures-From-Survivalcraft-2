using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCrossbowShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Componentes necesarios
		private ComponentCreature m_componentCreature;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentInventory m_componentInventory;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private ComponentCreatureModel m_componentModel;
		private ComponentNewHumanModel m_componentNewHumanModel; // Nueva referencia
		private SubsystemParticles m_subsystemParticles;
		private SubsystemNoise m_subsystemNoise;

		// Configuración
		public float MaxDistance = 25f;
		public float DrawTime = 1.5f;  // Tiempo para tensar la ballesta
		public float AimTime = 0.5f;
		public float ReloadTime = 0.8f; // Tiempo para recargar después de disparar
		public float FireSoundDistance = 15f;
		public float Accuracy = 0.02f;
		public float BoltSpeed = 45f;
		public bool CycleBoltTypes = true;
		public bool ShowBoltWhenIdle = false;

		// Tipos de virotes a usar (solo los compatibles con ballesta)
		public ArrowBlock.ArrowType[] AvailableBoltTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
		};

		// Estado
		private bool m_isAiming = false;
		private bool m_isDrawing = false;
		private bool m_isFiring = false;
		private bool m_isReloading = false;
		private double m_animationStartTime;
		private double m_drawStartTime;
		private double m_fireTime;
		private int m_crossbowSlot = -1;
		private int m_currentBoltTypeIndex = 0;
		private float m_currentDraw = 0f;
		private Random m_random = new Random();
		private bool m_initialized = false;
		private bool m_hasCycledForNextShot = false;

		// Variables para suavizado de animaciones
		private float m_smoothedAimHandAngle = 0f;
		private Vector3 m_smoothedItemOffset = Vector3.Zero;
		private Vector3 m_smoothedItemRotation = Vector3.Zero;
		private float m_animationSmoothFactor = 0.18f; // Factor de suavizado igual que NewHumanModel
		private float m_aimSmoothFactor = 0.3f; // Factor específico para apuntar

		// UpdateOrder
		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros
			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 25f);
			DrawTime = valuesDictionary.GetValue<float>("DrawTime", 1.5f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 0.5f);
			ReloadTime = valuesDictionary.GetValue<float>("ReloadTime", 0.8f);
			FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 15f);
			Accuracy = valuesDictionary.GetValue<float>("Accuracy", 0.02f);
			BoltSpeed = valuesDictionary.GetValue<float>("BoltSpeed", 45f);
			CycleBoltTypes = valuesDictionary.GetValue<bool>("CycleBoltTypes", true);
			ShowBoltWhenIdle = valuesDictionary.GetValue<bool>("ShowBoltWhenIdle", false);

			// Inicializar componentes
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);

			// Intentar primero obtener el ComponentNewHumanModel para mejor fluidez
			m_componentNewHumanModel = base.Entity.FindComponent<ComponentNewHumanModel>(false);
			if (m_componentNewHumanModel != null)
			{
				m_componentModel = m_componentNewHumanModel;
				// Ajustar parámetros de suavizado del nuevo modelo
				m_componentNewHumanModel.SetAimSmoothness(0.4f); // Valor óptimo para ballesta
			}
			else
			{
				// Fallback al modelo humano normal
				m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			}

			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
		}

		public override void OnEntityAdded()
		{
			base.OnEntityAdded();

			// Inicializar con virote aleatorio
			m_currentBoltTypeIndex = m_random.Int(0, AvailableBoltTypes.Length);
			m_initialized = true;
			m_hasCycledForNextShot = false;

			// Buscar ballesta
			FindCrossbow();

			// Inicializar valores suavizados
			m_smoothedAimHandAngle = 0f;
			m_smoothedItemOffset = Vector3.Zero;
			m_smoothedItemRotation = Vector3.Zero;

			// Mostrar virote inicialmente si está configurado
			if (ShowBoltWhenIdle && m_crossbowSlot >= 0)
			{
				SetCrossbowWithBolt(0, false); // Ballesta sin tensar, sin virote
			}
		}

		public void Update(float dt)
		{
			if (!m_initialized || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			// Verificar objetivo
			if (m_componentChaseBehavior.Target == null)
			{
				ResetAnimations();
				// Mantener ballesta en estado idle
				if (ShowBoltWhenIdle && m_crossbowSlot >= 0)
				{
					SetCrossbowWithBolt(0, false);
				}
				return;
			}

			// Calcular distancia
			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				m_componentChaseBehavior.Target.ComponentBody.Position
			);

			// Lógica de ataque - Solo verifica distancia máxima
			if (distance <= MaxDistance)
			{
				if (!m_isAiming && !m_isDrawing && !m_isFiring && !m_isReloading)
				{
					StartAiming();
				}
			}
			else
			{
				ResetAnimations();
				if (ShowBoltWhenIdle && m_crossbowSlot >= 0)
				{
					SetCrossbowWithBolt(0, false);
				}
				return;
			}

			// Aplicar suavizado de animaciones (igual que NewHumanModel)
			ApplySmoothAnimations(dt);

			// Lógica de estados
			if (m_isAiming)
			{
				ApplyAimingAnimation(dt);

				if (m_subsystemTime.GameTime - m_animationStartTime >= AimTime)
				{
					m_isAiming = false;
					StartDrawing();
				}
			}
			else if (m_isDrawing)
			{
				ApplyDrawingAnimation(dt);

				m_currentDraw = MathUtils.Clamp((float)((m_subsystemTime.GameTime - m_drawStartTime) / DrawTime), 0f, 1f);

				// Actualizar tensión visual
				SetCrossbowWithBolt((int)(m_currentDraw * 15f), false);

				if (m_subsystemTime.GameTime - m_drawStartTime >= DrawTime)
				{
					// Tensado completo, cargar virote
					LoadBolt();
				}
			}
			else if (m_isReloading)
			{
				ApplyReloadingAnimation(dt);

				// Después de cargar el virote, disparar inmediatamente
				if (m_subsystemTime.GameTime - m_animationStartTime >= 0.3f)
				{
					m_isReloading = false;
					Fire();
				}
			}
			else if (m_isFiring)
			{
				ApplyFiringAnimation(dt);

				if (m_subsystemTime.GameTime - m_fireTime >= 0.2f)
				{
					m_isFiring = false;

					// Quitar virote después de disparar
					ClearBoltFromCrossbow();

					// IMPORTANTE: Ciclar para el próximo disparo aquí
					if (CycleBoltTypes && AvailableBoltTypes.Length > 1)
					{
						m_currentBoltTypeIndex = (m_currentBoltTypeIndex + 1) % AvailableBoltTypes.Length;
						m_hasCycledForNextShot = true;
					}
					else if (!CycleBoltTypes && AvailableBoltTypes.Length > 0)
					{
						m_currentBoltTypeIndex = 0;
					}

					// Pausa antes de recargar
					if (m_subsystemTime.GameTime - m_fireTime >= 0.8f)
					{
						StartAiming();
					}
				}
			}
		}

		// Nuevo método: Aplicar suavizado igual que NewHumanModel
		private void ApplySmoothAnimations(float dt)
		{
			float smoothSpeed = MathUtils.Min(10f * dt, 0.85f); // Igual que NewHumanModel
			float aimSmoothSpeed = MathUtils.Min(5f * dt, 0.9f); // Igual que NewHumanModel

			// Si tenemos ComponentNewHumanModel, usar su sistema de suavizado interno
			if (m_componentNewHumanModel != null)
			{
				// El modelo ya maneja el suavizado internamente
				return;
			}

			// Suavizado manual para el modelo normal
			if (m_componentModel != null)
			{
				// Suavizar el ángulo de apuntar
				float targetAimAngle = m_componentModel.AimHandAngleOrder;
				m_smoothedAimHandAngle = MathUtils.Lerp(m_smoothedAimHandAngle,
					targetAimAngle, aimSmoothSpeed * 0.7f);

				// Aplicar ángulo suavizado
				m_componentModel.AimHandAngleOrder = m_smoothedAimHandAngle;

				// Suavizar offset del ítem
				Vector3 targetOffset = m_componentModel.InHandItemOffsetOrder;
				m_smoothedItemOffset = Vector3.Lerp(m_smoothedItemOffset,
					targetOffset, smoothSpeed);

				// Suavizar rotación del ítem
				Vector3 targetRotation = m_componentModel.InHandItemRotationOrder;
				m_smoothedItemRotation = Vector3.Lerp(m_smoothedItemRotation,
					targetRotation, smoothSpeed);

				// Aplicar valores suavizados
				m_componentModel.InHandItemOffsetOrder = m_smoothedItemOffset;
				m_componentModel.InHandItemRotationOrder = m_smoothedItemRotation;
			}
		}

		private void FindCrossbow()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					if (block is CrossbowBlock)
					{
						m_crossbowSlot = i;
						m_componentInventory.ActiveSlotIndex = i;
						break;
					}
				}
			}
		}

		private void SetCrossbowWithBolt(int drawValue, bool hasBolt)
		{
			if (m_crossbowSlot < 0)
			{
				FindCrossbow();
				if (m_crossbowSlot < 0) return;
			}

			try
			{
				int currentCrossbowValue = m_componentInventory.GetSlotValue(m_crossbowSlot);
				if (currentCrossbowValue == 0) return;

				int currentData = Terrain.ExtractData(currentCrossbowValue);

				// Asegurarnos de que el índice está dentro del rango
				ArrowBlock.ArrowType? boltType = null;
				if (hasBolt && AvailableBoltTypes.Length > 0)
				{
					// IMPORTANTE: Usar el índice actual para este disparo
					int indexToUse = m_currentBoltTypeIndex;

					// Si ya ciclamos para el próximo disparo, usar el índice anterior
					if (m_hasCycledForNextShot && CycleBoltTypes && AvailableBoltTypes.Length > 1)
					{
						indexToUse = (m_currentBoltTypeIndex - 1 + AvailableBoltTypes.Length) % AvailableBoltTypes.Length;
					}

					if (indexToUse >= 0 && indexToUse < AvailableBoltTypes.Length)
					{
						boltType = AvailableBoltTypes[indexToUse];
					}
					else
					{
						indexToUse = 0;
						boltType = AvailableBoltTypes[0];
					}
				}

				// Configurar ballesta con tensión y virote
				int newData = CrossbowBlock.SetDraw(currentData, MathUtils.Clamp(drawValue, 0, 15));
				newData = CrossbowBlock.SetArrowType(newData, boltType);

				// Actualizar la ballesta en el inventario
				int newCrossbowValue = Terrain.ReplaceData(currentCrossbowValue, newData);
				m_componentInventory.RemoveSlotItems(m_crossbowSlot, 1);
				m_componentInventory.AddSlotItems(m_crossbowSlot, newCrossbowValue, 1);
			}
			catch (Exception ex)
			{
				// Log.Error($"Error en SetCrossbowWithBolt: {ex.Message}");
			}
		}

		private void ClearBoltFromCrossbow()
		{
			SetCrossbowWithBolt(0, false);
		}

		private void StartAiming()
		{
			m_isAiming = true;
			m_isDrawing = false;
			m_isFiring = false;
			m_isReloading = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;
			m_hasCycledForNextShot = false; // Resetear el flag de ciclado

			// Mostrar ballesta sin virote
			SetCrossbowWithBolt(0, false);
		}

		private void ApplyAimingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE APUNTADO - VERTICAL COMO MOSQUETE
				// La ballesta se sostiene verticalmente con brazo alto
				float targetAimAngle = 1.4f;
				Vector3 targetOffset = new Vector3(-0.08f, -0.08f, 0.07f);
				Vector3 targetRotation = new Vector3(-1.7f, 0f, 0f);

				// Si no tenemos el nuevo modelo, aplicar valores directamente
				if (m_componentNewHumanModel == null)
				{
					m_componentModel.AimHandAngleOrder = targetAimAngle;
					m_componentModel.InHandItemOffsetOrder = targetOffset;
					m_componentModel.InHandItemRotationOrder = targetRotation;
				}
				else
				{
					// El nuevo modelo maneja el suavizado internamente
					m_componentModel.AimHandAngleOrder = targetAimAngle;
					m_componentModel.InHandItemOffsetOrder = targetOffset;
					m_componentModel.InHandItemRotationOrder = targetRotation;
				}

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
			m_isReloading = false;
			m_drawStartTime = m_subsystemTime.GameTime;

			// Sonido de tensado de ballesta
			m_subsystemAudio.PlaySound("Audio/CrossbowDraw", 0.5f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);
		}

		private void ApplyDrawingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				float drawFactor = m_currentDraw;

				// ANIMACIÓN DE TENSADO - MOVIMIENTO HACIA ATRÁS
				float targetAimAngle = 1.4f;
				Vector3 targetOffset = new Vector3(
					-0.08f + (0.05f * drawFactor),
					-0.08f,
					0.07f - (0.03f * drawFactor)
				);
				Vector3 targetRotation = new Vector3(-1.7f, 0f, 0f);

				// Si no tenemos el nuevo modelo, aplicar valores directamente
				if (m_componentNewHumanModel == null)
				{
					m_componentModel.AimHandAngleOrder = targetAimAngle;
					m_componentModel.InHandItemOffsetOrder = targetOffset;
					m_componentModel.InHandItemRotationOrder = targetRotation;
				}
				else
				{
					// El nuevo modelo maneja el suavizado internamente
					m_componentModel.AimHandAngleOrder = targetAimAngle;
					m_componentModel.InHandItemOffsetOrder = targetOffset;
					m_componentModel.InHandItemRotationOrder = targetRotation;
				}

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void LoadBolt()
		{
			m_isDrawing = false;
			m_isReloading = true;
			m_animationStartTime = m_subsystemTime.GameTime;

			// Cargar virote en la ballesta (tensada completamente)
			SetCrossbowWithBolt(15, true);

			// Sonido de recarga
			m_subsystemAudio.PlaySound("Audio/Reload", 1f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);
		}

		private void ApplyReloadingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE CARGA - PEQUEÑO MOVIMIENTO HACIA ABAJO
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / 0.3f);

				float targetAimAngle = 1.4f;
				Vector3 targetOffset = new Vector3(
					-0.08f,
					-0.08f - (0.05f * reloadProgress),
					0.07f
				);
				Vector3 targetRotation = new Vector3(-1.7f, 0f, 0f);

				// Si no tenemos el nuevo modelo, aplicar valores directamente
				if (m_componentNewHumanModel == null)
				{
					m_componentModel.AimHandAngleOrder = targetAimAngle;
					m_componentModel.InHandItemOffsetOrder = targetOffset;
					m_componentModel.InHandItemRotationOrder = targetRotation;
				}
				else
				{
					// El nuevo modelo maneja el suavizado internamente
					m_componentModel.AimHandAngleOrder = targetAimAngle;
					m_componentModel.InHandItemOffsetOrder = targetOffset;
					m_componentModel.InHandItemRotationOrder = targetRotation;
				}

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void Fire()
		{
			m_isReloading = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			// Disparar virote (usar el índice actual sin ciclar todavía)
			ShootBolt();

			// Sonido de disparo de ballesta
			m_subsystemAudio.PlaySound("Audio/Bow", 0.8f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, FireSoundDistance, false);

			// Retroceso ligero
			if (m_componentChaseBehavior.Target != null)
			{
				Vector3 direction = Vector3.Normalize(
					m_componentChaseBehavior.Target.ComponentBody.Position -
					m_componentCreature.ComponentBody.Position
				);
				m_componentCreature.ComponentBody.ApplyImpulse(-direction * 1.5f);
			}
		}

		private void ApplyFiringAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				float fireProgress = (float)((m_subsystemTime.GameTime - m_fireTime) / 0.2f);

				if (fireProgress < 0.5f)
				{
					// Pequeño retroceso
					float recoil = 0.05f * (1f - (fireProgress * 2f));

					Vector3 targetOffset = m_componentModel.InHandItemOffsetOrder + new Vector3(recoil, 0f, 0f);
					Vector3 targetRotation = m_componentModel.InHandItemRotationOrder + new Vector3(recoil * 2f, 0f, 0f);
					float targetAimAngle = 1.4f;

					// Si no tenemos el nuevo modelo, aplicar valores directamente
					if (m_componentNewHumanModel == null)
					{
						m_componentModel.AimHandAngleOrder = targetAimAngle;
						m_componentModel.InHandItemOffsetOrder = targetOffset;
						m_componentModel.InHandItemRotationOrder = targetRotation;
					}
					else
					{
						// El nuevo modelo maneja el suavizado internamente
						m_componentModel.AimHandAngleOrder = targetAimAngle;
						m_componentModel.InHandItemOffsetOrder = targetOffset;
						m_componentModel.InHandItemRotationOrder = targetRotation;
					}
				}
				else
				{
					// Volver a posición normal gradualmente
					float returnProgress = (fireProgress - 0.5f) / 0.5f;

					float targetAimAngle = 1.4f * (1f - returnProgress);
					Vector3 targetOffset = new Vector3(
						-0.08f * (1f - returnProgress),
						-0.08f * (1f - returnProgress),
						0.07f * (1f - returnProgress)
					);
					Vector3 targetRotation = new Vector3(
						-1.7f * (1f - returnProgress),
						0f,
						0f
					);

					// Si no tenemos el nuevo modelo, aplicar valores directamente
					if (m_componentNewHumanModel == null)
					{
						m_componentModel.AimHandAngleOrder = targetAimAngle;
						m_componentModel.InHandItemOffsetOrder = targetOffset;
						m_componentModel.InHandItemRotationOrder = targetRotation;
					}
					else
					{
						// El nuevo modelo maneja el suavizado internamente
						m_componentModel.AimHandAngleOrder = targetAimAngle;
						m_componentModel.InHandItemOffsetOrder = targetOffset;
						m_componentModel.InHandItemRotationOrder = targetRotation;
					}
				}
			}
		}

		private void ResetAnimations()
		{
			m_isAiming = false;
			m_isDrawing = false;
			m_isFiring = false;
			m_isReloading = false;
			m_currentDraw = 0f;
			m_hasCycledForNextShot = false;

			// Resetear valores suavizados
			m_smoothedAimHandAngle = 0f;
			m_smoothedItemOffset = Vector3.Zero;
			m_smoothedItemRotation = Vector3.Zero;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ShootBolt()
		{
			if (m_componentChaseBehavior.Target == null)
				return;

			try
			{
				// IMPORTANTE: Usar el índice actual sin ciclar todavía
				int indexToUse = m_currentBoltTypeIndex;

				// Si ya ciclamos para el próximo disparo, usar el índice anterior
				if (m_hasCycledForNextShot && CycleBoltTypes && AvailableBoltTypes.Length > 1)
				{
					indexToUse = (m_currentBoltTypeIndex - 1 + AvailableBoltTypes.Length) % AvailableBoltTypes.Length;
				}

				if (indexToUse < 0 || indexToUse >= AvailableBoltTypes.Length)
				{
					indexToUse = 0;
				}

				ArrowBlock.ArrowType boltType = AvailableBoltTypes[indexToUse];

				// Posición de disparo
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				firePosition.Y -= 0.1f;

				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				// Aplicar precisión
				direction += new Vector3(
					m_random.Float(-Accuracy, Accuracy),
					m_random.Float(-Accuracy * 0.5f, Accuracy * 0.5f),
					m_random.Float(-Accuracy, Accuracy)
				);
				direction = Vector3.Normalize(direction);

				// Velocidad del virote (más rápido que flechas normales)
				float speed = BoltSpeed * (0.8f + (m_currentDraw * 0.4f));

				int boltData = ArrowBlock.SetArrowType(0, boltType);
				int boltValue = Terrain.MakeBlockValue(ArrowBlock.Index, 0, boltData);

				var projectile = m_subsystemProjectiles.FireProjectile(
					boltValue,
					firePosition,
					direction * speed,
					Vector3.Zero,
					m_componentCreature
				);

				// Configurar propiedades según el tipo de virote
				if (boltType == ArrowBlock.ArrowType.ExplosiveBolt && projectile != null)
				{
					projectile.IsIncendiary = false;
					// Los virotes explosivos ya tienen presión de explosión definida en ArrowBlock
				}

				// Ruido
				if (m_subsystemNoise != null)
				{
					m_subsystemNoise.MakeNoise(firePosition, 0.5f, 20f);
				}
			}
			catch (Exception ex)
			{
				m_currentBoltTypeIndex = 0;
				m_hasCycledForNextShot = false;
			}
		}
	}
}
