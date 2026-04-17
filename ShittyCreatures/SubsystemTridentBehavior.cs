using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Comportamiento para el tridente: al impactar un cuerpo, invoca un rayo sobre la víctima y
	// además evita que el tridente desaparezca, creando un nuevo pickable en el suelo.
	public class SubsystemTridentBehavior : SubsystemBlockBehavior
	{
		private SubsystemSky m_subsystemSky;
		private SubsystemPickables m_subsystemPickables;

		// El tridente se identifica por su índice de bloque (321 según TridentBlock)
		public override int[] HandledBlocks => new int[] { 321 };

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
		}

		/// <summary>
		/// Se llama cuando un proyectil (WorldItem) impacta.
		/// </summary>
		/// <param name="cellFace">Celda del bloque impactado (null si no fue un bloque).</param>
		/// <param name="componentBody">Cuerpo impactado (null si no fue un cuerpo).</param>
		/// <param name="worldItem">El proyectil que impactó.</param>
		/// <returns>false para permitir que otros comportamientos también procesen el evento.</returns>
		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			// Solo actuar si golpeamos un cuerpo (no un bloque)
			if (componentBody != null)
			{
				int contents = Terrain.ExtractContents(worldItem.Value);
				if (contents == TridentBlock.Index) // TridentBlock.Index
				{
					// Hacemos caer un rayo directamente sobre la víctima
					m_subsystemSky.MakeLightningStrike(componentBody.Position, false);

					// Evitar que el tridente desaparezca: eliminamos el original y creamos uno nuevo en el suelo
					// en la posición del cuerpo (para que caiga y pueda ser recogido)
					if (worldItem is Pickable pickable)
					{
						pickable.ToRemove = true; // marcar para eliminar el original
					}

					// Crear un nuevo pickable con el mismo valor en la posición del impacto
					// Se añade con velocidad cero para que caiga directamente al suelo
					m_subsystemPickables.AddPickable(worldItem.Value, 1, componentBody.Position, null, null);
				}
			}
			// Devolvemos false para no bloquear otros comportamientos
			return false;
		}
	}
}
