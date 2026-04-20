using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Comportamiento para el tridente: al impactar un cuerpo vivo, invoca un rayo sobre la víctima y
	// además evita que el tridente desaparezca, creando un nuevo pickable en el suelo.
	public class SubsystemTridentBehavior : SubsystemBlockBehavior
	{
		private SubsystemSky m_subsystemSky;
		private SubsystemPickables m_subsystemPickables;

		// El tridente se identifica por su bloque original (TridentBlock)
		public override int[] HandledBlocks => new int[] { TridentBlock.Index };

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
		}

		/// <summary>
		/// Se llama cuando un proyectil (WorldItem) impacta.
		/// </summary>
		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			// Solo actuar si golpeamos un cuerpo (no un bloque)
			if (componentBody != null)
			{
				int contents = Terrain.ExtractContents(worldItem.Value);
				if (contents == TridentBlock.Index)
				{
					// Verificar si el cuerpo pertenece a una criatura viva
					ComponentHealth componentHealth = componentBody.Entity.FindComponent<ComponentHealth>();
					if (componentHealth == null || componentHealth.Health <= 0f)
					{
						// El cuerpo está muerto o no tiene salud → no hacer nada
						return false;
					}

					// Hacemos caer un rayo directamente sobre la víctima viva
					m_subsystemSky.MakeLightningStrike(componentBody.Position, false);

					// Evitar que el tridente desaparezca: eliminamos el original y creamos uno nuevo en el suelo
					if (worldItem is Pickable pickable)
					{
						pickable.ToRemove = true; // marcar para eliminar el original
					}

					// Crear un nuevo pickable con el mismo valor en la posición del impacto
					m_subsystemPickables.AddPickable(worldItem.Value, 1, componentBody.Position, null, null);
				}
			}
			return false;
		}
	}
}
