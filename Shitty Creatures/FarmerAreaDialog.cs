using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;

namespace Game
{
	/// <summary>
	/// Diálogo de administración de áreas de cultivo.
	/// Todos los textos (estáticos y dinámicos) se resuelven desde C# con
	/// LanguageControl.Get("FarmerAreaDialog", N).
	/// </summary>
	public class FarmerAreaDialog : Dialog
	{
		private SubsystemFarmerWandBlockBehavior m_subsystem;
		private ComponentPlayer m_player;

		private LabelWidget m_headerLabel, m_formatLabel;
		private LabelWidget m_pointALabel, m_pointBLabel;
		private TextBoxWidget m_textBoxA, m_textBoxB;
		private LabelWidget m_areaTitleLabel;
		private LabelWidget m_creaturesLabel;

		private ButtonWidget m_prevAreaButton, m_nextAreaButton;
		private ButtonWidget m_newAreaButton, m_deleteAreaButton;
		private ButtonWidget m_toggleShowButton;
		private ButtonWidget m_browseButton, m_removeButton;
		private ButtonWidget m_okButton, m_resetButton, m_cancelButton;

		private int m_assignedCount;

		public FarmerAreaDialog(SubsystemFarmerWandBlockBehavior subsystem, ComponentPlayer player)
		{
			m_subsystem = subsystem;
			m_player = player;

			XElement node = ContentManager.Get<XElement>("Dialogs/FarmerAreaDialog");
			this.LoadContents(this, node);

			// --- Referencias a widgets ---
			m_headerLabel = Children.Find<LabelWidget>("FarmerAreaDialog.HeaderLabel", true);
			m_formatLabel = Children.Find<LabelWidget>("FarmerAreaDialog.FormatLabel", true);
			m_pointALabel = Children.Find<LabelWidget>("FarmerAreaDialog.PointALabel", true);
			m_pointBLabel = Children.Find<LabelWidget>("FarmerAreaDialog.PointBLabel", true);

			m_textBoxA = Children.Find<TextBoxWidget>("FarmerAreaDialog.TextBoxA", true);
			m_textBoxB = Children.Find<TextBoxWidget>("FarmerAreaDialog.TextBoxB", true);
			m_areaTitleLabel = Children.Find<LabelWidget>("FarmerAreaDialog.AreaTitleLabel", true);
			m_creaturesLabel = Children.Find<LabelWidget>("FarmerAreaDialog.CreaturesLabel", true);

			m_prevAreaButton = Children.Find<ButtonWidget>("FarmerAreaDialog.PrevAreaButton", true);
			m_nextAreaButton = Children.Find<ButtonWidget>("FarmerAreaDialog.NextAreaButton", true);
			m_newAreaButton = Children.Find<ButtonWidget>("FarmerAreaDialog.NewAreaButton", true);
			m_deleteAreaButton = Children.Find<ButtonWidget>("FarmerAreaDialog.DeleteAreaButton", true);

			m_toggleShowButton = Children.Find<ButtonWidget>("FarmerAreaDialog.ToggleShowButton", true);
			m_okButton = Children.Find<ButtonWidget>("FarmerAreaDialog.OkButton", true);
			m_resetButton = Children.Find<ButtonWidget>("FarmerAreaDialog.ResetButton", true);
			m_cancelButton = Children.Find<ButtonWidget>("FarmerAreaDialog.CancelButton", true);

			m_browseButton = Children.Find<ButtonWidget>("FarmerAreaDialog.BrowseButton", true);
			m_removeButton = Children.Find<ButtonWidget>("FarmerAreaDialog.RemoveButton", true);

			ApplyStaticTexts();

			if (m_subsystem.GetActiveArea() == null)
				m_subsystem.CreateArea();

			PopulateFromArea();
			UpdateControls();
		}

		// -----------------------------------------------------------------
		//  Textos estáticos (una sola vez)
		// -----------------------------------------------------------------
		private void ApplyStaticTexts()
		{
			m_headerLabel.Text = LanguageControl.Get("FarmerAreaDialog", 0);
			m_formatLabel.Text = LanguageControl.Get("FarmerAreaDialog", 1);
			m_pointALabel.Text = LanguageControl.Get("FarmerAreaDialog", 2);
			m_pointBLabel.Text = LanguageControl.Get("FarmerAreaDialog", 3);

			m_newAreaButton.Text = LanguageControl.Get("FarmerAreaDialog", 14);
			m_deleteAreaButton.Text = LanguageControl.Get("FarmerAreaDialog", 15);
			m_browseButton.Text = LanguageControl.Get("FarmerAreaDialog", 9);
			m_removeButton.Text = LanguageControl.Get("FarmerAreaDialog", 10);
			m_okButton.Text = LanguageControl.Get("FarmerAreaDialog", 11);
			m_resetButton.Text = LanguageControl.Get("FarmerAreaDialog", 12);
			m_cancelButton.Text = LanguageControl.Get("FarmerAreaDialog", 13);
		}

