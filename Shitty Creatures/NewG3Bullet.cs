using Engine;
using Engine.Graphics;

namespace Game
{
	public class NewG3Bullet : ShittyTexturesFlat
	{
		public const int Index = 548; // Índice único para la bala. Debe coincidir con el CSV.

		public NewG3Bullet() : base("Textures/Armas De Mierda/G3Bullet")
		{
			this.BlockIndex = Index;
		}
	}
}
