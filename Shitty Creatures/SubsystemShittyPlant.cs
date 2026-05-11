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
		private double m_lastOrphanCheckTime;

		static SubsystemShittyPlant()
		{
			FruitIndices = new int[]
			{
				BlocksManager.GetBlockIndex("AppleBlock", true),
				BlocksManager.GetBlockIndex("PearBlock", true),
				BlocksManager.GetBlockIndex("OrangeBlock", true),
				BlocksManager.GetBlockIndex("CherryBlock", true)
			};

			LeavesIndices = new int[]
			{
				BlocksManager.GetBlockIndex("AppleLeavesBlock", true),
				BlocksManager.GetBlockIndex("PearLeavesBlock", true),
				BlocksManager.GetBlockIndex("OrangeLeavesBlock", true),
				BlocksManager.GetBlockIndex("CherryLeavesBlock", true)
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
			m_lastOrphanCheckTime = m_subsystemTime.GameTime;
		}

		public sealed override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
		{
			int cellValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y, z);
			int contents = Terrain.ExtractContents(cellValue);

			if (FruitIndices.Contains(contents))
			{
				// FRUTA: Depende del bloque de ARRIBA (la hoja)
				if (neighborY == y + 1)
				{
					int aboveValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
					int aboveContents = Terrain.ExtractContents(aboveValue);

					// Si arriba hay aire, fuego, o cualquier cosa que no sea una hoja válida → destruir fruta
					if (aboveContents == 0 || aboveContents == 20 || !LeavesIndices.Contains(aboveContents))
					{
						base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
					}
				}
			}
			else if (LeavesIndices.Contains(contents))
			{
				// HOJA FRUTAL: Si se destruye, limpiar frutas debajo
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
				// ARBUSTO: Depende del bloque de ABAJO
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
			// Verificación periódica de frutas huérfanas (cada 10 segundos)
			if (pollPass == 0 && m_subsystemTime != null)
			{
				double currentTime = m_subsystemTime.GameTime;
				if (currentTime - m_lastOrphanCheckTime >= 10.0)
				{
					m_lastOrphanCheckTime = currentTime;
					CheckOrphanFruits(x, y, z);
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

				// Si no hay una hoja válida arriba → destruir
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
	}
}
