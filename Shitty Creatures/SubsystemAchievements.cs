using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemAchievements : Subsystem
	{
		public static event Action<int> AchievementUnlocked;
		private Dictionary<int, long> m_unlockTimes = new Dictionary<int, long>();
		private Dictionary<int, string> m_achievements = new Dictionary<int, string>();
		private HashSet<int> m_claimedRewards = new HashSet<int>();

		// Contadores por jugador (clave = PlayerIndex)
		private Dictionary<int, int> m_infectedKills = new Dictionary<int, int>();
		private Dictionary<int, int> m_bossKills = new Dictionary<int, int>();
		private Dictionary<int, int> m_tankKills = new Dictionary<int, int>();
		private Dictionary<int, int> m_ghostKills = new Dictionary<int, int>();
		private Dictionary<int, int> m_ghostTankKills = new Dictionary<int, int>();
		private Dictionary<int, int> m_banditKills = new Dictionary<int, int>();
		private Dictionary<int, int> m_heals = new Dictionary<int, int>();
		private Dictionary<int, int> m_pirateKills = new Dictionary<int, int>();
		private Dictionary<int, int> m_flyingKills = new Dictionary<int, int>();
		private Dictionary<int, int> m_boomerKills = new Dictionary<int, int>();

		// NUEVOS: Contadores de domesticación
		private Dictionary<int, int> m_normalTames = new Dictionary<int, int>();   // Infectados normales (no jefe, no fantasma)
		private Dictionary<int, int> m_bossTames = new Dictionary<int, int>();     // Jefes (Tanks, MachineGun, FlyingInfectedBoss)
		private Dictionary<int, int> m_ghostTames = new Dictionary<int, int>();    // Fantasmas (todos los tipos fantasma)

		private bool m_allAchievementsCelebrationTriggered = false;
		private double m_celebrationEndTime = 0;
		private int m_backgroundState = 0;

		public bool IsAllAchievementsCelebrationTriggered() => m_allAchievementsCelebrationTriggered;
		public void SetAllAchievementsCelebrationTriggered(bool triggered) => m_allAchievementsCelebrationTriggered = triggered;

		public double GetCelebrationEndTime() => m_celebrationEndTime;
		public void SetCelebrationEndTime(double time) => m_celebrationEndTime = time;

		public int GetUnlockedAchievementCount() => m_achievements.Count;
		public int GetBackgroundState() => m_backgroundState;
		public void SetBackgroundState(int state) => m_backgroundState = state;

		public bool IsAchievementUnlocked(int achievementNumber) => m_achievements.ContainsKey(achievementNumber);
		public void UnlockAchievement(int achievementNumber, string achievementId)
		{
			if (m_achievements.ContainsKey(achievementNumber)) return;
			m_achievements.Add(achievementNumber, achievementId);
			// Registrar tiempo de desbloqueo (en ticks UTC)
			m_unlockTimes[achievementNumber] = DateTime.UtcNow.Ticks;
			AchievementUnlocked?.Invoke(achievementNumber);
		}

		public bool IsRewardClaimed(int achievementNumber) => m_claimedRewards.Contains(achievementNumber);
		public void ClaimReward(int achievementNumber)
		{
			if (!IsAchievementUnlocked(achievementNumber)) return;
			m_claimedRewards.Add(achievementNumber);
		}

		// ========== DOMESTICACIONES NORMALES ==========
		public int GetNormalTames(int playerIndex) => m_normalTames.TryGetValue(playerIndex, out int v) ? v : 0;
		public void AddNormalTame(int playerIndex)
		{
			if (!m_normalTames.ContainsKey(playerIndex)) m_normalTames[playerIndex] = 0;
			m_normalTames[playerIndex]++;
		}

		// ========== DOMESTICACIONES JEFES ==========
		public int GetBossTames(int playerIndex) => m_bossTames.TryGetValue(playerIndex, out int v) ? v : 0;
		public void AddBossTame(int playerIndex)
		{
			if (!m_bossTames.ContainsKey(playerIndex)) m_bossTames[playerIndex] = 0;
			m_bossTames[playerIndex]++;
		}

		// ========== DOMESTICACIONES FANTASMAS ==========
		public int GetGhostTames(int playerIndex) => m_ghostTames.TryGetValue(playerIndex, out int v) ? v : 0;
		public void AddGhostTame(int playerIndex)
		{
			if (!m_ghostTames.ContainsKey(playerIndex)) m_ghostTames[playerIndex] = 0;
			m_ghostTames[playerIndex]++;
		}

		// ========== MÉTODOS EXISTENTES (infectados, jefes, tanques, etc.) ==========
		public int GetInfectedKills(int playerIndex) => m_infectedKills.TryGetValue(playerIndex, out int v) ? v : 0;
		public void AddInfectedKill(int playerIndex)
		{
			if (!m_infectedKills.ContainsKey(playerIndex)) m_infectedKills[playerIndex] = 0;
			m_infectedKills[playerIndex]++;
		}

		public int GetBossKills(int playerIndex) => m_bossKills.TryGetValue(playerIndex, out int v) ? v : 0;
		public void AddBossKill(int playerIndex)
		{
			if (!m_bossKills.ContainsKey(playerIndex)) m_bossKills[playerIndex] = 0;
			m_bossKills[playerIndex]++;
		}

		public int GetTankKills(int playerIndex) => m_tankKills.TryGetValue(playerIndex, out int v) ? v : 0;
		public void AddTankKill(int playerIndex)
		{
			if (!m_tankKills.ContainsKey(playerIndex)) m_tankKills[playerIndex] = 0;
			m_tankKills[playerIndex]++;
		}

		public int GetGhostKills(int playerIndex) => m_ghostKills.TryGetValue(playerIndex, out int v) ? v : 0;
		public void AddGhostKill(int playerIndex)
		{
			if (!m_ghostKills.ContainsKey(playerIndex)) m_ghostKills[playerIndex] = 0;
			m_ghostKills[playerIndex]++;
		}

		public int GetGhostTankKills(int playerIndex) => m_ghostTankKills.TryGetValue(playerIndex, out int v) ? v : 0;
		public void AddGhostTankKill(int playerIndex)
		{
			if (!m_ghostTankKills.ContainsKey(playerIndex)) m_ghostTankKills[playerIndex] = 0;
			m_ghostTankKills[playerIndex]++;
		}

		public int GetBanditKills(int playerIndex) => m_banditKills.TryGetValue(playerIndex, out int v) ? v : 0;
		public void AddBanditKill(int playerIndex)
		{
			if (!m_banditKills.ContainsKey(playerIndex)) m_banditKills[playerIndex] = 0;
			m_banditKills[playerIndex]++;
		}

		public int GetHeals(int playerIndex) => m_heals.TryGetValue(playerIndex, out int v) ? v : 0;
		public void AddHeal(int playerIndex)
		{
			if (!m_heals.ContainsKey(playerIndex)) m_heals[playerIndex] = 0;
			m_heals[playerIndex]++;
		}

		public int GetFlyingKills(int playerIndex) => m_flyingKills.TryGetValue(playerIndex, out int v) ? v : 0;
		public void AddFlyingKill(int playerIndex)
		{
			if (!m_flyingKills.ContainsKey(playerIndex)) m_flyingKills[playerIndex] = 0;
			m_flyingKills[playerIndex]++;
		}

		public int GetPirateKills(int playerIndex) => m_pirateKills.TryGetValue(playerIndex, out int v) ? v : 0;
		public void AddPirateKill(int playerIndex)
		{
			if (!m_pirateKills.ContainsKey(playerIndex)) m_pirateKills[playerIndex] = 0;
			m_pirateKills[playerIndex]++;
		}

		public int GetBoomerKills(int playerIndex) => m_boomerKills.TryGetValue(playerIndex, out int v) ? v : 0;
		public void AddBoomerKill(int playerIndex)
		{
			if (!m_boomerKills.ContainsKey(playerIndex)) m_boomerKills[playerIndex] = 0;
			m_boomerKills[playerIndex]++;
		}

		private void LoadCounter(ValuesDictionary dict, string key, Dictionary<int, int> target)
		{
			if (dict.TryGetValue(key, out object raw) && raw is string str && !string.IsNullOrEmpty(str))
			{
				foreach (string pair in str.Split(';', StringSplitOptions.RemoveEmptyEntries))
				{
					string[] parts = pair.Split(':');
					if (parts.Length == 2 && int.TryParse(parts[0], out int idx) && int.TryParse(parts[1], out int val))
						target[idx] = val;
				}
			}
		}

		private void SaveCounter(ValuesDictionary dict, string key, Dictionary<int, int> source)
		{
			if (source.Count > 0)
			{
				List<string> pairs = new List<string>();
				foreach (var kvp in source)
					pairs.Add($"{kvp.Key}:{kvp.Value}");
				dict.SetValue(key, string.Join(";", pairs));
			}
		}

		public long GetUnlockTime(int achievementNumber)
		{
			return m_unlockTimes.TryGetValue(achievementNumber, out long time) ? time : 0;
		}

		public void SetUnlockTime(int achievementNumber, long ticks)
		{
			m_unlockTimes[achievementNumber] = ticks;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			// Logros desbloqueados
			for (int i = 1; i <= 100; i++)
			{
				string achievementId = valuesDictionary.GetValue<string>($"Achievement{i}", null);
				if (!string.IsNullOrEmpty(achievementId))
					m_achievements[i] = achievementId;
			}

			// Recompensas reclamadas
			if (valuesDictionary.TryGetValue("ClaimedRewards", out object rawValue) && rawValue != null)
			{
				if (rawValue is int intValue)
				{
					for (int i = 1; i <= intValue; i++)
						m_claimedRewards.Add(i);
				}
				else if (rawValue is string strValue && !string.IsNullOrEmpty(strValue))
				{
					foreach (string part in strValue.Split(','))
						if (int.TryParse(part, out int num))
							m_claimedRewards.Add(num);
				}
				else if (rawValue is List<object> list)
				{
					foreach (var obj in list)
						if (obj is int num)
							m_claimedRewards.Add(num);
				}
			}

			// Contadores existentes
			LoadCounter(valuesDictionary, "InfectedKills", m_infectedKills);
			LoadCounter(valuesDictionary, "BossKills", m_bossKills);
			LoadCounter(valuesDictionary, "TankKills", m_tankKills);
			LoadCounter(valuesDictionary, "GhostKills", m_ghostKills);
			LoadCounter(valuesDictionary, "GhostTankKills", m_ghostTankKills);
			LoadCounter(valuesDictionary, "BanditKills", m_banditKills);
			LoadCounter(valuesDictionary, "Heals", m_heals);
			LoadCounter(valuesDictionary, "PirateKills", m_pirateKills);
			LoadCounter(valuesDictionary, "FlyingKills", m_flyingKills);
			LoadCounter(valuesDictionary, "BoomerKills", m_boomerKills);

			// NUEVOS: Contadores de domesticación
			LoadCounter(valuesDictionary, "NormalTames", m_normalTames);
			LoadCounter(valuesDictionary, "BossTames", m_bossTames);
			LoadCounter(valuesDictionary, "GhostTames", m_ghostTames);

			if (valuesDictionary.TryGetValue("AllAchievementsCelebrationTriggered", out object triggerValue) && triggerValue is bool flag)
				m_allAchievementsCelebrationTriggered = flag;

			if (valuesDictionary.TryGetValue("CelebrationEndTime", out object endTimeValue) && endTimeValue is double endTime)
				m_celebrationEndTime = endTime;

			if (valuesDictionary.TryGetValue("BackgroundState", out object bgState) && bgState is int state)
				m_backgroundState = state;
			else
				m_backgroundState = 0;

			if (valuesDictionary.TryGetValue("UnlockTimes", out object timesObj) && timesObj is string timesStr)
			{
				foreach (string pair in timesStr.Split(';', StringSplitOptions.RemoveEmptyEntries))
				{
					string[] parts = pair.Split(':');
					if (parts.Length == 2 && int.TryParse(parts[0], out int num) && long.TryParse(parts[1], out long ticks))
						m_unlockTimes[num] = ticks;
				}
			}
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			// Logros desbloqueados
			foreach (var kvp in m_achievements)
				valuesDictionary.SetValue<string>($"Achievement{kvp.Key}", kvp.Value);

			// Recompensas reclamadas
			if (m_claimedRewards.Count > 0)
			{
				var sorted = m_claimedRewards.OrderBy(x => x).ToList();
				valuesDictionary.SetValue("ClaimedRewards", string.Join(",", sorted));
			}

			// Contadores existentes
			SaveCounter(valuesDictionary, "InfectedKills", m_infectedKills);
			SaveCounter(valuesDictionary, "BossKills", m_bossKills);
			SaveCounter(valuesDictionary, "TankKills", m_tankKills);
			SaveCounter(valuesDictionary, "GhostKills", m_ghostKills);
			SaveCounter(valuesDictionary, "GhostTankKills", m_ghostTankKills);
			SaveCounter(valuesDictionary, "BanditKills", m_banditKills);
			SaveCounter(valuesDictionary, "Heals", m_heals);
			SaveCounter(valuesDictionary, "PirateKills", m_pirateKills);
			SaveCounter(valuesDictionary, "FlyingKills", m_flyingKills);
			SaveCounter(valuesDictionary, "BoomerKills", m_boomerKills);

			// NUEVOS: Contadores de domesticación
			SaveCounter(valuesDictionary, "NormalTames", m_normalTames);
			SaveCounter(valuesDictionary, "BossTames", m_bossTames);
			SaveCounter(valuesDictionary, "GhostTames", m_ghostTames);

			valuesDictionary.SetValue("AllAchievementsCelebrationTriggered", m_allAchievementsCelebrationTriggered);
			valuesDictionary.SetValue("CelebrationEndTime", m_celebrationEndTime);
			valuesDictionary.SetValue("BackgroundState", m_backgroundState);

			if (m_unlockTimes.Count > 0)
			{
				List<string> pairs = new List<string>();
				foreach (var kvp in m_unlockTimes)
					pairs.Add($"{kvp.Key}:{kvp.Value}");
				valuesDictionary.SetValue("UnlockTimes", string.Join(";", pairs));
			}
		}
	}
}
