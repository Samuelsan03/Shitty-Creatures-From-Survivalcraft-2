using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class StructurePlacementSubsystem : Subsystem, IUpdateable
	{
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public void Update(float dt)
		{
			bool flag = this.m_subsystemGameInfo.WorldSettings.TerrainGenerationMode == TerrainGenerationMode.FlatContinent || this.m_subsystemGameInfo.WorldSettings.TerrainGenerationMode == TerrainGenerationMode.FlatIsland;
			if (!flag)
			{
				bool flag2 = !this.m_isInitialized;
				if (flag2)
				{
					this.InitializeStructures();
					this.m_isInitialized = true;
				}
				this.CheckForNewChunks();
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemSpawn = base.Project.FindSubsystem<SubsystemSpawn>(true);
			this.m_subsystemBlockEntities = base.Project.FindSubsystem<SubsystemBlockEntities>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			ValuesDictionary value = valuesDictionary.GetValue<ValuesDictionary>("ProcessedChunks", null);
			bool flag = value != null;
			if (flag)
			{
				this.m_processedChunks.Clear();
				foreach (string text in value.Keys)
				{
					string[] array = text.Split(',', StringSplitOptions.None);
					int x = 0; // Inicializada
					int y = 0; // Inicializada
					bool flag2 = array.Length == 2 && int.TryParse(array[0], out x) && int.TryParse(array[1], out y);
					if (flag2)
					{
						this.m_processedChunks.Add(new Point2(x, y));
					}
					else
					{
						Log.Warning("[Structures] Invalid chunk coordinate format: " + text);
					}
				}
			}
			this.LoadStructuresData();
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			ValuesDictionary valuesDictionary2 = new ValuesDictionary();
			foreach (Point2 point in this.m_processedChunks)
			{
				valuesDictionary2.SetValue<bool>(point.ToString(), true);
			}
			valuesDictionary.SetValue<ValuesDictionary>("ProcessedChunks", valuesDictionary2);
		}

		private void InitializeStructures()
		{
			try
			{
				this.m_subsystemTerrain.TerrainUpdater.ChunkInitialized += this.OnChunkInitialized;
			}
			catch (Exception ex)
			{
				Log.Error("[Structures] Error subscribing to chunk events: " + ex.Message);
			}
		}

		private void CheckForNewChunks()
		{
			foreach (TerrainChunk terrainChunk in this.m_subsystemTerrain.Terrain.AllocatedChunks)
			{
				bool flag = terrainChunk.State >= TerrainChunkState.Valid && !this.m_processedChunks.Contains(terrainChunk.Coords) && terrainChunk.ModificationCounter == 0;
				if (flag)
				{
					this.TryGenerateStructureInChunk(terrainChunk);
				}
			}
		}

		private void OnChunkInitialized(TerrainChunk chunk)
		{
			bool flag = chunk.State >= TerrainChunkState.Valid && !this.m_processedChunks.Contains(chunk.Coords) && chunk.ModificationCounter == 0;
			if (flag)
			{
				this.TryGenerateStructureInChunk(chunk);
			}
		}

		private void TryGenerateStructureInChunk(TerrainChunk chunk)
		{
			Point2 coords = chunk.Coords;
			bool flag = this.m_processedChunks.Contains(coords);
			if (!flag)
			{
				bool flag2 = chunk.ModificationCounter > 0;
				if (flag2)
				{
					this.m_processedChunks.Add(coords);
				}
				else
				{
					this.m_processedChunks.Add(coords);
					bool flag3 = !this.IsChunkSuitableForStructures(chunk);
					if (!flag3)
					{
						StructureData structureData = this.SelectRandomStructure();
						bool flag4 = structureData != null;
						if (flag4)
						{
							this.GenerateStructure(chunk, structureData);
						}
					}
				}
			}
		}

		private bool IsChunkSuitableForStructures(TerrainChunk chunk)
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < 16; i++)
			{
				for (int j = 0; j < 16; j++)
				{
					int num3 = chunk.Origin.X + i;
					int num4 = chunk.Origin.Y + j;
					int topHeight = this.m_subsystemTerrain.Terrain.GetTopHeight(num3, num4);
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValue(num3, topHeight, num4);
					int num5 = Terrain.ExtractContents(cellValue);
					bool flag = num5 != 18 && num5 != 92;
					bool flag2 = this.CheckAreaFlatness(num3, topHeight, num4, 3);
					bool flag3 = flag2 && flag;
					if (flag3)
					{
						num++;
					}
					num2++;
				}
			}
			return (float)num / (float)num2 > 0.4f;
		}

		private bool CheckAreaFlatness(int centerX, int centerY, int centerZ, int radius)
		{
			int num = 2;
			for (int i = -radius; i <= radius; i++)
			{
				for (int j = -radius; j <= radius; j++)
				{
					int topHeight = this.m_subsystemTerrain.Terrain.GetTopHeight(centerX + i, centerZ + j);
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValue(centerX + i, topHeight, centerZ + j);
					int num2 = Terrain.ExtractContents(cellValue);
					bool flag = num2 == 18 || num2 == 92;
					if (flag)
					{
						return false;
					}
					bool flag2 = Math.Abs(topHeight - centerY) > num;
					if (flag2)
					{
						return false;
					}
				}
			}
			return true;
		}

		private StructureData SelectRandomStructure()
		{
			bool flag = this.m_structuresData.Count == 0;
			StructureData result;
			if (flag)
			{
				result = null;
			}
			else
			{
				float num = this.m_random.Float(0f, 1f);
				float num2 = 0f;
				foreach (StructureData structureData in this.m_structuresData.Values)
				{
					num2 += structureData.Probability;
					bool flag2 = num <= num2;
					if (flag2)
					{
						return structureData;
					}
				}
				result = null;
			}
			return result;
		}

		private void GenerateStructure(TerrainChunk chunk, StructureData structureData)
		{
			try
			{
				string text = ContentManager.Get<string>("Estructuras De Mierda/" + structureData.FilePath);
				bool flag = string.IsNullOrEmpty(text);
				if (flag)
				{
					Log.Warning("[Structures] Structure file not found: " + structureData.FilePath);
				}
				else
				{
					int x = chunk.Origin.X + 8;
					int z = chunk.Origin.Y + 8;
					int baseY = this.m_subsystemTerrain.Terrain.CalculateTopmostCellHeight(x, z);
					this.ParseAndPlaceStructure(chunk, text, structureData, baseY);
					bool flag2 = structureData.Creatures != null && structureData.SpawnLocations != null;
					if (flag2)
					{
						this.GenerateCreatures(chunk, structureData, baseY);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("[Structures] Error generating structure '" + structureData.Name + "': " + ex.Message);
			}
		}

		private void ParseAndPlaceStructure(TerrainChunk chunk, string structureContent, StructureData structureData, int baseY)
		{
			string[] array = structureContent.Split(new char[]
			{
				'\r',
				'\n'
			}, StringSplitOptions.RemoveEmptyEntries);
			string[] array2 = array;
			int i = 0;
			while (i < array2.Length)
			{
				string text = array2[i];
				string[] array3 = text.Split(',', StringSplitOptions.None);
				bool flag = array3.Length >= 4;
				if (flag)
				{
					int num = int.Parse(array3[0]);
					int num2 = int.Parse(array3[1]);
					int num3 = int.Parse(array3[2]);
					string blockValueStr = array3[3];
					int x = chunk.Origin.X + num; // Inicializada y asignada
					int num4 = baseY + num2;
					int z = chunk.Origin.Y + num3; // Inicializada y asignada
					bool flag2 = num4 < 0 || num4 >= 256;
					if (!flag2)
					{
						int value = this.ParseBlockValue(blockValueStr);
						bool flag3 = this.IsReplaceable(this.m_subsystemTerrain.Terrain.GetCellValue(x, num4, z));
						if (flag3)
						{
							this.m_subsystemTerrain.ChangeCell(x, num4, z, value, true, null);
						}
					}
				}
				i++;
			}
		}

		private int ParseBlockValue(string blockValueStr)
		{
			bool flag = blockValueStr.Contains(':');
			if (flag)
			{
				string[] array = blockValueStr.Split(':', StringSplitOptions.None);
				string blockName = array[0];
				int data = int.Parse(array[1]);
				int blockIndex = BlocksManager.GetBlockIndex(blockName, false);
				bool flag2 = blockIndex != -1;
				if (flag2)
				{
					return Terrain.MakeBlockValue(blockIndex, 0, data);
				}
			}
			else
			{
				int result = 0; // Inicializada
				bool flag3 = int.TryParse(blockValueStr, out result);
				if (flag3)
				{
					return result;
				}
				int blockIndex2 = BlocksManager.GetBlockIndex(blockValueStr, false);
				bool flag4 = blockIndex2 != -1;
				if (flag4)
				{
					return Terrain.MakeBlockValue(blockIndex2);
				}
			}
			return 0;
		}

		private void GenerateCreatures(TerrainChunk chunk, StructureData structureData, int baseY)
		{
			bool flag = structureData.Creatures == null || structureData.SpawnLocations == null;
			if (!flag)
			{
				foreach (Point3 point in structureData.SpawnLocations)
				{
					string templateName = structureData.Creatures[this.m_random.Int(0, structureData.Creatures.Length - 1)];
					Vector3 position = new Vector3((float)(chunk.Origin.X + point.X), (float)(baseY + point.Y), (float)(chunk.Origin.Y + point.Z));
					SpawnEntityData data = new SpawnEntityData
					{
						TemplateName = templateName,
						Position = position,
						ConstantSpawn = false
					};
					this.m_subsystemSpawn.SpawnEntity(data);
				}
			}
		}

		private void LoadStructuresData()
		{
			try
			{
				XElement xelement = ContentManager.Get<XElement>("Buildings/StructuresData");
				this.m_structuresData.Clear();
				foreach (XElement structureElement in xelement.Elements("SmallStructure"))
				{
					StructureData structureData = this.LoadStructureData(structureElement);
					bool flag = structureData != null;
					if (flag)
					{
						this.m_structuresData[structureData.Name] = structureData;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("[Structures] Error loading structures data: " + ex.Message);
			}
		}

		private StructureData LoadStructureData(XElement structureElement)
		{
			XAttribute xattribute = structureElement.Attribute("Name");
			string text = (xattribute != null) ? xattribute.Value : null;
			XAttribute xattribute2 = structureElement.Attribute("Value");
			string text2 = (xattribute2 != null) ? xattribute2.Value : null;
			bool flag = string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text2);
			StructureData result;
			if (flag)
			{
				Log.Warning("[Structures] Invalid structure element, skipping.");
				result = null;
			}
			else
			{
				StructureData structureData = new StructureData();
				structureData.Name = text;
				structureData.FilePath = text2.Replace("Buildings/", "");
				foreach (XElement xelement in structureElement.Elements("Values"))
				{
					XAttribute xattribute3 = xelement.Attribute("Name");
					string text3 = (xattribute3 != null) ? xattribute3.Value : null;
					XAttribute xattribute4 = xelement.Attribute("Value");
					string text4 = (xattribute4 != null) ? xattribute4.Value : null;
					bool flag2 = string.IsNullOrEmpty(text3) || string.IsNullOrEmpty(text4);
					if (!flag2)
					{
						string text5 = text3;
						string a = text5;
						if (!(a == "StructureProbability"))
						{
							if (!(a == "Creatures"))
							{
								if (a == "CreaturesRandomSpawnLocations")
								{
									List<Point3> list = new List<Point3>();
									foreach (string text6 in text4.Split(new char[]
									{
										';'
									}, StringSplitOptions.RemoveEmptyEntries))
									{
										string[] array2 = text6.Split(',', StringSplitOptions.None);
										bool flag3 = array2.Length == 3;
										if (flag3)
										{
											int x = 0; // Inicializada
											int y = 0; // Inicializada
											int z = 0; // Inicializada
											bool flag4 = int.TryParse(array2[0], out x) && int.TryParse(array2[1], out y) && int.TryParse(array2[2], out z);
											if (flag4)
											{
												list.Add(new Point3(x, y, z));
											}
										}
									}
									structureData.SpawnLocations = list.ToArray();
								}
							}
							else
							{
								structureData.Creatures = text4.Split(new char[]
								{
									','
								}, StringSplitOptions.RemoveEmptyEntries);
							}
						}
						else
						{
							float num = 0f; // Inicializada
							bool flag5 = float.TryParse(text4, out num);
							if (flag5)
							{
								structureData.Probability = num / 10f;
							}
						}
					}
				}
				result = structureData;
			}
			return result;
		}

		private bool IsReplaceable(int blockValue)
		{
			int num = Terrain.ExtractContents(blockValue);
			return num == 0 || num == 8 || num == 2 || num == 7 || num == 3 || num == 1 || num == 61 || num == 62 || num == 19 || num == 20 || num == 24 || num == 25 || num == 28 || num == 99 || num == 127 || num == 197 || num == 232 || num == 233 || num == 12 || num == 13 || num == 14 || num == 225 || num == 256 || num == 87 || num == 18 || num == 24 || num == 25 || num == 28 || num == 99 || num == 174 || num == BlocksManager.GetBlockIndex<GrassBlock>(false, false) || num == BlocksManager.GetBlockIndex<SnowBlock>(false, false) || num == BlocksManager.GetBlockIndex<IceBlock>(false, false);
		}

		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemSpawn m_subsystemSpawn;
		public SubsystemBlockEntities m_subsystemBlockEntities;
		public SubsystemGameInfo m_subsystemGameInfo;
		private Dictionary<string, StructureData> m_structuresData = new Dictionary<string, StructureData>();
		public Random m_random = new Random();
		private HashSet<Point2> m_processedChunks = new HashSet<Point2>();
		private bool m_isInitialized = false;
	}
}
