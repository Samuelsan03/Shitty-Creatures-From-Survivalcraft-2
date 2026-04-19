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

		private void LoadVersionData()
		{
			// Datos estáticos de versiones con fechas proporcionadas
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

			// Diccionario para almacenar orden -> texto
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
							// Extraer número para ordenar
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

			// Si no se encontraron traducciones, usar descripción de respaldo
			if (linesDict.Count == 0)
			{
				var info = m_versions.Find(v => v.Version == version);
				return info?.Description ?? "No description available.";
			}

			// Ordenar por clave (número de línea) y concatenar
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
			if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
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
