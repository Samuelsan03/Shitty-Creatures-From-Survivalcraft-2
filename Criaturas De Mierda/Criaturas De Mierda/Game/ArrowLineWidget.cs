using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Engine.Serialization;

namespace Game
{
	// Token: 0x020003AB RID: 939
	public class ArrowLineWidget : Widget
	{
		// Token: 0x1700047B RID: 1147
		// (get) Token: 0x06001E61 RID: 7777 RVA: 0x000F16E4 File Offset: 0x000EF8E4
		// (set) Token: 0x06001E62 RID: 7778 RVA: 0x000F16EC File Offset: 0x000EF8EC
		public float Width
		{
			get
			{
				return this.m_width;
			}
			set
			{
				this.m_width = value;
				this.m_parsingPending = true;
			}
		}

		// Token: 0x1700047C RID: 1148
		// (get) Token: 0x06001E63 RID: 7779 RVA: 0x000F16FC File Offset: 0x000EF8FC
		// (set) Token: 0x06001E64 RID: 7780 RVA: 0x000F1704 File Offset: 0x000EF904
		public float ArrowWidth
		{
			get
			{
				return this.m_arrowWidth;
			}
			set
			{
				this.m_arrowWidth = value;
				this.m_parsingPending = true;
			}
		}

		// Token: 0x1700047D RID: 1149
		// (get) Token: 0x06001E65 RID: 7781 RVA: 0x000F1714 File Offset: 0x000EF914
		// (set) Token: 0x06001E66 RID: 7782 RVA: 0x000F171C File Offset: 0x000EF91C
		public Color Color { get; set; }

		// Token: 0x1700047E RID: 1150
		// (get) Token: 0x06001E67 RID: 7783 RVA: 0x000F1725 File Offset: 0x000EF925
		// (set) Token: 0x06001E68 RID: 7784 RVA: 0x000F172D File Offset: 0x000EF92D
		public string PointsString
		{
			get
			{
				return this.m_pointsString;
			}
			set
			{
				this.m_pointsString = value;
				this.m_parsingPending = true;
			}
		}

		// Token: 0x1700047F RID: 1151
		// (get) Token: 0x06001E69 RID: 7785 RVA: 0x000F173D File Offset: 0x000EF93D
		// (set) Token: 0x06001E6A RID: 7786 RVA: 0x000F1745 File Offset: 0x000EF945
		public bool AbsoluteCoordinates
		{
			get
			{
				return this.m_absoluteCoordinates;
			}
			set
			{
				this.m_absoluteCoordinates = value;
				this.m_parsingPending = true;
			}
		}

		// Token: 0x06001E6B RID: 7787 RVA: 0x000F1758 File Offset: 0x000EF958
		public ArrowLineWidget()
		{
			this.Width = 6f;
			this.ArrowWidth = 0f;
			this.Color = Color.White;
			this.IsHitTestVisible = false;
			this.PointsString = "0, 0; 50, 0";
		}

		// Token: 0x06001E6C RID: 7788 RVA: 0x000F17AC File Offset: 0x000EF9AC
		public override void Draw(Widget.DrawContext dc)
		{
			if (this.m_parsingPending)
			{
				this.ParsePoints();
			}
			Color color = this.Color * base.GlobalColorTransform;
			FlatBatch2D flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch(1, DepthStencilState.None, null, null);
			int count = flatBatch2D.TriangleVertices.Count;
			for (int i = 0; i < this.m_vertices.Count; i += 3)
			{
				Vector2 vector = this.m_startOffset + this.m_vertices[i];
				Vector2 vector2 = this.m_startOffset + this.m_vertices[i + 1];
				Vector2 vector3 = this.m_startOffset + this.m_vertices[i + 2];
				flatBatch2D.QueueTriangle(vector, vector2, vector3, 0f, color);
			}
			flatBatch2D.TransformTriangles(base.GlobalTransform, count, -1);
		}

		// Token: 0x06001E6D RID: 7789 RVA: 0x000F1880 File Offset: 0x000EFA80
		public override void MeasureOverride(Vector2 parentAvailableSize)
		{
			if (this.m_parsingPending)
			{
				this.ParsePoints();
			}
			base.IsDrawRequired = (this.Color.A > 0 && this.Width > 0f);
		}

