using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureAI : ComponentBehavior, IUpdateable
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
		private SubsystemNoise m_subsystemNoise;
		private SubsystemTerrain m_subsystemTerrain;
		private ComponentMiner m_componentMiner;
		private Random m_random = new Random();

		// Tipos de flechas disponibles para arco
		private ArrowBlock.ArrowType[] m_bowArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.FireArrow,
			ArrowBlock.ArrowType.CopperArrow
		};

		// Tipos de virotes disponibles para ballesta
		private ArrowBlock.ArrowType[] m_crossbowBoltTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
		};

		// Parámetro configurable desde la base de datos
		public bool CanUseInventory = false;

		// Estados (exactamente como en los shooters)
		private bool m_isAiming = false;
		private bool m_isDrawing = false;
		private bool m_isFiring = false;
		private bool m_isReloading = false;
		private bool m_isCocking = false;
		private double m_animationStartTime;
		private double m_drawStartTime;
		private double m_fireTime;

		// Estado específico por arma
		private int m_currentWeaponSlot = -1;
		private int m_weaponType = -1; // 0=Arco, 1=Ballesta, 2=Mosquete
		private float m_currentDraw = 0f;
		private ArrowBlock.ArrowType m_currentArrowType;
		private ArrowBlock.ArrowType m_currentBoltType;
		private float m_currentTargetDistance = 0f;
		private bool m_arrowVisible = false;
		private bool m_hasArrowInBow = false;

		// Configuraciones (igual que los shooters originales)
		private float m_maxDistance = 25f;
		private float m_drawTime = 1.2f;
		private float m_aimTime = 0.5f;
		private float m_reloadTime = 0.8f;
		private float m_cockTime = 0.5f;

		// Distancia mínima para usar virotes explosivos
		private float m_explosiveBoltMinDistance = 15f;

		// UpdateOrder
		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar únicamente el parámetro CanUseInventory
			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);

			// Inicializar componentes
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
			m_componentInventory = Entity.FindComponent<ComponentInventory>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentModel = Entity.FindComponent<ComponentCreatureModel>();

			// Inicializar subsystems
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
		}

		public void Update(float dt)
		{
			if (!CanUseInventory || m_componentCreature == null || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = m_componentChaseBehavior?.Target;

			if (target != null && target.ComponentHealth.Health > 0f)
			{
				float distance = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					target.ComponentBody.Position
				);

				// Guardar la distancia actual para usarla en la selección de virotes
				m_currentTargetDistance = distance;

				if (distance <= m_maxDistance)
				{
					if (m_currentWeaponSlot == -1)
					{
						FindRangedWeapon();
					}

					if (m_currentWeaponSlot != -1)
					{
						ProcessWeaponBehavior(target, distance);
					}
				}
				else
				{
					ResetWeaponState();
				}
			}
			else
			{
				ResetWeaponState();
			}
		}

		// Método auxiliar para seleccionar el tipo de virote basado en la distancia
		private ArrowBlock.ArrowType SelectBoltTypeBasedOnDistance(float distance)
		{
			// Filtrar los tipos de virotes disponibles según la distancia
			List<ArrowBlock.ArrowType> availableBolts = new List<ArrowBlock.ArrowType>(m_crossbowBoltTypes);

			// Si la distancia es menor a la mínima, excluir los virotes explosivos
			if (distance < m_explosiveBoltMinDistance)
			{
				availableBolts.Remove(ArrowBlock.ArrowType.ExplosiveBolt);
			}

			// Si no hay virotes disponibles después de filtrar, usar IronBolt como predeterminado
			if (availableBolts.Count == 0)
			{
				availableBolts.Add(ArrowBlock.ArrowType.IronBolt);
			}

			// Seleccionar aleatoriamente de los virotes disponibles
			return availableBolts[m_random.Int(0, availableBolts.Count - 1)];
		}

		private void FindRangedWeapon()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];

					if (block is BowBlock)
					{
						m_currentWeaponSlot = i;
						m_weaponType = 0;
						m_componentInventory.ActiveSlotIndex = i;
						StartAiming();
						break;
					}
					else if (block is CrossbowBlock)
					{
						m_currentWeaponSlot = i;
						m_weaponType = 1;
						m_componentInventory.ActiveSlotIndex = i;
						StartAiming();
						break;
					}
					else if (block is MusketBlock)
					{
						m_currentWeaponSlot = i;
						m_weaponType = 2;
						m_componentInventory.ActiveSlotIndex = i;
						StartAiming();
						break;
					}
				}
			}
		}

		private void ProcessWeaponBehavior(ComponentCreature target, float distance)
		{
			switch (m_weaponType)
			{
				case 0: ProcessBowBehavior(target); break;
				case 1: ProcessCrossbowBehavior(target, distance); break;
				case 2: ProcessMusketBehavior(target); break;
			}
		}

		#region Comportamiento de Arco - CORREGIDO PARA EVITAR PARPADEO
		private void ProcessBowBehavior(ComponentCreature target)
		{
			if (!m_isAiming && !m_isDrawing && !m_isFiring)
			{
				StartAiming();
			}

			if (m_isAiming)
			{
				ApplyBowAimingAnimation(target);

				if (m_subsystemTime.GameTime - m_animationStartTime >= m_aimTime)
				{
					m_isAiming = false;
					StartBowDrawing();
				}
			}
			else if (m_isDrawing)
			{
				ApplyBowDrawingAnimation(target);

				m_currentDraw = MathUtils.Clamp((float)((m_subsystemTime.GameTime - m_drawStartTime) / m_drawTime), 0f, 1f);

				// ACTUALIZAR LA TENSIÓN SOLO UNA VEZ CUANDO EMPIEZA EL TENSAO
				if (!m_hasArrowInBow)
				{
					SetBowWithArrow((int)(m_currentDraw * 15f));
					m_hasArrowInBow = true;
				}
				else
				{
					// Solo actualizar el valor de tensión, no la flecha completa
					UpdateBowDraw((int)(m_currentDraw * 15f));
				}

				if (m_subsystemTime.GameTime - m_drawStartTime >= m_drawTime)
				{
					FireBow(target);
				}
			}
			else if (m_isFiring)
			{
				ApplyBowFiringAnimation();

				if (m_subsystemTime.GameTime - m_fireTime >= 0.2)
				{
					m_isFiring = false;

					// Solo quitar la flecha una vez al final de la animación
					if (m_hasArrowInBow)
					{
						ClearArrowFromBow();
						m_hasArrowInBow = false;
					}

					if (m_subsystemTime.GameTime - m_fireTime >= 0.8)
					{
						StartAiming();
					}
				}
			}
		}

		private void SetBowWithArrow(int drawValue)
		{
			if (m_currentWeaponSlot < 0) return;

			try
			{
				int currentBowValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				if (currentBowValue == 0) return;

				int currentData = Terrain.ExtractData(currentBowValue);

				// Seleccionar tipo de flecha aleatorio para el arco
				m_currentArrowType = m_bowArrowTypes[m_random.Int(0, m_bowArrowTypes.Length - 1)];

				int newData = BowBlock.SetArrowType(currentData, m_currentArrowType);
				newData = BowBlock.SetDraw(newData, MathUtils.Clamp(drawValue, 0, 15));

				int newBowValue = Terrain.ReplaceData(currentBowValue, newData);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newBowValue, 1);
				m_arrowVisible = true;
			}
			catch { }
		}

		private void UpdateBowDraw(int drawValue)
		{
			if (m_currentWeaponSlot < 0 || !m_arrowVisible) return;

			try
			{
				int currentBowValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				if (currentBowValue == 0) return;

				int currentData = Terrain.ExtractData(currentBowValue);

				// Solo actualizar la tensión, no cambiar el tipo de flecha
				int newData = BowBlock.SetDraw(currentData, MathUtils.Clamp(drawValue, 0, 15));

				int newBowValue = Terrain.ReplaceData(currentBowValue, newData);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newBowValue, 1);
			}
			catch { }
		}

		private void ClearArrowFromBow()
		{
			if (m_currentWeaponSlot < 0) return;

			try
			{
				int currentBowValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				if (currentBowValue == 0) return;

				int currentData = Terrain.ExtractData(currentBowValue);
				int newData = BowBlock.SetArrowType(currentData, null);
				newData = BowBlock.SetDraw(newData, 0);

				int newBowValue = Terrain.ReplaceData(currentBowValue, newData);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newBowValue, 1);
				m_arrowVisible = false;
			}
			catch { }
		}

		private void StartBowDrawing()
		{
			m_isDrawing = true;
			m_drawStartTime = m_subsystemTime.GameTime;

			// Sonido de tensado del arco (exactamente como el original)
			m_subsystemAudio.PlaySound("Audio/BowDraw", 0.5f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);
		}

		private void ApplyBowAimingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				// Animación idéntica al shooter original
				m_componentModel.AimHandAngleOrder = 0.2f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(0.05f, 0.05f, 0.05f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-0.05f, 0.3f, 0.05f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void ApplyBowDrawingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				float drawFactor = m_currentDraw;

				// Animación idéntica al shooter original
				float horizontalOffset = 0.05f - (0.03f * drawFactor);
				float verticalOffset = 0.05f + (0.02f * drawFactor);
				float depthOffset = 0.05f - (0.02f * drawFactor);

				float pitchRotation = -0.05f - (0.1f * drawFactor);
				float yawRotation = 0.3f - (0.05f * drawFactor);
				float rollRotation = 0.05f - (0.02f * drawFactor);

				m_componentModel.AimHandAngleOrder = 0.2f + (0.3f * drawFactor);
				m_componentModel.InHandItemOffsetOrder = new Vector3(horizontalOffset, verticalOffset, depthOffset);
				m_componentModel.InHandItemRotationOrder = new Vector3(pitchRotation, yawRotation, rollRotation);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void FireBow(ComponentCreature target)
		{
			m_isDrawing = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			// Disparar flecha (igual que el original)
			ShootBowArrow(target);

			// Sonido de disparo del arco (exactamente igual)
			m_subsystemAudio.PlaySound("Audio/Bow", 0.8f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 15f, false);
		}

		private void ApplyBowFiringAnimation()
		{
			if (m_componentModel != null)
			{
				float fireProgress = (float)((m_subsystemTime.GameTime - m_fireTime) / 0.2);

				if (fireProgress < 0.5f)
				{
					float recoil = 0.02f * (1f - (fireProgress * 2f));
					m_componentModel.InHandItemOffsetOrder += new Vector3(recoil, 0f, 0f);
					m_componentModel.InHandItemRotationOrder += new Vector3(recoil * 2f, 0f, 0f);
				}
				else
				{
					float returnProgress = (fireProgress - 0.5f) / 0.5f;
					m_componentModel.AimHandAngleOrder = 0.2f * (1f - returnProgress);
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						0.05f * (1f - returnProgress),
						0.05f * (1f - returnProgress),
						0.05f * (1f - returnProgress)
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(
						-0.05f * (1f - returnProgress),
						0.3f * (1f - returnProgress),
						0.05f * (1f - returnProgress)
					);
				}
			}
		}
		#endregion

		#region Comportamiento de Ballesta
		private void ProcessCrossbowBehavior(ComponentCreature target, float distance)
		{
			if (!m_isAiming && !m_isDrawing && !m_isFiring && !m_isReloading)
			{
				StartAiming();
			}

			if (m_isAiming)
			{
				ApplyCrossbowAimingAnimation(target);

				if (m_subsystemTime.GameTime - m_animationStartTime >= m_aimTime)
				{
					m_isAiming = false;
					StartCrossbowDrawing(distance);
				}
			}
			else if (m_isDrawing)
			{
				ApplyCrossbowDrawingAnimation(target);

				m_currentDraw = MathUtils.Clamp((float)((m_subsystemTime.GameTime - m_drawStartTime) / m_drawTime), 0f, 1f);
				SetCrossbowWithBolt((int)(m_currentDraw * 15f), false, distance);

				if (m_subsystemTime.GameTime - m_drawStartTime >= m_drawTime)
				{
					LoadCrossbowBolt(distance);
				}
			}
			else if (m_isReloading)
			{
				ApplyCrossbowReloadingAnimation(target);

				if (m_subsystemTime.GameTime - m_animationStartTime >= 0.3f)
				{
					m_isReloading = false;
					FireCrossbow(target);
				}
			}
			else if (m_isFiring)
			{
				ApplyCrossbowFiringAnimation();

				if (m_subsystemTime.GameTime - m_fireTime >= 0.2f)
				{
					m_isFiring = false;
					ClearBoltFromCrossbow();

					if (m_subsystemTime.GameTime - m_fireTime >= 0.8f)
					{
						StartAiming();
					}
				}
			}
		}

		private void SetCrossbowWithBolt(int drawValue, bool hasBolt, float distance)
		{
			if (m_currentWeaponSlot < 0) return;

			try
			{
				int currentCrossbowValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				if (currentCrossbowValue == 0) return;

				int currentData = Terrain.ExtractData(currentCrossbowValue);

				// Seleccionar tipo de virote basado en la distancia
				if (hasBolt)
				{
					m_currentBoltType = SelectBoltTypeBasedOnDistance(distance);
				}

				ArrowBlock.ArrowType? boltType = hasBolt ? m_currentBoltType : (ArrowBlock.ArrowType?)null;

				int newData = CrossbowBlock.SetDraw(currentData, MathUtils.Clamp(drawValue, 0, 15));
				newData = CrossbowBlock.SetArrowType(newData, boltType);

				int newCrossbowValue = Terrain.ReplaceData(currentCrossbowValue, newData);
				m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
				m_componentInventory.AddSlotItems(m_currentWeaponSlot, newCrossbowValue, 1);
			}
			catch { }
		}

		private void ClearBoltFromCrossbow()
		{
			SetCrossbowWithBolt(0, false, m_currentTargetDistance);
		}

		private void StartCrossbowDrawing(float distance)
		{
			m_isDrawing = true;
			m_drawStartTime = m_subsystemTime.GameTime;

			// Sonido de tensado de ballesta (igual que el original)
			m_subsystemAudio.PlaySound("Audio/CrossbowDraw", 0.5f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);
		}

		private void LoadCrossbowBolt(float distance)
		{
			m_isDrawing = false;
			m_isReloading = true;
			m_animationStartTime = m_subsystemTime.GameTime;

			// Cargar virote en la ballesta (tensada completamente)
			SetCrossbowWithBolt(15, true, distance);

			// Sonido de recarga (igual que el original)
			m_subsystemAudio.PlaySound("Audio/Reload", 1f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);
		}

		private void ApplyCrossbowAimingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				// Animación idéntica al shooter original
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void ApplyCrossbowDrawingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				float drawFactor = m_currentDraw;

				// Animación idéntica al shooter original
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.08f + (0.05f * drawFactor),
					-0.08f,
					0.07f - (0.03f * drawFactor)
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void ApplyCrossbowReloadingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / 0.3f);

				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.08f,
					-0.08f - (0.05f * reloadProgress),
					0.07f
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void FireCrossbow(ComponentCreature target)
		{
			m_isReloading = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			// Disparar virote
			ShootCrossbowBolt(target);

			// Sonido de disparo (igual que el original - usa Audio/Bow)
			m_subsystemAudio.PlaySound("Audio/Bow", 0.8f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 15f, false);

			// Retroceso (igual que el original)
			if (target != null)
			{
				Vector3 direction = Vector3.Normalize(
					target.ComponentBody.Position - m_componentCreature.ComponentBody.Position
				);
				m_componentCreature.ComponentBody.ApplyImpulse(-direction * 1.5f);
			}
		}

		private void ApplyCrossbowFiringAnimation()
		{
			if (m_componentModel != null)
			{
				float fireProgress = (float)((m_subsystemTime.GameTime - m_fireTime) / 0.2f);

				if (fireProgress < 0.5f)
				{
					float recoil = 0.05f * (1f - (fireProgress * 2f));
					m_componentModel.InHandItemOffsetOrder += new Vector3(recoil, 0f, 0f);
					m_componentModel.InHandItemRotationOrder += new Vector3(recoil * 2f, 0f, 0f);
				}
				else
				{
					float returnProgress = (fireProgress - 0.5f) / 0.5f;
					m_componentModel.AimHandAngleOrder = 1.4f * (1f - returnProgress);
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						-0.08f * (1f - returnProgress),
						-0.08f * (1f - returnProgress),
						0.07f * (1f - returnProgress)
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(
						-1.7f * (1f - returnProgress),
						0f,
						0f
					);
				}
			}
		}
		#endregion

		#region Comportamiento de Mosquete - MEJORADO
		private void ProcessMusketBehavior(ComponentCreature target)
		{
			if (!m_isAiming && !m_isFiring && !m_isReloading && !m_isCocking)
			{
				StartAiming();
			}

			if (m_isCocking)
			{
				// ANIMACIÓN DE AMARTILLADO CON MOVIMIENTO PROGRESIVO
				float cockProgress = (float)((m_subsystemTime.GameTime - m_drawStartTime) / m_cockTime);

				if (m_componentModel != null)
				{
					// Movimiento de las manos durante el amartillado
					m_componentModel.AimHandAngleOrder = 1.2f + (0.2f * cockProgress);
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						-0.08f + (0.03f * cockProgress),
						-0.08f - (0.01f * cockProgress),
						0.07f + (0.01f * cockProgress)
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(
						-1.6f - (0.1f * cockProgress),
						0f,
						0f
					);

					if (target != null)
					{
						m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
					}
				}

				if (m_subsystemTime.GameTime - m_drawStartTime >= m_cockTime)
				{
					m_isCocking = false;
					m_isAiming = true;
					m_animationStartTime = m_subsystemTime.GameTime;

					// Asegurar que el martillo esté visualmente amartillado
					UpdateMusketHammerState(true);
				}
			}
			else if (m_isAiming)
			{
				ApplyMusketAimingAnimation(target);

				if (m_subsystemTime.GameTime - m_animationStartTime >= m_aimTime)
				{
					FireMusket(target);
				}
			}
			else if (m_isFiring)
			{
				ApplyMusketFiringAnimation();

				if (m_subsystemTime.GameTime - m_fireTime >= 0.2)
				{
					m_isFiring = false;
					StartMusketReloading();
				}
			}
			else if (m_isReloading)
			{
				ApplyMusketReloadingAnimation();

				if (m_subsystemTime.GameTime - m_animationStartTime >= m_reloadTime)
				{
					m_isReloading = false;
					UpdateMusketLoadState(MusketBlock.LoadState.Loaded);
					UpdateMusketBulletType(BulletBlock.BulletType.MusketBall);

					// Volver a amartillar después de recargar
					StartMusketCocking();
				}
			}
		}

		private void StartMusketCocking()
		{
			m_isCocking = true;
			m_isAiming = false;
			m_drawStartTime = m_subsystemTime.GameTime;

			// ACTUALIZAR VISUALMENTE EL MARTILLO INMEDIATAMENTE
			UpdateMusketHammerState(true);

			// Sonido de amartillar (igual que el original)
			m_subsystemAudio.PlaySound("Audio/HammerCock", 1f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);

			// MOSTRAR ANIMACIÓN DE AMARTILLADO INMEDIATAMENTE
			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 1.2f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.6f, 0f, 0f);
			}
		}

		private void StartMusketReloading()
		{
			m_isReloading = true;
			m_animationStartTime = m_subsystemTime.GameTime;

			// Sonido de recarga (igual que el original)
			m_subsystemAudio.PlaySound("Audio/Reload", 1.5f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 5f, false);
		}

		private void UpdateMusketHammerState(bool hammerState)
		{
			if (m_currentWeaponSlot >= 0)
			{
				int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				int contents = Terrain.ExtractContents(slotValue);
				if (BlocksManager.Blocks[contents] is MusketBlock)
				{
					int data = Terrain.ExtractData(slotValue);
					data = MusketBlock.SetHammerState(data, hammerState);
					int newValue = Terrain.ReplaceData(slotValue, data);
					m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
					m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
				}
			}
		}

		private void UpdateMusketLoadState(MusketBlock.LoadState loadState)
		{
			if (m_currentWeaponSlot >= 0)
			{
				int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				int contents = Terrain.ExtractContents(slotValue);
				if (BlocksManager.Blocks[contents] is MusketBlock)
				{
					int data = Terrain.ExtractData(slotValue);
					data = MusketBlock.SetLoadState(data, loadState);
					int newValue = Terrain.ReplaceData(slotValue, data);
					m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
					m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
				}
			}
		}

		private void UpdateMusketBulletType(BulletBlock.BulletType bulletType)
		{
			if (m_currentWeaponSlot >= 0)
			{
				int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				int contents = Terrain.ExtractContents(slotValue);
				if (BlocksManager.Blocks[contents] is MusketBlock)
				{
					int data = Terrain.ExtractData(slotValue);
					data = MusketBlock.SetBulletType(data, bulletType);
					int newValue = Terrain.ReplaceData(slotValue, data);
					m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
					m_componentInventory.AddSlotItems(m_currentWeaponSlot, newValue, 1);
				}
			}
		}

		private void ApplyMusketAimingAnimation(ComponentCreature target)
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE APUNTADO CON MARTILLO AMARTILLADO
				m_componentModel.AimHandAngleOrder = 1.4f;
				m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

				// PEQUEÑO MOVIMIENTO DE RESPIRACIÓN
				float breath = (float)Math.Sin(m_subsystemTime.GameTime * 3f) * 0.01f;
				m_componentModel.InHandItemOffsetOrder += new Vector3(0f, breath, 0f);

				if (target != null)
				{
					m_componentModel.LookAtOrder = new Vector3?(target.ComponentCreatureModel.EyePosition);
				}
			}
		}

		private void FireMusket(ComponentCreature target)
		{
			m_isAiming = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			// Asegurar que el martillo esté visualmente amartillado antes de disparar
			UpdateMusketHammerState(true);

			// Pequeña pausa antes del sonido de disparo (para drama)
			double fireDelay = 0.05;

			// Programar los sonidos con retardo
			m_subsystemAudio.PlaySound("Audio/HammerUncock", 1f, m_random.Float(-0.1f, 0.1f),
				m_componentCreature.ComponentBody.Position, 3f, false);

			// Usar un timer para el sonido de disparo (como en el original)
			m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + fireDelay, delegate
			{
				m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, m_random.Float(-0.1f, 0.1f),
					m_componentCreature.ComponentBody.Position, 15f, false);
			});

			// Actualizar estado visual del martillo después de disparar
			UpdateMusketHammerState(false);
			UpdateMusketLoadState(MusketBlock.LoadState.Empty);

			// Disparar bala
			ShootMusketBullet(target);

			// Retroceso
			if (target != null)
			{
				Vector3 direction = Vector3.Normalize(
					target.ComponentBody.Position - m_componentCreature.ComponentBody.Position
				);
				m_componentCreature.ComponentBody.ApplyImpulse(-direction * 3f);
			}
		}

		private void ApplyMusketFiringAnimation()
		{
			if (m_componentModel != null)
			{
				// ANIMACIÓN DE DISPARO CON RETROCESO MÁS NOTORIO
				float fireProgress = (float)((m_subsystemTime.GameTime - m_fireTime) / 0.2f);

				if (fireProgress < 0.5f)
				{
					// Retroceso fuerte
					float recoil = 0.1f * (1f - (fireProgress * 2f));
					m_componentModel.InHandItemOffsetOrder += new Vector3(recoil, 0f, 0f);
					m_componentModel.InHandItemRotationOrder += new Vector3(recoil * 3f, 0f, 0f);
				}
				else
				{
					// Retorno suave
					float returnProgress = (fireProgress - 0.5f) / 0.5f;
					m_componentModel.AimHandAngleOrder = 1.4f * (1f - returnProgress);
					m_componentModel.InHandItemOffsetOrder = new Vector3(
						-0.08f * (1f - returnProgress),
						-0.08f * (1f - returnProgress),
						0.07f * (1f - returnProgress)
					);
					m_componentModel.InHandItemRotationOrder = new Vector3(
						-1.7f * (1f - returnProgress),
						0f,
						0f
					);
				}
			}
		}

		private void ApplyMusketReloadingAnimation()
		{
			if (m_componentModel != null)
			{
				float reloadProgress = (float)((m_subsystemTime.GameTime - m_animationStartTime) / m_reloadTime);

				// Animación idéntica al shooter original
				m_componentModel.AimHandAngleOrder = MathUtils.Lerp(1.0f, 0.5f, reloadProgress);
				m_componentModel.InHandItemOffsetOrder = new Vector3(
					-0.08f,
					-0.08f,
					0.07f - (0.1f * reloadProgress)
				);
				m_componentModel.InHandItemRotationOrder = new Vector3(
					-1.7f + (0.5f * reloadProgress),
					0f,
					0f
				);

				m_componentModel.LookAtOrder = null;
			}
		}
		#endregion

		#region Métodos comunes
		private void StartAiming()
		{
			m_isAiming = true;
			m_isDrawing = false;
			m_isFiring = false;
			m_isReloading = false;
			m_isCocking = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;
			m_arrowVisible = false;
			m_hasArrowInBow = false;

			// Para arco, mostrar flecha sin tensión SOLO AL INICIO
			if (m_weaponType == 0)
			{
				SetBowWithArrow(0);
				m_hasArrowInBow = true;
			}
			// Para ballesta, mostrar sin virote
			else if (m_weaponType == 1)
			{
				SetCrossbowWithBolt(0, false, m_currentTargetDistance);
			}
			// Para mosquete, verificar si necesita recarga o amartillado
			else if (m_weaponType == 2 && m_currentWeaponSlot >= 0)
			{
				int slotValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
				int data = Terrain.ExtractData(slotValue);
				MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
				bool hammerState = MusketBlock.GetHammerState(data);

				// Si no está cargado, cargarlo
				if (loadState != MusketBlock.LoadState.Loaded)
				{
					UpdateMusketLoadState(MusketBlock.LoadState.Loaded);
					UpdateMusketBulletType(BulletBlock.BulletType.MusketBall);
				}

				// Si está cargado pero no amartillado, empezar a amartillar
				if (loadState == MusketBlock.LoadState.Loaded && !hammerState)
				{
					StartMusketCocking();
				}
				else if (loadState == MusketBlock.LoadState.Loaded && hammerState)
				{
					// Ya está amartillado, ir directamente a apuntar
					m_isAiming = true;
					m_animationStartTime = m_subsystemTime.GameTime;
				}
			}
		}

		private void ResetWeaponState()
		{
			m_isAiming = false;
			m_isDrawing = false;
			m_isFiring = false;
			m_isReloading = false;
			m_isCocking = false;
			m_currentDraw = 0f;
			m_currentWeaponSlot = -1;
			m_weaponType = -1;
			m_currentTargetDistance = 0f;
			m_arrowVisible = false;
			m_hasArrowInBow = false;

			if (m_componentModel != null)
			{
				m_componentModel.AimHandAngleOrder = 0f;
				m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				m_componentModel.InHandItemRotationOrder = Vector3.Zero;
				m_componentModel.LookAtOrder = null;
			}
		}

		private void ShootBowArrow(ComponentCreature target)
		{
			if (target == null) return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				firePosition.Y -= 0.1f;

				Vector3 targetPosition = target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				float currentAccuracy = 0.03f * (1.5f - m_currentDraw);
				direction += new Vector3(
					m_random.Float(-currentAccuracy, currentAccuracy),
					m_random.Float(-currentAccuracy * 0.5f, currentAccuracy * 0.5f),
					m_random.Float(-currentAccuracy, currentAccuracy)
				);
				direction = Vector3.Normalize(direction);

				float speedMultiplier = 0.5f + (m_currentDraw * 1.5f);
				float currentSpeed = 35f * speedMultiplier;

				// Usar el tipo de flecha seleccionado
				int arrowData = ArrowBlock.SetArrowType(0, m_currentArrowType);
				int arrowValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(), 0, arrowData);

				m_subsystemProjectiles.FireProjectile(
					arrowValue,
					firePosition,
					direction * currentSpeed,
					Vector3.Zero,
					m_componentCreature
				);
			}
			catch { }
		}

		private void ShootCrossbowBolt(ComponentCreature target)
		{
			if (target == null) return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				firePosition.Y -= 0.1f;

				Vector3 targetPosition = target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				direction += new Vector3(
					m_random.Float(-0.02f, 0.02f),
					m_random.Float(-0.01f, 0.01f),
					m_random.Float(-0.02f, 0.02f)
				);
				direction = Vector3.Normalize(direction);

				float speed = 45f * (0.8f + (m_currentDraw * 0.4f));

				// Usar el tipo de virote seleccionado
				int boltData = ArrowBlock.SetArrowType(0, m_currentBoltType);
				int boltValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(), 0, boltData);

				m_subsystemProjectiles.FireProjectile(
					boltValue,
					firePosition,
					direction * speed,
					Vector3.Zero,
					m_componentCreature
				);

				// Ruido
				if (m_subsystemNoise != null)
				{
					m_subsystemNoise.MakeNoise(firePosition, 0.5f, 20f);
				}
			}
			catch { }
		}

		private void ShootMusketBullet(ComponentCreature target)
		{
			if (target == null) return;

			try
			{
				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPosition = target.ComponentCreatureModel.EyePosition;

				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);
				direction += new Vector3(
					m_random.Float(-0.02f, 0.02f),
					m_random.Float(-0.01f, 0.01f),
					m_random.Float(-0.02f, 0.02f)
				);
				direction = Vector3.Normalize(direction);

				// Crear bala
				int bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>();
				if (bulletBlockIndex > 0)
				{
					int bulletData = BulletBlock.SetBulletType(0, BulletBlock.BulletType.MusketBall);
					int bulletValue = Terrain.MakeBlockValue(bulletBlockIndex, 0, bulletData);

					m_subsystemProjectiles.FireProjectile(
						bulletValue,
						firePosition,
						direction * 120f,
						Vector3.Zero,
						m_componentCreature
					);

					// Humo (igual que el original)
					Vector3 smokePosition = firePosition + direction * 0.3f;
					if (m_subsystemParticles != null && m_subsystemTerrain != null)
					{
						m_subsystemParticles.AddParticleSystem(
							new GunSmokeParticleSystem(m_subsystemTerrain, smokePosition, direction),
							false
						);
					}

					// Ruido (igual que el original)
					if (m_subsystemNoise != null)
					{
						m_subsystemNoise.MakeNoise(firePosition, 1f, 40f);
					}
				}
			}
			catch { }
		}
		#endregion
	}
}
