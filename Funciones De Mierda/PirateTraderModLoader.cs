using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class PirateTraderModLoader : ModLoader
	{
		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("OnPlayerInputInteract", this);
		}

		public override void OnPlayerInputInteract(ComponentPlayer player, ref bool playerOperated, ref double timeIntervalLastActionTime, ref int priorityUse, ref int priorityInteract, ref int priorityPlace)
		{
			int activeBlockValue = player.ComponentMiner.ActiveBlockValue;
			int activeBlockIndex = Terrain.ExtractContents(activeBlockValue);
			int mediumFirstAidKitIndex = BlocksManager.GetBlockIndex<MediumFirstAidKitBlock>();
			int largeFirstAidKitIndex = BlocksManager.GetBlockIndex<LargeFirstAidKitBlock>();
			int antidoteBucketIndex = BlocksManager.GetBlockIndex<AntidoteBucketBlock>();
			if (activeBlockIndex == mediumFirstAidKitIndex || activeBlockIndex == largeFirstAidKitIndex || activeBlockIndex == antidoteBucketIndex) return;
			var input = player.ComponentInput.PlayerInput;
			var raycast = player.ComponentMiner.Raycast<BodyRaycastResult>(input.Interact.Value, RaycastMode.Interaction);
			if (raycast.HasValue)
			{
				float distance = Vector3.Distance(player.ComponentBody.Position, raycast.Value.ComponentBody.Position);
				if (distance <= 5f)
				{
					var trader = raycast.Value.ComponentBody.Entity.FindComponent<ComponentPirateTrader>();
					if (trader != null)
					{
						priorityInteract = Math.Max(priorityInteract, 2000);
						if (!(player.ComponentGui.ModalPanelWidget is PirateTraderWidget))
						{
							var playerInventory = player.Entity.FindComponent<ComponentInventory>(true);
							if (playerInventory != null)
							{
								var widget = new PirateTraderWidget(trader, playerInventory, raycast.Value.ComponentBody.Entity);
								widget.Size = new Vector2(614f, 382f);
								widget.HorizontalAlignment = WidgetAlignment.Center;
								widget.VerticalAlignment = WidgetAlignment.Center;
								player.ComponentGui.ModalPanelWidget = widget;
								playerOperated = true;
								player.m_isAimBlocked = true;
							}
						}
					}
				}
			}
		}
	}
}
