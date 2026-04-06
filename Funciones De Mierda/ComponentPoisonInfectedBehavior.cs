using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentPoisonInfectedBehavior : ComponentBehavior, IUpdateable
	{
		public override float ImportanceLevel => m_importanceLevel;

		public void Update(float dt)
		{
			m_stateMachine.Update();
		}

		public bool StartInfect(ComponentCreature target)
		{
			if (target != null)
			{
				ComponentPoisonInfected componentPoisonInfected = target.Entity.FindComponent<ComponentPoisonInfected>();
				ComponentPlayer componentPlayer = target as ComponentPlayer;
				if (componentPlayer != null)
				{
					if (componentPlayer.ComponentSickness.IsSick)
						return true;
					componentPlayer.ComponentSickness.StartSickness();
					if (componentPoisonInfected != null)
						componentPlayer.ComponentSickness.m_sicknessDuration = m_poisonIntensity - componentPoisonInfected.PoisonResistance;
					return componentPlayer.ComponentSickness.IsSick;
				}
				else if (componentPoisonInfected != null)
				{
					if (componentPoisonInfected.IsInfected)
						return true;
					componentPoisonInfected.StartInfect(m_poisonIntensity);
					return componentPoisonInfected.IsInfected;
				}
			}
			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);

			// Obtener referencias a los tres tipos de chase behavior
			m_newChaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_oldChaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
			m_zombieChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>();

			m_poisonIntensity = valuesDictionary.GetValue<float>("PoisonIntensity");
			m_infectProbability = valuesDictionary.GetValue<float>("InfectProbability");

			// Estado Inactive: solo busca objetivo y decide si infectar
			m_stateMachine.AddState("Inactive", null, delegate
			{
				// Actualizar objetivo desde cualquier chase behavior
				m_target = GetChaseTarget();

				// Condición para activar la infección
				bool shouldInfect = m_target != null &&
									m_componentCreature.ComponentCreatureModel.IsAttackHitMoment &&
									m_random.Float(0f, 1f) < m_infectProbability;

				if (shouldInfect)
				{
					m_importanceLevel = 201f;
					m_stateMachine.TransitionTo("PoisonInfect");
				}
				else
				{
					m_importanceLevel = 0f;
					// Permanecer en Inactive
				}
			}, null);

			// Estado PoisonInfect: intenta infectar y luego huye o vuelve a Inactive
			m_stateMachine.AddState("PoisonInfect", delegate
			{
				if (m_target != null)
				{
					m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(m_target.ComponentCreatureModel.EyePosition);
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}
			}, delegate
			{
				bool infected = StartInfect(m_target);
				if (infected)
				{
					// Huir después de infectar
					ComponentRunAwayBehavior runAway = m_componentCreature.Entity.FindComponent<ComponentRunAwayBehavior>();
					if (runAway != null)
						runAway.RunAwayFrom(m_target.ComponentBody);

					ComponentNewRunAwayBehavior newRunAway = m_componentCreature.Entity.FindComponent<ComponentNewRunAwayBehavior>();
					if (newRunAway != null)
						newRunAway.RunAwayFrom(m_target.ComponentBody);
				}
				// Siempre volver a Inactive después de intentar infectar (tanto si tuvo éxito como si no)
				m_stateMachine.TransitionTo("Inactive");
			}, null);

			m_stateMachine.TransitionTo("Inactive");
		}

		private ComponentCreature GetChaseTarget()
		{
			if (m_newChaseBehavior != null && m_newChaseBehavior.Target != null)
				return m_newChaseBehavior.Target;

			if (m_zombieChaseBehavior != null && m_zombieChaseBehavior.Target != null)
				return m_zombieChaseBehavior.Target;

			if (m_oldChaseBehavior != null && m_oldChaseBehavior.Target != null)
				return m_oldChaseBehavior.Target;

			return null;
		}

		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_componentCreature;
		private ComponentNewChaseBehavior m_newChaseBehavior;
		private ComponentChaseBehavior m_oldChaseBehavior;
		private ComponentZombieChaseBehavior m_zombieChaseBehavior;
		private readonly StateMachine m_stateMachine = new StateMachine();
		private readonly Random m_random = new Random();
		private float m_importanceLevel;
		public float m_poisonIntensity;
		private float m_infectProbability;
		private ComponentCreature m_target;
	}
}
