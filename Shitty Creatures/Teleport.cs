using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Componente que permite a una criatura teletransportarse hacia su objetivo cuando la distancia supera un umbral.
	/// </summary>
	public class Teleport : Component, IUpdateable
	{
		// ===== PARÁMETROS CONFIGURABLES =====
		public float TeleportationDistance = 15f;
		public float TeleportationCooldown = 5f;
		public float DisappearanceTime = 0.75f;
		public float ChanceToTeleport = 0.3f;

		// ===== REFERENCIAS =====
		private SubsystemTime m_subsystemTime;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemTerrain m_subsystemTerrain;
		private Random m_random = new Random();

		private ComponentCreature m_componentCreature;
		private ComponentBody m_componentBody;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentNewChaseBehavior m_chaseBehavior;
		private ComponentRider m_componentRider;

		// ===== ESTADO INTERNO =====
		private ComponentCreature m_targetCreature;
		private bool m_isDisappeared;
		private float m_disappearRemaining;
		private float m_cooldownRemaining;
		private Vector3 m_originalPosition;
		private bool m_originalBodyCollidable;
		private bool m_originalIsRaycastTransparent;

		// ===== PROPIEDADES PÚBLICAS =====
		public ComponentCreature Target
		{
			get => m_targetCreature;
			set => m_targetCreature = value;
		}

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// ===== MÉTODOS PÚBLICOS =====
		public void ForceTeleport(ComponentCreature target)
		{
			if (target == null || m_isDisappeared)
				return;
			m_targetCreature = target;
			StartTeleport();
		}

		public void StopTeleport()
		{
			if (m_isDisappeared)
				Reappear();
			m_cooldownRemaining = TeleportationCooldown;
			m_targetCreature = null;
		}

		// ===== CICLO DE VIDA =====
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>();
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>();
			m_componentRider = Entity.FindComponent<ComponentRider>();
			m_chaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();

			TeleportationDistance = valuesDictionary.GetValue<float>("TeleportationDistance", TeleportationDistance);
			TeleportationCooldown = valuesDictionary.GetValue<float>("TeleportationCooldown", TeleportationCooldown);
			DisappearanceTime = valuesDictionary.GetValue<float>("DisappearanceTime", DisappearanceTime);
			ChanceToTeleport = valuesDictionary.GetValue<float>("ChanceToTeleport", ChanceToTeleport);
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap) { }

		public virtual void Update(float dt)
		{
			if (m_cooldownRemaining > 0f)
				m_cooldownRemaining -= dt;

			if (m_isDisappeared)
			{
				m_disappearRemaining -= dt;
				if (m_disappearRemaining <= 0f)
					Reappear();
				return;
			}

			// NUEVO: Si la criatura está montada, no teletransportarse
			if (m_componentRider != null && m_componentRider.Mount != null)
				return;

			UpdateTargetFromChaseBehavior();

			if (m_targetCreature == null || m_cooldownRemaining > 0f)
				return;

			bool isChasing = m_chaseBehavior != null && m_chaseBehavior.IsActive && m_chaseBehavior.Target != null;
			if (!isChasing)
				return;

			float distance = Vector3.Distance(m_componentBody.Position, m_targetCreature.ComponentBody.Position);

			// CORRECCIÓN: usar m_random.Bool() que está diseñado para probabilidades
			if (distance >= TeleportationDistance && m_random.Bool(ChanceToTeleport))
				StartTeleport();
		}

		// ===== MÉTODOS PRIVADOS =====
		private void UpdateTargetFromChaseBehavior()
		{
			if (m_chaseBehavior == null)
				return;

			if (m_chaseBehavior.IsActive && m_chaseBehavior.Target != null)
				m_targetCreature = m_chaseBehavior.Target;
			else
				m_targetCreature = null;
		}

		private void StartTeleport()
		{
			if (m_targetCreature == null || m_isDisappeared)
				return;

			// NUEVO: No teletransportar si está montado
			if (m_componentRider != null && m_componentRider.Mount != null)
				return;

			m_originalPosition = m_componentBody.Position;
			m_originalBodyCollidable = m_componentBody.BodyCollidable;
			m_originalIsRaycastTransparent = m_componentBody.IsRaycastTransparent;

			PlaySound("Audio/teleport 1", m_originalPosition);
			AddTeleportParticles(m_originalPosition, false);

			m_componentBody.BodyCollidable = false;
			m_componentBody.IsRaycastTransparent = true;

			if (m_componentCreatureModel != null)
				m_componentCreatureModel.Opacity = 0f;

			if (m_componentPathfinding != null)
				m_componentPathfinding.Stop();

			m_componentBody.Position = new Vector3(0f, -1000f, 0f);

			m_isDisappeared = true;
			m_disappearRemaining = DisappearanceTime;
		}

		private void Reappear()
		{
			Vector3 finalPosition;
			if (m_targetCreature != null && m_targetCreature.ComponentHealth.Health > 0f)
				finalPosition = FindTeleportPositionNearTarget(m_targetCreature.ComponentBody.Position);
			else
				finalPosition = m_originalPosition;

			m_componentBody.Position = finalPosition;
			m_componentBody.MoveToFreeSpace();

			m_componentBody.BodyCollidable = m_originalBodyCollidable;
			m_componentBody.IsRaycastTransparent = m_originalIsRaycastTransparent;
			if (m_componentCreatureModel != null)
				m_componentCreatureModel.Opacity = null;

			PlaySound("Audio/teleport 2", finalPosition);
			AddTeleportParticles(finalPosition, true);

			m_cooldownRemaining = TeleportationCooldown;
			m_isDisappeared = false;
		}

		private Vector3 FindTeleportPositionNearTarget(Vector3 targetPos)
		{
			int cx = Terrain.ToCell(targetPos.X);
			int cz = Terrain.ToCell(targetPos.Z);
			int startY = Terrain.ToCell(targetPos.Y + 1.5f);

			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dz = -1; dz <= 1; dz++)
				{
					int x = cx + dx;
					int z = cz + dz;
					for (int yOffset = 0; yOffset <= 3; yOffset++)
					{
						int y = startY - yOffset;
						if (y < 0 || y > 255) continue;

						int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
						int content = Terrain.ExtractContents(cellValue);
						Block block = BlocksManager.Blocks[content];
						if (block.IsCollidable_(cellValue))
						{
							BoundingBox[] boxes = block.GetCustomCollisionBoxes(m_subsystemTerrain, cellValue);
							if (boxes != null && boxes.Length > 0)
							{
								float surfaceY = y + boxes[0].Max.Y;
								return new Vector3(x + 0.5f, surfaceY + 0.05f, z + 0.5f);
							}
							return new Vector3(x + 0.5f, y + 1.0f, z + 0.5f);
						}
					}
				}
			}
			return targetPos + new Vector3(0f, 1.5f, 0f);
		}

		private void PlaySound(string soundName, Vector3 position)
		{
			if (m_subsystemAudio != null)
			{
				m_subsystemAudio.PlaySound(soundName, 1f, m_random.Float(-0.2f, 0.2f),
					position, 4f, false);
			}
		}

		private void AddTeleportParticles(Vector3 position, bool isAppearEffect)
		{
			if (m_subsystemParticles == null)
				return;

			float size = Math.Max(0.8f, m_componentBody.BoxSize.Length() * 0.6f);
			var particleSys = new TeleportParticleSystem(m_subsystemTerrain, position, size, isAppearEffect);
			m_subsystemParticles.AddParticleSystem(particleSys, false);
		}
	}
}
