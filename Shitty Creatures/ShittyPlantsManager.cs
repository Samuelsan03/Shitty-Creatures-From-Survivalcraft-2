using System;
using System.Collections.Generic;
using Engine;

namespace Game
{
	public static class ShittyPlantsManager
	{
		public static List<TerrainBrush>[] m_treeBrushesByType;
		public static int[] m_treeTrunksByType;
		public static int[] m_treeLeavesByType;
		public static int[] m_treeFruitByType;
		private static HashSet<int> s_allLeafIndices;

		static ShittyPlantsManager()
		{
			int treeTypeCount = EnumUtils.GetEnumValues<ShittyTreeType>().Max() + 1;
			m_treeBrushesByType = new List<TerrainBrush>[treeTypeCount];
			m_treeTrunksByType = new int[treeTypeCount];
			m_treeLeavesByType = new int[treeTypeCount];
			m_treeFruitByType = new int[treeTypeCount];

			RegisterBlockIndices();

			// Inicializar conjunto de índices de hojas
			s_allLeafIndices = new HashSet<int>();
			foreach (int leaf in m_treeLeavesByType)
			{
				if (leaf != 0) s_allLeafIndices.Add(leaf);
			}

			Random random = new Random(33);
			for (int i = 0; i < treeTypeCount; i++)
			{
				m_treeBrushesByType[i] = new List<TerrainBrush>();
				ShittyTreeType treeType = (ShittyTreeType)i;
				CreateTreeBrushSet(random, treeType, 16);
			}
		}

		private static void RegisterBlockIndices()
		{
			m_treeTrunksByType[(int)ShittyTreeType.Apple] = GetBlockIndex("AppleWoodBlock");
			m_treeLeavesByType[(int)ShittyTreeType.Apple] = GetBlockIndex("AppleLeavesBlock");
			m_treeFruitByType[(int)ShittyTreeType.Apple] = GetBlockIndex("AppleBlock");

			m_treeTrunksByType[(int)ShittyTreeType.Pear] = GetBlockIndex("PearWoodBlock");
			m_treeLeavesByType[(int)ShittyTreeType.Pear] = GetBlockIndex("PearLeavesBlock");
			m_treeFruitByType[(int)ShittyTreeType.Pear] = GetBlockIndex("PearBlock");

			m_treeTrunksByType[(int)ShittyTreeType.Orange] = GetBlockIndex("OrangeWoodBlock");
			m_treeLeavesByType[(int)ShittyTreeType.Orange] = GetBlockIndex("OrangeLeavesBlock");
			m_treeFruitByType[(int)ShittyTreeType.Orange] = GetBlockIndex("OrangeBlock");

			m_treeTrunksByType[(int)ShittyTreeType.Cherry] = GetBlockIndex("CherryWoodBlock");
			m_treeLeavesByType[(int)ShittyTreeType.Cherry] = GetBlockIndex("CherryLeavesBlock");
			m_treeFruitByType[(int)ShittyTreeType.Cherry] = GetBlockIndex("CherryBlock");

			m_treeTrunksByType[(int)ShittyTreeType.Banana] = GetBlockIndex("BananaWoodBlock");
			m_treeLeavesByType[(int)ShittyTreeType.Banana] = GetBlockIndex("BananaLeavesBlock");
			m_treeFruitByType[(int)ShittyTreeType.Banana] = GetBlockIndex("BananaBlock");
		}

		private static int GetBlockIndex(string blockTypeName)
		{
			int index = BlocksManager.GetBlockIndex(blockTypeName, false);
			if (index < 0)
				throw new InvalidOperationException($"Bloque {blockTypeName} no encontrado.");
			return index;
		}

