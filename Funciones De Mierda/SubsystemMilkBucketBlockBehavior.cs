using System;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemMilkBucketBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => new int[] { MilkBucketBlock.Index };
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

			ComponentThirst thirst = componentPlayer.Entity.FindComponent<ComponentThirst>();

			if (ShittyCreaturesSettingsManager.ThirstEnabled)
			{
				if (thirst != null && !thirst.Drink(0.3f))
					return false; // Sed llena, no se puede beber
			}

			// Mostrar mensaje de satisfacción (usando la clave que corresponda, puedes cambiarla si existe una específica para leche)
			string message = LanguageControl.Get("ComponentThirst", "DrankDrinkableWater");
			componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.Blue, true, false);

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
