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

			// Peral (sin fruta por ahora)
			m_treeTrunksByType[(int)ShittyTreeType.Pear] = GetBlockIndex("PearWoodBlock");
			m_treeLeavesByType[(int)ShittyTreeType.Pear] = GetBlockIndex("PearLeavesBlock");
			m_treeFruitByType[(int)ShittyTreeType.Pear] = 0;

			// Naranjo
			m_treeTrunksByType[(int)ShittyTreeType.Orange] = GetBlockIndex("OrangeWoodBlock");
			m_treeLeavesByType[(int)ShittyTreeType.Orange] = GetBlockIndex("OrangeLeavesBlock");
			m_treeFruitByType[(int)ShittyTreeType.Orange] = 0;

			// Cerezo
			m_treeTrunksByType[(int)ShittyTreeType.Cherry] = GetBlockIndex("CherryWoodBlock");
			m_treeLeavesByType[(int)ShittyTreeType.Cherry] = GetBlockIndex("CherryLeavesBlock");
			m_treeFruitByType[(int)ShittyTreeType.Cherry] = 0;
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

			for (int i = 0; i < variations; i++)
			{
				int height = random.Int(5, 8);
				int branchesCount = random.Int(4, 8);
				float leafStartFactor = 0.5f;

				switch (treeType)
				{
					case ShittyTreeType.Apple:
						height = random.Int(5, 7);
						branchesCount = random.Int(5, 9);
						leafStartFactor = 0.55f;
						break;
					case ShittyTreeType.Pear:
						height = random.Int(6, 8);
						branchesCount = random.Int(4, 7);
						leafStartFactor = 0.5f;
						break;
					case ShittyTreeType.Orange:
						height = random.Int(5, 7);
						branchesCount = random.Int(6, 10);
						leafStartFactor = 0.6f;
						break;
					case ShittyTreeType.Cherry:
						height = random.Int(5, 6);
						branchesCount = random.Int(4, 6);
						leafStartFactor = 0.5f;
						break;
				}

				TerrainBrush brush = CreateFruitTreeBrush(random, wood, leaves, fruit, height, branchesCount, leafStartFactor);
				m_treeBrushesByType[(int)treeType].Add(brush);
			}
		}

		public static TerrainBrush CreateFruitTreeBrush(Random random, int woodIndex, int leavesIndex, int fruitIndex, int height, int branchesCount, float leafStartFactor)
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

			// Hojas y frutas - colocación mejorada
			int fruitPlaced = 0;

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
						for (int dy = -1; dy <= 1; dy++)
						{
							int nx = dx, ny = y + dy, nz = dz;
							float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
							if (dist <= radius && random.Float(0f, 1f) < 0.7f)
							{
								if (brush.GetValue(nx, ny, nz) == null)
								{
									int? existing = brush.GetValue(nx, ny, nz);
									if (existing == null || Terrain.ExtractContents(existing.Value) != woodIndex)
									{
										bool placeFruit = false;
										// Solo fruta si:
										// 1. Hay fruta definida
										// 2. Aún no se alcanzó el máximo
										// 3. La celda está en el borde exterior (colgando)
										// 4. No está en el centro del árbol
										// 5. La altura está dentro de la mitad inferior (ny <= height)
										if (fruitIndex != 0 && fruitPlaced < 8 && ny <= height)
										{
											bool isCenter = Math.Abs(nx) <= 1 && Math.Abs(nz) <= 1;
											bool isOuterEdge = (dist > radius - 0.8f); // borde exterior del follaje
											if (!isCenter && isOuterEdge && random.Float(0f, 1f) < 0.2f) // 20% prob al borde
											{
												// Comprobar que no haya otra fruta en celdas adyacentes (horizontal)
												bool adjacentFruit = false;
												for (int fx = -1; fx <= 1 && !adjacentFruit; fx++)
												{
													for (int fz = -1; fz <= 1 && !adjacentFruit; fz++)
													{
														if (fx == 0 && fz == 0) continue;
														int? adj = brush.GetValue(nx + fx, ny, nz + fz);
														if (adj != null && Terrain.ExtractContents(adj.Value) == fruitIndex)
														{
															adjacentFruit = true;
														}
													}
												}
												if (!adjacentFruit)
													placeFruit = true;
											}
										}

										if (placeFruit)
										{
											brush.AddCell(nx, ny, nz, fruitIndex);
											fruitPlaced++;
										}
										else
										{
											brush.AddCell(nx, ny, nz, leavesIndex);
										}
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
			float appleProb = 0.9f;
			float pearProb = 0.03f;
			float orangeProb = 0.04f;
			float cherryProb = 0.03f;

			float total = appleProb + pearProb + orangeProb + cherryProb;
			float roll = random.Float(0f, total);

			if (roll < appleProb)
				return ShittyTreeType.Apple;
			if (roll < appleProb + pearProb)
				return ShittyTreeType.Pear;
			if (roll < appleProb + pearProb + orangeProb)
				return ShittyTreeType.Orange;
			return ShittyTreeType.Cherry;
		}
	}
}
