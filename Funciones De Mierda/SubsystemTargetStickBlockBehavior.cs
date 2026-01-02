using System;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemTargetStickBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					329
				};
			}
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			object obj = componentMiner.Raycast(ray, RaycastMode.Interaction, true, true, true, null);
			if (!(obj is BodyRaycastResult))
			{
				return false;
			}

			BodyRaycastResult bodyRaycastResult = (BodyRaycastResult)obj;
			ComponentCreature targetCreature = bodyRaycastResult.ComponentBody.Entity.FindComponent<ComponentCreature>();

			if (targetCreature == null || targetCreature.Entity.FindComponent<ComponentPlayer>() != null)
			{
				return false;
			}

			this.m_subsystemAudio.PlaySound("Audio/UI/Attack", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentBody.Position, 2f, true);

			FindAndCommandAllies(componentMiner.ComponentCreature, targetCreature);

			return true;
		}

		private void FindAndCommandAllies(ComponentCreature commander, ComponentCreature target)
		{
			Vector3 commanderPosition = commander.ComponentBody.Position;
			float commandRadius = 20f;
			float commandRadiusSquared = commandRadius * commandRadius;

			DynamicArray<ComponentBody> nearbyBodies = new DynamicArray<ComponentBody>();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(commanderPosition.X, commanderPosition.Z), commandRadius, nearbyBodies);

			for (int i = 0; i < nearbyBodies.Count; i++)
			{
				ComponentCreature allyCreature = nearbyBodies.Array[i].Entity.FindComponent<ComponentCreature>();

				if (allyCreature != null && allyCreature != commander && allyCreature != target)
				{
					bool isAlly = IsPlayerAlly(allyCreature);

					if (isAlly)
					{
						CommandAllyToAttackImmediately(allyCreature, target);
					}
				}
			}
		}

		private bool IsPlayerAlly(ComponentCreature creature)
		{
			ComponentNewHerdBehavior newHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (newHerd != null && !string.IsNullOrEmpty(newHerd.HerdName))
			{
				return newHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
			}

			ComponentHerdBehavior oldHerd = creature.Entity.FindComponent<ComponentHerdBehavior>();
			if (oldHerd != null && !string.IsNullOrEmpty(oldHerd.HerdName))
			{
				return oldHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
			}

			return false;
		}

		private bool CanAttackTarget(ComponentCreature ally, ComponentCreature target)
		{
			ComponentNewHerdBehavior newHerd = ally.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (newHerd != null)
			{
				return newHerd.CanAttackCreature(target);
			}

			ComponentHerdBehavior oldHerd = ally.Entity.FindComponent<ComponentHerdBehavior>();
			if (oldHerd != null)
			{
				ComponentHerdBehavior targetHerd = target.Entity.FindComponent<ComponentHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName))
				{
					return !targetHerd.HerdName.Equals(oldHerd.HerdName, StringComparison.OrdinalIgnoreCase);
				}
			}

			return true;
		}

		// MÉTODO NUEVO: Comando inmediato
		private void CommandAllyToAttackImmediately(ComponentCreature ally, ComponentCreature target)
		{
			if (!CanAttackTarget(ally, target))
			{
				return;
			}

			ComponentNewChaseBehavior newChase = ally.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (newChase != null)
			{
				// Usar el método de respuesta inmediata
				newChase.RespondToCommandImmediately(target);
				return;
			}

			ComponentNewHerdBehavior newHerd = ally.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (newHerd != null)
			{
				var autoHelpField = newHerd.GetType().GetField("m_autoNearbyCreaturesHelp",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				bool autoHelpEnabled = true;

				if (autoHelpField != null)
				{
					try
					{
						autoHelpEnabled = (bool)autoHelpField.GetValue(newHerd);
					}
					catch
					{
					}
				}

				if (autoHelpEnabled)
				{
					var callHelpMethod = newHerd.GetType().GetMethod("CallNearbyCreaturesHelp");
					if (callHelpMethod != null)
					{
						try
						{
							callHelpMethod.Invoke(newHerd, new object[] { target, 0f, 1000f, false });
							return;
						}
						catch
						{
						}
					}
				}
			}

			ComponentChaseBehavior oldChase = ally.Entity.FindComponent<ComponentChaseBehavior>();
			if (oldChase != null)
			{
				var attackMethod = oldChase.GetType().GetMethod("Attack");
				if (attackMethod != null)
				{
					try
					{
						attackMethod.Invoke(oldChase, new object[] { target, 0f, 1000f, false });
					}
					catch
					{
					}
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
		}

		private SubsystemAudio m_subsystemAudio;
		private SubsystemBodies m_subsystemBodies;
		private readonly Game.Random m_random = new Game.Random();
		private const int TargeterBlockID = 329;
	}
}
