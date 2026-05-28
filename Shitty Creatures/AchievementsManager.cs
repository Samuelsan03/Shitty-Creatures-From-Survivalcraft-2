using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public static class AchievementsManager
	{
		private static SubsystemAchievements s_subsystemAchievements;
		private static Project s_currentProject;

		public static void Initialize(Project project)
		{
			s_currentProject = project;
			s_subsystemAchievements = project.FindSubsystem<SubsystemAchievements>(true);
			if (s_subsystemAchievements == null)
			{
				Log.Warning("[AchievementsManager] No se encontró SubsystemAchievements. Los logros no se guardarán.");
			}

			var banditInvasion = project.FindSubsystem<SubsystemBanditInvasion>(true);
			if (banditInvasion != null)
			{
				banditInvasion.InvasionCompleted -= OnInvasionCompleted;
				banditInvasion.InvasionCompleted += OnInvasionCompleted;
			}
		}

		public static void Shutdown()
		{
			if (s_currentProject != null)
			{

				var banditInvasion = s_currentProject.FindSubsystem<SubsystemBanditInvasion>(true);
				if (banditInvasion != null)
					banditInvasion.InvasionCompleted -= OnInvasionCompleted;
			}
			s_subsystemAchievements = null;
			s_currentProject = null;
		}

		public static void OnCreatureDied(ComponentHealth health, Injury injury)
		{
			if (s_subsystemAchievements == null) return;

			Entity deadEntity = health.Entity;
			if (deadEntity == null) return;
			ComponentCreature creature = deadEntity.FindComponent<ComponentCreature>();
			if (creature == null) return;

			string templateName = deadEntity.ValuesDictionary?.DatabaseObject?.Name;

			ComponentPlayer killer = null;
			if (injury != null && injury.Attacker != null)
			{
				killer = injury.Attacker.Entity.FindComponent<ComponentPlayer>();
			}
			if (killer == null) return;

			// Logro 1: Tank (normal o congelado)
			if (templateName == "Tank1" || templateName == "Tank2" || templateName == "Tank3" || templateName == "FrozenTank")
			{
				UnlockAchievement(killer, 1, "KillTank", LanguageControl.Get(AchievementsWidget.fName, 6));
			}

			// Logro 2: Infectado
			if (killer != null && (
	templateName == "InfectedNormal1" ||
	templateName == "InfectedNormal2" ||
	templateName == "InfectedFast1" ||
	templateName == "InfectedFast2" ||
	templateName == "InfectedMuscle1" ||
	templateName == "InfectedMuscle2"))
			{
				UnlockAchievement(killer, 2, "KillInfected", LanguageControl.Get(AchievementsWidget.fName, 8));
			}

			// Logro 3: Bandido
			if (deadEntity.FindComponent<ComponentBanditHerdBehavior>() != null)
			{
				UnlockAchievement(killer, 3, "KillBandit", LanguageControl.Get(AchievementsWidget.fName, 10));
			}

			// Logro 4: Fantasma
			if (templateName == "GhostNormal" || templateName == "GhostFast" || templateName == "PoisonousGhost" ||
				templateName == "GhostBoomer1" || templateName == "GhostBoomer2" || templateName == "GhostBoomer3" ||
				templateName == "FrozenGhost")
			{
				UnlockAchievement(killer, 4, "KillGhost", LanguageControl.Get(AchievementsWidget.fName, 12));
			}

			// Logro 5: Tank Fantasma
			if (templateName == "TankGhost1" || templateName == "TankGhost2" || templateName == "TankGhost3" ||
				templateName == "FrozenTankGhost")
			{
				UnlockAchievement(killer, 5, "KillGhostTank", LanguageControl.Get(AchievementsWidget.fName, 14));
			}
		}

		private static void OnInvasionCompleted()
		{
			if (s_subsystemAchievements == null) return;

			var players = s_currentProject?.FindSubsystem<SubsystemPlayers>(true);
			if (players == null) return;

			foreach (var player in players.ComponentPlayers)
				UnlockAchievement(player, 8, "DrugWarSurvived", LanguageControl.Get(AchievementsWidget.fName, 20));
		}

		private static void UnlockAchievement(ComponentPlayer player, int achievementNumber, string achievementId, string displayName)
		{
			if (s_subsystemAchievements == null) return;
			if (s_subsystemAchievements.IsAchievementUnlocked(achievementNumber)) return;

			s_subsystemAchievements.UnlockAchievement(achievementNumber, achievementId);

			player.ComponentGui.DisplayLargeMessage(
				LanguageControl.Get("AchievementsMessages", 0),
				displayName, 4f, 0f);

			var audio = player.Project.FindSubsystem<SubsystemAudio>(true);
			if (audio != null)
			{
				audio.PlaySound("Audio/pump it up", 1f, 0f, player.ComponentBody.Position, 10f, false);
			}
		}

		public static bool IsAchievementUnlocked(ComponentPlayer player, int achievementNumber)
		{
			return s_subsystemAchievements != null && s_subsystemAchievements.IsAchievementUnlocked(achievementNumber);
		}

		public static bool IsRewardClaimed(ComponentPlayer player, int achievementNumber)
		{
			return s_subsystemAchievements != null && s_subsystemAchievements.IsRewardClaimed(achievementNumber);
		}

		public static bool ClaimAchievementReward(ComponentPlayer player, int achievementNumber, int rewardAmount)
		{
			if (s_subsystemAchievements == null) return false;
			if (!s_subsystemAchievements.IsAchievementUnlocked(achievementNumber)) return false;
			if (s_subsystemAchievements.IsRewardClaimed(achievementNumber)) return false;

			IInventory inventory = player.ComponentMiner.Inventory;
			if (inventory == null) return false;

			Block nuclearCoinBlock = BlocksManager.GetBlock<NuclearCoinBlock>(false, false);
			if (nuclearCoinBlock == null) return false;

			int coinValue = nuclearCoinBlock.BlockIndex;
			int remaining = rewardAmount;

			while (remaining > 0)
			{
				int slot = ComponentInventoryBase.FindAcquireSlotForItem(inventory, coinValue);
				if (slot < 0)
				{
					player.ComponentGui.DisplaySmallMessage(
						LanguageControl.Get("AchievementsMessages", 2),
						Color.Red, false, true);
					return false;
				}
				int capacity = inventory.GetSlotCapacity(slot, coinValue);
				int existing = inventory.GetSlotCount(slot);
				int canAdd = Math.Min(capacity - existing, remaining);
				if (canAdd <= 0) continue;

				inventory.AddSlotItems(slot, coinValue, canAdd);
				remaining -= canAdd;
			}

			s_subsystemAchievements.ClaimReward(achievementNumber);
			return true;
		}

		public static void UnlockAchievementStatic(ComponentPlayer player, int achievementNumber, string achievementId, string displayName)
		{
			if (s_subsystemAchievements == null) return;
			if (s_subsystemAchievements.IsAchievementUnlocked(achievementNumber)) return;

			s_subsystemAchievements.UnlockAchievement(achievementNumber, achievementId);

			player.ComponentGui.DisplayLargeMessage(
				LanguageControl.Get("AchievementsMessages", 0),
				displayName, 4f, 0f);

			var audio = player.Project.FindSubsystem<SubsystemAudio>(true);
			if (audio != null)
			{
				audio.PlaySound("Audio/pump it up", 1f, 0f, player.ComponentBody.Position, 10f, false);
			}
		}
	}
}
