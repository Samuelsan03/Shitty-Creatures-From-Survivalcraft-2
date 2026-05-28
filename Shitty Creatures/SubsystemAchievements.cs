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
		private Dictionary<int, string> m_achievements = new Dictionary<int, string>();

		public bool IsAchievementUnlocked(int achievementNumber)
		{
			return m_achievements.ContainsKey(achievementNumber);
		}

		public void UnlockAchievement(int achievementNumber, string achievementId)
		{
			if (m_achievements.ContainsKey(achievementNumber))
				return;
			m_achievements.Add(achievementNumber, achievementId);
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			for (int i = 1; i <= 100; i++)
			{
				string achievementId = valuesDictionary.GetValue<string>($"Achievement{i}", null);
				if (!string.IsNullOrEmpty(achievementId))
				{
					m_achievements[i] = achievementId;
				}
			}
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			foreach (var kvp in m_achievements)
			{
				valuesDictionary.SetValue<string>($"Achievement{kvp.Key}", kvp.Value);
			}
		}
	}
}
