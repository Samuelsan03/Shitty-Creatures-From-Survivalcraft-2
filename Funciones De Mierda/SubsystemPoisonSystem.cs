using System;
using Engine;
using Game;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemPoisonSystem : SubsystemBlockBehavior  // Cambia esto
	{
		public override int[] HandledBlocks
		{
			get
			{
				return Array.Empty<int>();  // Este subsistema no maneja bloques específicos
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			// Sistema para manejar efectos de veneno globalmente
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			base.Save(valuesDictionary);
		}
	}
}
