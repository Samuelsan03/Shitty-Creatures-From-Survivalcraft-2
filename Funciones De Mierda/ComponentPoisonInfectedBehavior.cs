using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentPoisonInfectedBehavior : ComponentBehavior, IUpdateable
	{
		public override float ImportanceLevel => this.m_importanceLevel;

		public float PoisonIntensity
		{
			get => this.m_poisonIntensity;
			set => this.m_poisonIntensity = MathUtils.Max(value, 0f);
		}

		public float InfectionProbability
		{
			get => this.m_infectionProbability;
			set => this.m_infectionProbability = MathUtils.Clamp(value, 0f, 1f);
		}

		public float InfectionCooldown
		{
			get => this.m_infectionCooldown;
			set => this.m_infectionCooldown = MathUtils.Max(value, 0f);
		}

		public float AttackRange
		{
			get => this.m_attackRange;
			set => this.m_attackRange = MathUtils.Max(value, 0f);
		}

		public void Update(float dt)
		{
			this.m_stateMachine.Update();
		}

		public bool StartInfect(ComponentCreature target)
		{
			if (target == null) return false;

			// === NUEVO: VERIFICAR SI EL OBJETIVO ES UN JUGADOR EN MODO CREATIVO ===
			ComponentPlayer targetPlayer = target as ComponentPlayer;
			if (targetPlayer != null)
			{
				// Verificar si el mundo permite mecánicas de supervivencia
				SubsystemGameInfo subsystemGameInfo = target.Project.FindSubsystem<SubsystemGameInfo>();
				if (subsystemGameInfo != null)
				{
					// Si está en modo creativo O las mecánicas de supervivencia están desactivadas, NO infectar
					if (subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
						!subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
					{
						return false; // Jugador en modo creativo, NO infectar
					}
				}

				// También verificar mediante el componente de inmunidad si existe
				ComponentPlayerPoisonImmunity immunity = target.Entity.FindComponent<ComponentPlayerPoisonImmunity>();
				if (immunity != null && !immunity.CanReceivePoison(this.m_poisonIntensity))
				{
					return false; // El componente de inmunidad bloquea la infección
				}
			}
			// === FIN DE LA NUEVA VERIFICACIÓN ===

			// Verificar cooldown
			if (this.m_subsystemTime.GameTime - this.m_lastInfectionTime < (double)this.m_infectionCooldown)
				return false;

			// Verificar probabilidad (solo si > 0)
			if (this.m_infectionProbability > 0f && this.m_random.Float(0f, 1f) > this.m_infectionProbability)
				return false;

			ComponentPoisonInfected componentPoisonInfected = target.Entity.FindComponent<ComponentPoisonInfected>();
			ComponentPlayer componentPlayer = target as ComponentPlayer;

			if (componentPlayer != null)
			{
				if (componentPlayer.ComponentSickness != null && componentPlayer.ComponentSickness.IsSick)
					return true;

				if (componentPlayer.ComponentSickness != null)
				{
					componentPlayer.ComponentSickness.StartSickness();
					if (componentPoisonInfected != null)
					{
						componentPlayer.ComponentSickness.m_sicknessDuration = MathUtils.Max(
							componentPlayer.ComponentSickness.m_sicknessDuration,
							this.m_poisonIntensity - componentPoisonInfected.PoisonResistance
						);
					}
					else
					{
						componentPlayer.ComponentSickness.m_sicknessDuration = MathUtils.Max(
							componentPlayer.ComponentSickness.m_sicknessDuration,
							this.m_poisonIntensity
						);
					}
					this.m_lastInfectionTime = this.m_subsystemTime.GameTime;

					// Sonido de infección
					if (this.m_componentCreature != null && this.m_componentCreature.ComponentCreatureSounds != null)
					{
						this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
					}

					return componentPlayer.ComponentSickness.IsSick;
				}
			}
			else if (componentPoisonInfected != null)
			{
				if (componentPoisonInfected.IsInfected)
					return true;

				componentPoisonInfected.StartInfect(this.m_poisonIntensity);
				this.m_lastInfectionTime = this.m_subsystemTime.GameTime;
				return componentPoisonInfected.IsInfected;
			}

			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_chaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>();

			// Cargar parámetros CON VALORES POR DEFECTO
			this.PoisonIntensity = valuesDictionary.GetValue<float>("PoisonIntensity", 0f);
			this.InfectionProbability = valuesDictionary.GetValue<float>("InfectionProbability", 0f);
			this.InfectionCooldown = valuesDictionary.GetValue<float>("InfectionCooldown", 0f);
			this.AttackRange = valuesDictionary.GetValue<float>("AttackRange", 0f);

			// Estado Inactivo
			this.m_stateMachine.AddState("Inactive", delegate
			{
				this.m_importanceLevel = 0f;
			}, delegate
			{
				// Solo activar si PoisonIntensity > 0
				if (this.m_poisonIntensity <= 0f)
				{
					this.m_importanceLevel = 0f;
					return;
				}

				ComponentCreature target = null;
				if (this.m_chaseBehavior != null)
					target = this.m_chaseBehavior.Target;

				this.m_target = target;

				// Verificar condiciones para activar la infección
				if (this.m_target != null &&
					this.m_componentCreature != null &&
					this.m_componentCreature.ComponentHealth != null &&
					this.m_componentCreature.ComponentBody != null &&
					this.m_target.ComponentBody != null)
				{
					// Verificar rango de ataque
					float distance = Vector3.Distance(
						this.m_componentCreature.ComponentBody.Position,
						this.m_target.ComponentBody.Position
					);

					bool inRange = (this.m_attackRange <= 0f) || (distance <= this.m_attackRange);
					bool isAttacking = this.m_componentCreature.ComponentCreatureModel != null &&
									   this.m_componentCreature.ComponentCreatureModel.IsAttackHitMoment;
					bool isChasing = this.m_chaseBehavior != null && this.m_chaseBehavior.IsActive;

					if (inRange && (isAttacking || isChasing))
					{
						this.m_importanceLevel = 201f;
					}
				}

				if (!this.IsActive) return;
				this.m_stateMachine.TransitionTo("PoisonInfect");
			}, null);

			// Estado de Infección
			this.m_stateMachine.AddState("PoisonInfect", delegate
			{
				if (this.m_target == null || this.m_componentCreature == null)
					return;

				// Solo mirar al objetivo si tiene modelo
				if (this.m_componentCreature.ComponentCreatureModel != null &&
					this.m_target.ComponentCreatureModel != null)
				{
					this.m_componentCreature.ComponentCreatureModel.LookAtOrder =
						new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
				}
			}, delegate
			{
				if (this.StartInfect(this.m_target))
				{
					this.m_stateMachine.TransitionTo("Inactive");
				}

				if (this.IsActive && this.m_target != null)
					return;

				this.m_stateMachine.TransitionTo("Inactive");
			}, null);

			this.m_stateMachine.TransitionTo("Inactive");
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
		}

		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_componentCreature;
		private ComponentChaseBehavior m_chaseBehavior;
		private readonly StateMachine m_stateMachine = new StateMachine();
		private readonly Random m_random = new Random();
		private float m_importanceLevel;
		private float m_poisonIntensity = 0f;
		private float m_infectionProbability = 0f;
		private float m_infectionCooldown = 0f;
		private float m_attackRange = 0f;
		private double m_lastInfectionTime;
		private ComponentCreature m_target;
	}
}
