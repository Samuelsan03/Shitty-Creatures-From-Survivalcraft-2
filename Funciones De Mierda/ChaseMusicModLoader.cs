using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using XmlUtilities;

namespace Game
{
	public class ChaseMusicModLoader : ModLoader
	{
		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("OnSettingsScreenCreated", this);
			ShittyCreaturesSettingsManager.Load();
		}

		public override void OnSettingsScreenCreated(SettingsScreen settingsScreen, out Dictionary<ButtonWidget, Action> buttonsToAdd)
		{
			buttonsToAdd = new Dictionary<ButtonWidget, Action>();

			try
			{
				var shittyButton = new BevelledButtonWidget
				{
					Text = LanguageControl.Get(new string[] { "ShittyCreatures", "SettingsButton" }),
					Size = new Vector2(310f, 60f),
					BevelColor = Color.DarkRed,
					CenterColor = Color.DarkRed,
					Name = "ShittyCreaturesSettingsButton"
				};

				buttonsToAdd.Add(shittyButton, () =>
				{
					if (ScreensManager.FindScreen<ShittyCreaturesSettingsScreen>("ShittyCreaturesSettings") == null)
					{
						ScreensManager.AddScreen("ShittyCreaturesSettings", new ShittyCreaturesSettingsScreen());
					}
					ScreensManager.SwitchScreen("ShittyCreaturesSettings");
				});
			}
			catch (Exception ex)
			{
				Log.Error($"[ChaseMusic] Error al añadir botón: {ex.Message}");
			}
		}

		public override void SaveSettings(XElement xElement) { }
		public override void LoadSettings(XElement xElement) { }
	}
}
