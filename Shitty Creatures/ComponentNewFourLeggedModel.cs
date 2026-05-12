using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewFourLeggedModel : ComponentFourLeggedModel
	{
		// Nuevos parámetros de suavidad (Valores por defecto equivalentes al original)
		public float m_legSmoothness = 12f;
		public float m_headSmoothness = 12f;
		public float m_bobSmoothness = 12f;
		public float m_feedSmoothness = 2f;
		public float m_attackSmoothness = 4f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargamos los valores personalizados. Si no existen en el XML, usan los originales.
			m_legSmoothness = valuesDictionary.GetValue<float>("LegSmoothness", 12f);
			m_headSmoothness = valuesDictionary.GetValue<float>("HeadSmoothness", 12f);
			m_bobSmoothness = valuesDictionary.GetValue<float>("BobSmoothness", 12f);
			m_feedSmoothness = valuesDictionary.GetValue<float>("FeedSmoothness", 2f);
			m_attackSmoothness = valuesDictionary.GetValue<float>("AttackSmoothness", 4f);
		}

		public override void Update(float dt)
		{
			float footstepsPhase = this.m_footstepsPhase;
			float num = this.m_componentCreature.ComponentLocomotion.SlipSpeed ?? Vector3.Dot(this.m_componentCreature.ComponentBody.Velocity, this.m_componentCreature.ComponentBody.Matrix.Forward);

			if (this.m_canCanter && num > 0.7f * this.m_componentCreature.ComponentLocomotion.WalkSpeed)
			{
				this.m_gait = ComponentFourLeggedModel.Gait.Canter;
				base.MovementAnimationPhase += num * dt * 0.7f * this.m_walkAnimationSpeed;
				this.m_footstepsPhase += 0.7f * this.m_walkAnimationSpeed * num * dt;
			}
			else if (this.m_canTrot && num > 0.5f * this.m_componentCreature.ComponentLocomotion.WalkSpeed)
			{
				this.m_gait = ComponentFourLeggedModel.Gait.Trot;
				base.MovementAnimationPhase += num * dt * this.m_walkAnimationSpeed;
				this.m_footstepsPhase += 1.25f * this.m_walkAnimationSpeed * num * dt;
			}
			else if (MathF.Abs(num) > 0.2f)
			{
				this.m_gait = ComponentFourLeggedModel.Gait.Walk;
				base.MovementAnimationPhase += num * dt * this.m_walkAnimationSpeed;
				this.m_footstepsPhase += 1.25f * this.m_walkAnimationSpeed * num * dt;
			}
			else
			{
				this.m_gait = ComponentFourLeggedModel.Gait.Walk;
				base.MovementAnimationPhase = 0f;
				this.m_footstepsPhase = 0f;
			}

			float num2 = 0f;
			if (this.m_gait == ComponentFourLeggedModel.Gait.Canter)
			{
				num2 = (0f - this.m_walkBobHeight) * 1.5f * MathF.Sin(6.2831855f * base.MovementAnimationPhase);
			}
			else if (this.m_gait == ComponentFourLeggedModel.Gait.Trot)
			{
				num2 = this.m_walkBobHeight * 1.5f * MathUtils.Sqr(MathF.Sin(6.2831855f * base.MovementAnimationPhase));
			}
			else if (this.m_gait == ComponentFourLeggedModel.Gait.Walk)
			{
				num2 = (0f - this.m_walkBobHeight) * MathUtils.Sqr(MathF.Sin(6.2831855f * base.MovementAnimationPhase));
			}

			// --- CAMBIO 1: Suavidad para el Bob ( cabeceo vertical del cuerpo ) ---
			float num3 = MathUtils.Min(m_bobSmoothness * this.m_subsystemTime.GameTimeDelta, 1f);
			base.Bob += num3 * (num2 - base.Bob);

			if (this.m_gait == ComponentFourLeggedModel.Gait.Canter && this.m_useCanterSound)
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

			// --- CAMBIO 2: Suavidad para Agacharse a Comer ---
			this.m_feedFactor = (base.FeedOrder ? MathUtils.Min(this.m_feedFactor + m_feedSmoothness * dt, 1f) : MathUtils.Max(this.m_feedFactor - m_feedSmoothness * dt, 0f));

			base.IsAttackHitMoment = false;
			if (base.AttackOrder)
			{
				// --- CAMBIO 3: Suavidad para Atacar (Coz) ---
				this.m_buttFactor = MathUtils.Min(this.m_buttFactor + m_attackSmoothness * dt, 1f);
				float buttPhase = this.m_buttPhase;
				this.m_buttPhase = MathUtils.Remainder(this.m_buttPhase + dt * 2f, 1f);
				if (buttPhase < 0.5f && this.m_buttPhase >= 0.5f)
				{
					base.IsAttackHitMoment = true;
				}
			}
			else
			{
				this.m_buttFactor = MathUtils.Max(this.m_buttFactor - m_attackSmoothness * dt, 0f);
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

			// NOTA: No podemos llamar a base.Update(dt) porque eso ejecutaría el código original con los valores hardcodeados.
			// Por lo tanto, replicamos la lógica vital de ComponentCreatureModel.Update aquí:
			if (this.LookRandomOrder)
			{
				Matrix matrix = this.m_componentCreature.ComponentBody.Matrix;
				Vector3 v = Vector3.Normalize(this.m_randomLookPoint - this.m_componentCreature.ComponentCreatureModel.EyePosition);
				if (this.m_random.Float(0f, 1f) < 0.25f * dt || Vector3.Dot(matrix.Forward, v) < 0.2f)
				{
					float s = this.m_random.Float(-5f, 5f);
					float s2 = this.m_random.Float(-1f, 1f);
					float s3 = this.m_random.Float(3f, 8f);
					this.m_randomLookPoint = this.m_componentCreature.ComponentCreatureModel.EyePosition + s3 * matrix.Forward + s2 * matrix.Up + s * matrix.Right;
				}
				this.LookAtOrder = new Vector3?(this.m_randomLookPoint);
			}
			if (this.LookAtOrder != null)
			{
				Vector3 forward = this.m_componentCreature.ComponentBody.Matrix.Forward;
				Vector3 vector = this.LookAtOrder.Value - this.m_componentCreature.ComponentCreatureModel.EyePosition;
				float x = Vector2.Angle(new Vector2(forward.X, forward.Z), new Vector2(vector.X, vector.Z));
				float y = MathF.Asin(0.99f * Vector3.Normalize(vector).Y);
				this.m_componentCreature.ComponentLocomotion.LookOrder = new Vector2(x, y) - this.m_componentCreature.ComponentLocomotion.LookAngles;
			}
			if (this.HeadShakeOrder > 0f)
			{
				this.HeadShakeOrder = MathUtils.Max(this.HeadShakeOrder - dt, 0f);
				float numShake = 1f * MathUtils.Saturate(4f * this.HeadShakeOrder);
				this.m_componentCreature.ComponentLocomotion.LookOrder = new Vector2(numShake * (float)Math.Sin(16.0 * this.m_subsystemTime.GameTime + (double)(0.01f * (float)this.GetHashCode())), 0f) - this.m_componentCreature.ComponentLocomotion.LookAngles;
			}
			if (this.m_componentCreature.ComponentHealth.Health == 0f)
			{
				this.DeathPhase = MathUtils.Min(this.DeathPhase + 3f * dt, 1f);
			}
			this.m_eyePosition = null;
			this.m_eyeRotation = null;
			this.LookRandomOrder = false;
			this.LookAtOrder = null;
		}

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
					if (this.m_gait == ComponentFourLeggedModel.Gait.Canter)
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
					else if (this.m_gait == ComponentFourLeggedModel.Gait.Trot)
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

				// --- CAMBIO 4: Separación de suavidad para patas y cabeza ---
				float legLerp = MathUtils.Min(m_legSmoothness * this.m_subsystemTime.GameTimeDelta, 1f);
				float headLerp = MathUtils.Min(m_headSmoothness * this.m_subsystemTime.GameTimeDelta, 1f);

				this.m_legAngle1 += legLerp * (num - this.m_legAngle1);
				this.m_legAngle2 += legLerp * (num2 - this.m_legAngle2);
				this.m_legAngle3 += legLerp * (num3 - this.m_legAngle3);
				this.m_legAngle4 += legLerp * (num4 - this.m_legAngle4);
				this.m_headAngleY += headLerp * (num5 - this.m_headAngleY);

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
				}
				if (this.m_buttFactor != 0f)
				{
					float y2 = (0f - MathUtils.DegToRad(40f)) * MathF.Sin(6.2831855f * MathUtils.Sigmoid(this.m_buttPhase, 4f));
					Vector2 v = new Vector2(0f, y2);
					vector2 = Vector2.Lerp(vector2, v, this.m_buttFactor);
				}

				this.SetBoneTransform(this.m_bodyBone.Index, new Matrix?(Matrix.CreateRotationY(vector.X) * Matrix.CreateTranslation(position.X, position.Y + base.Bob, position.Z)));
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

			// Animación de muerte (se respeta exactamente igual que el original)
			float num19 = 1f - base.DeathPhase;
			float num20 = (float)((Vector3.Dot(this.m_componentFrame.Matrix.Right, base.DeathCauseOffset) > 0f) ? 1 : -1);
			float num21 = this.m_componentCreature.ComponentBody.BoundingBox.Max.Y - this.m_componentCreature.ComponentBody.BoundingBox.Min.Y;
			this.SetBoneTransform(this.m_bodyBone.Index, new Matrix?(Matrix.CreateTranslation(-0.5f * num21 * Vector3.UnitY * base.DeathPhase) * Matrix.CreateFromYawPitchRoll(vector.X, 0f, 1.5707964f * base.DeathPhase * num20) * Matrix.CreateTranslation(0.2f * num21 * Vector3.UnitY * base.DeathPhase) * Matrix.CreateTranslation(position)));
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
	}
}
