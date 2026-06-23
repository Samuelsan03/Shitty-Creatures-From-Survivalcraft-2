using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class CreatureInventoryWidget : CanvasWidget
	{
		private ComponentCreatureInventory m_creatureInventory;
		private ComponentFarmerBehavior m_farmerBehavior;
		private GridPanelWidget m_creatureGrid;
		private GridPanelWidget m_inventoryGrid;
		private BevelledButtonWidget m_agricultorButton;
		private bool m_buttonPositioned;
		private bool m_lastCheckedState;

		public CreatureInventoryWidget(IInventory playerInventory, ComponentCreatureInventory creatureInventory)
		{
			m_creatureInventory = creatureInventory;

			// Obtener el ComponentFarmerBehavior de la misma entidad
			m_farmerBehavior = creatureInventory.Entity.FindComponent<ComponentFarmerBehavior>();

			XElement node = ContentManager.Get<XElement>("Widgets/CreatureInventoryWidget");
			LoadContents(this, node);

			m_creatureGrid = Children.Find<GridPanelWidget>("CreatureGrid", true);
			m_inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid", true);

			int creatureSlots = creatureInventory.SlotsCount;

			// Ajustar cuadrícula de la criatura (4 columnas fijas)
			int columns = 4;
			int rows = (creatureSlots + columns - 1) / columns;
			m_creatureGrid.RowsCount = rows;
			m_creatureGrid.ColumnsCount = columns;

			int slotIndex = 0;
			for (int row = 0; row < rows; row++)
			{
				for (int col = 0; col < columns; col++)
				{
					if (slotIndex >= creatureSlots) break;
					InventorySlotWidget slot = new InventorySlotWidget();
					slot.AssignInventorySlot(creatureInventory, slotIndex);
					m_creatureGrid.Children.Add(slot);
					m_creatureGrid.SetWidgetCell(slot, new Point2(col, row));
					slotIndex++;
				}
			}

			// ============================================
			// Botón Agricultor
			// ============================================
			m_agricultorButton = new BevelledButtonWidget();
			m_agricultorButton.IsAutoCheckingEnabled = true;
			m_agricultorButton.Size = new Vector2(200, 50);
			m_agricultorButton.Color = Color.White;

			// Inicializar estado basado en FarmerEnabled
			bool initialState = m_farmerBehavior?.FarmerEnabled ?? false;
			m_agricultorButton.IsChecked = initialState;
			m_lastCheckedState = initialState;
			UpdateButtonColors(initialState);

			// Si no hay comportamiento, deshabilitar el botón
			if (m_farmerBehavior == null)
			{
				m_agricultorButton.IsEnabled = false;
				m_agricultorButton.Text = LanguageControl.GetContentWidgets("CreatureInventoryWidget", 2);
			}
			else
			{
				m_agricultorButton.Text = LanguageControl.GetContentWidgets("CreatureInventoryWidget", 1);
			}

			this.Children.Add(m_agricultorButton);
			m_buttonPositioned = false;
		}

		/// <summary>
		/// Actualiza los colores del botón según el estado activo/inactivo
		/// </summary>
		private void UpdateButtonColors(bool isActive)
		{
			if (m_agricultorButton == null) return;

			if (isActive)
			{
				m_agricultorButton.CenterColor = Color.Green;
				m_agricultorButton.BevelColor = Color.DarkGreen;
			}
			else
			{
				m_agricultorButton.CenterColor = Color.Red;
				m_agricultorButton.BevelColor = Color.DarkRed;
			}
		}

		public override void Update()
		{
			// Comportamiento original: eliminar si el inventario ya no existe
			if (!m_creatureInventory.IsAddedToProject)
			{
				ParentWidget.Children.Remove(this);
				return;
			}

			// Posicionar el botón una vez que el widget esté medido
			if (m_agricultorButton != null && !m_buttonPositioned)
			{
				float left = 306f;
				float top = 52f;
				float right = this.ActualSize.X;
				float bottom = this.ActualSize.Y;

				float centerX = (left + right) / 2f;
				float centerY = (top + bottom) / 2f;

				Vector2 buttonSize = m_agricultorButton.Size;
				if (float.IsInfinity(buttonSize.X)) buttonSize.X = 200;
				if (float.IsInfinity(buttonSize.Y)) buttonSize.Y = 50;

				Vector2 position = new Vector2(centerX - buttonSize.X / 2f, centerY - buttonSize.Y / 2f);
				CanvasWidget.SetPosition(m_agricultorButton, position);
				m_buttonPositioned = true;
			}

			// ============================================
			// Detectar cambios en el botón y sincronizar con FarmerEnabled
			// ============================================
			if (m_agricultorButton != null && m_farmerBehavior != null)
			{
				// Detectar cuando el usuario hace clic en el botón
				if (m_agricultorButton.IsChecked != m_lastCheckedState)
				{
					m_lastCheckedState = m_agricultorButton.IsChecked;

					// Usar FarmerEnabled
					m_farmerBehavior.FarmerEnabled = m_agricultorButton.IsChecked;

					// Actualizar colores
					UpdateButtonColors(m_agricultorButton.IsChecked);

					// Reproducir sonido al cambiar estado
					AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				}

				// Sincronizar en caso de que el estado cambie externamente
				if (m_agricultorButton.IsChecked != m_farmerBehavior.FarmerEnabled)
				{
					m_agricultorButton.IsChecked = m_farmerBehavior.FarmerEnabled;
					m_lastCheckedState = m_farmerBehavior.FarmerEnabled;
					UpdateButtonColors(m_farmerBehavior.FarmerEnabled);
				}
			}
		}
	}
}
