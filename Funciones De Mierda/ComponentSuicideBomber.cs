using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentSuicideBomber : Component, IUpdateable
	{
		// Parámetros configurables
		public float ActivationRange = 3f;
		public float CountdownDuration = 5f;
		public string SparkSound = "Audio/Spark";
		public float ExplosionPressure = 10f;
		public bool IsIncendiary = true;
		public string TargetCategories = "LandPredator,LandOther,WaterPredator,WaterOther";

		// Referencias a subsistemas
		public SubsystemExplosions m_subsystemExplosions;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemTime m_subsystemTime;
		public SubsystemBodies m_subsystemBodies;

		// Referencias a componentes de la entidad
		public ComponentHealth m_componentHealth;
		public ComponentBody m_componentBody;
		public ComponentCreature m_componentCreature;

		// Estado interno
		public bool m_countdownActive = false;
		public double m_countdownStartTime = 0;
		public bool m_exploded = false;
		public float m_lastHealth = 0f;
		public bool m_isDying = false;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros con GUIDs específicos
			ActivationRange = valuesDictionary.GetValue<float>("ActivationRange", ActivationRange);
			CountdownDuration = valuesDictionary.GetValue<float>("CountdownDuration", CountdownDuration);
			SparkSound = valuesDictionary.GetValue<string>("SparkSound", SparkSound);
			ExplosionPressure = valuesDictionary.GetValue<float>("ExplosionPressure", ExplosionPressure);
			IsIncendiary = valuesDictionary.GetValue<bool>("IsIncendiary", IsIncendiary);
			TargetCategories = valuesDictionary.GetValue<string>("TargetCategories", TargetCategories);

			// Obtener referencias a subsistemas
			m_subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);

			// Obtener referencias a componentes
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);

			// Guardar la salud inicial
			if (m_componentHealth != null)
			{
				m_lastHealth = m_componentHealth.Health;
			}

			// Asegurarse de que los valores estén en rangos razonables
			ActivationRange = MathUtils.Clamp(ActivationRange, 1f, 20f);
			CountdownDuration = MathUtils.Clamp(CountdownDuration, 0.1f, 30f);
			ExplosionPressure = MathUtils.Clamp(ExplosionPressure, 1f, 1000f);
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);

			valuesDictionary.SetValue("ActivationRange", ActivationRange);
			valuesDictionary.SetValue("CountdownDuration", CountdownDuration);
			valuesDictionary.SetValue("SparkSound", SparkSound);
			valuesDictionary.SetValue("ExplosionPressure", ExplosionPressure);
			valuesDictionary.SetValue("IsIncendiary", IsIncendiary);
			valuesDictionary.SetValue("TargetCategories", TargetCategories);
		}

		public void Update(float dt)
		{
			// Verificar referencias nulas
			if (m_componentHealth == null || m_componentBody == null)
				return;

			// Si ya explotó, no hacer nada
			if (m_exploded)
				return;

			// Verificar si murió
			CheckForDeath();

			// Si el countdown está activo, actualizarlo
			if (m_countdownActive)
			{
				UpdateCountdown();
				return;
			}
		}

		public void CheckForDeath()
		{
			if (m_componentHealth == null)
				return;

			// Verificar si la salud llegó a 0 (muerte)
			if (m_componentHealth.Health <= 0 && !m_isDying)
			{
				m_isDying = true;
				StartDeathExplosion();
				return;
			}

			// Verificar si la salud disminuyó (recibió daño)
			// Esto es para detectar el momento exacto de la muerte
			if (m_componentHealth.Health < m_lastHealth && m_componentHealth.Health <= 0)
			{
				// Si no hemos iniciado la explosión y está muriendo
				if (!m_isDying && !m_countdownActive)
				{
					m_isDying = true;
					StartDeathExplosion();
				}
			}

			// Actualizar el registro de salud
			m_lastHealth = m_componentHealth.Health;
		}

		public void StartDeathExplosion()
		{
			if (m_countdownActive || m_exploded || m_isDying == false)
				return;

			m_countdownActive = true;
			m_countdownStartTime = m_subsystemTime.GameTime;

			// Reproducir sonido de chispa inicial
			if (m_subsystemAudio != null && !string.IsNullOrEmpty(SparkSound))
			{
				m_subsystemAudio.PlaySound(SparkSound, 1f, 0f, m_componentBody.Position, 10f, 0f);
			}

			// Si CountdownDuration es muy corto (<= 0.5 segundos), explotar inmediatamente
			if (CountdownDuration <= 0.5f)
			{
				Explode();
			}
		}

		public void UpdateCountdown()
		{
			if (m_subsystemTime == null || m_componentBody == null)
				return;

			double elapsedTime = m_subsystemTime.GameTime - m_countdownStartTime;

			// Reproducir sonido de chispa intermitente (cada segundo)
			if (m_subsystemAudio != null && elapsedTime % 1.0 < 0.1 && !string.IsNullOrEmpty(SparkSound))
			{
				float volume = 0.7f - (float)(elapsedTime / CountdownDuration) * 0.3f;
				m_subsystemAudio.PlaySound(SparkSound, volume, 0f, m_componentBody.Position, 8f, 0f);
			}

			// Verificar si el tiempo ha terminado
			if (elapsedTime >= CountdownDuration)
			{
				Explode();
				return;
			}
		}

		public void Explode()
		{
			if (m_exploded || m_componentBody == null || m_subsystemExplosions == null)
				return;

			m_exploded = true;
			m_countdownActive = false;

			// Crear la explosión en la posición actual
			int x = (int)MathUtils.Floor(m_componentBody.Position.X);
			int y = (int)MathUtils.Floor(m_componentBody.Position.Y);
			int z = (int)MathUtils.Floor(m_componentBody.Position.Z);

			m_subsystemExplosions.AddExplosion(x, y, z, ExplosionPressure, IsIncendiary, false);

			// Solo matar si aún no está muerto (por si acaso)
			if (m_componentHealth != null && m_componentHealth.Health > 0)
			{
				m_componentHealth.Die(new ExplosionInjury(ExplosionPressure));
			}
		}

		public override void OnEntityRemoved()
		{
			base.OnEntityRemoved();

			// Si la entidad fue removida sin explotar, explotar ahora
			// Esto cubre casos como ser eliminado por comandos o scripts
			if (!m_exploded && m_componentBody != null && m_subsystemExplosions != null)
			{
				// Explosión instantánea al ser removida
				int x = (int)MathUtils.Floor(m_componentBody.Position.X);
				int y = (int)MathUtils.Floor(m_componentBody.Position.Y);
				int z = (int)MathUtils.Floor(m_componentBody.Position.Z);

				m_subsystemExplosions.AddExplosion(x, y, z, ExplosionPressure, IsIncendiary, false);
			}
		}
	}
}
