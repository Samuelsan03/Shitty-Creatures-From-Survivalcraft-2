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
		// Prioridades de tarea
		private const int PRIORITY_HARVEST_MATURE = 0;
		private const int PRIORITY_RAKE_TRAMPLED = 1;
		private const int PRIORITY_PLANT = 2;
		private const int PRIORITY_RAKE = 3;

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

		private bool m_stateMachineBuilt;
		private bool m_initialized; // <--- NUEVO: indica si ya se puede usar ComponentPathfinding

		// Para detectar cuando se cambió el slot activo externamente (ej. por combate)
		private int m_lastKnownActiveSlotIndex = -1;
		private double m_lastToolCheckTime;
		private const double TOOL_CHECK_INTERVAL = 0.5;

		public override float ImportanceLevel => m_importanceLevel;

		/// <summary>
		/// Controlado por el usuario desde el widget. No se desactiva automáticamente.
		/// </summary>
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
						// Solo transicionar si la máquina de estados ya está construida y estamos inicializados
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

			BuildStateMachine();
			m_stateMachineBuilt = true;

			// NO transicionar a "Inactive" aquí - esperar a que todos los componentes estén cargados
			// m_stateMachine.TransitionTo("Inactive");  // <--- ELIMINADO

			m_initialized = false; // <--- NUEVO: aún no estamos listos
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
			if (!m_hasFarmAreaCenter) return false;

			Vector3 pos = m_componentCreature.ComponentBody.Position;
			float dx = pos.X - m_farmAreaCenter.X;
			float dz = pos.Z - m_farmAreaCenter.Z;
			float horizontalDist = MathF.Sqrt(dx * dx + dz * dz);

			return horizontalDist > MAX_DISTANCE_FROM_AREA;
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
					// REINICIAR TIEMPO DE ESCANEO
					m_nextScanTime = 0;

					// Detener pathfinding solo si estamos inicializados
					if (m_componentPathfinding != null && m_initialized)
					{
						m_componentPathfinding.Stop();
						m_componentPathfinding.IsStuck = false;
					}

					if (m_farmerEnabled && HasFarmingTools())
					{
						m_importanceLevel = m_random.Float(BASE_IMPORTANCE_MIN, BASE_IMPORTANCE_MAX);
						EnsureFarmingToolEquipped();
					}
					else
					{
						m_importanceLevel = 0f;
					}
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
						m_importanceLevel = m_random.Float(BASE_IMPORTANCE_MIN, BASE_IMPORTANCE_MAX);
						return;
					}

					if (m_subsystemTime.GameTime - m_lastToolCheckTime > TOOL_CHECK_INTERVAL)
					{
						m_lastToolCheckTime = m_subsystemTime.GameTime;
						CheckAndRestoreFarmingTool();
					}

					m_importanceLevel = m_random.Float(BASE_IMPORTANCE_MIN, BASE_IMPORTANCE_MAX);

					if (IsTooFarFromArea())
					{
						m_stateMachine.TransitionTo("ReturnToArea");
						return;
					}

					if (m_subsystemTime.GameTime > m_nextScanTime)
					{
						m_nextScanTime = m_subsystemTime.GameTime + m_random.Float(0.2f, 0.5f);
						if (FindBestTask(out CellFace target, out int priority))
						{
							m_targetCellFace = target;
							m_targetPosition = new Vector3(target.X + 0.5f, target.Y + 0.5f, target.Z + 0.5f);
							m_importanceLevel = m_random.Float(TASK_IMPORTANCE_MIN, TASK_IMPORTANCE_MAX);

							if (!m_hasFarmAreaCenter)
							{
								UpdateFarmAreaCenter();
							}

							m_stateMachine.TransitionTo("MoveToTarget");
						}
					}
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
						else if (IsGrassOrDirt(contents) && HasPlantAbove(x, y, z))
						{
							m_stateMachine.TransitionTo("RakeTrampled");
						}
						else if (IsGrassOrDirt(contents))
						{
							m_stateMachine.TransitionTo("Rake");
						}
						else if (IsSoil(contents))
						{
							if (HasTool(typeof(SeedsBlock)) && HasFertilizer())
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

					// Después de arreglar el suelo pisoteado, continuar el ciclo
					if (HasTool(typeof(SeedsBlock)))
					{
						if (HasFertilizer())
						{
							m_stateMachine.TransitionTo("FertilizeDelay");
						}
						else
						{
							m_stateMachine.TransitionTo("PlantDelay");
						}
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

					if (m_targetCellFace != null)
					{
						int value = m_subsystemTerrain.Terrain.GetCellValue(
							m_targetCellFace.Value.X,
							m_targetCellFace.Value.Y,
							m_targetCellFace.Value.Z
						);
						int contents = Terrain.ExtractContents(value);

						if (IsSoil(contents) && HasTool(typeof(SeedsBlock)))
						{
							if (HasFertilizer())
							{
								m_stateMachine.TransitionTo("FertilizeDelay");
							}
							else
							{
								m_stateMachine.TransitionTo("PlantDelay");
							}
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
				},
				update: null,
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
					{
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

						if (IsHarvestable(currentContents, currentValue))
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
							if (HasFertilizer())
							{
								m_stateMachine.TransitionTo("FertilizeDelay");
							}
							else
							{
								m_stateMachine.TransitionTo("PlantDelay");
							}
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

		private bool FindBestTask(out CellFace bestCell, out int bestPriority)
		{
			bestCell = default;
			bestPriority = int.MaxValue;

			Vector3 pos = m_componentCreature.ComponentBody.Position;
			int cx = Terrain.ToCell(pos.X);
			int cy = Terrain.ToCell(pos.Y);
			int cz = Terrain.ToCell(pos.Z);
			int radius = (int)Math.Ceiling(SCAN_RADIUS);

			bool hasRake = HasTool(typeof(RakeBlock));
			bool hasSeeds = HasTool(typeof(SeedsBlock));

			int yMin = Math.Max(0, cy - 5);
			int yMax = Math.Min(255, cy + 6);

			for (int x = cx - radius; x <= cx + radius; x++)
			{
				for (int z = cz - radius; z <= cz + radius; z++)
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
							if (IsBetterTask(PRIORITY_HARVEST_MATURE, dist, bestPriority, bestCell, pos))
							{
								bestPriority = PRIORITY_HARVEST_MATURE;
								bestCell = cell;
							}
							continue;
						}

						int above = m_subsystemTerrain.Terrain.GetCellContents(x, y + 1, z);

						if (IsGrassOrDirt(contents) && above != 0 && hasRake)
						{
							if (IsBetterTask(PRIORITY_RAKE_TRAMPLED, dist, bestPriority, bestCell, pos))
							{
								bestPriority = PRIORITY_RAKE_TRAMPLED;
								bestCell = cell;
							}
							continue;
						}

						if (IsSoil(contents) && above == 0 && hasSeeds)
						{
							if (IsBetterTask(PRIORITY_PLANT, dist, bestPriority, bestCell, pos))
							{
								bestPriority = PRIORITY_PLANT;
								bestCell = cell;
							}
							continue;
						}

						if (IsGrassOrDirt(contents) && above == 0 && hasRake)
						{
							if (IsBetterTask(PRIORITY_RAKE, dist, bestPriority, bestCell, pos))
							{
								bestPriority = PRIORITY_RAKE;
								bestCell = cell;
							}
						}
					}
				}
			}

			return bestPriority != int.MaxValue;
		}

		private float GetCellDistance(int x, int y, int z, Vector3 pos)
		{
			return Vector3.Distance(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), pos);
		}

		private bool IsBetterTask(int newPriority, float newDist, int currentPriority, CellFace currentCell, Vector3 pos)
		{
			if (newPriority < currentPriority)
				return true;

			if (newPriority == currentPriority && currentPriority != int.MaxValue)
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
			// Primera actualización: ahora todos los componentes están cargados, transicionamos a Inactive
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
			valuesDictionary.SetValue<bool>("FarmerEnabled", m_farmerEnabled);
			valuesDictionary.SetValue<bool>("HasFarmAreaCenter", m_hasFarmAreaCenter);
			if (m_hasFarmAreaCenter)
			{
				valuesDictionary.SetValue("FarmAreaCenter", m_farmAreaCenter);
			}
		}
	}
}
