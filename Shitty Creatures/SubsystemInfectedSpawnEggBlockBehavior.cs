using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemInfectedSpawnEggBlockBehavior : SubsystemBlockBehavior
	{
		// Diccionario que enlaza el NOMBRE del enum con su lista de templates correspondiente
		private static readonly Dictionary<InfectedSpawnEggBlock.InfectedType, string[]> s_spawnTemplates = new Dictionary<InfectedSpawnEggBlock.InfectedType, string[]>
		{
			{
				InfectedSpawnEggBlock.InfectedType.Common, new string[]
				{
					"InfectedNormal1",
					"InfectedNormal2",
					"InfectedMuscle1",
					"InfectedMuscle2",
					"GhostNormal",
					"GhostFast",
					"InfectedFast1",
					"InfectedFast2",
					"PoisonousInfected1",
					"PoisonousInfected2",
					"PoisonousGhost",
					"InfectedFreezer",
					"HumanoidSkeleton",
					"Charger1",
					"Charger2",
					"GhostCharger"
				}
			},
			{
				InfectedSpawnEggBlock.InfectedType.Boomer, new string[]
				{
					"Boomer1",
					"Boomer2",
					"Boomer3",
					"GhostBoomer1",
					"GhostBoomer2",
					"GhostBoomer3"
				}
			},
			{
				InfectedSpawnEggBlock.InfectedType.Special, new string[]
				{
					"Tank1",
					"Tank2",
					"Tank3",
					"TankGhost1",
					"TankGhost2",
					"TankGhost3",
					"MachineGunInfected",
					"FrozenTank",
					"FrozenTankGhost"
				}
			},
			{
				InfectedSpawnEggBlock.InfectedType.Flying, new string[]
				{
					"InfectedFly1",
					"InfectedFly2",
					"InfectedFly3",
					"InfectedBird",
					"FlyingInfectedBoss"
				}
			},
			{
				InfectedSpawnEggBlock.InfectedType.Animal, new string[]
				{
					"InfectedBear",
					"InfectedWildboar",
					"PredatoryChameleon",
					"InfectedHyena",
					"InfectedWerewolf",
					"InfectedWolf",
					"InfectedSpider"
				}
			}
		};

		private static readonly Dictionary<InfectedSpawnEggBlock.InfectedType, string> s_errorKeys = new Dictionary<InfectedSpawnEggBlock.InfectedType, string>
		{
			{ InfectedSpawnEggBlock.InfectedType.Common, "SpawnErrorCommon" },
			{ InfectedSpawnEggBlock.InfectedType.Boomer, "SpawnErrorBoomer" },
			{ InfectedSpawnEggBlock.InfectedType.Special, "SpawnErrorSpecial" },
			{ InfectedSpawnEggBlock.InfectedType.Flying, "SpawnErrorFlying" },
			{ InfectedSpawnEggBlock.InfectedType.Animal, "SpawnErrorAnimal" }
		};

		private SubsystemGameInfo m_subsystemGameInfo;
		private Random m_random = new Random();

		public override int[] HandledBlocks
		{
			get { return new int[] { InfectedSpawnEggBlock.Index }; }
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			InfectedSpawnEggBlock.InfectedType type = InfectedSpawnEggBlock.GetInfectedType(worldItem.Value);

			// Uso estricto del nombre del enum como clave para buscar la lista
			if (s_spawnTemplates.TryGetValue(type, out string[] templates))
			{
				try
				{
					string templateName = templates[m_random.Int(0, templates.Length - 1)];
					Entity entity = DatabaseManager.CreateEntity(base.Project, templateName, true);
					ComponentBody body = entity.FindComponent<ComponentBody>(true);
					body.Position = worldItem.Position;
					body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, m_random.Float(0f, 6.2831855f));
					ComponentSpawn spawn = entity.FindComponent<ComponentSpawn>(true);
					spawn.SpawnDuration = 0.25f;
					base.Project.AddEntity(entity);
				}
				catch (Exception ex)
				{
					Log.Error($"Error spawning {type} infected: {ex}");
					ShowErrorMessage(worldItem, s_errorKeys[type]);
				}
			}

			return true;
		}

		private void ShowErrorMessage(WorldItem worldItem, string errorKey)
		{
			if (worldItem is Projectile projectile && projectile.Owner != null)
			{
				ComponentGui gui = projectile.Owner.Entity.FindComponent<ComponentGui>();
				if (gui != null)
				{
					string message = LanguageControl.Get("Blocks", "InfectedSpawnEgg", errorKey);
					gui.DisplaySmallMessage(message, Color.White, true, false);
				}
			}
		}
	}
}
