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
		public ComponentZombieChaseBehavior ZombieChaseBehavior { get; set; }

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
		private const float CHARGE_DURATION = 1.2f;
		private const float CHARGE_SPEED = 20f;
		private const float MIN_ATTACK_DISTANCE = 0.8f;
		private const float MAX_ATTACK_DISTANCE = 1.2f;
		private bool m_isProvoked = false;
		private double m_provokedUntilTime = 0;

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
			ZombieChaseBehavior = base.Entity.FindComponent<ComponentZombieChaseBehavior>();

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

			double currentTime = m_subsystemTime.GameTime;

			// Verificar si el tiempo de provocación ha expirado
			if (m_isProvoked && currentTime > m_provokedUntilTime)
			{
				m_isProvoked = false;
			}

			// Si está cargando, manejar la carga
			if (m_isCharging)
			{
				UpdateCharge(dt, currentTime);
				return;
			}

			// Verificar cooldown de ataque
			if (currentTime - m_lastAttackTime < AttackCooldown)
				return;

			// Buscar objetivos si está provocado
			if (m_isProvoked)
			{
				TryFindAndChargeTarget();
			}
		}

		private void UpdateCharge(float dt, double currentTime)
		{
			m_chargeTime += dt;

			// Solo aplicar impulso si está en el suelo
			if (ComponentBody.StandingOnValue != null)
			{
				// Aplicar impulso en la dirección de carga
				Vector3 impulse = m_chargeDirection * CHARGE_SPEED * dt;
				
				// Solo aplicar en X y Z, mantener Y como está
				ComponentBody.Velocity = new Vector3(
					ComponentBody.Velocity.X + impulse.X,
					ComponentBody.Velocity.Y,
					ComponentBody.Velocity.Z + impulse.Z
				);
			}

			// Verificar colisiones solo después de cierto tiempo
			if (m_chargeTime > 0.2f)
			{
				CheckChargeCollisions();
			}

			// Terminar carga después de tiempo máximo
			if (m_chargeTime > CHARGE_DURATION)
			{
				EndCharge();
			}
		}

		private void TryFindAndChargeTarget()
		{
			// Buscar jugador primero
			ComponentBody target = FindNearestTarget();
			
			if (target != null)
			{
				StartCharge(target.Position);
			}
		}

		private ComponentBody FindNearestTarget()
		{
			ComponentBody nearestTarget = null;
			float nearestDistance = float.MaxValue;

			// Buscar jugadores
			if (m_subsystemPlayers != null)
			{
				foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
				{
					if (player == null || player.ComponentHealth == null || player.ComponentHealth.Health <= 0)
						continue;

					ComponentBody playerBody = player.ComponentBody;
					if (playerBody == null || playerBody.Entity == Entity)
						continue;

					float distance = Vector3.Distance(playerBody.Position, ComponentBody.Position);
					
					if (distance <= AttackRange && distance < nearestDistance)
					{
						// Verificar línea de visión
						Vector3 directionToPlayer = Vector3.Normalize(playerBody.Position - ComponentBody.Position);
						Vector3 forward = ComponentBody.Rotation.GetForwardVector();
						float dot = Vector3.Dot(forward, directionToPlayer);
						
						if (dot > 0.3f) // Campo de visión amplio
						{
							nearestDistance = distance;
							nearestTarget = playerBody;
						}
					}
				}
			}

			// Si no hay jugadores, buscar NPCs
			if (nearestTarget == null && m_subsystemBodies != null)
			{
				Vector3 searchMin = ComponentBody.Position - new Vector3(AttackRange, 2f, AttackRange);
				Vector3 searchMax = ComponentBody.Position + new Vector3(AttackRange, 2f, AttackRange);
				Vector2 min2D = new Vector2(searchMin.X, searchMin.Z);
				Vector2 max2D = new Vector2(searchMax.X, searchMax.Z);

				m_tempBodiesList.Clear();
				m_subsystemBodies.FindBodiesInArea(min2D, max2D, m_tempBodiesList);

				foreach (ComponentBody body in m_tempBodiesList)
				{
					if (body == null || body.Entity == Entity || body == ComponentBody)
						continue;

					ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
					if (creature == null || creature.ComponentHealth == null || creature.ComponentHealth.Health <= 0)
						continue;

					float distance = Vector3.Distance(body.Position, ComponentBody.Position);
					
					if (distance <= AttackRange && distance < nearestDistance)
					{
						Vector3 directionToTarget = Vector3.Normalize(body.Position - ComponentBody.Position);
						Vector3 forward = ComponentBody.Rotation.GetForwardVector();
						float dot = Vector3.Dot(forward, directionToTarget);
						
						if (dot > 0.3f)
						{
							nearestDistance = distance;
							nearestTarget = body;
						}
					}
				}
			}

			return nearestTarget;
		}

		private void StartCharge(Vector3 targetPosition)
		{
			// Calcular dirección horizontal hacia el objetivo
			Vector3 toTarget = targetPosition - ComponentBody.Position;
			m_chargeDirection = new Vector3(toTarget.X, 0, toTarget.Z);
			
			if (m_chargeDirection.LengthSquared() > 0.01f)
			{
				m_chargeDirection = Vector3.Normalize(m_chargeDirection);
			}
			else
			{
				// Si está muy cerca, usar la dirección forward
				m_chargeDirection = ComponentBody.Rotation.GetForwardVector();
				m_chargeDirection = new Vector3(m_chargeDirection.X, 0, m_chargeDirection.Z);
				m_chargeDirection = Vector3.Normalize(m_chargeDirection);
			}

			m_isCharging = true;
			m_chargeTime = 0f;
			m_lastAttackTime = m_subsystemTime.GameTime;
			
			// Desactivar temporalmente el ZombieChaseBehavior si existe
			if (ZombieChaseBehavior != null)
			{
				// Podríamos intentar desactivarlo si tiene un método para eso
			}
		}

		private void EndCharge()
		{
			m_isCharging = false;
			m_chargeTime = 0f;
			
			// Frenar gradualmente
			ComponentBody.Velocity *= 0.4f;
			
			// Extender el tiempo de provocación
			m_provokedUntilTime = m_subsystemTime.GameTime + 3.0;
			m_isProvoked = true;
		}

		private void CheckChargeCollisions()
		{
			if (m_subsystemBodies == null || ComponentBody == null)
				return;

			// Área de colisión pequeña justo delante
			Vector3 checkPosition = ComponentBody.Position + m_chargeDirection * 0.5f;
			float checkRadius = 0.6f;
			float checkHeight = 1.0f;

			Vector3 searchMin = checkPosition - new Vector3(checkRadius, checkHeight, checkRadius);
			Vector3 searchMax = checkPosition + new Vector3(checkRadius, checkHeight, checkRadius);

			Vector2 min2D = new Vector2(searchMin.X, searchMin.Z);
			Vector2 max2D = new Vector2(searchMax.X, searchMax.Z);

			m_tempBodiesList.Clear();
			m_subsystemBodies.FindBodiesInArea(min2D, max2D, m_tempBodiesList);

			foreach (ComponentBody body in m_tempBodiesList)
			{
				if (body == null || body.Entity == Entity)
					continue;

				// Verificar distancia real
				float distance = Vector3.Distance(body.Position, ComponentBody.Position);

				// Solo atacar si está en el rango correcto
				if (distance < MIN_ATTACK_DISTANCE || distance > MAX_ATTACK_DISTANCE)
					continue;

				// Verificar si está en la dirección de carga
				Vector3 directionToTarget = Vector3.Normalize(body.Position - ComponentBody.Position);
				float dot = Vector3.Dot(m_chargeDirection, directionToTarget);

				// Solo atacar si el objetivo está en la dirección frontal
				if (dot < 0.8f)
					continue;

				// Verificar si es una criatura viva
				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
				ComponentHealth health = body.Entity.FindComponent<ComponentHealth>();
				
				if (creature != null && health != null && health.Health > 0)
				{
					AttackCreature(creature, body);
					EndCharge();
					return;
				}
			}
		}

		private void AttackCreature(ComponentCreature creature, ComponentBody creatureBody)
		{
			ComponentHealth creatureHealth = creature.Entity.FindComponent<ComponentHealth>();

			if (creatureHealth == null || creatureBody == null)
				return;

			// Calcular dirección del empuje
			Vector3 direction = Vector3.Normalize(creatureBody.Position - ComponentBody.Position);
			// Hacerlo principalmente horizontal con un poco de fuerza hacia arriba
			direction = new Vector3(direction.X, 0.15f, direction.Z);
			direction = Vector3.Normalize(direction);

			// Aplicar daño
			creatureHealth.Injure(AttackDamage, null, false, "Charger attack");

			// Aplicar fuerza de empuje a la criatura
			Vector3 pushForce = direction * PushForce;
			creatureBody.Velocity = new Vector3(
				creatureBody.Velocity.X + pushForce.X,
				Math.Min(creatureBody.Velocity.Y + 1.2f, 6f), // Fuerza vertical limitada
				creatureBody.Velocity.Z + pushForce.Z
			);

			// Frenar al cargador más
			ComponentBody.Velocity *= 0.3f;
		}

		// Método para provocar al Charger (llamar desde otros componentes o cuando es atacado)
		public void Provoke()
		{
			m_isProvoked = true;
			m_provokedUntilTime = m_subsystemTime.GameTime + 5.0; // Provocado por 5 segundos
		}

		// Método para forzar una carga en una dirección
		public void ChargeInDirection(Vector3 direction)
		{
			m_chargeDirection = new Vector3(direction.X, 0, direction.Z);
			if (m_chargeDirection.LengthSquared() > 0.01f)
			{
				m_chargeDirection = Vector3.Normalize(m_chargeDirection);
			}
			
			m_isCharging = true;
			m_chargeTime = 0f;
			m_lastAttackTime = m_subsystemTime.GameTime;
			Provoke();
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
		
		// Método para verificar si está provocado
		public bool IsProvoked()
		{
			return m_isProvoked;
		}
	}
}
