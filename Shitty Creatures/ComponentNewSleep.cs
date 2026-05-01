using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewSleep : ComponentSleep, IUpdateable
	{
		public Dictionary<string, Func<string>> m_conditionsToSleep = new Dictionary<string, Func<string>>();
		public SubsystemPlayers m_subsystemPlayers;
		public SubsystemSky m_subsystemSky;
		public SubsystemTime m_subsystemTime;
		public SubsystemUpdate m_subsystemUpdate;
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemTimeOfDay m_subsystemTimeOfDay;
		public SubsystemTerrain m_subsystemTerrain;
		public ComponentPlayer m_componentPlayer;
		public SubsystemGreenNightSky m_subsystemGreenNightSky;

		public float m_sleepFactor;
		public bool m_allowManualWakeUp;
		public static string fName = "ComponentSleep";
		public float m_minWetness;
		public float m_messageFactor;
		public double m_minAutoSleepTime = 180.0;
		public bool m_wakeUpWhenWet = true;
		public bool m_wakeUpWhenInjured = true;
		public bool m_wakeUpWhenAttacked = true;
		public float MaxSleepBlackoutFactor = 1f;

		public new bool IsSleeping => base.IsSleeping;
		public float SleepFactor => m_sleepFactor;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public double GetCurrentSleepDuration()
		{
			if (base.IsSleeping)
				return m_subsystemGameInfo.TotalElapsedGameTime - base.m_sleepStartTime.Value;
			return 0.0;
		}

		public bool CanSleep(out string reason)
		{
			foreach (string key in m_conditionsToSleep.Keys)
			{
				string text = m_conditionsToSleep[key]();
				if (!string.IsNullOrEmpty(text))
				{
					reason = text;
					return false;
				}
			}
			reason = string.Empty;
			return true;
		}

		public override void Sleep(bool allowManualWakeup)
		{
			string reason;
			if (!CanSleep(out reason))
			{
				m_componentPlayer.ComponentGui.DisplaySmallMessage(reason, Color.Yellow, true, true);
				return;
			}

			if (!base.IsSleeping)
			{
				base.Sleep(allowManualWakeup);
				// Nuestras variables adicionales
				m_allowManualWakeUp = allowManualWakeup;
				m_minWetness = float.MaxValue;
				m_messageFactor = 0f;
				if (m_componentPlayer.PlayerStats != null)
					m_componentPlayer.PlayerStats.TimesWentToSleep++;
			}
		}

		public override void WakeUp()
		{
			if (base.IsSleeping)
				base.WakeUp();
		}

		public override void Update(float dt)
		{
			// Lógica personalizada: despertar si la Noche Verde está activa
			if (m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive && base.IsSleeping && GetCurrentSleepDuration() > 1.0)
			{
				WakeUp();
				m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + 1.0, delegate
				{
					string msg = LanguageControl.Get("ComponentNewSleep", "CantSleepGreenNight");
					if (string.IsNullOrEmpty(msg)) msg = "Cannot sleep during Green Night";
					m_componentPlayer.ComponentGui.DisplaySmallMessage(msg, Color.Yellow, false, true);
				});
			}

			// Llamar a la implementación base para mantener toda la lógica original
			base.Update(dt);
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_componentPlayer = Entity.FindComponent<ComponentPlayer>(true);
			m_subsystemGreenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>(true);

			m_allowManualWakeUp = valuesDictionary.GetValue<bool>("AllowManualWakeUp");

			ComponentBody componentBody = m_componentPlayer.ComponentBody;
			componentBody.Attacked = (Action<Attackment>)Delegate.Combine(componentBody.Attacked, new Action<Attackment>(delegate (Attackment attackment)
			{
				if (m_wakeUpWhenAttacked && base.IsSleeping && m_componentPlayer.ComponentVitalStats.Sleep > 0.25f)
					WakeUp();
			}));

			// Condiciones para dormir (incluyendo Noche Verde)
			m_conditionsToSleep["OnDryLand"] = delegate
			{
				if (m_componentPlayer.ComponentBody.StandingOnValue == null || BlocksManager.Blocks[Terrain.ExtractContents(m_componentPlayer.ComponentBody.StandingOnValue.Value)] == null || m_componentPlayer.ComponentBody.ImmersionDepth > 0f)
					return LanguageControl.Get(ComponentSleep.fName, 1);
				return string.Empty;
			};

			m_conditionsToSleep["BlockIsComfortable"] = delegate
			{
				if (m_componentPlayer.ComponentBody.StandingOnValue != null)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(m_componentPlayer.ComponentBody.StandingOnValue.Value)];
					if (block != null && block.SleepSuitability == 0f)
						return LanguageControl.Get(ComponentSleep.fName, 2);
				}
				return string.Empty;
			};

			m_conditionsToSleep["TiredEnough"] = delegate
			{
				if (m_componentPlayer.ComponentVitalStats.Sleep > 0.99f)
					return LanguageControl.Get(ComponentSleep.fName, 3);
				return string.Empty;
			};

			m_conditionsToSleep["NotTooWet"] = delegate
			{
				if (m_componentPlayer.ComponentVitalStats.Wetness > 0.95f)
					return LanguageControl.Get(ComponentSleep.fName, 4);
				return string.Empty;
			};

			m_conditionsToSleep["Ceiling"] = delegate
			{
				for (int i = -1; i <= 1; i++)
				{
					for (int j = -1; j <= 1; j++)
					{
						Vector3 start = m_componentPlayer.ComponentBody.Position + new Vector3(i, 1f, j);
						Vector3 end = new Vector3(start.X, 255f, start.Z);
						if (m_subsystemTerrain.Raycast(start, end, false, true, (int value, float _) => Terrain.ExtractContents(value) != 0) == null)
							return LanguageControl.Get(ComponentSleep.fName, 5);
					}
				}
				return string.Empty;
			};

			m_conditionsToSleep["GreenNight"] = delegate
			{
				if (m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive)
				{
					string msg = LanguageControl.Get("ComponentNewSleep", "CantSleepGreenNight");
					if (string.IsNullOrEmpty(msg)) msg = "Cannot sleep during Green Night";
					return msg;
				}
				return string.Empty;
			};
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			valuesDictionary.SetValue("AllowManualWakeUp", m_allowManualWakeUp);
		}
	}
}
