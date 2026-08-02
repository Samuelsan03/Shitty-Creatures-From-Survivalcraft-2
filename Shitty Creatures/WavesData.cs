using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public static class WavesData
	{
		public static Dictionary<int, List<WaveEntry>> LoadFromXml()
		{
			var result = new Dictionary<int, List<WaveEntry>>();
			XElement root = null;

			// 1. Intentar cargar con ContentManager
			try
			{
				root = ContentManager.Get<XElement>("Waves/Waves Programming");
			}
			catch (Exception ex)
			{
				Log.Warning($"Error al cargar con ContentManager: {ex.Message}");
			}

			// 2. Fallback con sistema de archivos
			if (root == null)
			{
				string xmlContent = null;
				string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
				if (!string.IsNullOrEmpty(assemblyLocation))
				{
					string modDir = Path.GetDirectoryName(assemblyLocation);
					if (!string.IsNullOrEmpty(modDir))
					{
						string[] filePaths = new string[]
						{
							Path.Combine(modDir, "Waves Programming.xml"),
							Path.Combine(modDir, "Waves Programming", "Waves Programming.xml")
						};
						foreach (var filePath in filePaths)
						{
							if (File.Exists(filePath))
							{
								try
								{
									xmlContent = File.ReadAllText(filePath);
									break;
								}
								catch (Exception ex)
								{
									Log.Warning($"Error al leer {filePath}: {ex.Message}");
								}
							}
						}
					}
				}
				if (!string.IsNullOrEmpty(xmlContent))
				{
					try
					{
						root = XElement.Parse(xmlContent);
					}
					catch (Exception ex)
					{
						Log.Error($"Error al parsear XML desde archivo: {ex.Message}");
					}
				}
			}

			if (root == null)
			{
				Log.Error("No se pudo cargar el archivo de oleadas (XML).");
				return result;
			}

			// Parsear el XML
			try
			{
				foreach (var waveElement in root.Elements("Wave"))
				{
					// ===== NUEVO: Detectar si es la ola especial =====
					XAttribute numberAttr = waveElement.Attribute("number");
					XAttribute nameAttr = waveElement.Attribute("name");

					int waveNumber;
					bool isSpecial = false;

					if (numberAttr != null)
					{
						waveNumber = (int)numberAttr;
					}
					else if (nameAttr != null && nameAttr.Value == "Special")
					{
						waveNumber = -1; // Clave especial
						isSpecial = true;
					}
					else
					{
						// Saltar elementos Wave que no tengan número ni nombre especial
						Log.Warning($"Ola sin número ni nombre 'Special' ignorada.");
						continue;
					}

					var entries = new List<WaveEntry>();

					foreach (var entryElement in waveElement.Elements("Entry"))
					{
						string templateName = (string)entryElement.Attribute("template");

						// Atributo "count" opcional, si no existe peso = 1
						int weight = 1;
						var countAttr = entryElement.Attribute("count");
						if (countAttr != null)
						{
							weight = (int)countAttr;
						}

						if (!string.IsNullOrEmpty(templateName) && weight > 0)
						{
							entries.Add(new WaveEntry(templateName, weight));
						}
					}

					if (entries.Count > 0)
					{
						result[waveNumber] = entries;
						if (isSpecial)
						{
							Log.Information($"Ola especial cargada con {entries.Count} entradas.");
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error($"Error al parsear XML de oleadas: {ex.Message}");
			}

			return result;
		}
	}

	public class WaveEntry
	{
		public string TemplateName { get; }
		public int Weight { get; }

		public WaveEntry(string name, int weight)
		{
			TemplateName = name;
			Weight = weight;
		}
	}
}
