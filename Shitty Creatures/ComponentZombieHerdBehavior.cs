using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using static Game.SubsystemGreenNightSky;

namespace Game
{
	public class ComponentZombieHerdBehavior : ComponentBehavior, IUpdateable
	{
		// ==========================================
		// PROPIEDADES Y CAMPOS (copiados de ComponentHerdBehavior)
		// ==========================================
		public string HerdName { get; set; }
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		private SubsystemTime m_subsystemTime;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private StateMachine m_stateMachine = new StateMachine();
		private float m_dt;
		private float m_importanceLevel;
		private Random m_random = new Random();
		private Vector2 m_look;
		private float m_herdingRange;
		private bool m_autoNearbyCreaturesHelp;

		// ==========================================
		// CAMPOS ESPECÍFICOS DE ZOMBIE
		// ==========================================
		public bool CallForHelpWhenAttacked { get; set; } = true;
		public float HelpCallRange { get; set; } = 25f;
		public float HelpChaseTime { get; set; } = 30f;
		public bool IsPersistentHelp { get; set; } = false;
		public bool ZombieAggressiveGrouping { get; set; } = false;

		private DifficultyMode m_currentDifficulty;
		private float m_baseHelpCallRange;
		private bool m_baseCallForHelp;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;

		// ==========================================
		// MÉTODO LOAD (sin base, todo copiado)
		// ==========================================
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Inicialización de subsistemas (copiado de ComponentHerdBehavior)
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);

			// Cargar valores base
			HerdName = valuesDictionary.GetValue<string>("HerdName");
			if (string.IsNullOrEmpty(HerdName))
				HerdName = "Zombie";

			m_herdingRange = valuesDictionary.GetValue<float>("HerdingRange", 40f);
			m_autoNearbyCreaturesHelp = valuesDictionary.GetValue<bool>("AutoNearbyCreaturesHelp", true);

			// Cargar valores específicos de zombi
			CallForHelpWhenAttacked = valuesDictionary.GetValue<bool>("CallForHelpWhenAttacked", true);
			HelpCallRange = valuesDictionary.GetValue<float>("HelpCallRange", 25f);
			HelpChaseTime = valuesDictionary.GetValue<float>("HelpChaseTime", 30f);
			IsPersistentHelp = valuesDictionary.GetValue<bool>("IsPersistentHelp", false);
			ZombieAggressiveGrouping = valuesDictionary.GetValue<bool>("ZombieAggressiveGrouping", false);

			// Configurar handler de heridas (reemplaza el handler base)
			SetupZombieInjuryHandler();

