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
			m_creatureComponent = creatureEntity.FindComponent<ComponentCreature>();

			XElement node = ContentManager.Get<XElement>("Widgets/CreatureInventoryWidget");
			this.LoadContents(this, node);

			m_creatureGrid = this.Children.Find<GridPanelWidget>("CreatureGrid", true);
			GridPanelWidget inventoryGrid = this.Children.Find<GridPanelWidget>("InventoryGrid", true);
			m_creatureInventoryLabel = this.Children.Find<LabelWidget>("CreatureInventoryLabel", true);
			m_inventoryLabel = this.Children.Find<LabelWidget>("InventoryLabel", true);

			UpdateLabels();

			for (int i = 0; i < 16; i++)
			{
				InventorySlotWidget slot = new InventorySlotWidget();
				slot.AssignInventorySlot(creatureInventory, i);
				m_creatureSlots.Add(slot);
				m_creatureGrid.Children.Add(slot);
				m_creatureGrid.SetWidgetCell(slot, new Point2(i % 4, i / 4));
			}

			for (int i = 0; i < 16; i++)
			{
				InventorySlotWidget slot = new InventorySlotWidget();
				slot.AssignInventorySlot(playerInventory, 10 + i);
				inventoryGrid.Children.Add(slot);
				inventoryGrid.SetWidgetCell(slot, new Point2(i % 4, i / 4));
			}

			ComponentCreatureInventory creatureInv = creatureInventory as ComponentCreatureInventory;
			if (creatureInv != null)
			{
				creatureInv.ActiveSlotChanged += OnCreatureActiveSlotChanged;
				creatureInv.InventoryChanged += OnCreatureInventoryChanged;
			}
		}

		private void UpdateLabels()
		{
			if (m_creatureInventoryLabel != null)
			{
				string creatureName = GetEntityName();
				string formatText = LanguageControl.GetContentWidgets("CreatureInventoryWidget", "CreatureInventoryLabel");
				if (formatText.StartsWith("ContentWidgets:"))
					formatText = "Inventory of {0}";
				m_creatureInventoryLabel.Text = string.Format(formatText, creatureName);
			}

			if (m_inventoryLabel != null)
			{
				string inventoryText = LanguageControl.GetContentWidgets("CreatureInventoryWidget", "InventoryLabel");
				if (inventoryText.StartsWith("ContentWidgets:"))
					inventoryText = "Inventory";
				m_inventoryLabel.Text = inventoryText;
			}
		}

		private string GetEntityName()
		{
			if (m_creatureComponent != null && !string.IsNullOrEmpty(m_creatureComponent.DisplayName))
				return m_creatureComponent.DisplayName;

			var componentName = m_creatureEntity.FindComponent<ComponentName>();
			if (componentName != null && !string.IsNullOrEmpty(componentName.Name))
				return componentName.Name;

			return "Creature";
		}

		private void OnCreatureActiveSlotChanged()
		{
			RefreshCreatureSlots();
		}

		private void OnCreatureInventoryChanged()
		{
			RefreshCreatureSlots();
		}

		private void RefreshCreatureSlots()
		{
			foreach (var slot in m_creatureSlots)
			{
				slot.Update();
			}
		}

		public override void Update()
		{
			base.Update();
			RefreshCreatureSlots();
		}

		public override void Dispose()
		{
			ComponentCreatureInventory creatureInv = m_creatureInventory as ComponentCreatureInventory;
			if (creatureInv != null)
			{
				creatureInv.ActiveSlotChanged -= OnCreatureActiveSlotChanged;
				creatureInv.InventoryChanged -= OnCreatureInventoryChanged;
			}
			base.Dispose();
		}
	}
}
