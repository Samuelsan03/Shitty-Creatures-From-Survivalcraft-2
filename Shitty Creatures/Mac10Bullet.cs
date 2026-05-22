using Engine;
using Engine.Graphics;

namespace Game
{
	public class Mac10Bullet : ShittyTexturesFlat
	{
		public const int Index = 546; // Índice único para la bala. Debe coincidir con el CSV.

		public Mac10Bullet() : base("Textures/Armas De Mierda/MAC10Bullet")
		{
			this.BlockIndex = Index;
		}
	}
}
