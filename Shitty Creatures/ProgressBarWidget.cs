using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public enum ProgressState
	{
		Start,      // 0% - 33%
		Progress,   // 34% - 66%
		Almost,     // 67% - 99%
		Complete    // 100%
	}

	public class ProgressBarWidget : Widget
	{
		private float m_value;
		public float Value
		{
			get => m_value;
			set
			{
				m_value = Math.Clamp(value, 0f, 1f);
				UpdateColors();
			}
		}

		public Color BarColor { get; private set; } = new Color(70, 130, 180); // SteelBlue
		public Color BackgroundColor { get; set; } = new Color(60, 60, 60);
		public Vector2 BarSize { get; set; } = new Vector2(80f, 12f);

		public ProgressBarWidget()
		{
			IsHitTestVisible = false;
			IsDrawRequired = true;
			UpdateColors();
		}

		private void UpdateColors()
		{
			ProgressState state = GetState(Value);
			switch (state)
			{
				case ProgressState.Start:
					BarColor = new Color(70, 130, 180);   // SteelBlue
					break;
				case ProgressState.Progress:
					BarColor = new Color(64, 224, 208);   // Turquoise
					break;
				case ProgressState.Almost:
					BarColor = new Color(218, 165, 32);   // Goldenrod
					break;
				case ProgressState.Complete:
					BarColor = new Color(60, 179, 113);   // MediumSeaGreen
					break;
				default:
					BarColor = new Color(70, 130, 180);
					break;
			}
		}

		private ProgressState GetState(float value)
		{
			if (value >= 1f) return ProgressState.Complete;
			if (value >= 0.67f) return ProgressState.Almost;
			if (value >= 0.34f) return ProgressState.Progress;
			return ProgressState.Start;
		}

		public override void MeasureOverride(Vector2 parentAvailableSize)
		{
			DesiredSize = BarSize;
			IsDrawRequired = true;
		}

		public override void Draw(DrawContext dc)
		{
			if (ActualSize.X <= 0f || ActualSize.Y <= 0f)
				return;

			Vector2 size = ActualSize;
			Vector2 fillSize = new Vector2(size.X * Value, size.Y);

			// Fondo
			FlatBatch2D flatBatch = dc.PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None, null, BlendState.AlphaBlend);
			int start = flatBatch.TriangleVertices.Count;
			flatBatch.QueueQuad(Vector2.Zero, size, 0f, BackgroundColor);
			flatBatch.TransformTriangles(GlobalTransform, start, -1);

			// Barra rellena
			if (Value > 0f)
			{
				flatBatch = dc.PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None, null, BlendState.AlphaBlend);
				start = flatBatch.TriangleVertices.Count;
				flatBatch.QueueQuad(Vector2.Zero, fillSize, 0f, BarColor);
				flatBatch.TransformTriangles(GlobalTransform, start, -1);
			}

			// Borde
			flatBatch = dc.PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None, null, BlendState.AlphaBlend);
			start = flatBatch.TriangleVertices.Count;
			flatBatch.QueueRectangle(Vector2.Zero, size, 0f, new Color(100, 100, 100));
			flatBatch.TransformTriangles(GlobalTransform, start, -1);
		}
	}
}
