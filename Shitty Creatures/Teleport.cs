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

		// Componentes de montura (caché)
		private ComponentRider m_componentRider;
		private ComponentMount m_componentMount;
		private ComponentNewMount m_componentNewMount;
		private ComponentSteedBehavior m_componentSteed;
		private ComponentNewSteedBehavior m_componentNewSteed;

		// ===== ESTADO INTERNO =====
		private ComponentCreature m_targetCreature;
		private bool m_isDisappeared;
		private float m_disappearRemaining;
		private float m_cooldownRemaining;
		private Vector3 m_originalPosition;
		private bool m_originalBodyCollidable;
		private bool m_originalIsRaycastTransparent;

		// Temporizador para evaluar la probabilidad una vez por segundo (no cada frame)
		private float m_checkTimer;

		// Color elegido una vez por ciclo de teletransporte, compartido entre desaparecer y aparecer
		private Color m_teleportColor;

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
			if (IsMountedOrMounting())
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
			m_chaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();

			// Obtener todos los componentes relacionados con montura
			m_componentRider = Entity.FindComponent<ComponentRider>();
			m_componentMount = Entity.FindComponent<ComponentMount>();
			m_componentNewMount = Entity.FindComponent<ComponentNewMount>();
			m_componentSteed = Entity.FindComponent<ComponentSteedBehavior>();
			m_componentNewSteed = Entity.FindComponent<ComponentNewSteedBehavior>();

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

			// Verificar estado de montura (jinete o montura)
			if (IsMountedOrMounting())
				return;

			UpdateTargetFromChaseBehavior();

			if (m_targetCreature == null || m_cooldownRemaining > 0f)
				return;

			bool isChasing = m_chaseBehavior != null && m_chaseBehavior.IsActive && m_chaseBehavior.Target != null;
			if (!isChasing)
				return;

			float distance = Vector3.Distance(m_componentBody.Position, m_targetCreature.ComponentBody.Position);
			if (distance >= TeleportationDistance)
			{
				m_checkTimer -= dt;
				if (m_checkTimer <= 0f)
				{
					m_checkTimer = 1f;
					if (m_random.Float(0f, 1f) < ChanceToTeleport)
						StartTeleport();
				}
			}
			else
			{
				m_checkTimer = 0f;
			}
		}

		// ===== MÉTODOS PRIVADOS =====

		/// <summary>
		/// Detecta si la criatura está montada (es jinete) o es una montura (tiene un jinete).
		/// </summary>
		private bool IsMountedOrMounting()
		{
			// 1. Es jinete (está montando algo)
			if (m_componentRider != null && m_componentRider.Mount != null)
				return true;

			// 2. Es montura (alguien lo está montando) - ComponentMount tradicional
			if (m_componentMount != null && m_componentMount.Rider != null)
				return true;

			// 3. Es montura - ComponentNewMount (nuevo sistema)
			if (m_componentNewMount != null && m_componentNewMount.Rider != null)
				return true;

			// 4. ComponentSteedBehavior (base) - puede tener un jinete
			if (m_componentSteed != null)
			{
				// Intentar obtener la propiedad pública "Rider"
				var riderProp = m_componentSteed.GetType().GetProperty("Rider");
				if (riderProp != null)
				{
					var rider = riderProp.GetValue(m_componentSteed) as ComponentRider;
					if (rider != null)
						return true;
				}
				// Intentar obtener el campo privado "m_rider" (por si la propiedad no es pública)
				var riderField = m_componentSteed.GetType().GetField("m_rider",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (riderField != null)
				{
					var rider = riderField.GetValue(m_componentSteed) as ComponentRider;
					if (rider != null)
						return true;
				}
			}

			// 5. ComponentNewSteedBehavior (derivado) - mismo proceso
			if (m_componentNewSteed != null)
			{
				var riderProp = m_componentNewSteed.GetType().GetProperty("Rider");
				if (riderProp != null)
				{
					var rider = riderProp.GetValue(m_componentNewSteed) as ComponentRider;
					if (rider != null)
						return true;
				}
				var riderField = m_componentNewSteed.GetType().GetField("m_rider",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (riderField != null)
				{
					var rider = riderField.GetValue(m_componentNewSteed) as ComponentRider;
					if (rider != null)
						return true;
				}
			}

			return false;
		}

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
			// Doble verificación por si cambió durante el frame
			if (IsMountedOrMounting())
				return;

			// Elegir el color UNA vez para todo el ciclo de teletransporte
			m_teleportColor = m_random.Bool()
				? new Color(180, 60, 255, 255)
				: new Color(100, 20, 180, 200);

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
			var particleSys = new TeleportParticleSystem(m_subsystemTerrain, position, size, isAppearEffect, m_teleportColor);
			m_subsystemParticles.AddParticleSystem(particleSys, false);
		}
	}
}
