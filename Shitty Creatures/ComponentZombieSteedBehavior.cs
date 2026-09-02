using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieSteedBehavior : ComponentSteedBehavior
	{
		public bool IsEnabled { get; set; } = true;
		public Vector2 FullLookOrder { get; private set; }
		public float ExternalVerticalInput { get; set; }

		// Campos propios
		private SubsystemTime m_subsystemTime;
		private SubsystemBodies m_subsystemBodies;
		private ComponentCreature m_componentCreature;
		private ComponentLocomotion m_componentLocomotion;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentZombieMount m_componentZombieMount;
		private StateMachine m_stateMachine = new StateMachine();
		private float m_importanceLevel;
		private Random m_random = new Random();
		private DynamicArray<ComponentBody> m_bodies = new DynamicArray<ComponentBody>();
		private float[] m_speedLevels = new float[] { -0.33f, 0f, 0.33f, 0.66f, 1f };
		private int m_speedLevel;
		private float m_speed;
		private float m_turnSpeed;
		private float m_speedChangeFactor;
		private float m_timeToSpeedReduction;
		private double m_lastNotBlockedTime;

		public override float ImportanceLevel => m_importanceLevel;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentLocomotion = Entity.FindComponent<ComponentLocomotion>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentZombieMount = Entity.FindComponent<ComponentZombieMount>(true);

			IsEnabled = valuesDictionary.GetValue<bool>("IsEnabled", true);

			m_stateMachine.AddState("Inactive", null, delegate
			{
				if (IsActive)
					m_stateMachine.TransitionTo("Wait");
			}, null);

			m_stateMachine.AddState("Wait", delegate
			{
				ComponentRider rider = FindNearbyRider(6f);
				// Solo aceptar jinetes que NO sean jugadores (zombis)
				if (rider != null && rider.Entity.FindComponent<ComponentPlayer>() == null)
				{
					m_componentPathfinding.SetDestination(
						new Vector3?(rider.ComponentCreature.ComponentBody.Position),
						m_random.Float(0.2f, 0.3f), 3.25f, 0, false, true, false, null);
					if (m_random.Float(0f, 1f) < 0.5f)
						m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
				}
			}, delegate
			{
				if (m_componentZombieMount != null && m_componentZombieMount.Rider != null &&
					m_componentZombieMount.Rider.Entity.FindComponent<ComponentPlayer>() == null)
					m_stateMachine.TransitionTo("Steed");
				m_componentCreature.ComponentCreatureModel.LookRandomOrder = true;
			}, null);

			m_stateMachine.AddState("Steed", delegate
			{
				m_componentPathfinding.Stop();
				m_speed = 0f;
				m_speedLevel = 1;
			}, delegate
			{
				ProcessRidingOrders();
			}, null);

			m_stateMachine.TransitionTo("Inactive");
		}

		public override void Update(float dt)
		{
			m_stateMachine.Update();

			if (SpeedOrder != 0 || TurnOrder != 0f || JumpOrder != 0f)
			{
				SpeedOrder = 0;
				TurnOrder = 0f;
				JumpOrder = 0f;
				WasOrderIssued = true;
			}
			else
			{
				WasOrderIssued = false;
			}

			if (m_subsystemTime.PeriodicGameTimeEvent(1.0, (GetHashCode() % 100) * 0.01f))
			{
				m_importanceLevel = 0f;
				if (IsEnabled)
				{
					if (m_componentZombieMount != null && m_componentZombieMount.Rider != null &&
						m_componentZombieMount.Rider.Entity.FindComponent<ComponentPlayer>() == null)
						m_importanceLevel = 275f;
					else if (FindNearbyRider(7f) != null)
						m_importanceLevel = 7f;
				}
			}

			if (!IsActive)
				m_stateMachine.TransitionTo("Inactive");
		}

		public virtual ComponentRider FindNearbyRider(float range)
		{
			m_bodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(
				new Vector2(m_componentCreature.ComponentBody.Position.X, m_componentCreature.ComponentBody.Position.Z),
				range, m_bodies);
			foreach (ComponentBody body in m_bodies)
			{
				if (Vector3.DistanceSquared(m_componentCreature.ComponentBody.Position, body.Position) < range * range)
				{
					ComponentRider rider = body.Entity.FindComponent<ComponentRider>();
					if (rider != null)
						return rider;
				}
			}
			return null;
		}

		/// <summary>
		/// Calcula la entrada vertical necesaria para alcanzar la altitud del objetivo del jinete (si existe y es zombi).
		/// </summary>
		private float GetVerticalInputFromRiderTarget()
		{
			ComponentRider rider = m_componentZombieMount?.Rider;
			if (rider == null) return 0f;

			// Intentar obtener el ComponentZombieAI del jinete
			ComponentZombieAI zombieAI = rider.Entity.FindComponent<ComponentZombieAI>();
			if (zombieAI == null) return 0f;

			// Obtener el objetivo del chase behavior mediante reflexión o campo público
			// Como ComponentZombieAI tiene m_chaseBehavior privado, usamos reflexión (o agregamos propiedad pública, pero no modificamos IA)
			var chaseField = typeof(ComponentZombieAI).GetField("m_chaseBehavior", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (chaseField == null) return 0f;
			ComponentZombieChaseBehavior chase = chaseField.GetValue(zombieAI) as ComponentZombieChaseBehavior;
			if (chase == null) return 0f;

			ComponentCreature target = chase.Target;
			if (target == null || target.ComponentHealth.Health <= 0f) return 0f;

			// Calcular diferencia de altura entre la montura y el objetivo
			float mountHeight = m_componentCreature.ComponentBody.Position.Y;
			float targetHeight = target.ComponentBody.Position.Y;
			float heightDiff = targetHeight - mountHeight;

			// Umbral de zona muerta (no mover si la diferencia es menor a 2 bloques)
			const float deadZone = 2f;
			if (Math.Abs(heightDiff) < deadZone) return 0f;

			// Máxima altura para control proporcional (evitar subidas/descensos bruscos)
			const float maxVerticalInput = 1f;
			const float maxHeightDiff = 10f;
			float verticalInput = Math.Clamp(heightDiff / maxHeightDiff, -maxVerticalInput, maxVerticalInput);

			// Si la montura puede volar y estamos muy lejos verticalmente, aumentar un poco la sensibilidad
			if (m_componentLocomotion.FlySpeed > 0f && Math.Abs(heightDiff) > 6f)
				verticalInput = Math.Sign(verticalInput) * Math.Min(1f, Math.Abs(verticalInput) * 1.5f);

			return verticalInput;
		}

		public virtual void ProcessRidingOrders()
		{
			ComponentRider rider = m_componentZombieMount?.Rider;

			// Si el rider es un jugador, ignorar todas las órdenes
			if (rider != null && rider.Entity.FindComponent<ComponentPlayer>() != null)
			{
				SpeedOrder = 0;
				TurnOrder = 0f;
				JumpOrder = 0f;
				m_componentCreature.ComponentLocomotion.TurnOrder = Vector2.Zero;
				m_componentCreature.ComponentLocomotion.WalkOrder = null;
				m_componentCreature.ComponentLocomotion.JumpOrder = 0f;
				return;
			}

			// ===== NUEVO: Calcular entrada vertical automática desde el objetivo del jinete =====
			float verticalInput = ExternalVerticalInput;
			if (Math.Abs(verticalInput) < 0.01f)
			{
				// Si no hay entrada externa, calcular automáticamente desde el objetivo
				verticalInput = GetVerticalInputFromRiderTarget();
			}

			// ===== FIN NUEVO =====

			// Solo procesar órdenes si el rider NO es jugador (ej. otro zombi)
			m_speedLevel = Math.Clamp(m_speedLevel + SpeedOrder, 0, m_speedLevels.Length - 1);
			if (m_speedLevel == m_speedLevels.Length - 1 && SpeedOrder > 0)
				m_timeToSpeedReduction = m_random.Float(8f, 12f);
			if (m_speedLevel == 0 && SpeedOrder < 0)
				m_timeToSpeedReduction = 1.25f;

			m_timeToSpeedReduction -= m_subsystemTime.GameTimeDelta;
			if (m_timeToSpeedReduction <= 0f && m_speedLevel == m_speedLevels.Length - 1)
			{
				m_speedLevel--;
				m_speedChangeFactor = 0.25f;
			}
			else if (m_timeToSpeedReduction <= 0f && m_speedLevel == 0)
			{
				m_speedLevel = 1;
				m_speedChangeFactor = 100f;
			}
			else
			{
				m_speedChangeFactor = 100f;
			}

			if (m_subsystemTime.PeriodicGameTimeEvent(0.25, 0.0))
			{
				float collisionSpeed = new Vector2(
					m_componentCreature.ComponentBody.CollisionVelocityChange.X,
					m_componentCreature.ComponentBody.CollisionVelocityChange.Z).Length();
				if (m_speedLevel == 0 || collisionSpeed < 0.1f ||
					m_componentCreature.ComponentBody.Velocity.Length() > MathF.Abs(0.5f * m_speed * m_componentCreature.ComponentLocomotion.WalkSpeed))
				{
					m_lastNotBlockedTime = m_subsystemTime.GameTime;
				}
				else if (m_subsystemTime.GameTime - m_lastNotBlockedTime > 0.75)
				{
					m_speedLevel = 1;
				}
			}

			m_speed += MathUtils.Saturate(m_speedChangeFactor * m_subsystemTime.GameTimeDelta) *
					   (m_speedLevels[m_speedLevel] - m_speed);
			m_turnSpeed += 2f * m_subsystemTime.GameTimeDelta *
						   (Math.Clamp(TurnOrder, -0.5f, 0.5f) - m_turnSpeed);

			// ===== SISTEMA DE MOVIMIENTO MEJORADO =====
			float immersionDepth = m_componentCreature.ComponentBody.ImmersionDepth;
			bool canFly = (m_componentLocomotion.FlySpeed > 0f);
			bool canSwim = (m_componentLocomotion.SwimSpeed > 0f);

			// Modo vuelo
			if (canFly && immersionDepth < 0.3f)
			{
				if (Math.Abs(m_speed) > 0.01f)
				{
					m_componentLocomotion.WalkOrder = new Vector2?(new Vector2(m_speed * TurnOrder, m_speed));
				}
				else
				{
					m_componentLocomotion.WalkOrder = null;
				}

				m_componentLocomotion.IsCreativeFlyEnabled = true;

				if (Math.Abs(verticalInput) > 0.01f)
				{
					float verticalSpeed = verticalInput * (m_speed > 0.1f ? m_speed : 4f);
					m_componentLocomotion.FlyOrder = new Vector3(0f, verticalSpeed, 0f);
				}
				else
				{
					m_componentLocomotion.FlyOrder = Vector3.Zero;
					Vector3 vel = m_componentCreature.ComponentBody.Velocity;
					if (Math.Abs(vel.Y) > 0.01f)
					{
						vel.Y = 0f;
						m_componentCreature.ComponentBody.Velocity = vel;
					}
				}
			}
			else if (canSwim && immersionDepth > 0.5f)
			{
				m_componentLocomotion.SwimOrder = new Vector3(0f, verticalInput * m_speed, m_speed);
			}
			else
			{
				m_componentLocomotion.WalkOrder = new Vector2?(new Vector2(m_speed * TurnOrder, m_speed));
				if (canFly && verticalInput > 0.5f && m_componentCreature.ComponentBody.StandingOnValue != null)
				{
					m_componentLocomotion.JumpOrder = MathF.Max(m_componentLocomotion.JumpOrder, verticalInput);
				}
			}

			m_componentLocomotion.TurnOrder = new Vector2(m_turnSpeed, 0f);

			if (JumpOrder > 0f)
				m_componentLocomotion.JumpOrder = JumpOrder;

			FullLookOrder = new Vector2(2f * m_turnSpeed, verticalInput);

			ExternalVerticalInput = 0f;
		}
	}
}