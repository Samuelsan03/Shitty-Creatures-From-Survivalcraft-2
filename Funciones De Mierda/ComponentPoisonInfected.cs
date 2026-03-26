using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x020000C4 RID: 196
	public class ComponentPoisonInfectBehavior : ComponentBehavior, IUpdateable
	{
		// Token: 0x17000055 RID: 85
		// (get) Token: 0x060005DC RID: 1500 RVA: 0x0001F6B2 File Offset: 0x0001D8B2
		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		// Token: 0x060005DD RID: 1501 RVA: 0x0001F6BA File Offset: 0x0001D8BA
		public void Update(float dt)
		{
			this.m_stateMachine.Update();
		}

		// Token: 0x060005DE RID: 1502 RVA: 0x0001F6C8 File Offset: 0x0001D8C8
		public bool StartInfect(ComponentCreature target)
		{
			if (target != null)
			{
				ComponentPoisonInfected componentPoisonInfected = target.Entity.FindComponent<ComponentPoisonInfected>();
				ComponentPlayer componentPlayer = target as ComponentPlayer;
				if (componentPlayer != null)
				{
					if (componentPlayer.ComponentSickness.IsSick)
					{
						return true;
					}
					componentPlayer.ComponentSickness.StartSickness();
					if (componentPoisonInfected != null)
					{
						componentPlayer.ComponentSickness.m_sicknessDuration = this.m_poisonIntensity - componentPoisonInfected.PoisonResistance;
					}
					return componentPlayer.ComponentSickness.IsSick;
				}
				else if (componentPoisonInfected != null)
				{
					if (componentPoisonInfected.IsInfected)
					{
						return true;
					}
					componentPoisonInfected.StartInfect(this.m_poisonIntensity);
					return componentPoisonInfected.IsInfected;
				}
			}
			return false;
		}

		// Token: 0x060005DF RID: 1503 RVA: 0x0001F750 File Offset: 0x0001D950
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_newChaseBehavior = base.Entity.FindComponent<ComponentNewChaseBehavior>();
			this.m_chaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>();
			this.m_poisonIntensity = valuesDictionary.GetValue<float>("PoisonIntensity");
			this.m_infectProbability = valuesDictionary.GetValue<float>("InfectProbability", 1f);
			this.m_stateMachine.AddState("Inactive", delegate
			{
				this.m_importanceLevel = 0f;
			}, delegate
			{
				ComponentNewChaseBehavior newChaseBehavior = this.m_newChaseBehavior;
				ComponentCreature target = null;

				// Usar la propiedad pública Target en lugar del campo privado m_target
				if (newChaseBehavior != null)
				{
					target = newChaseBehavior.Target;
				}
				if (target == null)
				{
					ComponentChaseBehavior chaseBehavior = this.m_chaseBehavior;
					if (chaseBehavior != null)
					{
						target = chaseBehavior.m_target;
					}
				}
				this.m_target = target;

				if (this.m_target != null && this.m_componentCreature.ComponentCreatureModel.IsAttackHitMoment && (double)this.m_random.Float(0f, 1f) < (double)this.m_infectProbability)
				{
					this.m_importanceLevel = 201f;
				}
				if (!this.IsActive)
				{
					return;
				}
				this.m_stateMachine.TransitionTo("PoisonInfect");
			}, null);
			this.m_stateMachine.AddState("PoisonInfect", delegate
			{
				if (this.m_target == null)
				{
					return;
				}
				this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
				this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
			}, delegate
			{
				if (this.StartInfect(this.m_target))
				{
					ComponentRunAwayBehavior componentRunAwayBehavior = this.m_componentCreature.Entity.FindComponent<ComponentRunAwayBehavior>();
					if (componentRunAwayBehavior != null)
					{
						componentRunAwayBehavior.RunAwayFrom(this.m_target.ComponentBody);
					}
					ComponentNewRunAwayBehavior componentNewRunAwayBehavior = this.m_componentCreature.Entity.FindComponent<ComponentNewRunAwayBehavior>();
					if (componentNewRunAwayBehavior != null)
					{
						componentNewRunAwayBehavior.RunAwayFrom(this.m_target.ComponentBody);
					}
					this.m_stateMachine.TransitionTo("Inactive");
				}
				if (this.IsActive && this.m_target != null)
				{
					return;
				}
				this.m_stateMachine.TransitionTo("Inactive");
			}, null);
			this.m_stateMachine.TransitionTo("Inactive");
		}

		// Token: 0x04000341 RID: 833
		private SubsystemTime m_subsystemTime;

		// Token: 0x04000342 RID: 834
		private ComponentCreature m_componentCreature;

		// Token: 0x04000343 RID: 835
		private ComponentNewChaseBehavior m_newChaseBehavior;

		// Token: 0x04000344 RID: 836
		private ComponentChaseBehavior m_chaseBehavior;

		// Token: 0x04000345 RID: 837
		private readonly StateMachine m_stateMachine = new StateMachine();

		// Token: 0x04000346 RID: 838
		private readonly Game.Random m_random = new Game.Random();

		// Token: 0x04000347 RID: 839
		private float m_importanceLevel;

		// Token: 0x04000348 RID: 840
		public float m_poisonIntensity;

		// Token: 0x04000349 RID: 841
		private ComponentCreature m_target;

		// Token: 0x0400034A RID: 842
		private float m_infectProbability = 1f;
	}
}
