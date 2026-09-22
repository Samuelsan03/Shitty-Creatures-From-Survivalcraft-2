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

		// Chunks donde YA se colocó estructura (permanente, no se reescanea)
		private HashSet<Point2> m_processedChunks = new HashSet<Point2>();

		// Chunks en cooldown temporal (no se colocó nada, se reintentará)
		private Dictionary<Point2, float> m_chunkScanCooldowns = new Dictionary<Point2, float>();

		private List<StructureConfig> m_configs = new List<StructureConfig>();
		private Dictionary<string, int> m_placementCounts = new Dictionary<string, int>();
		private Random m_random = new Random();

		public const string ConfigName = "Structures/StructureDefinitions";
		public const float RescanInterval = 3.0f;   // Re-escanear chunks cada 3s si no se colocó nada
		public const int MaxScanDepth = 6;           // Profundidad máxima al buscar el bloque asignado

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
			public List<CreatureSpawnEntry> Creatures = new List<CreatureSpawnEntry>();
		}

		public class CreatureSpawnEntry
		{
			public string TemplateName;
			public float Probability;
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

		// =====================================================================
		//  LOAD / SAVE
		// =====================================================================

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
							$"{m_processedChunks.Count} chunks con estructura.");
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

		// =====================================================================
		//  LOAD CONFIG (XML)
		// =====================================================================

		private void LoadConfig()
		{
			m_configs.Clear();

			XElement root = null;

			try
			{
				root = ContentManager.Get<XElement>(ConfigName, null, false);
			}
			catch (Exception ex)
			{
				Log.Warning($"[SubsystemStructureGenerator] Get<XElement> falló: {ex.Message}");
			}

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
						case "Creatures":
							ParseCreaturesString(config.Creatures, pValue);
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

				// ---- Interpretación de MaxCount ----
				if (!config.AllowMultiple)
					config.MaxCount = 1;
				else if (config.MaxCount <= 0)
					config.MaxCount = int.MaxValue;

				// ---- Obtener BlockIndex via BlocksManager ----
				config.BlockIndex = BlocksManager.GetBlockIndex(config.BlockName, false);
				if (config.BlockIndex < 0)
				{
					Log.Warning($"[SubsystemStructureGenerator] Bloque '{config.BlockName}' no encontrado " +
								$"para estructura '{config.Name}'. ¿El nombre es exacto?");
					continue;
				}

				if (!LoadStructureData(config))
					continue;

				if (!m_placementCounts.ContainsKey(config.Name))
					m_placementCounts[config.Name] = 0;

				m_configs.Add(config);

				string maxCountStr = config.MaxCount == int.MaxValue ? "∞" : config.MaxCount.ToString();
				Log.Information($"[SubsystemStructureGenerator] Cargada: '{config.Name}' " +
								$"(Block={config.BlockName}[{config.BlockIndex}], " +
								$"Prob={config.Probability}, MaxCount={maxCountStr}, " +
								$"Creatures={config.Creatures.Count})");
			}
		}

		private void ParseCreaturesString(List<CreatureSpawnEntry> list, string data)
		{
			list.Clear();
			if (string.IsNullOrWhiteSpace(data))
				return;

			// Formato esperado: "Cerdo:0.5,Vaca:0.2,Zombi:1.0"
			string[] entries = data.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string entry in entries)
			{
				string[] parts = entry.Split(new char[] { ':' }, StringSplitOptions.None);
				if (parts.Length == 2)
				{
					string name = parts[0].Trim();
					if (float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float prob))
					{
						list.Add(new CreatureSpawnEntry { TemplateName = name, Probability = prob });
					}
					else
					{
						Log.Warning($"[SubsystemStructureGenerator] Probabilidad inválida para criatura '{name}' en XML.");
					}
				}
			}
		}

		// =====================================================================
		//  LOAD STRUCTURE DATA (JSON)
		// =====================================================================

		private bool LoadStructureData(StructureConfig config)
		{
			JsonDocument doc = null;

			try
			{
				doc = ContentManager.Get<JsonDocument>(config.Path, null, false);
			}
			catch (Exception ex)
			{
				Log.Warning($"[SubsystemStructureGenerator] Get<JsonDocument> falló: {ex.Message}");
			}

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

		// =====================================================================
		//  UPDATE
		// =====================================================================

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

			if (m_chunkScanCooldowns.Count > 0)
			{
				List<Point2> expired = new List<Point2>();
				List<Point2> keys = new List<Point2>(m_chunkScanCooldowns.Keys);
				foreach (Point2 key in keys)
				{
					float newTime = m_chunkScanCooldowns[key] - dt;
					if (newTime <= 0f)
						expired.Add(key);
					else
						m_chunkScanCooldowns[key] = newTime;
				}
				foreach (Point2 p in expired)
					m_chunkScanCooldowns.Remove(p);
			}

			Terrain terrain = m_subsystemTerrain.Terrain;
			TerrainChunk[] chunks = terrain.AllocatedChunks;

			foreach (TerrainChunk chunk in chunks)
			{
				if (chunk == null)
					continue;

				Point2 coords = chunk.Coords;

				if (m_processedChunks.Contains(coords))
					continue;

				if (m_chunkScanCooldowns.ContainsKey(coords))
					continue;

				if (chunk.State != TerrainChunkState.Valid)
					continue;

				bool placedAny = TryPlaceStructuresInChunk(chunk);

				if (placedAny)
				{
					m_processedChunks.Add(coords);
				}
				else
				{
					m_chunkScanCooldowns[coords] = RescanInterval;
				}

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

		// =====================================================================
		//  TRY PLACE
		// =====================================================================

		private bool TryPlaceStructuresInChunk(TerrainChunk chunk)
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
				return false;

			for (int x = 0; x < 16; x++)
			{
				for (int z = 0; z < 16; z++)
				{
					int worldX = chunk.Origin.X + x;
					int worldZ = chunk.Origin.Y + z;

					int topY = chunk.CalculateTopmostCellHeight(x, z);
					if (topY <= 0 || topY >= 254)
						continue;

					int surfaceY = -1;
					int surfaceContents = -1;

					for (int y = topY, depth = 0; y >= 1 && depth <= MaxScanDepth; y--, depth++)
					{
						int cellValue = terrain.GetCellValue(worldX, y, worldZ);
						int contents = Terrain.ExtractContents(cellValue);

						if (contents == 0)
							continue;

						Block block = BlocksManager.Blocks[contents];
						if (!block.IsCollidable)
							continue;

						for (int i = 0; i < available.Count; i++)
						{
							if (contents == available[i].BlockIndex)
							{
								surfaceY = y;
								surfaceContents = contents;
								break;
							}
						}
						break;
					}

					if (surfaceY < 0)
						continue;

					for (int i = available.Count - 1; i >= 0; i--)
					{
						StructureConfig config = available[i];

						if (surfaceContents != config.BlockIndex)
							continue;

						int aboveValue = terrain.GetCellValue(worldX, surfaceY + 1, worldZ);
						if (Terrain.ExtractContents(aboveValue) != 0)
							break;

						if (m_random.Float() > config.Probability)
							break;

						// 1. Colocar los bloques de la estructura
						PlaceStructure(config, worldX, surfaceY + 1, worldZ);

						// 2. Invocar criaturas si la estructura las tiene definidos
						if (config.Creatures.Count > 0)
						{
							SpawnCreaturesForStructure(config, worldX, surfaceY + 1, worldZ);
						}

						int current = m_placementCounts.GetValueOrDefault(config.Name, 0);
						current++;
						m_placementCounts[config.Name] = current;

						MarkChunksProcessed(config.Data, worldX, worldZ, 8);

						return true;
					}
				}
			}

			return false;
		}

		// =====================================================================
		//  PLACE STRUCTURE
		// =====================================================================

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

		// =====================================================================
		//  SPAWN CREATURES (NUEVO)
		// =====================================================================

		private void SpawnCreaturesForStructure(StructureConfig config, int originX, int originY, int originZ)
		{
			foreach (CreatureSpawnEntry entry in config.Creatures)
			{
				if (m_random.Float() <= entry.Probability)
				{
					try
					{
						ValuesDictionary entityValues = DatabaseManager.FindEntityValuesDictionary(entry.TemplateName, true);
						if (entityValues == null)
						{
							Log.Warning($"[SubsystemStructureGenerator] Plantilla de criatura '{entry.TemplateName}' no encontrada.");
							continue;
						}

						Entity entity = Project.CreateEntity(entityValues, 0);

						ComponentBody componentBody = entity.FindComponent<ComponentBody>(true);
						if (componentBody != null)
						{
							// Aparecen en el centro de la estructura, 2 bloques arriba del piso para no ahogarse en el suelo
							float spawnX = originX + (config.Data.SizeX / 2f) + 0.5f;
							float spawnZ = originZ + (config.Data.SizeZ / 2f) + 0.5f;
							float spawnY = originY + 2.0f;

							componentBody.Position = new Vector3(spawnX, spawnY, spawnZ);
							componentBody.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, m_random.Float(0f, MathF.PI * 2f));
						}

						Project.AddEntity(entity);
					}
					catch (Exception ex)
					{
						Log.Error($"[SubsystemStructureGenerator] Error al spawnear criatura '{entry.TemplateName}': {ex.Message}");
					}
				}
			}
		}

		// =====================================================================
		//  MARK CHUNKS PROCESSED
		// =====================================================================

		private void MarkChunksProcessed(StructureData data, int originX, int originZ, int paddingBlocks = 0)
		{
			int minX = originX - paddingBlocks;
			int maxX = originX + Math.Max(0, data.SizeX - 1) + paddingBlocks;
			int minZ = originZ - paddingBlocks;
			int maxZ = originZ + Math.Max(0, data.SizeZ - 1) + paddingBlocks;

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
