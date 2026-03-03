// ComponentFluInfectBehavior.cs
using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentFluInfectBehavior : ComponentBehavior, IUpdateable
	{
		public override float ImportanceLevel => m_importanceLevel;

		public void Update(float dt)
		{
			m_stateMachine.Update();
		}

		public bool StartFlu(ComponentCreature target)
		{
			if (target == null)
				return false;

			// 1. Intentar con el sistema de criaturas (ComponentFluInfected)
			var targetFlu = target.Entity.FindComponent<ComponentFluInfected>();
			if (targetFlu != null)
			{
				if (targetFlu.IsInfected)
					return true; // ya infectado

				targetFlu.StartFlu(m_fluIntensity);
				return targetFlu.IsInfected;
			}

			// 2. Si el target es un jugador, usar su ComponentFlu nativo
			var player = target as ComponentPlayer;
			if (player != null && player.ComponentFlu != null)
			{
				if (player.ComponentFlu.HasFlu)
					return true;

				player.ComponentFlu.StartFlu();
				return player.ComponentFlu.HasFlu;
			}

			// 3. No se puede infectar (no tiene componente de gripe)
			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_newChaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_chaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();

			m_fluIntensity = valuesDictionary.GetValue<float>("FluIntensity");
			m_infectProbability = valuesDictionary.GetValue<float>("InfectProbability", 1f);

			m_stateMachine.AddState("Inactive", null, delegate
			{
				m_importanceLevel = 0f;

				ComponentCreature target = null;
				if (m_newChaseBehavior != null)
					target = m_newChaseBehavior.m_target;
				if (target == null && m_chaseBehavior != null)
					target = m_chaseBehavior.m_target;
				m_target = target;

				if (m_target != null && m_componentCreature.ComponentCreatureModel.IsAttackHitMoment)
				{
					if (m_random.Float(0f, 1f) < m_infectProbability)
					{
						m_importanceLevel = 201f;
					}
				}

				if (IsActive)
					m_stateMachine.TransitionTo("FluInfect");
			}, null);

			m_stateMachine.AddState("FluInfect", delegate
			{
				if (m_target == null)
					return;

				m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(m_target.ComponentCreatureModel.EyePosition);
				m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
			}, delegate
			{
				if (StartFlu(m_target))
				{
					// Hacer que la criatura huya después de infectar
					var runAway = m_componentCreature.Entity.FindComponent<ComponentRunAwayBehavior>();
					if (runAway != null)
						runAway.RunAwayFrom(m_target.ComponentBody);

					var newRunAway = m_componentCreature.Entity.FindComponent<ComponentNewRunAwayBehavior>();
					if (newRunAway != null)
						newRunAway.RunAwayFrom(m_target.ComponentBody);

					m_stateMachine.TransitionTo("Inactive");
				}
				else if (!IsActive || m_target == null)
				{
					m_stateMachine.TransitionTo("Inactive");
				}
			}, null);

			m_stateMachine.TransitionTo("Inactive");
		}

		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_componentCreature;
		private ComponentNewChaseBehavior m_newChaseBehavior;
		private ComponentChaseBehavior m_chaseBehavior;
		private readonly StateMachine m_stateMachine = new StateMachine();
		private readonly Game.Random m_random = new Game.Random();
		private float m_importanceLevel;
		private float m_fluIntensity;
		private float m_infectProbability;
		private ComponentCreature m_target;
	}
}
