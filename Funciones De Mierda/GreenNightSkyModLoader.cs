using System;
using Engine;
using GameEntitySystem;

namespace Game
{
	public class GreenNightSkyModLoader : ModLoader
	{
		private SubsystemGreenNightSky m_subsystem;

		public override void OnProjectLoaded(Project project)
		{
			// Buscar nuestro Subsystem en el proyecto
			this.m_subsystem = project.FindSubsystem<SubsystemGreenNightSky>();

			// Si no existe, el proyecto debe crearlo automáticamente
			// No lo creamos aquí para evitar problemas
		}

		public override Color ChangeSkyColor(Color oldColor, Vector3 direction, float timeOfDay, int temperature)
		{
			if (this.m_subsystem != null)
			{
				return this.m_subsystem.GetModifiedSkyColor(oldColor, direction, timeOfDay, temperature);
			}

			// Si el subsistema no está disponible aún, regresar color original
			// Esto puede pasar durante la carga inicial
			return oldColor;
		}
	}
}
