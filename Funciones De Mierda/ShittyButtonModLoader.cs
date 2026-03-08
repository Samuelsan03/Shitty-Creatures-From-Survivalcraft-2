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
			// Buscar si ya existe el botón (para no duplicar)
			BevelledButtonWidget existingButton = mainMenuScreen.Children.Find<BevelledButtonWidget>("ShittyButton", true);

			if (existingButton == null)
			{
				// Crear el botón cuadrado con el icono de Veemon
				BevelledButtonWidget shittyButton = new BevelledButtonWidget
				{
					Name = "ShittyButton",
					Size = new Vector2(60f, 60f),
					Margin = new Vector2(10f, 5f) // Margen para separación
				};

				// Icono del botón
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

				// Buscar la barra superior donde poner el botón (para que sea visible en móvil)
				StackPanelWidget topBar = mainMenuScreen.Children.Find<StackPanelWidget>("TopBar", false);

				if (topBar != null)
				{
					// Si existe la barra superior, agregar el botón allí (extremo derecho)
					// Buscar la etiqueta de versión o el botón de ajustes para insertar antes/después
					var existingChildren = topBar.Children;

					// Insertar al final de la barra superior (después de todos los elementos)
					topBar.Children.Add(shittyButton);
				}
				else
				{
					// Fallback: si no hay barra superior, usar la barra inferior derecha (comportamiento original)
					rightBottomBar.Children.Add(shittyButton);
				}
			}
		}

		public override void BeforeWidgetUpdate(Widget widget)
		{
			MainMenuScreen mainMenuScreen = widget as MainMenuScreen;
			if (mainMenuScreen == null) return;

			// Buscar el botón en toda la jerarquía (no solo en rightBottomBar)
			BevelledButtonWidget shittyButton = mainMenuScreen.Children.Find<BevelledButtonWidget>("ShittyButton", true);

			if (shittyButton != null && shittyButton.IsClicked)
			{
				// Mostrar el diálogo de changelog
				DialogsManager.ShowDialog(null, new ShittyCreaturesLogDialog());
			}
		}
	}
}
