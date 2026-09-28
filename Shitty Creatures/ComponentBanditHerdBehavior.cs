using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBanditHerdBehavior : ComponentBehavior, IUpdateable
	{
		public string HerdName { get; set; }

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override float ImportanceLevel => m_importanceLevel;

		public void CallNearbyCreaturesHelp(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (target == null) return;

			Vector3 position = target.ComponentBody.Position;
			foreach (ComponentCreature componentCreature in m_subsystemCreatureSpawn.Creatures)
			{
				if (Vector3.DistanceSquared(position, componentCreature.ComponentBody.Position) < 256f)
				{
					ComponentBanditHerdBehavior componentHerdBehavior = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
					if (componentHerdBehavior != null && !string.IsNullOrEmpty(componentHerdBehavior.HerdName) && componentHerdBehavior.HerdName == HerdName && componentHerdBehavior.m_autoNearbyCreaturesHelp)
					{
						ComponentBanditChaseBehavior chaseBehavior = componentCreature.Entity.FindComponent<ComponentBanditChaseBehavior>();
						if (chaseBehavior != null && chaseBehavior.Target == null)
						{
							chaseBehavior.Attack(target, maxRange, maxChaseTime, isPersistent);
						}
					}
				}
			}
		}

		public Vector3? FindHerdCenter()
		{
			if (string.IsNullOrEmpty(HerdName)) return null;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			int count = 0;
			Vector3 center = Vector3.Zero;

			foreach (ComponentCreature componentCreature in m_subsystemCreatureSpawn.Creatures)
			{
				if (componentCreature.ComponentHealth.Health > 0f)
				{
					ComponentBanditHerdBehavior componentHerdBehavior = componentCreature.Entity.FindComponent<ComponentBanditHerdBehavior>();
					if (componentHerdBehavior != null && componentHerdBehavior.HerdName == HerdName)
					{
						Vector3 position2 = componentCreature.ComponentBody.Position;
						if (Vector3.DistanceSquared(position, position2) < m_herdingRange * m_herdingRange)
						{
							center += position2;
							count++;
						}
					}
				}
			}

			return count > 0 ? (Vector3?)(center / (float)count) : null;
		}

		public virtual void Update(float dt)
		{
			if (string.IsNullOrEmpty(m_stateMachine.CurrentState) || !IsActive)
			{
				m_stateMachine.TransitionTo("Inactive");
			}
			m_dt = dt;
			m_stateMachine.Update();
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);

			HerdName = valuesDictionary.GetValue<string>("HerdName");
			m_herdingRange = valuesDictionary.GetValue<float>("HerdingRange");
			m_autoNearbyCreaturesHelp = valuesDictionary.GetValue<bool>("AutoNearbyCreaturesHelp");

			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;

			// Evitar daño entre bandidos de la misma manada
			if (HerdName == "bandits")
			{
				componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(delegate (Injury injury)
				{
					if (injury.Attacker != null)
					{
						ComponentBanditHerdBehavior attackerHerd = injury.Attacker.Entity.FindComponent<ComponentBanditHerdBehavior>();
						if (attackerHerd != null && attackerHerd.HerdName == "bandits")
						{
							injury.Amount = 0f;
						}
					}
				}));
			}

			// Pedir ayuda al ser atacado
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				CallNearbyCreaturesHelp(attacker, 20f, 30f, false);
			}));

			// Restaurado al comportamiento original sin verificar AttackAllCreatures
			m_stateMachine.AddState("Inactive", null, delegate
			{
				if (m_subsystemTime.PeriodicGameTimeEvent(1.0, (double)(1f * (GetHashCode() % 256) / 256f)))
				{
					Vector3? center = FindHerdCenter();
					if (center != null)
					{
						float num = Vector3.Distance(center.Value, m_componentCreature.ComponentBody.Position);
						if (num > 10f) m_importanceLevel = 1f;
						if (num > 12f) m_importanceLevel = 3f;
						if (num > 16f) m_importanceLevel = 50f;
						if (num > 20f) m_importanceLevel = 250f;
					}
				}
				if (IsActive)
					m_stateMachine.TransitionTo("Herd");
			}, null);

			m_stateMachine.AddState("Stuck", delegate
			{
				m_stateMachine.TransitionTo("Herd");
				if (m_random.Bool(0.5f))
				{
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
					m_importanceLevel = 0f;
				}
			}, null, null);

			m_stateMachine.AddState("Herd", delegate
			{
				Vector3? center = FindHerdCenter();
				if (center != null && Vector3.Distance(m_componentCreature.ComponentBody.Position, center.Value) > 6f)
				{
					float speed = (m_importanceLevel > 10f) ? m_random.Float(0.9f, 1f) : m_random.Float(0.25f, 0.35f);
					int maxPathfindingPositions = (m_importanceLevel > 200f) ? 100 : 0;
					m_componentPathfinding.SetDestination(center, speed, 7f, maxPathfindingPositions, false, true, false, null);
					return;
				}
				m_importanceLevel = 0f;
			}, delegate
			{
				m_componentCreature.ComponentLocomotion.LookOrder = m_look - m_componentCreature.ComponentLocomotion.LookAngles;
				if (m_componentPathfinding.IsStuck)
					m_stateMachine.TransitionTo("Stuck");
				if (m_componentPathfinding.Destination == null)
					m_importanceLevel = 0f;
				if (m_random.Float(0f, 1f) < 0.05f * m_dt)
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				if (m_random.Float(0f, 1f) < 1.5f * m_dt)
					m_look = new Vector2(MathUtils.DegToRad(45f) * m_random.Float(-1f, 1f), MathUtils.DegToRad(10f) * m_random.Float(-1f, 1f));
			}, null);
		}

		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private readonly StateMachine m_stateMachine = new StateMachine();
		private float m_dt;
		private float m_importanceLevel;
		private readonly Random m_random = new Random();
		private Vector2 m_look;
		private float m_herdingRange;
		private bool m_autoNearbyCreaturesHelp;
	}
}