		private static string FormatPoint(Point3 p) => p.X + "," + p.Y + "," + p.Z;

		private void PopulateFromArea()
		{
			var area = m_subsystem.GetActiveArea();
			if (area == null)
			{
				m_textBoxA.Text = "";
				m_textBoxB.Text = "";
				return;
			}
			m_textBoxA.Text = area.PointA != null ? FormatPoint(area.PointA.Value) : "";
			m_textBoxB.Text = area.PointB != null ? FormatPoint(area.PointB.Value) : "";
		}

		public override void Update()
		{
			HandleAreaNavigation();
			UpdateControls();

			if (m_browseButton.IsClicked) OpenBrowseDialog();
			if (m_removeButton.IsClicked) OpenRemoveDialog();

			if (m_okButton.IsClicked)
			{
				if (TryParsePoints(out Point3? a, out Point3? b))
				{
					var area = m_subsystem.GetActiveArea();
					if (area != null)
					{
						area.PointA = a;
						area.PointB = b;
						area.Preview = null;
						area.PointBMarkedTime = Time.RealTime;
						m_subsystem.ApplyFarmAreaToAssignedCreatures(area);
					}
					Dismiss();
				}
			}

			if (m_resetButton.IsClicked)
			{
				m_subsystem.ResetAreaPoints(m_subsystem.GetActiveArea());
				PopulateFromArea();
				UpdateControls();
			}

			if (m_toggleShowButton.IsClicked)
			{
				m_subsystem.ToggleShowArea(m_subsystem.GetActiveArea());
				UpdateControls();
			}

			if (base.Input.Cancel || m_cancelButton.IsClicked)
			{
				Dismiss();
			}
		}

		// -----------------------------------------------------------------
		//  Navegación de áreas
		// -----------------------------------------------------------------
		private void HandleAreaNavigation()
		{
			if (m_prevAreaButton.IsClicked) CycleArea(-1);
			if (m_nextAreaButton.IsClicked) CycleArea(+1);

			if (m_newAreaButton.IsClicked)
			{
				m_subsystem.CreateArea();
				PopulateFromArea();
				UpdateControls();
			}

			if (m_deleteAreaButton.IsClicked)
			{
				var area = m_subsystem.GetActiveArea();
				if (area != null)
				{
					m_subsystem.DeleteArea(area);
					if (m_subsystem.GetActiveArea() == null)
						m_subsystem.CreateArea();
					PopulateFromArea();
					UpdateControls();
				}
			}
		}

		private void CycleArea(int delta)
		{
			var areas = m_subsystem.m_areas;
			if (areas.Count == 0) return;
			var cur = m_subsystem.GetActiveArea();
			int idx = cur != null ? areas.IndexOf(cur) : -1;
			if (idx < 0) idx = 0;
			idx = (idx + delta + areas.Count) % areas.Count;
			m_subsystem.SetActiveArea(areas[idx]);
			PopulateFromArea();
			UpdateControls();
		}

		// -----------------------------------------------------------------
		//  Añadir / Quitar criaturas
		// -----------------------------------------------------------------
		private List<ComponentCreature> CollectFarmers(out int assignedCount)
		{
			var list = new List<ComponentCreature>();
			assignedCount = 0;
			var area = m_subsystem.GetActiveArea();

			foreach (Entity entity in m_subsystem.Project.Entities)
			{
				var farmer = entity.FindComponent<ComponentFarmerBehavior>();
				if (farmer == null || !farmer.FarmerEnabled) continue;
				var creature = entity.FindComponent<ComponentCreature>();
				if (creature == null) continue;

				list.Add(creature);
				if (area != null && farmer.FarmAreaId == area.Id)
					assignedCount++;
			}
			return list;
		}

		private void OpenBrowseDialog()
		{
			var area = m_subsystem.GetActiveArea();
			if (area == null) return;

			var available = new List<ComponentCreature>();
			foreach (Entity entity in m_subsystem.Project.Entities)
			{
				var farmer = entity.FindComponent<ComponentFarmerBehavior>();
				if (farmer == null || !farmer.FarmerEnabled) continue;
				if (farmer.FarmAreaId == area.Id) continue;
				var creature = entity.FindComponent<ComponentCreature>();
				if (creature == null) continue;
				available.Add(creature);
			}

			if (available.Count == 0)
			{
				m_player.ComponentGui.DisplaySmallMessage(
					LanguageControl.Get("FarmerAreaDialog", 16),
					Color.Yellow, true, true);
				return;
			}

			DialogsManager.ShowDialog(m_player.GuiWidget,
				new ListSelectionDialog(
					string.Format(LanguageControl.Get("FarmerAreaDialog", 18), area.Id),
					available,
					60f,
					(object item) => GetCreatureName((ComponentCreature)item),
					(object item) =>
					{
						var current = m_subsystem.GetActiveArea();
						if (current != null)
							m_subsystem.ToggleCreatureAssignment(current, (ComponentCreature)item);
					}));
		}

