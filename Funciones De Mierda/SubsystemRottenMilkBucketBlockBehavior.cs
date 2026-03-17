using System;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemRottenMilkBucketBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => new int[] { RottenMilkBucketBlock.Index };
		public SubsystemAudio m_subsystemAudio;
		public SubsystemPickables m_subsystemPickables;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			ComponentPlayer componentPlayer = componentMiner?.ComponentPlayer;
			if (componentPlayer == null) return false;
			SubsystemGameInfo gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			if (gameInfo.WorldSettings.GameMode == GameMode.Creative) return false;
			if (!ShittyCreaturesSettingsManager.ThirstEnabled) return false;
			ComponentSickness sickness = componentPlayer.Entity.FindComponent<ComponentSickness>();
			if (sickness != null)
			{
				sickness.m_sicknessDuration = 600f;
			}
			ComponentThirst thirst = componentPlayer.Entity.FindComponent<ComponentThirst>();
			if (thirst != null)
			{
				thirst.Water = Math.Clamp(thirst.Water - 0.1f, 0f, 1f);
				string rottenMessage = LanguageControl.Get("ComponentThirst", "DrankRotten");
				componentPlayer.ComponentGui.DisplaySmallMessage(rottenMessage, new Color(150, 150, 150), true, false);
			}
			m_subsystemAudio.PlaySound("Audio/UI/drinking", 1f, 0f, componentPlayer.ComponentBody.Position, 2f, 0f);
			int emptyBucketValue = Terrain.MakeBlockValue(EmptyBucketBlock.Index);
			componentMiner.RemoveActiveTool(1);
			IInventory inventory = componentMiner.Inventory;
			if (inventory != null)
			{
				int emptySlot = -1;
				for (int i = 0; i < inventory.SlotsCount; i++)
				{
					if (inventory.GetSlotCount(i) == 0)
					{
						emptySlot = i;
						break;
					}
				}
				if (emptySlot >= 0)
				{
					inventory.AddSlotItems(emptySlot, emptyBucketValue, 1);
				}
				else
				{
					Vector3 position = componentPlayer.ComponentBody.Position + new Vector3(0f, 1f, 0f) + 0.5f * componentPlayer.ComponentBody.Matrix.Forward;
					m_subsystemPickables.AddPickable(emptyBucketValue, 1, position, null, null);
				}
			}
			return true;
		}
	}
}
