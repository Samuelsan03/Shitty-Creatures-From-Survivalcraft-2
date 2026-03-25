using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCrossbowShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Componentes necesarios
		private ComponentCreature m_componentCreature;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentInventory m_componentInventory;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentCreatureModel m_componentModel;

		// Configuración
		public float MaxDistance = 25f;
		public float DrawTime = 1.5f;           // Tiempo de tensado
		public float AimTime = 0.5f;             // Tiempo de apuntado antes de tensar
		public float ReloadTime = 0.8f;           // Tiempo de carga del virote
		public float Accuracy = 0.02f;            // Dispersión
		public float BoltSpeed = 45f;             // Velocidad base del virote

		// Tipos de virotes disponibles
		public ArrowBlock.ArrowType[] AvailableBoltTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
		};

		// Estado
		private bool m_isAiming = false;
		private bool m_isDrawing = false;
		private bool m_isFiring = false;
		private bool m_isReloading = false;
		private double m_animationStartTime;
		private double m_drawStartTime;
		private double m_fireTime;
		private int m_crossbowSlot = -1;
		private float m_currentDraw = 0f;
		private Random m_random = new Random();
		private bool m_initialized = false;
		private ArrowBlock.ArrowType? m_nextBoltType = null;

		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros
			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 25f);
			DrawTime = valuesDictionary.GetValue<float>("DrawTime", 1.5f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 0.5f);
			ReloadTime = valuesDictionary.GetValue<float>("ReloadTime", 0.8f);
			Accuracy = valuesDictionary.GetValue<float>("Accuracy", 0.02f);
			BoltSpeed = valuesDictionary.GetValue<float>("BoltSpeed", 45f);

			// Componentes
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(false);
			m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
		}

		public override void OnEntityAdded()
		{
			base.OnEntityAdded();
			m_initialized = true;
			FindCrossbow();

			// Dejar la ballesta sin virote
			if (m_crossbowSlot >= 0)
				SetCrossbowWithBolt(0, false);
		}

		private bool IsStuck() => m_componentPathfinding != null && m_componentPathfinding.IsStuck;

		private bool IsLineOfSightBlocked(ComponentCreature target)
		{
			if (target == null) return true;
			Vector3 start = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 end = target.ComponentCreatureModel.EyePosition;
			float distance = Vector3.Distance(start, end);
			if (distance <= 0.1f) return false;

			TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(start, end, true, true,
				(int value, float d) =>
				{
					int contents = Terrain.ExtractContents(value);
					Block block = BlocksManager.Blocks[contents];
					return block.IsCollidable_(value);
				});
			if (terrainHit != null && terrainHit.Value.Distance < distance) return true;

			BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(start, end, 0.35f,
				(ComponentBody body, float dist) =>
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

		public void Update(float dt)
		{
			if (!m_initialized || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = GetChaseTarget();

			if (target == null || IsStuck() || IsLineOfSightBlocked(target))
			{
				ResetState();
				if (m_crossbowSlot >= 0)
					SetCrossbowWithBolt(0, false);
				return;
			}

			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				target.ComponentBody.Position
			);

			if (distance <= MaxDistance)
			{
				// Iniciar secuencia si no estamos en ningún estado
				if (!m_isAiming && !m_isDrawing && !m_isFiring && !m_isReloading)
					StartAiming(target);
			}
			else
			{
				ResetState();
				if (m_crossbowSlot >= 0)
					SetCrossbowWithBolt(0, false);
				return;
			}

			// Mirar al objetivo (solo visual, sin animación extra)
			if (m_componentModel != null && target != null)
				m_componentModel.LookAtOrder = target.ComponentCreatureModel.EyePosition;

			// Máquina de estados
			if (m_isAiming)
			{
				if (m_subsystemTime.GameTime - m_animationStartTime >= AimTime)
				{
					m_isAiming = false;
					StartDrawing();
				}
			}
			else if (m_isDrawing)
			{
				m_currentDraw = MathUtils.Clamp((float)((m_subsystemTime.GameTime - m_drawStartTime) / DrawTime), 0f, 1f);
				SetCrossbowWithBolt((int)(m_currentDraw * 15f), false);

				if (m_subsystemTime.GameTime - m_drawStartTime >= DrawTime)
				{
					LoadBolt(target);
				}
			}
			else if (m_isReloading)
			{
				if (m_subsystemTime.GameTime - m_animationStartTime >= ReloadTime)
				{
					m_isReloading = false;
					Fire(target);
				}
			}
			else if (m_isFiring)
			{
				if (m_subsystemTime.GameTime - m_fireTime >= 0.2f) // breve pausa tras disparo
				{
					m_isFiring = false;
					ClearBoltFromCrossbow();
					// Volver a empezar el ciclo (el siguiente frame empezará a apuntar)
				}
			}
		}

		private void FindCrossbow()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					if (block is CrossbowBlock)
					{
						m_crossbowSlot = i;
						m_componentInventory.ActiveSlotIndex = i;
						break;
					}
				}
			}
		}

		private void SetCrossbowWithBolt(int drawValue, bool hasBolt)
		{
			if (m_crossbowSlot < 0)
			{
				FindCrossbow();
				if (m_crossbowSlot < 0) return;
			}

			try
			{
				int currentCrossbowValue = m_componentInventory.GetSlotValue(m_crossbowSlot);
				if (currentCrossbowValue == 0) return;

				int currentData = Terrain.ExtractData(currentCrossbowValue);
				ArrowBlock.ArrowType? boltType = hasBolt ? m_nextBoltType : null;

				int newData = CrossbowBlock.SetDraw(currentData, MathUtils.Clamp(drawValue, 0, 15));
				newData = CrossbowBlock.SetArrowType(newData, boltType);

				int newCrossbowValue = Terrain.ReplaceData(currentCrossbowValue, newData);
				m_componentInventory.RemoveSlotItems(m_crossbowSlot, 1);
				m_componentInventory.AddSlotItems(m_crossbowSlot, newCrossbowValue, 1);
			}
			catch (Exception) { }
		}

		private void ClearBoltFromCrossbow() => SetCrossbowWithBolt(0, false);

		private void StartAiming(ComponentCreature target)
		{
			m_isAiming = true;
			m_isDrawing = false;
			m_isFiring = false;
			m_isReloading = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;

			if (target != null)
			{
				float distance = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					target.ComponentBody.Position
				);
				m_nextBoltType = SelectBoltTypeForDistance(distance);
			}
			else
			{
				m_nextBoltType = GetFirstNonExplosiveBoltType();
			}

			SetCrossbowWithBolt(0, false);
		}

		private void StartDrawing()
		{
			m_isAiming = false;
			m_isDrawing = true;
			m_isFiring = false;
			m_isReloading = false;
			m_drawStartTime = m_subsystemTime.GameTime;
		}

		private void LoadBolt(ComponentCreature target)
		{
			if (target != null)
			{
				float distance = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					target.ComponentBody.Position
				);
				m_nextBoltType = SelectBoltTypeForDistance(distance);
			}
			else
			{
				m_nextBoltType = GetFirstNonExplosiveBoltType();
			}

			m_isDrawing = false;
			m_isReloading = true;
			m_animationStartTime = m_subsystemTime.GameTime;

			SetCrossbowWithBolt(15, true);
		}

		private void Fire(ComponentCreature target)
		{
			m_isReloading = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			ShootBolt(target);
		}

		private void ResetState()
		{
			m_isAiming = false;
			m_isDrawing = false;
			m_isFiring = false;
			m_isReloading = false;
			m_currentDraw = 0f;
			m_nextBoltType = null;

			if (m_componentModel != null)
				m_componentModel.LookAtOrder = null;
		}

		private ArrowBlock.ArrowType? SelectBoltTypeForDistance(float distance)
		{
			const float explosiveMinDistance = 20f;

			if (distance >= explosiveMinDistance)
			{
				if (AvailableBoltTypes.Length > 0)
				{
					int index = m_random.Int(0, AvailableBoltTypes.Length - 1);
					return AvailableBoltTypes[index];
				}
			}
			else
			{
				var nonExplosiveTypes = new List<ArrowBlock.ArrowType>();
				foreach (var boltType in AvailableBoltTypes)
				{
					if (boltType != ArrowBlock.ArrowType.ExplosiveBolt)
						nonExplosiveTypes.Add(boltType);
				}
				if (nonExplosiveTypes.Count > 0)
				{
					int index = m_random.Int(0, nonExplosiveTypes.Count - 1);
					return nonExplosiveTypes[index];
				}
			}
			return AvailableBoltTypes.Length > 0 ? AvailableBoltTypes[0] : (ArrowBlock.ArrowType?)null;
		}

		private ArrowBlock.ArrowType? GetFirstNonExplosiveBoltType()
		{
			foreach (var boltType in AvailableBoltTypes)
			{
				if (boltType != ArrowBlock.ArrowType.ExplosiveBolt)
					return boltType;
			}
			return AvailableBoltTypes.Length > 0 ? AvailableBoltTypes[0] : (ArrowBlock.ArrowType?)null;
		}

		private void ShootBolt(ComponentCreature target)
		{
			if (target == null) return;

			try
			{
				ArrowBlock.ArrowType? boltType = m_nextBoltType;
				if (boltType == null)
				{
					float distance = Vector3.Distance(
						m_componentCreature.ComponentBody.Position,
						target.ComponentBody.Position
					);
					boltType = SelectBoltTypeForDistance(distance);
				}
				if (boltType == null) return;

				float currentDistance = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					target.ComponentBody.Position
				);
				if (boltType == ArrowBlock.ArrowType.ExplosiveBolt && currentDistance < 20f)
				{
					var nonExplosive = GetFirstNonExplosiveBoltType();
					if (nonExplosive != null)
						boltType = nonExplosive;
				}

				Vector3 firePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
				firePosition.Y -= 0.1f;

				Vector3 targetPosition = target.ComponentCreatureModel.EyePosition;
				Vector3 direction = Vector3.Normalize(targetPosition - firePosition);

				direction += new Vector3(
					m_random.Float(-Accuracy, Accuracy),
					m_random.Float(-Accuracy * 0.5f, Accuracy * 0.5f),
					m_random.Float(-Accuracy, Accuracy)
				);
				direction = Vector3.Normalize(direction);

				float speed = BoltSpeed * (0.8f + (m_currentDraw * 0.4f));

				int boltData = ArrowBlock.SetArrowType(0, boltType.Value);
				int boltValue = Terrain.MakeBlockValue(ArrowBlock.Index, 0, boltData);

				var projectile = m_subsystemProjectiles.FireProjectile(
					boltValue,
					firePosition,
					direction * speed,
					Vector3.Zero,
					m_componentCreature
				);

				if (projectile != null)
				{
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
					if (boltType == ArrowBlock.ArrowType.ExplosiveBolt)
						projectile.IsIncendiary = false;
				}
			}
			catch (Exception) { }
		}
	}
}
