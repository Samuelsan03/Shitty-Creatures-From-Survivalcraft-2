using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewBirdModel : ComponentCreatureModel
	{
		// Propiedades públicas para control de animación
		public float FlyPhase { get; set; }
		public float WingFlapIntensity { get; set; } = 1.0f;
		public float GlideSmoothness { get; set; } = 0.5f;

		// Variables de estado para animación fluida
		private float m_targetFlyPhase;
		private float m_previousFlySpeed;
		private float m_flapAcceleration;

		public override float AttackPhase
		{
			get => m_kickPhase;
			set => m_kickPhase = value;
		}

		public override float AttackFactor
		{
			get => m_peckAnimationSpeed;
			set => m_peckAnimationSpeed = value;
		}

		public override void Update(float dt)
		{
			// Caminar - movimiento mejorado
			float forwardSpeed = Vector3.Dot(m_componentCreature.ComponentBody.Velocity,
				m_componentCreature.ComponentBody.Matrix.Forward);

			if (MathF.Abs(forwardSpeed) > 0.1f)
			{
				// Suavizar transiciones de caminata
				float speedFactor = MathUtils.Saturate(MathF.Abs(forwardSpeed) / 2f);
				base.MovementAnimationPhase += forwardSpeed * dt * m_walkAnimationSpeed * speedFactor;
			}
			else
			{
				// Reposo suave
				float targetPhase = MathF.Floor(base.MovementAnimationPhase);
				if (MathF.Abs(base.MovementAnimationPhase - targetPhase) > 0.01f)
				{
					base.MovementAnimationPhase = MathUtils.Lerp(base.MovementAnimationPhase,
						targetPhase, 3f * dt);
				}
			}

			// Bob suave
			float targetBob = -m_walkBobHeight * MathUtils.Sqr(MathF.Sin(6.2831855f * base.MovementAnimationPhase));
			float bobSpeed = (m_componentCreature.ComponentBody.StandingOnValue != null) ? 12f : 6f;
			base.Bob += MathUtils.Min(bobSpeed * dt, 1f) * (targetBob - base.Bob);

			// ANIMACIÓN DE VUELO MEJORADA
			if (m_hasWings)
			{
				Vector3? flyOrder = m_componentCreature.ComponentLocomotion.LastFlyOrder;

				if (flyOrder != null)
				{
					float orderMagnitude = flyOrder.Value.LengthSquared();
					float currentFlySpeed = m_componentCreature.ComponentBody.Velocity.Length();

					// Determinar intensidad del aleteo basado en velocidad y orden
					float targetFlapIntensity = 1.0f;

					if (orderMagnitude > 0.99f)
					{
						// Vuelo activo - aleteo fuerte
						targetFlapIntensity = 1.5f + 0.5f * MathUtils.Saturate(currentFlySpeed / 10f);
					}
					else if (orderMagnitude > 0.1f)
					{
						// Vuelo suave
						targetFlapIntensity = 1.0f + 0.3f * MathUtils.Saturate(currentFlySpeed / 5f);
					}

					// Suavizar transición de intensidad
					WingFlapIntensity = MathUtils.Lerp(WingFlapIntensity, targetFlapIntensity, 4f * dt);

					// Calcular velocidad de animación basada en múltiples factores
					float speedFactor = 1.0f + MathUtils.Saturate(currentFlySpeed / 8f);
					float verticalFactor = 1.0f - 0.3f * MathUtils.Clamp(flyOrder.Value.Y, -1f, 0f);
					float accelerationFactor = MathUtils.Saturate(MathF.Abs(currentFlySpeed - m_previousFlySpeed) / dt);

					// Combinar factores
					float effectiveSpeed = m_flyAnimationSpeed * speedFactor * verticalFactor *
										 (1.0f + 0.2f * accelerationFactor);

					// Actualizar fase de vuelo con aceleración suave
					float targetPhase = MathUtils.Remainder(FlyPhase + effectiveSpeed * dt, 1f);

					// Manejar descenso - transición suave
					if (flyOrder.Value.Y < -0.15f && currentFlySpeed > 3f)
					{
						float glideTarget = 0.72f;
						float glideSpeed = MathUtils.Lerp(1.0f, 2.0f, GlideSmoothness);
						targetPhase = MathUtils.Lerp(FlyPhase, glideTarget, glideSpeed * dt);
					}

					FlyPhase = targetPhase;
					m_previousFlySpeed = currentFlySpeed;

					// Ajustar suavidad del planeo basado en velocidad vertical
					float targetGlideSmoothness = (flyOrder.Value.Y < -0.1f) ? 0.8f : 0.3f;
					GlideSmoothness = MathUtils.Lerp(GlideSmoothness, targetGlideSmoothness, 3f * dt);
				}
				else
				{
					// Reposo - alas plegadas suavemente
					if (MathF.Abs(FlyPhase - 1f) > 0.01f)
					{
						float restSpeed = MathUtils.Lerp(m_flyAnimationSpeed * 0.5f,
							m_flyAnimationSpeed * 2f, 1f - GlideSmoothness);
						FlyPhase = MathUtils.Lerp(FlyPhase, 1f, restSpeed * dt);
					}

					// Reducir intensidad gradualmente
					WingFlapIntensity = MathUtils.Lerp(WingFlapIntensity, 0.2f, 2f * dt);
					GlideSmoothness = MathUtils.Lerp(GlideSmoothness, 0.5f, 2f * dt);
				}
			}

			// ANIMACIÓN DE PICOTEO MEJORADA
			if (base.FeedOrder)
			{
				float peckSpeed = m_peckAnimationSpeed * (1.0f + 0.5f * MathF.Sin(m_peckPhase * MathF.PI));
				m_peckPhase += peckSpeed * dt;

				if (m_peckPhase > 0.75f)
				{
					m_peckPhase = 0.25f; // Reinicio suave
				}
			}
			else if (m_peckPhase > 0f)
			{
				m_peckPhase = MathUtils.Max(m_peckPhase - 2f * m_peckAnimationSpeed * dt, 0f);
			}

			base.FeedOrder = false;
			base.IsAttackHitMoment = false;

			// ANIMACIÓN DE ATAQUE MEJORADA
			if (base.AttackOrder)
			{
				float attackAcceleration = 3f;
				m_peckAnimationSpeed = MathUtils.Min(m_peckAnimationSpeed + attackAcceleration * dt, 1.5f);

				float previousKickPhase = m_kickPhase;
				m_kickPhase = MathUtils.Remainder(m_kickPhase + dt * 2.5f, 1f);

				if (previousKickPhase < 0.5f && m_kickPhase >= 0.5f)
				{
					base.IsAttackHitMoment = true;
				}
			}
			else
			{
				float deceleration = 2.5f;
				m_peckAnimationSpeed = MathUtils.Max(m_peckAnimationSpeed - deceleration * dt, 0f);

				if (m_kickPhase > 0f)
				{
					if (m_kickPhase > 0.5f)
					{
						m_kickPhase = MathUtils.Remainder(MathUtils.Min(m_kickPhase + dt * 3f, 1f), 1f);
					}
					else
					{
						m_kickPhase = MathUtils.Max(m_kickPhase - dt * 3f, 0f);
					}
				}
			}

			base.AttackOrder = false;
			base.Update(dt);
		}

		public override void AnimateCreature()
		{
			Vector3 rotation = m_componentCreature.ComponentBody.Rotation.ToYawPitchRoll();

			if (m_componentCreature.ComponentHealth.Health > 0f)
			{
				// Animación viva
				AnimateAlive(rotation);
			}
			else
			{
				// Animación de muerte
				AnimateDead(rotation);
			}
		}

		private void AnimateAlive(Vector3 rotation)
		{
			float wingMovement = 0f;

			// MOVIMIENTO DE ALAS MEJORADO
			if (m_hasWings)
			{
				// Movimiento principal del aleteo
				float primaryFlap = 1.2f * WingFlapIntensity *
					MathF.Sin(6.2831855f * (FlyPhase + 0.75f));

				// Movimiento secundario para mayor naturalidad
				float secondaryFlap = 0.4f * WingFlapIntensity *
					MathF.Sin(12.566371f * (FlyPhase + 0.25f));

				// Oscilación suave adicional
				float smoothOscillation = 0.2f * (1f - GlideSmoothness) *
					MathF.Sin(3.1415927f * FlyPhase);

				wingMovement = primaryFlap + secondaryFlap + smoothOscillation;

				// Movimiento adicional en el suelo
				if (m_componentCreature.ComponentBody.StandingOnValue != null)
				{
					wingMovement += 0.3f * MathF.Sin(6.2831855f * base.MovementAnimationPhase);
				}
			}

			// MOVIMIENTO DE PATAS MEJORADO
			float legMovement1, legMovement2;
			bool isGrounded = m_componentCreature.ComponentBody.StandingOnValue != null ||
							m_componentCreature.ComponentBody.ImmersionFactor > 0f;

			if (isGrounded || m_componentCreature.ComponentLocomotion.FlySpeed == 0f)
			{
				// Movimiento de caminata con variación
				float baseLegMovement = 0.6f * MathF.Sin(6.2831855f * base.MovementAnimationPhase);
				float variation = 0.1f * MathF.Sin(12.566371f * base.MovementAnimationPhase);

				legMovement1 = baseLegMovement + variation;
				legMovement2 = -legMovement1 + variation;
			}
			else
			{
				// Posición de descanso en vuelo
				float restAngle = MathUtils.DegToRad(60f);
				legMovement1 = legMovement2 = -restAngle * (1f - MathUtils.Saturate(FlyPhase));
			}

			// CABEZA Y CUELLO MEJORADOS
			float headYaw = m_componentCreature.ComponentLocomotion.LookAngles.X / 2f;
			float neckYaw = m_componentCreature.ComponentLocomotion.LookAngles.X / 2f;

			float neckPitch = 0f;
			float headPitch = 0f;

			if (isGrounded)
			{
				// Movimiento natural al caminar
				neckPitch = 0.5f * MathF.Sin(6.2831855f * base.MovementAnimationPhase / 2f);
				headPitch = -neckPitch * 0.8f;
			}

			// Efecto de picoteo/ataque
			float attackEffect = MathF.Cos((m_kickPhase != 0f) ?
				m_kickPhase * 2f * MathF.PI : m_peckPhase * 2f * MathF.PI);

			float peckOffset = 1.25f * (1f - MathUtils.Clamp(attackEffect, -0.5f, 1f));
			neckPitch -= peckOffset;

			// Añadir mirada vertical
			neckPitch += m_componentCreature.ComponentLocomotion.LookAngles.Y * 0.7f;
			headPitch += m_componentCreature.ComponentLocomotion.LookAngles.Y * 0.3f;

			// APLICAR TRANSFORMACIONES
			Matrix bodyTransform = Matrix.CreateFromYawPitchRoll(rotation.X, 0f, 0f) *
								 Matrix.CreateTranslation(m_componentCreature.ComponentBody.Position +
								 new Vector3(0f, base.Bob, 0f));

			Matrix neckTransform = Matrix.CreateFromYawPitchRoll(neckYaw, neckPitch, 0f);
			Matrix headTransform = Matrix.CreateFromYawPitchRoll(headYaw,
				headPitch + MathUtils.Clamp(rotation.Y, -0.7853982f, 0.7853982f), rotation.Z);

			SetBoneTransform(m_bodyBone.Index, bodyTransform);
			SetBoneTransform(m_neckBone.Index, neckTransform);
			SetBoneTransform(m_headBone.Index, headTransform);

			if (m_hasWings)
			{
				Matrix wing1Transform = Matrix.CreateRotationY(wingMovement);
				Matrix wing2Transform = Matrix.CreateRotationY(-wingMovement * 0.9f); // Ligeramente asimétrico
				SetBoneTransform(m_wing1Bone.Index, wing1Transform);
				SetBoneTransform(m_wing2Bone.Index, wing2Transform);
			}

			Matrix leg1Transform = Matrix.CreateRotationX(legMovement1);
			Matrix leg2Transform = Matrix.CreateRotationX(legMovement2);
			SetBoneTransform(m_leg1Bone.Index, leg1Transform);
			SetBoneTransform(m_leg2Bone.Index, leg2Transform);
		}

		private void AnimateDead(Vector3 rotation)
		{
			float deathBlend = 1f - base.DeathPhase;
			float bodyHeight = m_componentCreature.ComponentBody.BoundingBox.Max.Y -
							 m_componentCreature.ComponentBody.BoundingBox.Min.Y;

			Vector3 deathPosition = m_componentCreature.ComponentBody.Position +
								  0.5f * bodyHeight *
								  Vector3.Normalize(m_componentCreature.ComponentBody.Matrix.Forward *
								  new Vector3(1f, 0f, 1f));

			// Cuerpo caído
			Matrix bodyTransform = Matrix.CreateFromYawPitchRoll(rotation.X,
				1.5707964f * base.DeathPhase, 0f) *
				Matrix.CreateTranslation(deathPosition);

			SetBoneTransform(m_bodyBone.Index, bodyTransform);

			// Cabeza y cuello relajados
			SetBoneTransform(m_neckBone.Index, Matrix.Identity);
			SetBoneTransform(m_headBone.Index, Matrix.Identity);

			// Alas y patas relajadas gradualmente
			if (m_hasWings)
			{
				SetBoneTransform(m_wing1Bone.Index, Matrix.CreateRotationY(0f));
				SetBoneTransform(m_wing2Bone.Index, Matrix.CreateRotationY(0f));
			}

			SetBoneTransform(m_leg1Bone.Index, Matrix.Identity);
			SetBoneTransform(m_leg2Bone.Index, Matrix.Identity);
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros de animación
			m_flyAnimationSpeed = valuesDictionary.GetValue<float>("FlyAnimationSpeed");
			m_walkAnimationSpeed = valuesDictionary.GetValue<float>("WalkAnimationSpeed");
			m_peckAnimationSpeed = valuesDictionary.GetValue<float>("PeckAnimationSpeed");
			m_walkBobHeight = valuesDictionary.GetValue<float>("WalkBobHeight");

			// Parámetros adicionales para fluidez
			WingFlapIntensity = valuesDictionary.GetValue<float>("WingFlapIntensity", 1.0f);
			GlideSmoothness = valuesDictionary.GetValue<float>("GlideSmoothness", 0.5f);
		}

		public override void SetModel(Model model)
		{
			base.SetModel(model);

			if (this.IsSet || base.Model == null)
				return;

			// Encontrar huesos del modelo
			m_bodyBone = base.Model.FindBone("Body", true);
			m_neckBone = base.Model.FindBone("Neck", true);
			m_headBone = base.Model.FindBone("Head", true);
			m_leg1Bone = base.Model.FindBone("Leg1", true);
			m_leg2Bone = base.Model.FindBone("Leg2", true);
			m_wing1Bone = base.Model.FindBone("Wing1", false);
			m_wing2Bone = base.Model.FindBone("Wing2", false);

			m_hasWings = (m_wing1Bone != null && m_wing2Bone != null);

			// Inicializar estado
			FlyPhase = 1f; // Alas plegadas inicialmente
			WingFlapIntensity = 1.0f;
			GlideSmoothness = 0.5f;
			m_previousFlySpeed = 0f;
			m_flapAcceleration = 0f;
		}

		// Variables miembro
		private bool m_hasWings;
		private ModelBone m_bodyBone;
		private ModelBone m_neckBone;
		private ModelBone m_headBone;
		private ModelBone m_leg1Bone;
		private ModelBone m_leg2Bone;
		private ModelBone m_wing1Bone;
		private ModelBone m_wing2Bone;

		private float m_flyAnimationSpeed;
		private float m_walkAnimationSpeed;
		private float m_peckAnimationSpeed;
		private float m_walkBobHeight;
		private float m_peckPhase;
		private float m_kickPhase;
	}
}
