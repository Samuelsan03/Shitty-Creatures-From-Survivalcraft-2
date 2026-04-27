using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class NewPanoramaWidget : Widget
	{
		private static readonly string[] WallpaperPaths = new string[]
		{
			"Textures/Wallpapers/Digimon Fusion Wallpaper",
			"Textures/Wallpapers/Tai and Wargreymon",
			"Textures/Wallpapers/GTA SA wallpaper",
			"Textures/Wallpapers/Wallpaper digimon 1",
			"Textures/Wallpapers/sparkster rocket",
			"Textures/Wallpapers/left 4 dead",
			"Textures/Wallpapers/digimon frontier",
			"Textures/Wallpapers/multi digimon",
			"Textures/Wallpapers/axel gear",
			"Textures/Wallpapers/infinite jack",
			"Textures/Wallpapers/jack metamorphosis",
			"Textures/Wallpapers/jack the chacal serious",
		};

		private enum TransitionState
		{
			ShowingTexture,
			FadingToBlack,
			HoldingBlack,
			SwitchingTexture,
			FadingFromBlack
		}

		private TransitionState m_transitionState = TransitionState.ShowingTexture;
		private float m_transitionTime = 0f;
		private float m_blackFadeAlpha = 0f;

		private Texture2D currentTexture;
		private Texture2D nextTexture;
		private int currentIndex = 0;
		private int nextIndex = 0;   // Almacena el índice de la siguiente textura

		// Tiempos ajustables (en segundos)
		private const float ShowDuration = 5f;
		private const float FadeDuration = 0.5f;
		private const float HoldBlackDuration = 0f;

		// Generador de números aleatorios
		private static Random m_random = new Random();

		public NewPanoramaWidget()
		{
			// Elegir índice inicial aleatorio
			currentIndex = m_random.Int(0, WallpaperPaths.Length - 1);
			currentTexture = ContentManager.Get<Texture2D>(WallpaperPaths[currentIndex]);
		}

		public override void Update()
		{
			base.Update();
			float dt = Time.FrameDuration;

			switch (m_transitionState)
			{
				case TransitionState.ShowingTexture:
					m_transitionTime += dt;
					if (m_transitionTime >= ShowDuration)
					{
						m_transitionState = TransitionState.FadingToBlack;
						m_transitionTime = 0f;
					}
					break;

				case TransitionState.FadingToBlack:
					m_transitionTime += dt;
					m_blackFadeAlpha = MathUtils.Saturate(m_transitionTime / FadeDuration);
					if (m_transitionTime >= FadeDuration)
					{
						m_transitionState = TransitionState.HoldingBlack;
						m_transitionTime = 0f;
					}
					break;

				case TransitionState.HoldingBlack:
					m_transitionTime += dt;
					m_blackFadeAlpha = 1f;
					if (m_transitionTime >= HoldBlackDuration)
					{
						m_transitionState = TransitionState.SwitchingTexture;
						m_transitionTime = 0f;

						// Seleccionar índice aleatorio diferente al actual (si hay más de una imagen)
						if (WallpaperPaths.Length > 1)
						{
							do
							{
								nextIndex = m_random.Int(0, WallpaperPaths.Length - 1);
							} while (nextIndex == currentIndex);
						}
						else
						{
							nextIndex = 0;
						}
						nextTexture = ContentManager.Get<Texture2D>(WallpaperPaths[nextIndex]);
					}
					break;

				case TransitionState.SwitchingTexture:
					m_transitionTime += dt;
					if (m_transitionTime >= 0.05f) // breve pausa para cargar
					{
						currentTexture = nextTexture;
						currentIndex = nextIndex;   // Actualizar el índice actual directamente
						nextTexture = null;

						m_transitionState = TransitionState.FadingFromBlack;
						m_transitionTime = 0f;
					}
					break;

				case TransitionState.FadingFromBlack:
					m_transitionTime += dt;
					m_blackFadeAlpha = 1f - MathUtils.Saturate(m_transitionTime / FadeDuration);
					if (m_transitionTime >= FadeDuration)
					{
						m_transitionState = TransitionState.ShowingTexture;
						m_blackFadeAlpha = 0f;
						m_transitionTime = 0f;
					}
					break;
			}
		}

		public override void Draw(Widget.DrawContext dc)
		{
			Vector2 screenSize = base.ActualSize;
			float screenAspect = screenSize.X / screenSize.Y;
			float textureAspect = (float)currentTexture.Width / currentTexture.Height;

			Vector2 texCoord0, texCoord1;

			if (textureAspect > screenAspect)
			{
				float scale = screenAspect / textureAspect;
				texCoord0 = new Vector2((1f - scale) / 2f, 0f);
				texCoord1 = new Vector2(1f - ((1f - scale) / 2f), 1f);
			}
			else
			{
				float scale = textureAspect / screenAspect;
				texCoord0 = new Vector2(0f, (1f - scale) / 2f);
				texCoord1 = new Vector2(1f, 1f - ((1f - scale) / 2f));
			}

			// Dibujar la textura actual
			if (currentTexture != null)
			{
				TexturedBatch2D batch = dc.PrimitivesRenderer2D.TexturedBatch(
					currentTexture, false, 0, DepthStencilState.DepthWrite,
					null, BlendState.AlphaBlend, SamplerState.LinearClamp);

				int count = batch.TriangleVertices.Count;
				batch.QueueQuad(Vector2.Zero, screenSize, 1f, texCoord0, texCoord1, base.GlobalColorTransform);
				batch.TransformTriangles(base.GlobalTransform, count, -1);
			}

			// Dibujar el rectángulo negro de transición
			if (m_blackFadeAlpha > 0.01f)
			{
				FlatBatch2D fadeBatch = dc.PrimitivesRenderer2D.FlatBatch(1, DepthStencilState.None, null, BlendState.AlphaBlend);
				int countFade = fadeBatch.TriangleVertices.Count;
				fadeBatch.QueueQuad(Vector2.Zero, screenSize, 0f, new Color(0f, 0f, 0f, m_blackFadeAlpha));
				fadeBatch.TransformTriangles(base.GlobalTransform, countFade, -1);
			}
		}

		public override void MeasureOverride(Vector2 parentAvailableSize)
		{
			base.IsDrawRequired = true;
		}
	}
}
