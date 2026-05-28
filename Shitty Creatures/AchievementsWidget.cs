using System;
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
			{
				m_titleLabel.Text = LanguageControl.Get(fName, 0);
			}
			if (m_closeButton != null)
			{
				m_closeButton.Text = LanguageControl.Get(fName, 1);
			}

			var achievements = new[]
			{
		new { Number = 1, TitleKey = 6, DescKey = 7, Reward = 50 },
		new { Number = 2, TitleKey = 8, DescKey = 9, Reward = 25 },
		new { Number = 3, TitleKey = 10, DescKey = 11, Reward = 30 },
		new { Number = 4, TitleKey = 12, DescKey = 13, Reward = 30 },
		new { Number = 5, TitleKey = 14, DescKey = 15, Reward = 30 },
		new { Number = 6, TitleKey = 16, DescKey = 17, Reward = 50 },
		new { Number = 7, TitleKey = 18, DescKey = 19, Reward = 150 },
		new { Number = 8, TitleKey = 20, DescKey = 21, Reward = 100 },
		new { Number = 9,  TitleKey = 22, DescKey = 23, Reward = 10 },
	new { Number = 10, TitleKey = 24, DescKey = 25, Reward = 20 },
	new { Number = 11, TitleKey = 26, DescKey = 27, Reward = 50 },
	new { Number = 12, TitleKey = 28, DescKey = 29, Reward = 100 },
	new { Number = 13, TitleKey = 30, DescKey = 31, Reward = 150 },
	new { Number = 14, TitleKey = 32, DescKey = 33, Reward = 500 },
	};

			foreach (var a in achievements)
			{
				CreateAchievementItem(
					title: LanguageControl.Get(fName, a.TitleKey),
					description: LanguageControl.Get(fName, a.DescKey),
					achievementNumber: a.Number,
					rewardAmount: a.Reward,
					unlocked: AchievementsManager.IsAchievementUnlocked(m_componentPlayer, a.Number),
					rewardClaimed: AchievementsManager.IsRewardClaimed(m_componentPlayer, a.Number)
				);
			}
		}

		private void CreateAchievementItem(string title, string description, int achievementNumber, int rewardAmount, bool unlocked, bool rewardClaimed)
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

			var descLabel = new LabelWidget
			{
				Text = description,
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

			// Determinar si el botón debe estar habilitado
			bool buttonEnabled = unlocked && !rewardClaimed;

			// Colores: verde/verde desaturado cuando está desbloqueado, gris cuando NO está desbloqueado
			// Cuando está desbloqueado pero la recompensa ya fue reclamada -> verde desaturado pero deshabilitado
			Color buttonColor;
			Color bevelColor;
			Color centerColor;

			if (unlocked)
			{
				// Logro completado: colores verdes
				buttonColor = rewardClaimed ? new Color(100, 100, 100) : Color.White; // Texto blanco o gris claro
				bevelColor = new Color(0, 80, 0);    // Verde oscuro
				centerColor = new Color(0, 60, 0);   // Verde más oscuro
			}
			else
			{
				// Logro pendiente: colores grises
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
		}

		public override void Update()
		{
			if (m_closeButton.IsClicked)
			{
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

									// Reproducir sonido de éxito
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
	}
}