		// Token: 0x06001E6E RID: 7790 RVA: 0x000F18C4 File Offset: 0x000EFAC4
		public void ParsePoints()
		{
			this.m_parsingPending = false;
			List<Vector2> list = new List<Vector2>();
			foreach (string text in this.m_pointsString.Split(new string[]
			{
				";"
			}, StringSplitOptions.None))
			{
				list.Add(HumanReadableConverter.ConvertFromString<Vector2>(text));
			}
			this.m_vertices.Clear();
			for (int j = 0; j < list.Count; j++)
			{
				if (j >= 1)
				{
					Vector2 vector = list[j - 1];
					Vector2 vector2 = list[j];
					Vector2 vector3 = Vector2.Normalize(vector2 - vector);
					Vector2 vector4 = vector3;
					Vector2 vector5 = vector3;
					if (j >= 2)
					{
						vector4 = Vector2.Normalize(vector - list[j - 2]);
					}
					if (j <= list.Count - 2)
					{
						vector5 = Vector2.Normalize(list[j + 1] - vector2);
					}
					Vector2 vector6 = Vector2.Perpendicular(vector4);
					Vector2 vector7 = Vector2.Perpendicular(vector3);
					float num = 3.1415927f - Vector2.Angle(vector4, vector3);
					float num2 = 0.5f * this.Width / MathF.Tan(num / 2f);
					Vector2 vector8 = 0.5f * vector6 * this.Width - vector4 * num2;
					float num3 = 3.1415927f - Vector2.Angle(vector3, vector5);
					float num4 = 0.5f * this.Width / MathF.Tan(num3 / 2f);
					Vector2 vector9 = 0.5f * vector7 * this.Width - vector3 * num4;
					this.m_vertices.Add(vector + vector8);
					this.m_vertices.Add(vector - vector8);
					this.m_vertices.Add(vector2 - vector9);
					this.m_vertices.Add(vector2 - vector9);
					this.m_vertices.Add(vector2 + vector9);
					this.m_vertices.Add(vector + vector8);
					if (j == list.Count - 1)
					{
						this.m_vertices.Add(vector2 - 0.5f * this.ArrowWidth * vector7);
						this.m_vertices.Add(vector2 + 0.5f * this.ArrowWidth * vector7);
						this.m_vertices.Add(vector2 + 0.5f * this.ArrowWidth * vector3);
					}
				}
			}
			if (this.m_vertices.Count <= 0)
			{
				base.DesiredSize = Vector2.Zero;
				this.m_startOffset = Vector2.Zero;
				return;
			}
			float? num5 = null;
			float? num6 = null;
			float? num7 = null;
			float? num8 = null;
			int k = 0;
			while (k < this.m_vertices.Count)
			{
				if (num5 == null)
				{
					goto IL_2FB;
				}
				float x = this.m_vertices[k].X;
				float? num9 = num5;
				if (x < num9.GetValueOrDefault() & num9 != null)
				{
					goto IL_2FB;
				}
				IL_314:
				if (num6 == null)
				{
					goto IL_346;
				}
				float y = this.m_vertices[k].Y;
				num9 = num6;
				if (y < num9.GetValueOrDefault() & num9 != null)
				{
					goto IL_346;
				}
				IL_35F:
				if (num7 == null)
				{
					goto IL_391;
				}
				float x2 = this.m_vertices[k].X;
				num9 = num7;
				if (x2 > num9.GetValueOrDefault() & num9 != null)
				{
					goto IL_391;
				}
				IL_3AA:
				if (num8 == null)
				{
					goto IL_3DC;
				}
				float y2 = this.m_vertices[k].Y;
				num9 = num8;
				if (y2 > num9.GetValueOrDefault() & num9 != null)
				{
					goto IL_3DC;
				}
				IL_3F5:
				k++;
				continue;
				IL_3DC:
				num8 = new float?(this.m_vertices[k].Y);
				goto IL_3F5;
				IL_391:
				num7 = new float?(this.m_vertices[k].X);
				goto IL_3AA;
				IL_346:
				num6 = new float?(this.m_vertices[k].Y);
				goto IL_35F;
				IL_2FB:
				num5 = new float?(this.m_vertices[k].X);
				goto IL_314;
			}
			if (this.AbsoluteCoordinates)
			{
				base.DesiredSize = new Vector2(num7.Value, num8.Value);
				this.m_startOffset = Vector2.Zero;
				return;
			}
			base.DesiredSize = new Vector2(num7.Value - num5.Value, num8.Value - num6.Value);
			this.m_startOffset = -new Vector2(num5.Value, num6.Value);
		}

		// Token: 0x040014B3 RID: 5299
		public string m_pointsString;

		// Token: 0x040014B4 RID: 5300
		public float m_width;

		// Token: 0x040014B5 RID: 5301
		public float m_arrowWidth;

		// Token: 0x040014B6 RID: 5302
		public bool m_absoluteCoordinates;

		// Token: 0x040014B7 RID: 5303
		public List<Vector2> m_vertices = new List<Vector2>();

		// Token: 0x040014B8 RID: 5304
		public bool m_parsingPending;

		// Token: 0x040014B9 RID: 5305
		public Vector2 m_startOffset;
	}
}
