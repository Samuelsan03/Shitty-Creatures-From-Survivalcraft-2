using System;
using System.Collections.Generic;
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

		public bool VomitPoison { get; set; }
		public bool VomitFire { get; set; }
		public bool VomitFrozen { get; set; }
		public bool VomitBlood { get; set; }

		public float VomitProbability { get; set; } = 0.1f;
		public float VomitCooldown { get; set; } = 5f;
		public Vector2 VomitDistanceRange { get; set; } = new Vector2(2f, 12f);

		private PoisonVomitParticleSystem m_activePoisonVomit;
		private FireVomitParticleSystem m_activeFireVomit;
		private FrozenVomitParticleSystem m_activeFrozenVomit;
		private BloodVomitParticleSystem m_activeBloodVomit;
		private VomitType m_activeType;
		private double m_vomitStartTime;
		private double m_lastVomitTime;
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

			VomitPoison = valuesDictionary.GetValue<bool>("VomitPoison", false);
			VomitFire = valuesDictionary.GetValue<bool>("VomitFire", false);
			VomitFrozen = valuesDictionary.GetValue<bool>("VomitFrozen", false);
			VomitBlood = valuesDictionary.GetValue<bool>("VomitBlood", false);
			VomitProbability = valuesDictionary.GetValue<float>("VomitProbability", 0.1f);
			VomitCooldown = valuesDictionary.GetValue<float>("VomitCooldown", 5f);
			VomitDistanceRange = valuesDictionary.GetValue<Vector2>("VomitDistanceRange", new Vector2(2f, 12f));
		}

		private Vector3 GetVomitMouthPosition()
		{
			Vector3 basePos = m_componentCreatureModel.EyePosition
				- m_componentCreatureModel.EyeRotation.GetUpVector() * 0.08f
				+ m_componentCreatureModel.EyeRotation.GetForwardVector() * 0.3f;

			Type modelType = m_componentCreatureModel.GetType();
			if (modelType == typeof(ComponentBirdModel))
				return m_componentCreatureModel.EyePosition + m_componentCreatureModel.EyeRotation.GetForwardVector() * 0.8f;
			if (modelType == typeof(ComponentFishModel))
				return m_componentCreatureModel.EyePosition + m_componentCreatureModel.EyeRotation.GetForwardVector() * 0.7f;
			if (modelType == typeof(ComponentFourLeggedModel))
				return m_componentCreatureModel.EyePosition + m_componentCreatureModel.EyeRotation.GetForwardVector() * 0.5f - m_componentCreatureModel.EyeRotation.GetUpVector() * 0.1f;

			return basePos;
		}

		private bool IsTargetInViewCone(ComponentCreature target)
		{
			if (target == null) return false;

			Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
			Vector3 toTarget = target.ComponentBody.Position - m_componentCreature.ComponentBody.Position;

			forward.Y = 0f;
			toTarget.Y = 0f;

			float dot = Vector3.Dot(forward, toTarget);
			if (dot <= 0f) return false;

			float forwardLength = forward.Length();
			float toTargetLength = toTarget.Length();
			if (toTargetLength < 0.001f) return true;

			float cosAngle = dot / (forwardLength * toTargetLength);
			float halfAngleRad = MathUtils.DegToRad(60f);
			float cosHalfAngle = MathF.Cos(halfAngleRad);

			return cosAngle >= cosHalfAngle;
		}

		private bool IsTargetInDistanceRange(ComponentCreature target)
		{
			if (target == null) return false;
			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);
			return distance >= VomitDistanceRange.X && distance <= VomitDistanceRange.Y;
		}

		private bool IsTargetTooClose(ComponentCreature target)
		{
			if (target == null) return false;
			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);
			return distance < VomitDistanceRange.X;
		}

		public void Update(float dt)
		{
			if (AchievementsManager.IsCelebrationActive) return;

			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = GetCurrentChaseTarget();

			// Actualizar vómito activo
			if (m_activePoisonVomit != null)
			{
				bool shouldStop = false;
				if (target == null)
					shouldStop = true;
				else if (!IsTargetInViewCone(target))
					shouldStop = true;
				else if (IsTargetTooClose(target))
					shouldStop = true;
				else if (m_subsystemTime.GameTime - m_vomitStartTime > 3.5f)
					shouldStop = true;
				else if (m_activePoisonVomit.IsStopped)
					shouldStop = true;

				if (shouldStop)
				{
					m_activePoisonVomit.IsStopped = true;
					m_activePoisonVomit = null;
				}
				else
				{
					m_activePoisonVomit.Position = GetVomitMouthPosition();
					m_activePoisonVomit.Direction = Vector3.Normalize(target.ComponentCreatureModel.EyePosition - m_activePoisonVomit.Position);
				}
				return;
			}

			if (m_activeFireVomit != null)
			{
				bool shouldStop = false;
				if (target == null)
					shouldStop = true;
				else if (!IsTargetInViewCone(target))
					shouldStop = true;
				else if (IsTargetTooClose(target))
					shouldStop = true;
				else if (m_subsystemTime.GameTime - m_vomitStartTime > 3.5f)
					shouldStop = true;
				else if (m_activeFireVomit.IsStopped)
					shouldStop = true;

				if (shouldStop)
				{
					m_activeFireVomit.IsStopped = true;
					m_activeFireVomit = null;
				}
				else
				{
					m_activeFireVomit.Position = GetVomitMouthPosition();
					m_activeFireVomit.Direction = Vector3.Normalize(target.ComponentCreatureModel.EyePosition - m_activeFireVomit.Position);
				}
				return;
			}

			if (m_activeFrozenVomit != null)
			{
				bool shouldStop = false;
				if (target == null)
					shouldStop = true;
				else if (!IsTargetInViewCone(target))
					shouldStop = true;
				else if (IsTargetTooClose(target))
					shouldStop = true;
				else if (m_subsystemTime.GameTime - m_vomitStartTime > 3.5f)
					shouldStop = true;
				else if (m_activeFrozenVomit.IsStopped)
					shouldStop = true;

				if (shouldStop)
				{
					m_activeFrozenVomit.IsStopped = true;
					m_activeFrozenVomit = null;
				}
				else
				{
					m_activeFrozenVomit.Position = GetVomitMouthPosition();
					m_activeFrozenVomit.Direction = Vector3.Normalize(target.ComponentCreatureModel.EyePosition - m_activeFrozenVomit.Position);
				}
				return;
			}

			if (m_activeBloodVomit != null)
			{
				bool shouldStop = false;
				if (target == null)
					shouldStop = true;
				else if (!IsTargetInViewCone(target))
					shouldStop = true;
				else if (IsTargetTooClose(target))
					shouldStop = true;
				else if (m_subsystemTime.GameTime - m_vomitStartTime > 3.5f)
					shouldStop = true;
				else if (m_activeBloodVomit.IsStopped)
					shouldStop = true;

				if (shouldStop)
				{
					m_activeBloodVomit.IsStopped = true;
					m_activeBloodVomit = null;
				}
				else
				{
					m_activeBloodVomit.Position = GetVomitMouthPosition();
					m_activeBloodVomit.Direction = Vector3.Normalize(target.ComponentCreatureModel.EyePosition - m_activeBloodVomit.Position);
				}
				return;
			}

			if (target == null)
				return;

			if (!IsTargetInViewCone(target))
				return;

			if (!IsTargetInDistanceRange(target))
				return;

			if (m_random.Float(0f, 1f) > VomitProbability * dt)
				return;

			double currentTime = m_subsystemTime.GameTime;
			if (currentTime - m_lastVomitTime < (double)VomitCooldown)
				return;

			List<VomitType> availableTypes = new List<VomitType>();
			if (VomitPoison) availableTypes.Add(VomitType.Poison);
			if (VomitFire) availableTypes.Add(VomitType.Fire);
			if (VomitFrozen) availableTypes.Add(VomitType.Frozen);
			if (VomitBlood) availableTypes.Add(VomitType.Blood);

			if (availableTypes.Count == 0)
				return;

			VomitType chosenType = availableTypes[m_random.Int(availableTypes.Count)];

			Vector3 mouthPos = GetVomitMouthPosition();
			Vector3 direction = Vector3.Normalize(target.ComponentCreatureModel.EyePosition - mouthPos);

			switch (chosenType)
			{
				case VomitType.Poison:
					var poison = new PoisonVomitParticleSystem(
						m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials,
						m_subsystemTime, m_subsystemParticles, m_componentCreature);
					poison.Position = mouthPos;
					poison.Direction = direction;
					poison.PoisonIntensity = 180f;
					m_activePoisonVomit = poison;
					m_subsystemParticles.AddParticleSystem(poison, false);
					break;

				case VomitType.Fire:
					var fire = new FireVomitParticleSystem(
						m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials,
						m_subsystemTime, m_componentCreature);
					fire.Position = mouthPos;
					fire.Direction = direction;
					fire.FireDuration = 30f;
					fire.ImpactDamage = 0.01f;
					m_activeFireVomit = fire;
					m_subsystemParticles.AddParticleSystem(fire, false);
					break;

				case VomitType.Frozen:
					var frozen = new FrozenVomitParticleSystem(
						m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials,
						m_subsystemTime, m_componentCreature);
					frozen.Position = mouthPos;
					frozen.Direction = direction;
					m_activeFrozenVomit = frozen;
					m_subsystemParticles.AddParticleSystem(frozen, false);
					break;

				case VomitType.Blood:
					var blood = new BloodVomitParticleSystem(
						m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials,
						m_subsystemTime, m_subsystemParticles, m_componentCreature);
					blood.Position = mouthPos;
					blood.Direction = direction;
					blood.BleedingIntensity = 180f;
					m_activeBloodVomit = blood;
					m_subsystemParticles.AddParticleSystem(blood, false);
					break;
			}

			m_lastVomitTime = currentTime;
			m_vomitStartTime = currentTime;
			m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
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
			if (m_activeFrozenVomit != null)
				m_activeFrozenVomit.IsStopped = true;
			if (m_activeBloodVomit != null)
				m_activeBloodVomit.IsStopped = true;
		}

		public enum VomitType
		{
			Poison,
			Fire,
			Frozen,
			Blood
		}
	}
}