		private void OpenRemoveDialog()
		{
			var area = m_subsystem.GetActiveArea();
			if (area == null) return;

			var assigned = new List<ComponentCreature>();
			foreach (Entity entity in m_subsystem.Project.Entities)
			{
				var farmer = entity.FindComponent<ComponentFarmerBehavior>();
				if (farmer == null || !farmer.FarmerEnabled) continue;
				if (farmer.FarmAreaId != area.Id) continue;
				var creature = entity.FindComponent<ComponentCreature>();
				if (creature == null) continue;
				assigned.Add(creature);
			}

			if (assigned.Count == 0)
			{
				m_player.ComponentGui.DisplaySmallMessage(
					LanguageControl.Get("FarmerAreaDialog", 17),
					Color.Yellow, true, true);
				return;
			}

			DialogsManager.ShowDialog(m_player.GuiWidget,
				new ListSelectionDialog(
					string.Format(LanguageControl.Get("FarmerAreaDialog", 19), area.Id),
					assigned,
					60f,
					(object item) => GetCreatureName((ComponentCreature)item),
					(object item) =>
					{
						var current = m_subsystem.GetActiveArea();
						if (current != null)
							m_subsystem.ToggleCreatureAssignment(current, (ComponentCreature)item);
					}));
		}

		private static string GetCreatureName(ComponentCreature c)
		{
			string name = c.DisplayName;
			if (string.IsNullOrEmpty(name)) name = "Criatura";
			return name;
		}

		// -----------------------------------------------------------------
		//  Controles dinámicos
		// -----------------------------------------------------------------
		private void UpdateControls()
		{
			var area = m_subsystem.GetActiveArea();
			bool hasArea = area != null;
			bool hasBoth = hasArea && area.HasBothPoints;
			bool showArea = hasArea && area.ShowAreaPersistent;

			m_areaTitleLabel.Text = hasArea
				? string.Format(LanguageControl.Get("FarmerAreaDialog", 7),
					area.Id,
					m_subsystem.m_areas.IndexOf(area) + 1,
					m_subsystem.m_areas.Count)
				: LanguageControl.Get("FarmerAreaDialog", 6);

			m_deleteAreaButton.IsEnabled = hasArea && m_subsystem.m_areas.Count > 1;
			m_prevAreaButton.IsEnabled = m_subsystem.m_areas.Count > 1;
			m_nextAreaButton.IsEnabled = m_subsystem.m_areas.Count > 1;

			m_toggleShowButton.IsEnabled = hasBoth;
			var bev = m_toggleShowButton as BevelledButtonWidget;
			if (bev != null)
				bev.Text = showArea
					? LanguageControl.Get("FarmerAreaDialog", 5)
					: LanguageControl.Get("FarmerAreaDialog", 4);

			m_assignedCount = 0;
			CollectFarmers(out m_assignedCount);

			m_creaturesLabel.Text = string.Format(
				LanguageControl.Get("FarmerAreaDialog", 8), m_assignedCount);

			m_browseButton.IsEnabled = hasArea;
			m_removeButton.IsEnabled = hasArea && m_assignedCount > 0;
		}

		// -----------------------------------------------------------------
		//  Parseo
		// -----------------------------------------------------------------
		private bool TryParsePoints(out Point3? a, out Point3? b)
		{
			a = null;
			b = null;

			string rawA = m_textBoxA.Text;
			string rawB = m_textBoxB.Text;
			bool hasAnyA = !string.IsNullOrWhiteSpace(rawA);
			bool hasAnyB = !string.IsNullOrWhiteSpace(rawB);

			if (hasAnyA)
			{
				if (!TryParsePoint(rawA, out Point3 parsed))
				{
					m_player.ComponentGui.DisplaySmallMessage(
						LanguageControl.Get("FarmerAreaDialog", 20),
						Color.Red, true, true);
					return false;
				}
				a = parsed;
			}

			if (hasAnyB)
			{
				if (!TryParsePoint(rawB, out Point3 parsed))
				{
					m_player.ComponentGui.DisplaySmallMessage(
						LanguageControl.Get("FarmerAreaDialog", 21),
						Color.Red, true, true);
					return false;
				}
				b = parsed;
			}

			if ((a == null) != (b == null))
			{
				m_player.ComponentGui.DisplaySmallMessage(
					LanguageControl.Get("FarmerAreaDialog", 22),
					Color.Yellow, true, true);
				return false;
			}

			return true;
		}

		private static bool TryParsePoint(string text, out Point3 result)
		{
			result = default;
			if (string.IsNullOrWhiteSpace(text)) return false;

			string[] parts = text.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length != 3) return false;

			int x, y, z;
			if (int.TryParse(parts[0], out x) &&
				int.TryParse(parts[1], out y) &&
				int.TryParse(parts[2], out z))
			{
				result = new Point3(x, y, z);
				return true;
			}
			return false;
		}

		public void Dismiss() => DialogsManager.HideDialog(this);
	}
}
