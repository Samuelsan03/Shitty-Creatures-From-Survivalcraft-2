using System;
using System.Collections.Generic;
using System.Reflection;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class ShittyModLoader : ModLoader
	{
		private static FieldInfo m_cachesField;

		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("OnMainMenuScreenCreated", this);
			ModsManager.RegisterHook("BeforeWidgetUpdate", this);

			// --- Reemplazar overlay de captura de pantalla con logo personalizado (620x220) ---
			ReplaceScreenCaptureOverlay();
		}

		private void ReplaceScreenCaptureOverlay()
		{
			try
			{
				// Obtener la caché privada de ContentManager mediante reflexión
				if (m_cachesField == null)
				{
					m_cachesField = typeof(ContentManager).GetField("Caches",
						BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

					if (m_cachesField == null)
					{
						Log.Error("[ShittyCreatures] No se pudo encontrar ContentManager.Caches");
						return;
					}
				}

				var caches = m_cachesField.GetValue(null) as System.Collections.IDictionary;
				if (caches == null)
				{
					Log.Error("[ShittyCreatures] ContentManager.Caches es null");
					return;
				}

				// Cargar la textura personalizada
				Texture2D customOverlay = ContentManager.Get<Texture2D>("Textures/Gui/ScreenCaptureOverlay");
				if (customOverlay == null)
				{
					Log.Error("[ShittyCreatures] No se pudo cargar la textura personalizada");
					return;
				}

				string key = "Textures/Gui/ScreenCaptureOverlay";

				// Buscar o crear la entrada en la caché
				if (!caches.Contains(key))
				{
					caches[key] = new List<object>();
				}

				var cacheList = caches[key] as List<object>;
				if (cacheList != null)
				{
					// Eliminar cualquier Texture2D existente
					for (int i = cacheList.Count - 1; i >= 0; i--)
					{
						if (cacheList[i] is Texture2D)
						{
							cacheList.RemoveAt(i);
						}
					}
					cacheList.Add(customOverlay);
					Log.Information("[ShittyCreatures] Overlay de captura personalizado cargado (620x220)");
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[ShittyCreatures] Error al cargar overlay personalizado: {ex.Message}");
			}
		}

		public override void OnMainMenuScreenCreated(MainMenuScreen mainMenuScreen, StackPanelWidget leftBottomBar, StackPanelWidget rightBottomBar)
		{
			// --- AJUSTAR EL LOGO PRINCIPAL CON TAMAÑO PERSONALIZADO (336x128) ---
			RectangleWidget logo = mainMenuScreen.Children.Find<RectangleWidget>("Logo", true);
			if (logo != null)
			{
				logo.Size = new Vector2(336f, 128f);
				logo.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/Logo");
				logo.HorizontalAlignment = WidgetAlignment.Center;
				logo.TextureLinearFilter = true;
				logo.Margin = new Vector2(0f, 5f);
			}

			// --- AÑADIR ETIQUETA DE VERSIÓN DEL MOD ---
			StackPanelWidget topArea = mainMenuScreen.Children.Find<StackPanelWidget>("TopArea", true);
			LabelWidget versionLabel = mainMenuScreen.Children.Find<LabelWidget>("Version", true);
			if (topArea != null && versionLabel != null)
			{
				LabelWidget modVersionLabel = new LabelWidget
				{
					Name = "ShittyCreaturesVersion",
					FontScale = 0.75f,
					HorizontalAlignment = WidgetAlignment.Center,
					Color = new Color(215, 0, 0),
					DropShadow = true,
					Text = "Shitty Creatures v1.0.6"
				};
				topArea.Children.InsertBefore(versionLabel, modVersionLabel);
			}

			// --- AÑADIR BOTONES "ACERCA DEL MOD" Y "SALIR" EN EL CENTRO ---
			StackPanelWidget centerButtons = mainMenuScreen.Children.Find<StackPanelWidget>("CenterButtons", true);
			if (centerButtons != null)
			{
				if (centerButtons.Children.Find<StackPanelWidget>("ShittyButtonRow", false) == null)
				{
					StackPanelWidget buttonRow = new StackPanelWidget
					{
						Name = "ShittyButtonRow",
						Direction = LayoutDirection.Horizontal,
						HorizontalAlignment = WidgetAlignment.Center,
						Margin = new Vector2(0f, 5f)
					};

					string aboutButtonText = LanguageControl.Get(new string[] { "ShittyCreaturesAbout", "AboutButton" });
					BevelledButtonWidget aboutButton = new BevelledButtonWidget
					{
						Name = "ShittyAboutButton",
						Text = aboutButtonText,
						Size = new Vector2(310f, 60f),
						BevelColor = new Color(128, 0, 128),
						CenterColor = new Color(128, 0, 128),
						Margin = new Vector2(10f, 0f)
					};

					string exitButtonText = LanguageControl.Get(new string[] { "ShittyCreaturesAbout", "ExitButton" });
					BevelledButtonWidget exitButton = new BevelledButtonWidget
					{
						Name = "ShittyExitButton",
						Text = exitButtonText,
						Size = new Vector2(310f, 60f),
						BevelColor = new Color(128, 128, 128),
						CenterColor = new Color(128, 128, 128),
						Margin = new Vector2(10f, 0f)
					};

					buttonRow.Children.Add(aboutButton);
					buttonRow.Children.Add(exitButton);
					centerButtons.Children.Add(buttonRow);
				}
			}

			// --- BOTÓN ORIGINAL (ESQUINA INFERIOR DERECHA) ---
			BevelledButtonWidget existing = rightBottomBar.Children.Find<BevelledButtonWidget>("ShittyButton", false);
			if (existing == null)
			{
				BevelledButtonWidget button = new BevelledButtonWidget
				{
					Name = "ShittyButton",
					Size = new Vector2(60f, 60f)
				};
				RectangleWidget icon = new RectangleWidget
				{
					Size = new Vector2(28f, 28f),
					TextureLinearFilter = true,
					HorizontalAlignment = WidgetAlignment.Center,
					VerticalAlignment = WidgetAlignment.Center,
					Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/Veemon Logo"),
					OutlineColor = new Color(0, 0, 0, 0),
					FillColor = Color.White,
					IsVisible = true,
					TextureAnisotropicFilter = true,
					BlendState = BlendState.NonPremultiplied
				};
				button.Children.Add(icon);
				rightBottomBar.Children.Add(button);
			}
		}

		public override void BeforeWidgetUpdate(Widget widget)
		{
			MainMenuScreen mainMenu = widget as MainMenuScreen;
			if (mainMenu == null) return;

			BevelledButtonWidget shittyButton = mainMenu.Children.Find<BevelledButtonWidget>("ShittyButton", false);
			if (shittyButton != null && shittyButton.IsClicked)
			{
				DialogsManager.ShowDialog(null, new ShittyCreaturesLogDialog());
			}

			BevelledButtonWidget aboutButton = mainMenu.Children.Find<BevelledButtonWidget>("ShittyAboutButton", false);
			if (aboutButton != null && aboutButton.IsClicked)
			{
				DialogsManager.ShowDialog(null, new ShittyCreaturesAboutDialog());
			}

			BevelledButtonWidget exitButton = mainMenu.Children.Find<BevelledButtonWidget>("ShittyExitButton", false);
			if (exitButton != null && exitButton.IsClicked)
			{
				Environment.Exit(0);
			}
		}
	}
}
