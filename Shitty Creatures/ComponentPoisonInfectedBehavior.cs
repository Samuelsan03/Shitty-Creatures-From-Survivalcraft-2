using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentPoisonInfectedBehavior : ComponentBehavior, IUpdateable
	{
		public override float ImportanceLevel
		{
			get { return this.m_importanceLevel; }
		}

		public void Update(float dt)
		{
			this.m_stateMachine.Update();
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
						componentPlayer.ComponentSickness.m_sicknessDuration = this.m_poisonIntensity - componentPoisonInfected.PoisonResistance;
					return componentPlayer.ComponentSickness.IsSick;
				}
				else if (componentPoisonInfected != null)
				{
					if (componentPoisonInfected.IsInfected)
						return true;
					componentPoisonInfected.StartInfect(this.m_poisonIntensity);
					return componentPoisonInfected.IsInfected;
				}
			}
			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_newChaseBehavior = base.Entity.FindComponent<ComponentNewChaseBehavior>();
			this.m_chaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>();
			this.m_zombieChaseBehavior = base.Entity.FindComponent<ComponentZombieChaseBehavior>();
			this.m_poisonIntensity = valuesDictionary.GetValue<float>("PoisonIntensity");
			this.m_infectProbability = valuesDictionary.GetValue<float>("InfectProbability", 1f); // Nuevo parámetro, por defecto 1 (100%)

			this.m_stateMachine.AddState("Inactive", delegate
			{
				this.m_importanceLevel = 0f;
			}, delegate
			{
				ComponentCreature target = null;

				if (this.m_newChaseBehavior != null)
					target = this.m_newChaseBehavior.Target;

				if (target == null && this.m_zombieChaseBehavior != null)
					target = this.m_zombieChaseBehavior.Target;

				if (target == null && this.m_chaseBehavior != null)
					target = this.m_chaseBehavior.Target;

				this.m_target = target;

				// Condición modificada: ahora incluye la probabilidad de infección
				if (this.m_target != null &&
					this.m_componentCreature.ComponentCreatureModel.IsAttackHitMoment &&
					this.m_random.Float() < this.m_infectProbability)
				{
					this.m_importanceLevel = 201f;
				}

				if (!this.IsActive)
					return;

				this.m_stateMachine.TransitionTo("PoisonInfect");
			}, null);

			this.m_stateMachine.AddState("PoisonInfect", delegate
			{
				if (this.m_target == null)
					return;
				this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
				this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
			}, delegate
			{
				if (this.StartInfect(this.m_target))
				{
					ComponentRunAwayBehavior componentRunAwayBehavior = this.m_componentCreature.Entity.FindComponent<ComponentRunAwayBehavior>();
					if (componentRunAwayBehavior != null)
						componentRunAwayBehavior.RunAwayFrom(this.m_target.ComponentBody);

					ComponentNewRunAwayBehavior componentNewRunAwayBehavior = this.m_componentCreature.Entity.FindComponent<ComponentNewRunAwayBehavior>();
					if (componentNewRunAwayBehavior != null)
						componentNewRunAwayBehavior.RunAwayFrom(this.m_target.ComponentBody);

					this.m_stateMachine.TransitionTo("Inactive");
				}
				if (this.IsActive && this.m_target != null)
					return;
				this.m_stateMachine.TransitionTo("Inactive");
			}, null);

			this.m_stateMachine.TransitionTo("Inactive");
		}

		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_componentCreature;
		private ComponentNewChaseBehavior m_newChaseBehavior;
		private ComponentChaseBehavior m_chaseBehavior;
		private ComponentZombieChaseBehavior m_zombieChaseBehavior;
		private readonly StateMachine m_stateMachine = new StateMachine();
		private readonly Game.Random m_random = new Game.Random();
		private float m_importanceLevel;
		public float m_poisonIntensity;
		private ComponentCreature m_target;
		private float m_infectProbability; // Nuevo campo: probabilidad de infectar al golpear (0..1)
	}
}
