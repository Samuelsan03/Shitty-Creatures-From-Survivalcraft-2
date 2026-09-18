using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureCollect : Component, IUpdateable
	{
		public SubsystemPickables m_subsystemPickables;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemGameInfo m_subsystemGameInfo;

		public ComponentBody m_componentBody;
		public ComponentInventoryBase m_inventory;

		public bool CanCollect = true;
		public readonly List<string> CollectableItems = new List<string>();
		public float CollectRange = 1.75f;

		private Random m_random = new Random();
		private bool m_initialItemsGiven = false;

		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			m_inventory = base.Entity.FindComponent<ComponentInventoryBase>();

			CanCollect = valuesDictionary.GetValue<bool>("CanCollect", false);

			CollectableItems.Clear();
			string rawItems = valuesDictionary.GetValue<string>("CollectableItems", "");
			if (!string.IsNullOrEmpty(rawItems))
			{
				string[] partes = rawItems.Split(',');
				for (int i = 0; i < partes.Length; i++)
				{
					string categoria = partes[i].Trim();
					if (!string.IsNullOrEmpty(categoria))
					{
						CollectableItems.Add(categoria);
					}
				}
			}
		}

		public virtual void Update(float dt)
		{
			// Objetos iniciales: solo una vez
			if (!m_initialItemsGiven)
			{
				m_initialItemsGiven = true;
				GiveInitialItems();
			}

			// Recogida automática
			if (!CanCollect) return;
			if (m_inventory == null) return;
			if (m_componentBody == null) return;
			if (CollectableItems.Count == 0) return;

			Vector3 miPos = m_componentBody.Position;
			float rangoSq = CollectRange * CollectRange;

			var pickables = m_subsystemPickables.Pickables;
			foreach (Pickable p in pickables)
			{
				if (p == null || p.ToRemove) continue;
				if ((p.Position - miPos).LengthSquared() > rangoSq) continue;

				int contents = Terrain.ExtractContents(p.Value);
				if (contents == 0) continue;

				Block block = BlocksManager.Blocks[contents];
				string categoria;
				try { categoria = block.GetCategory(p.Value); }
				catch { continue; }

				if (!CollectableItems.Contains(categoria)) continue;

				int restante = ComponentInventoryBase.AcquireItems(m_inventory, p.Value, p.Count);
				int adquirido = p.Count - restante;
				if (adquirido <= 0) continue;

				m_subsystemAudio.PlaySound("Audio/PickableCollected", 1f, 0f, p.Position, 2f, false);

				if (restante <= 0) p.ToRemove = true;
				else p.Count = restante;
			}
		}

		// =========================================================
		//  OBJETOS INICIALES AL APARECER
		// =========================================================
		public virtual void GiveInitialItems()
		{
			if (m_inventory == null || m_inventory.SlotsCount == 0) return;

			// Si ya tiene algo (reload) → no duplicar
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				if (m_inventory.GetSlotCount(i) > 0) return;
			}

			SubsystemGreenNightSky greenNight = base.Project.FindSubsystem<SubsystemGreenNightSky>();
			bool isHard = greenNight != null && greenNight.DifficultyMode >= DifficultyMode.Hard;
			string typeName = base.Entity.ValuesDictionary.DatabaseObject.Name;

			int s0Val = 0, s0Cnt = 0;
			int s1Val = 0, s1Cnt = 0;

			// ---- GRUPO 1: lógica estándar ----
			if (typeName == "InfectedNormal1" || typeName == "InfectedNormal2"
				|| typeName == "InfectedMuscle1" || typeName == "InfectedMuscle2"
				|| typeName == "GhostNormal" || typeName == "GhostFast"
				|| typeName == "Boomer1" || typeName == "Boomer2" || typeName == "Boomer3"
				|| typeName == "GhostBoomer1" || typeName == "GhostBoomer2" || typeName == "GhostBoomer3"
				|| typeName == "HumanoidSkeleton")
			{
				string[] melees = { "WoodenClubBlock", "StoneClubBlock", "WoodenMacheteBlock",
					"StoneMacheteBlock", "IronMacheteBlock", "DiamondMacheteBlock",
					"WoodenAxeBlock", "StoneAxeBlock", "IronAxeBlock", "DiamondAxeBlock",
					"WoodenSpearBlock", "StoneSpearBlock", "IronSpearBlock", "DiamondSpearBlock" };
				string[] ranged = { "BowBlock", "CrossbowBlock", "MusketBlock", "FlameThrowerBlock", "RepeatCrossbowBlock" };
				string[] bombs = { "BombBlock", "IncendiaryBombBlock", "PoisonBombBlock" };

				if (!isHard)
				{
					if (m_random.Bool(0.70f))
					{
						s0Val = BlocksManager.GetBlockIndex(melees[m_random.Int(0, melees.Length - 1)], false);
						s0Cnt = 1;
					}
				}
				else
				{
					float roll = m_random.Float(0f, 1f);
					if (roll < 0.01f)
					{
						s0Val = BlocksManager.GetBlockIndex(ranged[m_random.Int(0, ranged.Length - 1)], false);
						s0Cnt = 1;
						s1Val = BlocksManager.GetBlockIndex(melees[m_random.Int(0, melees.Length - 1)], false);
						s1Cnt = 1;
					}
					else if (roll < 0.51f)
					{
						s0Val = BlocksManager.GetBlockIndex(melees[m_random.Int(0, melees.Length - 1)], false);
						s0Cnt = 1;
					}
					else if (roll < 0.71f)
					{
						if (m_random.Bool(0.50f))
						{
							s0Val = BlocksManager.GetBlockIndex(melees[m_random.Int(0, melees.Length - 1)], false);
							s0Cnt = 1;
							s1Val = BlocksManager.GetBlockIndex(bombs[m_random.Int(0, bombs.Length - 1)], false);
							s1Cnt = m_random.Int(8, 12);
						}
						else
						{
							s0Val = BlocksManager.GetBlockIndex(bombs[m_random.Int(0, bombs.Length - 1)], false);
							s0Cnt = m_random.Int(8, 12);
						}
					}
				}

				// Slot 0
				if (s0Val > 0 && s0Cnt > 0)
				{
					int cap = m_inventory.GetSlotCapacity(0, s0Val);
					int add = MathUtils.Min(cap, s0Cnt);
					if (add > 0) m_inventory.AddSlotItems(0, s0Val, add);
				}

				// Desde slot 1
				if (s1Val > 0 && s1Cnt > 0)
				{
					int restante = s1Cnt;
					for (int i = 1; i < m_inventory.SlotsCount && restante > 0; i++)
					{
						int existente = m_inventory.GetSlotCount(i);
						int valorExistente = m_inventory.GetSlotValue(i);
						if (existente > 0 && valorExistente != s1Val) continue;
						int cap = m_inventory.GetSlotCapacity(i, s1Val);
						int add = MathUtils.Min(cap - existente, restante);
						if (add > 0) { m_inventory.AddSlotItems(i, s1Val, add); restante -= add; }
					}
				}

				// 25% extra en Hard+: bombas desde slot 2
				if (isHard && m_random.Bool(0.25f))
				{
					int bVal = BlocksManager.GetBlockIndex(bombs[m_random.Int(0, bombs.Length - 1)], false);
					int bCnt = m_random.Int(8, 12);
					if (bVal > 0)
					{
						for (int i = 2; i < m_inventory.SlotsCount && bCnt > 0; i++)
						{
							int existente = m_inventory.GetSlotCount(i);
							int valorExistente = m_inventory.GetSlotValue(i);
							if (existente > 0 && valorExistente != bVal) continue;
							int cap = m_inventory.GetSlotCapacity(i, bVal);
							int add = MathUtils.Min(cap - existente, bCnt);
							if (add > 0) { m_inventory.AddSlotItems(i, bVal, add); bCnt -= add; }
						}
					}
				}
			}
			// ---- GRUPO 2: congelados ----
			else if (typeName == "InfectedFreezer" || typeName == "FrozenGhostBoomer"
				  || typeName == "BoomerFrozen" || typeName == "FrozenGhost")
			{
				float emptyChance = isHard ? 0.20f : 0.3333f;
				if (m_random.Bool(emptyChance)) return;

				bool hasSnowball = false;

				// FreezingSnowballBlock
				float snowballChance = isHard ? 0.80f : 0.50f;
				if (m_random.Bool(snowballChance))
				{
					int cnt = isHard ? m_random.Int(20, 40) : (m_random.Bool() ? 5 : 40);
					int val = BlocksManager.GetBlockIndex("FreezingSnowballBlock", false);
					if (val > 0)
					{
						int cap = m_inventory.GetSlotCapacity(0, val);
						int add = MathUtils.Min(cap, cnt);
						if (add > 0) { m_inventory.AddSlotItems(0, val, add); hasSnowball = true; }
					}
				}

				// FreezeBombBlock
				float bombChance = isHard ? 0.01f : 0.0005f;
				if (m_random.Bool(bombChance))
				{
					int cnt = isHard ? m_random.Int(10, 20) : (m_random.Bool() ? 5 : 40);
					int val = BlocksManager.GetBlockIndex("FreezeBombBlock", false);
					if (val > 0)
					{
						int start = hasSnowball ? 1 : 0;
						for (int i = start; i < m_inventory.SlotsCount && cnt > 0; i++)
						{
							int existente = m_inventory.GetSlotCount(i);
							int valorExistente = m_inventory.GetSlotValue(i);
							if (existente > 0 && valorExistente != val) continue;
							int cap = m_inventory.GetSlotCapacity(i, val);
							int add = MathUtils.Min(cap - existente, cnt);
							if (add > 0) { m_inventory.AddSlotItems(i, val, add); cnt -= add; }
						}
					}
				}

				// Arma a distancia
				float rangedChance = isHard ? 0.15f : 0f;
				if (rangedChance > 0f && m_random.Bool(rangedChance))
				{
					string[] rangedNames = { "BowBlock", "CrossbowBlock", "MusketBlock" };
					int val = BlocksManager.GetBlockIndex(rangedNames[m_random.Int(0, rangedNames.Length - 1)], false);
					if (val > 0)
					{
						for (int i = 1; i < m_inventory.SlotsCount; i++)
						{
							int existente = m_inventory.GetSlotCount(i);
							int valorExistente = m_inventory.GetSlotValue(i);
							if (existente > 0 && valorExistente != val) continue;
							int cap = m_inventory.GetSlotCapacity(i, val);
							if (cap - existente >= 1) { m_inventory.AddSlotItems(i, val, 1); break; }
						}
					}
				}

				// Arma cuerpo a cuerpo
				float meleeChance = isHard ? 0.80f : 0.50f;
				if (m_random.Bool(meleeChance))
				{
					string[] meleeNames = { "WoodenClubBlock", "StoneClubBlock", "WoodenMacheteBlock",
						"StoneMacheteBlock", "IronMacheteBlock", "DiamondMacheteBlock",
						"WoodenAxeBlock", "StoneAxeBlock", "IronAxeBlock", "DiamondAxeBlock",
						"WoodenSpearBlock", "StoneSpearBlock", "IronSpearBlock", "DiamondSpearBlock" };
					int val = BlocksManager.GetBlockIndex(meleeNames[m_random.Int(0, meleeNames.Length - 1)], false);
					if (val > 0)
					{
						for (int i = 0; i < m_inventory.SlotsCount; i++)
						{
							int existente = m_inventory.GetSlotCount(i);
							int valorExistente = m_inventory.GetSlotValue(i);
							if (existente > 0 && valorExistente != val) continue;
							int cap = m_inventory.GetSlotCapacity(i, val);
							if (cap - existente >= 1) { m_inventory.AddSlotItems(i, val, 1); break; }
						}
					}
				}
			}
		}
	}
}
