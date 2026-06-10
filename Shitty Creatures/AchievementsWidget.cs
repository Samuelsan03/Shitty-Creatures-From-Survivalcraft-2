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
			// Luego llamar a ReorderAchievements para aplicar el orden dinámico inicial
			ReorderAchievements();
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
			// Reordenar la lista cuando se desbloquea cualquier logro
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
			if (m_achievementItems.TryGetValue(16, out var item16) && !AchievementsManager.IsAchievementUnlocked(player, 16))
				UpdateCounterDescription(16, currentKills, 10, item16.BaseDescription, item16.DescriptionLabel);
			if (m_achievementItems.TryGetValue(17, out var item17) && !AchievementsManager.IsAchievementUnlocked(player, 17))
				UpdateCounterDescription(17, currentKills, 50, item17.BaseDescription, item17.DescriptionLabel);
			if (m_achievementItems.TryGetValue(18, out var item18) && !AchievementsManager.IsAchievementUnlocked(player, 18))
				UpdateCounterDescription(18, currentKills, 100, item18.BaseDescription, item18.DescriptionLabel);

			m_needsReorder = true;
		}

		private void OnBossCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			if (m_achievementItems.TryGetValue(19, out var item19) && !AchievementsManager.IsAchievementUnlocked(player, 19))
				UpdateCounterDescription(19, currentKills, 10, item19.BaseDescription, item19.DescriptionLabel);
			if (m_achievementItems.TryGetValue(20, out var item20) && !AchievementsManager.IsAchievementUnlocked(player, 20))
				UpdateCounterDescription(20, currentKills, 50, item20.BaseDescription, item20.DescriptionLabel);
			if (m_achievementItems.TryGetValue(21, out var item21) && !AchievementsManager.IsAchievementUnlocked(player, 21))
				UpdateCounterDescription(21, currentKills, 100, item21.BaseDescription, item21.DescriptionLabel);

			m_needsReorder = true;
		}

		private void OnTankCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			if (m_achievementItems.TryGetValue(22, out var item22) && !AchievementsManager.IsAchievementUnlocked(player, 22))
				UpdateCounterDescription(22, currentKills, 10, item22.BaseDescription, item22.DescriptionLabel);
			if (m_achievementItems.TryGetValue(23, out var item23) && !AchievementsManager.IsAchievementUnlocked(player, 23))
				UpdateCounterDescription(23, currentKills, 50, item23.BaseDescription, item23.DescriptionLabel);
			if (m_achievementItems.TryGetValue(24, out var item24) && !AchievementsManager.IsAchievementUnlocked(player, 24))
				UpdateCounterDescription(24, currentKills, 100, item24.BaseDescription, item24.DescriptionLabel);

			m_needsReorder = true;
		}

		private void OnGhostCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			if (m_achievementItems.TryGetValue(25, out var item25) && !AchievementsManager.IsAchievementUnlocked(player, 25))
				UpdateCounterDescription(25, currentKills, 10, item25.BaseDescription, item25.DescriptionLabel);
			if (m_achievementItems.TryGetValue(26, out var item26) && !AchievementsManager.IsAchievementUnlocked(player, 26))
				UpdateCounterDescription(26, currentKills, 50, item26.BaseDescription, item26.DescriptionLabel);
			if (m_achievementItems.TryGetValue(27, out var item27) && !AchievementsManager.IsAchievementUnlocked(player, 27))
				UpdateCounterDescription(27, currentKills, 100, item27.BaseDescription, item27.DescriptionLabel);

			m_needsReorder = true;
		}

		private void OnGhostTankCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			if (m_achievementItems.TryGetValue(28, out var item28) && !AchievementsManager.IsAchievementUnlocked(player, 28))
				UpdateCounterDescription(28, currentKills, 10, item28.BaseDescription, item28.DescriptionLabel);
			if (m_achievementItems.TryGetValue(29, out var item29) && !AchievementsManager.IsAchievementUnlocked(player, 29))
				UpdateCounterDescription(29, currentKills, 50, item29.BaseDescription, item29.DescriptionLabel);
			if (m_achievementItems.TryGetValue(30, out var item30) && !AchievementsManager.IsAchievementUnlocked(player, 30))
				UpdateCounterDescription(30, currentKills, 100, item30.BaseDescription, item30.DescriptionLabel);

			m_needsReorder = true;
		}

		private void OnBanditCounterChanged(ComponentPlayer player, int currentKills, int targetKills)
		{
			if (player != m_componentPlayer) return;
			if (m_achievementItems.TryGetValue(31, out var item31) && !AchievementsManager.IsAchievementUnlocked(player, 31))
				UpdateCounterDescription(31, currentKills, 10, item31.BaseDescription, item31.DescriptionLabel);
			if (m_achievementItems.TryGetValue(32, out var item32) && !AchievementsManager.IsAchievementUnlocked(player, 32))
				UpdateCounterDescription(32, currentKills, 50, item32.BaseDescription, item32.DescriptionLabel);
			if (m_achievementItems.TryGetValue(33, out var item33) && !AchievementsManager.IsAchievementUnlocked(player, 33))
				UpdateCounterDescription(33, currentKills, 100, item33.BaseDescription, item33.DescriptionLabel);

			m_needsReorder = true;
		}

		private void OnHealCounterChanged(ComponentPlayer player, int currentHeals, int targetHeals)
		{
			if (player != m_componentPlayer) return;
			if (m_achievementItems.TryGetValue(34, out var item34) && !AchievementsManager.IsAchievementUnlocked(player, 34))
				UpdateCounterDescription(34, currentHeals, 10, item34.BaseDescription, item34.DescriptionLabel);
			if (m_achievementItems.TryGetValue(35, out var item35) && !AchievementsManager.IsAchievementUnlocked(player, 35))
				UpdateCounterDescription(35, currentHeals, 50, item35.BaseDescription, item35.DescriptionLabel);
			if (m_achievementItems.TryGetValue(36, out var item36) && !AchievementsManager.IsAchievementUnlocked(player, 36))
				UpdateCounterDescription(36, currentHeals, 100, item36.BaseDescription, item36.DescriptionLabel);

			m_needsReorder = true;
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

			m_needsReorder = true;
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

			m_needsReorder = true;
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

			m_needsReorder = true;
		}

		private void OnNormalTameCounterChanged(ComponentPlayer player, int current, int target)
		{
			if (player != m_componentPlayer) return;
			if (m_achievementItems.TryGetValue(57, out var i57) && !AchievementsManager.IsAchievementUnlocked(player, 57))
				UpdateCounterDescription(57, current, 10, i57.BaseDescription, i57.DescriptionLabel);
			if (m_achievementItems.TryGetValue(58, out var i58) && !AchievementsManager.IsAchievementUnlocked(player, 58))
				UpdateCounterDescription(58, current, 25, i58.BaseDescription, i58.DescriptionLabel);
			if (m_achievementItems.TryGetValue(59, out var i59) && !AchievementsManager.IsAchievementUnlocked(player, 59))
				UpdateCounterDescription(59, current, 50, i59.BaseDescription, i59.DescriptionLabel);
			if (m_achievementItems.TryGetValue(60, out var i60) && !AchievementsManager.IsAchievementUnlocked(player, 60))
				UpdateCounterDescription(60, current, 100, i60.BaseDescription, i60.DescriptionLabel);

			m_needsReorder = true;
		}

		private void OnBossTameCounterChanged(ComponentPlayer player, int current, int target)
		{
			if (player != m_componentPlayer) return;
			if (m_achievementItems.TryGetValue(61, out var i61) && !AchievementsManager.IsAchievementUnlocked(player, 61))
				UpdateCounterDescription(61, current, 1, i61.BaseDescription, i61.DescriptionLabel);
			if (m_achievementItems.TryGetValue(62, out var i62) && !AchievementsManager.IsAchievementUnlocked(player, 62))
				UpdateCounterDescription(62, current, 5, i62.BaseDescription, i62.DescriptionLabel);
			if (m_achievementItems.TryGetValue(63, out var i63) && !AchievementsManager.IsAchievementUnlocked(player, 63))
				UpdateCounterDescription(63, current, 10, i63.BaseDescription, i63.DescriptionLabel);
			if (m_achievementItems.TryGetValue(64, out var i64) && !AchievementsManager.IsAchievementUnlocked(player, 64))
				UpdateCounterDescription(64, current, 25, i64.BaseDescription, i64.DescriptionLabel);

			m_needsReorder = true;
		}

		private void OnGhostTameCounterChanged(ComponentPlayer player, int current, int target)
		{
			if (player != m_componentPlayer) return;
			if (m_achievementItems.TryGetValue(65, out var i65) && !AchievementsManager.IsAchievementUnlocked(player, 65))
				UpdateCounterDescription(65, current, 5, i65.BaseDescription, i65.DescriptionLabel);
			if (m_achievementItems.TryGetValue(66, out var i66) && !AchievementsManager.IsAchievementUnlocked(player, 66))
				UpdateCounterDescription(66, current, 10, i66.BaseDescription, i66.DescriptionLabel);
			if (m_achievementItems.TryGetValue(67, out var i67) && !AchievementsManager.IsAchievementUnlocked(player, 67))
				UpdateCounterDescription(67, current, 25, i67.BaseDescription, i67.DescriptionLabel);
			if (m_achievementItems.TryGetValue(68, out var i68) && !AchievementsManager.IsAchievementUnlocked(player, 68))
				UpdateCounterDescription(68, current, 50, i68.BaseDescription, i68.DescriptionLabel);

			m_needsReorder = true;
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
			int currentKills = 0;
			int target = 0;

			if (!unlocked)
			{
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
					case 57: currentKills = AchievementsManager.GetNormalTames(m_componentPlayer); target = 10; break;
					case 58: currentKills = AchievementsManager.GetNormalTames(m_componentPlayer); target = 25; break;
					case 59: currentKills = AchievementsManager.GetNormalTames(m_componentPlayer); target = 50; break;
					case 60: currentKills = AchievementsManager.GetNormalTames(m_componentPlayer); target = 100; break;
					case 61: currentKills = AchievementsManager.GetBossTames(m_componentPlayer); target = 10; break;
					case 62: currentKills = AchievementsManager.GetBossTames(m_componentPlayer); target = 25; break;
					case 63: currentKills = AchievementsManager.GetBossTames(m_componentPlayer); target = 50; break;
					case 64: currentKills = AchievementsManager.GetBossTames(m_componentPlayer); target = 100; break;
					case 65: currentKills = AchievementsManager.GetGhostTames(m_componentPlayer); target = 10; break;
					case 66: currentKills = AchievementsManager.GetGhostTames(m_componentPlayer); target = 25; break;
					case 67: currentKills = AchievementsManager.GetGhostTames(m_componentPlayer); target = 50; break;
					case 68: currentKills = AchievementsManager.GetGhostTames(m_componentPlayer); target = 100; break;
				}

				if (target > 0)
				{
					int displayKills = Math.Min(currentKills, target);
					finalDescription = $"{baseDescription} ({displayKills}/{target})";
				}
			}

			var textStack = new StackPanelWidget
			{
				Direction = LayoutDirection.Vertical,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(15, 0)
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

			float statusHeight = 20f;
			float titleHeight = 25f;
			float textStackHeight = wrappedLines.Count * lineHeight + (wrappedLines.Count - 1) * 4f;
			float bottomRowHeight = 40f;
			float totalHeight = statusHeight + titleHeight + textStackHeight + bottomRowHeight + 20f;
			totalHeight = Math.Max(95f, totalHeight);

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
				ClaimButton = claimButton  // <-- AGREGAR ESTO
			};
		}

		private List<string> WrapText(BitmapFont font, string text, float maxWidth, float fontScale)
		{
			List<string> lines = new List<string>();
			if (string.IsNullOrEmpty(text))
			{
				lines.Add("");
				return lines;
			}

			string[] words = text.Split(' ');
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
					{
						lines.Add(currentLine);
					}
					currentLine = word;
				}
			}

			if (!string.IsNullOrEmpty(currentLine))
			{
				lines.Add(currentLine);
			}

			if (lines.Count == 0)
			{
				lines.Add("");
			}

			return lines;
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
			public BevelledButtonWidget ClaimButton;  // <-- AGREGAR ESTO
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

			// Logros de día (9-14) no tienen progreso incremental, solo completado o no
			if ((number >= 9 && number <= 14) || number == 52 || number == 8 || number == 37 || number == 41 || number == 42 || number == 43)
				return 0f;

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

		// Método para reordenar los widgets según los datos actuales
		private void ReorderAchievements()
		{
			if (m_achievementsStack == null || m_achievementItems.Count == 0) return;

			var subsystem = m_componentPlayer?.Project?.FindSubsystem<SubsystemAchievements>(true);

			// Recopilar datos de ordenamiento
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

			// Ordenar según reglas: completados por unlockTime descendente, luego no completados por progreso descendente,
			// y como desempate usar el orden base (índice aleatorio inicial)
			m_sortData.Sort((a, b) =>
			{
				if (a.Unlocked != b.Unlocked)
					return a.Unlocked ? -1 : 1;

				if (a.Unlocked)
				{
					int timeCompare = b.UnlockTime.CompareTo(a.UnlockTime);
					if (timeCompare != 0)
						return timeCompare;
					// Mismo tiempo de desbloqueo: usar orden base
					int baseA = subsystem?.GetBaseOrder(a.Number) ?? int.MaxValue;
					int baseB = subsystem?.GetBaseOrder(b.Number) ?? int.MaxValue;
					return baseA.CompareTo(baseB);
				}
				else
				{
					int progressCompare = b.Progress.CompareTo(a.Progress);
					if (progressCompare != 0)
						return progressCompare;
					// Mismo progreso: usar orden base
					int baseA = subsystem?.GetBaseOrder(a.Number) ?? int.MaxValue;
					int baseB = subsystem?.GetBaseOrder(b.Number) ?? int.MaxValue;
					return baseA.CompareTo(baseB);
				}
			});

			// Reconstruir la lista de hijos en el nuevo orden
			var currentContainers = new List<Widget>();
			foreach (var child in m_achievementsStack.Children)
				currentContainers.Add(child);

			var orderedContainers = new List<Widget>();
			foreach (var sortData in m_sortData)
			{
				if (m_achievementItems.TryGetValue(sortData.Number, out var itemData) && itemData.Container != null)
					orderedContainers.Add(itemData.Container);
			}

			// Verificar si el orden cambió para evitar operaciones innecesarias
			bool orderChanged = false;
			for (int i = 0; i < currentContainers.Count; i++)
			{
				if (currentContainers[i] != orderedContainers[i])
				{
					orderChanged = true;
					break;
				}
			}

			if (orderChanged)
			{
				m_achievementsStack.Children.Clear();
				foreach (var container in orderedContainers)
					m_achievementsStack.Children.Add(container);
			}
		}

		private void UnsubscribeEvents()
		{
			SubsystemAchievements.AchievementUnlocked -= OnAnyAchievementUnlocked;
		}
	}	
}
