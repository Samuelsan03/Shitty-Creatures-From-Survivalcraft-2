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
		private int m_highestClaimedReward = 0;

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

		public bool IsRewardClaimed(int achievementNumber)
		{
			return achievementNumber <= m_highestClaimedReward;
		}

		public void ClaimReward(int achievementNumber)
		{
			if (!IsAchievementUnlocked(achievementNumber))
				return;
			if (achievementNumber > m_highestClaimedReward)
			{
				m_highestClaimedReward = achievementNumber;
			}
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

			// Leer ClaimedRewards con compatibilidad hacia atrás
			// Mundo nuevo: guarda un int directamente (ej: 3)
			// Mundo viejo: guardaba un string (ej: "1,2,3")
			object rawValue;
			if (valuesDictionary.TryGetValue("ClaimedRewards", out rawValue) && rawValue != null)
			{
				if (rawValue is int intValue)
				{
					m_highestClaimedReward = intValue;
				}
				else if (rawValue is string strValue && !string.IsNullOrEmpty(strValue))
				{
					// Formato viejo: "1,2,3" -> tomar el mayor
					int max = 0;
					foreach (string part in strValue.Split(','))
					{
						int num;
						if (int.TryParse(part, out num) && num > max)
						{
							max = num;
						}
					}
					m_highestClaimedReward = max;
				}
			}
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			foreach (var kvp in m_achievements)
			{
				valuesDictionary.SetValue<string>($"Achievement{kvp.Key}", kvp.Value);
			}

			if (m_highestClaimedReward > 0)
			{
				valuesDictionary.SetValue<int>("ClaimedRewards", m_highestClaimedReward);
			}
		}
	}
}