		private static void CreateTreeBrushSet(Random random, ShittyTreeType treeType, int variations)
		{
			int wood = m_treeTrunksByType[(int)treeType];
			int leaves = m_treeLeavesByType[(int)treeType];
			int fruit = m_treeFruitByType[(int)treeType];

			switch (treeType)
			{
				case ShittyTreeType.Apple:
					for (int i = 0; i < variations; i++)
					{
						int height = random.Int(5, 6);
						int clearTrunk = random.Int(1, 2);
						TerrainBrush brush = CreateFruitTreeBrush(
							random, wood, leaves, fruit, height, clearTrunk,
							(y, h) => {
								if (y < clearTrunk) return 0f;
								float t = (y - clearTrunk) / (float)(h - clearTrunk);
								float r = 2.8f + t * 2.2f;
								if (t > 0.7f) r -= (t - 0.7f) * 5f;
								return Math.Max(0.5f, r);
							},
							leafDensity: 1.0f,
							maxFruit: 6, fruitProbability: 0.4f,
							branchesCount: 5
						);
						m_treeBrushesByType[(int)treeType].Add(brush);
					}
					break;

				case ShittyTreeType.Pear:
					for (int i = 0; i < variations; i++)
					{
						int height = random.Int(7, 8);
						int clearTrunk = random.Int(2, 3);
						TerrainBrush brush = CreateFruitTreeBrush(
							random, wood, leaves, fruit, height, clearTrunk,
							(y, h) => {
								if (y < clearTrunk) return 0f;
								float t = (y - clearTrunk) / (float)(h - clearTrunk);
								return 3.2f * (1f - t) + 0.6f * t;
							},
							leafDensity: 0.95f,
							maxFruit: 5, fruitProbability: 0.35f,
							branchesCount: 4
						);
						m_treeBrushesByType[(int)treeType].Add(brush);
					}
					break;

				case ShittyTreeType.Orange:
					for (int i = 0; i < variations; i++)
					{
						int height = random.Int(4, 5);
						int clearTrunk = random.Int(1, 2);
						TerrainBrush brush = CreateFruitTreeBrush(
							random, wood, leaves, fruit, height, clearTrunk,
							(y, h) => {
								if (y < clearTrunk + 1) return 0f;
								float t = (y - (clearTrunk + 1)) / (float)(h - (clearTrunk + 1));
								float r = 2.3f + t * 1.0f;
								if (t > 0.8f) r -= (t - 0.8f) * 6f;
								return Math.Max(0.5f, r);
							},
							leafDensity: 0.75f,
							maxFruit: 8, fruitProbability: 0.45f,
							branchesCount: 6
						);
						m_treeBrushesByType[(int)treeType].Add(brush);
					}
					break;

				case ShittyTreeType.Cherry:
					for (int i = 0; i < variations; i++)
					{
						int height = random.Int(5, 6);
						int clearTrunk = random.Int(2, 3);
						TerrainBrush brush = CreateFruitTreeBrush(
							random, wood, leaves, fruit, height, clearTrunk,
							(y, h) => {
								if (y < clearTrunk) return 0f;
								float t = (y - clearTrunk) / (float)(h - clearTrunk);
								return MathUtils.Lerp(1.0f, 4.0f, t);
							},
							leafDensity: 0.85f,
							maxFruit: 4, fruitProbability: 0.3f,
							branchesCount: 4
						);
						m_treeBrushesByType[(int)treeType].Add(brush);
					}
					break;

				case ShittyTreeType.Banana:
					for (int i = 0; i < variations; i++)
					{
						int height = random.Int(6, 8);
						int clearTrunk = random.Int(2, 3);
						TerrainBrush brush = CreateFruitTreeBrush(
							random, wood, leaves, fruit, height, clearTrunk,
							(y, h) => {
								if (y < clearTrunk) return 0f;
								float t = (y - clearTrunk) / (float)(h - clearTrunk);
								float r = 2.5f + t * 2.0f;
								if (t > 0.6f) r += (t - 0.6f) * 1.5f;
								return Math.Min(4.5f, r);
							},
							leafDensity: 1.2f,
							maxFruit: 7, fruitProbability: 0.5f,
							branchesCount: 6
						);
						m_treeBrushesByType[(int)treeType].Add(brush);
					}
					break;
			}
		}

