using System;
using Engine;
using GameEntitySystem;

namespace Game
{
	public class GreenNightSkyModLoader : ModLoader
	{
		private SubsystemGreenNightSky m_subsystemGreenNightSky;

		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("ChangeSkyColor", this);
		}

		public override void OnProjectLoaded(Project project)
		{
			m_subsystemGreenNightSky = project.FindSubsystem<SubsystemGreenNightSky>(true);
		}

		public override Color ChangeSkyColor(Color oldColor, Vector3 direction, float timeOfDay, int temperature)
		{
			var instance = SubsystemGreenNightSky.Instance;
			if (instance != null && instance.IsGreenNightActive)
			{
				// El efecto verde se aplica mientras esté activo
				// (desde Middusk hasta Middawn)
				Color greenColor = new Color(0, 50, 0);
				float factor = 1f - instance.m_subsystemSky.SkyLightIntensity;
				factor = MathUtils.Saturate(factor * 2f);
				return Color.Lerp(oldColor, greenColor, factor);
			}
			return oldColor;
		}
	}
}
