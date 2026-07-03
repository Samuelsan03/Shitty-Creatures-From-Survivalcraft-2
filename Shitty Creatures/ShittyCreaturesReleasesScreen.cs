using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class ShittyCreaturesReleasesScreen : Screen
	{
		private ListPanelWidget m_releasesListPanel;
		private ScrollPanelWidget m_scrollPanel;
		private LabelWidget m_titleLabel;
		private LabelWidget m_infoLabel;
		private LabelWidget m_textLabel;
		private ButtonWidget m_backButton;

		private List<ModVersionInfo> m_versions;

		public ShittyCreaturesReleasesScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/ShittyCreaturesReleasesScreen");
			LoadContents(this, node);

			m_titleLabel = Children.Find<LabelWidget>("ReleaseTitle", true);
			m_textLabel = Children.Find<LabelWidget>("ReleaseText", true);
			m_scrollPanel = Children.Find<ScrollPanelWidget>("ScrollPanel", true);
			m_releasesListPanel = Children.Find<ListPanelWidget>("ReleasesList", true);
			m_infoLabel = Children.Find<LabelWidget>("ReleaseInfo", true);

			// ─────────────────────────────────────────────
			// 1. Crear la barra superior (topbar) si no existe
			// ─────────────────────────────────────────────
			var topBar = Children.Find<CanvasWidget>("TopBar", false);
			if (topBar == null)
			{
				topBar = CreateTopBar();
				Children.Add(topBar);
			}

			// Cambiar el color de la barra a rojo
			foreach (var child in topBar.Children)
			{
				if (child is BevelledRectangleWidget rect)
				{
					rect.CenterColor = Color.Red;
					rect.BevelColor = Color.Red;
				}
				if (child is BevelledButtonWidget btn)
				{
					foreach (var btnChild in btn.Children)
					{
						if (btnChild is BevelledRectangleWidget btnRect)
						{
							btnRect.CenterColor = Color.Red;
							btnRect.BevelColor = Color.Red;
						}
					}
				}
			}

			// Configurar la lista de versiones
			m_releasesListPanel.ItemWidgetFactory = (item) =>
			{
				var versionInfo = item as ModVersionInfo;
				var label = new LabelWidget
				{
					Text = versionInfo?.DisplayName ?? string.Empty,
					HorizontalAlignment = WidgetAlignment.Center,
					VerticalAlignment = WidgetAlignment.Center
				};
				return label;
			};

			m_releasesListPanel.ItemClicked += (obj) => DisplayVersionInfo(obj as ModVersionInfo);

			LoadVersionData();
		}

		// ─────────────────────────────────────────────
		// 2. Método para crear la barra superior manualmente
		// ─────────────────────────────────────────────
		private CanvasWidget CreateTopBar()
		{
			var canvas = new CanvasWidget
			{
				Name = "TopBar",
				Size = new Vector2(64f, float.PositiveInfinity)
			};

			// Fondo de la barra (rojo)
			var background = new BevelledRectangleWidget
			{
				Size = new Vector2(64f, float.PositiveInfinity),
				TextureScale = 0.5f,
				CenterColor = Color.Red,
				BevelColor = Color.Red,
				IsHitTestVisible = false
			};

			// Botón de retroceso
			var backButton = new BevelledButtonWidget
			{
				Name = "TopBar.Back",
				Size = new Vector2(60f, 60f)
			};
			var btnRect = new BevelledRectangleWidget
			{
				Name = "BevelledButton.Rectangle",
				Size = new Vector2(float.PositiveInfinity, float.PositiveInfinity),
				CenterColor = Color.Red,
				BevelColor = Color.Red
			};
			var arrow = new RectangleWidget
			{
				Name = "BevelledButton.Image",
				Size = new Vector2(32f, 32f),
				Subtexture = ContentManager.Get<Subtexture>("Textures/Atlas/ArrowLeft"),
				FillColor = Color.White,
				OutlineColor = Color.Transparent,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				IsVisible = true
			};
			backButton.Children.Add(btnRect);
			backButton.Children.Add(arrow);
			m_backButton = backButton;

			// Etiqueta del título (texto vertical) - CORREGIDO: usar "ShittyCreaturesReleasesScreen" y "0"
			var titleLabel = new LabelWidget
			{
				Name = "TopBar.Label",
				Text = LanguageControl.GetContentWidgets("ShittyCreaturesReleasesScreen", "0"),
				Color = Color.White,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				TextOrientation = TextOrientation.VerticalLeft
			};

			// Stack principal (botón + fondo)
			var stack = new StackPanelWidget
			{
				Direction = LayoutDirection.Vertical,
				Margin = new Vector2(4f, 0f),
				IsHitTestVisible = false
			};

			stack.Children.Add(new CanvasWidget { Size = new Vector2(0f, 4f) });
			stack.Children.Add(backButton);
			stack.Children.Add(new CanvasWidget { Size = new Vector2(0f, 4f) });
			stack.Children.Add(background);
			stack.Children.Add(new CanvasWidget { Size = new Vector2(0f, 4f) });

			// Stack para la etiqueta
			var labelStack = new StackPanelWidget
			{
				Direction = LayoutDirection.Vertical,
				Margin = new Vector2(4f, 0f),
				IsHitTestVisible = false
			};
			labelStack.Children.Add(new CanvasWidget { Size = new Vector2(0f, 4f) });
			labelStack.Children.Add(new CanvasWidget { Size = new Vector2(0f, 64f) });

			var labelContainer = new CanvasWidget
			{
				Size = new Vector2(54f, float.PositiveInfinity),
				VerticalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0f, 10f),
				ClampToBounds = true
			};
			labelContainer.Children.Add(titleLabel);
			labelStack.Children.Add(labelContainer);
			labelStack.Children.Add(new CanvasWidget { Size = new Vector2(0f, 4f) });

			canvas.Children.Add(stack);
			canvas.Children.Add(labelStack);

			return canvas;
		}

		// ─────────────────────────────────────────────
		// 3. Resto del código
		// ─────────────────────────────────────────────
		private void LoadVersionData()
		{
			m_versions = new List<ModVersionInfo>
			{
				new ModVersionInfo { Version = "1.0.6", DisplayName = "1.0.6", ReleaseDate = "" },
				new ModVersionInfo { Version = "1.0.5", DisplayName = "1.0.5", ReleaseDate = "2026-01-08" },
				new ModVersionInfo { Version = "1.0.4", DisplayName = "1.0.4", ReleaseDate = "2025-11-15" },
				new ModVersionInfo { Version = "1.0.3", DisplayName = "1.0.3", ReleaseDate = "2025-11-07" },
				new ModVersionInfo { Version = "1.0.2", DisplayName = "1.0.2", ReleaseDate = "2025-11-06" },
				new ModVersionInfo { Version = "1.0.1", DisplayName = "1.0.1", ReleaseDate = "2025-11-04" },
				new ModVersionInfo { Version = "1.0.0", DisplayName = "1.0.0", ReleaseDate = "2025-11-04" }
			};

			foreach (var version in m_versions)
			{
				version.DetailedChanges = BuildDetailedChanges(version.Version);
			}

			m_releasesListPanel.ClearItems();
			foreach (var version in m_versions)
			{
				m_releasesListPanel.AddItem(version);
			}

			if (m_versions.Count > 0)
				DisplayVersionInfo(m_versions[0]);
		}

		private string BuildDetailedChanges(string version)
		{
			const string category = "ShittyCreaturesLog";
			string versionPrefix = $"Line{version.Replace(".", "_")}_";

			var linesDict = new Dictionary<int, string>();

			var jsonNode = typeof(LanguageControl).GetField("jsonNode",
				System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.GetValue(null) as System.Text.Json.Nodes.JsonNode;

			if (jsonNode != null)
			{
				var shittyNode = jsonNode[category];
				if (shittyNode is System.Text.Json.Nodes.JsonObject obj)
				{
					foreach (var prop in obj)
					{
						if (prop.Key.StartsWith(versionPrefix))
						{
							string numPart = prop.Key.Substring(versionPrefix.Length);
							if (int.TryParse(numPart, out int order))
							{
								string translated = prop.Value?.ToString();
								if (!string.IsNullOrEmpty(translated))
									linesDict[order] = translated;
							}
						}
					}
				}
			}

			if (linesDict.Count == 0)
			{
				var info = m_versions.Find(v => v.Version == version);
				return info?.Description ?? "No description available.";
			}

			var sorted = linesDict.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value);
			return string.Join(Environment.NewLine, sorted);
		}

		private void DisplayVersionInfo(ModVersionInfo versionInfo)
		{
			if (versionInfo == null) return;

			m_titleLabel.Text = versionInfo.DisplayName;
			m_infoLabel.Text = string.IsNullOrEmpty(versionInfo.ReleaseDate)
				? string.Empty
				: versionInfo.ReleaseDate;
			m_textLabel.Text = versionInfo.DetailedChanges ?? versionInfo.Description;

			m_scrollPanel.ScrollPosition = 0f;
		}

		public override void Update()
		{
			if (Input.Back || Input.Cancel || (m_backButton != null && m_backButton.IsClicked))
			{
				ScreensManager.SwitchScreen("MainMenu", Array.Empty<object>());
			}
		}

		private class ModVersionInfo
		{
			public string Version { get; set; }
			public string DisplayName { get; set; }
			public string ReleaseDate { get; set; }
			public string Description { get; set; }
			public string DetailedChanges { get; set; }
		}
	}
}
