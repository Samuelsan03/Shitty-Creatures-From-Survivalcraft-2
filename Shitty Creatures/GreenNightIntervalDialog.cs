using System;
using System.Xml.Linq;
using Engine;
using Game;

namespace Game
{
	public class GreenNightIntervalDialog : Dialog
	{
		private SubsystemGreenNightSky m_greenNightSky;
		private ComponentPlayer m_player;
		private int m_selectedDays;
		private bool m_isClosing;
		private bool m_isFirstTime;
		private bool m_showMessageOnAccept;

		private BevelledButtonWidget m_daysButton;
		private BevelledButtonWidget m_okButton;
		private BevelledButtonWidget m_cancelButton;
		private LabelWidget m_descriptionLabel;
		private StackPanelWidget m_warningPanel;

		private readonly int[] m_options = { 4, 8, 12, 16 };

		private string GetText(int key)
		{
			return LanguageControl.GetContentWidgets("GreenNightIntervalDialog", key.ToString());
		}

		private string GetDescription(int days)
		{
			if (days == 4) return GetText(6);
			if (days == 8) return GetText(7);
			if (days == 12) return GetText(8);
			return GetText(9);
		}

		public GreenNightIntervalDialog(SubsystemGreenNightSky greenNightSky, ComponentPlayer player, bool isFirstTime = false, bool showMessageOnAccept = true)
		{
			m_greenNightSky = greenNightSky;
			m_player = player;
			m_selectedDays = m_greenNightSky.GreenNightIntervalDays;
			m_isFirstTime = isFirstTime;
			m_showMessageOnAccept = showMessageOnAccept;

			XElement node = ContentManager.Get<XElement>("Dialogs/GreenNightIntervalDialog");
			LoadContents(null, node);

			m_daysButton = Children.Find<BevelledButtonWidget>("GreenNightIntervalDialog.DaysButton", true);
			m_descriptionLabel = Children.Find<LabelWidget>("GreenNightIntervalDialog.DescriptionLabel", true);
			m_okButton = Children.Find<BevelledButtonWidget>("GreenNightIntervalDialog.OkButton", true);
			m_cancelButton = Children.Find<BevelledButtonWidget>("GreenNightIntervalDialog.CancelButton", true);
			m_warningPanel = Children.Find<StackPanelWidget>("GreenNightIntervalDialog.WarningPanel", true);

			if (m_warningPanel != null)
			{
				m_warningPanel.IsVisible = m_isFirstTime;
			}

			UpdateDaysButton();
			UpdateDescription(m_selectedDays);
		}

		private void UpdateDaysButton()
		{
			string format = GetText(12);
			m_daysButton.Text = string.Format(format, m_selectedDays);
		}

		private void UpdateDescription(int days)
		{
			m_descriptionLabel.Text = GetDescription(days);
			m_descriptionLabel.Color = new Color(255, 165, 0);
		}

		public override void Update()
		{
			if (m_isClosing) return;

			if (m_daysButton.IsClicked)
			{
				CycleDays();
			}
			else if (m_okButton.IsClicked)
			{
				Accept();
			}
			else if (m_cancelButton.IsClicked)
			{
				Cancel();
			}

			base.Update();
		}

		private void CycleDays()
		{
			int index = Array.IndexOf(m_options, m_selectedDays);
			index = (index + 1) % m_options.Length;
			m_selectedDays = m_options[index];
			UpdateDaysButton();
			UpdateDescription(m_selectedDays);
		}

		private void Accept()
		{
			if (m_isClosing) return;
			m_isClosing = true;

			m_greenNightSky.GreenNightIntervalDays = m_selectedDays;

			// Mostrar mensaje solo si se pide explícitamente
			if (m_showMessageOnAccept)
			{
				string message = string.Format(GetText(11), m_selectedDays);
				m_player.ComponentGui.DisplaySmallMessage(message, Color.White, false, true);
			}

			DialogsManager.HideDialog(this);
		}

		private void Cancel()
		{
			if (m_isClosing) return;
			m_isClosing = true;

			// Si es el primer spawn (diálogo inicial) y se cancela, mostrar mensaje con el intervalo original
			if (m_isFirstTime && m_showMessageOnAccept)
			{
				string message = string.Format(GetText(11), m_greenNightSky.GreenNightIntervalDays);
				m_player.ComponentGui.DisplaySmallMessage(message, Color.White, false, true);
			}

			DialogsManager.HideDialog(this);
		}
	}
}
