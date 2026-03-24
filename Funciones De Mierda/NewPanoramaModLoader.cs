using System;
using Engine;
using Game;

namespace Game
{
	public class NewPanoramaModLoader : ModLoader
	{
		private static bool isRegistered = false;

		public override void __ModInitialize()
		{
			base.__ModInitialize();

			if (!isRegistered)
			{
				// Registrar hook para interceptar la construcción de cualquier widget
				ModsManager.RegisterHook("OnWidgetConstruct", this, -100);
				isRegistered = true;
			}
		}

		// Este método se ejecuta cada vez que se construye un widget en el juego
		public override void OnWidgetConstruct(ref Widget widget)
		{
			// Verificar si el widget es un PanoramaWidget y no es ya el nuestro
			if (widget != null && widget.GetType().Name == "PanoramaWidget" && !(widget is NewPanoramaWidget))
			{
				// Reemplazar por nuestro widget personalizado
				widget = new NewPanoramaWidget();
			}
		}
	}
}
