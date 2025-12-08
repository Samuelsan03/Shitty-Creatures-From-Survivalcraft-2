using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;
using WonderfulEra;

namespace Game
{
	// Token: 0x02000096 RID: 150
	public class ComponentPoisonInfectedBehavior : ComponentBehavior, IUpdateable
	{
		// Token: 0x17000044 RID: 68
		// (get) Token: 0x06000497 RID: 1175 RVA: 0x00018C9A File Offset: 0x00016E9A
		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		// Token: 0x06000498 RID: 1176 RVA: 0x00018CA2 File Offset: 0x00016EA2
		public void Update(float dt)
		{
			this.m_stateMachine.Update();
		}

		// Token: 0x06000499 RID: 1177 RVA: 0x00018CB0 File Offset: 0x00016EB0
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

		// Token: 0x0600049A RID: 1178 RVA: 0x00018D38 File Offset: 0x00016F38
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_chaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>();
			this.m_poisonIntensity = valuesDictionary.GetValue<float>("PoisonIntensity");
			this.m_stateMachine.AddState("Inactive", delegate
			{
				this.m_importanceLevel = 0f;
			}, delegate
			{
				ComponentCreature target = null;
				ComponentChaseBehavior chaseBehavior = this.m_chaseBehavior;
				if (chaseBehavior != null)
				{
					target = chaseBehavior.m_target;
				}

				this.m_target = target;
				if (this.m_target != null && ((double)this.m_random.Float(0f, 1f) < 5.0 * (double)this.m_subsystemTime.GameTimeDelta || this.m_componentCreature.ComponentHealth.Health < 0.85f) && this.m_componentCreature.ComponentCreatureModel.IsAttackHitMoment)
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

		// Token: 0x0400024C RID: 588
		private SubsystemTime m_subsystemTime;

		// Token: 0x0400024D RID: 589
		private ComponentCreature m_componentCreature;

		// Token: 0x0400024F RID: 591
		private ComponentChaseBehavior m_chaseBehavior;

		// Token: 0x04000250 RID: 592
		private readonly StateMachine m_stateMachine = new StateMachine();

		// Token: 0x04000251 RID: 593
		private readonly Game.Random m_random = new Game.Random();

		// Token: 0x04000252 RID: 594
		private float m_importanceLevel;

		// Token: 0x04000253 RID: 595
		public float m_poisonIntensity;

		// Token: 0x04000254 RID: 596
		private ComponentCreature m_target;
	}
}
