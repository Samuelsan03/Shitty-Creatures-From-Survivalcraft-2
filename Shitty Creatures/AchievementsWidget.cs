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

		public AchievementsWidget(ComponentPlayer player)
		{
			m_componentPlayer = player;
			XElement node = ContentManager.Get<XElement>("Widgets/AchievementsWidget");
			LoadContents(this, node);
			m_closeButton = Children.Find<BevelledButtonWidget>("CloseButton", true);
			m_achievementsStack = Children.Find<StackPanelWidget>("AchievementsStack", true);

			bool unlocked = ShittyCreaturesModLoader.IsAchievementUnlocked(m_componentPlayer, 1);
			bool rewardClaimed = ShittyCreaturesModLoader.IsRewardClaimed(m_componentPlayer, 1);
			CreateAchievementItem(
				title: "Mata un Tank",
				description: "Elimina un Tank con cualquier arma (cuerpo a cuerpo o distancia)",
				achievementNumber: 1,
				rewardAmount: 50,
				unlocked: unlocked,
				rewardClaimed: rewardClaimed
			);
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

			// Estado (esquina superior izquierda)
			var statusLabel = new LabelWidget
			{
				Text = unlocked ? "COMPLETADO" : "PENDIENTE",
				Color = unlocked ? Color.Green : Color.Red,
				FontScale = 0.7f,
				HorizontalAlignment = WidgetAlignment.Near,
				VerticalAlignment = WidgetAlignment.Near,
				Margin = new Vector2(10, 5)
			};
			achievementContainer.Children.Add(statusLabel);

			// 1. Título (centrado, arriba)
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

			// 2. Descripción (centrada, debajo del título)
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

			// 3. Fila recompensa + botón (abajo)
			var bottomRow = new StackPanelWidget
			{
				Direction = LayoutDirection.Horizontal,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Far,
				Margin = new Vector2(0, 5)
			};
			achievementContainer.Children.Add(bottomRow);

			// Icono de moneda
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

			// Texto recompensa
			var rewardLabel = new LabelWidget
			{
				Text = $"Recompensa: {rewardAmount} monedas nucleares",
				Color = unlocked ? new Color(255, 215, 0) : new Color(150, 150, 150),
				FontScale = 0.7f,
				Margin = new Vector2(5, 0),
				VerticalAlignment = WidgetAlignment.Center
			};
			bottomRow.Children.Add(rewardLabel);

			// Botón Reclamar
			var claimButton = new BevelledButtonWidget
			{
				Name = $"ClaimButton_{achievementNumber}",
				Text = "Reclamar",
				Size = new Vector2(95, 25),  // Botón más pequeño
				FontScale = 0.75f,            // Texto más pequeño dentro del botón
				Margin = new Vector2(15, 0),
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				IsEnabled = unlocked && !rewardClaimed,
				Color = unlocked && !rewardClaimed ? Color.White : Color.Gray,
				BevelColor = unlocked && !rewardClaimed ? new Color(0, 100, 0) : new Color(80, 80, 80),
				CenterColor = unlocked && !rewardClaimed ? new Color(0, 80, 0) : new Color(60, 60, 60)
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
								bool success = ShittyCreaturesModLoader.ClaimAchievementReward(
									m_componentPlayer,
									data.AchievementNumber,
									data.RewardAmount
								);
								if (success)
								{
									claimButton.IsEnabled = false;
									claimButton.Color = Color.Gray;
									m_componentPlayer.ComponentGui.DisplaySmallMessage("¡Recompensa reclamada!", Color.Green, false, true);
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
