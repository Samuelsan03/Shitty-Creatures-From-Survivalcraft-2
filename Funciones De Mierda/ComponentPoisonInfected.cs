using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace WonderfulEra
{
	// Token: 0x02000097 RID: 151
	public class ComponentPoisonInfected : Component, IUpdateable
	{
		// Token: 0x17000045 RID: 69
		// (get) Token: 0x060004A0 RID: 1184 RVA: 0x00018FD0 File Offset: 0x000171D0
		// (set) Token: 0x060004A1 RID: 1185 RVA: 0x00018FD8 File Offset: 0x000171D8
		public bool IsJumpMove { get; set; }

		// Token: 0x17000046 RID: 70
		// (get) Token: 0x060004A2 RID: 1186 RVA: 0x00018FE1 File Offset: 0x000171E1
		public bool IsInfected
		{
			get
			{
				return (double)this.m_InfectDuration > 0.0;
			}
		}

		// Token: 0x17000047 RID: 71
		// (get) Token: 0x060004A3 RID: 1187 RVA: 0x00018FF5 File Offset: 0x000171F5
		public bool IsPuking
		{
			get
			{
				return this.m_pukeParticleSystem != null;
			}
		}

		// Token: 0x060004A4 RID: 1188 RVA: 0x00019000 File Offset: 0x00017200
		public void StartInfect(float infectDuration)
		{
			this.m_InfectDuration = MathUtils.Max(infectDuration - this.PoisonResistance, 0f);
		}

		// Token: 0x060004A5 RID: 1189 RVA: 0x0001901C File Offset: 0x0001721C
		public void NauseaEffect()
		{
			this.m_lastNauseaTime = new double?(this.m_subsystemTime.GameTime);
			float injury = MathUtils.Min(0.1f, this.m_componentCreature.ComponentHealth.Health - 0.075f);
			if ((double)injury > 0.0)
			{
				this.m_subsystemTime.QueueGameTimeDelayedExecution(this.m_subsystemTime.GameTime + 0.75, delegate
				{
					this.m_componentCreature.ComponentHealth.Injure(injury, null, false, "PoisonInfected");
					this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
				});
			}
			if (this.m_pukeParticleSystem != null)
			{
				return;
			}
			this.m_lastPukeTime = new double?(this.m_subsystemTime.GameTime);
			this.m_pukeParticleSystem = new PukeParticleSystem(this.m_subsystemTerrain);
			this.m_subsystemParticles.AddParticleSystem(this.m_pukeParticleSystem, false);
			this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
		}

		// Token: 0x060004A6 RID: 1190 RVA: 0x00019104 File Offset: 0x00017304
		public void Update(float dt)
		{
			ComponentPlayer componentPlayer = base.Entity.FindComponent<ComponentPlayer>();
			if (!this.m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled || componentPlayer != null)
			{
				this.m_InfectDuration = 0f;
				return;
			}
			ComponentCreature componentCreature = this.m_componentCreature;
			ComponentLocomotion componentLocomotion = componentCreature.ComponentLocomotion;
			ComponentHealth componentHealth = componentCreature.ComponentHealth;
			ComponentBody componentBody = componentCreature.ComponentBody;
			ComponentCreatureModel componentCreatureModel = componentCreature.ComponentCreatureModel;
			if (this.m_InfectDuration > 0f)
			{
				this.m_InfectDuration = MathUtils.Max(this.m_InfectDuration - dt, 0f);
				if (componentHealth.Health > 0f && this.m_subsystemTime.PeriodicGameTimeEvent(3.0, -0.01))
				{
					double num = (double)((this.m_InfectDuration > 150f) ? 7 : 15);
					if (this.m_lastNauseaTime == null || this.m_subsystemTime.GameTime - this.m_lastNauseaTime.Value > num)
					{
						this.NauseaEffect();
					}
				}
			}
			if (this.m_pukeParticleSystem != null)
			{
				float num2 = (float)MathUtils.Remainder(this.m_subsystemTime.GameTime, 10000.0);
				float f = SimplexNoise.Noise(2f * num2);
				float x = MathUtils.DegToRad(MathUtils.Lerp(-35f, -60f, f)) - componentLocomotion.LookAngles.Y;
				componentLocomotion.LookOrder = new Vector2(componentLocomotion.LookOrder.X, MathUtils.Clamp(x, -2f, 2f));
				Quaternion eyeRotation = componentCreatureModel.EyeRotation;
				Vector3 upVector = eyeRotation.GetUpVector();
				Vector3 forwardVector = eyeRotation.GetForwardVector();
				this.m_pukeParticleSystem.Position = componentCreatureModel.EyePosition - upVector * 0.08f + forwardVector * 0.3f;
				this.m_pukeParticleSystem.Direction = Vector3.Normalize(forwardVector + upVector * 0.5f);
				if (this.m_pukeParticleSystem.IsStopped)
				{
					this.m_pukeParticleSystem = null;
				}
			}
			float infectDuration = this.m_InfectDuration;
			if (infectDuration <= 150f)
			{
				if (infectDuration <= 0f)
				{
					componentLocomotion.WalkSpeed = this.oldWalkSpeed;
					componentLocomotion.FlySpeed = this.oldFlySpeed;
					componentLocomotion.SwimSpeed = this.oldSwimSpeed;
					componentLocomotion.JumpSpeed = this.oldJumpSpeed;
				}
				else
				{
					componentLocomotion.WalkSpeed = 0.5f * this.oldWalkSpeed;
					componentLocomotion.FlySpeed = 0.5f * this.oldFlySpeed;
					componentLocomotion.SwimSpeed = 0.5f * this.oldSwimSpeed;
					componentLocomotion.JumpSpeed = 0.5f * this.oldJumpSpeed;
				}
			}
			else
			{
				componentLocomotion.WalkSpeed = 0.25f * this.oldWalkSpeed;
				componentLocomotion.FlySpeed = 0.25f * this.oldFlySpeed;
				componentLocomotion.SwimSpeed = 0.25f * this.oldSwimSpeed;
				componentLocomotion.JumpSpeed = 0.25f * this.oldJumpSpeed;
			}
			if (!this.IsJumpMove)
			{
				return;
			}
			ComponentHumanModel componentHumanModel = componentCreatureModel as ComponentHumanModel;
			if (componentHumanModel != null)
			{
				componentHumanModel.m_handAngles2 = new Vector2(2f, 0f);
				componentHumanModel.m_handAngles1 = new Vector2(2f, 0f);
			}
			if (componentHealth.Health <= 0f || this.componentPilot.Speed <= 0f || (componentBody.StandingOnBody == null && componentBody.StandingOnValue == null && componentBody.ImmersionDepth <= 0f) || !this.m_subsystemTime.PeriodicGameTimeEvent(2.0, 0.0))
			{
				return;
			}
			float jumpSpeed = componentLocomotion.JumpSpeed;
			componentLocomotion.JumpOrder = 1f;
			Vector3 forward = componentBody.Matrix.Forward;
			componentBody.Velocity = new Vector3(forward.X * jumpSpeed, jumpSpeed, forward.Z * jumpSpeed);
		}

		// Token: 0x060004A7 RID: 1191 RVA: 0x000194D8 File Offset: 0x000176D8
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.componentPilot = this.m_componentCreature.Entity.FindComponent<ComponentPilot>(true);
			this.IsJumpMove = valuesDictionary.GetValue<bool>("IsJumpMove", false);
			this.m_InfectDuration = valuesDictionary.GetValue<float>("InfectDuration", 0f);
			this.PoisonResistance = valuesDictionary.GetValue<float>("PoisonResistance", 0f);
			this.oldWalkSpeed = this.m_componentCreature.ComponentLocomotion.WalkSpeed;
			this.oldFlySpeed = this.m_componentCreature.ComponentLocomotion.FlySpeed;
			this.oldSwimSpeed = this.m_componentCreature.ComponentLocomotion.SwimSpeed;
			this.oldJumpSpeed = this.m_componentCreature.ComponentLocomotion.JumpSpeed;
		}

		// Token: 0x060004A8 RID: 1192 RVA: 0x000195FE File Offset: 0x000177FE
		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<float>("InfectDuration", this.m_InfectDuration);
		}

		// Token: 0x04000255 RID: 597
		private ComponentPilot componentPilot;

		// Token: 0x04000256 RID: 598
		private SubsystemGameInfo m_subsystemGameInfo;

		// Token: 0x04000257 RID: 599
		private SubsystemTerrain m_subsystemTerrain;

		// Token: 0x04000258 RID: 600
		private SubsystemTime m_subsystemTime;

		// Token: 0x04000259 RID: 601
		private SubsystemAudio m_subsystemAudio;

		// Token: 0x0400025A RID: 602
		private SubsystemParticles m_subsystemParticles;

		// Token: 0x0400025B RID: 603
		private ComponentCreature m_componentCreature;

		// Token: 0x0400025C RID: 604
		private readonly Game.Random m_random = new Game.Random();

		// Token: 0x0400025D RID: 605
		private PukeParticleSystem m_pukeParticleSystem;

		// Token: 0x0400025E RID: 606
		public float m_InfectDuration;

		// Token: 0x0400025F RID: 607
		public float PoisonResistance;

		// Token: 0x04000260 RID: 608
		private double? m_lastNauseaTime;

		// Token: 0x04000261 RID: 609
		private double? m_lastPukeTime;

		// Token: 0x04000262 RID: 610
		private float oldWalkSpeed;

		// Token: 0x04000263 RID: 611
		private float oldFlySpeed;

		// Token: 0x04000264 RID: 612
		private float oldSwimSpeed;

		// Token: 0x04000265 RID: 613
		private float oldJumpSpeed;

		// Token: 0x04000267 RID: 615
		public const float SeriousPoisonInfectPeriod = 150f;
	}
}
