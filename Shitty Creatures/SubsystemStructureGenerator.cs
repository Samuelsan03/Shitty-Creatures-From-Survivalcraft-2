using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemStructureGenerator : Subsystem, IUpdateable
	{
		public SubsystemTerrain m_subsystemTerrain;

		private HashSet<Point2> m_processedChunks = new HashSet<Point2>();
		private List<StructureConfig> m_configs = new List<StructureConfig>();
		private Dictionary<string, int> m_placementCounts = new Dictionary<string, int>();
		private Random m_random = new Random();

		public const string ConfigName = "Structures/StructureDefinitions";

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public float FloatUpdateOrder => (float)UpdateOrder.Default;

		public class StructureConfig
		{
			public string Name;
			public string Path;
			public float Probability;
			public string BlockName;
			public int BlockIndex = -1;
			public bool AllowMultiple;
			public int MaxCount = 1;
			public StructureData Data;
		}

		public class StructureBlock
		{
			public int X;
			public int Y;
			public int Z;
			public int Content;
			public int Data;
		}

		public class StructureData
		{
			public string Name;
			public int SizeX, SizeY, SizeZ;
			public bool IncludeAir;
			public List<StructureBlock> Blocks = new List<StructureBlock>();
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);

			string counts = valuesDictionary.GetValue<string>("PlacementCounts", "");
			if (!string.IsNullOrEmpty(counts))
			{
				foreach (string entry in counts.Split('|', StringSplitOptions.RemoveEmptyEntries))
				{
					string[] parts = entry.Split(',');
					if (parts.Length == 2 && int.TryParse(parts[1], out int c))
						m_placementCounts[parts[0]] = c;
				}
			}

			string processed = valuesDictionary.GetValue<string>("ProcessedChunks", "");
			if (!string.IsNullOrEmpty(processed))
			{
				foreach (string s in processed.Split('|', StringSplitOptions.RemoveEmptyEntries))
				{
					string[] parts = s.Split(',');
					if (parts.Length == 2 &&
						int.TryParse(parts[0], out int cx) &&
						int.TryParse(parts[1], out int cz))
						m_processedChunks.Add(new Point2(cx, cz));
				}
			}

			LoadConfig();

			Log.Information($"[SubsystemStructureGenerator] Configuración cargada: " +
							$"{m_configs.Count} estructuras, " +
							$"{m_placementCounts.Count} ya colocadas, " +
							$"{m_processedChunks.Count} chunks ya procesados.");
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			List<string> countEntries = new List<string>();
			foreach (KeyValuePair<string, int> kv in m_placementCounts)
				countEntries.Add($"{kv.Key},{kv.Value}");
			valuesDictionary["PlacementCounts"] = string.Join("|", countEntries);

			List<string> chunks = new List<string>();
			foreach (Point2 p in m_processedChunks)
				chunks.Add($"{p.X},{p.Y}");
			valuesDictionary["ProcessedChunks"] = string.Join("|", chunks);
		}

		private void LoadConfig()
		{
			m_configs.Clear();

			XElement root = null;

			// Metodo 1: ContentManager.Get<XElement> (usa content reader + cache)
			try
			{
				root = ContentManager.Get<XElement>(ConfigName, null, false);
			}
			catch (Exception ex)
			{
				Log.Warning($"[SubsystemStructureGenerator] Get<XElement> falló: {ex.Message}");
			}

			// Metodo 2: fallback con GetStream
			if (root == null)
			{
				try
				{
					Stream configStream = ContentManager.GetStream(ConfigName + ".xml");
					if (configStream != null)
					{
						using (configStream)
							root = XElement.Load(configStream);
					}
				}
				catch (Exception ex)
				{
					Log.Warning($"[SubsystemStructureGenerator] GetStream falló: {ex.Message}");
				}
			}

			if (root == null)
			{
				Log.Warning($"[SubsystemStructureGenerator] No se pudo cargar: {ConfigName}");
				return;
			}

			foreach (XElement structEl in root.Elements())
			{
				if (structEl.Name.LocalName != "StructureTemplate")
					continue;

				StructureConfig config = new StructureConfig();
				config.Name = structEl.Attribute("Name")?.Value ?? string.Empty;

				if (string.IsNullOrEmpty(config.Name))
					continue;

				foreach (XElement param in structEl.Elements())
				{
					if (param.Name.LocalName != "Parameter")
						continue;

					string pName = param.Attribute("Name")?.Value ?? string.Empty;
					string pValue = param.Attribute("Value")?.Value ?? string.Empty;

					switch (pName)
					{
						case "Path":
							config.Path = pValue;
							break;
						case "Probability":
							float.TryParse(pValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float prob);
							config.Probability = prob;
							break;
						case "Block":
							config.BlockName = pValue;
							break;
						case "AllowMultiple":
							bool.TryParse(pValue, out bool allow);
							config.AllowMultiple = allow;
							break;
						case "MaxCount":
							int.TryParse(pValue, out int mc);
							config.MaxCount = mc;
							break;
					}
				}

				if (string.IsNullOrEmpty(config.Path))
				{
					Log.Warning($"[SubsystemStructureGenerator] '{config.Name}' sin Path.");
					continue;
				}

				if (string.IsNullOrEmpty(config.BlockName))
				{
					Log.Warning($"[SubsystemStructureGenerator] '{config.Name}' sin Block.");
					continue;
				}

				config.BlockIndex = BlocksManager.GetBlockIndex(config.BlockName, false);
				if (config.BlockIndex < 0)
				{
					Log.Warning($"[SubsystemStructureGenerator] Bloque '{config.BlockName}' no encontrado.");
					continue;
				}

				if (!config.AllowMultiple)
					config.MaxCount = 1;
				else if (config.MaxCount < 1)
					config.MaxCount = 1;

				if (!LoadStructureData(config))
					continue;

				if (!m_placementCounts.ContainsKey(config.Name))
					m_placementCounts[config.Name] = 0;

				m_configs.Add(config);

				Log.Information($"[SubsystemStructureGenerator] Cargada: '{config.Name}' " +
								$"(Block={config.BlockName}, Prob={config.Probability}, " +
								$"AllowMultiple={config.AllowMultiple}, MaxCount={config.MaxCount}, " +
								$"Blocks={config.Data.Blocks.Count})");
			}
		}

		private bool LoadStructureData(StructureConfig config)
		{
			JsonDocument doc = null;

			// Metodo 1: Usar el ContentReader nativo del juego (JsonDocumentReader)
			// Esto usa la caché y evita el problema del Stream disposed.
			try
			{
				doc = ContentManager.Get<JsonDocument>(config.Path, null, false);
			}
			catch (Exception ex)
			{
				Log.Warning($"[SubsystemStructureGenerator] Get<JsonDocument> falló: {ex.Message}");
			}

			// Metodo 2: Intentar sin la extensión (por si el Path ya la trae)
			if (doc == null && config.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					string pathNoExt = config.Path.Substring(0, config.Path.Length - 5);
					doc = ContentManager.Get<JsonDocument>(pathNoExt, null, false);
				}
				catch (Exception ex)
				{
					Log.Warning($"[SubsystemStructureGenerator] Get<JsonDocument> sin extensión falló: {ex.Message}");
				}
			}

			if (doc == null)
			{
				Log.Error($"[SubsystemStructureGenerator] No se pudo leer JSON: {config.Path}");
				return false;
			}

			try
			{
				JsonElement root = doc.RootElement;

				StructureData data = new StructureData();
				data.Name = config.Name;

				if (root.TryGetProperty("includeAir", out JsonElement airEl))
					data.IncludeAir = airEl.GetBoolean();

				if (root.TryGetProperty("size", out JsonElement sizeEl))
				{
					if (sizeEl.TryGetProperty("x", out JsonElement sx)) data.SizeX = sx.GetInt32();
					if (sizeEl.TryGetProperty("y", out JsonElement sy)) data.SizeY = sy.GetInt32();
					if (sizeEl.TryGetProperty("z", out JsonElement sz)) data.SizeZ = sz.GetInt32();
				}

				if (root.TryGetProperty("blocks", out JsonElement blocksEl))
				{
					foreach (JsonElement block in blocksEl.EnumerateArray())
					{
						StructureBlock sb = new StructureBlock();
						sb.X = block.GetProperty("x").GetInt32();
						sb.Y = block.GetProperty("y").GetInt32();
						sb.Z = block.GetProperty("z").GetInt32();
						sb.Content = block.GetProperty("content").GetInt32();
						sb.Data = block.TryGetProperty("data", out JsonElement de) ? de.GetInt32() : 0;

						if (!data.IncludeAir && sb.Content == 0)
							continue;

						data.Blocks.Add(sb);
					}
				}

				config.Data = data;
				return true;
			}
			catch (Exception e)
			{
				Log.Error($"[SubsystemStructureGenerator] Error parseando JSON '{config.Path}': {e}");
				return false;
			}
		}

		public void Update(float dt)
		{
			if (m_configs.Count == 0)
				return;

			if (m_subsystemTerrain == null || m_subsystemTerrain.Terrain == null)
				return;

			bool allDone = true;
			foreach (StructureConfig c in m_configs)
			{
				int placed = m_placementCounts.GetValueOrDefault(c.Name, 0);
				if (placed < c.MaxCount)
				{
					allDone = false;
					break;
				}
			}
			if (allDone)
				return;

			Terrain terrain = m_subsystemTerrain.Terrain;
			TerrainChunk[] chunks = terrain.AllocatedChunks;

			foreach (TerrainChunk chunk in chunks)
			{
				if (chunk == null)
					continue;

				Point2 coords = chunk.Coords;
				if (m_processedChunks.Contains(coords))
					continue;

				if (chunk.State != TerrainChunkState.Valid)
					continue;

				m_processedChunks.Add(coords);
				TryPlaceStructuresInChunk(chunk);

				allDone = true;
				foreach (StructureConfig c in m_configs)
				{
					int placed = m_placementCounts.GetValueOrDefault(c.Name, 0);
					if (placed < c.MaxCount)
					{
						allDone = false;
						break;
					}
				}
				if (allDone)
					return;
			}
		}

		private void TryPlaceStructuresInChunk(TerrainChunk chunk)
		{
			Terrain terrain = m_subsystemTerrain.Terrain;

			List<StructureConfig> available = new List<StructureConfig>();
			foreach (StructureConfig c in m_configs)
			{
				int placed = m_placementCounts.GetValueOrDefault(c.Name, 0);
				if (placed < c.MaxCount)
					available.Add(c);
			}

			if (available.Count == 0)
				return;

			for (int x = 0; x < 16; x++)
			{
				for (int z = 0; z < 16; z++)
				{
					int worldX = chunk.Origin.X + x;
					int worldZ = chunk.Origin.Y + z;

					int topY = chunk.CalculateTopmostCellHeight(x, z);
					if (topY <= 0 || topY >= 254)
						continue;

					int cellValue = terrain.GetCellValue(worldX, topY, worldZ);
					int contents = Terrain.ExtractContents(cellValue);

					for (int i = available.Count - 1; i >= 0; i--)
					{
						StructureConfig config = available[i];

						if (contents != config.BlockIndex)
							continue;

						int aboveValue = terrain.GetCellValue(worldX, topY + 1, worldZ);
						if (Terrain.ExtractContents(aboveValue) != 0)
							continue;

						if (m_random.Float() > config.Probability)
							continue;

						PlaceStructure(config, worldX, topY + 1, worldZ);

						int current = m_placementCounts.GetValueOrDefault(config.Name, 0);
						current++;
						m_placementCounts[config.Name] = current;

						MarkChunksProcessed(config.Data, worldX, worldZ);

						Log.Information($"[SubsystemStructureGenerator] '{config.Name}' colocada en " +
										$"({worldX}, {topY + 1}, {worldZ}) — {current}/{config.MaxCount}");

						if (current >= config.MaxCount)
							available.RemoveAt(i);

						break;
					}

					if (available.Count == 0)
						return;
				}
			}
		}

		private void PlaceStructure(StructureConfig config, int originX, int originY, int originZ)
		{
			foreach (StructureBlock block in config.Data.Blocks)
			{
				int x = originX + block.X;
				int y = originY + block.Y;
				int z = originZ + block.Z;

				if (y < 0 || y >= 256)
					continue;

				int blockValue = Terrain.MakeBlockValue(block.Content, 0, block.Data);
				m_subsystemTerrain.ChangeCell(x, y, z, blockValue, true, null);
			}

			m_subsystemTerrain.TerrainUpdater.RequestSynchronousUpdate();
		}

		private void MarkChunksProcessed(StructureData data, int originX, int originZ)
		{
			int minX = originX;
			int maxX = originX + Math.Max(0, data.SizeX - 1);
			int minZ = originZ;
			int maxZ = originZ + Math.Max(0, data.SizeZ - 1);

			int minChunkX = minX >> 4;
			int maxChunkX = maxX >> 4;
			int minChunkZ = minZ >> 4;
			int maxChunkZ = maxZ >> 4;

			for (int cx = minChunkX; cx <= maxChunkX; cx++)
			{
				for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
				{
					m_processedChunks.Add(new Point2(cx, cz));
				}
			}
		}
	}
}
