using System;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x020000BA RID: 186
	public class SubsystemGreenNightSky : Subsystem, IUpdateable
	{
		// Token: 0x17000093 RID: 147
		// (get) Token: 0x06000745 RID: 1861 RVA: 0x00052BB8 File Offset: 0x00050DB8
		// (set) Token: 0x06000746 RID: 1862 RVA: 0x00052BC0 File Offset: 0x00050DC0
		public virtual bool IsGreenNightActive { get; set; }

		// Token: 0x17000094 RID: 148
		// (get) Token: 0x06000747 RID: 1863 RVA: 0x00052BC9 File Offset: 0x00050DC9
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x17000095 RID: 149
		// (get) Token: 0x06000748 RID: 1864 RVA: 0x00052BCC File Offset: 0x00050DCC
		// (set) Token: 0x06000749 RID: 1865 RVA: 0x00052BD4 File Offset: 0x00050DD4
		public bool HasRolledTonight { get; set; }

		// Token: 0x17000096 RID: 150
		// (get) Token: 0x0600074A RID: 1866 RVA: 0x00052BDD File Offset: 0x00050DDD
		// (set) Token: 0x0600074B RID: 1867 RVA: 0x00052BE5 File Offset: 0x00050DE5
		public double LastCheckedDay { get; set; }

		// Token: 0x17000097 RID: 151
		// (get) Token: 0x0600074C RID: 1868 RVA: 0x00052BEE File Offset: 0x00050DEE
		// (set) Token: 0x0600074D RID: 1869 RVA: 0x00052BF6 File Offset: 0x00050DF6
		public int DaysSinceLastGreenNight { get; set; }

		// Token: 0x17000098 RID: 152
		// (get) Token: 0x0600074E RID: 1870 RVA: 0x00052BFF File Offset: 0x00050DFF
		// (set) Token: 0x0600074F RID: 1871 RVA: 0x00052C07 File Offset: 0x00050E07
		public float GreenNightChance { get; set; } = 0.5f;

		// Token: 0x17000099 RID: 153
		// (get) Token: 0x06000750 RID: 1872 RVA: 0x00052C10 File Offset: 0x00050E10
		// (set) Token: 0x06000751 RID: 1873 RVA: 0x00052C17 File Offset: 0x00050E17
		public static SubsystemGreenNightSky Instance { get; private set; }

		// Token: 0x06000752 RID: 1874 RVA: 0x00052C1F File Offset: 0x00050E1F
		public virtual void Update(float dt)
		{
			this.UpdateGreenNight();
		}

		// Token: 0x06000753 RID: 1875 RVA: 0x00052C2C File Offset: 0x00050E2C
		public virtual void UpdateGreenNight()
		{
			double day = this.m_subsystemTimeOfDay.Day;
			float timeOfDay = this.m_subsystemTimeOfDay.TimeOfDay;
			bool flag = Math.Floor(day) > Math.Floor(this.LastCheckedDay);
			bool flag2 = flag;
			if (flag2)
			{
				this.LastCheckedDay = day;
				int daysSinceLastGreenNight = this.DaysSinceLastGreenNight;
				this.DaysSinceLastGreenNight = daysSinceLastGreenNight + 1;
			}
			bool flag3 = IntervalUtils.IsBetween(timeOfDay, this.m_subsystemTimeOfDay.DuskStart, this.m_subsystemTimeOfDay.NightStart) || IntervalUtils.IsBetween(timeOfDay, this.m_subsystemTimeOfDay.NightStart, this.m_subsystemTimeOfDay.DawnStart);
			bool flag4 = IntervalUtils.IsBetween(timeOfDay, this.m_subsystemTimeOfDay.DawnStart, this.m_subsystemTimeOfDay.DayStart) || IntervalUtils.IsBetween(timeOfDay, this.m_subsystemTimeOfDay.DayStart, this.m_subsystemTimeOfDay.DuskStart);
			bool isGreenNightActive = this.IsGreenNightActive;
			bool flag5 = !this.IsGreenNightActive && (this.m_subsystemSky.MoonPhase == 0 || this.m_subsystemSky.MoonPhase == 4);
			if (flag5)
			{
				bool flag6 = flag3 && !this.HasRolledTonight && this.DaysSinceLastGreenNight >= 1;
				if (flag6)
				{
					this.HasRolledTonight = true;
					float num = Math.Min(1f, this.GreenNightChance * (float)this.DaysSinceLastGreenNight);
					float num2 = this.m_random.Float(0f, 1f);
					bool flag7 = num2 < num;
					if (flag7)
					{
						this.IsGreenNightActive = true;
						this.DaysSinceLastGreenNight = 0;
						SubsystemPlayers subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
						bool flag8 = subsystemPlayers != null;
						if (flag8)
						{
							foreach (ComponentPlayer componentPlayer in subsystemPlayers.ComponentPlayers)
							{
								bool flag9 = ((componentPlayer != null) ? componentPlayer.ComponentGui : null) != null;
								if (flag9)
								{
									componentPlayer.ComponentGui.DisplaySmallMessage(LanguageControl.Get("GreenNightSky", "GreenMoonBegins"), new Color(0, 255, 0), false, true);
								}
							}
						}
					}
				}
			}
			bool flag10 = this.IsGreenNightActive && flag4;
			if (flag10)
			{
				this.IsGreenNightActive = false;
			}
			bool flag11 = this.HasRolledTonight && flag4;
			if (flag11)
			{
				this.HasRolledTonight = false;
			}
			bool flag12 = this.m_subsystemSky.ViewUnderWaterDepth > 0f || this.m_subsystemSky.ViewUnderMagmaDepth > 0f;
			if (flag12)
			{
				this.IsGreenNightActive = false;
			}
		}

		// Token: 0x06000754 RID: 1876 RVA: 0x00052EDC File Offset: 0x000510DC
		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemTimeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
			SubsystemGreenNightSky.Instance = this;
		}

		// Token: 0x0400063F RID: 1599
		public SubsystemSky m_subsystemSky;

		// Token: 0x04000640 RID: 1600
		public SubsystemGameInfo m_subsystemGameInfo;

		// Token: 0x04000641 RID: 1601
		public SubsystemTimeOfDay m_subsystemTimeOfDay;

		// Token: 0x04000642 RID: 1602
		private Random m_random = new Random();
	}
}
