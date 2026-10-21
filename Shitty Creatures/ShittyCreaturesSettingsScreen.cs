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
		private BevelledButtonWidget m_coordinateButton;
		private BevelledButtonWidget m_punchCommandButton;
		private BevelledButtonWidget m_creativeDefenseButton;
		private BevelledButtonWidget m_freeCameraButton;
		private BevelledButtonWidget m_bleedingButton;
		private BevelledButtonWidget m_healthBarButton;
		private BevelledButtonWidget m_skeletonSpawnButton;
		private BevelledButtonWidget m_spiderSpawnButton;
		private BevelledButtonWidget m_musicButton;
		private BevelledButtonWidget m_deathMusicButton;
		private LabelWidget m_titleLabel;

		public ShittyCreaturesSettingsScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/ShittyCreaturesSettingsScreen");
			this.LoadContents(this, node);

			m_titleLabel = this.Children.Find<LabelWidget>("TopBar.Label", true);
			m_titleLabel.Text = LanguageControl.Get("ShittyCreaturesSettings", "ScreenTitle");

			m_ghostButton = this.Children.Find<BevelledButtonWidget>("GhostButton", true);
			this.Children.Find<LabelWidget>("GhostDescriptionLabel", true).Text = LanguageControl.Get("ShittyCreaturesSettings", "GhostMusic", "Description");
			m_ghostButton.Text = GetGhostButtonText();

			m_tankButton = this.Children.Find<BevelledButtonWidget>("TankButton", true);
			this.Children.Find<LabelWidget>("TankDescriptionLabel", true).Text = LanguageControl.Get("ShittyCreaturesSettings", "TankMusic", "Description");
			m_tankButton.Text = GetTankButtonText();

			m_spawnButton = this.Children.Find<BevelledButtonWidget>("SpawnButton", true);
			this.Children.Find<LabelWidget>("SpawnDescriptionLabel", true).Text = LanguageControl.Get("ShittyCreaturesSettings", "DeathSpawn", "Description");
			m_spawnButton.Text = GetSpawnButtonText();

			m_thirstButton = this.Children.Find<BevelledButtonWidget>("ThirstButton", true);
			this.Children.Find<LabelWidget>("ThirstDescriptionLabel", true).Text = LanguageControl.Get("ShittyCreaturesSettings", "Thirst", "Description");
			m_thirstButton.Text = GetThirstButtonText();

			m_coordinateButton = this.Children.Find<BevelledButtonWidget>("CoordinateButton", true);
			this.Children.Find<LabelWidget>("CoordinateDisplayLabel", true).Text = LanguageControl.Get("ShittyCreaturesSettings", "CoordinateDisplay", "Description");
			m_coordinateButton.Text = GetCoordinateButtonText();

			m_punchCommandButton = this.Children.Find<BevelledButtonWidget>("PunchCommandButton", true);
			this.Children.Find<LabelWidget>("PunchCommandDescriptionLabel", true).Text = LanguageControl.Get("ShittyCreaturesSettings", "PunchCommand", "Description");
			m_punchCommandButton.Text = GetPunchCommandButtonText();

			m_creativeDefenseButton = this.Children.Find<BevelledButtonWidget>("CreativeDefenseButton", true);
			this.Children.Find<LabelWidget>("CreativeDefenseDescriptionLabel", true).Text = LanguageControl.Get("ShittyCreaturesSettings", "CreativeDefense", "Description");
			m_creativeDefenseButton.Text = GetCreativeDefenseButtonText();

			m_freeCameraButton = this.Children.Find<BevelledButtonWidget>("FreeCameraButton", true);
			this.Children.Find<LabelWidget>("FreeCameraDescriptionLabel", true).Text = LanguageControl.Get("ShittyCreaturesSettings", "FreeCamera", "Description");
			m_freeCameraButton.Text = GetFreeCameraButtonText();

			m_bleedingButton = this.Children.Find<BevelledButtonWidget>("BleedingButton", true);
			this.Children.Find<LabelWidget>("BleedingDescriptionLabel", true).Text = LanguageControl.Get("ShittyCreaturesSettings", "Bleeding", "Description");
			m_bleedingButton.Text = GetBleedingButtonText();

			m_skeletonSpawnButton = this.Children.Find<BevelledButtonWidget>("SkeletonSpawnButton", true);
			this.Children.Find<LabelWidget>("SkeletonSpawnDescriptionLabel", true).Text = LanguageControl.Get("ShittyCreaturesSettings", "SkeletonSpawn", "Description");
			m_skeletonSpawnButton.Text = GetSkeletonSpawnButtonText();

			m_spiderSpawnButton = this.Children.Find<BevelledButtonWidget>("SpiderSpawnButton", true);
			this.Children.Find<LabelWidget>("SpiderSpawnDescriptionLabel", true).Text = LanguageControl.Get("ShittyCreaturesSettings", "SpiderSpawn", "Description");
			m_spiderSpawnButton.Text = GetSpiderSpawnButtonText();

			m_deathMusicButton = this.Children.Find<BevelledButtonWidget>("DeathMusicButton", true);
			this.Children.Find<LabelWidget>("DeathMusicDescriptionLabel", true).Text = LanguageControl.Get("ShittyCreaturesSettings", "DeathMusic", "Description");
			m_deathMusicButton.Text = GetDeathMusicButtonText();
		}

		private string GetGhostButtonText() =>
	ShittyCreaturesSettingsManager.GhostMusicEnabled
		? LanguageControl.Get("ShittyCreaturesSettings", "GhostMusic", "EnabledText")
		: LanguageControl.Get("ShittyCreaturesSettings", "GhostMusic", "DisabledText");

		private string GetTankButtonText() =>
			ShittyCreaturesSettingsManager.TankMusicEnabled
				? LanguageControl.Get("ShittyCreaturesSettings", "TankMusic", "EnabledText")
				: LanguageControl.Get("ShittyCreaturesSettings", "TankMusic", "DisabledText");

		private string GetSpawnButtonText() =>
			ShittyCreaturesSettingsManager.DeathSpawnEnabled
				? LanguageControl.Get("ShittyCreaturesSettings", "DeathSpawn", "EnabledText")
				: LanguageControl.Get("ShittyCreaturesSettings", "DeathSpawn", "DisabledText");

		private string GetThirstButtonText() =>
			ShittyCreaturesSettingsManager.ThirstEnabled
				? LanguageControl.Get("ShittyCreaturesSettings", "Thirst", "EnabledText")
				: LanguageControl.Get("ShittyCreaturesSettings", "Thirst", "DisabledText");

		private string GetCoordinateButtonText() =>
			ShittyCreaturesSettingsManager.CoordinateDisplayEnabled
				? LanguageControl.Get("ShittyCreaturesSettings", "CoordinateDisplay", "EnabledText")
				: LanguageControl.Get("ShittyCreaturesSettings", "CoordinateDisplay", "DisabledText");

		private string GetPunchCommandButtonText() =>
			ShittyCreaturesSettingsManager.PunchCommandEnabled
				? LanguageControl.Get("ShittyCreaturesSettings", "PunchCommand", "EnabledText")
				: LanguageControl.Get("ShittyCreaturesSettings", "PunchCommand", "DisabledText");

		private string GetCreativeDefenseButtonText() =>
			ShittyCreaturesSettingsManager.CreativeDefenseEnabled
				? LanguageControl.Get("ShittyCreaturesSettings", "CreativeDefense", "EnabledText")
				: LanguageControl.Get("ShittyCreaturesSettings", "CreativeDefense", "DisabledText");

		private string GetFreeCameraButtonText() =>
			ShittyCreaturesSettingsManager.FreeCameraEnabled
				? LanguageControl.Get("ShittyCreaturesSettings", "FreeCamera", "EnabledText")
				: LanguageControl.Get("ShittyCreaturesSettings", "FreeCamera", "DisabledText");

		private string GetBleedingButtonText() =>
			ShittyCreaturesSettingsManager.BleedingEnabled
				? LanguageControl.Get("ShittyCreaturesSettings", "Bleeding", "EnabledText")
				: LanguageControl.Get("ShittyCreaturesSettings", "Bleeding", "DisabledText");

		private string GetSkeletonSpawnButtonText() =>
			ShittyCreaturesSettingsManager.SkeletonSpawnEnabled
				? LanguageControl.Get("ShittyCreaturesSettings", "SkeletonSpawn", "EnabledText")
				: LanguageControl.Get("ShittyCreaturesSettings", "SkeletonSpawn", "DisabledText");

		private string GetSpiderSpawnButtonText() =>
			ShittyCreaturesSettingsManager.SpiderSpawnEnabled
				? LanguageControl.Get("ShittyCreaturesSettings", "SpiderSpawn", "EnabledText")
				: LanguageControl.Get("ShittyCreaturesSettings", "SpiderSpawn", "DisabledText");

		private string GetDeathMusicButtonText() =>
			ShittyCreaturesSettingsManager.DeathMusicEnabled
				? LanguageControl.Get("ShittyCreaturesSettings", "DeathMusic", "EnabledText")
				: LanguageControl.Get("ShittyCreaturesSettings", "DeathMusic", "DisabledText");

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

			if (m_coordinateButton != null && m_coordinateButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.CoordinateDisplayEnabled = !ShittyCreaturesSettingsManager.CoordinateDisplayEnabled;
				m_coordinateButton.Text = GetCoordinateButtonText();
			}

			if (m_punchCommandButton != null && m_punchCommandButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.PunchCommandEnabled = !ShittyCreaturesSettingsManager.PunchCommandEnabled;
				m_punchCommandButton.Text = GetPunchCommandButtonText();
			}

			if (m_creativeDefenseButton != null && m_creativeDefenseButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.CreativeDefenseEnabled = !ShittyCreaturesSettingsManager.CreativeDefenseEnabled;
				m_creativeDefenseButton.Text = GetCreativeDefenseButtonText();
			}

			if (m_freeCameraButton != null && m_freeCameraButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.FreeCameraEnabled = !ShittyCreaturesSettingsManager.FreeCameraEnabled;
				m_freeCameraButton.Text = GetFreeCameraButtonText();
			}
			if (m_bleedingButton != null && m_bleedingButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.BleedingEnabled = !ShittyCreaturesSettingsManager.BleedingEnabled;
				m_bleedingButton.Text = GetBleedingButtonText();
			}
			if (m_skeletonSpawnButton != null && m_skeletonSpawnButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.SkeletonSpawnEnabled = !ShittyCreaturesSettingsManager.SkeletonSpawnEnabled;
				m_skeletonSpawnButton.Text = GetSkeletonSpawnButtonText();
			}
			if (m_spiderSpawnButton != null && m_spiderSpawnButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.SpiderSpawnEnabled = !ShittyCreaturesSettingsManager.SpiderSpawnEnabled;
				m_spiderSpawnButton.Text = GetSpiderSpawnButtonText();
			}
			if (m_deathMusicButton != null && m_deathMusicButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.DeathMusicEnabled = !ShittyCreaturesSettingsManager.DeathMusicEnabled;
				m_deathMusicButton.Text = GetDeathMusicButtonText();
			}
		}
	}
}
