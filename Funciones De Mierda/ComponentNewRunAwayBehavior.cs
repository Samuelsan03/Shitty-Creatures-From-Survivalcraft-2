using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000091 RID: 145
	public class ComponentNewRunAwayBehavior : ComponentBehavior, IUpdateable, IComponentEscapeBehavior
	{
		// Token: 0x1700003B RID: 59
		// (get) Token: 0x0600045A RID: 1114 RVA: 0x00016AB0 File Offset: 0x00014CB0
		// (set) Token: 0x0600045B RID: 1115 RVA: 0x00016AB8 File Offset: 0x00014CB8
		public float LowHealthToEscape { get; set; }

		// Token: 0x1700003C RID: 60
		// (get) Token: 0x0600045C RID: 1116 RVA: 0x00016AC1 File Offset: 0x00014CC1
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x1700003D RID: 61
		// (get) Token: 0x0600045D RID: 1117 RVA: 0x00016AC4 File Offset: 0x00014CC4
		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		// Token: 0x0600045E RID: 1118 RVA: 0x00016ACC File Offset: 0x00014CCC
		public virtual void RunAwayFrom(ComponentBody componentBody)
		{
			this.m_attacker = componentBody;
			this.m_timeToForgetAttacker = this.m_random.Float(10f, 20f);
		}

		// Token: 0x0600045F RID: 1119 RVA: 0x00016AF0 File Offset: 0x00014CF0
		public virtual void Update(float dt)
		{
			this.m_stateMachine.Update();
		}

		// Token: 0x06000460 RID: 1120 RVA: 0x00016B00 File Offset: 0x00014D00
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.m_componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();
			this.LowHealthToEscape = valuesDictionary.GetValue<float>("LowHealthToEscape");
			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				this.RunAwayFrom((attacker != null) ? attacker.ComponentBody : null);
			}));
			this.m_stateMachine.AddState("Inactive", delegate
			{
				this.m_importanceLevel = 0f;
				this.m_lastNoiseSourcePosition = null;
			}, delegate
			{
				if (this.m_attacker != null)
				{
					this.m_timeToForgetAttacker -= this.m_subsystemTime.GameTimeDelta;
					if (this.m_timeToForgetAttacker <= 0f)
					{
						this.m_attacker = null;
					}
				}
				if (this.m_componentCreature.ComponentHealth.HealthChange < 0f || (this.m_attacker != null && Vector3.DistanceSquared(this.m_attacker.Position, this.m_componentCreature.ComponentBody.Position) < 36f))
				{
					this.m_importanceLevel = MathUtils.Max(this.m_importanceLevel, (float)((this.m_componentCreature.ComponentHealth.Health < this.LowHealthToEscape) ? 300 : 100));
				}
				else if (!this.IsActive)
				{
					this.m_importanceLevel = 0f;
				}
				if (this.IsActive)
				{
					this.m_stateMachine.TransitionTo("RunningAway");
				}
			}, null);
			this.m_stateMachine.AddState("RunningAway", delegate
			{
				Vector3 value = this.FindSafePlace();
				this.m_componentPathfinding.SetDestination(new Vector3?(value), 1f, 1f, 0, false, true, false, null);
				this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
				this.m_subsystemNoise.MakeNoise(this.m_componentCreature.ComponentBody, 0.25f, 6f);
			}, delegate
			{
				if (!this.IsActive)
				{
					this.m_stateMachine.TransitionTo("Inactive");
					return;
				}
				if (this.m_componentPathfinding.Destination == null || this.m_componentPathfinding.IsStuck)
				{
					this.m_importanceLevel = 0f;
					return;
				}
				if (this.m_attacker != null)
				{
					if (!this.m_attacker.IsAddedToProject)
					{
						this.m_importanceLevel = 0f;
						this.m_attacker = null;
						return;
					}
					ComponentHealth componentHealth2 = this.m_attacker.Entity.FindComponent<ComponentHealth>();
					if (componentHealth2 != null && componentHealth2.Health == 0f)
					{
						this.m_importanceLevel = 0f;
						this.m_attacker = null;
					}
				}
			}, null);
			this.m_stateMachine.TransitionTo("Inactive");
		}

		// Token: 0x06000461 RID: 1121 RVA: 0x00016C18 File Offset: 0x00014E18
		public virtual Vector3 FindSafePlace()
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentHerdBehavior componentHerdBehavior = this.m_componentHerdBehavior;
			Vector3? herdPosition = (componentHerdBehavior != null) ? componentHerdBehavior.FindHerdCenter() : null;
			if (herdPosition != null && Vector3.DistanceSquared(position, herdPosition.Value) < 144f)
			{
				herdPosition = null;
			}
			float num = float.NegativeInfinity;
			Vector3 result = position;
			for (int i = 0; i < 30; i++)
			{
				int num2 = Terrain.ToCell(position.X + this.m_random.Float(-25f, 25f));
				int num3 = Terrain.ToCell(position.Z + this.m_random.Float(-25f, 25f));
				int j = 255;
				while (j >= 0)
				{
					int cellValue = this.m_subsystemTerrain.Terrain.GetCellValue(num2, j, num3);
					if (BlocksManager.Blocks[Terrain.ExtractContents(cellValue)].IsCollidable_(cellValue) || Terrain.ExtractContents(cellValue) == 18)
					{
						Vector3 vector = new Vector3((float)num2 + 0.5f, (float)j + 1.1f, (float)num3 + 0.5f);
						float num4 = this.ScoreSafePlace(position, vector, herdPosition, this.m_lastNoiseSourcePosition, Terrain.ExtractContents(cellValue));
						if (num4 > num)
						{
							num = num4;
							result = vector;
							break;
						}
						break;
					}
					else
					{
						j--;
					}
				}
			}
			return result;
		}

		// Token: 0x06000462 RID: 1122 RVA: 0x00016D78 File Offset: 0x00014F78
		public virtual float ScoreSafePlace(Vector3 currentPosition, Vector3 safePosition, Vector3? herdPosition, Vector3? noiseSourcePosition, int contents)
		{
			float num = 0f;
			Vector2 vector = new Vector2(currentPosition.X, currentPosition.Z);
			Vector2 vector2 = new Vector2(safePosition.X, safePosition.Z);
			Segment2 s = new Segment2(vector, vector2);
			if (this.m_attacker != null)
			{
				Vector3 position = this.m_attacker.Position;
				Vector2 vector3 = new Vector2(position.X, position.Z);
				float num2 = Vector2.Distance(vector3, vector2);
				float num3 = Segment2.Distance(s, vector3);
				num += num2 + 3f * num3;
			}
			else
			{
				num += 2f * Vector2.Distance(vector, vector2);
			}
			Vector2? vector4 = (herdPosition != null) ? new Vector2?(new Vector2(herdPosition.Value.X, herdPosition.Value.Z)) : null;
			float num4 = (vector4 != null) ? Segment2.Distance(s, vector4.Value) : 0f;
			num -= num4;
			Vector2? vector5 = (noiseSourcePosition != null) ? new Vector2?(new Vector2(noiseSourcePosition.Value.X, noiseSourcePosition.Value.Z)) : null;
			float num5 = (vector5 != null) ? Segment2.Distance(s, vector5.Value) : 0f;
			num += 1.5f * num5;
			if (contents == 18)
			{
				num -= 4f;
			}
			return num;
		}

		// Token: 0x04000213 RID: 531
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x04000214 RID: 532
		public SubsystemTime m_subsystemTime;

		// Token: 0x04000215 RID: 533
		public SubsystemNoise m_subsystemNoise;

		// Token: 0x04000216 RID: 534
		public ComponentCreature m_componentCreature;

		// Token: 0x04000217 RID: 535
		public ComponentPathfinding m_componentPathfinding;

		// Token: 0x04000218 RID: 536
		public ComponentHerdBehavior m_componentHerdBehavior;

		// Token: 0x04000219 RID: 537
		public Game.Random m_random = new Game.Random();

		// Token: 0x0400021A RID: 538
		public StateMachine m_stateMachine = new StateMachine();

		// Token: 0x0400021B RID: 539
		public float m_importanceLevel;

		// Token: 0x0400021C RID: 540
		public ComponentFrame m_attacker;

		// Token: 0x0400021D RID: 541
		public float m_timeToForgetAttacker;

		// Token: 0x0400021E RID: 542
		public Vector3? m_lastNoiseSourcePosition;
	}
}
