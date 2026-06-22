using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewInventory : ComponentInventory
	{
		// Diccionario principal: NombreDeCriatura -> (NombreDeBloque -> Cantidad)
		public Dictionary<string, Dictionary<string, int>> m_creatureItems = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

		public IReadOnlyDictionary<string, Dictionary<string, int>> CreatureItems
		{
			get { return m_creatureItems; }
		}

		#region Gestión de Objetos por Nombre de Bloque

		public bool AddCreatureItemByName(string creatureName, string blockName, int count)
		{
			if (string.IsNullOrEmpty(creatureName) || string.IsNullOrEmpty(blockName) || count <= 0)
				return false;

			if (BlocksManager.GetBlockIndex(blockName, false) < 0)
				return false;

			if (!m_creatureItems.ContainsKey(creatureName))
			{
				m_creatureItems[creatureName] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			}

			Dictionary<string, int> items = m_creatureItems[creatureName];
			if (!items.ContainsKey(blockName))
			{
				items[blockName] = 0;
			}
			items[blockName] += count;

			return true;
		}

		public bool RemoveCreatureItemByName(string creatureName, string blockName, int count)
		{
			if (string.IsNullOrEmpty(creatureName) || string.IsNullOrEmpty(blockName) || count <= 0)
				return false;

			Dictionary<string, int> items;
			if (!m_creatureItems.TryGetValue(creatureName, out items))
				return false;

			int currentCount;
			if (!items.TryGetValue(blockName, out currentCount))
				return false;

			if (currentCount < count)
				return false;

			items[blockName] -= count;
			if (items[blockName] <= 0)
			{
				items.Remove(blockName);
				if (items.Count == 0)
				{
					m_creatureItems.Remove(creatureName);
				}
			}

			return true;
		}

		public int GetCreatureItemCountByName(string creatureName, string blockName)
		{
			if (string.IsNullOrEmpty(creatureName) || string.IsNullOrEmpty(blockName))
				return 0;

			Dictionary<string, int> items;
			if (!m_creatureItems.TryGetValue(creatureName, out items))
				return 0;

			int count;
			if (!items.TryGetValue(blockName, out count))
				return 0;

			return count;
		}

		public Dictionary<string, int> GetCreatureItemsByName(string creatureName)
		{
			Dictionary<string, int> items;
			if (m_creatureItems.TryGetValue(creatureName, out items))
			{
				return new Dictionary<string, int>(items, StringComparer.OrdinalIgnoreCase);
			}
			return null;
		}

		#endregion

		#region Gestión de Objetos por Índice (Compatibilidad Original)

		public bool AddCreatureItemByIndex(string creatureName, int blockIndex, int count)
		{
			string blockName = GetBlockNameByIndex(blockIndex);
			if (string.IsNullOrEmpty(blockName))
				return false;

			return AddCreatureItemByName(creatureName, blockName, count);
		}

		public bool RemoveCreatureItemByIndex(string creatureName, int blockIndex, int count)
		{
			string blockName = GetBlockNameByIndex(blockIndex);
			if (string.IsNullOrEmpty(blockName))
				return false;

			return RemoveCreatureItemByName(creatureName, blockName, count);
		}

		public int GetCreatureItemCountByIndex(string creatureName, int blockIndex)
		{
			string blockName = GetBlockNameByIndex(blockIndex);
			if (string.IsNullOrEmpty(blockName))
				return 0;

			return GetCreatureItemCountByName(creatureName, blockName);
		}

		public Dictionary<int, int> GetCreatureItemsByIndex(string creatureName)
		{
			Dictionary<string, int> itemsByName = GetCreatureItemsByName(creatureName);
			if (itemsByName == null)
				return null;

			var result = new Dictionary<int, int>();
			foreach (var kvp in itemsByName)
			{
				int blockIndex = BlocksManager.GetBlockIndex(kvp.Key, false);
				if (blockIndex >= 0)
				{
					result[blockIndex] = kvp.Value;
				}
			}
			return result.Count > 0 ? result : null;
		}

		#endregion

		#region Aplicar y Limpiar

		public int ApplyCreatureItemsToInventory(string creatureName)
		{
			Dictionary<int, int> itemsByIndex = GetCreatureItemsByIndex(creatureName);
			if (itemsByIndex == null)
				return 0;

			int addedCount = 0;
			foreach (var kvp in itemsByIndex)
			{
				int blockValue = Terrain.MakeBlockValue(kvp.Key, 0, 0);
				int remaining = ComponentInventoryBase.AcquireItems(this, blockValue, kvp.Value);
				addedCount += kvp.Value - remaining;
			}

			return addedCount;
		}

		public void ApplyAllCreatureItemsToInventory()
		{
			foreach (var creatureKvp in m_creatureItems.ToList())
			{
				ApplyCreatureItemsToInventory(creatureKvp.Key);
			}
		}

		public bool ClearCreatureItems(string creatureName)
		{
			return m_creatureItems.Remove(creatureName);
		}

		public void ClearAllCreatureItems()
		{
			m_creatureItems.Clear();
		}

		public bool HasCreatureItems(string creatureName)
		{
			Dictionary<string, int> items;
			if (m_creatureItems.TryGetValue(creatureName, out items))
			{
				return items.Count > 0;
			}
			return false;
		}

		public void SetCreatureItems(string creatureName, Dictionary<string, int> items)
		{
			if (string.IsNullOrEmpty(creatureName) || items == null)
				return;

			m_creatureItems.Remove(creatureName);

			var validItems = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (var kvp in items)
			{
				if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value > 0)
				{
					if (BlocksManager.GetBlockIndex(kvp.Key, false) >= 0)
					{
						validItems[kvp.Key] = kvp.Value;
					}
				}
			}

			if (validItems.Count > 0)
			{
				m_creatureItems[creatureName] = validItems;
			}
		}

		#endregion

		#region Métodos Auxiliares

		private static string GetBlockNameByIndex(int blockIndex)
		{
			if (blockIndex >= 0 && blockIndex < BlocksManager.Blocks.Length)
			{
				Block block = BlocksManager.Blocks[blockIndex];
				if (block != null && !(block is AirBlock))
				{
					return block.GetType().Name;
				}
			}
			return string.Empty;
		}

		#endregion

		#region Serialización (Estilo original puro)

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_creatureItems.Clear();

			ValuesDictionary creaturesDict = valuesDictionary.GetValue<ValuesDictionary>("CreatureItems", null);
			if (creaturesDict != null)
			{
				foreach (KeyValuePair<string, object> kvp in creaturesDict)
				{
					string creatureName = kvp.Key;
					ValuesDictionary itemsDict = kvp.Value as ValuesDictionary;

					if (itemsDict != null && itemsDict.Count > 0)
					{
						var items = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
						foreach (KeyValuePair<string, object> itemKvp in itemsDict)
						{
							int count = Convert.ToInt32(itemKvp.Value, CultureInfo.InvariantCulture);
							if (count > 0)
							{
								items[itemKvp.Key] = count;
							}
						}

						if (items.Count > 0)
						{
							m_creatureItems[creatureName] = items;
						}
					}
				}
			}

			// CORRECCIÓN DEL BUG DE DUPLICACIÓN:
			// Solo inyectamos los items si el inventario está completamente vacío.
			// Si ya tiene items, significa que vienen de un archivo de guardado (base.Load los puso ahí)
			// y NO debemos volver a inyectarlos para evitar que se dupliquen.
			bool hasLoadedItems = false;
			for (int i = 0; i < m_slots.Count; i++)
			{
				if (m_slots[i].Count > 0)
				{
					hasLoadedItems = true;
					break;
				}
			}

			if (!hasLoadedItems)
			{
				ApplyAllCreatureItemsToInventory();
			}
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);

			if (m_creatureItems.Count > 0)
			{
				ValuesDictionary creaturesDict = new ValuesDictionary();
				valuesDictionary.SetValue<ValuesDictionary>("CreatureItems", creaturesDict);

				foreach (var creatureKvp in m_creatureItems)
				{
					if (creatureKvp.Value != null && creatureKvp.Value.Count > 0)
					{
						ValuesDictionary itemsDict = new ValuesDictionary();
						creaturesDict.SetValue<ValuesDictionary>(creatureKvp.Key, itemsDict);

						foreach (var itemKvp in creatureKvp.Value)
						{
							if (itemKvp.Value > 0)
							{
								itemsDict.SetValue<int>(itemKvp.Key, itemKvp.Value);
							}
						}
					}
				}
			}
		}

		#endregion
	}
}