		public static TerrainBrush CreateFruitTreeBrush(
			Random random,
			int woodIndex, int leavesIndex, int fruitIndex,
			int height,
			int clearTrunk,
			Func<int, int, float> maxRadiusFunc,
			float leafDensity,
			int maxFruit, float fruitProbability,
			int branchesCount)
		{
			TerrainBrush brush = new TerrainBrush();

			// Tronco principal
			brush.AddRay(0, -1, 0, 0, height - 1, 0, 1, 1, 1, woodIndex);

			// Ramas
			for (int i = 0; i < branchesCount; i++)
			{
				int startY = random.Int(1, height - 2);
				int endY = random.Int(startY, height - 1);
				float maxRadius = maxRadiusFunc(endY, height);
				float targetDist = maxRadius * random.Float(0.7f, 1.0f);
				float angle = random.Float(0f, MathF.PI * 2f);
				int dx = (int)MathF.Round(MathF.Cos(angle) * targetDist);
				int dz = (int)MathF.Round(MathF.Sin(angle) * targetDist);
				int dy = endY - startY;

				int cutFace = 0;
				Vector3 dir = new Vector3(dx, dy, dz);
				if (dir.LengthSquared() > 0.01f)
				{
					Vector3 normDir = Vector3.Normalize(dir);
					if (MathF.Abs(normDir.X) >= MathF.Abs(normDir.Y) && MathF.Abs(normDir.X) >= MathF.Abs(normDir.Z))
						cutFace = 1;
					else if (MathF.Abs(normDir.Y) >= MathF.Abs(normDir.X) && MathF.Abs(normDir.Y) >= MathF.Abs(normDir.Z))
						cutFace = 4;
				}

				TerrainBrush.Brush branchBrush = new TerrainBrush.Brush();
				branchBrush.m_handler1 = delegate (int? v)
				{
					if (v == null)
						return new int?(Terrain.MakeBlockValue(woodIndex, 0, WoodBlock.SetCutFace(0, cutFace)));
					return null;
				};

				brush.AddRay(0, startY, 0, dx, endY, dz, 1, 1, 1, branchBrush);
			}

			// Crecimiento de hojas (5 rondas)
			for (int r = 0; r < 5; r++)
			{
				Point3 min, max;
				brush.CalculateBounds(out min, out max);

				for (int x = min.X - 1; x <= max.X + 1; x++)
				{
					for (int z = min.Z - 1; z <= max.Z + 1; z++)
					{
						for (int y = 0; y <= max.Y + 1; y++)
						{
							int? value = brush.GetValue(x, y, z);
							if (value != null && Terrain.ExtractContents(value.Value) == woodIndex)
							{
								for (int dx = -1; dx <= 1; dx++)
								{
									for (int dy = -1; dy <= 1; dy++)
									{
										for (int dz = -1; dz <= 1; dz++)
										{
											if (dx == 0 && dy == 0 && dz == 0) continue;
											int nx = x + dx;
											int ny = y + dy;
											int nz = z + dz;
											if (brush.GetValue(nx, ny, nz) != null) continue;

											float radius = maxRadiusFunc(ny, height);
											float distToAxis = MathF.Sqrt(nx * nx + nz * nz);
											if (distToAxis > radius + 0.5f) continue;

											float prob = leafDensity * (1f - distToAxis / (radius + 0.5f));
											if (r < 2) prob += 0.2f;
											if (random.Float(0f, 1f) < prob)
												brush.AddCell(nx, ny, nz, 0);
										}
									}
								}
							}
						}
					}
				}
				brush.Replace(0, leavesIndex);
			}

			// Frutos (solo durante la generación inicial)
			int fruitPlaced = 0;
			List<Point3> edgeLeaves = new List<Point3>();
			Point3 minB, maxB;
			brush.CalculateBounds(out minB, out maxB);
			for (int y = 0; y <= maxB.Y; y++)
			{
				float radius = maxRadiusFunc(y, height);
				if (radius <= 0f) continue;
				int rad = (int)MathF.Ceiling(radius);
				for (int dx = -rad; dx <= rad; dx++)
				{
					for (int dz = -rad; dz <= rad; dz++)
					{
						int? cell = brush.GetValue(dx, y, dz);
						if (cell != null && Terrain.ExtractContents(cell.Value) == leavesIndex)
						{
							float dist = MathF.Sqrt(dx * dx + dz * dz);
							if (dist > radius - 0.7f)
								edgeLeaves.Add(new Point3(dx, y, dz));
						}
					}
				}
			}

			edgeLeaves.Sort((a, b) => random.Int(0, 1) - random.Int(0, 1));
			foreach (Point3 leaf in edgeLeaves)
			{
				if (fruitPlaced >= maxFruit) break;

				float dynamicProb = fruitProbability * (1f - (float)fruitPlaced / maxFruit);
				if (random.Float(0f, 1f) >= dynamicProb) continue;

				int fx = leaf.X;
				int fy = leaf.Y - 1;
				int fz = leaf.Z;

				if (brush.GetValue(fx, fy, fz) != null) continue;

				bool adjacent = false;
				for (int ix = -1; ix <= 1 && !adjacent; ix++)
					for (int iy = -1; iy <= 1 && !adjacent; iy++)
						for (int iz = -1; iz <= 1 && !adjacent; iz++)
							if (brush.GetValue(fx + ix, fy + iy, fz + iz) == fruitIndex)
								adjacent = true;
				if (adjacent) continue;

				brush.AddCell(fx, fy, fz, fruitIndex);
				fruitPlaced++;
			}

			brush.Compile();
			return brush;
		}

