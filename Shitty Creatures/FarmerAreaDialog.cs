using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;
using static Game.SubsystemFarmerWandBlockBehavior;

namespace Game
{
	/// <summary>
	/// Diálogo de administración de áreas de cultivo (transaccional).
	///
	/// Los cambios se aplican sobre una COPIA DE TRABAJO de las áreas y de las
	/// asignaciones de criaturas. NADA se escribe al subsistema hasta que el
	/// jugador pulsa "Aplicar". Si pulsa "Cancelar" (o cierra con Escape), los
	/// cambios pendientes se descartan. Así el botón "Aplicar" tiene un uso
	/// real: confirmar todos los cambios realizados en el diálogo.
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

		// -----------------------------------------------------------------
		//  Estado de trabajo (transaccional)
		//  Nada de esto toca el subsistema hasta pulsar "Aplicar".
		// -----------------------------------------------------------------
		private List<FarmArea> m_workAreas;
		private FarmArea m_workActive;
		private int m_workNextId;
		// Asignaciones de criaturas pendientes: farmer -> farmAreaId (-1 = sin área)
		private Dictionary<ComponentFarmerBehavior, int> m_workAssignments;

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

			InitializeWorkingState();

			ApplyStaticTexts();

			if (m_workActive == null && m_workAreas.Count == 0)
				CreateWorkingArea();

