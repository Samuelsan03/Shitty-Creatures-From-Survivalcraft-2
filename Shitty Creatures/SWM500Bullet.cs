using Engine;
using Engine.Graphics;

namespace Game
{
	public class SWM500Bullet : ShittyTexturesFlat
	{
		public const int Index = 550; // Índice único para la bala. Debe coincidir con el CSV.

		public SWM500Bullet() : base("Textures/Armas De Mierda/desertt")
		{
			this.BlockIndex = Index;
		}
	}
}
