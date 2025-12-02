using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000029 RID: 41
	public class ComponentCustomChaseBehavior : Component, IUpdateable
	{
		// Token: 0x17000026 RID: 38
		// (get) Token: 0x06000127 RID: 295 RVA: 0x0000CF2E File Offset: 0x0000B12E
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x06000129 RID: 297 RVA: 0x0000CF68 File Offset: 0x0000B168
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_componentChase = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			string value = valuesDictionary.GetValue<string>("ChaseByCreatures", "");
			string value2 = valuesDictionary.GetValue<string>("ChaseToCreatures", "");
			bool flag = !string.IsNullOrEmpty(value);
			if (flag)
			{
				this.m_chaseByCreatures = (from s in value.Split(',', StringSplitOptions.None)
										   select s.Trim()).ToList<string>();
			}
			bool flag2 = !string.IsNullOrEmpty(value2);
			if (flag2)
			{
				this.m_chaseToCreatures = (from s in value2.Split(',', StringSplitOptions.None)
										   select s.Trim()).ToList<string>();
			}
			this.m_chaseToCreatureProbability = valuesDictionary.GetValue<float>("ChaseToCreatureProbability");
			this.m_chaseByCreatureProbability = valuesDictionary.GetValue<float>("ChaseByCreatureProbability");
			this.m_checkInterval = valuesDictionary.GetValue<float>("CheckInterval", this.m_random.Float(5f, 15f));
		}

		// Token: 0x0600012A RID: 298 RVA: 0x0000D0BC File Offset: 0x0000B2BC
		public void Update(float dt)
		{
			bool flag = this.m_subsystemTime.GameTime < this.m_nextCheckTime || this.m_componentChase.Suppressed || this.m_componentChase.Target != null;
			if (!flag)
			{
				this.m_nextCheckTime = this.m_subsystemTime.GameTime + (double)this.m_checkInterval;
				foreach (string creatureType in this.m_chaseByCreatures)
				{
					List<ComponentCreature> list = this.FindCreaturesByType(creatureType, 20f);
					foreach (ComponentCreature componentCreature in list)
					{
						bool flag2 = this.m_random.Bool(this.m_chaseByCreatureProbability);
						if (flag2)
						{
							ComponentChaseBehavior componentChaseBehavior = componentCreature.Entity.FindComponent<ComponentChaseBehavior>();
							bool flag3 = componentChaseBehavior != null && componentChaseBehavior.Target == null && !componentChaseBehavior.Suppressed;
							if (flag3)
							{
								componentChaseBehavior.Attack(this.m_componentCreature, componentChaseBehavior.ChaseRangeOnTouch, componentChaseBehavior.ChaseTimeOnTouch, false);
							}
						}
					}
				}
				foreach (string creatureType2 in this.m_chaseToCreatures)
				{
					List<ComponentCreature> list2 = this.FindCreaturesByType(creatureType2, this.m_componentChase.ChaseRangeOnTouch);
					foreach (ComponentCreature componentCreature2 in list2)
					{
						bool flag4 = this.m_random.Bool(this.m_chaseToCreatureProbability);
						if (flag4)
						{
							this.m_componentChase.Attack(componentCreature2, this.m_componentChase.ChaseRangeOnTouch, this.m_componentChase.ChaseTimeOnTouch, false);
							break;
						}
					}
				}
			}
		}

		// Token: 0x0600012B RID: 299 RVA: 0x0000D2F0 File Offset: 0x0000B4F0
		private List<ComponentCreature> FindCreaturesByType(string creatureType, float range)
		{
			List<ComponentCreature> list = new List<ComponentCreature>();
			DynamicArray<ComponentBody> dynamicArray = new DynamicArray<ComponentBody>();
			Vector2 point = new Vector2(this.m_componentCreature.ComponentBody.Position.X, this.m_componentCreature.ComponentBody.Position.Z);
			this.m_subsystemBodies.FindBodiesAroundPoint(point, range, dynamicArray);
			for (int i = 0; i < dynamicArray.Count; i++)
			{
				ComponentCreature componentCreature = dynamicArray.Array[i].Entity.FindComponent<ComponentCreature>();
				bool flag = componentCreature != null && componentCreature.Entity.ValuesDictionary.DatabaseObject.Name == creatureType && componentCreature != this.m_componentCreature;
				if (flag)
				{
					list.Add(componentCreature);
				}
			}
			return list;
		}

		// Token: 0x04000124 RID: 292
		private SubsystemBodies m_subsystemBodies;

		// Token: 0x04000125 RID: 293
		private SubsystemTime m_subsystemTime;

		// Token: 0x04000126 RID: 294
		private ComponentChaseBehavior m_componentChase;

		// Token: 0x04000127 RID: 295
		private ComponentCreature m_componentCreature;

		// Token: 0x04000128 RID: 296
		private readonly Random m_random = new Random();

		// Token: 0x04000129 RID: 297
		private float m_chaseToCreatureProbability;

		// Token: 0x0400012A RID: 298
		private float m_chaseByCreatureProbability;

		// Token: 0x0400012B RID: 299
		private List<string> m_chaseByCreatures = new List<string>();

		// Token: 0x0400012C RID: 300
		private List<string> m_chaseToCreatures = new List<string>();

		// Token: 0x0400012D RID: 301
		private float m_checkInterval = 1f;

		// Token: 0x0400012E RID: 302
		private double m_nextCheckTime;
	}
}
