using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemAnimalInfectedSpawnEggBlockBehavior : SubsystemBlockBehavior
	{
		// Solo infectados de origen animal (excluye humanos, especiales y voladores)
		private static readonly string[] AnimalInfectedTemplates = new string[]
		{
			"InfectedBear",
			"InfectedWildboar",
			"PredatoryChameleon",
			"InfectedHyena",
			"InfectedWerewolf",
			"InfectedWolf",
			"InfectedSpider"
		};

		private SubsystemGameInfo m_subsystemGameInfo;
		private Random m_random = new Random();

		public override int[] HandledBlocks
		{
			get { return new int[] { AnimalInfectedSpawnEggBlock.Index }; }
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			try
			{
				string templateName = AnimalInfectedTemplates[m_random.Int(0, AnimalInfectedTemplates.Length - 1)];
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
				Log.Error($"Error spawning animal infected: {ex}");
				if (worldItem is Projectile projectile && projectile.Owner != null)
				{
					ComponentGui gui = projectile.Owner.Entity.FindComponent<ComponentGui>();
					if (gui != null)
						gui.DisplaySmallMessage("Error al spawnear infectado animal", Color.White, true, false);
				}
			}
			return true;
		}
	}
}
