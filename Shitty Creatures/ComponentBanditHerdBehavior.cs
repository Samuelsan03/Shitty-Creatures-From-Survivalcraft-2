using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBanditHerdBehavior : ComponentBehavior, IUpdateable
	{
		public string HerdName { get; set; }
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		private SubsystemTime m_subsystemTime;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentBanditChaseBehavior m_componentBanditChase;
		private StateMachine m_stateMachine = new StateMachine();
		private float m_dt;
		private float m_importanceLevel;
		private Random m_random = new Random();
		private Vector2 m_look;
		private float m_herdingRange;
		private bool m_autoNearbyCreaturesHelp;
		private float m_helpRange = 20f;
		private float m_helpChaseTime = 30f;
		private bool m_helpPersistent = false;

		public void CallNearbyCreaturesHelp(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (target == null) return;
			Vector3 position = target.ComponentBody.Position;
			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (Vector3.DistanceSquared(position, creature.ComponentBody.Position) < maxRange * maxRange)
				{
					ComponentBanditHerdBehavior otherHerd = creature.Entity.FindComponent<ComponentBanditHerdBehavior>();
					if (otherHerd != null && !string.IsNullOrEmpty(otherHerd.HerdName) &&
						otherHerd.HerdName == HerdName && otherHerd.m_autoNearbyCreaturesHelp)
					{
						ComponentBanditChaseBehavior chase = creature.Entity.FindComponent<ComponentBanditChaseBehavior>();
						if (chase != null && chase.Target == null)
						{
							chase.Attack(target, maxRange, maxChaseTime, isPersistent);
						}
					}
				}
			}
		}

		public Vector3? FindHerdCenter()
		{
			if (string.IsNullOrEmpty(HerdName)) return null;
			Vector3 myPos = m_componentCreature.ComponentBody.Position;
			int count = 0;
			Vector3 sum = Vector3.Zero;
			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature.ComponentHealth.Health > 0f)
				{
					ComponentBanditHerdBehavior herd = creature.Entity.FindComponent<ComponentBanditHerdBehavior>();
					if (herd != null && herd.HerdName == HerdName)
					{
						Vector3 pos = creature.ComponentBody.Position;
						if (Vector3.DistanceSquared(myPos, pos) < m_herdingRange * m_herdingRange)
						{
							sum += pos;
							count++;
						}
					}
				}
			}
			return count > 0 ? (Vector3?)(sum / count) : null;
		}

		public virtual void Update(float dt)
		{
			if (string.IsNullOrEmpty(m_stateMachine.CurrentState) || !IsActive)
				m_stateMachine.TransitionTo("Inactive");
			m_dt = dt;
			m_stateMachine.Update();
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentBanditChase = Entity.FindComponent<ComponentBanditChaseBehavior>(true);

			HerdName = valuesDictionary.GetValue<string>("HerdName");
			m_herdingRange = valuesDictionary.GetValue<float>("HerdingRange");
			m_autoNearbyCreaturesHelp = valuesDictionary.GetValue<bool>("AutoNearbyCreaturesHelp");
			m_helpRange = valuesDictionary.GetValue<float>("HelpRange", 20f);
			m_helpChaseTime = valuesDictionary.GetValue<float>("HelpChaseTime", 30f);
			m_helpPersistent = valuesDictionary.GetValue<bool>("HelpPersistent", false);

			ComponentHealth health = m_componentCreature.ComponentHealth;
			health.Injured = (Action<Injury>)Delegate.Combine(health.Injured, new Action<Injury>(OnInjured));

			ComponentBody body = m_componentCreature.ComponentBody;
			body.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(body.CollidedWithBody, new Action<ComponentBody>(OnCollidedWithBody));

			m_stateMachine.AddState("Inactive", null, InactiveUpdate, null);
			m_stateMachine.AddState("Stuck", StuckEnter, null, null);
			m_stateMachine.AddState("Herd", HerdEnter, HerdUpdate, null);

			m_stateMachine.TransitionTo("Inactive");
		}

		private void OnInjured(Injury injury)
		{
			ComponentCreature attacker = injury.Attacker;
			if (attacker != null)
			{
				ComponentBanditHerdBehavior attackerHerd = attacker.Entity.FindComponent<ComponentBanditHerdBehavior>();
				if (attackerHerd != null && attackerHerd.HerdName == HerdName)
					return;
			}
			CallNearbyCreaturesHelp(attacker, m_helpRange, m_helpChaseTime, m_helpPersistent);
		}

		private void OnCollidedWithBody(ComponentBody otherBody)
		{
			if (m_componentBanditChase != null && m_componentBanditChase.Target == null &&
				m_componentBanditChase.m_autoChaseSuppressionTime <= 0f &&
				m_componentBanditChase.m_random.Float(0f, 1f) < m_componentBanditChase.m_chaseOnTouchProbability)
			{
				ComponentCreature creature = otherBody.Entity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					ComponentBanditHerdBehavior otherHerd = creature.Entity.FindComponent<ComponentBanditHerdBehavior>();
					if (otherHerd != null && otherHerd.HerdName == HerdName)
						return;

					bool isPlayer = m_componentBanditChase.m_subsystemPlayers.IsPlayer(otherBody.Entity);
					bool validCategory = (creature.Category & m_componentBanditChase.m_autoChaseMask) > (CreatureCategory)0;
					if ((m_componentBanditChase.AttacksPlayer && isPlayer && m_componentBanditChase.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
						(m_componentBanditChase.AttacksNonPlayerCreature && !isPlayer && validCategory))
					{
						m_componentBanditChase.Attack(creature, m_componentBanditChase.ChaseRangeOnTouch, m_componentBanditChase.ChaseTimeOnTouch, false);
					}
				}
			}
		}

		private void InactiveUpdate()
		{
			if (m_subsystemTime.PeriodicGameTimeEvent(1.0, (double)(1f * ((float)(GetHashCode() % 256) / 256f))))
			{
				Vector3? center = FindHerdCenter();
				if (center != null)
				{
					float dist = Vector3.Distance(center.Value, m_componentCreature.ComponentBody.Position);
					if (dist > 10f) m_importanceLevel = 1f;
					if (dist > 12f) m_importanceLevel = 3f;
					if (dist > 16f) m_importanceLevel = 50f;
					if (dist > 20f) m_importanceLevel = 250f;
				}
			}
			if (IsActive)
				m_stateMachine.TransitionTo("Herd");
		}

		private void StuckEnter()
		{
			m_stateMachine.TransitionTo("Herd");
			if (m_random.Bool(0.5f))
			{
				m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				m_importanceLevel = 0f;
			}
		}

		private void HerdEnter()
		{
			Vector3? center = FindHerdCenter();
			if (center != null && Vector3.Distance(m_componentCreature.ComponentBody.Position, center.Value) > 6f)
			{
				float speed = (m_importanceLevel > 10f) ? m_random.Float(0.9f, 1f) : m_random.Float(0.25f, 0.35f);
				int maxPath = (m_importanceLevel > 200f) ? 100 : 0;
				m_componentPathfinding.SetDestination(center.Value, speed, 7f, maxPath, false, true, false, null);
				return;
			}
			m_importanceLevel = 0f;
		}

		private void HerdUpdate()
		{
			m_componentCreature.ComponentLocomotion.LookOrder = m_look - m_componentCreature.ComponentLocomotion.LookAngles;
			if (m_componentPathfinding.IsStuck)
				m_stateMachine.TransitionTo("Stuck");
			if (m_componentPathfinding.Destination == null)
				m_importanceLevel = 0f;
			if (m_random.Float(0f, 1f) < 0.05f * m_dt)
				m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
			if (m_random.Float(0f, 1f) < 1.5f * m_dt)
				m_look = new Vector2(MathUtils.DegToRad(45f) * m_random.Float(-1f, 1f),
									 MathUtils.DegToRad(10f) * m_random.Float(-1f, 1f));
		}
	}
}
