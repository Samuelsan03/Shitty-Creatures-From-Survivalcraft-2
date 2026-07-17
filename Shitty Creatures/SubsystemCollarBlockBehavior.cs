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

		private Dictionary<string, CreatureEntry> m_creaturesByWildName;
		private Dictionary<string, CategoriesOfInfected> m_categoryByTamedName;

		private static readonly Dictionary<CategoriesOfInfected, CreatureEntry[]> s_creaturesByCategory = new Dictionary<CategoriesOfInfected, CreatureEntry[]>
		{
			{
				CategoriesOfInfected.Normals, new CreatureEntry[]
				{
					new CreatureEntry { Wild = "InfectedNormal1", Tamed = "InfectedNormalTamed1" },
					new CreatureEntry { Wild = "InfectedNormal2", Tamed = "InfectedNormalTamed2" },
					new CreatureEntry { Wild = "InfectedFast1", Tamed = "InfectedFastTamed1" },
					new CreatureEntry { Wild = "InfectedFast2", Tamed = "InfectedFastTamed2" },
					new CreatureEntry { Wild = "InfectedMuscle1", Tamed = "InfectedMuscleTamed1" },
					new CreatureEntry { Wild = "InfectedMuscle2", Tamed = "InfectedMuscleTamed2" },
					new CreatureEntry { Wild = "PoisonousInfected1", Tamed = "PoisonousInfectedTamed1" },
					new CreatureEntry { Wild = "PoisonousInfected2", Tamed = "PoisonousInfectedTamed2" },
					new CreatureEntry { Wild = "Charger1", Tamed = "ChargerTamed1" },
					new CreatureEntry { Wild = "Charger2", Tamed = "ChargerTamed2" },
					new CreatureEntry { Wild = "InfectedFreezer", Tamed = "InfectedFreezerTamed" },
				}
			},
			{
				CategoriesOfInfected.Animals, new CreatureEntry[]
				{
					new CreatureEntry { Wild = "InfectedWolf", Tamed = "InfectedWolfTamed" },
					new CreatureEntry { Wild = "InfectedWerewolf", Tamed = "InfectedWerewolfTamed" },
					new CreatureEntry { Wild = "InfectedHyena", Tamed = "InfectedHyenaTamed" },
					new CreatureEntry { Wild = "InfectedBear", Tamed = "InfectedBearTamed" },
					new CreatureEntry { Wild = "HumanoidSkeleton", Tamed = "HumanoidSkeletonTamed" },
					new CreatureEntry { Wild = "PredatoryChameleon", Tamed = "PredatoryChameleonTamed" },
					new CreatureEntry { Wild = "InfectedBird", Tamed = "InfectedBirdTamed" },
					new CreatureEntry { Wild = "InfectedWildboar", Tamed = "InfectedWildboarTamed" },
					new CreatureEntry { Wild = "InfectedSpider", Tamed = "InfectedSpiderTamed" },
				}
			},
			{
				CategoriesOfInfected.Boomers, new CreatureEntry[]
				{
					new CreatureEntry { Wild = "Boomer1", Tamed = "BoomerTamed1" },
					new CreatureEntry { Wild = "Boomer2", Tamed = "BoomerTamed2" },
					new CreatureEntry { Wild = "Boomer3", Tamed = "BoomerTamed3" },
					new CreatureEntry { Wild = "BoomerFrozen", Tamed = "BoomerFrozenTamed" },
				}
			},
			{
				CategoriesOfInfected.Ghosts, new CreatureEntry[]
				{
					new CreatureEntry { Wild = "GhostNormal", Tamed = "GhostNormalTamed" },
					new CreatureEntry { Wild = "GhostFast", Tamed = "GhostFastTamed" },
					new CreatureEntry { Wild = "PoisonousGhost", Tamed = "PoisonousGhostTamed" },
					new CreatureEntry { Wild = "GhostCharger", Tamed = "GhostChargerTamed" },
					new CreatureEntry { Wild = "GhostBoomer1", Tamed = "GhostBoomerTamed1" },
					new CreatureEntry { Wild = "GhostBoomer2", Tamed = "GhostBoomerTamed2" },
					new CreatureEntry { Wild = "GhostBoomer3", Tamed = "GhostBoomerTamed3" },
					new CreatureEntry { Wild = "FrozenGhost", Tamed = "FrozenGhostTamed" },
					new CreatureEntry { Wild = "FrozenGhostBoomer", Tamed = "FrozenGhostBoomerTamed" },
				}
			},
			{
				CategoriesOfInfected.Bosses, new CreatureEntry[]
				{
					new CreatureEntry { Wild = "Tank1", Tamed = "TankTamed1" },
					new CreatureEntry { Wild = "Tank2", Tamed = "TankTamed2" },
					new CreatureEntry { Wild = "Tank3", Tamed = "TankTamed3" },
					new CreatureEntry { Wild = "TankGhost1", Tamed = "TankGhostTamed1" },
					new CreatureEntry { Wild = "TankGhost2", Tamed = "TankGhostTamed2" },
					new CreatureEntry { Wild = "TankGhost3", Tamed = "TankGhostTamed3" },
					new CreatureEntry { Wild = "MachineGunInfected", Tamed = "MachineGunInfectedTamed" },
					new CreatureEntry { Wild = "FlyingInfectedBoss", Tamed = "FlyingInfectedBossTamed" },
					new CreatureEntry { Wild = "FrozenTank", Tamed = "FrozenTankTamed" },
					new CreatureEntry { Wild = "FrozenTankGhost", Tamed = "FrozenTankGhostTamed" },
				}
			},
			{
				CategoriesOfInfected.Flyers, new CreatureEntry[]
				{
					new CreatureEntry { Wild = "InfectedFly1", Tamed = "InfectedFlyTamed1" },
					new CreatureEntry { Wild = "InfectedFly2", Tamed = "InfectedFlyTamed2" },
					new CreatureEntry { Wild = "InfectedFly3", Tamed = "InfectedFlyTamed3" },
				}
			},
		};

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
			get { return new int[] { 437 }; }
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);

			m_creaturesByWildName = new Dictionary<string, CreatureEntry>();
			m_categoryByTamedName = new Dictionary<string, CategoriesOfInfected>();

			foreach (var kvp in s_creaturesByCategory)
			{
				CategoriesOfInfected category = kvp.Key;
				foreach (CreatureEntry entry in kvp.Value)
				{
					m_creaturesByWildName[entry.Wild] = entry;
					m_categoryByTamedName[entry.Tamed] = category;
				}
			}
		}

		// ==========================================
		// CONSULTAS
		// ==========================================

		public bool IsCreatureTameable(Entity entity)
		{
			if (entity == null || m_creaturesByWildName == null) return false;
			try
			{
				string entityName = entity.ValuesDictionary?.DatabaseObject?.Name;
				return !string.IsNullOrEmpty(entityName) && m_creaturesByWildName.ContainsKey(entityName);
			}
			catch
			{
				return false;
			}
		}

		public string GetTamedTemplateName(Entity entity)
		{
			if (entity == null || m_creaturesByWildName == null) return null;
			try
			{
				string entityName = entity.ValuesDictionary?.DatabaseObject?.Name;
				if (string.IsNullOrEmpty(entityName)) return null;
				return m_creaturesByWildName.TryGetValue(entityName, out CreatureEntry entry) ? entry.Tamed : null;
			}
			catch
			{
				return null;
			}
		}

		private CategoriesOfInfected GetCategoryByWildName(string wildName)
		{
			if (string.IsNullOrEmpty(wildName) || m_creaturesByWildName == null) return CategoriesOfInfected.Normals;
			if (m_creaturesByWildName.TryGetValue(wildName, out CreatureEntry entry))
			{
				return m_categoryByTamedName.TryGetValue(entry.Tamed, out CategoriesOfInfected cat) ? cat : CategoriesOfInfected.Normals;
			}
			return CategoriesOfInfected.Normals;
		}

		private CategoriesOfInfected GetCategoryByTamedName(string tamedName)
		{
			if (string.IsNullOrEmpty(tamedName) || m_categoryByTamedName == null) return CategoriesOfInfected.Normals;
			m_categoryByTamedName.TryGetValue(tamedName, out CategoriesOfInfected category);
			return category;
		}

		// ==========================================
		// EXPLOSIONES
		// ==========================================

		private void SetExplosionPrevention(Entity entity, bool prevent)
		{
			var boom1 = entity.FindComponent<ComponentBoomerExplosion>();
			var boom2 = entity.FindComponent<ComponentBoomerFireExplosion>();
			var boom3 = entity.FindComponent<ComponentBoomerPoisonExplosion>();
			var boom4 = entity.FindComponent<ComponentBoomerFrozenExplosion>();
			if (boom1 != null) boom1.PreventExplosion = prevent;
			if (boom2 != null) boom2.PreventExplosion = prevent;
			if (boom3 != null) boom3.PreventExplosion = prevent;
			if (boom4 != null) boom4.PreventExplosion = prevent;
		}

		// ==========================================
		// DISPLAY Y SONIDO
		// ==========================================

		private void GetTamingDisplayInfo(string tamedName, out string messageKey, out Color messageColor, out string sound)
		{
			messageKey = "CollarTamedMessage";
			messageColor = new Color(0, 255, 128);
			sound = "Audio/UI/Tada";

			if (string.IsNullOrEmpty(tamedName)) return;

			switch (tamedName)
			{
				case "FlyingInfectedBossTamed":
					messageKey = "CollarTamedBossMessage";
					messageColor = new Color(66, 0, 142);
					sound = "Audio/UI/Bosses FNAF 3";
					return;

				case "MachineGunInfectedTamed":
					messageKey = "CollarTamedMachineGunMessage";
					messageColor = new Color(255, 140, 0);
					sound = "Audio/UI/Tank Tamed Sound";
					return;

				case "FrozenTankTamed":
					messageKey = "CollarTamedFrozenTankMessage";
					messageColor = new Color(0, 191, 255);
					sound = "Audio/UI/Tank Tamed Sound";
					return;

				case "FrozenTankGhostTamed":
					messageKey = "CollarTamedFrozenTankGhostMessage";
					messageColor = new Color(100, 200, 255);
					sound = "Audio/UI/Tank Tamed Sound";
					return;

				case "FrozenGhostBoomerTamed":
					messageKey = "CollarTamedFrozenGhostBoomerMessage";
					messageColor = new Color(0, 191, 255);
					sound = "Audio/UI/Bosses FNAF 3";
					return;

				case "BoomerFrozenTamed":
					messageKey = "CollarTamedBoomerFrozenMessage";
					messageColor = new Color(0, 191, 255);
					sound = "Audio/UI/Bosses FNAF 3";
					return;

				case "FrozenGhostTamed":
					messageKey = "CollarTamedFrozenGhostMessage";
					messageColor = new Color(0, 191, 255);
					return;

				case "InfectedFreezerTamed":
					messageKey = "CollarTamedInfectedFreezerMessage";
					messageColor = new Color(0, 191, 255);
					return;
			}

			if (tamedName.StartsWith("TankTamed"))
			{
				messageKey = "CollarTamedTankMessage";
				messageColor = new Color(153, 0, 0);
				sound = "Audio/UI/Tank Tamed Sound";
				return;
			}

			if (tamedName.StartsWith("TankGhostTamed"))
			{
				messageKey = "CollarTamedGhostTankMessage";
				messageColor = new Color(139, 0, 139);
				sound = "Audio/UI/Tank Tamed Sound";
				return;
			}

			if (tamedName.StartsWith("BoomerTamed"))
			{
				messageKey = "CollarTamedBoomerMessage";
				messageColor = new Color(153, 0, 76);
				sound = "Audio/UI/Bosses FNAF 3";
				return;
			}

			if (tamedName.StartsWith("GhostBoomerTamed"))
			{
				messageKey = "CollarTamedGhostBoomerMessage";
				messageColor = new Color(102, 0, 153);
				sound = "Audio/UI/Bosses FNAF 3";
				return;
			}

			if (tamedName.StartsWith("ChargerTamed"))
			{
				messageKey = "CollarTamedChargerMessage";
				messageColor = new Color(44, 44, 110);
				sound = "Audio/UI/Bosses FNAF 3";
				return;
			}

			if (tamedName == "GhostChargerTamed")
			{
				messageKey = "CollarTamedGhostChargerMessage";
				messageColor = new Color(75, 0, 130);
				sound = "Audio/UI/Bosses FNAF 3";
				return;
			}

			if (tamedName == "GhostNormalTamed" || tamedName == "GhostFastTamed" || tamedName == "PoisonousGhostTamed")
			{
				messageKey = "CollarTamedGhostMessage";
				messageColor = new Color(128, 0, 128);
				return;
			}

			switch (tamedName)
			{
				case "InfectedWolfTamed":
					messageKey = "CollarTamedInfectedWolfMessage";
					messageColor = new Color(0, 255, 128);
					return;

				case "InfectedWerewolfTamed":
					messageKey = "CollarTamedInfectedWerewolfMessage";
					messageColor = new Color(0, 255, 128);
					return;

				case "InfectedHyenaTamed":
					messageKey = "CollarTamedInfectedHyenaMessage";
					messageColor = new Color(255, 215, 0);
					return;

				case "InfectedBearTamed":
					messageKey = "CollarTamedInfectedBearMessage";
					messageColor = new Color(131, 0, 0);
					return;

				case "HumanoidSkeletonTamed":
					messageKey = "CollarTamedHumanoidSkeletonMessage";
					messageColor = new Color(200, 200, 200);
					return;

				case "PredatoryChameleonTamed":
					messageKey = "CollarTamedPredatoryChameleonMessage";
					messageColor = new Color(50, 205, 50);
					return;

				case "InfectedBirdTamed":
					messageKey = "CollarTamedInfectedBirdMessage";
					messageColor = new Color(135, 206, 235);
					return;

				case "InfectedWildboarTamed":
					messageKey = "CollarTamedInfectedWildboarMessage";
					messageColor = new Color(139, 69, 19);
					return;

				case "InfectedSpiderTamed":
					messageKey = "CollarTamedInfectedSpiderMessage";
					messageColor = new Color(128, 0, 128);
					return;
			}
		}

		private void AwardTamingAchievement(ComponentPlayer player, CategoriesOfInfected category)
		{
			if (player == null) return;
			switch (category)
			{
				case CategoriesOfInfected.Bosses:
					AchievementsManager.OnBossTame(player);
					break;
				case CategoriesOfInfected.Ghosts:
					AchievementsManager.OnGhostTame(player);
					break;
				default:
					AchievementsManager.OnNormalTame(player);
					break;
			}
		}

		private string GetTamingSound(string tamedName)
		{
			GetTamingDisplayInfo(tamedName, out _, out _, out string sound);
			return sound;
		}

		// ==========================================
		// TRANSFORMACIÓN
		// ==========================================

		private Entity CreateEntityAt(string templateName, Vector3 position, Quaternion rotation, Vector3 velocity)
		{
			Entity entity = DatabaseManager.CreateEntity(base.Project, templateName, false);
			if (entity == null) return null;
			ComponentBody body = entity.FindComponent<ComponentBody>(true);
			body.Position = position;
			body.Rotation = rotation;
			body.Velocity = velocity;
			entity.FindComponent<ComponentSpawn>(true).SpawnDuration = 0f;
			return entity;
		}

		private void CopyInventory(Entity source, Entity target)
		{
			ComponentCollectPickableBehavior collect = target.FindComponent<ComponentCollectPickableBehavior>();
			if (collect != null)
			{
				collect.CopyInventoryFrom(source);
			}
		}

		// ==========================================
		// OnUse
		// ==========================================

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

			if (m_creaturesByWildName.TryGetValue(currentEntityName, out CreatureEntry entry))
			{
				CategoriesOfInfected category = m_categoryByTamedName[entry.Tamed];

				SetExplosionPrevention(entity, true);

				Entity tamedEntity = CreateEntityAt(
					entry.Tamed,
					bodyRaycastResult.Value.ComponentBody.Position,
					bodyRaycastResult.Value.ComponentBody.Rotation,
					bodyRaycastResult.Value.ComponentBody.Velocity
				);

				if (tamedEntity == null)
				{
					SetExplosionPrevention(entity, false);
					return true;
				}

				CopyInventory(entity, tamedEntity);

				base.Project.RemoveEntity(entity, true);
				base.Project.AddEntity(tamedEntity);

				if (componentPlayer != null)
				{
					GetTamingDisplayInfo(entry.Tamed, out string messageKey, out Color messageColor, out string sound);

					string message = LanguageControl.Get("Messages", messageKey);
					componentPlayer.ComponentGui.DisplaySmallMessage(message, messageColor, false, false);

					if (this.m_subsystemAudio != null)
					{
						this.m_subsystemAudio.PlaySound(sound, 1f, 0f, 0f, 0.0001f);
					}

					AwardTamingAchievement(componentPlayer, category);
				}

				componentMiner.RemoveActiveTool(1);
				return true;
			}
			else
			{
				string collarVariant = CollarVariants[this.m_random.Next(CollarVariants.Length)];
				Entity collarEntity = CreateEntityAt(
					collarVariant,
					bodyRaycastResult.Value.ComponentBody.Position,
					bodyRaycastResult.Value.ComponentBody.Rotation,
					bodyRaycastResult.Value.ComponentBody.Velocity
				);

				if (collarEntity == null) return true;

				base.Project.RemoveEntity(entity, true);
				base.Project.AddEntity(collarEntity);
				componentMiner.RemoveActiveTool(1);
				return true;
			}
		}

		// ==========================================
		// TryTameCreature
		// ==========================================

		public bool TryTameCreature(Entity targetEntity, IInventory collarInventory, int collarSlotIndex)
		{
			if (targetEntity == null || collarInventory == null || m_creaturesByWildName == null)
				return false;

			try
			{
				string currentEntityName = targetEntity.ValuesDictionary?.DatabaseObject?.Name;
				if (string.IsNullOrEmpty(currentEntityName) || !m_creaturesByWildName.TryGetValue(currentEntityName, out CreatureEntry entry))
					return false;

				ComponentHealth componentHealth = targetEntity.FindComponent<ComponentHealth>();
				if (componentHealth != null && componentHealth.Health <= 0f)
					return false;

				CategoriesOfInfected category = m_categoryByTamedName[entry.Tamed];

				SetExplosionPrevention(targetEntity, true);

				ComponentBody targetBody = targetEntity.FindComponent<ComponentBody>();
				Vector3 position = targetBody != null ? targetBody.Position : Vector3.Zero;
				Quaternion rotation = targetBody != null ? targetBody.Rotation : Quaternion.Identity;
				Vector3 velocity = targetBody != null ? targetBody.Velocity : Vector3.Zero;

				Entity tamedEntity = CreateEntityAt(entry.Tamed, position, rotation, velocity);

				if (tamedEntity == null)
				{
					SetExplosionPrevention(targetEntity, false);
					return false;
				}

				CopyInventory(targetEntity, tamedEntity);

				base.Project.RemoveEntity(targetEntity, true);
				base.Project.AddEntity(tamedEntity);

				collarInventory.RemoveSlotItems(collarSlotIndex, 1);

				string sound = GetTamingSound(entry.Tamed);
				if (this.m_subsystemAudio != null && !string.IsNullOrEmpty(sound))
				{
					this.m_subsystemAudio.PlaySound(sound, 1f, 0f, position, 5f, false);
				}

				return true;
			}
			catch
			{
				return false;
			}
		}

		// ==========================================
		// AUXILIARES
		// ==========================================

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

		public enum CategoriesOfInfected
		{
			Normals,
			Animals,
			Boomers,
			Ghosts,
			Bosses,
			Flyers
		}

		public struct CreatureEntry
		{
			public string Wild;
			public string Tamed;
		}
	}
}
