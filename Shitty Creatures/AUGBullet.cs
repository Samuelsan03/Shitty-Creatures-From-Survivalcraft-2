using Engine;
using Engine.Graphics;

namespace Game
{
	public class AUGBullet : ShittyTexturesFlat
	{
		public const int Index = 543; // Índice único para la bala. Debe coincidir con el CSV.

		public AUGBullet() : base("Textures/Armas De Mierda/M16Bullet")
		{
			this.BlockIndex = Index;
		}
	}
}
