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

		public override int[] HandledBlocks
		{
			get { return new int[] { BlocksManager.GetBlockIndex(typeof(TargetStickBlock), true, false) }; }
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

				if (m_subsystemAudio == null)
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

				if (!foundTarget && m_subsystemCreatureSpawn != null)
				{
					float maxDistance = 6f;
					float maxAngle = 100f;
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

						float score = (maxDistance - distance) * (maxAngle - angle);

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

					if (m_subsystemCreatureSpawn != null)
					{
						Vector3 commanderPosition = componentMiner.ComponentCreature.ComponentBody.Position;
						float commandRange = 30f;
						float maxChaseTime = 45f;

						foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
						{
							if (creature == null || creature == componentMiner.ComponentCreature || creature == targetCreature)
								continue;

							float distance = Vector3.Distance(creature.ComponentBody.Position, commanderPosition);
							if (distance > commandRange)
								continue;

							if (!IsPlayerAlly(creature))
								continue;

							// Verificar si puede atacar al objetivo (usando NewHerd)
							ComponentNewHerdBehavior herdBehavior = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
							if (herdBehavior != null)
							{
								if (!herdBehavior.CanAttackCreature(targetCreature))
								{
									continue;
								}
							}
							else
							{
								// Verificar manualmente si es aliado (para criaturas sin NewHerd)
								if (IsCreatureAlly(creature, targetCreature))
								{
									continue;
								}
							}

							// ORDENAR EL ATAQUE - Incluye todos los tipos de chase behavior
							ComponentNewChaseBehavior newChase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
							ComponentNewChaseBehavior2 newChase2 = creature.Entity.FindComponent<ComponentNewChaseBehavior2>();
							ComponentChaseBehavior oldChase = creature.Entity.FindComponent<ComponentChaseBehavior>();
							ComponentZombieChaseBehavior zombieChase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
							ComponentBanditChaseBehavior banditChase = creature.Entity.FindComponent<ComponentBanditChaseBehavior>();

							if (newChase != null && !newChase.Suppressed)
							{
								// ComponentNewChaseBehavior tiene Attack (heredado de ComponentChaseBehavior)
								newChase.Attack(targetCreature, commandRange, maxChaseTime, true);
							}
							else if (newChase2 != null)
							{
								// ComponentNewChaseBehavior2 tiene su propio método Attack
								newChase2.Attack(targetCreature, commandRange, maxChaseTime, true);
							}
							else if (oldChase != null)
							{
								oldChase.Attack(targetCreature, commandRange, maxChaseTime, true);
							}
							else if (zombieChase != null)
							{
								// ComponentZombieChaseBehavior hereda de ComponentChaseBehavior
								zombieChase.Attack(targetCreature, commandRange, maxChaseTime, true);
							}
							else if (banditChase != null)
							{
								// ComponentBanditChaseBehavior hereda de ComponentChaseBehavior
								banditChase.Attack(targetCreature, commandRange, maxChaseTime, true);
							}
						}
					}

					return true; // Éxito - se encontró y se ordenó atacar
				}

				return false; // No se encontró objetivo válido
			}
			catch (Exception)
			{
				return false;
			}
		}

		private bool IsPlayerAlly(ComponentCreature creature)
		{
			if (creature == null)
				return false;

			// Verificar NewHerdBehavior
			ComponentNewHerdBehavior newHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (newHerd != null && !string.IsNullOrEmpty(newHerd.HerdName))
			{
				string herdName = newHerd.HerdName.ToLower();
				return herdName == "player" || herdName.Contains("guardian");
			}

			// Verificar ComponentPlayer directamente
			ComponentPlayer player = creature.Entity.FindComponent<ComponentPlayer>();
			if (player != null)
				return true;

			return false;
		}

		private bool IsCreatureAlly(ComponentCreature creature, ComponentCreature target)
		{
			if (creature == null || target == null)
				return false;

			// Verificar NewHerd en ambos
			ComponentNewHerdBehavior creatureHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			ComponentNewHerdBehavior targetHerd = target.Entity.FindComponent<ComponentNewHerdBehavior>();

			if (creatureHerd != null && targetHerd != null)
			{
				return creatureHerd.IsSameHerdOrGuardian(target);
			}

			return false;
		}
	}
}
