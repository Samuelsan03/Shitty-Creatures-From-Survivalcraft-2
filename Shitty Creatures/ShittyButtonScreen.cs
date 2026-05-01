using System;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class ShittyButtonScreen : Screen
	{
		public ShittyButtonScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/ShittyButtonScreen");
			this.LoadContents(this, node);
		}

		public override void Update()
		{
			if (base.Input.Back || base.Input.Cancel || this.Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
			{
				ScreensManager.SwitchScreen("MainMenu", Array.Empty<object>());
			}
		}
	}
}
