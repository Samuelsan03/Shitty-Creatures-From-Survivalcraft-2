using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewZombieModel : ComponentHumanModel, IUpdateable
	{
		// Factores de suavizado ajustados para mejor fluidez
		private float m_smoothFactor = 0.22f; // Aumentado para más suavidad
		private float m_animationResponsiveness = 8f; // Reducido para respuesta más suave

		// Variables para tracking del estado anterior para interpolación
		private Vector2 m_targetHeadAngles;
		private Vector2 m_targetHandAngles1;
		private Vector2 m_targetHandAngles2;
		private Vector2 m_targetLegAngles1;
		private Vector2 m_targetLegAngles2;

		// Variables para suavizado de movimiento
		private float m_smoothedMovementPhase;
		private float m_smoothedBob;

		// Tiempo para animaciones independientes
		private float m_animationTime;

		public override void Update(float dt)
		{
			// Primero, actualizar el tiempo de animación
			m_animationTime += dt;

			// Llamar al Update base con dt normal (no modificado)
			base.Update(dt);

			// Después de que base.Update haya calculado todo, aplicar suavizado
			float smoothSpeed = MathUtils.Min(m_animationResponsiveness * dt, 0.9f);

			// Suavizar la fase de movimiento (ajustado para mayor fluidez)
			m_smoothedMovementPhase = MathUtils.Lerp(m_smoothedMovementPhase, base.MovementAnimationPhase, smoothSpeed * 1.2f);

			// Suavizar el bob (ajustado para mayor fluidez)
			m_smoothedBob = MathUtils.Lerp(m_smoothedBob, base.Bob, smoothSpeed * 1.5f);

			// Actualizar los ángulos base con suavizado
			this.m_headAngles = Vector2.Lerp(this.m_headAngles, m_targetHeadAngles, smoothSpeed);
			this.m_handAngles1 = Vector2.Lerp(this.m_handAngles1, m_targetHandAngles1, smoothSpeed);
			this.m_handAngles2 = Vector2.Lerp(this.m_handAngles2, m_targetHandAngles2, smoothSpeed);
			this.m_legAngles1 = Vector2.Lerp(this.m_legAngles1, m_targetLegAngles1, smoothSpeed);
			this.m_legAngles2 = Vector2.Lerp(this.m_legAngles2, m_targetLegAngles2, smoothSpeed);
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

				// Usar bob suavizado
				position.Y += m_smoothedBob;
				vector.X += this.m_headingOffset;

				float num2 = (float)MathUtils.Remainder(0.75 * this.m_subsystemGameInfo.TotalElapsedGameTime + (double)(this.GetHashCode() & 65535), 10000.0);

				// Calcular ángulos objetivo con menos ruido
				float noiseScale = 0.08f; // Reducido para más suavidad

				float targetHeadX = MathUtils.Clamp(
					MathUtils.Lerp(-noiseScale, noiseScale, SimplexNoise.Noise(1.02f * num2 - 100f)) +
					this.m_componentCreature.ComponentLocomotion.LookAngles.X +
					0.5f * this.m_componentCreature.ComponentLocomotion.LastTurnOrder.X + // Reducido para menos brusquedad
					this.m_headingOffset,
					-MathUtils.DegToRad(80f), MathUtils.DegToRad(80f));

				float targetHeadY = MathUtils.Clamp(
					MathUtils.Lerp(-noiseScale, noiseScale, SimplexNoise.Noise(0.96f * num2 - 200f)) +
					this.m_componentCreature.ComponentLocomotion.LookAngles.Y,
					-MathUtils.DegToRad(45f), MathUtils.DegToRad(45f));

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
						position.Y -= 0.2f;
						vector.X += 3.1415927f;
						num4 = 0.4f;
						num6 = 0.4f;
						num5 = 0.2f;
						num7 = -0.2f;
						num3 = 1.1f;
						x2 = 1.1f;
						y2 = 0.2f;
						y3 = -0.2f;
					}
					else
					{
						num4 = 0.5f;
						num6 = 0.5f;
						num5 = 0.15f;
						num7 = -0.15f;
						y2 = 0.55f;
						y3 = -0.55f;
					}
				}
				else if (this.m_componentCreature.ComponentLocomotion.IsCreativeFlyEnabled)
				{
					float num8 = (this.m_componentCreature.ComponentLocomotion.LastWalkOrder != null) ?
						MathUtils.Min(0.03f * this.m_componentCreature.ComponentBody.Velocity.XZ.LengthSquared(), 0.5f) : 0f;
					num3 = -0.1f - num8;
					x2 = num3;
					y2 = MathUtils.Lerp(0f, 0.25f, SimplexNoise.Noise(1.07f * num2 + 400f));
					y3 = 0f - MathUtils.Lerp(0f, 0.25f, SimplexNoise.Noise(0.93f * num2 + 500f));
				}
				else if (m_smoothedMovementPhase != 0f)
				{
					// Animación de caminar suavizada - sin movimiento lateral en las piernas
					num4 = -0.35f * num;               // brazo derecho
					num6 = 0.35f * num;                 // brazo izquierdo
					num3 = this.m_walkLegsAngle * num * 0.6f;  // pierna derecha
					x2 = 0f - num3;                     // pierna izquierda
														// Eliminamos y2 e y3 para evitar rotación lateral
					y2 = 0f;
					y3 = 0f;
				}

				// Efecto de minería
				float num9 = 0f;
				if (this.m_componentMiner != null)
				{
					float num10 = MathF.Sin(MathF.Sqrt(this.m_componentMiner.PokingPhase) * 3.1415927f);
					num9 = ((this.m_componentMiner.ActiveBlockValue == 0) ? (1f * num10) : (0.3f + 1f * num10));
				}

				// Efecto de puñetazo
				float num11 = (this.m_punchPhase != 0f) ?
					((0f - MathUtils.DegToRad(90f)) * MathF.Sin(6.2831855f * MathUtils.Sigmoid(this.m_punchPhase, 3f))) : 0f; // Ajustado para más suavidad
				float num12 = ((this.m_punchCounter & 1) == 0) ? num11 : 0f;
				float num13 = ((this.m_punchCounter & 1) != 0) ? num11 : 0f;

				// Efecto de remar
				float num14 = 0f;
				float num15 = 0f;
				float num16 = 0f;
				float num17 = 0f;
				if (this.m_rowLeft || this.m_rowRight)
				{
					float num18 = 0.6f * (float)Math.Sin(6.91150426864624 * this.m_subsystemTime.GameTime);
					float num19 = 0.2f + 0.2f * (float)Math.Cos(6.91150426864624 * (this.m_subsystemTime.GameTime + 0.5));
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

				// Efecto de apuntar
				float num20 = 0f;
				float num21 = 0f;
				float num22 = 0f;
				float num23 = 0f;
				if (this.m_aimHandAngle != 0f)
				{
					// La mano derecha se eleva según el ángulo de apuntado (escalado para un rango adecuado)
					num20 = this.m_aimHandAngle * 1.2f;
					// Pequeña rotación lateral para naturalidad
					num21 = -0.1f;
					// La mano izquierda también se eleva, pero ligeramente menos (puede ajustarse)
					num22 = this.m_aimHandAngle * 0.8f;
					num23 = 0.1f;
				}

				float num24 = (float)((!this.m_componentCreature.ComponentLocomotion.IsCreativeFlyEnabled) ? 1 : 4);

				// Calcular ángulos finales con ruido reducido
				float handNoiseScale = 0.05f; // Reducido para más suavidad
				num4 += MathUtils.Lerp(-handNoiseScale, handNoiseScale, SimplexNoise.Noise(num2)) + num12 + num14 + num20;
				num5 += MathUtils.Lerp(0f, num24 * 0.08f, SimplexNoise.Noise(1.1f * num2 + 100f)) + num15 + num21; // Reducido
				num6 += num9 + MathUtils.Lerp(-handNoiseScale, handNoiseScale, SimplexNoise.Noise(0.9f * num2 + 200f)) + num13 + num16 + num22;
				num7 += 0f - MathUtils.Lerp(0f, num24 * 0.08f, SimplexNoise.Noise(1.05f * num2 + 300f)) + num17 + num23; // Reducido

				// Establecer ángulos objetivo para interpolación
				m_targetHandAngles1 = new Vector2(num4, num5);
				m_targetHandAngles2 = new Vector2(num6, num7);
				m_targetLegAngles1 = new Vector2(num3, y2);
				m_targetLegAngles2 = new Vector2(x2, y3);

				// Aplicar factor de agacharse
				if (this.m_componentCreature.ComponentBody.CrouchFactor == 1f)
				{
					m_targetLegAngles1 *= 0.5f;
					m_targetLegAngles2 *= 0.5f;
				}

				float f = MathUtils.Sigmoid(this.m_componentCreature.ComponentBody.CrouchFactor, 4f);
				Vector3 position2 = new Vector3(position.X, position.Y - MathUtils.Lerp(0f, 0.7f, f), position.Z);
				Vector3 position3 = new Vector3(0f, MathUtils.Lerp(0f, 7f, f), MathUtils.Lerp(0f, 28f, f));
				Vector3 scale = new Vector3(1f, 1f, MathUtils.Lerp(1f, 0.5f, f));

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

			// Inicializar variables de suavizado
			m_targetHeadAngles = Vector2.Zero;
			m_targetHandAngles1 = Vector2.Zero;
			m_targetHandAngles2 = Vector2.Zero;
			m_targetLegAngles1 = Vector2.Zero;
			m_targetLegAngles2 = Vector2.Zero;

			m_smoothedMovementPhase = 0f;
			m_smoothedBob = 0f;
			m_animationTime = 0f;

			// Ajustar parámetros para mayor fluidez (valores reducidos para menos exageración)
			this.m_walkAnimationSpeed *= 1.05f; // Reducido de 1.1f
			this.m_walkBobHeight *= 0.95f; // Reducido de 0.9f
		}

		// Métodos para ajustar parámetros dinámicamente
		public void SetSmoothFactor(float factor)
		{
			m_smoothFactor = MathUtils.Clamp(factor, 0.1f, 0.3f); // Ajustado rango mínimo
		}

		// Añadir esta propiedad pública para recibir el ángulo de apuntado
		public float AimHandAngleOrder
		{
			set { m_aimHandAngle = value; }
		}

		public void SetAnimationResponsiveness(float responsiveness)
		{
			m_animationResponsiveness = MathUtils.Clamp(responsiveness, 5f, 15f); // Ajustado rango
		}
	}
}
