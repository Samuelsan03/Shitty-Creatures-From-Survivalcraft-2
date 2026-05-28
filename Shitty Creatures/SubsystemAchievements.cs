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
		private HashSet<int> m_claimedRewards = new HashSet<int>();

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
			return m_claimedRewards.Contains(achievementNumber);
		}

		public void ClaimReward(int achievementNumber)
		{
			if (!IsAchievementUnlocked(achievementNumber))
				return;
			m_claimedRewards.Add(achievementNumber);
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			// Cargar logros desbloqueados
			for (int i = 1; i <= 100; i++)
			{
				string achievementId = valuesDictionary.GetValue<string>($"Achievement{i}", null);
				if (!string.IsNullOrEmpty(achievementId))
				{
					m_achievements[i] = achievementId;
				}
			}

			// Cargar recompensas reclamadas (compatibilidad con versiones anteriores)
			if (valuesDictionary.TryGetValue("ClaimedRewards", out object rawValue) && rawValue != null)
			{
				if (rawValue is int intValue)
				{
					// Versión antigua: se guardaba el máximo reclamado (ej: 3)
					// Convertir a conjunto con todos los números desde 1 hasta intValue
					for (int i = 1; i <= intValue; i++)
						m_claimedRewards.Add(i);
				}
				else if (rawValue is string strValue && !string.IsNullOrEmpty(strValue))
				{
					// Versión antigua: lista separada por comas (ej: "1,2,3")
					foreach (string part in strValue.Split(','))
					{
						if (int.TryParse(part, out int num))
							m_claimedRewards.Add(num);
					}
				}
				else if (rawValue is List<object> list)
				{
					// Formato futuro: lista de enteros
					foreach (var obj in list)
					{
						if (obj is int num)
							m_claimedRewards.Add(num);
					}
				}
			}
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			// Guardar logros desbloqueados
			foreach (var kvp in m_achievements)
			{
				valuesDictionary.SetValue<string>($"Achievement{kvp.Key}", kvp.Value);
			}

			// Guardar recompensas reclamadas como lista de enteros (string separado por comas)
			if (m_claimedRewards.Count > 0)
			{
				var sorted = m_claimedRewards.OrderBy(x => x).ToList();
				string claimedStr = string.Join(",", sorted);
				valuesDictionary.SetValue("ClaimedRewards", claimedStr);
			}
		}
	}
}
