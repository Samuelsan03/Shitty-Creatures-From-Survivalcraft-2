using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBowShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Componentes necesarios
		private ComponentCreature m_componentCreature;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentInventory m_componentInventory;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private ComponentCreatureModel m_componentModel;
		private SubsystemParticles m_subsystemParticles;

		// Configuración
		public float MaxDistance = 25f;
		public float DrawTime = 1.2f;
		public float AimTime = 0.5f;
		public float FireSoundDistance = 15f;
		public float Accuracy = 0.03f;
		public float ArrowSpeed = 35f;
		public bool CycleArrowTypes = true;
		public bool ShowArrowWhenIdle = true;

		// Tipos de flechas a usar
		public ArrowBlock.ArrowType[] AvailableArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.CopperArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.FireArrow
		};

		// Estado
		private bool m_isAiming = false;
		private bool m_isDrawing = false;
		private bool m_isFiring = false;
		private double m_animationStartTime;
		private double m_drawStartTime;
		private double m_fireTime;
		private int m_bowSlot = -1;
		private int m_currentArrowTypeIndex = 0;
		private float m_currentDraw = 0f;
		private Random m_random = new Random();
		private bool m_initialized = false;

		// UpdateOrder
		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 25f);
			DrawTime = valuesDictionary.GetValue<float>("DrawTime", 1.2f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 0.5f);
			FireSoundDistance = valuesDictionary.GetValue<float>("FireSoundDistance", 15f);
			Accuracy = valuesDictionary.GetValue<float>("Accuracy", 0.03f);
			ArrowSpeed = valuesDictionary.GetValue<float>("ArrowSpeed", 35f);
			CycleArrowTypes = valuesDictionary.GetValue<bool>("CycleArrowTypes", true);
			ShowArrowWhenIdle = valuesDictionary.GetValue<bool>("ShowArrowWhenIdle", true);

			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
		}

		public override void OnEntityAdded()
		{
			base.OnEntityAdded();

			// FIX: Asegurar que el índice sea válido
			if (AvailableArrowTypes.Length > 0)
			{
				m_currentArrowTypeIndex = m_random.Int(0, AvailableArrowTypes.Length);
			}
			else
			{
				m_currentArrowTypeIndex = 0;
			}

			m_initialized = true;

			FindBow();

			if (ShowArrowWhenIdle && m_bowSlot >= 0)
			{
				SetBowWithArrow(0);
			}
		}

		public void Update(float dt)
		{
			if (!m_initialized || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			if (m_bowSlot < 0)
			{
				FindBow();
				if (m_bowSlot < 0) return;
			}

			if (m_componentChaseBehavior.Target == null)
			{
				ResetAnimations();
				if (ShowArrowWhenIdle && m_bowSlot >= 0)
				{
					SetBowWithArrow(0);
				}
				return;
			}

			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				m_componentChaseBehavior.Target.ComponentBody.Position
			);

			if (distance <= MaxDistance)
			{
				if (!m_isAiming && !m_isDrawing && !m_isFiring)
				{
					StartAiming();
				}
			}
			else
			{
				ResetAnimations();
				if (ShowArrowWhenIdle && m_bowSlot >= 0)
				{
					SetBowWithArrow(0);
				}
				return;
			}

			if (m_isAiming)
			{
				ApplyAimingAnimation(dt);

				if (m_subsystemTime.GameTime - m_animationStartTime >= AimTime)
				{
					m_isAiming = false;
					StartDrawing();
				}
			}
			else if (m_isDrawing)
			{
				ApplyDrawingAnimation(dt);

				m_currentDraw = MathUtils.Clamp((float)((m_subsystemTime.GameTime - m_drawStartTime) / DrawTime), 0f, 1f);

				SetBowWithArrow((int)(m_currentDraw * 15f));

				if (m_subsystemTime.GameTime - m_drawStartTime >= DrawTime)
				{
					Fire();
				}
			}
			else if (m_isFiring)
			{
				ApplyFiringAnimation(dt);

				if (m_subsystemTime.GameTime - m_fireTime >= 0.2)
				{
					m_isFiring = false;

					ClearArrowFromBow();

					if (CycleArrowTypes && AvailableArrowTypes.Length > 1)
					{
						m_currentArrowTypeIndex = (m_currentArrowTypeIndex + 1) % AvailableArrowTypes.Length;
					}

					if (m_subsystemTime.GameTime - m_fireTime >= 0.8)
					{
						StartAiming();
					}
				}
			}
		}

		private bool IsArrowVisibleOnBow()
		{
			if (m_bowSlot < 0) return false;

			try
			{
				int currentBowValue = m_componentInventory.GetSlotValue(m_bowSlot);
				if (currentBowValue == 0) return false;

				int currentData = Terrain.ExtractData(currentBowValue);
				ArrowBlock.ArrowType? currentArrowType = BowBlock.GetArrowType(currentData);

				return currentArrowType.HasValue;
			}
			catch
			{
				return false;
			}
		}

		private void FindBow()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					if (block is BowBlock)
					{
						m_bowSlot = i;
						m_componentInventory.ActiveSlotIndex = i;
						break;
					}
				}
			}
		}

		private void SetBowWithArrow(int drawValue)
		{
			if (m_bowSlot < 0)
			{
				FindBow();
				if (m_bowSlot < 0) return;
			}

			try
			{
				int currentBowValue = m_componentInventory.GetSlotValue(m_bowSlot);
				if (currentBowValue == 0) return;

				int currentData = Terrain.ExtractData(currentBowValue);

				// FIX: Verificar que el índice sea válido
				if (m_currentArrowTypeIndex >= 0 && m_currentArrowTypeIndex < AvailableArrowTypes.Length)
				{
					ArrowBlock.ArrowType arrowType = AvailableArrowTypes[m_currentArrowTypeIndex];

					ArrowBlock.ArrowType? currentArrowType = BowBlock.GetArrowType(currentData);
					if (currentArrowType.HasValue && currentArrowType.Value == arrowType &&
						BowBlock.GetDraw(currentData) == MathUtils.Clamp(drawValue, 0, 15))
					{
						return;
					}

					int newData = BowBlock.SetArrowType(currentData, new ArrowBlock.ArrowType?(arrowType));
					newData = BowBlock.SetDraw(newData, MathUtils.Clamp(drawValue, 0, 15));

					int newBowValue = Terrain.ReplaceData(currentBowValue, newData);
					m_componentInventory.RemoveSlotItems(m_bowSlot, 1);
					m_componentInventory.AddSlotItems(m_bowSlot, newBowValue, 1);
				}
			}
			catch
			{
				// Ignorar errores
			}
		}

		private void ClearArrowFromBow()
		{
			if (m_bowSlot < 0) return;

			try
			{
				int currentBowValue = m_componentInventory.GetSlotValue(m_bowSlot);
				if (currentBowValue == 0) return;

				int currentData = Terrain.ExtractData(currentBowValue);

				int newData = BowBlock.SetArrowType(currentData, null);
				newData = BowBlock.SetDraw(newData, 0);

				int newBowValue = Terrain.ReplaceData(currentBowValue, newData);
				m_componentInventory.RemoveSlotItems(m_bowSlot, 1);
				m_componentInventory.AddSlotItems(m_bowSlot, newBowValue, 1);
			}
			catch
			{
				// Ignorar errores
			}
		}

		private void StartAiming()
		{
			m_isAiming = true;
			m_isDrawing = false;
			m_isFiring = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;

			if (m_bowSlot < 0)
			{
				FindBow();
				if (m_bowSlot < 0) return;
			}

			SetBowWithArrow(0);
		}

		private void ApplyAimingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				// FIX: VALORES PARA ARCO CENTRADO - posición natural del arco
				// La mano se levanta pero el arco queda centrado
				m_componentModel.AimHandAngleOrder = 0.5f; // Mano levantada

				// ARCO CENTRADO: Valores pequeños para que el arco quede en el medio
				// X: pequeño offset horizontal (centrado)
				// Y: altura adecuada (ni muy alto ni muy bajo)
				// Z: profundidad (cerca del cuerpo)
				m_componentModel.InHandItemOffsetOrder = new Vector3(0.02f, 0.12f, 0.08f);

				// ROTACIÓN PARA ARCO RECTO:
				// X: pequeña inclinación hacia adelante
				// Y: orientación lateral (apuntando hacia adelante)
				// Z: casi cero para que esté recto
				m_componentModel.InHandItemRotationOrder = new Vector3(-0.05f, 0.25f, 0.01f);

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void StartDrawing()
		{
			m_isAiming = false;
			m_isDrawing = true;
			m_isFiring = false;
			m_drawStartTime = m_subsystemTime.GameTime;

			m_subsystemAudio.PlaySound("Audio/BowDraw", 0.5f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);
		}

		private void ApplyDrawingAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				float drawFactor = m_currentDraw;

				// FIX: ANIMACIÓN CON ARCO CENTRADO durante el tensado
				m_componentModel.AimHandAngleOrder = 0.5f + (0.4f * drawFactor);

				// Durante el tensado, el arco se mueve ligeramente pero se mantiene centrado
				float horizontalOffset = 0.02f - (0.01f * drawFactor); // Muy pequeño movimiento horizontal
				float verticalOffset = 0.12f + (0.05f * drawFactor);   // Sube ligeramente
				float depthOffset = 0.08f - (0.03f * drawFactor);      // Se acerca al cuerpo

				// Rotaciones suaves para mantener el arco recto
				float pitchRotation = -0.05f - (0.15f * drawFactor);   // Inclinación hacia atrás
				float yawRotation = 0.25f - (0.08f * drawFactor);      // Gira hacia el centro
				float rollRotation = 0.01f - (0.005f * drawFactor);    // Casi sin rotación en Z

				m_componentModel.InHandItemOffsetOrder = new Vector3(
					horizontalOffset,
					verticalOffset,
					depthOffset
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					pitchRotation,
					yawRotation,
					rollRotation
				);

				if (m_componentChaseBehavior.Target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(
						m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition
					);
				}
			}
		}

		private void Fire()
		{
			m_isDrawing = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			// FIX: Asegurar que se pueda disparar sin errores
			try
			{
				ShootArrow();
			}
			catch (Exception ex)
			{
				Log.Error($"Error en Fire(): {ex.Message}");
			}

			m_subsystemAudio.PlaySound("Audio/Bow", 0.8f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, FireSoundDistance, false);
		}

		private void ApplyFiringAnimation(float dt)
		{
			if (m_componentModel != null)
			{
				float fireProgress = (float)((m_subsystemTime.GameTime - m_fireTime) / 0.2);

				if (fireProgress < 0.5f)
				{
					float recoil = 0.01f * (1f - (fireProgress * 2f));

					m_componentModel.InHandItemOffsetOrder += new Vector3(recoil, 0f, 0f);
					m_componentModel.InHandItemRotationOrder += new Vector3(recoil * 1.5f, 0f, 0f);
				}
				else
				{
					float returnProgress = (fireProgress - 0.5f) / 0.5f;

					// Volver a posición centrada
					m_componentModel.AimHandAngleOrder = 0.5f * (1f - returnProgress);
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						0.02f * (1f - returnProgress),
						0.12f * (1f - returnProgress),
						0.08f * (1f - returnProgress)
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(
						-0.05f * (1f - returnProgress),
						0.25f * (1f - returnProgress),
						0.01f * (1f - returnProgress)
					);
				}
			}
		}

		private void ResetAnimations()
		{
			m_isAiming = false;
			m_isDrawing = false;
			m_isFiring = false;
			m_currentDraw = 0f;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ShootArrow()
		{
			if (m_componentChaseBehavior.Target == null)
				return;

			try
			{
				// FIX: Verificar que el índice sea válido antes de usarlo
				if (m_currentArrowTypeIndex < 0 || m_currentArrowTypeIndex >= AvailableArrowTypes.Length)
				{
					m_currentArrowTypeIndex = 0; // Resetear a un valor seguro
				}

				ArrowBlock.ArrowType arrowType = AvailableArrowTypes[m_currentArrowTypeIndex];

				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				firePosition.Y -= 0.1f;

				Vector3 targetPosition = m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				float currentAccuracy = Accuracy * (1.5f - m_currentDraw);

				direction += new Vector3(
					m_random.Float(-currentAccuracy, currentAccuracy),
					m_random.Float(-currentAccuracy * 0.5f, currentAccuracy * 0.5f),
					m_random.Float(-currentAccuracy, currentAccuracy)
				);
				direction = Vector3.Normalize(direction);

				float speedMultiplier = 0.5f + (m_currentDraw * 1.5f);
				float currentSpeed = ArrowSpeed * speedMultiplier;

				int arrowData = ArrowBlock.SetArrowType(0, arrowType);
				int arrowValue = Terrain.MakeBlockValue(ArrowBlock.Index, 0, arrowData);

				var projectile = m_subsystemProjectiles.FireProjectile(
					arrowValue,
					firePosition,
					direction * currentSpeed,
					Vector3.Zero,
					m_componentCreature
				);

				if (arrowType == ArrowBlock.ArrowType.FireArrow && projectile != null)
				{
					m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero,
						new SmokeTrailParticleSystem(15, 0.5f, float.MaxValue, Color.White));
					projectile.IsIncendiary = true;
				}
			}
			catch (Exception ex)
			{
				Log.Error($"Error en ShootArrow(): {ex.Message}");
			}
		}
	}
}
