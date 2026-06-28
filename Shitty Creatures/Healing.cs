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

		/// <summary>
		/// Verifica si una criatura está realmente viva y puede ser curada
		/// </summary>
		private bool IsCreatureAliveAndHealable(ComponentCreature creature)
		{
			if (creature == null)
				return false;

			var health = creature.ComponentHealth;
			if (health == null)
				return false;

			if (health.Health <= 0f)
				return false;

			if (health.DeathTime.HasValue)
				return false;

			if (creature.ComponentBody == null)
				return false;

			return true;
		}

		/// <summary>
		/// Verifica si una criatura está herida Y puede ser curada (no muerta)
		/// </summary>
		private bool IsCreatureHurtAndHealable(ComponentCreature creature)
		{
			if (!IsCreatureAliveAndHealable(creature))
				return false;

			return creature.ComponentHealth.Health < 0.2f;
		}

		/// <summary>
		/// Verifica si una criatura está enferma (gripe o veneno)
		/// </summary>
		private bool IsCreatureDiseased(ComponentCreature creature)
		{
			if (creature == null)
				return false;

			return (creature.Entity.FindComponent<ComponentFluInfected>()?.IsInfected == true)
				|| (creature.Entity.FindComponent<ComponentPoisonInfected>()?.IsInfected == true)
				|| (creature.Entity.FindComponent<ComponentFlu>()?.HasFlu == true)
				|| (creature.Entity.FindComponent<ComponentSickness>()?.IsSick == true);
		}

		/// <summary>
		/// Verifica si una criatura necesita curación (herida O enferma)
		/// </summary>
		private bool NeedsHealing(ComponentCreature creature)
		{
			if (!IsCreatureAliveAndHealable(creature))
				return false;

			bool isHurt = creature.ComponentHealth.Health < 1f;
			bool isDiseased = CureDiseases && IsCreatureDiseased(creature);

			return isHurt || isDiseased;
		}

		/// <summary>
		/// Verifica si la criatura sanadora necesita auto-curarse
		/// </summary>
		private bool NeedsSelfHealing()
		{
			if (!IsCreatureAliveAndHealable(m_componentCreature))
				return false;

			bool isHurt = m_componentHealth.Health < 0.2f;
			bool isDiseased = CureDiseases && IsCreatureDiseased(m_componentCreature);

			return isHurt || isDiseased;
		}

		/// <summary>
		/// Verifica si una criatura está contratada (solo se curan NPCs contratados)
		/// </summary>
		private bool IsCreatureHired(ComponentCreature creature)
		{
			if (creature == null)
				return false;

			var hireable = creature.Entity.FindComponent<ComponentHireableNPC>();
			return hireable != null && hireable.IsHired;
		}

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
				// No intentar curar si el sanador está muerto
				if (!IsCreatureAliveAndHealable(m_componentCreature))
					return;

				if (m_subsystemTime.GameTime >= m_nextUpdateTime)
				{
					m_dt = m_random.Float(0.25f, 0.35f);
					m_nextUpdateTime = m_subsystemTime.GameTime + m_dt;

					if (SelfHealing && NeedsSelfHealing() && m_random.Float(0f, 1f) < Probability)
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
				if (!IsCreatureAliveAndHealable(m_componentCreature))
				{
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				m_isSelfHealing = true;
				m_selfHealStartTime = m_subsystemTime.GameTime;
				m_selfHealParticles = new HealingParticleSystem();
				m_subsystemParticles.AddParticleSystem(m_selfHealParticles, false);
				m_componentPathfinding.Stop();
				if (m_componentHerd != null)
				{
					m_componentHerd.IsActive = false;
				}
				m_componentCreatureModel.AimHandAngleOrder = 3.2f;
				m_componentCreatureSounds.PlayIdleSound(false);
				m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, m_componentCreature.ComponentBody.Position, 3f, true);
			}, () =>
			{
				if (!IsCreatureAliveAndHealable(m_componentCreature))
				{
					m_componentCreatureModel.AimHandAngleOrder = 0f;
					if (m_selfHealParticles != null)
						m_selfHealParticles.Stopped = true;
					m_isSelfHealing = false;
					if (m_componentHerd != null)
					{
						m_componentHerd.IsActive = true;
					}
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				double elapsed = m_subsystemTime.GameTime - m_selfHealStartTime;
				m_selfHealParticles.BoundingBox = m_componentCreature.ComponentBody.BoundingBox;
				m_componentCreatureModel.AimHandAngleOrder = 3.2f;

				if (elapsed >= 1.5)
				{
					if (!IsCreatureAliveAndHealable(m_componentCreature))
					{
						m_componentCreatureModel.AimHandAngleOrder = 0f;
						if (m_selfHealParticles != null)
							m_selfHealParticles.Stopped = true;
						m_isSelfHealing = false;
						if (m_componentHerd != null)
						{
							m_componentHerd.IsActive = true;
						}
						m_stateMachine.TransitionTo("Idle");
						return;
					}

					if (!CanHealCreature(m_componentCreature))
					{
						m_componentCreatureModel.AimHandAngleOrder = 0f;
						if (m_selfHealParticles != null)
							m_selfHealParticles.Stopped = true;
						m_stateMachine.TransitionTo("Idle");
						return;
					}

					if (CureDiseases)
						CureCreatureDiseases(m_componentCreature, null);

					if (m_componentHealth.Health < 1f)
					{
						m_componentHealth.Health = 1f;
					}

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
				if (m_componentHerd != null)
				{
					m_componentHerd.IsActive = true;
				}
			});

			m_stateMachine.AddState("HealingAllies", () =>
			{
				if (!IsCreatureAliveAndHealable(m_componentCreature))
				{
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				if (m_componentChase != null) m_componentChase.Suppressed = true;

				m_isHealingAllies = true;
				m_allyHealStartTime = m_subsystemTime.GameTime;

				m_healerChargeParticles = new HealingParticleSystem();
				m_subsystemParticles.AddParticleSystem(m_healerChargeParticles, false);

				m_componentPathfinding.Stop();
				if (m_componentHerd != null)
				{
					m_componentHerd.IsActive = false;
				}

				m_componentCreatureModel.AimHandAngleOrder = 3.2f;

				m_componentCreatureSounds.PlayIdleSound(false);
				m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, m_componentCreature.ComponentBody.Position, 3f, true);

				m_allyHealParticles.Clear();
				m_allyHealBodies.Clear();

				foreach (var ally in m_healTargets)
				{
					if (!IsCreatureAliveAndHealable(ally))
						continue;

					// *** VERIFICACIÓN ADICIONAL: Solo curar si está contratada ***
					if (!IsCreatureHired(ally))
						continue;

					var allyParticles = new HealingParticleSystem();
					m_allyHealParticles.Add(allyParticles);
					m_subsystemParticles.AddParticleSystem(allyParticles, false);
					allyParticles.BoundingBox = ally.ComponentBody.BoundingBox;
					m_allyHealBodies.Add(ally.ComponentBody);
				}

				if (m_allyHealBodies.Count == 0)
				{
					CleanupHealingAllies();
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				m_alliesHealApplied = false;
			}, () =>
			{
				if (!IsCreatureAliveAndHealable(m_componentCreature))
				{
					CleanupHealingAllies();
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				m_healTargets.RemoveAll(c => !IsCreatureAliveAndHealable(c) || !IsCreatureHired(c));

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
						if (!CanHealCreature(ally))
							continue;

						if (!IsCreatureAliveAndHealable(ally))
							continue;

						// *** VERIFICACIÓN ADICIONAL: Solo curar si está contratada ***
						if (!IsCreatureHired(ally))
							continue;

						if (ally.ComponentHealth.Health <= 0f || ally.ComponentHealth.DeathTime.HasValue)
							continue;

						bool diseaseCured = false;
						if (CureDiseases)
							diseaseCured = CureCreatureDiseases(ally, m_componentCreature);

						if (!CanHealCreature(ally))
							continue;

						if (!IsCreatureAliveAndHealable(ally))
							continue;

						if (ally.ComponentHealth.Health > 0f && ally.ComponentHealth.Health < 1f)
						{
							ally.ComponentHealth.Health = 1f;

							if (!diseaseCured && ally is ComponentPlayer player)
							{
								string healMsg = LanguageControl.Get("Healing", "0");
								if (!string.IsNullOrEmpty(healMsg))
								{
									healMsg = string.Format(healMsg, m_componentCreature.DisplayName);
									player.ComponentGui.DisplaySmallMessage(healMsg, new Color(50, 200, 50), false, false);
									m_subsystemAudio.PlaySound("Audio/UI/success", 1f, 0f, 0f, 0f);
								}
							}
						}
						else if (diseaseCured && ally is ComponentPlayer player)
						{
							// El mensaje ya se muestra en CureCreatureDiseases
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
				m_allyHealBodies.Clear();
				m_healTargets.Clear();
				m_isHealingAllies = false;
				if (m_componentChase != null) m_componentChase.Suppressed = false;
				if (m_componentHerd != null)
				{
					m_componentHerd.IsActive = true;
				}
			}
		}

		public virtual void Update(float dt)
		{
			if (!IsCreatureAliveAndHealable(m_componentCreature))
			{
				if (m_isSelfHealing || m_isHealingAllies)
				{
					m_stateMachine.TransitionTo("Idle");
				}
				return;
			}

			m_stateMachine.Update();
		}

		void FindAllHurtOrDiseasedAllies(List<ComponentCreature> list)
		{
			list.Clear();
			if (m_componentHerd == null || string.IsNullOrEmpty(m_componentHerd.HerdName))
				return;

			Vector3 pos = m_componentCreature.ComponentBody.Position;
			float radiusSq = HealRadius * HealRadius;
			SubsystemGameInfo gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);

			foreach (ComponentCreature creature in Project.FindSubsystem<SubsystemCreatureSpawn>(true).Creatures)
			{
				if (creature == m_componentCreature)
					continue;

				if (!IsCreatureAliveAndHealable(creature))
					continue;

				if (Vector3.DistanceSquared(pos, creature.ComponentBody.Position) > radiusSq)
					continue;

				ComponentNewHerdBehavior herd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (herd == null || !m_componentHerd.IsSameHerdOrGuardian(creature))
					continue;

				// *** CORRECCIÓN PRINCIPAL: Solo curar criaturas contratadas ***
				if (!IsCreatureHired(creature))
					continue;

				bool isHurt = creature.ComponentHealth.Health < 0.2f;
				bool isDiseased = false;

				if (CureDiseases)
				{
					if (creature is ComponentPlayer)
					{
						if (gameInfo.WorldSettings.GameMode == GameMode.Creative ||
							!gameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
						{
							isDiseased = false;
						}
						else
						{
							isDiseased = IsCreatureDiseased(creature);
						}
					}
					else
					{
						isDiseased = IsCreatureDiseased(creature);
					}
				}

				if (isHurt || isDiseased)
				{
					list.Add(creature);
				}
			}
		}

		bool CureCreatureDiseases(ComponentCreature creature, ComponentCreature healer)
		{
			if (!CanHealCreature(creature))
				return false;

			if (creature == null)
				return false;

			if (!IsCreatureAliveAndHealable(creature))
				return false;

			if (creature is ComponentPlayer)
			{
				SubsystemGameInfo gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
				if (gameInfo.WorldSettings.GameMode == GameMode.Creative ||
					!gameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
				{
					return false;
				}
			}

			bool hadFlu = false;
			bool hadPoison = false;

			var fluInfected = creature.Entity.FindComponent<ComponentFluInfected>();
			if (fluInfected != null && fluInfected.IsInfected)
			{
				fluInfected.m_fluDuration = 0f;
				hadFlu = true;
			}

			var poisonInfected = creature.Entity.FindComponent<ComponentPoisonInfected>();
			if (poisonInfected != null && poisonInfected.IsInfected)
			{
				poisonInfected.m_InfectDuration = 0f;
				hadPoison = true;
			}

			var playerFlu = creature.Entity.FindComponent<ComponentFlu>();
			if (playerFlu != null && playerFlu.HasFlu)
			{
				playerFlu.m_fluDuration = 0f;
				hadFlu = true;
				var vitalStats = creature.Entity.FindComponent<ComponentVitalStats>();
				if (vitalStats != null)
					vitalStats.Temperature = 12f;
			}

			var playerSickness = creature.Entity.FindComponent<ComponentSickness>();
			if (playerSickness != null && playerSickness.IsSick)
			{
				playerSickness.m_sicknessDuration = 0f;
				hadPoison = true;
			}

			if (healer != null && creature is ComponentPlayer player)
			{
				if (hadFlu)
				{
					string msg = LanguageControl.Get("Healing", "1");
					if (!string.IsNullOrEmpty(msg))
					{
						msg = string.Format(msg, healer.DisplayName);
						player.ComponentGui.DisplaySmallMessage(msg, new Color(100, 150, 255), false, false);
						m_subsystemAudio.PlaySound("Audio/UI/success", 1f, 0f, 0f, 0f);
					}
				}
				if (hadPoison)
				{
					string msg = LanguageControl.Get("Healing", "2");
					if (!string.IsNullOrEmpty(msg))
					{
						msg = string.Format(msg, healer.DisplayName);
						player.ComponentGui.DisplaySmallMessage(msg, new Color(200, 100, 255), false, false);
						m_subsystemAudio.PlaySound("Audio/UI/success", 1f, 0f, 0f, 0f);
					}
				}
			}

			return hadFlu || hadPoison;
		}

		private bool CanHealCreature(ComponentCreature creature)
		{
			if (creature == null) return false;
			var infiniteChallenge = creature.Entity.FindComponent<ComponentInfiniteChallenge>();
			if (infiniteChallenge != null && !infiniteChallenge.HasBeenDefeated)
				return false;
			return true;
		}
	}
}
