using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCloningBehavior : ComponentBehavior, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		// ===== Parámetros (solo 3 ahora) =====
		public int CloneCount = 5;
		public bool CanClone = false;
		public float CloningProbability = 0.3f;

		// ===== Constantes internas =====
		private const int RequiredAttackers = 3;        // mínimo de atacantes o miembros de manada en combate
		private const float AttackerMemory = 10f;       // segundos que recordamos a un atacante
		private const float PreparationTime = 2.5f;     // duración de la fase de preparación

		// ===== Campos internos =====
		private SubsystemTime m_subsystemTime;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;

		private ComponentCreature m_componentCreature;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentNewChaseBehavior m_componentChase;
		private ComponentNewHerdBehavior m_componentHerd;
		private ComponentHireableNPC m_componentHireable;

		private StateMachine m_stateMachine = new StateMachine();
		private Random m_random = new Random();
		private float m_importanceLevel;
		private float m_dt;

		private CloningParticleSystem m_particleSystem;
		private double m_preparationStartTime;
		private ComponentCreature m_target;
		private string m_cloneTemplateName;
		private bool m_hasClonedDuringChase = false;
		private List<Entity> m_activeClones = new List<Entity>();

		// Control de atacantes individuales
		private Dictionary<ComponentCreature, double> m_recentAttackers = new Dictionary<ComponentCreature, double>();

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentChase = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_componentHerd = Entity.FindComponent<ComponentNewHerdBehavior>();
			m_componentHireable = Entity.FindComponent<ComponentHireableNPC>();

			CloneCount = valuesDictionary.GetValue<int>("CloneCount", 5);
			CanClone = valuesDictionary.GetValue<bool>("CanClone", false);
			CloningProbability = valuesDictionary.GetValue<float>("CloningProbability", 0.3f);

			m_cloneTemplateName = Entity.ValuesDictionary.DatabaseObject.Name;

			m_componentCreature.ComponentHealth.Injured += OnInjured;

			SetupStateMachine();
			m_stateMachine.TransitionTo("Idle");
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap) { }

		public void Update(float dt)
		{
			if (m_componentHireable != null && !m_componentHireable.IsHired)
				return;

			if (!CanClone)
				return;

			// Si la persecución ha terminado, reseteamos el flag y destruimos clones
			if (m_componentChase.Target == null)
			{
				if (m_hasClonedDuringChase)
				{
					m_hasClonedDuringChase = false;
					DestroyAllClones();
				}
			}

			// Eliminar atacantes antiguos
			var toRemove = new List<ComponentCreature>();
			foreach (var pair in m_recentAttackers)
			{
				if (pair.Value < m_subsystemTime.GameTime - AttackerMemory ||
					pair.Key == null || pair.Key.ComponentHealth.Health <= 0f)
				{
					toRemove.Add(pair.Key);
				}
			}
			foreach (var key in toRemove)
				m_recentAttackers.Remove(key);

			m_dt = dt;
			m_stateMachine.Update();
		}

		private void OnInjured(Injury injury)
		{
			if (m_componentHireable != null && !m_componentHireable.IsHired)
				return;

			ComponentCreature attacker = injury.Attacker;
			if (attacker != null && attacker != m_componentCreature &&
				!m_recentAttackers.ContainsKey(attacker))
			{
				m_recentAttackers[attacker] = m_subsystemTime.GameTime;
			}
		}

		private bool IsCloningConditionMet()
		{
			if (m_recentAttackers.Count >= RequiredAttackers)
				return true;

			if (m_componentHerd != null && !string.IsNullOrEmpty(m_componentHerd.HerdName))
			{
				int herdCombatCount = 0;
				string herdName = m_componentHerd.HerdName;
				foreach (var creature in m_subsystemCreatureSpawn.Creatures)
				{
					if (creature == m_componentCreature || creature.ComponentHealth.Health <= 0f)
						continue;
					var otherHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
					if (otherHerd == null || !otherHerd.HerdName.Equals(herdName, StringComparison.OrdinalIgnoreCase))
						continue;
					var chase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
					if (chase != null && chase.Target != null && chase.Target.ComponentHealth.Health > 0f)
						herdCombatCount++;
				}
				if (herdCombatCount >= RequiredAttackers)
					return true;
			}
			return false;
		}

		private void SetupStateMachine()
		{
			m_stateMachine.AddState("Idle", null, () =>
			{
				if (CanClone && m_componentChase != null && m_componentChase.Target != null &&
					!m_hasClonedDuringChase && IsCloningConditionMet() &&
					m_random.Float(0f, 1f) < CloningProbability * m_dt)
				{
					m_target = m_componentChase.Target;
					m_stateMachine.TransitionTo("Preparing");
				}
			}, null);

			m_stateMachine.AddState("Preparing", () =>
			{
				m_preparationStartTime = m_subsystemTime.GameTime;
				if (m_componentPathfinding != null)
					m_componentPathfinding.Stop();
				m_componentCreatureModel.AttackOrder = false;

				if (m_particleSystem == null)
				{
					m_particleSystem = new CloningParticleSystem();
					m_subsystemParticles.AddParticleSystem(m_particleSystem, false);
				}
				m_particleSystem.BoundingBox = m_componentCreature.ComponentBody.BoundingBox;
				m_particleSystem.Stopped = false;
			}, () =>
			{
				if (m_target == null || m_target.ComponentHealth.Health <= 0f)
				{
					StopPreparation();
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				m_componentCreatureModel.LookAtOrder = m_target.ComponentCreatureModel.EyePosition;
				m_componentCreatureModel.AimHandAngleOrder = 3.2f;
				m_componentCreatureModel.InHandItemOffsetOrder = new Vector3(0f, -0.25f, 0f);
				m_componentCreatureModel.InHandItemRotationOrder = new Vector3(3.14159f, 0f, 0f);

				if (m_particleSystem != null)
					m_particleSystem.BoundingBox = m_componentCreature.ComponentBody.BoundingBox;

				if (m_subsystemTime.GameTime - m_preparationStartTime >= PreparationTime)
				{
					m_stateMachine.TransitionTo("Cloning");
				}
			}, () =>
			{
				StopPreparation();
			});

			m_stateMachine.AddState("Cloning", () =>
			{
				m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
				m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, m_componentCreature.ComponentBody.Position, 3f, true);

				SpawnClones(m_target);

				m_recentAttackers.Clear();
				m_hasClonedDuringChase = true;   // solo una clonación por persecución

				if (m_particleSystem != null)
					m_particleSystem.Stopped = true;

				m_stateMachine.TransitionTo("Idle");
			}, null, () =>
			{
				StopPreparation();
			});
		}

		private void StopPreparation()
		{
			if (m_particleSystem != null)
				m_particleSystem.Stopped = true;
			m_componentCreatureModel.AimHandAngleOrder = 0f;
			m_componentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
			m_componentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
			m_componentCreatureModel.LookAtOrder = null;
		}

		private void SpawnClones(ComponentCreature target)
		{
			if (string.IsNullOrEmpty(m_cloneTemplateName))
				return;

			Vector3 basePos = m_componentCreature.ComponentBody.Position;
			Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
			Vector3 right = m_componentCreature.ComponentBody.Matrix.Right;

			for (int i = 0; i < CloneCount; i++)
			{
				float angle = m_random.Float(-2f, 2f);
				float dist = m_random.Float(2f, 4f);
				Vector3 offset = (forward * MathF.Cos(angle) + right * MathF.Sin(angle)) * dist;
				Vector3 spawnPos = basePos + offset;
				spawnPos.Y = basePos.Y;

				Entity cloneEntity = DatabaseManager.CreateEntity(Project, m_cloneTemplateName, true);
				if (cloneEntity == null) continue;

				ComponentBody cloneBody = cloneEntity.FindComponent<ComponentBody>(true);
				cloneBody.Position = spawnPos;
				cloneBody.Rotation = m_componentCreature.ComponentBody.Rotation;

				ComponentHealth cloneHealth = cloneEntity.FindComponent<ComponentHealth>();
				if (cloneHealth != null)
					cloneHealth.AttackResilience = float.MaxValue;

				ComponentCloningBehavior cloneCloning = cloneEntity.FindComponent<ComponentCloningBehavior>();
				if (cloneCloning != null)
					cloneCloning.CanClone = false;

				// Ya no se usa CloneDuration, los clones se eliminan cuando termina la persecución
				ComponentNewChaseBehavior cloneChase = cloneEntity.FindComponent<ComponentNewChaseBehavior>();
				if (cloneChase != null && target != null)
				{
					cloneChase.Attack(target, 30f, 60f /* tiempo máximo por si acaso */, false);
				}

				Project.AddEntity(cloneEntity);
				m_activeClones.Add(cloneEntity);
			}
		}

		private void DestroyAllClones()
		{
			foreach (var entity in m_activeClones)
			{
				if (entity != null && entity.IsAddedToProject)
					Project.RemoveEntity(entity, true);
			}
			m_activeClones.Clear();
		}
	}
}
