using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBoomerFrozenExplosion : Component, IUpdateable
	{
		public float FreezePressure = 50f;
		public float FluDuration = 300f;
		public bool NoExplosionSound = false;
		public bool PreventExplosion = false;

		private SubsystemFreezeExplosions m_subsystemFreezeExplosions;
		private SubsystemAudio m_subsystemAudio;
		private ComponentHealth m_componentHealth;
		private ComponentBody m_componentBody;
		private Random m_random = new Random();

		private bool m_exploded = false;
		private float m_lastHealth = 0f;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			FreezePressure = valuesDictionary.GetValue<float>("FreezePressure", FreezePressure);
			FluDuration = valuesDictionary.GetValue<float>("FluDuration", FluDuration);
			NoExplosionSound = valuesDictionary.GetValue<bool>("NoExplosionSound", NoExplosionSound);
			PreventExplosion = valuesDictionary.GetValue<bool>("PreventExplosion", PreventExplosion);

			m_subsystemFreezeExplosions = Project.FindSubsystem<SubsystemFreezeExplosions>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);

			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);

			if (m_componentHealth != null)
			{
				m_lastHealth = m_componentHealth.Health;
			}

			FreezePressure = MathUtils.Clamp(FreezePressure, 10f, 200f);
			FluDuration = MathUtils.Clamp(FluDuration, 0f, 600f);
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
		}

		public void Update(float dt)
		{
			if (m_componentHealth == null || m_componentBody == null || m_exploded)
				return;

			CheckForDeath();
		}

		private void CheckForDeath()
		{
			if (m_componentHealth == null) return;

			bool isDead = (m_lastHealth > 0 && m_componentHealth.Health <= 0) ||
						  (m_componentHealth.Health <= 0 && !m_exploded);

			if (!PreventExplosion && isDead)
			{
				CreateFreezeExplosion();
			}

			m_lastHealth = m_componentHealth.Health;
		}

		private void CreateFreezeExplosion()
		{
			if (m_exploded || m_componentBody == null)
				return;

			m_exploded = true;

			// Escalar por dificultad
			DifficultyMode difficulty = DifficultyMode.Normal;
			if (SubsystemGreenNightSky.Instance != null)
				difficulty = SubsystemGreenNightSky.Instance.DifficultyMode;

			float pressureMult = 1f, fluDurationMult = 1f;
			switch (difficulty)
			{
				case DifficultyMode.VeryEasy:
					pressureMult = 0.4f;
					fluDurationMult = 0.4f;
					break;
				case DifficultyMode.Easy:
					pressureMult = 0.6f;
					fluDurationMult = 0.5f;
					break;
				case DifficultyMode.Normal:
					pressureMult = 1.0f;
					fluDurationMult = 1.0f;
					break;
				case DifficultyMode.Medium:
					pressureMult = 1.2f;
					fluDurationMult = 1.2f;
					break;
				case DifficultyMode.Hard:
					pressureMult = 1.5f;
					fluDurationMult = 1.5f;
					break;
				case DifficultyMode.Extreme:
					pressureMult = 2.0f;
					fluDurationMult = 2.0f;
					break;
				case DifficultyMode.Impossible:
					pressureMult = 3.0f;
					fluDurationMult = 3.0f;
					break;
			}

			float finalPressure = FreezePressure * pressureMult;
			float finalFluDuration = FluDuration * fluDurationMult;

			Vector3 position = m_componentBody.Position;
			int x = (int)MathUtils.Floor(position.X);
			int y = (int)MathUtils.Floor(position.Y);
			int z = (int)MathUtils.Floor(position.Z);

			if (!NoExplosionSound && m_subsystemAudio != null)
			{
				float pitch = m_random.Float(-0.1f, 0.1f);
				m_subsystemAudio.PlaySound("Audio/explosion congelante", 1f, pitch, position, 15f, false);
			}

			if (m_subsystemFreezeExplosions != null)
			{
				m_subsystemFreezeExplosions.AddFreezeExplosion(x, y, z, finalPressure, finalFluDuration, NoExplosionSound);
			}
		}

		public override void OnEntityRemoved()
		{
			base.OnEntityRemoved();
			if (!PreventExplosion && !m_exploded && m_componentBody != null)
			{
				CreateFreezeExplosion();
			}
		}
	}
}
