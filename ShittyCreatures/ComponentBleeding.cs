using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBleeding : Component, IUpdateable
	{
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemTime m_subsystemTime;
		public ComponentHealth m_componentHealth;
		public ComponentCreature m_componentCreature;
		public ComponentBody m_componentBody;

		public float BleedingRate = 1f;
		public float LowHealthThreshold = 0.3f;
		public float m_passiveBleedInterval = 0.8f;

		public double m_lastDamageTime;
		public double m_lastPassiveBleedTime;
		public double m_lastTrailTime;
		public double m_deathTime;

		public bool m_bloodPoolPlaced;
		public BloodParticleSystem m_bloodPoolSystem;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);

			BleedingRate = valuesDictionary.GetValue<float>("BleedingRate");
			LowHealthThreshold = valuesDictionary.GetValue<float>("LowHealthThreshold");
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<float>("BleedingRate", BleedingRate);
			valuesDictionary.SetValue<float>("LowHealthThreshold", LowHealthThreshold);
		}

		public void Update(float dt)
		{
			// ===== MANEJO DE MUERTE =====
			if (m_componentHealth.Health <= 0f)
			{
				if (m_deathTime == 0.0)
				{
					m_deathTime = m_subsystemTime.GameTime;

					// CHORRO DE SANGRE AL MORIR - 5 sistemas
					for (int i = 0; i < 5; i++)
					{
						Vector3 pos = GetBloodPosition(i);
						var deathBlood = new BloodParticleSystem(m_subsystemTerrain, pos, true);
						m_subsystemParticles.AddParticleSystem(deathBlood, false);
					}

					// CREAR EL CHARCO INMEDIATAMENTE
					CreateBloodPool();
				}

				return;
			}

			// ===== ESTADO VIVO =====
			m_deathTime = 0.0;
			m_bloodPoolPlaced = false;
			m_bloodPoolSystem = null;

			// AÑADE AQUÍ OTRA LÓGICA DE SANGRADO SI ES NECESARIO
			// Por ejemplo: sangrado por daño, sangrado pasivo cuando la vida es baja, etc.
		}

		public void CreateBloodPool()
		{
			Vector3 pos = m_componentBody.Position;

			// Ajustar posición al suelo
			pos.Y = (float)Math.Floor(pos.Y) + 0.05f;

			// Crear UN SOLO sistema grande para el charco
			m_bloodPoolSystem = new BloodParticleSystem(m_subsystemTerrain, pos, true);

			// Modificar TODAS las partículas para que sean el charco
			for (int j = 0; j < m_bloodPoolSystem.Particles.Length; j++)
			{
				var p = m_bloodPoolSystem.Particles[j];

				// Posición aleatoria alrededor del cuerpo
				Vector3 offset = new Vector3(
					m_random.Float(-0.8f, 0.8f),
					0.0f,
					m_random.Float(-0.8f, 0.8f)
				);

				p.Position = pos + offset;
				p.Velocity = Vector3.Zero; // No se mueven
				p.Duration = 10f; // Duran 10 segundos exactamente
				p.TimeToLive = 10f;
				p.Size = new Vector2(0.25f); // Más grandes para el charco

				// Color rojo oscuro con opacidad
				p.Color = new Color(140, 20, 20, 220);

				// Textura aleatoria para variar
				p.TextureSlot = m_random.Int(0, 2);
			}

			m_subsystemParticles.AddParticleSystem(m_bloodPoolSystem, false);
			m_bloodPoolPlaced = true;
		}

		public Vector3 GetBloodPosition(int index)
		{
			Vector3 basePos = m_componentBody.Position;
			Vector3 forward = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
			Vector3 right = m_componentCreature.ComponentCreatureModel.EyeRotation.GetRightVector();

			if (Entity.FindComponent<ComponentHumanModel>() != null)
			{
				if (index == 0) return basePos + new Vector3(0f, 1.6f, 0f) + forward * 0.2f;
				if (index == 1) return basePos + new Vector3(0f, 1.2f, 0f) + forward * 0.3f;
				if (index == 2) return basePos + new Vector3(0f, 0.8f, 0f);
				if (index == 3) return basePos + new Vector3(0f, 0.4f, 0f) + right * 0.3f;
				return basePos + new Vector3(0f, 0.4f, 0f) - right * 0.3f;
			}
			if (Entity.FindComponent<ComponentFourLeggedModel>() != null)
			{
				if (index == 0) return basePos + new Vector3(0f, 1.3f, 0f);
				if (index == 1) return basePos + new Vector3(0f, 1.0f, 0f);
				if (index == 2) return basePos + new Vector3(0f, 0.7f, 0f);
				return basePos + new Vector3(0f, 0.4f, 0f);
			}

			return basePos + new Vector3(0f, 1f, 0f);
		}

		private Random m_random = new Random();
	}
}
