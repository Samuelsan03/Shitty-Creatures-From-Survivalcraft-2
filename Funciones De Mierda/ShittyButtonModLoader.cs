using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class ShittyButtonModLoader : ModLoader
	{
		public override void OnMainMenuScreenCreated(MainMenuScreen mainMenuScreen, StackPanelWidget leftBottomBar, StackPanelWidget rightBottomBar)
		{
			BevelledButtonWidget existingButton = rightBottomBar.Children.Find<BevelledButtonWidget>("ShittyButton", false);

			if (existingButton == null)
			{
				BevelledButtonWidget shittyButton = new BevelledButtonWidget
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
					IsVisible = true
				};

				shittyButton.Children.Add(icon);
				rightBottomBar.Children.Add(shittyButton);
			}
		}

		public override void BeforeWidgetUpdate(Widget widget)
		{
			MainMenuScreen mainMenuScreen = widget as MainMenuScreen;
			if (mainMenuScreen == null) return;

			BevelledButtonWidget shittyButton = mainMenuScreen.Children.Find<BevelledButtonWidget>("ShittyButton", false);
			if (shittyButton != null && shittyButton.IsClicked)
			{
				// Mostrar el diálogo de changelog
				DialogsManager.ShowDialog(null, new ShittyCreaturesLogDialog());
			}
		}
	}
}
