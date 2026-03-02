using System;
using Engine;
using Engine.Graphics;
using Game;

namespace ShittyMod
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
			// --- AÑADIR ETIQUETA DE VERSIÓN DEL MOD EN EL MENÚ PRINCIPAL ---
			// Buscar el contenedor "TopArea" y la etiqueta "Version" que ya existen
			StackPanelWidget topArea = mainMenuScreen.Children.Find<StackPanelWidget>("TopArea", true);
			LabelWidget versionLabel = mainMenuScreen.Children.Find<LabelWidget>("Version", true);

			if (topArea != null && versionLabel != null)
			{
				// Crear una nueva etiqueta para mostrar la versión del mod
				LabelWidget modVersionLabel = new LabelWidget
				{
					Name = "ShittyCreaturesVersion",
					FontScale = 0.6f,                     // Mismo tamaño que la versión del juego
					HorizontalAlignment = WidgetAlignment.Center,
					Color = new Color(64, 192, 64),       // Color verde similar al del juego
					DropShadow = true,
					Text = "Shitty Creatures v1.0.6"       // Texto con el nombre y versión del mod
				};

				// Insertar la nueva etiqueta justo antes de la versión del juego
				topArea.Children.InsertBefore(versionLabel, modVersionLabel);
			}
			// ----------------------------------------------------------------

			// Verificar si el botón ya existe para no duplicarlo
			BevelledButtonWidget existing = rightBottomBar.Children.Find<BevelledButtonWidget>("ShittyButton", false);
			if (existing != null) return;

			// Crear el botón
			BevelledButtonWidget button = new BevelledButtonWidget
			{
				Name = "ShittyButton",
				Size = new Vector2(60f, 60f)
			};

			// Crear el icono dentro del botón
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

		public override void BeforeWidgetUpdate(Widget widget)
		{
			MainMenuScreen mainMenu = widget as MainMenuScreen;
			if (mainMenu == null) return;

			BevelledButtonWidget shittyButton = mainMenu.Children.Find<BevelledButtonWidget>("ShittyButton", false);
			if (shittyButton != null && shittyButton.IsClicked)
			{
				DialogsManager.ShowDialog(null, new ShittyCreaturesLogDialog());
			}
		}
	}
}
