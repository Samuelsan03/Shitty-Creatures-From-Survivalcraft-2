using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Comportamiento de granjero: ara, fertiliza (si tiene salitre), siembra, cosecha, recolecta items/experiencia y vuelve a arar si la tierra se convierte en césped/tierra.
	/// Ciclo con salitre: 1. Rastrilleo → 2. Fertilizante (salitre) → 3. Semilla → repetir
	/// Ciclo sin salitre: 1. Rastrilleo → 2. Semilla → repetir
	/// </summary>
	public class ComponentFarmerBehavior : ComponentBehavior, IUpdateable
	{
		// -----------------------------------------------------------------
		//  Prioridades de tarea (el orden del enum define la prioridad;
		//  los valores más bajos se ejecutan primero).
		// -----------------------------------------------------------------
		private enum TaskPriority
		{
			HarvestMature,   // recoger cultivo maduro
			ClearBadCrop,    // romper cultivo muerto, silvestre o que no crecerá
			RakeTrampled,    // arar tierra pisoteada con planta encima
			Plant,           // sembrar en suelo arado vacío
			Rake             // arar césped/tierra limpia
		}

		// ID del área de cultivo a la que está asignado este granjero (-1 = libre)
		public int FarmAreaId = -1;

		private const float BASE_IMPORTANCE_MIN = 5f;
		private const float BASE_IMPORTANCE_MAX = 10f;
		private const float TASK_IMPORTANCE_MIN = 10f;
		private const float TASK_IMPORTANCE_MAX = 15f;

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemPickables m_subsystemPickables;
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentPathfinding m_componentPathfinding;
		private IInventory m_inventory;

		private readonly Random m_random = new Random();
		private readonly StateMachine m_stateMachine = new StateMachine();

		private float m_importanceLevel;
		private double m_nextScanTime;
		private CellFace? m_targetCellFace;
		private Vector3 m_targetPosition;
		private int m_blockedCount;
		private bool m_farmerEnabled;
		private double m_stateEnterTime;

		// Contador y límite para intentos de rastrillado
		private int m_rakeAttempts;
		private const int MAX_RAKE_ATTEMPTS = 5;

		private const float SCAN_RADIUS = 15f;
		private const double STATE_DELAY = 0.5;
		private const float PICKUP_RADIUS = 2.5f;
		private const double STATE_TIMEOUT = 10.0;

		// Tiempos de acción separados
		private const double TIME_TO_RAKE = 0.5;
		private const double TIME_TO_FERTILIZE = 0.5;
		private const double TIME_TO_PLANT_SEED = 0.5;

		// Radio máximo permitido antes de volver al área (2.5 veces el radio de escaneo)
		private const float MAX_DISTANCE_FROM_AREA = SCAN_RADIUS * 2.5f;

		// Centro del área de cultivos
		private Vector3 m_farmAreaCenter;
		private bool m_hasFarmAreaCenter;

		// Límites del área de cultivo (esquina mínima y máxima del rectángulo marcado)
		private Point3 m_farmAreaMin;
		private Point3 m_farmAreaMax;
		private bool m_hasFarmAreaBounds;

		private bool m_stateMachineBuilt;
		private bool m_initialized;

		// Para detectar cuando se cambió el slot activo externamente (ej. por combate)
		private int m_lastKnownActiveSlotIndex = -1;
		private double m_lastToolCheckTime;
		private const double TOOL_CHECK_INTERVAL = 0.5;

		public override float ImportanceLevel => m_importanceLevel;

		public bool FarmerEnabled
		{
			get => m_farmerEnabled;
			set
			{
				if (m_farmerEnabled != value)
				{
					m_farmerEnabled = value;

					if (!value)
					{
						if (m_stateMachineBuilt && m_initialized)
						{
							m_stateMachine.TransitionTo("Inactive");
						}
						if (m_componentPathfinding != null && m_initialized)
						{
							m_componentPathfinding.Stop();
							m_componentPathfinding.IsStuck = false;
						}
						m_importanceLevel = 0f;
					}
					else
					{
						m_nextScanTime = 0;
						if (!m_hasFarmAreaCenter)
						{
							m_farmAreaCenter = m_componentCreature.ComponentBody.Position;
							m_hasFarmAreaCenter = true;
						}
					}
				}
			}
		}

		public override bool IsActive
		{
			get => m_farmerEnabled;
			set
			{
				if (value)
				{
					m_farmerEnabled = true;
					m_nextScanTime = 0;
					if (!m_hasFarmAreaCenter)
					{
						m_farmAreaCenter = m_componentCreature.ComponentBody.Position;
						m_hasFarmAreaCenter = true;
					}
				}
			}
		}

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_inventory = m_componentMiner.Inventory;

			m_farmerEnabled = valuesDictionary.GetValue<bool>("FarmerEnabled", false);
			m_importanceLevel = 0f;

			m_hasFarmAreaCenter = valuesDictionary.GetValue<bool>("HasFarmAreaCenter", false);
			if (m_hasFarmAreaCenter)
			{
				m_farmAreaCenter = valuesDictionary.GetValue<Vector3>("FarmAreaCenter");
			}

			m_hasFarmAreaBounds = valuesDictionary.GetValue<bool>("HasFarmAreaBounds", false);
			if (m_hasFarmAreaBounds)
			{
				m_farmAreaMin = valuesDictionary.GetValue<Point3>("FarmAreaMin");
				m_farmAreaMax = valuesDictionary.GetValue<Point3>("FarmAreaMax");
			}

			FarmAreaId = valuesDictionary.GetValue<int>("FarmAreaId", -1);

			var wandSubsystem = Project.FindSubsystem<SubsystemFarmerWandBlockBehavior>(false);
			if (wandSubsystem != null && FarmAreaId >= 0)
			{
				var area = wandSubsystem.FindAreaById(FarmAreaId);
				if (area != null && area.HasBothPoints)
				{
					ApplyFarmAreaBounds(area.PointA.Value, area.PointB.Value);
				}
				else
				{
					FarmAreaId = -1; // el área ya no existe
				}
			}

			BuildStateMachine();
			m_stateMachineBuilt = true;

			m_initialized = false;
		}

		/// <summary>Guarda centro y límites del área sin tocar el state machine.</summary>
		private void ApplyFarmAreaBounds(Point3 a, Point3 b)
		{
			int minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X);
			int minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y);
			int minZ = Math.Min(a.Z, b.Z), maxZ = Math.Max(a.Z, b.Z);

			m_farmAreaMin = new Point3(minX, minY, minZ);
			m_farmAreaMax = new Point3(maxX, maxY, maxZ);
			m_hasFarmAreaBounds = true;

			m_farmAreaCenter = new Vector3(
				(minX + maxX + 1) * 0.5f,
				(minY + maxY + 1) * 0.5f,
				(minZ + maxZ + 1) * 0.5f);
			m_hasFarmAreaCenter = true;
		}

		/// <summary>
		/// Asigna al granjero un área de cultivo definida por dos esquinas.
		/// Restringe su escaneo a esos límites y lo reubica si está fuera.
		/// </summary>
		public void SetFarmArea(Point3 a, Point3 b)
		{
			ApplyFarmAreaBounds(a, b);

			m_nextScanTime = 0;

			if (m_importanceLevel < BASE_IMPORTANCE_MIN)
				m_importanceLevel = BASE_IMPORTANCE_MIN;

			if (m_stateMachineBuilt && m_initialized)
			{
				m_stateMachine.TransitionTo("Inactive");
			}
		}

		private bool HasFarmingTools()
		{
			return HasTool(typeof(RakeBlock)) || HasTool(typeof(SeedsBlock));
		}

		private bool IsFarmingTool(int contents)
		{
			if (contents <= 0 || contents >= BlocksManager.Blocks.Length) return false;
			Block block = BlocksManager.Blocks[contents];
			if (block == null) return false;
			return block is RakeBlock || block is SeedsBlock || block is SaltpeterChunkBlock;
		}

		private bool EnsureFarmingToolEquipped()
		{
			if (m_inventory == null) return false;

			int activeSlot = m_inventory.ActiveSlotIndex;
			int activeValue = m_inventory.GetSlotValue(activeSlot);
			int activeContents = Terrain.ExtractContents(activeValue);

			if (IsFarmingTool(activeContents))
			{
				m_lastKnownActiveSlotIndex = activeSlot;
				return true;
			}

			int rakeSlot = FindSlotWithTool(typeof(RakeBlock));
			if (rakeSlot >= 0)
			{
				m_inventory.ActiveSlotIndex = rakeSlot;
				m_lastKnownActiveSlotIndex = rakeSlot;
				return true;
			}

			int seedSlot = FindSlotWithSeed();
			if (seedSlot >= 0)
			{
				m_inventory.ActiveSlotIndex = seedSlot;
				m_lastKnownActiveSlotIndex = seedSlot;
				return true;
			}

			int fertilizerSlot = FindSlotWithFertilizer();
			if (fertilizerSlot >= 0)
			{
				m_inventory.ActiveSlotIndex = fertilizerSlot;
				m_lastKnownActiveSlotIndex = fertilizerSlot;
				return true;
			}

			return false;
		}

		private bool CheckAndRestoreFarmingTool()
		{
			if (m_inventory == null) return false;

			int currentSlot = m_inventory.ActiveSlotIndex;
			int currentValue = m_inventory.GetSlotValue(currentSlot);
			int currentContents = Terrain.ExtractContents(currentValue);

			if (IsFarmingTool(currentContents))
			{
				m_lastKnownActiveSlotIndex = currentSlot;
				return true;
			}

			if (currentSlot != m_lastKnownActiveSlotIndex)
			{
				return EnsureFarmingToolEquipped();
			}

			return false;
		}

		private int FindSlotWithTool(Type toolType)
		{
			if (m_inventory == null) return -1;
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				if (value == 0) continue;
				int contents = Terrain.ExtractContents(value);
				if (contents <= 0 || contents >= BlocksManager.Blocks.Length) continue;
				Block block = BlocksManager.Blocks[contents];
				if (block != null && toolType.IsAssignableFrom(block.GetType()))
					return i;
			}
			return -1;
		}

		private int FindSlotWithSeed()
		{
			if (m_inventory == null) return -1;
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				if (value == 0) continue;
				int contents = Terrain.ExtractContents(value);
				if (contents <= 0 || contents >= BlocksManager.Blocks.Length) continue;
				Block block = BlocksManager.Blocks[contents];
				if (block != null && block is SeedsBlock)
					return i;
			}
			return -1;
		}

		private int FindSlotWithFertilizer()
		{
			if (m_inventory == null) return -1;
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				if (value == 0) continue;
				int contents = Terrain.ExtractContents(value);
				if (contents <= 0 || contents >= BlocksManager.Blocks.Length) continue;
				if (BlocksManager.Blocks[contents] is SaltpeterChunkBlock)
					return i;
			}
			return -1;
		}

		private bool IsTooFarFromArea()
		{
			if (!m_hasFarmAreaBounds)
			{
				// Fallback al comportamiento antiguo (por centro/radio)
				if (!m_hasFarmAreaCenter) return false;
				Vector3 pos0 = m_componentCreature.ComponentBody.Position;
				float dx0 = pos0.X - m_farmAreaCenter.X;
				float dz0 = pos0.Z - m_farmAreaCenter.Z;
				return MathF.Sqrt(dx0 * dx0 + dz0 * dz0) > MAX_DISTANCE_FROM_AREA;
			}

			// Distancia horizontal del farmer al punto más cercano del rectángulo.
			Vector3 pos = m_componentCreature.ComponentBody.Position;
			int px = (int)MathF.Floor(pos.X);
			int pz = (int)MathF.Floor(pos.Z);

			int nearestX = Math.Clamp(px, m_farmAreaMin.X, m_farmAreaMax.X);
			int nearestZ = Math.Clamp(pz, m_farmAreaMin.Z, m_farmAreaMax.Z);

			int dx = px - nearestX;
			int dz = pz - nearestZ;
			float distSq = dx * dx + dz * dz;

			return distSq > SCAN_RADIUS * SCAN_RADIUS; // 15 bloques
		}

		private float GetDistanceToAreaCenter()
		{
			if (!m_hasFarmAreaCenter) return 0f;

			Vector3 pos = m_componentCreature.ComponentBody.Position;
			float dx = pos.X - m_farmAreaCenter.X;
			float dz = pos.Z - m_farmAreaCenter.Z;
			return MathF.Sqrt(dx * dx + dz * dz);
		}

		private Vector3 GetRandomPointInArea()
		{
			float angle = m_random.Float(0, MathF.PI * 2);
			float distance = m_random.Float(0, SCAN_RADIUS * 0.8f);
			return new Vector3(
				m_farmAreaCenter.X + MathF.Cos(angle) * distance,
				m_farmAreaCenter.Y,
				m_farmAreaCenter.Z + MathF.Sin(angle) * distance
			);
		}

		private void UpdateFarmAreaCenter()
		{
			m_farmAreaCenter = m_componentCreature.ComponentBody.Position;
			m_hasFarmAreaCenter = true;
		}

		private void BuildStateMachine()
		{
			m_stateMachine.AddState("Inactive",
				enter: () =>
				{
					m_nextScanTime = 0;

					if (m_componentPathfinding != null && m_initialized)
					{
						m_componentPathfinding.Stop();
						m_componentPathfinding.IsStuck = false;
					}

					if (m_farmerEnabled && HasFarmingTools())
						EnsureFarmingToolEquipped();

					// Importancia baja en reposo: deja que WalkAround (u otras behaviors) tomen el control.
					m_importanceLevel = 0f;
					m_stateEnterTime = m_subsystemTime.GameTime;
				},
				update: () =>
				{
					if (!m_farmerEnabled)
					{
						m_importanceLevel = 0f;
						return;
					}

					if (!HasFarmingTools())
					{
						m_importanceLevel = 0f;
						return;
					}

					if (m_subsystemTime.GameTime - m_lastToolCheckTime > TOOL_CHECK_INTERVAL)
					{
						m_lastToolCheckTime = m_subsystemTime.GameTime;
						CheckAndRestoreFarmingTool();
					}

					// Si se aleja demasiado del área, siempre vuelve (aunque esté descansando).
					if (IsTooFarFromArea())
					{
						m_importanceLevel = 10f;
						m_stateMachine.TransitionTo("ReturnToArea");
						return;
					}

					// Escaneo periódico: si aparece una tarea, la tomamos.
					if (m_subsystemTime.GameTime > m_nextScanTime)
					{
						m_nextScanTime = m_subsystemTime.GameTime + m_random.Float(0.4f, 0.8f);
						if (FindBestTask(out CellFace target, out TaskPriority priority))
						{
							m_targetCellFace = target;
							m_targetPosition = new Vector3(target.X + 0.5f, target.Y + 0.5f, target.Z + 0.5f);
							m_importanceLevel = m_random.Float(TASK_IMPORTANCE_MIN, TASK_IMPORTANCE_MAX);

							if (!m_hasFarmAreaCenter)
								UpdateFarmAreaCenter();

							m_stateMachine.TransitionTo("MoveToTarget");
							return;
						}
					}

					// Sin tareas → importancia 0 para que WalkAround pueda activarse.
					m_importanceLevel = 0f;
				},
				leave: null
			);

			m_stateMachine.AddState("ReturnToArea",
				enter: () =>
				{
					m_stateEnterTime = m_subsystemTime.GameTime;
					m_blockedCount = 0;
					m_importanceLevel = 10f;

					EnsureFarmingToolEquipped();

					if (m_componentPathfinding != null && m_initialized)
					{
						m_componentPathfinding.IsStuck = false;

						Vector3 returnTarget = m_farmAreaCenter;
						m_componentPathfinding.SetDestination(
							returnTarget,
							speed: 1.0f,
							range: 2.0f,
							maxPathfindingPositions: 1000,
							useRandomMovements: true,
							ignoreHeightDifference: false,
							raycastDestination: false,
							doNotAvoidBody: null
						);
					}
				},
				update: () =>
				{
					if (!m_farmerEnabled)
					{
						if (m_componentPathfinding != null && m_initialized)
							m_componentPathfinding.Stop();
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					if (m_subsystemTime.GameTime - m_stateEnterTime > STATE_TIMEOUT * 3)
					{
						UpdateFarmAreaCenter();
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					if (!IsTooFarFromArea())
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					if (m_componentPathfinding != null && m_initialized && m_componentPathfinding.IsStuck)
					{
						m_blockedCount++;
						if (m_blockedCount > 5)
						{
							UpdateFarmAreaCenter();
							m_stateMachine.TransitionTo("Inactive");
							return;
						}

						m_componentPathfinding.IsStuck = false;
						Vector3 newTarget = GetRandomPointInArea();
						m_componentPathfinding.SetDestination(
							newTarget,
							1.0f,
							2.0f,
							1000,
							true,
							false,
							false,
							null
						);
					}
				},
				leave: null
			);

			m_stateMachine.AddState("MoveToTarget",
				enter: () =>
				{
					m_stateEnterTime = m_subsystemTime.GameTime;
					m_blockedCount = 0;
					m_importanceLevel = 10f;
					if (m_componentPathfinding != null && m_initialized)
					{
						m_componentPathfinding.IsStuck = false;
						m_componentPathfinding.SetDestination(
							m_targetPosition,
							speed: 0.8f,
							range: 0.8f,
							maxPathfindingPositions: 500,
							useRandomMovements: false,
							ignoreHeightDifference: false,
							raycastDestination: false,
							doNotAvoidBody: null
						);
					}
				},
				update: () =>
				{
					if (!m_farmerEnabled)
					{
						if (m_componentPathfinding != null && m_initialized)
							m_componentPathfinding.Stop();
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					if (IsTooFarFromArea())
					{
						if (m_componentPathfinding != null && m_initialized)
							m_componentPathfinding.Stop();
						m_stateMachine.TransitionTo("ReturnToArea");
						return;
					}

					if (m_subsystemTime.GameTime - m_stateEnterTime > STATE_TIMEOUT)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					if (m_targetCellFace == null)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position, m_targetPosition);
					if (dist < 1.2f)
					{
						int x = m_targetCellFace.Value.X;
						int y = m_targetCellFace.Value.Y;
						int z = m_targetCellFace.Value.Z;
						int value = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
						int contents = Terrain.ExtractContents(value);

						if (IsHarvestable(contents, value))
						{
							m_stateMachine.TransitionTo("Harvest");
						}
						else if (IsBadCrop(contents, value, x, y, z))
						{
							// Cultivo muerto, silvestre o que no crecerá: romperlo
							// y dejar que HarvestCheck decida entre arar o replantar.
							m_stateMachine.TransitionTo("Harvest");
						}
						else if (IsGrassOrDirt(contents) && HasPlantAbove(x, y, z))
						{
							m_stateMachine.TransitionTo("RakeTrampled");
						}
						else if (IsGrassOrDirt(contents))
						{
							m_rakeAttempts = 0;
							m_stateMachine.TransitionTo("Rake");
						}
						else if (IsSoil(contents))
						{
							bool needsFertilizer = !IsSoilAlreadyFertilized(value);

							if (HasTool(typeof(SeedsBlock)) && needsFertilizer && HasFertilizer())
							{
								m_stateMachine.TransitionTo("FertilizeDelay");
							}
							else if (HasTool(typeof(SeedsBlock)))
							{
								m_stateMachine.TransitionTo("PlantDelay");
							}
							else
							{
								m_stateMachine.TransitionTo("Inactive");
							}
						}
						else
						{
							m_stateMachine.TransitionTo("Inactive");
						}
					}
					else if (m_componentPathfinding != null && m_initialized && m_componentPathfinding.IsStuck)
					{
						m_blockedCount++;
						if (m_blockedCount > 3)
						{
							m_stateMachine.TransitionTo("Inactive");
						}
						else
						{
							m_componentPathfinding.IsStuck = false;
							m_targetPosition += new Vector3(m_random.Float(-0.5f, 0.5f), 0, m_random.Float(-0.5f, 0.5f));
							m_componentPathfinding.SetDestination(m_targetPosition, 0.8f, 0.8f, 500, false, false, false, null);
						}
					}
				},
				leave: null
			);

			m_stateMachine.AddState("RakeTrampled",
				enter: () =>
				{
					m_stateEnterTime = m_subsystemTime.GameTime;

					if (m_targetCellFace == null)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					int x = m_targetCellFace.Value.X;
					int y = m_targetCellFace.Value.Y;
					int z = m_targetCellFace.Value.Z;

					int currentValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
					int currentContents = Terrain.ExtractContents(currentValue);

					if (IsGrassOrDirt(currentContents))
					{
						int soilIndex = BlocksManager.GetBlockIndex("SoilBlock", false);
						if (soilIndex >= 0)
						{
							int newValue = Terrain.MakeBlockValue(soilIndex, 0, 0);
							m_subsystemTerrain.DestroyCell(0, x, y, z, newValue, true, false, null);
						}
					}

					if (HasTool(typeof(SeedsBlock)))
					{
						if (HasFertilizer())
							m_stateMachine.TransitionTo("FertilizeDelay");
						else
							m_stateMachine.TransitionTo("PlantDelay");
					}
					else
					{
						m_stateMachine.TransitionTo("Inactive");
					}
				},
				update: null,
				leave: null
			);

			m_stateMachine.AddState("Rake",
				enter: () =>
				{
					m_stateEnterTime = m_subsystemTime.GameTime;

					if (!SwitchToTool(typeof(RakeBlock)))
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					m_lastKnownActiveSlotIndex = m_inventory.ActiveSlotIndex;

					if (m_targetCellFace != null)
					{
						Ray3 ray = GetRayToBlock(m_targetCellFace.Value);
						m_componentMiner.Use(ray);
					}

					m_stateMachine.TransitionTo("RakeCheck");
				},
				update: null,
				leave: null
			);

			m_stateMachine.AddState("RakeCheck",
				enter: () =>
				{
					m_stateEnterTime = m_subsystemTime.GameTime;
				},
				update: () =>
				{
					if (m_subsystemTime.GameTime - m_stateEnterTime < STATE_DELAY)
						return;

					if (m_targetCellFace == null)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					int x = m_targetCellFace.Value.X;
					int y = m_targetCellFace.Value.Y;
					int z = m_targetCellFace.Value.Z;
					int value = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
					int contents = Terrain.ExtractContents(value);

					if (IsSoil(contents))
					{
						if (HasTool(typeof(SeedsBlock)))
						{
							bool needsFertilizer = !IsSoilAlreadyFertilized(value);

							if (needsFertilizer && HasFertilizer())
								m_stateMachine.TransitionTo("FertilizeDelay");
							else
								m_stateMachine.TransitionTo("PlantDelay");
						}
						else
						{
							m_stateMachine.TransitionTo("Inactive");
						}
					}
					else
					{
						m_rakeAttempts++;
						if (m_rakeAttempts < MAX_RAKE_ATTEMPTS)
							m_stateMachine.TransitionTo("Rake");
						else
							m_stateMachine.TransitionTo("Inactive");
					}
				},
				leave: null
			);

			m_stateMachine.AddState("FertilizeDelay",
				enter: () => { m_stateEnterTime = m_subsystemTime.GameTime; },
				update: () =>
				{
					if (m_subsystemTime.GameTime - m_stateEnterTime > TIME_TO_FERTILIZE)
						m_stateMachine.TransitionTo("Fertilize");
				},
				leave: null
			);

			m_stateMachine.AddState("Fertilize",
				enter: () =>
				{
					m_stateEnterTime = m_subsystemTime.GameTime;

					if (m_targetCellFace == null)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					if (!SwitchToFertilizer())
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					m_lastKnownActiveSlotIndex = m_inventory.ActiveSlotIndex;

					Ray3 fertilizeRay = GetRayToBlock(m_targetCellFace.Value);
					m_componentMiner.Use(fertilizeRay);

					if (HasTool(typeof(SeedsBlock)))
						m_stateMachine.TransitionTo("PlantDelay");
					else
						m_stateMachine.TransitionTo("Inactive");
				},
				update: null,
				leave: null
			);

			m_stateMachine.AddState("PlantDelay",
				enter: () => { m_stateEnterTime = m_subsystemTime.GameTime; },
				update: () =>
				{
					if (m_subsystemTime.GameTime - m_stateEnterTime > TIME_TO_PLANT_SEED)
						m_stateMachine.TransitionTo("Plant");
				},
				leave: null
			);

			m_stateMachine.AddState("Plant",
				enter: () =>
				{
					m_stateEnterTime = m_subsystemTime.GameTime;

					if (m_targetCellFace == null)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					if (!SwitchToSeed())
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					m_lastKnownActiveSlotIndex = m_inventory.ActiveSlotIndex;

					int above = m_subsystemTerrain.Terrain.GetCellContents(
						m_targetCellFace.Value.X,
						m_targetCellFace.Value.Y + 1,
						m_targetCellFace.Value.Z
					);
					if (above != 0)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					Ray3 plantRay = GetRayToBlock(m_targetCellFace.Value);
					var result = m_componentMiner.Raycast<TerrainRaycastResult>(plantRay, RaycastMode.Interaction, true, true, true, null);
					if (result != null && result.Value.CellFace.Face == 4 && IsSoil(Terrain.ExtractContents(result.Value.Value)))
					{
						m_componentMiner.Place(result.Value);
					}

					m_stateMachine.TransitionTo("Inactive");
				},
				update: null,
				leave: null
			);

			m_stateMachine.AddState("Harvest",
				enter: () =>
				{
					m_stateEnterTime = m_subsystemTime.GameTime;

					if (m_targetCellFace == null)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					CellFace harvestedCell = m_targetCellFace.Value;
					m_subsystemTerrain.DestroyCell(0, harvestedCell.X, harvestedCell.Y, harvestedCell.Z, 0, false, false, null);

					CollectPickables();

					m_stateMachine.TransitionTo("HarvestCheck");
				},
				update: null,
				leave: null
			);

			m_stateMachine.AddState("HarvestCheck",
				enter: () =>
				{
					m_stateEnterTime = m_subsystemTime.GameTime;
				},
				update: () =>
				{
					if (m_subsystemTime.GameTime - m_stateEnterTime > STATE_TIMEOUT)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					if (m_subsystemTime.GameTime - m_stateEnterTime < STATE_DELAY)
						return;

					if (m_targetCellFace == null)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					int x = m_targetCellFace.Value.X;
					int y = m_targetCellFace.Value.Y;
					int z = m_targetCellFace.Value.Z;

					if (y <= 0)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					int groundValue = m_subsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
					int groundContents = Terrain.ExtractContents(groundValue);
					int current = m_subsystemTerrain.Terrain.GetCellContents(x, y, z);

					if (current != 0)
					{
						int currentValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
						int currentContents = Terrain.ExtractContents(currentValue);

						if (IsHarvestable(currentContents, currentValue) ||
							IsBadCrop(currentContents, currentValue, x, y, z))
						{
							m_stateMachine.TransitionTo("Harvest");
							return;
						}
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					if (IsGrassOrDirt(groundContents))
					{
						if (HasTool(typeof(RakeBlock)))
						{
							m_targetCellFace = new CellFace { X = x, Y = y - 1, Z = z, Face = 4 };
							m_targetPosition = new Vector3(x + 0.5f, (y - 1) + 0.5f, z + 0.5f);
							m_rakeAttempts = 0;
							m_stateMachine.TransitionTo("Rake");
							return;
						}
					}

					if (IsSoil(groundContents))
					{
						if (HasTool(typeof(SeedsBlock)))
						{
							m_targetCellFace = new CellFace { X = x, Y = y - 1, Z = z, Face = 4 };
							m_targetPosition = new Vector3(x + 0.5f, (y - 1) + 0.5f, z + 0.5f);

							bool needsFertilizer = !IsSoilAlreadyFertilized(groundValue);

							if (needsFertilizer && HasFertilizer())
								m_stateMachine.TransitionTo("FertilizeDelay");
							else
								m_stateMachine.TransitionTo("PlantDelay");
							return;
						}
					}

					m_stateMachine.TransitionTo("Inactive");
				},
				leave: null
			);
		}

		private bool HasPlantAbove(int x, int y, int z)
		{
			if (y + 1 > 255) return false;
			int aboveContents = m_subsystemTerrain.Terrain.GetCellContents(x, y + 1, z);
			return aboveContents != 0;
		}

		private bool IsCropPlant(int contents)
		{
			if (contents <= 0 || contents >= BlocksManager.Blocks.Length) return false;
			Block block = BlocksManager.Blocks[contents];
			return block is RyeBlock || block is CottonBlock || block is BasePumpkinBlock || block is BlueberryBushBlock || block is BaseWatermelonBlock;
		}

		private void CollectPickables()
		{
			if (m_inventory == null || m_subsystemPickables == null)
				return;

			Vector3 pos = m_componentCreature.ComponentBody.Position;
			List<Pickable> toRemove = new List<Pickable>();

			foreach (Pickable pickable in m_subsystemPickables.Pickables)
			{
				float dist = Vector3.Distance(pickable.Position, pos);
				if (dist > PICKUP_RADIUS)
					continue;

				int value = pickable.Value;
				int count = pickable.Count;
				int remaining = count;

				for (int i = 0; i < m_inventory.SlotsCount; i++)
				{
					int slotValue = m_inventory.GetSlotValue(i);
					int slotCount = m_inventory.GetSlotCount(i);
					int capacity = m_inventory.GetSlotCapacity(i, value);

					if (slotValue == 0 || slotValue == value)
					{
						int space = capacity - slotCount;
						if (space > 0)
						{
							int add = Math.Min(space, remaining);
							m_inventory.AddSlotItems(i, value, add);
							remaining -= add;
							if (remaining == 0)
								break;
						}
					}
				}

				if (remaining == 0)
					toRemove.Add(pickable);
				else if (remaining < count)
					pickable.Count = remaining;
			}

			foreach (Pickable p in toRemove)
				p.ToRemove = true;
		}

		private bool FindBestTask(out CellFace bestCell, out TaskPriority bestPriority)
		{
			bestCell = default;
			bestPriority = TaskPriority.Rake; // valor por defecto (no usado si return false)
			TaskPriority? bestPriorityNullable = null;

			Vector3 pos = m_componentCreature.ComponentBody.Position;
			int cx = Terrain.ToCell(pos.X);
			int cy = Terrain.ToCell(pos.Y);
			int cz = Terrain.ToCell(pos.Z);
			int radius = (int)Math.Ceiling(SCAN_RADIUS);

			bool hasRake = HasTool(typeof(RakeBlock));
			bool hasSeeds = HasTool(typeof(SeedsBlock));

			int xMin = cx - radius;
			int xMax = cx + radius;
			int zMin = cz - radius;
			int zMax = cz + radius;
			int yMin = Math.Max(0, cy - 5);
			int yMax = Math.Min(255, cy + 6);

			// Si hay área asignada, restringir el escaneo al rectángulo marcado.
			if (m_hasFarmAreaBounds)
			{
				xMin = Math.Max(xMin, m_farmAreaMin.X);
				xMax = Math.Min(xMax, m_farmAreaMax.X);
				zMin = Math.Max(zMin, m_farmAreaMin.Z);
				zMax = Math.Min(zMax, m_farmAreaMax.Z);
				yMin = Math.Max(yMin, m_farmAreaMin.Y);
				// +2: los cultivos crecen por encima del suelo marcado;
				//     sin este margen nunca serían escaneados.
				yMax = Math.Min(yMax, m_farmAreaMax.Y + 2);
			}

			// Si el rectángulo quedó vacío (fuera del radio del farmer), no hay tareas.
			if (xMin > xMax || zMin > zMax || yMin > yMax)
				return false;

			for (int x = xMin; x <= xMax; x++)
			{
				for (int z = zMin; z <= zMax; z++)
				{
					for (int y = yMin; y <= yMax; y++)
					{
						int value = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
						int contents = Terrain.ExtractContents(value);

						if (contents == 0) continue;

						CellFace cell = new CellFace { X = x, Y = y, Z = z, Face = 4 };
						float dist = GetCellDistance(x, y, z, pos);

						if (IsHarvestable(contents, value))
						{
							if (IsBetterTask(TaskPriority.HarvestMature, dist, bestPriorityNullable, bestCell, pos))
							{
								bestPriorityNullable = TaskPriority.HarvestMature;
								bestCell = cell;
							}
							continue;
						}

						if (IsBadCrop(contents, value, x, y, z))
						{
							if (IsBetterTask(TaskPriority.ClearBadCrop, dist, bestPriorityNullable, bestCell, pos))
							{
								bestPriorityNullable = TaskPriority.ClearBadCrop;
								bestCell = cell;
							}
							continue;
						}

						int above = m_subsystemTerrain.Terrain.GetCellContents(x, y + 1, z);

						if (IsGrassOrDirt(contents) && above != 0 && hasRake)
						{
							if (IsBetterTask(TaskPriority.RakeTrampled, dist, bestPriorityNullable, bestCell, pos))
							{
								bestPriorityNullable = TaskPriority.RakeTrampled;
								bestCell = cell;
							}
							continue;
						}

						if (IsSoil(contents) && above == 0 && hasSeeds)
						{
							if (IsBetterTask(TaskPriority.Plant, dist, bestPriorityNullable, bestCell, pos))
							{
								bestPriorityNullable = TaskPriority.Plant;
								bestCell = cell;
							}
							continue;
						}

						if (IsGrassOrDirt(contents) && above == 0 && hasRake)
						{
							if (IsBetterTask(TaskPriority.Rake, dist, bestPriorityNullable, bestCell, pos))
							{
								bestPriorityNullable = TaskPriority.Rake;
								bestCell = cell;
							}
						}
					}
				}
			}

			if (bestPriorityNullable == null)
				return false;

			bestPriority = bestPriorityNullable.Value;
			return true;
		}

		private float GetCellDistance(int x, int y, int z, Vector3 pos)
		{
			return Vector3.Distance(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), pos);
		}

		private bool IsBetterTask(TaskPriority newPriority, float newDist, TaskPriority? currentPriority, CellFace currentCell, Vector3 pos)
		{
			if (currentPriority == null)
				return true;

			if (newPriority < currentPriority.Value)
				return true;

			if (newPriority == currentPriority.Value)
			{
				float currentDist = GetCellDistance(currentCell.X, currentCell.Y, currentCell.Z, pos);
				return newDist < currentDist;
			}

			return false;
		}

		private bool IsGrassOrDirt(int contents)
		{
			if (contents <= 0 || contents >= BlocksManager.Blocks.Length) return false;
			Block block = BlocksManager.Blocks[contents];
			return block is GrassBlock || block is DirtBlock;
		}

		private bool IsSoil(int contents)
		{
			if (contents <= 0 || contents >= BlocksManager.Blocks.Length) return false;
			return BlocksManager.Blocks[contents] is SoilBlock;
		}

		private bool IsHarvestable(int contents, int value)
		{
			if (contents <= 0 || contents >= BlocksManager.Blocks.Length) return false;
			Block block = BlocksManager.Blocks[contents];

			if (block is RyeBlock)
			{
				int data = Terrain.ExtractData(value);
				int size = RyeBlock.GetSize(data);
				return size >= 7;
			}
			if (block is CottonBlock)
			{
				int data = Terrain.ExtractData(value);
				int size = CottonBlock.GetSize(data);
				return size >= 2;
			}
			if (block is BasePumpkinBlock)
			{
				int data = Terrain.ExtractData(value);
				int size = BasePumpkinBlock.GetSize(data);
				bool isDead = BasePumpkinBlock.GetIsDead(data);
				return size >= 7 && !isDead;
			}
			if (block is BaseWatermelonBlock)
			{
				int data = Terrain.ExtractData(value);
				int size = BaseWatermelonBlock.GetSize(data);
				bool isDead = BaseWatermelonBlock.GetIsDead(data);
				return size >= 7 && !isDead;
			}
			if (block is BlueberryBushBlock)
			{
				int data = Terrain.ExtractData(value);
				bool isSmall = BlueberryBushBlock.GetIsSmall(data);
				return !isSmall;
			}
			return false;
		}

		/// <summary>
		/// True si el bloque es un cultivo "inservible": ya no dará fruto aunque
		/// se espere. Incluye:
		///   - Podridos / muertos (pumpkin / watermelon con flag isDead).
		///   - Silvestres (rye / cotton con flag wild): nunca serán cultivo doméstico.
		///   - Arraigados sobre algo que no es tierra arada (SoilBlock): no crecerán.
		/// </summary>
		private bool IsBadCrop(int contents, int value, int x, int y, int z)
		{
			if (contents <= 0 || contents >= BlocksManager.Blocks.Length) return false;
			Block block = BlocksManager.Blocks[contents];

			// 1) Podrido / muerto
			if (block is BasePumpkinBlock)
			{
				int data = Terrain.ExtractData(value);
				if (BasePumpkinBlock.GetIsDead(data)) return true;
			}
			if (block is BaseWatermelonBlock)
			{
				int data = Terrain.ExtractData(value);
				if (BaseWatermelonBlock.GetIsDead(data)) return true;
			}

			// 2) Silvestre (solo rye y cotton tienen flag wild)
			if (block is RyeBlock && RyeBlock.GetIsWild(Terrain.ExtractData(value)))
				return true;
			if (block is CottonBlock && CottonBlock.GetIsWild(Terrain.ExtractData(value)))
				return true;

			// 3) Arraigado en algo que no es SoilBlock → no crecerá bien.
			//    Aplica a cultivos que van directos sobre la tierra (rye, cotton,
			//    blueberry). Pumpkins/watermelons forman tallo + fruto y su bloque
			//    inferior no es necesariamente tierra, así que no se chequean aquí.
			if (block is RyeBlock || block is CottonBlock || block is BlueberryBushBlock)
			{
				if (y > 0)
				{
					int belowContents = m_subsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
					if (!IsSoil(belowContents)) return true;
				}
			}

			return false;
		}

		private bool HasTool(Type toolType)
		{
			return FindSlotWithTool(toolType) >= 0;
		}

		private bool SwitchToTool(Type toolType)
		{
			int slot = FindSlotWithTool(toolType);
			if (slot < 0) return false;
			m_inventory.ActiveSlotIndex = slot;
			return true;
		}

		private bool SwitchToSeed()
		{
			int slot = FindSlotWithSeed();
			if (slot < 0) return false;
			m_inventory.ActiveSlotIndex = slot;
			return true;
		}

		private bool HasFertilizer()
		{
			return FindSlotWithFertilizer() >= 0;
		}

		private bool SwitchToFertilizer()
		{
			int slot = FindSlotWithFertilizer();
			if (slot < 0) return false;
			m_inventory.ActiveSlotIndex = slot;
			return true;
		}

		private Ray3 GetRayToBlock(CellFace cell)
		{
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 target = new Vector3(cell.X + 0.5f, cell.Y + 0.5f, cell.Z + 0.5f);
			Vector3 dir = Vector3.Normalize(target - eyePos);
			return new Ray3(eyePos, dir);
		}

		public virtual void Update(float dt)
		{
			if (!m_initialized && m_stateMachineBuilt)
			{
				m_initialized = true;
				m_stateMachine.TransitionTo("Inactive");
			}

			m_stateMachine.Update();
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			valuesDictionary.SetValue("FarmAreaId", FarmAreaId);
			valuesDictionary.SetValue<bool>("FarmerEnabled", m_farmerEnabled);
			valuesDictionary.SetValue<bool>("HasFarmAreaCenter", m_hasFarmAreaCenter);
			if (m_hasFarmAreaCenter)
			{
				valuesDictionary.SetValue("FarmAreaCenter", m_farmAreaCenter);
			}

			valuesDictionary.SetValue<bool>("HasFarmAreaBounds", m_hasFarmAreaBounds);
			if (m_hasFarmAreaBounds)
			{
				valuesDictionary.SetValue("FarmAreaMin", m_farmAreaMin);
				valuesDictionary.SetValue("FarmAreaMax", m_farmAreaMax);
			}
		}

		/// <summary>
		/// Llamado por SubsystemFarmerWandBlockBehavior cuando el jugador marca
		/// un área con la varilla del granjero.
		/// </summary>
		public void SetFarmArea(Vector3 center, float radius)
		{
			m_farmAreaCenter = center;
			m_hasFarmAreaCenter = true;

			m_nextScanTime = 0;

			if (m_importanceLevel < BASE_IMPORTANCE_MIN)
				m_importanceLevel = BASE_IMPORTANCE_MIN;

			if (m_stateMachineBuilt && m_initialized)
			{
				m_stateMachine.TransitionTo("Inactive");
			}
		}

		private bool IsSoilAlreadyFertilized(int value)
		{
			int contents = Terrain.ExtractContents(value);
			if (!IsSoil(contents)) return false;
			return SoilBlock.GetNitrogen(Terrain.ExtractData(value)) > 0;
		}
	}
}
