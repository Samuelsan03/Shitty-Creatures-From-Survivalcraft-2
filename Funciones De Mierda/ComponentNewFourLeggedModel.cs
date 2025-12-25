using System;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000090 RID: 144
	public class ComponentNewFourLeggedModel : ComponentCreatureModel
	{
		// Token: 0x1700003A RID: 58
		// (get) Token: 0x06000453 RID: 1107 RVA: 0x00015B17 File Offset: 0x00013D17
		// (set) Token: 0x06000454 RID: 1108 RVA: 0x00015B1F File Offset: 0x00013D1F
		public float GrowStage { get; set; } = 1f;

		// Token: 0x06000455 RID: 1109 RVA: 0x00015B28 File Offset: 0x00013D28
		public override void Update(float dt)
		{
			float footstepsPhase = this.m_footstepsPhase;
			float num = this.m_componentCreature.ComponentLocomotion.SlipSpeed ?? Vector3.Dot(this.m_componentCreature.ComponentBody.Velocity, this.m_componentCreature.ComponentBody.Matrix.Forward);
			if (this.m_canCanter && num > 0.7f * this.m_componentCreature.ComponentLocomotion.WalkSpeed)
			{
				this.m_gait = ComponentNewFourLeggedModel.Gait.Canter;
				base.MovementAnimationPhase += num * dt * 0.7f * this.m_walkAnimationSpeed;
				this.m_footstepsPhase += 0.7f * this.m_walkAnimationSpeed * num * dt;
			}
			else if (this.m_canTrot && num > 0.5f * this.m_componentCreature.ComponentLocomotion.WalkSpeed)
			{
				this.m_gait = ComponentNewFourLeggedModel.Gait.Trot;
				base.MovementAnimationPhase += num * dt * this.m_walkAnimationSpeed;
				this.m_footstepsPhase += 1.25f * this.m_walkAnimationSpeed * num * dt;
			}
			else if (MathF.Abs(num) > 0.2f)
			{
				this.m_gait = ComponentNewFourLeggedModel.Gait.Walk;
				base.MovementAnimationPhase += num * dt * this.m_walkAnimationSpeed;
				this.m_footstepsPhase += 1.25f * this.m_walkAnimationSpeed * num * dt;
			}
			else
			{
				this.m_gait = ComponentNewFourLeggedModel.Gait.Walk;
				base.MovementAnimationPhase = 0f;
				this.m_footstepsPhase = 0f;
			}
			float num2 = 0f;
			if (this.m_gait == ComponentNewFourLeggedModel.Gait.Canter)
			{
				num2 = (0f - this.m_walkBobHeight) * 1.5f * MathF.Sin(6.2831855f * base.MovementAnimationPhase);
			}
			else if (this.m_gait == ComponentNewFourLeggedModel.Gait.Trot)
			{
				num2 = this.m_walkBobHeight * 1.5f * MathUtils.Sqr(MathF.Sin(6.2831855f * base.MovementAnimationPhase));
			}
			else if (this.m_gait == ComponentNewFourLeggedModel.Gait.Walk)
			{
				num2 = (0f - this.m_walkBobHeight) * MathUtils.Sqr(MathF.Sin(6.2831855f * base.MovementAnimationPhase));
			}
			float num3 = MathUtils.Min(12f * this.m_subsystemTime.GameTimeDelta, 1f);
			base.Bob += num3 * (num2 - base.Bob);
			if (this.m_gait == ComponentNewFourLeggedModel.Gait.Canter && this.m_useCanterSound)
			{
				float num4 = MathF.Floor(this.m_footstepsPhase);
				if (this.m_footstepsPhase > num4 && footstepsPhase <= num4)
				{
					string footstepSoundMaterialName = this.m_subsystemSoundMaterials.GetFootstepSoundMaterialName(this.m_componentCreature);
					if (!string.IsNullOrEmpty(footstepSoundMaterialName) && footstepSoundMaterialName != "Water")
					{
						this.m_subsystemAudio.PlayRandomSound("Audio/Footsteps/CanterDirt", 0.75f, this.m_random.Float(-0.25f, 0f), this.m_componentCreature.ComponentBody.Position, 3f, true);
					}
				}
			}
			else
			{
				float num5 = MathF.Floor(this.m_footstepsPhase);
				if (this.m_footstepsPhase > num5 && footstepsPhase <= num5)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayFootstepSound(1f);
				}
			}
			this.m_feedFactor = (base.FeedOrder ? MathUtils.Min(this.m_feedFactor + 2f * dt, 1f) : MathUtils.Max(this.m_feedFactor - 2f * dt, 0f));
			base.IsAttackHitMoment = false;
			if (base.AttackOrder)
			{
				this.m_buttFactor = MathUtils.Min(this.m_buttFactor + 4f * dt, 1f);
				float buttPhase = this.m_buttPhase;
				this.m_buttPhase = MathUtils.Remainder(this.m_buttPhase + dt * 2f, 1f);
				if (buttPhase < 0.5f && this.m_buttPhase >= 0.5f)
				{
					base.IsAttackHitMoment = true;
				}
			}
			else
			{
				this.m_buttFactor = MathUtils.Max(this.m_buttFactor - 4f * dt, 0f);
				if (this.m_buttPhase != 0f)
				{
					if (this.m_buttPhase > 0.5f)
					{
						this.m_buttPhase = MathUtils.Remainder(MathUtils.Min(this.m_buttPhase + dt * 2f, 1f), 1f);
					}
					else if (this.m_buttPhase > 0f)
					{
						this.m_buttPhase = MathUtils.Max(this.m_buttPhase - dt * 2f, 0f);
					}
				}
			}
			base.FeedOrder = false;
			base.AttackOrder = false;
			{
			}
			base.Update(dt);
		}

		// Token: 0x06000456 RID: 1110 RVA: 0x00015FCC File Offset: 0x000141CC
		public override void AnimateCreature()
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			Vector3 vector = this.m_componentCreature.ComponentBody.Rotation.ToYawPitchRoll();
			if (this.m_componentCreature.ComponentHealth.Health > 0f)
			{
				float num = 0f;
				float num2 = 0f;
				float num3 = 0f;
				float num4 = 0f;
				float num5 = 0f;
				if (base.MovementAnimationPhase != 0f && (this.m_componentCreature.ComponentBody.StandingOnValue != null || this.m_componentCreature.ComponentBody.ImmersionFactor > 0f))
				{
					if (this.m_gait == ComponentNewFourLeggedModel.Gait.Canter)
					{
						float num6 = MathF.Sin(6.2831855f * (base.MovementAnimationPhase + 0f));
						float num7 = MathF.Sin(6.2831855f * (base.MovementAnimationPhase + 0.25f));
						float num8 = MathF.Sin(6.2831855f * (base.MovementAnimationPhase + 0.15f));
						float num9 = MathF.Sin(6.2831855f * (base.MovementAnimationPhase + 0.4f));
						num = this.m_walkFrontLegsAngle * this.m_canterLegsAngleFactor * num6;
						num2 = this.m_walkFrontLegsAngle * this.m_canterLegsAngleFactor * num7;
						num3 = this.m_walkHindLegsAngle * this.m_canterLegsAngleFactor * num8;
						num4 = this.m_walkHindLegsAngle * this.m_canterLegsAngleFactor * num9;
						num5 = MathUtils.DegToRad(8f) * MathF.Sin(6.2831855f * base.MovementAnimationPhase);
					}
					else if (this.m_gait == ComponentNewFourLeggedModel.Gait.Trot)
					{
						float num10 = MathF.Sin(6.2831855f * (base.MovementAnimationPhase + 0f));
						float num11 = MathF.Sin(6.2831855f * (base.MovementAnimationPhase + 0.5f));
						float num12 = MathF.Sin(6.2831855f * (base.MovementAnimationPhase + 0.5f));
						float num13 = MathF.Sin(6.2831855f * (base.MovementAnimationPhase + 0f));
						num = this.m_walkFrontLegsAngle * num10;
						num2 = this.m_walkFrontLegsAngle * num11;
						num3 = this.m_walkHindLegsAngle * num12;
						num4 = this.m_walkHindLegsAngle * num13;
						num5 = MathUtils.DegToRad(3f) * MathF.Sin(12.566371f * base.MovementAnimationPhase);
					}
					else
					{
						float num14 = MathF.Sin(6.2831855f * (base.MovementAnimationPhase + 0f));
						float num15 = MathF.Sin(6.2831855f * (base.MovementAnimationPhase + 0.5f));
						float num16 = MathF.Sin(6.2831855f * (base.MovementAnimationPhase + 0.25f));
						float num17 = MathF.Sin(6.2831855f * (base.MovementAnimationPhase + 0.75f));
						num = this.m_walkFrontLegsAngle * num14;
						num2 = this.m_walkFrontLegsAngle * num15;
						num3 = this.m_walkHindLegsAngle * num16;
						num4 = this.m_walkHindLegsAngle * num17;
						num5 = MathUtils.DegToRad(3f) * MathF.Sin(12.566371f * base.MovementAnimationPhase);
					}
				}
				float num18 = MathUtils.Min(12f * this.m_subsystemTime.GameTimeDelta, 1f);
				this.m_legAngle1 += num18 * (num - this.m_legAngle1);
				this.m_legAngle2 += num18 * (num2 - this.m_legAngle2);
				this.m_legAngle3 += num18 * (num3 - this.m_legAngle3);
				this.m_legAngle4 += num18 * (num4 - this.m_legAngle4);
				this.m_headAngleY += num18 * (num5 - this.m_headAngleY);
				Vector2 vector2 = this.m_componentCreature.ComponentLocomotion.LookAngles;
				vector2.Y += this.m_headAngleY;
				vector2.X = Math.Clamp(vector2.X, 0f - MathUtils.DegToRad(65f), MathUtils.DegToRad(65f));
				vector2.Y = Math.Clamp(vector2.Y, 0f - MathUtils.DegToRad(55f), MathUtils.DegToRad(55f));
				Vector2 vector3 = Vector2.Zero;
				if (this.m_neckBone != null)
				{
					vector3 = 0.6f * vector2;
					vector2 = 0.4f * vector2;
				}
				if (this.m_feedFactor > 0f)
				{
					float y = 0f - MathUtils.DegToRad(25f + 45f * SimplexNoise.OctavedNoise((float)this.m_subsystemTime.GameTime, 3f, 2, 2f, 0.75f, false));
					Vector2 v = new Vector2(0f, y);
					vector2 = Vector2.Lerp(vector2, v, this.m_feedFactor);
					if (this.m_moveLegWhenFeeding)
					{
						float x = MathUtils.DegToRad(20f) + MathUtils.PowSign(SimplexNoise.OctavedNoise((float)this.m_subsystemTime.GameTime, 1f, 1, 1f, 1f, false) - 0.5f, 0.33f) / 0.5f * MathUtils.DegToRad(25f) * (float)Math.Sin(17.0 * this.m_subsystemTime.GameTime);
						num2 = MathUtils.Lerp(num2, x, this.m_feedFactor);
					}
				}
				if (this.m_buttFactor != 0f)
				{
					float y2 = (0f - MathUtils.DegToRad(40f)) * MathF.Sin(6.2831855f * MathUtils.Sigmoid(this.m_buttPhase, 4f));
					Vector2 v = new Vector2(0f, y2);
					vector2 = Vector2.Lerp(vector2, v, this.m_buttFactor);
				}
				this.SetBoneTransform(this.m_bodyBone.Index, new Matrix?(Matrix.CreateScale(this.m_bodyScale * this.GrowStage) * Matrix.CreateRotationY(vector.X) * Matrix.CreateTranslation(position.X, position.Y + base.Bob, position.Z)));
				this.SetBoneTransform(this.m_headBone.Index, new Matrix?(Matrix.CreateRotationX(vector2.Y) * Matrix.CreateRotationZ(0f - vector2.X)));
				if (this.m_neckBone != null)
				{
					this.SetBoneTransform(this.m_neckBone.Index, new Matrix?(Matrix.CreateRotationX(vector3.Y) * Matrix.CreateRotationZ(0f - vector3.X)));
				}
				this.SetBoneTransform(this.m_leg1Bone.Index, new Matrix?(Matrix.CreateRotationX(this.m_legAngle1)));
				this.SetBoneTransform(this.m_leg2Bone.Index, new Matrix?(Matrix.CreateRotationX(this.m_legAngle2)));
				this.SetBoneTransform(this.m_leg3Bone.Index, new Matrix?(Matrix.CreateRotationX(this.m_legAngle3)));
				this.SetBoneTransform(this.m_leg4Bone.Index, new Matrix?(Matrix.CreateRotationX(this.m_legAngle4)));
				return;
			}
			float num19 = 1f - base.DeathPhase;
			float num20 = (float)((Vector3.Dot(this.m_componentFrame.Matrix.Right, base.DeathCauseOffset) > 0f) ? 1 : -1);
			float num21 = this.m_componentCreature.ComponentBody.BoundingBox.Max.Y - this.m_componentCreature.ComponentBody.BoundingBox.Min.Y;
			this.SetBoneTransform(this.m_bodyBone.Index, new Matrix?(Matrix.CreateScale(this.m_bodyScale * this.GrowStage) * Matrix.CreateTranslation(-0.5f * num21 * Vector3.UnitY * base.DeathPhase) * Matrix.CreateFromYawPitchRoll(vector.X, 0f, 1.5707964f * base.DeathPhase * num20) * Matrix.CreateTranslation(0.2f * num21 * Vector3.UnitY * base.DeathPhase) * Matrix.CreateTranslation(position)));
			this.SetBoneTransform(this.m_headBone.Index, new Matrix?(Matrix.CreateRotationX(MathUtils.DegToRad(50f) * base.DeathPhase)));
			if (this.m_neckBone != null)
			{
				this.SetBoneTransform(this.m_neckBone.Index, new Matrix?(Matrix.Identity));
			}
			this.SetBoneTransform(this.m_leg1Bone.Index, new Matrix?(Matrix.CreateRotationX(this.m_legAngle1 * num19)));
			this.SetBoneTransform(this.m_leg2Bone.Index, new Matrix?(Matrix.CreateRotationX(this.m_legAngle2 * num19)));
			this.SetBoneTransform(this.m_leg3Bone.Index, new Matrix?(Matrix.CreateRotationX(this.m_legAngle3 * num19)));
			this.SetBoneTransform(this.m_leg4Bone.Index, new Matrix?(Matrix.CreateRotationX(this.m_legAngle4 * num19)));
		}

		// Token: 0x06000457 RID: 1111 RVA: 0x000168A4 File Offset: 0x00014AA4
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemSoundMaterials = base.Project.FindSubsystem<SubsystemSoundMaterials>(true);
			this.m_walkAnimationSpeed = valuesDictionary.GetValue<float>("WalkAnimationSpeed");
			this.m_walkFrontLegsAngle = valuesDictionary.GetValue<float>("WalkFrontLegsAngle");
			this.m_walkHindLegsAngle = valuesDictionary.GetValue<float>("WalkHindLegsAngle");
			this.m_canterLegsAngleFactor = valuesDictionary.GetValue<float>("CanterLegsAngleFactor");
			this.m_walkBobHeight = valuesDictionary.GetValue<float>("WalkBobHeight");
			this.m_moveLegWhenFeeding = valuesDictionary.GetValue<bool>("MoveLegWhenFeeding");
			this.m_canCanter = valuesDictionary.GetValue<bool>("CanCanter");
			this.m_canTrot = valuesDictionary.GetValue<bool>("CanTrot");
			this.m_useCanterSound = valuesDictionary.GetValue<bool>("UseCanterSound");
			this.m_bodyScale = valuesDictionary.GetValue<float>("Scale", 1f);
		}

		// Token: 0x06000458 RID: 1112 RVA: 0x000169A0 File Offset: 0x00014BA0
		public override void SetModel(Model model)
		{
			base.SetModel(model);
			if (base.Model != null)
			{
				this.m_bodyBone = base.Model.FindBone("Body", true);
				this.m_neckBone = base.Model.FindBone("Neck", false);
				this.m_headBone = base.Model.FindBone("Head", true);
				this.m_leg1Bone = base.Model.FindBone("Leg1", true);
				this.m_leg2Bone = base.Model.FindBone("Leg2", true);
				this.m_leg3Bone = base.Model.FindBone("Leg3", true);
				this.m_leg4Bone = base.Model.FindBone("Leg4", true);
				return;
			}
			this.m_bodyBone = null;
			this.m_neckBone = null;
			this.m_headBone = null;
			this.m_leg1Bone = null;
			this.m_leg2Bone = null;
			this.m_leg3Bone = null;
			this.m_leg4Bone = null;
		}

		// Token: 0x040001F4 RID: 500
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x040001F5 RID: 501
		public SubsystemSoundMaterials m_subsystemSoundMaterials;

		// Token: 0x040001F6 RID: 502
		public ModelBone m_bodyBone;

		// Token: 0x040001F7 RID: 503
		public ModelBone m_neckBone;

		// Token: 0x040001F8 RID: 504
		public ModelBone m_headBone;

		// Token: 0x040001F9 RID: 505
		public ModelBone m_leg1Bone;

		// Token: 0x040001FA RID: 506
		public ModelBone m_leg2Bone;

		// Token: 0x040001FB RID: 507
		public ModelBone m_leg3Bone;

		// Token: 0x040001FC RID: 508
		public ModelBone m_leg4Bone;

		// Token: 0x040001FD RID: 509
		public float m_walkAnimationSpeed;

		// Token: 0x040001FE RID: 510
		public float m_canterLegsAngleFactor;

		// Token: 0x040001FF RID: 511
		public float m_walkFrontLegsAngle;

		// Token: 0x04000200 RID: 512
		public float m_walkHindLegsAngle;

		// Token: 0x04000201 RID: 513
		public float m_walkBobHeight;

		// Token: 0x04000202 RID: 514
		public bool m_moveLegWhenFeeding;

		// Token: 0x04000203 RID: 515
		public bool m_canCanter;

		// Token: 0x04000204 RID: 516
		public bool m_canTrot;

		// Token: 0x04000205 RID: 517
		public bool m_useCanterSound;

		// Token: 0x04000206 RID: 518
		public ComponentNewFourLeggedModel.Gait m_gait;

		// Token: 0x04000207 RID: 519
		public float m_feedFactor;

		// Token: 0x04000208 RID: 520
		public float m_buttFactor;

		// Token: 0x04000209 RID: 521
		public float m_buttPhase;

		// Token: 0x0400020A RID: 522
		public float m_footstepsPhase;

		// Token: 0x0400020B RID: 523
		public float m_legAngle1;

		// Token: 0x0400020C RID: 524
		public float m_legAngle2;

		// Token: 0x0400020D RID: 525
		public float m_legAngle3;

		// Token: 0x0400020E RID: 526
		public float m_legAngle4;

		// Token: 0x0400020F RID: 527
		public float m_headAngleY;

		// Token: 0x04000210 RID: 528
		public float m_bodyScale = 1f;

		// Token: 0x02000131 RID: 305
		public enum Gait
		{
			// Token: 0x04000519 RID: 1305
			Walk,
			// Token: 0x0400051A RID: 1306
			Trot,
			// Token: 0x0400051B RID: 1307
			Canter
		}
	}
}
