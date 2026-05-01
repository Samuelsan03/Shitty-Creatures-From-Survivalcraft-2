using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Componente para un Boomer que al explotar solo propaga gripe (flu) sin dañar bloques.
	/// Al morir la entidad, genera una explosión congelante que infecta a las criaturas cercanas.
	/// </summary>
	public class ComponentBoomerFrozenExplosion : Component, IUpdateable
	{
		// ===== CONFIGURACIÓN =====
		public float FreezePressure = 50f;          // Presión que determina el radio de la explosión (3 a 10 bloques)
		public float FluDuration = 300f;            // Duración de la gripe en segundos
		public bool NoExplosionSound = false;       // Suprimir sonido de explosión
		public bool PreventExplosion = false;       // Evita que explote (útil para depuración)

		// ===== REFERENCIAS =====
		private SubsystemFreezeExplosions m_subsystemFreezeExplosions;
		private SubsystemAudio m_subsystemAudio;
		private ComponentHealth m_componentHealth;
		private ComponentBody m_componentBody;
		private Random m_random = new Random();

		// ===== ESTADO INTERNO =====
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
				m_subsystemFreezeExplosions.AddFreezeExplosion(x, y, z, FreezePressure, FluDuration, NoExplosionSound);
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
