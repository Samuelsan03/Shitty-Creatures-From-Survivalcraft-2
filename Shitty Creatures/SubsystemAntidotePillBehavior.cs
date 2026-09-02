using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemAntidotePillBehavior : SubsystemBlockBehavior
	{
		private SubsystemAudio m_subsystemAudio;

		public override int[] HandledBlocks
		{
			get
			{
				int blockIndex = BlocksManager.GetBlockIndex("AntidotePillBlock");
				if (blockIndex < 0)
				{
					return Array.Empty<int>();
				}
				return new int[] { blockIndex };
			}
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			int antidoteIndex = BlocksManager.GetBlockIndex("AntidotePillBlock");
			if (antidoteIndex < 0 || Terrain.ExtractContents(componentMiner.ActiveBlockValue) != antidoteIndex)
			{
				return false;
			}

			ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
			if (componentPlayer == null)
			{
				return false;
			}

			SubsystemGameInfo subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			if (subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative)
			{
				return false;
			}

			bool hasFlu = componentPlayer.ComponentFlu != null && componentPlayer.ComponentFlu.HasFlu;
			bool hasSickness = componentPlayer.ComponentSickness != null && componentPlayer.ComponentSickness.IsSick;

			if (!hasFlu && !hasSickness)
			{
				return false;
			}

			if (hasFlu)
			{
				componentPlayer.ComponentFlu.m_fluDuration = 0f;
				componentPlayer.ComponentGui.DisplaySmallMessage("¡Gripa curada!", Color.White, true, false);
			}

			if (hasSickness)
			{
				componentPlayer.ComponentSickness.m_sicknessDuration = 0f;
				componentPlayer.ComponentSickness.m_greenoutDuration = 0f;
				componentPlayer.ComponentSickness.m_greenoutFactor = 0f;
				if (componentPlayer.ComponentSickness.m_pukeParticleSystem != null)
				{
					componentPlayer.ComponentSickness.m_pukeParticleSystem = null;
				}
				componentPlayer.ComponentGui.DisplaySmallMessage("¡Náuseas curadas!", Color.White, true, false);
			}

			componentMiner.RemoveActiveTool(1);

			if (m_subsystemAudio == null)
			{
				m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			}
			m_subsystemAudio.PlaySound("Audio/Items/consumo antidoto", 1f, 0f, componentPlayer.ComponentBody.Position, 2f, false);

			return true;
		}
	}
}
