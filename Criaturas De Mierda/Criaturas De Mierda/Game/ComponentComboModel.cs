using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{

    public class ComponentComboModel : ComponentHumanModel
    {
        public enum Gait
        {
            Walk,
            Trot,
            Canter
        }

        public float m_buttFactor;

        public float m_buttPhase;

        public float m_footstepsPhase;

        public float m_bitingPhase;

        public float m_digInTailPhase;

        public float m_legAngle1;

        public float m_legAngle2;

        public float m_legAngle3;

        public float m_legAngle4;

        public float m_headAngleY;

        public float? BendOrder;

        public bool m_hasVerticalTail;

        public bool m_hasWings;

        public float DigInOrder;

        public float m_swimAnimationSpeed;

        public float m_flyAnimationSpeed;

        public float m_peckAnimationSpeed;

        public float m_digInDepth;

        public float m_opacity;

        public float TailPhase;

        public float FlyPhase;

        public float m_peckPhase;

        public Vector2 m_tailTurn;

        public int m_attackMode;

        public float m_bodyScale = 1f;

        public Vector3 m_bodyColor;

        public ModelBone m_bodyBone;

        public ModelBone m_neckBone;

        public ModelBone m_neck1Bone;

        public ModelBone m_neck2Bone;

        public ModelBone m_neck3Bone;

        public ModelBone m_neck4Bone;

        public ModelBone m_neck5Bone;

        public ModelBone m_neck6Bone;

        public ModelBone m_headBone;

        public ModelBone m_leg1Bone;

        public ModelBone m_leg2Bone;

        public ModelBone m_leg3Bone;

        public ModelBone m_leg4Bone;

        public ModelBone m_wing1Bone;

        public ModelBone m_wing2Bone;

        public ModelBone m_tail1Bone;

        public ModelBone m_tail2Bone;

        public ModelBone m_tail3Bone;

        public ModelBone m_tail4Bone;

        public ModelBone m_tail5Bone;

        public ModelBone m_tail6Bone;

        public ModelBone m_tail7Bone;

        public ModelBone m_tail8Bone;

        public ModelBone m_jawBone;

        public ModelBone m_bodyBone1;

        public ModelBone m_bodyBone2;

        public ModelBone m_bodyBone3;

        public ModelBone m_bodyBone4;

        public bool m_verticalMoveBody12;

        public bool m_verticalMoveBody34;

        public float m_walkAnimationSpeed;

        public float m_canterLegsAngleFactor;

        public float m_walkFrontLegsAngle;

        public float m_walkHindLegsAngle;

        public float m_walkBobHeight;

        public bool m_moveLegWhenFeeding;

        public bool m_canCanter;

        public bool m_canTrot;

        public bool m_useCanterSound;

        public Gait m_gait;

        public float m_feedFactor;

        public SubsystemModelsRenderer m_subsystemModelsRenderer;

        public SubsystemAudio m_subsystemAudio;

        public SubsystemSoundMaterials m_subsystemSoundMaterials;

        public float GrowStage { get; set; } = 1f;


        [Obsolete]
        public override void Update(float dt)
        {
            float footstepsPhase = m_footstepsPhase;
            float num = Vector3.Dot(m_componentCreature.ComponentBody.Velocity, m_componentCreature.ComponentBody.Matrix.Forward);
            if (m_canCanter && num > 0.7f * m_componentCreature.ComponentLocomotion.WalkSpeed)
            {
                m_gait = Gait.Canter;
                base.MovementAnimationPhase += num * dt * 0.7f * m_walkAnimationSpeed;
                m_footstepsPhase += 0.7f * m_walkAnimationSpeed * num * dt;
                TailPhase = base.MovementAnimationPhase;
            }
            else if (m_canTrot && num > 0.5f * m_componentCreature.ComponentLocomotion.WalkSpeed)
            {
                m_gait = Gait.Trot;
                base.MovementAnimationPhase += num * dt * m_walkAnimationSpeed;
                m_footstepsPhase += 1.25f * m_walkAnimationSpeed * num * dt;
                TailPhase = base.MovementAnimationPhase;
            }
            else if (Math.Abs(num) > 0.2f)
            {
                m_gait = Gait.Walk;
                base.MovementAnimationPhase += num * dt * m_walkAnimationSpeed;
                m_footstepsPhase += 1.25f * m_walkAnimationSpeed * num * dt;
                TailPhase = base.MovementAnimationPhase;
            }
            else
            {
                m_gait = Gait.Walk;
                m_footstepsPhase = 0f;
                base.MovementAnimationPhase = 0f;
                TailPhase = MathUtils.Remainder(TailPhase + 0.3f * m_walkAnimationSpeed * dt, 1000f);
            }
            if (!m_componentCreature.ComponentBody.StandingOnValue.HasValue)
            {
                if (m_componentCreature.ComponentLocomotion.LastSwimOrder.HasValue && m_componentCreature.ComponentLocomotion.LastSwimOrder.Value != Vector3.Zero)
                {
                    float num2 = ((m_componentCreature.ComponentLocomotion.LastSwimOrder.Value.LengthSquared() > 0.99f) ? 1.75f : 1f);
                    TailPhase = MathUtils.Remainder(TailPhase + m_swimAnimationSpeed * num2 * dt, 1000f);
                    DigInOrder = m_digInDepth;
                }
                else if (m_componentCreature.ComponentLocomotion.LastFlyOrder.HasValue && m_componentCreature.ComponentLocomotion.LastFlyOrder.Value != Vector3.Zero)
                {
                    float num3 = ((m_componentCreature.ComponentLocomotion.LastFlyOrder.Value.LengthSquared() > 0.99f) ? 1.75f : 1f);
                    TailPhase = MathUtils.Remainder(TailPhase + m_flyAnimationSpeed * num3 * dt, 1000f);
                }
            }
            else if (m_componentCreature.ComponentHealth.BreathingMode == BreathingMode.Water && m_componentCreature.ComponentHealth.Health < 0.5f)
            {
                BendOrder = 2f * (2f * MathUtils.Saturate(SimplexNoise.OctavedNoise((float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0), 1.2f * m_componentCreature.ComponentLocomotion.TurnSpeed, 1, 1f, 1f)) - 1f);
            }
            float num4 = 0f;
            if (m_gait == Gait.Canter)
            {
                num4 = (0f - m_walkBobHeight) * 1.5f * MathUtils.Sin((float)Math.PI * 2f * base.MovementAnimationPhase);
            }
            else if (m_gait == Gait.Trot)
            {
                num4 = m_walkBobHeight * 1.5f * MathUtils.Sqr(MathUtils.Sin((float)Math.PI * 2f * base.MovementAnimationPhase));
            }
            else if (m_gait == Gait.Walk)
            {
                num4 = (0f - m_walkBobHeight) * MathUtils.Sqr(MathUtils.Sin((float)Math.PI * 2f * base.MovementAnimationPhase));
            }
            float num5 = MathUtils.Min(12f * m_subsystemTime.GameTimeDelta, 1f);
            base.Bob += num5 * (num4 - base.Bob);
            if (m_gait == Gait.Canter && m_useCanterSound)
            {
                float num6 = MathUtils.Floor(m_footstepsPhase);
                if (m_footstepsPhase > num6 && footstepsPhase <= num6)
                {
                    string footstepSoundMaterialName = m_subsystemSoundMaterials.GetFootstepSoundMaterialName(m_componentCreature);
                    if (!string.IsNullOrEmpty(footstepSoundMaterialName) && footstepSoundMaterialName != "Water")
                    {
                        m_subsystemAudio.PlayRandomSound("Audio/Footsteps/CanterDirt", 0.75f, ((ComponentCreatureModel)this).m_random.Float(-0.25f, 0f), m_componentCreature.ComponentBody.Position, 3f, autoDelay: true);
                    }
                }
            }
            else
            {
                float num7 = MathUtils.Floor(m_footstepsPhase);
                if (m_footstepsPhase > num7 && footstepsPhase <= num7)
                {
                    m_componentCreature.ComponentCreatureSounds.PlayFootstepSound(1f);
                }
            }
            if (m_hasWings)
            {
                if (m_componentCreature.ComponentLocomotion.LastFlyOrder.HasValue)
                {
                    float num8 = ((m_componentCreature.ComponentLocomotion.LastFlyOrder.Value.LengthSquared() > 0.99f) ? 1.5f : 1f);
                    FlyPhase = MathUtils.Remainder(FlyPhase + m_flyAnimationSpeed * num8 * dt, 1f);
                    if (m_componentCreature.ComponentLocomotion.LastFlyOrder.Value.Y < -0.1f && m_componentCreature.ComponentBody.Velocity.Length() > 4f)
                    {
                        FlyPhase = 0.72f;
                    }
                }
                else if (FlyPhase != 1f)
                {
                    FlyPhase = MathUtils.Min(FlyPhase + m_flyAnimationSpeed * dt, 1f);
                }
            }
            if (BendOrder.HasValue)
            {
                if (m_hasVerticalTail)
                {
                    m_tailTurn.X = 0f;
                    m_tailTurn.Y = BendOrder.Value;
                }
                else
                {
                    m_tailTurn.X = BendOrder.Value;
                    m_tailTurn.Y = 0f;
                }
            }
            else
            {
                m_tailTurn.X += MathUtils.Saturate(2f * m_componentCreature.ComponentLocomotion.TurnSpeed * dt) * (0f - m_componentCreature.ComponentLocomotion.LastTurnOrder.X - m_tailTurn.X);
            }
            if (DigInOrder > m_digInDepth)
            {
                float num9 = (DigInOrder - m_digInDepth) * MathUtils.Min(1.5f * dt, 1f);
                m_digInDepth += num9;
                m_digInTailPhase += 20f * num9;
            }
            else if (DigInOrder < m_digInDepth)
            {
                m_digInDepth += (DigInOrder - m_digInDepth) * MathUtils.Min(5f * dt, 1f);
            }
            if (base.FeedOrder)
            {
                m_feedFactor = MathUtils.Min(m_feedFactor + 2f * dt, 1f);
                m_peckPhase += m_peckAnimationSpeed * dt;
                if (m_peckPhase > 0.75f)
                {
                    m_peckPhase -= 0.5f;
                }
            }
            else if (m_peckPhase != 0f)
            {
                m_peckPhase = MathUtils.Remainder(MathUtils.Min(m_peckPhase + m_peckAnimationSpeed * dt, 1f), 1f);
            }
            else
            {
                m_feedFactor = MathUtils.Max(m_feedFactor - 2f * dt, 0f);
            }
            base.IsAttackHitMoment = false;
            float num10 = 0.75f * m_componentCreature.ComponentLocomotion.TurnSpeed;
            if ((base.AttackOrder || base.FeedOrder) && m_jawBone != null)
            {
                float bitingPhase = m_bitingPhase;
                m_bitingPhase = MathUtils.Remainder(m_bitingPhase + num10 * dt, 1f);
                if (base.AttackOrder && bitingPhase < 0.5f && m_bitingPhase >= 0.5f)
                {
                    base.IsAttackHitMoment = true;
                }
            }
            else if (m_bitingPhase != 0f)
            {
                m_bitingPhase = MathUtils.Remainder(MathUtils.Min(m_bitingPhase + num10 * dt, 1f), 1f);
            }
            if (base.AttackOrder && (m_jawBone == null || m_attackMode == 0))
            {
                m_buttFactor = MathUtils.Min(m_buttFactor + 4f * dt, 1f);
                float buttPhase = m_buttPhase;
                m_buttPhase = MathUtils.Remainder(m_buttPhase + dt * 2f, 1f);
                if (buttPhase < 0.5f && m_buttPhase >= 0.5f)
                {
                    base.IsAttackHitMoment = true;
                }
            }
            else
            {
                m_buttFactor = MathUtils.Max(m_buttFactor - 4f * dt, 0f);
                if (m_buttPhase != 0f)
                {
                    if (m_buttPhase > 0.5f)
                    {
                        m_buttPhase = MathUtils.Remainder(MathUtils.Min(m_buttPhase + dt * 2f, 1f), 1f);
                    }
                    else if (m_buttPhase > 0f)
                    {
                        m_buttPhase = MathUtils.Max(m_buttPhase - dt * 2f, 0f);
                    }
                }
            }
            base.FeedOrder = false;
            base.AttackOrder = false;
            BendOrder = null;
            DigInOrder = 0f;
            base.Update(dt);
        }

        [Obsolete]
        public override void Animate()
        {
            float num = 0f;
            Vector3 position = m_componentCreature.ComponentBody.Position;
            Vector3 vector = m_componentCreature.ComponentBody.Rotation.ToYawPitchRoll();
            if (m_componentCreature.ComponentHealth.Health > 0f)
            {
                float num2 = 0f;
                float num3 = 0f;
                float num4 = 0f;
                float num5 = 0f;
                float num6 = 0f;
                if (m_hasWings)
                {
                    num += 1.2f * MathUtils.Sin((float)Math.PI * 2f * (FlyPhase + 0.75f));
                    if (m_componentCreature.ComponentBody.StandingOnValue.HasValue)
                    {
                        num += 0.3f * MathUtils.Sin((float)Math.PI * 2f * base.MovementAnimationPhase);
                    }
                }
                float num7;
                float num8;
                float num9;
                float num10;
                if (m_hasVerticalTail)
                {
                    num7 = MathUtils.DegToRad(25f) * MathUtils.Clamp(0.5f * MathUtils.Sin((float)Math.PI * 2f * m_digInTailPhase) - m_tailTurn.X, -1f, 1f);
                    num8 = MathUtils.DegToRad(30f) * MathUtils.Clamp(0.5f * MathUtils.Sin(2f * ((float)Math.PI * MathUtils.Max(m_digInTailPhase - 0.25f, 0f))) - m_tailTurn.X, -1f, 1f);
                    num9 = MathUtils.DegToRad(25f) * MathUtils.Clamp(0.5f * MathUtils.Sin((float)Math.PI * 2f * TailPhase) - m_tailTurn.Y, -1f, 1f);
                    num10 = MathUtils.DegToRad(30f) * MathUtils.Clamp(0.5f * MathUtils.Sin((float)Math.PI * 2f * MathUtils.Max(TailPhase - 0.25f, 0f)) - m_tailTurn.Y, -1f, 1f);
                }
                else
                {
                    num7 = MathUtils.DegToRad(25f) * MathUtils.Clamp(0.5f * MathUtils.Sin((float)Math.PI * 2f * (TailPhase + m_digInTailPhase)) - m_tailTurn.X, -1f, 1f);
                    num8 = MathUtils.DegToRad(30f) * MathUtils.Clamp(0.5f * MathUtils.Sin(2f * ((float)Math.PI * MathUtils.Max(TailPhase + m_digInTailPhase - 0.25f, 0f))) - m_tailTurn.X, -1f, 1f);
                    num9 = MathUtils.DegToRad(25f) * MathUtils.Clamp(0f - m_tailTurn.Y, -1f, 1f);
                    num10 = MathUtils.DegToRad(30f) * MathUtils.Clamp(0f - m_tailTurn.Y, -1f, 1f);
                }
                Matrix identity = Matrix.Identity;
                if (num7 != 0f)
                {
                    identity *= Matrix.CreateRotationZ(num7);
                }
                if (num9 != 0f)
                {
                    identity *= Matrix.CreateRotationX(num9);
                }
                Matrix identity2 = Matrix.Identity;
                if (num8 != 0f)
                {
                    identity2 *= Matrix.CreateRotationZ(num8);
                }
                if (num10 != 0f)
                {
                    identity2 *= Matrix.CreateRotationX(num10);
                }
                Matrix identity3 = Matrix.Identity;
                Matrix identity4 = Matrix.Identity;
                if (m_bodyBone1 != null)
                {
                    float num11;
                    float num12;
                    float num13;
                    float num14;
                    if (m_verticalMoveBody12)
                    {
                        num11 = MathUtils.DegToRad(30f) * MathUtils.Clamp(0.5f * MathUtils.Sin((float)Math.PI * 2f * m_digInTailPhase) - m_tailTurn.X, -1f, 1f);
                        num12 = MathUtils.DegToRad(-30f) * MathUtils.Clamp(0.5f * MathUtils.Sin(2f * ((float)Math.PI * MathUtils.Max(m_digInTailPhase - 0.25f, 0f))) - m_tailTurn.X, -1f, 1f);
                        num13 = MathUtils.DegToRad(30f) * MathUtils.Clamp(0.5f * MathUtils.Sin((float)Math.PI * 2f * TailPhase) - m_tailTurn.Y, -1f, 1f);
                        num14 = MathUtils.DegToRad(-30f) * MathUtils.Clamp(0.5f * MathUtils.Sin((float)Math.PI * 2f * MathUtils.Max(TailPhase - 0.25f, 0f)) - m_tailTurn.Y, -1f, 1f);
                    }
                    else
                    {
                        num11 = MathUtils.DegToRad(30f) * MathUtils.Clamp(0.5f * MathUtils.Sin((float)Math.PI * 2f * (TailPhase + m_digInTailPhase)) - m_tailTurn.X, -1f, 1f);
                        num12 = MathUtils.DegToRad(-30f) * MathUtils.Clamp(0.5f * MathUtils.Sin(2f * ((float)Math.PI * MathUtils.Max(TailPhase + m_digInTailPhase - 0.25f, 0f))) - m_tailTurn.X, -1f, 1f);
                        num13 = MathUtils.DegToRad(30f) * MathUtils.Clamp(0f - m_tailTurn.Y, -1f, 1f);
                        num14 = MathUtils.DegToRad(-30f) * MathUtils.Clamp(0f - m_tailTurn.Y, -1f, 1f);
                    }
                    if (num11 != 0f)
                    {
                        identity3 *= Matrix.CreateRotationZ(num11);
                    }
                    if (num13 != 0f)
                    {
                        identity3 *= Matrix.CreateRotationX(num13);
                    }
                    if (num12 != 0f)
                    {
                        identity4 *= Matrix.CreateRotationZ(num12);
                    }
                    if (num14 != 0f)
                    {
                        identity4 *= Matrix.CreateRotationX(num14);
                    }
                }
                Matrix identity5 = Matrix.Identity;
                Matrix identity6 = Matrix.Identity;
                if (m_bodyBone3 != null)
                {
                    float num15;
                    float num16;
                    float num17;
                    float num18;
                    if (m_verticalMoveBody34)
                    {
                        num15 = MathUtils.DegToRad(-30f) * MathUtils.Clamp(0.5f * MathUtils.Sin((float)Math.PI * 2f * m_digInTailPhase) - m_tailTurn.X, -1f, 1f);
                        num16 = MathUtils.DegToRad(-45f) * MathUtils.Clamp(0.5f * MathUtils.Sin(2f * ((float)Math.PI * MathUtils.Max(m_digInTailPhase - 0.25f, 0f))) - m_tailTurn.X, -1f, 1f);
                        num17 = MathUtils.DegToRad(-30f) * MathUtils.Clamp(0.5f * MathUtils.Sin((float)Math.PI * 2f * TailPhase) - m_tailTurn.Y, -1f, 1f);
                        num18 = MathUtils.DegToRad(-45f) * MathUtils.Clamp(0.5f * MathUtils.Sin((float)Math.PI * 2f * MathUtils.Max(TailPhase - 0.25f, 0f)) - m_tailTurn.Y, -1f, 1f);
                    }
                    else
                    {
                        num15 = MathUtils.DegToRad(-30f) * MathUtils.Clamp(0.5f * MathUtils.Sin((float)Math.PI * 2f * (TailPhase + m_digInTailPhase)) - m_tailTurn.X, -1f, 1f);
                        num16 = MathUtils.DegToRad(-45f) * MathUtils.Clamp(0.5f * MathUtils.Sin(2f * ((float)Math.PI * MathUtils.Max(TailPhase + m_digInTailPhase - 0.25f, 0f))) - m_tailTurn.X, -1f, 1f);
                        num17 = MathUtils.DegToRad(-30f) * MathUtils.Clamp(0f - m_tailTurn.Y, -1f, 1f);
                        num18 = MathUtils.DegToRad(-45f) * MathUtils.Clamp(0f - m_tailTurn.Y, -1f, 1f);
                    }
                    if (num15 != 0f)
                    {
                        identity5 *= Matrix.CreateRotationZ(num15);
                    }
                    if (num17 != 0f)
                    {
                        identity5 *= Matrix.CreateRotationX(num17);
                    }
                    if (num16 != 0f)
                    {
                        identity6 *= Matrix.CreateRotationZ(num16);
                    }
                    if (num18 != 0f)
                    {
                        identity6 *= Matrix.CreateRotationX(num18);
                    }
                }
                float num19 = MathUtils.Min(12f * m_subsystemTime.GameTimeDelta, 1f);
                if (base.MovementAnimationPhase != 0f && (m_componentCreature.ComponentBody.StandingOnValue.HasValue || m_componentCreature.ComponentBody.ImmersionFactor > 0f))
                {
                    if (m_gait == Gait.Canter)
                    {
                        float num20 = MathUtils.Sin((float)Math.PI * 2f * (base.MovementAnimationPhase + 0f));
                        float num21 = MathUtils.Sin((float)Math.PI * 2f * (base.MovementAnimationPhase + 0.25f));
                        float num22 = MathUtils.Sin((float)Math.PI * 2f * (base.MovementAnimationPhase + 0.15f));
                        float num23 = MathUtils.Sin((float)Math.PI * 2f * (base.MovementAnimationPhase + 0.4f));
                        num2 = m_walkFrontLegsAngle * m_canterLegsAngleFactor * num20;
                        num3 = m_walkFrontLegsAngle * m_canterLegsAngleFactor * num21;
                        num4 = m_walkHindLegsAngle * m_canterLegsAngleFactor * num22;
                        num5 = m_walkHindLegsAngle * m_canterLegsAngleFactor * num23;
                        num6 = MathUtils.DegToRad(8f) * MathUtils.Sin((float)Math.PI * 2f * base.MovementAnimationPhase);
                    }
                    else if (m_gait == Gait.Trot)
                    {
                        float num24 = MathUtils.Sin((float)Math.PI * 2f * (base.MovementAnimationPhase + 0f));
                        float num25 = MathUtils.Sin((float)Math.PI * 2f * (base.MovementAnimationPhase + 0.5f));
                        float num26 = MathUtils.Sin((float)Math.PI * 2f * (base.MovementAnimationPhase + 0.5f));
                        float num27 = MathUtils.Sin((float)Math.PI * 2f * (base.MovementAnimationPhase + 0f));
                        num2 = m_walkFrontLegsAngle * num24;
                        num3 = m_walkFrontLegsAngle * num25;
                        num4 = m_walkHindLegsAngle * num26;
                        num5 = m_walkHindLegsAngle * num27;
                        num6 = MathUtils.DegToRad(3f) * MathUtils.Sin((float)Math.PI * 4f * base.MovementAnimationPhase);
                    }
                    else
                    {
                        float num28 = MathUtils.Sin((float)Math.PI * 2f * (base.MovementAnimationPhase + 0f));
                        float num29 = MathUtils.Sin((float)Math.PI * 2f * (base.MovementAnimationPhase + 0.5f));
                        float num30 = MathUtils.Sin((float)Math.PI * 2f * (base.MovementAnimationPhase + 0.25f));
                        float num31 = MathUtils.Sin((float)Math.PI * 2f * (base.MovementAnimationPhase + 0.75f));
                        num2 = m_walkFrontLegsAngle * num28;
                        num3 = m_walkFrontLegsAngle * num29;
                        num4 = m_walkHindLegsAngle * num30;
                        num5 = m_walkHindLegsAngle * num31;
                        num6 = MathUtils.DegToRad(3f) * MathUtils.Sin((float)Math.PI * 4f * base.MovementAnimationPhase);
                    }
                }
                m_legAngle1 += num19 * (num2 - m_legAngle1);
                m_legAngle2 += num19 * (num3 - m_legAngle2);
                m_legAngle3 += num19 * (num4 - m_legAngle3);
                m_legAngle4 += num19 * (num5 - m_legAngle4);
                if (m_leg3Bone == null && m_leg1Bone != null)
                {
                    if (m_componentCreature.ComponentBody.StandingOnValue.HasValue || m_componentCreature.ComponentBody.ImmersionFactor > 0f || m_componentCreature.ComponentLocomotion.FlySpeed == 0f)
                    {
                        m_legAngle1 = 0.6f * MathUtils.Sin((float)Math.PI * 2f * base.MovementAnimationPhase);
                        m_legAngle2 = 0f - m_legAngle1;
                    }
                    else
                    {
                        m_legAngle2 = (m_legAngle1 = 0f - MathUtils.DegToRad(80f));
                    }
                }
                m_headAngleY += num19 * (num6 - m_headAngleY);
                Vector2 vector2 = m_componentCreature.ComponentLocomotion.LookAngles;
                vector2.Y += m_headAngleY;
                vector2.X = MathUtils.Clamp(vector2.X, 0f - MathUtils.DegToRad(65f), MathUtils.DegToRad(65f));
                vector2.Y = MathUtils.Clamp(vector2.Y, 0f - MathUtils.DegToRad(55f), MathUtils.DegToRad(55f));
                Vector2 vector3 = Vector2.Zero;
                if (m_neckBone != null)
                {
                    vector3 = 0.6f * vector2;
                    vector2 = 0.4f * vector2;
                }
                if (m_feedFactor > 0f)
                {
                    float y = 0f - MathUtils.DegToRad(25f + 45f * SimplexNoise.OctavedNoise((float)m_subsystemTime.GameTime, 3f, 2, 2f, 0.75f));
                    vector2 = Vector2.Lerp(v2: new Vector2(0f, y), v1: vector2, f: m_feedFactor);
                    bool moveLegWhenFeeding = m_moveLegWhenFeeding;
                }
                if (m_buttFactor != 0f)
                {
                    float y2 = (0f - MathUtils.DegToRad(40f)) * MathUtils.Sin((float)Math.PI * 2f * MathUtils.Sigmoid(m_buttPhase, 4f));
                    vector2 = Vector2.Lerp(v2: new Vector2(0f, y2), v1: vector2, f: m_buttFactor);
                }
                if (m_bodyBone != null)
                {
                    if (m_componentCreature.ComponentLocomotion.LastSwimOrder.HasValue && !m_componentCreature.ComponentBody.StandingOnValue.HasValue)
                    {
                        SetBoneTransform(m_bodyBone.Index, Matrix.CreateFromYawPitchRoll(vector.X, 0f, 0f) * Matrix.CreateScale(m_bodyScale * GrowStage) * Matrix.CreateTranslation(position + new Vector3(0f, 0f - m_digInDepth, 0f)));
                    }
                    else
                    {
                        SetBoneTransform(m_bodyBone.Index, Matrix.CreateFromYawPitchRoll(vector.X, 0f, 0f) * Matrix.CreateScale(m_bodyScale * GrowStage) * Matrix.CreateTranslation(m_componentCreature.ComponentBody.Position + new Vector3(0f, base.Bob, 0f) + new Vector3(0f, 0f - m_digInDepth, 0f)));
                    }
                }
                // Definir un arreglo con pares (hueso, transformaci�n)
                var tailBones = new[]
                {
                    (Bone: m_tail1Bone, Transform: identity),
                    (Bone: m_tail2Bone, Transform: identity2),
                    (Bone: m_tail3Bone, Transform: identity2),
                    (Bone: m_tail4Bone, Transform: identity2),
                    (Bone: m_tail5Bone, Transform: identity2),
                    (Bone: m_tail6Bone, Transform: identity2),
                    (Bone: m_tail7Bone, Transform: identity2),
                    (Bone: m_tail8Bone, Transform: identity2)
                };

                foreach (var entry in tailBones)
                {
                    if (entry.Bone == null)
                        break; // Detenerse si un hueso es nulo

                    SetBoneTransform(entry.Bone.Index, entry.Transform);
                }

                if (this.m_headBone == null) return;

                // Aplicar transformaciones a huesos del cuello
                var neckBones = new[] { m_neckBone, m_neck1Bone, m_neck2Bone, m_neck3Bone, m_neck4Bone, m_neck5Bone };
                var neckTransform = Matrix.CreateRotationX(vector3.Y) * Matrix.CreateRotationZ(-vector3.X);

                foreach (var bone in neckBones)
                {
                    if (bone == null) break; // Detenerse si un hueso es nulo
                    this.SetBoneTransform(bone.Index, neckTransform);
                }

                // Aplicar transformaci�n a la cabeza
                var headTransform = Matrix.CreateRotationX(vector2.Y) * Matrix.CreateRotationZ(-vector2.X);
                this.SetBoneTransform(this.m_headBone.Index, headTransform);


                var legs = new[] { m_leg1Bone, m_leg2Bone, m_leg3Bone, m_leg4Bone };
                var angles = new[] { m_legAngle1, m_legAngle2, m_legAngle3, m_legAngle4 };
                for (int i = 0; i < legs.Length; i++)
                {
                    if (legs[i] != null)
                        SetBoneTransform(legs[i].Index, Matrix.CreateRotationX(angles[i]));
                }

                if (m_jawBone != null)
                {
                    float radians = 0f;
                    if (m_bitingPhase > 0f)
                    {
                        radians = (0f - MathUtils.DegToRad(30f)) * MathUtils.Sin((float)Math.PI * m_bitingPhase);
                    }
                    SetBoneTransform(m_jawBone.Index, Matrix.CreateRotationX(radians));
                }
                if (m_hasWings)
                {
                    SetBoneTransform(m_wing1Bone.Index, Matrix.CreateRotationY(num));
                    SetBoneTransform(m_wing2Bone.Index, Matrix.CreateRotationY(0f - num));
                }
            }
            else
            {
                float num32 = 1f - base.DeathPhase;
                float num33 = ((Vector3.Dot(m_componentFrame.Matrix.Right, base.DeathCauseOffset) > 0f) ? 1 : (-1));
                float num34 = m_componentCreature.ComponentBody.BoundingBox.Max.Y - m_componentCreature.ComponentBody.BoundingBox.Min.Y;
                if (m_bodyBone != null)
                {
                    if (m_componentCreature.Category.Equals(CreatureCategory.WaterOther) || m_componentCreature.Category.Equals(CreatureCategory.WaterPredator))
                    {
                        float num35 = m_componentCreature.ComponentBody.BoundingBox.Max.Y - m_componentCreature.ComponentBody.BoundingBox.Min.Y;
                        Vector3 position2 = m_componentCreature.ComponentBody.Position + 1f * num35 * base.DeathPhase * Vector3.UnitY;
                        SetBoneTransform(m_bodyBone.Index, Matrix.CreateFromYawPitchRoll(vector.X, 0f, (float)Math.PI * base.DeathPhase) * Matrix.CreateScale(m_bodyScale * GrowStage) * Matrix.CreateTranslation(position2));
                    }
                    else
                    {
                        SetBoneTransform(m_bodyBone.Index, Matrix.CreateTranslation(-0.5f * num34 * Vector3.UnitY * base.DeathPhase) * Matrix.CreateScale(m_bodyScale * GrowStage) * Matrix.CreateFromYawPitchRoll(vector.X, 0f, (float)Math.PI / 2f * base.DeathPhase * num33) * Matrix.CreateTranslation(0.2f * num34 * Vector3.UnitY * base.DeathPhase) * Matrix.CreateTranslation(position));
                    }
                }
                if (m_bodyBone1 != null)
                {
                    SetBoneTransform(m_bodyBone1.Index, Matrix.Identity);
                    if (m_bodyBone2 != null)
                    {
                        SetBoneTransform(m_bodyBone2.Index, Matrix.Identity);
                    }
                }
                if (m_bodyBone3 != null)
                {
                    SetBoneTransform(m_bodyBone3.Index, Matrix.Identity);
                    if (m_bodyBone4 != null)
                    {
                        SetBoneTransform(m_bodyBone4.Index, Matrix.Identity);
                    }
                }
                if (m_headBone != null)
                {
                    if (m_neckBone != null)
                    {
                        SetBoneTransform(m_neckBone.Index, Matrix.Identity);
                    }
                    SetBoneTransform(m_headBone.Index, Matrix.CreateRotationX(MathUtils.DegToRad(-50f) * base.DeathPhase));
                }
                if (m_leg1Bone != null)
                {
                    SetBoneTransform(m_leg1Bone.Index, Matrix.CreateRotationX(m_legAngle1 * num32));
                    SetBoneTransform(m_leg2Bone.Index, Matrix.CreateRotationX(m_legAngle2 * num32));
                    if (m_leg3Bone != null)
                    {
                        SetBoneTransform(m_leg3Bone.Index, Matrix.CreateRotationX(m_legAngle3 * num32));
                        SetBoneTransform(m_leg4Bone.Index, Matrix.CreateRotationX(m_legAngle4 * num32));
                    }
                }
                if (m_jawBone != null)
                {
                    SetBoneTransform(m_jawBone.Index, Matrix.Identity);
                }
                if (m_hasWings)
                {
                    SetBoneTransform(m_wing1Bone.Index, Matrix.CreateRotationY(num * num32));
                    SetBoneTransform(m_wing2Bone.Index, Matrix.CreateRotationY((0f - num) * num32));
                }
            }
            base.Opacity = ((m_componentCreature.ComponentSpawn.SpawnDuration > 0f) ? ((float)MathUtils.Saturate((m_subsystemGameInfo.TotalElapsedGameTime - m_componentCreature.ComponentSpawn.SpawnTime) / (double)m_componentCreature.ComponentSpawn.SpawnDuration)) : m_opacity);
            if (m_componentCreature.ComponentSpawn.DespawnTime.HasValue)
            {
                base.Opacity = MathUtils.Min(base.Opacity.Value, (float)MathUtils.Saturate(1.0 - (m_subsystemGameInfo.TotalElapsedGameTime - m_componentCreature.ComponentSpawn.DespawnTime.Value) / (double)m_componentCreature.ComponentSpawn.DespawnDuration));
            }
            base.DiffuseColor = m_bodyColor;
            base.DiffuseColor *= Vector3.Lerp(Vector3.One, new Vector3(1f, 0f, 0f), ((ComponentCreatureModel)this).m_injuryColorFactor);
            if (!base.Opacity.HasValue || base.Opacity.Value >= 1f)
            {
                RenderingMode = ModelRenderingMode.AlphaThreshold;
                return;
            }
            bool flag = m_componentCreature.ComponentBody.ImmersionFactor >= 1f;
            bool flag2 = ((ComponentModel)this).m_subsystemSky.ViewUnderWaterDepth > 0f;
            if (flag == flag2)
            {
                RenderingMode = ModelRenderingMode.TransparentAfterWater;
            }
            else
            {
                RenderingMode = ModelRenderingMode.TransparentBeforeWater;
            }
        }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            /*
              Textura brillante del modelo
              Usada si quieres agregarle ojos brillantes a tu modelo, actualmente no esta desarrollado el codigo.

             string value3 = valuesDictionary.GetValue<string>("GlowingOverride");
             m_glowingTexture = (string.IsNullOrEmpty(value3) ? null : ContentManager.Get<Texture2D>(value3));
            */


            m_bodyScale = valuesDictionary.GetValue("Scale", 1f);
            m_bodyColor = valuesDictionary.GetValue("Color", Vector3.One);
            base.Load(valuesDictionary, idToEntityMap);
            m_subsystemModelsRenderer = base.Project.FindSubsystem<SubsystemModelsRenderer>(throwOnError: true);
            m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(throwOnError: true);
            m_subsystemSoundMaterials = base.Project.FindSubsystem<SubsystemSoundMaterials>(throwOnError: true);
            m_walkAnimationSpeed = valuesDictionary.GetValue<float>("WalkAnimationSpeed");
            m_walkFrontLegsAngle = valuesDictionary.GetValue<float>("WalkFrontLegsAngle");
            m_walkHindLegsAngle = valuesDictionary.GetValue<float>("WalkHindLegsAngle");
            m_walkBobHeight = valuesDictionary.GetValue<float>("WalkBobHeight");
            m_canCanter = valuesDictionary.GetValue<bool>("CanCanter");
            m_canterLegsAngleFactor = valuesDictionary.GetValue<float>("CanterLegsAngleFactor");
            m_useCanterSound = valuesDictionary.GetValue<bool>("UseCanterSound");
            m_canTrot = valuesDictionary.GetValue<bool>("CanTrot");
            m_moveLegWhenFeeding = valuesDictionary.GetValue<bool>("MoveLegWhenFeeding");
            m_attackMode = valuesDictionary.GetValue("AttackMode", 0);
            m_hasVerticalTail = valuesDictionary.GetValue<bool>("HasVerticalTail");
            m_verticalMoveBody12 = valuesDictionary.GetValue("HasVerticalTail", m_hasVerticalTail);
            m_verticalMoveBody34 = valuesDictionary.GetValue("HasVerticalTail", m_hasVerticalTail);
            m_swimAnimationSpeed = valuesDictionary.GetValue<float>("SwimAnimationSpeed");
            m_flyAnimationSpeed = valuesDictionary.GetValue<float>("FlyAnimationSpeed");
            m_peckAnimationSpeed = valuesDictionary.GetValue<float>("PeckAnimationSpeed");
            m_opacity = valuesDictionary.GetValue("Opacity", 1f);
        }

        public override void SetModel(Model model)
        {
            base.SetModel(model);
            if (base.Model != null)
            {
                m_bodyBone = base.Model.FindBone("Body", throwIfNotFound: false);
                m_neckBone = base.Model.FindBone("Neck", throwIfNotFound: false);
                m_neck1Bone = base.Model.FindBone("Neck1", throwIfNotFound: false);
                m_neck2Bone = base.Model.FindBone("Neck2", throwIfNotFound: false);
                m_neck3Bone = base.Model.FindBone("Neck3", throwIfNotFound: false);
                m_neck4Bone = base.Model.FindBone("Neck4", throwIfNotFound: false);
                m_neck5Bone = base.Model.FindBone("Neck5", throwIfNotFound: false);
                m_headBone = base.Model.FindBone("Head", throwIfNotFound: false);
                m_jawBone = base.Model.FindBone("Jaw", throwIfNotFound: false);
                m_wing1Bone = base.Model.FindBone("Wing1", throwIfNotFound: false);
                m_wing2Bone = base.Model.FindBone("Wing2", throwIfNotFound: false);
                m_leg1Bone = base.Model.FindBone("Leg1", throwIfNotFound: false);
                m_leg2Bone = base.Model.FindBone("Leg2", throwIfNotFound: false);
                m_leg3Bone = base.Model.FindBone("Leg3", throwIfNotFound: false);
                m_leg4Bone = base.Model.FindBone("Leg4", throwIfNotFound: false);
                m_tail1Bone = base.Model.FindBone("Tail1", throwIfNotFound: false);
                m_tail2Bone = base.Model.FindBone("Tail2", throwIfNotFound: false);
                m_tail3Bone = base.Model.FindBone("Tail3", throwIfNotFound: false);
                m_tail4Bone = base.Model.FindBone("Tail4", throwIfNotFound: false);
                m_tail5Bone = base.Model.FindBone("Tail5", throwIfNotFound: false);
                m_tail6Bone = base.Model.FindBone("Tail6", throwIfNotFound: false);
                m_tail7Bone = base.Model.FindBone("Tail7", throwIfNotFound: false);
                m_tail8Bone = base.Model.FindBone("Tail8", throwIfNotFound: false);
                m_bodyBone1 = base.Model.FindBone("Body1", throwIfNotFound: false);
                m_bodyBone2 = base.Model.FindBone("Body2", throwIfNotFound: false);
                m_bodyBone3 = base.Model.FindBone("Body3", throwIfNotFound: false);
                m_bodyBone4 = base.Model.FindBone("Body4", throwIfNotFound: false);
            }
            else
            {
                m_bodyBone = null;
                m_neckBone = null;
                m_neck1Bone = null;
                m_neck2Bone = null;
                m_neck3Bone = null;
                m_neck4Bone = null;
                m_neck5Bone = null;
                m_headBone = null;
                m_jawBone = null;
                m_wing1Bone = null;
                m_wing2Bone = null;
                m_leg1Bone = null;
                m_leg2Bone = null;
                m_leg3Bone = null;
                m_leg4Bone = null;
                m_tail1Bone = null;
                m_tail2Bone = null;
                m_tail3Bone = null;
                m_tail4Bone = null;
                m_tail5Bone = null;
                m_tail6Bone = null;
                m_tail7Bone = null;
                m_tail8Bone = null;
                m_bodyBone1 = null;
                m_bodyBone2 = null;
                m_bodyBone3 = null;
                m_bodyBone4 = null;
            }
            m_hasWings = m_wing1Bone != null && m_wing2Bone != null;
        }

        public override Vector3 CalculateEyePosition()
        {
            Matrix matrix = m_componentCreature.ComponentBody.Matrix;
            Vector3 vector = matrix.Up * 0.95f * m_componentCreature.ComponentBody.BoxSize.Y;
            if (m_componentCreature.Category == CreatureCategory.WaterOther || m_componentCreature.Category == CreatureCategory.WaterPredator)
            {
                vector = matrix.Up * 1f * m_componentCreature.ComponentBody.BoxSize.Y;
            }
            return m_componentCreature.ComponentBody.Position + vector + matrix.Forward * 0.45f * m_componentCreature.ComponentBody.BoxSize.Z;
        }

        public override void AnimateCreature()
        {
            throw new NotImplementedException();
        }
    }
}
