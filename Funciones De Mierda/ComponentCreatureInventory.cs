using System;
using System.Globalization;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureInventory : ComponentInventory
	{
		// Evento para notificar cambios en el slot activo
		public event Action ActiveSlotChanged;

		// Token: 0x06002400 RID: 9216 RVA: 0x0011D000 File Offset: 0x0011B200
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar el slot activo específico para criatura
			this.ActiveSlotIndex = valuesDictionary.GetValue<int>("CreatureActiveSlotIndex", 0);
		}

		// Token: 0x06002401 RID: 9217 RVA: 0x0011D024 File Offset: 0x0011B224
		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);

			// Guardar el slot activo específico para criatura
			valuesDictionary.SetValue<int>("CreatureActiveSlotIndex", this.ActiveSlotIndex);
		}

		// Sobrescribir la propiedad ActiveSlotIndex para disparar el evento
		private int m_activeSlotIndex;
		public new int ActiveSlotIndex
		{
			get { return m_activeSlotIndex; }
			set
			{
				if (m_activeSlotIndex != value)
				{
					m_activeSlotIndex = value;
					base.ActiveSlotIndex = value; // Llamar a la implementación base
					ActiveSlotChanged?.Invoke(); // Notificar el cambio
				}
			}
		}
	}
}
