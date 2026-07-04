using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using Engine.Media;
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
		private int m_backgroundState = 0;

		private Color m_originalCenterColor;
		private Color m_originalBevelColor;

		// Agregar campos en la clase:
		private List<AchievementSortData> m_sortData = new List<AchievementSortData>();
		private bool m_needsReorder = false;

		private Dictionary<int, AchievementItemData> m_achievementItems = new Dictionary<int, AchievementItemData>();

		private float m_bgTransitionFactor = 1f;
		private int m_pendingBgState = -1;
		private bool m_isBgTransitioning = false;

		// Nuevos indicadores
		private LabelWidget m_progressLabel;
		private LabelWidget m_unlockedLabel;
		private int m_totalAchievements = 0;

		public static string fName = "AchievementsWidget";

		public AchievementsWidget(ComponentPlayer player)
		{
			m_componentPlayer = player;
			XElement node = ContentManager.Get<XElement>("Widgets/AchievementsWidget");
			LoadContents(this, node);
			m_closeButton = Children.Find<BevelledButtonWidget>("CloseButton", true);
			m_achievementsStack = Children.Find<StackPanelWidget>("AchievementsStack", true);
			m_titleLabel = Children.Find<LabelWidget>("TitleLabel", true);

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

			m_backgroundImageWidget = new ImageWidgetSimple
			{
				IsVisible = false,
				HorizontalAlignment = WidgetAlignment.Stretch,
				VerticalAlignment = WidgetAlignment.Stretch,
				Margin = Vector2.Zero
			};
			Children.Insert(0, m_backgroundImageWidget);

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

			// Suscribirse al evento de desbloqueo global
			SubsystemAchievements.AchievementUnlocked += OnAnyAchievementUnlocked;

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
			AchievementsManager.OnNormalTameCounterChanged += OnNormalTameCounterChanged;
			AchievementsManager.OnBossTameCounterChanged += OnBossTameCounterChanged;
			AchievementsManager.OnGhostTameCounterChanged += OnGhostTameCounterChanged;

			XElement achievementsXml = ContentManager.Get<XElement>("AchievementsData");
			if (achievementsXml == null)
			{
				Log.Error("[AchievementsWidget] No se pudo cargar AchievementsData.xml");
				return;
			}

			// Contar logros totales
			m_totalAchievements = 0;
			foreach (XElement elem in achievementsXml.Elements("Achievement"))
			{
				m_totalAchievements++;
				int number = (int)elem.Attribute("AchievementNumber");
				int titleKey = (int)elem.Attribute("TitleKey");
				int descKey = (int)elem.Attribute("DescriptionKey");
				int reward = (int)elem.Attribute("Reward");
				string typeOfAchievement = elem.Attribute("TypeOfAchievement")?.Value ?? "Combat";

				bool hidden = false;
				XAttribute hiddenAttr = elem.Attribute("Hidden");
				if (hiddenAttr != null && hiddenAttr.Value == "true")
					hidden = true;

				bool isPremium = elem.Attribute("IsTheAchievementPremium")?.Value == "true";

				// LEER LOS NUEVOS ATRIBUTOS
				bool isCounter = elem.Attribute("IsCounter")?.Value == "true";
				int counterTarget = 0;
				if (isCounter)
				{
					XAttribute targetAttr = elem.Attribute("CounterTarget");
					if (targetAttr != null)
						int.TryParse(targetAttr.Value, out counterTarget);
				}

				bool unlocked = AchievementsManager.IsAchievementUnlocked(m_componentPlayer, number);

				if (hidden && !unlocked)
					continue;

				string title = LanguageControl.Get(fName, titleKey);
				string description = LanguageControl.Get(fName, descKey);

				CreateAchievementItem(
					title: title,
					baseDescription: description,
					achievementNumber: number,
					rewardAmount: reward,
					unlocked: unlocked,
					rewardClaimed: AchievementsManager.IsRewardClaimed(m_componentPlayer, number),
					typeOfAchievement: typeOfAchievement,
					isPremium: isPremium,
					isCounter: isCounter,
					counterTarget: counterTarget
				);
			}

			var subsystemAchievements = m_componentPlayer.Project.FindSubsystem<SubsystemAchievements>(true);
			if (subsystemAchievements != null && !subsystemAchievements.HasBaseOrder)
			{
				// Generar orden aleatorio base usando la semilla del mundo
				var worldSeed = m_componentPlayer.Project.FindSubsystem<SubsystemGameInfo>(true)?.WorldSeed ?? 0;
				var rng = new Random(worldSeed);
				var achievementNumbers = new List<int>(m_achievementItems.Keys);
				// Mezclar la lista (Fisher-Yates)
				for (int i = achievementNumbers.Count - 1; i > 0; i--)
				{
					int j = rng.Int(i + 1);
					int temp = achievementNumbers[i];
					achievementNumbers[i] = achievementNumbers[j];
					achievementNumbers[j] = temp;
				}
				// Construir diccionario de orden (índice)
				var baseOrder = new Dictionary<int, int>();
				for (int i = 0; i < achievementNumbers.Count; i++)
				{
					baseOrder[achievementNumbers[i]] = i;
				}
				subsystemAchievements.SetBaseOrder(baseOrder);

				// Reordenar los contenedores según el orden base
				var orderedContainers = new List<Widget>();
				foreach (int num in achievementNumbers)
				{
					if (m_achievementItems.TryGetValue(num, out var itemData))
						orderedContainers.Add(itemData.Container);
				}
				m_achievementsStack.Children.Clear();
				foreach (var container in orderedContainers)
					m_achievementsStack.Children.Add(container);
			}
			if (subsystemAchievements != null)
			{
				m_backgroundState = subsystemAchievements.GetBackgroundState();
				ApplyBackgroundState();
			}

			// ========== Crear los nuevos indicadores ==========
			m_progressLabel = new LabelWidget
			{
				Text = "",
				FontScale = 0.7f,
				Color = Color.White,
				HorizontalAlignment = WidgetAlignment.Near,
				VerticalAlignment = WidgetAlignment.Far,
				MarginLeft = 10,
				MarginBottom = 10
			};
			m_unlockedLabel = new LabelWidget
			{
				Text = "",
				FontScale = 0.7f,
				Color = Color.White,
				HorizontalAlignment = WidgetAlignment.Far,
				VerticalAlignment = WidgetAlignment.Far,
				MarginRight = 10,
				MarginBottom = 10
			};
			Children.Add(m_progressLabel);
			Children.Add(m_unlockedLabel);

			ReorderAchievements(); // Orden inicial basado en progreso actual
		}

		// Añadir handler:
		private void OnAnyAchievementUnlocked(int achievementNumber)
		{
			if (m_achievementItems.TryGetValue(achievementNumber, out var itemData))
			{
				if (itemData.ProgressBar != null)
					itemData.ProgressBar.Value = 1f;
				if (itemData.StatusLabel != null)
				{
					itemData.StatusLabel.Text = LanguageControl.Get(fName, 2);
					itemData.StatusLabel.Color = Color.Green;
				}
			}
			m_needsReorder = true;
		}

		private void ChangeBackground()
		{
			if (m_isBgTransitioning) return;

			m_pendingBgState = (m_backgroundState + 1) % 3;
			m_bgTransitionFactor = 0f;
			m_isBgTransitioning = true;
		}

		private void UpdateBackgroundTransition()
		{
			if (!m_isBgTransitioning) return;

			m_bgTransitionFactor += 6f * MathUtils.Min(Time.FrameDuration, 0.1f);

			if (m_bgTransitionFactor >= 0.5f && m_pendingBgState >= 0)
			{
				m_backgroundState = m_pendingBgState;

				var subsystemAchievements = m_componentPlayer?.Project?.FindSubsystem<SubsystemAchievements>(true);
				if (subsystemAchievements != null)
					subsystemAchievements.SetBackgroundState(m_backgroundState);

				ApplyBackgroundState();
				m_pendingBgState = -1;
			}

			if (m_bgTransitionFactor >= 1f)
			{
				m_bgTransitionFactor = 1f;
				m_isBgTransitioning = false;
				base.ColorTransform = Color.White;
				base.RenderTransform = Matrix.Identity;
				return;
			}

			float scale;
			if (m_bgTransitionFactor < 0.5f)
			{
				float t = m_bgTransitionFactor * 2f;
				scale = 0.5f + 0.5f * MathF.Pow(1f - t, 0.1f);
			}
			else
			{
				float t = (m_bgTransitionFactor - 0.5f) * 2f;
				scale = 0.5f + 0.5f * MathF.Pow(t, 0.1f);
			}

			Vector2 size = base.ActualSize;
			if (size.X > 0f && size.Y > 0f)
			{
				base.RenderTransform = Matrix.CreateTranslation((0f - size.X) / 2f, (0f - size.Y) / 2f, 0f)
									 * Matrix.CreateScale(scale, scale, 1f)
									 * Matrix.CreateTranslation(size.X / 2f, size.Y / 2f, 0f);
			}
		}

		private void ApplyBackgroundState()
		{
			switch (m_backgroundState)
			{
				case 0:
					if (m_originalBackground != null)
					{
						m_originalBackground.IsVisible = true;
						m_originalBackground.CenterColor = m_originalCenterColor;
						m_originalBackground.BevelColor = m_originalBevelColor;
					}
					m_backgroundImageWidget.IsVisible = false;
					break;
				case 1:
					if (m_originalBackground != null)
						m_originalBackground.IsVisible = false;
					m_backgroundImageWidget.IsVisible = true;
					Texture2D foundTexture = ContentManager.Get<Texture2D>("Textures/Wallpapers/found");
					if (foundTexture != null)
						m_backgroundImageWidget.Texture = foundTexture;
					else
						Log.Error("[AchievementsWidget] No se encontró 'Textures/Wallpapers/found'");
					break;
				case 2:
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
			if (m_componentPlayer?.Project == null) return;

			if (!m_achievementItems.TryGetValue(achievementNumber, out var itemData) || itemData.Container == null)
				return;

			if (itemData.TextStack == null) return;

			int displayKills = Math.Min(currentKills, target);
			string newText = $"{baseDescription} ({displayKills}/{target})";

			BitmapFont font = LabelWidget.BitmapFont;
			float fontScale = 0.65f;
			float maxWidth = 500f;
			List<string> newWrappedLines = WrapText(font, newText, maxWidth, fontScale);
			float lineHeight = font.LineHeight * fontScale * font.Scale;

			itemData.TextStack.Children.Clear();
			foreach (string line in newWrappedLines)
			{
				var lineLabel = new LabelWidget
				{
					Text = line,
					Color = itemData.Container.Children.Find<BevelledRectangleWidget>(null, false)?.CenterColor ?? new Color(200, 200, 200),
					FontScale = fontScale,
					HorizontalAlignment = WidgetAlignment.Center,
					Margin = new Vector2(0, 2)
				};
				itemData.TextStack.Children.Add(lineLabel);
			}

			if (itemData.Container.ParentWidget != null)
			{
				float statusHeight = 20f;
				float titleHeight = 25f;
				float textStackHeight = newWrappedLines.Count * lineHeight + (newWrappedLines.Count - 1) * 4f;
				float bottomRowHeight = 40f;
				float totalHeight = statusHeight + titleHeight + textStackHeight + bottomRowHeight + 20f;
				totalHeight = Math.Max(95f, totalHeight);

				itemData.Container.Size = new Vector2(530, totalHeight);
				var background = itemData.Container.Children.Find<BevelledRectangleWidget>(null, false);
				if (background != null)
					background.Size = new Vector2(530, totalHeight);
			}
		}

		private void OnInfectedCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			UpdateProgressBarAndStatus(16, currentKills, 10);
			UpdateProgressBarAndStatus(17, currentKills, 50);
			UpdateProgressBarAndStatus(18, currentKills, 100);
			m_needsReorder = true;
		}

		private void OnBossCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			UpdateProgressBarAndStatus(19, currentKills, 10);
			UpdateProgressBarAndStatus(20, currentKills, 50);
			UpdateProgressBarAndStatus(21, currentKills, 100);
			m_needsReorder = true;
		}

		private void OnTankCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			UpdateProgressBarAndStatus(22, currentKills, 10);
			UpdateProgressBarAndStatus(23, currentKills, 50);
			UpdateProgressBarAndStatus(24, currentKills, 100);
			m_needsReorder = true;
		}

		private void OnGhostCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			UpdateProgressBarAndStatus(25, currentKills, 10);
			UpdateProgressBarAndStatus(26, currentKills, 50);
			UpdateProgressBarAndStatus(27, currentKills, 100);
			m_needsReorder = true;
		}

		private void OnGhostTankCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			UpdateProgressBarAndStatus(28, currentKills, 10);
			UpdateProgressBarAndStatus(29, currentKills, 50);
			UpdateProgressBarAndStatus(30, currentKills, 100);
			m_needsReorder = true;
		}

		private void OnBanditCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			UpdateProgressBarAndStatus(31, currentKills, 10);
			UpdateProgressBarAndStatus(32, currentKills, 50);
			UpdateProgressBarAndStatus(33, currentKills, 100);
			m_needsReorder = true;
		}

		private void OnHealCounterChanged(ComponentPlayer player, int currentHeals, int targetHeals)
		{
			if (player != m_componentPlayer) return;
			UpdateProgressBarAndStatus(34, currentHeals, 10);
			UpdateProgressBarAndStatus(35, currentHeals, 50);
			UpdateProgressBarAndStatus(36, currentHeals, 100);
			m_needsReorder = true;
		}

		private void OnPirateCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			UpdateProgressBarAndStatus(38, currentKills, 10);
			UpdateProgressBarAndStatus(39, currentKills, 50);
			UpdateProgressBarAndStatus(40, currentKills, 100);
			m_needsReorder = true;
		}

		private void OnFlyingCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			UpdateProgressBarAndStatus(44, currentKills, 10);
			UpdateProgressBarAndStatus(45, currentKills, 25);
			UpdateProgressBarAndStatus(46, currentKills, 50);
			UpdateProgressBarAndStatus(47, currentKills, 100);
			m_needsReorder = true;
		}

		private void OnBoomerCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			UpdateProgressBarAndStatus(48, currentKills, 10);
			UpdateProgressBarAndStatus(49, currentKills, 25);
			UpdateProgressBarAndStatus(50, currentKills, 55);
			UpdateProgressBarAndStatus(51, currentKills, 100);
			m_needsReorder = true;
		}

		private void OnNormalTameCounterChanged(ComponentPlayer player, int current, int target)
		{
			if (player != m_componentPlayer) return;
			UpdateProgressBarAndStatus(57, current, 10);
			UpdateProgressBarAndStatus(58, current, 25);
			UpdateProgressBarAndStatus(59, current, 50);
			UpdateProgressBarAndStatus(60, current, 100);
			m_needsReorder = true;
		}

		private void OnBossTameCounterChanged(ComponentPlayer player, int current, int target)
		{
			if (player != m_componentPlayer) return;
			UpdateProgressBarAndStatus(61, current, 1);
			UpdateProgressBarAndStatus(62, current, 5);
			UpdateProgressBarAndStatus(63, current, 10);
			UpdateProgressBarAndStatus(64, current, 25);
			m_needsReorder = true;
		}

		private void OnGhostTameCounterChanged(ComponentPlayer player, int current, int target)
		{
			if (player != m_componentPlayer) return;
			UpdateProgressBarAndStatus(65, current, 5);
			UpdateProgressBarAndStatus(66, current, 10);
			UpdateProgressBarAndStatus(67, current, 25);
			UpdateProgressBarAndStatus(68, current, 50);
			m_needsReorder = true;
		}

		private void CreateAchievementItem(string title, string baseDescription, int achievementNumber, int rewardAmount, bool unlocked, bool rewardClaimed, string typeOfAchievement, bool isPremium, bool isCounter, int counterTarget)
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

			// --- FILA SUPERIOR: Estado (izquierda) ---
			var topRow = new CanvasWidget
			{
				Size = new Vector2(530, 25),
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Near,
				Margin = new Vector2(0, 2)
			};
			achievementContainer.Children.Add(topRow);

			// Estado
			string statusText = unlocked ? LanguageControl.Get(fName, 2) : LanguageControl.Get(fName, 3);
			Color statusColor = unlocked ? Color.Green : Color.Red;
			var statusLabel = new LabelWidget
			{
				Text = statusText,
				Color = statusColor,
				FontScale = 0.7f,
				HorizontalAlignment = WidgetAlignment.Near,
				VerticalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(10, 0)
			};
			topRow.Children.Add(statusLabel);

			// Título
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

			// --- CATEGORÍA (TypeOfAchievement) ---
			string typeText = LanguageControl.Get("TypesOfAchievements", 0) + ": " + LanguageControl.Get("TypesOfAchievements", GetTypeKey(typeOfAchievement));
			var categoryLabel = new LabelWidget
			{
				Text = typeText,
				Color = new Color(140, 140, 140),
				FontScale = 0.8f,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Near,
				Margin = new Vector2(0, 40)
			};
			achievementContainer.Children.Add(categoryLabel);

			// --- BARRA DE PROGRESO Y PORCENTAJE (SOLO LOGROS CON CONTADOR) ---
			bool hasProgressBar = isCounter && counterTarget > 0;
			ProgressBarWidget progressBar = null;
			LabelWidget percentLabel = null;
			int currentKills = 0;
			int target = counterTarget;

			if (hasProgressBar)
			{
				GetCounterValues(achievementNumber, out currentKills, out target);

				if (unlocked)
				{
					progressBar = new ProgressBarWidget
					{
						BarSize = new Vector2(250f, 14f),
						BackgroundColor = new Color(60, 60, 60),
						HorizontalAlignment = WidgetAlignment.Center,
						VerticalAlignment = WidgetAlignment.Center,
						Margin = new Vector2(0, 0),
						Value = 1f
					};
				}
				else
				{
					progressBar = new ProgressBarWidget
					{
						BarSize = new Vector2(250f, 14f),
						BackgroundColor = new Color(60, 60, 60),
						HorizontalAlignment = WidgetAlignment.Center,
						VerticalAlignment = WidgetAlignment.Center,
						Margin = new Vector2(0, 0),
						Value = target > 0 ? Math.Clamp((float)currentKills / target, 0f, 1f) : 0f
					};
				}
				achievementContainer.Children.Add(progressBar);
				CanvasWidget.SetPosition(progressBar, new Vector2(140f, 82f));

				// Porcentaje
				string percentText = unlocked ? "100%" : (target > 0 ? $"{Math.Min(currentKills, target)}%" : "0%");
				percentLabel = new LabelWidget
				{
					Text = percentText,
					Color = Color.White,
					FontScale = 0.65f,
					HorizontalAlignment = WidgetAlignment.Center,
					VerticalAlignment = WidgetAlignment.Center,
					Margin = new Vector2(0, 0)
				};
				achievementContainer.Children.Add(percentLabel);
				CanvasWidget.SetPosition(percentLabel, new Vector2(400f, 82f));
			}

			// Descripción
			string finalDescription = baseDescription;
			if (!unlocked && hasProgressBar && target > 0)
			{
				int displayKills = Math.Min(currentKills, target);
				finalDescription = $"{baseDescription} ({displayKills}/{target})";
			}

			var textStack = new StackPanelWidget
			{
				Direction = LayoutDirection.Vertical,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = hasProgressBar ? WidgetAlignment.Near : WidgetAlignment.Center,
				Margin = hasProgressBar ? new Vector2(15, 110) : new Vector2(15, 0)
			};

			BitmapFont font = LabelWidget.BitmapFont;
			float fontScale = 0.65f;
			float maxWidth = 500f;
			List<string> wrappedLines = WrapText(font, finalDescription, maxWidth, fontScale);
			float lineHeight = font.LineHeight * fontScale * font.Scale;

			foreach (string line in wrappedLines)
			{
				var lineLabel = new LabelWidget
				{
					Text = line,
					Color = unlocked ? new Color(200, 200, 200) : new Color(140, 140, 140),
					FontScale = fontScale,
					HorizontalAlignment = WidgetAlignment.Center,
					Margin = new Vector2(0, 2)
				};
				textStack.Children.Add(lineLabel);
			}
			achievementContainer.Children.Add(textStack);

			// --- FILA INFERIOR: Recompensa y botón ---
			var bottomRow = new StackPanelWidget
			{
				Direction = LayoutDirection.Horizontal,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Far,
				Margin = new Vector2(0, 8)
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

			// --- CÁLCULO DE ALTURA TOTAL ---
			float statusHeight = 25f;
			float titleHeight = 30f;
			float categoryHeight = 20f;
			float textStackHeight = wrappedLines.Count * lineHeight + (wrappedLines.Count - 1) * 4f;
			float bottomRowHeight = 40f;
			float progressBarHeight = hasProgressBar ? (14f + 8f) : 0f;
			float topMargin = hasProgressBar ? 110f : 60f;
			float totalHeight = topMargin + textStackHeight + bottomRowHeight + 20f;
			totalHeight = Math.Max(120f, totalHeight);

			achievementContainer.Size = new Vector2(530, totalHeight);
			background.Size = new Vector2(530, totalHeight);

			m_achievementsStack.Children.Add(achievementContainer);

			m_achievementItems[achievementNumber] = new AchievementItemData
			{
				Container = achievementContainer,
				DescriptionLabel = null,
				BaseDescription = baseDescription,
				TextStack = textStack,
				WrappedLines = wrappedLines,
				ClaimButton = claimButton,
				ProgressBar = progressBar,
				StatusLabel = statusLabel,
				PercentLabel = percentLabel,
				TitleLabel = titleLabel,
				IsPremium = isPremium,
				IsCounter = isCounter,
				CounterTarget = counterTarget
			};
		}

		private void GetCounterValues(int achievementNumber, out int current, out int target)
		{
			current = 0;
			target = 0;

			// Obtener target desde el XML si está disponible
			if (m_achievementItems.TryGetValue(achievementNumber, out var itemData) && itemData.IsCounter)
			{
				target = itemData.CounterTarget;
			}

			switch (achievementNumber)
			{
				case 16: current = AchievementsManager.GetInfectedKills(m_componentPlayer); break;
				case 17: current = AchievementsManager.GetInfectedKills(m_componentPlayer); break;
				case 18: current = AchievementsManager.GetInfectedKills(m_componentPlayer); break;
				case 19: current = AchievementsManager.GetBossKills(m_componentPlayer); break;
				case 20: current = AchievementsManager.GetBossKills(m_componentPlayer); break;
				case 21: current = AchievementsManager.GetBossKills(m_componentPlayer); break;
				case 22: current = AchievementsManager.GetTankKills(m_componentPlayer); break;
				case 23: current = AchievementsManager.GetTankKills(m_componentPlayer); break;
				case 24: current = AchievementsManager.GetTankKills(m_componentPlayer); break;
				case 25: current = AchievementsManager.GetGhostKills(m_componentPlayer); break;
				case 26: current = AchievementsManager.GetGhostKills(m_componentPlayer); break;
				case 27: current = AchievementsManager.GetGhostKills(m_componentPlayer); break;
				case 28: current = AchievementsManager.GetGhostTankKills(m_componentPlayer); break;
				case 29: current = AchievementsManager.GetGhostTankKills(m_componentPlayer); break;
				case 30: current = AchievementsManager.GetGhostTankKills(m_componentPlayer); break;
				case 31: current = AchievementsManager.GetBanditKills(m_componentPlayer); break;
				case 32: current = AchievementsManager.GetBanditKills(m_componentPlayer); break;
				case 33: current = AchievementsManager.GetBanditKills(m_componentPlayer); break;
				case 34: current = AchievementsManager.GetHeals(m_componentPlayer); break;
				case 35: current = AchievementsManager.GetHeals(m_componentPlayer); break;
				case 36: current = AchievementsManager.GetHeals(m_componentPlayer); break;
				case 38: current = AchievementsManager.GetPirateKills(m_componentPlayer); break;
				case 39: current = AchievementsManager.GetPirateKills(m_componentPlayer); break;
				case 40: current = AchievementsManager.GetPirateKills(m_componentPlayer); break;
				case 44: current = AchievementsManager.GetFlyingKills(m_componentPlayer); break;
				case 45: current = AchievementsManager.GetFlyingKills(m_componentPlayer); break;
				case 46: current = AchievementsManager.GetFlyingKills(m_componentPlayer); break;
				case 47: current = AchievementsManager.GetFlyingKills(m_componentPlayer); break;
				case 48: current = AchievementsManager.GetBoomerKills(m_componentPlayer); break;
				case 49: current = AchievementsManager.GetBoomerKills(m_componentPlayer); break;
				case 50: current = AchievementsManager.GetBoomerKills(m_componentPlayer); break;
				case 51: current = AchievementsManager.GetBoomerKills(m_componentPlayer); break;
				case 57: current = AchievementsManager.GetNormalTames(m_componentPlayer); break;
				case 58: current = AchievementsManager.GetNormalTames(m_componentPlayer); break;
				case 59: current = AchievementsManager.GetNormalTames(m_componentPlayer); break;
				case 60: current = AchievementsManager.GetNormalTames(m_componentPlayer); break;
				case 61: current = AchievementsManager.GetBossTames(m_componentPlayer); break;
				case 62: current = AchievementsManager.GetBossTames(m_componentPlayer); break;
				case 63: current = AchievementsManager.GetBossTames(m_componentPlayer); break;
				case 64: current = AchievementsManager.GetBossTames(m_componentPlayer); break;
				case 65: current = AchievementsManager.GetGhostTames(m_componentPlayer); break;
				case 66: current = AchievementsManager.GetGhostTames(m_componentPlayer); break;
				case 67: current = AchievementsManager.GetGhostTames(m_componentPlayer); break;
				case 68: current = AchievementsManager.GetGhostTames(m_componentPlayer); break;
			}

			// Si no se encontró target en el XML, usar valores por defecto (fallback)
			if (target == 0)
			{
				switch (achievementNumber)
				{
					case 16: case 25: case 31: case 34: case 38: case 44: case 48: case 57: case 61: case 65: target = 10; break;
					case 17: case 26: case 32: case 35: case 39: case 45: case 49: case 58: case 62: case 66: target = 25; break;
					case 18: case 27: case 33: case 36: case 40: case 47: case 51: case 60: case 64: case 68: target = 100; break;
					case 19: case 22: case 28: target = 10; break;
					case 20: case 23: case 29: target = 50; break;
					case 21: case 24: case 30: target = 100; break;
					case 46: target = 50; break;
					case 50: target = 55; break;
					case 59: case 63: case 67: target = 50; break;
				}
			}
		}

		private List<string> WrapText(BitmapFont font, string text, float maxWidth, float fontScale)
		{
			List<string> result = new List<string>();
			if (string.IsNullOrEmpty(text))
			{
				result.Add("");
				return result;
			}

			// Dividir por saltos de línea existentes (maneja los \n manuales)
			string[] paragraphs = text.Split(new char[] { '\n' }, StringSplitOptions.None);
			foreach (string paragraph in paragraphs)
			{
				if (string.IsNullOrEmpty(paragraph))
				{
					result.Add("");
					continue;
				}

				bool hasSpaces = paragraph.Contains(' ');
				if (hasSpaces)
				{
					// Algoritmo original para idiomas con espacios
					string[] words = paragraph.Split(' ');
					string currentLine = "";
					foreach (string word in words)
					{
						string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
						float testWidth = font.MeasureText(testLine, new Vector2(fontScale), Vector2.Zero).X;
						if (testWidth <= maxWidth)
						{
							currentLine = testLine;
						}
						else
						{
							if (!string.IsNullOrEmpty(currentLine))
								result.Add(currentLine);
							currentLine = word;
						}
					}
					if (!string.IsNullOrEmpty(currentLine))
						result.Add(currentLine);
				}
				else
				{
					// Para chino, japonés, coreano y otros sin espacios: wrap por caracteres
					string currentLine = "";
					foreach (char c in paragraph)
					{
						string testLine = currentLine + c;
						float testWidth = font.MeasureText(testLine, new Vector2(fontScale), Vector2.Zero).X;
						if (testWidth <= maxWidth)
						{
							currentLine = testLine;
						}
						else
						{
							// Si un solo carácter ya excede, se agrega forzosamente (caso poco común)
							if (string.IsNullOrEmpty(currentLine))
							{
								result.Add(c.ToString());
								currentLine = "";
							}
							else
							{
								result.Add(currentLine);
								currentLine = c.ToString();
							}
						}
					}
					if (!string.IsNullOrEmpty(currentLine))
						result.Add(currentLine);
				}
			}

			if (result.Count == 0)
				result.Add("");

			return result;
		}

		public override void Update()
		{
			UpdateBackgroundTransition();

			// Actualizar los indicadores de progreso y desbloqueados
			UpdateStatistics();

			if (m_needsReorder)
			{
				ReorderAchievements();
				m_needsReorder = false;
			}

			// === EFECTO ARCOÍRIS PARA LOGROS PREMIUM ===
			float hue = (float)((Time.RealTime * 30.0) % 360.0);
			Vector3 rainbowRgb = Color.HsvToRgb(new Vector3(hue, 1f, 1f));
			Color rainbowColor = new Color(rainbowRgb);

			foreach (var kvp in m_achievementItems)
			{
				var itemData = kvp.Value;
				if (itemData.IsPremium && itemData.TitleLabel != null)
				{
					// El arcoíris se aplica SIEMPRE, esté desbloqueado o no
					itemData.TitleLabel.Color = rainbowColor;
				}
			}

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
				AchievementsManager.OnNormalTameCounterChanged -= OnNormalTameCounterChanged;
				AchievementsManager.OnBossTameCounterChanged -= OnBossTameCounterChanged;
				AchievementsManager.OnGhostTameCounterChanged -= OnGhostTameCounterChanged;

				if (m_componentPlayer?.ComponentGui != null)
				{
					m_componentPlayer.ComponentGui.ModalPanelWidget = null;
				}
				return;
			}

			if (m_paintButton != null && m_paintButton.IsClicked && !m_isBgTransitioning)
			{
				ChangeBackground();
			}

			if (!m_isBgTransitioning)
			{
				// Usar referencias directas en lugar de Find
				foreach (var kvp in m_achievementItems)
				{
					var claimButton = kvp.Value.ClaimButton;
					if (claimButton == null) continue;
					if (!claimButton.IsClicked || !claimButton.IsEnabled) continue;

					int achievementNumber = kvp.Key;
					int rewardAmount = 0;

					// Obtener reward del Tag
					if (claimButton.Tag is AchievementButtonData data)
					{
						rewardAmount = data.RewardAmount;
					}
					else
					{
						continue;
					}

					bool success = AchievementsManager.ClaimAchievementReward(
						m_componentPlayer,
						achievementNumber,
						rewardAmount
					);

					if (success)
					{
						claimButton.IsEnabled = false;

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

		private void UpdateStatistics()
		{
			if (m_componentPlayer?.Project == null) return;
			var subsystemAchievements = m_componentPlayer.Project.FindSubsystem<SubsystemAchievements>(true);
			if (subsystemAchievements == null) return;

			int unlockedCount = subsystemAchievements.GetUnlockedAchievementCount();
			int percentage = m_totalAchievements > 0 ? (int)((float)unlockedCount / m_totalAchievements * 100f) : 0;

			string progressText = $"{LanguageControl.Get("AchievementsMessages", 6)}: {percentage}%";
			string unlockedText = $"{LanguageControl.Get("AchievementsMessages", 7)}: {unlockedCount}/{m_totalAchievements}";

			m_progressLabel.Text = progressText;
			m_unlockedLabel.Text = unlockedText;
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
			public StackPanelWidget TextStack;
			public List<string> WrappedLines;
			public BevelledButtonWidget ClaimButton;
			public ProgressBarWidget ProgressBar;
			public LabelWidget StatusLabel;
			public LabelWidget PercentLabel;
			public LabelWidget TitleLabel;
			public bool IsPremium;
			public bool IsCounter;        // NUEVO
			public int CounterTarget;     // NUEVO
		}

		private bool IsProgressBarAchievement(int number)
		{
			// Usar el valor leído del XML
			if (m_achievementItems.TryGetValue(number, out var itemData))
				return itemData.IsCounter && itemData.CounterTarget > 0;
			return false;
		}

		private void UpdateProgressBarAndStatus(int achievementNumber, int current, int target)
		{
			if (!m_achievementItems.TryGetValue(achievementNumber, out var itemData))
				return;

			bool unlocked = AchievementsManager.IsAchievementUnlocked(m_componentPlayer, achievementNumber);

			if (itemData.ProgressBar != null)
			{
				float progress = unlocked ? 1f : (target > 0 ? Math.Clamp((float)current / target, 0f, 1f) : 0f);
				itemData.ProgressBar.Value = progress;
			}

			if (itemData.PercentLabel != null)
			{
				string percentText = unlocked ? "100%" : (target > 0 ? $"{Math.Min(current, target)}%" : "0%");
				itemData.PercentLabel.Text = percentText;
			}

			if (itemData.StatusLabel != null)
			{
				if (unlocked)
				{
					itemData.StatusLabel.Text = LanguageControl.Get(fName, 2);
					itemData.StatusLabel.Color = Color.Green;
				}
				else if (current > 0)
				{
					itemData.StatusLabel.Text = $"{current}/{target}";
					itemData.StatusLabel.Color = Color.Yellow;
				}
				else
				{
					itemData.StatusLabel.Text = LanguageControl.Get(fName, 3);
					itemData.StatusLabel.Color = Color.Red;
				}
			}
		}

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

		// Estructura auxiliar:
		private struct AchievementSortData
		{
			public int Number;
			public bool Unlocked;
			public float Progress; // 0..1
			public long UnlockTime;
			public int Current; // para depuración
			public int Target;
		}

		// Método para calcular el progreso de cada logro:
		private float GetAchievementProgress(int number)
		{
			if (m_componentPlayer == null) return 0f;
			if (AchievementsManager.IsAchievementUnlocked(m_componentPlayer, number))
				return 1f;

			// Logros de día (9-14) y otros condicionales: van al final (progreso -1)
			if ((number >= 9 && number <= 14) || number == 52 || number == 8 || number == 37 || number == 41 || number == 42 || number == 43 || number == 70)
				return -1f;

			// Logros de contador: obtener current / target
			int current = 0, target = 0;
			switch (number)
			{
				case 16: current = AchievementsManager.GetInfectedKills(m_componentPlayer); target = 10; break;
				case 17: current = AchievementsManager.GetInfectedKills(m_componentPlayer); target = 50; break;
				case 18: current = AchievementsManager.GetInfectedKills(m_componentPlayer); target = 100; break;
				case 19: current = AchievementsManager.GetBossKills(m_componentPlayer); target = 10; break;
				case 20: current = AchievementsManager.GetBossKills(m_componentPlayer); target = 50; break;
				case 21: current = AchievementsManager.GetBossKills(m_componentPlayer); target = 100; break;
				case 22: current = AchievementsManager.GetTankKills(m_componentPlayer); target = 10; break;
				case 23: current = AchievementsManager.GetTankKills(m_componentPlayer); target = 50; break;
				case 24: current = AchievementsManager.GetTankKills(m_componentPlayer); target = 100; break;
				case 25: current = AchievementsManager.GetGhostKills(m_componentPlayer); target = 10; break;
				case 26: current = AchievementsManager.GetGhostKills(m_componentPlayer); target = 50; break;
				case 27: current = AchievementsManager.GetGhostKills(m_componentPlayer); target = 100; break;
				case 28: current = AchievementsManager.GetGhostTankKills(m_componentPlayer); target = 10; break;
				case 29: current = AchievementsManager.GetGhostTankKills(m_componentPlayer); target = 50; break;
				case 30: current = AchievementsManager.GetGhostTankKills(m_componentPlayer); target = 100; break;
				case 31: current = AchievementsManager.GetBanditKills(m_componentPlayer); target = 10; break;
				case 32: current = AchievementsManager.GetBanditKills(m_componentPlayer); target = 50; break;
				case 33: current = AchievementsManager.GetBanditKills(m_componentPlayer); target = 100; break;
				case 34: current = AchievementsManager.GetHeals(m_componentPlayer); target = 10; break;
				case 35: current = AchievementsManager.GetHeals(m_componentPlayer); target = 50; break;
				case 36: current = AchievementsManager.GetHeals(m_componentPlayer); target = 100; break;
				case 38: current = AchievementsManager.GetPirateKills(m_componentPlayer); target = 10; break;
				case 39: current = AchievementsManager.GetPirateKills(m_componentPlayer); target = 50; break;
				case 40: current = AchievementsManager.GetPirateKills(m_componentPlayer); target = 100; break;
				case 44: current = AchievementsManager.GetFlyingKills(m_componentPlayer); target = 10; break;
				case 45: current = AchievementsManager.GetFlyingKills(m_componentPlayer); target = 25; break;
				case 46: current = AchievementsManager.GetFlyingKills(m_componentPlayer); target = 50; break;
				case 47: current = AchievementsManager.GetFlyingKills(m_componentPlayer); target = 100; break;
				case 48: current = AchievementsManager.GetBoomerKills(m_componentPlayer); target = 10; break;
				case 49: current = AchievementsManager.GetBoomerKills(m_componentPlayer); target = 25; break;
				case 50: current = AchievementsManager.GetBoomerKills(m_componentPlayer); target = 55; break;
				case 51: current = AchievementsManager.GetBoomerKills(m_componentPlayer); target = 100; break;
				case 57: current = AchievementsManager.GetNormalTames(m_componentPlayer); target = 10; break;
				case 58: current = AchievementsManager.GetNormalTames(m_componentPlayer); target = 25; break;
				case 59: current = AchievementsManager.GetNormalTames(m_componentPlayer); target = 50; break;
				case 60: current = AchievementsManager.GetNormalTames(m_componentPlayer); target = 100; break;
				case 61: current = AchievementsManager.GetBossTames(m_componentPlayer); target = 10; break;
				case 62: current = AchievementsManager.GetBossTames(m_componentPlayer); target = 25; break;
				case 63: current = AchievementsManager.GetBossTames(m_componentPlayer); target = 50; break;
				case 64: current = AchievementsManager.GetBossTames(m_componentPlayer); target = 100; break;
				case 65: current = AchievementsManager.GetGhostTames(m_componentPlayer); target = 10; break;
				case 66: current = AchievementsManager.GetGhostTames(m_componentPlayer); target = 25; break;
				case 67: current = AchievementsManager.GetGhostTames(m_componentPlayer); target = 50; break;
				case 68: current = AchievementsManager.GetGhostTames(m_componentPlayer); target = 100; break;
				default: return 0f;
			}
			if (target <= 0) return 0f;
			return Math.Clamp((float)current / target, 0f, 0.999f);
		}

		private void ReorderAchievements()
		{
			if (m_achievementsStack == null || m_achievementItems.Count == 0) return;

			var subsystem = m_componentPlayer?.Project?.FindSubsystem<SubsystemAchievements>(true);

			m_sortData.Clear();
			foreach (var item in m_achievementItems)
			{
				int num = item.Key;
				bool unlocked = AchievementsManager.IsAchievementUnlocked(m_componentPlayer, num);
				float progress = unlocked ? 1f : GetAchievementProgress(num);
				long unlockTime = unlocked && subsystem != null ? subsystem.GetUnlockTime(num) : 0;
				m_sortData.Add(new AchievementSortData
				{
					Number = num,
					Unlocked = unlocked,
					Progress = progress,
					UnlockTime = unlockTime
				});
			}

			m_sortData.Sort((a, b) =>
			{
				// Determinar prioridad basada SOLO en progreso, no en unlocked
				int GetPriority(AchievementSortData x)
				{
					// Logros NO completados CON progreso (0 < progress < 1) -> Prioridad 0 (más alta)
					if (x.Progress > 0f && x.Progress < 1f) return 0;
					// Logros completados (progress == 1) -> Prioridad 1
					if (x.Progress >= 1f) return 1;
					// Logros sin progreso (progress <= 0) -> Prioridad 2 (más baja)
					return 2;
				}

				int priorityA = GetPriority(a);
				int priorityB = GetPriority(b);

				if (priorityA != priorityB) return priorityA.CompareTo(priorityB);

				if (priorityA == 0) // En progreso: mayor progreso primero
					return b.Progress.CompareTo(a.Progress);

				if (priorityA == 1) // Completados: más reciente primero
					return b.UnlockTime.CompareTo(a.UnlockTime);

				// Sin progreso: orden base
				int baseA = subsystem?.GetBaseOrder(a.Number) ?? int.MaxValue;
				int baseB = subsystem?.GetBaseOrder(b.Number) ?? int.MaxValue;
				return baseA.CompareTo(baseB);
			});

			var orderedContainers = new List<Widget>();
			foreach (var sortData in m_sortData)
			{
				if (m_achievementItems.TryGetValue(sortData.Number, out var itemData) && itemData.Container != null)
					orderedContainers.Add(itemData.Container);
			}

			// Aplicar el nuevo orden
			m_achievementsStack.Children.Clear();
			foreach (var container in orderedContainers)
				m_achievementsStack.Children.Add(container);
		}

		private void UnsubscribeEvents()
		{
			SubsystemAchievements.AchievementUnlocked -= OnAnyAchievementUnlocked;
		}

		private string GetTypeKey(string typeName)
		{
			// Intentar parsear el string al enum AchievementCategory
			if (Enum.TryParse<AchievementCategory>(typeName, true, out var category))
			{
				// Mapear el enum a la clave de idioma
				switch (category)
				{
					case AchievementCategory.Combat: return "1";
					case AchievementCategory.Survival: return "2";
					case AchievementCategory.Taming: return "3";
					case AchievementCategory.Healing: return "4";
					case AchievementCategory.Trade: return "5";
					case AchievementCategory.Special: return "6";
					default: return "1";
				}
			}

			// Si no se pudo parsear, intentar con el método antiguo como fallback
			Log.Warning($"[AchievementsWidget] Tipo de logro desconocido: '{typeName}', usando fallback a Combat");

			switch (typeName)
			{
				case "Combat": return "1";
				case "Survival": return "2";
				case "Taming": return "3";
				case "Healing": return "4";
				case "Trade": return "5";
				case "Special": return "6";
				default: return "1";
			}
		}
	}
}
