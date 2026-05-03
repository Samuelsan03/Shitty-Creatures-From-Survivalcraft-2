using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentAutoMountBehavior : ComponentBehavior, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		public float SearchRange = 20f;

		private static readonly string[] s_mountableTemplates = new string[]
		{
			"InfectedBearTamed", "FlyingInfectedBossTamed", "InfectedFlyTamed1",
			"Horse_Bay_Saddled", "Donkey_Saddled", "Horse_Chestnut_Saddled",
			"Camel_Saddled", "Horse_Black_Saddled", "Horse_Palomino_Saddled",
			"Horse_White_Saddled"
		};

		private SubsystemTime m_subsystemTime;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemCampfireBlockBehavior m_subsystemCampfireBlockBehavior;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentRider m_componentRider;
		private StateMachine m_stateMachine = new StateMachine();
		private Random m_random = new Random();
		private float m_importanceLevel;
		private ComponentMount m_targetMount;

		private Vector3? m_wanderDestination;
		private double m_nextWanderUpdateTime;

		private ComponentNewChaseBehavior m_chaseBehavior;
		private ComponentSummonBehavior m_summonBehavior;

		private DynamicArray<ComponentBody> m_tempBodies = new DynamicArray<ComponentBody>();

		public virtual void Update(float dt)
		{
			// Si la criatura jinete está muerta, forzar desmontaje y detener todo
			if (m_componentCreature.ComponentHealth.Health <= 0f)
			{
				if (m_componentRider != null && m_componentRider.Mount != null)
					m_componentRider.StartDismounting();
				return;
			}

			m_stateMachine.Update();
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemCampfireBlockBehavior = Project.FindSubsystem<SubsystemCampfireBlockBehavior>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentRider = Entity.FindComponent<ComponentNewRider>() as ComponentRider
							   ?? Entity.FindComponent<ComponentRider>();

			m_chaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_summonBehavior = Entity.FindComponent<ComponentSummonBehavior>();

			SearchRange = valuesDictionary.GetValue<float>("SearchRange", 20f);

			// ----- Estados -----

			// Idle
			m_stateMachine.AddState("Idle", delegate {
				m_importanceLevel = 0f;
				m_targetMount = null;
			}, delegate {
				if (m_componentRider.Mount != null)
				{
					m_stateMachine.TransitionTo("Wander");
					return;
				}
				ComponentMount mount = FindMountableCreature();
				if (mount != null)
				{
					m_targetMount = mount;
					m_importanceLevel = 300f;
					m_stateMachine.TransitionTo("Approach");
				}
				else m_importanceLevel = 0f;
			}, null);

			// Approach
			m_stateMachine.AddState("Approach", delegate {
				m_componentPathfinding.SetDestination(
					m_targetMount.ComponentBody.Position, 1f, 1.5f, 100, false, false, true, null);
			}, delegate {
				if (m_targetMount == null || m_targetMount.ComponentBody == null ||
					m_targetMount.Entity.FindComponent<ComponentHealth>()?.Health <= 0f ||
					m_targetMount.Rider != null)
				{
					m_stateMachine.TransitionTo("Idle");
					return;
				}
				float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position,
					m_targetMount.ComponentBody.Position);
				if (dist < 2.5f) m_stateMachine.TransitionTo("Mounting");
				else if (m_componentPathfinding.IsStuck && dist > 8f)
				{
					m_targetMount = null;
					m_stateMachine.TransitionTo("Idle");
				}
			}, () => m_componentPathfinding.Stop());

			// Mounting
			m_stateMachine.AddState("Mounting", delegate {
				m_componentRider.StartMounting(m_targetMount);
			}, delegate {
				if (m_componentRider.Mount == m_targetMount)
				{
					m_stateMachine.TransitionTo("Wander");
					return;
				}
				if (m_targetMount == null || m_targetMount.ComponentBody == null ||
					m_targetMount.Entity.FindComponent<ComponentHealth>()?.Health <= 0f ||
					m_targetMount.Rider != null)
				{
					m_targetMount = null;
					m_stateMachine.TransitionTo("Idle");
				}
			}, null);

			// Wander
			m_stateMachine.AddState("Wander", delegate {
				m_importanceLevel = 10f;
				m_wanderDestination = null;
				m_nextWanderUpdateTime = 0.0;
			}, delegate {
				if (m_componentRider.Mount == null)
				{
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				ComponentMount mount = m_componentRider.Mount;
				Entity mountEntity = mount.Entity;
				ComponentPathfinding mountPathfinding = mountEntity.FindComponent<ComponentPathfinding>();
				if (mountPathfinding == null) return;

				Vector3? urgentTarget = null;
				float urgentImportance = 10f;
				float speed = 1f;
				float range = 1.5f;

				if (m_chaseBehavior != null && m_chaseBehavior.IsActive && m_chaseBehavior.Target != null &&
					m_chaseBehavior.Target.ComponentHealth.Health > 0f)
				{
					urgentTarget = m_chaseBehavior.Target.ComponentBody.Position;
					urgentImportance = 250f;
					range = 1.0f;
					m_componentPathfinding.Stop();

					ComponentNewChaseBehavior mountChase = mountEntity.FindComponent<ComponentNewChaseBehavior>();
					if (mountChase != null)
					{
						ComponentCreature target = m_chaseBehavior.Target;
						if (mountChase.Target != target || !mountChase.IsActive)
						{
							mountChase.Attack(target, 40f, 120f, true);
						}
					}
				}

				if (urgentTarget.HasValue)
				{
					m_importanceLevel = urgentImportance;
					mountPathfinding.SetDestination(urgentTarget.Value, speed, range, 100, false, true, false, null);
					m_wanderDestination = null;
					return;
				}

				m_importanceLevel = 10f;

				if (m_subsystemTime.GameTime >= m_nextWanderUpdateTime)
				{
					m_wanderDestination = FindWanderDestination();
					m_nextWanderUpdateTime = m_subsystemTime.GameTime + m_random.Float(8f, 20f);
				}
				if (m_wanderDestination.HasValue && mountPathfinding.Destination == null)
				{
					mountPathfinding.SetDestination(m_wanderDestination.Value,
						m_random.Float(0.25f, 0.4f), 3f, 0, false, true, false, null);
				}
				if (mountPathfinding.IsStuck)
				{
					m_wanderDestination = null;
					m_nextWanderUpdateTime = m_subsystemTime.GameTime + 2f;
				}
			}, () => {
				if (m_componentRider.Mount != null)
				{
					ComponentPathfinding mp = m_componentRider.Mount.Entity.FindComponent<ComponentPathfinding>();
					if (mp != null) mp.Stop();
				}
			});

			m_stateMachine.TransitionTo("Idle");
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
		}

		private Vector3 FindWanderDestination()
		{
			Vector3 currentPos = m_componentRider.Mount != null
				? m_componentRider.Mount.ComponentBody.Position
				: m_componentCreature.ComponentBody.Position;

			float bestScore = float.MinValue;
			Vector3 bestDest = currentPos;

			for (int i = 0; i < 8; i++)
			{
				Vector2 offset = m_random.Vector2(6f, 18f);
				Vector3 candidate = new Vector3(currentPos.X + offset.X, 0f, currentPos.Z + offset.Y);

				int topHeight = m_subsystemTerrain.Terrain.GetTopHeight(
					Terrain.ToCell(candidate.X),
					Terrain.ToCell(candidate.Z));

				candidate.Y = topHeight + 1;

				float score = ScoreWanderDestination(candidate, currentPos);
				if (score > bestScore)
				{
					bestScore = score;
					bestDest = candidate;
				}
			}
			return bestDest;
		}

		private float ScoreWanderDestination(Vector3 dest, Vector3 currentPos)
		{
			float score = MathF.Max(0f, 10f - MathF.Abs(dest.Y - currentPos.Y));

			int cx = Terrain.ToCell(dest.X);
			int cz = Terrain.ToCell(dest.Z);
			int topY = m_subsystemTerrain.Terrain.GetTopHeight(cx, cz);

			if (m_subsystemTerrain.Terrain.GetCellContents(cx, topY, cz) == 18)
				score -= 20f;

			return score;
		}

		private ComponentMount FindMountableCreature()
		{
			Vector3 pos = m_componentCreature.ComponentBody.Position;

			m_tempBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(pos.X, pos.Z), SearchRange, m_tempBodies);

			foreach (ComponentBody body in m_tempBodies)
			{
				Entity entity = body.Entity;

				ComponentMount mount = entity.FindComponent<ComponentNewMount>() as ComponentMount
									   ?? entity.FindComponent<ComponentMount>();

				if (mount == null) continue;
				if (mount.Rider != null) continue;

				ComponentHealth health = entity.FindComponent<ComponentHealth>();
				if (health != null && health.Health <= 0f) continue;

				string name = entity.ValuesDictionary.DatabaseObject.Name;

				foreach (string t in s_mountableTemplates)
					if (name == t)
						return mount;
			}

			return null;
		}
	}
}
