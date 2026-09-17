using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Varilla del granjero — soporta múltiples áreas.
	/// Cada área: A, B, flag de visualización persistente, y lista de criaturas
	/// asignadas (por FarmAreaId en ComponentFarmerBehavior). Se persisten en el save.
	/// </summary>
	public class SubsystemFarmerWandBlockBehavior : SubsystemBlockBehavior, IUpdateable, IDrawable
	{
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemTime m_subsystemTime;
		public SubsystemSky m_subsystemSky;
		public SubsystemGameWidgets m_subsystemGameWidgets;

		public PrimitivesRenderer3D m_primitivesRenderer = new PrimitivesRenderer3D();

		public const double AREA_DISPLAY_DURATION = 5.0;

		// ---------------------------------------------------------------
		//  FarmArea
		// ---------------------------------------------------------------
		public class FarmArea
		{
			public int Id;
			public Point3? PointA;
			public Point3? PointB;
			public Point3? Preview;             // efímero
			public bool ShowAreaPersistent;
			public double PointBMarkedTime;   // < 0 significa "sin marca reciente"

			public bool HasBothPoints => PointA != null && PointB != null;

			public Vector3 GetCenter()
			{
				if (!HasBothPoints) return Vector3.Zero;
				Point3 a = PointA.Value, b = PointB.Value;
				int minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X);
				int minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y);
				int minZ = Math.Min(a.Z, b.Z), maxZ = Math.Max(a.Z, b.Z);
				return new Vector3(
					(minX + maxX + 1) * 0.5f,
					(minY + maxY + 1) * 0.5f,
					(minZ + maxZ + 1) * 0.5f);
			}

			public float GetRadius()
			{
				if (!HasBothPoints) return 0f;
				Point3 a = PointA.Value, b = PointB.Value;
				int dx = Math.Abs(a.X - b.X) + 1;
				int dz = Math.Abs(a.Z - b.Z) + 1;
				return MathF.Max(dx, dz) * 0.5f;
			}
		}

		public List<FarmArea> m_areas = new List<FarmArea>();
		public FarmArea m_activeArea;
		public int m_nextAreaId = 1;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public int[] DrawOrders => new int[] { 1999 };

		public override int[] HandledBlocks
		{
			get
			{
				int idx = BlocksManager.GetBlockIndex(typeof(FarmerWandBlock), false, false);
				if (idx < 0) idx = FarmerWandBlock.Index;
				return new int[] { idx };
			}
		}

		// ---------------------------------------------------------------
		//  Load / Save
		// ---------------------------------------------------------------
		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);

			m_subsystemTerrain = SubsystemTerrain;
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemGameWidgets = Project.FindSubsystem<SubsystemGameWidgets>(true);

			m_areas = new List<FarmArea>();
			m_activeArea = null;

			ValuesDictionary areasDict = valuesDictionary.GetValue<ValuesDictionary>("FarmAreas", null);
			if (areasDict != null)
			{
				foreach (object obj in areasDict.Values)
				{
					ValuesDictionary areaDict = obj as ValuesDictionary;
					if (areaDict == null) continue;

					FarmArea area = new FarmArea();
					area.Id = areaDict.GetValue<int>("Id", 0);

					if (areaDict.GetValue<bool>("HasPointA", false))
						area.PointA = areaDict.GetValue<Point3>("PointA");
					if (areaDict.GetValue<bool>("HasPointB", false))
						area.PointB = areaDict.GetValue<Point3>("PointB");

					area.ShowAreaPersistent = areaDict.GetValue<bool>("ShowAreaPersistent", false);

					// NUEVO: recuperar la marca temporal como "segundos transcurridos"
					// para no depender de un Time.RealTime absoluto.
					double elapsed = areaDict.GetValue<double>("PointBMarkedElapsed", -1.0);
					area.PointBMarkedTime = elapsed >= 0.0
						? Time.RealTime - elapsed
						: -1.0;

					m_areas.Add(area);
				}
			}

			m_nextAreaId = valuesDictionary.GetValue<int>("NextAreaId", 1);

			int activeId = valuesDictionary.GetValue<int>("ActiveAreaId", -1);
			if (activeId >= 0) m_activeArea = FindAreaById(activeId);
			if (m_activeArea == null && m_areas.Count > 0) m_activeArea = m_areas[0];
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			base.Save(valuesDictionary);

			ValuesDictionary areasDict = new ValuesDictionary();
			valuesDictionary.SetValue("FarmAreas", areasDict);

			int idx = 0;
			foreach (FarmArea area in m_areas)
			{
				ValuesDictionary areaDict = new ValuesDictionary();
				areasDict.SetValue(idx.ToString(), areaDict);
				areaDict.SetValue("Id", area.Id);
				areaDict.SetValue("HasPointA", area.PointA != null);
				if (area.PointA != null) areaDict.SetValue("PointA", area.PointA.Value);
				areaDict.SetValue("HasPointB", area.PointB != null);
				if (area.PointB != null) areaDict.SetValue("PointB", area.PointB.Value);
				areaDict.SetValue("ShowAreaPersistent", area.ShowAreaPersistent);

				// NUEVO: persistir el "recién marcada" como delta relativo.
				double elapsed = area.PointBMarkedTime >= 0.0
					? Math.Max(0.0, Time.RealTime - area.PointBMarkedTime)
					: -1.0;
				areaDict.SetValue("PointBMarkedElapsed", elapsed);

				idx++;
			}

			valuesDictionary.SetValue("NextAreaId", m_nextAreaId);
			valuesDictionary.SetValue("ActiveAreaId", m_activeArea != null ? m_activeArea.Id : -1);
		}

		// ---------------------------------------------------------------
		//  API de áreas (usada por el diálogo)
		// ---------------------------------------------------------------
		public FarmArea GetActiveArea() => m_activeArea;
		public void SetActiveArea(FarmArea area) => m_activeArea = area;
		public FarmArea FindAreaById(int id) => m_areas.Find(a => a.Id == id);

		public FarmArea CreateArea()
		{
			FarmArea area = new FarmArea
			{
				Id = m_nextAreaId++,
				PointBMarkedTime = -1.0      // sin marca reciente
			};
			m_areas.Add(area);
			m_activeArea = area;
			return area;
		}

		public void DeleteArea(FarmArea area)
		{
			if (area == null) return;
			m_areas.Remove(area);

			foreach (Entity entity in Project.Entities)
			{
				ComponentFarmerBehavior farmer = entity.FindComponent<ComponentFarmerBehavior>();
				if (farmer != null && farmer.FarmAreaId == area.Id)
					farmer.FarmAreaId = -1;
			}

			if (m_activeArea == area)
				m_activeArea = m_areas.Count > 0 ? m_areas[0] : null;
		}

		public void ResetAreaPoints(FarmArea area)
		{
			if (area == null) return;
			area.PointA = null;
			area.PointB = null;
			area.Preview = null;
			area.ShowAreaPersistent = false;
			area.PointBMarkedTime = -1.0;

			foreach (Entity entity in Project.Entities)
			{
				ComponentFarmerBehavior farmer = entity.FindComponent<ComponentFarmerBehavior>();
				if (farmer != null && farmer.FarmAreaId == area.Id)
					farmer.FarmAreaId = -1;
			}
		}

		public void ToggleShowArea(FarmArea area)
		{
			if (area == null) return;

			if (IsAreaVisible(area))
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

		public bool IsCreatureAssignedToArea(FarmArea area, ComponentCreature creature)
		{
			if (area == null || creature == null) return false;
			ComponentFarmerBehavior farmer = creature.Entity.FindComponent<ComponentFarmerBehavior>();
			return farmer != null && farmer.FarmAreaId == area.Id;
		}

		public void ToggleCreatureAssignment(FarmArea area, ComponentCreature creature)
		{
			if (area == null || creature == null) return;
			ComponentFarmerBehavior farmer = creature.Entity.FindComponent<ComponentFarmerBehavior>();
			if (farmer == null) return;

			if (farmer.FarmAreaId == area.Id)
			{
				farmer.FarmAreaId = -1;
			}
			else
			{
				farmer.FarmAreaId = area.Id;
				if (area.HasBothPoints)
					farmer.SetFarmArea(area.PointA.Value, area.PointB.Value);
			}
		}

		public void ApplyFarmAreaToAssignedCreatures(FarmArea area)
		{
			if (area == null || !area.HasBothPoints) return;
			Point3 a = area.PointA.Value;
			Point3 b = area.PointB.Value;

			foreach (Entity entity in Project.Entities)
			{
				ComponentFarmerBehavior farmer = entity.FindComponent<ComponentFarmerBehavior>();
				if (farmer != null && farmer.FarmAreaId == area.Id)
					farmer.SetFarmArea(a, b);
			}
		}

		// ---------------------------------------------------------------
		//  Apertura del diálogo
		// ---------------------------------------------------------------
		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			if (componentPlayer == null) return false;
			DialogsManager.ShowDialog(componentPlayer.GuiWidget,
				new FarmerAreaDialog(this, componentPlayer));
			return true;
		}

		// ---------------------------------------------------------------
		//  Uso de la varilla
		// ---------------------------------------------------------------
		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			ComponentPlayer player = componentMiner.ComponentPlayer;
			if (player == null) return false;

			TerrainRaycastResult? hit = componentMiner.Raycast<TerrainRaycastResult>(
				ray, RaycastMode.Interaction, true, false, false, 40f);

			if (hit == null)
			{
				player.ComponentGui.DisplaySmallMessage(
					LanguageControl.Get("SubsystemFarmerWandBlockBehavior", 0),
					Color.Yellow, true, true);
				return false;
			}

			Point3 target = hit.Value.CellFace.Point;

			if (m_activeArea == null)
				CreateArea();

			FarmArea area = m_activeArea;

			if (area.PointA == null)
			{
				area.PointA = target;
				area.PointB = null;
				area.Preview = null;
				area.ShowAreaPersistent = false;
				player.ComponentGui.DisplaySmallMessage(
					string.Format(
						LanguageControl.Get("SubsystemFarmerWandBlockBehavior", 1),
						target.X, target.Y, target.Z),
					new Color(0, 220, 0, 255), true, true);
			}
			else if (area.PointB == null)
			{
				if (area.PointA.Value == target)
				{
					player.ComponentGui.DisplaySmallMessage(
						LanguageControl.Get("SubsystemFarmerWandBlockBehavior", 2),
						Color.Yellow, true, true);
					return true;
				}
				area.PointB = target;
				area.Preview = null;
				area.PointBMarkedTime = Time.RealTime;

				player.ComponentGui.DisplaySmallMessage(
					string.Format(
						LanguageControl.Get("SubsystemFarmerWandBlockBehavior", 3),
						target.X, target.Y, target.Z),
					new Color(255, 150, 0, 255), true, true);

				ApplyFarmAreaToAssignedCreatures(area);
			}
			else
			{
				area.PointA = target;
				area.PointB = null;
				area.Preview = null;
				area.ShowAreaPersistent = false;
				player.ComponentGui.DisplaySmallMessage(
					string.Format(
						LanguageControl.Get("SubsystemFarmerWandBlockBehavior", 4),
						area.Id),
					Color.Orange, true, true);
			}

			return true;
		}

		// ---------------------------------------------------------------
		//  Update (preview de la mira en el área activa)
		// ---------------------------------------------------------------
		public void Update(float dt)
		{
			foreach (FarmArea a in m_areas) a.Preview = null;

			if (m_activeArea == null) return;
			if (m_activeArea.PointA == null || m_activeArea.PointB != null) return;

			Camera camera = null;
			ComponentMiner miner = null;
			foreach (Entity entity in Project.Entities)
			{
				ComponentPlayer p = entity.FindComponent<ComponentPlayer>();
				if (p == null || p.GameWidget == null) continue;
				Camera cam = p.GameWidget.ActiveCamera;
				if (cam == null) continue;
				camera = cam;
				miner = p.ComponentMiner;
				break;
			}
			if (camera == null || miner == null) return;

			Ray3 ray = new Ray3(camera.ViewPosition, camera.ViewDirection);
			TerrainRaycastResult? hit = miner.Raycast<TerrainRaycastResult>(
				ray, RaycastMode.Interaction, true, false, false, 40f);

			m_activeArea.Preview = hit != null ? hit.Value.CellFace.Point : (Point3?)null;
		}

		// ---------------------------------------------------------------
		//  Draw
		// ---------------------------------------------------------------
		public void Draw(Camera camera, int drawOrder)
		{
			double now = Time.RealTime;

			foreach (FarmArea area in m_areas)
			{
				bool hasA = area.PointA != null;
				bool hasB = area.PointB != null;
				bool isActive = area == m_activeArea;

				// 1) Solo A marcada → feedback de marcado, no es "el campo asignado".
				//    Se dibuja siempre (para poder elegir B), independientemente
				//    del ShowAreaPersistent.
				if (hasA && !hasB)
				{
					Color cA = isActive
						? new Color(0, 220, 0, 210)
						: new Color(0, 220, 0, 130);
					DrawPointMarker(area.PointA.Value, cA);

					if (isActive && area.Preview != null && area.Preview.Value != area.PointA.Value)
					{
						DrawAreaBox(area.PointA.Value, area.Preview.Value,
									new Color(0, 200, 255, 130));
					}
					continue;
				}

				else if (hasA && hasB)
				{
					if (!IsAreaVisible(area))
						continue;

					DrawPointMarker(area.PointA.Value, new Color(0, 220, 0, 210));
					DrawPointMarker(area.PointB.Value, new Color(255, 140, 0, 210));

					Color lineColor = isActive
						? new Color(255, 220, 0, 160)
						: new Color(180, 180, 180, 110);

					DrawAreaBox(area.PointA.Value, area.PointB.Value, lineColor);
				}
			}

				m_primitivesRenderer.Flush(camera.ViewProjectionMatrix, true, int.MaxValue);
		}

		private void DrawPointMarker(Point3 p, Color color)
		{
			Vector3 min = new Vector3(p.X - 0.02f, p.Y - 0.02f, p.Z - 0.02f);
			Vector3 max = new Vector3(p.X + 1.02f, p.Y + 1.02f, p.Z + 1.02f);
			FlatBatch3D batch = m_primitivesRenderer.FlatBatch(0, DepthStencilState.None, null, null);
			batch.QueueBoundingBox(new BoundingBox(min, max), color);
		}

		private void DrawAreaBox(Point3 a, Point3 b, Color color)
		{
			int minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X) + 1;
			int minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y) + 1;
			int minZ = Math.Min(a.Z, b.Z), maxZ = Math.Max(a.Z, b.Z) + 1;
			BoundingBox box = new BoundingBox(
				new Vector3(minX, minY, minZ),
				new Vector3(maxX, maxY, maxZ));
			FlatBatch3D batch = m_primitivesRenderer.FlatBatch(0, DepthStencilState.None, null, null);
			batch.QueueBoundingBox(box, color);
		}

		/// <summary>
		/// True si el área debe dibujarse AHORA: ya sea porque el jugador la
		/// fijó como persistente o porque fue marcada hace menos de AREA_DISPLAY_DURATION.
		/// Es la única fuente de verdad para dibujo y para el estado del botón.
		/// </summary>
		public bool IsAreaVisible(FarmArea area)
		{
			if (area == null || !area.HasBothPoints)
				return false;

			if (area.ShowAreaPersistent)
				return true;

			return area.PointBMarkedTime >= 0.0
				&& (Time.RealTime - area.PointBMarkedTime) < AREA_DISPLAY_DURATION;
		}
	}
}
