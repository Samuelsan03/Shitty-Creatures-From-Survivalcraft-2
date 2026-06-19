using System;
using System.Collections.Generic;
using System.Linq;
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
		private static readonly object m_lock = new object();

		// Eliminamos el constructor estático para evitar inicialización temprana

		public static void EnsureInitialized()
		{
			// Si no está inicializado o los índices son inválidos, reinicializar
			if (!m_initialized || m_treeTrunksByType == null || m_treeTrunksByType.Any(i => i < 0) || m_treeLeavesByType == null || m_treeLeavesByType.Any(i => i < 0))
			{
				Initialize();
			}
		}

		public static void Initialize()
		{
			lock (m_lock)
			{
				try
				{
					// Obtener índices de bloques dinámicamente (siempre frescos)
					m_treeTrunksByType = new int[]
					{
						BlocksManager.GetBlockIndex(typeof(AppleWoodBlock), false, false),
						BlocksManager.GetBlockIndex(typeof(PearWoodBlock), false, false),
						BlocksManager.GetBlockIndex(typeof(OrangeWoodBlock), false, false),
						BlocksManager.GetBlockIndex(typeof(CherryWoodBlock), false, false),
						BlocksManager.GetBlockIndex(typeof(BananaWoodBlock), false, false)
					};

					m_treeLeavesByType = new int[]
					{
						BlocksManager.GetBlockIndex(typeof(AppleLeavesBlock), false, false),
						BlocksManager.GetBlockIndex(typeof(PearLeavesBlock), false, false),
						BlocksManager.GetBlockIndex(typeof(OrangeLeavesBlock), false, false),
						BlocksManager.GetBlockIndex(typeof(CherryLeavesBlock), false, false),
						BlocksManager.GetBlockIndex(typeof(BananaLeavesBlock), false, false)
					};

					// Fallback para bloques no encontrados
					for (int i = 0; i < m_treeTrunksByType.Length; i++)
					{
						if (m_treeTrunksByType[i] < 0)
						{
							Log.Warning($"ShittyPlantsManager: Tree trunk block for {((ShittyTreeType)i)} not found! Using OakWood (9).");
							m_treeTrunksByType[i] = 9; // OakWood
						}
						if (m_treeLeavesByType[i] < 0)
						{
							Log.Warning($"ShittyPlantsManager: Tree leaves block for {((ShittyTreeType)i)} not found! Using OakLeaves (12).");
							m_treeLeavesByType[i] = 12; // OakLeaves
						}
					}

					int maxTreeType = Enum.GetValues(typeof(ShittyTreeType)).Cast<int>().Max();
					m_treeBrushesByType = new List<TerrainBrush>[maxTreeType + 1];

					Random random = new Random(33);

					CreateAppleBrushes(random);
					CreatePearBrushes(random);
					CreateOrangeBrushes(random);
					CreateCherryBrushes(random);
					CreateBananaBrushes(random);

					m_initialized = true;
					Log.Information("ShittyPlantsManager re-initialized successfully.");
				}
				catch (Exception e)
				{
					Log.Error($"ShittyPlantsManager initialization failed: {e}");
					// Asegurar que no quede en un estado inconsistente
					m_treeBrushesByType = new List<TerrainBrush>[Enum.GetValues(typeof(ShittyTreeType)).Cast<int>().Max() + 1];
					for (int i = 0; i < m_treeBrushesByType.Length; i++)
					{
						m_treeBrushesByType[i] = new List<TerrainBrush>();
					}
					m_initialized = true; // Evita bucles infinitos
				}
			}
		}

		private static void CreateAppleBrushes(Random random)
		{
			m_treeBrushesByType[(int)ShittyTreeType.Apple] = new List<TerrainBrush>();
			int trunk = m_treeTrunksByType[(int)ShittyTreeType.Apple];
			int leaves = m_treeLeavesByType[(int)ShittyTreeType.Apple];
			for (int i = 0; i < 16; i++)
			{
				int[] heights = { 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 13 };
				int height = heights[i];
				int branches = (int)MathUtils.Lerp(8, 18, i / 16f);
				var brush = PlantsManager.CreateTreeBrush(
					random,
					trunk,
					leaves,
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
		}

		private static void CreatePearBrushes(Random random)
		{
			m_treeBrushesByType[(int)ShittyTreeType.Pear] = new List<TerrainBrush>();
			int trunk = m_treeTrunksByType[(int)ShittyTreeType.Pear];
			int leaves = m_treeLeavesByType[(int)ShittyTreeType.Pear];
			for (int i = 0; i < 16; i++)
			{
				int[] heights = { 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13 };
				int height = heights[i];
				int branches = (int)MathUtils.Lerp(6, 16, i / 16f);
				var brush = PlantsManager.CreateTreeBrush(
					random,
					trunk,
					leaves,
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
		}

		private static void CreateOrangeBrushes(Random random)
		{
			m_treeBrushesByType[(int)ShittyTreeType.Orange] = new List<TerrainBrush>();
			int trunk = m_treeTrunksByType[(int)ShittyTreeType.Orange];
			int leaves = m_treeLeavesByType[(int)ShittyTreeType.Orange];
			for (int i = 0; i < 16; i++)
			{
				int[] heights = { 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 14 };
				int height = heights[i];
				int branches = (int)MathUtils.Lerp(10, 20, i / 16f);
				var brush = PlantsManager.CreateTreeBrush(
					random,
					trunk,
					leaves,
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
		}

		private static void CreateCherryBrushes(Random random)
		{
			m_treeBrushesByType[(int)ShittyTreeType.Cherry] = new List<TerrainBrush>();
			int trunk = m_treeTrunksByType[(int)ShittyTreeType.Cherry];
			int leaves = m_treeLeavesByType[(int)ShittyTreeType.Cherry];
			for (int i = 0; i < 16; i++)
			{
				int[] heights = { 5, 5, 6, 6, 7, 7, 8, 8, 8, 9, 9, 10, 10, 11, 11, 12 };
				int height = heights[i];
				int branches = (int)MathUtils.Lerp(12, 22, i / 16f);
				var brush = PlantsManager.CreateTreeBrush(
					random,
					trunk,
					leaves,
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
		}

		private static void CreateBananaBrushes(Random random)
		{
			m_treeBrushesByType[(int)ShittyTreeType.Banana] = new List<TerrainBrush>();
			int trunk = m_treeTrunksByType[(int)ShittyTreeType.Banana];
			int leaves = m_treeLeavesByType[(int)ShittyTreeType.Banana];
			for (int i = 0; i < 16; i++)
			{
				int[] heights = { 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13, 14, 14, 15 };
				int height = heights[i];
				int branches = (int)MathUtils.Lerp(8, 16, i / 16f);
				var brush = PlantsManager.CreateTreeBrush(
					random,
					trunk,
					leaves,
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
		}

		public static int GetTreeTrunkValue(ShittyTreeType treeType)
		{
			EnsureInitialized();
			return m_treeTrunksByType[(int)treeType];
		}

		public static int GetTreeLeavesValue(ShittyTreeType treeType)
		{
			EnsureInitialized();
			return m_treeLeavesByType[(int)treeType];
		}

		public static ReadOnlyList<TerrainBrush> GetTreeBrushes(ShittyTreeType treeType)
		{
			EnsureInitialized();
			if (m_treeBrushesByType == null || m_treeBrushesByType.Length <= (int)treeType || m_treeBrushesByType[(int)treeType] == null)
			{
				return new ReadOnlyList<TerrainBrush>(new List<TerrainBrush>());
			}
			return new ReadOnlyList<TerrainBrush>(m_treeBrushesByType[(int)treeType]);
		}

		public static float CalculateTreeProbability(ShittyTreeType treeType, int temperature, int humidity, int y)
		{
			switch (treeType)
			{
				case ShittyTreeType.Apple:
					return RangeProb(temperature, 4, 6, 12, 14) * RangeProb(humidity, 4, 6, 12, 14) * RangeProb(y, 0, 0, 80, 90);
				case ShittyTreeType.Pear:
					return RangeProb(temperature, 5, 7, 13, 15) * RangeProb(humidity, 6, 8, 14, 15) * RangeProb(y, 0, 0, 80, 90);
				case ShittyTreeType.Orange:
					return RangeProb(temperature, 6, 8, 14, 15) * RangeProb(humidity, 8, 10, 15, 15) * RangeProb(y, 0, 0, 70, 85);
				case ShittyTreeType.Cherry:
					return RangeProb(temperature, 3, 5, 11, 13) * RangeProb(humidity, 3, 6, 11, 13) * RangeProb(y, 0, 0, 85, 92);
				case ShittyTreeType.Banana:
					return RangeProb(temperature, 7, 9, 15, 15) * RangeProb(humidity, 10, 12, 15, 15) * RangeProb(y, 0, 0, 70, 80);
				default: return 0f;
			}
		}

		public static float CalculateTreeDensity(ShittyTreeType treeType, int temperature, int humidity, int y)
		{
			switch (treeType)
			{
				case ShittyTreeType.Apple:
					return RangeProb(temperature, 4, 6, 12, 14) * RangeProb(humidity, 4, 6, 12, 14);
				case ShittyTreeType.Pear:
					return RangeProb(temperature, 5, 7, 13, 15) * RangeProb(humidity, 6, 8, 14, 15);
				case ShittyTreeType.Orange:
					return RangeProb(temperature, 6, 8, 14, 15) * RangeProb(humidity, 8, 10, 15, 15);
				case ShittyTreeType.Cherry:
					return RangeProb(temperature, 3, 5, 11, 13) * RangeProb(humidity, 3, 6, 11, 13);
				case ShittyTreeType.Banana:
					return RangeProb(temperature, 7, 9, 15, 15) * RangeProb(humidity, 10, 12, 15, 15);
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
			EnsureInitialized();
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
				case ShittyTreeType.Apple: return BlocksManager.GetBlockIndex(typeof(AppleBlock), true, true);
				case ShittyTreeType.Pear: return BlocksManager.GetBlockIndex(typeof(PearBlock), true, true);
				case ShittyTreeType.Orange: return BlocksManager.GetBlockIndex(typeof(OrangeBlock), true, true);
				case ShittyTreeType.Cherry: return BlocksManager.GetBlockIndex(typeof(CherryBlock), true, true);
				case ShittyTreeType.Banana: return BlocksManager.GetBlockIndex(typeof(BananaBlock), true, true);
				default: return 0;
			}
		}

		public static float CalculateFruitDensity(ShittyTreeType treeType, int temperature, int humidity, int y)
		{
			float prob = CalculateTreeProbability(treeType, temperature, humidity, y);
			switch (treeType)
			{
				case ShittyTreeType.Apple:
					if (prob < 0.2f) return 0.1f;
					if (prob < 0.4f) return 0.3f;
					if (prob < 0.7f) return 0.6f;
					return 0.8f;
				case ShittyTreeType.Pear:
					if (prob < 0.2f) return 0.1f;
					if (prob < 0.4f) return 0.3f;
					if (prob < 0.7f) return 0.5f;
					return 0.7f;
				case ShittyTreeType.Orange:
					if (prob < 0.2f) return 0.2f;
					if (prob < 0.4f) return 0.4f;
					if (prob < 0.7f) return 0.7f;
					return 0.9f;
				case ShittyTreeType.Cherry:
					if (prob < 0.2f) return 0.1f;
					if (prob < 0.4f) return 0.3f;
					if (prob < 0.7f) return 0.6f;
					return 0.8f;
				case ShittyTreeType.Banana:
					if (prob < 0.2f) return 0.2f;
					if (prob < 0.4f) return 0.5f;
					if (prob < 0.7f) return 0.8f;
					return 0.95f;
				default: return 0f;
			}
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
			float baseMultiplier = 1f;
			switch (treeType)
			{
				case ShittyTreeType.Apple: baseMultiplier = 1.0f; break;
				case ShittyTreeType.Pear: baseMultiplier = 0.9f; break;
				case ShittyTreeType.Orange: baseMultiplier = 1.2f; break;
				case ShittyTreeType.Cherry: baseMultiplier = 1.0f; break;
				case ShittyTreeType.Banana: baseMultiplier = 1.3f; break;
			}
			float adjustedDensity = fruitDensity * baseMultiplier;
			adjustedDensity = Math.Clamp(adjustedDensity, 0f, 0.95f);
			if (adjustedDensity >= 0.8f)
			{
				maxFruits = int.MaxValue;
				perLeafProb = 0.6f * Math.Min(1f, baseMultiplier);
			}
			else if (adjustedDensity >= 0.4f)
			{
				maxFruits = (int)(3 * baseMultiplier);
				perLeafProb = 0.3f * Math.Min(1f, baseMultiplier);
			}
			else
			{
				maxFruits = (int)(1 * baseMultiplier);
				perLeafProb = 0.1f * Math.Min(1f, baseMultiplier);
			}
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
			float baseMultiplier = 1f;
			switch (treeType)
			{
				case ShittyTreeType.Apple: baseMultiplier = 1.0f; break;
				case ShittyTreeType.Pear: baseMultiplier = 0.9f; break;
				case ShittyTreeType.Orange: baseMultiplier = 1.2f; break;
				case ShittyTreeType.Cherry: baseMultiplier = 1.0f; break;
				case ShittyTreeType.Banana: baseMultiplier = 1.3f; break;
			}
			float adjustedDensity = fruitDensity * baseMultiplier;
			adjustedDensity = Math.Clamp(adjustedDensity, 0f, 0.95f);
			if (adjustedDensity >= 0.8f)
			{
				maxFruits = int.MaxValue;
				perLeafProb = 0.6f * Math.Min(1f, baseMultiplier);
			}
			else if (adjustedDensity >= 0.4f)
			{
				maxFruits = (int)(3 * baseMultiplier);
				perLeafProb = 0.3f * Math.Min(1f, baseMultiplier);
			}
			else
			{
				maxFruits = (int)(1 * baseMultiplier);
				perLeafProb = 0.1f * Math.Min(1f, baseMultiplier);
			}
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

		public static void TryRegenerateFruit(SubsystemTerrain subsystemTerrain, int leafX, int leafY, int leafZ, Random random)
		{
			EnsureInitialized();
			int leafValue = subsystemTerrain.Terrain.GetCellValueFast(leafX, leafY, leafZ);
			int leafContents = Terrain.ExtractContents(leafValue);

			ShittyTreeType? treeType = GetTreeTypeFromLeaf(leafContents);
			if (!treeType.HasValue)
				return;

			int fruitIndex = GetFruitIndexFromType(treeType.Value);
			if (fruitIndex == 0)
				return;

			int belowValue = subsystemTerrain.Terrain.GetCellValueFast(leafX, leafY - 1, leafZ);
			int belowContents = Terrain.ExtractContents(belowValue);
			if (belowContents != 0)
				return;

			int seasonalTemperature = subsystemTerrain.Terrain.GetSeasonalTemperature(leafX, leafZ);
			int seasonalHumidity = subsystemTerrain.Terrain.GetSeasonalHumidity(leafX, leafZ);

			float baseProbability = 0.02f;
			float probability = baseProbability * CalculateFruitTreeProbability(
				treeType.Value, seasonalTemperature, seasonalHumidity, leafY - 1);

			if (random.Float(0f, 1f) < probability)
			{
				int newFruitValue = Terrain.MakeBlockValue(fruitIndex);
				subsystemTerrain.ChangeCell(leafX, leafY - 1, leafZ, newFruitValue, true, null);
			}
		}

		private static ShittyTreeType? GetTreeTypeFromLeaf(int leafContents)
		{
			EnsureInitialized();
			for (int i = 0; i < m_treeLeavesByType.Length; i++)
			{
				if (m_treeLeavesByType[i] == leafContents)
					return (ShittyTreeType)i;
			}
			return null;
		}
	}
}
