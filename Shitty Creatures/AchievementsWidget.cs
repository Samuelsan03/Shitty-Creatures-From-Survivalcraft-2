using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using Game;

namespace Game
{
	public class AchievementsWidget : CanvasWidget
	{
		private ComponentPlayer m_componentPlayer;
		private BevelledButtonWidget m_closeButton;
		private StackPanelWidget m_achievementsStack;
		private LabelWidget m_titleLabel;

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

			if (m_titleLabel != null)
				m_titleLabel.Text = LanguageControl.Get(fName, 0);
			if (m_closeButton != null)
				m_closeButton.Text = LanguageControl.Get(fName, 1);

			// Suscribirse a todos los eventos
			AchievementsManager.OnInfectedCounterChanged += OnInfectedCounterChanged;
			AchievementsManager.OnBossCounterChanged += OnBossCounterChanged;
			AchievementsManager.OnTankCounterChanged += OnTankCounterChanged;
			AchievementsManager.OnGhostCounterChanged += OnGhostCounterChanged;
			AchievementsManager.OnGhostTankCounterChanged += OnGhostTankCounterChanged;
			AchievementsManager.OnBanditCounterChanged += OnBanditCounterChanged;

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

			// Descripción dinámica SOLO para logros progresivos (16-33, excepto los base 1-15 individuales)
			string finalDescription = baseDescription;
			if (!unlocked)
			{
				int currentKills = 0;
				int target = 0;

				// Solo los logros progresivos (10, 50, 100) tienen contador
				switch (achievementNumber)
				{
					// Infectados
					case 16: currentKills = AchievementsManager.GetInfectedKills(m_componentPlayer); target = 10; break;
					case 17: currentKills = AchievementsManager.GetInfectedKills(m_componentPlayer); target = 50; break;
					case 18: currentKills = AchievementsManager.GetInfectedKills(m_componentPlayer); target = 100; break;
					// Jefes
					case 19: currentKills = AchievementsManager.GetBossKills(m_componentPlayer); target = 10; break;
					case 20: currentKills = AchievementsManager.GetBossKills(m_componentPlayer); target = 50; break;
					case 21: currentKills = AchievementsManager.GetBossKills(m_componentPlayer); target = 100; break;
					// Tanks
					case 22: currentKills = AchievementsManager.GetTankKills(m_componentPlayer); target = 10; break;
					case 23: currentKills = AchievementsManager.GetTankKills(m_componentPlayer); target = 50; break;
					case 24: currentKills = AchievementsManager.GetTankKills(m_componentPlayer); target = 100; break;
					// Fantasmas
					case 25: currentKills = AchievementsManager.GetGhostKills(m_componentPlayer); target = 10; break;
					case 26: currentKills = AchievementsManager.GetGhostKills(m_componentPlayer); target = 50; break;
					case 27: currentKills = AchievementsManager.GetGhostKills(m_componentPlayer); target = 100; break;
					// Tanks Fantasmas
					case 28: currentKills = AchievementsManager.GetGhostTankKills(m_componentPlayer); target = 10; break;
					case 29: currentKills = AchievementsManager.GetGhostTankKills(m_componentPlayer); target = 50; break;
					case 30: currentKills = AchievementsManager.GetGhostTankKills(m_componentPlayer); target = 100; break;
					// Bandidos
					case 31: currentKills = AchievementsManager.GetBanditKills(m_componentPlayer); target = 10; break;
					case 32: currentKills = AchievementsManager.GetBanditKills(m_componentPlayer); target = 50; break;
					case 33: currentKills = AchievementsManager.GetBanditKills(m_componentPlayer); target = 100; break;
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
				m_componentPlayer.ComponentGui.ModalPanelWidget = null;
				return;
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
										LanguageControl.Get("AchievementsMessages", 1),
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
	}
}
