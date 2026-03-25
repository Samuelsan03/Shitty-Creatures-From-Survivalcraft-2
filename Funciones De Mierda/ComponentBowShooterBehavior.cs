// ComponentBowShooterBehavior.cs - Versión modificada que usa ComponentMiner.Aim
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
		private ComponentMiner m_componentMiner;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private ComponentCreatureModel m_componentModel;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private ComponentPathfinding m_componentPathfinding;

		// Configuración
		public float MaxDistance = 25f;
		public float DrawTime = 1.2f;
		public float AimTime = 0.5f;
		public float FireSoundDistance = 15f;
		public float Accuracy = 0.03f;
		public float ArrowSpeed = 35f;

		// Tipos de flechas a usar (selección aleatoria entre todas)
		private ArrowBlock.ArrowType[] m_availableArrowTypes = new ArrowBlock.ArrowType[]
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
		private float m_currentDraw = 0f;
		private Random m_random = new Random();
		private bool m_initialized = false;

		// Tipo de flecha seleccionado para el próximo disparo
		private ArrowBlock.ArrowType m_nextArrowType = ArrowBlock.ArrowType.WoodenArrow;

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

			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(false);
		}

		public override void OnEntityAdded()
		{
			base.OnEntityAdded();
			m_initialized = true;
			FindBow();
		}

		private bool IsStuck() => m_componentPathfinding != null && m_componentPathfinding.IsStuck;

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
			return bodyHit != null && bodyHit.Value.Distance < distance;
		}

		private ComponentCreature GetChaseTarget()
		{
			return m_componentChaseBehavior?.Target;
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

			ComponentCreature target = GetChaseTarget();

			if (target == null || IsStuck() || IsLineOfSightBlocked(target))
			{
				ResetState();
				if (m_bowSlot >= 0)
					ClearBowState();
				return;
			}

			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				target.ComponentBody.Position
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
				ResetState();
				if (m_bowSlot >= 0)
					ClearBowState();
				return;
			}

			// MIRAR AL OBJETIVO SIEMPRE
			if (m_componentModel != null && target != null)
			{
				m_componentModel.LookAtOrder = target.ComponentCreatureModel.EyePosition;
			}

			// Ciclo de disparo usando Aim
			if (m_isAiming)
			{
				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPos = target.ComponentBody.Position;
				targetPos.Y += target.ComponentBody.BoxSize.Y * 0.5f;
				Vector3 dir = Vector3.Normalize(targetPos - eyePos);
				Ray3 aimRay = new Ray3(eyePos, dir);
				m_componentMiner.Aim(aimRay, AimState.InProgress);

				if (m_subsystemTime.GameTime - m_animationStartTime >= AimTime)
				{
					m_isAiming = false;
					StartDrawing();
				}
			}
			else if (m_isDrawing)
			{
				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPos = target.ComponentBody.Position;
				targetPos.Y += target.ComponentBody.BoxSize.Y * 0.5f;
				Vector3 dir = Vector3.Normalize(targetPos - eyePos);
				Ray3 aimRay = new Ray3(eyePos, dir);
				m_componentMiner.Aim(aimRay, AimState.InProgress);

				m_currentDraw = MathUtils.Clamp((float)((m_subsystemTime.GameTime - m_drawStartTime) / DrawTime), 0f, 1f);

				if (m_subsystemTime.GameTime - m_drawStartTime >= DrawTime)
				{
					m_isDrawing = false;
					Fire(target);
				}
			}
			else if (m_isFiring)
			{
				if (m_subsystemTime.GameTime - m_fireTime >= 0.2)
				{
					m_isFiring = false;
					if (m_subsystemTime.GameTime - m_fireTime >= 0.8)
					{
						StartAiming();
					}
				}
			}
		}

		private void FindBow()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0 && BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is BowBlock)
				{
					m_bowSlot = i;
					m_componentInventory.ActiveSlotIndex = i;
					break;
				}
			}
		}

		private void SetBowArrowType(ArrowBlock.ArrowType arrowType)
		{
			if (m_bowSlot < 0) return;
			int bowValue = m_componentInventory.GetSlotValue(m_bowSlot);
			if (bowValue == 0) return;

			int data = Terrain.ExtractData(bowValue);
			data = BowBlock.SetArrowType(data, arrowType);
			int newValue = Terrain.ReplaceData(bowValue, data);

			m_componentInventory.RemoveSlotItems(m_bowSlot, 1);
			m_componentInventory.AddSlotItems(m_bowSlot, newValue, 1);
		}

		private void ClearBowState()
		{
			if (m_bowSlot < 0) return;
			int bowValue = m_componentInventory.GetSlotValue(m_bowSlot);
			if (bowValue == 0) return;

			int data = Terrain.ExtractData(bowValue);
			data = BowBlock.SetDraw(data, 0);
			data = BowBlock.SetArrowType(data, null);
			int newValue = Terrain.ReplaceData(bowValue, data);

			m_componentInventory.RemoveSlotItems(m_bowSlot, 1);
			m_componentInventory.AddSlotItems(m_bowSlot, newValue, 1);
		}

		private void StartAiming()
		{
			m_isAiming = true;
			m_isDrawing = false;
			m_isFiring = false;
			m_animationStartTime = m_subsystemTime.GameTime;
			m_currentDraw = 0f;

			// Seleccionar tipo de flecha aleatorio para este disparo
			m_nextArrowType = m_availableArrowTypes[m_random.Int(0, m_availableArrowTypes.Length - 1)];

			// Pre-cargar la flecha en el arco (sin tensar)
			SetBowArrowType(m_nextArrowType);

			if (m_bowSlot >= 0 && m_componentInventory.ActiveSlotIndex != m_bowSlot)
				m_componentInventory.ActiveSlotIndex = m_bowSlot;
		}

		private void StartDrawing()
		{
			m_isAiming = false;
			m_isDrawing = true;
			m_isFiring = false;
			m_drawStartTime = m_subsystemTime.GameTime;
		}

		private void Fire(ComponentCreature target)
		{
			m_isDrawing = false;
			m_isFiring = true;
			m_fireTime = m_subsystemTime.GameTime;

			// Llamar a Aim con estado Completed
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = target.ComponentBody.Position;
			targetPos.Y += target.ComponentBody.BoxSize.Y * 0.5f;
			Vector3 dir = Vector3.Normalize(targetPos - eyePos);
			Ray3 aimRay = new Ray3(eyePos, dir);
			m_componentMiner.Aim(aimRay, AimState.Completed);
		}

		private void ResetState()
		{
			m_isAiming = false;
			m_isDrawing = false;
			m_isFiring = false;
			m_currentDraw = 0f;

			if (m_componentMiner != null)
			{
				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				m_componentMiner.Aim(new Ray3(eyePos, Vector3.Zero), AimState.Cancelled);
			}

			if (m_componentModel != null)
				m_componentModel.LookAtOrder = null;
		}
	}
}
