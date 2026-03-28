using System;
using Engine;
using GameEntitySystem;

namespace Game
{
	public class GunsTraderModLoader : ModLoader
	{
		public override void __ModInitialize() => ModsManager.RegisterHook("OnPlayerInputInteract", this);

		public override void OnPlayerInputInteract(ComponentPlayer player, ref bool playerOperated,
			ref double timeIntervalLastActionTime, ref int priorityUse, ref int priorityInteract, ref int priorityPlace)
		{
			if (playerOperated) return;
			var input = player.ComponentInput.PlayerInput;
			if (input.Interact == null) return;

			int activeSlot = player.ComponentMiner.Inventory.ActiveSlotIndex;
			int activeValue = player.ComponentMiner.Inventory.GetSlotValue(activeSlot);
			int activeBlockIndex = Terrain.ExtractContents(activeValue);

			if (IsHealingItem(activeBlockIndex))
			{
				return;
			}

			var result = player.ComponentMiner.Raycast<BodyRaycastResult>(input.Interact.Value,
				RaycastMode.Interaction, false, true, false, null);
			if (result.HasValue)
			{
				var target = result.Value.ComponentBody?.Entity;
				if (target != null && target != player.Entity)
				{
					var trader = target.FindComponent<ComponentTrader>();
					if (trader != null)
					{
						// Verificar que la entidad sea el comerciante de armas por su nombre en la base de datos
						string entityName = target.ValuesDictionary?.DatabaseObject?.Name;
						if (entityName != "FirearmsDealer")
							return;

						var health = target.FindComponent<ComponentHealth>();
						if (health == null || health.Health <= 0f)
							return;

						player.ComponentGui.ModalPanelWidget = new GunsTradeWidget(
							player.ComponentMiner.Inventory, trader, player);
						playerOperated = true;
					}
				}
			}
		}

		private bool IsHealingItem(int blockIndex)
		{
			int antidoteIndex = BlocksManager.GetBlockIndex<AntidoteBucketBlock>(false, false);
			int teaIndex = BlocksManager.GetBlockIndex<TeaAntifluBucketBlock>(false, false);
			int largeKitIndex = BlocksManager.GetBlockIndex<LargeFirstAidKitBlock>(false, false);
			int mediumKitIndex = BlocksManager.GetBlockIndex<MediumFirstAidKitBlock>(false, false);

			return blockIndex == antidoteIndex ||
				   blockIndex == teaIndex ||
				   blockIndex == largeKitIndex ||
				   blockIndex == mediumKitIndex;
		}
	}
}
