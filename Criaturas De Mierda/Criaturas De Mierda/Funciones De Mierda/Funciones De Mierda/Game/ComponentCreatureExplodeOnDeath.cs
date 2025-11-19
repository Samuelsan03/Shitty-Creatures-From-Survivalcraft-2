using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureExplodeOnDeath : GameEntitySystem.Component, IUpdateable
	{
		public SubsystemTime m_subsystemTime;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemExplosions m_subsystemExplosions;
		public SubsystemPlayers m_subsystemPlayers;
		public SubsystemBodies m_subsystemBodies;
		public ComponentHealth m_componentHealth;
		public ComponentBody m_componentBody;
		public ComponentCreature m_componentCreature;

		public Vector2 ExplosionPressureRange = new Vector2(60f, 300f);
		public Vector2 FuseDurationRange = new Vector2(5f, 10f);
		public float AttackDistance = 3f; // Distancia para encender mecha cuando encuentra víctima

		private bool m_isIgnited = false;
		private bool m_hasExploded = false;
		private bool m_isProvoked = false;
		private bool m_decisionMade = false;
		private double m_ignitionTime;
		private double m_explosionTime;
		private float m_initialHealth;
		private Random m_random = new Random();
		private ComponentPlayer m_targetPlayer;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemExplosions = base.Project.FindSubsystem<SubsystemExplosions>(true);
			m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_componentHealth = base.Entity.FindComponent<ComponentHealth>(true);
			m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);

			if (valuesDictionary.ContainsKey("MinMaxRandomExplosionPresure"))
			{
				string[] values = valuesDictionary.GetValue<string>("MinMaxRandomExplosionPresure").Split(';');
				if (values.Length == 2)
					ExplosionPressureRange = new Vector2(float.Parse(values[0]), float.Parse(values[1]));
			}

			if (valuesDictionary.ContainsKey("MinMaxRandomFuseDuration"))
			{
				string[] values = valuesDictionary.GetValue<string>("MinMaxRandomFuseDuration").Split(';');
				if (values.Length == 2)
					FuseDurationRange = new Vector2(float.Parse(values[0]), float.Parse(values[1]));
			}

			m_initialHealth = m_componentHealth.Health;
		}

		public void Update(float dt)
		{
			if (m_hasExploded) return;

			// Si el NPC está muerto, remover componente sin explotar
			if (m_componentHealth.Health <= 0f)
			{
				base.Entity.RemoveComponent(this);
				return;
			}

			if (!m_decisionMade)
			{
				// PASO 1: Verificar si es provocado (recibe daño de criaturas)
				if (m_componentHealth.Health < m_initialHealth && IsAttackedByCreature())
				{
					m_isProvoked = true;
					m_targetPlayer = FindNearestPlayer();
				}

				// PASO 2: Si está provocado Y tiene objetivo cerca, ENCENDER MECHA
				if (m_isProvoked && m_targetPlayer != null && IsPlayerNearby(m_targetPlayer, AttackDistance))
				{
					StartIgnition();
					return;
				}
			}

			// Si está encendido, esperar a que termine el tiempo de la mecha para explotar
			if (m_isIgnited && !m_hasExploded)
			{
				// Explotar cuando pase el tiempo del fusible
				if (m_subsystemTime.GameTime >= m_explosionTime)
				{
					Explode();
					return;
				}
			}

			// Efecto visual y sonido del fusible
			if (m_isIgnited && m_componentHealth.Health > 0f && m_subsystemTime.PeriodicGameTimeEvent(1.0, 0.0))
			{
				Vector3 position = m_componentBody.Position;
				m_subsystemAudio.PlaySound("Audio/Fuse", 1.5f, 0f, position, 8f, 0f);
			}

			// Actualizar salud inicial para el próximo frame
			m_initialHealth = m_componentHealth.Health;
		}

		private bool IsAttackedByCreature()
		{
			DynamicArray<ComponentBody> nearbyBodies = new DynamicArray<ComponentBody>();
			m_subsystemBodies.FindBodiesInArea(
				new Vector2(m_componentBody.Position.X - 8f, m_componentBody.Position.Z - 8f),
				new Vector2(m_componentBody.Position.X + 8f, m_componentBody.Position.Z + 8f),
				nearbyBodies
			);

			foreach (ComponentBody body in nearbyBodies)
			{
				if (body.Entity != base.Entity)
				{
					var creature = body.Entity.FindComponent<ComponentCreature>();
					var player = body.Entity.FindComponent<ComponentPlayer>();

					if (creature != null || player != null)
					{
						return true;
					}
				}
			}
			return false;
		}

		private ComponentPlayer FindNearestPlayer()
		{
			if (m_subsystemPlayers == null) return null;

			ComponentPlayer nearestPlayer = null;
			float nearestDistance = float.MaxValue;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player.ComponentBody != null && player.ComponentHealth != null && player.ComponentHealth.Health > 0)
				{
					float distance = Vector3.Distance(m_componentBody.Position, player.ComponentBody.Position);
					if (distance < nearestDistance)
					{
						nearestDistance = distance;
						nearestPlayer = player;
					}
				}
			}
			return nearestPlayer;
		}

		private bool IsPlayerNearby(ComponentPlayer player, float maxDistance)
		{
			if (player == null || player.ComponentBody == null) return false;

			float distance = Vector3.Distance(m_componentBody.Position, player.ComponentBody.Position);
			return distance <= maxDistance;
		}

		private void StartIgnition()
		{
			if (m_decisionMade) return;

			m_decisionMade = true;
			Ignite();
		}

		private void Ignite()
		{
			m_isIgnited = true;
			m_ignitionTime = m_subsystemTime.GameTime;
			float fuseDuration = m_random.Float(FuseDurationRange.X, FuseDurationRange.Y);
			m_explosionTime = m_ignitionTime + fuseDuration;

			Vector3 position = m_componentBody.Position;
			m_subsystemAudio.PlaySound("Audio/Ignite", 1f, 0f, position, 5f, 0f);
		}

		private void Explode()
		{
			if (m_hasExploded) return;

			m_hasExploded = true;
			float pressure = m_random.Float(ExplosionPressureRange.X, ExplosionPressureRange.Y);
			Vector3 position = m_componentBody.Position;

			Point3 explosionPoint = new Point3(
				Terrain.ToCell(position.X),
				Terrain.ToCell(position.Y),
				Terrain.ToCell(position.Z)
			);

			// Crear la explosión
			m_subsystemExplosions.AddExplosion(explosionPoint.X, explosionPoint.Y, explosionPoint.Z, pressure, true, false);

			// Matar a la criatura cuando explota
			if (m_componentHealth != null)
			{
				m_componentHealth.Health = 0f;
			}

			// Remover el componente
			base.Entity.RemoveComponent(this);
		}
	}
}
