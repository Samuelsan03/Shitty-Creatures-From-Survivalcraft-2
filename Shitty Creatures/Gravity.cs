using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class Gravity : ComponentBehavior, IUpdateable
	{
		public float Probability = 1f;
		public float Force = 10f;

		private Random m_random;
		private ComponentMiner m_miner;
		private ComponentBody m_componentBody;
		private ComponentHealth m_componentHealth;
		private SubsystemTime m_subsystemTime;

		private ComponentChaseBehavior m_chaseBehavior;
		private ComponentNewChaseBehavior m_newChaseBehavior;
		private ComponentZombieChaseBehavior m_zombieChaseBehavior;

		private StateMachine m_stateMachine;
		private double m_lastHitTime;
		private bool m_hasHit;
		private float m_originalMaxSpeed;
		private ComponentBody m_currentVictimBody;

		public override float ImportanceLevel => 0f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			Probability = valuesDictionary.GetValue<float>("Probability", 1f);
			Force = valuesDictionary.GetValue<float>("Force", 10f);

			m_random = new Random();
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_miner = Entity.FindComponent<ComponentMiner>();
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(); // FIX 1

			m_chaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
			m_newChaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_zombieChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>();

			m_stateMachine = new StateMachine();
			m_stateMachine.AddState("Idle", null, null, null);
			m_stateMachine.AddState("Hit", null, null, null);
			m_stateMachine.TransitionTo("Idle");
		}

		public void Update(float dt)
		{
			// =====================================================================
			// FIX 1: Si la criatura dueña de este componente está MUERTA, no debe
			// seguir golpeando/empujando a su víctima. El Update sigue corriendo
			// en el cadáver y los chase behaviors pueden conservar/readquirir
			// target, por eso hay que cortar aquí explícitamente.
			// =====================================================================
			if (m_componentHealth != null && m_componentHealth.Health <= 0f)
			{
				// Devolver la velocidad original por si quedó modificada al morir.
				RestoreVictimMaxSpeed();

				if (m_stateMachine.CurrentState == "Hit")
				{
					m_stateMachine.TransitionTo("Idle");
					m_hasHit = false;
				}
				return; // Muerto = no empuja nada.
			}

			m_stateMachine.Update();

			if (m_currentVictimBody != null && m_subsystemTime.GameTime - m_lastHitTime > 0.2)
			{
				RestoreVictimMaxSpeed();
			}

			if (m_miner == null)
				return;

			ComponentCreature victim = GetCurrentTarget();
			if (victim != null && victim.ComponentBody != null)
			{
				if (victim.ComponentHealth != null && victim.ComponentHealth.Health <= 0f)
					return;

				// =================================================================
				// FIX 2: Empujar SOLAMENTE cuando tiene a la presa a la vista,
				// usando el MISMO cálculo que ComponentChaseBehavior.
				// IsTargetInAttackRange / IsBodyInAttackRange (rango por bounding
				// boxes + dot con Forward > 0.25f, incluido su caso vertical).
				// Ya NO se usa la distancia fija de 1.75f.
				// =================================================================
				if (IsTargetInAttackRange(victim.ComponentBody))
				{
					if (m_random.Float(0f, 1f) <= Probability && m_subsystemTime.GameTime - m_lastHitTime > m_miner.HitInterval)
					{
						Vector3 attackerCenter = m_componentBody.BoundingBox.Center();
						Vector3 victimCenter = victim.ComponentBody.BoundingBox.Center();

						Vector3 toVictim = victimCenter - attackerCenter;
						Vector3 direction = (toVictim.LengthSquared() > 0.0001f) ? Vector3.Normalize(toVictim) : Vector3.UnitY;
						direction.Y = Math.Max(direction.Y, 0.5f);
						if (direction.LengthSquared() > 0.001f)
							direction = Vector3.Normalize(direction);

						Vector3 hitPoint = victim.ComponentBody.Position;

						StopAttackBehaviors();
						StopVictimChaseBehaviors(victim);

						// Si había una víctima anterior DISTINTA sin restaurar, restaurarla
						// antes de sobrescribir (evita dejarle MaxSpeed = 1e9 para siempre).
						if (m_currentVictimBody != null && m_currentVictimBody != victim.ComponentBody)
							RestoreVictimMaxSpeed();

						m_currentVictimBody = victim.ComponentBody;
						m_originalMaxSpeed = m_currentVictimBody.MaxSpeed;
						m_currentVictimBody.MaxSpeed = 1e9f;

						// Usar SOLAMENTE Miner.Hit para saber de verdad que es un golpe real
						m_miner.Hit(victim.ComponentBody, hitPoint, direction);

						victim.ComponentBody.ApplyImpulse(direction * Force);

						m_lastHitTime = m_subsystemTime.GameTime;
						m_stateMachine.TransitionTo("Hit");
						m_hasHit = true;
					}
				}
			}

			if (m_stateMachine.CurrentState == "Hit" && m_hasHit)
			{
				if (m_subsystemTime.GameTime - m_lastHitTime > 0)
				{
					m_stateMachine.TransitionTo("Idle");
					m_hasHit = false;
				}
			}
		}

		/// <summary>
		/// Copia fiel de ComponentChaseBehavior.IsTargetInAttackRange:
		/// la presa está "a la vista" si su cuerpo (o el cuerpo sobre el que está
		/// montada/parada) pasa la validación de IsBodyInAttackRange.
		/// </summary>
		private bool IsTargetInAttackRange(ComponentBody target)
		{
			if (target == null)
				return false;

			if (IsBodyInAttackRange(target))
				return true;

			if (target.ParentBody != null && IsTargetInAttackRange(target.ParentBody))
				return true;

			if (target.StandingOnBody != null
				&& target.StandingOnBody.Position.Y < target.Position.Y
				&& IsTargetInAttackRange(target.StandingOnBody))
				return true;

			return false;
		}

		/// <summary>
		/// Copia fiel de ComponentChaseBehavior.IsBodyInAttackRange:
		/// - Caso horizontal: distancia centro-a-centro (bounding boxes) menor que
		///   la suma de semianchos + 0.99f Y la presa DELANTE del atacante
		///   (Vector3.Dot(dir, Matrix.Forward) > 0.25f).
		/// - Caso vertical: presa casi directamente arriba/abajo
		///   (|dot con UnitY| > 0.8f) y a menos de suma de semialtos + 0.3f.
		/// </summary>
		private bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox myBox = m_componentBody.BoundingBox;
			BoundingBox targetBox = target.BoundingBox;

			Vector3 myCenter = 0.5f * (myBox.Min + myBox.Max);
			Vector3 delta = 0.5f * (targetBox.Min + targetBox.Max) - myCenter;

			float distance = delta.Length();
			if (distance <= 0f)
				return false;

			Vector3 dir = delta / distance;

			float combinedWidthX = 0.5f * (myBox.Max.X - myBox.Min.X + targetBox.Max.X - targetBox.Min.X);
			float combinedHeightY = 0.5f * (myBox.Max.Y - myBox.Min.Y + targetBox.Max.Y - targetBox.Min.Y);

			if (MathF.Abs(delta.Y) < combinedHeightY * 0.99f)
			{
				// Caso horizontal: debe estar DELANTE (en la mirada) del atacante
				if (distance < combinedWidthX + 0.99f && Vector3.Dot(dir, m_componentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (distance < combinedHeightY + 0.3f && MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
			{
				// Caso vertical: casi directamente arriba o abajo
				return true;
			}
			return false;
		}

		private void RestoreVictimMaxSpeed()
		{
			if (m_currentVictimBody != null)
			{
				m_currentVictimBody.MaxSpeed = m_originalMaxSpeed;
				m_currentVictimBody = null;
			}
		}

		private ComponentCreature GetCurrentTarget()
		{
			if (m_zombieChaseBehavior != null && m_zombieChaseBehavior.Target != null)
				return m_zombieChaseBehavior.Target;
			if (m_newChaseBehavior != null && m_newChaseBehavior.Target != null)
				return m_newChaseBehavior.Target;
			if (m_chaseBehavior != null && m_chaseBehavior.Target != null)
				return m_chaseBehavior.Target;
			return null;
		}

		private void StopAttackBehaviors()
		{
			if (m_chaseBehavior != null && m_chaseBehavior.IsActive)
				m_chaseBehavior.StopAttack();
			if (m_newChaseBehavior != null && m_newChaseBehavior.IsActive)
				m_newChaseBehavior.StopAttack();
			if (m_zombieChaseBehavior != null && m_zombieChaseBehavior.IsActive)
				m_zombieChaseBehavior.StopAttack();
		}

		private void StopVictimChaseBehaviors(ComponentCreature victim)
		{
			var chase = victim.Entity.FindComponent<ComponentChaseBehavior>();
			if (chase != null && chase.IsActive)
				chase.StopAttack();
			var newChase = victim.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (newChase != null && newChase.IsActive)
				newChase.StopAttack();
			var zombieChase = victim.Entity.FindComponent<ComponentZombieChaseBehavior>();
			if (zombieChase != null && zombieChase.IsActive)
				zombieChase.StopAttack();
		}

		public override void Dispose()
		{
			// FIX: si la entidad se elimina (despawn) mientras la víctima tenía
			// MaxSpeed = 1e9, devolvérselo para no dejarla rota para siempre.
			RestoreVictimMaxSpeed();
			base.Dispose();
		}
	}
}
