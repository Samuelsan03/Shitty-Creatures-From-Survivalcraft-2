using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemGreenNightSky : Subsystem, IUpdateable
	{
		public virtual bool IsGreenNightActive { get; set; }

		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

		public bool HasRolledTonight { get; set; }
		public double LastCheckedDay { get; set; }
		public int DaysSinceLastGreenNight { get; set; }
		public float GreenNightChance { get; set; } = 0.5f;

		public static SubsystemGreenNightSky Instance { get; private set; }

		public SubsystemSky m_subsystemSky;
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemTimeOfDay m_subsystemTimeOfDay;
		private Random m_random = new Random();

		public virtual void Update(float dt)
		{
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

			bool flag3 = IntervalUtils.IsBetween(timeOfDay, this.m_subsystemTimeOfDay.DuskStart, this.m_subsystemTimeOfDay.NightStart) ||
						IntervalUtils.IsBetween(timeOfDay, this.m_subsystemTimeOfDay.NightStart, this.m_subsystemTimeOfDay.DawnStart);
			bool flag4 = IntervalUtils.IsBetween(timeOfDay, this.m_subsystemTimeOfDay.DawnStart, this.m_subsystemTimeOfDay.DayStart) ||
						IntervalUtils.IsBetween(timeOfDay, this.m_subsystemTimeOfDay.DayStart, this.m_subsystemTimeOfDay.DuskStart);

			if (!this.IsGreenNightActive && (this.m_subsystemSky.MoonPhase == 0 || this.m_subsystemSky.MoonPhase == 4))
			{
				if (flag3 && !this.HasRolledTonight && this.DaysSinceLastGreenNight >= 1)
				{
					this.HasRolledTonight = true;
					float num = Math.Min(1f, this.GreenNightChance * (float)this.DaysSinceLastGreenNight);
					float num2 = this.m_random.Float(0f, 1f);

					if (num2 < num)
					{
						this.IsGreenNightActive = true;
						this.DaysSinceLastGreenNight = 0;

						SubsystemPlayers subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
						if (subsystemPlayers != null)
						{
							foreach (ComponentPlayer componentPlayer in subsystemPlayers.ComponentPlayers)
							{
								if (componentPlayer?.ComponentGui != null)
								{
									componentPlayer.ComponentGui.DisplaySmallMessage(
										LanguageControl.Get("GreenNightSky", "GreenMoonBegins"),
										new Color(0, 255, 0), false, true);
								}
							}
						}
					}
				}
			}

			if (this.IsGreenNightActive && flag4)
			{
				this.IsGreenNightActive = false;
			}

			if (this.HasRolledTonight && flag4)
			{
				this.HasRolledTonight = false;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemTimeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);

			// Cargar los valores guardados del mundo
			this.IsGreenNightActive = valuesDictionary.GetValue<bool>("IsGreenNightActive");
			this.HasRolledTonight = valuesDictionary.GetValue<bool>("HasRolledTonight");
			this.LastCheckedDay = valuesDictionary.GetValue<double>("LastCheckedDay");
			this.DaysSinceLastGreenNight = valuesDictionary.GetValue<int>("DaysSinceLastGreenNight");

			SubsystemGreenNightSky.Instance = this;
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			// Guardar los valores actuales
			valuesDictionary.SetValue<bool>("IsGreenNightActive", this.IsGreenNightActive);
			valuesDictionary.SetValue<bool>("HasRolledTonight", this.HasRolledTonight);
			valuesDictionary.SetValue<double>("LastCheckedDay", this.LastCheckedDay);
			valuesDictionary.SetValue<int>("DaysSinceLastGreenNight", this.DaysSinceLastGreenNight);
		}
	}
}
