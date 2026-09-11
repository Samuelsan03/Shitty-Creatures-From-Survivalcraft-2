using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNoiseAttraction : ComponentBehavior, INoiseAttractListener, IUpdateable
	{
		// ComponentBehavior requirements
		public override float ImportanceLevel { get { return 10f; } }

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Dependencies
		private SubsystemTime m_subsystemTime;
		private SubsystemAttractNoise m_subsystemAttractNoise;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentBody m_componentBody;

		// State Machine
		private StateMachine m_stateMachine = new StateMachine();

		// Logic Variables
		private Vector3? m_noisePosition;
		private double m_investigationEndTime;
		private float m_investigationDuration = 4f; // Seconds to look around
		private float m_attractRange = 20f;

		// Load method: No dictionaries for values, just normal initialization
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Find dependencies
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemAttractNoise = Project.FindSubsystem<SubsystemAttractNoise>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentBody = m_componentCreature.ComponentBody;

			// --- Define States ---

			// State: Idle (Waiting for noise)
			m_stateMachine.AddState("Idle", null, delegate
			{
				// If we have a target, start moving
				if (m_noisePosition.HasValue)
				{
					m_stateMachine.TransitionTo("Attracted");
				}
			}, null);

			// State: Attracted (Moving towards the noise)
			m_stateMachine.AddState("Attracted", delegate
			{
				if (m_noisePosition.HasValue)
				{
					// Start moving to the source
					float speed = m_componentCreature.ComponentLocomotion.WalkSpeed;
					m_componentPathfinding.SetDestination(
						m_noisePosition,
						speed,
						0.5f,
						100, // Max pathfinding nodes
						false,
						false,
						true, // Raycast destination
						null
					);
				}
				else
				{
					m_stateMachine.TransitionTo("Idle");
				}
			}, delegate
			{
				if (m_noisePosition.HasValue)
				{
					float distanceSquared = Vector3.DistanceSquared(m_componentBody.Position, m_noisePosition.Value);

					// If we arrived close enough, or if the pathfinding got stuck/is done
					if (distanceSquared < 2f || m_componentPathfinding.Destination == null || m_componentPathfinding.IsStuck)
					{
						m_stateMachine.TransitionTo("Investigating");
					}
				}
				else
				{
					m_componentPathfinding.Stop();
					m_stateMachine.TransitionTo("Idle");
				}
			}, null);

			// State: Investigating (Looking around at the source)
			m_stateMachine.AddState("Investigating", delegate
			{
				// Stop moving and set timer
				m_componentPathfinding.Stop();
				m_investigationEndTime = m_subsystemTime.GameTime + m_investigationDuration;
			}, delegate
			{
				// Simple rotation to look around (optional visual effect)
				// m_componentBody.Rotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1f * Time.FrameDuration);

				// Check if time is up
				if (m_subsystemTime.GameTime >= m_investigationEndTime)
				{
					// Clear the target and go back to idle
					m_noisePosition = null;
					m_stateMachine.TransitionTo("Idle");
				}
			}, null);

			// Start state
			m_stateMachine.TransitionTo("Idle");
		}

		// Implementation of INoiseAttractListener
		public void AttractedToNoise(ComponentBody sourceBody, Vector3 sourcePosition, float lureStrength)
		{
			// Set the target
			m_noisePosition = sourcePosition;

			// If we are doing nothing, start moving immediately
			if (m_stateMachine.CurrentState == "Idle")
			{
				m_stateMachine.TransitionTo("Attracted");
			}
			// If we are already investigating but hear a new noise, interrupt and go to it
			else if (m_stateMachine.CurrentState == "Investigating")
			{
				m_stateMachine.TransitionTo("Attracted");
			}
			// If already Attracted, the Update loop will recalculate path automatically
		}

		// IUpdateable implementation
		public void Update(float dt)
		{
			m_stateMachine.Update();
		}
	}
}
