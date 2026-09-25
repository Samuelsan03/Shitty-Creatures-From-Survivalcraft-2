using System;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemCannonBallBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return Array.Empty<int>();
			}
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			bool result = true;
			if (cellFace != null)
			{
				int cellValue = this.m_subsystemTerrain.Terrain.GetCellValue(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z);
				int num = Terrain.ExtractContents(cellValue);
				Block block = BlocksManager.Blocks[num];
				if (worldItem.Velocity.Length() > 30f)
				{
					this.m_subsystemExplosions.TryExplodeBlock(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z, cellValue);
				}
				if (block.GetDensity(cellValue) >= 1.5f && worldItem.Velocity.Length() > 30f)
				{
					// Cannonballs have lower ricochet chance (0.1f vs 1.0f for musket balls)
					float num2 = 0.1f;
					float minDistance = 8f;
					if (this.m_random.Float(0f, 1f) < num2)
					{
						this.m_subsystemAudio.PlayRandomSound("Audio/Ricochets", 1f, this.m_random.Float(-0.2f, 0.2f), new Vector3((float)cellFace.Value.X, (float)cellFace.Value.Y, (float)cellFace.Value.Z), minDistance, true);
						result = false;
					}
				}
			}
			return result;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemExplosions = base.Project.FindSubsystem<SubsystemExplosions>(true);
		}

		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemExplosions m_subsystemExplosions;
		public SubsystemAudio m_subsystemAudio;
		public Random m_random = new Random();
	}
}
