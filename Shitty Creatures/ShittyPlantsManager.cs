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

		static ShittyPlantsManager()
		{
			int treeTypeCount = EnumUtils.GetEnumValues<ShittyTreeType>().Max() + 1;
			m_treeBrushesByType = new List<TerrainBrush>[treeTypeCount];
			m_treeTrunksByType = new int[treeTypeCount];
			m_treeLeavesByType = new int[treeTypeCount];
			m_treeFruitByType = new int[treeTypeCount];

			RegisterBlockIndices();

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
						int trunkHeight = random.Int(2, 3);
						int overallHeight = random.Int(5, 6);
						int branchesCount = random.Int(4, 6);
						TerrainBrush brush = CreateFruitTreeBrush(
							random, wood, leaves, fruit,
							trunkHeight, overallHeight, branchesCount,
							(y, trunkH, overallH) =>
							{
								if (y <= trunkH) return 3.5f;   // radio amplio desde abajo
								float t = (y - trunkH) / (float)(overallH - trunkH);
								// Cúpula achatada: crece rápido, luego decrece
								float radius = MathUtils.Lerp(3.5f, 4.2f, t);
								if (t > 0.6f) radius -= (t - 0.6f) * 4f;
								return Math.Max(0.5f, radius);
							},
							leafDensity: 1.0f,   // densidad completa (sin huecos)
							maxFruit: 6,
							fruitProbability: 0.35f
						);
						m_treeBrushesByType[(int)treeType].Add(brush);
					}
					break;

				case ShittyTreeType.Pear:
					for (int i = 0; i < variations; i++)
					{
						int trunkHeight = random.Int(3, 4);
						int overallHeight = random.Int(7, 8);
						int branchesCount = random.Int(3, 5);
						TerrainBrush brush = CreateFruitTreeBrush(
							random, wood, leaves, fruit,
							trunkHeight, overallHeight, branchesCount,
							(y, trunkH, overallH) =>
							{
								if (y <= trunkH) return 3.2f;
								float t = (y - trunkH) / (float)(overallH - trunkH);
								// Forma cónica: el radio disminuye linealmente
								return Math.Max(0.4f, MathUtils.Lerp(3.2f, 0.6f, t));
							},
							leafDensity: 0.95f,
							maxFruit: 5,
							fruitProbability: 0.3f
						);
						m_treeBrushesByType[(int)treeType].Add(brush);
					}
					break;

				case ShittyTreeType.Orange:
					for (int i = 0; i < variations; i++)
					{
						int trunkHeight = random.Int(1, 2);
						int overallHeight = random.Int(4, 5);
						int branchesCount = random.Int(5, 7);
						TerrainBrush brush = CreateFruitTreeBrush(
							random, wood, leaves, fruit,
							trunkHeight, overallHeight, branchesCount,
							(y, trunkH, overallH) =>
							{
								// Tronco limpio: sin hojas hasta trunkH+1
								if (y <= trunkH + 1) return 0f;
								float t = (y - (trunkH + 1)) / (float)(overallH - (trunkH + 1));
								// Copa esférica achatada
								float radius = 2.2f + t * 1.0f;
								if (t > 0.7f) radius -= (t - 0.7f) * 3f;
								return Math.Max(0f, radius);
							},
							leafDensity: 0.75f,
							maxFruit: 8,
							fruitProbability: 0.4f
						);
						m_treeBrushesByType[(int)treeType].Add(brush);
					}
					break;

				case ShittyTreeType.Cherry:
					for (int i = 0; i < variations; i++)
					{
						int trunkHeight = random.Int(2, 3);
						int overallHeight = random.Int(5, 6);
						int branchesCount = random.Int(3, 5);
						TerrainBrush brush = CreateFruitTreeBrush(
							random, wood, leaves, fruit,
							trunkHeight, overallHeight, branchesCount,
							(y, trunkH, overallH) =>
							{
								if (y <= trunkH) return 1.2f;
								float t = (y - trunkH) / (float)(overallH - trunkH);
								// Vaso: muy estrecho abajo, se ensancha mucho arriba
								return MathUtils.Lerp(1.2f, 4.2f, t);
							},
							leafDensity: 0.9f,   // un poco más denso para que se vea tupido
							maxFruit: 4,
							fruitProbability: 0.25f
						);
						m_treeBrushesByType[(int)treeType].Add(brush);
					}
					break;
			}
		}

		public static TerrainBrush CreateFruitTreeBrush(
	Random random,
	int woodIndex, int leavesIndex, int fruitIndex,
	int trunkHeight, int overallHeight, int branchesCount,
	Func<int, int, int, float> maxRadiusFunc,
	float leafDensity,
	int maxFruit, float fruitProbability)
		{
			TerrainBrush brush = new TerrainBrush();

			// Tronco: desde el subsuelo (-1) hasta trunkHeight
			brush.AddRay(0, -1, 0, 0, trunkHeight, 0, 1, 1, 1, woodIndex);

			// Ramas principales (se extienden hasta cerca del borde de la copa)
			for (int i = 0; i < branchesCount; i++)
			{
				int startY = random.Int(1, trunkHeight);
				int endY = random.Int(Math.Max(startY, trunkHeight), overallHeight);
				float maxRadiusAtEnd = maxRadiusFunc(endY, trunkHeight, overallHeight);
				float targetDist = maxRadiusAtEnd * random.Float(0.6f, 0.9f);
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
			int rounds = 5;
			for (int r = 0; r < rounds; r++)
			{
				Point3 min, max;
				brush.CalculateBounds(out min, out max);

				for (int x = min.X - 1; x <= max.X + 1; x++)
				{
					for (int z = min.Z - 1; z <= max.Z + 1; z++)
					{
						for (int y = trunkHeight; y <= max.Y + 1; y++)
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

											float maxRadius = maxRadiusFunc(ny, trunkHeight, overallHeight);
											float distToAxis = MathF.Sqrt(nx * nx + nz * nz);
											if (distToAxis > maxRadius + 0.5f) continue;

											float prob = leafDensity * (1f - distToAxis / (maxRadius + 0.5f));
											if (r < 2) prob += 0.2f;
											if (random.Float(0f, 1f) < prob)
												brush.AddCell(nx, ny, nz, 0); // temporal
										}
									}
								}
							}
						}
					}
				}
				brush.Replace(0, leavesIndex);
			}

			// Colocar frutas DEBAJO de las hojas (cuelgan)
			int fruitPlaced = 0;
			List<Point3> leafPositions = new List<Point3>();
			Point3 minB, maxB;
			brush.CalculateBounds(out minB, out maxB);
			for (int y = trunkHeight; y <= maxB.Y; y++)
			{
				float maxRadius = maxRadiusFunc(y, trunkHeight, overallHeight);
				int rad = (int)MathF.Ceiling(maxRadius);
				for (int dx = -rad; dx <= rad; dx++)
				{
					for (int dz = -rad; dz <= rad; dz++)
					{
						int? cell = brush.GetValue(dx, y, dz);
						if (cell != null && Terrain.ExtractContents(cell.Value) == leavesIndex)
						{
							// Solo consideramos hojas en el borde exterior para que las frutas se vean
							float dist = MathF.Sqrt(dx * dx + dz * dz);
							if (dist > maxRadius - 0.8f)
								leafPositions.Add(new Point3(dx, y, dz));
						}
					}
				}
			}

			leafPositions.Sort((a, b) => random.Int(0, 1) - random.Int(0, 1));
			foreach (Point3 leaf in leafPositions)
			{
				if (fruitPlaced >= maxFruit) break;
				// Intentar colocar fruta en el bloque inmediatamente debajo de la hoja
				int fx = leaf.X;
				int fy = leaf.Y - 1;
				int fz = leaf.Z;
				if (brush.GetValue(fx, fy, fz) != null) continue; // debe estar vacío
																  // Verificar que no haya fruta adyacente
				bool adjacentFruit = false;
				for (int ix = -1; ix <= 1 && !adjacentFruit; ix++)
					for (int iy = -1; iy <= 1 && !adjacentFruit; iy++)
						for (int iz = -1; iz <= 1 && !adjacentFruit; iz++)
							if (brush.GetValue(fx + ix, fy + iy, fz + iz) == fruitIndex)
								adjacentFruit = true;

				if (!adjacentFruit && random.Float(0f, 1f) < fruitProbability)
				{
					brush.AddCell(fx, fy, fz, fruitIndex);
					fruitPlaced++;
				}
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

			float maxWeight = MathUtils.Max(appleWeight, pearWeight, orangeWeight, cherryWeight);
			if (maxWeight <= 0f)
				return null;

			ShittyTreeType? result = maxWeight == appleWeight ? ShittyTreeType.Apple :
									maxWeight == pearWeight ? ShittyTreeType.Pear :
									maxWeight == orangeWeight ? ShittyTreeType.Orange :
									ShittyTreeType.Cherry;

			if (random.Bool(CalculateFruitTreeDensity(result.Value, temperature, humidity, y)))
				return result;
			return null;
		}

		private static float CalculateFruitTreeProbability(ShittyTreeType type, int temperature, int humidity, int y)
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
	}
}
