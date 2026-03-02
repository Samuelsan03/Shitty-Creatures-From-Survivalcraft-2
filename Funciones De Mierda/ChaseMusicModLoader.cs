using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using XmlUtilities;

namespace Game
{
	public class ChaseMusicModLoader : ModLoader
	{
		private const string GhostKey = "GhostMusicEnabled";
		private const string TankKey = "TankMusicEnabled";

		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("OnSettingsScreenCreated", this);
		}

		public override void OnSettingsScreenCreated(SettingsScreen settingsScreen, out Dictionary<ButtonWidget, Action> buttonsToAdd)
		{
			buttonsToAdd = new Dictionary<ButtonWidget, Action>();

			try
			{
				// Botón para Fantasmas (color gris fijo)
				var ghostButton = new BevelledButtonWidget
				{
					Text = GetGhostButtonText(),
					Size = new Vector2(310f, 60f),
					BevelColor = Color.Gray,
					CenterColor = Color.Gray,
					Name = "GhostMusicButton"
				};
				buttonsToAdd.Add(ghostButton, () =>
				{
					ChaseMusicConfig.GhostMusicEnabled = !ChaseMusicConfig.GhostMusicEnabled;
					ghostButton.Text = GetGhostButtonText();
					SaveSettingsNow();
				});

				// Botón para Tanques (color rojo fijo)
				var tankButton = new BevelledButtonWidget
				{
					Text = GetTankButtonText(),
					Size = new Vector2(310f, 60f),
					BevelColor = Color.Red,
					CenterColor = Color.Red,
					Name = "TankMusicButton"
				};
				buttonsToAdd.Add(tankButton, () =>
				{
					ChaseMusicConfig.TankMusicEnabled = !ChaseMusicConfig.TankMusicEnabled;
					tankButton.Text = GetTankButtonText();
					SaveSettingsNow();
				});
			}
			catch (Exception ex)
			{
				Log.Error($"[ChaseMusic] Error al añadir botones: {ex.Message}");
			}
		}

		private string GetGhostButtonText()
		{
			string template = LanguageControl.Get("ChaseMusic", "GhostButton");
			string onOff = ChaseMusicConfig.GhostMusicEnabled ? LanguageControl.On : LanguageControl.Off;
			return string.Format(template, onOff);
		}

		private string GetTankButtonText()
		{
			string template = LanguageControl.Get("ChaseMusic", "TankButton");
			string onOff = ChaseMusicConfig.TankMusicEnabled ? LanguageControl.On : LanguageControl.Off;
			return string.Format(template, onOff);
		}

		public override void SaveSettings(XElement xElement)
		{
			xElement.SetAttributeValue(GhostKey, ChaseMusicConfig.GhostMusicEnabled);
			xElement.SetAttributeValue(TankKey, ChaseMusicConfig.TankMusicEnabled);
			Log.Information("[ChaseMusic] Configuración guardada.");
		}

		public override void LoadSettings(XElement xElement)
		{
			if (xElement.Attribute(GhostKey) != null)
				ChaseMusicConfig.GhostMusicEnabled = XmlUtils.GetAttributeValue<bool>(xElement, GhostKey);
			if (xElement.Attribute(TankKey) != null)
				ChaseMusicConfig.TankMusicEnabled = XmlUtils.GetAttributeValue<bool>(xElement, TankKey);
			Log.Information("[ChaseMusic] Configuración cargada.");
		}

		private void SaveSettingsNow()
		{
			if (ModSettingsManager.ModSettingsCache.TryGetValue(Entity.modInfo.PackageName, out XElement element))
			{
				SaveSettings(element);
			}
			else
			{
				element = new XElement("Mod");
				XmlUtils.SetAttributeValue(element, "PackageName", Entity.modInfo.PackageName);
				ModSettingsManager.ModSettingsCache[Entity.modInfo.PackageName] = element;
				SaveSettings(element);
			}
			ModSettingsManager.SaveModSettings();
		}
	}
}
