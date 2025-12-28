using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewFourLeggedModel : ComponentCreatureModel
	{
		public override float AttackPhase
		{
			get { return this.m_buttPhase; }
			set { this.m_buttPhase = value; }
		}

		public override float AttackFactor
		{
			get { return this.m_buttFactor; }
			set { this.m_buttFactor = value; }
		}

		public override void Update(float dt)
		{
			float footstepsPhase = this.m_footstepsPhase;
			float num = this.m_componentCreature.ComponentLocomotion.SlipSpeed ??
						Vector3.Dot(this.m_componentCreature.ComponentBody.Velocity,
						this.m_componentCreature.ComponentBody.Matrix.Forward);

			// Determinar gait objetivo - CORREGIDO: usar el enum local
			Gait targetGait = Gait.Walk;
			float animationSpeedMultiplier = 1f;

			if (this.m_canCanter && num > 0.7f * this.m_componentCreature.ComponentLocomotion.WalkSpeed)
			{
				targetGait = Gait.Canter;
				animationSpeedMultiplier = 0.7f;
			}
			else if (this.m_canTrot && num > 0.5f * this.m_componentCreature.ComponentLocomotion.WalkSpeed)
			{
				targetGait = Gait.Trot;
				animationSpeedMultiplier = 1f;
			}
			else if (MathF.Abs(num) > 0.2f)
			{
				targetGait = Gait.Walk;
				animationSpeedMultiplier = 1f;
			}

			// Transición suave entre gaits
			if (this.m_targetGait != targetGait)
			{
				this.m_targetGait = targetGait;
				this.m_gaitTransitionFactor = 0f;
			}

			// Actualizar factor de transición
			if (this.m_gaitTransitionFactor < 1f)
			{
				this.m_gaitTransitionFactor = MathUtils.Min(
					this.m_gaitTransitionFactor + dt * this.m_gaitTransitionSpeed, 1f);
			}

			// Interpolar suavemente entre gaits - CORREGIDO: crear función SmoothStep de un parámetro
			float transition = this.SmoothStep(this.m_gaitTransitionFactor);

			if (transition < 1f)
			{
				// Durante transición, interpolar parámetros
				this.m_gait = this.m_targetGait;
			}
			else
			{
				this.m_gait = this.m_targetGait;
			}

			// Calcular intensidad de paso basada en aceleración
			float currentSpeed = num;
			float acceleration = MathF.Abs(currentSpeed - m_lastSpeed) / Math.Max(dt, 0.001f);
			m_lastSpeed = currentSpeed;

			m_stepIntensity = MathUtils.Lerp(m_stepIntensity,
				MathUtils.Clamp(1f + acceleration * 0.5f, 0.5f, 1.5f),
				dt * 8f);

			// Añadir balanceo natural de la cabeza
			if (MathF.Abs(num) > 0.1f)
			{
				m_headSwayPhase += dt * num * 0.5f;
				m_headSwayAmount = MathUtils.Lerp(m_headSwayAmount,
					MathUtils.DegToRad(3f), dt * 4f);
			}
			else
			{
				m_headSwayAmount = MathUtils.Lerp(m_headSwayAmount, 0f, dt * 4f);
			}

			// Actualizar animaciones según gait actual con transición suave - CORREGIDO: usar enum local
			if (this.m_gait == Gait.Canter)
			{
				base.MovementAnimationPhase += num * dt * 0.7f * this.m_walkAnimationSpeed * transition;
				this.m_footstepsPhase += 0.7f * this.m_walkAnimationSpeed * num * dt * transition;
			}
			else if (this.m_gait == Gait.Trot)
			{
				base.MovementAnimationPhase += num * dt * this.m_walkAnimationSpeed * transition;
				this.m_footstepsPhase += 1.25f * this.m_walkAnimationSpeed * num * dt * transition;
			}
			else if (MathF.Abs(num) > 0.2f)
			{
				this.m_gait = Gait.Walk;
				base.MovementAnimationPhase += num * dt * this.m_walkAnimationSpeed * transition;
				this.m_footstepsPhase += 1.25f * this.m_walkAnimationSpeed * num * dt * transition;
			}
			else
			{
				this.m_gait = Gait.Walk;
				base.MovementAnimationPhase = 0f;
				this.m_footstepsPhase = 0f;
			}

			// Calcular bobbing con interpolación suave
			float targetBob = 0f;
			if (this.m_gait == Gait.Canter)
			{
				targetBob = (0f - this.m_walkBobHeight) * 1.5f * MathF.Sin(6.2831855f * base.MovementAnimationPhase);
			}
			else if (this.m_gait == Gait.Trot)
			{
				targetBob = this.m_walkBobHeight * 1.5f * MathUtils.Sqr(MathF.Sin(6.2831855f * base.MovementAnimationPhase));
			}
			else if (this.m_gait == Gait.Walk)
			{
				targetBob = (0f - this.m_walkBobHeight) * MathUtils.Sqr(MathF.Sin(6.2831855f * base.MovementAnimationPhase));
			}

			float bobTransition = MathUtils.Min(12f * this.m_subsystemTime.GameTimeDelta, 1f);
			bobTransition = this.SmoothStep(bobTransition);
			base.Bob += bobTransition * (targetBob - base.Bob);

			// Sonidos de pasos con intensidad dinámica - CORREGIDO: usar enum local
			if (this.m_gait == Gait.Canter && this.m_useCanterSound)
			{
				float num4 = MathF.Floor(this.m_footstepsPhase);
				if (this.m_footstepsPhase > num4 && footstepsPhase <= num4)
				{
					string footstepSoundMaterialName = this.m_subsystemSoundMaterials.GetFootstepSoundMaterialName(this.m_componentCreature);
					if (!string.IsNullOrEmpty(footstepSoundMaterialName) && footstepSoundMaterialName != "Water")
					{
						float volume = 0.75f * m_stepIntensity;
						float pitch = this.m_random.Float(-0.2f, 0.1f);
						this.m_subsystemAudio.PlayRandomSound("Audio/Footsteps/CanterDirt",
							volume, pitch, this.m_componentCreature.ComponentBody.Position,
							3f * m_stepIntensity, true);
					}
				}
			}
			else
			{
				float num5 = MathF.Floor(this.m_footstepsPhase);
				if (this.m_footstepsPhase > num5 && footstepsPhase <= num5)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayFootstepSound(1f * m_stepIntensity);
				}
			}

			// Animación de alimentación
			this.m_feedFactor = (base.FeedOrder ?
				MathUtils.Min(this.m_feedFactor + 2f * dt, 1f) :
				MathUtils.Max(this.m_feedFactor - 2f * dt, 0f));

			// Animación de ataque
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
			base.Update(dt);
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

				if (base.MovementAnimationPhase != 0f &&
					(this.m_componentCreature.ComponentBody.StandingOnValue != null ||
					 this.m_componentCreature.ComponentBody.ImmersionFactor > 0f))
				{
					// CORREGIDO: usar enum local
					if (this.m_gait == Gait.Canter)
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
					else if (this.m_gait == Gait.Trot)
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

				// Interpolación mejorada con easing
				float interpolationSpeed = 16f;
				float t = MathUtils.Min(interpolationSpeed * this.m_subsystemTime.GameTimeDelta, 1f);
				t = this.SmoothStep(t); // CORREGIDO: usar función local

				this.m_legAngle1 += t * (num - this.m_legAngle1);
				this.m_legAngle2 += t * (num2 - this.m_legAngle2);
				this.m_legAngle3 += t * (num3 - this.m_legAngle3);
				this.m_legAngle4 += t * (num4 - this.m_legAngle4);
				this.m_headAngleY += t * (num5 - this.m_headAngleY);

				Vector2 vector2 = this.m_componentCreature.ComponentLocomotion.LookAngles;
				vector2.Y += this.m_headAngleY;

				// Añadir balanceo natural a la cabeza
				float headSway = MathF.Sin(m_headSwayPhase * 6.2831855f) * m_headSwayAmount;
				vector2.Y += headSway;

				vector2.X = Math.Clamp(vector2.X, 0f - MathUtils.DegToRad(65f), MathUtils.DegToRad(65f));
				vector2.Y = Math.Clamp(vector2.Y, 0f - MathUtils.DegToRad(55f), MathUtils.DegToRad(55f));

				Vector2 vector3 = Vector2.Zero;
				if (this.m_neckBone != null)
				{
					vector3 = 0.6f * vector2;
					vector2 = 0.4f * vector2;
				}

				// Animación de alimentación
				if (this.m_feedFactor > 0f)
				{
					float y = 0f - MathUtils.DegToRad(25f + 45f * SimplexNoise.OctavedNoise(
						(float)this.m_subsystemTime.GameTime, 3f, 2, 2f, 0.75f, false));
					Vector2 v = new Vector2(0f, y);
					vector2 = Vector2.Lerp(vector2, v, this.m_feedFactor);
				}

				// Animación de ataque
				if (this.m_buttFactor != 0f)
				{
					float y2 = (0f - MathUtils.DegToRad(40f)) * MathF.Sin(6.2831855f * MathUtils.Sigmoid(this.m_buttPhase, 4f));
					Vector2 v = new Vector2(0f, y2);
					vector2 = Vector2.Lerp(vector2, v, this.m_buttFactor);
				}

				// Transformación del cuerpo con balanceo natural durante movimiento
				float currentSpeed = Vector3.Dot(this.m_componentCreature.ComponentBody.Velocity,
					this.m_componentCreature.ComponentBody.Matrix.Forward);
				float transition = this.SmoothStep(this.m_gaitTransitionFactor); // CORREGIDO: usar función local

				// CORREGIDO: usar enum local
				if ((this.m_gait != Gait.Walk || MathF.Abs(currentSpeed) > 0.3f) && transition > 0.5f)
				{
					float bodyRoll = MathUtils.DegToRad(2f) * MathF.Sin(6.2831855f * base.MovementAnimationPhase + 1.5707964f);
					float bodyYaw = MathUtils.DegToRad(1f) * MathF.Sin(6.2831855f * base.MovementAnimationPhase);

					Matrix bodyTransform =
						Matrix.CreateRotationZ(bodyRoll * m_bodySwayAmount) *
						Matrix.CreateRotationY(bodyYaw * m_bodySwayAmount + vector.X) *
						Matrix.CreateTranslation(position.X, position.Y + base.Bob, position.Z);

					this.SetBoneTransform(this.m_bodyBone.Index, new Matrix?(bodyTransform));
				}
				else
				{
					this.SetBoneTransform(this.m_bodyBone.Index,
						new Matrix?(Matrix.CreateRotationY(vector.X) *
						Matrix.CreateTranslation(position.X, position.Y + base.Bob, position.Z)));
				}

				this.SetBoneTransform(this.m_headBone.Index,
					new Matrix?(Matrix.CreateRotationX(vector2.Y) * Matrix.CreateRotationZ(0f - vector2.X)));

				if (this.m_neckBone != null)
				{
					this.SetBoneTransform(this.m_neckBone.Index,
						new Matrix?(Matrix.CreateRotationX(vector3.Y) * Matrix.CreateRotationZ(0f - vector3.X)));
				}

				this.SetBoneTransform(this.m_leg1Bone.Index, new Matrix?(Matrix.CreateRotationX(this.m_legAngle1)));
				this.SetBoneTransform(this.m_leg2Bone.Index, new Matrix?(Matrix.CreateRotationX(this.m_legAngle2)));
				this.SetBoneTransform(this.m_leg3Bone.Index, new Matrix?(Matrix.CreateRotationX(this.m_legAngle3)));
				this.SetBoneTransform(this.m_leg4Bone.Index, new Matrix?(Matrix.CreateRotationX(this.m_legAngle4)));
				return;
			}

			// Animación de muerte
			float num19 = 1f - base.DeathPhase;
			float num20 = (float)((Vector3.Dot(this.m_componentFrame.Matrix.Right, base.DeathCauseOffset) > 0f) ? 1 : -1);
			float num21 = this.m_componentCreature.ComponentBody.BoundingBox.Max.Y - this.m_componentCreature.ComponentBody.BoundingBox.Min.Y;

			this.SetBoneTransform(this.m_bodyBone.Index,
				new Matrix?(Matrix.CreateTranslation(-0.5f * num21 * Vector3.UnitY * base.DeathPhase) *
				Matrix.CreateFromYawPitchRoll(vector.X, 0f, 1.5707964f * base.DeathPhase * num20) *
				Matrix.CreateTranslation(0.2f * num21 * Vector3.UnitY * base.DeathPhase) *
				Matrix.CreateTranslation(position)));

			this.SetBoneTransform(this.m_headBone.Index,
				new Matrix?(Matrix.CreateRotationX(MathUtils.DegToRad(50f) * base.DeathPhase)));

			if (this.m_neckBone != null)
			{
				this.SetBoneTransform(this.m_neckBone.Index, new Matrix?(Matrix.Identity));
			}

			this.SetBoneTransform(this.m_leg1Bone.Index,
				new Matrix?(Matrix.CreateRotationX(this.m_legAngle1 * num19)));
			this.SetBoneTransform(this.m_leg2Bone.Index,
				new Matrix?(Matrix.CreateRotationX(this.m_legAngle2 * num19)));
			this.SetBoneTransform(this.m_leg3Bone.Index,
				new Matrix?(Matrix.CreateRotationX(this.m_legAngle3 * num19)));
			this.SetBoneTransform(this.m_leg4Bone.Index,
				new Matrix?(Matrix.CreateRotationX(this.m_legAngle4 * num19)));
		}

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

			// Nuevos parámetros para suavidad
			this.m_gaitTransitionSpeed = valuesDictionary.GetValue<float>("GaitTransitionSpeed", 4f);
			this.m_bodySwayAmount = valuesDictionary.GetValue<float>("BodySwayAmount", 1f);
		}

		public override void SetModel(Model model)
		{
			base.SetModel(model);
			if (this.IsSet)
			{
				return;
			}
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

		// Función auxiliar para SmoothStep con un parámetro
		private float SmoothStep(float x)
		{
			// SmoothStep function: 3x² - 2x³
			return x * x * (3f - 2f * x);
		}

		// Campos existentes
		public SubsystemAudio m_subsystemAudio;
		public SubsystemSoundMaterials m_subsystemSoundMaterials;
		public ModelBone m_bodyBone;
		public ModelBone m_neckBone;
		public ModelBone m_headBone;
		public ModelBone m_leg1Bone;
		public ModelBone m_leg2Bone;
		public ModelBone m_leg3Bone;
		public ModelBone m_leg4Bone;
		public float m_walkAnimationSpeed;
		public float m_canterLegsAngleFactor;
		public float m_walkFrontLegsAngle;
		public float m_walkHindLegsAngle;
		public float m_walkBobHeight;
		public bool m_moveLegWhenFeeding;
		public bool m_canCanter;
		public bool m_canTrot;
		public bool m_useCanterSound;
		public Gait m_gait; // CORREGIDO: usar el enum local en lugar de ComponentFourLeggedModel.Gait
		public float m_feedFactor;
		public float m_buttFactor;
		public float m_buttPhase;
		public float m_footstepsPhase;
		public float m_legAngle1;
		public float m_legAngle2;
		public float m_legAngle3;
		public float m_legAngle4;
		public float m_headAngleY;

		// Nuevos campos para mejorar la fluidez
		private float m_gaitTransitionFactor = 1f;
		private Gait m_targetGait; // CORREGIDO: usar el enum local
		private float m_gaitTransitionSpeed = 4f;
		private float m_stepIntensity = 1f;
		private float m_lastSpeed = 0f;
		private float m_headSwayPhase;
		private float m_headSwayAmount;
		private float m_bodySwayAmount = 1f;

		public enum Gait
		{
			Walk,
			Trot,
			Canter
		}
	}
}
