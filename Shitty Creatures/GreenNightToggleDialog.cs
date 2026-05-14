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
		private ButtonWidget m_okButton;
		private ButtonWidget m_cancelButton;
		private LabelWidget m_titleLabel;
		private LabelWidget m_explanationLabel;
		private bool m_lastCheckState;

		public GreenNightToggleDialog(SubsystemGreenNightSky greenNightSky, ComponentPlayer player)
		{
			m_subsystemGreenNightSky = greenNightSky;
			m_player = player;

			XElement node = ContentManager.Get<XElement>("Dialogs/GreenNightToggleDialog");
			LoadContents(this, node);

			m_titleLabel = Children.Find<LabelWidget>("TitleLabel", true);
			m_checkbox = Children.Find<CheckboxWidget>("Checkbox", true);
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
