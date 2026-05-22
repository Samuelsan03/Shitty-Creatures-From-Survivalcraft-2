using Engine;
using Engine.Graphics;

namespace Game
{
	public class SniperBullet : ShittyTexturesFlat
	{
		public const int Index = 549; // Índice único para la bala. Debe coincidir con el CSV.

		public SniperBullet() : base("Textures/Armas De Mierda/sniper bala")
		{
			this.BlockIndex = Index;
		}
	}
}
