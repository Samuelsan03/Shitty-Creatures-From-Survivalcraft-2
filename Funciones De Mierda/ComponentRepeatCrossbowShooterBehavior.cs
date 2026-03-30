// ComponentRepeatCrossbowShooterBehavior.cs - Usando ComponentMiner.Aim
using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentRepeatCrossbowShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Componentes necesarios
		private ComponentCreature m_componentCreature;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentInventory m_componentInventory;
		private ComponentMiner m_componentMiner;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private ComponentPathfinding m_componentPathfinding;
		private Game.Random m_random = new Game.Random();

		// Configuración
		public float MaxDistance = 25f;
		public float BoltSpeed = 35f;
		public float MaxInaccuracy = 0.04f;
		public bool UseRecoil = true;

		// Estado
		private bool m_isAiming = false;
		private double m_aimStartTime;
		private int m_crossbowSlot = -1;
		private bool m_hasCrossbow = false;
		private ComponentCreature m_currentTarget = null;
		private Ray3 m_currentAimRay;

		// Tipos de flechas disponibles
		private RepeatArrowBlock.ArrowType[] m_availableArrowTypes = new RepeatArrowBlock.ArrowType[]
		{
			RepeatArrowBlock.ArrowType.CopperArrow,
			RepeatArrowBlock.ArrowType.IronArrow,
			RepeatArrowBlock.ArrowType.DiamondArrow,
			RepeatArrowBlock.ArrowType.ExplosiveArrow,
			RepeatArrowBlock.ArrowType.PoisonArrow,
			RepeatArrowBlock.ArrowType.SeriousPoisonArrow
		};

		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 25f);
			BoltSpeed = valuesDictionary.GetValue<float>("BoltSpeed", 35f);
			MaxInaccuracy = valuesDictionary.GetValue<float>("MaxInaccuracy", 0.04f);
			UseRecoil = valuesDictionary.GetValue<bool>("UseRecoil", true);

			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(false);
		}

		public override void OnEntityAdded()
		{
			base.OnEntityAdded();
			FindCrossbow();
		}

		private bool IsStuck()
		{
			return m_componentPathfinding != null && m_componentPathfinding.IsStuck;
		}

		private bool IsLineOfSightBlocked(ComponentCreature target)
		{
			if (target == null) return true;
			Vector3 start = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 end = target.ComponentCreatureModel.EyePosition;
			float distance = Vector3.Distance(start, end);
			if (distance <= 0.1f) return false;

			TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(start, end, true, true, (int value, float d) =>
			{
				int contents = Terrain.ExtractContents(value);
				Block block = BlocksManager.Blocks[contents];
				return block.IsCollidable_(value);
			});

			if (terrainHit != null && terrainHit.Value.Distance < distance) return true;

			BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(start, end, 0.35f, (ComponentBody body, float dist) =>
			{
				if (body == m_componentCreature.ComponentBody) return false;
				if (body == target.ComponentBody) return false;
				return true;
			});

			if (bodyHit != null && bodyHit.Value.Distance < distance) return true;
			return false;
		}

		private ComponentCreature GetChaseTarget()
		{
			if (m_componentChaseBehavior != null && m_componentChaseBehavior.Target != null)
				return m_componentChaseBehavior.Target;
			return null;
		}

		private void FindCrossbow()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					if (block is RepeatCrossbowBlock)
					{
						m_crossbowSlot = i;
						m_componentInventory.ActiveSlotIndex = i;
						m_hasCrossbow = true;
						break;
					}
				}
			}

			if (!m_hasCrossbow)
			{
				m_crossbowSlot = -1;
			}
		}

		// Selección aleatoria normal de tipo de flecha (sin inventario)
		private RepeatArrowBlock.ArrowType? SelectArrowTypeForDistance(float distance)
		{
			const float explosiveMinDistance = 20f;

			if (distance >= explosiveMinDistance)
			{
				// A larga distancia: todos los tipos, incluyendo explosivo
				if (m_availableArrowTypes.Length > 0)
				{
					int index = m_random.Int(0, m_availableArrowTypes.Length - 1);
					return m_availableArrowTypes[index];
				}
			}
			else
			{
				// A corta distancia: todos excepto explosivo
				var nonExplosiveTypes = new List<RepeatArrowBlock.ArrowType>();
				foreach (var arrowType in m_availableArrowTypes)
				{
					if (arrowType != RepeatArrowBlock.ArrowType.ExplosiveArrow)
						nonExplosiveTypes.Add(arrowType);
				}

				if (nonExplosiveTypes.Count > 0)
				{
					int index = m_random.Int(0, nonExplosiveTypes.Count - 1);
					return nonExplosiveTypes[index];
				}
			}

			return m_availableArrowTypes.Length > 0 ? m_availableArrowTypes[0] : null;
		}

		// Carga la ballesta con un tipo aleatorio (similar a ComponentNewChaseBehavior)
		private void LoadCrossbowWithRandomBolt()
		{
			int crossbowValue = m_componentInventory.GetSlotValue(m_crossbowSlot);
			if (crossbowValue == 0) return;

			int data = Terrain.ExtractData(crossbowValue);
			int draw = RepeatCrossbowBlock.GetDraw(data);
			RepeatArrowBlock.ArrowType? currentArrowType = RepeatCrossbowBlock.GetArrowType(data);

			// Si ya está tensada y cargada, no hacer nada
			if (draw == 15 && currentArrowType != null)
				return;

			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				m_currentTarget.ComponentBody.Position
			);

			RepeatArrowBlock.ArrowType? selectedType = SelectArrowTypeForDistance(distance);
			if (!selectedType.HasValue) return;

			// Crear la nueva ballesta cargada
			int newData = RepeatCrossbowBlock.SetDraw(data, 15);
			newData = RepeatCrossbowBlock.SetArrowType(newData, selectedType.Value);
			int newValue = Terrain.ReplaceData(crossbowValue, newData);

			// Reemplazar el ítem en el inventario
			m_componentInventory.RemoveSlotItems(m_crossbowSlot, 1);
			m_componentInventory.AddSlotItems(m_crossbowSlot, newValue, 1);
		}

		public void Update(float dt)
		{
			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			if (!m_hasCrossbow)
			{
				FindCrossbow();
				if (!m_hasCrossbow) return;
			}

			ComponentCreature target = GetChaseTarget();

			if (target == null || IsStuck() || IsLineOfSightBlocked(target))
			{
				if (m_isAiming)
				{
					// Cancelar apuntado
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Ray3 cancelRay = new Ray3(eyePos, Vector3.Zero);
					m_componentMiner.Aim(cancelRay, AimState.Cancelled);
					m_isAiming = false;
					m_currentTarget = null;
				}
				return;
			}

			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				target.ComponentBody.Position
			);

			if (distance > MaxDistance)
			{
				if (m_isAiming)
				{
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Ray3 cancelRay = new Ray3(eyePos, Vector3.Zero);
					m_componentMiner.Aim(cancelRay, AimState.Cancelled);
					m_isAiming = false;
					m_currentTarget = null;
				}
				return;
			}

			// Asegurar que la ballesta está en la mano activa
			if (m_componentInventory.ActiveSlotIndex != m_crossbowSlot)
			{
				m_componentInventory.ActiveSlotIndex = m_crossbowSlot;
			}

			// Calcular rayo hacia el objetivo
			Vector3 eyePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPosition = target.ComponentBody.Position;
			targetPosition.Y += target.ComponentBody.BoxSize.Y * 0.5f;
			Vector3 direction = Vector3.Normalize(targetPosition - eyePosition);
			Ray3 aimRay = new Ray3(eyePosition, direction);

			if (!m_isAiming)
			{
				// Iniciar apuntado: primero cargar la ballesta si es necesario
				m_currentTarget = target;
				m_currentAimRay = aimRay;
				m_aimStartTime = m_subsystemTime.GameTime;
				m_isAiming = true;

				// Cargar la ballesta con un tipo aleatorio (si no está lista)
				LoadCrossbowWithRandomBolt();

				// Llamar a Aim con estado InProgress
				m_componentMiner.Aim(aimRay, AimState.InProgress);
			}
			else if (m_isAiming)
			{
				// Actualizar apuntado mientras está en progreso
				m_componentMiner.Aim(aimRay, AimState.InProgress);

				// Tiempo de apuntado antes de disparar
				float aimTime = (float)(m_subsystemTime.GameTime - m_aimStartTime);

				if (aimTime >= 1.0f)
				{
					// Verificar que la ballesta esté realmente cargada
					int crossbowValue = m_componentInventory.GetSlotValue(m_crossbowSlot);
					if (crossbowValue != 0)
					{
						int data = Terrain.ExtractData(crossbowValue);
						int draw = RepeatCrossbowBlock.GetDraw(data);
						RepeatArrowBlock.ArrowType? loadedArrow = RepeatCrossbowBlock.GetArrowType(data);

						if (draw == 15 && loadedArrow.HasValue)
						{
							// Disparar
							m_componentMiner.Aim(aimRay, AimState.Completed);
						}
						else
						{
							// Si por algún motivo no está cargada, cancelar el disparo
							m_componentMiner.Aim(aimRay, AimState.Cancelled);
						}
					}

					m_isAiming = false;
					m_currentTarget = null;
				}
			}
		}
	}
}
