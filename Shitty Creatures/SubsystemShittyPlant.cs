using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemShittyPlant : SubsystemPollableBlockBehavior
	{
		private static readonly int[] FruitIndices;
		private static readonly int[] LeavesIndices;

		private SubsystemTime m_subsystemTime;
		private SubsystemTerrain m_subsystemTerrain;
		private double m_lastOrphanCheckTime;
		private double m_lastRegenerationTime;
		private Random m_regenerationRandom = new Random();

		static SubsystemShittyPlant()
		{
			FruitIndices = new int[]
			{
				BlocksManager.GetBlockIndex("AppleBlock", true),
				BlocksManager.GetBlockIndex("PearBlock", true),
				BlocksManager.GetBlockIndex("OrangeBlock", true),
				BlocksManager.GetBlockIndex("CherryBlock", true),
				BlocksManager.GetBlockIndex("BananaBlock", true)
			};

			LeavesIndices = new int[]
			{
				BlocksManager.GetBlockIndex("AppleLeavesBlock", true),
				BlocksManager.GetBlockIndex("PearLeavesBlock", true),
				BlocksManager.GetBlockIndex("OrangeLeavesBlock", true),
				BlocksManager.GetBlockIndex("CherryLeavesBlock", true),
				BlocksManager.GetBlockIndex("BananaLeavesBlock", true)
			};
		}

		public override int[] HandledBlocks
		{
			get
			{
				var list = new List<int> { BlueberryBushBlock.Index };
				list.AddRange(FruitIndices);
				list.AddRange(LeavesIndices);
				return list.ToArray();
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_lastOrphanCheckTime = m_subsystemTime.GameTime;
			m_lastRegenerationTime = m_subsystemTime.GameTime;
		}

		public sealed override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
		{
			int cellValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y, z);
			int contents = Terrain.ExtractContents(cellValue);

			if (FruitIndices.Contains(contents))
			{
				if (neighborY == y + 1)
				{
					int aboveValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
					int aboveContents = Terrain.ExtractContents(aboveValue);

					if (aboveContents == 0 || aboveContents == 20 || !LeavesIndices.Contains(aboveContents))
					{
						base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
					}
				}
			}
			else if (LeavesIndices.Contains(contents))
			{
				if (Terrain.ExtractContents(base.SubsystemTerrain.Terrain.GetCellValue(x, y, z)) == 0)
				{
					int belowValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
					if (FruitIndices.Contains(Terrain.ExtractContents(belowValue)))
					{
						base.SubsystemTerrain.DestroyCell(0, x, y - 1, z, 0, false, false, null);
					}
				}
			}
			else
			{
				if (neighborY == y - 1)
				{
					int belowValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
					Block belowBlock = BlocksManager.Blocks[Terrain.ExtractContents(belowValue)];

					if (!belowBlock.IsSuitableForPlants(belowValue, cellValue))
					{
						base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
					}
				}
			}
		}

		public override void OnPoll(int value, int x, int y, int z, int pollPass)
		{
			if (pollPass == 0 && m_subsystemTime != null)
			{
				double currentTime = m_subsystemTime.GameTime;

				// Verificar frutos huérfanos cada 10 segundos
				if (currentTime - m_lastOrphanCheckTime >= 10.0)
				{
					m_lastOrphanCheckTime = currentTime;
					CheckOrphanFruits(x, y, z);
				}

				// Regenerar frutos cada 60 segundos
				if (currentTime - m_lastRegenerationTime >= 60.0)
				{
					m_lastRegenerationTime = currentTime;
					TryRegenerateFruit(x, y, z);
				}
			}
		}

		private void CheckOrphanFruits(int x, int y, int z)
		{
			int cellValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y, z);
			int contents = Terrain.ExtractContents(cellValue);

			if (FruitIndices.Contains(contents))
			{
				int aboveValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
				int aboveContents = Terrain.ExtractContents(aboveValue);

				if (!LeavesIndices.Contains(aboveContents))
				{
					base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
				}
			}
		}

		public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
		{
			if (isLoaded) return;
			int contents = Terrain.ExtractContents(value);

			if (FruitIndices.Contains(contents))
			{
				int aboveValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
				if (!LeavesIndices.Contains(Terrain.ExtractContents(aboveValue)))
				{
					base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
				}
			}
		}

		// ==================== REGENERACIÓN DE FRUTOS ====================
		private void TryRegenerateFruit(int leafX, int leafY, int leafZ)
		{
			int leafValue = base.SubsystemTerrain.Terrain.GetCellValue(leafX, leafY, leafZ);
			int leafContents = Terrain.ExtractContents(leafValue);
			if (!LeavesIndices.Contains(leafContents))
				return;

			// Determinar el tipo de árbol por el bloque de hoja
			ShittyTreeType? treeType = GetTreeTypeFromLeaf(leafContents);
			if (!treeType.HasValue)
				return;

			// Obtener el índice de fruta correspondiente
			int fruitIndex = GetFruitIndexFromType(treeType.Value);
			if (fruitIndex == 0)
				return;

			// Buscar si ya hay fruto debajo de esta hoja
			int belowValue = base.SubsystemTerrain.Terrain.GetCellValue(leafX, leafY - 1, leafZ);
			int belowContents = Terrain.ExtractContents(belowValue);
			if (FruitIndices.Contains(belowContents))
				return; // Ya hay fruto, no regenerar

			// Obtener temperatura y humedad del Terrain
			int temperature = m_subsystemTerrain.Terrain.GetTemperature(leafX, leafZ);
			int humidity = m_subsystemTerrain.Terrain.GetHumidity(leafX, leafZ);
			int y = leafY - 1; // altura donde iría el fruto

			// Ajustar por estación (opcional, usar SeasonalTemperature/Humidity)
			int seasonalTemperature = m_subsystemTerrain.Terrain.GetSeasonalTemperature(leafX, leafZ);
			int seasonalHumidity = m_subsystemTerrain.Terrain.GetSeasonalHumidity(leafX, leafZ);

			// Probabilidad base por minuto (2% por hoja)
			float baseProbability = 0.02f;
			float probability = baseProbability *
				ShittyPlantsManager.CalculateFruitTreeProbability(treeType.Value, seasonalTemperature, seasonalHumidity, y);

			if (m_regenerationRandom.Float(0f, 1f) < probability)
			{
				// Colocar el fruto
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
