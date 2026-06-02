using System;
using System.Collections.Generic;
using System.Xml.Linq;
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
		private static Dictionary<int, int> s_achievementRewards;

		// Eventos para actualizar UI
		public static event Action<ComponentPlayer, int, int> OnInfectedCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnBossCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnTankCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnGhostCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnGhostTankCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnBanditCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnHealCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnPirateCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnFlyingCounterChanged;
		public static event Action<ComponentPlayer, int, int> OnBoomerCounterChanged;

		// Eventos para celebración de todos los logros
		public static event Action OnCelebrationStarted;
		public static event Action OnCelebrationEnded;

		// Variable para saber si la celebración está activa
		public static bool IsCelebrationActive { get; private set; } = false;

		// Control de generación de fuegos artificiales
		private static Random s_fireworkRandom = new Random();
		private static double s_celebrationEndTime = 0;
		private static bool s_isGeneratingFireworks = false;

		private static void LoadAchievementRewards()
		{
			if (s_achievementRewards != null) return;
			s_achievementRewards = new Dictionary<int, int>();
			try
			{
				XElement achievementsXml = ContentManager.Get<XElement>("AchievementsData");
				if (achievementsXml == null)
				{
					Log.Warning("[AchievementsManager] No se pudo cargar AchievementsData.xml");
					return;
				}
				foreach (XElement elem in achievementsXml.Elements("Achievement"))
				{
					int number = (int)elem.Attribute("Number");
					int reward = (int)elem.Attribute("Reward");
					s_achievementRewards[number] = reward;
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[AchievementsManager] Error cargando recompensas: {ex.Message}");
			}
		}

		private static int GetRewardForAchievement(int achievementNumber)
		{
			if (s_achievementRewards == null) LoadAchievementRewards();
			return s_achievementRewards != null && s_achievementRewards.TryGetValue(achievementNumber, out int reward) ? reward : 0;
		}

		public static void Initialize(Project project)
		{
			s_currentProject = project;
			s_subsystemAchievements = project.FindSubsystem<SubsystemAchievements>(true);
			s_subsystemTime = project.FindSubsystem<SubsystemTime>(true);
			s_subsystemGameInfo = project.FindSubsystem<SubsystemGameInfo>(true);
			s_subsystemTimeOfDay = project.FindSubsystem<SubsystemTimeOfDay>(true);
			LoadAchievementRewards();

			// 🔁 Reiniciar el temporizador de verificación de días
			s_lastDayCheckGameTime = -1.0;

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
			// Detener generación de fuegos artificiales
			s_isGeneratingFireworks = false;

			// Detener música de celebración
			if (IsCelebrationActive)
			{
				InGameMusicManager.StopMusic();
				IsCelebrationActive = false;
			}

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

			s_lastDayCheckGameTime = -1.0;
			s_celebrationEndTime = 0;
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

			double rawDay = s_subsystemTimeOfDay.Day;
			int currentDay = (int)Math.Floor(rawDay) + 1;

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

		private static void OnNaturalNightEnded()
		{
			if (s_subsystemAchievements == null) return;
			var greenNight = s_currentProject?.FindSubsystem<SubsystemGreenNightSky>(true);
			if (greenNight == null) return;
			if (greenNight.DifficultyMode != DifficultyMode.Extreme) return;
			if (s_subsystemAchievements.IsAchievementUnlocked(52)) return;
			var players = s_currentProject?.FindSubsystem<SubsystemPlayers>(true);
			if (players == null) return;
			string title = LanguageControl.Get(AchievementsWidget.fName, 108);
			foreach (var player in players.ComponentPlayers)
			{
				UnlockAchievement(player, 52, "ExtremeNightSurvived", title);
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

				// CORRECCIÓN: Usar 'if' independientes y sin límite <= 100 para que siempre notifique a la UI
				if (!s_subsystemAchievements.IsAchievementUnlocked(22)) OnTankCounterChanged?.Invoke(killer, kills, 10);
				if (!s_subsystemAchievements.IsAchievementUnlocked(23)) OnTankCounterChanged?.Invoke(killer, kills, 50);
				if (!s_subsystemAchievements.IsAchievementUnlocked(24)) OnTankCounterChanged?.Invoke(killer, kills, 100);

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
						 templateName == "InfectedBird" || templateName == "InfectedBear" ||
						 templateName == "InfectedFly1" || templateName == "InfectedFly2" || templateName == "Charger1" || templateName == "Charger2");
			if (isNormalInfected)
			{
				UnlockAchievement(killer, 2, "KillInfected", LanguageControl.Get(AchievementsWidget.fName, 8));
				s_subsystemAchievements.AddInfectedKill(playerIndex);
				int kills = s_subsystemAchievements.GetInfectedKills(playerIndex);

				if (!s_subsystemAchievements.IsAchievementUnlocked(16)) OnInfectedCounterChanged?.Invoke(killer, kills, 10);
				if (!s_subsystemAchievements.IsAchievementUnlocked(17)) OnInfectedCounterChanged?.Invoke(killer, kills, 50);
				if (!s_subsystemAchievements.IsAchievementUnlocked(18)) OnInfectedCounterChanged?.Invoke(killer, kills, 100);

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

				if (!s_subsystemAchievements.IsAchievementUnlocked(31)) OnBanditCounterChanged?.Invoke(killer, kills, 10);
				if (!s_subsystemAchievements.IsAchievementUnlocked(32)) OnBanditCounterChanged?.Invoke(killer, kills, 50);
				if (!s_subsystemAchievements.IsAchievementUnlocked(33)) OnBanditCounterChanged?.Invoke(killer, kills, 100);

				if (kills >= 10 && !s_subsystemAchievements.IsAchievementUnlocked(31))
					UnlockAchievement(killer, 31, "Kill10Bandits", LanguageControl.Get(AchievementsWidget.fName, 66));
				if (kills >= 50 && !s_subsystemAchievements.IsAchievementUnlocked(32))
					UnlockAchievement(killer, 32, "Kill50Bandits", LanguageControl.Get(AchievementsWidget.fName, 68));
				if (kills >= 100 && !s_subsystemAchievements.IsAchievementUnlocked(33))
					UnlockAchievement(killer, 33, "Kill100Bandits", LanguageControl.Get(AchievementsWidget.fName, 70));
			}

			// ========== FANTASMA NORMAL ==========
			bool isGhost = (templateName == "GhostNormal" || templateName == "GhostFast" || templateName == "PoisonousGhost" ||
				templateName == "FrozenGhost" || templateName == "GhostCharger");
			if (isGhost)
			{
				UnlockAchievement(killer, 4, "KillGhost", LanguageControl.Get(AchievementsWidget.fName, 12));
				s_subsystemAchievements.AddGhostKill(playerIndex);
				int kills = s_subsystemAchievements.GetGhostKills(playerIndex);

				if (!s_subsystemAchievements.IsAchievementUnlocked(25)) OnGhostCounterChanged?.Invoke(killer, kills, 10);
				if (!s_subsystemAchievements.IsAchievementUnlocked(26)) OnGhostCounterChanged?.Invoke(killer, kills, 50);
				if (!s_subsystemAchievements.IsAchievementUnlocked(27)) OnGhostCounterChanged?.Invoke(killer, kills, 100);

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

				if (!s_subsystemAchievements.IsAchievementUnlocked(28)) OnGhostTankCounterChanged?.Invoke(killer, kills, 10);
				if (!s_subsystemAchievements.IsAchievementUnlocked(29)) OnGhostTankCounterChanged?.Invoke(killer, kills, 50);
				if (!s_subsystemAchievements.IsAchievementUnlocked(30)) OnGhostTankCounterChanged?.Invoke(killer, kills, 100);

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

				if (!s_subsystemAchievements.IsAchievementUnlocked(19)) OnBossCounterChanged?.Invoke(killer, kills, 10);
				if (!s_subsystemAchievements.IsAchievementUnlocked(20)) OnBossCounterChanged?.Invoke(killer, kills, 50);
				if (!s_subsystemAchievements.IsAchievementUnlocked(21)) OnBossCounterChanged?.Invoke(killer, kills, 100);

				if (kills >= 10 && !s_subsystemAchievements.IsAchievementUnlocked(19))
					UnlockAchievement(killer, 19, "Kill10Bosses", LanguageControl.Get(AchievementsWidget.fName, 42));
				if (kills >= 50 && !s_subsystemAchievements.IsAchievementUnlocked(20))
					UnlockAchievement(killer, 20, "Kill50Bosses", LanguageControl.Get(AchievementsWidget.fName, 44));
				if (kills >= 100 && !s_subsystemAchievements.IsAchievementUnlocked(21))
					UnlockAchievement(killer, 21, "Kill100Bosses", LanguageControl.Get(AchievementsWidget.fName, 46));
			}

			// ========== VOLADORES ==========
			bool isFlying = IsFlyingCreature(templateName);
			if (isFlying)
			{
				s_subsystemAchievements.AddFlyingKill(playerIndex);
				int kills = s_subsystemAchievements.GetFlyingKills(playerIndex);

				if (!s_subsystemAchievements.IsAchievementUnlocked(44)) OnFlyingCounterChanged?.Invoke(killer, kills, 10);
				if (!s_subsystemAchievements.IsAchievementUnlocked(45)) OnFlyingCounterChanged?.Invoke(killer, kills, 25);
				if (!s_subsystemAchievements.IsAchievementUnlocked(46)) OnFlyingCounterChanged?.Invoke(killer, kills, 50);
				if (!s_subsystemAchievements.IsAchievementUnlocked(47)) OnFlyingCounterChanged?.Invoke(killer, kills, 100);

				if (kills >= 10 && !s_subsystemAchievements.IsAchievementUnlocked(44))
					UnlockAchievement(killer, 44, "Kill10Flying", LanguageControl.Get(AchievementsWidget.fName, 92));
				if (kills >= 25 && !s_subsystemAchievements.IsAchievementUnlocked(45))
					UnlockAchievement(killer, 45, "Kill25Flying", LanguageControl.Get(AchievementsWidget.fName, 94));
				if (kills >= 50 && !s_subsystemAchievements.IsAchievementUnlocked(46))
					UnlockAchievement(killer, 46, "Kill50Flying", LanguageControl.Get(AchievementsWidget.fName, 96));
				if (kills >= 100 && !s_subsystemAchievements.IsAchievementUnlocked(47))
					UnlockAchievement(killer, 47, "Kill100Flying", LanguageControl.Get(AchievementsWidget.fName, 98));
			}

			// ========== BOOMER (normal y fantasma) ==========
			bool isBoomer = (templateName == "Boomer1" || templateName == "Boomer2" || templateName == "Boomer3" || templateName == "BoomerFrozen" ||
							 templateName == "GhostBoomer1" || templateName == "GhostBoomer2" || templateName == "GhostBoomer3");
			if (isBoomer)
			{
				UnlockAchievement(killer, 48, "KillBoomer", LanguageControl.Get(AchievementsWidget.fName, 100)); // Logro base (opcional, si quieres uno por el primer Boomer)
				s_subsystemAchievements.AddBoomerKill(playerIndex);
				int kills = s_subsystemAchievements.GetBoomerKills(playerIndex);

				// Notificar a la UI para los cuatro logros progresivos
				if (!s_subsystemAchievements.IsAchievementUnlocked(48)) OnBoomerCounterChanged?.Invoke(killer, kills, 10);
				if (!s_subsystemAchievements.IsAchievementUnlocked(49)) OnBoomerCounterChanged?.Invoke(killer, kills, 25);
				if (!s_subsystemAchievements.IsAchievementUnlocked(50)) OnBoomerCounterChanged?.Invoke(killer, kills, 55);
				if (!s_subsystemAchievements.IsAchievementUnlocked(51)) OnBoomerCounterChanged?.Invoke(killer, kills, 100);

				// Desbloquear cada logro al alcanzar el objetivo
				if (kills >= 10 && !s_subsystemAchievements.IsAchievementUnlocked(48))
					UnlockAchievement(killer, 48, "Kill10Boomers", LanguageControl.Get(AchievementsWidget.fName, 100));
				if (kills >= 25 && !s_subsystemAchievements.IsAchievementUnlocked(49))
					UnlockAchievement(killer, 49, "Kill25Boomers", LanguageControl.Get(AchievementsWidget.fName, 102));
				if (kills >= 55 && !s_subsystemAchievements.IsAchievementUnlocked(50))
					UnlockAchievement(killer, 50, "Kill55Boomers", LanguageControl.Get(AchievementsWidget.fName, 104));
				if (kills >= 100 && !s_subsystemAchievements.IsAchievementUnlocked(51))
					UnlockAchievement(killer, 51, "Kill100Boomers", LanguageControl.Get(AchievementsWidget.fName, 106));
			}
		}

		private static bool IsFlyingCreature(string templateName)
		{
			return templateName == "InfectedFly1" ||
				   templateName == "InfectedFly2" ||
				   templateName == "InfectedFly3" ||
				   templateName == "InfectedBird";
		}

		public static void OnHeal(ComponentPlayer healer)
		{
			if (s_subsystemAchievements == null || healer == null) return;

			int playerIndex = healer.PlayerData.PlayerIndex;
			s_subsystemAchievements.AddHeal(playerIndex);
			int total = s_subsystemAchievements.GetHeals(playerIndex);

			if (!s_subsystemAchievements.IsAchievementUnlocked(34)) OnHealCounterChanged?.Invoke(healer, total, 10);
			if (!s_subsystemAchievements.IsAchievementUnlocked(35)) OnHealCounterChanged?.Invoke(healer, total, 50);
			if (!s_subsystemAchievements.IsAchievementUnlocked(36)) OnHealCounterChanged?.Invoke(healer, total, 100);

			if (total >= 10 && !s_subsystemAchievements.IsAchievementUnlocked(34))
				UnlockAchievement(healer, 34, "Heal10", LanguageControl.Get(AchievementsWidget.fName, 72));
			if (total >= 50 && !s_subsystemAchievements.IsAchievementUnlocked(35))
				UnlockAchievement(healer, 35, "Heal50", LanguageControl.Get(AchievementsWidget.fName, 74));
			if (total >= 100 && !s_subsystemAchievements.IsAchievementUnlocked(36))
				UnlockAchievement(healer, 36, "Heal100", LanguageControl.Get(AchievementsWidget.fName, 76));
		}

		public static void OnPirateKill(ComponentPlayer killer)
		{
			if (s_subsystemAchievements == null || killer == null) return;
			int playerIndex = killer.PlayerData.PlayerIndex;
			s_subsystemAchievements.AddPirateKill(playerIndex);
			int kills = s_subsystemAchievements.GetPirateKills(playerIndex);

			if (!s_subsystemAchievements.IsAchievementUnlocked(38)) OnPirateCounterChanged?.Invoke(killer, kills, 10);
			if (!s_subsystemAchievements.IsAchievementUnlocked(39)) OnPirateCounterChanged?.Invoke(killer, kills, 50);
			if (!s_subsystemAchievements.IsAchievementUnlocked(40)) OnPirateCounterChanged?.Invoke(killer, kills, 100);

			if (kills >= 10 && !s_subsystemAchievements.IsAchievementUnlocked(38))
				UnlockAchievement(killer, 38, "Kill10Pirates", LanguageControl.Get(AchievementsWidget.fName, 80));
			if (kills >= 50 && !s_subsystemAchievements.IsAchievementUnlocked(39))
				UnlockAchievement(killer, 39, "Kill50Pirates", LanguageControl.Get(AchievementsWidget.fName, 82));
			if (kills >= 100 && !s_subsystemAchievements.IsAchievementUnlocked(40))
				UnlockAchievement(killer, 40, "Kill100Pirates", LanguageControl.Get(AchievementsWidget.fName, 84));
		}

		public static void OnBuyFromPirateTrader(ComponentPlayer buyer)
		{
			if (s_subsystemAchievements == null || buyer == null) return;
			if (!s_subsystemAchievements.IsAchievementUnlocked(41))
				UnlockAchievement(buyer, 41, "BuyFromPirateTrader", LanguageControl.Get(AchievementsWidget.fName, 86));
		}

		public static void OnKillPirateTrader(ComponentPlayer killer)
		{
			if (s_subsystemAchievements == null || killer == null) return;
			if (!s_subsystemAchievements.IsAchievementUnlocked(42))
				UnlockAchievement(killer, 42, "KillPirateTrader", LanguageControl.Get(AchievementsWidget.fName, 88));
		}

		public static void OnKillPirateCaptain(ComponentPlayer killer)
		{
			if (s_subsystemAchievements == null || killer == null) return;
			if (!s_subsystemAchievements.IsAchievementUnlocked(43))
				UnlockAchievement(killer, 43, "KillPirateCaptain", LanguageControl.Get(AchievementsWidget.fName, 90));
		}

		public static void OnHireMercenary(ComponentPlayer player)
		{
			if (s_subsystemAchievements == null || player == null) return;
			if (!s_subsystemAchievements.IsAchievementUnlocked(37))
				UnlockAchievement(player, 37, "HireMercenary", LanguageControl.Get(AchievementsWidget.fName, 78));
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

			int reward = GetRewardForAchievement(achievementNumber);

			// Mostrar mensaje del logro
			player.ComponentGui.DisplayLargeMessage(
				string.Format(LanguageControl.Get("AchievementsMessages", 0), displayName),
				string.Format(LanguageControl.Get("AchievementsMessages", 1), reward),
				4f, 0f);

			// Sonido del logro
			var audio = player.Project.FindSubsystem<SubsystemAudio>(true);
			if (audio != null)
				audio.PlaySound("Audio/pump it up", 1f, 0f, player.ComponentBody.Position, 10f, false);

			// Programar la verificación del último logro después de 8 segundos
			var time = player.Project.FindSubsystem<SubsystemTime>(true);
			time.QueueGameTimeDelayedExecution(time.GameTime + 8.0, () => CheckAndTriggerAllAchievements(player));
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
		public static int GetHeals(ComponentPlayer player) => player == null ? 0 : s_subsystemAchievements?.GetHeals(player.PlayerData.PlayerIndex) ?? 0;
		public static int GetPirateKills(ComponentPlayer player) => player == null ? 0 : s_subsystemAchievements?.GetPirateKills(player.PlayerData.PlayerIndex) ?? 0;
		public static int GetFlyingKills(ComponentPlayer player) => player == null ? 0 : s_subsystemAchievements?.GetFlyingKills(player.PlayerData.PlayerIndex) ?? 0;
		public static int GetBoomerKills(ComponentPlayer player) => player == null ? 0 : s_subsystemAchievements?.GetBoomerKills(player.PlayerData.PlayerIndex) ?? 0;

		// Agregar estos métodos estáticos:

		private static void CheckAndTriggerAllAchievements(ComponentPlayer player)
		{
			if (s_subsystemAchievements == null || player == null) return;
			if (s_achievementRewards == null) LoadAchievementRewards();
			int totalAchievements = s_achievementRewards.Count;
			int unlocked = s_subsystemAchievements.GetUnlockedAchievementCount();
			if (unlocked >= totalAchievements && !s_subsystemAchievements.IsAllAchievementsCelebrationTriggered())
			{
				s_subsystemAchievements.SetAllAchievementsCelebrationTriggered(true);
				StartAllAchievementsCelebration(player);
			}
		}

		private static void StartAllAchievementsCelebration(ComponentPlayer player)
		{
			// No activar celebración todavía, solo mostrar mensaje y sonido inicial
			// La celebración real (bailes, supresión de ataques) comenzará después de 8 segundos, junto con la música

			// Mensaje especial
			player.ComponentGui.DisplayLargeMessage(
				LanguageControl.Get("AchievementsMessages", 4),
				LanguageControl.Get("AchievementsMessages", 5),
				5f, 0f);

			// Sonido especial (8 segundos)
			var audio = player.Project.FindSubsystem<SubsystemAudio>(true);
			if (audio != null)
				audio.PlaySound("Audio/Death of King Gedol", 1f, 0f, player.ComponentBody.Position, 30f, false);

			// Programar la cuenta atrás de 8 segundos; al terminar, iniciar la celebración real
			GameManager.SyncDispatcher.Add(() => {
				StartFireworkCountdown(player, 8.0f);
				return true;
			});
		}

		private static void StartFireworkCountdown(ComponentPlayer player, float remainingSeconds)
		{
			if (player == null || player.Project == null) return;

			if (remainingSeconds <= 0f)
			{
				// ¡Comienza la celebración real!
				IsCelebrationActive = true;
				s_isGeneratingFireworks = true;
				OnCelebrationStarted?.Invoke();

				// Música en bucle durante 600 segundos
				StartLoopingMusic(player, "MenuMusic/Sparkster Genesis Normal Ending", 600f);
				// Iniciar generación continua de fuegos artificiales
				StartContinuousFireworks(player, 600.0);
				return;
			}

			GameManager.SyncDispatcher.Add(() => {
				StartFireworkCountdown(player, remainingSeconds - Time.FrameDuration);
				return true;
			});
		}

		private static void StartLoopingMusic(ComponentPlayer player, string musicPath, float totalDurationSeconds)
		{
			double endTime = Time.RealTime + totalDurationSeconds;
			bool firstPlay = true;
			bool wasActive = true; // para saber si la última vez estaba activo

			Action loop = null;
			loop = () => {
				if (player?.Project == null || Time.RealTime >= endTime)
					return;

				bool isActive = IsGameActive(player);

				// Si pasó de activo a inactivo, detener música
				if (wasActive && !isActive)
				{
					InGameMusicManager.StopMusic();
				}

				wasActive = isActive;

				if (!isActive)
				{
					// No hacer nada, solo esperar
					GameManager.SyncDispatcher.Add(() => { loop(); return true; });
					return;
				}

				if (InGameMusicManager.IsFadingOut)
					return;

				bool needsRestart = firstPlay
					|| !InGameMusicManager.IsPlaying
					|| InGameMusicManager.IsPlaybackComplete();

				if (needsRestart)
				{
					InGameMusicManager.PlayMusic(musicPath, 0f);
					firstPlay = false;
				}

				GameManager.SyncDispatcher.Add(() => { loop(); return true; });
			};

			loop();
		}

		private static bool IsGameActive(ComponentPlayer player)
		{
			if (player?.Project == null) return false;

			// Verificar si la pantalla actual es GameScreen
			var currentScreen = ScreensManager.CurrentScreen;
			if (currentScreen == null) return false;

			string screenName = currentScreen.GetType().Name;

			// 🔇 Solo detener la música si la pantalla es Configuración o Ayuda
			if (screenName == "SettingsScreen" || screenName == "HelpScreen")
				return false;

			// Para cualquier otra pantalla (GameScreen, BestiaryScreen, RecipaediaScreen, etc.)
			// asumimos que el juego sigue activo (la música no se detiene)

			// Verificar pausa por tiempo detenido (opcional)
			var subsystemTime = player.Project.FindSubsystem<SubsystemTime>(true);
			if (subsystemTime != null && subsystemTime.GameTimeDelta == 0f)
				return false;

			// Verificar si hay un panel modal que no sea el juego (como GameMenuDialog)
			// Esto es opcional, pero evita que suene música en el menú de pausa si lo deseas
			if (player.ComponentGui.ModalPanelWidget != null)
			{
				// Si quieres que la música también se detenga en el menú de pausa, descomenta:
				// return false;
			}

			return true;
		}

		private static void StartContinuousFireworks(ComponentPlayer player, double durationSeconds)
		{
			if (player == null || player.Project == null) return;

			double startTime = Time.RealTime;
			double endTime = startTime + durationSeconds;
			s_celebrationEndTime = endTime;

			Action generateFireworks = null;
			generateFireworks = () =>
			{
				if (player?.Project == null) return;
				if (!s_isGeneratingFireworks) return;

				double currentTime = Time.RealTime;
				if (currentTime >= endTime)
				{
					// Terminar celebración
					s_isGeneratingFireworks = false;
					InGameMusicManager.FadeOutAndStop();
					IsCelebrationActive = false;
					OnCelebrationEnded?.Invoke();

					Action fadeLoop = null;
					fadeLoop = () => {
						InGameMusicManager.Update();
						if (InGameMusicManager.IsFadingOut)
						{
							GameManager.SyncDispatcher.Add(() => { fadeLoop(); return true; });
						}
					};
					fadeLoop();
					return;
				}

				// Calcular intensidad basada en el tiempo restante (más fuegos al principio y al final)
				float timeLeft = (float)(endTime - currentTime);
				float intensity = 1.5f;
				if (timeLeft < 30f)
					intensity = 4f; // Gran final
				else if (timeLeft > durationSeconds - 30f)
					intensity = 3f; // Comienzo fuerte
				else
					intensity = 1.2f; // Normal

				float probability = intensity * (float)Time.FrameDuration;

				if (s_fireworkRandom.Float(0f, 1f) < probability && IsGameActive(player))
				{
					SpawnRandomFirework(player);
				}

				GameManager.SyncDispatcher.Add(() => { generateFireworks(); return true; });
			};

			generateFireworks();
		}

		private static void ScheduleFireworksAndStopMusic(ComponentPlayer player, double durationSeconds = 150.0)
		{
			// Este método se mantiene por compatibilidad pero no se usa
			// La generación de fuegos ahora se maneja con StartContinuousFireworks
			StartContinuousFireworks(player, durationSeconds);
		}

		private static void SpawnRandomFirework(ComponentPlayer player)
		{
			if (player == null || player.Project == null) return;
			var projectiles = player.Project.FindSubsystem<SubsystemProjectiles>(true);
			if (projectiles == null) return;

			Vector3 playerPos = player.ComponentBody.Position;

			// Posición aleatoria en un círculo de radio 15-30 alrededor del jugador
			float angle = s_fireworkRandom.Float(0f, MathF.PI * 2);
			float radius = s_fireworkRandom.Float(15f, 30f);
			float dx = MathF.Cos(angle) * radius;
			float dz = MathF.Sin(angle) * radius;
			Vector3 launchPos = new Vector3(playerPos.X + dx, playerPos.Y + 0.5f, playerPos.Z + dz);

			// Datos aleatorios del fuego artificial
			int data = 0;
			data = FireworksBlock.SetShape(data, (FireworksBlock.Shape)s_fireworkRandom.Int(0, 7));
			data = FireworksBlock.SetColor(data, s_fireworkRandom.Int(0, 7));
			data = FireworksBlock.SetAltitude(data, s_fireworkRandom.Int(0, 1)); // 0 = baja, 1 = alta
			data = FireworksBlock.SetFlickering(data, s_fireworkRandom.Float(0f, 1f) < 0.25f);
			int value = Terrain.MakeBlockValue(215, 0, data);

			// Velocidad inicial: hacia arriba con ligera dispersión horizontal
			Vector3 velocity = new Vector3(s_fireworkRandom.Float(-3f, 3f), 45f, s_fireworkRandom.Float(-3f, 3f));
			projectiles.FireProjectile(value, launchPos, velocity, Vector3.Zero, null);
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

			int reward = GetRewardForAchievement(achievementNumber);

			player.ComponentGui.DisplayLargeMessage(
				string.Format(LanguageControl.Get("AchievementsMessages", 0), displayName),
				string.Format(LanguageControl.Get("AchievementsMessages", 1), reward),
				4f, 0f);

			var audio = player.Project.FindSubsystem<SubsystemAudio>(true);
			if (audio != null)
				audio.PlaySound("Audio/pump it up", 1f, 0f, player.ComponentBody.Position, 10f, false);

			// Programar la verificación después de 8 segundos
			var time = player.Project.FindSubsystem<SubsystemTime>(true);
			time.QueueGameTimeDelayedExecution(time.GameTime + 8.0, () => CheckAndTriggerAllAchievements(player));
		}
	}
}
