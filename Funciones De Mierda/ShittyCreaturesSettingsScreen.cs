using System;
using System.Xml.Linq;
using Engine;
using Game;

namespace Game
{
	public class ShittyCreaturesSettingsScreen : Screen
	{
		private BevelledButtonWidget m_ghostButton;
		private BevelledButtonWidget m_tankButton;
		private BevelledButtonWidget m_spawnButton;
		private BevelledButtonWidget m_thirstButton;
		// --- NUEVO BOTÓN ---
		private BevelledButtonWidget m_coordinateButton;
		// ------------------
		private StackPanelWidget m_contentPanel;
		private LabelWidget m_titleLabel;

		public ShittyCreaturesSettingsScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/ShittyCreaturesSettingsScreen");
			this.LoadContents(this, node);

			m_titleLabel = this.Children.Find<LabelWidget>("TopBar.Label", true);
			m_titleLabel.Text = LanguageControl.Get(new string[] { "ShittyCreatures", "ScreenTitle" });

			m_contentPanel = this.Children.Find<StackPanelWidget>("Content", true);

			CreateOptionRow("ShittyCreatures", "GhostDescription", out m_ghostButton, Color.Gray, GetGhostButtonText);
			CreateOptionRow("ShittyCreatures", "TankDescription", out m_tankButton, Color.Red, GetTankButtonText);
			CreateOptionRow("ShittyCreatures", "SpawnDescription", out m_spawnButton, Color.LightGreen, GetSpawnButtonText);
			CreateOptionRow("ShittyCreatures", "ThirstDescription", out m_thirstButton, Color.LightGray, GetThirstButtonText);

			// --- NUEVO BOTÓN: coordenadas con color normal (blanco) ---
			CreateOptionRow("ShittyCreatures", "CoordinateDisplay", out m_coordinateButton, Color.LightGray, GetCoordinateButtonText);
			// ----------------------------------------------------------
		}

		private void CreateOptionRow(string category, string descriptionKey, out BevelledButtonWidget button, Color buttonColor, Func<string> getButtonTextFunc)
		{
			var rowPanel = new UniformSpacingPanelWidget
			{
				Direction = LayoutDirection.Horizontal,
				Margin = new Vector2(0f, 8f)
			};

			var descriptionLabel = new LabelWidget
			{
				Text = LanguageControl.Get(new string[] { category, descriptionKey }),
				HorizontalAlignment = WidgetAlignment.Far,
				VerticalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(20f, 0f),
				Size = new Vector2(600f, -1f),
				WordWrap = true
			};

			button = new BevelledButtonWidget
			{
				Size = new Vector2(310f, 60f),
				BevelColor = buttonColor,
				CenterColor = buttonColor,
				Name = $"Button_{descriptionKey}",
				VerticalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(20f, 0f),
				Text = getButtonTextFunc()
			};

			rowPanel.Children.Add(descriptionLabel);
			rowPanel.Children.Add(button);
			m_contentPanel.Children.Add(rowPanel);
		}

		private string GetGhostButtonText() => ShittyCreaturesSettingsManager.GhostMusicEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetTankButtonText() => ShittyCreaturesSettingsManager.TankMusicEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetSpawnButtonText() => ShittyCreaturesSettingsManager.DeathSpawnEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetThirstButtonText() => ShittyCreaturesSettingsManager.ThirstEnabled ? LanguageControl.On : LanguageControl.Off;
		// --- NUEVO MÉTODO ---
		private string GetCoordinateButtonText() => ShittyCreaturesSettingsManager.CoordinateDisplayEnabled ? LanguageControl.On : LanguageControl.Off;
		// --------------------

		public override void Update()
		{
			if (base.Input.Back || base.Input.Cancel || this.Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
			{
				ShittyCreaturesSettingsManager.Save();
				ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
				return;
			}

			if (m_ghostButton != null && m_ghostButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.GhostMusicEnabled = !ShittyCreaturesSettingsManager.GhostMusicEnabled;
				m_ghostButton.Text = GetGhostButtonText();
			}

			if (m_tankButton != null && m_tankButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.TankMusicEnabled = !ShittyCreaturesSettingsManager.TankMusicEnabled;
				m_tankButton.Text = GetTankButtonText();
			}

			if (m_spawnButton != null && m_spawnButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.DeathSpawnEnabled = !ShittyCreaturesSettingsManager.DeathSpawnEnabled;
				m_spawnButton.Text = GetSpawnButtonText();
			}

			if (m_thirstButton != null && m_thirstButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.ThirstEnabled = !ShittyCreaturesSettingsManager.ThirstEnabled;
				m_thirstButton.Text = GetThirstButtonText();
			}

			// --- NUEVO MANEJADOR DE CLICK ---
			if (m_coordinateButton != null && m_coordinateButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.CoordinateDisplayEnabled = !ShittyCreaturesSettingsManager.CoordinateDisplayEnabled;
				m_coordinateButton.Text = GetCoordinateButtonText();
			}
			// --------------------------------
		}
	}
}
