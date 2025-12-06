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

		// Configuración
		public float MaxDistance = 25f;
		public float MinDistance = 5f;
		public float DrawTime = 2.5f;    // Tiempo para tensar completamente
		public float AimTime = 0.5f;
		public float TimeBetweenShots = 1.0f;  // Tiempo entre disparos de flechas múltiples
		public float MaxInaccuracy = 0.03f; // Mayor imprecisión para NPC
		public string DrawSound = "Audio/CrossbowDraw";
		public string FireSound = "Audio/Bow";
		public string ReleaseSound = "Audio/CrossbowBoing";
		public float FireSoundDistance = 15f;
		public bool UseRecoil = true;
		public float BoltSpeed = 40f;

		// Tipos de flechas que puede disparar (usa los 4 tipos de RepeatArrowBlock)
		private RepeatArrowBlock.ArrowType[] m_arrowTypes = new RepeatArrowBlock.ArrowType[]
		{
			RepeatArrowBlock.ArrowType.CopperArrow,    // 16 daño
            RepeatArrowBlock.ArrowType.IronArrow,      // 24 daño  
            RepeatArrowBlock.ArrowType.DiamondArrow,   // 36 daño
            RepeatArrowBlock.ArrowType.ExplosiveArrow  // 8 daño + explosión
        };

		// Estado
		private enum State
		{
			Idle,
			Aiming,
			Drawing,
			ReadyToFire,
			Firing
		}

		private State m_currentState = State.Idle;
		private double m_stateStartTime;
		private int m_crossbowSlot = -1;
		private int m_currentArrowType = 0; // Índice del tipo de flecha actual
		private int m_arrowsInCurrentVolley = 0; // Flechas en la ráfaga actual
		private int m_arrowsFired = 0; // Flechas disparadas en la ráfaga actual
		private float m_currentDraw = 0f;
		private bool m_isFullyDrawn = false;

		// Constantes
		private const int RepeatCrossbowIndex = 805; // RepeatCrossbowBlock.Index
		private const int RepeatArrowIndex = 804; // RepeatArrowBlock.Index
		private const int MaxArrowsPerVolley = 8; // Máximo según RepeatCrossbowBlock

		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros
			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 25f);
			MinDistance = valuesDictionary.GetValue<float>("MinDistance", 5f);
			DrawTime = valuesDictionary.GetValue<float>("DrawTime", 2.5f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 0.5f);
			TimeBetweenShots = valuesDictionary.GetValue<float>("TimeBetweenShots", 1.0f);
			MaxInaccuracy = valuesDictionary.GetValue<float>("MaxInaccuracy", 0.03f);
			DrawSound = valuesDictionary.GetValue<string>("DrawSound", "Audio/CrossbowDraw");
			FireSound = valuesDictionary.GetValue<string>("FireSound", "Audio/Bow");
			ReleaseSound = valuesDictionary.GetValue<string>("ReleaseSound", "Audio/CrossbowBoing");
			FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 15f);
			UseRecoil = valuesDictionary.GetValue<bool>("UseRecoil", true);
			BoltSpeed = valuesDictionary.GetValue<float>("BoltSpeed", 40f);

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

			// Elegir tipo de flecha aleatorio inicial
			m_currentArrowType = m_random.Int(0, m_arrowTypes.Length);

			// Buscar ballesta en el inventario
			FindCrossbow();

			// Inicializar con tensión mínima
			ResetCrossbowState();
		}

		public void Update(float dt)
		{
			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			// Verificar objetivo
			if (m_componentChaseBehavior.Target == null)
			{
				SetState(State.Idle);
				return;
			}

			// Calcular distancia
			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				m_componentChaseBehavior.Target.ComponentBody.Position
			);

			// Verificar distancia mínima
			if (distance < MinDistance)
			{
				SetState(State.Idle);
				return;
			}

			// Lógica de ataque
			if (distance <= MaxDistance && m_crossbowSlot >= 0)
			{
				ProcessAttackState();
			}
			else
			{
				SetState(State.Idle);
			}

			// Actualizar animaciones según estado
			UpdateAnimations(dt);
		}

		private void ProcessAttackState()
		{
			switch (m_currentState)
			{
				case State.Idle:
					// Comenzar a apuntar
					StartAiming();
					break;

				case State.Aiming:
					// Después de apuntar, comenzar a tensar
					if (m_subsystemTime.GameTime - m_stateStartTime >= AimTime)
					{
						StartDrawing();
					}
					break;

				case State.Drawing:
					UpdateDrawing();
					break;

				case State.ReadyToFire:
					// Disparar ráfaga de flechas
					if (m_arrowsInCurrentVolley == 0)
					{
						// Decidir cuántas flechas disparar (1-8)
						m_arrowsInCurrentVolley = m_random.Int(1, MaxArrowsPerVolley + 1);
						m_arrowsFired = 0;

						// Decidir tipo de flecha para esta ráfaga
						m_currentArrowType = m_random.Int(0, m_arrowTypes.Length);
					}

					// Disparar siguiente flecha
					if (m_subsystemTime.GameTime - m_stateStartTime >= TimeBetweenShots / m_arrowsInCurrentVolley)
					{
						FireArrow();
						m_arrowsFired++;

						if (m_arrowsFired >= m_arrowsInCurrentVolley)
						{
							// Terminó la ráfaga, volver a tensar
							m_arrowsInCurrentVolley = 0;
							m_isFullyDrawn = false;
							UpdateCrossbowDraw(0); // Destensar
							SetState(State.Aiming, 0.5f); // Breve pausa
						}
						else
						{
							// Preparar siguiente disparo en la ráfaga
							SetState(State.ReadyToFire, 0.1f);
						}
					}
					break;

				case State.Firing:
					// Terminar animación de disparo
					if (m_subsystemTime.GameTime - m_stateStartTime >= 0.2f)
					{
						SetState(State.ReadyToFire);
					}
					break;
			}
		}

		private void StartAiming()
		{
			SetState(State.Aiming);
			m_currentDraw = 0f;
			m_isFullyDrawn = false;
			UpdateCrossbowDraw(0);
		}

		private void StartDrawing()
		{
			SetState(State.Drawing);

			// Sonido de tensar
			if (!string.IsNullOrEmpty(DrawSound))
			{
				m_subsystemAudio.PlaySound(DrawSound, 0.5f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 3f, false);
			}
		}

		private void UpdateDrawing()
		{
			double elapsed = m_subsystemTime.GameTime - m_stateStartTime;
			m_currentDraw = MathUtils.Clamp((float)(elapsed / DrawTime), 0f, 1f);

			// Actualizar estado visual de la ballesta
			UpdateCrossbowDraw((int)(m_currentDraw * 15f));

			if (elapsed >= DrawTime)
			{
				// Tensado completo
				m_isFullyDrawn = true;
				UpdateCrossbowDraw(15);

				// Actualizar visualmente con tipo de flecha actual
				UpdateCrossbowArrowType(m_arrowTypes[m_currentArrowType]);

				// Listo para disparar
				SetState(State.ReadyToFire, 0.1f);
			}
		}

		private void FireArrow()
		{
			SetState(State.Firing);

			// Obtener tipo de flecha actual
			RepeatArrowBlock.ArrowType arrowType = m_arrowTypes[m_currentArrowType];

			// Posición de disparo (desde los ojos)
			Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
			firePosition.Y -= 0.1f;

			Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

			// Aplicar imprecisión (menor cuando está más cerca)
			float inaccuracyFactor = MathUtils.Lerp(0.5f, 1.0f,
				Vector3.Distance(firePosition, targetPosition) / MaxDistance);

			direction += new Vector3(
				m_random.Float(-MaxInaccuracy, MaxInaccuracy) * inaccuracyFactor,
				m_random.Float(-MaxInaccuracy * 0.5f, MaxInaccuracy * 0.5f) * inaccuracyFactor,
				m_random.Float(-MaxInaccuracy, MaxInaccuracy) * inaccuracyFactor
			);
			direction = Vector3.Normalize(direction);

			// Crear flecha
			int arrowData = RepeatArrowBlock.SetArrowType(0, arrowType);
			int arrowValue = Terrain.MakeBlockValue(RepeatArrowIndex, 0, arrowData);

			// Disparar
			var projectile = m_subsystemProjectiles.FireProjectile(
				arrowValue,
				firePosition,
				direction * BoltSpeed,
				Vector3.Zero,
				m_componentCreature
			);

			// Si es flecha explosiva, configurar para que desaparezca al impactar
			if (arrowType == RepeatArrowBlock.ArrowType.ExplosiveArrow && projectile != null)
			{
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			}

			// Sonido de disparo
			if (!string.IsNullOrEmpty(FireSound))
			{
				m_subsystemAudio.PlaySound(FireSound, 1f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, FireSoundDistance, false);
			}

			// Ruido
			if (m_subsystemNoise != null)
			{
				m_subsystemNoise.MakeNoise(firePosition, 0.5f, 20f);
			}

			// Retroceso
			if (UseRecoil)
			{
				m_componentCreature.ComponentBody.ApplyImpulse(-direction * 1.0f);
			}
		}

		private void FindCrossbow()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0 && Terrain.ExtractContents(slotValue) == RepeatCrossbowIndex)
				{
					m_crossbowSlot = i;
					m_componentInventory.ActiveSlotIndex = i;
					break;
				}
			}
		}

		private void ResetCrossbowState()
		{
			if (m_crossbowSlot < 0) return;

			int crossbowValue = m_componentInventory.GetSlotValue(m_crossbowSlot);
			if (crossbowValue == 0) return;

			int data = Terrain.ExtractData(crossbowValue);

			// Resetear a estado sin tensar y sin flechas
			data = RepeatCrossbowBlock.SetDraw(data, 0);
			data = RepeatCrossbowBlock.SetArrowType(data, null);
			crossbowValue = RepeatCrossbowBlock.SetLoadCount(crossbowValue, 0);

			// Actualizar en inventario
			UpdateInventorySlot(crossbowValue);
		}

		private void UpdateCrossbowDraw(int draw)
		{
			if (m_crossbowSlot < 0) return;

			int crossbowValue = m_componentInventory.GetSlotValue(m_crossbowSlot);
			if (crossbowValue == 0) return;

			int data = Terrain.ExtractData(crossbowValue);
			data = RepeatCrossbowBlock.SetDraw(data, MathUtils.Clamp(draw, 0, 15));

			crossbowValue = Terrain.ReplaceData(crossbowValue, data);
			UpdateInventorySlot(crossbowValue);
		}

		private void UpdateCrossbowArrowType(RepeatArrowBlock.ArrowType arrowType)
		{
			if (m_crossbowSlot < 0) return;

			int crossbowValue = m_componentInventory.GetSlotValue(m_crossbowSlot);
			if (crossbowValue == 0) return;

			int data = Terrain.ExtractData(crossbowValue);
			data = RepeatCrossbowBlock.SetArrowType(data, new RepeatArrowBlock.ArrowType?(arrowType));

			crossbowValue = Terrain.ReplaceData(crossbowValue, data);
			UpdateInventorySlot(crossbowValue);
		}

		private void UpdateCrossbowLoadCount(int count)
		{
			if (m_crossbowSlot < 0) return;

			int crossbowValue = m_componentInventory.GetSlotValue(m_crossbowSlot);
			if (crossbowValue == 0) return;

			crossbowValue = RepeatCrossbowBlock.SetLoadCount(crossbowValue, MathUtils.Clamp(count, 0, MaxArrowsPerVolley));
			UpdateInventorySlot(crossbowValue);
		}

		private void UpdateInventorySlot(int value)
		{
			m_componentInventory.RemoveSlotItems(m_crossbowSlot, 1);
			m_componentInventory.AddSlotItems(m_crossbowSlot, value, 1);
		}

		private void UpdateAnimations(float dt)
		{
			if (m_componentModel == null) return;

			switch (m_currentState)
			{
				case State.Idle:
					m_componentModel.AimHandAngleOrder = 0f;
					m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
					m_componentModel.InHandItemRotationOrder = Vector3.Zero;
					m_componentModel.LookAtOrder = null;
					break;

				case State.Aiming:
					// Posición de apuntado vertical
					m_componentModel.AimHandAngleOrder = 1.3f;
					m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);

					if (m_componentChaseBehavior.Target != null)
					{
						m_componentModel.LookAtOrder = new Vector3?(
							m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
						);
					}
					break;

				case State.Drawing:
					// Animación de tensado
					float drawProgress = m_currentDraw;
					m_componentModel.AimHandAngleOrder = 1.3f + drawProgress * 0.1f;
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						-0.08f + (0.05f * drawProgress),
						-0.1f,
						0.07f - (0.03f * drawProgress)
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);

					if (m_componentChaseBehavior.Target != null)
					{
						m_componentModel.LookAtOrder = new Vector3?(
							m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
						);
					}
					break;

				case State.ReadyToFire:
					// Mantener posición tensada
					m_componentModel.AimHandAngleOrder = 1.4f;
					m_componentModel.InHandItemOffsetOrder = new Vector3(-0.03f, -0.1f, 0.04f);
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);

					if (m_componentChaseBehavior.Target != null)
					{
						m_componentModel.LookAtOrder = new Vector3?(
							m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
						);
					}
					break;

				case State.Firing:
					// Animación de retroceso
					float fireProgress = (float)((m_subsystemTime.GameTime - m_stateStartTime) / 0.2f);
					float recoil = 0.08f * (1f - fireProgress);

					m_componentModel.AimHandAngleOrder = 1.4f;
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						-0.03f + recoil,
						-0.1f,
						0.04f
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(
						-1.55f + recoil * 1.5f,
						0f,
						0f
					);
					break;
			}
		}

		private void SetState(State newState, double delay = 0)
		{
			m_currentState = newState;
			m_stateStartTime = m_subsystemTime.GameTime + delay;
		}
	}
}
