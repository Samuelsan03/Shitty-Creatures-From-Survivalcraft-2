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
		private System.Random m_random = new System.Random();
		private Dictionary<string, string> m_tameableCreatures;

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

		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { 437 };
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);

			m_tameableCreatures = new Dictionary<string, string>()
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
				{"Tank3", "TankTamed3" },
				{"GhostNormal", "GhostNormalTamed" },
				{"GhostFast", "GhostFastTamed" },
				{"PoisonousGhost", "PoisonousGhostTamed" },
				{"GhostCharger", "GhostChargerTamed" },
				{"GhostBoomer1", "GhostBoomerTamed1" },
				{"GhostBoomer2", "GhostBoomerTamed2" },
				{"GhostBoomer3", "GhostBoomerTamed3" },
				{"TankGhost1", "TankGhostTamed1" },
				{"TankGhost2", "TankGhostTamed2" },
				{"TankGhost3", "TankGhostTamed3" },
				{"MachineGunInfected", "MachineGunInfectedTamed" },
				{"InfectedWolf", "InfectedWolfTamed"},
				{"InfectedWerewolf", "InfectedWerewolfTamed"},
				{"InfectedHyena", "InfectedHyenaTamed" },
				{"InfectedBear", "InfectedBearTamed" },
				{"HumanoidSkeleton", "HumanoidSkeletonTamed"},
				{"PredatoryChameleon", "PredatoryChameleonTamed"},
				{"InfectedBird", "InfectedBirdTamed"},
				{"InfectedWildboar", "InfectedWildboarTamed"},
				{"InfectedFreezer", "InfectedFreezerTamed" },
				{"BoomerFrozen", "BoomerFrozenTamed" },
				{"FrozenGhost", "FrozenGhostTamed" },
				{"FrozenGhostBoomer", "FrozenGhostBoomerTamed" },
				{"FrozenTank", "FrozenTankTamed"},
				{"FrozenTankGhost", "FrozenTankGhostTamed"},
				{"InfectedSpider", "InfectedSpiderTamed"},
			};
		}

		public bool IsCreatureTameable(Entity entity)
		{
			if (entity == null) return false;
			if (m_tameableCreatures == null) return false;
			try
			{
				string entityName = entity.ValuesDictionary?.DatabaseObject?.Name;
				if (string.IsNullOrEmpty(entityName)) return false;
				return m_tameableCreatures.ContainsKey(entityName);
			}
			catch
			{
				return false;
			}
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			BodyRaycastResult? bodyRaycastResult = componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			if (bodyRaycastResult == null)
				return false;
			float distance = Vector3.Distance(ray.Position, bodyRaycastResult.Value.HitPoint());
			if (distance > MaxUseDistance)
				return false;
			Entity entity = bodyRaycastResult.Value.ComponentBody.Entity;
			ComponentHealth componentHealth = entity.FindComponent<ComponentHealth>();
			if (componentHealth != null && componentHealth.Health <= 0f)
				return false;
			string currentEntityName = entity.ValuesDictionary.DatabaseObject.Name;

			ComponentPlayer componentPlayer = FindPlayerWithMiner(componentMiner);

			if (m_tameableCreatures.ContainsKey(currentEntityName))
			{
				bool isBoomer = currentEntityName.StartsWith("Boomer") || currentEntityName.StartsWith("GhostBoomer");
				bool isGhostBoomer = currentEntityName.StartsWith("GhostBoomer");
				bool isFrozenGhostBoomer = currentEntityName == "FrozenGhostBoomer";
				bool isBoomerFrozen = currentEntityName == "BoomerFrozen";
				bool isFrozenGhost = currentEntityName == "FrozenGhost";
				ComponentBoomerExplosion boomerExplosion = null;
				ComponentBoomerFireExplosion boomerExplosion2 = null;
				ComponentBoomerPoisonExplosion boomerPoisonExplosion = null;
				ComponentBoomerFrozenExplosion boomerFrozenExplosion = null;
				if (isBoomer)
				{
					boomerExplosion = entity.FindComponent<ComponentBoomerExplosion>();
					boomerExplosion2 = entity.FindComponent<ComponentBoomerFireExplosion>();
					boomerPoisonExplosion = entity.FindComponent<ComponentBoomerPoisonExplosion>();
					if (boomerExplosion != null) boomerExplosion.PreventExplosion = true;
					if (boomerExplosion2 != null) boomerExplosion2.PreventExplosion = true;
					if (boomerPoisonExplosion != null) boomerPoisonExplosion.PreventExplosion = true;
				}
				if (isFrozenGhostBoomer || isBoomerFrozen || isFrozenGhost)
				{
					boomerFrozenExplosion = entity.FindComponent<ComponentBoomerFrozenExplosion>();
					if (boomerFrozenExplosion != null) boomerFrozenExplosion.PreventExplosion = true;
				}

				string entityTemplateName = m_tameableCreatures[currentEntityName];
				Entity entity2 = DatabaseManager.CreateEntity(base.Project, entityTemplateName, false);
				if (entity2 == null)
				{
					if (boomerExplosion != null) boomerExplosion.PreventExplosion = false;
					if (boomerExplosion2 != null) boomerExplosion2.PreventExplosion = false;
					if (boomerPoisonExplosion != null) boomerPoisonExplosion.PreventExplosion = false;
					if (boomerFrozenExplosion != null) boomerFrozenExplosion.PreventExplosion = false;
					return true;
				}

				ComponentBody componentBody = entity2.FindComponent<ComponentBody>(true);
				componentBody.Position = bodyRaycastResult.Value.ComponentBody.Position;
				componentBody.Rotation = bodyRaycastResult.Value.ComponentBody.Rotation;
				componentBody.Velocity = bodyRaycastResult.Value.ComponentBody.Velocity;
				entity2.FindComponent<ComponentSpawn>(true).SpawnDuration = 0f;

				// ============================================================
				// COPIA DEL INVENTARIO USANDO EXCLUSIVAMENTE COMPONENTCOLLECTPICKABLEBEHAVIOR
				// ============================================================
				ComponentCollectPickableBehavior collectBehavior = entity2.FindComponent<ComponentCollectPickableBehavior>();
				if (collectBehavior != null)
				{
					collectBehavior.CopyInventoryFrom(entity);
				}
				// ============================================================

				base.Project.RemoveEntity(entity, true);
				base.Project.AddEntity(entity2);

				bool isTamedBoss = entityTemplateName == "FlyingInfectedBossTamed";
				bool isTamedBoomer = entityTemplateName.StartsWith("BoomerTamed");
				bool isTamedGhostBoomer = entityTemplateName.StartsWith("GhostBoomerTamed");
				bool isTamedFrozenGhostBoomer = entityTemplateName == "FrozenGhostBoomerTamed";
				bool isTamedBoomerFrozen = entityTemplateName == "BoomerFrozenTamed";
				bool isTamedFrozenGhost = entityTemplateName == "FrozenGhostTamed";
				bool isTamedCharger = entityTemplateName.StartsWith("ChargerTamed");
				bool isTamedGhostCharger = entityTemplateName == "GhostChargerTamed";
				bool isTamedTank = entityTemplateName.StartsWith("TankTamed");
				bool isTamedGhostTank = entityTemplateName.StartsWith("TankGhostTamed");
				bool isTamedMachineGun = entityTemplateName == "MachineGunInfectedTamed";
				bool isTamedGhost = entityTemplateName == "GhostNormalTamed" || entityTemplateName == "GhostFastTamed" || entityTemplateName == "PoisonousGhostTamed";
				bool isTamedWolf = entityTemplateName == "InfectedWolfTamed";
				bool isTamedWerewolf = entityTemplateName == "InfectedWerewolfTamed";
				bool isTamedHyena = entityTemplateName == "InfectedHyenaTamed";
				bool isTamedBear = entityTemplateName == "InfectedBearTamed";
				bool isTamedHumanoidSkeleton = entityTemplateName == "HumanoidSkeletonTamed";
				bool isTamedPredatoryChameleon = entityTemplateName == "PredatoryChameleonTamed";
				bool isTamedInfectedBird = entityTemplateName == "InfectedBirdTamed";
				bool isTamedInfectedWildboar = entityTemplateName == "InfectedWildboarTamed";
				bool isTamedInfectedFreezer = entityTemplateName == "InfectedFreezerTamed";
				bool isTamedFrozenTank = entityTemplateName == "FrozenTankTamed";
				bool isTamedFrozenTankGhost = entityTemplateName == "FrozenTankGhostTamed";
				bool isTamedInfectedSpider = entityTemplateName == "InfectedSpiderTamed";

				if (componentPlayer != null)
				{
					string messageKey = "";
					Color messageColor = Color.White;
					string soundToPlay = "Audio/UI/Tada";

					if (isTamedBoss)
					{
						messageKey = "CollarTamedBossMessage";
						messageColor = new Color(66, 0, 142);
						soundToPlay = "Audio/UI/Bosses FNAF 3";
					}
					else if (isTamedBoomer)
					{
						messageKey = "CollarTamedBoomerMessage";
						messageColor = new Color(153, 0, 76);
						soundToPlay = "Audio/UI/Bosses FNAF 3";
					}
					else if (isTamedGhostBoomer)
					{
						messageKey = "CollarTamedGhostBoomerMessage";
						messageColor = new Color(102, 0, 153);
						soundToPlay = "Audio/UI/Bosses FNAF 3";
					}
					else if (isTamedFrozenGhostBoomer)
					{
						messageKey = "CollarTamedFrozenGhostBoomerMessage";
						messageColor = new Color(0, 191, 255);
						soundToPlay = "Audio/UI/Bosses FNAF 3";
					}
					else if (isTamedBoomerFrozen)
					{
						messageKey = "CollarTamedBoomerFrozenMessage";
						messageColor = new Color(0, 191, 255);
						soundToPlay = "Audio/UI/Bosses FNAF 3";
					}
					else if (isTamedFrozenGhost)
					{
						messageKey = "CollarTamedFrozenGhostMessage";
						messageColor = new Color(0, 191, 255);
						soundToPlay = "Audio/UI/Tada";
					}
					else if (isTamedCharger)
					{
						messageKey = "CollarTamedChargerMessage";
						messageColor = new Color(44, 44, 110);
						soundToPlay = "Audio/UI/Bosses FNAF 3";
					}
					else if (isTamedGhostCharger)
					{
						messageKey = "CollarTamedGhostChargerMessage";
						messageColor = new Color(75, 0, 130);
						soundToPlay = "Audio/UI/Bosses FNAF 3";
					}
					else if (isTamedTank)
					{
						messageKey = "CollarTamedTankMessage";
						messageColor = new Color(153, 0, 0);
						soundToPlay = "Audio/UI/Tank Tamed Sound";
					}
					else if (isTamedMachineGun)
					{
						messageKey = "CollarTamedMachineGunMessage";
						messageColor = new Color(255, 140, 0);
						soundToPlay = "Audio/UI/Tank Tamed Sound";
					}
					else if (isTamedGhostTank)
					{
						messageKey = "CollarTamedGhostTankMessage";
						messageColor = new Color(139, 0, 139);
						soundToPlay = "Audio/UI/Tank Tamed Sound";
					}
					else if (isTamedFrozenTank)
					{
						messageKey = "CollarTamedFrozenTankMessage";
						messageColor = new Color(0, 191, 255);
						soundToPlay = "Audio/UI/Tank Tamed Sound";
					}
					else if (isTamedFrozenTankGhost)
					{
						messageKey = "CollarTamedFrozenTankGhostMessage";
						messageColor = new Color(100, 200, 255);
						soundToPlay = "Audio/UI/Tank Tamed Sound";
					}
					else if (isTamedGhost)
					{
						messageKey = "CollarTamedGhostMessage";
						messageColor = new Color(128, 0, 128);
						soundToPlay = "Audio/UI/Tada";
					}
					else if (isTamedWolf)
					{
						messageKey = "CollarTamedInfectedWolfMessage";
						messageColor = new Color(0, 255, 128);
						soundToPlay = "Audio/UI/Tada";
					}
					else if (isTamedWerewolf)
					{
						messageKey = "CollarTamedInfectedWerewolfMessage";
						messageColor = new Color(0, 255, 128);
						soundToPlay = "Audio/UI/Tada";
					}
					else if (isTamedHyena)
					{
						messageKey = "CollarTamedInfectedHyenaMessage";
						messageColor = new Color(255, 215, 0);
						soundToPlay = "Audio/UI/Tada";
					}
					else if (isTamedBear)
					{
						messageKey = "CollarTamedInfectedBearMessage";
						messageColor = new Color(131, 0, 0);
						soundToPlay = "Audio/UI/Tada";
					}
					else if (isTamedHumanoidSkeleton)
					{
						messageKey = "CollarTamedHumanoidSkeletonMessage";
						messageColor = new Color(200, 200, 200);
						soundToPlay = "Audio/UI/Tada";
					}
					else if (isTamedPredatoryChameleon)
					{
						messageKey = "CollarTamedPredatoryChameleonMessage";
						messageColor = new Color(50, 205, 50);
						soundToPlay = "Audio/UI/Tada";
					}
					else if (isTamedInfectedBird)
					{
						messageKey = "CollarTamedInfectedBirdMessage";
						messageColor = new Color(135, 206, 235);
						soundToPlay = "Audio/UI/Tada";
					}
					else if (isTamedInfectedWildboar)
					{
						messageKey = "CollarTamedInfectedWildboarMessage";
						messageColor = new Color(139, 69, 19);
						soundToPlay = "Audio/UI/Tada";
					}
					else if (isTamedInfectedFreezer)
					{
						messageKey = "CollarTamedInfectedFreezerMessage";
						messageColor = new Color(0, 191, 255);
						soundToPlay = "Audio/UI/Tada";
					}
					else if (isTamedInfectedSpider)
					{
						messageKey = "CollarTamedInfectedSpiderMessage";
						messageColor = new Color(128, 0, 128);
						soundToPlay = "Audio/UI/Tada";
					}
					else
					{
						messageKey = "CollarTamedMessage";
						messageColor = new Color(0, 255, 128);
						soundToPlay = "Audio/UI/Tada";
					}

					string message = LanguageControl.Get("Messages", messageKey);
					componentPlayer.ComponentGui.DisplaySmallMessage(message, messageColor, false, false);

					if (this.m_subsystemAudio != null)
					{
						this.m_subsystemAudio.PlaySound(soundToPlay, 1f, 0f, 0f, 0.0001f);
					}

					bool isBossTame = isTamedTank || isTamedGhostTank || isTamedMachineGun || isTamedBoss || isTamedFrozenTank || isTamedFrozenTankGhost;
					bool isGhostTame = isTamedGhost || isTamedGhostCharger || isTamedGhostBoomer || isTamedFrozenGhost || isTamedFrozenGhostBoomer;

					if (isBossTame)
					{
						AchievementsManager.OnBossTame(componentPlayer);
					}
					else if (isGhostTame)
					{
						AchievementsManager.OnGhostTame(componentPlayer);
					}
					else
					{
						AchievementsManager.OnNormalTame(componentPlayer);
					}
				}
				componentMiner.RemoveActiveTool(1);
				return true;
			}
			else
			{
				string entityTemplateName = CollarVariants[this.m_random.Next(CollarVariants.Length)];
				Entity entity2 = DatabaseManager.CreateEntity(base.Project, entityTemplateName, false);
				if (entity2 == null) return true;
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
			if (this.m_subsystemPlayers == null || componentMiner == null) return null;
			foreach (ComponentPlayer componentPlayer in this.m_subsystemPlayers.ComponentPlayers)
			{
				if (componentPlayer.ComponentMiner == componentMiner) return componentPlayer;
			}
			ComponentPlayer componentPlayerFromEntity = componentMiner.Entity.FindComponent<ComponentPlayer>();
			if (componentPlayerFromEntity != null) return componentPlayerFromEntity;
			if (this.m_subsystemPlayers.ComponentPlayers.Count > 0) return this.m_subsystemPlayers.ComponentPlayers[0];
			return null;
		}
	}
}
