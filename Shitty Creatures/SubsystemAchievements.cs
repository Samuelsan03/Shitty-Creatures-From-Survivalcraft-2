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
        private HashSet<int> m_rewardsClaimed = new HashSet<int>();  // Solo números de logros cuya recompensa ya fue reclamada

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
            return m_rewardsClaimed.Contains(achievementNumber);
        }

        public void ClaimReward(int achievementNumber)
        {
            if (!IsAchievementUnlocked(achievementNumber))
                return;
            m_rewardsClaimed.Add(achievementNumber);
        }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            // Cargar logros (formato: Achievement1="KillTank", Achievement2=...)
            for (int i = 1; i <= 100; i++)
            {
                string achievementId = valuesDictionary.GetValue<string>($"Achievement{i}", null);
                if (!string.IsNullOrEmpty(achievementId))
                {
                    m_achievements[i] = achievementId;
                }
            }

            // Cargar recompensas reclamadas (formato: ClaimedRewards="1,3,5")
            string claimedRewards = valuesDictionary.GetValue<string>("ClaimedRewards", null);
            if (!string.IsNullOrEmpty(claimedRewards))
            {
                foreach (string numStr in claimedRewards.Split(','))
                {
                    if (int.TryParse(numStr, out int num))
                        m_rewardsClaimed.Add(num);
                }
            }
        }

        public override void Save(ValuesDictionary valuesDictionary)
        {
            // Guardar logros
            foreach (var kvp in m_achievements)
            {
                valuesDictionary.SetValue<string>($"Achievement{kvp.Key}", kvp.Value);
            }

            // Guardar recompensas reclamadas como una cadena simple
            if (m_rewardsClaimed.Count > 0)
            {
                string claimed = string.Join(",", m_rewardsClaimed);
                valuesDictionary.SetValue<string>("ClaimedRewards", claimed);
            }
        }
    }
}
