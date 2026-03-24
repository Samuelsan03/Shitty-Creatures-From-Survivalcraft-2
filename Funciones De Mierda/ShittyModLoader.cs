using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class ShittyModLoader : ModLoader
	{
		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("OnMainMenuScreenCreated", this);
			ModsManager.RegisterHook("BeforeWidgetUpdate", this);
		}

		public override void OnMainMenuScreenCreated(MainMenuScreen mainMenuScreen, StackPanelWidget leftBottomBar, StackPanelWidget rightBottomBar)
		{
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
				// Verificar si ya existe la fila (para no duplicar)
				if (centerButtons.Children.Find<StackPanelWidget>("ShittyButtonRow", false) == null)
				{
					// Crear un panel horizontal para los dos botones
					StackPanelWidget buttonRow = new StackPanelWidget
					{
						Name = "ShittyButtonRow",
						Direction = LayoutDirection.Horizontal,
						HorizontalAlignment = WidgetAlignment.Center,
						Margin = new Vector2(0f, 5f)
					};

					// Botón "Acerca del Mod" (morado)
					string aboutButtonText = LanguageControl.Get(new string[] { "ShittyCreaturesAbout", "AboutButton" });
					BevelledButtonWidget aboutButton = new BevelledButtonWidget
					{
						Name = "ShittyAboutButton",
						Text = aboutButtonText,
						Size = new Vector2(310f, 60f),
						BevelColor = new Color(128, 0, 128),
						CenterColor = new Color(128, 0, 128),
						Margin = new Vector2(10f, 0f)  // Margen derecho para separar botones
					};

					// Botón "Salir" (color normal) usando traducción
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

			// Botón de changelog (original)
			BevelledButtonWidget shittyButton = mainMenu.Children.Find<BevelledButtonWidget>("ShittyButton", false);
			if (shittyButton != null && shittyButton.IsClicked)
			{
				DialogsManager.ShowDialog(null, new ShittyCreaturesLogDialog());
			}

			// Botón "Acerca Del Mod"
			BevelledButtonWidget aboutButton = mainMenu.Children.Find<BevelledButtonWidget>("ShittyAboutButton", false);
			if (aboutButton != null && aboutButton.IsClicked)
			{
				DialogsManager.ShowDialog(null, new ShittyCreaturesAboutDialog());
			}

			// Botón "Salir"
			BevelledButtonWidget exitButton = mainMenu.Children.Find<BevelledButtonWidget>("ShittyExitButton", false);
			if (exitButton != null && exitButton.IsClicked)
			{
				Environment.Exit(0);
			}
		}
	}
}
