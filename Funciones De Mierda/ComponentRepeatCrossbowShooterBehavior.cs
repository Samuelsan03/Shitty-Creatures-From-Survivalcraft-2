using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentRepeatCrossbowShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Componentes necesarios
		private ComponentCreature m_componentCreature;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentInventory m_componentInventory;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private ComponentCreatureModel m_componentModel;
		private SubsystemNoise m_subsystemNoise;
		private Random m_random = new Random();

		// Configuración - Mantener nombres originales del XML
		public float MaxDistance = 25f;
		public float DrawTime = 2.0f;    // Tiempo para tensar completamente
		public float AimTime = 0.3f;
		public float TimeBetweenShots = 0.5f;  // Tiempo entre disparos
		public float MaxInaccuracy = 0.04f;
		public float FireSoundDistance = 15f;
		public bool UseRecoil = true;
		public float BoltSpeed = 35f;

		// Estado (simplificado como el original)
		private bool m_isAiming = false;
		private bool m_isDrawing = false;
		private bool m_isFiring = false;
		private bool m_isReloading = false;
		private double m_animationStartTime;
		private double m_drawStartTime;
		private double m_fireTime;
		private int m_crossbowSlot = -1;
		private float m_currentDraw = 0f;
		private bool m_hasCrossbow = false;

		// Tipos de flechas disponibles
		private RepeatArrowBlock.ArrowType[] m_availableArrowTypes = new RepeatArrowBlock.ArrowType[]
		{
			RepeatArrowBlock.ArrowType.CopperArrow,
			RepeatArrowBlock.ArrowType.IronArrow,
			RepeatArrowBlock.ArrowType.DiamondArrow,
			RepeatArrowBlock.ArrowType.ExplosiveArrow,
			RepeatArrowBlock.ArrowType.PoisonArrow,
			RepeatArrowBlock.ArrowType.SeriousPoisonArrow
		};

		// Tipo de flecha seleccionado para el próximo disparo (basado en distancia)
		private RepeatArrowBlock.ArrowType? m_nextArrowType = null;

		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros según XML
			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 25f);
			DrawTime = valuesDictionary.GetValue<float>("DrawTime", 2.0f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 0.3f);
			TimeBetweenShots = valuesDictionary.GetValue<float>("TimeBetweenShots", 0.5f);
			MaxInaccuracy = valuesDictionary.GetValue<float>("MaxInaccuracy", 0.04f);
			FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 15f);
			UseRecoil = valuesDictionary.GetValue<bool>("UseRecoil", true);
			BoltSpeed = valuesDictionary.GetValue<float>("BoltSpeed", 35f);

			// Inicializar componentes
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
		}

		public override void OnEntityAdded()
		{
			base.OnEntityAdded();

			// Buscar ballesta repetidora
			FindCrossbow();
		}

		private ComponentCreature GetChaseTarget()
		{
			// Fallback al ComponentChaseBehavior original
			if (m_componentChaseBehavior != null && m_componentChaseBehavior.Target != null)
				return m_componentChaseBehavior.Target;

			return null;
		}

		public void Update(float dt)
		{
			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			// Verificar objetivo usando el nuevo método
			ComponentCreature target = GetChaseTarget();

			if (target == null)
			{
				ResetAnimations();
				return;
			}

			// Buscar ballesta si no la tenemos
			if (!m_hasCrossbow)
			{
				FindCrossbow();
				if (!m_hasCrossbow) return;
			}

			// Calcular distancia
			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				target.ComponentBody.Position
			);

			// Lógica de ataque - Solo verifica distancia máxima (sin mínima)
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
				return;
			}

			// Aplicar animaciones y lógica de estado
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

				// Actualizar tensión visual (0-15)
				UpdateCrossbowDraw((int)(m_currentDraw * 15f));

				if (m_subsystemTime.GameTime - m_drawStartTime >= DrawTime)
				{
					// Tensado completo, cargar flecha
					m_isDrawing = false;
					LoadArrow(target);
				}
			}
			else if (m_isReloading)
			{
				ApplyReloadingAnimation(dt, target);

				// Después de cargar la flecha, disparar inmediatamente
				if (m_subsystemTime.GameTime - m_animationStartTime >= 0.2f)
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

					// Quitar flecha después de disparar
					ClearArrowFromCrossbow();

					// Pausa antes de recargar según TimeBetweenShots del XML
					if (m_subsystemTime.GameTime - m_fireTime >= TimeBetweenShots)
					{
						StartAiming();
					}
				}
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
					if (block is RepeatCrossbowBlock)
					{
						m_crossbowSlot = i;
						m_componentInventory.ActiveSlotIndex = i;
						m_hasCrossbow = true;
						break;
					}
				}
			}

			if (!m_hasCrossbow)
			{
				m_crossbowSlot = -1;
			}
		}

		private void UpdateCrossbowDraw(int draw)
		{
			if (m_crossbowSlot < 0) return;

			int crossbowValue = m_componentInventory.GetSlotValue(m_crossbowSlot);
			if (crossbowValue == 0) return;

			int data = Terrain.ExtractData(crossbowValue);
			data = RepeatCrossbowBlock.SetDraw(data, MathUtils.Clamp(draw, 0, 15));

			UpdateInventorySlot(Terrain.ReplaceData(crossbowValue, data));
		}

		private void UpdateCrossbowArrowType(RepeatArrowBlock.ArrowType? arrowType)
		{
			if (m_crossbowSlot < 0) return;

			int crossbowValue = m_componentInventory.GetSlotValue(m_crossbowSlot);
			if (crossbowValue == 0) return;

			int data = Terrain.ExtractData(crossbowValue);
			data = RepeatCrossbowBlock.SetArrowType(data, arrowType);

			UpdateInventorySlot(Terrain.ReplaceData(crossbowValue, data));
		}

		private void UpdateInventorySlot(int value)
		{
			m_componentInventory.RemoveSlotItems(m_crossbowSlot, 1);
			m_componentInventory.AddSlotItems(m_crossbowSlot, value, 1);
		}

		private void ClearArrowFromCrossbow()
		{
			UpdateCrossbowDraw(0);
			UpdateCrossbowArrowType(null);
		}

		private void StartAiming()
		{
			m_isAiming = true;
			m_isDrawing = false;
			m_isFiring = false;
			m_isReloading = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;
			m_nextArrowType = null; // Limpiar el tipo seleccionado

			// Mostrar ballesta sin tensar y sin flecha
			UpdateCrossbowDraw(0);
			UpdateCrossbowArrowType(null);
		}

		private void ApplyAimingAnimation(float dt, ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE APUNTADO
				m_componentModel.AimHandAngleOrder = 1.3f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);

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

			// Sonido de tensado de ballesta repetidora
			m_subsystemAudio.PlaySound("Audio/Crossbow Remake/Crossbow Loading Remake", 0.5f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);
		}

		private void ApplyDrawingAnimation(float dt, ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				float drawFactor = m_currentDraw;

				m_componentModel.AimHandAngleOrder = 1.3f + drawFactor * 0.1f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.08f + (0.05f * drawFactor),
					-0.1f,
					0.07f - (0.03f * drawFactor)
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void LoadArrow(ComponentCreature target)
		{
			// Determinar el tipo de flecha según la distancia actual
			if (target != null)
			{
				float distance = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					target.ComponentBody.Position
				);

				// Elegir el tipo apropiado
				m_nextArrowType = SelectArrowTypeForDistance(distance);
			}
			else
			{
				// Si no hay objetivo, usar un tipo por defecto (el primero no explosivo)
				m_nextArrowType = GetFirstNonExplosiveArrowType();
			}

			m_isDrawing = false;
			m_isReloading = true;
			m_animationStartTime = m_subsystemTime.GameTime;

			// Cargar flecha en la ballesta (tensada completamente)
			UpdateCrossbowDraw(15);
			if (m_nextArrowType.HasValue)
			{
				UpdateCrossbowArrowType(m_nextArrowType.Value);
			}
		}

		// Selecciona el tipo de flecha basado en la distancia
		private RepeatArrowBlock.ArrowType? SelectArrowTypeForDistance(float distance)
		{
			// Distancia mínima para usar explosivos
			const float explosiveMinDistance = 20f;

			if (distance >= explosiveMinDistance)
			{
				// Intentar usar explosivo si está disponible
				foreach (var arrowType in m_availableArrowTypes)
				{
					if (arrowType == RepeatArrowBlock.ArrowType.ExplosiveArrow)
						return arrowType;
				}
				// Si no hay explosivo, usar cualquier otro
			}

			// Distancia corta: usar solo flechas no explosivas
			var nonExplosiveTypes = new List<RepeatArrowBlock.ArrowType>();
			foreach (var arrowType in m_availableArrowTypes)
			{
				if (arrowType != RepeatArrowBlock.ArrowType.ExplosiveArrow)
					nonExplosiveTypes.Add(arrowType);
			}

			if (nonExplosiveTypes.Count > 0)
			{
				// Elegir aleatoriamente entre las no explosivas
				int index = m_random.Int(0, nonExplosiveTypes.Count - 1);
				return nonExplosiveTypes[index];
			}

			// Si no hay ningún tipo no explosivo, devolver el primero disponible (aunque sea explosivo)
			if (m_availableArrowTypes.Length > 0)
				return m_availableArrowTypes[0];

			return null;
		}

		private RepeatArrowBlock.ArrowType? GetFirstNonExplosiveArrowType()
		{
			foreach (var arrowType in m_availableArrowTypes)
			{
				if (arrowType != RepeatArrowBlock.ArrowType.ExplosiveArrow)
					return arrowType;
			}
			return m_availableArrowTypes.Length > 0 ? m_availableArrowTypes[0] : (RepeatArrowBlock.ArrowType?)null;
		}

		private void ApplyReloadingAnimation(float dt, ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / 0.2f);

				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.03f,
					-0.1f - (0.05f * reloadProgress),
					0.04f
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);

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

			// Disparar flecha
			ShootArrow(target);

			// Sonido de disparo de ballesta repetidora
			m_subsystemAudio.PlaySound("Audio/Crossbow Remake/Crossbow Shoot", 1f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, FireSoundDistance, false);

			// Retroceso
			if (UseRecoil && target != null)
			{
				Vector3 direction = Vector3.Normalize(
					target.ComponentBody.Position -
					m_componentCreature.ComponentBody.Position
				);
				m_componentCreature.ComponentBody.ApplyImpulse(-direction * 1.0f);
			}

			// Limpiar el tipo seleccionado para el próximo disparo
			m_nextArrowType = null;
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

					m_componentModel.InHandItemOffsetOrder += new Vector3(recoil, 0f, 0f);
					m_componentModel.InHandItemRotationOrder += new Vector3(recoil * 2f, 0f, 0f);
				}
				else
				{
					// Volver a posición normal gradualmente
					float returnProgress = (fireProgress - 0.5f) / 0.5f;

					m_componentModel.AimHandAngleOrder = 1.4f * (1f - returnProgress);
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						-0.03f * (1f - returnProgress),
						-0.1f * (1f - returnProgress),
						0.04f * (1f - returnProgress)
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(
						-1.55f * (1f - returnProgress),
						0f,
						0f
					);
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
			m_nextArrowType = null;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ShootArrow(ComponentCreature target)
		{
			if (target == null)
				return;

			try
			{
				// Usar el tipo seleccionado previamente (en LoadArrow)
				RepeatArrowBlock.ArrowType? arrowType = m_nextArrowType;
				if (arrowType == null)
				{
					// Por si acaso, elegir uno ahora (no debería ocurrir)
					float currentDistance = Vector3.Distance(
						m_componentCreature.ComponentBody.Position,
						target.ComponentBody.Position
					);
					arrowType = SelectArrowTypeForDistance(currentDistance);
				}

				if (arrowType == null)
				{
					// No hay tipos disponibles
					return;
				}

				// Si por algún motivo el tipo es explosivo y la distancia es menor a 20, cambiamos a uno no explosivo
				float distanceToTarget = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					target.ComponentBody.Position
				);
				if (arrowType == RepeatArrowBlock.ArrowType.ExplosiveArrow && distanceToTarget < 20f)
				{
					var nonExplosive = GetFirstNonExplosiveArrowType();
					if (nonExplosive != null)
						arrowType = nonExplosive;
				}

				// Posición de disparo (desde los ojos del NPC)
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;

				// Posición objetivo (punto central del cuerpo)
				Vector3 targetPosition = target.ComponentBody.Position;

				// Ajustar para apuntar a un punto más central (pecho/abdomen)
				targetPosition.Y += target.ComponentBody.BoxSize.Y * 0.4f;

				// Calcular dirección PRECISA
				Vector3 direction = targetPosition - firePosition;
				float flightDistance = direction.Length();

				// Normalizar dirección
				if (flightDistance > 0.001f)
				{
					direction /= flightDistance;
				}
				else
				{
					direction = Vector3.UnitX;
				}

				// PRECISIÓN MEJORADA:
				// 1. Menor imprecisión base
				float baseInaccuracy = MaxInaccuracy * 0.3f;

				// 2. Factor de distancia (más preciso a distancia media)
				float distanceFactor = MathUtils.Clamp(flightDistance / MaxDistance, 0.1f, 1.0f);
				float inaccuracy = baseInaccuracy * distanceFactor;

				// 3. Aplicar menos imprecisión vertical que horizontal
				direction += new Vector3(
					m_random.Float(-inaccuracy, inaccuracy),
					m_random.Float(-inaccuracy * 0.3f, inaccuracy * 0.3f), // Vertical: 30% del horizontal
					m_random.Float(-inaccuracy, inaccuracy)
				);

				// 4. Re-normalizar para mantener velocidad constante
				direction = Vector3.Normalize(direction);

				// 5. Velocidad constante (sin variación por tensión para NPC)
				float speed = BoltSpeed * 1.1f; // 10% más rápido que el valor base

				// Crear flecha de RepeatArrowBlock
				int arrowData = RepeatArrowBlock.SetArrowType(0, arrowType.Value);
				int arrowValue = Terrain.MakeBlockValue(RepeatArrowBlock.Index, 0, arrowData);

				// Ajustar ligeramente la posición de inicio para mejor alineación
				Vector3 adjustedFirePosition = firePosition + direction * 0.3f;

				var projectile = m_subsystemProjectiles.FireProjectile(
					arrowValue,
					adjustedFirePosition,
					direction * speed,
					Vector3.Zero,
					m_componentCreature
				);

				// Configurar proyectil para desaparecer después del impacto
				if (projectile != null)
				{
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;

					// Configurar propiedades según el tipo de flecha
					if (arrowType == RepeatArrowBlock.ArrowType.ExplosiveArrow)
					{
						projectile.IsIncendiary = false;
					}
					// Las flechas de veneno ya tienen su comportamiento especial en SubsystemRepeatArrowBlockBehavior
				}

				// Ruido
				if (m_subsystemNoise != null)
				{
					m_subsystemNoise.MakeNoise(firePosition, 0.5f, 20f);
				}
			}
			catch (Exception ex)
			{
				// Silenciar error
			}
		}
	}
}
