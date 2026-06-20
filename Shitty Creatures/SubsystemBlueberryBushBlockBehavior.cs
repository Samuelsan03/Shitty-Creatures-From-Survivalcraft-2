using System;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemBlueberryBushBlockBehavior : SubsystemPollableBlockBehavior
	{
		public override int[] HandledBlocks => new int[] { BlueberryBushBlock.Index };

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemCellChangeQueue m_subsystemCellChangeQueue;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemCellChangeQueue = Project.FindSubsystem<SubsystemCellChangeQueue>(true);
		}

		public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
		{
			int belowValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);
			int cellValue = base.SubsystemTerrain.Terrain.GetCellValue(x, y, z);

			// Usar el método estático para verificación consistente
			if (!BlueberryBushBlock.IsValidSupportBlock(belowValue, cellValue))
			{
				base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
			}
		}

		public override void OnPoll(int value, int x, int y, int z, int pollPass)
		{
			// PRIMERO: Verificar soporte
			if (DestroyIfNoSupport(x, y, z))
				return;

			// SEGUNDO: Crecimiento
			if (m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
			{
				int data = Terrain.ExtractData(value);

				if (BlueberryBushBlock.GetIsSmall(data))
				{
					if (y < 255 && Terrain.ExtractLight(m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z)) >= 9)
					{
						int newData = BlueberryBushBlock.SetIsSmall(data, false);
						int newValue = Terrain.ReplaceData(value, newData);
						m_subsystemCellChangeQueue.QueueCellChange(x, y, z, newValue, false);
					}
				}
			}
		}

		private bool DestroyIfNoSupport(int x, int y, int z)
		{
			if (y <= 0)
			{
				m_subsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
				return true;
			}

			int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
			int contents = Terrain.ExtractContents(cellValue);

			if (contents != BlueberryBushBlock.Index)
				return false;

			int belowValue = m_subsystemTerrain.Terrain.GetCellValue(x, y - 1, z);

			// Usar el método estático para verificación consistente
			if (!BlueberryBushBlock.IsValidSupportBlock(belowValue, cellValue))
			{
				m_subsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
				return true;
			}

			return false;
		}
	}
}
