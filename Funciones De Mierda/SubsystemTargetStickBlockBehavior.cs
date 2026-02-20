// SubsystemTargetStickBlockBehavior.cs (SIN MENSAJES)
using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using System.Collections.Generic;

namespace Game
{
	public class SubsystemTargetStickBlockBehavior : SubsystemBlockBehavior
	{
		private SubsystemAudio m_subsystemAudio;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemTime m_subsystemTime;

		public override int[] HandledBlocks
		{
			get { return new int[] { BlocksManager.GetBlockIndex(typeof(TargetStickBlock), true, false) }; }
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			try
			{
				ComponentPlayer componentPlayer = componentMiner?.ComponentPlayer;
				if (componentPlayer == null)
				{
					return false;
				}

				int activeBlockValue = componentMiner.ActiveBlockValue;
				if (activeBlockValue == 0)
				{
					return false;
				}

				int activeBlockId = Terrain.ExtractContents(activeBlockValue);
				int targetStickBlockId = BlocksManager.GetBlockIndex(typeof(TargetStickBlock), true, false);

				if (activeBlockId != targetStickBlockId)
				{
					return false;
				}

				if (m_subsystemAudio == null)
				{
					m_subsystemAudio = componentMiner.Project.FindSubsystem<SubsystemAudio>(true);
				}

				if (m_subsystemCreatureSpawn == null)
				{
					m_subsystemCreatureSpawn = componentMiner.Project.FindSubsystem<SubsystemCreatureSpawn>(true);
				}

				if (m_subsystemPlayers == null)
				{
					m_subsystemPlayers = componentMiner.Project.FindSubsystem<SubsystemPlayers>(true);
				}

				if (m_subsystemAudio == null || m_subsystemCreatureSpawn == null)
				{
					return false;
				}

				ComponentCreatureModel creatureModel = componentMiner.ComponentCreature.ComponentCreatureModel;
				if (creatureModel == null)
				{
					return false;
				}

				Vector3 eyePosition = creatureModel.EyePosition;
				Vector3 lookDirection = ray.Direction;

				ComponentCreature targetCreature = null;
				Vector3? targetPosition = null;

				BodyRaycastResult? bodyRaycastResult = componentMiner.Raycast<BodyRaycastResult>(
					ray,
					RaycastMode.Interaction,
					true, true, true
				);

				bool foundTarget = false;

				if (bodyRaycastResult != null && bodyRaycastResult.Value.ComponentBody != null)
				{
					ComponentBody targetBody = bodyRaycastResult.Value.ComponentBody;
					targetCreature = targetBody.Entity.FindComponent<ComponentCreature>();

					if (targetCreature != null && !IsPlayerAlly(targetCreature))
					{
						targetPosition = bodyRaycastResult.Value.HitPoint();
						foundTarget = true;
					}
				}

				if (!foundTarget)
				{
					float maxDistance = 20f;
					float maxAngle = 45f;
					ComponentCreature bestTarget = null;
					float bestScore = 0f;

					foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
					{
						if (creature == null || creature == componentMiner.ComponentCreature)
							continue;

						if (IsPlayerAlly(creature))
							continue;

						float distance = Vector3.Distance(creature.ComponentBody.Position, eyePosition);
						if (distance > maxDistance)
							continue;

						Vector3 toCreature = Vector3.Normalize(creature.ComponentBody.Position - eyePosition);
						float angle = MathUtils.Acos(Vector3.Dot(lookDirection, toCreature)) * 180f / MathUtils.PI;

						if (angle > maxAngle)
							continue;

						float score = (maxDistance - distance) * (maxAngle - angle) * 100f;

						if (score > bestScore)
						{
							bestScore = score;
							bestTarget = creature;
						}
					}

					if (bestTarget != null)
					{
						targetCreature = bestTarget;
						targetPosition = bestTarget.ComponentBody.Position;
						foundTarget = true;
					}
				}

				if (foundTarget && targetCreature != null)
				{
					m_subsystemAudio.PlaySound("Audio/UI/Attack", 1f, 0f, 0f, 0f);

					Vector3 commanderPosition = componentMiner.ComponentCreature.ComponentBody.Position;
					float commandRange = 40f;
					float maxChaseTime = 60f;
					int alliesCommanded = 0;

					DynamicArray<ComponentBody> bodies = new DynamicArray<ComponentBody>();
					SubsystemBodies subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
					subsystemBodies.FindBodiesAroundPoint(new Vector2(commanderPosition.X, commanderPosition.Z), commandRange, bodies);

					for (int i = 0; i < bodies.Count; i++)
					{
						ComponentCreature creature = bodies.Array[i].Entity.FindComponent<ComponentCreature>();
						if (creature == null || creature == componentMiner.ComponentCreature || creature == targetCreature)
							continue;

						if (!IsPlayerAlly(creature))
							continue;

						if (!CanAttackTarget(creature, targetCreature))
							continue;

						ComponentNewChaseBehavior newChase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
						if (newChase != null)
						{
							newChase.Attack(targetCreature, commandRange, maxChaseTime, true);
							alliesCommanded++;
						}
						else
						{
							ComponentChaseBehavior oldChase = creature.Entity.FindComponent<ComponentChaseBehavior>();
							if (oldChase != null)
							{
								oldChase.Attack(targetCreature, commandRange, maxChaseTime, true);
								alliesCommanded++;
							}
						}
					}

					// SIN MENSAJES EN PANTALLA

					return true;
				}
				else
				{
					m_subsystemAudio.PlaySound("Audio/UI/ButtonCancel", 1f, 0f, 0f, 0f);
					return false;
				}
			}
			catch (Exception ex)
			{
				Log.Error($"Error en TargetStickBlockBehavior: {ex.Message}");
				return false;
			}
		}

		private bool CanAttackTarget(ComponentCreature attacker, ComponentCreature target)
		{
			if (attacker == null || target == null)
				return false;

			ComponentNewHerdBehavior herdBehavior = attacker.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (herdBehavior != null)
			{
				return herdBehavior.CanAttackCreature(target);
			}

			ComponentNewHerdBehavior targetHerd = target.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName))
			{
				string herdName = targetHerd.HerdName.ToLower();
				if (herdName == "player" || herdName.Contains("guardian"))
					return false;
			}

			return !IsPlayerAlly(target);
		}

		private bool IsPlayerAlly(ComponentCreature creature)
		{
			if (creature == null)
				return false;

			ComponentNewHerdBehavior newHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (newHerd != null && !string.IsNullOrEmpty(newHerd.HerdName))
			{
				string herdName = newHerd.HerdName.ToLower();
				if (herdName == "player" || herdName.Contains("guardian"))
					return true;
			}

			ComponentPlayer player = creature.Entity.FindComponent<ComponentPlayer>();
			if (player != null)
				return true;

			return false;
		}
	}
}
