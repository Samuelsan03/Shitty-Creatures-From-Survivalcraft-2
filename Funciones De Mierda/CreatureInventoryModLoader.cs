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
			// Prioridad baja para ejecutarse después de HireableNPCModLoader
			ModsManager.RegisterHook("OnPlayerInputInteract", this, 0);
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

			if (!bodyRaycast.HasValue) return;

			var targetBody = bodyRaycast.Value.ComponentBody;
			if (targetBody == null || targetBody.Entity == player.Entity) return;

			// 1. No abrir inventario si el objetivo está muerto
			var targetHealth = targetBody.Entity.FindComponent<ComponentHealth>();
			if (targetHealth != null && (targetHealth.Health <= 0f || targetHealth.DeathTime.HasValue))
				return;

			// 2. No abrir inventario si el jugador sostiene un objeto curativo
			int activeBlockValue = player.ComponentMiner.ActiveBlockValue;
			int activeBlockIndex = Terrain.ExtractContents(activeBlockValue);
			bool isHealingItem = activeBlockIndex == BlocksManager.GetBlockIndex<AntidoteBucketBlock>() ||
								 activeBlockIndex == BlocksManager.GetBlockIndex<TeaAntifluBucketBlock>() ||
								 activeBlockIndex == BlocksManager.GetBlockIndex<MediumFirstAidKitBlock>() ||
								 activeBlockIndex == BlocksManager.GetBlockIndex<LargeFirstAidKitBlock>();
			if (isHealingItem)
				return;

			var creatureInv = targetBody.Entity.FindComponent<ComponentCreatureInventory>();
			if (creatureInv != null)
			{
				// Verificar si tiene componente hireable y si no está contratado
				var hireable = targetBody.Entity.FindComponent<ComponentHireableNPC>();
				if (hireable != null && !hireable.IsHired)
				{
					// La criatura es contratable pero aún no ha sido contratada.
					// No abrimos inventario; dejamos que el mod loader de contratación lo maneje.
					return;
				}

				// Si llegamos aquí, o no tiene hireable, o ya está contratado -> abrir inventario
				creatureInv.OpenInventory(player);
				playerOperated = true;
			}
		}
	}
}
