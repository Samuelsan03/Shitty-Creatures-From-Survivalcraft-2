using Engine;
using Engine.Graphics;

namespace Game
{
	public class UziBullet : ShittyTexturesFlat
	{
		public const int Index = 551; // Índice único para la bala. Debe coincidir con el CSV.

		public UziBullet() : base("Textures/Armas De Mierda/AK47Bullet")
		{
			this.BlockIndex = Index;
		}
	}
}
