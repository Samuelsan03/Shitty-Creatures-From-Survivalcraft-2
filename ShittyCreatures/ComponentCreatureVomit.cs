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
		public bool VomitFrozen { get; set; }
		public float VomitProbability { get; set; } = 0.1f;
		public float VomitCooldown { get; set; } = 5f;
		public Vector2 VomitDistanceRange { get; set; } = new Vector2(2f, 12f); // X = min, Y = max

		private double m_lastVomitTime;
		private double m_vomitStartTime;
		private PoisonVomitParticleSystem m_activePoisonVomit;
		private FireVomitParticleSystem m_activeFireVomit;
		private FrozenVomitParticleSystem m_activeFrozenVomit;
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
			VomitFrozen = valuesDictionary.GetValue<bool>("VomitFrozen", false);
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

		// Nueva comprobación para saber si el objetivo está demasiado cerca
		private bool IsTargetTooClose(ComponentCreature target)
		{
			if (target == null) return false;
			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);
			return distance < VomitDistanceRange.X;
		}

		public void Update(float dt)
		{
			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = GetCurrentChaseTarget();

			// ---- Vómito de veneno activo ----
			if (m_activePoisonVomit != null)
			{
				bool shouldStop = false;
				if (target == null)
					shouldStop = true;
				else if (!IsTargetInViewCone(target))
					shouldStop = true;
				else if (IsTargetTooClose(target))          // <<< NUEVO: cancela si se acerca demasiado
					shouldStop = true;
				else if (m_subsystemTime.GameTime - m_vomitStartTime > 3.5f)
					shouldStop = true;

				if (shouldStop || m_activePoisonVomit.IsStopped)
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

			// ---- Vómito de fuego activo ----
			if (m_activeFireVomit != null)
			{
				bool shouldStop = false;
				if (target == null)
					shouldStop = true;
				else if (!IsTargetInViewCone(target))
					shouldStop = true;
				else if (IsTargetTooClose(target))          // <<< NUEVO
					shouldStop = true;
				else if (m_subsystemTime.GameTime - m_vomitStartTime > 3.5f)
					shouldStop = true;

				if (shouldStop || m_activeFireVomit.IsStopped)
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

			// ---- Vómito congelado activo ----
			if (m_activeFrozenVomit != null)
			{
				bool shouldStop = false;
				if (target == null)
					shouldStop = true;
				else if (!IsTargetInViewCone(target))
					shouldStop = true;
				else if (IsTargetTooClose(target))          // <<< NUEVO
					shouldStop = true;
				else if (m_subsystemTime.GameTime - m_vomitStartTime > 3.5f)
					shouldStop = true;

				if (shouldStop || m_activeFrozenVomit.IsStopped)
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

			// ---- Iniciar nuevo vómito ----
			if (target == null)
				return;

			if (!IsTargetInViewCone(target))
				return;

			if (!IsTargetInDistanceRange(target))
				return;

			// Probabilidad por frame
			if (m_random.Float(0f, 1f) > VomitProbability * dt)
				return;

			double currentTime = m_subsystemTime.GameTime;
			if (currentTime - m_lastVomitTime < (double)VomitCooldown)
				return;

			// Elegir aleatoriamente entre todos los tipos habilitados
			System.Collections.Generic.List<int> availableTypes = new System.Collections.Generic.List<int>();
			if (VomitShit) availableTypes.Add(0);   // 0 = Veneno
			if (VomitFire) availableTypes.Add(1);   // 1 = Fuego
			if (VomitFrozen) availableTypes.Add(2); // 2 = Congelado

			if (availableTypes.Count == 0)
				return;

			int chosenType = availableTypes[m_random.Int(availableTypes.Count)];

			Vector3 mouthPos = GetVomitMouthPosition();
			Vector3 direction = Vector3.Normalize(target.ComponentCreatureModel.EyePosition - mouthPos);

			if (chosenType == 0) // Veneno
			{
				m_activePoisonVomit = new PoisonVomitParticleSystem(
					m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials,
					m_subsystemTime, m_subsystemParticles, m_componentCreature);
				m_activePoisonVomit.Position = mouthPos;
				m_activePoisonVomit.Direction = direction;
				m_activePoisonVomit.PoisonIntensity = 180f;
				m_subsystemParticles.AddParticleSystem(m_activePoisonVomit, false);
			}
			else if (chosenType == 1) // Fuego
			{
				m_activeFireVomit = new FireVomitParticleSystem(
					m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials,
					m_subsystemTime, m_componentCreature);
				m_activeFireVomit.Position = mouthPos;
				m_activeFireVomit.Direction = direction;
				m_activeFireVomit.FireDuration = 30f;
				m_subsystemParticles.AddParticleSystem(m_activeFireVomit, false);
			}
			else if (chosenType == 2) // Congelado
			{
				m_activeFrozenVomit = new FrozenVomitParticleSystem(
					m_subsystemTerrain, m_subsystemBodies, m_subsystemSoundMaterials,
					m_subsystemTime, m_componentCreature);
				m_activeFrozenVomit.Position = mouthPos;
				m_activeFrozenVomit.Direction = direction;
				m_subsystemParticles.AddParticleSystem(m_activeFrozenVomit, false);
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
		}
	}
}
