using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewRunAwayBehavior : ComponentBehavior, IUpdateable, IComponentEscapeBehavior
	{
		public float LowHealthToEscape { get; set; }

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		public virtual void RunAwayFrom(ComponentBody componentBody)
		{
			this.m_attacker = componentBody;
			this.m_timeToForgetAttacker = this.m_random.Float(10f, 20f);
		}

		public virtual void Update(float dt)
		{
			this.m_stateMachine.Update();
		}

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

				// Solo activar por ataque o salud baja, no por ruidos
				if (this.m_componentCreature.ComponentHealth.HealthChange < 0f ||
					(this.m_attacker != null && Vector3.DistanceSquared(this.m_attacker.Position, this.m_componentCreature.ComponentBody.Position) < 36f))
				{
					this.m_importanceLevel = MathUtils.Max(this.m_importanceLevel,
						(float)((this.m_componentCreature.ComponentHealth.Health < this.LowHealthToEscape) ? 300 : 100));
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
						float num4 = this.ScoreSafePlace(position, vector, herdPosition, Terrain.ExtractContents(cellValue));
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

		public virtual float ScoreSafePlace(Vector3 currentPosition, Vector3 safePosition, Vector3? herdPosition, int contents)
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

			// Nota: Se eliminó la consideración de ruidos en el cálculo

			if (contents == 18)
			{
				num -= 4f;
			}

			return num;
		}

		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemTime m_subsystemTime;
		public SubsystemNoise m_subsystemNoise;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public ComponentHerdBehavior m_componentHerdBehavior;
		public Random m_random = new Random();
		public StateMachine m_stateMachine = new StateMachine();
		public float m_importanceLevel;
		public ComponentBody m_attacker;
		public float m_timeToForgetAttacker;
	}
}