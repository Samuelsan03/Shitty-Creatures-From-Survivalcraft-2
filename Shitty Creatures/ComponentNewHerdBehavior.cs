using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewHerdBehavior : ComponentBehavior, IUpdateable
	{
		// ===== PROPIEDADES PÚBLICAS =====
		public string HerdName { get; set; }
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		// ===== CAMPOS (cargados desde DB como en el original) =====
		public SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		public SubsystemTime m_subsystemTime;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public StateMachine m_stateMachine = new StateMachine();
		public float m_dt;
		public float m_importanceLevel;
		public Random m_random = new Random();
		public Vector2 m_look;
		public float m_herdingRange;
		public bool m_autoNearbyCreaturesHelp;

		// Componentes extra del new (se conservan)
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private ComponentNewChaseBehavior m_componentChase;
		private ComponentHireableNPC m_componentHireable;

		// ===== CALL NEARBY CREATURES HELP (lógica original + personalizaciones) =====
		public void CallNearbyCreaturesHelp(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent, bool forceResponse = false)
		{
			// === Impossible: no pedir ayuda contra zombies ===
			if (IsImpossibleModeActive() && IsZombieCreature(target))
				return;

			// Si es contratable y no está contratado, no pedir ayuda
			if (m_componentHireable != null && !m_componentHireable.IsHired)
				return;

			if (target == null)
				return;

			// Si ya está huyendo (defensive mode), no pedir ayuda
			ComponentDefensiveRunAwayBehavior defensiveRunAway = Entity.FindComponent<ComponentDefensiveRunAwayBehavior>();
			if (defensiveRunAway != null && defensiveRunAway.IsActive)
				return;

			// No pedir ayuda si el objetivo es de la misma manada o guardián
			if (target.Entity.FindComponent<ComponentNewHerdBehavior>() != null
				&& !string.IsNullOrEmpty(target.Entity.FindComponent<ComponentNewHerdBehavior>().HerdName)
				&& IsSameHerdOrGuardian(target))
			{
				return;
			}

			Vector3 position = target.ComponentBody.Position;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				// Lógica original: DistanceSquared < 256f (16f de rango fijo)
				if (Vector3.DistanceSquared(position, creature.ComponentBody.Position) < 256f)
				{
					ComponentNewHerdBehavior herdBehavior = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
					if (herdBehavior != null
						&& !string.IsNullOrEmpty(herdBehavior.HerdName)
						&& (IsSameHerdOrGuardian(creature) || herdBehavior.HerdName.Equals(HerdName, StringComparison.OrdinalIgnoreCase))
						&& herdBehavior.m_autoNearbyCreaturesHelp)
					{
						ComponentNewChaseBehavior chaseBehavior = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
						if (chaseBehavior != null && (forceResponse || chaseBehavior.Target == null))
						{
							// No atacar a miembros de la misma manada ni a guardianes aliados
							ComponentNewHerdBehavior targetHerd = target.Entity.FindComponent<ComponentNewHerdBehavior>();
							if (targetHerd == null || !IsSameHerdOrGuardian(target))
							{
								// Hireable no contratado no responde
								ComponentHireableNPC hireable = creature.Entity.FindComponent<ComponentHireableNPC>();
								if (hireable != null && !hireable.IsHired)
									continue;

								// Si la criatura está huyendo, no responde
								ComponentDefensiveRunAwayBehavior creatureDefensive = creature.Entity.FindComponent<ComponentDefensiveRunAwayBehavior>();
								if (creatureDefensive != null && creatureDefensive.IsActive)
									continue;

								chaseBehavior.Attack(target, maxRange, maxChaseTime, isPersistent);
							}
						}
					}
				}
			}
		}

		public void RespondToCommandImmediately(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (IsImpossibleModeActive() && IsZombieCreature(target))
				return;

			if (target == null || !CanAttackCreature(target))
				return;

			m_componentChase?.Attack(target, maxRange, maxChaseTime, isPersistent);
			CallNearbyCreaturesHelp(target, 15f, 30f, false, true);
		}

		// ===== FIND HERD CENTER (lógica original) =====
		public Vector3? FindHerdCenter()
		{
			if (string.IsNullOrEmpty(HerdName))
				return null;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			int count = 0;
			Vector3 center = Vector3.Zero;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature.ComponentHealth.Health > 0f)
				{
					ComponentNewHerdBehavior herdBehavior = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
					if (herdBehavior != null
						&& (IsSameHerdOrGuardian(creature) || herdBehavior.HerdName.Equals(HerdName, StringComparison.OrdinalIgnoreCase)))
					{
						Vector3 pos = creature.ComponentBody.Position;
						// Lógica original: m_herdingRange * m_herdingRange
						if (Vector3.DistanceSquared(position, pos) < m_herdingRange * m_herdingRange)
						{
							center += pos;
							count++;
						}
					}
				}
			}

			if (count > 0)
				return center / (float)count;

			return null;
		}

		// ===== HELPERS DE MANADA (se conservan) =====
		public bool IsSameHerdOrGuardian(ComponentCreature otherCreature)
		{
			if (otherCreature == null || string.IsNullOrEmpty(HerdName))
				return false;

			ComponentNewHerdBehavior otherHerd = otherCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (otherHerd == null || string.IsNullOrEmpty(otherHerd.HerdName))
				return false;

			if (HerdName.Equals(otherHerd.HerdName, StringComparison.OrdinalIgnoreCase))
				return true;

			bool isPlayer = HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
			bool isGuardian = HerdName.ToLower().Contains("guardian");
			bool otherIsPlayer = otherHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
			bool otherIsGuardian = otherHerd.HerdName.ToLower().Contains("guardian");

			return (isPlayer && otherIsGuardian) || (isGuardian && otherIsPlayer);
		}

		public bool IsSameHerd(ComponentCreature otherCreature)
		{
			if (otherCreature == null || string.IsNullOrEmpty(HerdName))
				return false;

			ComponentNewHerdBehavior otherHerd = otherCreature.Entity.FindComponent<ComponentNewHerdBehavior>();

			return otherHerd != null
				&& !string.IsNullOrEmpty(otherHerd.HerdName)
				&& HerdName.Equals(otherHerd.HerdName, StringComparison.OrdinalIgnoreCase);
		}

		public bool ShouldAttackCreature(ComponentCreature target)
		{
			if (target == null || string.IsNullOrEmpty(HerdName))
				return true;

			if (IsSameHerdOrGuardian(target))
				return false;

			if (HerdName.Equals("player", StringComparison.OrdinalIgnoreCase)
				&& target.Entity.FindComponent<ComponentPlayer>() != null)
				return false;

			if (HerdName.ToLower().Contains("guardian")
				&& target.Entity.FindComponent<ComponentPlayer>() != null)
				return false;

			return true;
		}

		public bool CanAttackCreature(ComponentCreature target)
		{
			if (m_componentHireable != null && !m_componentHireable.IsHired)
				return false;

			return ShouldAttackCreature(target);
		}

		public void HelpHerdMember(ComponentCreature herdMemberInCombat)
		{
			if (IsImpossibleModeActive())
			{
				var chaseTarget = herdMemberInCombat?.Entity.FindComponent<ComponentNewChaseBehavior>();
				if (chaseTarget != null && IsZombieCreature(chaseTarget.Target))
					return;
			}

			if (herdMemberInCombat == null || !IsSameHerdOrGuardian(herdMemberInCombat))
				return;

			ComponentNewChaseBehavior memberChase = herdMemberInCombat.Entity.FindComponent<ComponentNewChaseBehavior>();
			ComponentHireableNPC memberHireable = herdMemberInCombat.Entity.FindComponent<ComponentHireableNPC>();

			if (memberHireable != null && !memberHireable.IsHired)
				return;

			if (memberChase?.Target != null && ShouldAttackCreature(memberChase.Target))
			{
				m_componentChase?.Attack(memberChase.Target, 20f, 30f, false);
			}
		}

		public void PreventFriendlyFire()
		{
			if (m_componentChase?.Target != null && !ShouldAttackCreature(m_componentChase.Target))
			{
				m_componentChase.StopAttack();
			}
		}

		// ===== UPDATE =====
		public virtual void Update(float dt)
		{
			if (string.IsNullOrEmpty(m_stateMachine.CurrentState) || !IsActive)
			{
				m_stateMachine.TransitionTo("Inactive");
			}
			m_dt = dt;
			m_stateMachine.Update();
			CheckChaseBehaviorTarget();
		}

		private void CheckChaseBehaviorTarget()
		{
			if (m_componentChase?.Target != null && !ShouldAttackCreature(m_componentChase.Target))
			{
				m_componentChase.StopAttack();
			}
		}

		private void OnInjured(Injury injury)
		{
			// === Impossible: ignorar daño de zombies ===
			if (IsImpossibleModeActive() && injury.Attacker != null && IsZombieCreature(injury.Attacker))
				return;

			if (m_componentHireable != null && !m_componentHireable.IsHired)
				return;

			ComponentCreature attacker = injury.Attacker;
			if (attacker != null && ShouldAttackCreature(attacker))
			{
				// Lógica original del injured: 20f, 30f, false
				CallNearbyCreaturesHelp(attacker, 20f, 30f, false, true);
			}
		}

		// ===== STATE MACHINE (lógica original) =====
		private void SetupStateMachine()
		{
			// Estado: Inactive
			m_stateMachine.AddState("Inactive", null, () =>
			{
				if (m_subsystemTime.PeriodicGameTimeEvent(1.0, (double)(1f * ((float)(GetHashCode() % 256) / 256f))))
				{
					Vector3? vector = FindHerdCenter();
					if (vector != null)
					{
						float num = Vector3.Distance(vector.Value, m_componentCreature.ComponentBody.Position);
						if (num > 10f) m_importanceLevel = 1f;
						if (num > 12f) m_importanceLevel = 3f;
						if (num > 16f) m_importanceLevel = 50f;
						if (num > 20f) m_importanceLevel = 250f;
					}
				}
				if (IsActive)
				{
					m_stateMachine.TransitionTo("Herd");
				}
			}, null);

			// Estado: Stuck
			m_stateMachine.AddState("Stuck", () =>
			{
				m_stateMachine.TransitionTo("Herd");
				if (m_random.Bool(0.5f))
				{
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
					m_importanceLevel = 0f;
				}
			}, null, null);

			// Estado: Herd
			m_stateMachine.AddState("Herd", () =>
			{
				Vector3? vector = FindHerdCenter();
				if (vector != null && Vector3.Distance(m_componentCreature.ComponentBody.Position, vector.Value) > 6f)
				{
					float speed = (m_importanceLevel > 10f) ? m_random.Float(0.9f, 1f) : m_random.Float(0.25f, 0.35f);
					int maxPathfindingPositions = (m_importanceLevel > 200f) ? 100 : 0;
					m_componentPathfinding.SetDestination(new Vector3?(vector.Value), speed, 7f, maxPathfindingPositions, false, true, false, null);
					return;
				}
				m_importanceLevel = 0f;
			}, () =>
			{
				m_componentCreature.ComponentLocomotion.LookOrder = m_look - m_componentCreature.ComponentLocomotion.LookAngles;

				if (m_componentPathfinding.IsStuck)
				{
					m_stateMachine.TransitionTo("Stuck");
				}
				if (m_componentPathfinding.Destination == null)
				{
					m_importanceLevel = 0f;
				}
				if (m_random.Float(0f, 1f) < 0.05f * m_dt)
				{
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}
				if (m_random.Float(0f, 1f) < 1.5f * m_dt)
				{
					m_look = new Vector2(
						MathUtils.DegToRad(45f) * m_random.Float(-1f, 1f),
						MathUtils.DegToRad(10f) * m_random.Float(-1f, 1f)
					);
				}
			}, null);
		}

		// ===== LOAD (lógica original + extras) =====
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_subsystemGreenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>();

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentChase = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_componentHireable = Entity.FindComponent<ComponentHireableNPC>();

			// Lógica original: leer desde DB
			HerdName = valuesDictionary.GetValue<string>("HerdName");
			m_herdingRange = valuesDictionary.GetValue<float>("HerdingRange");
			m_autoNearbyCreaturesHelp = valuesDictionary.GetValue<bool>("AutoNearbyCreaturesHelp");

			// Evento Injured (como en el original)
			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(OnInjured));

			SetupStateMachine();
		}

		// ===== SAVE =====
		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			if (!string.IsNullOrEmpty(HerdName))
			{
				valuesDictionary.SetValue("HerdName", HerdName);
			}
		}

		// ===== IMPOSSIBLE / ZOMBIE HELPERS =====
		private bool IsImpossibleModeActive()
		{
			if (m_subsystemGreenNightSky == null) return false;
			if (m_subsystemGreenNightSky.DifficultyMode != DifficultyMode.Impossible) return false;
			if (!m_subsystemGreenNightSky.IsGreenNightActive) return false;

			var zombiesSpawn = Project.FindSubsystem<SubsystemZombiesSpawn>(true);
			if (zombiesSpawn != null && zombiesSpawn.IsAllWavesCompleted) return false;

			if (zombiesSpawn != null && zombiesSpawn.ImpossibleBlockDisabledForAllies)
				return false;

			return true;
		}

		private bool IsZombieCreature(ComponentCreature creature)
		{
			if (creature == null) return false;
			return creature.Entity.FindComponent<ComponentZombieChaseBehavior>() != null
				|| creature.Entity.FindComponent<ComponentZombieHerdBehavior>() != null
				|| (creature.Entity.FindComponent<ComponentHerdBehavior>() is ComponentHerdBehavior h && h.HerdName == "Zombie")
				|| (creature.Entity.FindComponent<ComponentNewHerdBehavior>() is ComponentNewHerdBehavior nh && nh.HerdName == "Zombie");
		}
	}
}
