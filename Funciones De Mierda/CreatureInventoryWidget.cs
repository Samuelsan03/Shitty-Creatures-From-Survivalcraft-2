using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;

namespace Game
{
	public class CreatureInventoryWidget : CanvasWidget
	{
		private IInventory m_creatureInventory;
		private List<InventorySlotWidget> m_creatureSlots = new List<InventorySlotWidget>();
		private GridPanelWidget m_creatureGrid;
		private LabelWidget m_creatureInventoryLabel;
		private LabelWidget m_inventoryLabel;
		private Entity m_creatureEntity;
		private ComponentCreature m_creatureComponent;

		public CreatureInventoryWidget(IInventory creatureInventory, IInventory playerInventory, Entity creatureEntity)
		{
			m_creatureInventory = creatureInventory;
			m_creatureEntity = creatureEntity;

			// Obtener el componente de criatura para acceder al DisplayName
			m_creatureComponent = creatureEntity.FindComponent<ComponentCreature>();

			// Cargar XML
			XElement node = ContentManager.Get<XElement>("Widgets/CreatureInventoryWidget");
			this.LoadContents(this, node);

			// Encontrar widgets
			m_creatureGrid = this.Children.Find<GridPanelWidget>("CreatureGrid", true);
			GridPanelWidget inventoryGrid = this.Children.Find<GridPanelWidget>("InventoryGrid", true);
			m_creatureInventoryLabel = this.Children.Find<LabelWidget>("CreatureInventoryLabel", true);
			m_inventoryLabel = this.Children.Find<LabelWidget>("InventoryLabel", true);

			// Actualizar los labels con los textos correctos
			UpdateLabels();

			// Grid criatura (16 slots)
			for (int i = 0; i < 16; i++)
			{
				InventorySlotWidget slot = new InventorySlotWidget();
				slot.AssignInventorySlot(creatureInventory, i);
				m_creatureSlots.Add(slot);
				m_creatureGrid.Children.Add(slot);
				m_creatureGrid.SetWidgetCell(slot, new Point2(i % 4, i / 4));
			}

			// Grid jugador (slots 10-25)
			for (int i = 0; i < 16; i++)
			{
				InventorySlotWidget slot = new InventorySlotWidget();
				slot.AssignInventorySlot(playerInventory, 10 + i);
				inventoryGrid.Children.Add(slot);
				inventoryGrid.SetWidgetCell(slot, new Point2(i % 4, i / 4));
			}

			// Suscribirse al evento de cambio de slot activo
			ComponentCreatureInventory creatureInv = creatureInventory as ComponentCreatureInventory;
			if (creatureInv != null)
			{
				creatureInv.ActiveSlotChanged += OnCreatureActiveSlotChanged;
			}
		}

		private void UpdateLabels()
		{
			// Actualizar el label del inventario de la criatura
			if (m_creatureInventoryLabel != null)
			{
				// Obtener el nombre de la entidad
				string creatureName = GetEntityName();
				string formatText = LanguageControl.GetContentWidgets("CreatureInventoryWidget", "CreatureInventoryLabel");

				// Si no se encontró el texto de formato, usar uno por defecto
				if (formatText.StartsWith("ContentWidgets:"))
				{
					formatText = "Inventory of {0}"; // Formato por defecto en inglés
				}

				m_creatureInventoryLabel.Text = string.Format(formatText, creatureName);
			}

			// Actualizar el label del inventario del jugador
			if (m_inventoryLabel != null)
			{
				string inventoryText = LanguageControl.GetContentWidgets("CreatureInventoryWidget", "InventoryLabel");

				// Si no se encontró el texto, usar uno por defecto
				if (inventoryText.StartsWith("ContentWidgets:"))
				{
					inventoryText = "Inventory"; // Texto por defecto en inglés
				}

				m_inventoryLabel.Text = inventoryText;
			}
		}

		private string GetEntityName()
		{
			// 1. Usar DisplayName del ComponentCreature (PRIMERA OPCIÓN)
			if (m_creatureComponent != null && !string.IsNullOrEmpty(m_creatureComponent.DisplayName))
				return m_creatureComponent.DisplayName;

			// 2. Buscar ComponentName como respaldo
			var componentName = m_creatureEntity.FindComponent<ComponentName>();
			if (componentName != null && !string.IsNullOrEmpty(componentName.Name))
				return componentName.Name;

			// 3. Si todo falla, nombre genérico
			return "Creature";
		}

		private void OnCreatureActiveSlotChanged()
		{
			foreach (var slot in m_creatureSlots)
			{
				slot.Update();
			}
		}

		public override void Update()
		{
			base.Update();
			foreach (var slot in m_creatureSlots)
			{
				slot.Update();
			}
		}

		public override void Dispose()
		{
			ComponentCreatureInventory creatureInv = m_creatureInventory as ComponentCreatureInventory;
			if (creatureInv != null)
			{
				creatureInv.ActiveSlotChanged -= OnCreatureActiveSlotChanged;
			}
			base.Dispose();
		}
	}
}
