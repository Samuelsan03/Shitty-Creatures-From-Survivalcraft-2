using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCharger : Component, IUpdateable
	{
		// Propiedades configurables
		public float AttackRange { get; set; } = 3f;
		public float AttackDamage { get; set; } = 10f;
		public float PushForce { get; set; } = 20f;
		public float AttackCooldown { get; set; } = 2f;

		// Componentes referenciados
		public ComponentCreature ComponentCreature { get; set; }
		public ComponentBody ComponentBody { get; set; }

		// Subsystems
		public SubsystemTime m_subsystemTime;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemPlayers m_subsystemPlayers;

		// Estado interno
		private double m_lastAttackTime;
		private bool m_isCharging = false;
		private Vector3 m_chargeDirection;
		private float m_chargeTime;
		private DynamicArray<ComponentBody> m_tempBodiesList = new DynamicArray<ComponentBody>();
		private Dictionary<ComponentBody, double> m_lastHitTimes = new Dictionary<ComponentBody, double>();
		private const float HIT_COOLDOWN = 0.5f; // Tiempo mínimo entre golpes al mismo objetivo
		private const float CHARGE_DURATION = 1.5f;
		private const float CHARGE_SPEED = 25f;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Obtener referencias a subsystems
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);

			// Obtener componentes de la entidad
			ComponentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			ComponentBody = base.Entity.FindComponent<ComponentBody>(true);

			// Cargar propiedades desde XML si existen
			if (valuesDictionary.ContainsKey("AttackRange"))
				AttackRange = valuesDictionary.GetValue<float>("AttackRange");
			if (valuesDictionary.ContainsKey("AttackDamage"))
				AttackDamage = valuesDictionary.GetValue<float>("AttackDamage");
			if (valuesDictionary.ContainsKey("PushForce"))
				PushForce = valuesDictionary.GetValue<float>("PushForce");
			if (valuesDictionary.ContainsKey("AttackCooldown"))
				AttackCooldown = valuesDictionary.GetValue<float>("AttackCooldown");
		}

		public void Update(float dt)
		{
			if (ComponentCreature == null || ComponentBody == null)
				return;

			// Verificar si podemos atacar (enfriamiento global)
			double currentTime = m_subsystemTime.GameTime;
			if (currentTime - m_lastAttackTime < AttackCooldown && !m_isCharging)
				return;

			// Limpiar hit times antiguos
			CleanOldHitTimes(currentTime);

			// Buscar objetivos cercanos si no estamos cargando
			if (!m_isCharging)
			{
				// Buscar primero jugadores
				ComponentBody playerTarget = FindPlayerTarget();
				if (playerTarget != null)
				{
					StartCharge(playerTarget.Position);
				}
				else
				{
					// Si no hay jugadores, buscar NPCs
					ComponentBody npcTarget = FindNPCTarget();
					if (npcTarget != null)
					{
						StartCharge(npcTarget.Position);
					}
				}
			}

			// Si está cargando, aplicar movimiento continuo
			if (m_isCharging)
			{
				m_chargeTime += dt;

				// Aplicar impulso en la dirección de carga
				Vector3 impulse = m_chargeDirection * CHARGE_SPEED * dt;
				ComponentBody.Velocity += impulse;

				// Verificar colisiones durante la carga
				CheckChargeCollisions();

				// Terminar carga después de tiempo máximo
				if (m_chargeTime > CHARGE_DURATION)
				{
					EndCharge();
				}
			}
		}

		private ComponentBody FindPlayerTarget()
		{
			if (m_subsystemPlayers == null)
				return null;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player == null || player.ComponentHealth == null)
					continue;

				ComponentBody playerBody = player.ComponentBody;
				if (playerBody == null || playerBody.Entity == Entity)
					continue;

				// Verificar si este jugador fue golpeado recientemente
				if (m_lastHitTimes.ContainsKey(playerBody))
				{
					double currentTime = m_subsystemTime.GameTime;
					if (currentTime - m_lastHitTimes[playerBody] < HIT_COOLDOWN)
						continue;
				}

				// Calcular distancia y dirección
				Vector3 delta = playerBody.Position - ComponentBody.Position;
				float distance = delta.Length();

				if (distance <= AttackRange)
				{
					// Verificar si el jugador está frente al cargador
					Vector3 forward = ComponentBody.Rotation.GetForwardVector();
					Vector3 directionToPlayer = Vector3.Normalize(delta);
					float dot = Vector3.Dot(forward, directionToPlayer);

					// Si está dentro del campo de visión (60 grados)
					if (dot > 0.5f)
					{
						return playerBody;
					}
				}
			}

			return null;
		}

		private ComponentBody FindNPCTarget()
		{
			if (m_subsystemBodies == null)
				return null;

			// Crear área de búsqueda
			Vector3 searchMin = ComponentBody.Position - new Vector3(AttackRange, AttackRange, AttackRange);
			Vector3 searchMax = ComponentBody.Position + new Vector3(AttackRange, AttackRange, AttackRange);

			Vector2 min2D = new Vector2(searchMin.X, searchMin.Z);
			Vector2 max2D = new Vector2(searchMax.X, searchMax.Z);

			m_tempBodiesList.Clear();
			m_subsystemBodies.FindBodiesInArea(min2D, max2D, m_tempBodiesList);

			ComponentBody bestTarget = null;
			float bestScore = 0f;

			foreach (ComponentBody body in m_tempBodiesList)
			{
				if (body == null || body.Entity == Entity || body == ComponentBody)
					continue;

				// Verificar si este objetivo fue golpeado recientemente
				if (m_lastHitTimes.ContainsKey(body))
				{
					double currentTime = m_subsystemTime.GameTime;
					if (currentTime - m_lastHitTimes[body] < HIT_COOLDOWN)
						continue;
				}

				// Verificar si es una criatura (NPC o jugador)
				ComponentCreature targetCreature = body.Entity.FindComponent<ComponentCreature>();
				if (targetCreature == null)
					continue;

				// Verificar si tiene salud
				ComponentHealth targetHealth = body.Entity.FindComponent<ComponentHealth>();
				if (targetHealth == null || targetHealth.Health <= 0)
					continue;

				// Calcular distancia y dirección
				Vector3 delta = body.Position - ComponentBody.Position;
				float distance = delta.Length();

				if (distance <= AttackRange)
				{
					// Verificar si el objetivo está frente al cargador
					Vector3 forward = ComponentBody.Rotation.GetForwardVector();
					Vector3 directionToTarget = Vector3.Normalize(delta);
					float dot = Vector3.Dot(forward, directionToTarget);

					// Si está dentro del campo de visión (60 grados)
					if (dot > 0.5f)
					{
						// Calcular puntuación basada en proximidad y alineación
						float distanceScore = 1f - (distance / AttackRange);
						float alignmentScore = (dot - 0.5f) * 2f; // Normalizar 0.5-1.0 a 0.0-1.0
						float totalScore = distanceScore * 0.7f + alignmentScore * 0.3f;

						if (totalScore > bestScore)
						{
							bestScore = totalScore;
							bestTarget = body;
						}
					}
				}
			}

			return bestTarget;
		}

		private void StartCharge(Vector3 targetPosition)
		{
			// Calcular dirección del ataque
			m_chargeDirection = Vector3.Normalize(targetPosition - ComponentBody.Position);
			m_isCharging = true;
			m_chargeTime = 0f;
			m_lastAttackTime = m_subsystemTime.GameTime;
		}

		private void EndCharge()
		{
			m_isCharging = false;
			m_chargeTime = 0f;
			// Reducir velocidad gradualmente
			ComponentBody.Velocity *= 0.5f;
		}

		private void CheckChargeCollisions()
		{
			if (m_subsystemBodies == null || ComponentBody == null)
				return;

			// Crear caja de colisión para la carga (más pequeña y en la dirección del movimiento)
			Vector3 chargeCenter = ComponentBody.Position + m_chargeDirection * 0.5f;
			Vector3 halfExtents = new Vector3(0.5f, 0.9f, 0.5f);

			BoundingBox chargeBox = new BoundingBox(
				chargeCenter - halfExtents,
				chargeCenter + halfExtents
			);

			// Obtener todos los cuerpos en el área
			m_tempBodiesList.Clear();

			// Convertir Vector3 a Vector2 para FindBodiesInArea
			Vector2 min2D = new Vector2(chargeBox.Min.X, chargeBox.Min.Z);
			Vector2 max2D = new Vector2(chargeBox.Max.X, chargeBox.Max.Z);
			m_subsystemBodies.FindBodiesInArea(min2D, max2D, m_tempBodiesList);

			foreach (ComponentBody body in m_tempBodiesList)
			{
				if (body == null || body.Entity == Entity) // Ignorarse a sí mismo
					continue;

				// Verificar si el cuerpo está dentro del rango Y
				if (body.Position.Y < chargeBox.Min.Y || body.Position.Y > chargeBox.Max.Y)
					continue;

				// Verificar colisión entre las cajas de colisión
				if (body.BoundingBox != null && BoundingBox.Intersection(chargeBox, body.BoundingBox) != null)
				{
					// Verificar si es una criatura
					ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
					if (creature != null)
					{
						// Verificar cooldown por objetivo
						double currentTime = m_subsystemTime.GameTime;
						if (m_lastHitTimes.ContainsKey(body))
						{
							if (currentTime - m_lastHitTimes[body] < HIT_COOLDOWN)
								continue;
						}

						AttackCreature(creature, body);

						// Registrar el tiempo del golpe
						m_lastHitTimes[body] = currentTime;
					}
				}
			}
		}

		private void AttackCreature(ComponentCreature creature, ComponentBody creatureBody)
		{
			ComponentHealth creatureHealth = creature.Entity.FindComponent<ComponentHealth>();

			if (creatureHealth == null || creatureBody == null)
				return;

			// Calcular dirección del empuje (usar la dirección de carga para consistencia)
			Vector3 direction = m_chargeDirection;

			// Aplicar daño
			creatureHealth.Injure(AttackDamage, null, false, "Charger attack");

			// Aplicar fuerza de empuje a la criatura (en la dirección de la carga)
			creatureBody.Velocity += direction * PushForce;

			// Empujar al cargador ligeramente hacia atrás (pero no demasiado)
			ComponentBody.Velocity += -direction * PushForce * 0.1f;

			// Pequeña reducción de velocidad después de golpear
			ComponentBody.Velocity *= 0.8f;
		}

		private void CleanOldHitTimes(double currentTime)
		{
			// Eliminar entradas antiguas del diccionario
			List<ComponentBody> toRemove = new List<ComponentBody>();
			foreach (var kvp in m_lastHitTimes)
			{
				if (currentTime - kvp.Value > HIT_COOLDOWN * 2)
				{
					toRemove.Add(kvp.Key);
				}
			}

			foreach (var body in toRemove)
			{
				m_lastHitTimes.Remove(body);
			}
		}

		// Método para ataque manual (desde otros componentes)
		public void ChargeInDirection(Vector3 direction)
		{
			m_chargeDirection = Vector3.Normalize(direction);
			m_isCharging = true;
			m_chargeTime = 0f;
			m_lastAttackTime = m_subsystemTime.GameTime;
		}

		// Método para verificar si está cargando
		public bool IsCharging()
		{
			return m_isCharging;
		}

		// Método para obtener la dirección de carga
		public Vector3 GetChargeDirection()
		{
			return m_chargeDirection;
		}
	}
}
