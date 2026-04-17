using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewHumanModel : ComponentHumanModel, IUpdateable
	{
		// Factores de suavizado (coinciden con los valores por defecto del XDB)
		protected float m_smoothFactor = 0.18f;
		protected float m_animationResponsiveness = 10f;
		private float m_aimSmoothFactor = 0.3f;
		private float m_aimTransitionSpeed = 5f;

		// Variables para interpolación de ángulos
		private Vector2 m_targetHeadAngles;
		private Vector2 m_targetHandAngles1;
		private Vector2 m_targetHandAngles2;
		private Vector2 m_targetLegAngles1;
		private Vector2 m_targetLegAngles2;

		// Suavizado de movimiento
		private float m_smoothedMovementPhase;
		private float m_smoothedBob;

		// Suavizado de apuntar
		protected float m_smoothedAimHandAngle;
		protected float m_aimIntensity;
		private bool m_wasAiming;

		protected float m_animationTime;

		public override void Update(float dt)
		{
			m_animationTime += dt;
			base.Update(dt);

			float smoothSpeed = MathUtils.Min(m_animationResponsiveness * dt, 0.85f);
			float aimSmoothSpeed = MathUtils.Min(m_aimTransitionSpeed * dt, 0.9f);

			m_smoothedMovementPhase = MathUtils.Lerp(m_smoothedMovementPhase, base.MovementAnimationPhase, smoothSpeed * 1.2f);
			m_smoothedBob = MathUtils.Lerp(m_smoothedBob, base.Bob, smoothSpeed * 1.5f);

			float targetAimAngle = base.m_aimHandAngle;
			m_smoothedAimHandAngle = MathUtils.Lerp(m_smoothedAimHandAngle, targetAimAngle, aimSmoothSpeed * 0.7f);

			bool isAiming = Math.Abs(targetAimAngle) > 0.001f;
			float targetAimIntensity = isAiming ? 1f : 0f;
			m_aimIntensity = MathUtils.Lerp(m_aimIntensity, targetAimIntensity, aimSmoothSpeed * 0.6f);
			m_wasAiming = isAiming;

			float bodySmoothSpeed = smoothSpeed * 0.8f;
			this.m_headAngles = Vector2.Lerp(this.m_headAngles, m_targetHeadAngles, bodySmoothSpeed);
			this.m_handAngles1 = Vector2.Lerp(this.m_handAngles1, m_targetHandAngles1, bodySmoothSpeed);
			this.m_handAngles2 = Vector2.Lerp(this.m_handAngles2, m_targetHandAngles2, bodySmoothSpeed);
			this.m_legAngles1 = Vector2.Lerp(this.m_legAngles1, m_targetLegAngles1, bodySmoothSpeed);
			this.m_legAngles2 = Vector2.Lerp(this.m_legAngles2, m_targetLegAngles2, bodySmoothSpeed);
		}

		public override void Animate()
		{
			bool flag = false;
			bool skip = false;
			ModsManager.HookAction("OnModelAnimate", delegate (ModLoader loader)
			{
				loader.OnModelAnimate(this, out skip);
				flag |= skip;
				return false;
			});

			if (flag)
			{
				base.Animate();
				return;
			}

			base.Animate();

			if (this.m_bodyBone == null || this.m_headBone == null ||
				this.m_hand1Bone == null || this.m_hand2Bone == null ||
				this.m_leg1Bone == null || this.m_leg2Bone == null)
			{
				return;
			}

			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			Vector3 vector = this.m_componentCreature.ComponentBody.Rotation.ToYawPitchRoll();

			if (this.OnAnimate != null && this.OnAnimate())
			{
				return;
			}

			if (this.m_lieDownFactorModel == 0f)
			{
				ComponentMount componentMount = (this.m_componentRider != null) ? this.m_componentRider.Mount : null;
				float num = MathF.Sin(6.2831855f * m_smoothedMovementPhase);
				position.Y += m_smoothedBob * 0.8f;
				vector.X += this.m_headingOffset;

				float num2 = (float)MathUtils.Remainder(0.75 * this.m_subsystemGameInfo.TotalElapsedGameTime + (double)(this.GetHashCode() & 65535), 10000.0);
				float noiseScale = 0.08f;

				float targetHeadX = MathUtils.Clamp(
					MathUtils.Lerp(-noiseScale, noiseScale, SimplexNoise.Noise(1.02f * num2 - 100f)) +
					this.m_componentCreature.ComponentLocomotion.LookAngles.X +
					0.4f * this.m_componentCreature.ComponentLocomotion.LastTurnOrder.X +
					this.m_headingOffset,
					-MathUtils.DegToRad(70f), MathUtils.DegToRad(70f));

				float targetHeadY = MathUtils.Clamp(
					MathUtils.Lerp(-noiseScale, noiseScale, SimplexNoise.Noise(0.96f * num2 - 200f)) +
					this.m_componentCreature.ComponentLocomotion.LookAngles.Y,
					-MathUtils.DegToRad(40f), MathUtils.DegToRad(40f));

				m_targetHeadAngles = new Vector2(targetHeadX, targetHeadY);

				float num3 = 0f, y2 = 0f, x2 = 0f, y3 = 0f;
				float num4 = 0f, num5 = 0f, num6 = 0f, num7 = 0f;

				if (componentMount != null)
				{
					if (componentMount.Entity.ValuesDictionary.DatabaseObject.Name == "Boat")
					{
						position.Y -= 0.15f;
						vector.X += 3.1415927f;
						num4 = 0.3f; num6 = 0.3f; num5 = 0.15f; num7 = -0.15f;
						num3 = 0.9f; x2 = 0.9f; y2 = 0.15f; y3 = -0.15f;
					}
					else
					{
						num4 = 0.4f; num6 = 0.4f; num5 = 0.12f; num7 = -0.12f;
						y2 = 0.45f; y3 = -0.45f;
					}
				}
				else if (this.m_componentCreature.ComponentLocomotion.IsCreativeFlyEnabled)
				{
					float num8 = (this.m_componentCreature.ComponentLocomotion.LastWalkOrder != null) ?
						MathUtils.Min(0.02f * this.m_componentCreature.ComponentBody.Velocity.XZ.LengthSquared(), 0.4f) : 0f;
					num3 = -0.08f - num8;
					x2 = num3;
					y2 = MathUtils.Lerp(0f, 0.2f, SimplexNoise.Noise(1.07f * num2 + 400f));
					y3 = 0f - MathUtils.Lerp(0f, 0.2f, SimplexNoise.Noise(0.93f * num2 + 500f));
				}
				else if (m_smoothedMovementPhase != 0f)
				{
					float walkMultiplier = 0.35f;
					num4 = -walkMultiplier * num;
					num6 = walkMultiplier * num;
					float legSwingMultiplier = 0.5f;
					num3 = this.m_walkLegsAngle * num * legSwingMultiplier;
					x2 = 0f - num3;
					y2 = 0.05f * MathF.Abs(num);
					y3 = -0.05f * MathF.Abs(num);
				}

				float num9 = 0f;
				if (this.m_componentMiner != null)
				{
					float num10 = MathF.Sin(MathF.Sqrt(this.m_componentMiner.PokingPhase) * 3.1415927f);
					num9 = ((this.m_componentMiner.ActiveBlockValue == 0) ? (0.8f * num10) : (0.25f + 0.8f * num10));
				}

				float num11 = (this.m_punchPhase != 0f) ?
					((0f - MathUtils.DegToRad(70f)) * MathF.Sin(6.2831855f * MathUtils.Sigmoid(this.m_punchPhase, 2.5f))) : 0f;
				float num12 = ((this.m_punchCounter & 1) == 0) ? num11 : 0f;
				float num13 = ((this.m_punchCounter & 1) != 0) ? num11 : 0f;

				float num14 = 0f, num15 = 0f, num16 = 0f, num17 = 0f;
				if (this.m_rowLeft || this.m_rowRight)
				{
					float num18 = 0.5f * (float)Math.Sin(6.91150426864624 * this.m_subsystemTime.GameTime);
					float num19 = 0.15f + 0.15f * (float)Math.Cos(6.91150426864624 * (this.m_subsystemTime.GameTime + 0.5));
					if (this.m_rowLeft) { num14 = num18; num15 = num19; }
					if (this.m_rowRight) { num16 = num18; num17 = 0f - num19; }
				}

				float num20 = 0f, num21 = 0f, num22 = 0f, num23 = 0f;
				if (m_aimIntensity > 0.001f)
				{
					float easeInOut = m_aimIntensity * m_aimIntensity * (3f - 2f * m_aimIntensity);
					num20 = 1.2f * easeInOut;
					num21 = -0.5f * easeInOut;
					num22 = m_smoothedAimHandAngle * easeInOut * 0.8f;
					num23 = 0f;
				}

				float num24 = (float)((!this.m_componentCreature.ComponentLocomotion.IsCreativeFlyEnabled) ? 1 : 4);
				float handNoiseScale = 0.03f;

				num4 += MathUtils.Lerp(-handNoiseScale, handNoiseScale, SimplexNoise.Noise(num2)) + num12 + num14 + num20;
				num5 += MathUtils.Lerp(0f, num24 * 0.08f, SimplexNoise.Noise(1.1f * num2 + 100f)) + num15 + num21;
				num6 += num9 + MathUtils.Lerp(-handNoiseScale, handNoiseScale, SimplexNoise.Noise(0.9f * num2 + 200f)) + num13 + num16 + num22;
				num7 += 0f - MathUtils.Lerp(0f, num24 * 0.08f, SimplexNoise.Noise(1.05f * num2 + 300f)) + num17 + num23;

				m_targetHandAngles1 = new Vector2(num4, num5);
				m_targetHandAngles2 = new Vector2(num6, num7);
				m_targetLegAngles1 = new Vector2(num3, y2);
				m_targetLegAngles2 = new Vector2(x2, y3);

				float crouchFactor = this.m_componentCreature.ComponentBody.CrouchFactor;
				if (crouchFactor > 0.95f)
				{
					m_targetLegAngles1 *= 0.7f;
					m_targetLegAngles2 *= 0.7f;
				}
				else if (crouchFactor > 0.5f)
				{
					float crouchScale = MathUtils.Lerp(1f, 0.7f, (crouchFactor - 0.5f) / 0.45f);
					m_targetLegAngles1 *= crouchScale;
					m_targetLegAngles2 *= crouchScale;
				}

				float f = MathUtils.Sigmoid(this.m_componentCreature.ComponentBody.CrouchFactor, 3f);
				Vector3 position2 = new Vector3(position.X, position.Y - MathUtils.Lerp(0f, 0.6f, f), position.Z);
				Vector3 position3 = new Vector3(0f, MathUtils.Lerp(0f, 6f, f), MathUtils.Lerp(0f, 25f, f));
				Vector3 scale = new Vector3(1f, 1f, MathUtils.Lerp(1f, 0.6f, f));

				this.SetBoneTransform(this.m_bodyBone.Index, new Matrix?(Matrix.CreateRotationY(vector.X) * Matrix.CreateTranslation(position2)));
				this.SetBoneTransform(this.m_headBone.Index, new Matrix?(Matrix.CreateRotationX(this.m_headAngles.Y) * Matrix.CreateRotationZ(0f - this.m_headAngles.X)));
				this.SetBoneTransform(this.m_hand1Bone.Index, new Matrix?(Matrix.CreateRotationY(this.m_handAngles1.Y) * Matrix.CreateRotationX(this.m_handAngles1.X)));
				this.SetBoneTransform(this.m_hand2Bone.Index, new Matrix?(Matrix.CreateRotationY(this.m_handAngles2.Y) * Matrix.CreateRotationX(this.m_handAngles2.X)));
				this.SetBoneTransform(this.m_leg1Bone.Index, new Matrix?(Matrix.CreateRotationY(this.m_legAngles1.Y) * Matrix.CreateRotationX(this.m_legAngles1.X) * Matrix.CreateTranslation(position3) * Matrix.CreateScale(scale)));
				this.SetBoneTransform(this.m_leg2Bone.Index, new Matrix?(Matrix.CreateRotationY(this.m_legAngles2.Y) * Matrix.CreateRotationX(this.m_legAngles2.X) * Matrix.CreateTranslation(position3) * Matrix.CreateScale(scale)));

				return;
			}

			// Código para estado acostado (sin cambios)
			float num25 = MathUtils.Max(base.DeathPhase, this.m_lieDownFactorModel);
			float num26 = 1f - num25;
			Vector3 position4 = position + num25 * 0.5f * this.m_componentCreature.ComponentBody.BoxSize.Y * Vector3.Normalize(this.m_componentCreature.ComponentBody.Matrix.Forward * new Vector3(1f, 0f, 1f)) + num25 * Vector3.UnitY * this.m_componentCreature.ComponentBody.BoxSize.Z * 0.1f;

			this.SetBoneTransform(this.m_bodyBone.Index, new Matrix?(Matrix.CreateFromYawPitchRoll(vector.X, 1.5707964f * num25, 0f) * Matrix.CreateTranslation(position4)));
			this.SetBoneTransform(this.m_headBone.Index, new Matrix?(Matrix.Identity));
			this.SetBoneTransform(this.m_hand1Bone.Index, new Matrix?(Matrix.CreateRotationY(this.m_handAngles1.Y * num26) * Matrix.CreateRotationX(this.m_handAngles1.X * num26)));
			this.SetBoneTransform(this.m_hand2Bone.Index, new Matrix?(Matrix.CreateRotationY(this.m_handAngles2.Y * num26) * Matrix.CreateRotationX(this.m_handAngles2.X * num26)));
			this.SetBoneTransform(this.m_leg1Bone.Index, new Matrix?(Matrix.CreateRotationY(this.m_legAngles1.Y * num26) * Matrix.CreateRotationX(this.m_legAngles1.X * num26)));
			this.SetBoneTransform(this.m_leg2Bone.Index, new Matrix?(Matrix.CreateRotationY(this.m_legAngles2.Y * num26) * Matrix.CreateRotationX(this.m_legAngles2.X * num26)));
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			if (valuesDictionary.ContainsKey("SmoothFactor"))
				m_smoothFactor = valuesDictionary.GetValue<float>("SmoothFactor");
			if (valuesDictionary.ContainsKey("AnimationResponsiveness"))
				m_animationResponsiveness = valuesDictionary.GetValue<float>("AnimationResponsiveness");
			if (valuesDictionary.ContainsKey("AimSmoothFactor"))
				m_aimSmoothFactor = valuesDictionary.GetValue<float>("AimSmoothFactor");
			if (valuesDictionary.ContainsKey("AimTransitionSpeed"))
				m_aimTransitionSpeed = valuesDictionary.GetValue<float>("AimTransitionSpeed");

			m_targetHeadAngles = Vector2.Zero;
			m_targetHandAngles1 = Vector2.Zero;
			m_targetHandAngles2 = Vector2.Zero;
			m_targetLegAngles1 = Vector2.Zero;
			m_targetLegAngles2 = Vector2.Zero;
			m_smoothedAimHandAngle = 0f;
			m_aimIntensity = 0f;
			m_wasAiming = false;
			m_smoothedMovementPhase = 0f;
			m_smoothedBob = 0f;
			m_animationTime = 0f;
		}

		// Métodos para ajustar parámetros dinámicamente
		public void SetSmoothFactor(float factor) => m_smoothFactor = MathUtils.Clamp(factor, 0.08f, 0.35f);
		public void SetAnimationResponsiveness(float responsiveness) => m_animationResponsiveness = MathUtils.Clamp(responsiveness, 6f, 25f);
		public void SetAimSmoothness(float smoothness)
		{
			m_aimSmoothFactor = MathUtils.Clamp(smoothness, 0.15f, 0.6f);
			m_aimTransitionSpeed = MathUtils.Clamp(12f - (smoothness * 20f), 2f, 10f);
		}
	}
}
