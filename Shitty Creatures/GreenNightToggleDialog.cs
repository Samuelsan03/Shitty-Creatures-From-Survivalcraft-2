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
		private LabelWidget m_intervalLabel;
		private LabelWidget m_intervalHintLabel;
		private bool m_lastCheckState;
		private int m_originalIntervalDays;
		private int m_tempIntervalDays;

		public GreenNightToggleDialog(SubsystemGreenNightSky greenNightSky, ComponentPlayer player)
		{
			m_subsystemGreenNightSky = greenNightSky;
			m_player = player;
			m_originalIntervalDays = m_subsystemGreenNightSky.GreenNightIntervalDays;
			m_tempIntervalDays = m_originalIntervalDays;

			XElement node = ContentManager.Get<XElement>("Dialogs/GreenNightToggleDialog");
			LoadContents(this, node);

			m_titleLabel = Children.Find<LabelWidget>("TitleLabel", true);
			m_checkbox = Children.Find<CheckboxWidget>("Checkbox", true);
			m_daysButton = Children.Find<ButtonWidget>("DaysButton", true);
			m_explanationLabel = Children.Find<LabelWidget>("ExplanationLabel", true);
			m_okButton = Children.Find<ButtonWidget>("OKButton", true);
			m_cancelButton = Children.Find<ButtonWidget>("CancelButton", true);
			m_intervalLabel = Children.Find<LabelWidget>("IntervalLabel", true);
			m_intervalHintLabel = Children.Find<LabelWidget>("IntervalHintLabel", true);

			m_titleLabel.Text = LanguageControl.GetContentWidgets("GreenNightToggleDialog", "Title");
			m_checkbox.Text = LanguageControl.GetContentWidgets("GreenNightToggleDialog", "CheckboxText");
			m_okButton.Text = LanguageControl.GetContentWidgets("GreenNightToggleDialog", "OkButton");
			m_cancelButton.Text = LanguageControl.GetContentWidgets("GreenNightToggleDialog", "CancelButton");
			m_intervalLabel.Text = LanguageControl.GetContentWidgets("GreenNightToggleDialog", "IntervalLabel");
			m_intervalHintLabel.Text = LanguageControl.GetContentWidgets("GreenNightToggleDialog", "IntervalHintLabel");
			m_daysButton.Text = LanguageControl.GetContentWidgets("GreenNightToggleDialog", "DaysButton");

			// Aplicar color (39, 146, 0) al botón de días
			if (m_daysButton is BevelledButtonWidget bevelledDaysButton)
			{
				bevelledDaysButton.BevelColor = new Color(39, 146, 0);
				bevelledDaysButton.CenterColor = new Color(39, 146, 0);
			}

			m_checkbox.IsChecked = m_subsystemGreenNightSky.GreenNightEnabled;
			m_lastCheckState = m_checkbox.IsChecked;

			UpdateExplanationText();
		}

		private void UpdateExplanationText()
		{
			string explanationKey = m_checkbox.IsChecked ? "EnableExplanation" : "DisableExplanation";
			m_explanationLabel.Text = LanguageControl.GetContentWidgets("GreenNightToggleDialog", explanationKey);
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
				var intervalDialog = new GreenNightIntervalDialog(
					m_subsystemGreenNightSky,
					m_player,
					(selectedDays) => { m_tempIntervalDays = selectedDays; },
					false,
					false
				);
				DialogsManager.ShowDialog(m_player.GuiWidget, intervalDialog);
			}

			if (m_okButton.IsClicked)
			{
				Accept();
			}

			if (m_cancelButton.IsClicked || Input.Cancel)
			{
				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				Dismiss();
			}
		}

		private void Accept()
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
					string message = LanguageControl.GetContentWidgets("GreenNightToggleDialog", messageKey);
					m_player.ComponentGui.DisplaySmallMessage(message, messageColor, false, true);
				}
			}

			if (m_tempIntervalDays != m_originalIntervalDays)
			{
				m_subsystemGreenNightSky.GreenNightIntervalDays = m_tempIntervalDays;
				string intervalMessage = string.Format(
					LanguageControl.GetContentWidgets("GreenNightIntervalDialog", "11"),
					m_tempIntervalDays);
				m_player.ComponentGui.DisplaySmallMessage(intervalMessage, Color.White, false, true);
			}

			AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
			Dismiss();
		}

		private void Dismiss()
		{
			DialogsManager.HideDialog(this);
		}
	}
}
