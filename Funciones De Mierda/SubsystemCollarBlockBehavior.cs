using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemCollarBlockBehavior : SubsystemBlockBehavior
	{
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemPlayers m_subsystemPlayers;

		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					437
				};
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			BodyRaycastResult? bodyRaycastResult = componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			if (bodyRaycastResult == null)
			{
				return false;
			}
			Entity entity = bodyRaycastResult.Value.ComponentBody.Entity;
			ComponentHealth componentHealth = entity.FindComponent<ComponentHealth>();
			if (componentHealth != null && componentHealth.Health <= 0f)
			{
				return false;
			}

			// OBTENER EL NOMBRE REAL DE LA PLANTILLA DE LA ENTIDAD
			string currentEntityName = entity.ValuesDictionary.DatabaseObject.Name;
			string entityTemplateName;

			Console.WriteLine($"Entidad detectada: {currentEntityName}");

			// DICCIONARIO DE CRIATURAS DOMESTICABLES
			// [NombreOriginal] -> [NombreDomesticado]
			Dictionary<string, string> tameableCreatures = new Dictionary<string, string>()
			{
				{"InfectedNormal1", "InfectedNormalTamed1"},
				// Agrega más criaturas aquí:
				// {"Wolf", "WolfTamed"},
				// {"Bear", "BearTamed"},
				// {"Lion", "LionTamed"}
			};

			// VERIFICAR SI ES UNA CRIATURA DOMESTICABLE
			if (tameableCreatures.ContainsKey(currentEntityName))
			{
				// Transformar a la versión domesticada
				entityTemplateName = tameableCreatures[currentEntityName];
				Console.WriteLine($"¡{currentEntityName} detectado! Transformando a {entityTemplateName}...");

				// Crear la nueva entidad domesticada
				Entity entity2 = DatabaseManager.CreateEntity(base.Project, entityTemplateName, false);
				if (entity2 == null)
				{
					Console.WriteLine($"ERROR: No se pudo crear la entidad: {entityTemplateName}");
					return true;
				}

				Console.WriteLine($"Nueva entidad creada: {entityTemplateName}");

				ComponentBody componentBody = entity2.FindComponent<ComponentBody>(true);
				componentBody.Position = bodyRaycastResult.Value.ComponentBody.Position;
				componentBody.Rotation = bodyRaycastResult.Value.ComponentBody.Rotation;
				componentBody.Velocity = bodyRaycastResult.Value.ComponentBody.Velocity;
				entity2.FindComponent<ComponentSpawn>(true).SpawnDuration = 0f;
				base.Project.RemoveEntity(entity, true);
				base.Project.AddEntity(entity2);

				Console.WriteLine("Transformación completada exitosamente!");

				// Mostrar mensaje de domesticación al jugador
				ComponentPlayer componentPlayer = FindPlayerWithMiner(componentMiner);
				if (componentPlayer != null)
				{
					try
					{
						// Obtener el mensaje traducido
						string message = LanguageControl.Get("Messages", "CollarTamedMessage", "You have tamed a hostile Infected! Now it will be your guardian!");
						Console.WriteLine($"Mostrando mensaje: {message}");

						// Mostrar el mensaje con color RGB personalizado
						// RGB: R=50, G=205, B=50 (verde claro/lime)
						// true, true = mostrar tintineo y jugar sonido
						componentPlayer.ComponentGui.DisplaySmallMessage(message, new Color(102, 255, 178), true, true);
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Error al mostrar mensaje: {ex.Message}");
						Console.WriteLine($"StackTrace: {ex.StackTrace}");

						// Si falla, mostrar mensaje por defecto
						componentPlayer.ComponentGui.DisplaySmallMessage("You have tamed a hostile Infected! Now it will be your guardian!", new Color(50, 205, 50), true, true);
					}
				}
				else
				{
					Console.WriteLine("No se encontró el componente del jugador");
				}

				componentMiner.RemoveActiveTool(1);
				return true;
			}
			else
			{
				// Para otras entidades, usar variantes de collar normales
				entityTemplateName = CollarVariants[this.m_random.Next(CollarVariants.Length)];
				Console.WriteLine($"Entidad no domesticable: {currentEntityName}. Usando collar normal.");

				Entity entity2 = DatabaseManager.CreateEntity(base.Project, entityTemplateName, false);
				if (entity2 == null)
				{
					Console.WriteLine($"ERROR: No se pudo crear la entidad: {entityTemplateName}");
					return true;
				}

				Console.WriteLine($"Nueva entidad creada: {entityTemplateName}");

				ComponentBody componentBody = entity2.FindComponent<ComponentBody>(true);
				componentBody.Position = bodyRaycastResult.Value.ComponentBody.Position;
				componentBody.Rotation = bodyRaycastResult.Value.ComponentBody.Rotation;
				componentBody.Velocity = bodyRaycastResult.Value.ComponentBody.Velocity;
				entity2.FindComponent<ComponentSpawn>(true).SpawnDuration = 0f;
				base.Project.RemoveEntity(entity, true);
				base.Project.AddEntity(entity2);

				componentMiner.RemoveActiveTool(1);
				return true;
			}
		}

		private ComponentPlayer FindPlayerWithMiner(ComponentMiner componentMiner)
		{
			if (this.m_subsystemPlayers == null || componentMiner == null)
				return null;

			// Buscar el jugador que tiene este componente minero
			foreach (ComponentPlayer componentPlayer in this.m_subsystemPlayers.ComponentPlayers)
			{
				if (componentPlayer.ComponentMiner == componentMiner)
				{
					return componentPlayer;
				}
			}

			// Si no lo encuentra, intentar obtener el jugador desde el componente padre
			ComponentPlayer componentPlayerFromEntity = componentMiner.Entity.FindComponent<ComponentPlayer>();
			if (componentPlayerFromEntity != null)
			{
				return componentPlayerFromEntity;
			}

			// Como último recurso, usar el primer jugador disponible
			if (this.m_subsystemPlayers.ComponentPlayers.Count > 0)
			{
				return this.m_subsystemPlayers.ComponentPlayers[0];
			}

			return null;
		}

		private System.Random m_random = new System.Random();

		// VARIANTES DE COLLAR DE NPC (para NPCs no zombies)
		private static readonly string[] CollarVariants = new string[]
		{
			"NPC_Collar_1",
			"NPC_Collar_2",
			"NPC_Collar_3",
			"NPC_Collar_4",
			"NPC_Collar_5",
			"NPC_Collar_6",
			"NPC_Collar_7"
		};
	}
}
