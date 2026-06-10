using System;
using System.Collections.Generic;
using Engine;
using Game;

namespace Game
{
	public static class ShittyPlantsManager
	{
		public static List<TerrainBrush>[] m_treeBrushesByType;
		public static int[] m_treeTrunksByType;
		public static int[] m_treeLeavesByType;
		private static bool m_initialized = false;

		public static void Initialize()
		{
			if (m_initialized) return;

			// Obtener índices de bloques de forma dinámica (ya están asignados)
			m_treeTrunksByType = new int[]
			{
				BlocksManager.GetBlockIndex("AppleWoodBlock", true),
				BlocksManager.GetBlockIndex("PearWoodBlock", true),
				BlocksManager.GetBlockIndex("OrangeWoodBlock", true),
				BlocksManager.GetBlockIndex("CherryWoodBlock", true),
				BlocksManager.GetBlockIndex("BananaWoodBlock", true)
			};

			m_treeLeavesByType = new int[]
			{
				BlocksManager.GetBlockIndex("AppleLeavesBlock", true),
				BlocksManager.GetBlockIndex("PearLeavesBlock", true),
				BlocksManager.GetBlockIndex("OrangeLeavesBlock", true),
				BlocksManager.GetBlockIndex("CherryLeavesBlock", true),
				BlocksManager.GetBlockIndex("BananaLeavesBlock", true)
			};

			// Inicializar el array de brushes
			int maxTreeType = EnumUtils.GetEnumValues<ShittyTreeType>().Max();
			m_treeBrushesByType = new List<TerrainBrush>[maxTreeType + 1];

			Random random = new Random(33);

			// ---- Apple ----
			m_treeBrushesByType[(int)ShittyTreeType.Apple] = new List<TerrainBrush>();
			for (int i = 0; i < 16; i++)
			{
				int[] heights = { 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 13 };
				int height = heights[i];
				int branches = (int)MathUtils.Lerp(8, 18, i / 16f);
				var brush = PlantsManager.CreateTreeBrush(
					random,
					m_treeTrunksByType[(int)ShittyTreeType.Apple],
					m_treeLeavesByType[(int)ShittyTreeType.Apple],
					height, branches, 3,
					(y, _) => {
						float t = y / (float)height;
						if (t < 0.3f) return 0f;
						if (t > 0.8f) return 0.2f * (1f - (t - 0.8f) / 0.2f);
						return MathUtils.Lerp(0.2f, 0.8f, (t - 0.3f) / 0.5f);
					},
					(y) => {
						if (y < height * 0.3f || y > height * 0.8f) return 0f;
						return random.Float(0.5f, 1.2f) * (height * 0.4f);
					});
				m_treeBrushesByType[(int)ShittyTreeType.Apple].Add(brush);
			}

			// ---- Pear ----
			m_treeBrushesByType[(int)ShittyTreeType.Pear] = new List<TerrainBrush>();
			for (int i = 0; i < 16; i++)
			{
				int[] heights = { 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13 };
				int height = heights[i];
				int branches = (int)MathUtils.Lerp(6, 16, i / 16f);
				var brush = PlantsManager.CreateTreeBrush(
					random,
					m_treeTrunksByType[(int)ShittyTreeType.Pear],
					m_treeLeavesByType[(int)ShittyTreeType.Pear],
					height, branches, 3,
					(y, _) => {
						float t = y / (float)height;
						if (t < 0.25f) return 0f;
						if (t > 0.85f) return 0.1f * (1f - (t - 0.85f) / 0.15f);
						return MathUtils.Lerp(0.1f, 0.7f, (t - 0.25f) / 0.6f);
					},
					(y) => {
						if (y < height * 0.4f || y > height * 0.75f) return 0f;
						return random.Float(0.4f, 1f) * (height * 0.35f);
					});
				m_treeBrushesByType[(int)ShittyTreeType.Pear].Add(brush);
			}

			// ---- Orange ----
			m_treeBrushesByType[(int)ShittyTreeType.Orange] = new List<TerrainBrush>();
			for (int i = 0; i < 16; i++)
			{
				int[] heights = { 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 14 };
				int height = heights[i];
				int branches = (int)MathUtils.Lerp(10, 20, i / 16f);
				var brush = PlantsManager.CreateTreeBrush(
					random,
					m_treeTrunksByType[(int)ShittyTreeType.Orange],
					m_treeLeavesByType[(int)ShittyTreeType.Orange],
					height, branches, 3,
					(y, _) => {
						float t = y / (float)height;
						if (t < 0.35f) return 0f;
						if (t > 0.9f) return 0.3f * (1f - (t - 0.9f) / 0.1f);
						return MathUtils.Lerp(0.2f, 0.9f, (t - 0.35f) / 0.55f);
					},
					(y) => {
						if (y < height * 0.35f || y > height * 0.85f) return 0f;
						return random.Float(0.6f, 1.3f) * (height * 0.45f);
					});
				m_treeBrushesByType[(int)ShittyTreeType.Orange].Add(brush);
			}

			// ---- Cherry ----
			m_treeBrushesByType[(int)ShittyTreeType.Cherry] = new List<TerrainBrush>();
			for (int i = 0; i < 16; i++)
			{
				int[] heights = { 5, 5, 6, 6, 7, 7, 8, 8, 8, 9, 9, 10, 10, 11, 11, 12 };
				int height = heights[i];
				int branches = (int)MathUtils.Lerp(12, 22, i / 16f);
				var brush = PlantsManager.CreateTreeBrush(
					random,
					m_treeTrunksByType[(int)ShittyTreeType.Cherry],
					m_treeLeavesByType[(int)ShittyTreeType.Cherry],
					height, branches, 3,
					(y, _) => {
						float t = y / (float)height;
						if (t < 0.4f) return 0f;
						if (t > 0.85f) return 0.4f * (1f - (t - 0.85f) / 0.15f);
						return MathUtils.Lerp(0.15f, 0.8f, (t - 0.4f) / 0.45f);
					},
					(y) => {
						if (y < height * 0.45f || y > height * 0.8f) return 0f;
						return random.Float(0.5f, 1.1f) * (height * 0.4f);
					});
				m_treeBrushesByType[(int)ShittyTreeType.Cherry].Add(brush);
			}

			// ---- Banana ----
			m_treeBrushesByType[(int)ShittyTreeType.Banana] = new List<TerrainBrush>();
			for (int i = 0; i < 16; i++)
			{
				int[] heights = { 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13, 14, 14, 15 };
				int height = heights[i];
				int branches = (int)MathUtils.Lerp(8, 16, i / 16f);
				var brush = PlantsManager.CreateTreeBrush(
					random,
					m_treeTrunksByType[(int)ShittyTreeType.Banana],
					m_treeLeavesByType[(int)ShittyTreeType.Banana],
					height, branches, 2,
					(y, _) => {
						if (y < height * 0.6f) return 0f;
						float t = (y - (int)(height * 0.6f)) / (height * 0.4f);
						return MathUtils.Lerp(0.1f, 0.9f, t);
					},
					(y) => {
						if (y < height * 0.7f) return 0f;
						return random.Float(0.8f, 1.5f) * (height * 0.3f);
					});
				m_treeBrushesByType[(int)ShittyTreeType.Banana].Add(brush);
			}

			m_initialized = true;
		}

