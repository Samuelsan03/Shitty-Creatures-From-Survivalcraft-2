using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class RemoteControlModLoader : ModLoader
	{
		public override void __ModInitialize()
		{
			Type remoteControlType = typeof(RemoteControlBlock);
			bool blockExists = false;

			foreach (var block in BlocksManager.Blocks)
			{
				if (block != null && block.GetType() == remoteControlType)
				{
					blockExists = true;
					break;
				}
			}

			if (!blockExists)
			{
				int freeIndex = -1;
				for (int i = 300; i < 1024; i++)
				{
					if (BlocksManager.Blocks[i] == null || BlocksManager.Blocks[i] is AirBlock)
					{
						freeIndex = i;
						break;
					}
				}

				if (freeIndex >= 0)
				{
					RemoteControlBlock block = new RemoteControlBlock();
					BlocksManager.m_blocks[freeIndex] = block;
					block.BlockIndex = freeIndex;
					BlocksManager.BlockNameToIndex["RemoteControlBlock"] = freeIndex;
					BlocksManager.BlockTypeToIndex[remoteControlType] = freeIndex;
				}
			}

			ModsManager.RegisterHook("OnPlayerInputInteract", this);
		}

		public override void OnPlayerInputInteract(ComponentPlayer player, ref bool playerOperated, ref double timeIntervalLastActionTime, ref int priorityUse, ref int priorityInteract, ref int priorityPlace)
		{
			int activeBlockValue = player.ComponentMiner.ActiveBlockValue;
			int activeBlockIndex = Terrain.ExtractContents(activeBlockValue);
			Block activeBlock = BlocksManager.Blocks[activeBlockIndex];

			if (activeBlock is RemoteControlBlock)
			{
				var greenNightSky = player.Project.FindSubsystem<SubsystemGreenNightSky>(true);
				if (greenNightSky != null)
				{
					// Pasar el jugador al diálogo
					GreenNightToggleDialog dialog = new GreenNightToggleDialog(greenNightSky, player);
					player.ComponentGui.ModalPanelWidget = dialog;
					AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
					playerOperated = true;
				}
			}
		}
	}
}