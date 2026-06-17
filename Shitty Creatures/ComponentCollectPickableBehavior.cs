using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Componente que permite a una criatura recolectar objetos (Pickables) de ciertas categorías o bloques específicos,
	/// con probabilidades configurables. Utiliza ComponentMiner para el "poking" y su Inventory para guardar.
	/// Prioriza la persecución (Chase) sobre la recolección.
	/// </summary>
	public class ComponentCollectPickableBehavior : ComponentBehavior, IUpdateable
	{
		// ---- Propiedades públicas ----

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override float ImportanceLevel => m_importanceLevel;

		// ---- Campos ----

		private SubsystemTime m_subsystemTime;
		private SubsystemPickables m_subsystemPickables;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentMiner m_componentMiner;

		// Componentes de persecución (Chase)
		private ComponentChaseBehavior m_chaseBehavior;
		private ComponentNewChaseBehavior m_newChaseBehavior;
		private ComponentBanditChaseBehavior m_banditChaseBehavior;
		private ComponentZombieChaseBehavior m_zombieChaseBehavior;

		private bool m_inventoryApplied = false;

		private readonly StateMachine m_stateMachine = new StateMachine();
		private readonly Random m_random = new Random();

		// Factores de recolección por categoría
		private readonly Dictionary<string, float> m_collectFactors = new Dictionary<string, float>();

		// Factores de recolección por bloque específico
		private readonly Dictionary<string, float> m_blockFactors = new Dictionary<string, float>();

		// Conjunto de pickables "interesantes"
		private readonly HashSet<Pickable> m_interestingPickables = new HashSet<Pickable>();

		// Pickable objetivo actual
		private Pickable m_targetPickable;

		// Nivel de importancia actual
		private float m_importanceLevel;

		// Tiempos para evitar búsquedas frecuentes
		private double m_nextFindPickableTime;
		private double m_nextPickablesUpdateTime;

		// Contador de bloqueos para evitar bucles infinitos
		private int m_blockedCount;
		private float m_blockedTime;

		// Tiempo de recolección (simula la duración de la animación de recoger)
		private double m_collectTime;

		// ---- Propiedad auxiliar para saber si hay persecución activa ----

		private bool IsAnyChaseActive
		{
			get
			{
				if (m_chaseBehavior != null && m_chaseBehavior.Target != null && !m_chaseBehavior.Suppressed)
					return true;

				if (m_newChaseBehavior != null && m_newChaseBehavior.Target != null && !m_newChaseBehavior.Suppressed)
					return true;

				if (m_banditChaseBehavior != null && m_banditChaseBehavior.Target != null && !m_banditChaseBehavior.Suppressed)
					return true;

				if (m_zombieChaseBehavior != null && m_zombieChaseBehavior.Target != null && !m_zombieChaseBehavior.Suppressed)
					return true;

				return false;
			}
		}

		// ---- Métodos públicos ----

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true);

			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);

			m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			if (m_componentMiner == null)
			{
				Log.Warning("ComponentCollectPickableBehavior: No se encontró ComponentMiner en la entidad.");
			}

			// Componentes de persecución
			m_chaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(false);
			m_newChaseBehavior = base.Entity.FindComponent<ComponentNewChaseBehavior>(false);
			m_banditChaseBehavior = base.Entity.FindComponent<ComponentBanditChaseBehavior>(false);
			m_zombieChaseBehavior = base.Entity.FindComponent<ComponentZombieChaseBehavior>(false);

			// Cargar factores por categoría
			var factorsDict = valuesDictionary.GetValue<ValuesDictionary>("CollectFactors");
			foreach (var pair in factorsDict)
			{
				if (pair.Value is float prob)
				{
					m_collectFactors[pair.Key] = prob;
				}
				else
				{
					Log.Warning($"CollectFactor '{pair.Key}' no es un número válido, se ignora.");
				}
			}

			// Cargar factores por bloque específico
			string blockFactorsString = valuesDictionary.GetValue<string>("BlockFactors", string.Empty);
			if (!string.IsNullOrEmpty(blockFactorsString))
			{
				string[] entries = blockFactorsString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string entry in entries)
				{
					string trimmed = entry.Trim();
					string[] parts = trimmed.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length == 2)
					{
						string blockName = parts[0].Trim();
						if (float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float probability))
						{
							int blockIndex = BlocksManager.GetBlockIndex(blockName, false);
							if (blockIndex >= 0)
							{
								m_blockFactors[blockName] = probability;
							}
							else
							{
								Log.Warning($"BlockFactor: '{blockName}' no es un bloque válido, se ignora.");
							}
						}
						else
						{
							Log.Warning($"BlockFactor: '{parts[1]}' no es un número válido en la entrada '{entry}', se ignora.");
						}
					}
					else
					{
						Log.Warning($"BlockFactor: formato inválido en '{entry}', se esperaba 'NombreBloque:probabilidad'.");
					}
				}
			}

			// Suscribirse a eventos de PickableAdded/Removed
			m_subsystemPickables.PickableAdded += OnPickableAdded;
			m_subsystemPickables.PickableRemoved += OnPickableRemoved;

			ConfigureStateMachine();
			m_stateMachine.TransitionTo("Inactive");
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			// No hay datos persistentes por ahora
		}

		public void Update(float dt)
		{
			// ===== NUEVO: Aplicar inventario en el primer update cuando esté disponible =====
			if (!m_inventoryApplied)
			{
				// Intentar aplicar el inventario
				if (m_componentMiner != null && m_componentMiner.Inventory != null)
				{
					ApplyCreatureInventory(this.Entity);
					m_inventoryApplied = true;
				}
				// Si no está listo, se reintentará en el próximo frame
			}

			if (IsAnyChaseActive)
			{
				if (m_stateMachine.CurrentState != "Inactive")
				{
					m_stateMachine.TransitionTo("Inactive");
					m_importanceLevel = 0f;
					m_targetPickable = null;
				}
			}

			m_stateMachine.Update();
		}

		// ---- Manejo de eventos ----

		private void OnPickableAdded(Pickable pickable)
		{
			// Solo asignar objetivo si no tenemos uno actual
			if (!IsAnyChaseActive && TryAddPickable(pickable) && m_targetPickable == null)
			{
				m_targetPickable = pickable;
			}
		}

		private void OnPickableRemoved(Pickable pickable)
		{
			m_interestingPickables.Remove(pickable);
			if (m_targetPickable == pickable)
			{
				m_targetPickable = null;
			}
		}

		// ---- Lógica de recolección ----

		private bool TryAddPickable(Pickable pickable)
		{
			if (pickable == null || pickable.ToRemove)
				return false;

			int contents = Terrain.ExtractContents(pickable.Value);
			Block block = BlocksManager.Blocks[contents];
			string blockName = block.GetType().Name;

			float probability = -1f;

			if (m_blockFactors.TryGetValue(blockName, out float blockProb))
			{
				probability = blockProb;
			}
			else
			{
				string category = block.GetCategory(pickable.Value);
				if (m_collectFactors.TryGetValue(category, out float catProb))
				{
					probability = catProb;
				}
			}

			if (probability < 0f)
				return false;

			if (Vector3.DistanceSquared(pickable.Position, m_componentCreature.ComponentBody.Position) > 256f)
				return false;

			if (m_random.Float(0f, 1f) >= probability)
				return false;

			m_interestingPickables.Add(pickable);
			return true;
		}

		private Pickable FindPickable(Vector3 position)
		{
			if (IsAnyChaseActive)
				return null;

			// Si ya tenemos un objetivo y sigue siendo válido, mantenerlo
			if (m_targetPickable != null && !m_targetPickable.ToRemove && m_interestingPickables.Contains(m_targetPickable))
			{
				return m_targetPickable;
			}

			// Si no tenemos objetivo o el actual no es válido, buscar el más cercano
			if (m_subsystemTime.GameTime > m_nextPickablesUpdateTime)
			{
				m_nextPickablesUpdateTime = m_subsystemTime.GameTime + m_random.Float(0.5f, 0.5f);
				m_interestingPickables.Clear();

				foreach (var pickable in m_subsystemPickables.Pickables)
				{
					TryAddPickable(pickable);
				}
			}

			if (m_interestingPickables.Count == 0)
				return null;

			Pickable best = null;
			float bestDistSq = float.MaxValue;
			foreach (var pickable in m_interestingPickables)
			{
				float distSq = Vector3.DistanceSquared(position, pickable.Position);
				if (distSq < bestDistSq)
				{
					bestDistSq = distSq;
					best = pickable;
				}
			}

			return best;
		}

		private void CollectPickable(Pickable pickable)
		{
			if (pickable == null || pickable.ToRemove)
				return;

			m_componentMiner?.Poke(false);

			IInventory inventory = m_componentMiner?.Inventory;
			if (inventory == null)
			{
				Log.Warning("ComponentCollectPickableBehavior: No se puede recolectar, el inventario del miner es nulo.");
				return;
			}

			int value = pickable.Value;
			int count = pickable.Count;

			int slotIndex = FindSlotForItem(inventory, value, count);
			if (slotIndex >= 0)
			{
				inventory.AddSlotItems(slotIndex, value, count);
				pickable.ToRemove = true;

				if (m_componentCreature is ComponentPlayer player)
				{
					// player.PlayerStats.ItemsCollected++;
				}
			}
			else
			{
				Log.Warning($"No hay espacio en el inventario para {count}x{value}");
			}
		}

		private int FindSlotForItem(IInventory inventory, int value, int count)
		{
			if (inventory == null) return -1;

			int active = inventory.ActiveSlotIndex;

			if (active >= 0 && active < inventory.SlotsCount)
			{
				int existingValue = inventory.GetSlotValue(active);
				int existingCount = inventory.GetSlotCount(active);
				int capacity = inventory.GetSlotCapacity(active, value);

				if (existingValue == value && existingCount + count <= capacity)
					return active;
				if (existingValue == 0 && capacity >= count)
					return active;
			}

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (i == active) continue;
				int existingValue = inventory.GetSlotValue(i);
				int existingCount = inventory.GetSlotCount(i);
				int capacity = inventory.GetSlotCapacity(i, value);

				if (existingValue == value && existingCount + count <= capacity)
					return i;
				if (existingValue == 0 && capacity >= count)
					return i;
			}

			return -1;
		}

		// ---- Configuración de la máquina de estados ----

		private void ConfigureStateMachine()
		{
			m_stateMachine.AddState("Inactive",
				enter: () =>
				{
					m_importanceLevel = 0f;
					m_targetPickable = null;
				},
				update: () =>
				{
					if (IsAnyChaseActive)
						return;

					if (m_subsystemTime.GameTime > m_nextFindPickableTime)
					{
						m_nextFindPickableTime = m_subsystemTime.GameTime + m_random.Float(2f, 4f);
						m_targetPickable = FindPickable(m_componentCreature.ComponentBody.Position);
					}

					if (m_targetPickable != null)
					{
						m_importanceLevel = m_random.Float(5f, 10f);
						m_stateMachine.TransitionTo("Move");
						m_blockedCount = 0;
					}
				},
				leave: null
			);

			m_stateMachine.AddState("Move",
				enter: () =>
				{
					if (m_targetPickable != null)
					{
						float speed = 0.5f;
						int maxPath = 500;

						if (m_importanceLevel > 8f)
						{
							speed = 0.7f;
							maxPath = 1000;
						}

						float eyeOffset = Vector3.Distance(
							m_componentCreature.ComponentCreatureModel.EyePosition,
							m_componentCreature.ComponentBody.Position
						);

						m_componentPathfinding.SetDestination(
							new Vector3?(m_targetPickable.Position),
							speed,
							1f + eyeOffset,
							maxPath,
							true, false, true, null
						);

						if (m_random.Float(0f, 1f) < 0.66f)
						{
							m_componentCreature.ComponentCreatureSounds?.PlayIdleSound(true);
						}
					}
				},
				update: () =>
				{
					if (IsAnyChaseActive)
					{
						m_stateMachine.TransitionTo("Inactive");
						m_importanceLevel = 0f;
						return;
					}

					if (!IsActive)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					if (m_targetPickable == null)
					{
						m_importanceLevel = 0f;
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					if (m_componentPathfinding.IsStuck)
					{
						m_blockedCount++;
						if (m_blockedCount >= 3)
						{
							m_importanceLevel = 0f;
							m_stateMachine.TransitionTo("Inactive");
						}
						else
						{
							m_importanceLevel = 0f;
							m_stateMachine.TransitionTo("Inactive");
						}
						return;
					}

					if (m_componentPathfinding.Destination == null)
					{
						m_stateMachine.TransitionTo("Collect");
						return;
					}

					if (Vector3.DistanceSquared(m_componentPathfinding.Destination.Value, m_targetPickable.Position) > 0.25f)
					{
						m_stateMachine.TransitionTo("PickableMoved");
						return;
					}

					if (m_random.Float(0f, 1f) < 0.1f * m_subsystemTime.GameTimeDelta)
					{
						m_componentCreature.ComponentCreatureSounds?.PlayIdleSound(true);
					}

					if (m_targetPickable != null)
					{
						m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(m_targetPickable.Position);
					}
					else
					{
						m_componentCreature.ComponentCreatureModel.LookRandomOrder = true;
					}
				},
				leave: null
			);

			m_stateMachine.AddState("PickableMoved",
				enter: null,
				update: () =>
				{
					if (IsAnyChaseActive)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					if (m_targetPickable != null)
					{
						m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(m_targetPickable.Position);
					}

					if (m_subsystemTime.PeriodicGameTimeEvent(0.25, (double)(GetHashCode() % 100) * 0.01))
					{
						m_stateMachine.TransitionTo("Move");
					}
				},
				leave: null
			);

			m_stateMachine.AddState("Collect",
				enter: () =>
				{
					m_collectTime = m_random.Float(0.5f, 0.5f);
					m_blockedTime = 0f;
				},
				update: () =>
				{
					if (IsAnyChaseActive)
					{
						m_stateMachine.TransitionTo("Inactive");
						m_importanceLevel = 0f;
						return;
					}

					if (!IsActive)
					{
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					if (m_targetPickable == null)
					{
						m_importanceLevel = 0f;
						m_stateMachine.TransitionTo("Inactive");
						return;
					}

					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 targetPos = m_targetPickable.Position;
					float distSq = Vector3.DistanceSquared(
						new Vector3(eyePos.X, m_componentCreature.ComponentBody.Position.Y, eyePos.Z),
						targetPos
					);

					if (distSq < 0.64f)
					{
						m_collectTime -= m_subsystemTime.GameTimeDelta;
						m_blockedTime = 0f;

						if (m_collectTime <= 0f)
						{
							CollectPickable(m_targetPickable);
							m_importanceLevel = 0f;
							m_stateMachine.TransitionTo("Inactive");
						}
					}
					else
					{
						float eyeOffset = Vector3.Distance(
							m_componentCreature.ComponentCreatureModel.EyePosition,
							m_componentCreature.ComponentBody.Position
						);

						m_componentPathfinding.SetDestination(
							new Vector3?(m_targetPickable.Position),
							0.3f,
							0.5f + eyeOffset,
							0,
							false, true, false, null
						);

						m_blockedTime += m_subsystemTime.GameTimeDelta;

						if (m_blockedTime > 3f)
						{
							m_blockedCount++;
							if (m_blockedCount >= 3)
							{
								m_importanceLevel = 0f;
								m_stateMachine.TransitionTo("Inactive");
							}
							else
							{
								m_stateMachine.TransitionTo("Move");
							}
						}
					}

					if (m_targetPickable != null)
					{
						m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(m_targetPickable.Position);
					}

					if (m_random.Float(0f, 1f) < 0.1f * m_subsystemTime.GameTimeDelta)
					{
						m_componentCreature.ComponentCreatureSounds?.PlayIdleSound(true);
					}
				},
				leave: null
			);
		}

		private bool IsActive => m_importanceLevel > 0f && m_targetPickable != null && !m_targetPickable.ToRemove;

		// =====================================================================
		// MÉTODO PARA ASIGNAR INVENTARIO A LAS CRIATURAS AL SPAWNEAR
		// =====================================================================

		public void ApplyCreatureInventory(Entity entity)
		{
			if (entity == null)
				return;

			string creatureName = entity.ValuesDictionary?.DatabaseObject?.Name;
			if (string.IsNullOrEmpty(creatureName))
				return;

			ComponentMiner miner = entity.FindComponent<ComponentMiner>(true);
			if (miner == null)
				return;

			IInventory inventory = miner.Inventory;
			if (inventory == null)
				return;

			DifficultyMode currentDifficulty = DifficultyMode.Normal;
			var greenNight = base.Project.FindSubsystem<SubsystemGreenNightSky>(true);
			if (greenNight != null)
			{
				currentDifficulty = greenNight.DifficultyMode;
			}
			bool isHardOrHigher = (currentDifficulty >= DifficultyMode.Hard);

			// Función mejorada: verifica que el slot esté vacío o tenga el mismo ítem
			int AddSafe(int value, int count = 1, int startSlot = 0)
			{
				if (value == 0 || Terrain.ExtractContents(value) <= 0 || Terrain.ExtractContents(value) >= 1024)
					return -1;
				for (int i = startSlot; i < inventory.SlotsCount; i++)
				{
					int existingValue = inventory.GetSlotValue(i);
					int existingCount = inventory.GetSlotCount(i);
					int capacity = inventory.GetSlotCapacity(i, value);
					if (existingValue == value && existingCount + count <= capacity)
					{
						inventory.AddSlotItems(i, value, count);
						return i;
					}
					else if (existingValue == 0 && capacity >= count)
					{
						inventory.AddSlotItems(i, value, count);
						return i;
					}
				}
				return -1;
			}

			int GetNormalRanged()
			{
				float r = m_random.Float(0f, 1f);
				if (r < 0.20f) return Terrain.MakeBlockValue(MusketBlock.Index);
				else if (r < 0.40f) return Terrain.MakeBlockValue(BowBlock.Index);
				else if (r < 0.60f) return Terrain.MakeBlockValue(CrossbowBlock.Index);
				else if (r < 0.80f) return Terrain.MakeBlockValue(RepeatCrossbowBlock.Index);
				else return FlameThrowerBlock.SetLoadCount(Terrain.MakeBlockValue(FlameThrowerBlock.Index, 0, FlameThrowerBlock.SetBulletType(FlameThrowerBlock.SetLoadState(0, FlameThrowerBlock.LoadState.Loaded), new FlameBulletBlock.FlameBulletType?(m_random.Bool(0.5f) ? FlameBulletBlock.FlameBulletType.Flame : FlameBulletBlock.FlameBulletType.Poison))), 8);
			}

			int GetInfectedRangedOrFirearm()
			{
				if (m_random.Float(0f, 1f) < 0.01f)
				{
					string[] firearmNames = new string[] { "AKBlock", "SPAS12Block", "SWM500Block", "BK43Block", "M4Block", "AK48Block", "AUGBlock", "P90Block", "SCARBlock", "M249Block", "SniperBlock", "Izh43Block", "KABlock", "G3Block", "NewG3Block", "MendozaBlock", "GrozaBlock", "Master308Block", "AA12Block", "MinigunBlock", "Mac10Block", "UziBlock", "MP5SSDBlock", "FamasBlock", "RevolverBlock" };
					string chosenFirearm = firearmNames[m_random.Int(0, firearmNames.Length - 1)];
					int firearmIndex = BlocksManager.GetBlockIndex(chosenFirearm);
					if (firearmIndex > 0 && firearmIndex < 1024)
						return Terrain.MakeBlockValue(firearmIndex);
				}
				float r = m_random.Float(0f, 1f);
				if (r < 0.20f) return Terrain.MakeBlockValue(MusketBlock.Index);
				else if (r < 0.40f) return Terrain.MakeBlockValue(BowBlock.Index);
				else if (r < 0.60f) return Terrain.MakeBlockValue(CrossbowBlock.Index);
				else if (r < 0.80f) return Terrain.MakeBlockValue(RepeatCrossbowBlock.Index);
				else return FlameThrowerBlock.SetLoadCount(Terrain.MakeBlockValue(FlameThrowerBlock.Index, 0, FlameThrowerBlock.SetBulletType(FlameThrowerBlock.SetLoadState(0, FlameThrowerBlock.LoadState.Loaded), new FlameBulletBlock.FlameBulletType?(m_random.Bool(0.5f) ? FlameBulletBlock.FlameBulletType.Flame : FlameBulletBlock.FlameBulletType.Poison))), 8);
			}

			int GetRandomMelee()
			{
				float weaponTypeChance = m_random.Float(0f, 1f);
				if (weaponTypeChance < 0.25f)
				{
					float r = m_random.Float(0f, 1f);
					if (r < 0.5f) return Terrain.MakeBlockValue(WoodenClubBlock.Index);
					else return Terrain.MakeBlockValue(StoneClubBlock.Index);
				}
				else if (weaponTypeChance < 0.50f)
				{
					float r = m_random.Float(0f, 1f);
					if (r < 0.1667f) return Terrain.MakeBlockValue(WoodMacheteBlock.Index);
					else if (r < 0.3333f) return Terrain.MakeBlockValue(StoneMacheteBlock.Index);
					else if (r < 0.5f) return Terrain.MakeBlockValue(CopperMacheteBlock.Index);
					else if (r < 0.6667f) return Terrain.MakeBlockValue(IronMacheteBlock.Index);
					else if (r < 0.8333f) return Terrain.MakeBlockValue(DiamondMacheteBlock.Index);
					else return Terrain.MakeBlockValue(LavaMacheteBlock.Index);
				}
				else if (weaponTypeChance < 0.75f)
				{
					float r = m_random.Float(0f, 1f);
					if (r < 0.1667f) return Terrain.MakeBlockValue(WoodenSpearBlock.Index);
					else if (r < 0.3333f) return Terrain.MakeBlockValue(StoneSpearBlock.Index);
					else if (r < 0.5f) return Terrain.MakeBlockValue(CopperSpearBlock.Index);
					else if (r < 0.6667f) return Terrain.MakeBlockValue(IronSpearBlock.Index);
					else if (r < 0.8333f) return Terrain.MakeBlockValue(DiamondSpearBlock.Index);
					else return Terrain.MakeBlockValue(LavaSpearBlock.Index);
				}
				else
				{
					float r = m_random.Float(0f, 1f);
					if (r < 0.1667f) return Terrain.MakeBlockValue(WoodAxeBlock.Index);
					else if (r < 0.3333f) return Terrain.MakeBlockValue(StoneAxeOriginalBlock.Index);
					else if (r < 0.5f) return Terrain.MakeBlockValue(CopperAxeBlock.Index);
					else if (r < 0.6667f) return Terrain.MakeBlockValue(IronAxeBlock.Index);
					else if (r < 0.8333f) return Terrain.MakeBlockValue(DiamondAxeBlock.Index);
					else return Terrain.MakeBlockValue(LavaAxeBlock.Index);
				}
			}

			void AddBombsToInventory(int startSlot)
			{
				float bombTypeChance = m_random.Float(0f, 1f);
				int bombValue = 0;
				if (bombTypeChance < 0.3333f) bombValue = Terrain.MakeBlockValue(BombBlock.Index);
				else if (bombTypeChance < 0.6666f) bombValue = Terrain.MakeBlockValue(IncendiaryBombBlock.Index);
				else bombValue = Terrain.MakeBlockValue(PoisonBombBlock.Index);

				if (bombValue != 0)
				{
					int bombCount = isHardOrHigher ? m_random.Int(8, 12) : m_random.Int(4, 8);
					int remainingBombs = bombCount;
					for (int i = startSlot; i < inventory.SlotsCount && remainingBombs > 0; i++)
					{
						int slotValue = inventory.GetSlotValue(i);
						int slotCount = inventory.GetSlotCount(i);
						int capacity = inventory.GetSlotCapacity(i, bombValue);
						if (slotValue == bombValue && slotCount + remainingBombs <= capacity)
						{
							inventory.AddSlotItems(i, bombValue, remainingBombs);
							remainingBombs = 0;
						}
						else if (slotValue == 0 && capacity > 0)
						{
							int add = Math.Min(capacity, remainingBombs);
							inventory.AddSlotItems(i, bombValue, add);
							remainingBombs -= add;
						}
					}
				}
			}

			// =====================================================================
			// LÓGICA DE INVENTARIOS POR CRIATURA
			// =====================================================================

			if (creatureName == "CapitanPirata")
			{
				string spawnCreatureName = m_random.Bool(0.5f) ? "PirataElite" : "PirataNormal";
				EggBlock eggBlock = BlocksManager.Blocks[EggBlock.Index] as EggBlock;
				EggBlock.EggType eggType = eggBlock?.GetEggTypeByCreatureTemplateName(spawnCreatureName) ?? eggBlock?.GetEggType(0);
				int eggData = EggBlock.SetEggType(0, eggType.EggTypeIndex);
				int eggValue = Terrain.MakeBlockValue(EggBlock.Index, 0, eggData);
				int eggCount = 10;
				int eggSlot = -1;
				for (int i = 0; i < inventory.SlotsCount; i++)
				{
					if (inventory.GetSlotCapacity(i, eggValue) >= eggCount) { eggSlot = i; break; }
				}
				if (eggSlot != -1)
				{
					inventory.AddSlotItems(eggSlot, eggValue, eggCount);
				}
				else
				{
					int rem = eggCount;
					for (int i = 0; i < inventory.SlotsCount && rem > 0; i++)
					{
						int cap = inventory.GetSlotCapacity(i, eggValue);
						if (cap > 0 && inventory.GetSlotCount(i) == 0)
						{
							int add = Math.Min(cap, rem);
							inventory.AddSlotItems(i, eggValue, add);
							rem -= add;
						}
					}
				}

				int rangedWeaponValue = m_random.Bool(0.5f) ? Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("FlameThrowerBlock")) : Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("RepeatCrossbowBlock"));
				if (rangedWeaponValue > 0)
				{
					// Si el huevo está en el slot 0, empezamos desde el 1; si no, desde 0
					int startSlot = (eggSlot == 0) ? 1 : 0;
					AddSafe(rangedWeaponValue, 1, startSlot);
				}

				int meleeWeaponValue = GetRandomMelee();
				if (meleeWeaponValue != 0)
				{
					int startSlot = (eggSlot == 0) ? 1 : 0;
					AddSafe(meleeWeaponValue, 1, startSlot + 1);
				}
			}
			else if (creatureName == "PirataHostilComerciante")
			{
				// Similar a CapitanPirata
				string spawnCreatureName = m_random.Bool(0.5f) ? "PirataElite" : "PirataNormal";
				EggBlock eggBlock = BlocksManager.Blocks[EggBlock.Index] as EggBlock;
				EggBlock.EggType eggType = eggBlock?.GetEggTypeByCreatureTemplateName(spawnCreatureName) ?? eggBlock?.GetEggType(0);
				int eggData = EggBlock.SetEggType(0, eggType.EggTypeIndex);
				int eggValue = Terrain.MakeBlockValue(EggBlock.Index, 0, eggData);
				int eggCount = 10;
				int eggSlot = -1;
				for (int i = 0; i < inventory.SlotsCount; i++)
				{
					if (inventory.GetSlotCapacity(i, eggValue) >= eggCount) { eggSlot = i; break; }
				}
				if (eggSlot != -1)
				{
					inventory.AddSlotItems(eggSlot, eggValue, eggCount);
				}
				else
				{
					int rem = eggCount;
					for (int i = 0; i < inventory.SlotsCount && rem > 0; i++)
					{
						int cap = inventory.GetSlotCapacity(i, eggValue);
						if (cap > 0 && inventory.GetSlotCount(i) == 0)
						{
							int add = Math.Min(cap, rem);
							inventory.AddSlotItems(i, eggValue, add);
							rem -= add;
						}
					}
				}

				float r = m_random.Float(0f, 1f);
				int rangedWeaponValue = 0;
				if (r < 0.45f) rangedWeaponValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("FlameThrowerBlock"));
				else if (r < 0.9f) rangedWeaponValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("RepeatCrossbowBlock"));
				else rangedWeaponValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("MusketBlock"));
				if (rangedWeaponValue > 0)
				{
					int startSlot = (eggSlot == 0) ? 1 : 0;
					AddSafe(rangedWeaponValue, 1, startSlot);
				}

				int meleeWeaponValue = GetRandomMelee();
				if (meleeWeaponValue != 0)
				{
					int startSlot = (eggSlot == 0) ? 1 : 0;
					AddSafe(meleeWeaponValue, 1, startSlot + 1);
				}
			}
			else if (creatureName == "PirataElite")
			{
				int weaponValue = GetNormalRanged();
				if (weaponValue > 0) AddSafe(weaponValue);
				int meleeValue = GetRandomMelee();
				if (meleeValue != 0) AddSafe(meleeValue, 1, 1);
				if (m_random.Float(0f, 1f) < 0.10f)
				{
					int bombValue = m_random.Bool(0.5f) ? Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("BombBlock")) : Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("IncendiaryBombBlock"));
					if (bombValue > 0) AddSafe(bombValue, 5, 1);
				}
			}
			else if (creatureName == "PirataNormal")
			{
				int weaponValue = GetNormalRanged();
				if (weaponValue > 0) AddSafe(weaponValue);
				int meleeValue = GetRandomMelee();
				if (meleeValue != 0) AddSafe(meleeValue, 1, 1);
				if (m_random.Float(0f, 1f) < 0.20f)
				{
					int bombValue = m_random.Bool(0.5f) ? Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("BombBlock")) : Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("IncendiaryBombBlock"));
					if (bombValue > 0) AddSafe(bombValue, 5, 1);
				}
			}
			else if (creatureName == "Werewolf")
			{
				float randomChance = m_random.Float(0f, 1f);
				int weaponValue = 0;
				if (randomChance < 0.40f) weaponValue = GetNormalRanged();
				else weaponValue = GetRandomMelee();
				if (weaponValue > 0) AddSafe(weaponValue);
				if (randomChance < 0.20f) AddBombsToInventory(0);
			}
			else if (creatureName == "InfectedNormal1" || creatureName == "InfectedNormal2" || creatureName == "InfectedMuscle1" || creatureName == "InfectedMuscle2" || creatureName == "GhostNormal" || creatureName == "GhostFast" || creatureName == "Boomer1" || creatureName == "Boomer2" || creatureName == "Boomer3" || creatureName == "GhostBoomer1" || creatureName == "GhostBoomer2" || creatureName == "GhostBoomer3" || creatureName == "HumanoidSkeleton")
			{
				int firstSlotValue = 0;
				int secondSlotValue = 0;

				if (!isHardOrHigher)
				{
					if (m_random.Float(0f, 1f) < 0.7f) firstSlotValue = GetRandomMelee();
				}
				else
				{
					float mainChoice = m_random.Float(0f, 1f);
					if (mainChoice < 0.55f)
					{
						firstSlotValue = GetRandomMelee();
						secondSlotValue = GetInfectedRangedOrFirearm();
					}
					else if (mainChoice < 0.90f)
					{
						firstSlotValue = GetInfectedRangedOrFirearm();
						secondSlotValue = GetRandomMelee();
					}
					else
					{
						float throwChoice = m_random.Float(0f, 1f);
						if (throwChoice < 0.50f)
						{
							firstSlotValue = GetRandomMelee();
						}
						else
						{
							float bombTypeChance = m_random.Float(0f, 1f);
							int bombValue = 0;
							if (bombTypeChance < 0.3333f) bombValue = Terrain.MakeBlockValue(BombBlock.Index);
							else if (bombTypeChance < 0.6666f) bombValue = Terrain.MakeBlockValue(IncendiaryBombBlock.Index);
							else bombValue = Terrain.MakeBlockValue(PoisonBombBlock.Index);
							if (bombValue != 0)
							{
								int bombCount = m_random.Int(8, 12);
								int slotCapacity = inventory.GetSlotCapacity(0, bombValue);
								int addCount = Math.Min(bombCount, slotCapacity);
								if (addCount > 0) inventory.AddSlotItems(0, bombValue, addCount);
								firstSlotValue = 0;
							}
						}
						if (firstSlotValue != 0)
						{
							float bombTypeChance2 = m_random.Float(0f, 1f);
							int bombValue = 0;
							if (bombTypeChance2 < 0.3333f) bombValue = Terrain.MakeBlockValue(BombBlock.Index);
							else if (bombTypeChance2 < 0.6666f) bombValue = Terrain.MakeBlockValue(IncendiaryBombBlock.Index);
							else bombValue = Terrain.MakeBlockValue(PoisonBombBlock.Index);
							if (bombValue != 0)
							{
								int bombCount = m_random.Int(8, 12);
								int slotCapacity = inventory.GetSlotCapacity(1, bombValue);
								int addCount = Math.Min(bombCount, slotCapacity);
								if (addCount > 0) inventory.AddSlotItems(1, bombValue, addCount);
							}
						}
					}
				}

				if (firstSlotValue > 0) AddSafe(firstSlotValue);
				if (secondSlotValue > 0) AddSafe(secondSlotValue, 1, 1);
				if (isHardOrHigher && m_random.Float(0f, 1f) < 0.25f) AddBombsToInventory(2);
			}
			else if (creatureName == "InfectedFreezer" || creatureName == "FrozenGhostBoomer" || creatureName == "BoomerFrozen" || creatureName == "FrozenGhost")
			{
				int freezingSnowballIndex = BlocksManager.GetBlockIndex("FreezingSnowballBlock");
				int freezeBombIndex = BlocksManager.GetBlockIndex("FreezeBombBlock");

				float emptyChance = isHardOrHigher ? 0.2f : 0.3333f;
				float mainChoice = m_random.Float(0f, 1f);

				if (mainChoice >= emptyChance)
				{
					bool hasSnowball = isHardOrHigher ? m_random.Float(0f, 1f) < 0.8f : m_random.Float(0f, 1f) < 0.5f;
					bool hasFreezeBomb = isHardOrHigher ? m_random.Float(0f, 1f) < 0.01f : m_random.Float(0f, 1f) < 0.0005f;
					bool hasMeleeWeapon = isHardOrHigher ? m_random.Float(0f, 1f) < 0.8f : m_random.Float(0f, 1f) < 0.5f;
					bool hasRangedWeapon = isHardOrHigher ? m_random.Float(0f, 1f) < 0.15f : false;

					if (hasSnowball && freezingSnowballIndex > 0 && freezingSnowballIndex < 1024)
					{
						int snowballValue = Terrain.MakeBlockValue(freezingSnowballIndex);
						int snowballCount = isHardOrHigher ? Math.Min(m_random.Int(20, 40), inventory.GetSlotCapacity(0, snowballValue)) : Math.Min(m_random.Bool() ? 40 : 5, inventory.GetSlotCapacity(0, snowballValue));
						if (snowballCount > 0) inventory.AddSlotItems(0, snowballValue, snowballCount);
					}

					if (hasFreezeBomb && freezeBombIndex > 0 && freezeBombIndex < 1024)
					{
						int freezeBombValue = Terrain.MakeBlockValue(freezeBombIndex);
						int freezeBombCount = isHardOrHigher ? Math.Min(m_random.Int(10, 20), inventory.GetSlotCapacity(0, freezeBombValue)) : Math.Min(m_random.Bool() ? 40 : 5, inventory.GetSlotCapacity(0, freezeBombValue));
						if (freezeBombCount > 0)
						{
							if (hasSnowball && inventory.GetSlotCount(0) > 0) AddSafe(freezeBombValue, freezeBombCount, 1);
							else inventory.AddSlotItems(0, freezeBombValue, freezeBombCount);
						}
					}

					if (hasRangedWeapon)
					{
						int rangedValue = GetInfectedRangedOrFirearm();
						if (rangedValue > 0) AddSafe(rangedValue, 1, 1);
					}

					if (hasMeleeWeapon)
					{
						int weaponValue = GetRandomMelee();
						if (weaponValue != 0) AddSafe(weaponValue, 1, 1);
					}
				}
			}
		}

		// ---- Copia de inventario para domesticación ----

		public void CopyInventoryFrom(Entity sourceEntity)
		{
			if (sourceEntity == null) return;
			ComponentMiner sourceMiner = sourceEntity.FindComponent<ComponentMiner>();
			if (sourceMiner == null) return;
			IInventory sourceInventory = sourceMiner.Inventory;
			if (sourceInventory == null) return;

			if (m_componentMiner == null) return;
			IInventory targetInventory = m_componentMiner.Inventory;
			if (targetInventory == null) return;

			int slots = Math.Min(sourceInventory.SlotsCount, targetInventory.SlotsCount);
			for (int i = 0; i < slots; i++)
			{
				int value = sourceInventory.GetSlotValue(i);
				int count = sourceInventory.GetSlotCount(i);
				if (value != 0 && count > 0)
				{
					int targetValue = targetInventory.GetSlotValue(i);
					int targetCount = targetInventory.GetSlotCount(i);
					int capacity = targetInventory.GetSlotCapacity(i, value);
					if (targetValue == value && targetCount + count <= capacity)
					{
						targetInventory.AddSlotItems(i, value, count);
					}
					else if (targetValue == 0 && capacity >= count)
					{
						targetInventory.AddSlotItems(i, value, count);
					}
					else
					{
						for (int j = 0; j < targetInventory.SlotsCount; j++)
						{
							if (j == i) continue;
							int slotVal = targetInventory.GetSlotValue(j);
							int slotCnt = targetInventory.GetSlotCount(j);
							int slotCap = targetInventory.GetSlotCapacity(j, value);
							if (slotVal == value && slotCnt + count <= slotCap)
							{
								targetInventory.AddSlotItems(j, value, count);
								break;
							}
							else if (slotVal == 0 && slotCap >= count)
							{
								targetInventory.AddSlotItems(j, value, count);
								break;
							}
						}
					}
				}
			}
		}

		// ---- Limpieza ----

		public override void Dispose()
		{
			base.Dispose();
			if (m_subsystemPickables != null)
			{
				m_subsystemPickables.PickableAdded -= OnPickableAdded;
				m_subsystemPickables.PickableRemoved -= OnPickableRemoved;
			}
		}
	}
}
