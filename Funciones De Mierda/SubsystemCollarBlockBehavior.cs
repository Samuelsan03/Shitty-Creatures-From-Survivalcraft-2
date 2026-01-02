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
		private SubsystemAudio m_subsystemAudio;
		private const float MaxUseDistance = 3f;

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
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			BodyRaycastResult? bodyRaycastResult = componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			if (bodyRaycastResult == null)
			{
				return false;
			}

			float distance = Vector3.Distance(ray.Position, bodyRaycastResult.Value.HitPoint());
			if (distance > MaxUseDistance)
			{
				Console.WriteLine($"Distancia demasiado grande para usar collar: {distance}m (máximo: {MaxUseDistance}m)");
				return false;
			}

			Entity entity = bodyRaycastResult.Value.ComponentBody.Entity;
			ComponentHealth componentHealth = entity.FindComponent<ComponentHealth>();
			if (componentHealth != null && componentHealth.Health <= 0f)
			{
				return false;
			}

			string currentEntityName = entity.ValuesDictionary.DatabaseObject.Name;
			string entityTemplateName;

			Console.WriteLine($"Entidad detectada: {currentEntityName} a distancia: {distance}m");

			Dictionary<string, string> tameableCreatures = new Dictionary<string, string>()
			{
				{"InfectedNormal1", "InfectedNormalTamed1"},
				{"InfectedNormal2", "InfectedNormalTamed2"},
				{"InfectedFast1", "InfectedFastTamed1"},
				{"InfectedFast2", "InfectedFastTamed2"},
				{"InfectedMuscle1", "InfectedMuscleTamed1"},
				{"InfectedMuscle2", "InfectedMuscleTamed2"},
				{"PoisonousInfected1", "PoisonousInfectedTamed1"},
				{"PoisonousInfected2", "PoisonousInfectedTamed2"},
				{"InfectedFly1", "InfectedFlyTamed1"},
				{"InfectedFly2", "InfectedFlyTamed2"},
				{"InfectedFly3", "InfectedFlyTamed3"},
				{"FlyingInfectedBoss", "FlyingInfectedBossTamed"},
				{"Boomer1", "BoomerTamed1"},
				{"Boomer2", "BoomerTamed2"},
				{"Boomer3", "BoomerTamed3"},
				{"Charger1", "ChargerTamed1"},
				{"Charger2", "ChargerTamed2"},
				{"Tank1", "TankTamed1" },
				{"Tank2", "TankTamed2" },
				{"Tank3", "TankTamed3" }
			};

			if (tameableCreatures.ContainsKey(currentEntityName))
			{
				bool isBoomer = currentEntityName.StartsWith("Boomer");
				ComponentBoomerExplosion boomerExplosion = null;
				ComponentBoomerExplosion2 boomerExplosion2 = null;
				ComponentBoomerPoisonExplosion boomerPoisonExplosion = null;

				if (isBoomer)
				{
					boomerExplosion = entity.FindComponent<ComponentBoomerExplosion>();
					boomerExplosion2 = entity.FindComponent<ComponentBoomerExplosion2>();
					boomerPoisonExplosion = entity.FindComponent<ComponentBoomerPoisonExplosion>();

					if (boomerExplosion != null)
					{
						boomerExplosion.PreventExplosion = true;
						Console.WriteLine($"Prevenida explosión del Boomer (ComponentBoomerExplosion) durante domesticación");
					}

					if (boomerExplosion2 != null)
					{
						boomerExplosion2.PreventExplosion = true;
						Console.WriteLine($"Prevenida explosión del Boomer (ComponentBoomerExplosion2) durante domesticación");
					}

					if (boomerPoisonExplosion != null)
					{
						boomerPoisonExplosion.PreventExplosion = true;
						Console.WriteLine($"Prevenida explosión de veneno del Boomer (ComponentBoomerPoisonExplosion) durante domesticación");
					}
				}

				// Obtener el ComponentNewCreatureCollect de la entidad original ANTES de eliminarla
				ComponentNewCreatureCollect originalCollectComponent = entity.FindComponent<ComponentNewCreatureCollect>();
				if (originalCollectComponent != null)
				{
					Console.WriteLine($"ComponentNewCreatureCollect encontrado en la entidad original");
				}
				else
				{
					Console.WriteLine($"ComponentNewCreatureCollect NO encontrado en la entidad original");
				}

				entityTemplateName = tameableCreatures[currentEntityName];
				Console.WriteLine($"¡{currentEntityName} detectado! Transformando a {entityTemplateName}...");

				Entity entity2 = DatabaseManager.CreateEntity(base.Project, entityTemplateName, false);
				if (entity2 == null)
				{
					Console.WriteLine($"ERROR: No se pudo crear la entidad: {entityTemplateName}");
					if (boomerExplosion != null) boomerExplosion.PreventExplosion = false;
					if (boomerExplosion2 != null) boomerExplosion2.PreventExplosion = false;
					if (boomerPoisonExplosion != null) boomerPoisonExplosion.PreventExplosion = false;
					return true;
				}

				Console.WriteLine($"Nueva entidad creada: {entityTemplateName}");

				ComponentBody componentBody = entity2.FindComponent<ComponentBody>(true);
				componentBody.Position = bodyRaycastResult.Value.ComponentBody.Position;
				componentBody.Rotation = bodyRaycastResult.Value.ComponentBody.Rotation;
				componentBody.Velocity = bodyRaycastResult.Value.ComponentBody.Velocity;
				entity2.FindComponent<ComponentSpawn>(true).SpawnDuration = 0f;

				// Obtener el ComponentNewCreatureCollect de la nueva entidad
				ComponentNewCreatureCollect newCollectComponent = entity2.FindComponent<ComponentNewCreatureCollect>();
				if (newCollectComponent != null)
				{
					Console.WriteLine($"ComponentNewCreatureCollect encontrado en la nueva entidad");
				}
				else
				{
					Console.WriteLine($"ComponentNewCreatureCollect NO encontrado en la nueva entidad");
				}

				// Copiar el inventario de la entidad original a la nueva
				if (originalCollectComponent != null && newCollectComponent != null)
				{
					Console.WriteLine("Copiando inventario de la entidad original a la nueva entidad domesticada...");
					newCollectComponent.CopyInventoryFrom(originalCollectComponent);
				}
				else
				{
					Console.WriteLine("No se pudo copiar el inventario - uno de los componentes es null");
				}

				// Remover la entidad original y agregar la nueva
				base.Project.RemoveEntity(entity, true);
				base.Project.AddEntity(entity2);

				Console.WriteLine("Transformación completada exitosamente!");

				bool isTamedBoss = entityTemplateName == "FlyingInfectedBossTamed";
				bool isTamedBoomer = entityTemplateName.StartsWith("BoomerTamed");
				bool isTamedCharger = entityTemplateName.StartsWith("ChargerTamed");
				bool isTamedTank = entityTemplateName.StartsWith("TankTamed");

				ComponentPlayer componentPlayer = FindPlayerWithMiner(componentMiner);
				if (componentPlayer != null)
				{
					try
					{
						string message;
						Color messageColor;
						string soundToPlay;

						if (isTamedBoss)
						{
							bool translationFound;
							message = LanguageControl.Get(out translationFound, "Messages", "CollarTamedBossMessage");

							if (!translationFound)
							{
								message = "You have tamed the Flying Infected Boss, use it wisely for emergency cases";
							}

							messageColor = new Color(66, 0, 142);
							soundToPlay = "Audio/UI/Bosses FNAF 3";
						}
						else if (isTamedBoomer)
						{
							bool translationFound;
							message = LanguageControl.Get(out translationFound, "Messages", "CollarTamedBoomerMessage");

							if (!translationFound)
							{
								message = "You have tamed a Boomer!\nNow you can use it as an explosive guardian slave.\nUse it wisely in emergency cases!";
							}

							messageColor = new Color(153, 0, 76);
							soundToPlay = "Audio/UI/Bosses FNAF 3";
						}
						else if (isTamedCharger)
						{
							bool translationFound;
							message = LanguageControl.Get(out translationFound, "Messages", "CollarTamedChargerMessage");

							if (!translationFound)
							{
								message = "You have tamed a Charger! Its brute force to push enemies strongly will now be your tool! Use it well and wisely!";
							}

							messageColor = new Color(44, 44, 110);
							soundToPlay = "Audio/UI/Bosses FNAF 3";
						}
						else if (isTamedTank)
						{
							bool translationFound;
							message = LanguageControl.Get(out translationFound, "Messages", "CollarTamedTankMessage");

							if (!translationFound)
							{
								message = "You have tamed a Tank! The most feared boss of bosses is now your slave! Take advantage of its brute force as your guardian!";
							}

							messageColor = new Color(153, 0, 0);
							soundToPlay = "Audio/UI/Tank Tamed Sound";
						}
						else
						{
							bool translationFound;
							message = LanguageControl.Get(out translationFound, "Messages", "CollarTamedMessage");

							if (!translationFound)
							{
								message = "You have tamed a hostile Infected!\nNow it will be your guardian!";
							}

							messageColor = new Color(0, 255, 128);
							soundToPlay = "Audio/UI/Tada";
						}

						Console.WriteLine($"Mostrando mensaje: {message}");
						componentPlayer.ComponentGui.DisplaySmallMessage(message, messageColor, false, false);

						if (this.m_subsystemAudio != null)
						{
							try
							{
								this.m_subsystemAudio.PlaySound(soundToPlay, 1f, 0f, 0f, 0.0001f);
								Console.WriteLine($"Sonido {soundToPlay} reproducido");
							}
							catch (Exception soundEx)
							{
								Console.WriteLine($"Error al reproducir sonido: {soundEx.Message}");
								componentPlayer.ComponentGui.DisplaySmallMessage(message, messageColor, false, true);
							}
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Error al mostrar mensaje: {ex.Message}");
						Console.WriteLine($"StackTrace: {ex.StackTrace}");

						string defaultMessage;
						Color defaultColor;

						if (isTamedBoss)
						{
							defaultMessage = "You have tamed the Flying Infected Boss, use it wisely for emergency cases";
							defaultColor = new Color(66, 0, 142);
						}
						else if (isTamedBoomer)
						{
							defaultMessage = "You have tamed a Boomer!\nNow you can use it as an explosive guardian slave.\nUse it wisely in emergency cases!";
							defaultColor = new Color(153, 0, 76);
						}
						else if (isTamedCharger)
						{
							defaultMessage = "You have tamed a Charger! Its brute force to push enemies strongly will now be your tool! Use it well and wisely!";
							defaultColor = new Color(44, 44, 110);
						}
						else if (isTamedTank)
						{
							defaultMessage = "You have tamed a Tank! The most feared boss of bosses is now your slave! Take advantage of its brute force as your guardian!";
							defaultColor = new Color(153, 0, 0);
						}
						else
						{
							defaultMessage = "You have tamed a hostile Infected! Now it will be your guardian!";
							defaultColor = new Color(0, 255, 128);
						}

						componentPlayer.ComponentGui.DisplaySmallMessage(defaultMessage, defaultColor, false, true);
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

			foreach (ComponentPlayer componentPlayer in this.m_subsystemPlayers.ComponentPlayers)
			{
				if (componentPlayer.ComponentMiner == componentMiner)
				{
					return componentPlayer;
				}
			}

			ComponentPlayer componentPlayerFromEntity = componentMiner.Entity.FindComponent<ComponentPlayer>();
			if (componentPlayerFromEntity != null)
			{
				return componentPlayerFromEntity;
			}

			if (this.m_subsystemPlayers.ComponentPlayers.Count > 0)
			{
				return this.m_subsystemPlayers.ComponentPlayers[0];
			}

			return null;
		}

		private System.Random m_random = new System.Random();

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
