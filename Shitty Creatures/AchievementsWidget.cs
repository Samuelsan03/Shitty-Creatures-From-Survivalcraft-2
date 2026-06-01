using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class AchievementsWidget : CanvasWidget
	{
		private ComponentPlayer m_componentPlayer;
		private BevelledButtonWidget m_closeButton;
		private StackPanelWidget m_achievementsStack;
		private LabelWidget m_titleLabel;
		private BevelledRectangleWidget m_originalBackground;
		private ImageWidgetSimple m_backgroundImageWidget;
		private PaintButtonWidget m_paintButton;
		private int m_backgroundState = 0; // 0=original, 1=textura found, 2=semitransparente

		// Guardar colores originales del fondo para restaurarlos después del modo semitransparente
		private Color m_originalCenterColor;
		private Color m_originalBevelColor;

		private Dictionary<int, AchievementItemData> m_achievementItems = new Dictionary<int, AchievementItemData>();

		public static string fName = "AchievementsWidget";

		public AchievementsWidget(ComponentPlayer player)
		{
			m_componentPlayer = player;
			XElement node = ContentManager.Get<XElement>("Widgets/AchievementsWidget");
			LoadContents(this, node);
			m_closeButton = Children.Find<BevelledButtonWidget>("CloseButton", true);
			m_achievementsStack = Children.Find<StackPanelWidget>("AchievementsStack", true);
			m_titleLabel = Children.Find<LabelWidget>("TitleLabel", true);

			// Obtener el fondo original por su nombre y guardar sus colores originales
			m_originalBackground = Children.Find<BevelledRectangleWidget>("Background", true);
			if (m_originalBackground != null)
			{
				m_originalCenterColor = m_originalBackground.CenterColor;
				m_originalBevelColor = m_originalBackground.BevelColor;
			}
			else
			{
				Log.Error("[AchievementsWidget] No se encontró el fondo con nombre 'Background'");
			}

			// Crear el widget de imagen para el fondo alternativo
			m_backgroundImageWidget = new ImageWidgetSimple
			{
				IsVisible = false,
				HorizontalAlignment = WidgetAlignment.Stretch,
				VerticalAlignment = WidgetAlignment.Stretch,
				Margin = Vector2.Zero
			};
			// Insertarlo detrás de todos los demás hijos (índice 0)
			Children.Insert(0, m_backgroundImageWidget);

			// Crear el botón de pintura
			Subtexture paintTexture = ContentManager.Get<Subtexture>("Textures/Gui/pintura");
			if (paintTexture != null)
			{
				m_paintButton = new PaintButtonWidget
				{
					Subtexture = paintTexture,
					Size = new Vector2(32, 32),
					HorizontalAlignment = WidgetAlignment.Far,
					VerticalAlignment = WidgetAlignment.Near,
					MarginLeft = 0,
					MarginTop = 10,
					MarginRight = 15,
					MarginBottom = 0,
					SoundName = "Audio/Click"
				};
				Children.Add(m_paintButton);
			}
			else
			{
				Log.Error("[AchievementsWidget] No se encontró la textura 'Textures/Gui/pintura'");
			}

			if (m_titleLabel != null)
				m_titleLabel.Text = LanguageControl.Get(fName, 0);
			if (m_closeButton != null)
				m_closeButton.Text = LanguageControl.Get(fName, 1);

			// Suscribirse a eventos
			AchievementsManager.OnInfectedCounterChanged += OnInfectedCounterChanged;
			AchievementsManager.OnBossCounterChanged += OnBossCounterChanged;
			AchievementsManager.OnTankCounterChanged += OnTankCounterChanged;
			AchievementsManager.OnGhostCounterChanged += OnGhostCounterChanged;
			AchievementsManager.OnGhostTankCounterChanged += OnGhostTankCounterChanged;
			AchievementsManager.OnBanditCounterChanged += OnBanditCounterChanged;
			AchievementsManager.OnHealCounterChanged += OnHealCounterChanged;
			AchievementsManager.OnPirateCounterChanged += OnPirateCounterChanged;
			AchievementsManager.OnFlyingCounterChanged += OnFlyingCounterChanged;
			AchievementsManager.OnBoomerCounterChanged += OnBoomerCounterChanged;

			// Cargar logros
			XElement achievementsXml = ContentManager.Get<XElement>("AchievementsData");
			if (achievementsXml == null)
			{
				Log.Error("[AchievementsWidget] No se pudo cargar AchievementsData.xml");
				return;
			}

			foreach (XElement elem in achievementsXml.Elements("Achievement"))
			{
				int number = (int)elem.Attribute("Number");
				int titleKey = (int)elem.Attribute("TitleKey");
				int descKey = (int)elem.Attribute("DescriptionKey");
				int reward = (int)elem.Attribute("Reward");

				string title = LanguageControl.Get(fName, titleKey);
				string description = LanguageControl.Get(fName, descKey);

				CreateAchievementItem(
					title: title,
					baseDescription: description,
					achievementNumber: number,
					rewardAmount: reward,
					unlocked: AchievementsManager.IsAchievementUnlocked(m_componentPlayer, number),
					rewardClaimed: AchievementsManager.IsRewardClaimed(m_componentPlayer, number)
				);
			}

			// Restaurar estado guardado
			var subsystemAchievements = m_componentPlayer.Project.FindSubsystem<SubsystemAchievements>(true);
			if (subsystemAchievements != null)
			{
				m_backgroundState = subsystemAchievements.GetBackgroundState();
				ApplyBackgroundState();
			}
		}

		private void ChangeBackground()
		{
			m_backgroundState = (m_backgroundState + 1) % 3;

			// Guardar el nuevo estado en el subsistema
			var subsystemAchievements = m_componentPlayer?.Project?.FindSubsystem<SubsystemAchievements>(true);
			if (subsystemAchievements != null)
				subsystemAchievements.SetBackgroundState(m_backgroundState);

			ApplyBackgroundState();
		}

		private void ApplyBackgroundState()
		{
			switch (m_backgroundState)
			{
				case 0: // Original
					if (m_originalBackground != null)
					{
						m_originalBackground.IsVisible = true;
						m_originalBackground.CenterColor = m_originalCenterColor;
						m_originalBackground.BevelColor = m_originalBevelColor;
					}
					m_backgroundImageWidget.IsVisible = false;
					break;
				case 1: // Textura "found"
					if (m_originalBackground != null)
						m_originalBackground.IsVisible = false;
					m_backgroundImageWidget.IsVisible = true;
					Texture2D foundTexture = ContentManager.Get<Texture2D>("Textures/Wallpapers/found");
					if (foundTexture != null)
						m_backgroundImageWidget.Texture = foundTexture;
					else
						Log.Error("[AchievementsWidget] No se encontró 'Textures/Wallpapers/found'");
					break;
				case 2: // Semitransparente
					if (m_originalBackground != null)
					{
						m_originalBackground.IsVisible = true;
						m_originalBackground.CenterColor = new Color(0, 0, 0, 80);
						m_originalBackground.BevelColor = new Color(0, 0, 0, 80);
					}
					m_backgroundImageWidget.IsVisible = false;
					break;
			}
		}

		private void UpdateCounterDescription(int achievementNumber, int currentKills, int target, string baseDescription, LabelWidget descLabel)
		{
			if (descLabel == null) return;
			int displayKills = Math.Min(currentKills, target);
			descLabel.Text = $"{baseDescription} ({displayKills}/{target})";
		}

		// Infectados: logros 16, 17, 18 (progresivos)
		private void OnInfectedCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;

			if (m_achievementItems.TryGetValue(16, out var item16) && !AchievementsManager.IsAchievementUnlocked(player, 16))
				UpdateCounterDescription(16, currentKills, 10, item16.BaseDescription, item16.DescriptionLabel);
			if (m_achievementItems.TryGetValue(17, out var item17) && !AchievementsManager.IsAchievementUnlocked(player, 17))
				UpdateCounterDescription(17, currentKills, 50, item17.BaseDescription, item17.DescriptionLabel);
			if (m_achievementItems.TryGetValue(18, out var item18) && !AchievementsManager.IsAchievementUnlocked(player, 18))
				UpdateCounterDescription(18, currentKills, 100, item18.BaseDescription, item18.DescriptionLabel);
		}

		// Jefes: logros 19, 20, 21 (progresivos)
		private void OnBossCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;

			if (m_achievementItems.TryGetValue(19, out var item19) && !AchievementsManager.IsAchievementUnlocked(player, 19))
				UpdateCounterDescription(19, currentKills, 10, item19.BaseDescription, item19.DescriptionLabel);
			if (m_achievementItems.TryGetValue(20, out var item20) && !AchievementsManager.IsAchievementUnlocked(player, 20))
				UpdateCounterDescription(20, currentKills, 50, item20.BaseDescription, item20.DescriptionLabel);
			if (m_achievementItems.TryGetValue(21, out var item21) && !AchievementsManager.IsAchievementUnlocked(player, 21))
				UpdateCounterDescription(21, currentKills, 100, item21.BaseDescription, item21.DescriptionLabel);
		}

		// Tanks: logros 22, 23, 24 (progresivos)
		private void OnTankCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;

			if (m_achievementItems.TryGetValue(22, out var item22) && !AchievementsManager.IsAchievementUnlocked(player, 22))
				UpdateCounterDescription(22, currentKills, 10, item22.BaseDescription, item22.DescriptionLabel);
			if (m_achievementItems.TryGetValue(23, out var item23) && !AchievementsManager.IsAchievementUnlocked(player, 23))
				UpdateCounterDescription(23, currentKills, 50, item23.BaseDescription, item23.DescriptionLabel);
			if (m_achievementItems.TryGetValue(24, out var item24) && !AchievementsManager.IsAchievementUnlocked(player, 24))
				UpdateCounterDescription(24, currentKills, 100, item24.BaseDescription, item24.DescriptionLabel);
		}

		// Fantasmas: logros 25, 26, 27 (progresivos)
		private void OnGhostCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;

			if (m_achievementItems.TryGetValue(25, out var item25) && !AchievementsManager.IsAchievementUnlocked(player, 25))
				UpdateCounterDescription(25, currentKills, 10, item25.BaseDescription, item25.DescriptionLabel);
			if (m_achievementItems.TryGetValue(26, out var item26) && !AchievementsManager.IsAchievementUnlocked(player, 26))
				UpdateCounterDescription(26, currentKills, 50, item26.BaseDescription, item26.DescriptionLabel);
			if (m_achievementItems.TryGetValue(27, out var item27) && !AchievementsManager.IsAchievementUnlocked(player, 27))
				UpdateCounterDescription(27, currentKills, 100, item27.BaseDescription, item27.DescriptionLabel);
		}

		// Tanks Fantasmas: logros 28, 29, 30 (progresivos)
		private void OnGhostTankCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;

			if (m_achievementItems.TryGetValue(28, out var item28) && !AchievementsManager.IsAchievementUnlocked(player, 28))
				UpdateCounterDescription(28, currentKills, 10, item28.BaseDescription, item28.DescriptionLabel);
			if (m_achievementItems.TryGetValue(29, out var item29) && !AchievementsManager.IsAchievementUnlocked(player, 29))
				UpdateCounterDescription(29, currentKills, 50, item29.BaseDescription, item29.DescriptionLabel);
			if (m_achievementItems.TryGetValue(30, out var item30) && !AchievementsManager.IsAchievementUnlocked(player, 30))
				UpdateCounterDescription(30, currentKills, 100, item30.BaseDescription, item30.DescriptionLabel);
		}

		// Bandidos/Narcotraficantes: logros 31, 32, 33 (progresivos)
		private void OnBanditCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;

			if (m_achievementItems.TryGetValue(31, out var item31) && !AchievementsManager.IsAchievementUnlocked(player, 31))
				UpdateCounterDescription(31, currentKills, 10, item31.BaseDescription, item31.DescriptionLabel);
			if (m_achievementItems.TryGetValue(32, out var item32) && !AchievementsManager.IsAchievementUnlocked(player, 32))
				UpdateCounterDescription(32, currentKills, 50, item32.BaseDescription, item32.DescriptionLabel);
			if (m_achievementItems.TryGetValue(33, out var item33) && !AchievementsManager.IsAchievementUnlocked(player, 33))
				UpdateCounterDescription(33, currentKills, 100, item33.BaseDescription, item33.DescriptionLabel);
		}

		// Curaciones: logros 34, 35, 36 (progresivos)
		private void OnHealCounterChanged(ComponentPlayer player, int currentHeals, int targetHeals)
		{
			if (player != m_componentPlayer) return;

			if (m_achievementItems.TryGetValue(34, out var item34) && !AchievementsManager.IsAchievementUnlocked(player, 34))
				UpdateCounterDescription(34, currentHeals, 10, item34.BaseDescription, item34.DescriptionLabel);
			if (m_achievementItems.TryGetValue(35, out var item35) && !AchievementsManager.IsAchievementUnlocked(player, 35))
				UpdateCounterDescription(35, currentHeals, 50, item35.BaseDescription, item35.DescriptionLabel);
			if (m_achievementItems.TryGetValue(36, out var item36) && !AchievementsManager.IsAchievementUnlocked(player, 36))
				UpdateCounterDescription(36, currentHeals, 100, item36.BaseDescription, item36.DescriptionLabel);
		}

		private void OnPirateCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			if (m_achievementItems.TryGetValue(38, out var item38) && !AchievementsManager.IsAchievementUnlocked(player, 38))
				UpdateCounterDescription(38, currentKills, 10, item38.BaseDescription, item38.DescriptionLabel);
			if (m_achievementItems.TryGetValue(39, out var item39) && !AchievementsManager.IsAchievementUnlocked(player, 39))
				UpdateCounterDescription(39, currentKills, 50, item39.BaseDescription, item39.DescriptionLabel);
			if (m_achievementItems.TryGetValue(40, out var item40) && !AchievementsManager.IsAchievementUnlocked(player, 40))
				UpdateCounterDescription(40, currentKills, 100, item40.BaseDescription, item40.DescriptionLabel);
		}

		private void OnFlyingCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;

			if (m_achievementItems.TryGetValue(44, out var item44) && !AchievementsManager.IsAchievementUnlocked(player, 44))
				UpdateCounterDescription(44, currentKills, 10, item44.BaseDescription, item44.DescriptionLabel);
			if (m_achievementItems.TryGetValue(45, out var item45) && !AchievementsManager.IsAchievementUnlocked(player, 45))
				UpdateCounterDescription(45, currentKills, 25, item45.BaseDescription, item45.DescriptionLabel);
			if (m_achievementItems.TryGetValue(46, out var item46) && !AchievementsManager.IsAchievementUnlocked(player, 46))
				UpdateCounterDescription(46, currentKills, 50, item46.BaseDescription, item46.DescriptionLabel);
			if (m_achievementItems.TryGetValue(47, out var item47) && !AchievementsManager.IsAchievementUnlocked(player, 47))
				UpdateCounterDescription(47, currentKills, 100, item47.BaseDescription, item47.DescriptionLabel);
		}

		private void OnBoomerCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;

			if (m_achievementItems.TryGetValue(48, out var item48) && !AchievementsManager.IsAchievementUnlocked(player, 48))
				UpdateCounterDescription(48, currentKills, 10, item48.BaseDescription, item48.DescriptionLabel);
			if (m_achievementItems.TryGetValue(49, out var item49) && !AchievementsManager.IsAchievementUnlocked(player, 49))
				UpdateCounterDescription(49, currentKills, 25, item49.BaseDescription, item49.DescriptionLabel);
			if (m_achievementItems.TryGetValue(50, out var item50) && !AchievementsManager.IsAchievementUnlocked(player, 50))
				UpdateCounterDescription(50, currentKills, 55, item50.BaseDescription, item50.DescriptionLabel);
			if (m_achievementItems.TryGetValue(51, out var item51) && !AchievementsManager.IsAchievementUnlocked(player, 51))
				UpdateCounterDescription(51, currentKills, 100, item51.BaseDescription, item51.DescriptionLabel);
		}

		private void CreateAchievementItem(string title, string baseDescription, int achievementNumber, int rewardAmount, bool unlocked, bool rewardClaimed)
		{
			var achievementContainer = new CanvasWidget
			{
				Size = new Vector2(530, 95),
				Margin = new Vector2(0, 3)
			};

			var background = new BevelledRectangleWidget
			{
				Size = new Vector2(530, 95),
				BevelSize = 2,
				CenterColor = unlocked ? new Color(0, 40, 0, 200) : new Color(40, 40, 40, 200),
				BevelColor = unlocked ? new Color(0, 100, 0) : new Color(80, 80, 80)
			};
			achievementContainer.Children.Add(background);

			var statusLabel = new LabelWidget
			{
				Text = unlocked ? LanguageControl.Get(fName, 2) : LanguageControl.Get(fName, 3),
				Color = unlocked ? Color.Green : Color.Red,
				FontScale = 0.7f,
				HorizontalAlignment = WidgetAlignment.Near,
				VerticalAlignment = WidgetAlignment.Near,
				Margin = new Vector2(10, 5)
			};
			achievementContainer.Children.Add(statusLabel);

			var titleLabel = new LabelWidget
			{
				Text = title,
				Color = unlocked ? Color.White : new Color(180, 180, 180),
				FontScale = 0.9f,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Near,
				Margin = new Vector2(0, 5)
			};
			achievementContainer.Children.Add(titleLabel);

			string finalDescription = baseDescription;
			if (!unlocked)
			{
				int currentKills = 0;
				int target = 0;

				switch (achievementNumber)
				{
					case 16: currentKills = AchievementsManager.GetInfectedKills(m_componentPlayer); target = 10; break;
					case 17: currentKills = AchievementsManager.GetInfectedKills(m_componentPlayer); target = 50; break;
					case 18: currentKills = AchievementsManager.GetInfectedKills(m_componentPlayer); target = 100; break;
					case 19: currentKills = AchievementsManager.GetBossKills(m_componentPlayer); target = 10; break;
					case 20: currentKills = AchievementsManager.GetBossKills(m_componentPlayer); target = 50; break;
					case 21: currentKills = AchievementsManager.GetBossKills(m_componentPlayer); target = 100; break;
					case 22: currentKills = AchievementsManager.GetTankKills(m_componentPlayer); target = 10; break;
					case 23: currentKills = AchievementsManager.GetTankKills(m_componentPlayer); target = 50; break;
					case 24: currentKills = AchievementsManager.GetTankKills(m_componentPlayer); target = 100; break;
					case 25: currentKills = AchievementsManager.GetGhostKills(m_componentPlayer); target = 10; break;
					case 26: currentKills = AchievementsManager.GetGhostKills(m_componentPlayer); target = 50; break;
					case 27: currentKills = AchievementsManager.GetGhostKills(m_componentPlayer); target = 100; break;
					case 28: currentKills = AchievementsManager.GetGhostTankKills(m_componentPlayer); target = 10; break;
					case 29: currentKills = AchievementsManager.GetGhostTankKills(m_componentPlayer); target = 50; break;
					case 30: currentKills = AchievementsManager.GetGhostTankKills(m_componentPlayer); target = 100; break;
					case 31: currentKills = AchievementsManager.GetBanditKills(m_componentPlayer); target = 10; break;
					case 32: currentKills = AchievementsManager.GetBanditKills(m_componentPlayer); target = 50; break;
					case 33: currentKills = AchievementsManager.GetBanditKills(m_componentPlayer); target = 100; break;
					case 34: currentKills = AchievementsManager.GetHeals(m_componentPlayer); target = 10; break;
					case 35: currentKills = AchievementsManager.GetHeals(m_componentPlayer); target = 50; break;
					case 36: currentKills = AchievementsManager.GetHeals(m_componentPlayer); target = 100; break;
					case 38: currentKills = AchievementsManager.GetPirateKills(m_componentPlayer); target = 10; break;
					case 39: currentKills = AchievementsManager.GetPirateKills(m_componentPlayer); target = 50; break;
					case 40: currentKills = AchievementsManager.GetPirateKills(m_componentPlayer); target = 100; break;
					case 44: currentKills = AchievementsManager.GetFlyingKills(m_componentPlayer); target = 10; break;
					case 45: currentKills = AchievementsManager.GetFlyingKills(m_componentPlayer); target = 25; break;
					case 46: currentKills = AchievementsManager.GetFlyingKills(m_componentPlayer); target = 50; break;
					case 47: currentKills = AchievementsManager.GetFlyingKills(m_componentPlayer); target = 100; break;
					case 48: currentKills = AchievementsManager.GetBoomerKills(m_componentPlayer); target = 10; break;
					case 49: currentKills = AchievementsManager.GetBoomerKills(m_componentPlayer); target = 25; break;
					case 50: currentKills = AchievementsManager.GetBoomerKills(m_componentPlayer); target = 55; break;
					case 51: currentKills = AchievementsManager.GetBoomerKills(m_componentPlayer); target = 100; break;
				}

				if (target > 0)
				{
					int displayKills = Math.Min(currentKills, target);
					finalDescription = $"{baseDescription} ({displayKills}/{target})";
				}
			}

			var descLabel = new LabelWidget
			{
				Text = finalDescription,
				Color = unlocked ? new Color(200, 200, 200) : new Color(140, 140, 140),
				FontScale = 0.65f,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0, 0)
			};
			achievementContainer.Children.Add(descLabel);

			var bottomRow = new StackPanelWidget
			{
				Direction = LayoutDirection.Horizontal,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Far,
				Margin = new Vector2(0, 5)
			};
			achievementContainer.Children.Add(bottomRow);

			Block nuclearCoinBlock = BlocksManager.GetBlock<NuclearCoinBlock>(false, false);
			int coinValue = nuclearCoinBlock != null ? nuclearCoinBlock.BlockIndex : 490;
			BlockIconWidget coinIcon = new BlockIconWidget
			{
				Value = coinValue,
				Size = new Vector2(20, 20),
				Margin = new Vector2(0, 0),
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center
			};
			bottomRow.Children.Add(coinIcon);

			var rewardLabel = new LabelWidget
			{
				Text = string.Format(LanguageControl.Get(fName, 4), rewardAmount),
				Color = unlocked ? new Color(255, 215, 0) : new Color(150, 150, 150),
				FontScale = 0.7f,
				Margin = new Vector2(5, 0),
				VerticalAlignment = WidgetAlignment.Center
			};
			bottomRow.Children.Add(rewardLabel);

			bool buttonEnabled = unlocked && !rewardClaimed;
			Color buttonColor, bevelColor, centerColor;

			if (unlocked)
			{
				buttonColor = rewardClaimed ? new Color(100, 100, 100) : Color.White;
				bevelColor = new Color(0, 80, 0);
				centerColor = new Color(0, 60, 0);
			}
			else
			{
				buttonColor = Color.Gray;
				bevelColor = new Color(80, 80, 80);
				centerColor = new Color(60, 60, 60);
			}

			var claimButton = new BevelledButtonWidget
			{
				Name = $"ClaimButton_{achievementNumber}",
				Text = LanguageControl.Get(fName, 5),
				Size = new Vector2(95, 35),
				FontScale = 0.75f,
				Margin = new Vector2(15, 0),
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				IsEnabled = buttonEnabled,
				Color = buttonColor,
				BevelColor = bevelColor,
				CenterColor = centerColor
			};
			bottomRow.Children.Add(claimButton);

			claimButton.Tag = new AchievementButtonData
			{
				AchievementNumber = achievementNumber,
				RewardAmount = rewardAmount,
				ClaimButton = claimButton
			};

			m_achievementsStack.Children.Add(achievementContainer);

			m_achievementItems[achievementNumber] = new AchievementItemData
			{
				Container = achievementContainer,
				DescriptionLabel = descLabel,
				BaseDescription = baseDescription
			};
		}

		public override void Update()
		{
			if (m_closeButton.IsClicked)
			{
				AchievementsManager.OnInfectedCounterChanged -= OnInfectedCounterChanged;
				AchievementsManager.OnBossCounterChanged -= OnBossCounterChanged;
				AchievementsManager.OnTankCounterChanged -= OnTankCounterChanged;
				AchievementsManager.OnGhostCounterChanged -= OnGhostCounterChanged;
				AchievementsManager.OnGhostTankCounterChanged -= OnGhostTankCounterChanged;
				AchievementsManager.OnBanditCounterChanged -= OnBanditCounterChanged;
				AchievementsManager.OnHealCounterChanged -= OnHealCounterChanged;
				AchievementsManager.OnPirateCounterChanged -= OnPirateCounterChanged;
				AchievementsManager.OnFlyingCounterChanged -= OnFlyingCounterChanged;
				AchievementsManager.OnBoomerCounterChanged -= OnBoomerCounterChanged;
				m_componentPlayer.ComponentGui.ModalPanelWidget = null;
				return;
			}

			// Detectar clic en el botón de pintura
			if (m_paintButton != null && m_paintButton.IsClicked)
			{
				ChangeBackground();
			}

			foreach (var child in m_achievementsStack.Children)
			{
				if (child is CanvasWidget container)
				{
					var bottomRow = container.Children.Find<StackPanelWidget>(null, false);
					if (bottomRow != null)
					{
						var claimButton = bottomRow.Children.Find<BevelledButtonWidget>(null, false);
						if (claimButton != null && claimButton.Tag is AchievementButtonData data)
						{
							if (claimButton.IsClicked && claimButton.IsEnabled)
							{
								bool success = AchievementsManager.ClaimAchievementReward(
									m_componentPlayer,
									data.AchievementNumber,
									data.RewardAmount
								);
								if (success)
								{
									claimButton.IsEnabled = false;
									claimButton.Color = Color.Gray;
									m_componentPlayer.ComponentGui.DisplaySmallMessage(
										LanguageControl.Get("AchievementsMessages", 3),
										Color.Green, false, true);

									var audio = m_componentPlayer.Project.FindSubsystem<SubsystemAudio>(true);
									if (audio != null)
									{
										audio.PlaySound("Audio/UI/success", 1f, 0f, m_componentPlayer.ComponentBody.Position, 10f, false);
									}
								}
							}
						}
					}
				}
			}
		}

		private class AchievementButtonData
		{
			public int AchievementNumber;
			public int RewardAmount;
			public BevelledButtonWidget ClaimButton;
		}

		private class AchievementItemData
		{
			public CanvasWidget Container;
			public LabelWidget DescriptionLabel;
			public string BaseDescription;
		}

		// Widget simple para mostrar una textura como fondo
		private class ImageWidgetSimple : Widget
		{
			public Texture2D Texture;
			public override void Draw(Widget.DrawContext dc)
			{
				if (Texture != null)
				{
					TexturedBatch2D batch = dc.PrimitivesRenderer2D.TexturedBatch(Texture, false, 0, DepthStencilState.None, null, BlendState.AlphaBlend, SamplerState.PointClamp);
					int count = batch.TriangleVertices.Count;
					batch.QueueQuad(Vector2.Zero, base.ActualSize, 0f, Vector2.Zero, Vector2.One, base.GlobalColorTransform);
					batch.TransformTriangles(base.GlobalTransform, count, -1);
				}
			}
			public override void MeasureOverride(Vector2 parentAvailableSize)
			{
				base.IsDrawRequired = true;
			}
		}

		// Botón de imagen simple que hereda de ClickableWidget
		private class PaintButtonWidget : ClickableWidget
		{
			public Subtexture Subtexture;
			private Vector2 m_size;
			public Vector2 Size
			{
				get => m_size;
				set { m_size = value; }
			}

			public override void MeasureOverride(Vector2 parentAvailableSize)
			{
				base.DesiredSize = m_size;
				base.IsDrawRequired = true;
				base.IsHitTestVisible = true;
			}

			public override void Draw(Widget.DrawContext dc)
			{
				if (Subtexture != null && Subtexture.Texture != null)
				{
					TexturedBatch2D batch = dc.PrimitivesRenderer2D.TexturedBatch(Subtexture.Texture, false, 0, DepthStencilState.None, null, BlendState.AlphaBlend, SamplerState.PointClamp);
					int count = batch.TriangleVertices.Count;
					Vector2 texCoord1 = Subtexture.TopLeft;
					Vector2 texCoord2 = Subtexture.BottomRight;
					batch.QueueQuad(Vector2.Zero, m_size, 0f, texCoord1, texCoord2, base.GlobalColorTransform);
					batch.TransformTriangles(base.GlobalTransform, count, -1);
				}
			}
		}
	}
}
