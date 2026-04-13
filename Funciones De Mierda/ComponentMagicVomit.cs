using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentMagicVomit : Component, IUpdateable
	{
		// Campos configurables
		public float VomitProbability = 1f;
		public float VomitCooldown = 15f;
		public bool VomitShit = false;   // ← CAMBIADO
		public bool VomitFire = false;

		private SubsystemTime m_subsystemTime;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemGameInfo m_subsystemGameInfo;
		private ComponentCreature m_componentCreature;
		private ComponentLocomotion m_componentLocomotion;
		private Random m_random = new Random();

		private ComponentChaseBehavior m_oldChaseBehavior;
		private ComponentNewChaseBehavior m_newChaseBehavior;
		private ComponentZombieChaseBehavior m_zombieChaseBehavior;

		private ParticleSystemBase m_vomitParticleSystem;
		private double m_lastVomitTime = -9999;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public bool IsVomiting => m_vomitParticleSystem != null;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentLocomotion = Entity.FindComponent<ComponentLocomotion>(true);

			m_oldChaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
			m_newChaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_zombieChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>();

			VomitProbability = valuesDictionary.GetValue<float>("VomitProbability", 1f);
			VomitShit = valuesDictionary.GetValue<bool>("VomitShit", false); // ← CAMBIADO
			VomitFire = valuesDictionary.GetValue<bool>("VomitFire", false);
		}

		public void Update(float dt)
		{
			if (!m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
				return;
			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature currentTarget = GetCurrentChaseTarget();

			if (m_vomitParticleSystem != null)
			{
				if (currentTarget != null)
					UpdateVomitParticle(currentTarget);

				bool stopped = false;
				if (m_vomitParticleSystem is MagicVomitParticleSystem poison)
					stopped = poison.IsStopped;
				else if (m_vomitParticleSystem is MagicFireVomitParticleSystem fire)
					stopped = fire.IsStopped;

				if (stopped)
					m_vomitParticleSystem = null;

				return;
			}

			if (currentTarget != null)
			{
				float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, currentTarget.ComponentBody.Position);
				if (distance <= 12f)
				{
					if (m_subsystemTime.GameTime - m_lastVomitTime >= VomitCooldown)
					{
						if (!HasLineOfSight(currentTarget))
							return;

						if (m_random.Float(0f, 1f) < VomitProbability * dt)
						{
							StartVomit(currentTarget);
						}
					}
				}
			}
		}

		private bool HasLineOfSight(ComponentCreature target)
		{
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
			Vector3 direction = targetPos - eyePos;
			float distance = direction.Length();
			if (distance < 0.1f) return true;
			direction /= distance;

			var raycastResult = m_subsystemTerrain.Raycast(eyePos, eyePos + direction * distance, true, true,
				(v, d) => d < distance && BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable_(v));
			if (raycastResult != null) return false;

			var bodyResult = m_subsystemBodies.Raycast(eyePos, eyePos + direction * distance, 0.2f,
				(body, d) => body.Entity != m_componentCreature.Entity && body.Entity != target.Entity && d < distance);
			return bodyResult == null;
		}

		private ComponentCreature GetCurrentChaseTarget()
		{
			if (m_newChaseBehavior != null && m_newChaseBehavior.IsActive && m_newChaseBehavior.Target != null)
				return m_newChaseBehavior.Target;
			if (m_zombieChaseBehavior != null && m_zombieChaseBehavior.IsActive && m_zombieChaseBehavior.Target != null)
				return m_zombieChaseBehavior.Target;
			if (m_oldChaseBehavior != null && m_oldChaseBehavior.IsActive && m_oldChaseBehavior.Target != null)
				return m_oldChaseBehavior.Target;
			return null;
		}

		private void StartVomit(ComponentCreature target)
		{
			m_lastVomitTime = m_subsystemTime.GameTime;

			ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
			if (model == null) return;

			Vector3 eyePos = model.EyePosition;
			Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetPos - eyePos);
			Vector3 up = model.EyeRotation.GetUpVector();

			bool useShit = VomitShit;   // ← CAMBIADO
			bool useFire = VomitFire;

			if (useShit && useFire)
			{
				useShit = m_random.Bool();
				useFire = !useShit;
			}

			if (useShit)
			{
				var system = new MagicVomitParticleSystem(m_subsystemTerrain, m_subsystemBodies, m_componentCreature);
				system.Position = eyePos - 0.08f * up + 0.3f * direction;
				system.Direction = Vector3.Normalize(direction + 0.2f * up);
				m_vomitParticleSystem = system;
			}
			else if (useFire)
			{
				var fireSystem = new MagicFireVomitParticleSystem(m_subsystemTerrain, m_subsystemBodies, m_componentCreature);
				fireSystem.Position = eyePos - 0.08f * up + 0.3f * direction;
				fireSystem.Direction = Vector3.Normalize(direction + 0.2f * up);
				m_vomitParticleSystem = fireSystem;
			}
			else
			{
				return;
			}

			m_subsystemParticles.AddParticleSystem(m_vomitParticleSystem, false);

			Project.FindSubsystem<SubsystemNoise>(true)?.MakeNoise(m_componentCreature.ComponentBody.Position, 0.25f, 10f);
		}

		private void UpdateVomitParticle(ComponentCreature target)
		{
			ComponentCreatureModel model = m_componentCreature.ComponentCreatureModel;
			if (model == null || m_vomitParticleSystem == null) return;

			Vector3 eyePos = model.EyePosition;
			Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetPos - eyePos);
			Vector3 up = model.EyeRotation.GetUpVector();

			if (m_componentLocomotion != null)
			{
				float pitch = MathUtils.Asin(direction.Y) - m_componentLocomotion.LookAngles.Y;
				pitch = Math.Clamp(pitch, -2f, 2f);
				m_componentLocomotion.LookOrder = new Vector2(m_componentLocomotion.LookOrder.X, pitch);
			}

			if (m_vomitParticleSystem is MagicVomitParticleSystem poison)
			{
				poison.Position = eyePos - 0.08f * up + 0.3f * direction;
				poison.Direction = direction;
			}
			else if (m_vomitParticleSystem is MagicFireVomitParticleSystem fire)
			{
				fire.Position = eyePos - 0.08f * up + 0.3f * direction;
				fire.Direction = direction;
			}
		}
	}
}
