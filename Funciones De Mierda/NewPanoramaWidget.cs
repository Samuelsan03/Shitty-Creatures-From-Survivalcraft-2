using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class NewPanoramaWidget : Widget
	{
		public static List<string> TexturePaths = new List<string>
		{
			"Textures/Wallpapers/Digimon Fusion Wallpaper",
			"Textures/Wallpapers/Tai and Wargreymon",
			"Textures/Wallpapers/GTA SA wallpaper",
			"Textures/Wallpapers/Wallpaper digimon 1",
			"Textures/Wallpapers/sparkster rocket",
			"Textures/Wallpapers/left 4 dead",
			"Textures/Wallpapers/digimon frontier",
			"Textures/Wallpapers/multi digimon"
		};

		public Texture2D Texture { get; set; }
		public int CurrentTextureIndex { get; protected set; }
		public float FadeAlpha { get; set; }
		public float DisplayTime { get; set; }
		public float FadeTime { get; set; }

		public enum TransitionState
		{
			Showing,
			FadingOut,
			FadingIn
		}
		public TransitionState State = TransitionState.Showing;

		public NewPanoramaWidget()
		{
			CurrentTextureIndex = 0;
			LoadTexture(CurrentTextureIndex);
			FadeAlpha = 0f;
			DisplayTime = 0f;
			FadeTime = 0f;
		}

		protected virtual void LoadTexture(int index)
		{
			if (index >= 0 && index < TexturePaths.Count)
			{
				try
				{
					Texture = ContentManager.Get<Texture2D>(TexturePaths[index]);
				}
				catch
				{
					Texture = null;
				}
			}
		}

		public virtual void SwitchToNextTexture()
		{
			State = TransitionState.FadingOut;
			FadeTime = 0f;
		}

		public virtual void DrawBlackFade(Widget.DrawContext dc, float alpha)
		{
			if (alpha <= 0f) return;

			FlatBatch2D flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch(1, DepthStencilState.None, null, BlendState.AlphaBlend);
			Color color = Color.Black * alpha;
			Vector2 zero = Vector2.Zero;
			Vector2 actualSize = base.ActualSize;

			flatBatch2D.QueueQuad(zero, actualSize, 0f, color);
			flatBatch2D.TransformTriangles(base.GlobalTransform, 0, -1);
		}

		public virtual void DrawImage(Widget.DrawContext dc)
		{
			if (Texture == null) return;

			// Calcular escala para que la imagen cubra toda la pantalla sin deformarse
			float scaleX = base.ActualSize.X / (float)Texture.Width;
			float scaleY = base.ActualSize.Y / (float)Texture.Height;
			float scale = Math.Max(scaleX, scaleY);
			
			// Calcular tamaño y posición centrada
			Vector2 size = new Vector2((float)Texture.Width * scale, (float)Texture.Height * scale);
			Vector2 offset = new Vector2((base.ActualSize.X - size.X) / 2f, (base.ActualSize.Y - size.Y) / 2f);

			// Dibujar la textura completa y estática
			TexturedBatch2D texturedBatch2D = dc.PrimitivesRenderer2D.TexturedBatch(Texture, false, 0, DepthStencilState.DepthWrite, null, BlendState.AlphaBlend, SamplerState.LinearClamp);
			int count = texturedBatch2D.TriangleVertices.Count;
			texturedBatch2D.QueueQuad(offset, offset + size, 1f, Vector2.Zero, Vector2.One, base.GlobalColorTransform);
			texturedBatch2D.TransformTriangles(base.GlobalTransform, count, -1);
		}

		public override void MeasureOverride(Vector2 parentAvailableSize)
		{
			base.IsDrawRequired = true;
		}

		public override void Update()
		{
			switch (State)
			{
				case TransitionState.Showing:
					DisplayTime += Time.FrameDuration;
					if (DisplayTime >= 5f)
					{
						SwitchToNextTexture();
					}
					break;

				case TransitionState.FadingOut:
					FadeTime += Time.FrameDuration;
					FadeAlpha = MathUtils.Saturate(FadeTime / 1f);

					if (FadeTime >= 1f)
					{
						CurrentTextureIndex = (CurrentTextureIndex + 1) % TexturePaths.Count;
						LoadTexture(CurrentTextureIndex);
						State = TransitionState.FadingIn;
						FadeTime = 0f;
					}
					break;

				case TransitionState.FadingIn:
					FadeTime += Time.FrameDuration;
					FadeAlpha = 1f - MathUtils.Saturate(FadeTime / 1f);

					if (FadeTime >= 1f)
					{
						FadeAlpha = 0f;
						State = TransitionState.Showing;
						DisplayTime = 0f;
					}
					break;
			}
		}

		public override void Draw(Widget.DrawContext dc)
		{
			DrawImage(dc);

			if (FadeAlpha > 0f)
			{
				DrawBlackFade(dc, FadeAlpha);
			}
		}
	}
}
