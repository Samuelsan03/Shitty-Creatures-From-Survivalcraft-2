using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class CreatureInventoryModLoader : ModLoader
	{
		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("OnPlayerInputInteract", this);
		}

		public override void OnPlayerInputInteract(
			ComponentPlayer player,
			ref bool playerOperated,
			ref double timeIntervalLastActionTime,
			ref int priorityUse,
			ref int priorityInteract,
			ref int priorityPlace)
		{
			if (playerOperated) return;

			var input = player.ComponentInput.PlayerInput;
			if (input.Interact == null) return;

			var bodyRaycast = player.ComponentMiner.Raycast<BodyRaycastResult>(
				input.Interact.Value,
				RaycastMode.Interaction,
				raycastTerrain: false,
				raycastBodies: true,
				raycastMovingBlocks: false,
				reach: null
			);

			if (bodyRaycast.HasValue)
			{
				var targetBody = bodyRaycast.Value.ComponentBody;
				if (targetBody != null && targetBody.Entity != player.Entity)
				{
					var creatureInv = targetBody.Entity.FindComponent<ComponentCreatureInventory>();
					if (creatureInv != null)
					{
						creatureInv.OpenInventory(player);
						playerOperated = true;
					}
				}
			}
		}
	}
}
