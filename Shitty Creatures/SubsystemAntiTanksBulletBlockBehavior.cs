using System;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemAntiTanksBulletBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get { return new int[] { AntiTanksBulletBlock.Index }; }
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			bool result = true;

			if (cellFace != null)
			{
				int cellValue = m_subsystemTerrain.Terrain.GetCellValue(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z);
				int contents = Terrain.ExtractContents(cellValue);
				Block block = BlocksManager.Blocks[contents];

				// Explosión al impactar con velocidad > 30
				if (worldItem.Velocity.Length() > 30f)
				{
					m_subsystemExplosions.TryExplodeBlock(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z, cellValue);
				}
			}

			return result;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true);
		}

		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemExplosions m_subsystemExplosions;
		public SubsystemAudio m_subsystemAudio;
		public Random m_random = new Random();
	}
}
