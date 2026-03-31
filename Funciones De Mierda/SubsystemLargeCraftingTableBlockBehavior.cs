using System;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemLargeCraftingTableBlockBehavior : SubsystemEntityBlockBehavior
	{
		public override int[] HandledBlocks => new int[] { LargeCraftingTableBlock.Index };

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			// Buscar la plantilla de entidad para la mesa grande (debe existir en la base de datos)
			m_databaseObject = base.Project.GameDatabase.Database.FindDatabaseObject("LargeCraftingTable", base.Project.GameDatabase.EntityTemplateType, true);
		}

		public override bool InteractBlockEntity(ComponentBlockEntity blockEntity, ComponentMiner componentMiner)
		{
			if (blockEntity != null && componentMiner.ComponentPlayer != null)
			{
				ComponentLargeCraftingTable craftingTable = blockEntity.Entity.FindComponent<ComponentLargeCraftingTable>(true);
				if (craftingTable != null)
				{
					componentMiner.ComponentPlayer.ComponentGui.ModalPanelWidget = new LargeCraftingTableWidget(componentMiner.Inventory, craftingTable);
					AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
					return true;
				}
			}
			return false;
		}
	}
}
