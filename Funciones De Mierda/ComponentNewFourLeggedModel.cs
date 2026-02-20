using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewFourLeggedModel : ComponentCreatureModel
	{
		// Token: 0x170000CE RID: 206
		public override float AttackPhase
		{
			get { return m_buttPhase; }
			set { m_buttPhase = value; }
		}

		public override float AttackFactor
		{
			get { return m_buttFactor; }
			set { m_buttFactor = value; }
		}

		// Nuevos campos para transiciones suaves
		private Gait m_targetGait;
		private float[] m_gaitWeights = new float[3]; // Peso para Walk, Trot, Canter
		private float m_gaitTransitionSpeed = 4f;     // Velocidad de transición entre marchas

		// Campos originales (mantenidos)
		private SubsystemAudio m_subsystemAudio;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;
		private ModelBone m_bodyBone;
		private ModelBone m_neckBone;
		private ModelBone m_headBone;
		private ModelBone m_leg1Bone;
		private ModelBone m_leg2Bone;
		private ModelBone m_leg3Bone;
		private ModelBone m_leg4Bone;
		private float m_walkAnimationSpeed;
		private float m_canterLegsAngleFactor;
		private float m_walkFrontLegsAngle;
		private float m_walkHindLegsAngle;
		private float m_walkBobHeight;
		private bool m_moveLegWhenFeeding;
		private bool m_canCanter;
		private bool m_canTrot;
		private bool m_useCanterSound;
		private float m_feedFactor;
		private float m_buttFactor;
		private float m_buttPhase;
		private float m_footstepsPhase;
		private float m_legAngle1, m_legAngle2, m_legAngle3, m_legAngle4;
		private float m_headAngleY;

		// Constantes para mejorar la interpolación
		private const float SMOOTH_TIME = 0.15f; // Tiempo de suavizado en segundos

		public override void Update(float dt)
		{
			// ===== Mejora 1: Transición suave de marchas =====
			float forwardSpeed = m_componentCreature.ComponentLocomotion.SlipSpeed ??
								  Vector3.Dot(m_componentCreature.ComponentBody.Velocity,
											  m_componentCreature.ComponentBody.Matrix.Forward);
			float absSpeed = Math.Abs(forwardSpeed);

			// Determinar la marcha objetivo según la velocidad
			Gait desiredGait;
			if (m_canCanter && absSpeed > 0.7f * m_componentCreature.ComponentLocomotion.WalkSpeed)
				desiredGait = Gait.Canter;
			else if (m_canTrot && absSpeed > 0.5f * m_componentCreature.ComponentLocomotion.WalkSpeed)
				desiredGait = Gait.Trot;
			else if (absSpeed > 0.2f)
				desiredGait = Gait.Walk;
			else
				desiredGait = Gait.Walk; // Quieto (se mezclará con Walk)

			// Actualizar pesos de mezcla con transición suave
			float blendSpeed = m_gaitTransitionSpeed * dt;
			for (int i = 0; i < 3; i++)
			{
				float targetWeight = (i == (int)desiredGait) ? 1f : 0f;
				m_gaitWeights[i] = MathUtils.Lerp(m_gaitWeights[i], targetWeight, blendSpeed);
			}
			// Normalizar pesos para evitar sumas != 1 (por si acaso)
			float sum = m_gaitWeights[0] + m_gaitWeights[1] + m_gaitWeights[2];
			if (sum > 0f)
			{
				m_gaitWeights[0] /= sum;
				m_gaitWeights[1] /= sum;
				m_gaitWeights[2] /= sum;
			}

			// ===== Mejora 2: Fase de movimiento mezclada =====
			// Calcular la fase de movimiento para cada marcha y mezclarlas
			float movementPhaseIncrement = forwardSpeed * dt * m_walkAnimationSpeed;
			float footstepsPhaseIncrement = 0f;

			// Para la mezcla, usamos el incremento ponderado por los pesos
			float weightedMovementIncrement = 0f;
			float weightedFootstepsIncrement = 0f;

			// Walk
			weightedMovementIncrement += m_gaitWeights[0] * (forwardSpeed * dt * m_walkAnimationSpeed);
			weightedFootstepsIncrement += m_gaitWeights[0] * (1.25f * m_walkAnimationSpeed * forwardSpeed * dt);

			// Trot
			weightedMovementIncrement += m_gaitWeights[1] * (forwardSpeed * dt * m_walkAnimationSpeed);
			weightedFootstepsIncrement += m_gaitWeights[1] * (1.25f * m_walkAnimationSpeed * forwardSpeed * dt);

			// Canter
			weightedMovementIncrement += m_gaitWeights[2] * (forwardSpeed * dt * 0.7f * m_walkAnimationSpeed);
			weightedFootstepsIncrement += m_gaitWeights[2] * (0.7f * m_walkAnimationSpeed * forwardSpeed * dt);

			if (absSpeed > 0.2f)
			{
				MovementAnimationPhase += weightedMovementIncrement;
				m_footstepsPhase += weightedFootstepsIncrement;
			}
			else
			{
				MovementAnimationPhase = 0f;
				m_footstepsPhase = 0f;
			}

			// ===== Mejora 3: Cálculo del cabeceo (bob) con mezcla de marchas =====
			float targetBob = 0f;
			if (m_gaitWeights[0] > 0f) // Walk
			{
				targetBob += m_gaitWeights[0] * (-m_walkBobHeight) * MathUtils.Sqr(MathF.Sin(6.2831855f * MovementAnimationPhase));
			}
			if (m_gaitWeights[1] > 0f) // Trot
			{
				targetBob += m_gaitWeights[1] * (m_walkBobHeight * 1.5f * MathUtils.Sqr(MathF.Sin(6.2831855f * MovementAnimationPhase)));
			}
			if (m_gaitWeights[2] > 0f) // Canter
			{
				targetBob += m_gaitWeights[2] * (-m_walkBobHeight * 1.5f * MathF.Sin(6.2831855f * MovementAnimationPhase));
			}

			// Interpolación suave hacia el bob objetivo (usando factor dependiente del tiempo)
			float bobLerpFactor = MathUtils.Saturate(12f * m_subsystemTime.GameTimeDelta);
			Bob += bobLerpFactor * (targetBob - Bob);

			// ===== Mejora 4: Sonidos de pasos con mezcla (manteniendo compatibilidad) =====
			float footstepsFloor = MathF.Floor(m_footstepsPhase);
			float prevFootstepsPhase = m_footstepsPhase - weightedFootstepsIncrement; // Estimación simple
			if (m_footstepsPhase > footstepsFloor && prevFootstepsPhase <= footstepsFloor)
			{
				if (m_gaitWeights[2] > 0.5f && m_useCanterSound) // Predomina galope
				{
					string mat = m_subsystemSoundMaterials.GetFootstepSoundMaterialName(m_componentCreature);
					if (!string.IsNullOrEmpty(mat) && mat != "Water")
					{
						m_subsystemAudio.PlayRandomSound("Audio/Footsteps/CanterDirt", 0.75f,
							m_random.Float(-0.25f, 0f), m_componentCreature.ComponentBody.Position, 3f, true);
					}
				}
				else
				{
					m_componentCreature.ComponentCreatureSounds.PlayFootstepSound(1f);
				}
			}

			// ===== Mejora 5: Animaciones de alimentación y ataque con transiciones suaves =====
			m_feedFactor = FeedOrder
				? MathUtils.Min(m_feedFactor + 2f * dt, 1f)
				: MathUtils.Max(m_feedFactor - 2f * dt, 0f);

			IsAttackHitMoment = false;
			if (AttackOrder)
			{
				m_buttFactor = MathUtils.Min(m_buttFactor + 4f * dt, 1f);
				float oldPhase = m_buttPhase;
				m_buttPhase = MathUtils.Remainder(m_buttPhase + dt * 2f, 1f);
				if (oldPhase < 0.5f && m_buttPhase >= 0.5f)
					IsAttackHitMoment = true;
			}
			else
			{
				m_buttFactor = MathUtils.Max(m_buttFactor - 4f * dt, 0f);
				if (m_buttPhase != 0f)
				{
					if (m_buttPhase > 0.5f)
						m_buttPhase = MathUtils.Remainder(MathUtils.Min(m_buttPhase + dt * 2f, 1f), 1f);
					else
						m_buttPhase = MathUtils.Max(m_buttPhase - dt * 2f, 0f);
				}
			}

			FeedOrder = false;
			AttackOrder = false;

			base.Update(dt);
		}

		public override void AnimateCreature()
		{
			Vector3 position = m_componentCreature.ComponentBody.Position;
			Vector3 rotationAngles = m_componentCreature.ComponentBody.Rotation.ToYawPitchRoll();

			if (m_componentCreature.ComponentHealth.Health > 0f)
			{
				// ===== Mejora 6: Cálculo de ángulos de patas con mezcla de marchas =====
				float targetLeg1 = 0f, targetLeg2 = 0f, targetLeg3 = 0f, targetLeg4 = 0f;
				float targetHeadY = 0f;

				// Walk
				if (m_gaitWeights[0] > 0f)
				{
					float sinWalk = MathF.Sin(6.2831855f * MovementAnimationPhase);
					float sinWalkHalf = MathF.Sin(6.2831855f * (MovementAnimationPhase + 0.5f));
					float sinWalkQuarter = MathF.Sin(6.2831855f * (MovementAnimationPhase + 0.25f));
					float sinWalkThreeQuarter = MathF.Sin(6.2831855f * (MovementAnimationPhase + 0.75f));

					targetLeg1 += m_gaitWeights[0] * (m_walkFrontLegsAngle * sinWalk);
					targetLeg2 += m_gaitWeights[0] * (m_walkFrontLegsAngle * sinWalkHalf);
					targetLeg3 += m_gaitWeights[0] * (m_walkHindLegsAngle * sinWalkQuarter);
					targetLeg4 += m_gaitWeights[0] * (m_walkHindLegsAngle * sinWalkThreeQuarter);
					targetHeadY += m_gaitWeights[0] * (MathUtils.DegToRad(3f) * MathF.Sin(12.566371f * MovementAnimationPhase));
				}

				// Trot
				if (m_gaitWeights[1] > 0f)
				{
					float sinTrot = MathF.Sin(6.2831855f * MovementAnimationPhase);
					float sinTrotHalf = MathF.Sin(6.2831855f * (MovementAnimationPhase + 0.5f));

					targetLeg1 += m_gaitWeights[1] * (m_walkFrontLegsAngle * sinTrot);
					targetLeg2 += m_gaitWeights[1] * (m_walkFrontLegsAngle * sinTrotHalf);
					targetLeg3 += m_gaitWeights[1] * (m_walkHindLegsAngle * sinTrotHalf);
					targetLeg4 += m_gaitWeights[1] * (m_walkHindLegsAngle * sinTrot);
					targetHeadY += m_gaitWeights[1] * (MathUtils.DegToRad(3f) * MathF.Sin(12.566371f * MovementAnimationPhase));
				}

				// Canter
				if (m_gaitWeights[2] > 0f)
				{
					float sinCanter0 = MathF.Sin(6.2831855f * MovementAnimationPhase);
					float sinCanter25 = MathF.Sin(6.2831855f * (MovementAnimationPhase + 0.25f));
					float sinCanter15 = MathF.Sin(6.2831855f * (MovementAnimationPhase + 0.15f));
					float sinCanter40 = MathF.Sin(6.2831855f * (MovementAnimationPhase + 0.4f));

					targetLeg1 += m_gaitWeights[2] * (m_walkFrontLegsAngle * m_canterLegsAngleFactor * sinCanter0);
					targetLeg2 += m_gaitWeights[2] * (m_walkFrontLegsAngle * m_canterLegsAngleFactor * sinCanter25);
					targetLeg3 += m_gaitWeights[2] * (m_walkHindLegsAngle * m_canterLegsAngleFactor * sinCanter15);
					targetLeg4 += m_gaitWeights[2] * (m_walkHindLegsAngle * m_canterLegsAngleFactor * sinCanter40);
					targetHeadY += m_gaitWeights[2] * (MathUtils.DegToRad(8f) * MathF.Sin(6.2831855f * MovementAnimationPhase));
				}

				// ===== Mejora 7: Interpolación suave hacia los objetivos (usando SmoothStep en lugar de lerp lineal) =====
				float smoothFactor = MathUtils.Saturate(m_subsystemTime.GameTimeDelta / SMOOTH_TIME);
				m_legAngle1 = MathUtils.Lerp(m_legAngle1, targetLeg1, smoothFactor);
				m_legAngle2 = MathUtils.Lerp(m_legAngle2, targetLeg2, smoothFactor);
				m_legAngle3 = MathUtils.Lerp(m_legAngle3, targetLeg3, smoothFactor);
				m_legAngle4 = MathUtils.Lerp(m_legAngle4, targetLeg4, smoothFactor);
				m_headAngleY = MathUtils.Lerp(m_headAngleY, targetHeadY, smoothFactor);

				// ===== Mejora 8: Movimiento de cabeza y cuello con transiciones suaves =====
				Vector2 lookAngles = m_componentCreature.ComponentLocomotion.LookAngles;
				lookAngles.Y += m_headAngleY;
				lookAngles.X = Math.Clamp(lookAngles.X, -MathUtils.DegToRad(65f), MathUtils.DegToRad(65f));
				lookAngles.Y = Math.Clamp(lookAngles.Y, -MathUtils.DegToRad(55f), MathUtils.DegToRad(55f));

				Vector2 neckAngles = Vector2.Zero;
				if (m_neckBone != null)
				{
					neckAngles = 0.6f * lookAngles;
					lookAngles = 0.4f * lookAngles;
				}

				// Mezcla con alimentación y ataque (suave gracias a m_feedFactor y m_buttFactor ya interpolados)
				if (m_feedFactor > 0f)
				{
					float feedY = -MathUtils.DegToRad(25f + 45f * SimplexNoise.OctavedNoise((float)m_subsystemTime.GameTime, 3f, 2, 2f, 0.75f, false));
					lookAngles = Vector2.Lerp(lookAngles, new Vector2(0f, feedY), m_feedFactor);
				}

				if (m_buttFactor > 0f)
				{
					float buttY = -MathUtils.DegToRad(40f) * MathF.Sin(6.2831855f * MathUtils.Sigmoid(m_buttPhase, 4f));
					lookAngles = Vector2.Lerp(lookAngles, new Vector2(0f, buttY), m_buttFactor);
				}

				// Aplicar transformaciones
				SetBoneTransform(m_bodyBone.Index, Matrix.CreateRotationY(rotationAngles.X) *
													Matrix.CreateTranslation(position.X, position.Y + Bob, position.Z));
				SetBoneTransform(m_headBone.Index, Matrix.CreateRotationX(lookAngles.Y) *
													 Matrix.CreateRotationZ(-lookAngles.X));
				if (m_neckBone != null)
				{
					SetBoneTransform(m_neckBone.Index, Matrix.CreateRotationX(neckAngles.Y) *
														 Matrix.CreateRotationZ(-neckAngles.X));
				}

				SetBoneTransform(m_leg1Bone.Index, Matrix.CreateRotationX(m_legAngle1));
				SetBoneTransform(m_leg2Bone.Index, Matrix.CreateRotationX(m_legAngle2));
				SetBoneTransform(m_leg3Bone.Index, Matrix.CreateRotationX(m_legAngle3));
				SetBoneTransform(m_leg4Bone.Index, Matrix.CreateRotationX(m_legAngle4));
			}
			else
			{
				// Animación de muerte (se mantiene similar al original)
				float deathFactor = 1f - DeathPhase;
				float sign = (Vector3.Dot(m_componentFrame.Matrix.Right, DeathCauseOffset) > 0f) ? 1 : -1;
				float height = m_componentCreature.ComponentBody.BoundingBox.Max.Y - m_componentCreature.ComponentBody.BoundingBox.Min.Y;

				SetBoneTransform(m_bodyBone.Index,
					Matrix.CreateTranslation(-0.5f * height * Vector3.UnitY * DeathPhase) *
					Matrix.CreateFromYawPitchRoll(rotationAngles.X, 0f, 1.5707964f * DeathPhase * sign) *
					Matrix.CreateTranslation(0.2f * height * Vector3.UnitY * DeathPhase) *
					Matrix.CreateTranslation(position));

				SetBoneTransform(m_headBone.Index, Matrix.CreateRotationX(MathUtils.DegToRad(50f) * DeathPhase));
				if (m_neckBone != null) SetBoneTransform(m_neckBone.Index, Matrix.Identity);

				SetBoneTransform(m_leg1Bone.Index, Matrix.CreateRotationX(m_legAngle1 * deathFactor));
				SetBoneTransform(m_leg2Bone.Index, Matrix.CreateRotationX(m_legAngle2 * deathFactor));
				SetBoneTransform(m_leg3Bone.Index, Matrix.CreateRotationX(m_legAngle3 * deathFactor));
				SetBoneTransform(m_leg4Bone.Index, Matrix.CreateRotationX(m_legAngle4 * deathFactor));
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);
			m_walkAnimationSpeed = valuesDictionary.GetValue<float>("WalkAnimationSpeed");
			m_walkFrontLegsAngle = valuesDictionary.GetValue<float>("WalkFrontLegsAngle");
			m_walkHindLegsAngle = valuesDictionary.GetValue<float>("WalkHindLegsAngle");
			m_canterLegsAngleFactor = valuesDictionary.GetValue<float>("CanterLegsAngleFactor");
			m_walkBobHeight = valuesDictionary.GetValue<float>("WalkBobHeight");
			m_moveLegWhenFeeding = valuesDictionary.GetValue<bool>("MoveLegWhenFeeding");
			m_canCanter = valuesDictionary.GetValue<bool>("CanCanter");
			m_canTrot = valuesDictionary.GetValue<bool>("CanTrot");
			m_useCanterSound = valuesDictionary.GetValue<bool>("UseCanterSound");

			// Inicializar pesos de gait (todo en walk)
			m_gaitWeights[0] = 1f;
			m_gaitWeights[1] = 0f;
			m_gaitWeights[2] = 0f;
		}

		public override void SetModel(Model model)
		{
			base.SetModel(model);
			if (IsSet) return;
			if (model != null)
			{
				m_bodyBone = model.FindBone("Body", true);
				m_neckBone = model.FindBone("Neck", false);
				m_headBone = model.FindBone("Head", true);
				m_leg1Bone = model.FindBone("Leg1", true);
				m_leg2Bone = model.FindBone("Leg2", true);
				m_leg3Bone = model.FindBone("Leg3", true);
				m_leg4Bone = model.FindBone("Leg4", true);
			}
			else
			{
				m_bodyBone = null;
				m_neckBone = null;
				m_headBone = null;
				m_leg1Bone = null;
				m_leg2Bone = null;
				m_leg3Bone = null;
				m_leg4Bone = null;
			}
		}

		private enum Gait
		{
			Walk,
			Trot,
			Canter
		}
	}
}
