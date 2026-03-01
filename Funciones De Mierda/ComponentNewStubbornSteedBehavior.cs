using System;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewStubbornSteedBehavior : ComponentBehavior, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		public bool IsEnabled { get; set; } = true;
		public float StubbornProbability { get; set; } = 0.5f;

		public virtual void Update(float dt)
		{
			m_stateMachine.Update();
			if (!IsActive)
				m_stateMachine.TransitionTo("Inactive");

			if (m_subsystemTime.PeriodicGameTimeEvent(1.0, m_periodicEventOffset))
			{
				m_importanceLevel = (m_subsystemGameInfo.TotalElapsedGameTime < m_stubbornEndTime &&
									 m_componentEatPickableBehavior.Satiation <= 0f &&
									 m_componentNewMount != null && m_componentNewMount.Rider != null) ? 210f : 0f;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentNewMount = Entity.FindComponent<ComponentNewMount>(true);
			m_componentNewSteedBehavior = Entity.FindComponent<ComponentNewSteedBehavior>(true);
			m_componentEatPickableBehavior = Entity.FindComponent<ComponentEatPickableBehavior>(true);

			IsEnabled = valuesDictionary.GetValue<bool>("IsEnabled", true);
			StubbornProbability = valuesDictionary.GetValue<float>("StubbornProbability", 0.5f);
			m_stubbornEndTime = valuesDictionary.GetValue<double>("StubbornEndTime", 0.0);
			m_periodicEventOffset = m_random.Float(0f, 100f);

			m_stateMachine.AddState("Inactive", null, delegate
			{
				if (IsEnabled && m_subsystemTime.PeriodicGameTimeEvent(1.0, m_periodicEventOffset) &&
					m_componentNewMount != null && m_componentNewMount.Rider != null &&
					m_random.Float(0f, 1f) < StubbornProbability &&
					m_componentEatPickableBehavior.Satiation <= 0f)
				{
					m_stubbornEndTime = m_subsystemGameInfo.TotalElapsedGameTime + m_random.Float(60f, 120f);
				}
				if (IsActive)
					m_stateMachine.TransitionTo("Stubborn");
			}, null);

			m_stateMachine.AddState("Stubborn", null, delegate
			{
				if (m_componentNewSteedBehavior.WasOrderIssued)
				{
					m_componentCreature.ComponentCreatureModel.HeadShakeOrder = m_random.Float(0.6f, 1f);
					m_componentCreature.ComponentCreatureSounds.PlayPainSound();
				}
			}, null);

			m_stateMachine.TransitionTo("Inactive");
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue("StubbornEndTime", m_stubbornEndTime);
		}

		private SubsystemTime m_subsystemTime;
		private SubsystemGameInfo m_subsystemGameInfo;
		private ComponentCreature m_componentCreature;
		private ComponentNewMount m_componentNewMount;
		private ComponentNewSteedBehavior m_componentNewSteedBehavior;
		private ComponentEatPickableBehavior m_componentEatPickableBehavior;
		private StateMachine m_stateMachine = new StateMachine();
		private float m_importanceLevel;
		private Random m_random = new Random();
		private float m_periodicEventOffset;
		private double m_stubbornEndTime;
	}
}
