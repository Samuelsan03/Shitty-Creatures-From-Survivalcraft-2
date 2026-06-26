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
				var list = new List<int> { BlueberryBushBlock.Index, WatermelonBlock.Index };
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
				// Solo verificar cuando el bloque DIRECTAMENTE arriba cambió (no diagonales)
				if (neighborX == x && neighborZ == z && neighborY == y + 1)
				{
					int aboveValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
					int aboveContents = Terrain.ExtractContents(aboveValue);

					// CORRECCIÓN: Eliminada la condición "aboveContents != 0 &&"
					// Ahora el fruto se destruye cuando arriba es aire (hoja destruida/quemada)
					// o cuando arriba es cualquier bloque que no sea hoja válida
					if (aboveContents != 20 && !IsLeavesBlock(aboveContents))
					{
						base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
					}
				}
			}
			else if (IsBlueberryBushBlock(contents))
			{
				int belowValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
				Block belowBlock = BlocksManager.Blocks[Terrain.ExtractContents(belowValue)];

				if (!belowBlock.IsSuitableForPlants(belowValue, cellValue))
				{
					base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
				}
			}
			else if (contents == WatermelonBlock.Index)
			{
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

			if (IsBlueberryBushBlock(contents))
			{
				if (m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
				{
					GrowBlueberryBush(value, x, y, z, pollPass);
				}
				return;
			}
			else if (contents == WatermelonBlock.Index)
			{
				if (m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
				{
					GrowWatermelon(value, x, y, z, pollPass);
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

		public void GrowWatermelon(int value, int x, int y, int z, int pollPass)
		{
			if (Terrain.ExtractLight(base.SubsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z)) < 9)
			{
				return;
			}
			int data = Terrain.ExtractData(value);
			int size = BaseWatermelonBlock.GetSize(data);
			if (BaseWatermelonBlock.GetIsDead(data) || size >= 7)
			{
				return;
			}
			int cellValueFast = base.SubsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
			int num = Terrain.ExtractContents(cellValueFast);
			int data2 = Terrain.ExtractData(cellValueFast);
			bool flag = num == 168 && SoilBlock.GetHydration(data2);
			int num2 = (num == 168) ? SoilBlock.GetNitrogen(data2) : 0;
			int num3 = base.SubsystemTerrain.Terrain.GetSeasonalTemperature(x, z) + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
			int num4 = 4;
			float num5 = 0.15f;
			if (num == 168)
			{
				num4--;
				num5 -= 0.05f;
			}
			if (num2 > 0)
			{
				num4--;
				num5 -= 0.05f;
			}
			if (flag)
			{
				num4--;
				num5 -= 0.05f;
			}
			if (num3 <= 8)
			{
				num4 += 5;
			}
			if (pollPass % MathUtils.Max(num4, 1) == 0 || num5 >= 1f)
			{
				int data3 = BaseWatermelonBlock.SetSize(data, MathUtils.Min(size + 1, 7));
				if (this.m_regenerationRandom.Float(0f, 1f) < num5)
				{
					data3 = BaseWatermelonBlock.SetIsDead(data3, true);
				}
				int value2 = Terrain.ReplaceData(value, data3);
				this.m_subsystemCellChangeQueue.QueueCellChange(x, y, z, value2, false);
				if (num == 168 && size + 1 == 7)
				{
					int data4 = SoilBlock.SetNitrogen(data2, MathUtils.Max(num2 - 3, 0));
					int value3 = Terrain.ReplaceData(cellValueFast, data4);
					this.m_subsystemCellChangeQueue.QueueCellChange(x, y - 1, z, value3, false);
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
				if (!IsLeavesBlock(Terrain.ExtractContents(aboveValue)) && Terrain.ExtractContents(aboveValue) != 20)
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
				// CORRECCIÓN: También incluir hojas de roble (20) como válidas
				if (aboveContents != 20 && !IsLeavesBlock(aboveContents))
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
