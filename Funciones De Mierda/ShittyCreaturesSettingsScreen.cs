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
		private StackPanelWidget m_contentPanel;
		private LabelWidget m_titleLabel;

		public ShittyCreaturesSettingsScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/ShittyCreaturesSettingsScreen");
			this.LoadContents(this, node);

			m_titleLabel = this.Children.Find<LabelWidget>("TopBar.Label", true);
			m_titleLabel.Text = LanguageControl.Get(new string[] { "ShittyCreatures", "ScreenTitle" });

			m_contentPanel = this.Children.Find<StackPanelWidget>("Content", true);

			// Crear las filas con ancho suficiente para las descripciones largas
			CreateOptionRow("ShittyCreatures", "GhostDescription", out m_ghostButton, Color.Gray, GetGhostButtonText);
			CreateOptionRow("ShittyCreatures", "TankDescription", out m_tankButton, Color.Red, GetTankButtonText);
			CreateOptionRow("ShittyCreatures", "SpawnDescription", out m_spawnButton, Color.LightGreen, GetSpawnButtonText);
		}

		private void CreateOptionRow(string category, string descriptionKey, out BevelledButtonWidget button, Color buttonColor, Func<string> getButtonTextFunc)
		{
			// Panel horizontal con separación vertical
			var rowPanel = new UniformSpacingPanelWidget
			{
				Direction = LayoutDirection.Horizontal,
				Margin = new Vector2(0f, 8f) // Espacio entre filas
			};

			// Etiqueta descriptiva
			var descriptionLabel = new LabelWidget
			{
				Text = LanguageControl.Get(new string[] { category, descriptionKey }),
				HorizontalAlignment = WidgetAlignment.Far,
				VerticalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(20f, 0f),
				Size = new Vector2(600f, -1f), // Suficiente espacio para la descripción
				WordWrap = true
			};

			// Botón con color personalizado y texto SIMPLE (Enabled/Disabled)
			button = new BevelledButtonWidget
			{
				Size = new Vector2(310f, 60f),
				BevelColor = buttonColor,
				CenterColor = buttonColor,
				Name = $"Button_{descriptionKey}",
				VerticalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(20f, 0f),
				Text = getButtonTextFunc() // Ahora devuelve solo "Enabled" o "Disabled"
			};

			rowPanel.Children.Add(descriptionLabel);
			rowPanel.Children.Add(button);
			m_contentPanel.Children.Add(rowPanel);
		}

		private string GetGhostButtonText()
		{
			// Solo devuelve "Enabled" o "Disabled" (traducido)
			return ShittyCreaturesSettingsManager.GhostMusicEnabled ? LanguageControl.On : LanguageControl.Off;
		}

		private string GetTankButtonText()
		{
			return ShittyCreaturesSettingsManager.TankMusicEnabled ? LanguageControl.On : LanguageControl.Off;
		}

		private string GetSpawnButtonText()
		{
			return ShittyCreaturesSettingsManager.DeathSpawnEnabled ? LanguageControl.On : LanguageControl.Off;
		}

		public override void Update()
		{
			// Botón de retroceso
			if (base.Input.Back || base.Input.Cancel || this.Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
			{
				ShittyCreaturesSettingsManager.Save();
				ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
				return;
			}

			// Manejar clics en los botones
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
		}
	}
}
