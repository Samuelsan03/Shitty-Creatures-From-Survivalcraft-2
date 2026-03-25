using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class GreenNightSkyModLoader : ModLoader
	{
		public override void __ModInitialize()
		{
			// Registrar el hook para cambiar el color del cielo
			ModsManager.RegisterHook("ChangeSkyColor", this);
			// Registrar el hook para congelar el sueño
			ModsManager.RegisterHook("OnVitalStatsUpdateSleep", this);
		}

		// Hook para modificar el color del cielo
		public override Color ChangeSkyColor(Color oldColor, Vector3 direction, float timeOfDay, int temperature)
		{
			var greenNight = SubsystemGreenNightSky.Instance;
			if (greenNight != null && greenNight.IsGreenNightActive)
			{
				// Color verde (0, 50, 0) en formato RGBA (alpha = 255)
				return new Color(0, 50, 0);
			}
			return oldColor;
		}

		// Hook para congelar la barra de sueño durante la Noche Verde
		public override void OnVitalStatsUpdateSleep(ComponentVitalStats vitalStats, ref float sleep, ref float gameTimeDelta, ref bool skipVanilla)
		{
			var greenNight = SubsystemGreenNightSky.Instance;
			if (greenNight != null && greenNight.IsGreenNightActive)
			{
				// Impedir que el sueño se modifique
				skipVanilla = true;
				// Opcional: mantener el valor actual (sleep ya contiene el valor actual)
				// sleep = vitalStats.Sleep; // No es necesario porque skipVanilla evita que se ejecute el código original
			}
		}
	}
}
