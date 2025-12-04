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
		public float DrawTime = 1.2f;  // Más rápido
		public float AimTime = 0.5f;
		public string DrawSound = "Audio/BowDraw";
		public string FireSound = "Audio/Bow";
		public float FireSoundDistance = 15f;
		public float Accuracy = 0.03f;
		public float ArrowSpeed = 35f;
		public bool CycleArrowTypes = true;
		public bool ShowArrowWhenIdle = true; // Mostrar flecha cuando está inactivo

		// Tipos de flechas a usar
		public ArrowBlock.ArrowType[] AvailableArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.CopperArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.FireArrow
		};

		// Estado
		private bool m_isAiming = false;
		private bool m_isDrawing = false;
		private bool m_isFiring = false;
		private double m_animationStartTime;
		private double m_drawStartTime;
		private double m_fireTime;
		private int m_bowSlot = -1;
		private int m_currentArrowTypeIndex = 0;
		private float m_currentDraw = 0f;
		private Random m_random = new Random();
		private int m_arrowBlockIndex = 192;
		private int m_bowBlockIndex = 191;
		private bool m_initialized = false;

		// UpdateOrder
		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros
			MinDistance = valuesDictionary.GetValue<float>("MinDistance", 5f);
			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 25f);
			DrawTime = valuesDictionary.GetValue<float>("DrawTime", 1.2f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 0.5f);
			DrawSound = valuesDictionary.GetValue<string>("DrawSound", "Audio/BowDraw");
			FireSound = valuesDictionary.GetValue<string>("FireSound", "Audio/Bow");
			FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 15f);
			Accuracy = valuesDictionary.GetValue<float>("Accuracy", 0.03f);
			ArrowSpeed = valuesDictionary.GetValue<float>("ArrowSpeed", 35f);
			CycleArrowTypes = valuesDictionary.GetValue<bool>("CycleArrowTypes", true);
			ShowArrowWhenIdle = valuesDictionary.GetValue<bool>("ShowArrowWhenIdle", true);

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

		public override void OnEntityAdded()
		{
			base.OnEntityAdded();
			
			// Inicializar con flecha aleatoria
			m_currentArrowTypeIndex = m_random.Int(0, AvailableArrowTypes.Length);
			m_initialized = true;
			
			// Buscar arco
			FindBow();
			
			// Mostrar flecha inicialmente si está configurado
			if (ShowArrowWhenIdle && m_bowSlot >= 0)
			{
				SetBowWithArrow(0); // Arco con flecha pero sin tensión
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
				// Mantener flecha visible si está configurado
				if (ShowArrowWhenIdle && m_bowSlot >= 0)
				{
					SetBowWithArrow(0);
				}
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
				if (!m_isAiming && !m_isDrawing && !m_isFiring)
				{
					StartAiming();
				}
			}
			else
			{
				ResetAnimations();
				if (ShowArrowWhenIdle && m_bowSlot >= 0)
				{
					SetBowWithArrow(0);
				}
				return;
			}

			// Aplicar animaciones
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
				SetBowWithArrow((int)(m_currentDraw * 15f));

				if (m_subsystemTime.GameTime - m_drawStartTime >= DrawTime)
				{
					Fire();
				}
			}
			else if (m_isFiring)
			{
				ApplyFiringAnimation(dt);

				if (m_subsystemTime.GameTime - m_fireTime >= 0.2) // Animación más corta
				{
					m_isFiring = false;
					
					// Quitar flecha después de disparar
					ClearArrowFromBow();

					if (CycleArrowTypes && AvailableArrowTypes.Length > 1)
					{
						m_currentArrowTypeIndex = (m_currentArrowTypeIndex + 1) % AvailableArrowTypes.Length;
					}

					// Pausa antes de recargar
					if (m_subsystemTime.GameTime - m_fireTime >= 0.8)
					{
						StartAiming();
					}
				}
			}
		}

		private void FindBow()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0 && Terrain.ExtractContents(slotValue) == m_bowBlockIndex)
				{
					m_bowSlot = i;
					m_componentInventory.ActiveSlotIndex = i;
					break;
				}
			}
		}

		private void SetBowWithArrow(int drawValue)
		{
			if (m_bowSlot < 0) 
			{
				FindBow();
				if (m_bowSlot < 0) return;
			}

			try
			{
				int currentBowValue = m_componentInventory.GetSlotValue(m_bowSlot);
				if (currentBowValue == 0) return;

				int currentData = Terrain.ExtractData(currentBowValue);
				ArrowBlock.ArrowType arrowType = AvailableArrowTypes[m_currentArrowTypeIndex];

				// Configurar arco con flecha y tensión
				int newData = BowBlock.SetArrowType(currentData, new ArrowBlock.ArrowType?(arrowType));
				newData = BowBlock.SetDraw(newData, MathUtils.Clamp(drawValue, 0, 15));

				// Actualizar el arco en el inventario
				int newBowValue = Terrain.ReplaceData(currentBowValue, newData);
				m_componentInventory.RemoveSlotItems(m_bowSlot, 1);
				m_componentInventory.AddSlotItems(m_bowSlot, newBowValue, 1);
			}
			catch
			{
				// Ignorar errores
			}
		}

		private void ClearArrowFromBow()
		{
			if (m_bowSlot < 0) return;

			try
			{
				int currentBowValue = m_componentInventory.GetSlotValue(m_bowSlot);
				if (currentBowValue == 0) return;

				int currentData = Terrain.ExtractData(currentBowValue);
				
				// Quitar flecha y resetear tensión
				int newData = BowBlock.SetArrowType(currentData, null);
				newData = BowBlock.SetDraw(newData, 0);

				int newBowValue = Terrain.ReplaceData(currentBowValue, newData);
				m_componentInventory.RemoveSlotItems(m_bowSlot, 1);
				m_componentInventory.AddSlotItems(m_bowSlot, newBowValue, 1);
			}
			catch
			{
				// Ignorar errores
			}
		}

		private void StartAiming()
		{
			m_isAiming = true;
			m_isDrawing = false;
			m_isFiring = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;

			// Mostrar flecha en el arco (sin tensión)
			SetBowWithArrow(0);
		}

		private void ApplyAimingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN SIMPLE Y ESTABLE - ARCO EN POSICIÓN BAJA
				// Valores muy pequeños para que no se salga
				m_componentModel.AimHandAngleOrder = 0.2f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(0.05f, 0.05f, 0.05f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-0.05f, 0.3f, 0.05f);

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

			if (!string.IsNullOrEmpty(DrawSound))
			{
				m_subsystemAudio.PlaySound(DrawSound, 0.5f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 3f, false);
			}
		}

		private void ApplyDrawingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				float drawFactor = m_currentDraw;

				// ANIMACIÓN MUY SUAVE - MOVIMIENTO MÍNIMO
				// Solo pequeños ajustes para mostrar el tensado
				float horizontalOffset = 0.05f - (0.03f * drawFactor);
				float verticalOffset = 0.05f + (0.02f * drawFactor);
				float depthOffset = 0.05f - (0.02f * drawFactor);

				// Rotaciones muy pequeñas
				float pitchRotation = -0.05f - (0.1f * drawFactor);
				float yawRotation = 0.3f - (0.05f * drawFactor);
				float rollRotation = 0.05f - (0.02f * drawFactor);

				// Cambios mínimos en el brazo
				m_componentModel.AimHandAngleOrder = 0.2f + (0.3f * drawFactor);
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					horizontalOffset,
					verticalOffset,
					depthOffset
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					pitchRotation,
					yawRotation,
					rollRotation
				);

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
			m_isDrawing = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			// Disparar flecha
			ShootArrow();

			if (!string.IsNullOrEmpty(FireSound))
			{
				m_subsystemAudio.PlaySound(FireSound, 0.8f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, FireSoundDistance, false);
			}
		}

		private void ApplyFiringAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				float fireProgress = (float)((m_subsystemTime.GameTime - m_fireTime) / 0.2);
				
				if (fireProgress < 0.5f)
				{
					// Pequeño retroceso
					float recoil = 0.02f * (1f - (fireProgress * 2f));
					
					m_componentModel.InHandItemOffsetOrder += new Vector3(recoil, 0f, 0f);
					m_componentModel.InHandItemRotationOrder += new Vector3(recoil * 2f, 0f, 0f);
				}
				else
				{
					// Volver a posición normal gradualmente
					float returnProgress = (fireProgress - 0.5f) / 0.5f;
					
					m_componentModel.AimHandAngleOrder = 0.2f * (1f - returnProgress);
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						0.05f * (1f - returnProgress),
						0.05f * (1f - returnProgress),
						0.05f * (1f - returnProgress)
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(
						-0.05f * (1f - returnProgress),
						0.3f * (1f - returnProgress),
						0.05f * (1f - returnProgress)
					);
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
				ArrowBlock.ArrowType arrowType = AvailableArrowTypes[m_currentArrowTypeIndex];
				
				// Posición de disparo simple
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				firePosition.Y -= 0.1f;
				
				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				// Aplicar precisión
				float currentAccuracy = Accuracy * (1.5f - m_currentDraw);
				
				direction += new Vector3(
					m_random.Float(-currentAccuracy, currentAccuracy),
					m_random.Float(-currentAccuracy * 0.5f, currentAccuracy * 0.5f),
					m_random.Float(-currentAccuracy, currentAccuracy)
				);
				direction = Vector3.Normalize(direction);

				// Velocidad según tensión
				float speedMultiplier = 0.5f + (m_currentDraw * 1.5f);
				float currentSpeed = ArrowSpeed * speedMultiplier;

				int arrowData = ArrowBlock.SetArrowType(0, arrowType);
				int arrowValue = Terrain.MakeBlockValue(m_arrowBlockIndex, 0, arrowData);

				var projectile = m_subsystemProjectiles.FireProjectile(
					arrowValue,
					firePosition,
					direction * currentSpeed,
					Vector3.Zero,
					m_componentCreature
				);

				if (arrowType == ArrowBlock.ArrowType.FireArrow && projectile != null)
				{
					m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero,
						new SmokeTrailParticleSystem(15, 0.5f, float.MaxValue, Color.White));
					projectile.IsIncendiary = true;
				}
			}
			catch
			{
				// Ignorar errores
			}
		}
	}
}
