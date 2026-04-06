using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x020000C5 RID: 197
	public class ComponentPoisonInfected : Component, IUpdateable
	{
		// Token: 0x17000056 RID: 86
		// (get) Token: 0x060005E5 RID: 1509 RVA: 0x0001F9E8 File Offset: 0x0001DBE8
		// (set) Token: 0x060005E6 RID: 1510 RVA: 0x0001F9F0 File Offset: 0x0001DBF0
		public bool IsJumpMove { get; set; }

		// Token: 0x17000057 RID: 87
		// (get) Token: 0x060005E7 RID: 1511 RVA: 0x0001F9F9 File Offset: 0x0001DBF9
		public bool IsInfected
		{
			get
			{
				return (double)this.m_InfectDuration > 0.0;
			}
		}

		// Token: 0x17000058 RID: 88
		// (get) Token: 0x060005E8 RID: 1512 RVA: 0x0001FA0D File Offset: 0x0001DC0D
		public bool IsPuking
		{
			get
			{
				return this.m_pukeParticleSystem != null;
			}
		}

		// Token: 0x060005E9 RID: 1513 RVA: 0x0001FA18 File Offset: 0x0001DC18
		public void StartInfect(float infectDuration)
		{
			this.m_InfectDuration = MathUtils.Max(infectDuration - this.PoisonResistance, 0f);
		}

		// Token: 0x060005EA RID: 1514 RVA: 0x0001FA34 File Offset: 0x0001DC34
		public void NauseaEffect()
		{
			this.m_lastNauseaTime = new double?(this.m_subsystemTime.GameTime);
			float injury = MathUtils.Min(0.1f, this.m_componentCreature.ComponentHealth.Health - 0.09f);
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

		// Token: 0x060005EB RID: 1515 RVA: 0x0001FB1C File Offset: 0x0001DD1C
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
					double num = (double)((this.m_InfectDuration > 150f) ? 5 : 12);
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
					componentLocomotion.LadderSpeed = this.oldLadderSpeed;
				}
				else
				{
					componentLocomotion.WalkSpeed = 0.4f * this.oldWalkSpeed;
					componentLocomotion.FlySpeed = 0.4f * this.oldFlySpeed;
					componentLocomotion.SwimSpeed = 0.4f * this.oldSwimSpeed;
					componentLocomotion.JumpSpeed = 0.4f * this.oldJumpSpeed;
					componentLocomotion.LadderSpeed = 0.4f * this.oldLadderSpeed;
				}
			}
			else
			{
				componentLocomotion.WalkSpeed = 0.2f * this.oldWalkSpeed;
				componentLocomotion.FlySpeed = 0.2f * this.oldFlySpeed;
				componentLocomotion.SwimSpeed = 0.2f * this.oldSwimSpeed;
				componentLocomotion.JumpSpeed = 0.2f * this.oldJumpSpeed;
				componentLocomotion.LadderSpeed = 0f;
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

		// Token: 0x060005EC RID: 1516 RVA: 0x0001FF1C File Offset: 0x0001E11C
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
			this.oldLadderSpeed = this.m_componentCreature.ComponentLocomotion.LadderSpeed;
		}

		// Token: 0x060005ED RID: 1517 RVA: 0x00020058 File Offset: 0x0001E258
		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<float>("InfectDuration", this.m_InfectDuration);
		}

		// Token: 0x0400034A RID: 842
		private ComponentPilot componentPilot;

		// Token: 0x0400034B RID: 843
		private SubsystemGameInfo m_subsystemGameInfo;

		// Token: 0x0400034C RID: 844
		private SubsystemTerrain m_subsystemTerrain;

		// Token: 0x0400034D RID: 845
		private SubsystemTime m_subsystemTime;

		// Token: 0x0400034E RID: 846
		private SubsystemAudio m_subsystemAudio;

		// Token: 0x0400034F RID: 847
		private SubsystemParticles m_subsystemParticles;

		// Token: 0x04000350 RID: 848
		private ComponentCreature m_componentCreature;

		// Token: 0x04000351 RID: 849
		private readonly Game.Random m_random = new Game.Random();

		// Token: 0x04000352 RID: 850
		private PukeParticleSystem m_pukeParticleSystem;

		// Token: 0x04000353 RID: 851
		public float m_InfectDuration;

		// Token: 0x04000354 RID: 852
		public float PoisonResistance;

		// Token: 0x04000355 RID: 853
		private double? m_lastNauseaTime;

		// Token: 0x04000356 RID: 854
		private double? m_lastPukeTime;

		// Token: 0x04000357 RID: 855
		private float oldWalkSpeed;

		// Token: 0x04000358 RID: 856
		private float oldFlySpeed;

		// Token: 0x04000359 RID: 857
		private float oldSwimSpeed;

		// Token: 0x0400035A RID: 858
		private float oldJumpSpeed;

		// Token: 0x0400035B RID: 859
		private float oldLadderSpeed;

		// Token: 0x0400035D RID: 861
		public const float SeriousPoisonInfectPeriod = 150f;
	}
}
