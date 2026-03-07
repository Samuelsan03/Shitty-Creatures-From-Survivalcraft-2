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
	}
}