			// ==========================================
			// ESTADOS DE LA MÁQUINA (copiados de ComponentHerdBehavior)
			// ==========================================
			m_stateMachine.AddState("Inactive", null, delegate
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
			}, null);

			m_stateMachine.AddState("Stuck", delegate
			{
				m_stateMachine.TransitionTo("Herd");
				if (m_random.Bool(0.5f))
				{
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
					m_importanceLevel = 0f;
				}
			}, null, null);

			m_stateMachine.AddState("Herd", delegate
			{
				Vector3? center = FindHerdCenter();
				if (center != null && Vector3.Distance(m_componentCreature.ComponentBody.Position, center.Value) > 6f)
				{
					float speed = (m_importanceLevel > 10f) ? m_random.Float(0.9f, 1f) : m_random.Float(0.25f, 0.35f);
					int maxPathfindingPositions = (m_importanceLevel > 200f) ? 100 : 0;
					m_componentPathfinding.SetDestination(new Vector3?(center.Value), speed, 7f, maxPathfindingPositions, false, true, false, null);
					return;
				}
				m_importanceLevel = 0f;
			}, delegate
			{
				m_componentCreature.ComponentLocomotion.LookOrder = m_look - m_componentCreature.ComponentLocomotion.LookAngles;
				if (m_componentPathfinding.IsStuck)
					m_stateMachine.TransitionTo("Stuck");
				if (m_componentPathfinding.Destination == null)
					m_importanceLevel = 0f;
				if (m_random.Float(0f, 1f) < 0.05f * m_dt)
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				if (m_random.Float(0f, 1f) < 1.5f * m_dt)
					m_look = new Vector2(MathUtils.DegToRad(45f) * m_random.Float(-1f, 1f), MathUtils.DegToRad(10f) * m_random.Float(-1f, 1f));
			}, null);

			// Estado adicional para deambular (opcional)
			AddZombieSpecificStates();

			// Inicializar GreenNight
			m_subsystemGreenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>(true);
			m_baseHelpCallRange = HelpCallRange;
			m_baseCallForHelp = CallForHelpWhenAttacked;
			if (m_subsystemGreenNightSky != null)
			{
				m_currentDifficulty = m_subsystemGreenNightSky.DifficultyMode;
				ApplyDifficultyToHerd();
			}

			// Transición inicial
			m_stateMachine.TransitionTo("Inactive");
		}

		// ==========================================
		// MÉTODO UPDATE (implementa IUpdateable)
		// ==========================================
		public void Update(float dt)
		{
			if (string.IsNullOrEmpty(m_stateMachine.CurrentState) || !IsActive)
				m_stateMachine.TransitionTo("Inactive");

			m_dt = dt;
			m_stateMachine.Update();

			// Comportamiento adicional de zombi (deambular)
			if (ZombieAggressiveGrouping && !IsActive)
			{
				if (m_random.Float(0f, 1f) < 0.001f * dt && m_stateMachine.CurrentState == "Inactive")
				{
					m_stateMachine.TransitionTo("ZombieRoam");
					m_importanceLevel = 5f;
				}
			}

			if (m_subsystemGreenNightSky != null)
				ApplyDifficultyToHerd();
		}

		// ==========================================
		// MÉTODOS BASE COPIADOS (sin hooks)
		// ==========================================
		public void CallNearbyCreaturesHelp(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (target == null || string.IsNullOrEmpty(HerdName))
				return;

			Vector3 position = target.ComponentBody.Position;
			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (Vector3.DistanceSquared(position, creature.ComponentBody.Position) < 256f)
				{
					// Usar ComponentZombieHerdBehavior en lugar de ComponentHerdBehavior
					ComponentZombieHerdBehavior herd = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (herd != null && !string.IsNullOrEmpty(herd.HerdName) && herd.HerdName == HerdName && herd.m_autoNearbyCreaturesHelp)
					{
						ComponentZombieChaseBehavior chase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
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
			if (string.IsNullOrEmpty(HerdName))
				return null;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			int count = 0;
			Vector3 sum = Vector3.Zero;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature.ComponentHealth.Health > 0f)
				{
					ComponentZombieHerdBehavior herd = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (herd != null && herd.HerdName == HerdName)
					{
						Vector3 pos = creature.ComponentBody.Position;
						if (Vector3.DistanceSquared(position, pos) < m_herdingRange * m_herdingRange)
						{
							sum += pos;
							count++;
						}
					}
				}
			}

			if (count > 0)
				return new Vector3?(sum / (float)count);
			return null;
		}

		// ==========================================
		// MÉTODOS ESPECÍFICOS DE ZOMBIE
		// ==========================================
		private void SetupZombieInjuryHandler()
		{
			ComponentHealth health = m_componentCreature.ComponentHealth;
			// Reemplazar el handler base (no usar Combine para evitar duplicados)
			health.Injured = delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				if (attacker == null) return;

				// Si el atacante es de la misma manada, huir y no llamar ayuda
				if (IsSameZombieHerd(attacker))
				{
					ActivateCustomFleeState(attacker);
					return;
				}

				// Si está configurado, llamar a otros zombis para ayudar
				if (CallForHelpWhenAttacked)
				{
					CallZombiesForHelp(attacker);
				}

				// También llamar a la versión base (para compatibilidad con otros comportamientos)
				// pero sin hooks, llamamos directamente a nuestro método
				CallNearbyCreaturesHelp(attacker, HelpCallRange, HelpChaseTime, IsPersistentHelp);
			};
		}

		private void ActivateCustomFleeState(ComponentCreature target)
		{
			if (target == null || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			Vector3 fleeDir = m_componentCreature.ComponentBody.Position - target.ComponentBody.Position;
			if (fleeDir.LengthSquared() > 0.01f)
			{
				fleeDir = Vector3.Normalize(fleeDir);
				Vector3 dest = m_componentCreature.ComponentBody.Position + fleeDir * 15f;
				m_componentPathfinding.SetDestination(new Vector3?(dest), 1f, 1.5f, 0, false, true, false, null);
				m_componentCreature.ComponentCreatureSounds.PlayPainSound();
			}
		}

		public void CallZombiesForHelp(ComponentCreature attacker)
		{
			if (attacker == null || string.IsNullOrEmpty(HerdName))
				return;

			// Si el atacante es de la misma manada, no llamar ayuda
			if (IsSameZombieHerd(attacker))
				return;

			// Llamar a todos los zombis cercanos (incluyendo los que no son de la manada? solo los de la manada)
			Vector3 pos = attacker.ComponentBody.Position;
			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature == m_componentCreature) continue;
				if (Vector3.DistanceSquared(pos, creature.ComponentBody.Position) < HelpCallRange * HelpCallRange)
				{
					ComponentZombieHerdBehavior herd = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (herd != null && herd.HerdName == HerdName)
					{
						ComponentZombieChaseBehavior chase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
						if (chase != null && chase.Target == null)
						{
							chase.Attack(attacker, HelpCallRange, HelpChaseTime, IsPersistentHelp);
						}
					}
				}
			}

			// Sonido de llamada
			m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);

			// Si está habilitado, llamar en rango extendido
			if (ZombieAggressiveGrouping)
				CallAdditionalZombies(attacker, HelpCallRange * 1.5f);
		}

		private void CallAdditionalZombies(ComponentCreature attacker, float extendedRange)
		{
			if (attacker == null || string.IsNullOrEmpty(HerdName))
				return;

			Vector3 pos = attacker.ComponentBody.Position;
			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature == m_componentCreature) continue;
				if (Vector3.DistanceSquared(pos, creature.ComponentBody.Position) < extendedRange * extendedRange)
				{
					ComponentZombieHerdBehavior herd = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (herd != null && herd.HerdName == HerdName)
					{
						ComponentZombieChaseBehavior chase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
						if (chase != null && chase.Target == null)
						{
							chase.Attack(attacker, HelpCallRange, HelpChaseTime, IsPersistentHelp);
						}
					}
				}
			}
		}

		public void CoordinateGroupAttack(ComponentCreature target)
		{
			if (target == null || string.IsNullOrEmpty(HerdName))
				return;

			if (IsSameZombieHerd(target))
				return;

			var nearby = GetNearbyZombies(HelpCallRange);
			foreach (var zombie in nearby)
			{
				if (IsSameZombieHerd(zombie))
					continue;

				ComponentZombieChaseBehavior chase = zombie.Entity.FindComponent<ComponentZombieChaseBehavior>();
				if (chase != null && chase.Target == null && m_random.Float(0f, 1f) < 0.7f)
				{
					chase.Attack(target, HelpCallRange, HelpChaseTime, IsPersistentHelp);
				}
			}
		}

		public List<ComponentCreature> GetNearbyZombies(float range)
		{
			var list = new List<ComponentCreature>();
			if (string.IsNullOrEmpty(HerdName))
				return list;

			Vector3 pos = m_componentCreature.ComponentBody.Position;
			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature == m_componentCreature) continue;
				if (creature.ComponentHealth.Health > 0f)
				{
					ComponentZombieHerdBehavior herd = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (herd != null && herd.HerdName == HerdName && Vector3.DistanceSquared(pos, creature.ComponentBody.Position) < range * range)
					{
						list.Add(creature);
					}
				}
			}
			return list;
		}

		public bool IsSameZombieHerd(ComponentCreature other)
		{
			if (other == null || string.IsNullOrEmpty(HerdName))
				return false;

			ComponentZombieHerdBehavior herd = other.Entity.FindComponent<ComponentZombieHerdBehavior>();
			return herd != null && herd.HerdName == HerdName;
		}

		private void AddZombieSpecificStates()
		{
			if (!ZombieAggressiveGrouping) return;

			m_stateMachine.AddState("ZombieRoam", delegate
			{
				if (m_random.Float(0f, 1f) < 0.1f)
				{
					Vector3 dir = new Vector3(m_random.Float(-1f, 1f), 0f, m_random.Float(-1f, 1f));
					if (dir.LengthSquared() > 0.01f)
					{
						dir = Vector3.Normalize(dir);
						Vector3 dest = m_componentCreature.ComponentBody.Position + dir * 15f;
						m_componentPathfinding.SetDestination(new Vector3?(dest), m_random.Float(0.3f, 0.5f), 5f, 0, false, true, false, null);
					}
				}
			}, delegate
			{
				if (IsActive)
					m_stateMachine.TransitionTo("Herd");

				if (m_random.Float(0f, 1f) < 0.02f * m_dt)
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);

				if (m_componentPathfinding.Destination == null)
					m_stateMachine.TransitionTo("Inactive");
			}, null);
		}

		private void ApplyDifficultyToHerd()
		{
			if (m_subsystemGreenNightSky == null) return;
			DifficultyMode mode = m_subsystemGreenNightSky.DifficultyMode;
			if (mode == m_currentDifficulty) return;
			m_currentDifficulty = mode;

			float rangeMult = SubsystemGreenNightSky.DifficultyModifiers.GetHelpCallRangeMultiplier(mode);
			HelpCallRange = m_baseHelpCallRange * rangeMult;

			CallForHelpWhenAttacked = SubsystemGreenNightSky.DifficultyModifiers.ShouldAlwaysCallHelp(mode);
		}
	}
}
