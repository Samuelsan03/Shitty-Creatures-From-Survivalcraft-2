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

		// Calcula la posición de la boca según el tipo de modelo
		private Vector3 GetVomitMouthPosition()
		{
			// Posición base (humanoides)
			Vector3 basePos = m_componentCreatureModel.EyePosition
				- m_componentCreatureModel.EyeRotation.GetUpVector() * 0.08f
				+ m_componentCreatureModel.EyeRotation.GetForwardVector() * 0.3f;

			Type modelType = m_componentCreatureModel.GetType();

			if (modelType == typeof(ComponentBirdModel))
			{
				// Aves: pico más adelante
				return m_componentCreatureModel.EyePosition
					+ m_componentCreatureModel.EyeRotation.GetForwardVector() * 0.8f;
			}
			if (modelType == typeof(ComponentFishModel))
			{
				// Peces: boca en el extremo frontal
				return m_componentCreatureModel.EyePosition
					+ m_componentCreatureModel.EyeRotation.GetForwardVector() * 0.7f;
			}
			if (modelType == typeof(ComponentFourLeggedModel))
			{
				// Cuadrúpedos: hocico hacia adelante y un poco abajo
				return m_componentCreatureModel.EyePosition
					+ m_componentCreatureModel.EyeRotation.GetForwardVector() * 0.5f
					- m_componentCreatureModel.EyeRotation.GetUpVector() * 0.1f;
			}
			// Humanoides y otros
			return basePos;
		}

		// Verifica si el objetivo está dentro del cono de visión
		private bool IsTargetInViewCone(ComponentCreature target)
		{
			if (target == null) return false;

			Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
			Vector3 toTarget = target.ComponentBody.Position - m_componentCreature.ComponentBody.Position;

			// Ignorar diferencia de altura para el cono horizontal
			forward.Y = 0f;
			toTarget.Y = 0f;

			float dot = Vector3.Dot(forward, toTarget);
			if (dot <= 0f) return false;

			float forwardLength = forward.Length();
			float toTargetLength = toTarget.Length();
			if (toTargetLength < 0.001f) return true;

			float cosAngle = dot / (forwardLength * toTargetLength);
			float halfAngleRad = MathUtils.DegToRad(60f); // Cono de 120° (60° a cada lado)
			float cosHalfAngle = MathF.Cos(halfAngleRad);

			return cosAngle >= cosHalfAngle;
		}

		public void Update(float dt)
		{
			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = GetCurrentChaseTarget();

			// --- Vómito de veneno activo ---
			if (m_activePoisonVomit != null)
			{
				if (m_activePoisonVomit.IsStopped)
				{
					m_activePoisonVomit = null;
				}
				else if (target != null)
				{
					m_activePoisonVomit.Position = GetVomitMouthPosition();
					m_activePoisonVomit.Direction = Vector3.Normalize(target.ComponentCreatureModel.EyePosition - m_activePoisonVomit.Position);
				}
				return;
			}

			// --- Vómito de fuego activo ---
			if (m_activeFireVomit != null)
			{
				if (m_activeFireVomit.IsStopped)
				{
					m_activeFireVomit = null;
				}
				else if (target != null)
				{
					m_activeFireVomit.Position = GetVomitMouthPosition();
					m_activeFireVomit.Direction = Vector3.Normalize(target.ComponentCreatureModel.EyePosition - m_activeFireVomit.Position);
				}
				return;
			}

			if (target == null)
				return;

			// Verificar que el objetivo esté dentro del cono de visión
			if (!IsTargetInViewCone(target))
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

			Vector3 mouthPos = GetVomitMouthPosition();
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
