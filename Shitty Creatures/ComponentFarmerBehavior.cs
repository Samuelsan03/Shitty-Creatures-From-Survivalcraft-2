using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Comportamiento de granjero: ara, siembra, cosecha, recolecta items/experiencia y vuelve a arar si la tierra se convierte en césped/tierra.
	/// </summary>
	public class ComponentFarmerBehavior : ComponentBehavior, IUpdateable
	{
		// Prioridades de tarea
		private const int PRIORITY_HARVEST_MATURE = 0;
		private const int PRIORITY_RAKE_TRAMPLED = 1;
		private const int PRIORITY_PLANT = 2;
		private const int PRIORITY_RAKE = 3;

		private const float BASE_IMPORTANCE_MIN = 0.01f;
		private const float BASE_IMPORTANCE_MAX = 0.5f;
		private const float TASK_IMPORTANCE_MIN = 5f;
		private const float TASK_IMPORTANCE_MAX = 10f;

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
		private bool m_farmerEnabled;  // CAMBIADO: Controlado SOLO por el usuario
		private double m_stateEnterTime;

		private const float SCAN_RADIUS = 15f;
		private const double STATE_DELAY = 0.5;
		private const float PICKUP_RADIUS = 2.5f;
		private const double STATE_TIMEOUT = 10.0;

		// Índices de bloques
		private int m_grassBlockIndex;
		private int m_dirtBlockIndex;
		private int m_soilBlockIndex;
		private int m_ryeBlockIndex;
		private int m_cottonBlockIndex;
		private int m_pumpkinBlockIndex;
		private int m_saltpeterBlockIndex;

		private int m_blueberrySeedIndex;
		private int m_watermelonSeedIndex;
		private int m_blueberryBushIndex;
		private int m_watermelonBlockIndex;

		private bool m_stateMachineBuilt;

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
						// Al desactivar: detener todo
						if (m_stateMachineBuilt)
						{
							m_stateMachine.TransitionTo("Inactive");
						}
						if (m_componentPathfinding != null)
						{
							m_componentPathfinding.Stop();
							m_componentPathfinding.IsStuck = false;
						}
						m_importanceLevel = 0f;
					}
					else
					{
						// Al activar: permitir que busque tareas
						m_nextScanTime = 0;
					}
				}
			}
		}

		// Override de IsActive para evitar que la clase base lo desactive
		public override bool IsActive
		{
			get => m_farmerEnabled;
			set
			{
				// Solo permitir activar, nunca desactivar automáticamente
				if (value)
				{
					m_farmerEnabled = true;
					m_nextScanTime = 0;
				}
				// Ignorar intentos de desactivar (la clase base podría intentar hacerlo)
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

			m_grassBlockIndex = BlocksManager.GetBlockIndex("GrassBlock", false);
			m_dirtBlockIndex = BlocksManager.GetBlockIndex("DirtBlock", false);
			m_soilBlockIndex = BlocksManager.GetBlockIndex("SoilBlock", false);
			m_ryeBlockIndex = BlocksManager.GetBlockIndex("RyeBlock", false);
			m_cottonBlockIndex = BlocksManager.GetBlockIndex("CottonBlock", false);
			m_pumpkinBlockIndex = BlocksManager.GetBlockIndex("PumpkinBlock", false);
			m_saltpeterBlockIndex = BlocksManager.GetBlockIndex("SaltpeterChunkBlock", false);

			m_blueberrySeedIndex = BlocksManager.GetBlockIndex("BlueberrySeedBlock", false);
			m_watermelonSeedIndex = BlocksManager.GetBlockIndex("WatermelonSeedBlock", false);
			m_blueberryBushIndex = BlocksManager.GetBlockIndex("BlueberryBushBlock", false);

			m_watermelonBlockIndex = BlocksManager.GetBlockIndex("WatermelonBlock", false);

			// Cargar estado guardado
			m_farmerEnabled = valuesDictionary.GetValue<bool>("FarmerEnabled", false);
			m_importanceLevel = 0f;
			BuildStateMachine();
			m_stateMachineBuilt = true;
			m_stateMachine.TransitionTo("Inactive");
		}

		private bool HasFarmingTools()
		{
			return HasTool(typeof(RakeBlock)) || HasTool(typeof(SeedsBlock));
		}

		private void BuildStateMachine()
		{
			m_stateMachine.AddState("Inactive",
				enter: () =>
				{
					// Si está habilitado y tiene herramientas, mantener importancia baja para seguir buscando
					if (m_farmerEnabled && HasFarmingTools())
					{
						m_importanceLevel = m_random.Float(BASE_IMPORTANCE_MIN, BASE_IMPORTANCE_MAX);
					}
					else
					{
						m_importanceLevel = 0f;
					}
					m_stateEnterTime = m_subsystemTime.GameTime;
					if (m_componentPathfinding != null)
					{
						m_componentPathfinding.IsStuck = false;
					}
				},
				update: () =>
				{
					// Si no está habilitado, no hacer nada
					if (!m_farmerEnabled)
					{
						m_importanceLevel = 0f;
						return;
					}

					if (!HasFarmingTools())
					{
						// Mantener importancia baja pero no cero, para reintentar cuando tenga herramientas
						m_importanceLevel = m_random.Float(BASE_IMPORTANCE_MIN, BASE_IMPORTANCE_MAX);
						return;
					}

					m_importanceLevel = m_random.Float(BASE_IMPORTANCE_MIN, BASE_IMPORTANCE_MAX);

					if (m_subsystemTime.GameTime > m_nextScanTime)
					{
						m_nextScanTime = m_subsystemTime.GameTime + m_random.Float(0.5f, 2.0f);
						if (FindBestTask(out CellFace target, out int priority))
						{
							m_targetCellFace = target;
							m_targetPosition = new Vector3(target.X + 0.5f, target.Y + 0.5f, target.Z + 0.5f);
							m_importanceLevel = m_random.Float(TASK_IMPORTANCE_MIN, TASK_IMPORTANCE_MAX);
							m_stateMachine.TransitionTo("MoveToTarget");
						}
					}
				},
				leave: null
			);

			m_stateMachine.AddState("MoveToTarget",
				enter: () =>
				{
					m_stateEnterTime = m_subsystemTime.GameTime;
					m_blockedCount = 0;
					if (m_componentPathfinding != null)
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
					// Si se deshabilitó durante el movimiento, volver a inactivo (pero sigue habilitado)
					if (!m_farmerEnabled)
					{
						if (m_componentPathfinding != null)
							m_componentPathfinding.Stop();
						m_stateMachine.TransitionTo("Inactive");
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
							m_stateMachine.TransitionTo("Plant");
						}
						else
						{
							m_stateMachine.TransitionTo("Inactive");
						}
					}
					else if (m_componentPathfinding != null && m_componentPathfinding.IsStuck)
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

					if (IsGrassOrDirt(currentContents) && m_soilBlockIndex >= 0)
					{
						int newValue = Terrain.MakeBlockValue(m_soilBlockIndex, 0, 0);
						m_subsystemTerrain.DestroyCell(0, x, y, z, newValue, true, false, null);
					}

					m_stateMachine.TransitionTo("Inactive");
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
							m_stateMachine.TransitionTo("Plant");
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

					if (HasFertilizer())
					{
						m_stateMachine.TransitionTo("FertilizeDelay");
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
					if (m_subsystemTime.GameTime - m_stateEnterTime > STATE_DELAY)
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

					Ray3 fertilizeRay = GetRayToBlock(m_targetCellFace.Value);
					m_componentMiner.Use(fertilizeRay);

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
							m_stateMachine.TransitionTo("Plant");
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
			return contents == m_ryeBlockIndex ||
				   contents == m_cottonBlockIndex ||
				   contents == m_pumpkinBlockIndex ||
				   contents == m_blueberryBushIndex ||
				   contents == m_watermelonBlockIndex;
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
			return contents == m_grassBlockIndex || contents == m_dirtBlockIndex;
		}

		private bool IsSoil(int contents)
		{
			return contents == m_soilBlockIndex;
		}

		private bool IsHarvestable(int contents, int value)
		{
			if (contents == m_ryeBlockIndex)
			{
				int data = Terrain.ExtractData(value);
				int size = RyeBlock.GetSize(data);
				return size >= 7;
			}
			if (contents == m_cottonBlockIndex)
			{
				int data = Terrain.ExtractData(value);
				int size = CottonBlock.GetSize(data);
				return size >= 2;
			}
			if (contents == m_pumpkinBlockIndex)
			{
				int data = Terrain.ExtractData(value);
				int size = BasePumpkinBlock.GetSize(data);
				bool isDead = BasePumpkinBlock.GetIsDead(data);
				return size >= 7 && !isDead;
			}
			if (contents == m_watermelonBlockIndex)
			{
				int data = Terrain.ExtractData(value);
				int size = BaseWatermelonBlock.GetSize(data);
				bool isDead = BaseWatermelonBlock.GetIsDead(data);
				return size >= 7 && !isDead;
			}
			if (contents == m_blueberryBushIndex)
			{
				int data = Terrain.ExtractData(value);
				bool isSmall = BlueberryBushBlock.GetIsSmall(data);
				return !isSmall;
			}
			return false;
		}

		private bool HasTool(Type toolType)
		{
			if (m_inventory == null) return false;
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(value);
				Block block = BlocksManager.Blocks[contents];
				if (toolType.IsAssignableFrom(block.GetType()))
					return true;
			}
			return false;
		}

		private bool SwitchToTool(Type toolType)
		{
			if (m_inventory == null) return false;
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(value);
				Block block = BlocksManager.Blocks[contents];
				if (toolType.IsAssignableFrom(block.GetType()))
				{
					m_inventory.ActiveSlotIndex = i;
					return true;
				}
			}
			return false;
		}

		private bool SwitchToSeed()
		{
			if (m_inventory == null) return false;
			List<int> seedSlots = new List<int>();
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(value);
				Block block = BlocksManager.Blocks[contents];

				if (typeof(SeedsBlock).IsAssignableFrom(block.GetType()) ||
					contents == m_blueberrySeedIndex ||
					contents == m_watermelonSeedIndex)
				{
					seedSlots.Add(i);
				}
			}
			if (seedSlots.Count == 0)
				return false;
			int selectedSlot = seedSlots[m_random.Int(seedSlots.Count)];
			m_inventory.ActiveSlotIndex = selectedSlot;
			return true;
		}

		private bool HasFertilizer()
		{
			if (m_inventory == null) return false;
			if (m_saltpeterBlockIndex < 0) return false;
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(value);
				if (contents == m_saltpeterBlockIndex)
					return true;
			}
			return false;
		}

		private bool SwitchToFertilizer()
		{
			if (m_inventory == null) return false;
			if (m_saltpeterBlockIndex < 0) return false;
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				int value = m_inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(value);
				if (contents == m_saltpeterBlockIndex)
				{
					m_inventory.ActiveSlotIndex = i;
					return true;
				}
			}
			return false;
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
			m_stateMachine.Update();
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			valuesDictionary.SetValue<bool>("FarmerEnabled", m_farmerEnabled);
		}
	}
}
