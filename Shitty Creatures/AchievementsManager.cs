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

		// Eventos para actualizar UI
		public static event Action<ComponentPlayer, int, int> OnInfectedCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnBossCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnTankCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnGhostCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnGhostTankCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnBanditCounterChanged;

		public static void Initialize(Project project)
		{
			s_currentProject = project;
			s_subsystemAchievements = project.FindSubsystem<SubsystemAchievements>(true);
			s_subsystemTime = project.FindSubsystem<SubsystemTime>(true);
			s_subsystemGameInfo = project.FindSubsystem<SubsystemGameInfo>(true);
			s_subsystemTimeOfDay = project.FindSubsystem<SubsystemTimeOfDay>(true);

			if (s_subsystemAchievements == null)
				Log.Warning("[AchievementsManager] No se encontró SubsystemAchievements.");

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
			string templateName = deadEntity.ValuesDictionary?.DatabaseObject?.Name;

			ComponentPlayer killer = null;
			if (injury != null && injury.Attacker != null)
				killer = injury.Attacker.Entity.FindComponent<ComponentPlayer>();
			if (killer == null) return;

			int playerIndex = killer.PlayerData.PlayerIndex;

			// ========== TANK NORMAL ==========
			bool isTank = (templateName == "Tank1" || templateName == "Tank2" || templateName == "Tank3" || templateName == "FrozenTank");
			if (isTank)
			{
				UnlockAchievement(killer, 1, "KillTank", LanguageControl.Get(AchievementsWidget.fName, 6));
				s_subsystemAchievements.AddTankKill(playerIndex);
				int kills = s_subsystemAchievements.GetTankKills(playerIndex);

				if (kills <= 100)
				{
					if (kills <= 10) OnTankCounterChanged?.Invoke(killer, kills, 10);
					else if (kills <= 50) OnTankCounterChanged?.Invoke(killer, kills, 50);
					else OnTankCounterChanged?.Invoke(killer, kills, 100);
				}

				if (kills >= 10 && !s_subsystemAchievements.IsAchievementUnlocked(22))
					UnlockAchievement(killer, 22, "Kill10Tanks", LanguageControl.Get(AchievementsWidget.fName, 48));
				if (kills >= 50 && !s_subsystemAchievements.IsAchievementUnlocked(23))
					UnlockAchievement(killer, 23, "Kill50Tanks", LanguageControl.Get(AchievementsWidget.fName, 50));
				if (kills >= 100 && !s_subsystemAchievements.IsAchievementUnlocked(24))
					UnlockAchievement(killer, 24, "Kill100Tanks", LanguageControl.Get(AchievementsWidget.fName, 52));
			}

			// ========== INFECTADO NORMAL ==========
			bool isNormalInfected = (templateName == "InfectedNormal1" || templateName == "InfectedNormal2" ||
									 templateName == "InfectedFast1" || templateName == "InfectedFast2" ||
									 templateName == "InfectedMuscle1" || templateName == "InfectedMuscle2" ||
									 templateName == "InfectedFreezer" || templateName == "HumanoidSkeleton" ||
									 templateName == "PredatoryChameleon" || templateName == "InfectedFly3" ||
									 templateName == "InfectedBird" || templateName == "Boomer1" ||
									 templateName == "Boomer2" || templateName == "Boomer3" ||
									 templateName == "BoomerFrozen" || templateName == "InfectedBear" ||
									 templateName == "InfectedFly1" || templateName == "InfectedFly2");
			if (isNormalInfected)
			{
				UnlockAchievement(killer, 2, "KillInfected", LanguageControl.Get(AchievementsWidget.fName, 8));
				s_subsystemAchievements.AddInfectedKill(playerIndex);
				int kills = s_subsystemAchievements.GetInfectedKills(playerIndex);

				if (kills <= 100)
				{
					if (kills <= 10) OnInfectedCounterChanged?.Invoke(killer, kills, 10);
					else if (kills <= 50) OnInfectedCounterChanged?.Invoke(killer, kills, 50);
					else OnInfectedCounterChanged?.Invoke(killer, kills, 100);
				}

				if (kills >= 10 && !s_subsystemAchievements.IsAchievementUnlocked(16))
					UnlockAchievement(killer, 16, "Kill10Infected", LanguageControl.Get(AchievementsWidget.fName, 36));
				if (kills >= 50 && !s_subsystemAchievements.IsAchievementUnlocked(17))
					UnlockAchievement(killer, 17, "Kill50Infected", LanguageControl.Get(AchievementsWidget.fName, 38));
				if (kills >= 100 && !s_subsystemAchievements.IsAchievementUnlocked(18))
					UnlockAchievement(killer, 18, "Kill100Infected", LanguageControl.Get(AchievementsWidget.fName, 40));
			}

			// ========== BANDIDO/NARCOTRAFICANTE ==========
			if (deadEntity.FindComponent<ComponentBanditHerdBehavior>() != null)
			{
				UnlockAchievement(killer, 3, "KillBandit", LanguageControl.Get(AchievementsWidget.fName, 10));
				s_subsystemAchievements.AddBanditKill(playerIndex);
				int kills = s_subsystemAchievements.GetBanditKills(playerIndex);

				if (kills <= 100)
				{
					if (kills <= 10) OnBanditCounterChanged?.Invoke(killer, kills, 10);
					else if (kills <= 50) OnBanditCounterChanged?.Invoke(killer, kills, 50);
					else OnBanditCounterChanged?.Invoke(killer, kills, 100);
				}

				if (kills >= 10 && !s_subsystemAchievements.IsAchievementUnlocked(31))
					UnlockAchievement(killer, 31, "Kill10Bandits", LanguageControl.Get(AchievementsWidget.fName, 66));
				if (kills >= 50 && !s_subsystemAchievements.IsAchievementUnlocked(32))
					UnlockAchievement(killer, 32, "Kill50Bandits", LanguageControl.Get(AchievementsWidget.fName, 68));
				if (kills >= 100 && !s_subsystemAchievements.IsAchievementUnlocked(33))
					UnlockAchievement(killer, 33, "Kill100Bandits", LanguageControl.Get(AchievementsWidget.fName, 70));
			}

			// ========== FANTASMA NORMAL ==========
			bool isGhost = (templateName == "GhostNormal" || templateName == "GhostFast" || templateName == "PoisonousGhost" ||
							templateName == "GhostBoomer1" || templateName == "GhostBoomer2" || templateName == "GhostBoomer3" ||
							templateName == "FrozenGhost");
			if (isGhost)
			{
				UnlockAchievement(killer, 4, "KillGhost", LanguageControl.Get(AchievementsWidget.fName, 12));
				s_subsystemAchievements.AddGhostKill(playerIndex);
				int kills = s_subsystemAchievements.GetGhostKills(playerIndex);

				if (kills <= 100)
				{
					if (kills <= 10) OnGhostCounterChanged?.Invoke(killer, kills, 10);
					else if (kills <= 50) OnGhostCounterChanged?.Invoke(killer, kills, 50);
					else OnGhostCounterChanged?.Invoke(killer, kills, 100);
				}

				if (kills >= 10 && !s_subsystemAchievements.IsAchievementUnlocked(25))
					UnlockAchievement(killer, 25, "Kill10Ghosts", LanguageControl.Get(AchievementsWidget.fName, 54));
				if (kills >= 50 && !s_subsystemAchievements.IsAchievementUnlocked(26))
					UnlockAchievement(killer, 26, "Kill50Ghosts", LanguageControl.Get(AchievementsWidget.fName, 56));
				if (kills >= 100 && !s_subsystemAchievements.IsAchievementUnlocked(27))
					UnlockAchievement(killer, 27, "Kill100Ghosts", LanguageControl.Get(AchievementsWidget.fName, 58));
			}

			// ========== TANK FANTASMA ==========
			bool isGhostTank = (templateName == "TankGhost1" || templateName == "TankGhost2" || templateName == "TankGhost3" ||
								templateName == "FrozenTankGhost");
			if (isGhostTank)
			{
				UnlockAchievement(killer, 5, "KillGhostTank", LanguageControl.Get(AchievementsWidget.fName, 14));
				s_subsystemAchievements.AddGhostTankKill(playerIndex);
				int kills = s_subsystemAchievements.GetGhostTankKills(playerIndex);

				if (kills <= 100)
				{
					if (kills <= 10) OnGhostTankCounterChanged?.Invoke(killer, kills, 10);
					else if (kills <= 50) OnGhostTankCounterChanged?.Invoke(killer, kills, 50);
					else OnGhostTankCounterChanged?.Invoke(killer, kills, 100);
				}

				if (kills >= 10 && !s_subsystemAchievements.IsAchievementUnlocked(28))
					UnlockAchievement(killer, 28, "Kill10GhostTanks", LanguageControl.Get(AchievementsWidget.fName, 60));
				if (kills >= 50 && !s_subsystemAchievements.IsAchievementUnlocked(29))
					UnlockAchievement(killer, 29, "Kill50GhostTanks", LanguageControl.Get(AchievementsWidget.fName, 62));
				if (kills >= 100 && !s_subsystemAchievements.IsAchievementUnlocked(30))
					UnlockAchievement(killer, 30, "Kill100GhostTanks", LanguageControl.Get(AchievementsWidget.fName, 64));
			}

			// ========== JEFE (excluye Tanks) ==========
			bool isBoss = (templateName == "MachineGunInfected" || templateName == "FlyingInfectedBoss");
			if (isBoss)
			{
				UnlockAchievement(killer, 15, "KillBoss", LanguageControl.Get(AchievementsWidget.fName, 34));
				s_subsystemAchievements.AddBossKill(playerIndex);
				int kills = s_subsystemAchievements.GetBossKills(playerIndex);

				if (kills <= 100)
				{
					if (kills <= 10) OnBossCounterChanged?.Invoke(killer, kills, 10);
					else if (kills <= 50) OnBossCounterChanged?.Invoke(killer, kills, 50);
					else OnBossCounterChanged?.Invoke(killer, kills, 100);
				}

				if (kills >= 10 && !s_subsystemAchievements.IsAchievementUnlocked(19))
					UnlockAchievement(killer, 19, "Kill10Bosses", LanguageControl.Get(AchievementsWidget.fName, 42));
				if (kills >= 50 && !s_subsystemAchievements.IsAchievementUnlocked(20))
					UnlockAchievement(killer, 20, "Kill50Bosses", LanguageControl.Get(AchievementsWidget.fName, 44));
				if (kills >= 100 && !s_subsystemAchievements.IsAchievementUnlocked(21))
					UnlockAchievement(killer, 21, "Kill100Bosses", LanguageControl.Get(AchievementsWidget.fName, 46));
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
				audio.PlaySound("Audio/pump it up", 1f, 0f, player.ComponentBody.Position, 10f, false);
		}

		public static bool IsAchievementUnlocked(ComponentPlayer player, int achievementNumber)
			=> s_subsystemAchievements != null && s_subsystemAchievements.IsAchievementUnlocked(achievementNumber);

		public static bool IsRewardClaimed(ComponentPlayer player, int achievementNumber)
			=> s_subsystemAchievements != null && s_subsystemAchievements.IsRewardClaimed(achievementNumber);

		// Métodos para obtener contadores
		public static int GetInfectedKills(ComponentPlayer player) => player == null ? 0 : s_subsystemAchievements?.GetInfectedKills(player.PlayerData.PlayerIndex) ?? 0;
		public static int GetBossKills(ComponentPlayer player) => player == null ? 0 : s_subsystemAchievements?.GetBossKills(player.PlayerData.PlayerIndex) ?? 0;
		public static int GetTankKills(ComponentPlayer player) => player == null ? 0 : s_subsystemAchievements?.GetTankKills(player.PlayerData.PlayerIndex) ?? 0;
		public static int GetGhostKills(ComponentPlayer player) => player == null ? 0 : s_subsystemAchievements?.GetGhostKills(player.PlayerData.PlayerIndex) ?? 0;
		public static int GetGhostTankKills(ComponentPlayer player) => player == null ? 0 : s_subsystemAchievements?.GetGhostTankKills(player.PlayerData.PlayerIndex) ?? 0;
		public static int GetBanditKills(ComponentPlayer player) => player == null ? 0 : s_subsystemAchievements?.GetBanditKills(player.PlayerData.PlayerIndex) ?? 0;

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
					player.ComponentGui.DisplaySmallMessage(LanguageControl.Get("AchievementsMessages", 2), Color.Red, false, true);
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
			player.ComponentGui.DisplayLargeMessage(LanguageControl.Get("AchievementsMessages", 0), displayName, 4f, 0f);

			var audio = player.Project.FindSubsystem<SubsystemAudio>(true);
			if (audio != null)
				audio.PlaySound("Audio/pump it up", 1f, 0f, player.ComponentBody.Position, 10f, false);
		}
	}
}
