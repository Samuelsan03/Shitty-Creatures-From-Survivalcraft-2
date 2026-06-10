using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Engine;
using Engine.Serialization;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFruitSaplingBlockBehavior : SubsystemBlockBehavior, IUpdateable
	{
		private static readonly string[] BlockNames = new string[]
		{
			"AppleSaplingBlock",
			"PearSaplingBlock",
			"CherrySaplingBlock",
			"OrangeSaplingBlock",
			"BananaSaplingBlock"
		};

		private Dictionary<int, ShittyTreeType> m_blockToTreeType;
		private int[] m_handledBlocks;
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemTimeOfDay m_subsystemTimeOfDay;
		private Dictionary<Point3, SaplingData> m_saplings = new Dictionary<Point3, SaplingData>();
		private Dictionary<Point3, SaplingData>.ValueCollection.Enumerator m_enumerator;
		private Random m_random = new Random();
		private StringBuilder m_stringBuilder = new StringBuilder();

		public override int[] HandledBlocks
		{
			get
			{
				if (m_handledBlocks == null)
				{
					var list = new List<int>();
					foreach (string name in BlockNames)
					{
						int idx = BlocksManager.GetBlockIndex(name, false);
						if (idx >= 0) list.Add(idx);
					}
					m_handledBlocks = list.ToArray();
				}
				return m_handledBlocks;
			}
		}

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
		{
			InitMapping();

			float growSeconds = (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative)
				? m_random.Float(8f, 12f)
				: m_random.Float(480f, 600f);

			ShittyTreeType treeType = GetTreeTypeFromBlockValue(value);
			AddSapling(new SaplingData
			{
				Point = new Point3(x, y, z),
				TreeType = treeType,
				MatureTime = m_subsystemGameInfo.TotalElapsedGameTime + (double)growSeconds
			});
		}

		public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
		{
			int cellValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
			if (BlocksManager.Blocks[Terrain.ExtractContents(cellValue)].IsNonAttachable(cellValue))
			{
				base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
			}
		}

		public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
		{
			RemoveSapling(new Point3(x, y, z));
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemTimeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
			m_enumerator = m_saplings.Values.GetEnumerator();
			InitMapping();

			ValuesDictionary saplingsDict = valuesDictionary.GetValue<ValuesDictionary>("FruitSaplings", null);
			if (saplingsDict != null)
			{
				foreach (object obj in saplingsDict.Values)
				{
					string data = (string)obj;
					AddSapling(LoadSaplingData(data));
				}
			}
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			ValuesDictionary saplingsDict = new ValuesDictionary();
			valuesDictionary.SetValue<ValuesDictionary>("FruitSaplings", saplingsDict);
			int num = 0;
			foreach (SaplingData saplingData in m_saplings.Values)
			{
				saplingsDict.SetValue<string>(num++.ToString(CultureInfo.InvariantCulture), SaveSaplingData(saplingData));
			}
		}

		public virtual void Update(float dt)
		{
			for (int i = 0; i < 10; i++)
			{
				if (!m_enumerator.MoveNext())
				{
					m_enumerator = m_saplings.Values.GetEnumerator();
					return;
				}
				MatureSapling(m_enumerator.Current);
			}
		}

		private void InitMapping()
		{
			if (m_blockToTreeType != null) return;
			m_blockToTreeType = new Dictionary<int, ShittyTreeType>();
			int appleIdx = BlocksManager.GetBlockIndex("AppleSaplingBlock", true);
			int pearIdx = BlocksManager.GetBlockIndex("PearSaplingBlock", true);
			int cherryIdx = BlocksManager.GetBlockIndex("CherrySaplingBlock", true);
			int orangeIdx = BlocksManager.GetBlockIndex("OrangeSaplingBlock", true);
			int bananaIdx = BlocksManager.GetBlockIndex("BananaSaplingBlock", true);

			m_blockToTreeType[appleIdx] = ShittyTreeType.Apple;
			m_blockToTreeType[pearIdx] = ShittyTreeType.Pear;
			m_blockToTreeType[cherryIdx] = ShittyTreeType.Cherry;
			m_blockToTreeType[orangeIdx] = ShittyTreeType.Orange;
			m_blockToTreeType[bananaIdx] = ShittyTreeType.Banana;
		}

		private ShittyTreeType GetTreeTypeFromBlockValue(int value)
		{
			int contents = Terrain.ExtractContents(value);
			return m_blockToTreeType[contents];
		}

		public SaplingData LoadSaplingData(string data)
		{
			string[] array = data.Split(new string[] { ";" }, StringSplitOptions.None);
			if (array.Length != 3)
				throw new InvalidOperationException("Invalid fruit sapling data string.");

			return new SaplingData
			{
				Point = HumanReadableConverter.ConvertFromString<Point3>(array[0]),
				TreeType = HumanReadableConverter.ConvertFromString<ShittyTreeType>(array[1]),
				MatureTime = HumanReadableConverter.ConvertFromString<double>(array[2])
			};
		}

		public string SaveSaplingData(SaplingData saplingData)
		{
			m_stringBuilder.Length = 0;
			m_stringBuilder.Append(HumanReadableConverter.ConvertToString(saplingData.Point));
			m_stringBuilder.Append(';');
			m_stringBuilder.Append(HumanReadableConverter.ConvertToString(saplingData.TreeType));
			m_stringBuilder.Append(';');
			m_stringBuilder.Append(HumanReadableConverter.ConvertToString(saplingData.MatureTime));
			return m_stringBuilder.ToString();
		}

		public void MatureSapling(SaplingData saplingData)
		{
			if (m_subsystemGameInfo.TotalElapsedGameTime < saplingData.MatureTime)
				return;

			int x = saplingData.Point.X;
			int y = saplingData.Point.Y;
			int z = saplingData.Point.Z;

			TerrainChunk chunk1 = base.SubsystemTerrain.Terrain.GetChunkAtCell(x - 6, z - 6);
			TerrainChunk chunk2 = base.SubsystemTerrain.Terrain.GetChunkAtCell(x - 6, z + 6);
			TerrainChunk chunk3 = base.SubsystemTerrain.Terrain.GetChunkAtCell(x + 6, z - 6);
			TerrainChunk chunk4 = base.SubsystemTerrain.Terrain.GetChunkAtCell(x + 6, z + 6);

			if (chunk1 == null || chunk1.State != TerrainChunkState.Valid ||
				chunk2 == null || chunk2.State != TerrainChunkState.Valid ||
				chunk3 == null || chunk3.State != TerrainChunkState.Valid ||
				chunk4 == null || chunk4.State != TerrainChunkState.Valid)
			{
				saplingData.MatureTime = m_subsystemGameInfo.TotalElapsedGameTime;
				return;
			}

			int cellBelow = base.SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
			int cellHere = base.SubsystemTerrain.Terrain.GetCellValue(x, y, z);
			int belowContents = Terrain.ExtractContents(cellBelow);

			if (!BlocksManager.Blocks[belowContents].IsSuitableForPlants(cellBelow, cellHere))
			{
				base.SubsystemTerrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(28, 0, 0), true, null);
				return;
			}

			if (base.SubsystemTerrain.Terrain.GetCellLight(x, y + 1, z) >= 9)
			{
				float probability;
				if (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative)
				{
					probability = 1f;
				}
				else
				{
					int temperature = base.SubsystemTerrain.Terrain.GetTemperature(x, z)
						+ SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
					int humidity = base.SubsystemTerrain.Terrain.GetHumidity(x, z);

					probability = 2f * ShittyPlantsManager.CalculateFruitTreeProbability(
						saplingData.TreeType, temperature, humidity, y);
				}

				if (!m_random.Bool(probability))
				{
					base.SubsystemTerrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(28, 0, 0), true, null);
					return;
				}

				base.SubsystemTerrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(0, 0, 0), true, null);

				if (!GrowTree(x, y, z, saplingData.TreeType))
				{
					base.SubsystemTerrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(28, 0, 0), true, null);
					return;
				}
			}
			else if (m_subsystemGameInfo.TotalElapsedGameTime > saplingData.MatureTime + (double)m_subsystemTimeOfDay.DayDuration)
			{
				base.SubsystemTerrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(28, 0, 0), true, null);
			}
		}

		public bool GrowTree(int x, int y, int z, ShittyTreeType treeType)
		{
			ReadOnlyList<TerrainBrush> treeBrushes = ShittyPlantsManager.GetTreeBrushes(treeType);
			for (int i = 0; i < 20; i++)
			{
				TerrainBrush brush = treeBrushes[m_random.Int(0, treeBrushes.Count - 1)];
				bool canPlace = true;
				foreach (TerrainBrush.Cell cell in brush.Cells)
				{
					if (cell.Y >= 0 && (cell.X != 0 || cell.Y != 0 || cell.Z != 0))
					{
						int contents = base.SubsystemTerrain.Terrain.GetCellContents(
							(int)cell.X + x, (int)cell.Y + y, (int)cell.Z + z);
						if (contents != 0 && !(BlocksManager.Blocks[contents] is LeavesBlock))
						{
							canPlace = false;
							break;
						}
					}
				}
				if (canPlace)
				{
					brush.Paint(base.SubsystemTerrain, x, y, z);

					int temperature = base.SubsystemTerrain.Terrain.GetTemperature(x, z) + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
					int humidity = base.SubsystemTerrain.Terrain.GetHumidity(x, z);
					float fruitDensity = ShittyPlantsManager.CalculateFruitDensity(treeType, temperature, humidity, y);
					fruitDensity = MathUtils.Clamp(fruitDensity * m_random.Float(0.8f, 1.2f), 0f, 1f);

					ShittyPlantsManager.AttachFruitsToTree(base.SubsystemTerrain, x, y, z, brush, treeType, m_random, fruitDensity);
					return true;
				}
			}
			return false;
		}

		public void AddSapling(SaplingData saplingData)
		{
			m_saplings[saplingData.Point] = saplingData;
			m_enumerator = m_saplings.Values.GetEnumerator();
		}

		public void RemoveSapling(Point3 point)
		{
			m_saplings.Remove(point);
			m_enumerator = m_saplings.Values.GetEnumerator();
		}

		public class SaplingData
		{
			public Point3 Point;
			public ShittyTreeType TreeType;
			public double MatureTime;
		}
	}
}
