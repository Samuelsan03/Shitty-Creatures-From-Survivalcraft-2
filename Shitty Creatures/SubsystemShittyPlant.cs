using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemShittyPlant : SubsystemPollableBlockBehavior
	{
		private SubsystemTime m_subsystemTime;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemCellChangeQueue m_subsystemCellChangeQueue;
		private SubsystemGameInfo m_subsystemGameInfo;
		private double m_lastOrphanCheckTime;
		private double m_lastRegenerationTime;
		private Random m_regenerationRandom = new Random();

		public override int[] HandledBlocks
		{
			get
			{
				var list = new List<int> { BlueberryBushBlock.Index };
				foreach (int idx in GetFruitIndices())
				{
					if (idx > 0) list.Add(idx);
				}
				foreach (int idx in GetLeavesIndices())
				{
					if (idx > 0) list.Add(idx);
				}
				return list.ToArray();
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			ShittyPlantsManager.EnsureInitialized();
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemCellChangeQueue = base.Project.FindSubsystem<SubsystemCellChangeQueue>(true);
			m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			m_lastOrphanCheckTime = m_subsystemTime.GameTime;
			m_lastRegenerationTime = m_subsystemTime.GameTime;
		}

		private int[] GetFruitIndices()
		{
			return new int[]
			{
				BlocksManager.GetBlockIndex("AppleBlock", false),
				BlocksManager.GetBlockIndex("PearBlock", false),
				BlocksManager.GetBlockIndex("OrangeBlock", false),
				BlocksManager.GetBlockIndex("CherryBlock", false),
				BlocksManager.GetBlockIndex("BananaBlock", false)
			};
		}

		private int[] GetLeavesIndices()
		{
			return new int[]
			{
				BlocksManager.GetBlockIndex("AppleLeavesBlock", false),
				BlocksManager.GetBlockIndex("PearLeavesBlock", false),
				BlocksManager.GetBlockIndex("OrangeLeavesBlock", false),
				BlocksManager.GetBlockIndex("CherryLeavesBlock", false),
				BlocksManager.GetBlockIndex("BananaLeavesBlock", false)
			};
		}

		private bool IsFruitBlock(int contents)
		{
			foreach (int idx in GetFruitIndices())
				if (contents == idx) return true;
			return false;
		}

		private bool IsLeavesBlock(int contents)
		{
			foreach (int idx in GetLeavesIndices())
				if (contents == idx) return true;
			return false;
		}

		// SOLUCIÓN: Usar el índice estático directamente, igual que el juego original.
		// NUNCA usar BlocksManager.GetBlockIndex en comprobaciones de lógica en tiempo real.
		private bool IsBlueberryBushBlock(int contents)
		{
			return contents == BlueberryBushBlock.Index;
		}

		public sealed override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
		{
			int cellValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y, z);
			int contents = Terrain.ExtractContents(cellValue);

			if (IsFruitBlock(contents))
			{
				if (neighborY == y + 1)
				{
					int aboveValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
					int aboveContents = Terrain.ExtractContents(aboveValue);
					if (aboveContents == 0 || aboveContents == 20 || !IsLeavesBlock(aboveContents))
					{
						base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
					}
				}
			}
			else if (IsLeavesBlock(contents))
			{
				int belowValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
				int belowContents = Terrain.ExtractContents(belowValue);
				if (IsFruitBlock(belowContents))
				{
					base.SubsystemTerrain.DestroyCell(0, x, y - 1, z, 0, false, false, null);
				}
			}
			else if (IsBlueberryBushBlock(contents))
			{
				// Lógica exacta del SubsystemPlantBlockBehavior original
				int belowValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
				Block belowBlock = BlocksManager.Blocks[Terrain.ExtractContents(belowValue)];

				if (!belowBlock.IsSuitableForPlants(belowValue, cellValue))
				{
					base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
				}
			}
		}

		public override void OnPoll(int value, int x, int y, int z, int pollPass)
		{
			int contents = Terrain.ExtractContents(value);

			// Crecimiento del arbusto de arándanos
			if (IsBlueberryBushBlock(contents))
			{
				// COMPROBACIÓN DE MODO DE JUEGO: Igual que el original
				if (m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
				{
					GrowBlueberryBush(value, x, y, z, pollPass);
				}
				return;
			}

			if (pollPass == 0 && m_subsystemTime != null)
			{
				double currentTime = m_subsystemTime.GameTime;
				if (currentTime - m_lastOrphanCheckTime >= 10.0)
				{
					m_lastOrphanCheckTime = currentTime;
					CheckOrphanFruits(x, y, z);
				}
				if (currentTime - m_lastRegenerationTime >= 60.0)
				{
					m_lastRegenerationTime = currentTime;
					TryRegenerateFruit(x, y, z);
				}
			}
		}

		public void GrowBlueberryBush(int value, int x, int y, int z, int pollPass)
		{
			int data = Terrain.ExtractData(value);

			if (BlueberryBushBlock.GetIsSmall(data))
			{
				int lightAbove = Terrain.ExtractLight(base.SubsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z));
				if (lightAbove >= 9)
				{
					int newData = BlueberryBushBlock.SetIsSmall(data, false);
					int newValue = Terrain.ReplaceData(value, newData);
					m_subsystemCellChangeQueue.QueueCellChange(x, y, z, newValue, false);
				}
			}
		}

		public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
		{
			if (isLoaded) return;
			int contents = Terrain.ExtractContents(value);
			if (IsFruitBlock(contents))
			{
				int aboveValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
				if (!IsLeavesBlock(Terrain.ExtractContents(aboveValue)))
				{
					base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
				}
			}
		}

		private void CheckOrphanFruits(int x, int y, int z)
		{
			int cellValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y, z);
			int contents = Terrain.ExtractContents(cellValue);
			if (IsFruitBlock(contents))
			{
				int aboveValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
				int aboveContents = Terrain.ExtractContents(aboveValue);
				if (!IsLeavesBlock(aboveContents))
				{
					base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
				}
			}
		}

		private void TryRegenerateFruit(int leafX, int leafY, int leafZ)
		{
			int leafValue = base.SubsystemTerrain.Terrain.GetCellValue(leafX, leafY, leafZ);
			int leafContents = Terrain.ExtractContents(leafValue);
			if (!IsLeavesBlock(leafContents))
				return;

			ShittyTreeType? treeType = GetTreeTypeFromLeaf(leafContents);
			if (!treeType.HasValue)
				return;

			int fruitIndex = GetFruitIndexFromType(treeType.Value);
			if (fruitIndex == 0)
				return;

			int belowValue = base.SubsystemTerrain.Terrain.GetCellValue(leafX, leafY - 1, leafZ);
			int belowContents = Terrain.ExtractContents(belowValue);
			if (IsFruitBlock(belowContents))
				return;

			int seasonalTemperature = m_subsystemTerrain.Terrain.GetSeasonalTemperature(leafX, leafZ);
			int seasonalHumidity = m_subsystemTerrain.Terrain.GetSeasonalHumidity(leafX, leafZ);

			float baseProbability = 0.02f;
			float probability = baseProbability * ShittyPlantsManager.CalculateFruitTreeProbability(
				treeType.Value, seasonalTemperature, seasonalHumidity, leafY - 1);

			if (m_regenerationRandom.Float(0f, 1f) < probability)
			{
				int newFruitValue = Terrain.MakeBlockValue(fruitIndex);
				base.SubsystemTerrain.ChangeCell(leafX, leafY - 1, leafZ, newFruitValue, true, null);
			}
		}

		private ShittyTreeType? GetTreeTypeFromLeaf(int leafContents)
		{
			if (leafContents == BlocksManager.GetBlockIndex("AppleLeavesBlock", false))
				return ShittyTreeType.Apple;
			if (leafContents == BlocksManager.GetBlockIndex("PearLeavesBlock", false))
				return ShittyTreeType.Pear;
			if (leafContents == BlocksManager.GetBlockIndex("OrangeLeavesBlock", false))
				return ShittyTreeType.Orange;
			if (leafContents == BlocksManager.GetBlockIndex("CherryLeavesBlock", false))
				return ShittyTreeType.Cherry;
			if (leafContents == BlocksManager.GetBlockIndex("BananaLeavesBlock", false))
				return ShittyTreeType.Banana;
			return null;
		}

		private int GetFruitIndexFromType(ShittyTreeType type)
		{
			switch (type)
			{
				case ShittyTreeType.Apple: return BlocksManager.GetBlockIndex("AppleBlock", false);
				case ShittyTreeType.Pear: return BlocksManager.GetBlockIndex("PearBlock", false);
				case ShittyTreeType.Orange: return BlocksManager.GetBlockIndex("OrangeBlock", false);
				case ShittyTreeType.Cherry: return BlocksManager.GetBlockIndex("CherryBlock", false);
				case ShittyTreeType.Banana: return BlocksManager.GetBlockIndex("BananaBlock", false);
				default: return 0;
			}
		}
	}
}
