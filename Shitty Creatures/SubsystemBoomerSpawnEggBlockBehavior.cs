using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemBoomerSpawnEggBlockBehavior : SubsystemBlockBehavior
	{
		// SOLO boomers (gordos explosivos)
		private static readonly string[] InfectedTemplates = new string[]
		{
			"Boomer1",
			"Boomer2",
			"Boomer3",
			"GhostBoomer1",
			"GhostBoomer2",
			"GhostBoomer3"
		};

		private SubsystemGameInfo m_subsystemGameInfo;
		private Random m_random = new Random();

		public override int[] HandledBlocks
		{
			get { return new int[] { BoomerSpawnEggBlock.Index }; }
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
				string templateName = InfectedTemplates[m_random.Int(0, InfectedTemplates.Length - 1)];
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
				Log.Error($"Error spawning boomer: {ex}");
				if (worldItem is Projectile projectile && projectile.Owner != null)
				{
					ComponentGui gui = projectile.Owner.Entity.FindComponent<ComponentGui>();
					if (gui != null)
						gui.DisplaySmallMessage("Error al spawnear boomer", Color.White, true, false);
				}
			}
			return true;
		}
	}
}