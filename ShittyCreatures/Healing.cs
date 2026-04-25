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
		public float HealRadius { get; set; } = 50f;
		public bool CureDiseases { get; set; } = false;

		SubsystemTime m_subsystemTime;
		SubsystemParticles m_subsystemParticles;
		SubsystemAudio m_subsystemAudio;
		ComponentCreature m_componentCreature;
		ComponentHealth m_componentHealth;
		ComponentPathfinding m_componentPathfinding;
		ComponentCreatureModel m_componentCreatureModel;
		ComponentCreatureSounds m_componentCreatureSounds;
		ComponentNewHerdBehavior m_componentHerd;
		ComponentNewChaseBehavior m_componentChase;

		Random m_random = new Random();
		StateMachine m_stateMachine = new StateMachine();
		float m_importanceLevel;
		float m_dt;
		double m_nextUpdateTime;

		bool m_isSelfHealing;
		double m_selfHealStartTime;
		HealingParticleSystem m_selfHealParticles;

		List<ComponentCreature> m_healTargets = new List<ComponentCreature>();
		bool m_isHealingAllies;
		double m_allyHealStartTime;
		HealingParticleSystem m_healerChargeParticles;
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
			m_componentCreatureSounds = Entity.FindComponent<ComponentCreatureSounds>(true);
			m_componentHerd = Entity.FindComponent<ComponentNewHerdBehavior>();
			m_componentChase = Entity.FindComponent<ComponentNewChaseBehavior>();

			SelfHealing = valuesDictionary.GetValue<bool>("SelfHealing", false);
			HealCreatures = valuesDictionary.GetValue<bool>("HealCreatures", false);
			Probability = valuesDictionary.GetValue<float>("Probability", 0.5f);
			CureDiseases = valuesDictionary.GetValue<bool>("CureDiseases", false);

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
						FindAllHurtOrDiseasedAllies(m_healTargets);
						if (m_healTargets.Count > 0 && m_random.Float(0f, 1f) < Probability)
						{
							m_stateMachine.TransitionTo("HealingAllies");
							return;
						}
					}
				}
			}, null);

			m_stateMachine.AddState("SelfHealing", () =>
			{
				m_isSelfHealing = true;
				m_selfHealStartTime = m_subsystemTime.GameTime;
				m_selfHealParticles = new HealingParticleSystem();
				m_subsystemParticles.AddParticleSystem(m_selfHealParticles, false);
				m_componentPathfinding.Stop();
				m_componentCreatureModel.AimHandAngleOrder = 3.2f;
				m_componentCreatureSounds.PlayIdleSound(false);
				m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, m_componentCreature.ComponentBody.Position, 3f, true);
			}, () =>
			{
				double elapsed = m_subsystemTime.GameTime - m_selfHealStartTime;
				m_selfHealParticles.BoundingBox = m_componentCreature.ComponentBody.BoundingBox;
				m_componentCreatureModel.AimHandAngleOrder = 3.2f;

				if (elapsed >= 1.5)
				{
					if (CureDiseases)
						CureCreatureDiseases(m_componentCreature);

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

			m_stateMachine.AddState("HealingAllies", () =>
			{
				if (m_componentChase != null) m_componentChase.Suppressed = true;

				m_isHealingAllies = true;
				m_allyHealStartTime = m_subsystemTime.GameTime;

				m_healerChargeParticles = new HealingParticleSystem();
				m_subsystemParticles.AddParticleSystem(m_healerChargeParticles, false);

				m_componentPathfinding.Stop();
				if (m_healTargets.Count > 0)
					m_componentCreatureModel.LookAtOrder = m_healTargets[0].ComponentCreatureModel.EyePosition;
				m_componentCreatureModel.AimHandAngleOrder = 3.2f;

				m_componentCreatureSounds.PlayIdleSound(false);
				m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, m_componentCreature.ComponentBody.Position, 3f, true);

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
				m_healTargets.RemoveAll(c => c == null || c.ComponentHealth.Health <= 0f);

				if (m_healTargets.Count == 0)
				{
					CleanupHealingAllies();
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				double elapsed = m_subsystemTime.GameTime - m_allyHealStartTime;
				m_healerChargeParticles.BoundingBox = m_componentCreature.ComponentBody.BoundingBox;
				m_componentCreatureModel.AimHandAngleOrder = 3.2f;

				for (int i = 0; i < m_allyHealParticles.Count; i++)
				{
					if (m_allyHealParticles[i] != null && i < m_allyHealBodies.Count)
						m_allyHealParticles[i].BoundingBox = m_allyHealBodies[i].BoundingBox;
				}

				if (!m_alliesHealApplied && elapsed >= 1.5)
				{
					foreach (var ally in m_healTargets)
					{
						if (ally != null && ally.ComponentHealth.Health > 0f)
						{
							if (CureDiseases)
								CureCreatureDiseases(ally);

							// Restaurar salud solo si no está ya llena
							if (ally.ComponentHealth.Health < 1f)
								ally.ComponentHealth.Health = 1f;
						}
					}

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
				if (m_componentChase != null) m_componentChase.Suppressed = false;
			}
		}

		public virtual void Update(float dt)
		{
			m_stateMachine.Update();
		}

		void FindAllHurtOrDiseasedAllies(List<ComponentCreature> list)
		{
			list.Clear();
			if (m_componentHerd == null || string.IsNullOrEmpty(m_componentHerd.HerdName))
				return;

			Vector3 pos = m_componentCreature.ComponentBody.Position;
			float radiusSq = HealRadius * HealRadius;
			foreach (ComponentCreature creature in Project.FindSubsystem<SubsystemCreatureSpawn>(true).Creatures)
			{
				if (creature == m_componentCreature || creature.ComponentHealth.Health <= 0f)
					continue;

				if (Vector3.DistanceSquared(pos, creature.ComponentBody.Position) > radiusSq)
					continue;

				ComponentNewHerdBehavior herd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (herd == null || !m_componentHerd.IsSameHerdOrGuardian(creature))
					continue;

				bool isHurt = creature.ComponentHealth.Health < 0.2f;
				bool isDiseased = false;

				if (CureDiseases)
				{
					isDiseased = (creature.Entity.FindComponent<ComponentFluInfected>()?.IsInfected == true)
							  || (creature.Entity.FindComponent<ComponentPoisonInfected>()?.IsInfected == true)
							  || (creature.Entity.FindComponent<ComponentFlu>()?.HasFlu == true)
							  || (creature.Entity.FindComponent<ComponentSickness>()?.IsSick == true);
				}

				if (isHurt || isDiseased)
				{
					list.Add(creature);
				}
			}
		}

		void CureCreatureDiseases(ComponentCreature creature)
		{
			if (creature == null) return;

			var fluInfected = creature.Entity.FindComponent<ComponentFluInfected>();
			if (fluInfected != null && fluInfected.IsInfected)
				fluInfected.m_fluDuration = 0f;

			var poisonInfected = creature.Entity.FindComponent<ComponentPoisonInfected>();
			if (poisonInfected != null && poisonInfected.IsInfected)
				poisonInfected.m_InfectDuration = 0f;

			var playerFlu = creature.Entity.FindComponent<ComponentFlu>();
			if (playerFlu != null && playerFlu.HasFlu)
				playerFlu.m_fluDuration = 0f;

			var playerSickness = creature.Entity.FindComponent<ComponentSickness>();
			if (playerSickness != null && playerSickness.IsSick)
				playerSickness.m_sicknessDuration = 0f;
		}
	}
}
