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
		private ComponentNewHumanModel m_componentNewHumanModel;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemNoise m_subsystemNoise;

		// Configuración
		public float MaxDistance = 25f;
		public float DrawTime = 1.5f;
		public float AimTime = 0.5f;
		public float ReloadTime = 0.8f;
		public float FireSoundDistance = 15f;
		public float Accuracy = 0.02f;
		public float BoltSpeed = 45f;
		public float MinExplosiveDistance = 20f; // Distancia mínima segura para virotes explosivos

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
		private float m_animationSmoothFactor = 0.18f;
		private float m_aimSmoothFactor = 0.3f;

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
			MinExplosiveDistance = valuesDictionary.GetValue<float>("MinExplosiveDistance", 20f);

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
				m_componentNewHumanModel.SetAimSmoothness(0.4f);
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

			// Inicializar con virote aleatorio (no explosivo por seguridad)
			m_currentBoltTypeIndex = GetRandomNonExplosiveBoltIndex();
			m_initialized = true;
			m_hasCycledForNextShot = false;

			// Buscar ballesta
			FindCrossbow();

			// Inicializar valores suavizados
			m_smoothedAimHandAngle = 0f;
			m_smoothedItemOffset = Vector3.Zero;
			m_smoothedItemRotation = Vector3.Zero;

			// Ballesta sin virote inicialmente
			if (m_crossbowSlot >= 0)
			{
				SetCrossbowWithBolt(0, false);
			}
		}

		private ComponentCreature GetChaseTarget()
		{
			// Fallback al ComponentChaseBehavior original
			if (m_componentChaseBehavior != null && m_componentChaseBehavior.Target != null)
				return m_componentChaseBehavior.Target;

			return null;
		}

		private int GetRandomNonExplosiveBoltIndex()
		{
			// Si solo hay un tipo o es el único disponible, retornar 0
			if (AvailableBoltTypes.Length <= 1)
				return 0;

			// Crear lista de índices no explosivos
			List<int> nonExplosiveIndices = new List<int>();
			for (int i = 0; i < AvailableBoltTypes.Length; i++)
			{
				if (AvailableBoltTypes[i] != ArrowBlock.ArrowType.ExplosiveBolt)
				{
					nonExplosiveIndices.Add(i);
				}
			}

			// Si hay índices no explosivos, elegir uno aleatorio
			if (nonExplosiveIndices.Count > 0)
			{
				return nonExplosiveIndices[m_random.Int(0, nonExplosiveIndices.Count - 1)];
			}

			// Si todos son explosivos (no debería pasar), retornar 0
			return 0;
		}

		private int GetSafeBoltIndexForDistance(float distance)
		{
			int currentIndex = m_currentBoltTypeIndex;

			// Verificar si el tipo actual es explosivo y la distancia es peligrosa
			if (currentIndex >= 0 && currentIndex < AvailableBoltTypes.Length)
			{
				if (AvailableBoltTypes[currentIndex] == ArrowBlock.ArrowType.ExplosiveBolt && distance < MinExplosiveDistance)
				{
					// Buscar un tipo no explosivo para esta distancia
					for (int i = 0; i < AvailableBoltTypes.Length; i++)
					{
						if (AvailableBoltTypes[i] != ArrowBlock.ArrowType.ExplosiveBolt)
						{
							return i; // Retornar el primer no explosivo encontrado
						}
					}
					// Si todos son explosivos, usar el actual (no hay alternativa segura)
					return currentIndex;
				}
			}

			// Si no es explosivo o la distancia es segura, usar el índice actual
			return currentIndex;
		}

		public void Update(float dt)
		{
			if (!m_initialized || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			// Verificar objetivo usando el nuevo método
			ComponentCreature target = GetChaseTarget();

			if (target == null)
			{
				ResetAnimations();
				// Mantener ballesta sin virote
				if (m_crossbowSlot >= 0)
				{
					SetCrossbowWithBolt(0, false);
				}
				return;
			}

			// Calcular distancia
			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				target.ComponentBody.Position
			);

			// Verificar si debemos cambiar a un tipo de virote seguro según la distancia
			int safeBoltIndex = GetSafeBoltIndexForDistance(distance);
			if (safeBoltIndex != m_currentBoltTypeIndex)
			{
				m_currentBoltTypeIndex = safeBoltIndex;
				m_hasCycledForNextShot = false; // Resetear el flag de ciclado
			}

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
				if (m_crossbowSlot >= 0)
				{
					SetCrossbowWithBolt(0, false);
				}
				return;
			}

			// Aplicar suavizado de animaciones
			ApplySmoothAnimations(dt);

			// Lógica de estados
			if (m_isAiming)
			{
				ApplyAimingAnimation(dt, target);

				if (m_subsystemTime.GameTime - m_animationStartTime >= AimTime)
				{
					m_isAiming = false;
					StartDrawing();
				}
			}
			else if (m_isDrawing)
			{
				ApplyDrawingAnimation(dt, target);

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
				ApplyReloadingAnimation(dt, target);

				// Después de cargar el virote, disparar inmediatamente
				if (m_subsystemTime.GameTime - m_animationStartTime >= 0.3f)
				{
					m_isReloading = false;
					Fire(target);
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

					// Ciclar para el próximo disparo
					if (AvailableBoltTypes.Length > 1)
					{
						// Intentar avanzar al siguiente tipo, pero verificar seguridad
						int nextIndex = (m_currentBoltTypeIndex + 1) % AvailableBoltTypes.Length;

						// Si el siguiente es explosivo y la distancia actual es peligrosa, buscar alternativas
						if (AvailableBoltTypes[nextIndex] == ArrowBlock.ArrowType.ExplosiveBolt && distance < MinExplosiveDistance)
						{
							// Buscar el siguiente tipo no explosivo
							bool found = false;
							for (int i = 1; i <= AvailableBoltTypes.Length; i++)
							{
								int candidateIndex = (m_currentBoltTypeIndex + i) % AvailableBoltTypes.Length;
								if (AvailableBoltTypes[candidateIndex] != ArrowBlock.ArrowType.ExplosiveBolt)
								{
									m_currentBoltTypeIndex = candidateIndex;
									found = true;
									break;
								}
							}
							if (!found)
							{
								// Si todos son explosivos, usar el siguiente (no hay alternativa)
								m_currentBoltTypeIndex = nextIndex;
							}
						}
						else
						{
							// Seguro para usar el siguiente tipo
							m_currentBoltTypeIndex = nextIndex;
						}

						m_hasCycledForNextShot = true;
					}

					// Pausa antes de recargar
					if (m_subsystemTime.GameTime - m_fireTime >= 0.8f)
					{
						StartAiming();
					}
				}
			}
		}

		// Aplicar suavizado igual que NewHumanModel
		private void ApplySmoothAnimations(float dt)
		{
			float smoothSpeed = MathUtils.Min(10f * dt, 0.85f);
			float aimSmoothSpeed = MathUtils.Min(5f * dt, 0.9f);

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
					int indexToUse = m_currentBoltTypeIndex;

					// Si ya ciclamos para el próximo disparo, usar el índice anterior
					if (m_hasCycledForNextShot && AvailableBoltTypes.Length > 1)
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
			m_hasCycledForNextShot = false;

			// Mostrar ballesta sin virote
			SetCrossbowWithBolt(0, false);
		}

		private void ApplyAimingAnimation(float dt, ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE APUNTADO - VERTICAL COMO MOSQUETE
				float targetAimAngle = 1.4f;
				Vector3 targetOffset = new Vector3(-0.08f, -0.08f, 0.07f);
				Vector3 targetRotation = new Vector3(-1.7f, 0f, 0f);

				m_componentModel.AimHandAngleOrder = targetAimAngle;
				m_componentModel.InHandItemOffsetOrder = targetOffset;
				m_componentModel.InHandItemRotationOrder = targetRotation;

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						target.ComponentCreatureModel.EyePosition
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

		private void ApplyDrawingAnimation(float dt, ComponentCreature target)
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

				m_componentModel.AimHandAngleOrder = targetAimAngle;
				m_componentModel.InHandItemOffsetOrder = targetOffset;
				m_componentModel.InHandItemRotationOrder = targetRotation;

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						target.ComponentCreatureModel.EyePosition
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

		private void ApplyReloadingAnimation(float dt, ComponentCreature target)
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

				m_componentModel.AimHandAngleOrder = targetAimAngle;
				m_componentModel.InHandItemOffsetOrder = targetOffset;
				m_componentModel.InHandItemRotationOrder = targetRotation;

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void Fire(ComponentCreature target)
		{
			m_isReloading = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			// Disparar virote
			ShootBolt(target);

			// Sonido de disparo de ballesta
			m_subsystemAudio.PlaySound("Audio/Bow", 0.8f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, FireSoundDistance, false);

			// Retroceso ligero
			if (target != null)
			{
				Vector3 direction = Vector3.Normalize(
					target.ComponentBody.Position -
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

					m_componentModel.AimHandAngleOrder = targetAimAngle;
					m_componentModel.InHandItemOffsetOrder = targetOffset;
					m_componentModel.InHandItemRotationOrder = targetRotation;
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

					m_componentModel.AimHandAngleOrder = targetAimAngle;
					m_componentModel.InHandItemOffsetOrder = targetOffset;
					m_componentModel.InHandItemRotationOrder = targetRotation;
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

		private void ShootBolt(ComponentCreature target)
		{
			if (target == null)
				return;

			try
			{
				// Obtener distancia actual para verificar seguridad
				float currentDistance = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					target.ComponentBody.Position
				);

				int indexToUse = m_currentBoltTypeIndex;

				// Si ya ciclamos para el próximo disparo, usar el índice anterior
				if (m_hasCycledForNextShot && AvailableBoltTypes.Length > 1)
				{
					indexToUse = (m_currentBoltTypeIndex - 1 + AvailableBoltTypes.Length) % AvailableBoltTypes.Length;
				}

				// Verificación de seguridad adicional antes de disparar
				if (indexToUse >= 0 && indexToUse < AvailableBoltTypes.Length)
				{
					if (AvailableBoltTypes[indexToUse] == ArrowBlock.ArrowType.ExplosiveBolt && currentDistance < MinExplosiveDistance)
					{
						// Distancia peligrosa para explosivo, buscar alternativa
						bool foundSafe = false;
						for (int i = 0; i < AvailableBoltTypes.Length; i++)
						{
							if (AvailableBoltTypes[i] != ArrowBlock.ArrowType.ExplosiveBolt)
							{
								indexToUse = i;
								foundSafe = true;
								break;
							}
						}

						if (!foundSafe)
						{
							// Si todos son explosivos, no disparar (evitar suicidio)
							return;
						}
					}
				}
				else
				{
					indexToUse = 0;
				}

				ArrowBlock.ArrowType boltType = AvailableBoltTypes[indexToUse];

				// Posición de disparo
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				firePosition.Y -= 0.1f;

				Vector3 targetPosition = target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				// Aplicar precisión
				direction += new Vector3(
					m_random.Float(-Accuracy, Accuracy),
					m_random.Float(-Accuracy * 0.5f, Accuracy * 0.5f),
					m_random.Float(-Accuracy, Accuracy)
				);
				direction = Vector3.Normalize(direction);

				// Velocidad del virote
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

				// Configurar proyectil para desaparecer después del impacto
				if (projectile != null)
				{
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;

					// Configurar propiedades según el tipo de virote
					if (boltType == ArrowBlock.ArrowType.ExplosiveBolt)
					{
						projectile.IsIncendiary = false;
					}
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
