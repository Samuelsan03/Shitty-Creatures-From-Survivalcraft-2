using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Versión defensiva de ComponentRunAwayBehavior que NO huye por:
	/// - Ruidos
	/// - Ataques recibidos
	/// - Salud baja
	/// Solo huye cuando se le ordena explícitamente.
	/// </summary>
	public class ComponentDefensiveRunAwayBehavior : ComponentBehavior, IUpdateable
	{
		// Token: 0x17000056 RID: 86
		// (get) Token: 0x06000887 RID: 2183 RVA: 0x0002AE84 File Offset: 0x00029084
		// (set) Token: 0x06000888 RID: 2184 RVA: 0x0002AE8C File Offset: 0x0002908C
		public float LowHealthToEscape { get; set; }

		// Token: 0x17000057 RID: 87
		// (get) Token: 0x06000889 RID: 2185 RVA: 0x0002AE95 File Offset: 0x00029095
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x17000058 RID: 88
		// (get) Token: 0x0600088A RID: 2186 RVA: 0x0002AE98 File Offset: 0x00029098
		public override float ImportanceLevel
		{
			get
			{
				return m_importanceLevel;
			}
		}

		// Token: 0x0600088B RID: 2187 RVA: 0x0002AEA0 File Offset: 0x000290A0
		public virtual void RunAwayFrom(ComponentBody componentBody)
		{
			m_attacker = componentBody;
			m_timeToForgetAttacker = m_random.Float(10f, 20f);
		}

		// Token: 0x0600088C RID: 2188 RVA: 0x0002AEC4 File Offset: 0x000290C4
		public virtual void Update(float dt)
		{
			m_stateMachine.Update();
		}

		// Token: 0x0600088E RID: 2190 RVA: 0x0002AEF8 File Offset: 0x000290F8
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			m_componentHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();

			this.LowHealthToEscape = valuesDictionary.GetValue<float>("LowHealthToEscape");

			// NOTA: NO nos suscribimos al evento Injured para no huir por ataques

			m_stateMachine.AddState("Inactive", delegate
			{
				m_importanceLevel = 0f;
			}, delegate
			{
				if (m_attacker != null)
				{
					m_timeToForgetAttacker -= m_subsystemTime.GameTimeDelta;
					if (m_timeToForgetAttacker <= 0f)
					{
						m_attacker = null;
					}
				}

				// SOLO huimos si estamos activos por una orden explícita
				if (this.IsActive)
				{
					m_stateMachine.TransitionTo("RunningAway");
				}
			}, null);

			m_stateMachine.AddState("RunningAway", delegate
			{
				Vector3 value = this.FindSafePlace();
				m_componentPathfinding.SetDestination(new Vector3?(value), 1f, 1f, 0, false, true, false, null);
				m_componentCreature.ComponentCreatureSounds.PlayPainSound();
			}, delegate
			{
				if (!this.IsActive)
				{
					m_stateMachine.TransitionTo("Inactive");
					return;
				}
				if (m_componentPathfinding.Destination == null || m_componentPathfinding.IsStuck)
				{
					m_importanceLevel = 0f;
					return;
				}
				if (m_attacker != null)
				{
					if (!m_attacker.IsAddedToProject)
					{
						m_importanceLevel = 0f;
						m_attacker = null;
						return;
					}
					ComponentHealth componentHealth2 = m_attacker.Entity.FindComponent<ComponentHealth>();
					if (componentHealth2 != null && componentHealth2.Health == 0f)
					{
						m_importanceLevel = 0f;
						m_attacker = null;
					}
				}
			}, null);

			m_stateMachine.TransitionTo("Inactive");
		}

		// Token: 0x0600088F RID: 2191 RVA: 0x0002B010 File Offset: 0x00029210
		public virtual Vector3 FindSafePlace()
		{
			Vector3 position = m_componentCreature.ComponentBody.Position;
			ComponentNewHerdBehavior componentHerdBehavior = m_componentHerdBehavior;
			Vector3? herdPosition = (componentHerdBehavior != null) ? componentHerdBehavior.FindHerdCenter() : null;

			if (herdPosition != null && Vector3.DistanceSquared(position, herdPosition.Value) < 144f)
			{
				herdPosition = null;
			}

			float num = float.NegativeInfinity;
			Vector3 result = position;

			for (int i = 0; i < 30; i++)
			{
				int num2 = Terrain.ToCell(position.X + m_random.Float(-25f, 25f));
				int num3 = Terrain.ToCell(position.Z + m_random.Float(-25f, 25f));
				int j = 255;

				while (j >= 0)
				{
					int cellValue = m_subsystemTerrain.Terrain.GetCellValue(num2, j, num3);
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

		// Token: 0x06000890 RID: 2192 RVA: 0x0002B170 File Offset: 0x00029370
		public virtual float ScoreSafePlace(Vector3 currentPosition, Vector3 safePosition, Vector3? herdPosition, int contents)
		{
			float num = 0f;
			Vector2 vector = new Vector2(currentPosition.X, currentPosition.Z);
			Vector2 vector2 = new Vector2(safePosition.X, safePosition.Z);
			Segment2 s = new Segment2(vector, vector2);

			if (m_attacker != null)
			{
				Vector3 position = m_attacker.Position;
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

			// ELIMINADA la puntuación por ruidos - no reacciona a ellos

			if (contents == 18)
			{
				num -= 4f;
			}

			return num;
		}

		// Token: 0x0400041A RID: 1050
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x0400041B RID: 1051
		public SubsystemTime m_subsystemTime;

		// Token: 0x0400041D RID: 1053
		public ComponentCreature m_componentCreature;

		// Token: 0x0400041E RID: 1054
		public ComponentPathfinding m_componentPathfinding;

		// Token: 0x0400041F RID: 1055
		public ComponentNewHerdBehavior m_componentHerdBehavior;

		// Token: 0x04000420 RID: 1056
		public Random m_random = new Random();

		// Token: 0x04000421 RID: 1057
		public StateMachine m_stateMachine = new StateMachine();

		// Token: 0x04000422 RID: 1058
		public float m_importanceLevel;

		// Token: 0x04000423 RID: 1059
		public ComponentFrame m_attacker;

		// Token: 0x04000424 RID: 1060
		public float m_timeToForgetAttacker;
	}
}
