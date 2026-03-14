using System;
using Engine;
using GameEntitySystem;

namespace Game
{
	public class HireableNPCModLoader : ModLoader
	{
		public override void __ModInitialize()
		{
			// Prioridad alta para ejecutarse antes que otros mods
			ModsManager.RegisterHook("OnPlayerInputInteract", this, 100);
		}

		public override void OnPlayerInputInteract(ComponentPlayer player, ref bool playerOperated,
			ref double timeIntervalLastActionTime, ref int priorityUse, ref int priorityInteract, ref int priorityPlace)
		{
			if (playerOperated) return;

			var input = player.ComponentInput.PlayerInput;
			if (input.Interact == null) return;

			// Evitar abrir si se sostienen objetos curativos
			int activeSlot = player.ComponentMiner.Inventory.ActiveSlotIndex;
			int activeValue = player.ComponentMiner.Inventory.GetSlotValue(activeSlot);
			int activeBlockIndex = Terrain.ExtractContents(activeValue);
			if (IsHealingItem(activeBlockIndex)) return;

			var result = player.ComponentMiner.Raycast<BodyRaycastResult>(input.Interact.Value,
				RaycastMode.Interaction, false, true, false, null);

			if (result.HasValue)
			{
				var target = result.Value.ComponentBody?.Entity;
				if (target != null && target != player.Entity)
				{
					var hireable = target.FindComponent<ComponentHireableNPC>();
					if (hireable != null)
					{
						// Si ya está contratado, no hacer nada (dejar que otros mods lo manejen)
						if (hireable.IsHired)
							return;

						var health = target.FindComponent<ComponentHealth>();
						if (health == null || health.Health <= 0f)
							return;

						// Abrir widget de contratación
						player.ComponentGui.ModalPanelWidget = new HireWidget(player, hireable);
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
