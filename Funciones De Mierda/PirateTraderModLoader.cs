using System;
using Engine;
using GameEntitySystem;

namespace Game
{
	public class PirateTraderModLoader : ModLoader
	{
		public override void __ModInitialize() => ModsManager.RegisterHook("OnPlayerInputInteract", this);

		public override void OnPlayerInputInteract(ComponentPlayer player, ref bool playerOperated,
			ref double timeIntervalLastActionTime, ref int priorityUse, ref int priorityInteract, ref int priorityPlace)
		{
			if (playerOperated) return;
			var input = player.ComponentInput.PlayerInput;
			if (input.Interact == null) return;

			// Obtener el objeto activo del jugador
			int activeSlot = player.ComponentMiner.Inventory.ActiveSlotIndex;
			int activeValue = player.ComponentMiner.Inventory.GetSlotValue(activeSlot);
			int activeBlockIndex = Terrain.ExtractContents(activeValue);

			// Lista de bloques de curación que NO deben abrir el panel del comerciante
			if (IsHealingItem(activeBlockIndex))
			{
				// No interceptamos la interacción, dejamos que el objeto se use normalmente
				return;
			}

			var result = player.ComponentMiner.Raycast<BodyRaycastResult>(input.Interact.Value,
				RaycastMode.Interaction, false, true, false, null);
			if (result.HasValue)
			{
				var target = result.Value.ComponentBody?.Entity;
				if (target != null && target != player.Entity)
				{
					var trader = target.FindComponent<ComponentPirateTrader>();
					if (trader != null)
					{
						// Verificar que el NPC esté vivo antes de abrir el panel
						var health = target.FindComponent<ComponentHealth>();
						if (health == null || health.Health <= 0f)
							return; // No abrir el panel, el NPC está muerto

						player.ComponentGui.ModalPanelWidget = new PirateTradeWidget(
							player.ComponentMiner.Inventory, trader, player);
						playerOperated = true;
					}
				}
			}
		}

		private bool IsHealingItem(int blockIndex)
		{
			// Obtén los índices de los bloques de curación
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
