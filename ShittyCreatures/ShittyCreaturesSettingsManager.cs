using System;
using System.IO;
using System.Xml.Linq;
using Engine;
using Game;
using XmlUtilities;

namespace Game
{
	public static class ShittyCreaturesSettingsManager
	{
		private static readonly string SettingsPath = ModsManager.ExternalPath + "/ShittyCreaturesSettings.xml";

		public static bool GhostMusicEnabled { get; set; } = true;
		public static bool TankMusicEnabled { get; set; } = true;
		public static bool DeathSpawnEnabled { get; set; } = true;
		public static bool ThirstEnabled { get; set; } = true;
		public static bool CoordinateDisplayEnabled { get; set; } = true;
		public static bool PunchCommandEnabled { get; set; } = true;
		public static bool FreeCameraEnabled { get; set; } = true;
		public static bool CreativeDefenseEnabled { get; set; } = true;

		// Nueva propiedad para la música del menú
		public static bool MenuMusicEnabled { get; set; } = true;

		public static void Load()
		{
			try
			{
				if (!Storage.FileExists(SettingsPath))
				{
					Save();
					return;
				}

				using (Stream stream = Storage.OpenFile(SettingsPath, OpenFileMode.Read))
				{
					XElement root = XmlUtils.LoadXmlFromStream(stream, null, true);

					foreach (XElement element in root.Elements("Value"))
					{
						string name = XmlUtils.GetAttributeValue<string>(element, "Name");
						bool value = XmlUtils.GetAttributeValue<bool>(element, "Value");

						switch (name)
						{
							case "GhostMusicEnabled":
								GhostMusicEnabled = value;
								break;
							case "TankMusicEnabled":
								TankMusicEnabled = value;
								break;
							case "DeathSpawnEnabled":
								DeathSpawnEnabled = value;
								break;
							case "ThirstEnabled":
								ThirstEnabled = value;
								break;
							case "CoordinateDisplayEnabled":
								CoordinateDisplayEnabled = value;
								break;
							case "PunchCommandEnabled":
								PunchCommandEnabled = value;
								break;
							case "CreativeDefenseEnabled":
								CreativeDefenseEnabled = value;
								break;
							case "FreeCameraEnabled":
								FreeCameraEnabled = value;
								break;
							case "MenuMusicEnabled":
								MenuMusicEnabled = value;
								break;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[ShittyCreatures] Error al cargar configuración: {ex.Message}");
			}

			ChaseMusicConfig.GhostMusicEnabled = GhostMusicEnabled;
			ChaseMusicConfig.TankMusicEnabled = TankMusicEnabled;
		}

		public static void Save()
		{
			try
			{
				using (Stream stream = Storage.OpenFile(SettingsPath, OpenFileMode.Create))
				{
					XElement root = new XElement("ShittyCreaturesSettings");

					root.Add(new XElement("Value",
						new XAttribute("Name", "GhostMusicEnabled"),
						new XAttribute("Type", "bool"),
						new XAttribute("Value", GhostMusicEnabled)));

					root.Add(new XElement("Value",
						new XAttribute("Name", "TankMusicEnabled"),
						new XAttribute("Type", "bool"),
						new XAttribute("Value", TankMusicEnabled)));

					root.Add(new XElement("Value",
						new XAttribute("Name", "DeathSpawnEnabled"),
						new XAttribute("Type", "bool"),
						new XAttribute("Value", DeathSpawnEnabled)));

					root.Add(new XElement("Value",
						new XAttribute("Name", "ThirstEnabled"),
						new XAttribute("Type", "bool"),
						new XAttribute("Value", ThirstEnabled)));

					root.Add(new XElement("Value",
						new XAttribute("Name", "CoordinateDisplayEnabled"),
						new XAttribute("Type", "bool"),
						new XAttribute("Value", CoordinateDisplayEnabled)));

					root.Add(new XElement("Value",
						new XAttribute("Name", "PunchCommandEnabled"),
						new XAttribute("Type", "bool"),
						new XAttribute("Value", PunchCommandEnabled)));

					root.Add(new XElement("Value",
						new XAttribute("Name", "CreativeDefenseEnabled"),
						new XAttribute("Type", "bool"),
						new XAttribute("Value", CreativeDefenseEnabled)));

					root.Add(new XElement("Value",
						new XAttribute("Name", "FreeCameraEnabled"),
						new XAttribute("Type", "bool"),
						new XAttribute("Value", FreeCameraEnabled)));

					root.Add(new XElement("Value",
						new XAttribute("Name", "MenuMusicEnabled"),
						new XAttribute("Type", "bool"),
						new XAttribute("Value", MenuMusicEnabled)));

					XmlUtils.SaveXmlToStream(root, stream, null, true);
				}

				ChaseMusicConfig.GhostMusicEnabled = GhostMusicEnabled;
				ChaseMusicConfig.TankMusicEnabled = TankMusicEnabled;
			}
			catch (Exception ex)
			{
				Log.Error($"[ShittyCreatures] Error al guardar configuración: {ex.Message}");
			}
		}
	}
}