			PopulateFromArea();
			UpdateControls();
		}

		// -----------------------------------------------------------------
		//  Estado de trabajo
		// -----------------------------------------------------------------
		private static FarmArea CloneArea(FarmArea src)
		{
			return new FarmArea
			{
				Id = src.Id,
				PointA = src.PointA,
				PointB = src.PointB,
				Preview = src.Preview,
				ShowAreaPersistent = src.ShowAreaPersistent,
				PointBMarkedTime = src.PointBMarkedTime,
			};
		}

		private void InitializeWorkingState()
		{
			m_workAreas = new List<FarmArea>();
			foreach (var a in m_subsystem.m_areas)
				m_workAreas.Add(CloneArea(a));

			m_workActive = null;
			if (m_subsystem.m_activeArea != null)
			{
				int idx = m_subsystem.m_areas.IndexOf(m_subsystem.m_activeArea);
				if (idx >= 0 && idx < m_workAreas.Count)
					m_workActive = m_workAreas[idx];
			}
			if (m_workActive == null && m_workAreas.Count > 0)
				m_workActive = m_workAreas[0];

			m_workNextId = m_subsystem.m_nextAreaId;

			m_workAssignments = new Dictionary<ComponentFarmerBehavior, int>();
			foreach (var entity in m_subsystem.Project.Entities)
			{
				var farmer = entity.FindComponent<ComponentFarmerBehavior>();
				if (farmer != null)
					m_workAssignments[farmer] = farmer.FarmAreaId;
			}
		}

		/// <summary>
		/// Vuelca la copia de trabajo al subsistema. Solo se llama desde
		/// "Aplicar" tras validar los puntos.
		/// </summary>
		private void CommitWorkingState()
		{
			// 1) Áreas: sustituimos la lista del subsistema por la de trabajo.
			m_subsystem.m_areas.Clear();
			m_subsystem.m_areas.AddRange(m_workAreas);
			m_subsystem.m_activeArea = m_workActive;
			m_subsystem.m_nextAreaId = m_workNextId;

			// 2) Asignaciones de criaturas.
			foreach (var kv in m_workAssignments)
			{
				var farmer = kv.Key;
				int newAreaId = kv.Value;
				farmer.FarmAreaId = newAreaId;

				if (newAreaId >= 0)
				{
					var area = m_subsystem.FindAreaById(newAreaId);
					if (area != null && area.HasBothPoints)
						farmer.SetFarmArea(area.PointA.Value, area.PointB.Value);
				}
			}
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
			var area = m_workActive;
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

			// ---- APLICAR ----
			// Valida los puntos y, si son correctos, vuelca TODA la copia
			// de trabajo al subsistema (áreas + asignaciones) y cierra.
			if (m_okButton.IsClicked)
			{
				if (TryParsePoints(out Point3? a, out Point3? b))
				{
					var area = m_workActive;
					if (area != null)
					{
						bool changed = (area.PointA != a) || (area.PointB != b);
						area.PointA = a;
						area.PointB = b;
						area.Preview = null;
						if (changed)
							area.PointBMarkedTime = Time.RealTime;
					}
					CommitWorkingState();
					Dismiss();
				}
			}

			if (m_resetButton.IsClicked)
			{
				ResetWorkingAreaPoints(m_workActive);
				PopulateFromArea();
				UpdateControls();
			}

			if (m_toggleShowButton.IsClicked)
			{
				ToggleWorkingShow(m_workActive);
				UpdateControls();
			}

			UpdateControls();

			// ---- CANCELAR ----
			// Basta con cerrar: la copia de trabajo se descarta y el
			// subsistema queda intacto.
			if (base.Input.Cancel || m_cancelButton.IsClicked)
			{
				Dismiss();
			}
		}

		// -----------------------------------------------------------------
		//  Navegación y gestión de áreas (sobre la copia de trabajo)
		// -----------------------------------------------------------------
		private void HandleAreaNavigation()
		{
			if (m_prevAreaButton.IsClicked) CycleArea(-1);
			if (m_nextAreaButton.IsClicked) CycleArea(+1);

			if (m_newAreaButton.IsClicked)
			{
				CreateWorkingArea();
				PopulateFromArea();
				UpdateControls();
			}

			if (m_deleteAreaButton.IsClicked)
			{
				var area = m_workActive;
				if (area != null)
				{
					DeleteWorkingArea(area);
					if (m_workActive == null)
						CreateWorkingArea();
					PopulateFromArea();
					UpdateControls();
				}
			}
		}

		private void CycleArea(int delta)
		{
			if (m_workAreas.Count == 0) return;
			var cur = m_workActive;
			int idx = cur != null ? m_workAreas.IndexOf(cur) : -1;
			if (idx < 0) idx = 0;
			idx = (idx + delta + m_workAreas.Count) % m_workAreas.Count;
			m_workActive = m_workAreas[idx];
			PopulateFromArea();
			UpdateControls();
		}

		private FarmArea CreateWorkingArea()
		{
			var area = new FarmArea
			{
				Id = m_workNextId++,
				PointBMarkedTime = -1.0
			};
			m_workAreas.Add(area);
			m_workActive = area;
			return area;
		}

		private void DeleteWorkingArea(FarmArea area)
		{
			if (area == null) return;
			m_workAreas.Remove(area);

			// Desasignar criaturas de esa área (solo en la copia de trabajo).
			var farmers = new List<ComponentFarmerBehavior>();
			foreach (var kv in m_workAssignments)
			{
				if (kv.Value == area.Id)
					farmers.Add(kv.Key);
			}
			foreach (var f in farmers)
				m_workAssignments[f] = -1;

			if (m_workActive == area)
				m_workActive = m_workAreas.Count > 0 ? m_workAreas[0] : null;
		}

		private void ResetWorkingAreaPoints(FarmArea area)
		{
			if (area == null) return;
			area.PointA = null;
			area.PointB = null;
			area.Preview = null;
			area.ShowAreaPersistent = false;
			area.PointBMarkedTime = -1.0;

			// Igual que el Reset original: se desasignan las criaturas.
			var farmers = new List<ComponentFarmerBehavior>();
			foreach (var kv in m_workAssignments)
			{
				if (kv.Value == area.Id)
					farmers.Add(kv.Key);
			}
			foreach (var f in farmers)
				m_workAssignments[f] = -1;
		}

		private void ToggleWorkingShow(FarmArea area)
		{
			if (area == null) return;

			if (IsAreaVisibleFor(area))
			{
				area.ShowAreaPersistent = false;
				area.PointBMarkedTime = -1.0;
			}
			else
			{
				area.ShowAreaPersistent = true;
				area.PointBMarkedTime = Time.RealTime;
			}
		}

		/// <summary>
		/// Replica de SubsystemFarmerWandBlockBehavior.IsAreaVisible pero
		/// trabajando sobre una FarmArea local (no la del subsistema).
		/// </summary>
		private static bool IsAreaVisibleFor(FarmArea area)
		{
			if (area == null || !area.HasBothPoints)
				return false;

			if (area.ShowAreaPersistent)
				return true;

			return area.PointBMarkedTime >= 0.0
				&& (Time.RealTime - area.PointBMarkedTime) < AREA_DISPLAY_DURATION;
		}

		// -----------------------------------------------------------------
		//  Selección de criaturas (sobre la copia de trabajo)
		// -----------------------------------------------------------------
		private void OpenBrowseDialog()
		{
			var area = m_workActive;
			if (area == null) return;

			// Solo criaturas NO asignadas a ninguna área en la copia de trabajo.
			var available = new List<ComponentCreature>();
			foreach (Entity entity in m_subsystem.Project.Entities)
			{
				var farmer = entity.FindComponent<ComponentFarmerBehavior>();
				if (farmer == null || !farmer.FarmerEnabled) continue;

				int assignedId;
				if (m_workAssignments.TryGetValue(farmer, out assignedId) && assignedId != -1)
					continue;

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

			FarmArea capturedArea = area;
			DialogsManager.ShowDialog(m_player.GuiWidget,
				new ListSelectionDialog(
					string.Format(LanguageControl.Get("FarmerAreaDialog", 18), capturedArea.Id),
					available,
					60f,
					(object item) => GetCreatureName((ComponentCreature)item),
					(object item) =>
					{
						ToggleWorkingAssignment(capturedArea, (ComponentCreature)item);
					}));
		}

		private void OpenRemoveDialog()
		{
			var area = m_workActive;
			if (area == null) return;

			var assigned = new List<ComponentCreature>();
			int targetId = area.Id;
			foreach (Entity entity in m_subsystem.Project.Entities)
			{
				var farmer = entity.FindComponent<ComponentFarmerBehavior>();
				if (farmer == null || !farmer.FarmerEnabled) continue;

				int assignedId;
				if (!m_workAssignments.TryGetValue(farmer, out assignedId)) continue;
				if (assignedId != targetId) continue;

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

			FarmArea capturedArea = area;
			DialogsManager.ShowDialog(m_player.GuiWidget,
				new ListSelectionDialog(
					string.Format(LanguageControl.Get("FarmerAreaDialog", 19), capturedArea.Id),
					assigned,
					60f,
					(object item) => GetCreatureName((ComponentCreature)item),
					(object item) =>
					{
						ToggleWorkingAssignment(capturedArea, (ComponentCreature)item);
					}));
		}

		/// <summary>
		/// Alterna la asignación SOLO en la copia de trabajo. El cambio
		/// real al ComponentFarmerBehavior ocurre en CommitWorkingState().
		/// </summary>
		private void ToggleWorkingAssignment(FarmArea area, ComponentCreature creature)
		{
			if (area == null || creature == null) return;
			var farmer = creature.Entity.FindComponent<ComponentFarmerBehavior>();
			if (farmer == null) return;

			int current;
			if (!m_workAssignments.TryGetValue(farmer, out current))
				current = -1;

			m_workAssignments[farmer] = (current == area.Id) ? -1 : area.Id;
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
			var area = m_workActive;
			bool hasArea = area != null;
			bool hasBoth = hasArea && area.HasBothPoints;
			bool showArea = hasArea && IsAreaVisibleFor(area);

			int position = hasArea ? m_workAreas.IndexOf(area) + 1 : 0;

			m_areaTitleLabel.Text = hasArea
				? string.Format(LanguageControl.Get("FarmerAreaDialog", 7),
					position,
					position,
					m_workAreas.Count)
				: LanguageControl.Get("FarmerAreaDialog", 6);

			m_deleteAreaButton.IsEnabled = hasArea && m_workAreas.Count > 1;
			m_prevAreaButton.IsEnabled = m_workAreas.Count > 1;
			m_nextAreaButton.IsEnabled = m_workAreas.Count > 1;

			m_toggleShowButton.IsEnabled = hasBoth;
			var bev = m_toggleShowButton as BevelledButtonWidget;
			if (bev != null)
				bev.Text = showArea
					? LanguageControl.Get("FarmerAreaDialog", 5)
					: LanguageControl.Get("FarmerAreaDialog", 4);

			// Conteo por-área sobre la copia de trabajo.
			int count = 0;
			if (hasArea)
			{
				int targetAreaId = area.Id;
				foreach (Entity entity in m_subsystem.Project.Entities)
				{
					var farmer = entity.FindComponent<ComponentFarmerBehavior>();
					if (farmer == null || !farmer.FarmerEnabled) continue;

					int assignedId;
					if (!m_workAssignments.TryGetValue(farmer, out assignedId)) continue;
					if (assignedId != targetAreaId) continue;
					if (entity.FindComponent<ComponentCreature>() == null) continue;
					count++;
				}
			}
			m_assignedCount = count;

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
