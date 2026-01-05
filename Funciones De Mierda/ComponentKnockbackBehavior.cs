using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000021 RID: 33
	public class ComponentKnockbackBehavior : Component, IUpdateable
	{
		// Token: 0x1700003C RID: 60
		// (get) Token: 0x060000F9 RID: 249 RVA: 0x00002A30 File Offset: 0x00000C30
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x060000FA RID: 250 RVA: 0x0000A2CC File Offset: 0x000084CC
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			this.m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(false);
			this.KnockbackForce = valuesDictionary.GetValue<float>("KnockbackForce");
			this.KnockbackChance = valuesDictionary.GetValue<float>("KnockbackChance");
		}

		// Token: 0x060000FB RID: 251 RVA: 0x0000A360 File Offset: 0x00008560
		public void Update(float dt)
		{
			if (this.m_componentChaseBehavior != null && this.m_componentChaseBehavior.Target != null)
			{
				if (this.m_componentCreatureModel.IsAttackHitMoment && !this.m_lastAttackWasHit)
				{
					this.m_lastAttackWasHit = true;
					if (this.m_random.Float(0f, 1f) < this.KnockbackChance)
					{
						ComponentBody componentBody = this.m_componentChaseBehavior.Target.ComponentBody;
						if (componentBody != null)
						{
							this.ApplyKnockback(componentBody);
							return;
						}
					}
				}
				else if (!this.m_componentCreatureModel.IsAttackHitMoment)
				{
					this.m_lastAttackWasHit = false;
					return;
				}
			}
			else
			{
				this.m_lastAttackWasHit = false;
			}
		}

		// Token: 0x060000FC RID: 252 RVA: 0x0000A3F4 File Offset: 0x000085F4
		private void ApplyKnockback(ComponentBody targetBody)
		{
			Vector3 vector = Vector3.Normalize(targetBody.Position - this.m_componentBody.Position);
			Vector3 impulse = new Vector3(vector.X, 0.5f, vector.Z) * this.KnockbackForce;
			targetBody.ApplyImpulse(impulse);
		}

		// Token: 0x040000FB RID: 251
		private ComponentCreature m_componentCreature;

		// Token: 0x040000FC RID: 252
		private ComponentBody m_componentBody;

		// Token: 0x040000FD RID: 253
		private SubsystemBodies m_subsystemBodies;

		// Token: 0x040000FE RID: 254
		private ComponentCreatureModel m_componentCreatureModel;

		// Token: 0x040000FF RID: 255
		private ComponentChaseBehavior m_componentChaseBehavior;

		// Token: 0x04000100 RID: 256
		public float KnockbackForce;

		// Token: 0x04000101 RID: 257
		public float KnockbackChance;

		// Token: 0x04000102 RID: 258
		private readonly Game.Random m_random = new Game.Random();

		// Token: 0x04000103 RID: 259
		private bool m_lastAttackWasHit;
	}
}
