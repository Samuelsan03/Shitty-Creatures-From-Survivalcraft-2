using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class Teleport : Component, IUpdateable
	{
		// ===== DISTANCIA FIJA DE CERCANÍA (no se carga del diccionario) =====
		private const float CloseRangeDistance = 1.5f;
		private const float HiddenY = -100f;

		// ===== PROPIEDADES PÚBLICAS =====
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public float TeleportationDistance { get; private set; }
		public float TeleportationCooldown { get; private set; }
		public float DisappearDuration { get; private set; }
		public float ProbabilityOfTeleporting { get; private set; }

		// ===== ESTADO INTERNO =====
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemTime m_subsystemTime;

		private ComponentCreature m_componentCreature;
		private ComponentBody m_componentBody;
		private ComponentNewChaseBehavior m_componentChase;
		private ComponentPathfinding m_componentPathfinding;

		private double m_lastTeleportTime;
		private bool m_isTeleporting;
		private float m_teleportPhase;
		private float m_phaseTimer;
		private Vector3 m_teleportDestination;
		private Vector3 m_teleportOrigin;

		private Random m_random = new Random();

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentChase = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>();

			TeleportationDistance = valuesDictionary.GetValue<float>("TeleportationDistance");
			TeleportationCooldown = valuesDictionary.GetValue<float>("TeleportationCooldown");
			DisappearDuration = valuesDictionary.GetValue<float>("DisappearDuration");
			ProbabilityOfTeleporting = valuesDictionary.GetValue<float>("ProbabilityOfTeleporting");

			m_lastTeleportTime = -TeleportationCooldown;
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
		}

		public void Update(float dt)
		{
			if (m_componentBody == null || m_componentChase == null)
				return;

			if (m_isTeleporting)
			{
				UpdateTeleportation(dt);
				return;
			}

			if (!CanTeleport())
				return;

			ComponentCreature target = m_componentChase.Target;
			if (target == null || target.ComponentBody == null || target.ComponentHealth.Health <= 0f)
				return;

			float distance = Vector3.Distance(m_componentBody.Position, target.ComponentBody.Position);

			if (distance > CloseRangeDistance && distance >= TeleportationDistance)
			{
				if (m_random.Float(0f, 1f) < ProbabilityOfTeleporting)
				{
					StartTeleportation(target);
				}
				else
				{
					m_lastTeleportTime = m_subsystemTime.GameTime;
				}
			}
		}

		private bool CanTeleport()
		{
			double currentTime = m_subsystemTime.GameTime;
			if (currentTime - m_lastTeleportTime < TeleportationCooldown)
				return false;

			if (m_componentChase == null || !m_componentChase.IsActive)
				return false;

			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return false;

			return true;
		}

		private void StartTeleportation(ComponentCreature target)
		{
			m_isTeleporting = true;
			m_teleportPhase = 1;
			m_phaseTimer = 0f;
			m_teleportOrigin = m_componentBody.Position;
			m_teleportDestination = CalculateTeleportDestination(target);

			Vector3 particlePos = m_componentBody.Position + new Vector3(0f, m_componentBody.StanceBoxSize.Y / 2f, 0f);
			float size = m_componentBody.StanceBoxSize.X;

			TeleportParticleSystem particles = new TeleportParticleSystem(m_subsystemTerrain, particlePos, size, false);
			m_subsystemParticles.AddParticleSystem(particles, false);

			// Pausar pathfinding antes de ocultar para evitar detección de atasco
			if (m_componentPathfinding != null)
			{
				m_componentPathfinding.Stop();
			}

			HideBody();

			m_subsystemAudio.PlaySound("Audio/teleport 1", 1f, 0f, m_teleportOrigin, 4f, false);
		}

		private void UpdateTeleportation(float dt)
		{
			m_phaseTimer += dt;

			if (m_teleportPhase == 1)
			{
				if (m_phaseTimer >= DisappearDuration)
				{
					ShowBody(m_teleportDestination);

					Vector3 particlePos = m_teleportDestination + new Vector3(0f, m_componentBody.StanceBoxSize.Y / 2f, 0f);
					float size = m_componentBody.StanceBoxSize.X;

					TeleportParticleSystem particles = new TeleportParticleSystem(m_subsystemTerrain, particlePos, size, true);
					m_subsystemParticles.AddParticleSystem(particles, false);

					m_teleportPhase = 2;
					m_phaseTimer = 0f;

					m_subsystemAudio.PlaySound("Audio/teleport 2", 1f, 0f, m_teleportDestination, 4f, false);
				}
			}
			else if (m_teleportPhase == 2)
			{
				if (m_phaseTimer >= 0.4f)
				{
					m_isTeleporting = false;
					m_teleportPhase = 0;
					m_lastTeleportTime = m_subsystemTime.GameTime;
				}
			}
		}

		private void HideBody()
		{
			m_componentBody.Position = new Vector3(m_teleportOrigin.X, HiddenY, m_teleportOrigin.Z);
			m_componentBody.Velocity = Vector3.Zero;
		}

		private void ShowBody(Vector3 position)
		{
			m_componentBody.Position = position;
			m_componentBody.Velocity = Vector3.Zero;
		}

		private Vector3 CalculateTeleportDestination(ComponentCreature target)
		{
			Vector3 targetPos = target.ComponentBody.Position;

			float angle = 180f + m_random.Float(-45f, 45f);

			float radians = angle * MathUtils.DegToRad(1f);
			Vector3 offset = new Vector3(
				MathF.Sin(radians) * CloseRangeDistance,
				0f,
				MathF.Cos(radians) * CloseRangeDistance);

			Vector3 destination = targetPos + offset;

			destination.Y = FindGroundLevel(destination);

			return destination;
		}

		private float FindGroundLevel(Vector3 position)
		{
			int x = Terrain.ToCell(position.X);
			int startY = Terrain.ToCell(position.Y + 5);
			int z = Terrain.ToCell(position.Z);

			for (int y = Math.Min(startY, 254); y >= 0; y--)
			{
				if (m_subsystemTerrain.Terrain.IsCellValid(x, y, z))
				{
					int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
					int contents = Terrain.ExtractContents(cellValue);
					Block block = BlocksManager.Blocks[contents];

					if (block.IsCollidable_(cellValue))
					{
						int aboveValue = m_subsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
						int aboveContents = Terrain.ExtractContents(aboveValue);
						Block aboveBlock = BlocksManager.Blocks[aboveContents];

						if (!aboveBlock.IsCollidable_(aboveValue))
						{
							return y + 1.01f;
						}
					}
				}
			}

			return position.Y;
		}

		public void ForceTeleportTo(Vector3 destination)
		{
			if (m_isTeleporting)
				return;

			m_isTeleporting = true;
			m_teleportPhase = 1;
			m_phaseTimer = 0f;
			m_teleportOrigin = m_componentBody.Position;
			m_teleportDestination = destination;

			Vector3 particlePos = m_componentBody.Position + new Vector3(0f, m_componentBody.StanceBoxSize.Y / 2f, 0f);
			float size = m_componentBody.StanceBoxSize.X;

			TeleportParticleSystem particles = new TeleportParticleSystem(m_subsystemTerrain, particlePos, size, false);
			m_subsystemParticles.AddParticleSystem(particles, false);

			if (m_componentPathfinding != null)
			{
				m_componentPathfinding.Stop();
			}

			HideBody();

			m_subsystemAudio.PlaySound("Audio/teleport 1", 1f, 0f, m_teleportOrigin, 4f, false);
		}

		public void CancelTeleport()
		{
			if (m_isTeleporting)
			{
				ShowBody(m_teleportOrigin);
				m_isTeleporting = false;
				m_teleportPhase = 0;
			}
		}

		public bool IsTeleporting => m_isTeleporting;

		public float RemainingCooldown
		{
			get
			{
				double elapsed = m_subsystemTime.GameTime - m_lastTeleportTime;
				return Math.Max(0f, (float)(TeleportationCooldown - elapsed));
			}
		}
	}
}
