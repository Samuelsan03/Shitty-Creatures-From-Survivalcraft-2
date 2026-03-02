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
					FontScale = 0.6f,
					HorizontalAlignment = WidgetAlignment.Center,
					Color = new Color(64, 192, 64),
					DropShadow = true,
					Text = "Shitty Creatures v1.0.6"
				};
				topArea.Children.InsertBefore(versionLabel, modVersionLabel);
			}

			// --- AÑADIR BOTÓN "ACERCA DEL MOD" EN EL CENTRO (CON TRADUCCIÓN) ---
			StackPanelWidget centerButtons = mainMenuScreen.Children.Find<StackPanelWidget>("CenterButtons", true);
			if (centerButtons != null)
			{
				// Crear una nueva fila horizontal para el botón
				StackPanelWidget newRow = new StackPanelWidget
				{
					HorizontalAlignment = WidgetAlignment.Center,
					Margin = new Vector2(0f, 5f)
				};

				// Obtener texto traducido para el botón
				string aboutButtonText = LanguageControl.Get(new string[] { "ShittyCreaturesAbout", "AboutButton" });

				// Crear el botón morado con texto traducido
				BevelledButtonWidget aboutButton = new BevelledButtonWidget
				{
					Name = "ShittyAboutButton",
					Text = aboutButtonText,
					Size = new Vector2(310f, 60f),
					BevelColor = new Color(128, 0, 128),   // Púrpura oscuro
					CenterColor = new Color(128, 0, 128), // Mismo color para el centro
					HorizontalAlignment = WidgetAlignment.Center
				};

				newRow.Children.Add(aboutButton);
				centerButtons.Children.Add(newRow);
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

			// Nuevo botón "Acerca Del Mod" (con traducción)
			BevelledButtonWidget aboutButton = mainMenu.Children.Find<BevelledButtonWidget>("ShittyAboutButton", false);
			if (aboutButton != null && aboutButton.IsClicked)
			{
				DialogsManager.ShowDialog(null, new ShittyCreaturesAboutDialog());
			}
		}
	}
}
