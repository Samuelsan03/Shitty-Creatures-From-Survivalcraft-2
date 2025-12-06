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
		public float DrawTime = 2.5f;    // Tiempo para tensar completamente
		public float AimTime = 0.5f;
		public float ReloadTime = 0.3f;  // Tiempo entre disparos con múltiples flechas
		public float MaxInaccuracy = 0.03f; // Mayor imprecisión para NPC
		public string DrawSound = "Audio/CrossbowDraw";
		public string FireSound = "Audio/Bow";
		public string ReleaseSound = "Audio/CrossbowBoing";
		public float FireSoundDistance = 15f;
		public bool CycleArrowTypes = true;
		public bool LoadArrowsFromInventory = true; // Si debe buscar flechas en inventario

		// Tipos de flechas disponibles (los 4 tipos de RepeatArrowBlock)
		public RepeatArrowBlock.ArrowType[] AvailableArrowTypes = new RepeatArrowBlock.ArrowType[]
		{
			RepeatArrowBlock.ArrowType.CopperArrow,
			RepeatArrowBlock.ArrowType.IronArrow,
			RepeatArrowBlock.ArrowType.DiamondArrow,
			RepeatArrowBlock.ArrowType.ExplosiveArrow
		};

		// Estado
		private enum State
		{
			Idle,
			Aiming,
			Drawing,
			ReadyToFire,
			Firing,
			Reloading
		}

		private State m_currentState = State.Idle;
		private double m_stateStartTime;
		private int m_crossbowSlot = -1;
		private int m_currentArrowTypeIndex = 0;
		private int m_remainingShots = 0;
		private float m_currentDraw = 0f;
		private bool m_isFullyDrawn = false;
		private bool m_hasArrowsLoaded = false;

		// Constantes
		private const int MaxArrowLoad = 8; // Máximo de flechas según RepeatCrossbowBlock
		private const int RepeatCrossbowIndex = 805; // RepeatCrossbowBlock.Index
		private const int RepeatArrowIndex = 804; // RepeatArrowBlock.Index

		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros
			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 25f);
			DrawTime = valuesDictionary.GetValue<float>("DrawTime", 2.5f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 0.5f);
			ReloadTime = valuesDictionary.GetValue<float>("ReloadTime", 0.3f);
			MaxInaccuracy = valuesDictionary.GetValue<float>("MaxInaccuracy", 0.03f);
			DrawSound = valuesDictionary.GetValue<string>("DrawSound", "Audio/CrossbowDraw");
			FireSound = valuesDictionary.GetValue<string>("FireSound", "Audio/Bow");
			ReleaseSound = valuesDictionary.GetValue<string>("ReleaseSound", "Audio/CrossbowBoing");
			FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 15f);
			CycleArrowTypes = valuesDictionary.GetValue<bool>("CycleArrowTypes", true);
			LoadArrowsFromInventory = valuesDictionary.GetValue<bool>("LoadArrowsFromInventory", true);

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

			// Inicializar con tipo de flecha aleatorio
			m_currentArrowTypeIndex = m_random.Int(0, AvailableArrowTypes.Length);
			FindCrossbow();
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
					StartAiming();
					break;

				case State.Aiming:
					if (m_subsystemTime.GameTime - m_stateStartTime >= AimTime)
					{
						// Verificar si necesita tensar
						int currentCrossbowValue = m_componentInventory.GetSlotValue(m_crossbowSlot);
						int data = Terrain.ExtractData(currentCrossbowValue);
						int draw = RepeatCrossbowBlock.GetDraw(data);

						if (draw < 15)
						{
							StartDrawing();
						}
						else if (!m_hasArrowsLoaded)
						{
							TryLoadArrows();
						}
						else
						{
							SetState(State.ReadyToFire);
						}
					}
					break;

				case State.Drawing:
					UpdateDrawing();
					break;

				case State.ReadyToFire:
					if (m_remainingShots > 0 && m_isFullyDrawn)
					{
						FireArrow();
					}
					else
					{
						// Recargar o tensar de nuevo
						if (m_remainingShots <= 0)
						{
							TryLoadArrows();
						}
						else if (!m_isFullyDrawn)
						{
							StartDrawing();
						}
					}
					break;

				case State.Firing:
					if (m_subsystemTime.GameTime - m_stateStartTime >= 0.2f)
					{
						m_remainingShots--;

						if (m_remainingShots > 0)
						{
							// Disparo rápido en secuencia
							SetState(State.ReadyToFire, 0.1f);
						}
						else
						{
							// Se acabaron las flechas
							SetState(State.Idle, 0.5f);
						}
					}
					break;

				case State.Reloading:
					if (m_subsystemTime.GameTime - m_stateStartTime >= ReloadTime)
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
				m_isFullyDrawn = true;
				UpdateCrossbowDraw(15); // Tensión completa

				// Si no tiene flechas cargadas, intentar cargar
				if (!m_hasArrowsLoaded)
				{
					TryLoadArrows();
				}
				else
				{
					SetState(State.ReadyToFire);
				}
			}
		}

		private void TryLoadArrows()
		{
			if (!LoadArrowsFromInventory)
			{
				// Usar flechas infinitas para NPC
				m_remainingShots = MaxArrowLoad;
				m_hasArrowsLoaded = true;
				SetState(State.ReadyToFire);
				return;
			}

			// Buscar flechas en el inventario
			bool loaded = LoadArrowsFromInventorySlots();

			if (loaded)
			{
				m_hasArrowsLoaded = true;
				SetState(State.ReadyToFire);
			}
			else
			{
				// No hay flechas, volver a idle
				SetState(State.Idle);
			}
		}

		private bool LoadArrowsFromInventorySlots()
		{
			int arrowsToLoad = MaxArrowLoad;
			RepeatArrowBlock.ArrowType arrowTypeToLoad = AvailableArrowTypes[m_currentArrowTypeIndex];

			// Buscar flechas del tipo actual
			for (int i = 0; i < m_componentInventory.SlotsCount && arrowsToLoad > 0; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0 && Terrain.ExtractContents(slotValue) == RepeatArrowIndex)
				{
					RepeatArrowBlock.ArrowType slotArrowType = RepeatArrowBlock.GetArrowType(Terrain.ExtractData(slotValue));

					if (slotArrowType == arrowTypeToLoad)
					{
						int slotCount = m_componentInventory.GetSlotCount(i);
						int arrowsTaken = Math.Min(slotCount, arrowsToLoad);

						// Remover flechas del inventario
						m_componentInventory.RemoveSlotItems(i, arrowsTaken);
						arrowsToLoad -= arrowsTaken;
					}
				}
			}

			if (arrowsToLoad < MaxArrowLoad)
			{
				// Cargar flechas en la ballesta
				m_remainingShots = MaxArrowLoad - arrowsToLoad;
				UpdateCrossbowArrowType(arrowTypeToLoad);
				UpdateCrossbowLoadCount(m_remainingShots);

				// Ciclar al siguiente tipo para próxima recarga
				if (CycleArrowTypes)
				{
					m_currentArrowTypeIndex = (m_currentArrowTypeIndex + 1) % AvailableArrowTypes.Length;
				}

				return true;
			}

			return false;
		}

		private void FireArrow()
		{
			SetState(State.Firing);

			// Obtener tipo de flecha actual
			RepeatArrowBlock.ArrowType currentArrowType = AvailableArrowTypes[m_currentArrowTypeIndex];

			// Posición de disparo
			Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
			firePosition.Y -= 0.1f;

			Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

			// Aplicar imprecisión
			direction += new Vector3(
				m_random.Float(-MaxInaccuracy, MaxInaccuracy),
				m_random.Float(-MaxInaccuracy * 0.5f, MaxInaccuracy * 0.5f),
				m_random.Float(-MaxInaccuracy, MaxInaccuracy)
			);
			direction = Vector3.Normalize(direction);

			// Crear flecha
			int arrowData = RepeatArrowBlock.SetArrowType(0, currentArrowType);
			int arrowValue = Terrain.MakeBlockValue(RepeatArrowIndex, 0, arrowData);

			// Disparar
			var projectile = m_subsystemProjectiles.FireProjectile(
				arrowValue,
				firePosition,
				direction * 40f, // Velocidad similar a la ballesta del jugador
				Vector3.Zero,
				m_componentCreature
			);

			// Actualizar contador de flechas en la ballesta
			UpdateCrossbowLoadCount(m_remainingShots - 1);

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
			m_componentCreature.ComponentBody.ApplyImpulse(-direction * 0.5f);
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

			crossbowValue = RepeatCrossbowBlock.SetLoadCount(crossbowValue, MathUtils.Clamp(count, 0, MaxArrowLoad));
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
					break;

				case State.Aiming:
				case State.Drawing:
				case State.ReadyToFire:
					// Animación similar a la del jugador con la ballesta repetidora
					m_componentModel.AimHandAngleOrder = 1.3f;
					m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);

					// Mirar al objetivo
					if (m_componentChaseBehavior.Target != null)
					{
						m_componentModel.LookAtOrder = new Vector3?(
							m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
						);
					}
					break;

				case State.Firing:
					// Pequeño retroceso
					float fireProgress = (float)((m_subsystemTime.GameTime - m_stateStartTime) / 0.2f);
					float recoil = 0.05f * (1f - fireProgress);

					m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f + recoil, -0.1f, 0.07f);
					m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f + recoil * 2f, 0f, 0f);
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
