using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCoordinateDisplay : Component, IUpdateable
	{
		private ComponentPlayer m_componentPlayer;
		private ComponentGui m_componentGui;

		private LabelWidget m_labelWidget;
		private CanvasWidget m_labelContainer;
		private ContainerWidget m_targetContainer;

		private string m_coordinateFormat;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_componentPlayer = Entity.FindComponent<ComponentPlayer>(true);
			m_componentGui = Entity.FindComponent<ComponentGui>(true);

			if (m_componentPlayer == null || m_componentGui == null)
			{
				Log.Warning("ComponentCoordinateDisplay: No se encontró ComponentPlayer o ComponentGui.");
				return;
			}

			// Formato localizado o por defecto
			if (!LanguageControl.TryGet(out m_coordinateFormat, "ComponentCoordinateDisplay", "CoordinateFormat"))
			{
				m_coordinateFormat = "Coordinates: X: {0:F1}  Y: {1:F1}  Z: {2:F1}";
			}

			// Crear LabelWidget con el estilo nativo del juego (sombra incluida)
			m_labelWidget = new LabelWidget
			{
				Font = LabelWidget.BitmapFont,
				Color = Color.White,
				DropShadow = true,               // ← Sombra estándar del GUI del juego
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				FontScale = 0.6f,
				Text = string.Format(m_coordinateFormat, 0f, 0f, 0f)
			};

			// Contenedor para posicionarlo en la esquina inferior derecha
			m_labelContainer = new CanvasWidget
			{
				HorizontalAlignment = WidgetAlignment.Far,
				VerticalAlignment = WidgetAlignment.Far,
				Margin = new Vector2(10, 10)
			};
			m_labelContainer.Children.Add(m_labelWidget);

			// Insertar en la GUI (preferiblemente en RightControlsContainer)
			ContainerWidget controlsContainer = m_componentGui.ControlsContainerWidget;
			if (controlsContainer != null)
			{
				var rightContainer = controlsContainer.Children.Find<ContainerWidget>("RightControlsContainer", true);
				if (rightContainer != null)
				{
					rightContainer.Children.Add(m_labelContainer);
					m_targetContainer = rightContainer;
				}
				else
				{
					controlsContainer.Children.Add(m_labelContainer);
					m_targetContainer = controlsContainer;
				}
			}
			else
			{
				m_componentPlayer.GuiWidget.Children.Add(m_labelContainer);
				m_targetContainer = m_componentPlayer.GuiWidget;
			}

			// Asegurar alineación
			m_labelContainer.HorizontalAlignment = WidgetAlignment.Far;
			m_labelContainer.VerticalAlignment = WidgetAlignment.Far;

			UpdateVisibility();
		}

		public void Update(float dt)
		{
			if (m_componentPlayer == null || m_labelWidget == null)
				return;

			UpdateVisibility();

			var body = m_componentPlayer.ComponentBody;
			if (body != null)
			{
				Vector3 pos = body.Position;
				m_labelWidget.Text = string.Format(m_coordinateFormat, pos.X, pos.Y, pos.Z);
			}
		}

		private void UpdateVisibility()
		{
			bool shouldShow = ShittyCreaturesSettingsManager.CoordinateDisplayEnabled;
			if (m_labelContainer != null)
			{
				m_labelContainer.IsVisible = shouldShow;
			}
		}

		public override void Dispose()
		{
			if (m_labelContainer != null && m_targetContainer != null)
			{
				m_targetContainer.Children.Remove(m_labelContainer);
			}
			base.Dispose();
		}
	}
}
