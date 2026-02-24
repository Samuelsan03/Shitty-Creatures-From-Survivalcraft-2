using System;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class GreenNightToggleDialog : Dialog
	{
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private ComponentPlayer m_player;
		private CheckboxWidget m_checkbox;
		private ButtonWidget m_okButton;
		private ButtonWidget m_cancelButton;
		private LabelWidget m_titleLabel;
		private LabelWidget m_explanationLabel;

		public GreenNightToggleDialog(SubsystemGreenNightSky greenNightSky, ComponentPlayer player)
		{
			m_subsystemGreenNightSky = greenNightSky;
			m_player = player;

			XElement node = ContentManager.Get<XElement>("Dialogs/GreenNightToggleDialog");
			LoadContents(this, node);

			m_checkbox = Children.Find<CheckboxWidget>("Checkbox", true);
			m_okButton = Children.Find<ButtonWidget>("OKButton", true);
			m_cancelButton = Children.Find<ButtonWidget>("CancelButton", true);
			m_titleLabel = Children.Find<LabelWidget>("TitleLabel", true);

			// Aplicar traducciones
			m_titleLabel.Text = LanguageControl.Get("GreenNightDialog", "Title");
			m_checkbox.Text = LanguageControl.Get("GreenNightDialog", "CheckboxText");
			m_okButton.Text = LanguageControl.Get("GreenNightDialog", "OkButton");
			m_cancelButton.Text = LanguageControl.Get("GreenNightDialog", "CancelButton");

			m_checkbox.IsChecked = m_subsystemGreenNightSky.GreenNightEnabled;

			// Agregar el texto explicativo
			AddExplanationText();
		}

		private void AddExplanationText()
		{
			// Buscar el StackPanel principal
			StackPanelWidget mainStack = null;
			foreach (var child in Children)
			{
				if (child is StackPanelWidget sp && sp.Direction == LayoutDirection.Vertical)
				{
					mainStack = sp;
					break;
				}
			}

			if (mainStack != null)
			{
				// Crear la etiqueta explicativa
				m_explanationLabel = new LabelWidget
				{
					Text = LanguageControl.Get("GreenNightDialog", "ToggleExplanation"),
					Color = new Color(255, 140, 0), // Naranja
					HorizontalAlignment = WidgetAlignment.Center,
					VerticalAlignment = WidgetAlignment.Center,
					FontScale = 0.8f,
					WordWrap = true,
					Margin = new Vector2(10, 5)
				};

				// Crear contenedor con fondo semitransparente para el texto explicativo
				CanvasWidget explanationContainer = new CanvasWidget
				{
					Size = new Vector2(float.PositiveInfinity, 90),
					Children =
					{
						new RectangleWidget
						{
							FillColor = new Color(0, 0, 0, 100),
							OutlineColor = Color.Transparent,
							OutlineThickness = 0
						},
						m_explanationLabel
					}
				};

				// Insertar después del checkbox (posición 3 en el stack vertical)
				// El stack tiene: [Label (título), CanvasWidget (spacer), CanvasWidget (checkbox), ...]
				if (mainStack.Children.Count >= 3)
				{
					mainStack.Children.Insert(3, explanationContainer);
				}
			}
		}

		public override void Update()
		{
			if (m_okButton.IsClicked)
			{
				bool oldValue = m_subsystemGreenNightSky.GreenNightEnabled;
				bool newValue = m_checkbox.IsChecked;

				// Solo aplicar cambios y mostrar mensaje si el valor realmente cambió
				if (oldValue != newValue)
				{
					m_subsystemGreenNightSky.GreenNightEnabled = newValue;

					// Mostrar mensaje al jugador solo si hubo cambio
					if (m_player != null && m_player.ComponentGui != null)
					{
						string messageKey = newValue ? "EnabledNotification" : "DisabledNotification";
						Color messageColor = newValue ? new Color(0, 100, 0) : new Color(0, 255, 0);
						string message = LanguageControl.Get("GreenNightDialog", messageKey);
						m_player.ComponentGui.DisplaySmallMessage(message, messageColor, false, true);
					}
				}

				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				Dismiss();
			}

			if (m_cancelButton.IsClicked)
			{
				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				Dismiss();
			}
		}

		private void Dismiss()
		{
			if (ParentWidget is ContainerWidget container)
				container.Children.Remove(this);
			else if (ParentWidget != null)
				ParentWidget.Children.Remove(this);
		}
	}
}