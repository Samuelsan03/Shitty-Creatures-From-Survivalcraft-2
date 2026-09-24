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
		private BevelledButtonWidget m_fastMeleeButton;
		private BevelledButtonWidget m_musicButton;
		private BevelledButtonWidget m_deathMusicButton;
		private LabelWidget m_titleLabel;

		public ShittyCreaturesSettingsScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/ShittyCreaturesSettingsScreen");
			this.LoadContents(this, node);

			m_titleLabel = this.Children.Find<LabelWidget>("TopBar.Label", true);
			m_titleLabel.Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "ScreenTitle" });

			m_ghostButton = this.Children.Find<BevelledButtonWidget>("GhostButton", true);
			this.Children.Find<LabelWidget>("GhostDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "GhostDescription" });
			m_ghostButton.Text = GetGhostButtonText();

			m_tankButton = this.Children.Find<BevelledButtonWidget>("TankButton", true);
			this.Children.Find<LabelWidget>("TankDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "TankDescription" });
			m_tankButton.Text = GetTankButtonText();

			m_spawnButton = this.Children.Find<BevelledButtonWidget>("SpawnButton", true);
			this.Children.Find<LabelWidget>("SpawnDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "SpawnDescription" });
			m_spawnButton.Text = GetSpawnButtonText();

			m_thirstButton = this.Children.Find<BevelledButtonWidget>("ThirstButton", true);
			this.Children.Find<LabelWidget>("ThirstDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "ThirstDescription" });
			m_thirstButton.Text = GetThirstButtonText();

			m_coordinateButton = this.Children.Find<BevelledButtonWidget>("CoordinateButton", true);
			this.Children.Find<LabelWidget>("CoordinateDisplayLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "CoordinateDisplay" });
			m_coordinateButton.Text = GetCoordinateButtonText();

			m_punchCommandButton = this.Children.Find<BevelledButtonWidget>("PunchCommandButton", true);
			this.Children.Find<LabelWidget>("PunchCommandDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "PunchCommandDescription" });
			m_punchCommandButton.Text = GetPunchCommandButtonText();

			m_creativeDefenseButton = this.Children.Find<BevelledButtonWidget>("CreativeDefenseButton", true);
			this.Children.Find<LabelWidget>("CreativeDefenseDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "CreativeDefenseDescription" });
			m_creativeDefenseButton.Text = GetCreativeDefenseButtonText();

			m_freeCameraButton = this.Children.Find<BevelledButtonWidget>("FreeCameraButton", true);
			this.Children.Find<LabelWidget>("FreeCameraDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "FreeCameraDescription" });
			m_freeCameraButton.Text = GetFreeCameraButtonText();

			m_bleedingButton = this.Children.Find<BevelledButtonWidget>("BleedingButton", true);
			this.Children.Find<LabelWidget>("BleedingDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "BleedingDescription" });
			m_bleedingButton.Text = GetBleedingButtonText();

			m_healthBarButton = this.Children.Find<BevelledButtonWidget>("HealthBarButton", true);
			this.Children.Find<LabelWidget>("HealthBarDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "HealthBarDescription" });
			m_healthBarButton.Text = GetHealthBarButtonText();

			m_skeletonSpawnButton = this.Children.Find<BevelledButtonWidget>("SkeletonSpawnButton", true);
			this.Children.Find<LabelWidget>("SkeletonSpawnDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "SkeletonSpawnDescription" });
			m_skeletonSpawnButton.Text = GetSkeletonSpawnButtonText();

			m_spiderSpawnButton = this.Children.Find<BevelledButtonWidget>("SpiderSpawnButton", true);
			this.Children.Find<LabelWidget>("SpiderSpawnDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "SpiderSpawnDescription" });
			m_spiderSpawnButton.Text = GetSpiderSpawnButtonText();

			m_fastMeleeButton = this.Children.Find<BevelledButtonWidget>("FastMeleeButton", true);
			this.Children.Find<LabelWidget>("FastMeleeDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "FastMeleeDescription" });
			m_fastMeleeButton.Text = GetFastMeleeButtonText();

			m_musicButton = this.Children.Find<BevelledButtonWidget>("MusicButton", true);
			this.Children.Find<LabelWidget>("InGameMusicDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "InGameMusicDescription" });
			m_musicButton.Text = GetMusicButtonText();

			m_deathMusicButton = this.Children.Find<BevelledButtonWidget>("DeathMusicButton", true);
			this.Children.Find<LabelWidget>("DeathMusicDescriptionLabel", true).Text = LanguageControl.Get(new string[] { "ShittyCreaturesSettings", "DeathMusicDescription" });
			m_deathMusicButton.Text = GetDeathMusicButtonText();
		}

		private string GetGhostButtonText() => ShittyCreaturesSettingsManager.GhostMusicEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetTankButtonText() => ShittyCreaturesSettingsManager.TankMusicEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetSpawnButtonText() => ShittyCreaturesSettingsManager.DeathSpawnEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetThirstButtonText() => ShittyCreaturesSettingsManager.ThirstEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetCoordinateButtonText() => ShittyCreaturesSettingsManager.CoordinateDisplayEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetPunchCommandButtonText() => ShittyCreaturesSettingsManager.PunchCommandEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetCreativeDefenseButtonText() => ShittyCreaturesSettingsManager.CreativeDefenseEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetFreeCameraButtonText() => ShittyCreaturesSettingsManager.FreeCameraEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetBleedingButtonText() => ShittyCreaturesSettingsManager.BleedingEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetHealthBarButtonText() => ShittyCreaturesSettingsManager.HealthBarEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetSkeletonSpawnButtonText() => ShittyCreaturesSettingsManager.SkeletonSpawnEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetSpiderSpawnButtonText() => ShittyCreaturesSettingsManager.SpiderSpawnEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetFastMeleeButtonText() => ShittyCreaturesSettingsManager.FastMeleeEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetMusicButtonText() => ShittyCreaturesSettingsManager.InGameMusicButtonEnabled ? LanguageControl.On : LanguageControl.Off;
		private string GetDeathMusicButtonText() => ShittyCreaturesSettingsManager.DeathMusicEnabled ? LanguageControl.On : LanguageControl.Off;

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
			if (m_healthBarButton != null && m_healthBarButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.HealthBarEnabled = !ShittyCreaturesSettingsManager.HealthBarEnabled;
				m_healthBarButton.Text = GetHealthBarButtonText();
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
			if (m_fastMeleeButton != null && m_fastMeleeButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.FastMeleeEnabled = !ShittyCreaturesSettingsManager.FastMeleeEnabled;
				m_fastMeleeButton.Text = GetFastMeleeButtonText();
			}
			if (m_musicButton != null && m_musicButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.InGameMusicButtonEnabled = !ShittyCreaturesSettingsManager.InGameMusicButtonEnabled;
				m_musicButton.Text = GetMusicButtonText();
			}
			if (m_deathMusicButton != null && m_deathMusicButton.IsClicked)
			{
				ShittyCreaturesSettingsManager.DeathMusicEnabled = !ShittyCreaturesSettingsManager.DeathMusicEnabled;
				m_deathMusicButton.Text = GetDeathMusicButtonText();
			}
		}
	}
}
