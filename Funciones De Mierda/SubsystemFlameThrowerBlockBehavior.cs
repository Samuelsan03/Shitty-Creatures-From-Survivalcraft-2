using System;
using Engine;
using Engine.Audio;
using Game;
using TemplatesDatabase;
using WonderfulEra;

namespace Game
{
	public class SubsystemFlameThrowerBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get { return new int[] { BlocksManager.GetBlockIndex<FlameThrowerBlock>(false, false) }; }
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			// CORRECCIÓN: Hacer casting explícito del resultado del raycast
			object raycastResult = componentMiner.Raycast(ray, RaycastMode.Digging);

			if (raycastResult is TerrainRaycastResult)
			{
				TerrainRaycastResult terrainRaycastResult = (TerrainRaycastResult)raycastResult;
				int x = terrainRaycastResult.CellFace.X;
				int y = terrainRaycastResult.CellFace.Y;
				int z = terrainRaycastResult.CellFace.Z;

				// Obtener el valor del bloque
				int value = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
				int contents = Terrain.ExtractContents(value);

				// Verificar si es el bloque correcto
				if (contents == BlocksManager.GetBlockIndex<FlameThrowerBlock>(false, false))
				{
					int data = Terrain.ExtractData(value);

					// Alternar el estado del interruptor
					bool switchState = FlameThrowerBlock.GetSwitchState(data);
					switchState = !switchState;
					data = FlameThrowerBlock.SetSwitchState(data, switchState);

					// Actualizar el bloque
					int newValue = Terrain.ReplaceData(value, data);
					m_subsystemTerrain.ChangeCell(x, y, z, newValue);

					// CORRECCIÓN: Reproducir sonido usando SubsystemAudio
					if (m_subsystemAudio != null)
					{
						m_subsystemAudio.PlaySound("Audio/Lantern", 1f, 0f, new Vector3(x, y, z), 2f, true);
					}

					return true;
				}
			}

			return false;
		}

		public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
		{
			base.OnBlockAdded(value, oldValue, x, y, z);

			// Inicializar el bloque cuando se coloca
			if (m_subsystemItemsLauncher != null)
			{
				// Aquí puedes inicializar el lanzallamas si es necesario
			}
		}

		public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
		{
			base.OnBlockRemoved(value, newValue, x, y, z);

			// Limpiar recursos si es necesario
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemItemsLauncher = Project.FindSubsystem<SubsystemItemsLauncherBlockBehavior>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
		}

		// Campos
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemItemsLauncherBlockBehavior m_subsystemItemsLauncher;
		public SubsystemProjectiles m_subsystemProjectiles;
		public SubsystemAudio m_subsystemAudio;
		public Random m_random = new Random();
	}
}
