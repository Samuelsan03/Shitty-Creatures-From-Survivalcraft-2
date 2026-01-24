using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewHumanModel : ComponentHumanModel
	{
		// Factores de suavizado mejorados - ajustados para mayor fluidez
		protected float m_smoothFactor = 0.18f; // Incrementado de 0.15f para más suavidad
		protected float m_animationResponsiveness = 10f; // Incrementado de 8f para respuesta más rápida pero suave

		// Factores específicos para apuntar - ajustados para transiciones más suaves
		private float m_aimSmoothFactor = 0.3f; // Incrementado de 0.25f
		private float m_aimTransitionSpeed = 5f; // Reducido de 6f para transición más gradual

		// Variables para tracking del estado anterior para interpolación
		private Vector2 m_targetHeadAngles;
		private Vector2 m_targetHandAngles1;
		private Vector2 m_targetHandAngles2;
		private Vector2 m_targetLegAngles1;
		private Vector2 m_targetLegAngles2;

		// Variables para suavizado de movimiento
		private float m_smoothedMovementPhase;
		private float m_smoothedBob;

		// Variables para suavizado de apuntar
		protected float m_smoothedAimHandAngle;
		protected float m_aimIntensity;
		private bool m_wasAiming;

		// Tiempo para animaciones independientes
		protected float m_animationTime;

		public override void Update(float dt)
		{
			// Primero, actualizar el tiempo de animación
			m_animationTime += dt;

			// Llamar al Update base con dt normal (no modificado)
			base.Update(dt);

			// Después de que base.Update haya calculado todo, aplicar suavizado
			float smoothSpeed = MathUtils.Min(m_animationResponsiveness * dt, 0.85f); // Reducido límite de 0.9f a 0.85f
			float aimSmoothSpeed = MathUtils.Min(m_aimTransitionSpeed * dt, 0.9f); // Reducido límite de 0.95f a 0.9f

			// Suavizar la fase de movimiento con curva más suave
			m_smoothedMovementPhase = MathUtils.Lerp(m_smoothedMovementPhase,
				base.MovementAnimationPhase, smoothSpeed * 1.2f); // Reducido de 1.5f

			// Suavizar el bob con menor amplitud
			m_smoothedBob = MathUtils.Lerp(m_smoothedBob, base.Bob, smoothSpeed * 1.5f); // Reducido de 2f

			// Suavizar el ángulo de apuntar con una curva más gradual
			float targetAimAngle = base.m_aimHandAngle;
			m_smoothedAimHandAngle = MathUtils.Lerp(m_smoothedAimHandAngle,
				targetAimAngle, aimSmoothSpeed * 0.7f); // Reducido factor

			// Calcular intensidad de apuntar con transición más suave
			bool isAiming = Math.Abs(targetAimAngle) > 0.001f;
			float targetAimIntensity = isAiming ? 1f : 0f;
			m_aimIntensity = MathUtils.Lerp(m_aimIntensity,
				targetAimIntensity, aimSmoothSpeed * 0.6f); // Reducido de 0.8f

			m_wasAiming = isAiming;

			// Actualizar los ángulos base con suavizado mejorado
			float bodySmoothSpeed = smoothSpeed * 0.8f; // Reducido para cuerpo más estable
			this.m_headAngles = Vector2.Lerp(this.m_headAngles, m_targetHeadAngles, bodySmoothSpeed);
			this.m_handAngles1 = Vector2.Lerp(this.m_handAngles1, m_targetHandAngles1, bodySmoothSpeed);
			this.m_handAngles2 = Vector2.Lerp(this.m_handAngles2, m_targetHandAngles2, bodySmoothSpeed);
			this.m_legAngles1 = Vector2.Lerp(this.m_legAngles1, m_targetLegAngles1, bodySmoothSpeed);
			this.m_legAngles2 = Vector2.Lerp(this.m_legAngles2, m_targetLegAngles2, bodySmoothSpeed);
		}

		public override void Animate()
		{
			// Usar el hook de mods
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

			// Llamar al Animate base primero
			base.Animate();

			// Verificar que tenemos huesos
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

				// Usar fase de movimiento suavizada
				float num = MathF.Sin(6.2831855f * m_smoothedMovementPhase);

				// Usar bob suavizado con amplitud reducida
				position.Y += m_smoothedBob * 0.8f; // Reducido amplitud del bob
				vector.X += this.m_headingOffset;

				float num2 = (float)MathUtils.Remainder(0.75 * this.m_subsystemGameInfo.TotalElapsedGameTime + (double)(this.GetHashCode() & 65535), 10000.0);

				// Calcular ángulos objetivo con menos ruido y más suavidad
				float noiseScale = 0.08f; // Reducido de 0.15f para mucho más suavidad

				float targetHeadX = MathUtils.Clamp(
					MathUtils.Lerp(-noiseScale, noiseScale, SimplexNoise.Noise(1.02f * num2 - 100f)) +
					this.m_componentCreature.ComponentLocomotion.LookAngles.X +
					0.4f * this.m_componentCreature.ComponentLocomotion.LastTurnOrder.X + // Reducido de 0.6f
					this.m_headingOffset,
					-MathUtils.DegToRad(70f), MathUtils.DegToRad(70f)); // Reducido rango de 80 a 70

				float targetHeadY = MathUtils.Clamp(
					MathUtils.Lerp(-noiseScale, noiseScale, SimplexNoise.Noise(0.96f * num2 - 200f)) +
					this.m_componentCreature.ComponentLocomotion.LookAngles.Y,
					-MathUtils.DegToRad(40f), MathUtils.DegToRad(40f)); // Reducido de 45 a 40

				m_targetHeadAngles = new Vector2(targetHeadX, targetHeadY);

				// Calcular ángulos de extremidades
				float num3 = 0f;
				float y2 = 0f;
				float x2 = 0f;
				float y3 = 0f;
				float num4 = 0f;
				float num5 = 0f;
				float num6 = 0f;
				float num7 = 0f;

				if (componentMount != null)
				{
					if (componentMount.Entity.ValuesDictionary.DatabaseObject.Name == "Boat")
					{
						position.Y -= 0.15f; // Reducido de 0.2f
						vector.X += 3.1415927f;
						num4 = 0.3f; // Reducido de 0.4f
						num6 = 0.3f; // Reducido de 0.4f
						num5 = 0.15f; // Reducido de 0.2f
						num7 = -0.15f; // Reducido de -0.2f
						num3 = 0.9f; // Reducido de 1.1f
						x2 = 0.9f; // Reducido de 1.1f
						y2 = 0.15f; // Reducido de 0.2f
						y3 = -0.15f; // Reducido de -0.2f
					}
					else
					{
						num4 = 0.4f; // Reducido de 0.5f
						num6 = 0.4f; // Reducido de 0.5f
						num5 = 0.12f; // Reducido de 0.15f
						num7 = -0.12f; // Reducido de -0.15f
						y2 = 0.45f; // Reducido de 0.55f
						y3 = -0.45f; // Reducido de -0.55f
					}
				}
				else if (this.m_componentCreature.ComponentLocomotion.IsCreativeFlyEnabled)
				{
					float num8 = (this.m_componentCreature.ComponentLocomotion.LastWalkOrder != null) ?
						MathUtils.Min(0.02f * this.m_componentCreature.ComponentBody.Velocity.XZ.LengthSquared(), 0.4f) : 0f; // Reducido de 0.03f y 0.5f
					num3 = -0.08f - num8; // Reducido de -0.1f
					x2 = num3;
					y2 = MathUtils.Lerp(0f, 0.2f, SimplexNoise.Noise(1.07f * num2 + 400f)); // Reducido de 0.25f
					y3 = 0f - MathUtils.Lerp(0f, 0.2f, SimplexNoise.Noise(0.93f * num2 + 500f)); // Reducido de 0.25f
				}
				else if (m_smoothedMovementPhase != 0f)
				{
					// Animación de caminar suavizada con menor amplitud y más fluidez
					float walkMultiplier = 0.35f; // Reducido significativamente de ~0.45f
					num4 = -walkMultiplier * num;
					num6 = walkMultiplier * num;

					// Movimiento de piernas menos exagerado
					float legSwingMultiplier = 0.5f; // Reducido de 0.7f
					num3 = this.m_walkLegsAngle * num * legSwingMultiplier;
					x2 = 0f - num3;

					// Agregar pequeña flexión de rodillas para mayor naturalidad
					y2 = 0.05f * MathF.Abs(num); // Pequeña flexión
					y3 = -0.05f * MathF.Abs(num); // Pequeña flexión
				}

				// Efecto de minería - reducido
				float num9 = 0f;
				if (this.m_componentMiner != null)
				{
					float num10 = MathF.Sin(MathF.Sqrt(this.m_componentMiner.PokingPhase) * 3.1415927f);
					num9 = ((this.m_componentMiner.ActiveBlockValue == 0) ? (0.8f * num10) : (0.25f + 0.8f * num10)); // Reducido de 1f
				}

				// Efecto de puñetazo - más suave y menos exagerado
				float num11 = (this.m_punchPhase != 0f) ?
					((0f - MathUtils.DegToRad(70f)) * MathF.Sin(6.2831855f * MathUtils.Sigmoid(this.m_punchPhase, 2.5f))) : 0f; // Reducido de 90 grados y 3f
				float num12 = ((this.m_punchCounter & 1) == 0) ? num11 : 0f;
				float num13 = ((this.m_punchCounter & 1) != 0) ? num11 : 0f;

				// Efecto de remar - reducido
				float num14 = 0f;
				float num15 = 0f;
				float num16 = 0f;
				float num17 = 0f;
				if (this.m_rowLeft || this.m_rowRight)
				{
					float num18 = 0.5f * (float)Math.Sin(6.91150426864624 * this.m_subsystemTime.GameTime); // Reducido de 0.6f
					float num19 = 0.15f + 0.15f * (float)Math.Cos(6.91150426864624 * (this.m_subsystemTime.GameTime + 0.5)); // Reducido de 0.2f
					if (this.m_rowLeft)
					{
						num14 = num18;
						num15 = num19;
					}
					if (this.m_rowRight)
					{
						num16 = num18;
						num17 = 0f - num19;
					}
				}

				// Efecto de apuntar - más suave y menos exagerado
				float num20 = 0f;
				float num21 = 0f;
				float num22 = 0f;
				float num23 = 0f;

				if (m_aimIntensity > 0.001f)
				{
					// Aplicar curva de entrada/salida más suave
					float easeInOut = m_aimIntensity * m_aimIntensity * (3f - 2f * m_aimIntensity);

					// Reducir amplitud de movimiento al apuntar
					num20 = 1.2f * easeInOut; // Reducido de 1.5f
					num21 = -0.5f * easeInOut; // Reducido de -0.7f

					// Suavizar también el ángulo de apuntar
					num22 = m_smoothedAimHandAngle * easeInOut * 0.8f; // Reducido factor
					num23 = 0f;
				}

				float num24 = (float)((!this.m_componentCreature.ComponentLocomotion.IsCreativeFlyEnabled) ? 1 : 4);

				// Calcular ángulos finales con ruido muy reducido
				float handNoiseScale = 0.03f; // Reducido de 0.06f
				num4 += MathUtils.Lerp(-handNoiseScale, handNoiseScale, SimplexNoise.Noise(num2)) + num12 + num14 + num20;
				num5 += MathUtils.Lerp(0f, num24 * 0.08f, SimplexNoise.Noise(1.1f * num2 + 100f)) + num15 + num21; // Reducido de 0.10f
				num6 += num9 + MathUtils.Lerp(-handNoiseScale, handNoiseScale, SimplexNoise.Noise(0.9f * num2 + 200f)) + num13 + num16 + num22;
				num7 += 0f - MathUtils.Lerp(0f, num24 * 0.08f, SimplexNoise.Noise(1.05f * num2 + 300f)) + num17 + num23; // Reducido de 0.10f

				// Establecer ángulos objetivo para interpolación
				m_targetHandAngles1 = new Vector2(num4, num5);
				m_targetHandAngles2 = new Vector2(num6, num7);
				m_targetLegAngles1 = new Vector2(num3, y2);
				m_targetLegAngles2 = new Vector2(x2, y3);

				// Aplicar factor de agacharse de forma más gradual y menos exagerada
				float crouchFactor = this.m_componentCreature.ComponentBody.CrouchFactor;
				if (crouchFactor > 0.95f)
				{
					m_targetLegAngles1 *= 0.7f; // Reducido de 0.5f (menos exagerado)
					m_targetLegAngles2 *= 0.7f; // Reducido de 0.5f
				}
				else if (crouchFactor > 0.5f)
				{
					float crouchScale = MathUtils.Lerp(1f, 0.7f, (crouchFactor - 0.5f) / 0.45f); // Reducido de 0.5f a 0.7f
					m_targetLegAngles1 *= crouchScale;
					m_targetLegAngles2 *= crouchScale;
				}

				float f = MathUtils.Sigmoid(this.m_componentCreature.ComponentBody.CrouchFactor, 3f); // Reducido de 4f
				Vector3 position2 = new Vector3(position.X, position.Y - MathUtils.Lerp(0f, 0.6f, f), position.Z); // Reducido de 0.7f
				Vector3 position3 = new Vector3(0f, MathUtils.Lerp(0f, 6f, f), MathUtils.Lerp(0f, 25f, f)); // Reducido de 7f y 28f
				Vector3 scale = new Vector3(1f, 1f, MathUtils.Lerp(1f, 0.6f, f)); // Reducido de 0.5f a 0.6f

				// Aplicar transformaciones usando los ángulos actuales (ya interpolados en Update)
				this.SetBoneTransform(this.m_bodyBone.Index,
					new Matrix?(Matrix.CreateRotationY(vector.X) * Matrix.CreateTranslation(position2)));

				this.SetBoneTransform(this.m_headBone.Index,
					new Matrix?(Matrix.CreateRotationX(this.m_headAngles.Y) *
					Matrix.CreateRotationZ(0f - this.m_headAngles.X)));

				this.SetBoneTransform(this.m_hand1Bone.Index,
					new Matrix?(Matrix.CreateRotationY(this.m_handAngles1.Y) *
					Matrix.CreateRotationX(this.m_handAngles1.X)));

				this.SetBoneTransform(this.m_hand2Bone.Index,
					new Matrix?(Matrix.CreateRotationY(this.m_handAngles2.Y) *
					Matrix.CreateRotationX(this.m_handAngles2.X)));

				this.SetBoneTransform(this.m_leg1Bone.Index,
					new Matrix?(Matrix.CreateRotationY(this.m_legAngles1.Y) *
					Matrix.CreateRotationX(this.m_legAngles1.X) *
					Matrix.CreateTranslation(position3) *
					Matrix.CreateScale(scale)));

				this.SetBoneTransform(this.m_leg2Bone.Index,
					new Matrix?(Matrix.CreateRotationY(this.m_legAngles2.Y) *
					Matrix.CreateRotationX(this.m_legAngles2.X) *
					Matrix.CreateTranslation(position3) *
					Matrix.CreateScale(scale)));

				return;
			}

			// Mantener código original para estado acostado
			float num25 = MathUtils.Max(base.DeathPhase, this.m_lieDownFactorModel);
			float num26 = 1f - num25;
			Vector3 position4 = position + num25 * 0.5f * this.m_componentCreature.ComponentBody.BoxSize.Y *
				Vector3.Normalize(this.m_componentCreature.ComponentBody.Matrix.Forward * new Vector3(1f, 0f, 1f)) +
				num25 * Vector3.UnitY * this.m_componentCreature.ComponentBody.BoxSize.Z * 0.1f;

			this.SetBoneTransform(this.m_bodyBone.Index,
				new Matrix?(Matrix.CreateFromYawPitchRoll(vector.X, 1.5707964f * num25, 0f) *
				Matrix.CreateTranslation(position4)));

			this.SetBoneTransform(this.m_headBone.Index, new Matrix?(Matrix.Identity));
			this.SetBoneTransform(this.m_hand1Bone.Index,
				new Matrix?(Matrix.CreateRotationY(this.m_handAngles1.Y * num26) *
				Matrix.CreateRotationX(this.m_handAngles1.X * num26)));

			this.SetBoneTransform(this.m_hand2Bone.Index,
				new Matrix?(Matrix.CreateRotationY(this.m_handAngles2.Y * num26) *
				Matrix.CreateRotationX(this.m_handAngles2.X * num26)));

			this.SetBoneTransform(this.m_leg1Bone.Index,
				new Matrix?(Matrix.CreateRotationY(this.m_legAngles1.Y * num26) *
				Matrix.CreateRotationX(this.m_legAngles1.X * num26)));

			this.SetBoneTransform(this.m_leg2Bone.Index,
				new Matrix?(Matrix.CreateRotationY(this.m_legAngles2.Y * num26) *
				Matrix.CreateRotationX(this.m_legAngles2.X * num26)));
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Primero cargar la configuración base
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros de suavizado desde XML
			if (valuesDictionary.ContainsKey("SmoothFactor"))
				m_smoothFactor = valuesDictionary.GetValue<float>("SmoothFactor");

			if (valuesDictionary.ContainsKey("AnimationResponsiveness"))
				m_animationResponsiveness = valuesDictionary.GetValue<float>("AnimationResponsiveness");

			if (valuesDictionary.ContainsKey("AimSmoothFactor"))
				m_aimSmoothFactor = valuesDictionary.GetValue<float>("AimSmoothFactor");

			if (valuesDictionary.ContainsKey("AimTransitionSpeed"))
				m_aimTransitionSpeed = valuesDictionary.GetValue<float>("AimTransitionSpeed");

			// Inicializar variables de suavizado
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

			// Ajustar parámetros para mayor fluidez - cambios más sutiles
			this.m_walkAnimationSpeed *= 1.02f;    // Reducido de 1.05f para menos aceleración
			this.m_walkBobHeight *= 0.9f;         // Reducido de 0.85f para bob menos pronunciado

			Log.Warning($"ComponentNewHumanModel cargado - Smooth: {m_smoothFactor}, Resp: {m_animationResponsiveness}, AimSmooth: {m_aimSmoothFactor}");
		}

		// Métodos para ajustar parámetros dinámicamente
		public void SetSmoothFactor(float factor)
		{
			m_smoothFactor = MathUtils.Clamp(factor, 0.08f, 0.35f); // Ampliado rango
		}

		public void SetAnimationResponsiveness(float responsiveness)
		{
			m_animationResponsiveness = MathUtils.Clamp(responsiveness, 6f, 25f); // Ampliado rango
		}

		public void SetAimSmoothness(float smoothness)
		{
			m_aimSmoothFactor = MathUtils.Clamp(smoothness, 0.15f, 0.6f); // Ampliado rango
			m_aimTransitionSpeed = MathUtils.Clamp(12f - (smoothness * 20f), 2f, 10f); // Ajustado para más control
		}
	}
}