		public static int GetTreeTrunkValue(ShittyTreeType treeType) => m_treeTrunksByType[(int)treeType];
		public static int GetTreeLeavesValue(ShittyTreeType treeType) => m_treeLeavesByType[(int)treeType];
		public static ReadOnlyList<TerrainBrush> GetTreeBrushes(ShittyTreeType treeType) => new ReadOnlyList<TerrainBrush>(m_treeBrushesByType[(int)treeType]);

		public static float CalculateTreeProbability(ShittyTreeType treeType, int temperature, int humidity, int y)
		{
			// Rangos ajustados para hacerlos más restrictivos
			switch (treeType)
			{
				// Manzano: templado-frío, humedad media (rango estrecho)
				case ShittyTreeType.Apple: return RangeProb(temperature, 6, 9, 12, 14) * RangeProb(humidity, 6, 9, 12, 14) * RangeProb(y, 0, 0, 80, 90);
				// Peral: templado, humedad media (rango más estrecho)
				case ShittyTreeType.Pear: return RangeProb(temperature, 7, 10, 13, 15) * RangeProb(humidity, 5, 8, 12, 14) * RangeProb(y, 0, 0, 80, 90);
				// Naranjo: cálido-húmedo (más restrictivo)
				case ShittyTreeType.Orange: return RangeProb(temperature, 8, 11, 14, 15) * RangeProb(humidity, 7, 10, 14, 15) * RangeProb(y, 0, 0, 75, 85);
				// Cerezo: fresco, humedad media-alta
				case ShittyTreeType.Cherry: return RangeProb(temperature, 5, 8, 11, 13) * RangeProb(humidity, 6, 9, 13, 15) * RangeProb(y, 0, 0, 85, 92);
				// Banano: tropical, cálido y muy húmedo
				case ShittyTreeType.Banana: return RangeProb(temperature, 9, 12, 15, 15) * RangeProb(humidity, 9, 12, 15, 15) * RangeProb(y, 0, 0, 70, 80);
				default: return 0f;
			}
		}

