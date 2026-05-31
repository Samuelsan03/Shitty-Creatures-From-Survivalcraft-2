using System;
using System.Xml.Linq;
using Engine;

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
		private Action<int> m_onAccept;

		private BevelledButtonWidget m_daysButton;
		private BevelledButtonWidget m_okButton;
		private BevelledButtonWidget m_cancelButton;
		private LabelWidget m_descriptionLabel;
		private StackPanelWidget m_warningPanel;

		// Botón único de dificultad
		private BevelledButtonWidget m_difficultyButton;
		private LabelWidget m_difficultyDescLabel;
		private DifficultyMode m_currentDifficulty;

		private readonly int[] m_options = { 4, 8, 12, 16 };

		// Modos de dificultad (solo lógica, los textos vienen del JSON)
		private readonly DifficultyMode[] m_difficultyModes = {
			DifficultyMode.Easy,
			DifficultyMode.Normal,
			DifficultyMode.Medium,
			DifficultyMode.Hard,
			DifficultyMode.Extreme
		};

		// Colores fijos (no se localizan)
		private readonly Color[] m_difficultyColors = {
			new Color(100, 200, 100),   // Verde claro
            new Color(100, 100, 255),   // Azul
            new Color(255, 200, 0),     // Amarillo
            new Color(255, 80, 80),     // Rojo
            new Color(150, 0, 150)      // Morado
        };

		private string GetText(int key) => LanguageControl.GetContentWidgets("GreenNightIntervalDialog", key.ToString());

		private string GetDescription(int days)
		{
			// Las descripciones están en el JSON con claves 6,7,8,9
			string key = days switch
			{
				4 => "6",
				8 => "7",
				12 => "8",
				16 => "9",
				_ => "6"
			};
			return LanguageControl.GetContentWidgets("GreenNightIntervalDialog", key);
		}

		public GreenNightIntervalDialog(SubsystemGreenNightSky greenNightSky, ComponentPlayer player, bool isFirstTime = false, bool showMessageOnAccept = true)
			: this(greenNightSky, player, null, isFirstTime, showMessageOnAccept) { }

		public GreenNightIntervalDialog(SubsystemGreenNightSky greenNightSky, ComponentPlayer player, Action<int> onAccept, bool isFirstTime = false, bool showMessageOnAccept = true)
		{
			m_greenNightSky = greenNightSky;
			m_player = player;
			m_selectedDays = m_greenNightSky.GreenNightIntervalDays;
			m_isFirstTime = isFirstTime;
			m_showMessageOnAccept = showMessageOnAccept;
			m_onAccept = onAccept;
			m_currentDifficulty = m_greenNightSky.DifficultyMode;

			XElement node = ContentManager.Get<XElement>("Dialogs/GreenNightIntervalDialog");
			LoadContents(null, node);

			m_daysButton = Children.Find<BevelledButtonWidget>("GreenNightIntervalDialog.DaysButton", true);
			m_descriptionLabel = Children.Find<LabelWidget>("GreenNightIntervalDialog.DescriptionLabel", true);
			m_okButton = Children.Find<BevelledButtonWidget>("GreenNightIntervalDialog.OkButton", true);
			m_cancelButton = Children.Find<BevelledButtonWidget>("GreenNightIntervalDialog.CancelButton", true);
			m_warningPanel = Children.Find<StackPanelWidget>("GreenNightIntervalDialog.WarningPanel", true);

			m_difficultyButton = Children.Find<BevelledButtonWidget>("DifficultyButton", true);
			m_difficultyDescLabel = Children.Find<LabelWidget>("DifficultyDescLabel", true);

			if (m_daysButton != null)
			{
				m_daysButton.BevelColor = new Color(113, 255, 61);
				m_daysButton.CenterColor = new Color(113, 255, 61);
			}

			if (m_warningPanel != null)
				m_warningPanel.IsVisible = m_isFirstTime;

			UpdateDifficultyUI();
			UpdateDaysButton();
			UpdateDescription(m_selectedDays);
		}

		private void UpdateDifficultyUI()
		{
			int idx = GetDifficultyIndex(m_currentDifficulty);
			if (m_difficultyButton != null)
			{
				string difficultyName = GetDifficultyName(m_currentDifficulty);
				m_difficultyButton.Text = difficultyName;
				m_difficultyButton.BevelColor = m_difficultyColors[idx];
				m_difficultyButton.CenterColor = m_difficultyColors[idx];
			}
			UpdateDifficultyDescription();
		}

		private int GetDifficultyIndex(DifficultyMode mode)
		{
			for (int i = 0; i < m_difficultyModes.Length; i++)
				if (m_difficultyModes[i] == mode) return i;
			return 1; // Normal
		}

		private DifficultyMode GetNextDifficulty()
		{
			int idx = GetDifficultyIndex(m_currentDifficulty);
			idx = (idx + 1) % m_difficultyModes.Length;
			return m_difficultyModes[idx];
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

		private string GetDifficultyDescription(DifficultyMode mode)
		{
			string key = mode switch
			{
				DifficultyMode.Easy => "Easy_Desc",
				DifficultyMode.Normal => "Normal_Desc",
				DifficultyMode.Medium => "Medium_Desc",
				DifficultyMode.Hard => "Hard_Desc",
				DifficultyMode.Extreme => "Extreme_Desc",
				_ => "Normal_Desc"
			};
			return LanguageControl.GetContentWidgets("GreenNightDifficulty", key);
		}

		private void CycleDifficulty()
		{
			m_currentDifficulty = GetNextDifficulty();
			m_greenNightSky.DifficultyMode = m_currentDifficulty;
			UpdateDifficultyUI();
			AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
		}

		private void UpdateDifficultyDescription()
		{
			if (m_difficultyDescLabel != null)
				m_difficultyDescLabel.Text = GetDifficultyDescription(m_currentDifficulty);
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

			if (m_difficultyButton != null && m_difficultyButton.IsClicked)
				CycleDifficulty();
			else if (m_daysButton.IsClicked)
				CycleDays();
			else if (m_okButton.IsClicked)
				Accept();
			else if (m_cancelButton.IsClicked)
				Cancel();

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

			if (m_onAccept != null)
				m_onAccept(m_selectedDays);
			else
			{
				m_greenNightSky.GreenNightIntervalDays = m_selectedDays;
				if (m_showMessageOnAccept)
				{
					string message = string.Format(GetText(11), m_selectedDays);
					m_player.ComponentGui.DisplaySmallMessage(message, Color.White, false, true);
				}
			}

			DialogsManager.HideDialog(this);
		}

		private void Cancel()
		{
			if (m_isClosing) return;
			m_isClosing = true;

			if (m_isFirstTime && m_showMessageOnAccept)
			{
				string message = string.Format(GetText(11), m_greenNightSky.GreenNightIntervalDays);
				m_player.ComponentGui.DisplaySmallMessage(message, Color.White, false, true);
			}

			DialogsManager.HideDialog(this);
		}
	}
}
