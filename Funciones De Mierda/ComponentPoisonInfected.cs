using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentPoisonInfected : Component, IUpdateable
	{
		public bool IsJumpMove { get; set; }

		public bool IsInfected
		{
			get
			{
				return (double)this.m_InfectDuration > 0.0;
			}
		}

		public bool IsPuking
		{
			get
			{
				return this.m_pukeParticleSystem != null && !this.m_pukeParticleSystem.IsStopped;
			}
		}

		public void StartInfect(float infectDuration)
		{
			this.m_InfectDuration = MathUtils.Max(infectDuration - this.PoisonResistance, 0f);
			this.m_lastPukeTime = this.m_subsystemTime.GameTime;
		}

		public void NauseaEffect()
		{
			if (this.m_componentCreature == null || this.m_componentCreature.ComponentHealth == null)
				return;

			this.m_lastNauseaTime = new double?(this.m_subsystemTime.GameTime);
			float injury = 0.1f; // Daño fijo de 0.1 por efecto de náuseas

			if ((double)injury > 0.0)
			{
				this.m_subsystemTime.QueueGameTimeDelayedExecution(this.m_subsystemTime.GameTime + 0.75, delegate
				{
					if (this.m_componentCreature != null && this.m_componentCreature.ComponentHealth != null)
					{
						this.m_componentCreature.ComponentHealth.Injure(injury, null, false, "PoisonInfected");
						this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
					}
				});
			}

			if (this.m_pukeParticleSystem == null && this.m_componentCreature != null)
			{
				this.StartPuking();
			}
		}

		private void StartPuking()
		{
			if (this.m_componentCreature == null || this.m_componentCreature.ComponentBody == null)
				return;

			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			Vector3 direction = this.m_componentCreature.ComponentBody.Matrix.Forward;

			position += new Vector3(0f, 1.5f, 0f);
			position += direction * 0.3f;

			this.m_pukeParticleSystem = new PukeParticleSystem(this.m_subsystemTerrain);
			this.m_pukeParticleSystem.Position = position;
			this.m_pukeParticleSystem.Direction = direction;

			this.m_subsystemParticles.AddParticleSystem(this.m_pukeParticleSystem, false);

			if (this.m_componentCreature.ComponentCreatureSounds != null)
			{
				this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
			}

			this.m_lastPukeTime = this.m_subsystemTime.GameTime;
		}

		public void Update(float dt)
		{
			if (this.m_componentCreature == null || this.m_componentCreature.ComponentHealth == null)
				return;

			if (!this.m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
			{
				this.m_InfectDuration = 0f;
				return;
			}

			ComponentCreature componentCreature = this.m_componentCreature;
			if (componentCreature.ComponentLocomotion == null ||
				componentCreature.ComponentHealth == null ||
				componentCreature.ComponentBody == null ||
				componentCreature.ComponentCreatureModel == null)
				return;

			ComponentLocomotion componentLocomotion = componentCreature.ComponentLocomotion;
			ComponentHealth componentHealth = componentCreature.ComponentHealth;
			ComponentBody componentBody = componentCreature.ComponentBody;

			if (this.m_InfectDuration > 0f)
			{
				this.m_InfectDuration = MathUtils.Max(this.m_InfectDuration - dt, 0f);

				if (componentHealth.Health > 0f && this.m_subsystemTime.PeriodicGameTimeEvent(3.0, -0.01))
				{
					double nauseaInterval = (double)((this.m_InfectDuration > 150f) ? 5 : 10);
					if (this.m_lastNauseaTime == null || this.m_subsystemTime.GameTime - this.m_lastNauseaTime.Value > nauseaInterval)
					{
						this.NauseaEffect();
					}
				}

				if (this.m_lastPukeTime == null || this.m_subsystemTime.GameTime - this.m_lastPukeTime.Value > 30.0)
				{
					if (this.m_pukeParticleSystem == null || this.m_pukeParticleSystem.IsStopped)
					{
						this.StartPuking();
					}
				}
			}

			if (this.m_pukeParticleSystem != null && !this.m_pukeParticleSystem.IsStopped)
			{
				if (componentBody != null)
				{
					Vector3 position = componentBody.Position;
					Vector3 direction = componentBody.Matrix.Forward;

					position += new Vector3(0f, 1.5f, 0f);
					position += direction * 0.3f;

					this.m_pukeParticleSystem.Position = position;
					this.m_pukeParticleSystem.Direction = direction;
				}

				float num2 = (float)MathUtils.Remainder(this.m_subsystemTime.GameTime, 10000.0);
				float f = SimplexNoise.Noise(2f * num2);
				float x = MathUtils.DegToRad(MathUtils.Lerp(-35f, -60f, f)) - componentLocomotion.LookAngles.Y;
				componentLocomotion.LookOrder = new Vector2(componentLocomotion.LookOrder.X, MathUtils.Clamp(x, -2f, 2f));

				if (this.m_pukeParticleSystem.IsStopped)
				{
					this.m_pukeParticleSystem = null;
				}
			}

			float infectDuration = this.m_InfectDuration;
			if (infectDuration <= 0f)
			{
				componentLocomotion.WalkSpeed = this.oldWalkSpeed;
				componentLocomotion.FlySpeed = this.oldFlySpeed;
				componentLocomotion.SwimSpeed = this.oldSwimSpeed;
				componentLocomotion.JumpSpeed = this.oldJumpSpeed;
			}
			else if (infectDuration <= 150f)
			{
				float progress = infectDuration / 150f;
				float factor = MathUtils.Lerp(0.6f, 0.4f, progress);

				componentLocomotion.WalkSpeed = factor * this.oldWalkSpeed;
				componentLocomotion.FlySpeed = factor * this.oldFlySpeed;
				componentLocomotion.SwimSpeed = factor * this.oldSwimSpeed;
				componentLocomotion.JumpSpeed = factor * this.oldJumpSpeed;
			}
			else
			{
				float progress = MathUtils.Min((infectDuration - 150f) / 150f, 1f);
				float factor = MathUtils.Lerp(0.4f, 0.2f, progress);

				componentLocomotion.WalkSpeed = factor * this.oldWalkSpeed;
				componentLocomotion.FlySpeed = factor * this.oldFlySpeed;
				componentLocomotion.SwimSpeed = factor * this.oldSwimSpeed;
				componentLocomotion.JumpSpeed = factor * this.oldJumpSpeed;
			}

			if (this.IsJumpMove && this.m_InfectDuration > 0f)
			{
				if (this.m_subsystemTime.PeriodicGameTimeEvent(3.0, 0.0) &&
					this.m_random.Float(0f, 1f) < 0.15f)
				{
					Vector3 currentVelocity = componentBody.Velocity;

					float wobbleX = this.m_random.Float(-0.02f, 0.02f);
					float wobbleZ = this.m_random.Float(-0.02f, 0.02f);

					componentBody.Velocity = new Vector3(
						currentVelocity.X + wobbleX,
						currentVelocity.Y,
						currentVelocity.Z + wobbleZ
					);
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);

			this.IsJumpMove = valuesDictionary.GetValue<bool>("IsJumpMove", false);
			this.m_InfectDuration = valuesDictionary.GetValue<float>("InfectDuration", 0f);
			this.PoisonResistance = valuesDictionary.GetValue<float>("PoisonResistance", 0f);

			if (this.m_componentCreature != null && this.m_componentCreature.ComponentLocomotion != null)
			{
				this.oldWalkSpeed = this.m_componentCreature.ComponentLocomotion.WalkSpeed;
				this.oldFlySpeed = this.m_componentCreature.ComponentLocomotion.FlySpeed;
				this.oldSwimSpeed = this.m_componentCreature.ComponentLocomotion.SwimSpeed;
				this.oldJumpSpeed = this.m_componentCreature.ComponentLocomotion.JumpSpeed;
			}
			else
			{
				this.oldWalkSpeed = 1f;
				this.oldFlySpeed = 1f;
				this.oldSwimSpeed = 1f;
				this.oldJumpSpeed = 1f;
			}
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<float>("InfectDuration", this.m_InfectDuration);
			valuesDictionary.SetValue<float>("PoisonResistance", this.PoisonResistance);
		}

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private ComponentCreature m_componentCreature;
		private readonly Random m_random = new Random();
		private PukeParticleSystem m_pukeParticleSystem;
		public float m_InfectDuration;
		public float PoisonResistance;
		private double? m_lastNauseaTime;
		private double? m_lastPukeTime;
		private float oldWalkSpeed;
		private float oldFlySpeed;
		private float oldSwimSpeed;
		private float oldJumpSpeed;
		public const float SeriousPoisonInfectPeriod = 150f;
	}
}
