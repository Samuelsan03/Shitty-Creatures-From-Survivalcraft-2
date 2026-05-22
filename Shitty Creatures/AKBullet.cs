using Engine;
using Engine.Graphics;

namespace Game
{
	public class AKBullet : ShittyTexturesFlat
	{
		public const int Index = 542; // Índice único para la bala. Debe coincidir con el CSV.

		public AKBullet() : base("Textures/Armas De Mierda/AK47Bullet")
		{
			this.BlockIndex = Index;
		}
	}
}
