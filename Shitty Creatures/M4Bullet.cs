using Engine;
using Engine.Graphics;

namespace Game
{
	public class M4Bullet : ShittyTexturesFlat
	{
		public const int Index = 545; // Índice único para la bala. Debe coincidir con el CSV.

		public M4Bullet() : base("Textures/Armas De Mierda/M16Bullet")
		{
			this.BlockIndex = Index;
		}
	}
}
