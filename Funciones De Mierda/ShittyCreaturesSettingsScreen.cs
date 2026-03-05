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
		private BevelledButtonWidget m_spawnButton; // Nuevo botón
		private StackPanelWidget m_contentPanel;
		private LabelWidget m_titleLabel;

		public ShittyCreaturesSettingsScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/ShittyCreaturesSettingsScreen");
			this.LoadContents(this, node);

			m_titleLabel = this.Children.Find<LabelWidget>("TopBar.Label", true);
			m_titleLabel.Text = LanguageControl.Get(new string[] { "ShittyCreatures", "ScreenTitle" });

			m_contentPanel = this.Children.Find<StackPanelWidget>("Content", true);
			m_contentPanel.Direction = LayoutDirection.Vertical;
			m_contentPanel.HorizontalAlignment = WidgetAlignment.Near;
			m_contentPanel.Margin = new Vector2(20f, 10f);

			// Botón Ghost
			m_ghostButton = new BevelledButtonWidget
			{
				Text = GetGhostButtonText(),
				Size = new Vector2(310f, 60f),
				BevelColor = Color.Gray,
				CenterColor = Color.Gray,
				Name = "GhostMusicButton",
				HorizontalAlignment = WidgetAlignment.Far
			};

			// Botón Tank
			m_tankButton = new BevelledButtonWidget
			{
				Text = GetTankButtonText(),
				Size = new Vector2(310f, 60f),
				BevelColor = Color.Red,
				CenterColor = Color.Red,
				Name = "TankMusicButton",
				HorizontalAlignment = WidgetAlignment.Far
			};

			// Nuevo botón Spawn (verde claro)
			m_spawnButton = new BevelledButtonWidget
			{
				Text = GetSpawnButtonText(),
				Size = new Vector2(310f, 60f),
				BevelColor = Color.LightGreen,
				CenterColor = Color.LightGreen,
				Name = "DeathSpawnButton",
				HorizontalAlignment = WidgetAlignment.Far
			};

			// Crear filas
			CreateOptionRow("ShittyCreatures", "GhostDescription", m_ghostButton);
			CreateOptionRow("ShittyCreatures", "TankDescription", m_tankButton);
			CreateOptionRow("ShittyCreatures", "SpawnDescription", m_spawnButton); // Nueva fila
		}

		private void CreateOptionRow(string category, string descriptionKey, BevelledButtonWidget button)
		{
			var rowContainer = new CanvasWidget
			{
				Size = new Vector2(float.PositiveInfinity, 70f),
				Margin = new Vector2(0f, 5f)
			};

			var descLabel = new LabelWidget
			{
				Text = LanguageControl.Get(new string[] { category, descriptionKey }),
				FontScale = 0.7f,
				Color = new Color(180, 180, 180),
				HorizontalAlignment = WidgetAlignment.Near,
				VerticalAlignment = WidgetAlignment.Center,
				WordWrap = true
			};

			rowContainer.Children.Add(descLabel);
			rowContainer.Children.Add(button);
			m_contentPanel.Children.Add(rowContainer);
		}

		private string GetGhostButtonText()
		{
			string onOff = ShittyCreaturesSettingsManager.GhostMusicEnabled ? LanguageControl.On : LanguageControl.Off;
			string template = LanguageControl.Get(new string[] { "ChaseMusic", "GhostButton" });
			return string.Format(template, onOff);
		}

		private string GetTankButtonText()
		{
			string onOff = ShittyCreaturesSettingsManager.TankMusicEnabled ? LanguageControl.On : LanguageControl.Off;
			string template = LanguageControl.Get(new string[] { "ChaseMusic", "TankButton" });
			return string.Format(template, onOff);
		}

		// Nuevo método para el botón de spawn
		private string GetSpawnButtonText()
		{
			string onOff = ShittyCreaturesSettingsManager.DeathSpawnEnabled ? LanguageControl.On : LanguageControl.Off;
			string template = LanguageControl.Get(new string[] { "ShittyCreatures", "SpawnButton" });
			return string.Format(template, onOff);
		}

		public override void Update()
		{
			if (base.Input.Back || base.Input.Cancel || this.Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
			{
				ShittyCreaturesSettingsManager.Save();
				ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
				return;
			}

			if (m_ghostButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.GhostMusicEnabled = !ShittyCreaturesSettingsManager.GhostMusicEnabled;
				m_ghostButton.Text = GetGhostButtonText();
			}

			if (m_tankButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.TankMusicEnabled = !ShittyCreaturesSettingsManager.TankMusicEnabled;
				m_tankButton.Text = GetTankButtonText();
			}

			// Nuevo: manejar clic en botón de spawn
			if (m_spawnButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.DeathSpawnEnabled = !ShittyCreaturesSettingsManager.DeathSpawnEnabled;
				m_spawnButton.Text = GetSpawnButtonText();
			}
		}
	}
}
