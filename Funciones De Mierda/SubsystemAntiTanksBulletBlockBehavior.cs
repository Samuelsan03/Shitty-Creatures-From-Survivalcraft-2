using System;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemAntiTanksBulletBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { AntiTanksBulletBlock.Index };
			}
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			Console.WriteLine($"AntiTanksBulletBlockBehavior.OnHitAsProjectile llamado!");

			// LOG 1: Verifica si se está llamando
			if (worldItem == null)
			{
				Console.WriteLine("ERROR: worldItem es null!");
				return true;
			}

			Console.WriteLine($"Velocidad: {worldItem.Velocity.Length()}");

			// DAÑO DIRECTO SIMPLE - SIN VERIFICACIONES COMPLEJAS
			if (componentBody != null)
			{
				Console.WriteLine($"componentBody encontrado!");

				var health = componentBody.Entity.FindComponent<ComponentHealth>();
				if (health != null)
				{
					Console.WriteLine($"ComponentHealth encontrado! Aplicando daño...");
					// Daño MÁSICO para testing
					health.Injure(999f, null, false, "ANTI-TANK TEST DAMAGE");
					Console.WriteLine($"Daño aplicado: 999");
				}
				else
				{
					Console.WriteLine($"ERROR: ComponentHealth NO encontrado!");
				}
			}
			else
			{
				Console.WriteLine($"componentBody es null!");
			}

			// También daño por explosión
			if (cellFace != null)
			{
				Console.WriteLine($"Impacto en bloque en ({cellFace.Value.X}, {cellFace.Value.Y}, {cellFace.Value.Z})");
				if (m_subsystemExplosions != null)
				{
					int cellValue = m_subsystemTerrain.Terrain.GetCellValue(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z);
					m_subsystemExplosions.TryExplodeBlock(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z, cellValue);
					Console.WriteLine($"Explosión intentada");
				}
			}

			return true;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true);
			Console.WriteLine($"SubsystemAntiTanksBulletBlockBehavior CARGADO!");
		}

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemExplosions m_subsystemExplosions;
		private SubsystemAudio m_subsystemAudio;
	}
}
