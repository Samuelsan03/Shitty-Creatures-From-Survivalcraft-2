using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Componente que hace que la criatura recoja objetos cercanos directamente
	/// al inventario, sin necesidad de moverse hacia ellos.
	/// Filtra por categoría de bloque (terrain, plants, items, etc.).
	/// </summary>
	public class ComponentCreatureCollect : Component, IUpdateable
	{
		// Subsystems cacheados
		public SubsystemPickables m_subsystemPickables;
		public SubsystemAudio m_subsystemAudio;

		// Componentes de la criatura
		public ComponentBody m_componentBody;
		public ComponentInventoryBase m_inventory;

		// Diccionario 1: si está en false, no hace nada
		public bool CanCollect = true;

		// Diccionario 2: categorías que recoge (separadas por coma en el template)
		public readonly List<string> CollectableItems = new List<string>();

		// Rango opcional (por defecto 3 bloques)
		public float CollectRange = 1.75f;

		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			m_inventory = base.Entity.FindComponent<ComponentInventoryBase>();

			// ---- Diccionario 1: CanCollect ----
			CanCollect = valuesDictionary.GetValue<bool>("CanCollect", false);

			// ---- Diccionario 2: CollectableItems ----
			// Plantilla: "Terrain, Plants, Items, Food"
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
			// Si está desactivado, no hace absolutamente nada
			if (!CanCollect) return;
			if (m_inventory == null) return;
			if (m_componentBody == null) return;
			if (CollectableItems.Count == 0) return;

			Vector3 miPos = m_componentBody.Position;
			float rangoSq = CollectRange * CollectRange;

			// Recorremos la lista actual de pickables
			var pickables = m_subsystemPickables.Pickables;
			foreach (Pickable p in pickables)
			{
				if (p == null || p.ToRemove) continue;

				// Solo si está dentro del rango (sin necesidad de ir a por él)
				if ((p.Position - miPos).LengthSquared() > rangoSq) continue;

				int contents = Terrain.ExtractContents(p.Value);
				if (contents == 0) continue;

				// Comprobar categoría del bloque del pickable
				Block block = BlocksManager.Blocks[contents];
				string categoria;
				try
				{
					categoria = block.GetCategory(p.Value);
				}
				catch
				{
					continue;
				}

				if (!CollectableItems.Contains(categoria)) continue;

				// Intentamos meterlo en el inventario
				int restante = ComponentInventoryBase.AcquireItems(m_inventory, p.Value, p.Count);
				int adquirido = p.Count - restante;
				if (adquirido <= 0) continue;

				// Sonido para saber que recogió algo
				m_subsystemAudio.PlaySound(
					"Audio/PickableCollected",
					1f,                                     // volumen
					0f,                                     // pitch
					p.Position,                             // posición
					2f,                                     // distancia mínima
					false);                                 // autoDelay

				// Si el inventario llenó todo, marcamos el pickable para eliminar
				if (restante <= 0)
					p.ToRemove = true;
				else
					p.Count = restante; // sobró parte, dejamos el resto en el suelo
			}
		}
	}
}
