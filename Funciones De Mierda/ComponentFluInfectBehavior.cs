using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Comportamiento que permite a una criatura infectar a su objetivo con gripe durante el ataque.
	/// Utiliza únicamente ComponentChaseBehavior (estándar del juego).
	/// </summary>
	public class ComponentFluInfectBehavior : ComponentBehavior, IUpdateable
	{
		public override float ImportanceLevel => m_importanceLevel;

		public void Update(float dt)
		{
			m_stateMachine.Update();
		}

		private bool StartInfect(ComponentCreature target)
		{
			if (target == null)
				return false;

			// Por ahora, solo infectamos criaturas no jugador (se puede ampliar después)
			if (target is ComponentPlayer)
				return false;

			var fluInfected = target.Entity.FindComponent<ComponentFluInfected>();
			if (fluInfected == null)
				return false;

			if (fluInfected.IsInfected)
				return true;

			fluInfected.StartFlu(m_fluIntensity);
			return fluInfected.IsInfected;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_chaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();

			m_fluIntensity = valuesDictionary.GetValue<float>("FluIntensity");
			m_infectProbability = valuesDictionary.GetValue<float>("InfectProbability", 1f);

			m_stateMachine.AddState("Inactive", delegate
			{
				m_importanceLevel = 0f;
			}, delegate
			{
				if (m_chaseBehavior != null)
				{
					m_target = m_chaseBehavior.m_target;
				}

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
				if (StartInfect(m_target))
				{
					var runAway = m_componentCreature.Entity.FindComponent<ComponentRunAwayBehavior>();
					runAway?.RunAwayFrom(m_target.ComponentBody);
					m_stateMachine.TransitionTo("Inactive");
				}
				else if (IsActive && m_target != null)
				{
					// permanece
				}
				else
				{
					m_stateMachine.TransitionTo("Inactive");
				}
			}, null);

			m_stateMachine.TransitionTo("Inactive");
		}

		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_componentCreature;
		private ComponentChaseBehavior m_chaseBehavior;

		private readonly StateMachine m_stateMachine = new StateMachine();
		private readonly Game.Random m_random = new Game.Random();
		private float m_importanceLevel;
		private ComponentCreature m_target;

		public float m_fluIntensity;
		private float m_infectProbability = 1f;
	}
}
