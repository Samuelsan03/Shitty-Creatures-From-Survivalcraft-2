using System;
using Engine;

namespace Game
{
	public class NewPanoramaModLoader : ModLoader
	{
		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("OnWidgetConstruct", this);
		}

		public override void OnWidgetConstruct(ref Widget widget)
		{
			if (widget != null && widget is PanoramaWidget)
			{
				widget = new NewPanoramaWidget();
			}
		}
	}
}
