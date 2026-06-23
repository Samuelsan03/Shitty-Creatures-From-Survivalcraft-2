using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemGreenNightSky : Subsystem, IUpdateable
	{
		public event Action NaturalNightEnded;
		public event Action GreenNightStarted;
		public int GreenNightIntervalDays { get; set; } = 4;
		public DifficultyMode DifficultyMode { get; set; } = DifficultyMode.Normal;

		public virtual bool IsGreenNightActive { get; set; }
		private bool m_greenNightEnabled = true;
		public virtual bool GreenNightEnabled
		{
			get { return m_greenNightEnabled; }
			set
			{
				if (m_greenNightEnabled != value)
				{
					m_greenNightEnabled = value;
					if (!m_greenNightEnabled)
					{
						IsGreenNightActive = false;
					}
					else
					{
						if (m_subsystemTimeOfDay != null)
						{
							float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
							bool isNight = timeOfDay >= m_subsystemTimeOfDay.DuskStart || timeOfDay < m_subsystemTimeOfDay.DawnStart;
							if (isNight && this.DaysSinceLastGreenNight >= this.GreenNightIntervalDays)
							{
								IsGreenNightActive = true;
								HasRolledTonight = true;
								DaysSinceLastGreenNight = 0;
							}
						}
					}
				}
			}
		}

		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

		public bool HasRolledTonight { get; set; }
		public double LastCheckedDay { get; set; }
		public int DaysSinceLastGreenNight { get; set; }

		public static SubsystemGreenNightSky Instance { get; set; }

		public SubsystemSky m_subsystemSky;
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemTimeOfDay m_subsystemTimeOfDay;
		private Random m_random = new Random();

		private string GetLocalizedMessage(string key)
		{
			return LanguageControl.Get("GreenNightSky", key);
		}

		public virtual void Update(float dt)
		{
			if (!GreenNightEnabled) return;

			if (m_subsystemGameInfo.WorldSettings.TimeOfDayMode != TimeOfDayMode.Changing)
			{
				if (IsGreenNightActive)
				{
					IsGreenNightActive = false;
				}
				return;
			}

			if (AchievementsManager.IsCelebrationActive)
			{
				if (IsGreenNightActive)
				{
					IsGreenNightActive = false;
				}
				return;
			}

			this.UpdateGreenNight();
		}

		public virtual void UpdateGreenNight()
		{
			double day = this.m_subsystemTimeOfDay.Day;
			float timeOfDay = this.m_subsystemTimeOfDay.TimeOfDay;

			bool flag = Math.Floor(day) > Math.Floor(this.LastCheckedDay);
			if (flag)
			{
				this.LastCheckedDay = day;
				this.DaysSinceLastGreenNight++;
			}

			float middusk = this.m_subsystemTimeOfDay.Middusk;
			float duskTolerance = 0.005f;
			float middawn = this.m_subsystemTimeOfDay.Middawn;
			float dawnTolerance = 0.005f;

			bool isStartMoment = Math.Abs(timeOfDay - middusk) < duskTolerance;
			bool isEndMoment = Math.Abs(timeOfDay - middawn) < dawnTolerance;

			if (!this.IsGreenNightActive && this.DaysSinceLastGreenNight >= this.GreenNightIntervalDays)
			{
				if (isStartMoment && !this.HasRolledTonight)
				{
					this.HasRolledTonight = true;
					this.DaysSinceLastGreenNight = 0;

					if (!AchievementsManager.IsCelebrationActive)
					{
						this.IsGreenNightActive = true;

						GreenNightStarted?.Invoke();

						SubsystemPlayers subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
						if (subsystemPlayers != null)
						{
							foreach (ComponentPlayer componentPlayer in subsystemPlayers.ComponentPlayers)
							{
								if (componentPlayer?.ComponentGui != null)
								{
									string message = GetLocalizedMessage("GreenMoonBegins");
									componentPlayer.ComponentGui.DisplaySmallMessage(message, new Color(5, 154, 0), false, true);
								}
							}
						}
					}
				}
			}

			if (this.IsGreenNightActive && isEndMoment)
			{
				this.IsGreenNightActive = false;
				NaturalNightEnded?.Invoke();

				SubsystemPlayers subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
				if (subsystemPlayers != null)
				{
					foreach (ComponentPlayer componentPlayer in subsystemPlayers.ComponentPlayers)
					{
						if (componentPlayer?.ComponentGui != null)
						{
							string message = GetLocalizedMessage("GreenMoonEnds");
							componentPlayer.ComponentGui.DisplaySmallMessage(message, new Color(5, 154, 0), false, true);
						}
					}
				}
			}

			if (this.HasRolledTonight && isEndMoment)
			{
				this.HasRolledTonight = false;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemTimeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);

			this.IsGreenNightActive = valuesDictionary.GetValue<bool>("IsGreenNightActive");
			this.HasRolledTonight = valuesDictionary.GetValue<bool>("HasRolledTonight");
			this.LastCheckedDay = valuesDictionary.GetValue<double>("LastCheckedDay");
			this.DaysSinceLastGreenNight = valuesDictionary.GetValue<int>("DaysSinceLastGreenNight");
			this.GreenNightEnabled = valuesDictionary.GetValue<bool>("GreenNightEnabled", true);
			this.GreenNightIntervalDays = valuesDictionary.GetValue<int>("GreenNightIntervalDays", 4);
			this.DifficultyMode = (DifficultyMode)valuesDictionary.GetValue<int>("DifficultyMode", 2);

			// Sincronizar LastCheckedDay con el día actual al cargar.
			// Evita que el primer Update() detecte un cambio de día fantasma
			// (por desfase de double o timing de carga) e incremente el contador.
			double currentDay = this.m_subsystemTimeOfDay.Day;
			if (Math.Floor(this.LastCheckedDay) < Math.Floor(currentDay))
			{
				this.LastCheckedDay = currentDay;
			}

			SubsystemGreenNightSky.Instance = this;
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			valuesDictionary.SetValue<bool>("IsGreenNightActive", this.IsGreenNightActive);
			valuesDictionary.SetValue<bool>("HasRolledTonight", this.HasRolledTonight);
			valuesDictionary.SetValue<double>("LastCheckedDay", this.LastCheckedDay);
			valuesDictionary.SetValue<int>("DaysSinceLastGreenNight", this.DaysSinceLastGreenNight);
			valuesDictionary.SetValue<bool>("GreenNightEnabled", this.GreenNightEnabled);
			valuesDictionary.SetValue<int>("GreenNightIntervalDays", this.GreenNightIntervalDays);
			valuesDictionary.SetValue<int>("DifficultyMode", (int)this.DifficultyMode);
		}

		public static class DifficultyModifiers
		{
			public static float GetAggressionRangeMultiplier(DifficultyMode mode)
			{
				switch (mode)
				{
					case DifficultyMode.VeryEasy: return 0.5f;
					case DifficultyMode.Easy: return 0.7f;
					case DifficultyMode.Normal: return 1.0f;
					case DifficultyMode.Medium: return 1.2f;
					case DifficultyMode.Hard: return 1.5f;
					case DifficultyMode.Extreme: return 2.0f;
					default: return 1.0f;
				}
			}

			public static bool ShouldUseFlanking(DifficultyMode mode)
			{
				return mode == DifficultyMode.Hard || mode == DifficultyMode.Extreme;
			}

			public static bool ShouldAlwaysCallHelp(DifficultyMode mode)
			{
				return mode >= DifficultyMode.Medium;
			}

			public static float GetHelpCallRangeMultiplier(DifficultyMode mode)
			{
				switch (mode)
				{
					case DifficultyMode.VeryEasy: return 0.3f;
					case DifficultyMode.Easy: return 0.5f;
					case DifficultyMode.Normal: return 1.0f;
					case DifficultyMode.Medium: return 1.2f;
					case DifficultyMode.Hard: return 1.5f;
					case DifficultyMode.Extreme: return 2.0f;
					default: return 1.0f;
				}
			}

			public static bool IsChasePersistent(DifficultyMode mode)
			{
				return mode == DifficultyMode.Extreme;
			}
		}
	}
}
