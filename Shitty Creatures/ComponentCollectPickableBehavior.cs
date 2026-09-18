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
		private SubsystemAudio m_subsystemAudio;
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
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);

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

		/// <summary>
		/// Aplica el inventario inicial a la criatura según su plantilla.
		/// Todo por nombre de bloque (string) usando BlocksManager.
		/// Los huevos se añaden primero para que no se mezclen con las armas.
		/// </summary>
		public void ApplyStartingInventory()
		{
			if (m_componentMiner == null || m_componentMiner.Inventory == null) return;

			string templateName = base.Entity.ValuesDictionary.DatabaseObject.Name;
			if (DatabaseManager.FindEntityValuesDictionary(templateName, false) == null) return;

			IInventory inventory = m_componentMiner.Inventory;

			// ---- Bloque de huevo: uno solo, el tipo va en el data ----
			int eggBlockIndex = BlocksManager.GetBlockIndex("EggBlock", false);
			EggBlock eggBlock = (eggBlockIndex >= 0) ? BlocksManager.Blocks[eggBlockIndex] as EggBlock : null;

			// Devuelve el value de un huevo por NOMBRE DE PLANTILLA de la criatura
			int Egg(string creatureTemplate)
			{
				if (eggBlock == null) return 0;
				EggBlock.EggType eggType = eggBlock.GetEggTypeByCreatureTemplateName(creatureTemplate);
				if (eggType == null) return 0;
				int data = EggBlock.SetEggType(0, eggType.EggTypeIndex);
				return Terrain.MakeBlockValue(eggBlockIndex, 0, data);
			}

			// Devuelve el index de un bloque por nombre (armas, bombas, etc.)
			int Item(string blockName) => BlocksManager.GetBlockIndex(blockName, false);

			// Añade al inventario resolviendo slot
			void Give(int value, int count)
			{
				if (value == 0 || count <= 0) return;
				int slot = FindSlotForItem(inventory, value, count);
				if (slot >= 0) inventory.AddSlotItems(slot, value, count);
			}

			string[] melee =
			{
		"IronMacheteBlock", "IronAxeBlock", "IronSpearBlock",
		"CopperMacheteBlock", "CopperAxeBlock", "CopperSpearBlock",
		"DiamondMacheteBlock", "DiamondAxeBlock", "DiamondSpearBlock"
	};

			string[] eliteRanged = { "MusketBlock", "BowBlock", "CrossbowBlock", "RepeatCrossbowBlock", "FlameThrowerBlock" };

			switch (templateName)
			{
				case "CapitanPirata":
					{
						// 10 huevos: 50% PirataElite / 50% PirataNormal
						Give(Egg(m_random.Bool(0.5f) ? "PirataElite" : "PirataNormal"), 10);

						// 50% lanzallamas / 50% ballesta repetidora
						Give(Item(m_random.Bool(0.5f) ? "FlameThrowerBlock" : "RepeatCrossbowBlock"), 1);

						// Arma cuerpo a cuerpo aleatoria
						Give(Item(melee[m_random.Int(0, melee.Length - 1)]), 1);
						break;
					}

				case "PirataHostilComerciante":
					{
						// 10 huevos de pirata
						Give(Egg("PirataNormal"), 10);

						// 45% lanzallamas / 45% ballesta rep. / 10% mosquete
						float r = m_random.Float();
						string ranged = r < 0.45f ? "FlameThrowerBlock" : (r < 0.90f ? "RepeatCrossbowBlock" : "MusketBlock");
						Give(Item(ranged), 1);

						// Arma cuerpo a cuerpo aleatoria
						Give(Item(melee[m_random.Int(0, melee.Length - 1)]), 1);
						break;
					}

				case "PirataElite":
					{
						Give(Item(eliteRanged[m_random.Int(0, eliteRanged.Length - 1)]), 1);
						Give(Item(melee[m_random.Int(0, melee.Length - 1)]), 1);

						if (m_random.Bool(0.10f))
						{
							Give(Item(m_random.Bool(0.5f) ? "BombBlock" : "IncendiaryBombBlock"), 5);
						}
						break;
					}

				case "PirataNormal":
					{
						Give(Item(eliteRanged[m_random.Int(0, eliteRanged.Length - 1)]), 1);
						Give(Item(melee[m_random.Int(0, melee.Length - 1)]), 1);

						if (m_random.Bool(0.20f))
						{
							Give(Item(m_random.Bool(0.5f) ? "BombBlock" : "IncendiaryBombBlock"), 5);
						}
						break;
					}

				case "Werewolf":
					{
						// 40% a distancia / 60% cuerpo a cuerpo
						if (m_random.Bool(0.40f))
							Give(Item(eliteRanged[m_random.Int(0, eliteRanged.Length - 1)]), 1);
						else
							Give(Item(melee[m_random.Int(0, melee.Length - 1)]), 1);

						// 20% independiente de bombas
						if (m_random.Bool(0.20f))
						{
							Give(Item(m_random.Bool(0.5f) ? "BombBlock" : "IncendiaryBombBlock"), 5);
						}
						break;
					}
			}
		}

		public void Update(float dt)
		{
			// ===== Aplicar inventario en el primer update cuando esté disponible =====
			if (!m_inventoryApplied)
			{
				if (m_componentMiner != null && m_componentMiner.Inventory != null)
				{
					ApplyStartingInventory();   // <-- ESTO ES LO QUE FALTABA
					m_inventoryApplied = true;
				}
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

			// --- NUEVA VERIFICACIÓN: espacio en inventario ---
			if (!HasSpaceForPickable(pickable))
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

				// Reproducir sonido de recolección (igual que ComponentPickableGathererPlayer)
				m_subsystemAudio.PlaySound("Audio/PickableCollected", 3.0f, -0.4f, m_componentCreature.ComponentBody.Position, 2f, false);

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

		// Distancia horizontal ignorando la diferencia de altura exacta de los ojos
		float distSq = Vector3.DistanceSquared(
			new Vector3(eyePos.X, m_componentCreature.ComponentBody.Position.Y, eyePos.Z),
			targetPos
		);

		// === NUEVA LÓGICA DE MIRA (FIELD OF VIEW) ===
		Vector3 forward = Vector3.Transform(-Vector3.UnitZ, m_componentCreature.ComponentCreatureModel.EyeRotation);
		Vector3 toTarget = targetPos - eyePos;
		float distanceToTarget = MathF.Sqrt(distSq > 0f ? distSq : 0f);

		if (distanceToTarget > 0.001f)
		{
			toTarget /= distanceToTarget; // Normalizar
		}
		else
		{
			toTarget = forward; // Si está exactamente encima, asumir que mira hacia adelante
		}

		float dot = Vector3.Dot(forward, toTarget);
		float angle = MathF.Acos(MathUtils.Clamp(dot, -1f, 1f));
		float maxVisionAngle = MathUtils.DegToRad(60f); // 60 grados de visión

		// Si el objeto NO está en su mira, volver a moverse para girar
		if (angle > maxVisionAngle)
		{
			m_stateMachine.TransitionTo("Move");
			return;
		}
		// === FIN LÓGICA DE MIRA ===

		// Si está en la mira Y lo suficientemente cerca, recolectar
		if (distSq < 0.64f) // ~0.8 bloques de distancia
		{
			// --- VERIFICACIÓN ADICIONAL DE ESPACIO ---
			if (!HasSpaceForPickable(m_targetPickable))
			{
				m_interestingPickables.Remove(m_targetPickable);
				m_targetPickable = null;
				m_importanceLevel = 0f;
				m_stateMachine.TransitionTo("Inactive");
				return;
			}

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
			// Está en la mira pero demasiado lejos, dar un pasito más
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

		private bool HasSpaceForPickable(Pickable pickable)
		{
			if (pickable == null || pickable.ToRemove) return false;
			if (m_componentMiner == null || m_componentMiner.Inventory == null) return false;

			IInventory inventory = m_componentMiner.Inventory;
			int value = pickable.Value;
			int count = pickable.Count;

			return FindSlotForItem(inventory, value, count) >= 0;
		}

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
