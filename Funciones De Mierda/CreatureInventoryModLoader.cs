using System;
using Engine;
using Game;
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
			int activeBlockValue = player.ComponentMiner.ActiveBlockValue;
			int activeBlockIndex = Terrain.ExtractContents(activeBlockValue);
			int mediumFirstAidKitIndex = BlocksManager.GetBlockIndex<MediumFirstAidKitBlock>();
			int largeFirstAidKitIndex = BlocksManager.GetBlockIndex<LargeFirstAidKitBlock>();
			int antidoteBucketIndex = BlocksManager.GetBlockIndex<AntidoteBucketBlock>();

			if (activeBlockIndex == mediumFirstAidKitIndex ||
				activeBlockIndex == largeFirstAidKitIndex ||
				activeBlockIndex == antidoteBucketIndex)
			{
				return;
			}

			PlayerInput input = player.ComponentInput.PlayerInput;
			BodyRaycastResult? bodyRaycast =
				player.ComponentMiner.Raycast<BodyRaycastResult>(input.Interact.Value, RaycastMode.Interaction);

			bool handled = false;
			if (bodyRaycast.HasValue)
			{
				if (CheckInteractionDistance(player, bodyRaycast.Value))
				{
					priorityInteract = Math.Max(priorityInteract, 2000);
					handled = HandleCreatureInteraction(player, bodyRaycast.Value);
				}
			}

			if (handled)
			{
				var subsystemTerrain = player.m_subsystemTerrain;
				if (subsystemTerrain != null && subsystemTerrain.TerrainUpdater != null)
				{
					subsystemTerrain.TerrainUpdater.RequestSynchronousUpdate();
				}
				playerOperated = true;
				player.m_isAimBlocked = true;
			}
		}

		private bool CheckInteractionDistance(ComponentPlayer player, BodyRaycastResult raycast)
		{
			try
			{
				Vector3 playerPosition = player.ComponentBody.Position;
				Vector3 creaturePosition = raycast.ComponentBody.Position;
				float distance = Vector3.Distance(playerPosition, creaturePosition);
				const float MAX_INTERACTION_DISTANCE = 5f;
				return distance <= MAX_INTERACTION_DISTANCE;
			}
			catch
			{
				return true;
			}
		}

		private bool HandleCreatureInteraction(ComponentPlayer player, BodyRaycastResult raycast)
		{
			try
			{
				var health = raycast.ComponentBody.Entity.FindComponent<ComponentHealth>();
				if (health == null || health.Health <= 0f)
					return false;

				var creatureInventory = raycast.ComponentBody.Entity.FindComponent<ComponentCreatureInventory>();
				if (creatureInventory == null)
					return false;

				Entity creatureEntity = raycast.ComponentBody.Entity;
				ComponentInventory playerInventory = player.Entity.FindComponent<ComponentInventory>(true);
				if (playerInventory == null)
					return false;

				OpenCreatureInventoryForPlayer(player, creatureInventory, playerInventory, creatureEntity);

				try
				{
					AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				}
				catch { }

				player.ComponentMiner.Poke(false);
				return true;
			}
			catch (Exception ex)
			{
				return false;
			}
		}

		private void OpenCreatureInventoryForPlayer(ComponentPlayer player, IInventory creatureInventory,
			IInventory playerInventory, Entity creatureEntity)
		{
			try
			{
				CreatureInventoryWidget widget = new CreatureInventoryWidget(creatureInventory, playerInventory, creatureEntity);
				ComponentGui gui = player.ComponentGui;
				if (gui != null)
				{
					widget.Size = new Vector2(614f, 382f);
					widget.HorizontalAlignment = WidgetAlignment.Center;
					widget.VerticalAlignment = WidgetAlignment.Center;
					gui.ModalPanelWidget = widget;
				}
			}
			catch (Exception ex) { }
		}
	}
}