		public static float CalculateTreeDensity(ShittyTreeType treeType, int temperature, int humidity, int y)
		{
			// Densidad ahora usa temperatura Y humedad para todos los árboles
			switch (treeType)
			{
				case ShittyTreeType.Apple: return RangeProb(temperature, 6, 9, 12, 14) * RangeProb(humidity, 6, 9, 12, 14);
				case ShittyTreeType.Pear: return RangeProb(temperature, 7, 10, 13, 15) * RangeProb(humidity, 5, 8, 12, 14);
				case ShittyTreeType.Orange: return RangeProb(temperature, 8, 11, 14, 15) * RangeProb(humidity, 7, 10, 14, 15);
				case ShittyTreeType.Cherry: return RangeProb(temperature, 5, 8, 11, 13) * RangeProb(humidity, 6, 9, 13, 15);
				case ShittyTreeType.Banana: return RangeProb(temperature, 9, 12, 15, 15) * RangeProb(humidity, 9, 12, 15, 15);
				default: return 0f;
			}
		}

		public static ShittyTreeType? GenerateRandomTreeType(Random random, int temperature, int humidity, int y, float densityMultiplier = 1f)
		{
			ShittyTreeType? result = null;
			float maxProb = 0f;
			foreach (ShittyTreeType type in Enum.GetValues(typeof(ShittyTreeType)))
			{
				float prob = random.Float() * CalculateTreeProbability(type, temperature, humidity, y);
				if (prob > maxProb) { maxProb = prob; result = type; }
			}
			if (result != null && random.Bool(densityMultiplier * CalculateTreeDensity(result.Value, temperature, humidity, y)))
				return result;
			return null;
		}

		private static float RangeProb(float v, float a, float b, float c, float d)
		{
			if (v < a) return 0f;
			if (v < b) return (v - a) / (b - a);
			if (v <= c) return 1f;
			if (v <= d) return 1f - (v - c) / (d - c);
			return 0f;
		}

		public static bool IsLeafBlock(int blockContents)
		{
			foreach (int leaf in m_treeLeavesByType)
				if (blockContents == leaf) return true;
			return false;
		}

		public static float CalculateFruitTreeProbability(ShittyTreeType treeType, int temperature, int humidity, int y)
			=> CalculateTreeProbability(treeType, temperature, humidity, y);

		public static void AttachFruitsToTree(SubsystemTerrain subsystemTerrain, int originX, int originY, int originZ, TerrainBrush brush, ShittyTreeType treeType, Random random)
		{
			int fruitIndex = GetFruitIndexFromType(treeType);
			if (fruitIndex == 0) return;

			foreach (TerrainBrush.Cell cell in brush.Cells)
			{
				int x = originX + (int)cell.X;
				int y = originY + (int)cell.Y;
				int z = originZ + (int)cell.Z;

				int cellValue = subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
				int contents = Terrain.ExtractContents(cellValue);
				if (contents != GetTreeLeavesValue(treeType))
					continue;

				int belowValue = subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
				int belowContents = Terrain.ExtractContents(belowValue);
				if (belowContents == 0)
				{
					if (random.Float(0f, 1f) < 0.3f)
					{
						int fruitValue = Terrain.MakeBlockValue(fruitIndex);
						subsystemTerrain.ChangeCell(x, y - 1, z, fruitValue, true, null);
					}
				}
			}
		}

		private static int GetFruitIndexFromType(ShittyTreeType type)
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

		public static float CalculateFruitDensity(ShittyTreeType treeType, int temperature, int humidity, int y)
		{
			float prob = CalculateTreeProbability(treeType, temperature, humidity, y);
			if (prob < 0.2f) return 0.1f;      // muy baja -> casi sin frutos
			if (prob < 0.4f) return 0.3f;      // baja -> pocos frutos
			if (prob < 0.7f) return 0.6f;      // media -> algunos frutos
			return 0.9f;                        // alta -> muchos frutos
		}

