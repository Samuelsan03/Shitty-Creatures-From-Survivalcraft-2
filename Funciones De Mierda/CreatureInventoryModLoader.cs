using System;
using Game;
using GameEntitySystem;
using Engine;
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
			PlayerInput input = player.ComponentInput.PlayerInput;
			BodyRaycastResult? bodyRaycast =
				player.ComponentMiner.Raycast<BodyRaycastResult>(input.Interact.Value, RaycastMode.Interaction);

			bool handled = false;
			if (bodyRaycast.HasValue)
			{
				priorityInteract = Math.Max(priorityInteract, 2000);
				handled = HandleCreatureInteraction(player, bodyRaycast.Value);
				priorityInteract = -2;
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
			else
			{
				playerOperated = false;
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
