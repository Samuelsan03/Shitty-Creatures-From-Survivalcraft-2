using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemRemoteControlBlockBehavior : SubsystemBlockBehavior
	{
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemPlayers m_subsystemPlayers;
		public SubsystemGreenNightSky m_subsystemGreenNightSky;

		public override int[] HandledBlocks
		{
			get
			{
				Type remoteControlType = typeof(RemoteControlBlock);
				for (int i = 0; i < 1024; i++)
				{
					Block block = BlocksManager.Blocks[i];
					if (block != null && block.GetType() == remoteControlType)
					{
						return new int[] { i };
					}
				}
				return new int[] { 400 };
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemGreenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>(true);
		}

		public override bool OnUse(Ray3 ray, ComponentMiner miner)
		{
			ComponentPlayer player = miner.ComponentPlayer;
			if (player != null && player.ComponentGui != null)
			{
				// Pasar el jugador al diálogo
				GreenNightToggleDialog dialog = new GreenNightToggleDialog(m_subsystemGreenNightSky, player);
				player.ComponentGui.ModalPanelWidget = dialog;
				return true;
			}
			return false;
		}

		public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner miner)
		{
			Vector3 playerPos = miner.ComponentPlayer.ComponentBody.Position;
			Vector3 blockPos = new Vector3(raycastResult.CellFace.X + 0.5f, raycastResult.CellFace.Y + 0.5f, raycastResult.CellFace.Z + 0.5f);
			float distance = Vector3.Distance(playerPos, blockPos);
			if (distance > 5f)
				return false;

			ComponentPlayer player = miner.ComponentPlayer;
			if (player != null && player.ComponentGui != null)
			{
				// Pasar el jugador al diálogo
				GreenNightToggleDialog dialog = new GreenNightToggleDialog(m_subsystemGreenNightSky, player);
				player.ComponentGui.ModalPanelWidget = dialog;
				return true;
			}
			return false;
		}
	}
}