		public static void AttachFruitsToTree(SubsystemTerrain subsystemTerrain, int originX, int originY, int originZ, TerrainBrush brush, ShittyTreeType treeType, Random random, float fruitDensity = 0.5f)
		{
			int fruitIndex = GetFruitIndexFromType(treeType);
			if (fruitIndex == 0) return;

			List<Point3> leafPositions = new List<Point3>();
			foreach (TerrainBrush.Cell cell in brush.Cells)
			{
				int x = originX + (int)cell.X;
				int y = originY + (int)cell.Y;
				int z = originZ + (int)cell.Z;
				int cellValue = subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
				int contents = Terrain.ExtractContents(cellValue);
				if (contents == GetTreeLeavesValue(treeType))
				{
					leafPositions.Add(new Point3(x, y, z));
				}
			}

			int maxFruits = 0;
			float perLeafProb = 0f;
			if (fruitDensity >= 0.8f)
			{
				maxFruits = int.MaxValue;
				perLeafProb = 0.6f;
			}
			else if (fruitDensity >= 0.4f)
			{
				maxFruits = 3;
				perLeafProb = 0.3f;
			}
			else
			{
				maxFruits = 1;
				perLeafProb = 0.1f;
			}

			// Mezclar posiciones para aleatoriedad
			for (int i = 0; i < leafPositions.Count; i++)
			{
				int swap = random.Int(i, leafPositions.Count - 1);
				Point3 temp = leafPositions[i];
				leafPositions[i] = leafPositions[swap];
				leafPositions[swap] = temp;
			}

			int fruitsPlaced = 0;
			foreach (Point3 leaf in leafPositions)
			{
				if (fruitsPlaced >= maxFruits) break;
				if (random.Float(0f, 1f) < perLeafProb)
				{
					int belowValue = subsystemTerrain.Terrain.GetCellValueFast(leaf.X, leaf.Y - 1, leaf.Z);
					if (Terrain.ExtractContents(belowValue) == 0)
					{
						int fruitValue = Terrain.MakeBlockValue(fruitIndex);
						subsystemTerrain.ChangeCell(leaf.X, leaf.Y - 1, leaf.Z, fruitValue, true, null);
						fruitsPlaced++;
					}
				}
			}
		}

		public static void AttachFruitsToTreeFast(TerrainChunk chunk, int originX, int originY, int originZ, TerrainBrush brush, ShittyTreeType treeType, Random random, float fruitDensity = 0.5f)
		{
			int fruitIndex = GetFruitIndexFromType(treeType);
			if (fruitIndex == 0) return;

			List<Point3> leafPositions = new List<Point3>();
			foreach (TerrainBrush.Cell cell in brush.Cells)
			{
				int x = originX + (int)cell.X;
				int y = originY + (int)cell.Y;
				int z = originZ + (int)cell.Z;
				// Convertir a coordenadas locales del chunk
				int lx = x - chunk.Origin.X;
				int lz = z - chunk.Origin.Y;
				if (lx >= 0 && lx < 16 && lz >= 0 && lz < 16)
				{
					int cellValue = chunk.GetCellValueFast(lx, y, lz);
					int contents = Terrain.ExtractContents(cellValue);
					if (contents == GetTreeLeavesValue(treeType))
					{
						leafPositions.Add(new Point3(lx, y, lz));
					}
				}
			}

			int maxFruits = 0;
			float perLeafProb = 0f;
			if (fruitDensity >= 0.8f)
			{
				maxFruits = int.MaxValue;
				perLeafProb = 0.6f;
			}
			else if (fruitDensity >= 0.4f)
			{
				maxFruits = 3;
				perLeafProb = 0.3f;
			}
			else
			{
				maxFruits = 1;
				perLeafProb = 0.1f;
			}

			// Mezclar
			for (int i = 0; i < leafPositions.Count; i++)
			{
				int swap = random.Int(i, leafPositions.Count - 1);
				Point3 temp = leafPositions[i];
				leafPositions[i] = leafPositions[swap];
				leafPositions[swap] = temp;
			}

			int fruitsPlaced = 0;
			foreach (Point3 leaf in leafPositions)
			{
				if (fruitsPlaced >= maxFruits) break;
				if (random.Float(0f, 1f) < perLeafProb)
				{
					int belowValue = chunk.GetCellValueFast(leaf.X, leaf.Y - 1, leaf.Z);
					if (Terrain.ExtractContents(belowValue) == 0)
					{
						int fruitValue = Terrain.MakeBlockValue(fruitIndex);
						chunk.SetCellValueFast(leaf.X, leaf.Y - 1, leaf.Z, fruitValue);
						fruitsPlaced++;
					}
				}
			}
		}
	}
}
