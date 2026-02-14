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
			// Registrar el hook para cambiar el color del cielo
			ModsManager.RegisterHook("ChangeSkyColor", this);
			// También puedes registrar otros hooks si son necesarios
		}

		public override void OnProjectLoaded(Project project)
		{
			// Obtener referencia al subsistema de noche verde (opcional, pero útil)
			m_subsystemGreenNightSky = project.FindSubsystem<SubsystemGreenNightSky>(true);
			// Si no existe, podrías crearlo y agregarlo al proyecto, pero normalmente ya debería estar en la base de datos.
		}

		public override Color ChangeSkyColor(Color oldColor, Vector3 direction, float timeOfDay, int temperature)
		{
			// Usar la instancia estática o la referencia guardada
			var instance = SubsystemGreenNightSky.Instance; // o m_subsystemGreenNightSky
			if (instance != null && instance.IsGreenNightActive)
			{
				if (instance.m_subsystemTimeOfDay != null)
				{
					float duskStart = instance.m_subsystemTimeOfDay.DuskStart;
					float dawnStart = instance.m_subsystemTimeOfDay.DawnStart;
					if (IntervalUtils.IsBetween(timeOfDay, duskStart, dawnStart))
					{
						if (instance.m_subsystemSky != null && (instance.m_subsystemSky.MoonPhase == 0 || instance.m_subsystemSky.MoonPhase == 4))
						{
							Color greenColor = new Color(0, 50, 0);
							float factor = 1f - instance.m_subsystemSky.SkyLightIntensity;
							factor = MathUtils.Saturate(factor * 2f);
							return Color.Lerp(oldColor, greenColor, factor);
						}
					}
				}
			}
			return oldColor;
		}

		// El método SubsystemUpdate puede permanecer igual o eliminarse si no se usa
	}
}
