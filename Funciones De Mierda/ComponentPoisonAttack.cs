using System;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000030 RID: 48¿
	public class ComponentPoisonAttack : Component, IUpdateable
	{
		// Token: 0x17000036 RID: 54
		// (get) Token: 0x06000171 RID: 369 RVA: 0x000105CC File Offset: 0x0000E7CC
		// (set) Token: 0x06000172 RID: 370 RVA: 0x000105E4 File Offset: 0x0000E7E4
		public float SicknessProbability
		{
			get
			{
				return this.m_sicknessProbability;
			}
			set
			{
				this.m_sicknessProbability = MathUtils.Clamp(value, 0f, 1f);
			}
		}

		// Token: 0x17000037 RID: 55
		// (get) Token: 0x06000173 RID: 371 RVA: 0x00010600 File Offset: 0x0000E800
		// (set) Token: 0x06000174 RID: 372 RVA: 0x00010618 File Offset: 0x0000E818
		public float SicknessDuration
		{
			get
			{
				return this.m_sicknessDuration;
			}
			set
			{
				this.m_sicknessDuration = MathUtils.Max(value, 0f);
			}
		}

		// Token: 0x17000038 RID: 56
		// (get) Token: 0x06000175 RID: 373 RVA: 0x0001062C File Offset: 0x0000E82C
		// (set) Token: 0x06000176 RID: 374 RVA: 0x00010644 File Offset: 0x0000E844
		public float SicknessCooldown
		{
			get
			{
				return this.m_sicknessCooldown;
			}
			set
			{
				this.m_sicknessCooldown = MathUtils.Max(value, 0f);
			}
		}

		// Token: 0x17000039 RID: 57
		// (get) Token: 0x06000177 RID: 375 RVA: 0x00010658 File Offset: 0x0000E858
		// (set) Token: 0x06000178 RID: 376 RVA: 0x00010670 File Offset: 0x0000E870
		public float AttackRange
		{
			get
			{
				return this.m_attackRange;
			}
			set
			{
				this.m_attackRange = MathUtils.Max(value, 0f);
			}
		}

		// Token: 0x1700003A RID: 58
		// (get) Token: 0x06000179 RID: 377 RVA: 0x00010684 File Offset: 0x0000E884
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x0600017A RID: 378 RVA: 0x00010688 File Offset: 0x0000E888
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_componentHealth = base.Entity.FindComponent<ComponentHealth>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>();
			this.m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			this.SicknessProbability = valuesDictionary.GetValue<float>("SicknessProbability");
			this.SicknessDuration = valuesDictionary.GetValue<float>("SicknessDuration");
			this.SicknessCooldown = valuesDictionary.GetValue<float>("SicknessCooldown");
			this.AttackRange = valuesDictionary.GetValue<float>("AttackRange");
		}

		// Token: 0x0600017B RID: 379 RVA: 0x00010738 File Offset: 0x0000E938
		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<float>("SicknessProbability", this.SicknessProbability);
			valuesDictionary.SetValue<float>("SicknessDuration", this.SicknessDuration);
			valuesDictionary.SetValue<float>("SicknessCooldown", this.SicknessCooldown);
			valuesDictionary.SetValue<float>("AttackRange", this.AttackRange);
		}

		// Token: 0x0600017C RID: 380 RVA: 0x00010790 File Offset: 0x0000E990
		public void Update(float dt)
		{
			bool flag = this.m_subsystemTime.GameTime - this.m_lastSicknessTime < (double)this.m_sicknessCooldown;
			if (!flag)
			{
				bool flag2 = !this.IsAttackingPlayer();
				if (!flag2)
				{
					Random random = new Random();
					bool flag3 = random.Float(0f, 1f) > this.m_sicknessProbability * dt * 60f;
					if (!flag3)
					{
						ComponentPlayer attackTargetPlayer = this.GetAttackTargetPlayer();
						bool flag4 = attackTargetPlayer == null;
						if (!flag4)
						{
							ComponentSickness componentSickness = attackTargetPlayer.Entity.FindComponent<ComponentSickness>();
							bool flag5 = componentSickness != null && !componentSickness.IsSick;
							if (flag5)
							{
								componentSickness.StartSickness();
								bool flag6 = this.m_sicknessDuration != 900f;
								if (flag6)
								{
									componentSickness.m_sicknessDuration = MathUtils.Max(componentSickness.m_sicknessDuration, this.m_sicknessDuration);
								}
								this.m_lastSicknessTime = this.m_subsystemTime.GameTime;
								bool flag7 = this.m_componentCreature.ComponentCreatureSounds != null;
								if (flag7)
								{
									this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x0600017D RID: 381 RVA: 0x000108B0 File Offset: 0x0000EAB0
		private bool IsAttackingPlayer()
		{
			bool flag = this.m_componentChaseBehavior != null && this.m_componentChaseBehavior.IsActive;
			if (flag)
			{
				ComponentCreature target = this.m_componentChaseBehavior.Target;
				bool flag2 = target != null && target.Entity.FindComponent<ComponentPlayer>() != null;
				if (flag2)
				{
					float num = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);
					bool flag3 = num <= this.m_attackRange;
					if (flag3)
					{
						return true;
					}
				}
			}
			return this.m_componentCreatureModel != null && this.m_componentCreatureModel.IsAttackHitMoment;
		}

		// Token: 0x0600017E RID: 382 RVA: 0x00010964 File Offset: 0x0000EB64
		private ComponentPlayer GetAttackTargetPlayer()
		{
			bool flag = this.m_componentChaseBehavior != null && this.m_componentChaseBehavior.IsActive;
			if (flag)
			{
				ComponentCreature target = this.m_componentChaseBehavior.Target;
				bool flag2 = target != null;
				if (flag2)
				{
					return target.Entity.FindComponent<ComponentPlayer>();
				}
			}
			SubsystemPlayers subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			ComponentPlayer componentPlayer = subsystemPlayers.FindNearestPlayer(this.m_componentCreature.ComponentBody.Position);
			bool flag3 = componentPlayer != null;
			if (flag3)
			{
				float num = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, componentPlayer.ComponentBody.Position);
				bool flag4 = num <= this.m_attackRange;
				if (flag4)
				{
					return componentPlayer;
				}
			}
			return null;
		}

		// Token: 0x0600017F RID: 383 RVA: 0x00010A26 File Offset: 0x0000EC26
		public override void Dispose()
		{
			base.Dispose();
		}

		// Token: 0x04000195 RID: 405
		public SubsystemTime m_subsystemTime;

		// Token: 0x04000196 RID: 406
		public ComponentHealth m_componentHealth;

		// Token: 0x04000197 RID: 407
		public ComponentCreature m_componentCreature;

		// Token: 0x04000198 RID: 408
		public ComponentChaseBehavior m_componentChaseBehavior;

		// Token: 0x04000199 RID: 409
		public ComponentCreatureModel m_componentCreatureModel;

		// Token: 0x0400019A RID: 410
		public float m_sicknessProbability;

		// Token: 0x0400019B RID: 411
		public float m_sicknessDuration;

		// Token: 0x0400019C RID: 412
		public double m_lastSicknessTime;

		// Token: 0x0400019D RID: 413
		public float m_sicknessCooldown;

		// Token: 0x0400019E RID: 414
		public float m_attackRange = 2f;
	}
}
