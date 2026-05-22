using Engine;
using Engine.Graphics;

namespace Game
{
	public class MinigunBullet : ShittyTexturesFlat
	{
		public const int Index = 547; // Índice único para la bala. Debe coincidir con el CSV.

		public MinigunBullet() : base("Textures/Armas De Mierda/Minigun Bullet")
		{
			this.BlockIndex = Index;
		}
	}
}
