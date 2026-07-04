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
		private Action<int, DifficultyMode> m_onAccept;  // Cambiado para incluir dificultad

		private BevelledButtonWidget m_daysButton;
		private BevelledButtonWidget m_okButton;
		private BevelledButtonWidget m_cancelButton;
		private LabelWidget m_descriptionLabel;
		private StackPanelWidget m_warningPanel;

		private BevelledButtonWidget m_difficultyButton;
		private LabelWidget m_difficultyDescLabel;
		private ScrollPanelWidget m_difficultyDescScrollPanel;
		private DifficultyMode m_currentDisplayDifficulty;
		private DifficultyMode m_tempDifficulty;

		private readonly int[] m_options = { 4, 8, 12, 16 };

		private readonly DifficultyMode[] m_difficultyModes = {
	DifficultyMode.VeryEasy,
	DifficultyMode.Easy,
	DifficultyMode.Normal,
	DifficultyMode.Medium,
	DifficultyMode.Hard,
	DifficultyMode.Extreme,
	DifficultyMode.Impossible   // Nuevo
};

		private List<DifficultyMode> m_availableDifficultyModes; // ← NUEVO: lista filtrada

		private readonly Color[] m_difficultyColors = {
	new Color(230, 255, 240),   // VeryEasy - verde menta
    new Color(170, 240, 255),   // Easy - turquesa
    new Color(90, 190, 255),    // Normal - azul brillante
    new Color(70, 110, 220),    // Medium - azul profundo
    new Color(90, 60, 170),     // Hard - violeta
    new Color(60, 30, 110),     // Extreme - índigo oscuro
    new Color(20, 15, 40)       // Impossible - noche
};

		private string GetText(int key) => LanguageControl.GetContentWidgets("GreenNightIntervalDialog", key.ToString());

		private string GetDescription(int days)
		{
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

		public GreenNightIntervalDialog(SubsystemGreenNightSky greenNightSky, ComponentPlayer player, Action<int, DifficultyMode> onAccept, bool isFirstTime = false, bool showMessageOnAccept = true)
		{
			m_greenNightSky = greenNightSky;
			m_player = player;
			m_selectedDays = m_greenNightSky.GreenNightIntervalDays;
			m_isFirstTime = isFirstTime;
			m_showMessageOnAccept = showMessageOnAccept;
			m_onAccept = onAccept;
			m_currentDisplayDifficulty = m_greenNightSky.DifficultyMode;
			m_tempDifficulty = m_greenNightSky.DifficultyMode;

			// Obtener estado de desbloqueo de Impossible
			bool isImpossibleUnlocked = false;
			var zombiesSpawn = m_greenNightSky.Project.FindSubsystem<SubsystemZombiesSpawn>(true);
			if (zombiesSpawn != null)
				isImpossibleUnlocked = zombiesSpawn.HasAcceptedImpossibleChallenge;

			// Construir lista filtrada de dificultades
			m_availableDifficultyModes = new List<DifficultyMode>();
			foreach (var mode in m_difficultyModes)
			{
				if (mode == DifficultyMode.Impossible && !isImpossibleUnlocked)
					continue;
				m_availableDifficultyModes.Add(mode);
			}

			// Si la dificultad actual es Impossible pero no está desbloqueada, forzar a Normal
			if (m_tempDifficulty == DifficultyMode.Impossible && !isImpossibleUnlocked)
			{
				m_tempDifficulty = DifficultyMode.Normal;
				m_currentDisplayDifficulty = DifficultyMode.Normal;
			}

			XElement node = ContentManager.Get<XElement>("Dialogs/GreenNightIntervalDialog");
			LoadContents(null, node);

			m_daysButton = Children.Find<BevelledButtonWidget>("GreenNightIntervalDialog.DaysButton", true);
			m_descriptionLabel = Children.Find<LabelWidget>("GreenNightIntervalDialog.DescriptionLabel", true);
			m_okButton = Children.Find<BevelledButtonWidget>("GreenNightIntervalDialog.OkButton", true);
			m_cancelButton = Children.Find<BevelledButtonWidget>("GreenNightIntervalDialog.CancelButton", true);
			m_warningPanel = Children.Find<StackPanelWidget>("GreenNightIntervalDialog.WarningPanel", true);

			m_difficultyButton = Children.Find<BevelledButtonWidget>("DifficultyButton", true);
			m_difficultyDescLabel = Children.Find<LabelWidget>("DifficultyDescLabel", true);
			m_difficultyDescScrollPanel = Children.Find<ScrollPanelWidget>("DifficultyDescScrollPanel", true);

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
			int idx = GetDifficultyIndex(m_currentDisplayDifficulty);
			if (m_difficultyButton != null)
			{
				string difficultyName = GetDifficultyName(m_currentDisplayDifficulty);
				m_difficultyButton.Text = difficultyName;
				m_difficultyButton.BevelColor = m_difficultyColors[idx];
				m_difficultyButton.CenterColor = m_difficultyColors[idx];
			}

			// Resetear scroll al inicio cuando cambia la dificultad
			if (m_difficultyDescScrollPanel != null)
			{
				m_difficultyDescScrollPanel.ScrollPosition = 0f;
				m_difficultyDescScrollPanel.ScrollSpeed = 0f;
			}

			UpdateDifficultyDescription();
		}

		private int GetDifficultyIndex(DifficultyMode mode)
		{
			for (int i = 0; i < m_availableDifficultyModes.Count; i++)
				if (m_availableDifficultyModes[i] == mode) return i;
			return 0;
		}

		private DifficultyMode GetNextDifficulty(DifficultyMode current)
		{
			int idx = GetDifficultyIndex(current);
			idx = (idx + 1) % m_availableDifficultyModes.Count;
			return m_availableDifficultyModes[idx];
		}

		private string GetDifficultyName(DifficultyMode mode)
		{
			string key = mode switch
			{
				DifficultyMode.VeryEasy => "VeryEasy_Name",
				DifficultyMode.Easy => "Easy_Name",
				DifficultyMode.Normal => "Normal_Name",
				DifficultyMode.Medium => "Medium_Name",
				DifficultyMode.Hard => "Hard_Name",
				DifficultyMode.Extreme => "Extreme_Name",
				DifficultyMode.Impossible => "Impossible_Name",   // Nuevo
				_ => "Normal_Name"
			};
			return LanguageControl.GetContentWidgets("GreenNightDifficulty", key);
		}

		private string GetDifficultyDescription(DifficultyMode mode)
		{
			string key = mode switch
			{
				DifficultyMode.VeryEasy => "VeryEasy_Desc",
				DifficultyMode.Easy => "Easy_Desc",
				DifficultyMode.Normal => "Normal_Desc",
				DifficultyMode.Medium => "Medium_Desc",
				DifficultyMode.Hard => "Hard_Desc",
				DifficultyMode.Extreme => "Extreme_Desc",
				DifficultyMode.Impossible => "Impossible_Desc",   // Nuevo
				_ => "Normal_Desc"
			};
			return LanguageControl.GetContentWidgets("GreenNightDifficulty", key);
		}

		private void CycleDifficulty()
		{
			m_tempDifficulty = GetNextDifficulty(m_tempDifficulty);
			m_currentDisplayDifficulty = m_tempDifficulty;
			UpdateDifficultyUI();
			AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
		}

		private void UpdateDifficultyDescription()
		{
			if (m_difficultyDescLabel != null)
				m_difficultyDescLabel.Text = GetDifficultyDescription(m_currentDisplayDifficulty);
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

			// Verificar que la dificultad seleccionada sea válida
			bool isImpossibleUnlocked = false;
			var zombiesSpawn = m_greenNightSky.Project.FindSubsystem<SubsystemZombiesSpawn>(true);
			if (zombiesSpawn != null)
				isImpossibleUnlocked = zombiesSpawn.HasAcceptedImpossibleChallenge;

			if (m_tempDifficulty == DifficultyMode.Impossible && !isImpossibleUnlocked)
				m_tempDifficulty = DifficultyMode.Normal;

			if (m_onAccept != null)
			{
				// En lugar de aplicar cambios directamente, pasamos los valores al callback
				m_onAccept(m_selectedDays, m_tempDifficulty);
			}
			else
			{
				// Comportamiento original (cuando se usa directamente, sin toggle)
				m_greenNightSky.GreenNightIntervalDays = m_selectedDays;
				m_greenNightSky.DifficultyMode = m_tempDifficulty;

				// ===== Resetear el flag para que el bloqueo se aplique según la nueva dificultad =====
				if (zombiesSpawn != null)
					zombiesSpawn.ImpossibleBlockDisabledForAllies = false;

				ShittyCreaturesModLoader.NotifyDifficultyChanged(m_greenNightSky);

				if (m_showMessageOnAccept)
				{
					string difficultyName = GetDifficultyName(m_tempDifficulty);
					string message = string.Format(GetText(11), difficultyName, m_selectedDays);
					m_player.ComponentGui.DisplaySmallMessage(message, Color.White, false, true);
				}

				// Forzar actualización del label de dificultad en el HUD
				if (zombiesSpawn != null)
				{
					zombiesSpawn.ForceUpdateDifficultyLabel();
				}
			}

			DialogsManager.HideDialog(this);
		}

		private void Cancel()
		{
			if (m_isClosing) return;
			m_isClosing = true;

			if (m_showMessageOnAccept)
			{
				string difficultyName = GetDifficultyName(m_greenNightSky.DifficultyMode);
				int currentDays = m_greenNightSky.GreenNightIntervalDays;
				string message = string.Format(GetText(11), difficultyName, currentDays);
				m_player.ComponentGui.DisplaySmallMessage(message, Color.White, false, true);
			}

			DialogsManager.HideDialog(this);
		}
	}
}
