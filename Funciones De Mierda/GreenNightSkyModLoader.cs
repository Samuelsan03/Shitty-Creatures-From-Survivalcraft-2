using System;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;

namespace Game
{
	// Token: 0x02000058 RID: 88
	public class GreenNightSkyModLoader : ModLoader
	{
		// Token: 0x060004D6 RID: 1238 RVA: 0x000401C0 File Offset: 0x0003E3C0
		public override Color ChangeSkyColor(Color oldColor, Vector3 direction, float timeOfDay, int temperature)
		{
			SubsystemGreenNightSky instance = SubsystemGreenNightSky.Instance;
			bool flag = instance != null && instance.IsGreenNightActive;
			if (flag)
			{
				bool flag2 = instance.m_subsystemTimeOfDay != null;
				if (flag2)
				{
					float duskStart = instance.m_subsystemTimeOfDay.DuskStart;
					float dawnStart = instance.m_subsystemTimeOfDay.DawnStart;
					bool flag3 = IntervalUtils.IsBetween(timeOfDay, duskStart, dawnStart);
					if (flag3)
					{
						bool flag4 = instance.m_subsystemSky != null && (instance.m_subsystemSky.MoonPhase == 0 || instance.m_subsystemSky.MoonPhase == 4);
						if (flag4)
						{
							Color c = new Color(0, 204, 102);
							float num = 1f - instance.m_subsystemSky.SkyLightIntensity;
							num = MathUtils.Saturate(num * 2f);
							return Color.Lerp(oldColor, c, num);
						}
					}
				}
			}
			return oldColor;
		}

		// Token: 0x060004D7 RID: 1239 RVA: 0x0004029C File Offset: 0x0003E49C
		public override void SubsystemUpdate(SubsystemUpdate subsystemUpdate, float dt)
		{
			Project project = subsystemUpdate.Project;
			SubsystemGreenNightSky subsystemGreenNightSky = (project != null) ? project.FindSubsystem<SubsystemGreenNightSky>() : null;
			bool flag = subsystemGreenNightSky != null;
			if (flag)
			{
			}
		}
	}
}
