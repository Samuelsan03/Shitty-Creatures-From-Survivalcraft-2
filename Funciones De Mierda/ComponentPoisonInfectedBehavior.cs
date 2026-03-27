using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentPoisonInfectBehavior : ComponentBehavior, IUpdateable
	{
		public override float ImportanceLevel => m_importanceLevel;

		public void Update(float dt)
		{
			m_stateMachine.Update();
		}

		// FIX: Método que comprueba si el ataque realmente impactó usando raycast
		private bool IsAttackHitValid(ComponentCreature target)
		{
			if (m_componentCreature == null || target == null)
				return false;

			ComponentBody attackerBody = m_componentCreature.ComponentBody;
			ComponentBody targetBody = target.ComponentBody;
			if (attackerBody == null || targetBody == null)
				return false;

			// Obtener la posición de los ojos del atacante (punto de origen del raycast)
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			// Punto central del objetivo (podría ser el centro del cuerpo o sus ojos)
			Vector3 targetPos = targetBody.Position + new Vector3(0f, targetBody.BoxSize.Y * 0.5f, 0f);

			Vector3 direction = targetPos - eyePos;
			float distance = direction.Length();
			if (distance > 3f) // Alcance máximo del ataque
				return false;

			direction /= distance; // Normalizar

			// Realizar el raycast en el subsistema de cuerpos
			var ray = new Ray3(eyePos, direction);
			var result = m_subsystemBodies.Raycast(eyePos, eyePos + direction * distance, 0.35f,
				(ComponentBody body, float dist) => body == targetBody);

			return result.HasValue; // Si el raycast impactó en el objetivo, el ataque fue válido
		}

		public bool StartInfect(ComponentCreature target)
		{
			if (target == null)
				return false;

			// FIX: Verificar si el ataque realmente impactó mediante raycast
			if (!IsAttackHitValid(target))
				return false;

			ComponentPoisonInfected componentPoisonInfected = target.Entity.FindComponent<ComponentPoisonInfected>();
			ComponentPlayer componentPlayer = target as ComponentPlayer;
			if (componentPlayer != null)
			{
				if (componentPlayer.ComponentSickness.IsSick)
					return true;
				componentPlayer.ComponentSickness.StartSickness();
				if (componentPoisonInfected != null)
				{
					componentPlayer.ComponentSickness.m_sicknessDuration = m_poisonIntensity - componentPoisonInfected.PoisonResistance;
				}
				return componentPlayer.ComponentSickness.IsSick;
			}
			else if (componentPoisonInfected != null)
			{
				if (componentPoisonInfected.IsInfected)
					return true;
				componentPoisonInfected.StartInfect(m_poisonIntensity);
				return componentPoisonInfected.IsInfected;
			}
			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true); // FIX: Obtener referencia al subsistema de cuerpos
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_newChaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_chaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
			m_poisonIntensity = valuesDictionary.GetValue<float>("PoisonIntensity");
			m_infectProbability = valuesDictionary.GetValue<float>("InfectProbability", 1f);

			m_stateMachine.AddState("Inactive", delegate
			{
				m_importanceLevel = 0f;
			}, delegate
			{
				ComponentNewChaseBehavior newChaseBehavior = m_newChaseBehavior;
				ComponentCreature target = null;
				if (newChaseBehavior != null)
					target = newChaseBehavior.Target;
				if (target == null)
				{
					ComponentChaseBehavior chaseBehavior = m_chaseBehavior;
					if (chaseBehavior != null)
						target = chaseBehavior.m_target;
				}
				m_target = target;
				if (m_target != null && m_componentCreature.ComponentCreatureModel.IsAttackHitMoment && (double)m_random.Float(0f, 1f) < (double)m_infectProbability)
				{
					m_importanceLevel = 201f;
				}
				if (!IsActive)
					return;
				m_stateMachine.TransitionTo("PoisonInfect");
			}, null);

			m_stateMachine.AddState("PoisonInfect", delegate
			{
				if (m_target == null)
					return;
				m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(m_target.ComponentCreatureModel.EyePosition);
				m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
			}, delegate
			{
				if (StartInfect(m_target))
				{
					ComponentRunAwayBehavior componentRunAwayBehavior = m_componentCreature.Entity.FindComponent<ComponentRunAwayBehavior>();
					if (componentRunAwayBehavior != null)
						componentRunAwayBehavior.RunAwayFrom(m_target.ComponentBody);
					ComponentNewRunAwayBehavior componentNewRunAwayBehavior = m_componentCreature.Entity.FindComponent<ComponentNewRunAwayBehavior>();
					if (componentNewRunAwayBehavior != null)
						componentNewRunAwayBehavior.RunAwayFrom(m_target.ComponentBody);
					m_stateMachine.TransitionTo("Inactive");
				}
				if (IsActive && m_target != null)
					return;
				m_stateMachine.TransitionTo("Inactive");
			}, null);

			m_stateMachine.TransitionTo("Inactive");
		}

		private SubsystemTime m_subsystemTime;
		private SubsystemBodies m_subsystemBodies; // FIX: Nuevo campo para el raycast
		private ComponentCreature m_componentCreature;
		private ComponentNewChaseBehavior m_newChaseBehavior;
		private ComponentChaseBehavior m_chaseBehavior;
		private readonly StateMachine m_stateMachine = new StateMachine();
		private readonly Game.Random m_random = new Game.Random();
		private float m_importanceLevel;
		public float m_poisonIntensity;
		private ComponentCreature m_target;
		private float m_infectProbability = 1f;
	}
}
