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
				// Cambia esto:
				// DialogsManager.ShowDialog(null, new MessageDialog("Shitty Button", "¡Has hecho clic en el botón basura!", "OK", null, null));

				// Por esto:
				DialogsManager.ShowDialog(null, new ShittyCreaturesLogDialog());
			}
		}
	}
}
