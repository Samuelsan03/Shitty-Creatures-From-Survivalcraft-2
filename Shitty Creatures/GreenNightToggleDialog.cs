using System;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class GreenNightToggleDialog : Dialog
	{
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private ComponentPlayer m_player;
		private CheckboxWidget m_checkbox;
		private ButtonWidget m_daysButton;
		private ButtonWidget m_okButton;
		private ButtonWidget m_cancelButton;
		private LabelWidget m_titleLabel;
		private LabelWidget m_explanationLabel;
		private bool m_lastCheckState;
		private int m_originalIntervalDays;  // Guardar el intervalo original

		public GreenNightToggleDialog(SubsystemGreenNightSky greenNightSky, ComponentPlayer player)
		{
			m_subsystemGreenNightSky = greenNightSky;
			m_player = player;
			m_originalIntervalDays = m_subsystemGreenNightSky.GreenNightIntervalDays;  // Guardar valor original

			XElement node = ContentManager.Get<XElement>("Dialogs/GreenNightToggleDialog");
			LoadContents(this, node);

			m_titleLabel = Children.Find<LabelWidget>("TitleLabel", true);
			m_checkbox = Children.Find<CheckboxWidget>("Checkbox", true);
			m_daysButton = Children.Find<ButtonWidget>("DaysButton", true);
			m_explanationLabel = Children.Find<LabelWidget>("ExplanationLabel", true);
			m_okButton = Children.Find<ButtonWidget>("OKButton", true);
			m_cancelButton = Children.Find<ButtonWidget>("CancelButton", true);

			m_titleLabel.Text = LanguageControl.Get("GreenNightDialog", "Title");
			m_checkbox.Text = LanguageControl.Get("GreenNightDialog", "CheckboxText");
			m_okButton.Text = LanguageControl.Get("GreenNightDialog", "OkButton");
			m_cancelButton.Text = LanguageControl.Get("GreenNightDialog", "CancelButton");

			m_checkbox.IsChecked = m_subsystemGreenNightSky.GreenNightEnabled;
			m_lastCheckState = m_checkbox.IsChecked;

			UpdateExplanationText();
		}

		private void UpdateExplanationText()
		{
			string explanationKey = m_checkbox.IsChecked ? "EnableExplanation" : "DisableExplanation";
			m_explanationLabel.Text = LanguageControl.Get("GreenNightDialog", explanationKey);
		}

		public override void Update()
		{
			if (m_checkbox.IsChecked != m_lastCheckState)
			{
				m_lastCheckState = m_checkbox.IsChecked;
				UpdateExplanationText();
			}

			if (m_daysButton.IsClicked)
			{
				// isFirstTime = false (no muestra mensaje rojo), showMessageOnAccept = false (no muestra mensaje al aceptar)
				var intervalDialog = new GreenNightIntervalDialog(m_subsystemGreenNightSky, m_player, false, false);
				DialogsManager.ShowDialog(m_player.GuiWidget, intervalDialog);
			}

			if (m_okButton.IsClicked)
			{
				bool oldValue = m_subsystemGreenNightSky.GreenNightEnabled;
				bool newValue = m_checkbox.IsChecked;

				if (oldValue != newValue)
				{
					m_subsystemGreenNightSky.GreenNightEnabled = newValue;

					if (m_player != null && m_player.ComponentGui != null)
					{
						string messageKey = newValue ? "EnabledNotification" : "DisabledNotification";
						Color messageColor = newValue ? new Color(0, 100, 0) : new Color(0, 255, 0);
						string message = LanguageControl.Get("GreenNightDialog", messageKey);
						m_player.ComponentGui.DisplaySmallMessage(message, messageColor, false, true);
					}
				}

				// Verificar si el intervalo cambió (desde el diálogo de intervalo)
				int currentInterval = m_subsystemGreenNightSky.GreenNightIntervalDays;
				if (currentInterval != m_originalIntervalDays)
				{
					string intervalMessage = string.Format(
						LanguageControl.GetContentWidgets("GreenNightIntervalDialog", 11), // "Green Night has been set to every {0} days."
						currentInterval);
					m_player.ComponentGui.DisplaySmallMessage(intervalMessage, Color.White, false, true);
				}

				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				Dismiss();
			}

			if (m_cancelButton.IsClicked || Input.Cancel)
			{
				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				Dismiss();
			}
		}

		private void Dismiss()
		{
			DialogsManager.HideDialog(this);
		}
	}
}
