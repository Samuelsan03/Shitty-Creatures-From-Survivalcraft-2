using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemGreenNightSky : Subsystem, IDrawable, IUpdateable
	{
		private bool m_isGreenNightActive;
		private int m_lastMoonPhase = -1;
		private bool m_hasNotifiedPlayers = false;

		public virtual bool IsGreenNightActive
		{
			get { return m_isGreenNightActive; }
			set
			{
				if (m_isGreenNightActive != value)
				{
					m_isGreenNightActive = value;
					m_hasNotifiedPlayers = false; // Reset notification flag
				}
			}
		}

		public int LastMoonPhase
		{
			get { return m_lastMoonPhase; }
			set { m_lastMoonPhase = value; }
		}

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public int[] DrawOrders => new int[] { -100, 5, 105 };

		private SubsystemSky m_subsystemSky;
		private SubsystemTimeOfDay m_subsystemTimeOfDay;
		private SubsystemPlayers m_subsystemPlayers;
		private Game.Random m_random = new Game.Random();

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);

			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemTimeOfDay = base.Project.FindSubsystem<SubsystemTimeOfDay>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);

			this.m_isGreenNightActive = valuesDictionary.GetValue<bool>("IsGreenNightActive", false);
			this.m_lastMoonPhase = valuesDictionary.GetValue<int>("LastMoonPhase", -1);
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			base.Save(valuesDictionary);
			valuesDictionary.SetValue<bool>("IsGreenNightActive", this.m_isGreenNightActive);
			valuesDictionary.SetValue<int>("LastMoonPhase", this.m_lastMoonPhase);
		}

		public virtual void Update(float dt)
		{
			this.UpdateGreenNight();
		}

		private void UpdateGreenNight()
		{
			float timeOfDay = this.m_subsystemTimeOfDay.TimeOfDay;

			// Definir horas de noche (entre atardecer y amanecer)
			bool isNightTime = false;
			if (timeOfDay >= this.m_subsystemTimeOfDay.DuskStart && timeOfDay < 1.0f)
			{
				isNightTime = true;
			}
			else if (timeOfDay >= 0.0f && timeOfDay < this.m_subsystemTimeOfDay.DawnStart)
			{
				isNightTime = true;
			}

			int currentMoonPhase = this.m_subsystemSky.MoonPhase;

			// Solo verificar cambio de fase lunar si no está activo
			if (!this.m_isGreenNightActive && currentMoonPhase != this.m_lastMoonPhase)
			{
				this.m_lastMoonPhase = currentMoonPhase;

				// Solo activar en luna llena (4) o nueva (0) durante la noche
				if ((currentMoonPhase == 0 || currentMoonPhase == 4) && isNightTime)
				{
					// 50% de probabilidad
					if (this.m_random.Float(0f, 1f) < 0.5f)
					{
						this.m_isGreenNightActive = true;
						m_hasNotifiedPlayers = false; // Reset flag when night starts
					}
				}
			}

			// Notificar a los jugadores cuando comienza la Noche Verde
			if (this.m_isGreenNightActive && !m_hasNotifiedPlayers)
			{
				this.NotifyPlayers("GreenMoonBegins");
				m_hasNotifiedPlayers = true;
			}

			// Desactivar al amanecer
			bool isDayTime = timeOfDay >= this.m_subsystemTimeOfDay.DawnStart &&
							 timeOfDay < this.m_subsystemTimeOfDay.DuskStart;

			if (this.m_isGreenNightActive && isDayTime)
			{
				this.m_isGreenNightActive = false;
			}
		}

		// Método público para que el ModLoader obtenga el color modificado
		public Color GetModifiedSkyColor(Color originalColor, Vector3 direction, float timeOfDay, int temperature)
		{
			if (!this.m_isGreenNightActive)
				return originalColor;

			// Verificar si es de noche
			bool isNightTime = false;
			if (timeOfDay >= this.m_subsystemTimeOfDay.DuskStart && timeOfDay < 1.0f)
			{
				isNightTime = true;
			}
			else if (timeOfDay >= 0.0f && timeOfDay < this.m_subsystemTimeOfDay.DawnStart)
			{
				isNightTime = true;
			}

			if (!isNightTime)
				return originalColor;

			// Verificar fase lunar (ya debería ser 0 o 4, pero verificamos por si acaso)
			int currentMoonPhase = this.m_subsystemSky.MoonPhase;
			if (currentMoonPhase != 0 && currentMoonPhase != 4)
				return originalColor;

			// Color verde: 0,204,102
			Color greenColor = new Color(0, 204, 102);

			// Calcular intensidad basada en la oscuridad
			float skyLightIntensity = this.m_subsystemSky.SkyLightIntensity;
			float darknessFactor = 1.0f - skyLightIntensity;

			// Solo aplicar cuando esté suficientemente oscuro
			if (darknessFactor > 0.3f)
			{
				float intensity = MathUtils.Saturate((darknessFactor - 0.3f) * 2f);
				return Color.Lerp(originalColor, greenColor, intensity * 0.7f);
			}

			return originalColor;
		}

		private void NotifyPlayers(string messageKey)
		{
			if (this.m_subsystemPlayers == null)
				return;

			foreach (ComponentPlayer player in this.m_subsystemPlayers.ComponentPlayers)
			{
				if (player != null && player.ComponentGui != null && player.PlayerData != null)
				{
					// Mensaje directo sin depender de LanguageControl
					string message = "¡Ha comenzado la Luna Verde!";

					player.ComponentGui.DisplaySmallMessage(
						message,
						Color.Green,
						false,
						true
					);

					// También mostrar en log para debugging
					Log.Information($"Green Night notification sent to player: {message}");
				}
			}
		}

		public void Draw(Camera camera, int drawOrder)
		{
			// No necesitamos dibujar nada, solo modificar el color
		}
	}
}
