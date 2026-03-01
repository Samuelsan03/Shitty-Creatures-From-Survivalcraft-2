using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewDumpRiderBehavior : ComponentBehavior, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		public bool IsEnabled { get; set; } = false;

		public virtual void Update(float dt)
		{
			m_stateMachine.Update();
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentNewMount = Entity.FindComponent<ComponentNewMount>(true);

			IsEnabled = valuesDictionary.GetValue<bool>("IsEnabled", false);

			m_stateMachine.AddState("Inactive", delegate
			{
				m_importanceLevel = 0f;
				m_rider = null;
			}, delegate
			{
				if (IsEnabled && m_random.Float(0f, 1f) < 1f * m_subsystemTime.GameTimeDelta &&
					m_componentNewMount != null && m_componentNewMount.Rider != null)
				{
					m_importanceLevel = 220f;
					m_dumpStartTime = m_subsystemTime.GameTime;
					m_rider = m_componentNewMount.Rider;
				}
				if (IsActive)
					m_stateMachine.TransitionTo("WildJumping");
			}, null);

			m_stateMachine.AddState("WildJumping", delegate
			{
				m_componentCreature.ComponentCreatureSounds.PlayPainSound();
				m_componentPathfinding.Stop();
			}, delegate
			{
				if (!IsActive)
				{
					m_stateMachine.TransitionTo("Inactive");
				}
				else if (m_componentNewMount == null || m_componentNewMount.Rider == null)
				{
					m_importanceLevel = 0f;
					RunAway();
				}
				if (m_random.Float(0f, 1f) < 1f * m_subsystemTime.GameTimeDelta)
					m_componentCreature.ComponentCreatureSounds.PlayPainSound();
				if (m_random.Float(0f, 1f) < 3f * m_subsystemTime.GameTimeDelta)
					m_walkOrder = new Vector2(m_random.Float(-0.5f, 0.5f), m_random.Float(-0.5f, 1.5f));
				if (m_random.Float(0f, 1f) < 2.5f * m_subsystemTime.GameTimeDelta)
					m_turnOrder.X = m_random.Float(-1f, 1f);
				if (m_random.Float(0f, 1f) < 2f * m_subsystemTime.GameTimeDelta)
				{
					m_componentCreature.ComponentLocomotion.JumpOrder = m_random.Float(0.9f, 1f);
					if (m_componentNewMount != null && m_componentNewMount.Rider != null &&
						m_subsystemTime.GameTime - m_dumpStartTime > 3.0)
					{
						if (m_random.Float(0f, 1f) < 0.05f)
						{
							m_componentNewMount.Rider.StartDismounting();
							m_componentNewMount.Rider.ComponentCreature.ComponentHealth.Injure(
								m_random.Float(0.05f, 0.2f), m_componentCreature, false, "Thrown from a mount");
						}
						if (m_random.Float(0f, 1f) < 0.25f)
						{
							m_componentNewMount.Rider.ComponentCreature.ComponentHealth.Injure(
								0.05f, m_componentCreature, false, "Thrown from a mount");
						}
					}
				}
				if (m_random.Float(0f, 1f) < 4f * m_subsystemTime.GameTimeDelta)
					m_lookOrder = new Vector2(m_random.Float(-3f, 3f), m_lookOrder.Y);
				if (m_random.Float(0f, 1f) < 0.25f * m_subsystemTime.GameTimeDelta)
					TransitionToRandomDumpingBehavior();

				m_componentCreature.ComponentLocomotion.WalkOrder = new Vector2?(m_walkOrder);
				m_componentCreature.ComponentLocomotion.TurnOrder = m_turnOrder;
				m_componentCreature.ComponentLocomotion.LookOrder = m_lookOrder;
			}, null);

			m_stateMachine.AddState("BlindRacing", delegate
			{
				m_componentCreature.ComponentCreatureSounds.PlayPainSound();
				m_componentPathfinding.SetDestination(
					new Vector3?(m_componentCreature.ComponentBody.Position +
								 new Vector3(m_random.Float(-15f, 15f), 0f, m_random.Float(-15f, 15f))),
					1f, 2f, 0, false, true, false, null);
			}, delegate
			{
				if (!IsActive)
				{
					m_stateMachine.TransitionTo("Inactive");
				}
				else if (m_componentNewMount == null || m_componentNewMount.Rider == null)
				{
					m_importanceLevel = 0f;
					RunAway();
				}
				else if (m_componentPathfinding.Destination == null || m_componentPathfinding.IsStuck)
				{
					TransitionToRandomDumpingBehavior();
				}
				if (m_random.Float(0f, 1f) < 0.5f * m_subsystemTime.GameTimeDelta)
				{
					m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
					m_componentCreature.ComponentCreatureSounds.PlayPainSound();
				}
			}, null);

			m_stateMachine.AddState("Stupor", delegate
			{
				m_componentCreature.ComponentCreatureSounds.PlayPainSound();
				m_componentPathfinding.Stop();
			}, delegate
			{
				if (!IsActive)
				{
					m_stateMachine.TransitionTo("Inactive");
				}
				else if (m_componentNewMount == null || m_componentNewMount.Rider == null)
				{
					m_importanceLevel = 0f;
				}
				if (m_subsystemTime.PeriodicGameTimeEvent(2.0, 0.0))
					TransitionToRandomDumpingBehavior();
			}, null);

			m_stateMachine.TransitionTo("Inactive");
		}

		public virtual void TransitionToRandomDumpingBehavior()
		{
			float num = m_random.Float(0f, 1f);
			if (num < 0.5f)
				m_stateMachine.TransitionTo("WildJumping");
			else if (num < 0.8f)
				m_stateMachine.TransitionTo("BlindRacing");
			else
				m_stateMachine.TransitionTo("Stupor");
		}

		public virtual void RunAway()
		{
			if (m_rider != null)
			{
				ComponentRunAwayBehavior runAway = Entity.FindComponent<ComponentRunAwayBehavior>();
				if (runAway != null)
					runAway.RunAwayFrom(m_rider.ComponentCreature.ComponentBody);
			}
		}

		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentNewMount m_componentNewMount;
		private StateMachine m_stateMachine = new StateMachine();
		private float m_importanceLevel;
		private Random m_random = new Random();
		private ComponentRider m_rider;
		private double m_dumpStartTime;
		private Vector2 m_walkOrder;
		private Vector2 m_turnOrder;
		private Vector2 m_lookOrder;
	}
}