		public static ReadOnlyList<TerrainBrush> GetTreeBrushes(ShittyTreeType treeType)
		{
			return new ReadOnlyList<TerrainBrush>(m_treeBrushesByType[(int)treeType]);
		}

		public static ShittyTreeType? GenerateRandomFruitTreeType(Random random, int temperature, int humidity, int y)
		{
			if (SubsystemWeather.IsPlaceFrozen(temperature, y))
				return null;

			float appleWeight = random.Float() * CalculateFruitTreeProbability(ShittyTreeType.Apple, temperature, humidity, y);
			float pearWeight = random.Float() * CalculateFruitTreeProbability(ShittyTreeType.Pear, temperature, humidity, y);
			float orangeWeight = random.Float() * CalculateFruitTreeProbability(ShittyTreeType.Orange, temperature, humidity, y);
			float cherryWeight = random.Float() * CalculateFruitTreeProbability(ShittyTreeType.Cherry, temperature, humidity, y);
			float bananaWeight = random.Float() * CalculateFruitTreeProbability(ShittyTreeType.Banana, temperature, humidity, y);

			float maxWeight = MathUtils.Max(MathUtils.Max(appleWeight, pearWeight, orangeWeight, cherryWeight), bananaWeight);
			if (maxWeight <= 0f) return null;

			ShittyTreeType? result = null;
			if (maxWeight == appleWeight) result = ShittyTreeType.Apple;
			else if (maxWeight == pearWeight) result = ShittyTreeType.Pear;
			else if (maxWeight == orangeWeight) result = ShittyTreeType.Orange;
			else if (maxWeight == cherryWeight) result = ShittyTreeType.Cherry;
			else if (maxWeight == bananaWeight) result = ShittyTreeType.Banana;

			if (result.HasValue && random.Bool(CalculateFruitTreeDensity(result.Value, temperature, humidity, y)))
				return result;
			return null;
		}

		public static float CalculateFruitTreeProbability(ShittyTreeType type, int temperature, int humidity, int y)
		{
			switch (type)
			{
				case ShittyTreeType.Apple:
					return RangeProbability(temperature, 5f, 7f, 12f, 14f) *
						   RangeProbability(humidity, 4f, 6f, 12f, 14f) *
						   RangeProbability(y, 64f, 66f, 90f, 95f);
				case ShittyTreeType.Pear:
					return RangeProbability(temperature, 4f, 6f, 11f, 13f) *
						   RangeProbability(humidity, 4f, 6f, 13f, 15f) *
						   RangeProbability(y, 64f, 66f, 88f, 92f);
				case ShittyTreeType.Orange:
					return RangeProbability(temperature, 11f, 13f, 15f, 16f) *
						   RangeProbability(humidity, 7f, 9f, 15f, 16f) *
						   RangeProbability(y, 64f, 66f, 80f, 85f);
				case ShittyTreeType.Cherry:
					return RangeProbability(temperature, 2f, 4f, 9f, 11f) *
						   RangeProbability(humidity, 3f, 5f, 9f, 11f) *
						   RangeProbability(y, 68f, 70f, 100f, 105f);
				case ShittyTreeType.Banana:
					return RangeProbability(temperature, 12f, 14f, 18f, 20f) *
						   RangeProbability(humidity, 10f, 12f, 18f, 20f) *
						   RangeProbability(y, 66f, 70f, 88f, 92f);
				default: return 0f;
			}
		}

		private static float CalculateFruitTreeDensity(ShittyTreeType type, int temperature, int humidity, int y)
		{
			const float baseDensity = 0.04f;
			return baseDensity * CalculateFruitTreeProbability(type, temperature, humidity, y);
		}

		private static float RangeProbability(float v, float a, float b, float c, float d)
		{
			if (v < a) return 0f;
			if (v < b) return (v - a) / (b - a);
			if (v <= c) return 1f;
			if (v <= d) return 1f - (v - c) / (d - c);
			return 0f;
		}

		public static int GetBlueberryBushIndex() => GetBlockIndex("BlueberryBushBlock");

		public static bool CanPlaceBlueberryBush(int temperature, int humidity, int y)
		{
			if (SubsystemWeather.IsPlaceFrozen(temperature, y)) return false;
			return temperature >= 2 && temperature <= 10
				&& humidity >= 4 && humidity <= 12
				&& y >= 70 && y <= 100;
		}

		// Nuevo método público para comprobar si un bloque es hoja de árbol frutal
		public static bool IsLeafBlock(int blockIndex)
		{
			return s_allLeafIndices != null && s_allLeafIndices.Contains(blockIndex);
		}
	}
}
