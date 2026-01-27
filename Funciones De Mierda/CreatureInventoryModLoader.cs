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
				// VERIFICAR SI EL JUGADOR TIENE UN ITEM MÉDICO ACTIVO
				int activeBlockValue = player.ComponentMiner.ActiveBlockValue;
				int activeBlockIndex = Terrain.ExtractContents(activeBlockValue);
				int mediumFirstAidKitIndex = BlocksManager.GetBlockIndex<MediumFirstAidKitBlock>();
				int antidoteBucketIndex = BlocksManager.GetBlockIndex<AntidoteBucketBlock>();

				// Si el jugador tiene un item médico activo, NO abrir el inventario
				if (activeBlockIndex == mediumFirstAidKitIndex || activeBlockIndex == antidoteBucketIndex)
				{
					// Permitir que el item médico maneje la interacción
					return;
				}

				// SOLO incrementar la prioridad de interact, NO establecerla a -2
				priorityInteract = Math.Max(priorityInteract, 2000);
				handled = HandleCreatureInteraction(player, bodyRaycast.Value);

				// NO establecer priorityInteract = -2 aquí
				// Esto permitirá que otros comportamientos también se ejecuten
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
			// NO establecer playerOperated = false aquí
			// Dejar que otros hooks/manejadores se ejecuten
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
