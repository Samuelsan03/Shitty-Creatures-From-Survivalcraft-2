using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class Healing : ComponentBehavior, IUpdateable
	{
		List<ComponentBody> m_allyHealBodies = new List<ComponentBody>();
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		public bool SelfHealing { get; set; } = false;
		public bool HealCreatures { get; set; } = false;
		public float Probability { get; set; } = 0.5f;

		SubsystemTime m_subsystemTime;
		SubsystemParticles m_subsystemParticles;
		SubsystemAudio m_subsystemAudio;
		ComponentCreature m_componentCreature;
		ComponentHealth m_componentHealth;
		ComponentPathfinding m_componentPathfinding;
		ComponentCreatureModel m_componentCreatureModel;
		ComponentCreatureSounds m_componentCreatureSounds;   // ¡restaurado!
		ComponentNewHerdBehavior m_componentHerd;

		Random m_random = new Random();
		StateMachine m_stateMachine = new StateMachine();
		float m_importanceLevel;
		float m_dt;
		double m_nextUpdateTime;

		// Auto‑curación
		bool m_isSelfHealing;
		double m_selfHealStartTime;
		HealingParticleSystem m_selfHealParticles;

		// Curación masiva a compañeros
		List<ComponentCreature> m_healTargets = new List<ComponentCreature>();
		bool m_isHealingAllies;
		double m_allyHealStartTime;
		HealingParticleSystem m_healerChargeParticles;   // partículas en el sanador durante la carga
		List<HealingParticleSystem> m_allyHealParticles = new List<HealingParticleSystem>();
		bool m_alliesHealApplied;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentCreatureSounds = Entity.FindComponent<ComponentCreatureSounds>(true);   // ← cargado
			m_componentHerd = Entity.FindComponent<ComponentNewHerdBehavior>();

			SelfHealing = valuesDictionary.GetValue<bool>("SelfHealing", false);
			HealCreatures = valuesDictionary.GetValue<bool>("HealCreatures", false);
			Probability = valuesDictionary.GetValue<float>("Probability", 0.5f);

			SetupStateMachine();
			m_stateMachine.TransitionTo("Idle");
		}

		void SetupStateMachine()
		{
			m_stateMachine.AddState("Idle", null, () =>
			{
				if (m_subsystemTime.GameTime >= m_nextUpdateTime)
				{
					m_dt = m_random.Float(0.25f, 0.35f);
					m_nextUpdateTime = m_subsystemTime.GameTime + m_dt;

					if (SelfHealing && m_componentHealth.Health > 0f && m_componentHealth.Health < 0.2f && m_random.Float(0f, 1f) < Probability)
					{
						m_stateMachine.TransitionTo("SelfHealing");
						return;
					}

					if (HealCreatures && m_componentHerd != null)
					{
						FindAllHurtAllies(m_healTargets);
						if (m_healTargets.Count > 0 && m_random.Float(0f, 1f) < Probability)
						{
							m_stateMachine.TransitionTo("HealingAllies");
							return;
						}
					}
				}
			}, null);

			// Auto‑curación
			m_stateMachine.AddState("SelfHealing", () =>
			{
				m_isSelfHealing = true;
				m_selfHealStartTime = m_subsystemTime.GameTime;
				m_selfHealParticles = new HealingParticleSystem();
				m_subsystemParticles.AddParticleSystem(m_selfHealParticles, false);
				m_componentPathfinding.Stop();
				m_componentCreatureModel.AimHandAngleOrder = 3.2f;
				// Ambos sonidos: primero idle, luego mágico
				m_componentCreatureSounds.PlayIdleSound(false);
				m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, m_componentCreature.ComponentBody.Position, 3f, true);
			}, () =>
			{
				double elapsed = m_subsystemTime.GameTime - m_selfHealStartTime;
				m_selfHealParticles.BoundingBox = m_componentCreature.ComponentBody.BoundingBox;
				m_componentCreatureModel.AimHandAngleOrder = 3.2f;

				if (elapsed >= 1.5)
				{
					m_componentHealth.Health = 1f;
					m_componentCreatureModel.AimHandAngleOrder = 0f;
					m_selfHealParticles.Stopped = true;
					m_stateMachine.TransitionTo("Idle");
				}
			}, () =>
			{
				m_componentCreatureModel.AimHandAngleOrder = 0f;
				if (m_selfHealParticles != null)
					m_selfHealParticles.Stopped = true;
				m_isSelfHealing = false;
			});

			// Curación a TODOS los aliados
			m_stateMachine.AddState("HealingAllies", () =>
			{
				m_isHealingAllies = true;
				m_allyHealStartTime = m_subsystemTime.GameTime;

				// Partículas del sanador
				m_healerChargeParticles = new HealingParticleSystem();
				m_subsystemParticles.AddParticleSystem(m_healerChargeParticles, false);

				m_componentPathfinding.Stop();
				if (m_healTargets.Count > 0)
					m_componentCreatureModel.LookAtOrder = m_healTargets[0].ComponentCreatureModel.EyePosition;
				m_componentCreatureModel.AimHandAngleOrder = 3.2f;

				m_componentCreatureSounds.PlayIdleSound(false);
				m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, m_componentCreature.ComponentBody.Position, 3f, true);

				// Partículas en cada aliado herido
				m_allyHealParticles.Clear();
				m_allyHealBodies.Clear();
				foreach (var ally in m_healTargets)
				{
					if (ally == null) continue;
					var allyParticles = new HealingParticleSystem();
					m_allyHealParticles.Add(allyParticles);
					m_subsystemParticles.AddParticleSystem(allyParticles, false);
					allyParticles.BoundingBox = ally.ComponentBody.BoundingBox;
					m_allyHealBodies.Add(ally.ComponentBody);
				}

				m_alliesHealApplied = false;
			}, () =>
			{
				m_healTargets.RemoveAll(c => c == null || c.ComponentHealth.Health <= 0f || c.ComponentHealth.Health >= 1f);

				if (m_healTargets.Count == 0)
				{
					CleanupHealingAllies();
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				double elapsed = m_subsystemTime.GameTime - m_allyHealStartTime;
				m_healerChargeParticles.BoundingBox = m_componentCreature.ComponentBody.BoundingBox;
				m_componentCreatureModel.AimHandAngleOrder = 3.2f;

				// *** ACTUALIZAR PARTÍCULAS DE ALIADOS ***
				for (int i = 0; i < m_allyHealParticles.Count; i++)
				{
					if (m_allyHealParticles[i] != null && i < m_allyHealBodies.Count)
						m_allyHealParticles[i].BoundingBox = m_allyHealBodies[i].BoundingBox;
				}

				if (!m_alliesHealApplied && elapsed >= 1.5)
				{
					foreach (var ally in m_healTargets)
						if (ally != null && ally.ComponentHealth.Health > 0f)
							ally.ComponentHealth.Health = 1f;

					m_alliesHealApplied = true;
					m_healerChargeParticles.Stopped = true;
				}
				else if (m_alliesHealApplied && elapsed >= 2.0)
				{
					CleanupHealingAllies();
					m_stateMachine.TransitionTo("Idle");
				}
			}, () =>
			{
				CleanupHealingAllies();
			});

			void CleanupHealingAllies()
			{
				m_componentCreatureModel.AimHandAngleOrder = 0f;
				if (m_healerChargeParticles != null)
					m_healerChargeParticles.Stopped = true;
				foreach (var ps in m_allyHealParticles)
					if (ps != null) ps.Stopped = true;
				m_allyHealParticles.Clear();
				m_healTargets.Clear();
				m_isHealingAllies = false;
			}
		}

		public virtual void Update(float dt)
		{
			m_stateMachine.Update();
		}

		void FindAllHurtAllies(List<ComponentCreature> list)
		{
			list.Clear();
			if (m_componentHerd == null || string.IsNullOrEmpty(m_componentHerd.HerdName))
				return;

			Vector3 pos = m_componentCreature.ComponentBody.Position;
			foreach (ComponentCreature creature in Project.FindSubsystem<SubsystemCreatureSpawn>(true).Creatures)
			{
				if (creature == m_componentCreature || creature.ComponentHealth.Health <= 0f || creature.ComponentHealth.Health >= 0.2f)
					continue;

				if (Vector3.DistanceSquared(pos, creature.ComponentBody.Position) > 100f) // radio 10
					continue;

				ComponentNewHerdBehavior herd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (herd != null && m_componentHerd.IsSameHerdOrGuardian(creature))
					list.Add(creature);
			}
		}
	}
}
