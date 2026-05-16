using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemShittyRotBlockBehavior : SubsystemPollableBlockBehavior
	{
		private static readonly Dictionary<string, string> RotMapping = new Dictionary<string, string>
		{
			{ "AppleBlock", "RottenAppleBlock" },
			{ "PearBlock", "RottenPearBlock" },
			{ "OrangeBlock", "RottenOrangeBlock" },
			{ "CherryBlock", "RottenCherryBlock" },
			{ "SliceOfWatermelonBlock", "RottenSliceOfWatermelonBlock" },
			{ "BananaBlock", "RottenBananaBlock" },
			{ "BlueberryBlock", "RottenBlueberryBlock" }
		};

		private readonly Dictionary<int, int> m_freshToRotten = new Dictionary<int, int>();
		private int[] m_handledBlocksCache = null;

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemItemsScanner m_subsystemItemsScanner;
		private double m_lastRotTime;
		private int m_rotStep;
		private bool m_isRotEnabled;

		private const float RotPeriodBase = 60f;

		public override int[] HandledBlocks
		{
			get
			{
				if (m_handledBlocksCache != null)
					return m_handledBlocksCache;

				InitializeBlockIndices();
				var list = new List<int>(m_freshToRotten.Keys);
				m_handledBlocksCache = list.ToArray();
				return m_handledBlocksCache;
			}
		}

		private void InitializeBlockIndices()
		{
			if (m_freshToRotten.Count > 0)
				return;

			foreach (var kv in RotMapping)
			{
				int freshIndex = BlocksManager.GetBlockIndex(kv.Key, false);
				int rottenIndex = BlocksManager.GetBlockIndex(kv.Value, false);
				if (freshIndex != -1 && rottenIndex != -1)
				{
					m_freshToRotten[freshIndex] = rottenIndex;
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			InitializeBlockIndices();

			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemItemsScanner = Project.FindSubsystem<SubsystemItemsScanner>(true);
			m_lastRotTime = valuesDictionary.GetValue<double>("LastRotTime");
			m_rotStep = valuesDictionary.GetValue<int>("RotStep");

			if (m_subsystemItemsScanner != null)
			{
				m_subsystemItemsScanner.ItemsScanned += ItemsScanned;
			}

			m_isRotEnabled = (m_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative &&
							  m_subsystemGameInfo.WorldSettings.GameMode != GameMode.Adventure);
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			base.Save(valuesDictionary);
			valuesDictionary.SetValue("LastRotTime", m_lastRotTime);
			valuesDictionary.SetValue("RotStep", m_rotStep);
		}

		public override void OnPoll(int value, int x, int y, int z, int pollPass)
		{
			if (!m_isRotEnabled)
				return;

			int contents = Terrain.ExtractContents(value);
			if (!m_freshToRotten.TryGetValue(contents, out int rottenBlock))
				return;

			// --- INICIO MODIFICACIÓN: No pudrir si hay hoja encima ---
			int aboveValue = SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
			int aboveContents = Terrain.ExtractContents(aboveValue);
			if (ShittyPlantsManager.IsLeafBlock(aboveContents))
			{
				// El fruto aún está colgado del árbol → no se pudre
				return;
			}
			// --- FIN MODIFICACIÓN ---

			Block block = BlocksManager.Blocks[contents];
			int rotPeriod = block.GetRotPeriod(value);

			if (rotPeriod <= 0)
				return;

			if (pollPass % rotPeriod == 0)
			{
				int damage = block.GetDamage(value);
				int newDamage = damage + 1;

				if (newDamage <= 1)
				{
					int newValue = block.SetDamage(value, newDamage);
					SubsystemTerrain.ChangeCell(x, y, z, newValue, true, null);
				}
				else
				{
					int newValue = Terrain.MakeBlockValue(rottenBlock);
					SubsystemTerrain.ChangeCell(x, y, z, newValue, true, null);
				}
			}
		}

		private void ItemsScanned(ReadOnlyList<ScannedItemData> items)
		{
			int elapsedSteps = (int)((m_subsystemGameInfo.TotalElapsedGameTime - m_lastRotTime) / 60.0);
			if (elapsedSteps <= 0)
				return;

			if (m_isRotEnabled)
			{
				foreach (ScannedItemData item in items)
				{
					int value = item.Value;
					int contents = Terrain.ExtractContents(value);

					if (!m_freshToRotten.TryGetValue(contents, out int rottenBlock))
						continue;

					Block block = BlocksManager.Blocks[contents];
					int rotPeriod = block.GetRotPeriod(value);

					if (rotPeriod <= 0)
						continue;

					int damage = block.GetDamage(value);
					int newDamage = damage;
					int stepsProcessed = 0;

					while (stepsProcessed < elapsedSteps && newDamage <= 1)
					{
						if ((m_rotStep + stepsProcessed) % rotPeriod == 0)
						{
							newDamage++;
						}
						stepsProcessed++;
					}

					int newValue;
					if (newDamage <= 1)
					{
						newValue = block.SetDamage(value, newDamage);
						m_subsystemItemsScanner.TryModifyItem(item, newValue);
					}
					else
					{
						newValue = Terrain.MakeBlockValue(rottenBlock);
						m_subsystemItemsScanner.TryModifyItem(item, newValue);
					}
				}
			}

			m_rotStep += elapsedSteps;
			m_lastRotTime += elapsedSteps * 60.0;
		}
	}
}
