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
			// Manzano
			m_treeTrunksByType[(int)ShittyTreeType.Apple] = GetBlockIndex("AppleWoodBlock");
			m_treeLeavesByType[(int)ShittyTreeType.Apple] = GetBlockIndex("AppleLeavesBlock");
			m_treeFruitByType[(int)ShittyTreeType.Apple] = GetBlockIndex("AppleBlock");

			// Peral
			m_treeTrunksByType[(int)ShittyTreeType.Pear] = GetBlockIndex("PearWoodBlock");
			m_treeLeavesByType[(int)ShittyTreeType.Pear] = GetBlockIndex("PearLeavesBlock");
			m_treeFruitByType[(int)ShittyTreeType.Pear] = GetBlockIndex("PearBlock");

			// Naranjo
			m_treeTrunksByType[(int)ShittyTreeType.Orange] = GetBlockIndex("OrangeWoodBlock");
			m_treeLeavesByType[(int)ShittyTreeType.Orange] = GetBlockIndex("OrangeLeavesBlock");
			m_treeFruitByType[(int)ShittyTreeType.Orange] = GetBlockIndex("OrangeBlock");

			// Cerezo
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

			int maxFruit = 6;
			int heightMin = 5;
			int heightMax = 8;
			int branchesMin = 4;
			int branchesMax = 8;
			float leafStartFactor = 0.5f;
			float fruitProbability = 0.3f;

			switch (treeType)
			{
				case ShittyTreeType.Apple:
					maxFruit = 6;
					heightMin = 5;
					heightMax = 7;
					branchesMin = 4;
					branchesMax = 7;
					leafStartFactor = 0.55f;
					fruitProbability = 0.35f;
					break;
				case ShittyTreeType.Pear:
					maxFruit = 5;
					heightMin = 6;
					heightMax = 8;
					branchesMin = 3;
					branchesMax = 6;
					leafStartFactor = 0.5f;
					fruitProbability = 0.3f;
					break;
				case ShittyTreeType.Orange:
					maxFruit = 8;
					heightMin = 5;
					heightMax = 7;
					branchesMin = 5;
					branchesMax = 8;
					leafStartFactor = 0.6f;
					fruitProbability = 0.4f;
					break;
				case ShittyTreeType.Cherry:
					maxFruit = 4;
					heightMin = 5;
					heightMax = 6;
					branchesMin = 3;
					branchesMax = 5;
					leafStartFactor = 0.5f;
					fruitProbability = 0.25f;
					break;
			}

			for (int i = 0; i < variations; i++)
			{
				int height = random.Int(heightMin, heightMax);
				int branchesCount = random.Int(branchesMin, branchesMax);
				TerrainBrush brush = CreateFruitTreeBrush(random, wood, leaves, fruit, height, branchesCount, leafStartFactor, maxFruit, fruitProbability);
				m_treeBrushesByType[(int)treeType].Add(brush);
			}
		}

		public static TerrainBrush CreateFruitTreeBrush(Random random, int woodIndex, int leavesIndex, int fruitIndex, int height, int branchesCount, float leafStartFactor, int maxFruit, float fruitProbability)
		{
			TerrainBrush brush = new TerrainBrush();

			// Tronco principal
			brush.AddRay(0, -1, 0, 0, height, 0, 1, 1, 1, woodIndex);

			// Ramas
			for (int i = 0; i < branchesCount; i++)
			{
				int startY = random.Int(height / 2, height - 1);
				float angleRad = random.Float(0f, 2f * (float)Math.PI);
				float branchLength = random.Float(2f, 3.5f);
				int dx = (int)MathF.Round(MathF.Cos(angleRad) * branchLength);
				int dz = (int)MathF.Round(MathF.Sin(angleRad) * branchLength);
				int dy = random.Int(-1, 1);

				int endX = dx;
				int endY = startY + dy;
				int endZ = dz;

				int cutFace = 0;
				Vector3 dir = Vector3.Normalize(new Vector3(dx, dy, dz));
				if (MathF.Abs(dir.X) == MathUtils.Max(MathF.Abs(dir.X), MathF.Abs(dir.Y), MathF.Abs(dir.Z)))
					cutFace = 1;
				else if (MathF.Abs(dir.Y) == MathUtils.Max(MathF.Abs(dir.X), MathF.Abs(dir.Y), MathF.Abs(dir.Z)))
					cutFace = 4;

				TerrainBrush.Brush branchBrush = new TerrainBrush.Brush();
				branchBrush.m_handler1 = delegate (int? v)
				{
					if (v == null)
						return new int?(Terrain.MakeBlockValue(woodIndex, 0, WoodBlock.SetCutFace(0, cutFace)));
					return null;
				};

				brush.AddRay(0, startY, 0, endX, endY, endZ, 1, 1, 1, branchBrush);
			}

			// Hojas y frutas con mejor separación
			int fruitPlaced = 0;
			HashSet<Point3> fruitPositions = new HashSet<Point3>();

			for (int y = (int)(height * leafStartFactor); y <= height + 2; y++)
			{
				float radius = 2.0f;
				if (y == height) radius = 2.5f;
				if (y == height + 1) radius = 1.8f;
				if (y == height + 2) radius = 1.2f;

				int rad = (int)MathF.Ceiling(radius);
				for (int dx = -rad; dx <= rad; dx++)
				{
					for (int dz = -rad; dz <= rad; dz++)
					{
						float dist = MathF.Sqrt(dx * dx + dz * dz);
						if (dist <= radius && random.Float(0f, 1f) < 0.7f)
						{
							Point3 pos = new Point3(dx, y, dz);
							if (brush.GetValue(dx, y, dz) == null)
							{
								int? existing = brush.GetValue(dx, y, dz);
								if (existing == null || Terrain.ExtractContents(existing.Value) != woodIndex)
								{
									bool placeFruit = false;
									if (fruitIndex != 0 && fruitPlaced < maxFruit && y <= height)
									{
										bool isCenter = Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1;
										bool isOuterEdge = (dist > radius - 0.8f);

										if (!isCenter && isOuterEdge && random.Float(0f, 1f) < fruitProbability)
										{
											// Verificar que no haya otro fruto adyacente (en cualquier dirección)
											bool adjacentFruit = false;
											for (int fx = -1; fx <= 1 && !adjacentFruit; fx++)
											{
												for (int fy = -1; fy <= 1 && !adjacentFruit; fy++)
												{
													for (int fz = -1; fz <= 1 && !adjacentFruit; fz++)
													{
														if (fx == 0 && fy == 0 && fz == 0) continue;
														Point3 checkPos = new Point3(dx + fx, y + fy, dz + fz);
														if (fruitPositions.Contains(checkPos))
														{
															adjacentFruit = true;
														}
													}
												}
											}

											if (!adjacentFruit)
												placeFruit = true;
										}
									}

									if (placeFruit)
									{
										brush.AddCell(dx, y, dz, fruitIndex);
										fruitPositions.Add(pos);
										fruitPlaced++;
									}
									else
									{
										brush.AddCell(dx, y, dz, leavesIndex);
									}
								}
							}
						}
					}
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
			// Nunca en lugares helados
			if (SubsystemWeather.IsPlaceFrozen(temperature, y))
				return null;

			// Pesos para la selección (multiplicamos por random.Float como en vanilla)
			float appleWeight = random.Float() * CalculateFruitTreeProbability(ShittyTreeType.Apple, temperature, humidity, y);
			float pearWeight = random.Float() * CalculateFruitTreeProbability(ShittyTreeType.Pear, temperature, humidity, y);
			float orangeWeight = random.Float() * CalculateFruitTreeProbability(ShittyTreeType.Orange, temperature, humidity, y);
			float cherryWeight = random.Float() * CalculateFruitTreeProbability(ShittyTreeType.Cherry, temperature, humidity, y);

			float maxWeight = MathUtils.Max(appleWeight, pearWeight, orangeWeight, cherryWeight);
			if (maxWeight <= 0f)
				return null;

			ShittyTreeType? result = null;
			if (maxWeight == appleWeight)
				result = ShittyTreeType.Apple;
			else if (maxWeight == pearWeight)
				result = ShittyTreeType.Pear;
			else if (maxWeight == orangeWeight)
				result = ShittyTreeType.Orange;
			else
				result = ShittyTreeType.Cherry;

			// Filtro de densidad final (como random.Bool en el juego original)
			if (random.Bool(CalculateFruitTreeDensity(result.Value, temperature, humidity, y)))
				return result;

			return null;
		}

		private static float CalculateFruitTreeProbability(ShittyTreeType type, int temperature, int humidity, int y)
		{
			// Rangos de clima y altura para cada frutal (basados en sus necesidades reales)
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
				default:
					return 0f;
			}
		}

		private static float CalculateFruitTreeDensity(ShittyTreeType type, int temperature, int humidity, int y)
		{
			// Densidad baja para que no aparezcan en todos lados (ajusta según necesidad)
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

		public static int GetBlueberryBushIndex()
		{
			// 421 es el índice del BlueberryBushBlock
			return GetBlockIndex("BlueberryBushBlock");
		}

		// Condiciones para arándanos (ajusta según prefieras)
		public static bool CanPlaceBlueberryBush(int temperature, int humidity, int y)
		{
			if (SubsystemWeather.IsPlaceFrozen(temperature, y))
				return false;
			// Ejemplo: rango de temperatura 2..10, humedad 4..12, altura 70..100
			return temperature >= 2 && temperature <= 10
				&& humidity >= 4 && humidity <= 12
				&& y >= 70 && y <= 100;
		}
	}
}
