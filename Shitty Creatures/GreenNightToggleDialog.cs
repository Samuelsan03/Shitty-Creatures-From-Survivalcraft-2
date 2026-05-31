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
		private DifficultyMode m_originalDifficulty; // AGREGAR ESTO

		public GreenNightToggleDialog(SubsystemGreenNightSky greenNightSky, ComponentPlayer player)
		{
			m_subsystemGreenNightSky = greenNightSky;
			m_player = player;
			m_originalIntervalDays = m_subsystemGreenNightSky.GreenNightIntervalDays;
			m_tempIntervalDays = m_originalIntervalDays;
			m_originalDifficulty = m_subsystemGreenNightSky.DifficultyMode; // AGREGAR ESTO

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
			else if (m_okButton.IsClicked)
			{
				Accept();
			}
			else if (m_cancelButton.IsClicked || Input.Cancel)
			{
				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				Dismiss();
			}
		}

		private void Accept()
		{
			bool oldValue = m_subsystemGreenNightSky.GreenNightEnabled;
			bool newValue = m_checkbox.IsChecked;
			bool enabledChanged = oldValue != newValue;

			if (enabledChanged)
				m_subsystemGreenNightSky.GreenNightEnabled = newValue;

			bool intervalChanged = m_tempIntervalDays != m_originalIntervalDays;
			if (intervalChanged)
				m_subsystemGreenNightSky.GreenNightIntervalDays = m_tempIntervalDays;

			bool difficultyChanged = m_subsystemGreenNightSky.DifficultyMode != m_originalDifficulty;

			// SE CONSERVA EL MOSTRAR MENSAJES: pero solo si realmente cambió algo
			if ((enabledChanged || intervalChanged || difficultyChanged) && m_player != null && m_player.ComponentGui != null)
			{
				DifficultyMode currentDifficulty = m_subsystemGreenNightSky.DifficultyMode;
				string difficultyName = GetDifficultyName(currentDifficulty);
				string message = string.Format(
					LanguageControl.GetContentWidgets("GreenNightIntervalDialog", "11"),
					difficultyName,
					m_tempIntervalDays);
				m_player.ComponentGui.DisplaySmallMessage(message, Color.White, false, true);
			}

			AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
			Dismiss();
		}

		private string GetDifficultyName(DifficultyMode mode)
		{
			string key = mode switch
			{
				DifficultyMode.Easy => "Easy_Name",
				DifficultyMode.Normal => "Normal_Name",
				DifficultyMode.Medium => "Medium_Name",
				DifficultyMode.Hard => "Hard_Name",
				DifficultyMode.Extreme => "Extreme_Name",
				_ => "Normal_Name"
			};
			return LanguageControl.GetContentWidgets("GreenNightDifficulty", key);
		}

		private void Dismiss()
		{
			DialogsManager.HideDialog(this);
		}
	}
}
