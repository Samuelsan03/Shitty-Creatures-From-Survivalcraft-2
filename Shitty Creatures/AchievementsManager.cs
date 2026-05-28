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
		private static SubsystemTime s_subsystemTime;
		private static SubsystemGameInfo s_subsystemGameInfo;
		private static SubsystemTimeOfDay s_subsystemTimeOfDay;
		private static double s_lastDayCheckGameTime = -1.0;
		private const double DAY_CHECK_INTERVAL = 5.0;

		// Evento para notificar cambios en contadores (para actualizar UI)
		public static event Action<ComponentPlayer, int, int> OnInfectedCounterChanged; // player, kills, targetNumber

		public static void Initialize(Project project)
		{
			s_currentProject = project;
			s_subsystemAchievements = project.FindSubsystem<SubsystemAchievements>(true);
			s_subsystemTime = project.FindSubsystem<SubsystemTime>(true);
			s_subsystemGameInfo = project.FindSubsystem<SubsystemGameInfo>(true);
			s_subsystemTimeOfDay = project.FindSubsystem<SubsystemTimeOfDay>(true);

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
			s_subsystemTime = null;
			s_subsystemGameInfo = null;
			s_subsystemTimeOfDay = null;
		}

		public static void UpdateDayAchievements()
		{
			if (s_subsystemAchievements == null) return;
			if (s_subsystemGameInfo == null || s_subsystemTimeOfDay == null) return;
			if (s_subsystemTime == null) return;

			double currentGameTime = s_subsystemTime.GameTime;
			if (s_lastDayCheckGameTime < 0)
			{
				s_lastDayCheckGameTime = currentGameTime;
				return;
			}
			if (currentGameTime - s_lastDayCheckGameTime < DAY_CHECK_INTERVAL)
				return;
			s_lastDayCheckGameTime = currentGameTime;

			double totalElapsed = s_subsystemGameInfo.TotalElapsedGameTime;
			double dayDuration = s_subsystemTimeOfDay.DayDuration;
			if (dayDuration <= 0) return;
			int currentDay = (int)Math.Floor(totalElapsed / dayDuration) + 1;

			var dayAchievements = new (int days, int number, string id, int titleKey)[]
			{
				(5,   9, "Survive5Days",   22),
				(10, 10, "Survive10Days",  24),
				(25, 11, "Survive25Days",  26),
				(75, 12, "Survive75Days",  28),
				(100,13, "Survive100Days", 30),
				(300,14, "Survive300Days", 32)
			};

			var players = s_currentProject?.FindSubsystem<SubsystemPlayers>(true);
			if (players == null) return;

			foreach (var da in dayAchievements)
			{
				if (currentDay >= da.days && !s_subsystemAchievements.IsAchievementUnlocked(da.number))
				{
					string title = LanguageControl.Get(AchievementsWidget.fName, da.titleKey);
					foreach (var player in players.ComponentPlayers)
						UnlockAchievement(player, da.number, da.id, title);
				}
			}
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

			// Tank
			if (templateName == "Tank1" || templateName == "Tank2" || templateName == "Tank3" || templateName == "FrozenTank")
			{
				UnlockAchievement(killer, 1, "KillTank", LanguageControl.Get(AchievementsWidget.fName, 6));
			}

			// Infectado normal (para el contador progresivo)
			bool isNormalInfected = (templateName == "InfectedNormal1" || templateName == "InfectedNormal2" ||
									 templateName == "InfectedFast1" || templateName == "InfectedFast2" ||
									 templateName == "InfectedMuscle1" || templateName == "InfectedMuscle2" || templateName == "InfectedFreezer" || templateName == "HumanoidSkeleton" || templateName == "PredatoryChameleon" || templateName == "InfectedFly3" || templateName == "InfectedBird" || templateName == "Boomer1" || templateName == "Boomer2" || templateName == "Boomer3" || templateName == "BoomerFrozen" || templateName == "InfectedBear" || templateName == "InfectedFly1" || templateName == "InfectedFly2");
			if (isNormalInfected)
			{
				// Logro individual "Mata un Infectado"
				UnlockAchievement(killer, 2, "KillInfected", LanguageControl.Get(AchievementsWidget.fName, 8));

				// Contador progresivo (acumulativo) usando el subsistema
				int playerIndex = killer.PlayerData.PlayerIndex;
				s_subsystemAchievements.AddInfectedKill(playerIndex);
				int kills = s_subsystemAchievements.GetInfectedKills(playerIndex);

				// Notificar cambios de contador para actualizar UI
				if (kills <= 100)
				{
					if (kills <= 10)
						OnInfectedCounterChanged?.Invoke(killer, kills, 10);
					else if (kills <= 50)
						OnInfectedCounterChanged?.Invoke(killer, kills, 50);
					else
						OnInfectedCounterChanged?.Invoke(killer, kills, 100);
				}

				// Desbloquear logros según el número de infectados matados
				if (kills >= 10 && !s_subsystemAchievements.IsAchievementUnlocked(16))
				{
					UnlockAchievement(killer, 16, "Kill10Infected", LanguageControl.Get(AchievementsWidget.fName, 36));
				}
				if (kills >= 50 && !s_subsystemAchievements.IsAchievementUnlocked(17))
				{
					UnlockAchievement(killer, 17, "Kill50Infected", LanguageControl.Get(AchievementsWidget.fName, 38));
				}
				if (kills >= 100 && !s_subsystemAchievements.IsAchievementUnlocked(18))
				{
					UnlockAchievement(killer, 18, "Kill100Infected", LanguageControl.Get(AchievementsWidget.fName, 40));
				}
			}

			// Bandido
			if (deadEntity.FindComponent<ComponentBanditHerdBehavior>() != null)
			{
				UnlockAchievement(killer, 3, "KillBandit", LanguageControl.Get(AchievementsWidget.fName, 10));
			}

			// Fantasma
			if (templateName == "GhostNormal" || templateName == "GhostFast" || templateName == "PoisonousGhost" ||
				templateName == "GhostBoomer1" || templateName == "GhostBoomer2" || templateName == "GhostBoomer3" ||
				templateName == "FrozenGhost")
			{
				UnlockAchievement(killer, 4, "KillGhost", LanguageControl.Get(AchievementsWidget.fName, 12));
			}

			// Tank Fantasma
			if (templateName == "TankGhost1" || templateName == "TankGhost2" || templateName == "TankGhost3" ||
				templateName == "FrozenTankGhost")
			{
				UnlockAchievement(killer, 5, "KillGhostTank", LanguageControl.Get(AchievementsWidget.fName, 14));
			}

			// Jefe
			if (templateName == "MachineGunInfected" || templateName == "FlyingInfectedBoss")
			{
				UnlockAchievement(killer, 15, "KillBoss", LanguageControl.Get(AchievementsWidget.fName, 34));
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

		public static int GetInfectedKills(ComponentPlayer player)
		{
			if (player == null || s_subsystemAchievements == null) return 0;
			return s_subsystemAchievements.GetInfectedKills(player.PlayerData.PlayerIndex);
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
