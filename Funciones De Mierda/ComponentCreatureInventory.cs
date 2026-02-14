using System;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureInventory : ComponentInventory, IUpdateable
	{
		public event Action InventoryChanged;
		public event Action ActiveSlotChanged;

		private int m_activeSlotIndex;
		public new int ActiveSlotIndex
		{
			get { return m_activeSlotIndex; }
			set
			{
				if (m_activeSlotIndex != value)
				{
					m_activeSlotIndex = value;
					base.ActiveSlotIndex = value;
					ActiveSlotChanged?.Invoke();
				}
			}
		}

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.ActiveSlotIndex = valuesDictionary.GetValue<int>("CreatureActiveSlotIndex", 0);
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			valuesDictionary.SetValue<int>("CreatureActiveSlotIndex", this.ActiveSlotIndex);
		}

		protected virtual void OnInventoryChanged()
		{
			InventoryChanged?.Invoke();
		}

		public override void AddSlotItems(int slotIndex, int value, int count)
		{
			base.AddSlotItems(slotIndex, value, count);
			OnInventoryChanged();
		}

		public override int RemoveSlotItems(int slotIndex, int count)
		{
			int removed = base.RemoveSlotItems(slotIndex, count);
			if (removed > 0)
				OnInventoryChanged();
			return removed;
		}

		public void Update(float dt)
		{
			// Necesario para IUpdateable, aunque no hagamos nada aquí
		}
	}
}
