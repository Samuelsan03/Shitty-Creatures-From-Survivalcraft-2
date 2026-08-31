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
		/// <summary>
		/// Estados del teletransporte.
		/// </summary>
		public enum TeleportState
		{
			/// <summary>La criatura no está teletransportándose.</summary>
			Inactive,
			/// <summary>La criatura está desapareciendo.</summary>
			Disappearing,
			/// <summary>La criatura está apareciendo en la nueva posición.</summary>
			Appearing
		}

		// ===== PARÁMETROS CONFIGURABLES =====
		public float TeleportationDistance = 15f;
		public float TeleportationCooldown = 5f;
		public float DisappearanceTime = 0.75f;
		public float AppearanceTime = 0.75f;

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
		private TeleportState m_state = TeleportState.Inactive;
		private float m_stateTimer;
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

		public TeleportState State => m_state;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// ===== MÉTODOS PÚBLICOS =====
		public void ForceTeleport(ComponentCreature target)
		{
			if (target == null || m_state != TeleportState.Inactive)
				return;
			if (IsMountedOrMounting())
				return;
			m_targetCreature = target;
			StartTeleport();
		}

		public void StopTeleport()
		{
			if (m_state == TeleportState.Inactive)
				return;

			if (m_state == TeleportState.Disappearing)
			{
				m_componentBody.Position = m_originalPosition;
				m_componentBody.MoveToFreeSpace();
			}

			RestoreCreatureState();
			m_cooldownRemaining = TeleportationCooldown;
			m_state = TeleportState.Inactive;
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

			m_componentRider = Entity.FindComponent<ComponentRider>();
			m_componentMount = Entity.FindComponent<ComponentMount>();
			m_componentNewMount = Entity.FindComponent<ComponentNewMount>();
			m_componentSteed = Entity.FindComponent<ComponentSteedBehavior>();
			m_componentNewSteed = Entity.FindComponent<ComponentNewSteedBehavior>();

			TeleportationDistance = valuesDictionary.GetValue<float>("TeleportationDistance", TeleportationDistance);
			TeleportationCooldown = valuesDictionary.GetValue<float>("TeleportationCooldown", TeleportationCooldown);
			DisappearanceTime = valuesDictionary.GetValue<float>("DisappearanceTime", DisappearanceTime);
			AppearanceTime = valuesDictionary.GetValue<float>("AppearanceTime", AppearanceTime);
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap) { }

		public virtual void Update(float dt)
		{
			if (m_cooldownRemaining > 0f)
				m_cooldownRemaining -= dt;

			switch (m_state)
			{
				case TeleportState.Inactive:
					UpdateInactive();
					break;
				case TeleportState.Disappearing:
					UpdateDisappearing(dt);
					break;
				case TeleportState.Appearing:
					UpdateAppearing(dt);
					break;
			}
		}

		// ===== MÉTODOS DE ACTUALIZACIÓN POR ESTADO =====

		private void UpdateInactive()
		{
			if (m_cooldownRemaining > 0f)
				return;

			if (IsMountedOrMounting())
				return;

			UpdateTargetFromChaseBehavior();

			if (m_targetCreature == null)
				return;

			bool isChasing = m_chaseBehavior != null && m_chaseBehavior.IsActive && m_chaseBehavior.Target != null;
			if (!isChasing)
				return;

			float distance = Vector3.Distance(m_componentBody.Position, m_targetCreature.ComponentBody.Position);
			if (distance >= TeleportationDistance)
			{
				StartTeleport();
			}
		}

		private void UpdateDisappearing(float dt)
		{
			m_stateTimer -= dt;
			if (m_stateTimer <= 0f)
			{
				BeginAppearing();
			}
		}

		private void UpdateAppearing(float dt)
		{
			m_stateTimer -= dt;

			if (m_componentCreatureModel != null && AppearanceTime > 0f)
			{
				float progress = 1f - MathUtils.Max(0f, m_stateTimer / AppearanceTime);
				m_componentCreatureModel.Opacity = MathUtils.Saturate(progress);
			}

			if (m_stateTimer <= 0f)
			{
				FinishTeleport();
			}
		}

		// ===== MÉTODOS DE TRANSICIÓN DE ESTADO =====

		private void StartTeleport()
		{
			if (m_targetCreature == null || m_state != TeleportState.Inactive)
				return;
			if (IsMountedOrMounting())
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

			m_state = TeleportState.Disappearing;
			m_stateTimer = DisappearanceTime;
		}

		private void BeginAppearing()
		{
			Vector3 finalPosition;
			if (m_targetCreature != null && m_targetCreature.ComponentHealth.Health > 0f)
				finalPosition = FindTeleportPositionNearTarget(m_targetCreature.ComponentBody.Position);
			else
				finalPosition = m_originalPosition;

			m_componentBody.Position = finalPosition;
			m_componentBody.MoveToFreeSpace();

			PlaySound("Audio/teleport 2", finalPosition);
			AddTeleportParticles(finalPosition, true);

			m_state = TeleportState.Appearing;
			m_stateTimer = AppearanceTime;
		}

		private void FinishTeleport()
		{
			RestoreCreatureState();
			m_cooldownRemaining = TeleportationCooldown;
			m_state = TeleportState.Inactive;
		}

		private void RestoreCreatureState()
		{
			m_componentBody.BodyCollidable = m_originalBodyCollidable;
			m_componentBody.IsRaycastTransparent = m_originalIsRaycastTransparent;

			if (m_componentCreatureModel != null)
				m_componentCreatureModel.Opacity = null;
		}

		// ===== MÉTODOS PRIVADOS =====

		private bool IsMountedOrMounting()
		{
			if (m_componentRider != null && m_componentRider.Mount != null)
				return true;

			if (m_componentMount != null && m_componentMount.Rider != null)
				return true;

			if (m_componentNewMount != null && m_componentNewMount.Rider != null)
				return true;

			if (m_componentSteed != null)
			{
				var riderProp = m_componentSteed.GetType().GetProperty("Rider");
				if (riderProp != null)
				{
					var rider = riderProp.GetValue(m_componentSteed) as ComponentRider;
					if (rider != null)
						return true;
				}
				var riderField = m_componentSteed.GetType().GetField("m_rider",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (riderField != null)
				{
					var rider = riderField.GetValue(m_componentSteed) as ComponentRider;
					if (rider != null)
						return true;
				}
			}

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

		private Vector3 FindTeleportPositionNearTarget(Vector3 targetPos)
		{
			Vector3 boxSize = m_componentBody.StanceBoxSize;
			float bestDistSq = float.MaxValue;
			Vector3 bestPos = m_originalPosition;

			int cx = Terrain.ToCell(targetPos.X);
			int cy = Terrain.ToCell(targetPos.Y);
			int cz = Terrain.ToCell(targetPos.Z);

			for (int dx = -3; dx <= 3; dx++)
			{
				for (int dz = -3; dz <= 3; dz++)
				{
					int x = cx + dx;
					int z = cz + dz;
					for (int dy = -2; dy <= 3; dy++)
					{
						int y = cy + dy;
						if (y < 0 || y > 255) continue;

						Vector3 candidatePos = new Vector3(x + 0.5f, y, z + 0.5f);

						if (IsPositionFreeForCreature(candidatePos, boxSize))
						{
							float distSq = Vector3.DistanceSquared(candidatePos, targetPos);
							if (distSq < bestDistSq)
							{
								bestDistSq = distSq;
								bestPos = candidatePos;
							}
						}
					}
				}
			}

			return bestPos;
		}

		private bool IsPositionFreeForCreature(Vector3 position, Vector3 boxSize)
		{
			BoundingBox box = new BoundingBox(
				position - new Vector3(boxSize.X / 2f, 0f, boxSize.Z / 2f),
				position + new Vector3(boxSize.X / 2f, boxSize.Y, boxSize.Z / 2f)
			);

			box.Min += new Vector3(0.05f, 0.05f, 0.05f);
			box.Max -= new Vector3(0.05f, 0.05f, 0.05f);

			Point3 minCell = Terrain.ToCell(box.Min);
			Point3 maxCell = Terrain.ToCell(box.Max);

			minCell.Y = MathUtils.Max(minCell.Y, 0);
			maxCell.Y = MathUtils.Min(maxCell.Y, 255);

			if (minCell.Y > maxCell.Y) return false;

			for (int x = minCell.X; x <= maxCell.X; x++)
			{
				for (int y = minCell.Y; y <= maxCell.Y; y++)
				{
					for (int z = minCell.Z; z <= maxCell.Z; z++)
					{
						int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
						int content = Terrain.ExtractContents(cellValue);

						if (content != 0)
						{
							Block block = BlocksManager.Blocks[content];
							if (block.IsCollidable_(cellValue))
							{
								BoundingBox[] customBoxes = block.GetCustomCollisionBoxes(m_subsystemTerrain, cellValue);
								Vector3 cellPos = new Vector3(x, y, z);

								for (int i = 0; i < customBoxes.Length; i++)
								{
									BoundingBox blockBox = new BoundingBox(cellPos + customBoxes[i].Min, cellPos + customBoxes[i].Max);

									if (box.Intersection(blockBox))
									{
										return false;
									}
								}
							}
						}
					}
				}
			}

			return true;
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
