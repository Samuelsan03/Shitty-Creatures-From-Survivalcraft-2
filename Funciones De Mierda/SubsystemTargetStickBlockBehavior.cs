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
					329  // Debe coincidir con TargetStickBlock.Index
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

			// Verificar que el objetivo sea una criatura válida y no un jugador
			if (targetCreature == null || targetCreature.Entity.FindComponent<ComponentPlayer>() != null)
			{
				return false;
			}

			// Reproducir sonido de feedback
			this.m_subsystemAudio.PlaySound("Audio/UI/Fight", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentBody.Position, 2f, true);

			// Buscar aliados del jugador y ordenarles atacar al objetivo
			FindAndCommandAllies(componentMiner.ComponentCreature, targetCreature);

			return true;
		}

		private void FindAndCommandAllies(ComponentCreature commander, ComponentCreature target)
		{
			Vector3 commanderPosition = commander.ComponentBody.Position;
			float commandRadius = 20f; // Radio en el que se buscarán aliados
			float commandRadiusSquared = commandRadius * commandRadius;

			// Buscar todas las criaturas en el radio
			DynamicArray<ComponentBody> nearbyBodies = new DynamicArray<ComponentBody>();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(commanderPosition.X, commanderPosition.Z), commandRadius, nearbyBodies);

			for (int i = 0; i < nearbyBodies.Count; i++)
			{
				ComponentCreature allyCreature = nearbyBodies.Array[i].Entity.FindComponent<ComponentCreature>();

				if (allyCreature != null && allyCreature != commander && allyCreature != target)
				{
					// Verificar si es aliado (pertenece a la manada "player")
					bool isAlly = IsPlayerAlly(allyCreature);

					if (isAlly)
					{
						// Ordenar atacar al objetivo
						CommandAllyToAttack(allyCreature, target);
					}
				}
			}
		}

		private bool IsPlayerAlly(ComponentCreature creature)
		{
			// PRIMERO: Verificar ComponentNewHerdBehavior (nuevo sistema)
			ComponentNewHerdBehavior newHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (newHerd != null && !string.IsNullOrEmpty(newHerd.HerdName))
			{
				return newHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
			}

			// SEGUNDO: Verificar ComponentHerdBehavior (sistema antiguo)
			ComponentHerdBehavior oldHerd = creature.Entity.FindComponent<ComponentHerdBehavior>();
			if (oldHerd != null && !string.IsNullOrEmpty(oldHerd.HerdName))
			{
				return oldHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
			}

			return false;
		}

		private bool CanAttackTarget(ComponentCreature ally, ComponentCreature target)
		{
			// PRIMERO: Verificar con ComponentNewHerdBehavior (nuevo sistema)
			ComponentNewHerdBehavior newHerd = ally.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (newHerd != null)
			{
				// Usar el método CanAttackCreature del nuevo sistema
				return newHerd.CanAttackCreature(target);
			}

			// SEGUNDO: Verificar con ComponentHerdBehavior (sistema antiguo)
			ComponentHerdBehavior oldHerd = ally.Entity.FindComponent<ComponentHerdBehavior>();
			if (oldHerd != null)
			{
				// Verificar si el objetivo es de la misma manada
				ComponentHerdBehavior targetHerd = target.Entity.FindComponent<ComponentHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName))
				{
					return !targetHerd.HerdName.Equals(oldHerd.HerdName, StringComparison.OrdinalIgnoreCase);
				}
			}

			// Por defecto, puede atacar
			return true;
		}

		private void CommandAllyToAttack(ComponentCreature ally, ComponentCreature target)
		{
			// Verificar primero si puede atacar a este objetivo
			if (!CanAttackTarget(ally, target))
			{
				return;
			}

			// PRIMERA OPCIÓN: Usar ComponentNewChaseBehavior (nuevo sistema)
			ComponentNewChaseBehavior newChase = ally.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (newChase != null)
			{
				newChase.Attack(target, 30f, 30f, false);
				return;
			}

			// SEGUNDA OPCIÓN: Usar ComponentNewHerdBehavior para llamar ayuda (usando reflexión para acceder al campo privado)
			ComponentNewHerdBehavior newHerd = ally.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (newHerd != null)
			{
				// Intentar acceder al campo m_autoNearbyCreaturesHelp usando reflexión
				var autoHelpField = newHerd.GetType().GetField("m_autoNearbyCreaturesHelp",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				bool autoHelpEnabled = true; // Por defecto asumir que está habilitado

				if (autoHelpField != null)
				{
					try
					{
						autoHelpEnabled = (bool)autoHelpField.GetValue(newHerd);
					}
					catch
					{
						// Si falla, usar el valor por defecto
					}
				}

				if (autoHelpEnabled)
				{
					// Usar el método CallNearbyCreaturesHelp del ComponentNewHerdBehavior
					var callHelpMethod = newHerd.GetType().GetMethod("CallNearbyCreaturesHelp");
					if (callHelpMethod != null)
					{
						try
						{
							callHelpMethod.Invoke(newHerd, new object[] { target, 30f, 30f, false });
							return;
						}
						catch
						{
							// Continuar con la siguiente opción si falla
						}
					}
				}
			}

			// TERCERA OPCIÓN: Usar ComponentChaseBehavior (sistema antiguo)
			ComponentChaseBehavior oldChase = ally.Entity.FindComponent<ComponentChaseBehavior>();
			if (oldChase != null)
			{
				// Usar reflexión para llamar al método Attack si existe
				var attackMethod = oldChase.GetType().GetMethod("Attack");
				if (attackMethod != null)
				{
					try
					{
						attackMethod.Invoke(oldChase, new object[] { target, 30f, 30f, false });
					}
					catch
					{
						// Si falla, intentar de otra manera
					}
				}
			}

			// CUARTA OPCIÓN: Intentar directamente con ComponentNewChaseBehavior usando reflexión si no se encontró
			// (por si acaso el tipo no coincide exactamente)
			if (newChase == null)
			{
				var newChaseType = ally.Entity.FindComponent<ComponentNewChaseBehavior>();
				if (newChaseType != null)
				{
					var attackMethod = newChaseType.GetType().GetMethod("Attack");
					if (attackMethod != null)
					{
						try
						{
							attackMethod.Invoke(newChaseType, new object[] { target, 30f, 30f, false });
						}
						catch
						{
							// Si falla, no hacer nada
						}
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
		private const int TargeterBlockID = 329; // Debe coincidir
	}
}
