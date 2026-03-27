using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class RemoteControlModLoader : ModLoader
	{
		private static double s_lastProhibitionMessageTime = -10.0;
		private static bool s_isMatchingRemoteControlRecipe = false; // Prevenir recursión

		public override void __ModInitialize()
		{
			Type remoteControlType = typeof(RemoteControlBlock);
			bool blockExists = false;

			foreach (var block in BlocksManager.Blocks)
			{
				if (block != null && block.GetType() == remoteControlType)
				{
					blockExists = true;
					break;
				}
			}

			if (!blockExists)
			{
				int freeIndex = -1;
				for (int i = 300; i < 1024; i++)
				{
					if (BlocksManager.Blocks[i] == null || BlocksManager.Blocks[i] is AirBlock)
					{
						freeIndex = i;
						break;
					}
				}

				if (freeIndex >= 0)
				{
					RemoteControlBlock block = new RemoteControlBlock();
					BlocksManager.m_blocks[freeIndex] = block;
					block.BlockIndex = freeIndex;
					BlocksManager.BlockNameToIndex["RemoteControlBlock"] = freeIndex;
					BlocksManager.BlockTypeToIndex[remoteControlType] = freeIndex;
				}
			}

			ModsManager.RegisterHook("OnPlayerInputInteract", this);
			ModsManager.RegisterHook("MatchRecipe", this);
		}

		public override void OnPlayerInputInteract(ComponentPlayer player, ref bool playerOperated, ref double timeIntervalLastActionTime, ref int priorityUse, ref int priorityInteract, ref int priorityPlace)
		{
			int activeBlockValue = player.ComponentMiner.ActiveBlockValue;
			int activeBlockIndex = Terrain.ExtractContents(activeBlockValue);
			Block activeBlock = BlocksManager.Blocks[activeBlockIndex];

			if (activeBlock is RemoteControlBlock)
			{
				var greenNightSky = player.Project.FindSubsystem<SubsystemGreenNightSky>(true);
				if (greenNightSky != null)
				{
					GreenNightToggleDialog dialog = new GreenNightToggleDialog(greenNightSky, player);
					player.ComponentGui.ModalPanelWidget = dialog;
					AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
					playerOperated = true;
				}
			}
		}

		public override bool MatchRecipe(string[] requiredIngredients, string[] actualIngredients, out bool skipVanilla)
		{
			skipVanilla = false;

			// Primero, verificar si la receta que se está evaluando es la del control remoto
			if (IsRemoteControlRecipe(requiredIngredients))
			{
				// Evitar recursión si llamamos a MatchRemoteControlPattern
				if (s_isMatchingRemoteControlRecipe) return false;

				// Ahora comprobar si los ingredientes del jugador coinciden con la receta
				s_isMatchingRemoteControlRecipe = true;
				bool playerMatches = MatchRemoteControlPattern(actualIngredients);
				s_isMatchingRemoteControlRecipe = false;

				if (playerMatches)
				{
					var zombiesSpawn = SubsystemZombiesSpawn.Instance;
					if (zombiesSpawn == null || !zombiesSpawn.IsAllWavesCompleted)
					{
						skipVanilla = true; // Bloquear la receta

						double now = Time.RealTime;
						if (now - s_lastProhibitionMessageTime > 1.0)
						{
							s_lastProhibitionMessageTime = now;
							string smallMessage = LanguageControl.Get("RemoteControlAchievement", "CraftingLocked");
							if (string.IsNullOrEmpty(smallMessage))
								smallMessage = "You must survive all waves first!";

							var playersSubsystem = zombiesSpawn?.Project.FindSubsystem<SubsystemPlayers>(true);
							if (playersSubsystem != null)
							{
								foreach (var player in playersSubsystem.ComponentPlayers)
								{
									player.ComponentGui.DisplaySmallMessage(smallMessage, Color.White, false, true);
								}
							}
						}
						return false; // Receta no válida
					}
					// Si está desbloqueado, no intervenimos (el juego encontrará la receta normalmente)
				}
			}
			return false; // Para cualquier otra receta, no intervenimos
		}

		/// <summary>
		/// Comprueba si los ingredientes del jugador (actualIngredients) pueden formar el patrón del control remoto
		/// después de aplicar rotaciones y reflexiones, utilizando la misma lógica que el juego.
		/// </summary>
		private bool MatchRemoteControlPattern(string[] actualIngredients)
		{
			if (actualIngredients == null || actualIngredients.Length != 9)
				return false;

			// Patrón esperado del control remoto (sin transformaciones)
			string[] remotePattern = new string[9];
			remotePattern[0] = null;
			remotePattern[1] = "semiconductorblock";
			remotePattern[2] = null;
			remotePattern[3] = "wire";
			remotePattern[4] = "battery";
			remotePattern[5] = "wire";
			remotePattern[6] = null;
			remotePattern[7] = "semiconductorblock";
			remotePattern[8] = null;

			// Replicar la lógica de CraftingRecipesManager.MatchRecipe
			// pero solo para nuestro patrón y los ingredientes reales.
			string[] transformed = new string[9];
			for (int i = 0; i < 2; i++)
			{
				bool flip = i != 0;
				for (int shiftX = -4; shiftX <= 2; shiftX++)
				{
					for (int shiftY = -4; shiftY <= 2; shiftY++)
					{
						if (TransformRecipe(transformed, remotePattern, shiftX, shiftY, flip))
						{
							bool match = true;
							for (int j = 0; j < 9; j++)
							{
								if (!CompareIngredients(transformed[j], actualIngredients[j]))
								{
									match = false;
									break;
								}
							}
							if (match)
								return true;
						}
					}
				}
			}
			return false;
		}

		// Copia de CraftingRecipesManager.TransformRecipe
		private bool TransformRecipe(string[] transformedIngredients, string[] ingredients, int shiftX, int shiftY, bool flip)
		{
			for (int i = 0; i < 9; i++)
				transformedIngredients[i] = null;

			for (int j = 0; j < 3; j++)
			{
				for (int k = 0; k < 3; k++)
				{
					int x = (flip ? (3 - k - 1) : k) + shiftX;
					int y = j + shiftY;
					string ingredient = ingredients[k + j * 3];
					if (x >= 0 && y >= 0 && x < 3 && y < 3)
						transformedIngredients[x + y * 3] = ingredient;
					else if (!string.IsNullOrEmpty(ingredient))
						return false;
				}
			}
			return true;
		}

		// Copia de CraftingRecipesManager.CompareIngredients
		private bool CompareIngredients(string required, string actual)
		{
			if (required == null)
				return actual == null;
			if (actual == null)
				return false;

			string requiredId;
			int? requiredData;
			CraftingRecipesManager.DecodeIngredient(required, out requiredId, out requiredData);
			string actualId;
			int? actualData;
			CraftingRecipesManager.DecodeIngredient(actual, out actualId, out actualData);
			if (actualData == null)
				throw new InvalidOperationException("Actual ingredient data not specified.");

			return requiredId == actualId && (requiredData == null || requiredData.Value == actualData.Value);
		}

		private bool IsRemoteControlRecipe(string[] ingredients)
		{
			if (ingredients == null || ingredients.Length != 9)
				return false;

			return ingredients[0] == null &&
				   ingredients[1] == "semiconductorblock" &&
				   ingredients[2] == null &&
				   ingredients[3] == "wire" &&
				   ingredients[4] == "battery" &&
				   ingredients[5] == "wire" &&
				   ingredients[6] == null &&
				   ingredients[7] == "semiconductorblock" &&
				   ingredients[8] == null;
		}
	}
}
