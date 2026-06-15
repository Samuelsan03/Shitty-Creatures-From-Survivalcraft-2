using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x020000D4 RID: 212
	public class ComponentPoisonInfectedBehavior : ComponentBehavior, IUpdateable
	{
		// Token: 0x1700005C RID: 92
		// (get) Token: 0x06000667 RID: 1639 RVA: 0x00023172 File Offset: 0x00021372
		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		// Token: 0x06000668 RID: 1640 RVA: 0x0002317A File Offset: 0x0002137A
		public void Update(float dt)
		{
			this.m_stateMachine.Update();
		}

		// Token: 0x06000669 RID: 1641 RVA: 0x00023188 File Offset: 0x00021388
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

		// Token: 0x0600066A RID: 1642 RVA: 0x00023210 File Offset: 0x00021410
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_newChaseBehavior = base.Entity.FindComponent<ComponentNewChaseBehavior>();
			this.m_chaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>();
			this.m_zombieChaseBehavior = base.Entity.FindComponent<ComponentZombieChaseBehavior>(); // Agregado
			this.m_poisonIntensity = valuesDictionary.GetValue<float>("PoisonIntensity");

			this.m_stateMachine.AddState("Inactive", delegate
			{
				this.m_importanceLevel = 0f;
			}, delegate
			{
				ComponentCreature target = null;

				// 1. Verificar ComponentNewChaseBehavior
				ComponentNewChaseBehavior newChaseBehavior = this.m_newChaseBehavior;
				if (newChaseBehavior != null)
				{
					target = newChaseBehavior.Target;
				}

				// 2. Verificar ComponentZombieChaseBehavior (Hereda de ComponentChaseBehavior, por lo que tiene la propiedad Target)
				if (target == null && this.m_zombieChaseBehavior != null)
				{
					target = this.m_zombieChaseBehavior.Target;
				}

				// 3. Verificar ComponentChaseBehavior estándar
				if (target == null)
				{
					ComponentChaseBehavior chaseBehavior = this.m_chaseBehavior;
					if (chaseBehavior != null)
					{
						target = chaseBehavior.Target;
					}
				}

				this.m_target = target;

				if (this.m_target != null && this.m_componentCreature.ComponentCreatureModel.IsAttackHitMoment)
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

		// Token: 0x04000397 RID: 919
		private SubsystemTime m_subsystemTime;

		// Token: 0x04000398 RID: 920
		private ComponentCreature m_componentCreature;

		// Token: 0x04000399 RID: 921
		private ComponentNewChaseBehavior m_newChaseBehavior;

		// Token: 0x0400039A RID: 922
		private ComponentChaseBehavior m_chaseBehavior;

		// Campo agregado para el comportamiento de zombi
		private ComponentZombieChaseBehavior m_zombieChaseBehavior;

		// Token: 0x0400039B RID: 923
		private readonly StateMachine m_stateMachine = new StateMachine();

		// Token: 0x0400039C RID: 924
		private readonly Game.Random m_random = new Game.Random();

		// Token: 0x0400039D RID: 925
		private float m_importanceLevel;

		// Token: 0x0400039E RID: 926
		public float m_poisonIntensity;

		// Token: 0x0400039F RID: 927
		private ComponentCreature m_target;
	}
}
