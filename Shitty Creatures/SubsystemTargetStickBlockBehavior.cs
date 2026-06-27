using System;
using Engine;
using GameEntitySystem;

namespace Game
{
	public class SubsystemTargetStickBlockBehavior : SubsystemBlockBehavior
	{
		public ComponentCreature MarkedTarget { get; private set; }

		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex(typeof(TargetStickBlock), true, false)
				};
			}
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			try
			{
				ComponentPlayer componentPlayer = componentMiner?.ComponentPlayer;
				if (componentPlayer == null)
					return false;

				int activeBlockValue = componentMiner.ActiveBlockValue;
				if (activeBlockValue == 0)
					return false;

				int blockType = Terrain.ExtractContents(activeBlockValue);
				int targetStickBlockIndex = BlocksManager.GetBlockIndex(typeof(TargetStickBlock), true, false);

				if (blockType != targetStickBlockIndex)
					return false;

				if (m_subsystemAudio == null)
					m_subsystemAudio = componentMiner.Project.FindSubsystem<SubsystemAudio>(true);

				if (m_subsystemCreatureSpawn == null)
					m_subsystemCreatureSpawn = componentMiner.Project.FindSubsystem<SubsystemCreatureSpawn>(true);

				if (m_subsystemAudio == null)
					return false;

				ComponentCreatureModel creatureModel = componentMiner.ComponentCreature.ComponentCreatureModel;
				if (creatureModel == null)
					return false;

				Vector3 eyePosition = creatureModel.EyePosition;
				Vector3 direction = ray.Direction;

				ComponentCreature targetCreature = null;
				bool foundTarget = false;

				// 1. Intenta golpear directamente a una criatura
				BodyRaycastResult? bodyRaycast = componentMiner.Raycast<BodyRaycastResult>(
					ray, RaycastMode.Interaction, true, true, true, null);

				if (bodyRaycast != null && bodyRaycast.Value.ComponentBody != null)
				{
					ComponentBody hitBody = bodyRaycast.Value.ComponentBody;
					ComponentCreature hitCreature = hitBody.Entity.FindComponent<ComponentCreature>();

					if (hitCreature != null)
					{
						targetCreature = hitCreature;
						foundTarget = true;
					}
				}

				// 2. Si no golpeó directamente, busca en el área
				if (!foundTarget && m_subsystemCreatureSpawn != null)
				{
					Vector2 distanceRange = new Vector2(2f, 15f);
					float maxAngle = 45f;
					ComponentCreature bestCreature = null;
					float bestScore = 0f;

					foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
					{
						if (creature == null || creature == componentMiner.ComponentCreature)
							continue;

						if (creature.ComponentHealth.Health <= 0f)
							continue;

						Vector3 targetPos = creature.ComponentBody.Position;
						float distance = Vector3.Distance(targetPos, eyePosition);

						if (distance < distanceRange.X || distance > distanceRange.Y)
							continue;

						Vector3 directionToCreature = Vector3.Normalize(targetPos - eyePosition);
						float dot = Vector3.Dot(direction, directionToCreature);
						float angle = MathUtils.Acos(MathUtils.Clamp(dot, -1f, 1f)) * 180f / (float)Math.PI;

						if (angle > maxAngle)
							continue;

						float score = (distanceRange.Y - distance) * (maxAngle - angle) * 100f;

						if (score > bestScore)
						{
							bestScore = score;
							bestCreature = creature;
						}
					}

					if (bestCreature != null)
					{
						targetCreature = bestCreature;
						foundTarget = true;
					}
				}

				if (foundTarget && targetCreature != null)
				{
					MarkedTarget = targetCreature;
					m_subsystemAudio.PlaySound("Audio/UI/Attack", 1f, 0f, 0f, 0f);

					if (m_subsystemCreatureSpawn != null)
					{
						Vector3 playerPosition = componentMiner.ComponentCreature.ComponentBody.Position;
						float callRange = 30f;

						foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
						{
							if (creature == null ||
								creature == componentMiner.ComponentCreature ||
								creature == targetCreature)
								continue;

							float distanceToPlayer = Vector3.Distance(creature.ComponentBody.Position, playerPosition);
							if (distanceToPlayer > callRange)
								continue;

							// ===== SOLO ComponentNewChaseBehavior recibe el comando =====
							// Zombies y bandidos ya no responden al bastón de objetivo

							ComponentNewChaseBehavior newChase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
							if (newChase != null && !newChase.Suppressed)
							{
								newChase.RespondToCommandImmediately(targetCreature);
							}

							// ===== ELIMINADOS: ComponentBanditChaseBehavior y ComponentZombieChaseBehavior =====
							// Ya no responden al comando
						}
					}
					return true;
				}

				MarkedTarget = null;
				return false;
			}
			catch (Exception)
			{
				MarkedTarget = null;
				return false;
			}
		}

		private SubsystemAudio m_subsystemAudio;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
	}
}
