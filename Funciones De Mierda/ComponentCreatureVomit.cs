using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureVomit : Component, IUpdateable
	{
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;
		private SubsystemTime m_subsystemTime;
		private SubsystemParticles m_subsystemParticles;
		private ComponentCreature m_componentCreature;
		private ComponentBody m_componentBody;
		private ComponentCreatureModel m_componentCreatureModel;

		private ComponentNewChaseBehavior m_newChaseBehavior;
		private ComponentChaseBehavior m_oldChaseBehavior;
		private ComponentZombieChaseBehavior m_zombieChaseBehavior;

		public bool VomitShit { get; set; }
		public bool VomitFire { get; set; }
		public float VomitProbability { get; set; } = 0.1f;
		public float VomitCooldown { get; set; } = 5f;

		private double m_lastVomitTime;
		private PoisonVomitParticleSystem m_activePoisonVomit;
		private FireVomitParticleSystem m_activeFireVomit;
		private Random m_random = new Random();

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);

			m_newChaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_oldChaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
			m_zombieChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>();

			VomitShit = valuesDictionary.GetValue<bool>("VomitShit", false);
			VomitFire = valuesDictionary.GetValue<bool>("VomitFire", false);
			VomitProbability = valuesDictionary.GetValue<float>("VomitProbability", 0.1f);
			VomitCooldown = valuesDictionary.GetValue<float>("VomitCooldown", 5f);
		}
		public void Update(float dt)
		{
			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = GetCurrentChaseTarget();

			if (m_activePoisonVomit != null)
			{
				if (m_activePoisonVomit.IsStopped)
				{
					m_activePoisonVomit = null;
				}
				else if (target != null)
				{
					m_activePoisonVomit.Position = m_componentCreatureModel.EyePosition
						- m_componentCreatureModel.EyeRotation.GetUpVector() * 0.08f
						+ m_componentCreatureModel.EyeRotation.GetForwardVector() * 0.3f;
					m_activePoisonVomit.Direction = Vector3.Normalize(target.ComponentCreatureModel.EyePosition - m_activePoisonVomit.Position);
				}
				return;
			}

			if (m_activeFireVomit != null)
			{
				if (m_activeFireVomit.IsStopped)
				{
					m_activeFireVomit = null;
				}
				else if (target != null)
				{
					m_activeFireVomit.Position = m_componentCreatureModel.EyePosition
						- m_componentCreatureModel.EyeRotation.GetUpVector() * 0.08f
						+ m_componentCreatureModel.EyeRotation.GetForwardVector() * 0.3f;
					m_activeFireVomit.Direction = Vector3.Normalize(target.ComponentCreatureModel.EyePosition - m_activeFireVomit.Position);
				}
				return;
			}

			if (target == null)
				return;

			float distance = Vector3.Distance(m_componentBody.Position, target.ComponentBody.Position);
			if (distance < 3f)
				return;

			if (m_random.Float(0f, 1f) > VomitProbability * dt)
				return;

			double currentTime = m_subsystemTime.GameTime;
			if (currentTime - m_lastVomitTime < VomitCooldown)
				return;

			bool useFire = VomitFire;
			bool usePoison = VomitShit;

			if (!useFire && !usePoison)
				return;

			if (useFire && usePoison)
			{
				useFire = m_random.Bool();
				usePoison = !useFire;
			}

			Vector3 mouthPos = m_componentCreatureModel.EyePosition
				- m_componentCreatureModel.EyeRotation.GetUpVector() * 0.08f
				+ m_componentCreatureModel.EyeRotation.GetForwardVector() * 0.3f;
			Vector3 direction = Vector3.Normalize(target.ComponentCreatureModel.EyePosition - mouthPos);

			if (usePoison)
			{
				m_activePoisonVomit = new PoisonVomitParticleSystem(
					m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials,
					m_subsystemTime, m_subsystemParticles, m_componentCreature);
				m_activePoisonVomit.Position = mouthPos;
				m_activePoisonVomit.Direction = direction;
				m_activePoisonVomit.PoisonIntensity = 180f;
				m_subsystemParticles.AddParticleSystem(m_activePoisonVomit, false);
			}
			else if (useFire)
			{
				m_activeFireVomit = new FireVomitParticleSystem(
					m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials,
					m_subsystemTime, m_componentCreature);
				m_activeFireVomit.Position = mouthPos;
				m_activeFireVomit.Direction = direction;
				m_activeFireVomit.FireDuration = 30f;
				m_subsystemParticles.AddParticleSystem(m_activeFireVomit, false);
			}

			m_lastVomitTime = currentTime;
			m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
		}

		private ComponentCreature GetCurrentChaseTarget()
		{
			if (m_newChaseBehavior != null && m_newChaseBehavior.Target != null)
				return m_newChaseBehavior.Target;
			if (m_zombieChaseBehavior != null && m_zombieChaseBehavior.Target != null)
				return m_zombieChaseBehavior.Target;
			if (m_oldChaseBehavior != null && m_oldChaseBehavior.Target != null)
				return m_oldChaseBehavior.Target;
			return null;
		}

		public override void OnEntityRemoved()
		{
			if (m_activePoisonVomit != null)
				m_activePoisonVomit.IsStopped = true;
			if (m_activeFireVomit != null)
				m_activeFireVomit.IsStopped = true;
		}
	}
}
