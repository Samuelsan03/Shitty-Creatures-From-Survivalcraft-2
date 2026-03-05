using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemGreenNightSky : Subsystem, IUpdateable
	{
		// Evento para notificar el fin natural de la noche
		public event Action NaturalNightEnded;

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
						// Al activar, si es de noche y la luna es adecuada, comenzar inmediatamente
						if (m_subsystemTimeOfDay != null && m_subsystemSky != null)
						{
							float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
							// Es de noche si la hora es posterior al anochecer (DuskStart) o anterior al amanecer (DawnStart)
							bool isNight = timeOfDay >= m_subsystemTimeOfDay.DuskStart || timeOfDay < m_subsystemTimeOfDay.DawnStart;
							if (isNight && (m_subsystemSky.MoonPhase == 0 || m_subsystemSky.MoonPhase == 4))
							{
								IsGreenNightActive = true;
								HasRolledTonight = true;      // Evita que se intente activar de nuevo al anochecer
								DaysSinceLastGreenNight = 0;  // Reinicia el contador de días
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
		public float GreenNightChance { get; set; } = 1f;  // CAMBIADO: siempre 1 para que ocurra cada luna llena/nueva

		public static SubsystemGreenNightSky Instance { get; set; }

		public SubsystemSky m_subsystemSky;
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemTimeOfDay m_subsystemTimeOfDay;
		private Random m_random = new Random();

		public virtual void Update(float dt)
		{
			if (!GreenNightEnabled) return;
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

			if (!this.IsGreenNightActive && (this.m_subsystemSky.MoonPhase == 0 || this.m_subsystemSky.MoonPhase == 4))
			{
				// MODIFICADO: Eliminada la condición DaysSinceLastGreenNight >= 1
				if (isStartMoment && !this.HasRolledTonight)
				{
					this.HasRolledTonight = true;
					// Siempre activar (probabilidad 1)
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
									new Color(5, 154, 0), false, true);
							}
						}
					}
				}
			}

			if (this.IsGreenNightActive && isEndMoment)
			{
				this.IsGreenNightActive = false;

				// Disparar el evento de fin natural
				NaturalNightEnded?.Invoke();

				SubsystemPlayers subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
				if (subsystemPlayers != null)
				{
					foreach (ComponentPlayer componentPlayer in subsystemPlayers.ComponentPlayers)
					{
						if (componentPlayer?.ComponentGui != null)
						{
							componentPlayer.ComponentGui.DisplaySmallMessage(
								LanguageControl.Get("GreenNightSky", "GreenMoonEnds"),
								new Color(5, 154, 0), false, true);
						}
					}
				}
			}

			if (this.IsGreenNightActive && timeOfDay > this.m_subsystemTimeOfDay.DawnStart + 0.1f)
			{
				if (this.m_subsystemSky.MoonPhase != 0 && this.m_subsystemSky.MoonPhase != 4)
				{
					this.IsGreenNightActive = false;
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

			SubsystemGreenNightSky.Instance = this;
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			valuesDictionary.SetValue<bool>("IsGreenNightActive", this.IsGreenNightActive);
			valuesDictionary.SetValue<bool>("HasRolledTonight", this.HasRolledTonight);
			valuesDictionary.SetValue<double>("LastCheckedDay", this.LastCheckedDay);
			valuesDictionary.SetValue<int>("DaysSinceLastGreenNight", this.DaysSinceLastGreenNight);
			valuesDictionary.SetValue<bool>("GreenNightEnabled", this.GreenNightEnabled);
		}
	}
}
